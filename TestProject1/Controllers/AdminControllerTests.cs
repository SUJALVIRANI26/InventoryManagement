using InventoryManagement.Controllers;
using InventoryManagement.Models;
using InventoryManagement.Models.ViewModels;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TestProject1.Helpers;
using Xunit.Abstractions;

namespace TestProject1.Controllers;

public class AdminControllerTests
{
    private readonly ITestOutputHelper _output;

    public AdminControllerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Index_ReturnsViewWithDashboardMetricsAndLogsInformation()
    {
        using var db = TestDbContextFactory.Create();
        var reportLogger = new TestLogger<ReportService>(_output);
        var reportService = new ReportService(db, reportLogger);
        var adminLogger = new TestLogger<AdminController>(_output);

        var controller = new AdminController(db, reportService, adminLogger);

        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminDashboardViewModel>(viewResult.Model);

        Assert.Equal(0, model.UserCount);
        Assert.True(adminLogger.HasMessage("Admin dashboard requested", LogLevel.Information));
    }

    [Fact]
    public async Task Chart_ProfitType_ReturnsJsonAndLogsInformation()
    {
        using var db = TestDbContextFactory.Create();
        var reportService = new ReportService(db);
        var adminLogger = new TestLogger<AdminController>(_output);

        var controller = new AdminController(db, reportService, adminLogger);

        var result = await controller.Chart(type: "profit", period: ReportPeriod.Daily);

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.IsType<ChartPayload>(jsonResult.Value);
        Assert.True(adminLogger.HasMessage("Admin chart data requested", LogLevel.Information));
    }
}
