using Microsoft.AspNetCore.SignalR;
using MotorValley.Backend.Data;
using MotorValley.Backend.Hubs;
using MotorValley.Backend.Models;

namespace MotorValley.Backend.Services;

public interface IAlertIngestionService
{
    /// <summary>
    /// Persists an alert idempotently and, only when it is genuinely new, fans it out to
    /// the dashboard over SignalR and refreshes the latest-status cache. Returns
    /// <c>true</c> when a new row was written, <c>false</c> on a redelivered/duplicate
    /// alert (so callers can decide whether to acknowledge it upstream).
    /// </summary>
    Task<bool> IngestAsync(CriticalAlertDto dto, CancellationToken ct = default);
}

/// <summary>
/// The single place the persist → fan-out → cache pipeline lives, shared by the Kafka
/// consumer and the HTTP ingest endpoint so both transports behave identically. Keeping
/// it transport-agnostic is what lets the stack swap Kafka for a plain HTTP POST in the
/// no-Docker path without changing the alerting semantics.
/// </summary>
public class AlertIngestionService : IAlertIngestionService
{
    private readonly IAlertRepository _repo;
    private readonly IHubContext<AlertHub> _hub;
    private readonly ICacheService _cache;

    public AlertIngestionService(IAlertRepository repo, IHubContext<AlertHub> hub, ICacheService cache)
    {
        _repo = repo;
        _hub = hub;
        _cache = cache;
    }

    public async Task<bool> IngestAsync(CriticalAlertDto dto, CancellationToken ct = default)
    {
        var alert = new CriticalAlert
        {
            MachineId = dto.MachineId,
            Temperature = dto.Temperature,
            ConsecutiveCount = dto.ConsecutiveCount,
            Message = dto.Message,
            Timestamp = DateTime.TryParse(dto.Timestamp, out var ts) ? ts.ToUniversalTime() : DateTime.UtcNow,
        };

        // Idempotent write: on a redelivered alert this returns false and we skip the
        // fan-out, so the dashboard is not notified twice.
        var inserted = await _repo.AddAsync(alert, ct);
        if (inserted)
        {
            await _hub.Clients.All.SendAsync("ReceiveAlert", alert, ct);

            var latest = new LatestStatusDto(alert.MachineId, alert.Temperature, alert.Message, alert.Timestamp);
            await _cache.SetAsync("motorvalley:latest-status", latest, TimeSpan.FromMinutes(5), ct);
        }

        return inserted;
    }
}
