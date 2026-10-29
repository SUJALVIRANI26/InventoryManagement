using InventoryManagement.Controllers;
using InventoryManagement.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TestProject1.Helpers;
using Xunit.Abstractions;

namespace TestProject1.Controllers;

public class ProductsControllerTests
{
    private readonly ITestOutputHelper _output;

    public ProductsControllerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Index_ReturnsViewWithProductListAndLogsInformation()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var logger = new TestLogger<ProductsController>(_output);
        var controller = new ProductsController(db, logger);

        db.Products.Add(new Product { Name = "Headphones", Sku = "HDP-1", CurrentStock = 5, IsActive = true });
        await db.SaveChangesAsync();

        // Act
        var result = await controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<Product>>(viewResult.Model);
        Assert.Single(model);

        // Verify ILogger
        Assert.True(logger.HasLogLevel(LogLevel.Information));
        Assert.True(logger.HasMessage("Retrieving all products"));
    }

    [Fact]
    public async Task Create_ValidProduct_SavesAndRedirectsAndLogsInformation()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var logger = new TestLogger<ProductsController>(_output);
        var controller = new ProductsController(db, logger);

        var newProduct = new Product
        {
            Name = "Webcam",
            Sku = "CAM-1",
            SuggestedPrice = 45m,
            ReorderLevel = 5,
            IsActive = true
        };

        // Act
        var result = await controller.Create(newProduct);

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ProductsController.Index), redirect.ActionName);

        var saved = db.Products.FirstOrDefault(p => p.Sku == "CAM-1");
        Assert.NotNull(saved);
        Assert.Equal("Webcam", saved.Name);

        // Verify ILogger
        Assert.True(logger.HasLogLevel(LogLevel.Information));
        Assert.True(logger.HasMessage("Successfully created product 'Webcam'"));
    }

    [Fact]
    public async Task Create_InvalidModelState_ReturnsViewAndLogsWarning()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var logger = new TestLogger<ProductsController>(_output);
        var controller = new ProductsController(db, logger);
        controller.ModelState.AddModelError("Name", "Name is required");

        var product = new Product { Sku = "BAD-1" };

        // Act
        var result = await controller.Create(product);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal(product, viewResult.Model);

        // Verify ILogger Warning
        Assert.True(logger.HasLogLevel(LogLevel.Warning));
        Assert.True(logger.HasMessage("Failed to create product"));
    }

    [Fact]
    public async Task Edit_Get_ProductNotFound_ReturnsNotFoundAndLogsWarning()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var logger = new TestLogger<ProductsController>(_output);
        var controller = new ProductsController(db, logger);

        // Act
        var result = await controller.Edit(999);

        // Assert
        Assert.IsType<NotFoundResult>(result);

        // Verify ILogger Warning
        Assert.True(logger.HasLogLevel(LogLevel.Warning));
        Assert.True(logger.HasMessage("Product with ID 999 not found"));
    }

    [Fact]
    public async Task Edit_Post_IdMismatch_ReturnsBadRequestAndLogsWarning()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var logger = new TestLogger<ProductsController>(_output);
        var controller = new ProductsController(db, logger);

        var product = new Product { Id = 1, Name = "Item", Sku = "ITM-1" };

        // Act
        var result = await controller.Edit(2, product);

        // Assert
        Assert.IsType<BadRequestResult>(result);

        // Verify ILogger Warning
        Assert.True(logger.HasLogLevel(LogLevel.Warning));
        Assert.True(logger.HasMessage("Product ID mismatch on Edit"));
    }

    [Fact]
    public async Task Edit_Post_ValidUpdate_UpdatesAndRedirectsAndLogsInformation()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var logger = new TestLogger<ProductsController>(_output);
        var controller = new ProductsController(db, logger);

        var product = new Product { Name = "Old Name", Sku = "SKU-99", SuggestedPrice = 10m, IsActive = true };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        product.Name = "New Updated Name";

        // Act
        var result = await controller.Edit(product.Id, product);

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ProductsController.Index), redirect.ActionName);

        // Verify ILogger
        Assert.True(logger.HasLogLevel(LogLevel.Information));
        Assert.True(logger.HasMessage("Successfully updated product 'New Updated Name'"));
    }
}
