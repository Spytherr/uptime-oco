using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace uptime_oco;

public static class DataExtensions
{
    public const string AdminEmail = "admin@uptime-oco.local";
    public const string AdminPassword = "Admin123!";

    public const string AdminEmail2 = "admin2@uptime-oco.local";
    public const string AdminPassword2 = "Admin123!";

    public static void AddUptimeOcoDatabase(this WebApplicationBuilder builder, string? connectionString)
    {
        builder.Services.AddDbContext<UptimeOcoContext>(options =>
            options.UseSqlite(connectionString));
    }

    public static void MigrateDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<UptimeOcoContext>();
        context.Database.Migrate();
    }

    public static async Task SeedDemoDataAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<UptimeOcoContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            var roleResult = await roleManager.CreateAsync(new IdentityRole("Admin"));
            if (!roleResult.Succeeded)
            {
                var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create seed admin role: {errors}");
            }
        }

        var admin = await EnsureAdminUserAsync(userManager, AdminEmail, AdminPassword);
        var admin2 = await EnsureAdminUserAsync(userManager, AdminEmail2, AdminPassword2);

        if (await context.Monitors.AnyAsync())
        {
            return;
        }

        var now = DateTime.UtcNow;
        var rng = new Random(42);

        var monitors = new List<Monitor>
        {
            new() { Name = "Google", Url = "https://www.google.com", IntervalSeconds = 60, UserId = admin.Id },
            new() { Name = "GitHub", Url = "https://github.com", IntervalSeconds = 60, UserId = admin.Id },
            new() { Name = "JSONPlaceholder API", Url = "https://jsonplaceholder.typicode.com/posts/1", IntervalSeconds = 120, UserId = admin.Id },
            new() { Name = "HttpStat 200 OK", Url = "https://httpstat.us/200", IntervalSeconds = 60, UserId = admin.Id },
            new() { Name = "HttpStat 500 Error", Url = "https://httpstat.us/500", IntervalSeconds = 60, UserId = admin.Id },
            new() { Name = "Twitch", Url = "https://www.twitch.tv", IntervalSeconds = 60, UserId = admin2.Id }
        };

        context.Monitors.AddRange(monitors);
        await context.SaveChangesAsync();

        var pings = new List<PingResult>();
        foreach (var monitor in monitors)
        {
            var alwaysDown = monitor.Url!.Contains("/500");
            for (var i = 0; i < 100; i++)
            {
                var checkedAt = now.AddMinutes(-i * 14.4);
                var success = !alwaysDown && rng.Next(100) >= 4;
                pings.Add(new PingResult
                {
                    MonitorId = monitor.Id,
                    IsSuccess = success,
                    HttpStatusCode = success ? 200 : alwaysDown ? 500 : 503,
                    ResponseTimeMs = success ? rng.Next(30, 320) : null,
                    CheckedAt = checkedAt
                });
            }

            monitor.LastCheckAt = now;
            monitor.ConsecutiveFailures = alwaysDown ? monitor.RetryThreshold + 2 : 0;
        }

        context.PingResults.AddRange(pings);

        var http500 = monitors.First(m => m.Url!.Contains("/500"));
        var google = monitors[0];

        context.Incidents.AddRange(
            new Incident
            {
                MonitorId = http500.Id,
                StartedAt = now.AddHours(-3),
                ResolvedAt = null,
                Reason = "HTTP 500 Internal Server Error"
            },
            new Incident
            {
                MonitorId = google.Id,
                StartedAt = now.AddHours(-8),
                ResolvedAt = now.AddHours(-8).AddMinutes(12),
                Reason = "Request timed out after 10s"
            });

        context.NotificationChannels.Add(new NotificationChannel
        {
            Name = "Discord (przyklad)",
            Type = NotificationType.Discord,
            Target = "https://discord.com/api/webhooks/000000/replace-me",
            IsEnabled = false,
            UserId = admin.Id
        });

        await context.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> EnsureAdminUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create seed admin user: {errors}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, "Admin"))
        {
            var roleResult = await userManager.AddToRoleAsync(user, "Admin");
            if (!roleResult.Succeeded)
            {
                var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to assign seed admin role: {errors}");
            }
        }

        return user;
    }
}
