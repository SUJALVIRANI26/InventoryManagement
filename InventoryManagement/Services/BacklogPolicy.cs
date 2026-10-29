namespace InventoryManagement.Services;

public static class BacklogPolicy
{
    public const int DefaultCancellationDays = 7;
    public static int CancellationDays { get; set; } = DefaultCancellationDays;
}

