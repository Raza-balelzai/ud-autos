using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UD_Autos.Controllers;

/// <summary>Legacy Coming Soon stubs redirected to Phase 5 modules.</summary>
[Authorize]
public class ModulesController : Controller
{
    public IActionResult CashBank() => RedirectToAction("Index", "CashBank");
    public IActionResult Expenses() => RedirectToAction("Index", "Expenses");
    public IActionResult Reports() => RedirectToAction("Index", "Reports");
}
