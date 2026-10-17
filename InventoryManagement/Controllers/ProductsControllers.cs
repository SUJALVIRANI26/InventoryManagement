using InventoryManagement.Data;
using InventoryManagement.Models;
using InventoryManagement.Models.ViewModels;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Controllers
{
    [Authorize(Roles = AppRoles.InventoryManager)]
    public class ProductsController(ApplicationDbContext db) : Controller
    {
        public async Task<IActionResult> Index()
        {
            return View(await db.Products.OrderBy(p => p.Name).ToListAsync());
        }

        public IActionResult Create() => View(new Product { ReorderLevel = 10, IsActive = true });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product)
        {
            if (!ModelState.IsValid)
            {
                return View(product);
            }

            db.Products.Add(product);
            await db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var product = await db.Products.FindAsync(id);
            return product is null ? NotFound() : View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product)
        {
            if (id != product.Id)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return View(product);
            }

            db.Update(product);
            await db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
