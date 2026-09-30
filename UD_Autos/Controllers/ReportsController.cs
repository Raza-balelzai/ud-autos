using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UD_Autos.Authorization;
using UD_Autos.Models.ViewModels;
using UD_Autos.Services;

namespace UD_Autos.Controllers;

[Authorize(Policy = AppPolicies.ViewOperationalReports)]
public class ReportsController : Controller
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports) => _reports = reports;

    public async Task<IActionResult> Index(string family = "sales", string report = "daily", DateTime? from = null, DateTime? to = null, string? search = null, CancellationToken cancellationToken = default)
    {
        var canProfit = User.IsInRole(AppRoles.Owner) || User.IsInRole(AppRoles.Manager) || User.IsInRole(AppRoles.Accountant);
        // Financial-only depth for Owner/Accountant on profit net reports is still allowed for Manager via ViewProfit policy roles.
        if (family.Equals("profit", StringComparison.OrdinalIgnoreCase) && !canProfit)
            return Forbid();

        ViewData["Title"] = "Reports";
        ViewData["Subtitle"] = "Sales, profit, inventory, import and receivables — totals reconcile to source transactions.";
        var result = await _reports.RunAsync(family, report, from, to, search, canProfit, cancellationToken);
        return View(new ReportsIndexViewModel
        {
            Family = family,
            Report = report,
            From = from?.Date ?? DateTime.UtcNow.Date.AddDays(-30),
            To = to?.Date ?? DateTime.UtcNow.Date,
            Search = search,
            Result = result,
            CanViewProfit = canProfit
        });
    }

    [HttpGet]
    public async Task<IActionResult> Export(string family = "sales", string report = "daily", DateTime? from = null, DateTime? to = null, string? search = null, string format = "csv", CancellationToken cancellationToken = default)
    {
        var canProfit = User.IsInRole(AppRoles.Owner) || User.IsInRole(AppRoles.Manager) || User.IsInRole(AppRoles.Accountant);
        if (family.Equals("profit", StringComparison.OrdinalIgnoreCase) && !canProfit)
            return Forbid();

        var result = await _reports.RunAsync(family, report, from, to, search, canProfit, cancellationToken);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd");
        if (string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = _reports.ToExcel(result);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{family}-{report}-{stamp}.xlsx");
        }

        var csv = _reports.ToCsv(result);
        var csvBytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        return File(csvBytes, "text/csv", $"{family}-{report}-{stamp}.csv");
    }
}
