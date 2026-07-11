using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace uptime_oco;

[Authorize]
public class DashboardController(IDashboardService dashboardService) : Controller
{
    [OutputCache(Duration = 10)]
    public async Task<IActionResult> Index()
    {
        var result = await dashboardService.GetDashboardDataAsync();
        if (!result.IsSuccess)
        {
            return View(new DashboardViewModel());
        }

        return View(result.Value);
    }
}
