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

if (isProduction)
{
    // Log all environment variables for debugging
    foreach (var env in Environment.GetEnvironmentVariables().Keys)
    {
        if (env.ToString().StartsWith("PG") || env.ToString().Contains("DATABASE"))
        {
            Console.WriteLine($"{env}: {(env.ToString().Contains("PASSWORD") ? "REDACTED" : Environment.GetEnvironmentVariable(env.ToString()))}");
        }
    }

    var connectionBuilder = new NpgsqlConnectionStringBuilder();
    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    
    if (!string.IsNullOrEmpty(databaseUrl))
    {
        try
        {
            // Parse Railway's DATABASE_URL
            var uri = new Uri(databaseUrl);
            var userInfo = uri.UserInfo.Split(':');
            
            connectionBuilder.Host = uri.Host;
            connectionBuilder.Port = uri.Port;
            connectionBuilder.Database = uri.AbsolutePath.TrimStart('/');
            connectionBuilder.Username = userInfo[0];
            connectionBuilder.Password = userInfo[1];
            connectionBuilder.SslMode = SslMode.Require;
            connectionBuilder.TrustServerCertificate = true;
            connectionBuilder.Timeout = 30;
            
            Console.WriteLine($"Configured database connection using DATABASE_URL:");
            Console.WriteLine($"Host: {connectionBuilder.Host}");
            Console.WriteLine($"Port: {connectionBuilder.Port}");
            Console.WriteLine($"Database: {connectionBuilder.Database}");
            Console.WriteLine($"Username: {connectionBuilder.Username}");
            Console.WriteLine("Password: REDACTED");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing DATABASE_URL: {ex.Message}");
            throw; // Rethrow to prevent silent failures
        }
    }
    else
    {
        throw new Exception("DATABASE_URL environment variable is not set");
    }

    var connectionString = connectionBuilder.ToString();

    // Configure DbContext
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null);
        });
    });
}
else
{
    // Development configuration
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        options.UseSqlite(connectionString);
    });
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
