using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using System.Net;
using System.Threading.RateLimiting;
using uptime_oco;

var builder = WebApplication.CreateBuilder(args);

var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "data", "keys");
Directory.CreateDirectory(dataProtectionKeysPath);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("uptime-oco");

var knownProxyAddresses = builder.Configuration
    .GetSection("ReverseProxy:KnownProxies")
    .GetChildren()
    .Select(section => section.Value)
    .Where(value => !string.IsNullOrWhiteSpace(value))
    .Select(value => value!)
    .ToArray();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
    options.ForwardLimit = 1;
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Clear();

    foreach (var address in knownProxyAddresses)
    {
        if (!IPAddress.TryParse(address, out var ipAddress))
        {
            throw new InvalidOperationException(
                $"Invalid ReverseProxy:KnownProxies address '{address}'.");
        }

        options.KnownProxies.Add(ipAddress);
    }
});

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
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
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

var allowPrivateNetworks = builder.Configuration.GetValue(
    "OutboundHttp:AllowPrivateNetworks",
    true);
builder.Services.AddHttpClient("monitor")
    .ConfigurePrimaryHttpMessageHandler(() =>
        OutboundHttpHandlerFactory.Create(allowPrivateNetworks))
    .SetHandlerLifetime(TimeSpan.FromMinutes(5));
builder.Services.AddHttpClient("notification")
    .ConfigurePrimaryHttpMessageHandler(() =>
        OutboundHttpHandlerFactory.Create(allowPrivateNetworks))
    .SetHandlerLifetime(TimeSpan.FromMinutes(5));
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

app.UseForwardedHeaders();

app.MigrateDatabase();
await app.EnsureInitialAdminAsync();

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
