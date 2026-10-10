using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UD_Autos.Authorization;
using UD_Autos.Services;

namespace UD_Autos.Controllers;

[Authorize(Policy = AppPolicies.ManageSettings)]
public class ArchiveController : Controller
{
    private readonly IArchiveService _archive;

    public ArchiveController(IArchiveService archive) => _archive = archive;

    public async Task<IActionResult> Index(string? tab, CancellationToken cancellationToken = default)
    {
        tab = ArchiveService.NormalizeTab(tab);
        ViewData["Title"] = "Inactive records";
        ViewData["Subtitle"] = "Turn a deactivated record back on, or permanently delete one that was never used.";
        ViewBag.Tab = tab;
        return View(await _archive.ListAsync(tab, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(string tab, int id, CancellationToken cancellationToken = default)
    {
        var (ok, error) = await _archive.ReactivateAsync(tab, id, User.GetUserId(), cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok ? "Record reactivated." : error;
        return RedirectToAction(nameof(Index), new { tab = ArchiveService.NormalizeTab(tab) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HardDelete(string tab, int id, CancellationToken cancellationToken = default)
    {
        var (ok, error) = await _archive.HardDeleteAsync(tab, id, User.GetUserId(), cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok ? "Record permanently deleted." : error;
        return RedirectToAction(nameof(Index), new { tab = ArchiveService.NormalizeTab(tab) });
    }
}
