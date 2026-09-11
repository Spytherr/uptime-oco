using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace uptime_oco;

public static class DataExtensions
{

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

    public static async Task EnsureInitialAdminAsync(this WebApplication app)
    {
        var adminEmail = app.Configuration["InitialAdmin:Email"]
            ?? app.Configuration["INITIAL_ADMIN_EMAIL"];
        var adminPassword = app.Configuration["InitialAdmin:Password"]
            ?? app.Configuration["INITIAL_ADMIN_PASSWORD"];

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        if (await userManager.Users.AnyAsync())
        {
            return;
        }

        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            var roleResult = await roleManager.CreateAsync(new IdentityRole("Admin"));
            if (!roleResult.Succeeded)
            {
                var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create admin role: {errors}");
            }
        }

        var user = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(user, adminPassword);
        if (!createResult.Succeeded)
        {
            var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
            logger.LogError("Failed to create initial admin user '{Email}': {Errors}", adminEmail, errors);
            throw new InvalidOperationException($"Failed to create initial admin user: {errors}");
        }

        var roleAssignResult = await userManager.AddToRoleAsync(user, "Admin");
        if (!roleAssignResult.Succeeded)
        {
            var errors = string.Join(", ", roleAssignResult.Errors.Select(e => e.Description));
            logger.LogError("Failed to assign Admin role to initial user '{Email}': {Errors}", adminEmail, errors);
            throw new InvalidOperationException($"Failed to assign admin role: {errors}");
        }

        logger.LogInformation("Initial admin user '{Email}' created successfully.", adminEmail);
    }
}
