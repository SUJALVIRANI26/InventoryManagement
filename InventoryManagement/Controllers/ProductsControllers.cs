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
    public class ProductsController(ApplicationDbContext db, ILogger<ProductsController>? logger = null) : Controller
    {
        private readonly ILogger<ProductsController> _logger = logger ?? NullLogger<ProductsController>.Instance;

        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Retrieving all products.");
            return View(await db.Products.OrderBy(p => p.Name).ToListAsync());
        }

        public IActionResult Create() => View(new Product { ReorderLevel = 10, IsActive = true });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Failed to create product: invalid model state for SKU '{Sku}'.", product.Sku);
                return View(product);
            }

            db.Products.Add(product);
            await db.SaveChangesAsync();
            _logger.LogInformation("Successfully created product '{ProductName}' (ID: {ProductId}).", product.Name, product.Id);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var product = await db.Products.FindAsync(id);
            if (product is null)
            {
                _logger.LogWarning("Product with ID {ProductId} not found for editing.", id);
                return NotFound();
            }
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product)
        {
            if (id != product.Id)
            {
                _logger.LogWarning("Product ID mismatch on Edit: route ID {RouteId} vs model ID {ModelId}.", id, product.Id);
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Failed to update product ID {ProductId}: invalid model state.", id);
                return View(product);
            }

            db.Update(product);
            await db.SaveChangesAsync();
            _logger.LogInformation("Successfully updated product '{ProductName}' (ID: {ProductId}).", product.Name, product.Id);
            return RedirectToAction(nameof(Index));
        }
    }
}
