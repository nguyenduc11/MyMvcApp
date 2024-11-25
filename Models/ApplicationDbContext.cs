using Microsoft.EntityFrameworkCore;

namespace MyMvcApp.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSet for TodoItem
        public DbSet<TodoItem> TodoItems { get; set; }

        // Optional: You can override OnModelCreating to configure the model further if needed
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Optional: Seed initial data for TodoItems
            modelBuilder.Entity<TodoItem>().HasData(
                new TodoItem { Id = 1, Task = "Sample Task 1", Description = "This is a sample description.", IsCompleted = false, CreatedAt = DateTime.UtcNow },
                new TodoItem { Id = 2, Task = "Sample Task 2", Description = "This is another sample description.", IsCompleted = true, CreatedAt = DateTime.UtcNow }
            );
        }
    }
}
