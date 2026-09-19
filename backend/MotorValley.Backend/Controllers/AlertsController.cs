using Microsoft.AspNetCore.Mvc;
using MotorValley.Backend.Data;
using MotorValley.Backend.Services;

namespace MotorValley.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly IAlertRepository _repo;
    private readonly ICacheService _cache;

    public AlertsController(IAlertRepository repo, ICacheService cache)
    {
        _repo = repo;
        _cache = cache;
    }

    /// <summary>
    /// Top N failing machines by alert count. Includes alert count per machine.
    /// </summary>
    [HttpGet("top-failing")]
    public async Task<IActionResult> GetTopFailingMachines([FromQuery] int limit = 5, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 20);
        var alerts = await _repo.GetTopFailingWithCountsAsync(limit, ct);
        return Ok(alerts);
    }

    /// <summary>
    /// Latest status - served from Redis cache when available.
    /// </summary>
    [HttpGet("latest-status")]
    public async Task<IActionResult> GetLatestStatus(CancellationToken ct)
    {
        const string cacheKey = "motorvalley:latest-status";
        var cached = await _cache.GetAsync<MotorValley.Backend.Models.LatestStatusDto>(cacheKey, ct);
        if (cached != null)
            return Ok(cached);

        var status = await _repo.GetLatestStatusAsync(ct);
        if (status != null)
            await _cache.SetAsync(cacheKey, status, TimeSpan.FromMinutes(5d), ct);

        return Ok(status);
    }
}
