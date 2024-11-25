using Microsoft.EntityFrameworkCore;
using MyMvcApp.Areas.Blog.Models;

namespace MyMvcApp.Models
{
    public class BlogDbContext : DbContext
    {
        public BlogDbContext(DbContextOptions<BlogDbContext> options)
            : base(options)
        {
        }

        public DbSet<BlogPost> BlogPosts { get; set; } = null!;
    }
}
