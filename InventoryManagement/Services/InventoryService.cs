using InventoryManagement.Data;
using InventoryManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Services;

public class PurchaseReceiveResult
{
    public int PurchaseOrderId { get; set; }
    public List<string> Messages { get; set; } = [];
}

public interface IInventoryService
{
    Task<PurchaseReceiveResult> ReceivePurchaseAsync(
        int supplierId,
        string managerUserId,
        IEnumerable<(int ProductId, int Quantity, decimal UnitCost)> lines,
        string? notes,
        DateTime? orderDate = null);

    Task<SaleOrder> PlaceSaleAsync(
        string salesUserId,
        string customerName,
        IEnumerable<(int ProductId, int Quantity, decimal UnitPrice)> lines,
        string? notes);

    Task ProceedSaleAsync(int saleOrderId);
    Task<int> ExpireBackloggedOrdersAsync();
}

public class InventoryService(ApplicationDbContext db) : IInventoryService
{
    public async Task<PurchaseReceiveResult> ReceivePurchaseAsync(
        int supplierId,
        string managerUserId,
        IEnumerable<(int ProductId, int Quantity, decimal UnitCost)> lines,
        string? notes,
        DateTime? orderDate = null)
    {
        await ExpireBackloggedOrdersAsync();

        var items = lines.Where(l => l.ProductId > 0 && l.Quantity > 0).ToList();
        if (items.Count == 0)
        {
            throw new InvalidOperationException("Add at least one purchase line.");
        }

        var purchase = new PurchaseOrder
        {
            SupplierId = supplierId,
            CreatedByUserId = managerUserId,
            OrderDate = orderDate ?? DateTime.UtcNow,
            Notes = notes
        };

        foreach (var line in items)
        {
            var product = await db.Products.FindAsync(line.ProductId)
                ?? throw new InvalidOperationException("Product not found.");

            var previousStock = product.CurrentStock;
            var incoming = line.Quantity;

            product.AverageCost = previousStock + incoming > 0
                ? ((product.AverageCost * previousStock) + (line.UnitCost * incoming)) / (previousStock + incoming)
                : line.UnitCost;

            product.CurrentStock += incoming;

            purchase.Items.Add(new PurchaseOrderItem
            {
                ProductId = product.Id,
                Quantity = incoming,
                UnitCost = line.UnitCost
            });
        }

        purchase.TotalAmount = purchase.Items.Sum(i => i.Quantity * i.UnitCost);
        db.PurchaseOrders.Add(purchase);
        await db.SaveChangesAsync();

        var messages = new List<string> { $"Purchase #{purchase.Id} received. Incoming stock applied." };

        foreach (var productId in items.Select(i => i.ProductId).Distinct())
        {
            var product = await db.Products.FindAsync(productId);
            if (product is null)
            {
                continue;
            }

            await FulfillBacklogsForProductAsync(product, purchase.Id, messages);
        }

        await db.SaveChangesAsync();

        if (messages.Count == 1)
        {
            messages.Add("No open backlog for these products. Current stock is ready for new sales.");
        }

        return new PurchaseReceiveResult
        {
            PurchaseOrderId = purchase.Id,
            Messages = messages
        };
    }

    public async Task<SaleOrder> PlaceSaleAsync(
        string salesUserId,
        string customerName,
        IEnumerable<(int ProductId, int Quantity, decimal UnitPrice)> lines,
        string? notes)
    {
        var items = lines.Where(l => l.ProductId > 0 && l.Quantity > 0 && l.UnitPrice >= 0).ToList();
        if (items.Count == 0)
        {
            throw new InvalidOperationException("Add at least one sale line with a selling price.");
        }

        var sale = new SaleOrder
        {
            SalesUserId = salesUserId,
            CustomerName = customerName,
            OrderDate = DateTime.UtcNow,
            Notes = notes
        };

        foreach (var line in items)
        {
            var product = await db.Products.FindAsync(line.ProductId)
                ?? throw new InvalidOperationException("Product not found.");

            var requested = line.Quantity;
            var fulfillNow = Math.Min(requested, Math.Max(product.CurrentStock, 0));
            product.CurrentStock -= fulfillNow;

            var saleItem = new SaleOrderItem
            {
                ProductId = product.Id,
                QuantityRequested = requested,
                QuantityFulfilled = fulfillNow,
                UnitPrice = line.UnitPrice,
                UnitCost = product.AverageCost
            };
            sale.Items.Add(saleItem);

            var pending = requested - fulfillNow;
            if (pending > 0)
            {
                sale.BacklogItems.Add(new BacklogItem
                {
                    ProductId = product.Id,
                    SaleOrderItem = saleItem,
                    RemainingQuantity = pending,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        RecalculateSale(sale, createdNow: true);
        db.SaleOrders.Add(sale);
        await db.SaveChangesAsync();
        return sale;
    }

    public async Task ProceedSaleAsync(int saleOrderId)
    {
        await ExpireBackloggedOrdersAsync();

        var sale = await db.SaleOrders
            .Include(s => s.BacklogItems)
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == saleOrderId)
            ?? throw new InvalidOperationException("Sale not found.");

        if (sale.Status != SaleOrderStatus.ReadyToProceed)
        {
            throw new InvalidOperationException("Order is not ready to proceed. Open backlog must be fulfilled first.");
        }

        sale.Status = SaleOrderStatus.Completed;
        sale.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task<int> ExpireBackloggedOrdersAsync()
    {
        var cutoff = DateTime.UtcNow.AddDays(-BacklogPolicy.CancellationDays);
        var expiredOrders = await db.SaleOrders
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .Include(s => s.BacklogItems)
            .AsSplitQuery()
            .Where(s => (s.Status == SaleOrderStatus.Backlogged || s.Status == SaleOrderStatus.PartiallyBacklogged)
                && s.BacklogItems.Any(b => !b.IsFulfilled && b.RemainingQuantity > 0 && b.CreatedAt <= cutoff))
            .ToListAsync();

        foreach (var sale in expiredOrders)
        {
            foreach (var item in sale.Items.Where(i => i.QuantityFulfilled > 0))
            {
                item.Product.CurrentStock += item.QuantityFulfilled;
                item.QuantityFulfilled = 0;
            }

            sale.Status = SaleOrderStatus.Cancelled;
            sale.CancelledAt = DateTime.UtcNow;
            sale.TotalCost = 0;
            sale.Notes = AppendCancellationNote(sale.Notes);
        }

        if (expiredOrders.Count > 0)
        {
            await db.SaveChangesAsync();
        }

        return expiredOrders.Count;
    }

    private async Task FulfillBacklogsForProductAsync(Product product, int purchaseOrderId, List<string> messages)
    {
        var open = await db.BacklogItems
            .Include(b => b.SaleOrderItem)
            .Include(b => b.SaleOrder)
                .ThenInclude(s => s.Items)
            .Where(b => b.ProductId == product.Id
                && b.SaleOrder.Status != SaleOrderStatus.Cancelled
                && !b.IsFulfilled
                && b.RemainingQuantity > 0)
            .OrderBy(b => b.CreatedAt)
            .ToListAsync();

        foreach (var backlog in open)
        {
            if (product.CurrentStock <= 0)
            {
                break;
            }

            var allocate = Math.Min(backlog.RemainingQuantity, product.CurrentStock);
            product.CurrentStock -= allocate;
            backlog.RemainingQuantity -= allocate;
            backlog.SaleOrderItem.QuantityFulfilled += allocate;
            backlog.SaleOrderItem.UnitCost = product.AverageCost;

            if (backlog.RemainingQuantity == 0)
            {
                backlog.IsFulfilled = true;
                backlog.FulfilledAt = DateTime.UtcNow;
            }

            RecalculateSale(backlog.SaleOrder, createdNow: false);

            var message = backlog.IsFulfilled
                ? $"Backlog for sale #{backlog.SaleOrderId} ({product.Name}) is fulfilled. Allocated {allocate} units. Remaining stock: {product.CurrentStock}. Order can proceed."
                : $"Backlog for sale #{backlog.SaleOrderId} ({product.Name}) partially filled. Allocated {allocate}, still waiting {backlog.RemainingQuantity}. Remaining stock: {product.CurrentStock}.";

            db.BacklogFulfillmentLogs.Add(new BacklogFulfillmentLog
            {
                PurchaseOrderId = purchaseOrderId,
                ProductId = product.Id,
                BacklogItemId = backlog.Id,
                SaleOrderId = backlog.SaleOrderId,
                QuantityAllocated = allocate,
                RemainingStockAfter = product.CurrentStock,
                BacklogFullyFulfilled = backlog.IsFulfilled,
                Message = message
            });

            messages.Add(message);
        }
    }

    private static void RecalculateSale(SaleOrder sale, bool createdNow)
    {
        sale.TotalAmount = sale.Items.Sum(i => i.QuantityRequested * i.UnitPrice);
        sale.TotalCost = sale.Items.Sum(i => i.QuantityFulfilled * i.UnitCost);

        var pending = sale.Items.Sum(i => i.QuantityPending);
        var fulfilled = sale.Items.Sum(i => i.QuantityFulfilled);

        if (pending > 0)
        {
            sale.Status = fulfilled == 0 ? SaleOrderStatus.Backlogged : SaleOrderStatus.PartiallyBacklogged;
            return;
        }

        if (createdNow && sale.BacklogItems.Count == 0)
        {
            sale.Status = SaleOrderStatus.Completed;
            sale.CompletedAt = DateTime.UtcNow;
            return;
        }

        if (sale.Status != SaleOrderStatus.Completed)
        {
            sale.Status = SaleOrderStatus.ReadyToProceed;
        }
    }

    private static string AppendCancellationNote(string? notes)
    {
        const string cancellation = "Order automatically cancelled because its backlog was not fulfilled within the allowed period.";
        return string.IsNullOrWhiteSpace(notes)
            ? cancellation
            : $"{notes}\n{cancellation}"[..400];
    }
}
