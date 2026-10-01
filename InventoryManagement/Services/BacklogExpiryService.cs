namespace InventoryManagement.Services;

public sealed class BacklogExpiryService(IServiceScopeFactory scopeFactory, ILogger<BacklogExpiryService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var inventory = scope.ServiceProvider.GetRequiredService<IInventoryService>();
                var cancelled = await inventory.ExpireBackloggedOrdersAsync();
                if (cancelled > 0)
                {
                    logger.LogInformation("Automatically cancelled {CancelledOrders} expired backlog order(s).", cancelled);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Could not process expired backlog orders.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }
}
