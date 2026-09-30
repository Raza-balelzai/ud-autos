using Microsoft.EntityFrameworkCore;
using UD_Autos.Data;
using UD_Autos.Models.Common;
using UD_Autos.Models.Inventory;
using UD_Autos.Models.Products;

namespace UD_Autos.Services;

public class SupplierReturnLineInput
{
    public int InventoryBatchId { get; set; }
    public decimal Quantity { get; set; }
    public string? Notes { get; set; }
}

public interface ISupplierReturnService
{
    Task<PagedResult<SupplierReturn>> SearchAsync(string? search, int? supplierId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<SupplierReturn?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error, SupplierReturn? Return)> CreateAsync(
        int supplierId,
        DateTime returnDate,
        string reason,
        int? shipmentId,
        string? notes,
        IReadOnlyList<SupplierReturnLineInput> lines,
        string? userId,
        CancellationToken cancellationToken = default);
}

public class SupplierReturnService : ISupplierReturnService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICodeGeneratorService _codes;

    public SupplierReturnService(ApplicationDbContext db, IAuditService audit, ICodeGeneratorService codes)
    {
        _db = db;
        _audit = audit;
        _codes = codes;
    }

    public async Task<PagedResult<SupplierReturn>> SearchAsync(
        string? search, int? supplierId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);

        var query = _db.SupplierReturns.AsNoTracking()
            .Include(r => r.Supplier)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r => r.ReturnNumber.Contains(term) || r.Supplier.Name.Contains(term) || r.Reason.Contains(term));
        }
        if (supplierId.HasValue) query = query.Where(r => r.SupplierId == supplierId);
        if (from.HasValue) query = query.Where(r => r.ReturnDate >= from.Value.Date);
        if (to.HasValue) query = query.Where(r => r.ReturnDate <= to.Value.Date);

        query = query.OrderByDescending(r => r.ReturnDate).ThenByDescending(r => r.Id);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<SupplierReturn>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Search = search
        };
    }

    public Task<SupplierReturn?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => _db.SupplierReturns
            .Include(r => r.Supplier)
            .Include(r => r.Shipment)
            .Include(r => r.Items).ThenInclude(i => i.Product)
            .Include(r => r.Items).ThenInclude(i => i.InventoryBatch)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<(bool Ok, string? Error, SupplierReturn? Return)> CreateAsync(
        int supplierId,
        DateTime returnDate,
        string reason,
        int? shipmentId,
        string? notes,
        IReadOnlyList<SupplierReturnLineInput> lines,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) return (false, "Reason is required.", null);
        lines = lines.Where(l => l.InventoryBatchId > 0 && l.Quantity > 0).ToList();
        if (lines.Count == 0) return (false, "Add at least one batch line with quantity.", null);

        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId, cancellationToken);
        if (supplier is null) return (false, "Supplier not found.", null);

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var supplierReturn = new SupplierReturn
        {
            ReturnNumber = await _codes.NextSupplierReturnNumberAsync(cancellationToken),
            ReturnDate = returnDate.Date,
            SupplierId = supplierId,
            ShipmentId = shipmentId,
            Reason = reason.Trim(),
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        decimal totalQty = 0;
        foreach (var line in lines)
        {
            var batch = await _db.InventoryBatches
                .Include(b => b.Product)
                .FirstOrDefaultAsync(b => b.Id == line.InventoryBatchId, cancellationToken);
            if (batch is null) return (false, $"Batch #{line.InventoryBatchId} not found.", null);

            var available = batch.RemainingQuantity - batch.ReservedQuantity;
            if (line.Quantity > available)
                return (false, $"Batch {batch.BatchNumber}: only {available:N2} available (remaining minus reserved).", null);

            var product = batch.Product ?? await _db.Products.FirstAsync(p => p.Id == batch.ProductId, cancellationToken);
            var productAvailable = product.CurrentStock - product.ReservedQuantity;
            if (line.Quantity > productAvailable)
                return (false, $"{product.Sku}: only {productAvailable:N2} available to return.", null);

            batch.RemainingQuantity -= line.Quantity;
            batch.UpdatedAt = DateTime.UtcNow;
            batch.UpdatedBy = userId;

            product.CurrentStock -= line.Quantity;
            product.StockValue = Math.Round(product.CurrentStock * (product.AverageCost ?? product.LatestLandedCost ?? 0), 2);
            product.UpdatedAt = DateTime.UtcNow;
            product.UpdatedBy = userId;

            supplierReturn.Items.Add(new SupplierReturnItem
            {
                ProductId = product.Id,
                InventoryBatchId = batch.Id,
                Quantity = line.Quantity,
                UnitCostPkr = batch.LandedUnitCostPkr,
                Notes = line.Notes,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            });

            _db.StockMovements.Add(new StockMovement
            {
                ProductId = product.Id,
                InventoryBatchId = batch.Id,
                MovementType = StockMovementType.SupplierReturn,
                Quantity = -line.Quantity,
                UnitCostPkr = batch.LandedUnitCostPkr,
                QuantityAfter = product.CurrentStock,
                ReferenceType = "SupplierReturn",
                ReferenceNumber = supplierReturn.ReturnNumber,
                Reason = reason.Trim(),
                OccurredAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            });

            totalQty += line.Quantity;
        }

        supplierReturn.TotalQuantity = totalQty;
        _db.SupplierReturns.Add(supplierReturn);
        await _db.SaveChangesAsync(cancellationToken);

        // Fix movement reference numbers now that return is persisted with number
        var moves = await _db.StockMovements
            .Where(m => m.ReferenceType == "SupplierReturn" && m.ReferenceNumber == supplierReturn.ReturnNumber)
            .ToListAsync(cancellationToken);
        foreach (var m in moves)
            m.ReferenceId = supplierReturn.Id.ToString();
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(
            "SupplierReturn",
            nameof(SupplierReturn),
            supplierReturn.Id.ToString(),
            supplierReturn.ReturnNumber,
            reason,
            newValues: new { supplierReturn.SupplierId, supplierReturn.TotalQuantity },
            cancellationToken: cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return (true, null, await GetByIdAsync(supplierReturn.Id, cancellationToken));
    }
}
