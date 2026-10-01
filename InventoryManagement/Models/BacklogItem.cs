using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryManagement.Models;

public class BacklogItem
{
    public int Id { get; set; }

    public int SaleOrderId { get; set; }
    public SaleOrder SaleOrder { get; set; } = null!;

    public int SaleOrderItemId { get; set; }
    public SaleOrderItem SaleOrderItem { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int RemainingQuantity { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FulfilledAt { get; set; }

    public bool IsFulfilled { get; set; }

    [NotMapped]
    public bool IsOpen => !IsFulfilled && RemainingQuantity > 0;
}
