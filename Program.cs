using Microsoft.EntityFrameworkCore;
using MyMvcApp.Models;
using Npgsql;
using System;

var builder = WebApplication.CreateBuilder(args);
var isProduction = builder.Environment.IsProduction();

// Add services to the container.
builder.Services.AddControllersWithViews();

// Log environment information
Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine($"ASPNETCORE_ENVIRONMENT: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}");

// Configure database based on environment
if (isProduction)
{
    try
    {
        // Use DATABASE_URL for production (Railway)
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        Console.WriteLine($"Database URL exists: {!string.IsNullOrEmpty(databaseUrl)}");

        if (string.IsNullOrEmpty(databaseUrl))
        {
            throw new Exception("Production environment requires DATABASE_URL to be set");
        }

        // Parse and construct connection string from DATABASE_URL
        var databaseUri = new Uri(databaseUrl);
        var userInfo = databaseUri.UserInfo.Split(':');
        var connBuilder = new NpgsqlConnectionStringBuilder
        {
            Host = databaseUri.Host,
            Port = databaseUri.Port,
            Username = userInfo[0],
            Password = userInfo[1],
            Database = databaseUri.AbsolutePath.TrimStart('/'),
            SslMode = SslMode.Require
        };

        var connectionString = connBuilder.ToString();
        Console.WriteLine("PostgreSQL Connection string built successfully");

        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
            }));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error configuring database: {ex.Message}");
        throw;
    }
}
else
{
    // Use SQLite for development
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    Console.WriteLine($"Development connection string exists: {!string.IsNullOrEmpty(connectionString)}");
    
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(connectionString));
}

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