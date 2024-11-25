using Microsoft.EntityFrameworkCore;
using MyMvcApp.Models; // Adjust this to match your namespace
using System;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure database context based on environment
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    // Check if we're in production (Railway)
    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrEmpty(databaseUrl))
    {
        // Use PostgreSQL in production
        options.UseNpgsql(databaseUrl);
    }
    else
    {
        // Use SQLite in development
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        options.UseSqlite(connectionString);
    }
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
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
