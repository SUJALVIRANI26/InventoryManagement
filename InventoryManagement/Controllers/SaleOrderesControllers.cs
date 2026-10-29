using System.Security.Claims;
using InventoryManagement.Data;
using InventoryManagement.Models;
using InventoryManagement.Models.ViewModels;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventoryManagement.Controllers
{
    [Authorize(Roles = AppRoles.SalesTeam)]
    public class SaleOrdersController(
        ApplicationDbContext db,
        IInventoryService inventory,
        UserManager<ApplicationUser> users,
        ILogger<SaleOrdersController>? logger = null) : Controller
    {
        private readonly ILogger<SaleOrdersController> _logger = logger ?? NullLogger<SaleOrdersController>.Instance;
        public async Task<IActionResult> Index()
        {
            var user = await users.GetUserAsync(User);
            var orders = await db.SaleOrders
                .Include(s => s.Items)
                .Where(s => s.SalesUserId == user!.Id)
                .OrderByDescending(s => s.OrderDate)
                .ToListAsync();
            return View(orders);
        }

        public async Task<IActionResult> Details(int id)
        {
            var user = await users.GetUserAsync(User);
            var order = await db.SaleOrders
                .Include(s => s.Items).ThenInclude(i => i.Product)
                .Include(s => s.BacklogItems)
                .FirstOrDefaultAsync(s => s.Id == id && s.SalesUserId == user!.Id);

            return order is null ? NotFound() : View(order);
        }

        public async Task<IActionResult> Create()
        {
            return View(await BuildForm(new SaleOrderFormViewModel()));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SaleOrderFormViewModel model)
        {
            model = await BuildForm(model);
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Sale order creation validation failed for customer '{CustomerName}'.", model.CustomerName);
                return View(model);
            }

            var user = await users.GetUserAsync(User);
            try
            {
                _logger.LogInformation("Placing sale order for customer '{CustomerName}' by user '{UserId}'.", model.CustomerName, user?.Id);
                var sale = await inventory.PlaceSaleAsync(
                    user!.Id,
                    model.CustomerName,
                    model.Lines.Select(l => (l.ProductId, l.Quantity, l.UnitPrice)),
                    model.Notes);

                _logger.LogInformation("Sale order #{SaleOrderId} created with status '{Status}'.", sale.Id, sale.Status);

                TempData["Status"] = sale.Status switch
                {
                    SaleOrderStatus.Completed => $"Sale #{sale.Id} completed from current stock.",
                    SaleOrderStatus.PartiallyBacklogged => $"Sale #{sale.Id} is partially backlogged. Available stock was allocated; remaining quantity waits for purchase.",
                    SaleOrderStatus.Backlogged => $"Sale #{sale.Id} went to backlog because requested quantity exceeds current stock.",
                    _ => $"Sale #{sale.Id} saved."
                };
                return RedirectToAction(nameof(Details), new { id = sale.Id });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "InvalidOperationException while placing sale order for customer '{CustomerName}'.", model.CustomerName);
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Proceed(int id)
        {
            var user = await users.GetUserAsync(User);
            var owns = await db.SaleOrders.AnyAsync(s => s.Id == id && s.SalesUserId == user!.Id);
            if (!owns)
            {
                _logger.LogWarning("User '{UserId}' attempted to proceed sale order #{SaleOrderId} without ownership.", user?.Id, id);
                return NotFound();
            }

            try
            {
                _logger.LogInformation("User '{UserId}' proceeding sale order #{SaleOrderId}.", user?.Id, id);
                await inventory.ProceedSaleAsync(id);
                TempData["Status"] = $"Sale #{id} can proceed and is now completed. Backlog was fulfilled.";
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Failed to proceed sale order #{SaleOrderId}.", id);
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<SaleOrderFormViewModel> BuildForm(SaleOrderFormViewModel model)
        {
            model.Products = await db.Products.Where(p => p.IsActive).OrderBy(p => p.Name)
                .Select(p => new SelectListItem($"{p.Name} — stock {p.CurrentStock} — suggested {p.SuggestedPrice:0.00}", p.Id.ToString()))
                .ToListAsync();
            if (model.Lines.Count == 0)
            {
                model.Lines.Add(new SaleLineViewModel());
            }
            return model;
        }
    }

}
