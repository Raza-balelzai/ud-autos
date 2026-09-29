using Microsoft.EntityFrameworkCore;
using UD_Autos.Data;
using UD_Autos.Models.Inventory;
using UD_Autos.Models.Products;
using UD_Autos.Models.Shipments;

namespace UD_Autos.Services;

public class ReceivingLineInput
{
    public int ShipmentItemId { get; set; }
    public decimal ActualQuantity { get; set; }
}

public interface IShipmentFinalizationService
{
    Task<(bool Ok, string? Error)> SaveActualQuantitiesAsync(int shipmentId, IReadOnlyList<ReceivingLineInput> lines, string? userId, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> FinalizeAndReceiveAsync(int shipmentId, string? userId, CancellationToken cancellationToken = default);
}

public class ShipmentFinalizationService : IShipmentFinalizationService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICodeGeneratorService _codes;

    public ShipmentFinalizationService(ApplicationDbContext db, IAuditService audit, ICodeGeneratorService codes)
    {
        _db = db;
        _audit = audit;
        _codes = codes;
    }

    public async Task<(bool Ok, string? Error)> SaveActualQuantitiesAsync(
        int shipmentId, IReadOnlyList<ReceivingLineInput> lines, string? userId, CancellationToken cancellationToken = default)
    {
        var shipment = await _db.Shipments.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == shipmentId, cancellationToken);
        if (shipment is null) return (false, "Shipment not found.");
        if (shipment.IsFinalized) return (false, "Shipment already finalized.");

        foreach (var line in lines)
        {
            if (line.ActualQuantity < 0) return (false, "Actual quantity cannot be negative.");
            var item = shipment.Items.FirstOrDefault(i => i.Id == line.ShipmentItemId);
            if (item is null) return (false, $"Shipment item {line.ShipmentItemId} not found.");
            item.ActualQuantity = line.ActualQuantity;
            item.UpdatedAt = DateTime.UtcNow;
            item.UpdatedBy = userId;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> FinalizeAndReceiveAsync(
        int shipmentId, string? userId, CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var shipment = await _db.Shipments
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(s => s.Id == shipmentId, cancellationToken);

        if (shipment is null) return (false, "Shipment not found.");
        if (shipment.IsFinalized) return (false, "Shipment already finalized — duplicate receive blocked.");
        if (shipment.LandedCostCalculatedAt is null || shipment.TotalDirectCostPkr <= 0)
            return (false, "Calculate and save landed cost before finalization.");
        if (shipment.Items.Any(i => i.LandedUnitCostPkr <= 0 && (i.ActualQuantity ?? i.ExpectedQuantity) > 0))
            return (false, "Landed unit costs missing. Recalculate landed cost after entering actuals.");

        foreach (var item in shipment.Items)
        {
            if (item.ActualQuantity is null)
                item.ActualQuantity = item.ExpectedQuantity;
            if (item.ActualQuantity < 0)
                return (false, $"Invalid actual quantity for {item.Product.Sku}.");
        }

        // Idempotency: refuse if batches already exist for this shipment.
        if (await _db.InventoryBatches.AnyAsync(b => b.ShipmentId == shipmentId, cancellationToken))
            return (false, "Inventory batches already exist for this shipment.");

        var receivedAt = DateTime.UtcNow;
        foreach (var item in shipment.Items.Where(i => (i.ActualQuantity ?? 0) > 0))
        {
            var qty = item.ActualQuantity!.Value;
            var batchNumber = await NextBatchNumberAsync(shipment.ShipmentNumber, item.Product.Sku, cancellationToken);

            var batch = new InventoryBatch
            {
                BatchNumber = batchNumber,
                ProductId = item.ProductId,
                ShipmentId = shipment.Id,
                ShipmentItemId = item.Id,
                ReceivedQuantity = qty,
                RemainingQuantity = qty,
                ReservedQuantity = 0,
                DamagedQuantity = 0,
                LandedUnitCostPkr = item.LandedUnitCostPkr,
                ReceivedAt = receivedAt,
                IsLocked = true,
                CreatedAt = receivedAt,
                CreatedBy = userId
            };
            _db.InventoryBatches.Add(batch);

            var product = item.Product;
            var oldStock = product.CurrentStock;
            var oldAvg = product.AverageCost ?? product.LatestLandedCost ?? 0m;
            var newStock = oldStock + qty;
            product.CurrentStock = newStock;
            product.AverageCost = newStock > 0
                ? Math.Round(((oldStock * oldAvg) + (qty * item.LandedUnitCostPkr)) / newStock, 4)
                : item.LandedUnitCostPkr;
            product.PreviousLandedCost = product.LatestLandedCost;
            product.LatestLandedCost = item.LandedUnitCostPkr;
            if (item.UnitPriceRmb is > 0 && product.LatestRmbPrice != item.UnitPriceRmb)
            {
                product.PreviousRmbPrice = product.LatestRmbPrice;
                product.LatestRmbPrice = item.UnitPriceRmb;
                _db.ProductRmbPriceHistories.Add(new ProductRmbPriceHistory
                {
                    ProductId = product.Id,
                    SupplierId = item.SupplierId,
                    UnitPriceRmb = item.UnitPriceRmb.Value,
                    EffectiveDate = receivedAt,
                    SourceReference = shipment.ShipmentNumber,
                    Notes = "Shipment finalization",
                    CreatedAt = receivedAt,
                    CreatedBy = userId
                });
            }

            _db.ProductLandedCostHistories.Add(new ProductLandedCostHistory
            {
                ProductId = product.Id,
                LandedUnitCostPkr = item.LandedUnitCostPkr,
                EffectiveDate = receivedAt,
                SourceReference = shipment.ShipmentNumber,
                ShipmentReference = shipment.ShipmentNumber,
                Notes = $"Batch {batchNumber}",
                CreatedAt = receivedAt,
                CreatedBy = userId
            });

            product.StockValue = Math.Round(product.CurrentStock * (product.AverageCost ?? 0), 2);
            product.UpdatedAt = receivedAt;
            product.UpdatedBy = userId;

            // Save batch to get Id for movement — use navigation after SaveChanges mid-loop via explicit pairing.
            await _db.SaveChangesAsync(cancellationToken);

            _db.StockMovements.Add(new StockMovement
            {
                ProductId = product.Id,
                InventoryBatchId = batch.Id,
                MovementType = StockMovementType.ShipmentReceived,
                Quantity = qty,
                UnitCostPkr = item.LandedUnitCostPkr,
                QuantityAfter = product.CurrentStock,
                ReferenceType = nameof(Shipment),
                ReferenceId = shipment.Id.ToString(),
                ReferenceNumber = shipment.ShipmentNumber,
                Reason = "Shipment finalized and received",
                OccurredAt = receivedAt,
                CreatedAt = receivedAt,
                CreatedBy = userId
            });
        }

        shipment.IsFinalized = true;
        shipment.IsLandedCostLocked = true;
        shipment.FinalizedAt = receivedAt;
        shipment.FinalizedBy = userId;
        shipment.Status = ShipmentStatus.Received;
        shipment.UpdatedAt = receivedAt;
        shipment.UpdatedBy = userId;

        _db.ShipmentTimelineEvents.Add(new ShipmentTimelineEvent
        {
            ShipmentId = shipment.Id,
            EventType = ShipmentTimelineEventType.Completed,
            OccurredAt = receivedAt,
            RelatedStatus = ShipmentStatus.Received,
            Notes = "Finalize & Receive — inventory posted",
            CreatedAt = receivedAt,
            CreatedBy = userId
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (false, "Shipment was modified by another user. Reload and try again.");
        }

        await _audit.LogAsync("FinalizeReceive", nameof(Shipment), shipment.Id.ToString(), shipment.ShipmentNumber,
            reason: "Inventory increased via finalized receiving", cancellationToken: cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return (true, null);
    }

    private async Task<string> NextBatchNumberAsync(string shipmentNumber, string sku, CancellationToken cancellationToken)
    {
        var prefix = $"BAT-{shipmentNumber}-{sku}";
        var count = await _db.InventoryBatches.CountAsync(b => b.BatchNumber.StartsWith(prefix), cancellationToken);
        return $"{prefix}-{(count + 1):D2}";
    }
}
