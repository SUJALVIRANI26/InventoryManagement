using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.Extensions.Logging;
using TestProject1.Helpers;
using Xunit.Abstractions;

namespace TestProject1.Services;

public class ReportServiceTests
{
    private readonly ITestOutputHelper _output;

    public ReportServiceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ProfitChartAsync_CalculatesProfitAndLogsInformation()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var logger = new TestLogger<ReportService>(_output);
        var service = new ReportService(db, logger);

        db.SaleOrders.AddRange(
            new SaleOrder
            {
                SalesUserId = "user-1",
                CustomerName = "Cust A",
                Status = SaleOrderStatus.Completed,
                TotalAmount = 500m,
                TotalCost = 300m, // Profit = 200
                OrderDate = DateTime.UtcNow
            },
            new SaleOrder
            {
                SalesUserId = "user-1",
                CustomerName = "Cust B",
                Status = SaleOrderStatus.Cancelled, // Should be ignored
                TotalAmount = 1000m,
                TotalCost = 500m,
                OrderDate = DateTime.UtcNow
            }
        );
        await db.SaveChangesAsync();

        // Act
        var chart = await service.ProfitChartAsync(ReportPeriod.Daily, "user-1");

        // Assert
        Assert.NotNull(chart);
        Assert.NotEmpty(chart.Labels);
        Assert.NotEmpty(chart.Values);
        Assert.Contains(200m, chart.Values);

        // Verify ILogger
        Assert.True(logger.HasLogLevel(LogLevel.Information));
        Assert.True(logger.HasMessage("Generating profit chart"));
        Assert.True(logger.HasMessage("user-1"));
    }

    [Fact]
    public async Task SalesChartAsync_ReturnsSalesAndLogsInformation()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var logger = new TestLogger<ReportService>(_output);
        var service = new ReportService(db, logger);

        db.SaleOrders.Add(new SaleOrder
        {
            SalesUserId = "sales-1",
            CustomerName = "Cust C",
            Status = SaleOrderStatus.Completed,
            TotalAmount = 750m,
            TotalCost = 400m,
            OrderDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Act
        var chart = await service.SalesChartAsync(ReportPeriod.Daily);

        // Assert
        Assert.NotNull(chart);
        Assert.Contains(750m, chart.Values);

        // Verify ILogger
        Assert.True(logger.HasLogLevel(LogLevel.Information));
        Assert.True(logger.HasMessage("Generating sales chart"));
    }

    [Fact]
    public async Task PurchaseChartAsync_FiltersByManagerAndLogsInformation()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var logger = new TestLogger<ReportService>(_output);
        var service = new ReportService(db, logger);

        db.PurchaseOrders.AddRange(
            new PurchaseOrder
            {
                CreatedByUserId = "mgr-1",
                SupplierId = 1,
                TotalAmount = 1200m,
                OrderDate = DateTime.UtcNow
            },
            new PurchaseOrder
            {
                CreatedByUserId = "mgr-2",
                SupplierId = 1,
                TotalAmount = 800m,
                OrderDate = DateTime.UtcNow
            }
        );
        await db.SaveChangesAsync();

        // Act
        var chart = await service.PurchaseChartAsync(ReportPeriod.Daily, "mgr-1");

        // Assert
        Assert.NotNull(chart);
        Assert.Contains(1200m, chart.Values);
        Assert.DoesNotContain(2000m, chart.Values);

        // Verify ILogger
        Assert.True(logger.HasLogLevel(LogLevel.Information));
        Assert.True(logger.HasMessage("Generating purchase chart"));
        Assert.True(logger.HasMessage("mgr-1"));
    }

    [Fact]
    public async Task MostSoldAsync_ReturnsHighestFulfilledProductAndLogsInformation()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var logger = new TestLogger<ReportService>(_output);
        var service = new ReportService(db, logger);

        var productA = new Product { Name = "Super Mouse", Sku = "MS-1", CurrentStock = 100, AverageCost = 10m, IsActive = true };
        var productB = new Product { Name = "Keyboard Pro", Sku = "KB-1", CurrentStock = 100, AverageCost = 25m, IsActive = true };
        db.Products.AddRange(productA, productB);
        await db.SaveChangesAsync();

        var saleOrder = new SaleOrder
        {
            CustomerName = "Retail",
            SalesUserId = "sales-1",
            Status = SaleOrderStatus.Completed,
            Items =
            [
                new SaleOrderItem { ProductId = productA.Id, QuantityFulfilled = 25, UnitPrice = 20m },
                new SaleOrderItem { ProductId = productB.Id, QuantityFulfilled = 10, UnitPrice = 50m }
            ]
        };
        db.SaleOrders.Add(saleOrder);
        await db.SaveChangesAsync();

        // Act
        var (name, qty) = await service.MostSoldAsync();

        // Assert
        Assert.Equal("Super Mouse", name);
        Assert.Equal(25, qty);

        // Verify ILogger
        Assert.True(logger.HasLogLevel(LogLevel.Information));
        Assert.True(logger.HasMessage("Calculating most sold product"));
    }
}
