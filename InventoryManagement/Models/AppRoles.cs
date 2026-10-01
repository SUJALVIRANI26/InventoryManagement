namespace InventoryManagement.Models;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string InventoryManager = "InventoryManager";
    public const string SalesTeam = "SalesTeam";

    public static readonly string[] All = [Admin, InventoryManager, SalesTeam];

    public static string DisplayName(string role) => role switch
    {
        Admin => "Admin",
        InventoryManager => "Inventory Manager",
        SalesTeam => "Sales Team",
        _ => role
    };
}
