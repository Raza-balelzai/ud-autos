using Microsoft.EntityFrameworkCore;
using UD_Autos.Data;
using UD_Autos.Models.Common;
using UD_Autos.Models.Products;
using UD_Autos.Models.Purchases;

namespace UD_Autos.Services;

public class PurchaseOrderItemInput
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPriceRmb { get; set; }
    public string? Notes { get; set; }
}

public interface IPurchaseOrderService
{
    Task<PagedResult<PurchaseOrder>> SearchAsync(string? search, int? supplierId, PurchaseOrderStatus? status, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PurchaseOrder?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error, PurchaseOrder? Po)> CreateAsync(PurchaseOrder header, IReadOnlyList<PurchaseOrderItemInput> items, string? userId, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> UpdateAsync(PurchaseOrder header, IReadOnlyList<PurchaseOrderItemInput> items, string? userId, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> ChangeStatusAsync(int id, PurchaseOrderStatus newStatus, string? userId, string? reason = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseOrder>> GetOpenForSupplierAsync(int supplierId, CancellationToken cancellationToken = default);
}

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICodeGeneratorService _codes;
    private readonly ISupplierBalanceService _balances;

    public PurchaseOrderService(
        ApplicationDbContext db,
        IAuditService audit,
        ICodeGeneratorService codes,
        ISupplierBalanceService balances)
    {
        _db = db;
        _audit = audit;
        _codes = codes;
        _balances = balances;
    }

    public async Task<PagedResult<PurchaseOrder>> SearchAsync(
        string? search, int? supplierId, PurchaseOrderStatus? status, DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);

        var query = _db.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Supplier)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p => p.PoNumber.Contains(term) || p.Supplier.Name.Contains(term));
        }

        if (supplierId.HasValue) query = query.Where(p => p.SupplierId == supplierId);
        if (status.HasValue) query = query.Where(p => p.Status == status);
        if (from.HasValue) query = query.Where(p => p.OrderDate >= from.Value.Date);
        if (to.HasValue) query = query.Where(p => p.OrderDate <= to.Value.Date);

        query = query.OrderByDescending(p => p.OrderDate).ThenByDescending(p => p.Id);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<PurchaseOrder>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Search = search
        };
    }

    public Task<PurchaseOrder?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => _db.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<(bool Ok, string? Error, PurchaseOrder? Po)> CreateAsync(
        PurchaseOrder header, IReadOnlyList<PurchaseOrderItemInput> items, string? userId, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateItemsAsync(header, items, cancellationToken);
        if (validation is not null) return (false, validation, null);

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(header.PoNumber))
            header.PoNumber = await _codes.NextPoNumberAsync(cancellationToken);

        header.PoNumber = header.PoNumber.Trim().ToUpperInvariant();
        if (await _db.PurchaseOrders.AnyAsync(p => p.PoNumber == header.PoNumber, cancellationToken))
            return (false, $"PO number '{header.PoNumber}' already exists.", null);

        ApplyTotals(header, items);
        header.CreatedAt = DateTime.UtcNow;
        header.CreatedBy = userId;
        header.Items = BuildItems(items, userId);

        _db.PurchaseOrders.Add(header);
        await _db.SaveChangesAsync(cancellationToken);

        if (header.Status != PurchaseOrderStatus.Draft)
        {
            await RecordRmbHistoryAsync(header, userId, cancellationToken);
            await _balances.RecalculateAsync(header.SupplierId, cancellationToken);
        }

        await _audit.LogAsync("Create", nameof(PurchaseOrder), header.Id.ToString(), header.PoNumber, cancellationToken: cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return (true, null, header);
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(
        PurchaseOrder header, IReadOnlyList<PurchaseOrderItemInput> items, string? userId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.PurchaseOrders.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == header.Id, cancellationToken);
        if (existing is null) return (false, "Purchase order not found.");
        if (existing.Status >= PurchaseOrderStatus.Shipped)
            return (false, "Shipped/completed POs cannot be edited. Use status workflow or Phase 3 corrections.");

        var validation = await ValidateItemsAsync(header, items, cancellationToken);
        if (validation is not null) return (false, validation);

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        existing.SupplierId = header.SupplierId;
        existing.OrderDate = header.OrderDate.Date;
        existing.EstimatedExchangeRate = header.EstimatedExchangeRate;
        existing.Notes = header.Notes;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = userId;

        _db.PurchaseOrderItems.RemoveRange(existing.Items);
        existing.Items = BuildItems(items, userId);
        ApplyTotals(existing, items);

        await _db.SaveChangesAsync(cancellationToken);
        await _balances.RecalculateAsync(existing.SupplierId, cancellationToken);
        await _audit.LogAsync("Update", nameof(PurchaseOrder), existing.Id.ToString(), existing.PoNumber, cancellationToken: cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> ChangeStatusAsync(
        int id, PurchaseOrderStatus newStatus, string? userId, string? reason = null, CancellationToken cancellationToken = default)
    {
        var po = await _db.PurchaseOrders.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (po is null) return (false, "Purchase order not found.");
        if (!po.Items.Any()) return (false, "Add at least one line item before changing status.");

        var old = po.Status;
        if (newStatus == old) return (true, null);

        // Allow forward moves and Owner-controlled corrections via any change with reason.
        if ((int)newStatus < (int)old && string.IsNullOrWhiteSpace(reason))
            return (false, "Status rollback requires a reason.");

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        po.Status = newStatus;
        po.UpdatedAt = DateTime.UtcNow;
        po.UpdatedBy = userId;
        await _db.SaveChangesAsync(cancellationToken);

        if (old == PurchaseOrderStatus.Draft && newStatus != PurchaseOrderStatus.Draft)
            await RecordRmbHistoryAsync(po, userId, cancellationToken);

        await _balances.RecalculateAsync(po.SupplierId, cancellationToken);
        await _audit.LogAsync("StatusChange", nameof(PurchaseOrder), po.Id.ToString(), po.PoNumber, reason, new { old, newStatus }, cancellationToken: cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return (true, null);
    }

    public async Task<IReadOnlyList<PurchaseOrder>> GetOpenForSupplierAsync(int supplierId, CancellationToken cancellationToken = default)
        => await _db.PurchaseOrders.AsNoTracking()
            .Where(p => p.SupplierId == supplierId && p.Status != PurchaseOrderStatus.Completed && p.Status != PurchaseOrderStatus.Draft)
            .OrderByDescending(p => p.OrderDate)
            .ToListAsync(cancellationToken);

    private async Task<string?> ValidateItemsAsync(PurchaseOrder header, IReadOnlyList<PurchaseOrderItemInput> items, CancellationToken cancellationToken)
    {
        if (header.SupplierId <= 0) return "Supplier is required.";
        if (!await _db.Suppliers.AnyAsync(s => s.Id == header.SupplierId && s.IsActive, cancellationToken))
            return "Supplier not found or inactive.";
        if (items.Count == 0) return "Add at least one product line.";

        foreach (var item in items)
        {
            if (item.ProductId <= 0) return "Each line needs a product.";
            if (item.Quantity <= 0) return "Quantity must be greater than zero.";
            if (item.UnitPriceRmb < 0) return "Unit RMB price cannot be negative.";
            if (!await _db.Products.AnyAsync(p => p.Id == item.ProductId && p.IsActive, cancellationToken))
                return $"Product Id {item.ProductId} not found.";
        }

        return null;
    }

    private static void ApplyTotals(PurchaseOrder header, IReadOnlyList<PurchaseOrderItemInput> items)
    {
        header.TotalRmb = Math.Round(items.Sum(i => i.Quantity * i.UnitPriceRmb), 4);
        if (header.EstimatedExchangeRate is > 0)
            header.EstimatedPkr = Math.Round(header.TotalRmb * header.EstimatedExchangeRate.Value, 2);
        else
            header.EstimatedPkr = 0;
        header.BalanceRmb = Math.Round(header.TotalRmb - header.PaidRmb, 4);
    }

    private static List<PurchaseOrderItem> BuildItems(IReadOnlyList<PurchaseOrderItemInput> items, string? userId)
        => items.Select(i => new PurchaseOrderItem
        {
            ProductId = i.ProductId,
            Quantity = i.Quantity,
            UnitPriceRmb = i.UnitPriceRmb,
            LineTotalRmb = Math.Round(i.Quantity * i.UnitPriceRmb, 4),
            Notes = i.Notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        }).ToList();

    private async Task RecordRmbHistoryAsync(PurchaseOrder po, string? userId, CancellationToken cancellationToken)
    {
        foreach (var item in po.Items)
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId, cancellationToken);
            if (product is null) continue;

            if (product.LatestRmbPrice != item.UnitPriceRmb)
            {
                product.PreviousRmbPrice = product.LatestRmbPrice;
                product.LatestRmbPrice = item.UnitPriceRmb;
                product.UpdatedAt = DateTime.UtcNow;
                product.UpdatedBy = userId;

                _db.ProductRmbPriceHistories.Add(new ProductRmbPriceHistory
                {
                    ProductId = product.Id,
                    SupplierId = po.SupplierId,
                    UnitPriceRmb = item.UnitPriceRmb,
                    EffectiveDate = DateTime.UtcNow,
                    SourceReference = po.PoNumber,
                    Notes = "From purchase order",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
