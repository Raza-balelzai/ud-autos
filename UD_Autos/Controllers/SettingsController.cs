using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UD_Autos.Authorization;
using UD_Autos.Models.Settings;
using UD_Autos.Models.ViewModels;
using UD_Autos.Services;

namespace UD_Autos.Controllers;

[Authorize(Policy = AppPolicies.ManageSettings)]
public class SettingsController : Controller
{
    private readonly ISettingsService _settings;

    public SettingsController(ISettingsService settings)
    {
        _settings = settings;
    }

    public async Task<IActionResult> Index(string tab = "categories", CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Settings";
        ViewData["Subtitle"] = "Controlled lookups and business configuration.";
        var vm = new SettingsIndexViewModel
        {
            ActiveTab = tab,
            Categories = await _settings.GetCategoriesAsync(activeOnly: false, cancellationToken),
            Units = await _settings.GetUnitsAsync(activeOnly: false, cancellationToken),
            ExpenseCategories = await _settings.GetExpenseCategoriesAsync(activeOnly: false, cancellationToken),
            BusinessSettings = await _settings.GetBusinessSettingsAsync(cancellationToken),
            Mail = await _settings.GetMailSettingsAsync(cancellationToken)
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCategory(ProductCategory model, CancellationToken cancellationToken = default)
    {
        var (ok, error) = await _settings.SaveCategoryAsync(model, User.GetUserId(), cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok ? "Category saved." : error;
        return RedirectToAction(nameof(Index), new { tab = "categories" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken cancellationToken = default)
    {
        await _settings.SoftDeleteCategoryAsync(id, User.GetUserId(), cancellationToken);
        TempData["Success"] = "Category deactivated.";
        return RedirectToAction(nameof(Index), new { tab = "categories" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveUnit(UnitOfMeasure model, CancellationToken cancellationToken = default)
    {
        var (ok, error) = await _settings.SaveUnitAsync(model, User.GetUserId(), cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok ? "Unit saved." : error;
        return RedirectToAction(nameof(Index), new { tab = "units" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUnit(int id, CancellationToken cancellationToken = default)
    {
        await _settings.SoftDeleteUnitAsync(id, User.GetUserId(), cancellationToken);
        TempData["Success"] = "Unit deactivated.";
        return RedirectToAction(nameof(Index), new { tab = "units" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveExpenseCategory(ExpenseCategory model, CancellationToken cancellationToken = default)
    {
        var (ok, error) = await _settings.SaveExpenseCategoryAsync(model, User.GetUserId(), cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok ? "Expense category saved." : error;
        return RedirectToAction(nameof(Index), new { tab = "expenses" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteExpenseCategory(int id, CancellationToken cancellationToken = default)
    {
        await _settings.SoftDeleteExpenseCategoryAsync(id, User.GetUserId(), cancellationToken);
        TempData["Success"] = "Expense category deactivated.";
        return RedirectToAction(nameof(Index), new { tab = "expenses" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveBusinessSetting(BusinessSetting model, CancellationToken cancellationToken = default)
    {
        var (ok, error) = await _settings.SaveBusinessSettingAsync(model, User.GetUserId(), cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok ? "Setting saved." : error;
        return RedirectToAction(nameof(Index), new { tab = "business" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveMailSettings(MailSettingsForm model, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Check the mail settings and try again.";
            return RedirectToAction(nameof(Index), new { tab = "email" });
        }

        var (ok, error) = await _settings.SaveMailSettingsAsync(model, User.GetUserId(), cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok ? "Mail settings saved." : error;
        return RedirectToAction(nameof(Index), new { tab = "email" });
    }
}
