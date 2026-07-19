using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace uptime_oco;

[Authorize]
public class IncidentsController(
    UptimeOcoContext context,
    UserManager<ApplicationUser> userManager) : Controller
{
    public async Task<IActionResult> Index(string? filter, int page = 1)
    {
        var userId = userManager.GetUserId(User);
        var query = context.Incidents
            .AsNoTracking()
            .Include(i => i.Monitor)
            .Where(i => i.Monitor!.UserId == userId);

        filter = filter?.ToLowerInvariant();
        ViewData["Filter"] = filter;

        query = filter switch
        {
            "open" => query.Where(i => i.ResolvedAt == null),
            "resolved" => query.Where(i => i.ResolvedAt != null),
            _ => query
        };

        query = query.OrderByDescending(i => i.StartedAt);

        var pageSize = 15;
        var paginated = await PaginatedList<Incident>.CreateAsync(query, page, pageSize);

        return View(paginated);
    }

    public async Task<IActionResult> Details(int id)
    {
        var userId = userManager.GetUserId(User);
        var incident = await context.Incidents
            .AsNoTracking()
            .Include(i => i.Monitor)
            .ThenInclude(m => m!.PingResults.OrderByDescending(p => p.CheckedAt).Take(50))
            .FirstOrDefaultAsync(i => i.Id == id && i.Monitor!.UserId == userId);

        if (incident is null)
        {
            return NotFound();
        }

        return View(incident);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(int id)
    {
        var userId = userManager.GetUserId(User);
        var incident = await context.Incidents
            .Include(i => i.Monitor)
            .FirstOrDefaultAsync(i => i.Id == id && i.Monitor!.UserId == userId);

        if (incident is null)
        {
            return NotFound();
        }

        if (incident.ResolvedAt is null)
        {
            incident.ResolvedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }
}
