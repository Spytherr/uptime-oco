using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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
            path.StartsWithSegments("/Account", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!await userManager.Users.AnyAsync(context.RequestAborted))
        {
            context.Response.Redirect("/Setup");
            return;
        }

        _isSetupComplete = true;
        await _next(context);
    }
}
