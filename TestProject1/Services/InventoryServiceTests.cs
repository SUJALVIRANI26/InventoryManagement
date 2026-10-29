using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TestProject1.Helpers;
using Xunit.Abstractions;

namespace TestProject1.Services;

public class InventoryServiceTests
{
    private readonly ITestOutputHelper _output;

    public InventoryServiceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ReceivePurchaseAsync_ValidItems_AddsPurchaseUpdatesStockAndLogsInformation()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var logger = new TestLogger<InventoryService>(_output);
        var service = new InventoryService(db, logger);

        var product = new Product
        {
            Name = "Widget A",
            Sku = "WGT-A",
            CurrentStock = 10,
            AverageCost = 5.00m,
            SuggestedPrice = 12.00m,
            IsActive = true
        };
        var supplier = new Supplier { Name = "Acme Supplies", IsActive = true };
        db.Products.Add(product);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var lines = new List<(int ProductId, int Quantity, decimal UnitCost)>
        {
            (product.Id, 10, 15.00m)
        };

        // Act
        var result = await service.ReceivePurchaseAsync(supplier.Id, "manager-1", lines, "Test purchase");

        // Assert
        Assert.True(result.PurchaseOrderId > 0);
        Assert.NotEmpty(result.Messages);

        var updatedProduct = await db.Products.FindAsync(product.Id);
        Assert.NotNull(updatedProduct);
        Assert.Equal(20, updatedProduct.CurrentStock);
        // (10 * 5.00 + 10 * 15.00) / 20 = 200 / 20 = 10.00
        Assert.Equal(10.00m, updatedProduct.AverageCost);

        var purchaseOrder = await db.PurchaseOrders.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == result.PurchaseOrderId);
        Assert.NotNull(purchaseOrder);
        Assert.Equal(150.00m, purchaseOrder.TotalAmount);
        Assert.Single(purchaseOrder.Items);

        // Verify ILogger
        Assert.True(logger.HasLogLevel(LogLevel.Information));
        Assert.True(logger.HasMessage("Received purchase order"));
        Assert.True(logger.HasMessage("Processing purchase receipt"));
    }

    [Fact]
    public async Task ReceivePurchaseAsync_WithOpenBacklog_FulfillsBacklogFIFOAndLogsInformation()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var logger = new TestLogger<InventoryService>(_output);
        var service = new InventoryService(db, logger);

        var product = new Product { Name = "Gadget B", Sku = "GDT-B", CurrentStock = 0, AverageCost = 10m, IsActive = true };
        var supplier = new Supplier { Name = "Tech Supply", IsActive = true };
        db.Products.Add(product);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        // Create Sale 1 with backlog of 5 (earlier creation time)
        var saleItem1 = new SaleOrderItem { ProductId = product.Id, QuantityRequested = 5, QuantityFulfilled = 0, UnitPrice = 20m, UnitCost = 10m };
        var sale1 = new SaleOrder
        {
            CustomerName = "Alice",
            SalesUserId = "sales-1",
            Status = SaleOrderStatus.Backlogged,
            Items = [saleItem1],
            BacklogItems = [new BacklogItem { ProductId = product.Id, SaleOrderItem = saleItem1, RemainingQuantity = 5, CreatedAt = DateTime.UtcNow.AddMinutes(1) }]
        };

        // Create Sale 2 with backlog of 10 (later creation time)
        var saleItem2 = new SaleOrderItem { ProductId = product.Id, QuantityRequested = 10, QuantityFulfilled = 0, UnitPrice = 20m, UnitCost = 10m };
        var sale2 = new SaleOrder
        {
            CustomerName = "Bob",
            SalesUserId = "sales-2",
            Status = SaleOrderStatus.Backlogged,
            Items = [saleItem2],
            BacklogItems = [new BacklogItem { ProductId = product.Id, SaleOrderItem = saleItem2, RemainingQuantity = 10, CreatedAt = DateTime.UtcNow.AddMinutes(5) }]
        };

        db.SaleOrders.AddRange(sale1, sale2);
        await db.SaveChangesAsync();

        // Act: receive 8 units from supplier
        var lines = new List<(int ProductId, int Quantity, decimal UnitCost)> { (product.Id, 8, 10m) };
        var result = await service.ReceivePurchaseAsync(supplier.Id, "mgr-1", lines, "Backlog fulfill order");

        // Assert
        var updatedProduct = await db.Products.FindAsync(product.Id);
        Assert.Equal(0, updatedProduct!.CurrentStock); // 8 received - 5 (sale1) - 3 (sale2) = 0

        var updatedSale1 = await db.SaleOrders.Include(s => s.BacklogItems).FirstAsync(s => s.Id == sale1.Id);
        Assert.Equal(SaleOrderStatus.ReadyToProceed, updatedSale1.Status);
        Assert.True(updatedSale1.BacklogItems.First().IsFulfilled);
        Assert.Equal(0, updatedSale1.BacklogItems.First().RemainingQuantity);

        var updatedSale2 = await db.SaleOrders.Include(s => s.BacklogItems).FirstAsync(s => s.Id == sale2.Id);
        Assert.Equal(SaleOrderStatus.PartiallyBacklogged, updatedSale2.Status);
        Assert.False(updatedSale2.BacklogItems.First().IsFulfilled);
        Assert.Equal(7, updatedSale2.BacklogItems.First().RemainingQuantity); // 10 - 3 = 7

        // Verify ILogger
        Assert.True(logger.HasMessage("Allocated 5 units of product Gadget B"));
        Assert.True(logger.HasMessage("Allocated 3 units of product Gadget B"));
    }

    [Fact]
    public async Task ReceivePurchaseAsync_EmptyLines_ThrowsInvalidOperationException()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var logger = new TestLogger<InventoryService>(_output);
        var service = new InventoryService(db, logger);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReceivePurchaseAsync(1, "mgr-1", Enumerable.Empty<(int, int, decimal)>(), null));
    }

    [Fact]
    public async Task PlaceSaleAsync_SufficientStock_FulfillsOrderImmediatelyAndLogsInformation()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var logger = new TestLogger<InventoryService>(_output);
        var service = new InventoryService(db, logger);

        var product = new Product { Name = "Laptop", Sku = "LPT-1", CurrentStock = 10, AverageCost = 500m, SuggestedPrice = 800m, IsActive = true };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var lines = new List<(int ProductId, int Quantity, decimal UnitPrice)>
        {
            (product.Id, 3, 750m)
        };

        // Act
        var sale = await service.PlaceSaleAsync("sales-1", "Contoso Corp", lines, "Standard order");

        // Assert
        Assert.NotNull(sale);
        Assert.Equal(SaleOrderStatus.Completed, sale.Status);
        Assert.Equal(2250m, sale.TotalAmount);
        Assert.Equal(1500m, sale.TotalCost);
        Assert.Empty(sale.BacklogItems);

        var updatedProduct = await db.Products.FindAsync(product.Id);
        Assert.Equal(7, updatedProduct!.CurrentStock);

        // Verify ILogger
        Assert.True(logger.HasLogLevel(LogLevel.Information));
        Assert.True(logger.HasMessage("Placed sale order"));
        Assert.False(logger.HasLogLevel(LogLevel.Warning));
    }

    [Fact]
    public async Task PlaceSaleAsync_InsufficientStock_CreatesBacklogAndLogsWarning()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var logger = new TestLogger<InventoryService>(_output);
        var service = new InventoryService(db, logger);

        var product = new Product { Name = "Monitor", Sku = "MON-1", CurrentStock = 4, AverageCost = 150m, SuggestedPrice = 250m, IsActive = true };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var lines = new List<(int ProductId, int Quantity, decimal UnitPrice)>
        {
            (product.Id, 10, 240m)
        };

        // Act
        var sale = await service.PlaceSaleAsync("sales-1", "Fabrikam", lines, "Need 10 monitors");

        // Assert
        Assert.NotNull(sale);
        Assert.Equal(SaleOrderStatus.PartiallyBacklogged, sale.Status);
        Assert.Single(sale.BacklogItems);
        Assert.Equal(6, sale.BacklogItems.First().RemainingQuantity);

        var updatedProduct = await db.Products.FindAsync(product.Id);
        Assert.Equal(0, updatedProduct!.CurrentStock);

        // Verify ILogger Warning
        Assert.True(logger.HasLogLevel(LogLevel.Warning));
        Assert.True(logger.HasMessage("Insufficient stock for product"));
        Assert.True(logger.HasLogLevel(LogLevel.Information));
    }

    [Fact]
    public async Task PlaceSaleAsync_ZeroStock_SetsStatusBackloggedAndLogsWarning()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var logger = new TestLogger<InventoryService>(_output);
        var service = new InventoryService(db, logger);

        var product = new Product { Name = "Keyboard", Sku = "KBD-1", CurrentStock = 0, AverageCost = 30m, SuggestedPrice = 60m, IsActive = true };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var lines = new List<(int ProductId, int Quantity, decimal UnitPrice)>
        {
            (product.Id, 5, 55m)
        };

        // Act
        var sale = await service.PlaceSaleAsync("sales-1", "TechCo", lines, null);

        // Assert
        Assert.Equal(SaleOrderStatus.Backlogged, sale.Status);
        Assert.Single(sale.BacklogItems);
        Assert.Equal(5, sale.BacklogItems.First().RemainingQuantity);

        // Verify ILogger
        Assert.True(logger.HasLogLevel(LogLevel.Warning));
        Assert.True(logger.HasMessage("Insufficient stock"));
    }

    [Fact]
    public async Task PlaceSaleAsync_EmptyLines_ThrowsInvalidOperationException()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var logger = new TestLogger<InventoryService>(_output);
        var service = new InventoryService(db, logger);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PlaceSaleAsync("sales-1", "Customer", Enumerable.Empty<(int, int, decimal)>(), null));
    }

    [Fact]
    public async Task ProceedSaleAsync_WhenReadyToProceed_MarksCompletedAndLogsInformation()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var logger = new TestLogger<InventoryService>(_output);
        var service = new InventoryService(db, logger);

        var sale = new SaleOrder
        {
            CustomerName = "John Doe",
            SalesUserId = "sales-1",
            Status = SaleOrderStatus.ReadyToProceed,
            TotalAmount = 500m
        };
        db.SaleOrders.Add(sale);
        await db.SaveChangesAsync();

        // Act
        await service.ProceedSaleAsync(sale.Id);

        // Assert
        var updated = await db.SaleOrders.FindAsync(sale.Id);
        Assert.Equal(SaleOrderStatus.Completed, updated!.Status);
        Assert.NotNull(updated.CompletedAt);

        // Verify ILogger
        Assert.True(logger.HasLogLevel(LogLevel.Information));
        Assert.True(logger.HasMessage($"Sale order #{sale.Id} marked completed."));
    }

    [Fact]
    public async Task ProceedSaleAsync_WhenNotReadyToProceed_ThrowsAndLogsWarning()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var logger = new TestLogger<InventoryService>(_output);
        var service = new InventoryService(db, logger);

        var sale = new SaleOrder
        {
            CustomerName = "Jane Doe",
            SalesUserId = "sales-1",
            Status = SaleOrderStatus.Backlogged,
            TotalAmount = 500m
        };
        db.SaleOrders.Add(sale);
        await db.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProceedSaleAsync(sale.Id));
        Assert.Contains("not ready to proceed", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Verify ILogger Warning
        Assert.True(logger.HasLogLevel(LogLevel.Warning));
        Assert.True(logger.HasMessage($"Cannot proceed sale order #{sale.Id}"));
    }

    [Fact]
    public async Task ExpireBackloggedOrdersAsync_CancelsExpiredBacklogsAndRestoresStockAndLogs()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var logger = new TestLogger<InventoryService>(_output);
        var service = new InventoryService(db, logger);

        var product = new Product { Name = "Headphones", Sku = "HDP-1", CurrentStock = 2, AverageCost = 50m, IsActive = true };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var saleItem = new SaleOrderItem
        {
            ProductId = product.Id,
            Product = product,
            QuantityRequested = 5,
            QuantityFulfilled = 3,
            UnitPrice = 80m,
            UnitCost = 50m
        };
        var sale = new SaleOrder
        {
            CustomerName = "Old Order",
            SalesUserId = "sales-1",
            Status = SaleOrderStatus.PartiallyBacklogged,
            Items = [saleItem],
            BacklogItems = [new BacklogItem
            {
                ProductId = product.Id,
                SaleOrderItem = saleItem,
                RemainingQuantity = 2,
                CreatedAt = DateTime.UtcNow.AddDays(-10) // expired
            }]
        };
        db.SaleOrders.Add(sale);
        await db.SaveChangesAsync();

        // Act
        var cancelledCount = await service.ExpireBackloggedOrdersAsync();

        // Assert
        Assert.Equal(1, cancelledCount);

        var updatedSale = await db.SaleOrders.FindAsync(sale.Id);
        Assert.Equal(SaleOrderStatus.Cancelled, updatedSale!.Status);
        Assert.NotNull(updatedSale.CancelledAt);
        Assert.Contains("cancelled because its backlog was not fulfilled", updatedSale.Notes);

        var updatedProduct = await db.Products.FindAsync(product.Id);
        Assert.Equal(5, updatedProduct!.CurrentStock); // 2 + 3 restored = 5

        // Verify ILogger
        Assert.True(logger.HasLogLevel(LogLevel.Information));
        Assert.True(logger.HasMessage("Cancelled 1 expired backlog order(s)"));
    }

    [Fact]
    public async Task ReceivePurchaseAsync_PurchaseBeforeBacklogExpiry_FulfillsBacklogAndDoesNotExpire()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var logger = new TestLogger<InventoryService>(_output);
        var service = new InventoryService(db, logger);

        var product = new Product { Name = "Webcam Pro", Sku = "CAM-1", CurrentStock = 0, AverageCost = 40m, IsActive = true };
        var supplier = new Supplier { Name = "Camera World", IsActive = true };
        db.Products.Add(product);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var saleItem = new SaleOrderItem { ProductId = product.Id, QuantityRequested = 3, QuantityFulfilled = 0, UnitPrice = 80m, UnitCost = 40m };
        var sale = new SaleOrder
        {
            CustomerName = "Recent Customer",
            SalesUserId = "sales-1",
            Status = SaleOrderStatus.Backlogged,
            Items = [saleItem],
            BacklogItems = [new BacklogItem
            {
                ProductId = product.Id,
                SaleOrderItem = saleItem,
                RemainingQuantity = 3,
                CreatedAt = DateTime.UtcNow.AddHours(-2) // created 2 hours ago (within 7-day policy)
            }]
        };
        db.SaleOrders.Add(sale);
        await db.SaveChangesAsync();

        // Act: purchase 3 units before expiry
        var lines = new List<(int ProductId, int Quantity, decimal UnitCost)> { (product.Id, 3, 40m) };
        var result = await service.ReceivePurchaseAsync(supplier.Id, "mgr-1", lines, "Replenish Webcam");

        // Assert
        var updatedSale = await db.SaleOrders.Include(s => s.BacklogItems).FirstAsync(s => s.Id == sale.Id);
        Assert.NotEqual(SaleOrderStatus.Cancelled, updatedSale.Status);
        Assert.Equal(SaleOrderStatus.ReadyToProceed, updatedSale.Status);
        Assert.Null(updatedSale.CancelledAt);
        Assert.True(updatedSale.BacklogItems.First().IsFulfilled);
        Assert.Equal(0, updatedSale.BacklogItems.First().RemainingQuantity);

        var updatedProduct = await db.Products.FindAsync(product.Id);
        Assert.Equal(0, updatedProduct!.CurrentStock); // 3 received - 3 allocated to backlog = 0
    }

    [Fact]
    public async Task ReceivePurchaseAsync_PurchaseAfterBacklogExpiry_ExpiresBacklogAndRetainsStock()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var logger = new TestLogger<InventoryService>(_output);
        var service = new InventoryService(db, logger);

        var product = new Product { Name = "Old Webcam", Sku = "CAM-OLD", CurrentStock = 0, AverageCost = 30m, IsActive = true };
        var supplier = new Supplier { Name = "Camera World", IsActive = true };
        db.Products.Add(product);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var saleItem = new SaleOrderItem { ProductId = product.Id, QuantityRequested = 3, QuantityFulfilled = 0, UnitPrice = 60m, UnitCost = 30m };
        var sale = new SaleOrder
        {
            CustomerName = "Lapsed Customer",
            SalesUserId = "sales-1",
            Status = SaleOrderStatus.Backlogged,
            Items = [saleItem],
            BacklogItems = [new BacklogItem
            {
                ProductId = product.Id,
                SaleOrderItem = saleItem,
                RemainingQuantity = 3,
                CreatedAt = DateTime.UtcNow.AddDays(-10) // 10 days ago (older than 7-day policy, expired)
            }]
        };
        db.SaleOrders.Add(sale);
        await db.SaveChangesAsync();

        // Act: purchase arrives after expiry
        var lines = new List<(int ProductId, int Quantity, decimal UnitCost)> { (product.Id, 3, 30m) };
        var result = await service.ReceivePurchaseAsync(supplier.Id, "mgr-1", lines, "Late replenishment");

        // Assert: order was expired/cancelled, not fulfilled
        var updatedSale = await db.SaleOrders.Include(s => s.BacklogItems).FirstAsync(s => s.Id == sale.Id);
        Assert.Equal(SaleOrderStatus.Cancelled, updatedSale.Status);
        Assert.NotNull(updatedSale.CancelledAt);
        Assert.False(updatedSale.BacklogItems.First().IsFulfilled);

        // Product stock remains in inventory because the backlog was already expired
        var updatedProduct = await db.Products.FindAsync(product.Id);
        Assert.Equal(3, updatedProduct!.CurrentStock);
    }

    [Fact]
    public async Task ExpireBackloggedOrdersAsync_NonPositiveCancellationDays_DoesNotCancelAnyOrders()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var logger = new TestLogger<InventoryService>(_output);
        var service = new InventoryService(db, logger);

        var originalPolicy = BacklogPolicy.CancellationDays;
        try
        {
            BacklogPolicy.CancellationDays = 0; // disabled

            var product = new Product { Name = "Microphone", Sku = "MIC-1", CurrentStock = 0, AverageCost = 20m, IsActive = true };
            db.Products.Add(product);
            await db.SaveChangesAsync();

            var saleItem = new SaleOrderItem { ProductId = product.Id, QuantityRequested = 2, QuantityFulfilled = 0, UnitPrice = 50m, UnitCost = 20m };
            var sale = new SaleOrder
            {
                CustomerName = "Active Backlog",
                SalesUserId = "sales-1",
                Status = SaleOrderStatus.Backlogged,
                Items = [saleItem],
                BacklogItems = [new BacklogItem
                {
                    ProductId = product.Id,
                    SaleOrderItem = saleItem,
                    RemainingQuantity = 2,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                }]
            };
            db.SaleOrders.Add(sale);
            await db.SaveChangesAsync();

            // Act
            var cancelledCount = await service.ExpireBackloggedOrdersAsync();

            // Assert
            Assert.Equal(0, cancelledCount);
            var updatedSale = await db.SaleOrders.FindAsync(sale.Id);
            Assert.Equal(SaleOrderStatus.Backlogged, updatedSale!.Status);
        }
        finally
        {
            BacklogPolicy.CancellationDays = originalPolicy;
        }
    }
}
