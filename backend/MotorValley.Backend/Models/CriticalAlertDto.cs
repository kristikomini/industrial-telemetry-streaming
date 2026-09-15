using System.Text.Json.Serialization;

namespace MotorValley.Backend.Models;

/// <summary>
/// Wire shape of a critical alert as it arrives from the processor — over Kafka
/// (<see cref="Services.AlertConsumerWorker"/>) or over HTTP
/// (<see cref="Controllers.IngestController"/>) depending on the configured transport.
/// The processor is Python and emits snake_case keys, so the property names are pinned
/// with <see cref="JsonPropertyNameAttribute"/> rather than left to the serializer's
/// default casing. The timestamp is a string because it crosses that language boundary
/// (Python emits ISO-8601).
/// </summary>
public class CriticalAlertDto
{
    [JsonPropertyName("machine_id")]
    public string MachineId { get; set; } = "";

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("consecutive_count")]
    public int ConsecutiveCount { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";
}
