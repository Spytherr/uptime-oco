using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace uptime_oco;

[AllowAnonymous]
public class SetupController(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IValidator<SetupViewModel> validator) : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        if (userManager.Users.Any())
        {
            return RedirectToAction("Index", "Home");
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SetupViewModel model)
    {
        if (userManager.Users.Any())
        {
            return RedirectToAction("Index", "Home");
        }

        var validationResult = await validator.ValidateAsync(model);
        if (!validationResult.IsValid)
        {
            foreach (var error in validationResult.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await EnsureAdminRoleAsync();
        await userManager.AddToRoleAsync(user, "Admin");

        return RedirectToAction("Login", "Account");
    }

    private async Task EnsureAdminRoleAsync()
    {
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }
    }
}
