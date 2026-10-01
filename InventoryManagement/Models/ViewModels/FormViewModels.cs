using System.ComponentModel.DataAnnotations;
using InventoryManagement.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InventoryManagement.Models.ViewModels;

public class UserListItemViewModel
{
    public ApplicationUser User { get; set; } = null!;
    public string Role { get; set; } = "";
}

public class UserFormViewModel
{
    public string? Id { get; set; }

    [Required, Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Role")]
    [Required]
    public string Role { get; set; } = AppRoles.SalesTeam;

    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public IEnumerable<SelectListItem> RoleOptions { get; set; } =
    [
        new(AppRoles.DisplayName(AppRoles.InventoryManager), AppRoles.InventoryManager),
        new(AppRoles.DisplayName(AppRoles.SalesTeam), AppRoles.SalesTeam)
    ];
}

public class PurchaseOrderFormViewModel
{
    [Required]
    public int SupplierId { get; set; }

    public string? Notes { get; set; }

    public List<PurchaseLineViewModel> Lines { get; set; } = [new()];

    public IEnumerable<SelectListItem> Suppliers { get; set; } = [];
    public IEnumerable<SelectListItem> Products { get; set; } = [];
}

public class PurchaseLineViewModel
{
    public int ProductId { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitCost { get; set; }
}

public class SaleOrderFormViewModel
{
    [Required, Display(Name = "Customer")]
    public string CustomerName { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public List<SaleLineViewModel> Lines { get; set; } = [new()];

    public IEnumerable<SelectListItem> Products { get; set; } = [];
}

public class SaleLineViewModel
{
    public int ProductId { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
}

public class ChartPayload
{
    public List<string> Labels { get; set; } = [];
    public List<decimal> Values { get; set; } = [];
}

public class AdminDashboardViewModel
{
    public int UserCount { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalPurchases { get; set; }
    public decimal TotalProfit { get; set; }
    public string MostSoldItem { get; set; } = "—";
    public int MostSoldQty { get; set; }
    public int OpenBacklogs { get; set; }
    public int LowStockCount { get; set; }
}

public class InventoryDashboardViewModel
{
    public decimal TotalPurchases { get; set; }
    public int PurchaseCount { get; set; }
    public int LowStockCount { get; set; }
    public int OpenBacklogs { get; set; }
    public List<Product> LowStockProducts { get; set; } = [];
}

public class SalesDashboardViewModel
{
    public decimal TotalSales { get; set; }
    public decimal TotalProfit { get; set; }
    public int OrderCount { get; set; }
    public int OpenBacklogs { get; set; }
    public string MostSoldItem { get; set; } = "—";
}
