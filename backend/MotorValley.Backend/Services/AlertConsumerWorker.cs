using System.Text.Json;
using Confluent.Kafka;
using MotorValley.Backend.Models;

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
                        var ingestion = scope.ServiceProvider.GetRequiredService<IAlertIngestionService>();

                        // Persist idempotently and fan out to the dashboard — the same
                        // pipeline the HTTP ingest endpoint uses in the no-Kafka path.
                        await ingestion.IngestAsync(dto, stoppingToken);

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
                        await Task.Delay(TimeSpan.FromSeconds(2d), stoppingToken);
                    }
                }
            }
            catch (ConsumeException ex)
            {
                Console.WriteLine($"Kafka consume error: {ex.Error.Reason}, retrying in 5s...");
                await Task.Delay(TimeSpan.FromSeconds(5d), stoppingToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Kafka consumer error: {ex.Message}, retrying in 5s...");
                await Task.Delay(TimeSpan.FromSeconds(5d), stoppingToken);
            }
        }
    }
}
