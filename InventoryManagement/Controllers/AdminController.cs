using InventoryManagement.Data;
using InventoryManagement.Models;
using InventoryManagement.Models.ViewModels;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace InventoryManagement.Controllers;
[Authorize(Roles = AppRoles.Admin)]
public class AdminController(ApplicationDbContext db, ReportService reports) : Controller
{
    public async Task<IActionResult> Index()
    {
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
        var data = type switch
        {
            "sales" => await reports.SalesChartAsync(period),
            "purchase" => await reports.PurchaseChartAsync(period),
            _ => await reports.ProfitChartAsync(period)
        };
        return Json(data);
    }
}
