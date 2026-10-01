using InventoryManagement.Data;
using InventoryManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Services;

public class ReportPeriod
{
    public const string Daily = "daily";
    public const string Weekly = "weekly";
    public const string Monthly = "monthly";
    public const string Yearly = "yearly";
}

public class ReportService(ApplicationDbContext db)
{
    public async Task<Models.ViewModels.ChartPayload> ProfitChartAsync(string period, string? salesUserId = null)
    {
        var query = db.SaleOrders.Where(s => s.Status != SaleOrderStatus.Cancelled);
        if (!string.IsNullOrEmpty(salesUserId))
        {
            query = query.Where(s => s.SalesUserId == salesUserId);
        }

        var sales = await query.ToListAsync();
        return Bucket(sales, period, s => s.OrderDate, s => s.Profit);
    }

    public async Task<Models.ViewModels.ChartPayload> SalesChartAsync(string period, string? salesUserId = null)
    {
        var query = db.SaleOrders.Where(s => s.Status != SaleOrderStatus.Cancelled);
        if (!string.IsNullOrEmpty(salesUserId))
        {
            query = query.Where(s => s.SalesUserId == salesUserId);
        }

        var sales = await query.ToListAsync();
        return Bucket(sales, period, s => s.OrderDate, s => s.TotalAmount);
    }

    public async Task<Models.ViewModels.ChartPayload> PurchaseChartAsync(string period, string? managerUserId = null)
    {
        var query = db.PurchaseOrders.AsQueryable();
        if (!string.IsNullOrEmpty(managerUserId))
        {
            query = query.Where(p => p.CreatedByUserId == managerUserId);
        }

        var purchases = await query.ToListAsync();
        return Bucket(purchases, period, p => p.OrderDate, p => p.TotalAmount);
    }

    public async Task<(string Name, int Qty)> MostSoldAsync()
    {
        var row = await db.SaleOrderItems
            .Where(i => i.SaleOrder.Status != SaleOrderStatus.Cancelled)
            .Include(i => i.Product)
            .GroupBy(i => new { i.ProductId, i.Product.Name })
            .Select(g => new { g.Key.Name, Qty = g.Sum(x => x.QuantityFulfilled) })
            .OrderByDescending(x => x.Qty)
            .FirstOrDefaultAsync();

        return row is null ? ("—", 0) : (row.Name, row.Qty);
    }

    private static Models.ViewModels.ChartPayload Bucket<T>(
        IEnumerable<T> items,
        string period,
        Func<T, DateTime> dateSelector,
        Func<T, decimal> valueSelector)
    {
        var now = DateTime.UtcNow.Date;
        List<(string Label, DateTime Start, DateTime End)> buckets = period switch
        {
            ReportPeriod.Weekly => Enumerable.Range(0, 12)
                .Select(i =>
                {
                    var end = now.AddDays(-7 * i);
                    var start = end.AddDays(-6);
                    return ($"W{12 - i}", start, end.AddDays(1));
                })
                .Reverse()
                .ToList(),
            ReportPeriod.Monthly => Enumerable.Range(0, 12)
                .Select(i =>
                {
                    var month = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-i);
                    return (month.ToString("MMM yy"), month, month.AddMonths(1));
                })
                .Reverse()
                .ToList(),
            ReportPeriod.Yearly => Enumerable.Range(0, 5)
                .Select(i =>
                {
                    var year = new DateTime(now.Year - i, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    return (year.Year.ToString(), year, year.AddYears(1));
                })
                .Reverse()
                .ToList(),
            _ => Enumerable.Range(0, 14)
                .Select(i =>
                {
                    var day = now.AddDays(-i);
                    return (day.ToString("dd MMM"), day, day.AddDays(1));
                })
                .Reverse()
                .ToList()
        };

        var payload = new Models.ViewModels.ChartPayload();
        foreach (var (label, start, end) in buckets)
        {
            payload.Labels.Add(label);
            payload.Values.Add(items
                .Where(x => dateSelector(x) >= start && dateSelector(x) < end)
                .Sum(valueSelector));
        }

        return payload;
    }
}
