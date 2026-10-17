using InventoryManagement.Data;
using InventoryManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Controllers
{
    [Authorize]
    public class BacklogController(ApplicationDbContext db) : Controller
    {
        public async Task<IActionResult> Index()
        {
            var query = db.BacklogItems
                .Include(b => b.Product)
                .Include(b => b.SaleOrder)
                .AsQueryable();

            if (User.IsInRole(AppRoles.SalesTeam) && !User.IsInRole(AppRoles.Admin))
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                query = query.Where(b => b.SaleOrder.SalesUserId == userId);
            }

            var items = await query
                .OrderBy(b => b.SaleOrder.Status == SaleOrderStatus.Cancelled)
                .ThenBy(b => b.IsFulfilled)
                .ThenBy(b => b.CreatedAt)
                .ToListAsync();
            return View(items);
        }
    }

}
