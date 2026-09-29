using Microsoft.EntityFrameworkCore;
using UD_Autos.Data;
using UD_Autos.Models.Inventory;
using UD_Autos.Models.Shipments;

namespace UD_Autos.Services;

public class LandedCostLineResult
{
    public int ShipmentItemId { get; set; }
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal AllocationBasisValue { get; set; }
    public decimal ChinaProductCostPkr { get; set; }
    public decimal AllocatedDirectCostPkr { get; set; }
    public decimal LandedUnitCostPkr { get; set; }
}

public class LandedCostPreview
{
    public int ShipmentId { get; set; }
    public string ShipmentNumber { get; set; } = string.Empty;
    public LandedCostAllocationMethod Method { get; set; }
    public decimal CostingExchangeRate { get; set; }
    public decimal ChinaProductCostPkr { get; set; }
    public decimal HawalaDirectChargesPkr { get; set; }
    public decimal FreightAmountPkr { get; set; }
    public decimal AgentChargesPkr { get; set; }
    public decimal TransportCostPkr { get; set; }
    public decimal LoadingCostPkr { get; set; }
    public decimal UnloadingCostPkr { get; set; }
    public decimal OtherDirectCostPkr { get; set; }
    public decimal TotalDirectCostPkr { get; set; }
    public decimal AllocatedTotalPkr { get; set; }
    public decimal AllocationRemainderPkr { get; set; }
    public IReadOnlyList<LandedCostLineResult> Lines { get; set; } = Array.Empty<LandedCostLineResult>();
}

public class CustomAllocationInput
{
    public int ShipmentItemId { get; set; }
    public decimal Percent { get; set; }
    public decimal? AllocationWeightKg { get; set; }
    public decimal? AllocationCbm { get; set; }
}

public interface ILandedCostService
{
    Task<(bool Ok, string? Error, LandedCostPreview? Preview)> PreviewAsync(
        int shipmentId,
        LandedCostAllocationMethod method,
        decimal? costingExchangeRate,
        decimal? agentChargesPkr,
        IReadOnlyList<CustomAllocationInput>? customInputs,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, string? Error, LandedCostPreview? Preview)> CalculateAndPersistAsync(
        int shipmentId,
        LandedCostAllocationMethod method,
        decimal? costingExchangeRate,
        decimal? agentChargesPkr,
        IReadOnlyList<CustomAllocationInput>? customInputs,
        string? userId,
        CancellationToken cancellationToken = default);
}

public class LandedCostService : ILandedCostService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public LandedCostService(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<(bool Ok, string? Error, LandedCostPreview? Preview)> PreviewAsync(
        int shipmentId,
        LandedCostAllocationMethod method,
        decimal? costingExchangeRate,
        decimal? agentChargesPkr,
        IReadOnlyList<CustomAllocationInput>? customInputs,
        CancellationToken cancellationToken = default)
    {
        var shipment = await LoadShipmentAsync(shipmentId, cancellationToken);
        if (shipment is null) return (false, "Shipment not found.", null);
        if (shipment.IsFinalized || shipment.IsLandedCostLocked)
            return (false, "Landed cost is locked after finalization.", null);

        return await BuildPreviewAsync(shipment, method, costingExchangeRate, agentChargesPkr, customInputs, cancellationToken);
    }

    public async Task<(bool Ok, string? Error, LandedCostPreview? Preview)> CalculateAndPersistAsync(
        int shipmentId,
        LandedCostAllocationMethod method,
        decimal? costingExchangeRate,
        decimal? agentChargesPkr,
        IReadOnlyList<CustomAllocationInput>? customInputs,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var shipment = await LoadShipmentAsync(shipmentId, cancellationToken);
        if (shipment is null) return (false, "Shipment not found.", null);
        if (shipment.IsFinalized || shipment.IsLandedCostLocked)
            return (false, "Landed cost is locked after finalization.", null);

        var (ok, error, preview) = await BuildPreviewAsync(shipment, method, costingExchangeRate, agentChargesPkr, customInputs, cancellationToken);
        if (!ok || preview is null) return (false, error, null);

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        shipment.AllocationMethod = method;
        shipment.CostingExchangeRate = preview.CostingExchangeRate;
        shipment.AgentChargesPkr = preview.AgentChargesPkr;
        shipment.ChinaProductCostPkr = preview.ChinaProductCostPkr;
        shipment.HawalaDirectChargesPkr = preview.HawalaDirectChargesPkr;
        shipment.TotalDirectCostPkr = preview.TotalDirectCostPkr;
        shipment.LandedCostCalculatedAt = DateTime.UtcNow;
        shipment.UpdatedAt = DateTime.UtcNow;
        shipment.UpdatedBy = userId;

        var byId = preview.Lines.ToDictionary(l => l.ShipmentItemId);
        foreach (var item in shipment.Items)
        {
            if (!byId.TryGetValue(item.Id, out var line)) continue;
            var custom = customInputs?.FirstOrDefault(c => c.ShipmentItemId == item.Id);
            if (custom?.AllocationWeightKg is not null) item.AllocationWeightKg = custom.AllocationWeightKg;
            if (custom?.AllocationCbm is not null) item.AllocationCbm = custom.AllocationCbm;
            if (custom is not null) item.CustomAllocationPercent = custom.Percent;

            item.ChinaProductCostPkr = line.ChinaProductCostPkr;
            item.AllocatedDirectCostPkr = line.AllocatedDirectCostPkr;
            item.LandedUnitCostPkr = line.LandedUnitCostPkr;
            item.AllocationBasisValue = line.AllocationBasisValue;
            item.UpdatedAt = DateTime.UtcNow;
            item.UpdatedBy = userId;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("CalculateLandedCost", nameof(Shipment), shipment.Id.ToString(), shipment.ShipmentNumber,
            newValues: new { method, preview.TotalDirectCostPkr }, cancellationToken: cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return (true, null, preview);
    }

    private async Task<Shipment?> LoadShipmentAsync(int shipmentId, CancellationToken cancellationToken)
        => await _db.Shipments
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .Include(s => s.Items).ThenInclude(i => i.PurchaseOrder)
            .Include(s => s.Items).ThenInclude(i => i.PurchaseOrderItem)
            .FirstOrDefaultAsync(s => s.Id == shipmentId, cancellationToken);

    private async Task<(bool Ok, string? Error, LandedCostPreview? Preview)> BuildPreviewAsync(
        Shipment shipment,
        LandedCostAllocationMethod method,
        decimal? costingExchangeRate,
        decimal? agentChargesPkr,
        IReadOnlyList<CustomAllocationInput>? customInputs,
        CancellationToken cancellationToken)
    {
        if (shipment.Items.Count == 0) return (false, "Shipment has no items.", null);

        var rate = costingExchangeRate
            ?? shipment.CostingExchangeRate
            ?? await ResolveDefaultExchangeRateAsync(shipment, cancellationToken);

        if (rate <= 0) return (false, "Costing exchange rate (RMB→PKR) must be greater than zero.", null);

        if (agentChargesPkr.HasValue) shipment.AgentChargesPkr = Math.Max(0, agentChargesPkr.Value);

        // Prefer actual qty for costing when entered; else expected.
        foreach (var item in shipment.Items)
        {
            var qty = item.ActualQuantity ?? item.ExpectedQuantity;
            if (qty <= 0) return (false, $"Item {item.Product.Sku} has invalid quantity.", null);

            var unitRmb = item.UnitPriceRmb
                ?? item.PurchaseOrderItem?.UnitPriceRmb
                ?? item.Product.LatestRmbPrice
                ?? 0m;
            if (unitRmb < 0) return (false, $"Item {item.Product.Sku} has invalid RMB price.", null);

            item.UnitPriceRmb ??= unitRmb;
            if (item.AllocationWeightKg is null or <= 0)
                item.AllocationWeightKg = (item.Product.UnitWeightKg ?? 0) * qty;
            if (item.AllocationCbm is null or <= 0)
                item.AllocationCbm = (item.Product.Cbm ?? 0) * qty;
        }

        if (method == LandedCostAllocationMethod.Custom)
        {
            if (customInputs is null || customInputs.Count == 0)
                return (false, "Custom allocation requires percentage inputs per line.", null);
            var totalPct = customInputs.Sum(c => c.Percent);
            if (Math.Abs(totalPct - 100m) > 0.05m)
                return (false, $"Custom allocation percentages must total 100 (currently {totalPct:N2}).", null);
        }

        var defaultMethod = shipment.FreightMethod == FreightMethod.ByCbm
            ? LandedCostAllocationMethod.ByCbm
            : LandedCostAllocationMethod.ByWeight;
        if (method == default(LandedCostAllocationMethod))
            method = defaultMethod;

        var hawala = await SumHawalaDirectChargesAsync(shipment, cancellationToken);
        var chinaProduct = Math.Round(shipment.Items.Sum(i =>
        {
            var qty = i.ActualQuantity ?? i.ExpectedQuantity;
            return qty * (i.UnitPriceRmb ?? 0m) * rate;
        }), 2);

        var totalDirect = Math.Round(
            chinaProduct
            + hawala
            + shipment.FreightAmountPkr
            + shipment.AgentChargesPkr
            + shipment.TransportCostPkr
            + shipment.LoadingCostPkr
            + shipment.UnloadingCostPkr
            + shipment.OtherDirectCostPkr, 2);

        var lines = new List<LandedCostLineResult>();
        decimal basisTotal = 0;

        foreach (var item in shipment.Items)
        {
            var qty = item.ActualQuantity ?? item.ExpectedQuantity;
            var custom = customInputs?.FirstOrDefault(c => c.ShipmentItemId == item.Id);
            var basis = method switch
            {
                LandedCostAllocationMethod.ByWeight => custom?.AllocationWeightKg ?? item.AllocationWeightKg ?? 0,
                LandedCostAllocationMethod.ByCbm => custom?.AllocationCbm ?? item.AllocationCbm ?? 0,
                LandedCostAllocationMethod.ByQuantity => qty,
                LandedCostAllocationMethod.ByProductValue => qty * (item.UnitPriceRmb ?? 0m) * rate,
                LandedCostAllocationMethod.Custom => custom?.Percent ?? item.CustomAllocationPercent ?? 0,
                _ => qty
            };
            if (basis < 0) return (false, $"Negative allocation basis for {item.Product.Sku}.", null);
            basisTotal += basis;

            var chinaLine = Math.Round(qty * (item.UnitPriceRmb ?? 0m) * rate, 2);
            lines.Add(new LandedCostLineResult
            {
                ShipmentItemId = item.Id,
                ProductId = item.ProductId,
                Sku = item.Product.Sku,
                ProductName = item.Product.Name,
                Quantity = qty,
                AllocationBasisValue = basis,
                ChinaProductCostPkr = chinaLine
            });
        }

        if (basisTotal <= 0) return (false, "Allocation basis total is zero. Provide weights/CBM/qty/value/custom %.", null);

        // Allocate total direct cost by basis; fix rounding on last line so totals reconcile.
        var bases = lines.Select(l => l.AllocationBasisValue).ToList();
        var shares = AccountingMath.AllocateByBasis(totalDirect, bases);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            line.AllocatedDirectCostPkr = shares[i];
            line.LandedUnitCostPkr = line.Quantity > 0
                ? Math.Round(shares[i] / line.Quantity, 4)
                : 0;
        }

        var preview = new LandedCostPreview
        {
            ShipmentId = shipment.Id,
            ShipmentNumber = shipment.ShipmentNumber,
            Method = method,
            CostingExchangeRate = rate,
            ChinaProductCostPkr = chinaProduct,
            HawalaDirectChargesPkr = hawala,
            FreightAmountPkr = shipment.FreightAmountPkr,
            AgentChargesPkr = shipment.AgentChargesPkr,
            TransportCostPkr = shipment.TransportCostPkr,
            LoadingCostPkr = shipment.LoadingCostPkr,
            UnloadingCostPkr = shipment.UnloadingCostPkr,
            OtherDirectCostPkr = shipment.OtherDirectCostPkr,
            TotalDirectCostPkr = totalDirect,
            AllocatedTotalPkr = lines.Sum(l => l.AllocatedDirectCostPkr),
            AllocationRemainderPkr = Math.Round(totalDirect - lines.Sum(l => l.AllocatedDirectCostPkr), 2),
            Lines = lines
        };

        return (true, null, preview);
    }

    private async Task<decimal> ResolveDefaultExchangeRateAsync(Shipment shipment, CancellationToken cancellationToken)
    {
        var poIds = shipment.Items.Where(i => i.PurchaseOrderId.HasValue).Select(i => i.PurchaseOrderId!.Value).Distinct().ToList();
        if (poIds.Count > 0)
        {
            var hawalaRates = await _db.HawalaPayments.AsNoTracking()
                .Where(h => h.PurchaseOrderId.HasValue && poIds.Contains(h.PurchaseOrderId.Value) && h.EffectiveRmbRate > 0)
                .Select(h => h.EffectiveRmbRate)
                .ToListAsync(cancellationToken);
            if (hawalaRates.Count > 0) return Math.Round(hawalaRates.Average(), 6);

            var poRates = shipment.Items
                .Where(i => i.PurchaseOrder?.EstimatedExchangeRate is > 0)
                .Select(i => i.PurchaseOrder!.EstimatedExchangeRate!.Value)
                .ToList();
            if (poRates.Count > 0) return Math.Round(poRates.Average(), 6);
        }

        var latest = await _db.HawalaPayments.AsNoTracking()
            .Where(h => h.EffectiveRmbRate > 0)
            .OrderByDescending(h => h.PaymentDate)
            .Select(h => (decimal?)h.EffectiveRmbRate)
            .FirstOrDefaultAsync(cancellationToken);
        return latest ?? 0m;
    }

    private async Task<decimal> SumHawalaDirectChargesAsync(Shipment shipment, CancellationToken cancellationToken)
    {
        var poIds = shipment.Items.Where(i => i.PurchaseOrderId.HasValue).Select(i => i.PurchaseOrderId!.Value).Distinct().ToList();
        if (poIds.Count == 0) return 0m;

        // Fee + PKR paid on direct-import Hawala linked to shipment POs (identifiable for landed cost).
        // Count each Hawala payment once.
        var payments = await _db.HawalaPayments.AsNoTracking()
            .Where(h => h.IsDirectImportCharge && h.PurchaseOrderId.HasValue && poIds.Contains(h.PurchaseOrderId.Value))
            .Select(h => new { h.Id, Amount = h.PkrPaid + h.HawalaFee })
            .Distinct()
            .ToListAsync(cancellationToken);

        return Math.Round(payments.Sum(p => p.Amount), 2);
    }
}
