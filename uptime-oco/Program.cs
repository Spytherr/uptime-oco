using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using uptime_oco;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("UptimeOcoContext")
    ?? "Data Source=uptime-oco.db";

builder.AddUptimeOcoDatabase(connectionString);

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<UptimeOcoContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";
});

builder.Services.AddControllersWithViews();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.Configure<MonitoringOptions>(
    builder.Configuration.GetSection("Monitoring"));
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IMonitoringService, MonitoringService>();
builder.Services.AddSingleton<INotificationSender, DiscordNotificationSender>();
builder.Services.AddSingleton<INotificationSender, WebhookNotificationSender>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddHostedService<MonitorBackgroundService>();
builder.Services.AddHostedService<NotificationOutboxService>();
builder.Services.AddHostedService<DataRetentionService>();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<UptimeOcoContext>();
builder.Services.AddHttpClient("monitor");
builder.Services.AddHttpClient("notification");
builder.Services.AddOutputCache();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("setup", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
builder.Services.AddSignalR();

var app = builder.Build();

app.MigrateDatabase();

if (app.Environment.IsDevelopment())
{
    await app.SeedDemoDataAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseMiddleware<SetupCheckMiddleware>();
app.UseAuthorization();

app.UseOutputCache();

app.MapStaticAssets();
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapHub<MonitorHub>("/hubs/monitor");

app.Run();

public partial class Program { }
