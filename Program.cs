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
        Console.WriteLine($"DOTNET_RUNNING_IN_CONTAINER: {Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")}");
        
        if (isProduction)
        {
            // Configure the PostgreSQL connection string using Railway's environment variables
            var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");
            if (string.IsNullOrEmpty(connectionString))
            {
                // Build connection string from individual environment variables
                var builder = new NpgsqlConnectionStringBuilder
                {
                    Host = Environment.GetEnvironmentVariable("PGHOST") ?? "postgres.railway.internal",
                    Port = int.Parse(Environment.GetEnvironmentVariable("PGPORT") ?? "5432"),
                    Database = Environment.GetEnvironmentVariable("PGDATABASE") ?? "railway",
                    Username = Environment.GetEnvironmentVariable("PGUSER") ?? "postgres",
                    Password = Environment.GetEnvironmentVariable("PGPASSWORD"),
                    SslMode = SslMode.Require,
                    TrustServerCertificate = true
                };
                connectionString = builder.ToString();
            }

            // Configure DbContext with the connection string
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
