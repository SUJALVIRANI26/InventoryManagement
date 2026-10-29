using InventoryManagement.Controllers;
using InventoryManagement.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TestProject1.Helpers;
using Xunit.Abstractions;

namespace TestProject1.Controllers;

public class SuppliersControllerTests
{
    private readonly ITestOutputHelper _output;

    public SuppliersControllerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Index_ReturnsViewWithSuppliersAndLogsInformation()
    {
        using var db = TestDbContextFactory.Create();
        db.Suppliers.AddRange(
            new Supplier { Name = "Acme Corp", ContactPerson = "John Doe", Phone = "1234567890", Email = "acme@test.com" },
            new Supplier { Name = "Global Tech", ContactPerson = "Jane Smith", Phone = "9876543210", Email = "global@test.com" }
        );
        await db.SaveChangesAsync();

        var logger = new TestLogger<SuppliersController>(_output);
        var controller = new SuppliersController(db, logger);

        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<Supplier>>(viewResult.Model);
        Assert.Equal(2, model.Count());

        Assert.True(logger.HasLogLevel(LogLevel.Information));
        Assert.True(logger.HasMessage("Retrieving all suppliers", LogLevel.Information));
    }

    [Fact]
    public async Task Create_ValidSupplier_SavesAndRedirectsAndLogsInformation()
    {
        using var db = TestDbContextFactory.Create();
        var logger = new TestLogger<SuppliersController>(_output);
        var controller = new SuppliersController(db, logger);

        var supplier = new Supplier
        {
            Name = "Apex Supplies",
            ContactPerson = "Robert",
            Phone = "5551234",
            Email = "apex@test.com",
            IsActive = true
        };

        var result = await controller.Create(supplier);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SuppliersController.Index), redirect.ActionName);

        Assert.Equal(1, db.Suppliers.Count());
        Assert.True(logger.HasMessage("Successfully created supplier 'Apex Supplies'", LogLevel.Information));
    }

    [Fact]
    public async Task Create_InvalidModelState_ReturnsViewAndLogsWarning()
    {
        using var db = TestDbContextFactory.Create();
        var logger = new TestLogger<SuppliersController>(_output);
        var controller = new SuppliersController(db, logger);
        controller.ModelState.AddModelError("Name", "Name is required");

        var supplier = new Supplier { Name = "" };
        var result = await controller.Create(supplier);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Same(supplier, viewResult.Model);
        Assert.Equal(0, db.Suppliers.Count());

        Assert.True(logger.HasLogLevel(LogLevel.Warning));
        Assert.True(logger.HasMessage("invalid model state", LogLevel.Warning));
    }

    [Fact]
    public async Task Edit_Get_SupplierNotFound_ReturnsNotFoundAndLogsWarning()
    {
        using var db = TestDbContextFactory.Create();
        var logger = new TestLogger<SuppliersController>(_output);
        var controller = new SuppliersController(db, logger);

        var result = await controller.Edit(999);

        Assert.IsType<NotFoundResult>(result);
        Assert.True(logger.HasMessage("Supplier with ID 999 not found", LogLevel.Warning));
    }

    [Fact]
    public async Task Edit_Post_IdMismatch_ReturnsBadRequestAndLogsWarning()
    {
        using var db = TestDbContextFactory.Create();
        var logger = new TestLogger<SuppliersController>(_output);
        var controller = new SuppliersController(db, logger);

        var supplier = new Supplier { Id = 2, Name = "Mismatch" };
        var result = await controller.Edit(1, supplier);

        Assert.IsType<BadRequestResult>(result);
        Assert.True(logger.HasMessage("Supplier ID mismatch", LogLevel.Warning));
    }

    [Fact]
    public async Task Edit_Post_ValidUpdate_UpdatesAndRedirectsAndLogsInformation()
    {
        using var db = TestDbContextFactory.Create();
        var existing = new Supplier { Name = "Old Name", ContactPerson = "Old Contact" };
        db.Suppliers.Add(existing);
        await db.SaveChangesAsync();

        var logger = new TestLogger<SuppliersController>(_output);
        var controller = new SuppliersController(db, logger);

        existing.Name = "Updated Name";
        existing.ContactPerson = "New Contact";

        var result = await controller.Edit(existing.Id, existing);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SuppliersController.Index), redirect.ActionName);

        var fromDb = await db.Suppliers.FindAsync(existing.Id);
        Assert.Equal("Updated Name", fromDb!.Name);
        Assert.True(logger.HasMessage("Successfully updated supplier 'Updated Name'", LogLevel.Information));
    }
}
