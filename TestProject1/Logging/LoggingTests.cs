using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TestProject1.Helpers;
using Xunit.Abstractions;

namespace TestProject1.Logging;

public class SampleServiceWithLogger
{
    private readonly ILogger<SampleServiceWithLogger> _logger;

    public SampleServiceWithLogger(ILogger<SampleServiceWithLogger> logger)
    {
        _logger = logger;
    }

    public void ProcessOrder(int orderId, string customer, decimal total)
    {
        _logger.LogInformation("Processing order #{OrderId} for customer '{Customer}' with total {Total:C}.", orderId, customer, total);
    }

    public void CheckInventory(string sku, int currentStock, int threshold)
    {
        if (currentStock <= threshold)
        {
            _logger.LogWarning("Low inventory warning for SKU {Sku}. Current: {CurrentStock}, Threshold: {Threshold}.", sku, currentStock, threshold);
        }
    }

    public void FailOperation(string operation)
    {
        try
        {
            throw new InvalidOperationException($"Operation '{operation}' failed due to system state.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute {OperationName}.", operation);
            throw;
        }
    }
}

public class LoggingTests
{
    private readonly ITestOutputHelper _output;

    public LoggingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TestLogger_CapturesInformationLog_WithStructuredData()
    {
        // Arrange
        var logger = new TestLogger<SampleServiceWithLogger>(_output);
        var service = new SampleServiceWithLogger(logger);

        // Act
        service.ProcessOrder(101, "Acme", 499.99m);

        // Assert
        Assert.Single(logger.Logs);
        var entry = logger.Logs[0];
        Assert.Equal(LogLevel.Information, entry.LogLevel);
        Assert.Contains("Processing order #101 for customer 'Acme'", entry.Message);
        Assert.True(logger.HasMessage("Processing order #101"));
        Assert.True(logger.HasLogLevel(LogLevel.Information));
        Assert.False(logger.HasLogLevel(LogLevel.Error));
    }

    [Fact]
    public void TestLogger_CapturesWarningLog_WhenStockBelowThreshold()
    {
        // Arrange
        var logger = new TestLogger<SampleServiceWithLogger>(_output);
        var service = new SampleServiceWithLogger(logger);

        // Act
        service.CheckInventory("SKU-99", 2, 5);

        // Assert
        Assert.True(logger.HasLogLevel(LogLevel.Warning));
        Assert.True(logger.HasMessage("Low inventory warning for SKU SKU-99"));
        Assert.Equal(1, logger.Count(LogLevel.Warning));
    }

    [Fact]
    public void TestLogger_CapturesExceptionAndErrorLog()
    {
        // Arrange
        var logger = new TestLogger<SampleServiceWithLogger>(_output);
        var service = new SampleServiceWithLogger(logger);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => service.FailOperation("DataSync"));

        // Assert
        Assert.True(logger.HasLogLevel(LogLevel.Error));
        var errorEntry = logger.Logs.First(l => l.LogLevel == LogLevel.Error);
        Assert.NotNull(errorEntry.Exception);
        Assert.IsType<InvalidOperationException>(errorEntry.Exception);
        Assert.Contains("Failed to execute DataSync", errorEntry.Message);
    }

    [Fact]
    public void DependencyInjection_CanResolveAndInjectLogger()
    {
        // Arrange
        var services = new ServiceCollection();
        var testLogger = new TestLogger<SampleServiceWithLogger>(_output);

        services.AddSingleton<ILogger<SampleServiceWithLogger>>(testLogger);
        services.AddTransient<SampleServiceWithLogger>();

        using var provider = services.BuildServiceProvider();

        // Act
        var resolvedService = provider.GetRequiredService<SampleServiceWithLogger>();
        resolvedService.ProcessOrder(202, "Global Tech", 1500m);

        // Assert
        Assert.True(testLogger.HasMessage("Processing order #202 for customer 'Global Tech'"));
        Assert.True(testLogger.HasLogLevel(LogLevel.Information));
    }
}
