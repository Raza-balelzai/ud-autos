using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UD_Autos.Authorization;
using UD_Autos.Services;

namespace UD_Autos.Controllers;

[Authorize]
public class SearchController : Controller
{
    private readonly IGlobalSearchService _search;

    public SearchController(IGlobalSearchService search) => _search = search;

    [HttpGet]
    public async Task<IActionResult> Suggest(string? q, CancellationToken cancellationToken = default)
    {
        var includeShipments = User.IsInRole(AppRoles.Owner) || User.IsInRole(AppRoles.Manager);
        var results = await _search.SuggestAsync(q ?? string.Empty, includeShipments, cancellationToken);
        return Json(new { results });
    }
}
