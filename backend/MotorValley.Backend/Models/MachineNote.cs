namespace MotorValley.Backend.Models;

public class MachineNote
{
    public int Id { get; set; }
    public required string MachineId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
