using InventoryManagement.Data;
using InventoryManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventoryManagement.Controllers
{
    [Authorize(Roles = AppRoles.InventoryManager)]
    public class SuppliersController(
        ApplicationDbContext db,
        ILogger<SuppliersController>? logger = null) : Controller
    {
        private readonly ILogger<SuppliersController> _logger = logger ?? NullLogger<SuppliersController>.Instance;

        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Retrieving all suppliers.");
            return View(await db.Suppliers.OrderBy(s => s.Name).ToListAsync());
        }

        public IActionResult Create() => View(new Supplier { IsActive = true });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Supplier supplier)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Failed to create supplier: invalid model state for '{SupplierName}'.", supplier.Name);
                return View(supplier);
            }

            db.Suppliers.Add(supplier);
            await db.SaveChangesAsync();
            _logger.LogInformation("Successfully created supplier '{SupplierName}' (ID: {SupplierId}).", supplier.Name, supplier.Id);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var supplier = await db.Suppliers.FindAsync(id);
            if (supplier is null)
            {
                _logger.LogWarning("Supplier with ID {SupplierId} not found for editing.", id);
                return NotFound();
            }
            return View(supplier);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Supplier supplier)
        {
            if (id != supplier.Id)
            {
                _logger.LogWarning("Supplier ID mismatch on Edit: route ID {RouteId} vs model ID {ModelId}.", id, supplier.Id);
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Failed to update supplier ID {SupplierId}: invalid model state.", id);
                return View(supplier);
            }

            db.Update(supplier);
            await db.SaveChangesAsync();
            _logger.LogInformation("Successfully updated supplier '{SupplierName}' (ID: {SupplierId}).", supplier.Name, supplier.Id);
            return RedirectToAction(nameof(Index));
        }
    }
}
