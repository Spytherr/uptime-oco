using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace uptime_oco;

[Authorize]
public class DashboardController(
    IDashboardService dashboardService,
    UserManager<ApplicationUser> userManager) : Controller
{
    public async Task<IActionResult> Index()
    {
        var userId = userManager.GetUserId(User);
        if (userId is null)
        {
            return Challenge();
        }

        var result = await dashboardService.GetDashboardDataAsync(userId);
        if (!result.IsSuccess)
        {
            return View(new DashboardViewModel());
        }

        return View(result.Value);
    }
}
