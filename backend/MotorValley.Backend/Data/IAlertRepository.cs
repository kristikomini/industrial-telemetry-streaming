using MotorValley.Backend.Models;

namespace MotorValley.Backend.Data;

public interface IAlertRepository
{
    /// <summary>Idempotent insert; returns true when a new row was written, false on a duplicate.</summary>
    Task<bool> AddAsync(CriticalAlert alert, CancellationToken ct = default);
    Task<IEnumerable<CriticalAlert>> GetTopFailingMachinesAsync(int limit = 5, CancellationToken ct = default);
    Task<IEnumerable<TopFailingMachineDto>> GetTopFailingWithCountsAsync(int limit = 5, CancellationToken ct = default);
    Task<LatestStatusDto?> GetLatestStatusAsync(CancellationToken ct = default);
}
