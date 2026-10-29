using InventoryManagement.Data;
using InventoryManagement.Models;
using InventoryManagement.Models.ViewModels;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventoryManagement.Controllers
{
    [Authorize(Roles = AppRoles.Admin)]
    public class UsersController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger<UsersController>? logger = null) : Controller
    {
        private readonly ILogger<UsersController> _logger = logger ?? NullLogger<UsersController>.Instance;

        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Retrieving all users for administrative review.");
            var users = await userManager.Users.OrderBy(u => u.FullName).ToListAsync();
            var rows = new List<UserListItemViewModel>();
            foreach (var user in users)
            {
                var roles = await userManager.GetRolesAsync(user);
                var role = roles.FirstOrDefault(r => r != AppRoles.Admin) ?? roles.FirstOrDefault() ?? "—";
                rows.Add(new UserListItemViewModel { User = user, Role = role });
            }
            return View(rows);
        }

        public IActionResult Create()
        {
            return View(new UserFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserFormViewModel model)
        {
            _logger.LogInformation("Admin attempting to create user with email '{Email}' and role '{Role}'.", model.Email, model.Role);

            if (model.Role is not (AppRoles.InventoryManager or AppRoles.SalesTeam))
            {
                ModelState.AddModelError(nameof(model.Role), "Admin can only create inventory managers or sales team users.");
            }
            if (string.IsNullOrWhiteSpace(model.Password))
            {
                ModelState.AddModelError(nameof(model.Password), "Password is required.");
            }
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("User creation failed validation for email '{Email}'.", model.Email);
                return View(model);
            }
            if (!await roleManager.RoleExistsAsync(model.Role))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(model.Role));
                if (!roleResult.Succeeded)
                {
                    foreach (var error in roleResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    _logger.LogWarning("Failed to ensure role '{Role}'.", model.Role);
                    return View(model);
                }
            }
            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                EmailConfirmed = true,
                FullName = model.FullName,
                IsActive = model.IsActive
            };
            var result = await userManager.CreateAsync(user, model.Password!);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                _logger.LogWarning("Failed to create user account for '{Email}'.", model.Email);
                return View(model);
            }
            var assignment = await userManager.AddToRoleAsync(user, model.Role);
            if (!assignment.Succeeded)
            {
                foreach (var error in assignment.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                _logger.LogWarning("Failed to assign role '{Role}' to user '{Email}'.", model.Role, model.Email);
                return View(model);
            }
            _logger.LogInformation("Successfully created user '{Email}' (ID: {UserId}) with role '{Role}'.", user.Email, user.Id, model.Role);
            TempData["Status"] = "User created.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(string id)
        {
            var user = await userManager.FindByIdAsync(id);
            if (user is null)
            {
                _logger.LogWarning("User with ID {UserId} not found for editing.", id);
                return NotFound();
            }
            var roles = await userManager.GetRolesAsync(user);
            if (roles.Contains(AppRoles.Admin))
            {
                _logger.LogWarning("Attempted to edit the seeded admin account ID {UserId} was blocked.", id);
                TempData["Error"] = "The seeded admin account cannot be edited here.";
                return RedirectToAction(nameof(Index));
            }
            return View(new UserFormViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? "",
                Role = roles.FirstOrDefault() ?? AppRoles.SalesTeam,
                IsActive = user.IsActive
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UserFormViewModel model)
        {
            if (model.Id is null)
            {
                return NotFound();
            }
            if (model.Role is not (AppRoles.InventoryManager or AppRoles.SalesTeam))
            {
                ModelState.AddModelError(nameof(model.Role), "Role must be Inventory Manager or Sales Team.");
            }
            var user = await userManager.FindByIdAsync(model.Id);
            if (user is null)
            {
                _logger.LogWarning("User with ID {UserId} not found for edit update.", model.Id);
                return NotFound();
            }
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("User update failed validation for user ID {UserId}.", model.Id);
                return View(model);
            }
            user.FullName = model.FullName;
            user.Email = model.Email;
            user.UserName = model.Email;
            user.IsActive = model.IsActive;
            var update = await userManager.UpdateAsync(user);
            if (!update.Succeeded)
            {
                foreach (var error in update.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                _logger.LogWarning("Failed to update user entity for ID {UserId}.", model.Id);
                return View(model);
            }
            var currentRoles = await userManager.GetRolesAsync(user);
            await userManager.RemoveFromRolesAsync(user, currentRoles);
            await userManager.AddToRoleAsync(user, model.Role);
            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(user);
                var pwd = await userManager.ResetPasswordAsync(user, token, model.Password);
                if (!pwd.Succeeded)
                {
                    foreach (var error in pwd.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    _logger.LogWarning("Failed to reset password for user ID {UserId}.", model.Id);
                    return View(model);
                }
            }
            _logger.LogInformation("Successfully updated user '{Email}' (ID: {UserId}).", user.Email, user.Id);
            TempData["Status"] = "User updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await userManager.FindByIdAsync(id);
            if (user is null)
            {
                _logger.LogWarning("User with ID {UserId} not found for deletion.", id);
                return NotFound();
            }
            var roles = await userManager.GetRolesAsync(user);
            if (roles.Contains(AppRoles.Admin))
            {
                _logger.LogWarning("Attempted to delete the seeded admin account ID {UserId} was blocked.", id);
                TempData["Error"] = "Cannot delete the admin account.";
                return RedirectToAction(nameof(Index));
            }
            await userManager.DeleteAsync(user);
            _logger.LogInformation("Successfully deleted user '{Email}' (ID: {UserId}).", user.Email, id);
            TempData["Status"] = "User deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
