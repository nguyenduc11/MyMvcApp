using System.ComponentModel.DataAnnotations;

namespace MyMvcApp.Models
{
    public class TodoItem
    {
        // Primary key for the to-do item
        public int Id { get; set; }

        // Task field with data annotations for validation
        [Required]
        [MaxLength(200)]
        public required string Task { get; set; }

        // Description field with data annotations for validation
        [Required]
        [MaxLength(1000)]
        public required string Description { get; set; }

        // Optional: Status to indicate if the task is completed
        public bool IsCompleted { get; set; } = false;

        // Optional: Timestamp for when the task was created
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
