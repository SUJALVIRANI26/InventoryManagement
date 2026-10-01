using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryManagement.Models;

public class PurchaseOrder
{
    public int Id { get; set; }

    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    [Required]
    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser CreatedBy { get; set; } = null!;

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [StringLength(400)]
    public string? Notes { get; set; }

    public ICollection<PurchaseOrderItem> Items { get; set; } = [];
}

public class PurchaseOrderItem
{
    public int Id { get; set; }

    public int PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitCost { get; set; }

    [NotMapped]
    public decimal LineTotal => Quantity * UnitCost;
}
