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

namespace InventoryManagement.Controllers;

[Authorize(Roles = AppRoles.SalesTeam)]
public class SalesController(ApplicationDbContext db, ReportService reports, UserManager<ApplicationUser> users) : Controller
{
    public async Task<IActionResult> Index()
    {
        var user = await users.GetUserAsync(User);
        var mine = db.SaleOrders.Where(s => s.SalesUserId == user!.Id && s.Status != SaleOrderStatus.Cancelled);
        var most = await db.SaleOrderItems
            .Where(i => i.SaleOrder.SalesUserId == user!.Id && i.SaleOrder.Status != SaleOrderStatus.Cancelled)
            .GroupBy(i => i.Product.Name)
            .Select(g => new { Name = g.Key, Qty = g.Sum(x => x.QuantityFulfilled) })
            .OrderByDescending(x => x.Qty)
            .FirstOrDefaultAsync();

        var vm = new SalesDashboardViewModel
        {
            TotalSales = await mine.SumAsync(s => (decimal?)s.TotalAmount) ?? 0,
            TotalProfit = await mine.SumAsync(s => (decimal?)(s.TotalAmount - s.TotalCost)) ?? 0,
            OrderCount = await mine.CountAsync(),
            OpenBacklogs = await db.BacklogItems.CountAsync(b => !b.IsFulfilled && b.SaleOrder.Status != SaleOrderStatus.Cancelled && b.SaleOrder.SalesUserId == user!.Id),
            MostSoldItem = most?.Name ?? "—"
        };
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Chart(string type = "sales", string period = ReportPeriod.Daily)
    {
        var user = await users.GetUserAsync(User);
        var data = type == "profit"
            ? await reports.ProfitChartAsync(period, user!.Id)
            : await reports.SalesChartAsync(period, user!.Id);
        return Json(data);
    }
}


