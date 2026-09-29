using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UD_Autos.Authorization;
using UD_Autos.Models.Suppliers;
using UD_Autos.Services;

namespace UD_Autos.Controllers;

[Authorize(Policy = AppPolicies.ManagePurchases)]
public class SuppliersController : Controller
{
    private readonly ISupplierService _suppliers;

    public SuppliersController(ISupplierService suppliers)
    {
        _suppliers = suppliers;
    }

    public async Task<IActionResult> Index(string? search, string? sortBy, bool sortDesc = false, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Suppliers / Factories";
        ViewData["Subtitle"] = "China supplier and factory master data.";
        var result = await _suppliers.SearchAsync(search, sortBy, sortDesc, page, 20, cancellationToken);
        return View(result);
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken = default)
    {
        var supplier = await _suppliers.GetByIdAsync(id, cancellationToken);
        if (supplier is null) return NotFound();
        ViewData["Title"] = supplier.Name;
        ViewData["Subtitle"] = supplier.SupplierCode;
        return View(supplier);
    }

    public IActionResult Create()
    {
        ViewData["Title"] = "Add Supplier";
        return View("Form", new Supplier());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Supplier model, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid) return View("Form", model);

        var (ok, error, supplier) = await _suppliers.CreateAsync(model, User.GetUserId(), cancellationToken);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Could not create supplier.");
            return View("Form", model);
        }

        TempData["Success"] = $"Supplier {supplier!.SupplierCode} created.";
        return RedirectToAction(nameof(Details), new { id = supplier.Id });
    }

    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken = default)
    {
        var supplier = await _suppliers.GetByIdAsync(id, cancellationToken);
        if (supplier is null) return NotFound();
        ViewData["Title"] = "Edit Supplier";
        return View("Form", supplier);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Supplier model, CancellationToken cancellationToken = default)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) return View("Form", model);

        var (ok, error) = await _suppliers.UpdateAsync(model, User.GetUserId(), cancellationToken);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Could not update supplier.");
            return View("Form", model);
        }

        TempData["Success"] = "Supplier updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
    {
        var (ok, error) = await _suppliers.SoftDeleteAsync(id, User.GetUserId(), cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok ? "Supplier deactivated." : error;
        return RedirectToAction(nameof(Index));
    }
}
