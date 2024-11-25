using Microsoft.EntityFrameworkCore;
using MyMvcApp.Models;
using Npgsql;
using System;

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
        
        if (isProduction)
        {
            // Log all available environment variables
            Console.WriteLine("Available Environment Variables:");
            foreach (var envVar in Environment.GetEnvironmentVariables().Keys)
            {
                Console.WriteLine($"  {envVar}: {Environment.GetEnvironmentVariable(envVar?.ToString() ?? "")}");
            }

            // Try to get connection info from individual environment variables first
            var host = Environment.GetEnvironmentVariable("PGHOST");
            var port = Environment.GetEnvironmentVariable("PGPORT");
            var database = Environment.GetEnvironmentVariable("PGDATABASE");
            var username = Environment.GetEnvironmentVariable("PGUSER");
            var password = Environment.GetEnvironmentVariable("PGPASSWORD");
            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
            var databasePublicUrl = Environment.GetEnvironmentVariable("DATABASE_PUBLIC_URL");

            Console.WriteLine($"\nPostgreSQL Connection Variables for {typeof(T).Name}:");
            Console.WriteLine($"  PGHOST: {host}");
            Console.WriteLine($"  PGPORT: {port}");
            Console.WriteLine($"  PGDATABASE: {database}");
            Console.WriteLine($"  PGUSER: {username}");
            Console.WriteLine($"  PGPASSWORD: {password?.Length > 0}");
            Console.WriteLine($"  DATABASE_URL exists: {!string.IsNullOrEmpty(databaseUrl)}");
            Console.WriteLine($"  DATABASE_PUBLIC_URL exists: {!string.IsNullOrEmpty(databasePublicUrl)}");

            string connectionString;
            
            // Check if we have all the individual PostgreSQL environment variables
            if (!string.IsNullOrEmpty(host) && 
                !string.IsNullOrEmpty(port) && 
                !string.IsNullOrEmpty(database) && 
                !string.IsNullOrEmpty(username) && 
                !string.IsNullOrEmpty(password))
            {
                Console.WriteLine("Using individual PostgreSQL environment variables");
                var npgsqlBuilder = new NpgsqlConnectionStringBuilder
                {
                    Host = host,
                    Port = int.Parse(port),
                    Database = database,
                    Username = username,
                    Password = password,
                    SslMode = Npgsql.SslMode.Require,
                    TrustServerCertificate = true,
                    Pooling = true,
                    MinPoolSize = 0,
                    MaxPoolSize = 100,
                    ConnectionIdleLifetime = 300
                };
                connectionString = npgsqlBuilder.ToString();
            }
            else
            {
                // Fallback to DATABASE_URL if individual variables are not available
                var dbUrl = databaseUrl;
                if (string.IsNullOrEmpty(dbUrl))
                {
                    // If neither option is available, try DATABASE_PUBLIC_URL as last resort
                    dbUrl = databasePublicUrl;
                    if (string.IsNullOrEmpty(dbUrl))
                    {
                        var errorMessage = $"No PostgreSQL connection information available for {typeof(T).Name}. Environment variables status:\n" +
                            $"Individual vars complete: {!string.IsNullOrEmpty(host) && !string.IsNullOrEmpty(port) && !string.IsNullOrEmpty(database) && !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password)}\n" +
                            $"DATABASE_URL available: {!string.IsNullOrEmpty(databaseUrl)}\n" +
                            $"DATABASE_PUBLIC_URL available: {!string.IsNullOrEmpty(databasePublicUrl)}";
                        Console.WriteLine(errorMessage);
                        throw new Exception(errorMessage);
                    }
                    Console.WriteLine("Using DATABASE_PUBLIC_URL for connection");
                }
                else
                {
                    Console.WriteLine("Using DATABASE_URL for connection");
                }

                var databaseUri = new Uri(dbUrl);
                var userInfo = databaseUri.UserInfo.Split(':');
                var npgsqlBuilder = new NpgsqlConnectionStringBuilder
                {
                    Host = databaseUri.Host,
                    Port = databaseUri.Port,
                    Username = userInfo[0],
                    Password = userInfo[1],
                    Database = databaseUri.LocalPath.TrimStart('/'),
                    SslMode = Npgsql.SslMode.Require,
                    TrustServerCertificate = true,
                    Pooling = true,
                    MinPoolSize = 0,
                    MaxPoolSize = 100,
                    ConnectionIdleLifetime = 300
                };
                connectionString = npgsqlBuilder.ToString();
            }

            Console.WriteLine($"Configuring {typeof(T).Name} with PostgreSQL. Connection string: {connectionString}");
            
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
