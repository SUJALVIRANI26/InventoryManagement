using System.Diagnostics;
using InventoryManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        if (User?.Identity?.IsAuthenticated != true)
        {
            return View();
        }

        if (User.IsInRole(AppRoles.Admin))
        {
            return RedirectToAction("Index", "Admin");
        }

        if (User.IsInRole(AppRoles.InventoryManager))
        {
            return RedirectToAction("Index", "Inventory");
        }

        if (User.IsInRole(AppRoles.SalesTeam))
        {
            return RedirectToAction("Index", "Sales");
        }

        return View();
    }

    public IActionResult Privacy() => View();

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var requestId = Activity.Current?.Id ?? HttpContext?.TraceIdentifier ?? "Unknown";
        _logger.LogError("An error occurred during request processing. RequestId: {RequestId}", requestId);
        return View(new ErrorViewModel { RequestId = requestId });
    }
}
