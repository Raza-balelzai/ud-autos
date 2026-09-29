using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UD_Autos.Authorization;
using UD_Autos.Data;
using UD_Autos.Models.Inventory;
using UD_Autos.Models.ViewModels;
using UD_Autos.Services;

namespace UD_Autos.Controllers;

[Authorize(Policy = AppPolicies.ManageInventory)]
public class InventoryController : Controller
{
    private readonly IInventoryService _inventory;
    private readonly ApplicationDbContext _db;

    public InventoryController(IInventoryService inventory, ApplicationDbContext db)
    {
        _inventory = inventory;
        _db = db;
    }

    public async Task<IActionResult> Index(string? search, bool lowStock = false, bool outOfStock = false, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Inventory";
        ViewData["Subtitle"] = "Current stock, valuation and reorder status.";
        ViewBag.LowStock = lowStock;
        ViewBag.OutOfStock = outOfStock;
        return View(await _inventory.SearchStockAsync(search, lowStock, outOfStock, page, 20, cancellationToken));
    }

    public async Task<IActionResult> Batches(int? productId, string? search, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Batch Stock";
        ViewData["Subtitle"] = "Shipment/batch-level cost and remaining quantity.";
        ViewBag.ProductId = productId;
        return View(await _inventory.GetBatchesAsync(productId, search, cancellationToken));
    }

    public async Task<IActionResult> Movements(int? productId, StockMovementType? type, DateTime? from, DateTime? to, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Stock Movements";
        ViewData["Subtitle"] = "Immutable movement trail.";
        ViewBag.ProductId = productId;
        ViewBag.Type = type;
        ViewBag.From = from;
        ViewBag.To = to;
        return View(await _inventory.GetMovementsAsync(productId, type, from, to, page, 30, cancellationToken));
    }

    public async Task<IActionResult> Adjust(int? productId, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Stock Adjustment";
        ViewData["Subtitle"] = "Authorized adjustment with reason and audit trail.";
        ViewBag.Products = await _db.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(cancellationToken);
        ViewBag.Batches = productId.HasValue
            ? await _inventory.GetBatchesAsync(productId, null, cancellationToken)
            : Array.Empty<Models.Inventory.InventoryBatch>();
        return View(new StockAdjustmentViewModel { ProductId = productId ?? 0 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust(StockAdjustmentViewModel model, CancellationToken cancellationToken = default)
    {
        var (ok, error) = await _inventory.AdjustStockAsync(model.ProductId, model.BatchId, model.QuantityDelta, model.Reason, User.GetUserId(), cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok ? "Stock adjusted." : error;
        return ok ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Adjust), new { productId = model.ProductId });
    }

    public async Task<IActionResult> Damage(int? productId, int? batchId, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Damaged Stock";
        ViewBag.Products = await _db.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(cancellationToken);
        ViewBag.Batches = await _inventory.GetBatchesAsync(productId, null, cancellationToken);
        return View(new DamageStockViewModel { ProductId = productId ?? 0, BatchId = batchId ?? 0 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Damage(DamageStockViewModel model, CancellationToken cancellationToken = default)
    {
        var (ok, error) = await _inventory.MarkDamagedAsync(model.ProductId, model.BatchId, model.Quantity, model.Reason, User.GetUserId(), cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok ? "Damaged stock recorded." : error;
        return RedirectToAction(nameof(Batches), new { productId = model.ProductId });
    }

    public async Task<IActionResult> LowStock(CancellationToken cancellationToken = default)
        => RedirectToAction(nameof(Index), new { lowStock = true });
}
