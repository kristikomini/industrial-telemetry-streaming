using System.ComponentModel.DataAnnotations;

namespace MotorValley.Backend.Models
{
    public class MachineNoteDto
    {
        [Required]
        [MaxLength(2000)]
        public string Text { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Author { get; set; } = string.Empty;
    }
}
