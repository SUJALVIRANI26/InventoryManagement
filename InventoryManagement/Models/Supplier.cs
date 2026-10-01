using System.ComponentModel.DataAnnotations;

namespace InventoryManagement.Models;

public class Supplier
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(80)]
    public string ContactPerson { get; set; } = string.Empty;

    [StringLength(40)]
    public string Phone { get; set; } = string.Empty;

    [EmailAddress, StringLength(120)]
    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = [];
}
