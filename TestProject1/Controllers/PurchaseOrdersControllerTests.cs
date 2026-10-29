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

public class PurchaseOrdersControllerTests
{
    private readonly ITestOutputHelper _output;

    public PurchaseOrdersControllerTests(ITestOutputHelper output)
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
    public async Task Create_ValidPurchase_ReceivesPurchaseRedirectsAndLogsInformation()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var supplier = new Supplier { Id = 1, Name = "Best Supplier", IsActive = true };
        var product = new Product { Id = 1, Name = "Hard Drive", Sku = "HD-1", IsActive = true };
        db.Suppliers.Add(supplier);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var testUser = new ApplicationUser { Id = "mgr-usr-1", UserName = "manager@test.com" };
        var userManager = CreateMockUserManager(testUser);

        var mockInventory = new Mock<IInventoryService>();
        mockInventory
            .Setup(s => s.ReceivePurchaseAsync(1, testUser.Id, It.IsAny<IEnumerable<(int, int, decimal)>>(), "Stock replenishment", null))
            .ReturnsAsync(new PurchaseReceiveResult
            {
                PurchaseOrderId = 202,
                Messages = ["Purchase #202 received."]
            });

        var logger = new TestLogger<PurchaseOrdersController>(_output);
        var controller = new PurchaseOrdersController(db, mockInventory.Object, userManager.Object, logger)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var model = new PurchaseOrderFormViewModel
        {
            SupplierId = 1,
            Notes = "Stock replenishment",
            Lines = [new PurchaseLineViewModel { ProductId = product.Id, Quantity = 5, UnitCost = 40m }]
        };

        // Act
        var result = await controller.Create(model);

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PurchaseOrdersController.Details), redirect.ActionName);
        Assert.Equal(202, redirect.RouteValues?["id"]);

        // Verify ILogger
        Assert.True(logger.HasLogLevel(LogLevel.Information));
        Assert.True(logger.HasMessage("Receiving purchase order for supplier 1 by user mgr-usr-1"));
        Assert.True(logger.HasMessage("Purchase order #202 created successfully"));
    }

    [Fact]
    public async Task Create_MissingSupplier_AddsModelErrorReturnsViewAndLogsWarning()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var userManager = CreateMockUserManager();
        var mockInventory = new Mock<IInventoryService>();
        var logger = new TestLogger<PurchaseOrdersController>(_output);

        var controller = new PurchaseOrdersController(db, mockInventory.Object, userManager.Object, logger);
        var model = new PurchaseOrderFormViewModel { SupplierId = 0 };

        // Act
        var result = await controller.Create(model);

        // Assert
        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey(nameof(model.SupplierId)));

        // Verify ILogger
        Assert.True(logger.HasLogLevel(LogLevel.Warning));
        Assert.True(logger.HasMessage("Purchase order validation failed for supplier 0"));
    }

    [Fact]
    public async Task Details_OrderNotFound_ReturnsNotFound()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var userManager = CreateMockUserManager();
        var mockInventory = new Mock<IInventoryService>();
        var logger = new TestLogger<PurchaseOrdersController>(_output);

        var controller = new PurchaseOrdersController(db, mockInventory.Object, userManager.Object, logger);

        // Act
        var result = await controller.Details(999);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }
}
