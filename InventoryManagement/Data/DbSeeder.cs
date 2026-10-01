using InventoryManagement.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Data;

public static class DbSeeder
{
    private const string AdminEmail = "admin@gmail.com";
    private const string AdminPassword = "Admin@123";
    private const string AdminFullName = "System Administrator";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await db.Database.MigrateAsync(); // or EnsureCreatedAsync() if you don't use migrations

        // 1. Ensure all application roles exist
        foreach (var role in AppRoles.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                var roleResult = await roles.CreateAsync(new IdentityRole(role));
                if (!roleResult.Succeeded)
                {
                    var errors = string.Join("; ", roleResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Failed to create role '{role}': {errors}");
                }
            }
        }

        // 2. Ensure the admin user exists
        var admin = await users.FindByEmailAsync(AdminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = AdminEmail,
                Email = AdminEmail,
                EmailConfirmed = true,
                FullName = AdminFullName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await users.CreateAsync(admin, AdminPassword);
            if (!createResult.Succeeded)
            {
                var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to seed admin user: {errors}");
            }
        }

        // 3. Ensure the admin user is in the Admin role
        if (!await users.IsInRoleAsync(admin, AppRoles.Admin))
        {
            await users.AddToRoleAsync(admin, AppRoles.Admin);
        }
    }
}
