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
            SslMode = SslMode.Require,
            Pooling = true,
            MinPoolSize = 0,
            MaxPoolSize = 10,
            ConnectionIdleLifetime = 300
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

// Apply migrations and initialize database
try
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        Console.WriteLine("Ensuring database is created...");
        context.Database.EnsureCreated();
        
        Console.WriteLine("Checking pending migrations...");
        if (context.Database.GetPendingMigrations().Any())
        {
            Console.WriteLine("Applying pending migrations...");
            context.Database.Migrate();
            Console.WriteLine("Migrations applied successfully");
        }
        else
        {
            Console.WriteLine("No pending migrations");
        }

        // Seed initial data if needed
        if (!context.TodoItems.Any())
        {
            Console.WriteLine("Seeding initial data...");
            context.TodoItems.AddRange(
                new TodoItem
                {
                    Task = "Sample Task 1",
                    Description = "This is a sample task",
                    IsCompleted = false,
                    CreatedAt = DateTime.UtcNow
                },
                new TodoItem
                {
                    Task = "Sample Task 2",
                    Description = "This is another sample task",
                    IsCompleted = true,
                    CreatedAt = DateTime.UtcNow
                }
            );
            context.SaveChanges();
            Console.WriteLine("Initial data seeded successfully");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error during database initialization: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    // In production, we might want to continue running even if migrations fail
    if (!isProduction)
    {
        throw;
    }
}

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