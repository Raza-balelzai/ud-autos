using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UD_Autos.Authorization;
using UD_Autos.Data;
using UD_Autos.Models.ViewModels;
using UD_Autos.Services;

namespace UD_Autos.Controllers;

[Authorize(Policy = AppPolicies.ManageInventory)]
public class SupplierReturnsController : Controller
{
    private readonly ISupplierReturnService _returns;
    private readonly ApplicationDbContext _db;

    public SupplierReturnsController(ISupplierReturnService returns, ApplicationDbContext db)
    {
        _returns = returns;
        _db = db;
    }

    public async Task<IActionResult> Index(string? search, int? supplierId, DateTime? from, DateTime? to, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Supplier Returns";
        ViewData["Subtitle"] = "Stock returns to suppliers (inventory only — no supplier ledger credit).";
        ViewBag.Suppliers = await _db.Suppliers.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(cancellationToken);
        ViewBag.SupplierId = supplierId;
        return View(await _returns.SearchAsync(search, supplierId, from, to, page, 20, cancellationToken));
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _returns.GetByIdAsync(id, cancellationToken);
        if (entity is null) return NotFound();
        ViewData["Title"] = entity.ReturnNumber;
        ViewData["Subtitle"] = entity.Supplier.Name;
        return View(entity);
    }

    public async Task<IActionResult> Create(CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "New Supplier Return";
        ViewData["Subtitle"] = "Reduce batch stock when goods are returned to a factory/supplier.";
        await LoadCreateLookupsAsync(cancellationToken);
        return View(new SupplierReturnFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SupplierReturnFormViewModel model, CancellationToken cancellationToken = default)
    {
        var (ok, error, entity) = await _returns.CreateAsync(
            model.SupplierId,
            model.ReturnDate,
            model.Reason,
            model.ShipmentId,
            model.Notes,
            model.Lines,
            User.GetUserId(),
            cancellationToken);

        if (!ok)
        {
            TempData["Error"] = error;
            await LoadCreateLookupsAsync(cancellationToken);
            return View(model);
        }

        TempData["Success"] = $"Supplier return {entity!.ReturnNumber} posted.";
        return RedirectToAction(nameof(Details), new { id = entity.Id });
    }

    private async Task LoadCreateLookupsAsync(CancellationToken cancellationToken)
    {
        ViewBag.Suppliers = await _db.Suppliers.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(cancellationToken);
        ViewBag.Shipments = await _db.Shipments.AsNoTracking().OrderByDescending(s => s.Id).Take(100).ToListAsync(cancellationToken);
        ViewBag.Batches = await _db.InventoryBatches.AsNoTracking()
            .Include(b => b.Product)
            .Where(b => b.RemainingQuantity > b.ReservedQuantity)
            .OrderByDescending(b => b.ReceivedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
    }
}
