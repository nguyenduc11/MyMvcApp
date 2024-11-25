using Microsoft.EntityFrameworkCore;
using MyMvcApp.Models;
using Npgsql;
using System;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure database context based on environment
void ConfigureDbContext<T>(IServiceCollection services) where T : DbContext
{
    services.AddDbContext<T>(options =>
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var isProduction = environment == "Production";

        if (isProduction)
        {
            // Use Railway's PostgreSQL in production
            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
            if (!string.IsNullOrEmpty(databaseUrl))
            {
                try
                {
                    var databaseUri = new Uri(databaseUrl);
                    var userInfo = databaseUri.UserInfo.Split(':');
                    var builder = new NpgsqlConnectionStringBuilder
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

                    options.UseNpgsql(builder.ToString(), npgsqlOptions =>
                    {
                        npgsqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorCodesToAdd: null);
                    });
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error configuring PostgreSQL for {typeof(T).Name}: {ex.Message}", ex);
                }
            }
            else
            {
                throw new Exception($"DATABASE_URL environment variable is not set in production for {typeof(T).Name}");
            }
        }
        else
        {
            // Use SQLite in development
            var dbName = typeof(T).Name.Replace("Context", "").ToLower();
            var connectionString = $"Data Source=./{dbName}.db";
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

// Apply migrations for both contexts
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    
    async Task MigrateDbContext<T>(string contextName) where T : DbContext
    {
        try
        {
            var context = services.GetRequiredService<T>();
            var database = context.Database;
            
            // Log the current database provider
            logger.LogInformation("{Context} provider: {Provider}", contextName, database.ProviderName);
            logger.LogInformation("{Context} connection string: {ConnectionString}", contextName, database.GetConnectionString());
            
            // Ensure database is created
            await database.EnsureCreatedAsync();
            
            // Apply pending migrations
            if ((await database.GetPendingMigrationsAsync()).Any())
            {
                logger.LogInformation("Applying pending migrations for {Context}...", contextName);
                await database.MigrateAsync();
            }
            
            // Test the database connection
            if (await database.CanConnectAsync())
            {
                logger.LogInformation("Database connection and migrations successful for {Context}", contextName);
            }
            else
            {
                throw new Exception($"Cannot connect to the database after migration for {contextName}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while setting up the {Context}: {Message}", contextName, ex.Message);
            throw;
        }
    }

    // Migrate both contexts
    await MigrateDbContext<ApplicationDbContext>("ApplicationDbContext");
    await MigrateDbContext<BlogDbContext>("BlogDbContext");
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
