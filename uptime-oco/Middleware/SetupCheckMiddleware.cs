using Microsoft.AspNetCore.Identity;

namespace uptime_oco;

public class SetupCheckMiddleware
{
    private readonly RequestDelegate _next;
    private static bool _isSetupComplete = false;

    public SetupCheckMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager)
    {
        if (_isSetupComplete)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path;

        if (path.StartsWithSegments("/Setup", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/api/heartbeat", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/Account", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!userManager.Users.Any())
        {
            context.Response.Redirect("/Setup");
            return;
        }

        _isSetupComplete = true;
        await _next(context);
    }
}
