using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UD_Autos.Authorization;
using UD_Autos.Services;

namespace UD_Autos.Controllers;

[Authorize(Policy = AppPolicies.ManageUsers)]
public class UsersController : Controller
{
    private readonly IUserAdminService _users;

    public UsersController(IUserAdminService users) => _users = users;

    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Users & Roles";
        ViewData["Subtitle"] = "Owner-only user administration.";
        return View(await _users.ListAsync(cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string email, string fullName, string password, string role, CancellationToken cancellationToken = default)
    {
        var (ok, error) = await _users.CreateAsync(email, fullName, password, role, cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok ? "User created." : error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(string id, bool isActive, CancellationToken cancellationToken = default)
    {
        var (ok, error) = await _users.SetActiveAsync(id, isActive, cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok ? "User updated." : error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRole(string id, string role, CancellationToken cancellationToken = default)
    {
        var (ok, error) = await _users.SetRoleAsync(id, role, cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok ? "Role updated." : error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id, string password, CancellationToken cancellationToken = default)
    {
        var (ok, error) = await _users.ResetPasswordAsync(id, password, cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok ? "Password reset." : error;
        return RedirectToAction(nameof(Index));
    }
}
