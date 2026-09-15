using Microsoft.AspNetCore.Mvc;
using MotorValley.Backend.Models;
using MotorValley.Backend.Services;

namespace MotorValley.Backend.Controllers;

/// <summary>
/// HTTP ingress for critical alerts, used when the stack runs without Kafka (the
/// no-Docker path). The processor POSTs each alert here instead of publishing it to the
/// <c>critical-alerts</c> topic; the handler runs the identical persist → fan-out → cache
/// pipeline as the Kafka consumer via <see cref="IAlertIngestionService"/>.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class IngestController : ControllerBase
{
    private readonly IAlertIngestionService _ingestion;

    public IngestController(IAlertIngestionService ingestion) => _ingestion = ingestion;

    [HttpPost("alert")]
    public async Task<IActionResult> IngestAlert([FromBody] CriticalAlertDto dto, CancellationToken ct)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.MachineId))
            return BadRequest("machine_id is required");

        var inserted = await _ingestion.IngestAsync(dto, ct);
        // 201 for a new alert, 200 for an idempotent no-op — both are success from the
        // caller's perspective (the alert is durably accounted for either way).
        return inserted ? StatusCode(StatusCodes.Status201Created) : Ok();
    }
}
