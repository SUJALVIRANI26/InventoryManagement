using System.ComponentModel.DataAnnotations;
using InventoryManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class RegisterModel(UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool CanCreateFirstAdmin { get; private set; }

    public class InputModel
    {
        [Required, Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        CanCreateFirstAdmin = !await HasActiveAdminAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await HasActiveAdminAsync())
        {
            return RedirectToPage("./Login");
        }

        CanCreateFirstAdmin = true;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = new ApplicationUser
        {
            UserName = Input.Email,
            Email = Input.Email,
            EmailConfirmed = true,
            FullName = Input.FullName,
            IsActive = true
        };
        var result = await userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }

        await userManager.AddToRoleAsync(user, AppRoles.Admin);
        return RedirectToPage("./Login");
    }

    private async Task<bool> HasActiveAdminAsync()
    {
        var admins = await userManager.GetUsersInRoleAsync(AppRoles.Admin);
        return admins.Any(user => user.IsActive);
    }
}
