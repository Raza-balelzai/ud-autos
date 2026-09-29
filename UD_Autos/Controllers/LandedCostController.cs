using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UD_Autos.Authorization;
using UD_Autos.Models.Inventory;
using UD_Autos.Models.ViewModels;
using UD_Autos.Services;

namespace UD_Autos.Controllers;

[Authorize(Policy = AppPolicies.ManageShipments)]
public class LandedCostController : Controller
{
    private readonly IShipmentService _shipments;
    private readonly ILandedCostService _landedCost;
    private readonly IShipmentFinalizationService _finalization;

    public LandedCostController(
        IShipmentService shipments,
        ILandedCostService landedCost,
        IShipmentFinalizationService finalization)
    {
        _shipments = shipments;
        _landedCost = landedCost;
        _finalization = finalization;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int shipmentId, CancellationToken cancellationToken = default)
    {
        var shipment = await _shipments.GetByIdAsync(shipmentId, cancellationToken);
        if (shipment is null) return NotFound();

        ViewData["Title"] = "Landed Cost";
        ViewData["Subtitle"] = shipment.ShipmentNumber;

        var method = shipment.AllocationMethod
            ?? (shipment.FreightMethod == Models.Shipments.FreightMethod.ByCbm
                ? LandedCostAllocationMethod.ByCbm
                : LandedCostAllocationMethod.ByWeight);

        return View(new LandedCostPageViewModel
        {
            Shipment = shipment,
            Method = method,
            CostingExchangeRate = shipment.CostingExchangeRate,
            AgentChargesPkr = shipment.AgentChargesPkr,
            CustomInputs = shipment.Items.Select(i => new CustomAllocationInput
            {
                ShipmentItemId = i.Id,
                Percent = i.CustomAllocationPercent ?? 0,
                AllocationWeightKg = i.AllocationWeightKg,
                AllocationCbm = i.AllocationCbm
            }).ToList(),
            Preview = shipment.LandedCostCalculatedAt is null ? null : new LandedCostPreview
            {
                ShipmentId = shipment.Id,
                ShipmentNumber = shipment.ShipmentNumber,
                Method = method,
                CostingExchangeRate = shipment.CostingExchangeRate ?? 0,
                ChinaProductCostPkr = shipment.ChinaProductCostPkr,
                HawalaDirectChargesPkr = shipment.HawalaDirectChargesPkr,
                FreightAmountPkr = shipment.FreightAmountPkr,
                AgentChargesPkr = shipment.AgentChargesPkr,
                TransportCostPkr = shipment.TransportCostPkr,
                LoadingCostPkr = shipment.LoadingCostPkr,
                UnloadingCostPkr = shipment.UnloadingCostPkr,
                OtherDirectCostPkr = shipment.OtherDirectCostPkr,
                TotalDirectCostPkr = shipment.TotalDirectCostPkr,
                AllocatedTotalPkr = shipment.Items.Sum(i => i.AllocatedDirectCostPkr),
                Lines = shipment.Items.Select(i => new LandedCostLineResult
                {
                    ShipmentItemId = i.Id,
                    ProductId = i.ProductId,
                    Sku = i.Product.Sku,
                    ProductName = i.Product.Name,
                    Quantity = i.ActualQuantity ?? i.ExpectedQuantity,
                    AllocationBasisValue = i.AllocationBasisValue,
                    ChinaProductCostPkr = i.ChinaProductCostPkr,
                    AllocatedDirectCostPkr = i.AllocatedDirectCostPkr,
                    LandedUnitCostPkr = i.LandedUnitCostPkr
                }).ToList()
            }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Calculate(
        int shipmentId,
        LandedCostAllocationMethod method,
        decimal? costingExchangeRate,
        decimal? agentChargesPkr,
        List<CustomAllocationInput>? customInputs,
        string action,
        CancellationToken cancellationToken = default)
    {
        var customs = customInputs ?? new List<CustomAllocationInput>();
        if (string.Equals(action, "save", StringComparison.OrdinalIgnoreCase))
        {
            var (ok, error, preview) = await _landedCost.CalculateAndPersistAsync(
                shipmentId, method, costingExchangeRate, agentChargesPkr, customs, User.GetUserId(), cancellationToken);
            TempData[ok ? "Success" : "Error"] = ok
                ? $"Landed cost saved. Total direct Rs. {preview!.TotalDirectCostPkr:N2}"
                : error;
            return RedirectToAction(nameof(Index), new { shipmentId });
        }

        var (previewOk, previewError, previewResult) = await _landedCost.PreviewAsync(
            shipmentId, method, costingExchangeRate, agentChargesPkr, customs, cancellationToken);
        if (!previewOk)
        {
            TempData["Error"] = previewError;
            return RedirectToAction(nameof(Index), new { shipmentId });
        }

        var shipment = await _shipments.GetByIdAsync(shipmentId, cancellationToken);
        ViewData["Title"] = "Landed Cost";
        ViewData["Subtitle"] = shipment!.ShipmentNumber;
        return View("Index", new LandedCostPageViewModel
        {
            Shipment = shipment,
            Preview = previewResult,
            Method = method,
            CostingExchangeRate = costingExchangeRate ?? previewResult?.CostingExchangeRate,
            AgentChargesPkr = agentChargesPkr ?? previewResult?.AgentChargesPkr,
            CustomInputs = customs
        });
    }

    [HttpGet]
    public async Task<IActionResult> Receiving(int shipmentId, CancellationToken cancellationToken = default)
    {
        var shipment = await _shipments.GetByIdAsync(shipmentId, cancellationToken);
        if (shipment is null) return NotFound();
        ViewData["Title"] = "Final Receiving";
        ViewData["Subtitle"] = shipment.ShipmentNumber;
        return View(new ReceivingPageViewModel
        {
            Shipment = shipment,
            Lines = shipment.Items.Select(i => new ReceivingLineInput
            {
                ShipmentItemId = i.Id,
                ActualQuantity = i.ActualQuantity ?? i.ExpectedQuantity
            }).ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveActuals(int shipmentId, List<ReceivingLineInput> lines, CancellationToken cancellationToken = default)
    {
        var (ok, error) = await _finalization.SaveActualQuantitiesAsync(shipmentId, lines ?? new(), User.GetUserId(), cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok ? "Actual quantities saved. Recalculate landed cost if quantities changed." : error;
        return RedirectToAction(nameof(Receiving), new { shipmentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.ManageInventory)]
    public async Task<IActionResult> Finalize(int shipmentId, CancellationToken cancellationToken = default)
    {
        var (ok, error) = await _finalization.FinalizeAndReceiveAsync(shipmentId, User.GetUserId(), cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok
            ? "Shipment finalized. Batches created and inventory increased."
            : error;
        return RedirectToAction(nameof(Receiving), new { shipmentId });
    }
}
