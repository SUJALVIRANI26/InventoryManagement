using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryManagement.Models;

public class Product
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(40)]
    public string Sku { get; set; } = string.Empty;

    public int CurrentStock { get; set; }

    [Display(Name = "Low stock alert at")]
    public int ReorderLevel { get; set; } = 10;

    [Column(TypeName = "decimal(18,2)")]
    public decimal AverageCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SuggestedPrice { get; set; }

    public bool IsActive { get; set; } = true;

    [NotMapped]
    public bool IsLowStock => CurrentStock <= ReorderLevel;

    public ICollection<PurchaseOrderItem> PurchaseItems { get; set; } = [];
    public ICollection<SaleOrderItem> SaleItems { get; set; } = [];
    public ICollection<BacklogItem> BacklogItems { get; set; } = [];
}
