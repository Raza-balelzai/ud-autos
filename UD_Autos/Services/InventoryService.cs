using Microsoft.EntityFrameworkCore;
using UD_Autos.Data;
using UD_Autos.Models.Common;
using UD_Autos.Models.Inventory;
using UD_Autos.Models.Products;

namespace UD_Autos.Services;

public interface IInventoryService
{
    Task<PagedResult<Product>> SearchStockAsync(string? search, bool? lowStockOnly, bool? outOfStockOnly, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryBatch>> GetBatchesAsync(int? productId, string? search, CancellationToken cancellationToken = default);
    Task<PagedResult<StockMovement>> GetMovementsAsync(int? productId, StockMovementType? type, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> AdjustStockAsync(int productId, int? batchId, decimal quantityDelta, string reason, string? userId, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> MarkDamagedAsync(int productId, int batchId, decimal quantity, string reason, string? userId, CancellationToken cancellationToken = default);
}

public class InventoryService : IInventoryService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public InventoryService(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<PagedResult<Product>> SearchStockAsync(
        string? search, bool? lowStockOnly, bool? outOfStockOnly, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);

        var query = _db.Products.AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Unit)
            .Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p => p.Sku.Contains(term) || p.Name.Contains(term));
        }

        if (outOfStockOnly == true) query = query.Where(p => p.CurrentStock <= 0);
        else if (lowStockOnly == true) query = query.Where(p => p.CurrentStock > 0 && p.CurrentStock <= p.ReorderLevel);

        query = query.OrderBy(p => p.Name);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<Product>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Search = search
        };
    }

    public async Task<IReadOnlyList<InventoryBatch>> GetBatchesAsync(int? productId, string? search, CancellationToken cancellationToken = default)
    {
        var query = _db.InventoryBatches.AsNoTracking()
            .Include(b => b.Product)
            .Include(b => b.Shipment)
            .AsQueryable();

        if (productId.HasValue) query = query.Where(b => b.ProductId == productId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(b => b.BatchNumber.Contains(term) || b.Product.Sku.Contains(term) || b.Shipment.ShipmentNumber.Contains(term));
        }

        return await query.OrderByDescending(b => b.ReceivedAt).Take(200).ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<StockMovement>> GetMovementsAsync(
        int? productId, StockMovementType? type, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);

        var query = _db.StockMovements.AsNoTracking()
            .Include(m => m.Product)
            .Include(m => m.InventoryBatch)
            .AsQueryable();

        if (productId.HasValue) query = query.Where(m => m.ProductId == productId);
        if (type.HasValue) query = query.Where(m => m.MovementType == type);
        if (from.HasValue) query = query.Where(m => m.OccurredAt >= from.Value);
        if (to.HasValue) query = query.Where(m => m.OccurredAt < to.Value.Date.AddDays(1));

        query = query.OrderByDescending(m => m.OccurredAt).ThenByDescending(m => m.Id);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<StockMovement>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<(bool Ok, string? Error)> AdjustStockAsync(
        int productId, int? batchId, decimal quantityDelta, string reason, string? userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) return (false, "Adjustment reason is required.");
        if (quantityDelta == 0) return (false, "Adjustment quantity cannot be zero.");

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null) return (false, "Product not found.");

        InventoryBatch? batch = null;
        if (batchId.HasValue)
        {
            batch = await _db.InventoryBatches.FirstOrDefaultAsync(b => b.Id == batchId && b.ProductId == productId, cancellationToken);
            if (batch is null) return (false, "Batch not found.");
            var newRemaining = batch.RemainingQuantity + quantityDelta;
            if (newRemaining < 0) return (false, "Batch quantity would become negative.");
            batch.RemainingQuantity = newRemaining;
            batch.UpdatedAt = DateTime.UtcNow;
            batch.UpdatedBy = userId;
        }

        var newStock = product.CurrentStock + quantityDelta;
        if (newStock < 0) return (false, "Product stock would become negative.");

        product.CurrentStock = newStock;
        product.StockValue = Math.Round(product.CurrentStock * (product.AverageCost ?? product.LatestLandedCost ?? 0), 2);
        product.UpdatedAt = DateTime.UtcNow;
        product.UpdatedBy = userId;

        _db.StockMovements.Add(new StockMovement
        {
            ProductId = product.Id,
            InventoryBatchId = batch?.Id,
            MovementType = StockMovementType.Adjustment,
            Quantity = quantityDelta,
            UnitCostPkr = product.AverageCost ?? product.LatestLandedCost ?? 0,
            QuantityAfter = product.CurrentStock,
            ReferenceType = "Adjustment",
            ReferenceNumber = batch?.BatchNumber,
            Reason = reason.Trim(),
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        });

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("StockAdjustment", nameof(Product), product.Id.ToString(), product.Sku, reason,
            newValues: new { quantityDelta, product.CurrentStock }, cancellationToken: cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> MarkDamagedAsync(
        int productId, int batchId, decimal quantity, string reason, string? userId, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0) return (false, "Damaged quantity must be greater than zero.");
        if (string.IsNullOrWhiteSpace(reason)) return (false, "Reason is required.");

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        var batch = await _db.InventoryBatches.FirstOrDefaultAsync(b => b.Id == batchId && b.ProductId == productId, cancellationToken);
        if (product is null || batch is null) return (false, "Product/batch not found.");
        if (batch.RemainingQuantity < quantity) return (false, "Not enough batch quantity to mark damaged.");

        batch.RemainingQuantity -= quantity;
        batch.DamagedQuantity += quantity;
        batch.UpdatedAt = DateTime.UtcNow;
        batch.UpdatedBy = userId;

        product.CurrentStock -= quantity;
        product.DamagedStock += quantity;
        if (product.CurrentStock < 0) return (false, "Product stock would become negative.");
        product.StockValue = Math.Round(product.CurrentStock * (product.AverageCost ?? product.LatestLandedCost ?? 0), 2);
        product.UpdatedAt = DateTime.UtcNow;
        product.UpdatedBy = userId;

        _db.StockMovements.Add(new StockMovement
        {
            ProductId = product.Id,
            InventoryBatchId = batch.Id,
            MovementType = StockMovementType.Damaged,
            Quantity = -quantity,
            UnitCostPkr = batch.LandedUnitCostPkr,
            QuantityAfter = product.CurrentStock,
            ReferenceType = "Damage",
            ReferenceNumber = batch.BatchNumber,
            Reason = reason.Trim(),
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        });

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("MarkDamaged", nameof(InventoryBatch), batch.Id.ToString(), batch.BatchNumber, reason, cancellationToken: cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return (true, null);
    }
}
