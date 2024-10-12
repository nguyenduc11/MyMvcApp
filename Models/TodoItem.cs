using System.ComponentModel.DataAnnotations;

namespace MyMvcApp.Models
{
    public class TodoItem
    {
        // Primary key for the to-do item
        public int Id { get; set; }

        // Task field with data annotations for validation
        [Required(ErrorMessage = "Task is required.")]
        [StringLength(100, ErrorMessage = "Task cannot be longer than 100 characters.")]
        public string Task { get; set; }

        // Description field with data annotations for validation
        [Required(ErrorMessage = "Description is required.")]
        [StringLength(500, ErrorMessage = "Description cannot be longer than 500 characters.")]
        public string Description { get; set; }

        // Optional: Status to indicate if the task is completed
        public bool IsCompleted { get; set; } = false;

        // Optional: Timestamp for when the task was created
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
