using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace uptime_oco;

public static class DataExtensions
{
    public const string AdminEmail = "admin@uptime-oco.local";
    public const string AdminPassword = "Admin123!";

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

        if (await context.Monitors.AnyAsync())
        {
            return;
        }

        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }

        var admin = await userManager.FindByEmailAsync(AdminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = AdminEmail,
                Email = AdminEmail,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(admin, AdminPassword);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create seed admin user: {errors}");
            }

            await userManager.AddToRoleAsync(admin, "Admin");
        }

        var now = DateTime.UtcNow;
        var rng = new Random(42);

        var httpMonitors = new List<Monitor>
        {
            new() { Name = "Google", Url = "https://www.google.com", Type = MonitorType.Http, IntervalSeconds = 60, UserId = admin.Id },
            new() { Name = "GitHub", Url = "https://github.com", Type = MonitorType.Http, IntervalSeconds = 60, UserId = admin.Id },
            new() { Name = "JSONPlaceholder API", Url = "https://jsonplaceholder.typicode.com/posts/1", Type = MonitorType.Http, IntervalSeconds = 120, UserId = admin.Id },
            new() { Name = "HttpStat 200 OK", Url = "https://httpstat.us/200", Type = MonitorType.Http, IntervalSeconds = 60, UserId = admin.Id },
            new() { Name = "HttpStat 500 Error", Url = "https://httpstat.us/500", Type = MonitorType.Http, IntervalSeconds = 60, UserId = admin.Id }
        };

        var heartbeatMonitors = new List<Monitor>
        {
            new() { Name = "Backup CronJob", Type = MonitorType.Heartbeat, GracePeriodSeconds = 3600, UserId = admin.Id },
            new() { Name = "Daily Report Job", Type = MonitorType.Heartbeat, GracePeriodSeconds = 7200, UserId = admin.Id }
        };

        context.Monitors.AddRange(httpMonitors);
        context.Monitors.AddRange(heartbeatMonitors);
        await context.SaveChangesAsync();

        var pings = new List<PingResult>();
        foreach (var monitor in httpMonitors)
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

        var backupJob = heartbeatMonitors[0];
        var dailyJob = heartbeatMonitors[1];

        var heartbeats = new List<Heartbeat>();
        for (var i = 0; i < 20; i++)
        {
            heartbeats.Add(new Heartbeat
            {
                MonitorId = backupJob.Id,
                ReceivedAt = now.AddHours(-i),
                SourceIp = "203.0.113.10"
            });
        }

        backupJob.LastCheckAt = now;

        heartbeats.Add(new Heartbeat
        {
            MonitorId = dailyJob.Id,
            ReceivedAt = now.AddHours(-30),
            SourceIp = "203.0.113.20"
        });

        dailyJob.LastCheckAt = now.AddHours(-30);

        context.Heartbeats.AddRange(heartbeats);

        var http500 = httpMonitors.First(m => m.Url!.Contains("/500"));
        var google = httpMonitors[0];

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
}
