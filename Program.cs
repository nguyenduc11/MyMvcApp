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
    var connectionBuilder = new NpgsqlConnectionStringBuilder();
    bool connectionConfigured = false;

    // First try DATABASE_URL
    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    Console.WriteLine($"DATABASE_URL present: {!string.IsNullOrEmpty(databaseUrl)}");

    if (!string.IsNullOrEmpty(databaseUrl))
    {
        try
        {
            var uri = new Uri(databaseUrl);
            var userInfo = uri.UserInfo.Split(':');

            connectionBuilder.Host = uri.Host;
            connectionBuilder.Port = uri.Port;
            connectionBuilder.Database = uri.AbsolutePath.TrimStart('/');
            connectionBuilder.Username = userInfo[0];
            connectionBuilder.Password = userInfo[1];
            connectionConfigured = true;

            Console.WriteLine("Successfully parsed DATABASE_URL");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing DATABASE_URL: {ex.Message}");
        }
    }

    // If DATABASE_URL failed, try individual variables
    if (!connectionConfigured)
    {
        Console.WriteLine("Attempting to use individual PostgreSQL environment variables");
        
        var pgHost = Environment.GetEnvironmentVariable("PGHOST");
        var pgPort = Environment.GetEnvironmentVariable("PGPORT");
        var pgDatabase = Environment.GetEnvironmentVariable("PGDATABASE") ?? Environment.GetEnvironmentVariable("POSTGRES_DB");
        var pgUser = Environment.GetEnvironmentVariable("PGUSER") ?? Environment.GetEnvironmentVariable("POSTGRES_USER");
        var pgPassword = Environment.GetEnvironmentVariable("PGPASSWORD") ?? Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");

        Console.WriteLine($"PGHOST: {pgHost}");
        Console.WriteLine($"PGPORT: {pgPort}");
        Console.WriteLine($"Database: {pgDatabase}");
        Console.WriteLine($"Username: {pgUser}");
        Console.WriteLine($"Password length: {(string.IsNullOrEmpty(pgPassword) ? 0 : pgPassword.Length)}");

        if (!string.IsNullOrEmpty(pgHost) && !string.IsNullOrEmpty(pgPassword))
        {
            connectionBuilder.Host = pgHost;
            connectionBuilder.Port = int.TryParse(pgPort, out int port) ? port : 5432;
            connectionBuilder.Database = pgDatabase ?? "railway";
            connectionBuilder.Username = pgUser ?? "postgres";
            connectionBuilder.Password = pgPassword;
            connectionConfigured = true;

            Console.WriteLine("Successfully configured using individual variables");
        }
    }

    if (!connectionConfigured)
    {
        throw new Exception("Failed to configure PostgreSQL connection. Neither DATABASE_URL nor individual environment variables are properly set.");
    }

    // Add common settings
    connectionBuilder.SslMode = SslMode.Require;
    connectionBuilder.TrustServerCertificate = true;
    connectionBuilder.Timeout = 30;

    var connectionString = connectionBuilder.ToString();

    // Log connection info (without sensitive data)
    Console.WriteLine($"Final database connection info:");
    Console.WriteLine($"Host: {connectionBuilder.Host}");
    Console.WriteLine($"Port: {connectionBuilder.Port}");
    Console.WriteLine($"Database: {connectionBuilder.Database}");
    Console.WriteLine($"Username: {connectionBuilder.Username}");
    Console.WriteLine($"SSL Mode: {connectionBuilder.SslMode}");

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
