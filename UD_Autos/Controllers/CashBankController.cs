using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UD_Autos.Authorization;
using UD_Autos.Models.Finance;
using UD_Autos.Models.Sales;
using UD_Autos.Models.ViewModels;
using UD_Autos.Services;

namespace UD_Autos.Controllers;

[Authorize(Policy = AppPolicies.ManagePayments)]
public class CashBankController : Controller
{
    private readonly ICashBankService _cash;

    public CashBankController(ICashBankService cash) => _cash = cash;

    public async Task<IActionResult> Index(DateTime? from, DateTime? to, PaymentAccount? account, CancellationToken cancellationToken = default)
    {
        var start = from?.Date ?? DateTime.UtcNow.Date;
        var end = to?.Date ?? DateTime.UtcNow.Date;
        ViewData["Title"] = "Cash & Bank";
        ViewData["Subtitle"] = "Opening + Collections + Other Receipts − Hawala − Expenses − Other Payments − Refunds = Closing. Credit sales do not hit cash.";
        var statement = await _cash.GetStatementAsync(start, end, account, cancellationToken);
        return View(new CashBankPageViewModel
        {
            Statement = statement,
            AccountFilter = account,
            From = start,
            To = end
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetOpening(CashBankPageViewModel model, CancellationToken cancellationToken = default)
    {
        var (ok, error) = await _cash.SetOpeningAsync(model.OpeningAccount, model.OpeningDate, model.OpeningAmount, null, User.GetUserId(), cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok ? "Opening balance saved." : error;
        return RedirectToAction(nameof(Index), new { from = model.From, to = model.To, account = model.AccountFilter });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddManual(CashBankPageViewModel model, CancellationToken cancellationToken = default)
    {
        var (ok, error, _) = await _cash.AddManualAsync(
            model.ManualAccount, model.ManualDate, model.ManualType, model.ManualAmount,
            model.ManualDetail, model.ManualNotes, User.GetUserId(), cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok ? "Manual movement posted." : error;
        return RedirectToAction(nameof(Index), new { from = model.From, to = model.To, account = model.AccountFilter });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sync(CancellationToken cancellationToken = default)
    {
        await _cash.SyncMissingLinkedAsync(User.GetUserId(), cancellationToken);
        TempData["Success"] = "Linked collections, Hawala, expenses and refunds synced into the cash book.";
        return RedirectToAction(nameof(Index));
    }
}
