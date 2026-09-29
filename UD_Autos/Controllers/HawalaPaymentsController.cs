using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UD_Autos.Authorization;
using UD_Autos.Data;
using UD_Autos.Models.Hawala;
using UD_Autos.Models.ViewModels;
using UD_Autos.Services;

namespace UD_Autos.Controllers;

[Authorize(Policy = AppPolicies.ManagePayments)]
public class HawalaPaymentsController : Controller
{
    private readonly IHawalaPaymentService _hawala;
    private readonly ApplicationDbContext _db;

    public HawalaPaymentsController(IHawalaPaymentService hawala, ApplicationDbContext db)
    {
        _hawala = hawala;
        _db = db;
    }

    public async Task<IActionResult> Index(string? search, int? supplierId, DateTime? from, DateTime? to, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Hawala Payments";
        ViewData["Subtitle"] = "China factory transfers linked to suppliers and POs.";
        ViewBag.Suppliers = new SelectList(await _db.Suppliers.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(cancellationToken), "Id", "Name", supplierId);
        ViewBag.From = from;
        ViewBag.To = to;
        return View(await _hawala.SearchAsync(search, supplierId, from, to, page, 20, cancellationToken));
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken = default)
    {
        var payment = await _hawala.GetByIdAsync(id, cancellationToken);
        if (payment is null) return NotFound();
        ViewData["Title"] = payment.PaymentNumber;
        ViewData["Subtitle"] = payment.Supplier.Name;
        return View(payment);
    }

    public async Task<IActionResult> Create(int? supplierId, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "New Hawala Payment";
        var payment = new HawalaPayment { SupplierId = supplierId ?? 0, PaymentDate = DateTime.UtcNow.Date, IsDirectImportCharge = true };
        return View("Form", await BuildFormAsync(payment, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(HawalaFormViewModel model, CancellationToken cancellationToken = default)
    {
        var (ok, error, payment) = await _hawala.CreateAsync(model.Payment, User.GetUserId(), cancellationToken);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Could not save Hawala payment.");
            return View("Form", await BuildFormAsync(model.Payment, cancellationToken));
        }

        TempData["Success"] = $"Hawala {payment!.PaymentNumber} recorded. Effective rate ¥1 = Rs. {payment.EffectiveRmbRate:N4}";
        return RedirectToAction(nameof(Details), new { id = payment.Id });
    }

    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken = default)
    {
        var payment = await _hawala.GetByIdAsync(id, cancellationToken);
        if (payment is null) return NotFound();
        ViewData["Title"] = $"Edit {payment.PaymentNumber}";
        return View("Form", await BuildFormAsync(payment, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, HawalaFormViewModel model, CancellationToken cancellationToken = default)
    {
        if (id != model.Payment.Id) return BadRequest();
        var (ok, error) = await _hawala.UpdateAsync(model.Payment, User.GetUserId(), cancellationToken);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Could not update Hawala payment.");
            return View("Form", await BuildFormAsync(model.Payment, cancellationToken));
        }

        TempData["Success"] = "Hawala payment updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> OpenPurchaseOrders(int supplierId, CancellationToken cancellationToken = default)
    {
        var list = await _db.PurchaseOrders.AsNoTracking()
            .Where(p => p.SupplierId == supplierId && p.Status != Models.Purchases.PurchaseOrderStatus.Draft)
            .OrderByDescending(p => p.OrderDate)
            .Select(p => new { p.Id, Label = p.PoNumber + " — ¥" + p.TotalRmb.ToString("N2") })
            .ToListAsync(cancellationToken);
        return Json(list);
    }

    private async Task<HawalaFormViewModel> BuildFormAsync(HawalaPayment payment, CancellationToken cancellationToken)
    {
        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(cancellationToken);
        var pos = payment.SupplierId > 0
            ? await _db.PurchaseOrders.AsNoTracking()
                .Where(p => p.SupplierId == payment.SupplierId && p.Status != Models.Purchases.PurchaseOrderStatus.Draft)
                .OrderByDescending(p => p.OrderDate)
                .ToListAsync(cancellationToken)
            : new List<Models.Purchases.PurchaseOrder>();

        return new HawalaFormViewModel
        {
            Payment = payment,
            Suppliers = suppliers,
            PurchaseOrders = pos
        };
    }
}
