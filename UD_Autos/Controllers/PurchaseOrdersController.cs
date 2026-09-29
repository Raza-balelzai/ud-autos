using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UD_Autos.Authorization;
using UD_Autos.Data;
using UD_Autos.Models.Purchases;
using UD_Autos.Models.ViewModels;
using UD_Autos.Services;

namespace UD_Autos.Controllers;

[Authorize(Policy = AppPolicies.ManagePurchases)]
public class PurchaseOrdersController : Controller
{
    private readonly IPurchaseOrderService _orders;
    private readonly ApplicationDbContext _db;

    public PurchaseOrdersController(IPurchaseOrderService orders, ApplicationDbContext db)
    {
        _orders = orders;
        _db = db;
    }

    public async Task<IActionResult> Index(string? search, int? supplierId, PurchaseOrderStatus? status, DateTime? from, DateTime? to, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "China Purchases";
        ViewData["Subtitle"] = "Purchase orders, RMB lines and supplier activity.";
        ViewBag.Suppliers = new SelectList(await _db.Suppliers.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(cancellationToken), "Id", "Name", supplierId);
        ViewBag.Status = status;
        ViewBag.From = from;
        ViewBag.To = to;
        var result = await _orders.SearchAsync(search, supplierId, status, from, to, page, 20, cancellationToken);
        return View(result);
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken = default)
    {
        var po = await _orders.GetByIdAsync(id, cancellationToken);
        if (po is null) return NotFound();
        ViewData["Title"] = po.PoNumber;
        ViewData["Subtitle"] = po.Supplier.Name;
        return View(po);
    }

    public async Task<IActionResult> Create(CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "New Purchase Order";
        return View("Form", await BuildFormAsync(new PurchaseOrder { OrderDate = DateTime.UtcNow.Date }, new List<PurchaseOrderItemInput> { new() }, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PurchaseOrderFormViewModel model, CancellationToken cancellationToken = default)
    {
        model.Items = model.Items.Where(i => i.ProductId > 0).ToList();
        var (ok, error, po) = await _orders.CreateAsync(model.PurchaseOrder, model.Items, User.GetUserId(), cancellationToken);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Could not create PO.");
            return View("Form", await BuildFormAsync(model.PurchaseOrder, model.Items, cancellationToken));
        }

        TempData["Success"] = $"Purchase order {po!.PoNumber} created.";
        return RedirectToAction(nameof(Details), new { id = po.Id });
    }

    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken = default)
    {
        var po = await _orders.GetByIdAsync(id, cancellationToken);
        if (po is null) return NotFound();
        ViewData["Title"] = $"Edit {po.PoNumber}";
        var items = po.Items.Select(i => new PurchaseOrderItemInput
        {
            ProductId = i.ProductId,
            Quantity = i.Quantity,
            UnitPriceRmb = i.UnitPriceRmb,
            Notes = i.Notes
        }).ToList();
        return View("Form", await BuildFormAsync(po, items, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PurchaseOrderFormViewModel model, CancellationToken cancellationToken = default)
    {
        if (id != model.PurchaseOrder.Id) return BadRequest();
        model.Items = model.Items.Where(i => i.ProductId > 0).ToList();
        var (ok, error) = await _orders.UpdateAsync(model.PurchaseOrder, model.Items, User.GetUserId(), cancellationToken);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Could not update PO.");
            return View("Form", await BuildFormAsync(model.PurchaseOrder, model.Items, cancellationToken));
        }

        TempData["Success"] = "Purchase order updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, PurchaseOrderStatus status, string? reason, CancellationToken cancellationToken = default)
    {
        var (ok, error) = await _orders.ChangeStatusAsync(id, status, User.GetUserId(), reason, cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok ? $"Status updated to {status}." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<PurchaseOrderFormViewModel> BuildFormAsync(PurchaseOrder po, List<PurchaseOrderItemInput> items, CancellationToken cancellationToken)
    {
        return new PurchaseOrderFormViewModel
        {
            PurchaseOrder = po,
            Items = items.Count == 0 ? new List<PurchaseOrderItemInput> { new() } : items,
            Suppliers = await _db.Suppliers.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(cancellationToken),
            Products = await _db.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(cancellationToken)
        };
    }
}
