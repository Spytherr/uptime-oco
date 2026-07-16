using FluentValidation;
using Microsoft.AspNetCore.Identity;
using uptime_oco;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("UptimeOcoContext")
    ?? "Data Source=uptime-oco.db";

builder.Services.AddDbContext<UptimeOcoContext>(options => options.UseSqlServer(connectionString));

builder.AddUptimeOcoDatabase(connectionString);

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
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
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddHttpClient();
builder.Services.AddOutputCache();
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

app.Run();
