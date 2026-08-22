using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace uptime_oco;

[Authorize]
public class MonitorsController(
    UptimeOcoContext context,
    UserManager<ApplicationUser> userManager,
    IValidator<CreateMonitorViewModel> createValidator,
    IValidator<EditMonitorViewModel> editValidator) : Controller
{
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var userId = userManager.GetUserId(User);
        var query = context.Monitors.AsNoTracking().Where(m => m.UserId == userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(m => m.Name.Contains(search) || m.Url.Contains(search));
        }

        query = query.OrderByDescending(m => m.CreatedAt);

        var pageSize = 10;
        var paginated = await PaginatedList<Monitor>.CreateAsync(query, page, pageSize);

        var monitorIds = paginated.Items.Select(m => m.Id).ToList();
        var cutoff = DateTime.UtcNow.AddHours(-24);
        var pingStats = await context.PingResults
            .AsNoTracking()
            .Where(p => monitorIds.Contains(p.MonitorId) && p.CheckedAt >= cutoff)
            .GroupBy(p => p.MonitorId)
            .Select(g => new { MonitorId = g.Key, Total = g.Count(), Success = g.Count(p => p.IsSuccess) })
            .ToListAsync();

        foreach (var monitor in paginated.Items)
        {
            var stats = pingStats.FirstOrDefault(p => p.MonitorId == monitor.Id);
            monitor.UptimePercent = stats is not null && stats.Total > 0
                ? Math.Round((double)stats.Success / stats.Total * 100, 2)
                : 100;
        }

        ViewData["Search"] = search;
        return View(paginated);
    }

    public async Task<IActionResult> Details(int id)
    {
        var userId = userManager.GetUserId(User);
        var monitor = await context.Monitors
            .AsNoTracking()
            .Include(m => m.PingResults.OrderByDescending(p => p.CheckedAt).Take(50))
            .Include(m => m.Incidents.OrderByDescending(i => i.StartedAt).Take(20))
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

        if (monitor is null)
        {
            return NotFound();
        }

        var cutoff = DateTime.UtcNow.AddHours(-24);
        var uptimeStats = await context.PingResults
            .AsNoTracking()
            .Where(p => p.MonitorId == monitor.Id && p.CheckedAt >= cutoff)
            .GroupBy(_ => 1)
            .Select(g => new { Total = g.Count(), Success = g.Count(p => p.IsSuccess) })
            .FirstOrDefaultAsync();

        monitor.UptimePercent = uptimeStats is not null && uptimeStats.Total > 0
            ? Math.Round((double)uptimeStats.Success / uptimeStats.Total * 100, 2)
            : 100;

        return View(monitor);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateMonitorViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateMonitorViewModel model)
    {
        var validationResult = await createValidator.ValidateAsync(model);
        if (!validationResult.IsValid)
        {
            foreach (var error in validationResult.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }
            return View(model);
        }

        var userId = userManager.GetUserId(User)!;

        var monitor = new Monitor
        {
            Name = model.Name,
            Url = model.Url,
            IntervalSeconds = model.IntervalSeconds,
            ExpectedStatusCode = model.ExpectedStatusCode,
            TimeoutSeconds = model.TimeoutSeconds,
            RetryThreshold = model.RetryThreshold,
            UserId = userId
        };

        context.Monitors.Add(monitor);
        await context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = userManager.GetUserId(User);
        var monitor = await context.Monitors
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

        if (monitor is null)
        {
            return NotFound();
        }

        var vm = new EditMonitorViewModel
        {
            Id = monitor.Id,
            Name = monitor.Name,
            Url = monitor.Url,
            IntervalSeconds = monitor.IntervalSeconds,
            ExpectedStatusCode = monitor.ExpectedStatusCode,
            TimeoutSeconds = monitor.TimeoutSeconds,
            RetryThreshold = monitor.RetryThreshold,
            IsActive = monitor.IsActive
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditMonitorViewModel model)
    {
        var validationResult = await editValidator.ValidateAsync(model);
        if (!validationResult.IsValid)
        {
            foreach (var error in validationResult.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }
            return View(model);
        }

        var userId = userManager.GetUserId(User);
        var monitor = await context.Monitors
            .FirstOrDefaultAsync(m => m.Id == model.Id && m.UserId == userId);

        if (monitor is null)
        {
            return NotFound();
        }

        monitor.Name = model.Name;
        monitor.Url = model.Url;
        monitor.IntervalSeconds = model.IntervalSeconds;
        monitor.ExpectedStatusCode = model.ExpectedStatusCode;
        monitor.TimeoutSeconds = model.TimeoutSeconds;
        monitor.RetryThreshold = model.RetryThreshold;
        monitor.IsActive = model.IsActive;

        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = userManager.GetUserId(User);
        var monitor = await context.Monitors
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

        if (monitor is null)
        {
            return NotFound();
        }

        return View(monitor);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var userId = userManager.GetUserId(User);
        var monitor = await context.Monitors
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

        if (monitor is not null)
        {
            context.Monitors.Remove(monitor);
            await context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id, string? returnUrl = null)
    {
        var userId = userManager.GetUserId(User);
        var monitor = await context.Monitors
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

        if (monitor is null)
        {
            return NotFound();
        }

        monitor.IsActive = !monitor.IsActive;
        await context.SaveChangesAsync();

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }
}
