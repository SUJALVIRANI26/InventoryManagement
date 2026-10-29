using System.Security.Claims;
using InventoryManagement.Controllers;
using InventoryManagement.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TestProject1.Helpers;
using Xunit.Abstractions;

namespace TestProject1.Controllers;

public class BacklogControllerTests
{
    private readonly ITestOutputHelper _output;

    public BacklogControllerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Index_AdminRole_ReturnsAllBacklogItemsAndLogsInformation()
    {
        using var db = TestDbContextFactory.Create();
        var product = new Product { Name = "Gadget", Sku = "GDG-01", CurrentStock = 0 };
        db.Products.Add(product);

        var sale = new SaleOrder { CustomerName = "John", SalesUserId = "user-1", Status = SaleOrderStatus.Backlogged };
        db.SaleOrders.Add(sale);
        await db.SaveChangesAsync();

        var backlog = new BacklogItem
        {
            ProductId = product.Id,
            SaleOrderId = sale.Id,
            RemainingQuantity = 5,
            IsFulfilled = false
        };
        db.BacklogItems.Add(backlog);
        await db.SaveChangesAsync();

        var logger = new TestLogger<BacklogController>(_output);
        var controller = new BacklogController(db, logger);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "admin-user"),
            new Claim(ClaimTypes.Role, AppRoles.Admin)
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<BacklogItem>>(viewResult.Model);
        Assert.Single(model);
        Assert.True(logger.HasMessage("Retrieving backlog items", LogLevel.Information));
    }
}
