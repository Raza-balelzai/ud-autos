using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UD_Autos.Authorization;
using UD_Autos.Services;

namespace UD_Autos.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboard;
    private readonly IWebHostEnvironment _environment;

    public DashboardController(IDashboardService dashboard, IWebHostEnvironment environment)
    {
        _dashboard = dashboard;
        _environment = environment;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Dashboard";
        ViewData["Subtitle"] = "Sales and cash collections are shown separately.";
        ViewData["UdShopDownload"] = UdShopDesktopDownloads.Read(_environment);
        var canProfit = User.IsInRole(AppRoles.Owner) || User.IsInRole(AppRoles.Manager) || User.IsInRole(AppRoles.Accountant);
        return View(await _dashboard.GetAsync(canProfit, cancellationToken));
    }
}
