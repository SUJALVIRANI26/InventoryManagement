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
    [Authorize(Roles = AppRoles.InventoryManager)]
    public class PurchaseOrdersController(
        ApplicationDbContext db,
        IInventoryService inventory,
        UserManager<ApplicationUser> users,
        ILogger<PurchaseOrdersController>? logger = null) : Controller
    {
        private readonly ILogger<PurchaseOrdersController> _logger = logger ?? NullLogger<PurchaseOrdersController>.Instance;
        public async Task<IActionResult> Index()
        {
            var orders = await db.PurchaseOrders
                .Include(p => p.Supplier)
                .Include(p => p.CreatedBy)
                .OrderByDescending(p => p.OrderDate)
                .ToListAsync();
            return View(orders);
        }

        public async Task<IActionResult> Details(int id)
        {
            var order = await db.PurchaseOrders
                .Include(p => p.Supplier)
                .Include(p => p.Items).ThenInclude(i => i.Product)
                .Include(p => p.CreatedBy)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (order is null)
            {
                return NotFound();
            }

            var logs = await db.BacklogFulfillmentLogs
                .Where(l => l.PurchaseOrderId == id)
                .OrderBy(l => l.CreatedAt)
                .ToListAsync();
            ViewBag.Logs = logs;
            return View(order);
        }

        public async Task<IActionResult> Create()
        {
            return View(await BuildForm(new PurchaseOrderFormViewModel()));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PurchaseOrderFormViewModel model)
        {
            model = await BuildForm(model);
            if (model.SupplierId <= 0)
            {
                ModelState.AddModelError(nameof(model.SupplierId), "Select a seller.");
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Purchase order validation failed for supplier {SupplierId}.", model.SupplierId);
                return View(model);
            }

            var user = await users.GetUserAsync(User);
            try
            {
                _logger.LogInformation("Receiving purchase order for supplier {SupplierId} by user {UserId}.", model.SupplierId, user?.Id);
                var result = await inventory.ReceivePurchaseAsync(
                    model.SupplierId,
                    user!.Id,
                    model.Lines.Select(l => (l.ProductId, l.Quantity, l.UnitCost)),
                    model.Notes);

                _logger.LogInformation("Purchase order #{PurchaseOrderId} created successfully.", result.PurchaseOrderId);
                TempData["Status"] = string.Join(" ", result.Messages);
                return RedirectToAction(nameof(Details), new { id = result.PurchaseOrderId });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Failed to receive purchase order for supplier {SupplierId}.", model.SupplierId);
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }
        }

        private async Task<PurchaseOrderFormViewModel> BuildForm(PurchaseOrderFormViewModel model)
        {
            model.Suppliers = await db.Suppliers.Where(s => s.IsActive).OrderBy(s => s.Name)
                .Select(s => new SelectListItem(s.Name, s.Id.ToString())).ToListAsync();
            model.Products = await db.Products.Where(p => p.IsActive).OrderBy(p => p.Name)
                .Select(p => new SelectListItem($"{p.Name} ({p.Sku})", p.Id.ToString())).ToListAsync();
            if (model.Lines.Count == 0)
            {
                model.Lines.Add(new PurchaseLineViewModel());
            }
            return model;
        }
    }
    
}
