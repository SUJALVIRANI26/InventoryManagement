using InventoryManagement.Data;
using InventoryManagement.Models;
using InventoryManagement.Models.ViewModels;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Controllers;

[Authorize(Roles = AppRoles.InventoryManager)]
public class InventoryController(ApplicationDbContext db, ReportService reports) : Controller
{
    public async Task<IActionResult> Index()
    {
        var low = await db.Products
            .Where(p => p.IsActive && p.CurrentStock <= p.ReorderLevel)
            .OrderBy(p => p.CurrentStock)
            .ToListAsync();

        var vm = new InventoryDashboardViewModel
        {
            TotalPurchases = await db.PurchaseOrders.SumAsync(p => (decimal?)p.TotalAmount) ?? 0,
            PurchaseCount = await db.PurchaseOrders.CountAsync(),
            LowStockCount = low.Count,
            OpenBacklogs = await db.BacklogItems.CountAsync(b => !b.IsFulfilled && b.SaleOrder.Status != SaleOrderStatus.Cancelled),
            LowStockProducts = low
        };
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Chart(string period = ReportPeriod.Daily)
    {
        var data = await reports.PurchaseChartAsync(period);
        return Json(data);
    }
}

