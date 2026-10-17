using InventoryManagement.Data;
using InventoryManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Controllers
{
    [Authorize(Roles = AppRoles.InventoryManager)]
    public class SuppliersController(ApplicationDbContext db) : Controller
    {
        public async Task<IActionResult> Index()
        {
            return View(await db.Suppliers.OrderBy(s => s.Name).ToListAsync());
        }

        public IActionResult Create() => View(new Supplier { IsActive = true });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Supplier supplier)
        {
            if (!ModelState.IsValid)
            {
                return View(supplier);
            }

            db.Suppliers.Add(supplier);
            await db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var supplier = await db.Suppliers.FindAsync(id);
            return supplier is null ? NotFound() : View(supplier);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Supplier supplier)
        {
            if (id != supplier.Id)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return View(supplier);
            }

            db.Update(supplier);
            await db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
