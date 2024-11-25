using Microsoft.EntityFrameworkCore;
using MyMvcApp.Models;
using Npgsql;
using System;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure database context based on environment
builder.Services.AddDbContext<ApplicationDbContext>(options =>
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
                var logger = LoggerFactory.Create(builder => builder.AddConsole())
                    .CreateLogger("Program");
                logger.LogError(ex, "Error configuring PostgreSQL");
                throw;
            }
        }
        else
        {
            throw new Exception("DATABASE_URL environment variable is not set in production");
        }
    }
    else
    {
        // Use SQLite in development
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        options.UseSqlite(connectionString, sqliteOptions =>
        {
            sqliteOptions.MigrationsAssembly(typeof(Program).Assembly.FullName);
        });
    }
});

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

// Apply migrations
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.Migrate();
        
        // Test the database connection
        var canConnect = context.Database.CanConnect();
        if (!canConnect)
        {
            throw new Exception("Cannot connect to the database after migration");
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating or connecting to the database: {Message}", ex.Message);
        Console.WriteLine($"Database Error: {ex.Message}");
        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        throw;
    }
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
