using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UD_Autos.Authorization;
using UD_Autos.Models.Finance;
using UD_Autos.Models.ViewModels;
using UD_Autos.Services;

namespace UD_Autos.Controllers;

[Authorize(Policy = AppPolicies.ManageExpenses)]
public class ExpensesController : Controller
{
    private readonly IExpenseService _expenses;
    private readonly ISettingsService _settings;

    public ExpensesController(IExpenseService expenses, ISettingsService settings)
    {
        _expenses = expenses;
        _settings = settings;
    }

    public async Task<IActionResult> Index(string? search, int? categoryId, DateTime? from, DateTime? to, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Operating Expenses";
        ViewData["Subtitle"] = "Does not include capitalized shipment/import costs — those stay in landed cost.";
        ViewBag.Categories = await _settings.GetExpenseCategoriesAsync(true, cancellationToken);
        return View(await _expenses.SearchAsync(search, categoryId, from, to, page, 20, cancellationToken));
    }

    public async Task<IActionResult> Create(CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Add Expense";
        return View(new ExpenseFormViewModel
        {
            Categories = await _settings.GetExpenseCategoriesAsync(true, cancellationToken)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ExpenseFormViewModel model, CancellationToken cancellationToken = default)
    {
        var (ok, error, expense) = await _expenses.CreateAsync(model.Expense, User.GetUserId(), cancellationToken);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Could not save expense.");
            model.Categories = await _settings.GetExpenseCategoriesAsync(true, cancellationToken);
            return View(model);
        }

        TempData["Success"] = $"Expense {expense!.ExpenseNumber} posted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
    {
        var (ok, error) = await _expenses.SoftVoidAsync(id, User.GetUserId(), cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok ? "Expense removed and cash movement reversed." : error;
        return RedirectToAction(nameof(Index));
    }
}
