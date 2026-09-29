using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UD_Autos.Authorization;
using UD_Autos.Data;

namespace UD_Autos.Controllers;

[Authorize(Policy = AppPolicies.ManageSettings)]
public class AuditLogsController : Controller
{
    private readonly ApplicationDbContext _db;

    public AuditLogsController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? search, string? entityType, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Audit Log";
        ViewData["Subtitle"] = "Sensitive create/update/void/price/stock actions.";
        page = Math.Max(1, page);
        const int pageSize = 40;

        var query = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(a =>
                (a.ReferenceNumber != null && a.ReferenceNumber.Contains(term)) ||
                (a.UserName != null && a.UserName.Contains(term)) ||
                a.Action.Contains(term) ||
                (a.Reason != null && a.Reason.Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(a => a.EntityType == entityType);

        query = query.OrderByDescending(a => a.TimestampUtc);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        ViewBag.Page = page;
        ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        ViewBag.Search = search;
        ViewBag.EntityType = entityType;
        return View(items);
    }
}
