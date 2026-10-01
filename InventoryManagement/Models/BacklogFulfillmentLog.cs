namespace InventoryManagement.Models;

public class BacklogFulfillmentLog
{
    public int Id { get; set; }
    public int PurchaseOrderId { get; set; }
    public int BacklogItemId { get; set; }
    public int SaleOrderId { get; set; }
    public int ProductId { get; set; }
    public int QuantityAllocated { get; set; }
    public int RemainingStockAfter { get; set; }
    public bool BacklogFullyFulfilled { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Message { get; set; } = string.Empty;
}
