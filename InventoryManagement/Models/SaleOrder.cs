using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryManagement.Models;

public enum SaleOrderStatus
{
    Completed = 0,
    Backlogged = 1,
    PartiallyBacklogged = 2,
    ReadyToProceed = 3,
    Cancelled = 4
}

public class SaleOrder
{
    public int Id { get; set; }

    [Required]
    public string SalesUserId { get; set; } = string.Empty;
    public ApplicationUser SalesUser { get; set; } = null!;

    [Required, StringLength(120)]
    public string CustomerName { get; set; } = string.Empty;

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public SaleOrderStatus Status { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCost { get; set; }

    [NotMapped]
    public decimal Profit => TotalAmount - TotalCost;

    [StringLength(400)]
    public string? Notes { get; set; }

    public ICollection<SaleOrderItem> Items { get; set; } = [];
    public ICollection<BacklogItem> BacklogItems { get; set; } = [];
}

public class SaleOrderItem
{
    public int Id { get; set; }

    public int SaleOrderId { get; set; }
    public SaleOrder SaleOrder { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int QuantityRequested { get; set; }
    public int QuantityFulfilled { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitCost { get; set; }

    [NotMapped]
    public int QuantityPending => QuantityRequested - QuantityFulfilled;

    [NotMapped]
    public decimal LineTotal => QuantityRequested * UnitPrice;
}
