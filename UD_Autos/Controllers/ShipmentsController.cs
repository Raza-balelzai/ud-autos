using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UD_Autos.Authorization;
using UD_Autos.Data;
using UD_Autos.Models.Shipments;
using UD_Autos.Models.ViewModels;
using UD_Autos.Services;

namespace UD_Autos.Controllers;

[Authorize(Policy = AppPolicies.ManageShipments)]
public class ShipmentsController : Controller
{
    private readonly IShipmentService _shipments;
    private readonly IFileStorageService _files;
    private readonly ApplicationDbContext _db;

    private static readonly string[] AllowedDocs = [".pdf", ".jpg", ".jpeg", ".png", ".webp", ".gif"];

    public ShipmentsController(IShipmentService shipments, IFileStorageService files, ApplicationDbContext db)
    {
        _shipments = shipments;
        _files = files;
        _db = db;
    }

    public async Task<IActionResult> Index(string? search, ShipmentStatus? status, DateTime? from, DateTime? to, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Shipments";
        ViewData["Subtitle"] = "China warehouse → Karachi → Tarnol logistics.";
        ViewBag.Status = status;
        ViewBag.From = from;
        ViewBag.To = to;
        return View(await _shipments.SearchAsync(search, status, from, to, page, 20, cancellationToken));
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken = default)
    {
        var shipment = await _shipments.GetByIdAsync(id, cancellationToken);
        if (shipment is null) return NotFound();
        ViewData["Title"] = shipment.ShipmentNumber;
        ViewData["Subtitle"] = shipment.ShippingAgent;
        return View(shipment);
    }

    public async Task<IActionResult> Create(CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "New Shipment";
        return View("Form", await BuildFormAsync(new Shipment(), new List<ShipmentItemInput> { new() }, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ShipmentFormViewModel model, CancellationToken cancellationToken = default)
    {
        model.Items = model.Items.Where(i => i.ProductId > 0).ToList();
        var (ok, error, shipment) = await _shipments.CreateAsync(model.Shipment, model.Items, User.GetUserId(), cancellationToken);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Could not create shipment.");
            return View("Form", await BuildFormAsync(model.Shipment, model.Items, cancellationToken));
        }

        TempData["Success"] = $"Shipment {shipment!.ShipmentNumber} created.";
        return RedirectToAction(nameof(Details), new { id = shipment.Id });
    }

    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken = default)
    {
        var shipment = await _shipments.GetByIdAsync(id, cancellationToken);
        if (shipment is null) return NotFound();
        ViewData["Title"] = $"Edit {shipment.ShipmentNumber}";
        var items = shipment.Items.Select(i => new ShipmentItemInput
        {
            ProductId = i.ProductId,
            PurchaseOrderId = i.PurchaseOrderId,
            PurchaseOrderItemId = i.PurchaseOrderItemId,
            SupplierId = i.SupplierId,
            ExpectedQuantity = i.ExpectedQuantity,
            UnitPriceRmb = i.UnitPriceRmb,
            Notes = i.Notes
        }).ToList();
        return View("Form", await BuildFormAsync(shipment, items, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ShipmentFormViewModel model, CancellationToken cancellationToken = default)
    {
        if (id != model.Shipment.Id) return BadRequest();
        model.Items = model.Items.Where(i => i.ProductId > 0).ToList();

        var (okHeader, errHeader) = await _shipments.UpdateHeaderAsync(model.Shipment, User.GetUserId(), cancellationToken);
        if (!okHeader)
        {
            ModelState.AddModelError(string.Empty, errHeader ?? "Update failed.");
            return View("Form", await BuildFormAsync(model.Shipment, model.Items, cancellationToken));
        }

        var (okItems, errItems) = await _shipments.ReplaceItemsAsync(id, model.Items, User.GetUserId(), cancellationToken);
        if (!okItems)
        {
            ModelState.AddModelError(string.Empty, errItems ?? "Items update failed.");
            return View("Form", await BuildFormAsync(model.Shipment, model.Items, cancellationToken));
        }

        TempData["Success"] = "Shipment updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> AddSlip(int id, CancellationToken cancellationToken = default)
    {
        var shipment = await _shipments.GetByIdAsync(id, cancellationToken);
        if (shipment is null) return NotFound();
        ViewData["Title"] = "Add Warehouse Slip";
        ViewData["Subtitle"] = shipment.ShipmentNumber;
        return View(new ReceivingSlipFormViewModel
        {
            ShipmentId = shipment.Id,
            ShipmentNumber = shipment.ShipmentNumber,
            Slip = new ReceivingSlipInput { ReceivingDate = DateTime.UtcNow.Date },
            Suppliers = await _db.Suppliers.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(cancellationToken)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSlip(ReceivingSlipFormViewModel model, CancellationToken cancellationToken = default)
    {
        if (model.SlipFile is not null)
        {
            var saved = await _files.SaveAsync(model.SlipFile, $"shipments/{model.ShipmentId}/slips", AllowedDocs, 5_000_000, cancellationToken);
            if (!saved.Ok)
            {
                ModelState.AddModelError(nameof(model.SlipFile), saved.Error ?? "Upload failed.");
                model.Suppliers = await _db.Suppliers.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(cancellationToken);
                ViewData["Title"] = "Add Warehouse Slip";
                return View(model);
            }
            model.Slip.FilePath = saved.RelativePath;
        }

        var (ok, error) = await _shipments.AddReceivingSlipAsync(model.ShipmentId, model.Slip, User.GetUserId(), cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok ? "Receiving slip added." : error;
        return RedirectToAction(nameof(Details), new { id = model.ShipmentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateFreight(Shipment model, CancellationToken cancellationToken = default)
    {
        var (ok, error) = await _shipments.UpdateFreightAndKarachiAsync(model, User.GetUserId(), cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok ? "Freight & Karachi→Tarnol costs updated." : error;
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, ShipmentStatus status, string? reason, CancellationToken cancellationToken = default)
    {
        var (ok, error) = await _shipments.ChangeStatusAsync(id, status, User.GetUserId(), reason, cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok ? $"Status updated to {status}." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadDocument(ShipmentDocumentUploadViewModel model, CancellationToken cancellationToken = default)
    {
        if (model.File is null)
        {
            TempData["Error"] = "Choose a file to upload.";
            return RedirectToAction(nameof(Details), new { id = model.ShipmentId });
        }

        var saved = await _files.SaveAsync(model.File, $"shipments/{model.ShipmentId}/docs", AllowedDocs, 8_000_000, cancellationToken);
        if (!saved.Ok)
        {
            TempData["Error"] = saved.Error;
            return RedirectToAction(nameof(Details), new { id = model.ShipmentId });
        }

        var (ok, error) = await _shipments.AddDocumentAsync(
            model.ShipmentId, model.DocumentType, model.File.FileName, saved.RelativePath!, model.File.ContentType,
            model.File.Length, model.Notes, User.GetUserId(), cancellationToken);

        TempData[ok ? "Success" : "Error"] = ok ? "Document uploaded." : error;
        return RedirectToAction(nameof(Details), new { id = model.ShipmentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTimeline(int shipmentId, ShipmentTimelineEventType eventType, DateTime occurredAt, string? notes, CancellationToken cancellationToken = default)
    {
        var (ok, error) = await _shipments.AddTimelineEventAsync(shipmentId, eventType, occurredAt, notes, User.GetUserId(), cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok ? "Timeline event added." : error;
        return RedirectToAction(nameof(Details), new { id = shipmentId });
    }

    private async Task<ShipmentFormViewModel> BuildFormAsync(Shipment shipment, List<ShipmentItemInput> items, CancellationToken cancellationToken)
    {
        return new ShipmentFormViewModel
        {
            Shipment = shipment,
            Items = items.Count == 0 ? new List<ShipmentItemInput> { new() } : items,
            Products = await _db.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(cancellationToken),
            PurchaseOrders = await _db.PurchaseOrders.AsNoTracking()
                .Where(p => p.Status != Models.Purchases.PurchaseOrderStatus.Draft)
                .OrderByDescending(p => p.OrderDate)
                .ToListAsync(cancellationToken),
            Suppliers = await _db.Suppliers.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(cancellationToken)
        };
    }
}
