using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UD_Autos.Authorization;
using UD_Autos.Data;
using UD_Autos.Models.Sales;
using UD_Autos.Models.ViewModels;
using UD_Autos.Services;

namespace UD_Autos.Controllers;

[Authorize(Policy = AppPolicies.ManagePayments)]
public class PaymentsController : Controller
{
    private readonly IPaymentAllocationService _payments;
    private readonly IReceivableAgingService _aging;
    private readonly ApplicationDbContext _db;

    public PaymentsController(IPaymentAllocationService payments, IReceivableAgingService aging, ApplicationDbContext db)
    {
        _payments = payments;
        _aging = aging;
        _db = db;
    }

    public async Task<IActionResult> Index(int? customerId, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Receive Payment";
        ViewData["Subtitle"] = "FIFO allocation by default; manual allocation for authorized users.";
        return View(await BuildReceiveAsync(customerId, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ReceivePaymentViewModel model, CancellationToken cancellationToken = default)
    {
        var manual = model.UseManualAllocation
            ? model.Allocations.Where(a => a.InvoiceId > 0 && a.Amount > 0).ToList()
            : null;

        if (model.UseManualAllocation && !(User.IsInRole(AppRoles.Owner) || User.IsInRole(AppRoles.Manager)))
        {
            TempData["Error"] = "Manual payment allocation is restricted to Owner/Manager.";
            return View(await BuildReceiveAsync(model.CustomerId, cancellationToken));
        }

        var (ok, error, payment) = await _payments.ReceivePaymentAsync(
            model.CustomerId, model.PaymentDate, model.Amount, model.Account,
            model.Reference, model.Notes, manual, User.GetUserId(), cancellationToken);

        TempData[ok ? "Success" : "Error"] = ok ? $"Payment {payment!.PaymentNumber} recorded." : error;
        return ok
            ? RedirectToAction("Khata", "Customers", new { id = model.CustomerId })
            : View(await BuildReceiveAsync(model.CustomerId, cancellationToken));
    }

    [Authorize(Policy = AppPolicies.ViewOperationalReports)]
    public async Task<IActionResult> Aging(int? days, DateTime? before, int? customerId, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Receivable Aging";
        ViewData["Subtitle"] = "Based on unpaid invoice allocation, not last purchase date.";
        ViewBag.Days = days;
        ViewBag.Before = before;
        return View(await _aging.GetAgingAsync(days, before, customerId, cancellationToken));
    }

    private async Task<ReceivePaymentViewModel> BuildReceiveAsync(int? customerId, CancellationToken cancellationToken)
    {
        var customers = await _db.Customers.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(cancellationToken);
        var open = customerId.HasValue
            ? await _db.Invoices.AsNoTracking()
                .Where(i => i.CustomerId == customerId && !i.IsVoided && i.OutstandingAmount > 0)
                .OrderBy(i => i.InvoiceDate)
                .ToListAsync(cancellationToken)
            : new List<Invoice>();

        return new ReceivePaymentViewModel
        {
            CustomerId = customerId ?? 0,
            Customers = customers,
            OpenInvoices = open,
            Allocations = open.Select(i => new ManualAllocationInput { InvoiceId = i.Id, Amount = 0 }).ToList()
        };
    }
}
