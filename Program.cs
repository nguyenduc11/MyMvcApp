using Microsoft.EntityFrameworkCore;
using MyMvcApp.Models;
using Npgsql;
using System;
using DotEnv.Net;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure database contexts
void ConfigureDbContext<T>(IServiceCollection services) where T : DbContext
{
    services.AddDbContext<T>(options =>
    {
        var isProduction = builder.Environment.IsProduction();
        Console.WriteLine($"Environment IsProduction: {isProduction}");
        Console.WriteLine($"ASPNETCORE_ENVIRONMENT: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}");
        Console.WriteLine($"DOTNET_RUNNING_IN_CONTAINER: {Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")}");
        
        if (isProduction)
        {
            // Log environment variables for debugging
            Console.WriteLine("Database Environment Variables:");
            Console.WriteLine($"DATABASE_URL: {Environment.GetEnvironmentVariable("DATABASE_URL")}");
            Console.WriteLine($"PGHOST: {Environment.GetEnvironmentVariable("PGHOST")}");
            Console.WriteLine($"PGPORT: {Environment.GetEnvironmentVariable("PGPORT")}");
            Console.WriteLine($"PGDATABASE: {Environment.GetEnvironmentVariable("PGDATABASE")}");
            Console.WriteLine($"PGUSER: {Environment.GetEnvironmentVariable("PGUSER")}");
            Console.WriteLine($"PGPASSWORD length: {(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PGPASSWORD")) ? "0" : Environment.GetEnvironmentVariable("PGPASSWORD").Length)}");

            // Load environment variables from .env file in development
            if (!isProduction)
            {
                DotEnv.Load();
                DotEnv.AutoTrim = true;
                DotEnv.AutoParse = true;
            }

            // Log environment type
            Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");

            // Build connection string using environment variables
            var connectionBuilder = new NpgsqlConnectionStringBuilder();

            // Try to use DATABASE_URL first
            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
            if (!string.IsNullOrEmpty(databaseUrl))
            {
                try
                {
                    var uri = new Uri(databaseUrl);
                    var userInfo = uri.UserInfo.Split(':');
                    connectionBuilder.Host = uri.Host;
                    connectionBuilder.Port = uri.Port > 0 ? uri.Port : 5432;
                    connectionBuilder.Database = uri.AbsolutePath.TrimStart('/');
                    connectionBuilder.Username = userInfo[0];
                    connectionBuilder.Password = userInfo[1];
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing DATABASE_URL: {ex.Message}");
                }
            }

            // If DATABASE_URL parsing failed or wasn't available, use individual environment variables
            if (string.IsNullOrEmpty(connectionBuilder.Password))
            {
                connectionBuilder.Host = Environment.GetEnvironmentVariable("PGHOST") ?? "localhost";
                connectionBuilder.Port = int.TryParse(Environment.GetEnvironmentVariable("PGPORT"), out int port) ? port : 5432;
                connectionBuilder.Database = Environment.GetEnvironmentVariable("PGDATABASE") ?? "railway";
                connectionBuilder.Username = Environment.GetEnvironmentVariable("PGUSER") ?? "postgres";
                connectionBuilder.Password = Environment.GetEnvironmentVariable("PGPASSWORD");
            }

            // Add common settings
            connectionBuilder.SslMode = isProduction ? SslMode.Require : SslMode.Prefer;
            connectionBuilder.TrustServerCertificate = true;
            connectionBuilder.Timeout = 30;

            var connectionString = connectionBuilder.ToString();

            // Log the connection string (without password)
            var logBuilder = new NpgsqlConnectionStringBuilder(connectionString) { Password = "REDACTED" };
            Console.WriteLine($"Connection string (redacted): {logBuilder}");

            // Configure DbContext
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
                npgsqlOptions.MigrationsAssembly(typeof(Program).Assembly.FullName);
            });
        }
        else
        {
            // Use SQLite in development
            var dbName = typeof(T).Name.Replace("Context", "").ToLower();
            var connectionString = $"Data Source=./{dbName}.db";
            Console.WriteLine($"Configuring {typeof(T).Name} with SQLite. Connection string: {connectionString}");
            
            options.UseSqlite(connectionString, sqliteOptions =>
            {
                sqliteOptions.MigrationsAssembly(typeof(Program).Assembly.FullName);
            });
        }
    });
}

// Configure both contexts
ConfigureDbContext<ApplicationDbContext>(builder.Services);
ConfigureDbContext<BlogDbContext>(builder.Services);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// Apply migrations and ensure database is created
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    async Task MigrateDbContextAsync<T>(string contextName) where T : DbContext
    {
        try
        {
            var context = services.GetRequiredService<T>();
            var database = context.Database;

            // Log database information
            logger.LogInformation(
                "Database info for {Context}: Provider={Provider}, ConnectionString={ConnectionString}",
                contextName,
                database.ProviderName,
                database.GetConnectionString()
            );

            // Create database if it doesn't exist
            var created = await database.EnsureCreatedAsync();
            if (created)
            {
                logger.LogInformation("Database created for {Context}", contextName);
            }

            // Get pending migrations
            var pendingMigrations = await database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                logger.LogInformation(
                    "Applying {Count} pending migrations for {Context}: {Migrations}",
                    pendingMigrations.Count(),
                    contextName,
                    string.Join(", ", pendingMigrations)
                );

                await database.MigrateAsync();
                logger.LogInformation("Migrations applied successfully for {Context}", contextName);
            }
            else
            {
                logger.LogInformation("No pending migrations for {Context}", contextName);
            }

            // Verify connection
            if (await database.CanConnectAsync())
            {
                logger.LogInformation("Successfully connected to database for {Context}", contextName);
                
                // Log table names
                var tableNames = await context.Database.SqlQuery<string>($@"
                    SELECT table_name 
                    FROM information_schema.tables 
                    WHERE table_schema = 'public'
                ").ToListAsync();
                
                logger.LogInformation(
                    "Tables in database for {Context}: {Tables}",
                    contextName,
                    string.Join(", ", tableNames)
                );
            }
            else
            {
                throw new Exception($"Cannot connect to database for {contextName}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "An error occurred while setting up the database for {Context}: {Message}",
                contextName,
                ex.Message
            );
            throw;
        }
    }

    // Migrate both contexts
    await MigrateDbContextAsync<ApplicationDbContext>("ApplicationDbContext");
    await MigrateDbContextAsync<BlogDbContext>("BlogDbContext");
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
