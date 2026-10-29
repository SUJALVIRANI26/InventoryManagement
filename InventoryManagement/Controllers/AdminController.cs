using InventoryManagement.Data;
using InventoryManagement.Models;
using InventoryManagement.Models.ViewModels;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventoryManagement.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class AdminController(
    ApplicationDbContext db,
    ReportService reports,
    ILogger<AdminController>? logger = null) : Controller
{
    private readonly ILogger<AdminController> _logger = logger ?? NullLogger<AdminController>.Instance;

    public async Task<IActionResult> Index()
    {
        _logger.LogInformation("Admin dashboard requested.");
        var most = await reports.MostSoldAsync();
        var vm = new AdminDashboardViewModel
        {
            UserCount = await db.Users.CountAsync(),
            TotalSales = await db.SaleOrders.Where(s => s.Status != SaleOrderStatus.Cancelled).SumAsync(s => (decimal?)s.TotalAmount) ?? 0,
            TotalPurchases = await db.PurchaseOrders.SumAsync(p => (decimal?)p.TotalAmount) ?? 0,
            TotalProfit = await db.SaleOrders.Where(s => s.Status != SaleOrderStatus.Cancelled).SumAsync(s => (decimal?)(s.TotalAmount - s.TotalCost)) ?? 0,
            MostSoldItem = most.Name,
            MostSoldQty = most.Qty,
            OpenBacklogs = await db.BacklogItems.CountAsync(b => !b.IsFulfilled && b.SaleOrder.Status != SaleOrderStatus.Cancelled),
            LowStockCount = await db.Products.CountAsync(p => p.IsActive && p.CurrentStock <= p.ReorderLevel)
        };
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Chart(string type = "profit", string period = ReportPeriod.Daily)
    {
        _logger.LogInformation("Admin chart data requested. Type: {Type}, Period: {Period}", type, period);
        var data = type switch
        {
            "sales" => await reports.SalesChartAsync(period),
            "purchase" => await reports.PurchaseChartAsync(period),
            _ => await reports.ProfitChartAsync(period)
        };
        return Json(data);
    }
}
