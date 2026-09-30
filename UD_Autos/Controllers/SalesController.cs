using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UD_Autos.Authorization;
using UD_Autos.Data;
using UD_Autos.Models.Sales;
using UD_Autos.Models.ViewModels;
using UD_Autos.Services;

namespace UD_Autos.Controllers;

[Authorize(Policy = AppPolicies.ManageSales)]
public class SalesController : Controller
{
    private readonly ISalesService _sales;
    private readonly ApplicationDbContext _db;
    private readonly IInvoicePdfService _pdf;

    public SalesController(ISalesService sales, ApplicationDbContext db, IInvoicePdfService pdf)
    {
        _sales = sales;
        _db = db;
        _pdf = pdf;
    }

    public async Task<IActionResult> Index(string? search, int? customerId, DateTime? from, DateTime? to, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Sales / Billing";
        ViewData["Subtitle"] = "Invoices and daily billing.";
        return View(await _sales.SearchInvoicesAsync(search, customerId, from, to, page, 20, cancellationToken));
    }

    public async Task<IActionResult> Create(int? customerId, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "New Invoice";
        ViewData["Subtitle"] = "Select customer, add products, save atomically.";
        return View(await BuildBillingAsync(customerId, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BillingPageViewModel model, CancellationToken cancellationToken = default)
    {
        model.Items = model.Items.Where(i => i.ProductId > 0 && i.Quantity > 0).ToList();
        var canBypassMin = User.IsInRole(AppRoles.Owner) || User.IsInRole(AppRoles.Manager);
        var request = new CreateSaleRequest
        {
            CustomerId = model.CustomerId ?? 0,
            InvoiceDate = model.InvoiceDate,
            PaymentMode = model.PaymentMode,
            PaymentAccount = model.PaymentAccount,
            DiscountAmount = model.DiscountAmount,
            DeliveryCharge = model.DeliveryCharge,
            OtherCharge = model.OtherCharge,
            PaymentReceived = model.PaymentReceived,
            PaymentReference = model.PaymentReference,
            Notes = model.Notes,
            AllowBelowMinimum = canBypassMin,
            Items = model.Items,
            IdempotencyKey = model.IdempotencyKey
        };

        var (ok, error, invoice) = await _sales.CreateSaleAsync(request, User.GetUserId(),
            User.IsInRole(AppRoles.Owner) || User.IsInRole(AppRoles.Manager) || User.IsInRole(AppRoles.Accountant),
            cancellationToken);

        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Sale failed.");
            var vm = await BuildBillingAsync(model.CustomerId, cancellationToken);
            vm.Items = model.Items.Count == 0 ? vm.Items : model.Items;
            vm.PaymentMode = model.PaymentMode;
            vm.PaymentAccount = model.PaymentAccount;
            vm.DiscountAmount = model.DiscountAmount;
            vm.DeliveryCharge = model.DeliveryCharge;
            vm.OtherCharge = model.OtherCharge;
            vm.PaymentReceived = model.PaymentReceived;
            vm.IdempotencyKey = Guid.NewGuid().ToString("N");
            return View(vm);
        }

        TempData["Success"] = $"Invoice {invoice!.InvoiceNumber} saved.";
        return RedirectToAction(nameof(Details), new { id = invoice.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Owner + "," + AppRoles.Manager)]
    public async Task<IActionResult> Void(int id, string reason, CancellationToken cancellationToken = default)
    {
        var (ok, error) = await _sales.VoidInvoiceAsync(id, reason, User.GetUserId(), cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok ? "Invoice voided with stock/ledger reversal." : error;
        return ok ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken = default)
    {
        var invoice = await _sales.GetInvoiceAsync(id, cancellationToken);
        if (invoice is null) return NotFound();
        ViewData["Title"] = invoice.InvoiceNumber;
        ViewData["Subtitle"] = invoice.Customer.ShopName ?? invoice.Customer.Name;
        ViewBag.CanViewCosts = User.IsInRole(AppRoles.Owner) || User.IsInRole(AppRoles.Manager) || User.IsInRole(AppRoles.Accountant);
        return View(invoice);
    }

    public async Task<IActionResult> Print(int id, CancellationToken cancellationToken = default)
    {
        var invoice = await _sales.GetInvoiceAsync(id, cancellationToken);
        if (invoice is null) return NotFound();
        return View(invoice);
    }

    public async Task<IActionResult> Pdf(int id, CancellationToken cancellationToken = default)
    {
        var invoice = await _sales.GetInvoiceAsync(id, cancellationToken);
        if (invoice is null) return NotFound();
        var bytes = _pdf.Generate(invoice);
        return File(bytes, "application/pdf", $"{invoice.InvoiceNumber}.pdf");
    }

    public async Task<IActionResult> Daily(string? range, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var (start, end) = ResolveRange(range, from, to);
        ViewData["Title"] = "Daily Sales";
        ViewData["Subtitle"] = "Sales and collections are separate metrics.";
        ViewBag.Range = range ?? "today";
        ViewBag.CanViewProfit = User.IsInRole(AppRoles.Owner) || User.IsInRole(AppRoles.Manager) || User.IsInRole(AppRoles.Accountant);
        return View(await _sales.GetDailySalesAsync(start, end, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> SearchProducts(string? q, CancellationToken cancellationToken = default)
    {
        q ??= "";
        var products = await _db.Products.AsNoTracking()
            .Where(p => p.IsActive && (
                p.Sku.Contains(q) ||
                p.Name.Contains(q) ||
                (p.Barcode != null && p.Barcode.Contains(q))))
            .OrderBy(p => p.Name)
            .Take(20)
            .Select(p => new
            {
                p.Id,
                p.Sku,
                p.Barcode,
                p.Name,
                p.WholesalePrice,
                p.RetailPrice,
                p.MinimumSalePrice,
                Available = p.CurrentStock - p.ReservedQuantity,
                p.ImagePath
            })
            .ToListAsync(cancellationToken);
        return Json(products);
    }

    [HttpGet]
    public async Task<IActionResult> CustomerInfo(int id, CancellationToken cancellationToken = default)
    {
        var c = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (c is null) return NotFound();
        return Json(new
        {
            c.Id,
            c.CustomerCode,
            c.Name,
            c.ShopName,
            c.Phone,
            c.CurrentBalance,
            c.CreditLimit,
            c.CustomerType
        });
    }

    private async Task<BillingPageViewModel> BuildBillingAsync(int? customerId, CancellationToken cancellationToken)
    {
        var customers = await _db.Customers.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(cancellationToken);
        var products = await _db.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name).Take(200).ToListAsync(cancellationToken);
        var customer = customerId.HasValue ? customers.FirstOrDefault(c => c.Id == customerId) : null;
        return new BillingPageViewModel
        {
            CustomerId = customerId,
            Customer = customer,
            Customers = customers,
            Products = products
        };
    }

    private static (DateTime from, DateTime to) ResolveRange(string? range, DateTime? from, DateTime? to)
    {
        var today = DateTime.UtcNow.Date;
        return (range?.ToLowerInvariant()) switch
        {
            "yesterday" => (today.AddDays(-1), today.AddDays(-1)),
            "week" => (today.AddDays(-(int)today.DayOfWeek), today),
            "month" => (new DateTime(today.Year, today.Month, 1), today),
            "custom" when from.HasValue && to.HasValue => (from.Value.Date, to.Value.Date),
            _ => (today, today)
        };
    }
}
