using System.Text.Json;
using Confluent.Kafka;
using MotorValley.Backend.Data;
using MotorValley.Backend.Hubs;
using MotorValley.Backend.Models;
using MotorValley.Backend.Services;
using Microsoft.AspNetCore.SignalR;

namespace MotorValley.Backend.Services;

public class AlertConsumerWorker : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly IServiceProvider _services;

    public AlertConsumerWorker(IConfiguration config, IServiceProvider services)
    {
        _config = config;
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrap = _config["Kafka:BootstrapServers"] ?? "localhost:9093";
        var topic = _config["Kafka:AlertsTopic"] ?? "critical-alerts";
        var groupId = _config["Kafka:GroupId"] ?? "alert-consumer-group";

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = bootstrap,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            // At-least-once: we commit the offset ourselves only after the alert is
            // durably persisted, rather than letting the client auto-commit ahead of
            // the database write.
            EnableAutoCommit = false,
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
                consumer.Subscribe(topic);

                while (!stoppingToken.IsCancellationRequested)
                {
                    var result = consumer.Consume(stoppingToken);

                    CriticalAlertDto? dto;
                    try
                    {
                        dto = JsonSerializer.Deserialize<CriticalAlertDto>(result.Message.Value);
                    }
                    catch (JsonException ex)
                    {
                        // Malformed payload — a poison message. Commit past it so it does
                        // not block the partition forever; there is nothing to retry.
                        Console.WriteLine($"Skipping malformed alert at offset {result.Offset}: {ex.Message}");
                        consumer.Commit(result);
                        continue;
                    }

                    if (dto == null)
                    {
                        consumer.Commit(result);
                        continue;
                    }

                    try
                    {
                        using var scope = _services.CreateScope();
                        var repo = scope.ServiceProvider.GetRequiredService<IAlertRepository>();

                        var alert = new CriticalAlert
                        {
                            MachineId = dto.MachineId,
                            Temperature = dto.Temperature,
                            ConsecutiveCount = dto.ConsecutiveCount,
                            Message = dto.Message,
                            Timestamp = DateTime.TryParse(dto.Timestamp, out var ts) ? ts : DateTime.UtcNow
                        };

                        // Idempotent write: on a redelivered message this returns false and
                        // we skip the fan-out, so the dashboard is not notified twice.
                        var inserted = await repo.AddAsync(alert, stoppingToken);
                        if (inserted)
                        {
                            var hub = scope.ServiceProvider.GetRequiredService<IHubContext<AlertHub>>();
                            await hub.Clients.All.SendAsync("ReceiveAlert", alert, stoppingToken);

                            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
                            var latest = new MotorValley.Backend.Models.LatestStatusDto(alert.MachineId, alert.Temperature, alert.Message, alert.Timestamp);
                            await cache.SetAsync("motorvalley:latest-status", latest, TimeSpan.FromMinutes(5), stoppingToken);
                        }

                        // Commit only now that the alert is durably persisted.
                        consumer.Commit(result);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        // Transient failure (e.g. the database is momentarily unavailable).
                        // Rewind to this offset and retry rather than committing past it, so
                        // the alert is never silently dropped — at-least-once, in order.
                        Console.WriteLine($"Processing failed at offset {result.Offset}, rewinding to retry: {ex.Message}");
                        consumer.Seek(result.TopicPartitionOffset);
                        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    }
                }
            }
            catch (ConsumeException ex)
            {
                Console.WriteLine($"Kafka consume error: {ex.Error.Reason}, retrying in 5s...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Kafka consumer error: {ex.Message}, retrying in 5s...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private class CriticalAlertDto
    {
        public string MachineId { get; set; } = "";
        public double Temperature { get; set; }
        public int ConsecutiveCount { get; set; }
        public string Message { get; set; } = "";
        public string Timestamp { get; set; } = "";
    }
}
