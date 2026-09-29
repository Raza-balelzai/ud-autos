using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UD_Autos.Authorization;
using UD_Autos.Models.Sales;
using UD_Autos.Models.ViewModels;
using UD_Autos.Services;

namespace UD_Autos.Controllers;

[Authorize(Policy = AppPolicies.ManageSales)]
public class SalesReturnsController : Controller
{
    private readonly ISalesReturnService _returns;
    private readonly ISalesService _sales;

    public SalesReturnsController(ISalesReturnService returns, ISalesService sales)
    {
        _returns = returns;
        _sales = sales;
    }

    public async Task<IActionResult> Create(int invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await _sales.GetInvoiceAsync(invoiceId, cancellationToken);
        if (invoice is null) return NotFound();
        ViewData["Title"] = "Sales Return";
        ViewData["Subtitle"] = invoice.InvoiceNumber;
        return View(new SalesReturnFormViewModel
        {
            InvoiceId = invoice.Id,
            Invoice = invoice,
            Lines = invoice.Items.Select(i => new ReturnLineInput
            {
                InvoiceItemId = i.Id,
                Quantity = 0
            }).ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SalesReturnFormViewModel model, CancellationToken cancellationToken = default)
    {
        var lines = model.Lines.Where(l => l.Quantity > 0).ToList();
        var (ok, error, ret) = await _returns.CreateAsync(
            model.InvoiceId, model.ReturnDate, model.Reason, model.AdjustKhata, model.RefundAccount,
            lines, User.GetUserId(), cancellationToken);

        if (!ok)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Create), new { invoiceId = model.InvoiceId });
        }

        TempData["Success"] = $"Return {ret!.ReturnNumber} posted.";
        return RedirectToAction("Details", "Sales", new { id = model.InvoiceId });
    }
}
