using InventoryManagement.Controllers;
using InventoryManagement.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using TestProject1.Helpers;
using Xunit.Abstractions;

namespace TestProject1.Controllers;

public class HomeControllerTests
{
    private readonly ITestOutputHelper _output;

    public HomeControllerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Index_UnauthenticatedUser_ReturnsView()
    {
        // Arrange
        var logger = new TestLogger<HomeController>(_output);
        var controller = new HomeController(logger)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        // Act
        var result = controller.Index();

        // Assert
        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void Index_AdminUser_RedirectsToAdmin()
    {
        // Arrange
        var logger = new TestLogger<HomeController>(_output);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, AppRoles.Admin)],
            "TestAuth"));

        var controller = new HomeController(logger)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };

        // Act
        var result = controller.Index();

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Admin", redirect.ControllerName);
    }

    [Fact]
    public void Index_SalesTeamUser_RedirectsToSales()
    {
        // Arrange
        var logger = new TestLogger<HomeController>(_output);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, AppRoles.SalesTeam)],
            "TestAuth"));

        var controller = new HomeController(logger)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };

        // Act
        var result = controller.Index();

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Sales", redirect.ControllerName);
    }

    [Fact]
    public void Error_LogsErrorAndReturnsViewWithErrorViewModel()
    {
        // Arrange
        var logger = new TestLogger<HomeController>(_output);
        var controller = new HomeController(logger)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-12345" }
            }
        };

        // Act
        var result = controller.Error();

        // Assert
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ErrorViewModel>(view.Model);
        Assert.Equal("trace-12345", model.RequestId);

        // Verify ILogger
        Assert.True(logger.HasLogLevel(LogLevel.Error));
        Assert.True(logger.HasMessage("An error occurred during request processing"));
        Assert.True(logger.HasMessage("trace-12345"));
    }
}
