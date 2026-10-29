using InventoryManagement.Controllers;
using InventoryManagement.Models;
using InventoryManagement.Models.ViewModels;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using TestProject1.Helpers;
using Xunit.Abstractions;

namespace TestProject1.Controllers;

public class SaleOrdersControllerTests
{
    private readonly ITestOutputHelper _output;

    public SaleOrdersControllerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static Mock<UserManager<ApplicationUser>> CreateMockUserManager(ApplicationUser? currentUser = null)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var mgr = new Mock<UserManager<ApplicationUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        if (currentUser != null)
        {
            mgr.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(currentUser);
        }
        return mgr;
    }

    [Fact]
    public async Task Create_ValidSale_PlacesSaleRedirectsAndLogsInformation()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var product = new Product { Name = "Desktop PC", Sku = "PC-1", CurrentStock = 10, SuggestedPrice = 1000m, IsActive = true };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var testUser = new ApplicationUser { Id = "sales-usr-1", UserName = "seller@test.com" };
        var userManager = CreateMockUserManager(testUser);

        var mockInventory = new Mock<IInventoryService>();
        var createdSale = new SaleOrder
        {
            Id = 101,
            CustomerName = "Acme Corp",
            Status = SaleOrderStatus.Completed,
            SalesUserId = testUser.Id
        };
        mockInventory
            .Setup(s => s.PlaceSaleAsync(testUser.Id, "Acme Corp", It.IsAny<IEnumerable<(int, int, decimal)>>(), "Urgent"))
            .ReturnsAsync(createdSale);

        var logger = new TestLogger<SaleOrdersController>(_output);
        var controller = new SaleOrdersController(db, mockInventory.Object, userManager.Object, logger)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var model = new SaleOrderFormViewModel
        {
            CustomerName = "Acme Corp",
            Notes = "Urgent",
            Lines = [new SaleLineViewModel { ProductId = product.Id, Quantity = 2, UnitPrice = 1000m }]
        };

        // Act
        var result = await controller.Create(model);

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SaleOrdersController.Details), redirect.ActionName);
        Assert.Equal(101, redirect.RouteValues?["id"]);
        Assert.Contains("completed", controller.TempData["Status"]?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);

        // Verify ILogger
        Assert.True(logger.HasLogLevel(LogLevel.Information));
        Assert.True(logger.HasMessage("Placing sale order for customer 'Acme Corp'"));
        Assert.True(logger.HasMessage("Sale order #101 created with status 'Completed'"));
    }

    [Fact]
    public async Task Create_InvalidModelState_ReturnsViewAndLogsWarning()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var userManager = CreateMockUserManager(new ApplicationUser { Id = "u1" });
        var mockInventory = new Mock<IInventoryService>();
        var logger = new TestLogger<SaleOrdersController>(_output);

        var controller = new SaleOrdersController(db, mockInventory.Object, userManager.Object, logger);
        controller.ModelState.AddModelError("CustomerName", "Customer name is required");

        var model = new SaleOrderFormViewModel();

        // Act
        var result = await controller.Create(model);

        // Assert
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(model, view.Model);

        // Verify ILogger
        Assert.True(logger.HasLogLevel(LogLevel.Warning));
        Assert.True(logger.HasMessage("Sale order creation validation failed"));
    }

    [Fact]
    public async Task Create_InventoryServiceThrows_AddsModelErrorAndLogsWarning()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var product = new Product { Name = "Printer", Sku = "PRN-1", CurrentStock = 2, IsActive = true };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var testUser = new ApplicationUser { Id = "u1" };
        var userManager = CreateMockUserManager(testUser);

        var mockInventory = new Mock<IInventoryService>();
        mockInventory
            .Setup(s => s.PlaceSaleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<(int, int, decimal)>>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Add at least one sale line with a selling price."));

        var logger = new TestLogger<SaleOrdersController>(_output);
        var controller = new SaleOrdersController(db, mockInventory.Object, userManager.Object, logger);

        var model = new SaleOrderFormViewModel
        {
            CustomerName = "Test Co",
            Lines = [new SaleLineViewModel { ProductId = product.Id, Quantity = 1, UnitPrice = 50m }]
        };

        // Act
        var result = await controller.Create(model);

        // Assert
        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);

        // Verify ILogger Warning
        Assert.True(logger.HasLogLevel(LogLevel.Warning));
        Assert.True(logger.HasMessage("InvalidOperationException while placing sale order for customer 'Test Co'"));
    }

    [Fact]
    public async Task Proceed_NonOwner_ReturnsNotFoundAndLogsWarning()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var testUser = new ApplicationUser { Id = "sales-user-1" };
        var userManager = CreateMockUserManager(testUser);
        var mockInventory = new Mock<IInventoryService>();
        var logger = new TestLogger<SaleOrdersController>(_output);

        var sale = new SaleOrder
        {
            Id = 55,
            SalesUserId = "different-user",
            CustomerName = "Someone else"
        };
        db.SaleOrders.Add(sale);
        await db.SaveChangesAsync();

        var controller = new SaleOrdersController(db, mockInventory.Object, userManager.Object, logger);

        // Act
        var result = await controller.Proceed(55);

        // Assert
        Assert.IsType<NotFoundResult>(result);

        // Verify ILogger Warning
        Assert.True(logger.HasLogLevel(LogLevel.Warning));
        Assert.True(logger.HasMessage("attempted to proceed sale order #55 without ownership"));
    }

    [Fact]
    public async Task Proceed_OwnerAndReady_CallsServiceRedirectsAndLogsInformation()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var testUser = new ApplicationUser { Id = "sales-user-1" };
        var userManager = CreateMockUserManager(testUser);
        var mockInventory = new Mock<IInventoryService>();
        var logger = new TestLogger<SaleOrdersController>(_output);

        var sale = new SaleOrder
        {
            Id = 77,
            SalesUserId = testUser.Id,
            CustomerName = "My Customer",
            Status = SaleOrderStatus.ReadyToProceed
        };
        db.SaleOrders.Add(sale);
        await db.SaveChangesAsync();

        var controller = new SaleOrdersController(db, mockInventory.Object, userManager.Object, logger)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        // Act
        var result = await controller.Proceed(77);

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SaleOrdersController.Details), redirect.ActionName);
        mockInventory.Verify(s => s.ProceedSaleAsync(77), Times.Once);

        // Verify ILogger
        Assert.True(logger.HasLogLevel(LogLevel.Information));
        Assert.True(logger.HasMessage("User 'sales-user-1' proceeding sale order #77."));
    }
}
