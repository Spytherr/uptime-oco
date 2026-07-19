using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;

namespace uptime_oco;

[Authorize]
public class NotificationsController(
    UptimeOcoContext context,
    UserManager<ApplicationUser> userManager,
    IHttpClientFactory httpClientFactory,
    IValidator<CreateNotificationViewModel> createValidator,
    IValidator<EditNotificationViewModel> editValidator) : Controller
{
    public async Task<IActionResult> Index()
    {
        var userId = userManager.GetUserId(User);
        var channels = await context.NotificationChannels
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return View(channels);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateNotificationViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateNotificationViewModel model)
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

        var channel = new NotificationChannel
        {
            Name = model.Name,
            Type = model.Type,
            Target = model.Target,
            IsEnabled = model.IsEnabled,
            UserId = userId
        };

        context.NotificationChannels.Add(channel);
        await context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = userManager.GetUserId(User);
        var channel = await context.NotificationChannels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

        if (channel is null)
        {
            return NotFound();
        }

        var vm = new EditNotificationViewModel
        {
            Id = channel.Id,
            Name = channel.Name,
            Type = channel.Type,
            Target = channel.Target,
            IsEnabled = channel.IsEnabled
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditNotificationViewModel model)
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
        var channel = await context.NotificationChannels
            .FirstOrDefaultAsync(c => c.Id == model.Id && c.UserId == userId);

        if (channel is null)
        {
            return NotFound();
        }

        channel.Name = model.Name;
        channel.Type = model.Type;
        channel.Target = model.Target;
        channel.IsEnabled = model.IsEnabled;

        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = userManager.GetUserId(User);
        var channel = await context.NotificationChannels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

        if (channel is null)
        {
            return NotFound();
        }

        return View(channel);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var userId = userManager.GetUserId(User);
        var channel = await context.NotificationChannels
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

        if (channel is not null)
        {
            context.NotificationChannels.Remove(channel);
            await context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Test(int id)
    {
        var userId = userManager.GetUserId(User);
        var channel = await context.NotificationChannels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

        if (channel is null)
        {
            return NotFound();
        }

        var client = httpClientFactory.CreateClient("notification");
        client.Timeout = TimeSpan.FromSeconds(10);

        var message = $"**Test notification** from uptime-oco\nChannel: {channel.Name}\nTime: {DateTime.UtcNow:O}";

        try
        {
            if (channel.Type == NotificationType.Discord)
            {
                await client.PostAsJsonAsync(channel.Target, new { content = message });
            }
            else
            {
                await client.PostAsJsonAsync(channel.Target, new { text = message, title = "uptime-oco test" });
            }

            TempData["TestResult"] = "Test notification sent successfully.";
        }
        catch (Exception)
        {
            TempData["TestResult"] = "Failed to send test notification. Check the webhook URL.";
        }

        return RedirectToAction(nameof(Index));
    }
}
