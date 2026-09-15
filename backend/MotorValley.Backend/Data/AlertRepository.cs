using Microsoft.EntityFrameworkCore;
using MotorValley.Backend.Models;

namespace MotorValley.Backend.Data;

public class AlertRepository : IAlertRepository
{
    private readonly AppDbContext _db;

    public AlertRepository(AppDbContext db) => _db = db;

    /// <summary>
    /// Idempotent insert keyed by (MachineId, Timestamp). Returns <c>true</c> when a new
    /// row was written, <c>false</c> when the alert had already been persisted — which is
    /// what makes reprocessing a redelivered Kafka message safe (at-least-once delivery
    /// without duplicate rows). The unique index on the table is the hard backstop.
    /// </summary>
    public async Task<bool> AddAsync(CriticalAlert alert, CancellationToken ct = default)
    {
        var exists = await _db.CriticalAlerts
            .AnyAsync(a => a.MachineId == alert.MachineId && a.Timestamp == alert.Timestamp, ct);
        if (exists) return false;

        _db.CriticalAlerts.Add(alert);
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Lost a race to another writer between the check and the insert; the unique
            // index rejected the duplicate. The row exists now, so treat it as processed.
            _db.Entry(alert).State = EntityState.Detached;
            return false;
        }
    }

    /// <summary>
    /// Top N machines by alert count, each represented by its most recent alert.
    /// Single query: the table is joined to a grouped subquery on (MachineId, latest
    /// Timestamp), which the unique index guarantees resolves to exactly one row per
    /// machine. Replaces the previous 1 + N query loop.
    /// </summary>
    public async Task<IEnumerable<CriticalAlert>> GetTopFailingMachinesAsync(int limit = 5, CancellationToken ct = default)
    {
        var grouped = _db.CriticalAlerts
            .GroupBy(a => a.MachineId)
            .Select(g => new { MachineId = g.Key, Count = g.Count(), LastTs = g.Max(x => x.Timestamp) });

        var query =
            from a in _db.CriticalAlerts
            join g in grouped
                on new { a.MachineId, Ts = a.Timestamp } equals new { g.MachineId, Ts = g.LastTs }
            orderby g.Count descending
            select a;

        return await query.Take(limit).ToListAsync(ct);
    }

    public async Task<LatestStatusDto?> GetLatestStatusAsync(CancellationToken ct = default)
    {
        var row = await _db.CriticalAlerts
            .OrderByDescending(a => a.Timestamp)
            .Select(a => new LatestStatusDto(a.MachineId, a.Temperature, a.Message, a.Timestamp))
            .FirstOrDefaultAsync(ct);
        return row;
    }

    /// <summary>
    /// Top N failing machines with their alert counts and latest reading, in one query
    /// (see <see cref="GetTopFailingMachinesAsync"/> for the join strategy).
    /// </summary>
    public async Task<IEnumerable<TopFailingMachineDto>> GetTopFailingWithCountsAsync(int limit = 5, CancellationToken ct = default)
    {
        var grouped = _db.CriticalAlerts
            .GroupBy(a => a.MachineId)
            .Select(g => new { MachineId = g.Key, Count = g.Count(), LastTs = g.Max(x => x.Timestamp) });

        var query =
            from a in _db.CriticalAlerts
            join g in grouped
                on new { a.MachineId, Ts = a.Timestamp } equals new { g.MachineId, Ts = g.LastTs }
            orderby g.Count descending
            select new TopFailingMachineDto(
                a.Id, a.MachineId, a.Temperature, a.ConsecutiveCount, a.Message, a.Timestamp, g.Count);

        return await query.Take(limit).ToListAsync(ct);
    }
}
