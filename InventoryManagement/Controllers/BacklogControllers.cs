using InventoryManagement.Data;
using InventoryManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventoryManagement.Controllers
{
    [Authorize]
    public class BacklogController(
        ApplicationDbContext db,
        ILogger<BacklogController>? logger = null) : Controller
    {
        private readonly ILogger<BacklogController> _logger = logger ?? NullLogger<BacklogController>.Instance;

        public async Task<IActionResult> Index()
        {
            var isSales = User.IsInRole(AppRoles.SalesTeam) && !User.IsInRole(AppRoles.Admin);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            _logger.LogInformation("Retrieving backlog items. User: '{UserId}', IsRestrictedToOwn: {IsSales}", userId, isSales);

            var query = db.BacklogItems
                .Include(b => b.Product)
                .Include(b => b.SaleOrder)
                .AsQueryable();

            if (isSales)
            {
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
