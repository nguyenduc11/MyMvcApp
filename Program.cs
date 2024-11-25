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

// Log all environment variables for debugging
Console.WriteLine("\nAll environment variables:");
foreach (System.Collections.DictionaryEntry env in Environment.GetEnvironmentVariables())
{
    var key = env.Key.ToString();
    var value = env.Value?.ToString();
    Console.WriteLine($"{key}: {(key.Contains("PASSWORD") ? "REDACTED" : value ?? "null")}");
}

if (isProduction)
{
    var connectionBuilder = new NpgsqlConnectionStringBuilder();
    bool connectionConfigured = false;

    // First try DATABASE_URL
    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    Console.WriteLine($"\nTrying DATABASE_URL connection method:");
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
        Console.WriteLine("\nTrying individual PostgreSQL variables:");
        
        var pgHost = Environment.GetEnvironmentVariable("PGHOST");
        var pgPort = Environment.GetEnvironmentVariable("PGPORT");
        var pgDatabase = Environment.GetEnvironmentVariable("PGDATABASE") ?? 
                        Environment.GetEnvironmentVariable("POSTGRES_DB");
        var pgUser = Environment.GetEnvironmentVariable("PGUSER") ?? 
                    Environment.GetEnvironmentVariable("POSTGRES_USER");
        var pgPassword = Environment.GetEnvironmentVariable("PGPASSWORD") ?? 
                        Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");

        Console.WriteLine($"PGHOST: {pgHost ?? "null"}");
        Console.WriteLine($"PGPORT: {pgPort ?? "null"}");
        Console.WriteLine($"Database: {pgDatabase ?? "null"}");
        Console.WriteLine($"Username: {pgUser ?? "null"}");
        Console.WriteLine($"Password present: {!string.IsNullOrEmpty(pgPassword)}");

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
        else
        {
            Console.WriteLine("Failed to configure using individual variables");
            if (string.IsNullOrEmpty(pgHost)) Console.WriteLine("- PGHOST is missing");
            if (string.IsNullOrEmpty(pgPassword)) Console.WriteLine("- PGPASSWORD is missing");
            // Fallback logic
            Console.WriteLine("Attempting to use a default password for testing purposes.");
            pgPassword = "default_password"; // Replace with a secure method in production
            if (!string.IsNullOrEmpty(pgHost) && !string.IsNullOrEmpty(pgPassword))
            {
                connectionBuilder.Host = pgHost;
                connectionBuilder.Port = int.TryParse(pgPort, out int port) ? port : 5432;
                connectionBuilder.Database = pgDatabase ?? "railway";
                connectionBuilder.Username = pgUser ?? "postgres";
                connectionBuilder.Password = pgPassword;
                connectionConfigured = true;

                Console.WriteLine("Successfully configured using fallback password");
            }
        }
    }

    if (!connectionConfigured)
    {
        throw new Exception("Failed to configure PostgreSQL connection. Neither DATABASE_URL nor individual environment variables are properly set. Check the logs above for details.");
    }

    // Add common settings
    connectionBuilder.SslMode = SslMode.Require;
    connectionBuilder.TrustServerCertificate = true;
    connectionBuilder.Timeout = 30;

    var connectionString = connectionBuilder.ToString();

    // Log connection info (without sensitive data)
    Console.WriteLine("\nFinal database connection info:");
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
