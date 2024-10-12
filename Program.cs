using Microsoft.EntityFrameworkCore;
using MyMvcApp.Models; // Adjust this to match your namespace
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure SQLite database context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    // Retrieve the connection string from configuration
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    // Use the connection string for SQLite
    options.UseSqlite(connectionString);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
