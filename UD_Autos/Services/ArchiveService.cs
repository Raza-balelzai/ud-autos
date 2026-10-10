using Microsoft.EntityFrameworkCore;
using UD_Autos.Data;
using UD_Autos.Models.Customers;
using UD_Autos.Models.Finance;
using UD_Autos.Models.Hawala;
using UD_Autos.Models.Inventory;
using UD_Autos.Models.Products;
using UD_Autos.Models.Purchases;
using UD_Autos.Models.Sales;
using UD_Autos.Models.Settings;
using UD_Autos.Models.Shipments;
using UD_Autos.Models.Suppliers;

namespace UD_Autos.Services;

public class ArchiveRow
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Note { get; set; } = "";
    public bool CanHardDelete { get; set; }
    public string? BlockReason { get; set; }
}

public interface IArchiveService
{
    Task<IReadOnlyList<ArchiveRow>> ListAsync(string tab, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> ReactivateAsync(string tab, int id, string? userId, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> HardDeleteAsync(string tab, int id, string? userId, CancellationToken cancellationToken = default);
}

public class ArchiveService : IArchiveService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly IWebHostEnvironment _env;

    public ArchiveService(ApplicationDbContext db, IAuditService audit, IWebHostEnvironment env)
    {
        _db = db;
        _audit = audit;
        _env = env;
    }

    public static string NormalizeTab(string? tab) => (tab ?? "").Trim().ToLowerInvariant() switch
    {
        "customers" => "customers",
        "suppliers" => "suppliers",
        "categories" => "categories",
        "units" => "units",
        "expenses" => "expenses",
        _ => "products"
    };

    public Task<IReadOnlyList<ArchiveRow>> ListAsync(string tab, CancellationToken cancellationToken = default)
        => NormalizeTab(tab) switch
        {
            "customers" => ListCustomersAsync(cancellationToken),
            "suppliers" => ListSuppliersAsync(cancellationToken),
            "categories" => ListCategoriesAsync(cancellationToken),
            "units" => ListUnitsAsync(cancellationToken),
            "expenses" => ListExpenseCategoriesAsync(cancellationToken),
            _ => ListProductsAsync(cancellationToken)
        };

    public async Task<(bool Ok, string? Error)> ReactivateAsync(string tab, int id, string? userId, CancellationToken cancellationToken = default)
    {
        switch (NormalizeTab(tab))
        {
            case "customers":
                return await ReactivateAsync<Customer>(id, userId, "Customer", c => c.CustomerCode, cancellationToken);
            case "suppliers":
                return await ReactivateAsync<Supplier>(id, userId, "Supplier", s => s.SupplierCode, cancellationToken);
            case "categories":
                return await ReactivateAsync<ProductCategory>(id, userId, "ProductCategory", c => c.Name, cancellationToken);
            case "units":
                return await ReactivateAsync<UnitOfMeasure>(id, userId, "UnitOfMeasure", u => u.Abbreviation, cancellationToken);
            case "expenses":
                return await ReactivateAsync<ExpenseCategory>(id, userId, "ExpenseCategory", c => c.Name, cancellationToken);
            default:
                return await ReactivateAsync<Product>(id, userId, "Product", p => p.Sku, cancellationToken);
        }
    }

    public Task<(bool Ok, string? Error)> HardDeleteAsync(string tab, int id, string? userId, CancellationToken cancellationToken = default)
        => NormalizeTab(tab) switch
        {
            "customers" => HardDeleteCustomerAsync(id, cancellationToken),
            "suppliers" => HardDeleteSupplierAsync(id, cancellationToken),
            "categories" => HardDeleteCategoryAsync(id, cancellationToken),
            "units" => HardDeleteUnitAsync(id, cancellationToken),
            "expenses" => HardDeleteExpenseCategoryAsync(id, cancellationToken),
            _ => HardDeleteProductAsync(id, cancellationToken)
        };

    private async Task<IReadOnlyList<ArchiveRow>> ListProductsAsync(CancellationToken cancellationToken)
    {
        var products = await _db.Products.AsNoTracking()
            .Where(p => !p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Sku, p.Name, p.ProductKind })
            .ToListAsync(cancellationToken);

        var ids = products.Select(p => p.Id).ToList();
        var reasons = await ProductBlockReasonsAsync(ids, cancellationToken);
        return products.Select(p =>
        {
            reasons.TryGetValue(p.Id, out var reason);
            return new ArchiveRow
            {
                Id = p.Id,
                Code = p.Sku ?? "",
                Name = p.Name,
                Note = p.ProductKind == ProductKind.CompleteSet ? "Complete set" : "Single item",
                CanHardDelete = reason is null,
                BlockReason = reason
            };
        }).ToList();
    }

    private async Task<IReadOnlyList<ArchiveRow>> ListCustomersAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.Customers.AsNoTracking()
            .Where(c => !c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.CustomerCode, c.Name, c.ShopName, c.City })
            .ToListAsync(cancellationToken);
        var ids = rows.Select(c => c.Id).ToList();
        var reasons = await CustomerBlockReasonsAsync(ids, cancellationToken);
        return rows.Select(c =>
        {
            reasons.TryGetValue(c.Id, out var reason);
            return new ArchiveRow
            {
                Id = c.Id,
                Code = c.CustomerCode ?? "",
                Name = string.IsNullOrWhiteSpace(c.ShopName) ? c.Name : c.ShopName,
                Note = c.City ?? "",
                CanHardDelete = reason is null,
                BlockReason = reason
            };
        }).ToList();
    }

    private async Task<IReadOnlyList<ArchiveRow>> ListSuppliersAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.Suppliers.AsNoTracking()
            .Where(s => !s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new { s.Id, s.SupplierCode, s.Name, s.City, s.Country })
            .ToListAsync(cancellationToken);
        var ids = rows.Select(s => s.Id).ToList();
        var reasons = await SupplierBlockReasonsAsync(ids, cancellationToken);
        return rows.Select(s =>
        {
            reasons.TryGetValue(s.Id, out var reason);
            var place = string.Join(", ", new[] { s.City, s.Country }.Where(x => !string.IsNullOrWhiteSpace(x)));
            return new ArchiveRow
            {
                Id = s.Id,
                Code = s.SupplierCode ?? "",
                Name = s.Name,
                Note = place,
                CanHardDelete = reason is null,
                BlockReason = reason
            };
        }).ToList();
    }

    private async Task<IReadOnlyList<ArchiveRow>> ListCategoriesAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.ProductCategories.AsNoTracking()
            .Where(c => !c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
        return rows.Select(c => new ArchiveRow
        {
            Id = c.Id,
            Code = "",
            Name = c.Name,
            Note = c.Description ?? "",
            CanHardDelete = true
        }).ToList();
    }

    private async Task<IReadOnlyList<ArchiveRow>> ListUnitsAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.UnitsOfMeasure.AsNoTracking()
            .Where(u => !u.IsActive)
            .OrderBy(u => u.Name)
            .ToListAsync(cancellationToken);
        return rows.Select(u => new ArchiveRow
        {
            Id = u.Id,
            Code = u.Abbreviation,
            Name = u.Name,
            Note = "",
            CanHardDelete = true
        }).ToList();
    }

    private async Task<IReadOnlyList<ArchiveRow>> ListExpenseCategoriesAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.ExpenseCategories.AsNoTracking()
            .Where(c => !c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
        var ids = rows.Select(c => c.Id).ToList();
        var used = ids.Count == 0
            ? new HashSet<int>()
            : (await _db.OperatingExpenses.AsNoTracking()
                .Where(e => ids.Contains(e.CategoryId))
                .Select(e => e.CategoryId)
                .Distinct()
                .ToListAsync(cancellationToken)).ToHashSet();
        return rows.Select(c =>
        {
            var blocked = used.Contains(c.Id);
            return new ArchiveRow
            {
                Id = c.Id,
                Code = "",
                Name = c.Name,
                Note = c.IsDirectImportCost ? "Import cost" : (c.Description ?? ""),
                CanHardDelete = !blocked,
                BlockReason = blocked ? "Used on an expense." : null
            };
        }).ToList();
    }

    private async Task<(bool Ok, string? Error)> ReactivateAsync<T>(
        int id, string? userId, string entityType, Func<T, string?> reference, CancellationToken cancellationToken)
        where T : class
    {
        var entity = await _db.Set<T>().FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id, cancellationToken);
        if (entity is null) return (false, "Record not found.");

        var entry = _db.Entry(entity);
        if (entry.Property("IsActive").CurrentValue is true) return (false, "This record is already active.");

        entry.Property("IsActive").CurrentValue = true;
        entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
        entry.Property("UpdatedBy").CurrentValue = userId;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("Reactivate", entityType, id.ToString(), reference(entity), cancellationToken: cancellationToken);
        return (true, null);
    }

    private async Task<(bool Ok, string? Error)> HardDeleteProductAsync(int id, CancellationToken cancellationToken)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null) return (false, "Product not found.");
        if (product.IsActive) return (false, "Deactivate the product before deleting it.");

        var reason = await ProductBlockReasonAsync(id, cancellationToken);
        if (reason is not null) return (false, reason);

        var imagePath = product.ImagePath;
        var sku = product.Sku;
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var components = await _db.ProductSetComponents.Where(c => c.SetProductId == id).ToListAsync(cancellationToken);
            var rmb = await _db.ProductRmbPriceHistories.Where(h => h.ProductId == id).ToListAsync(cancellationToken);
            var landed = await _db.ProductLandedCostHistories.Where(h => h.ProductId == id).ToListAsync(cancellationToken);
            var movements = await _db.StockMovements.Where(m => m.ProductId == id).ToListAsync(cancellationToken);
            _db.ProductSetComponents.RemoveRange(components);
            _db.ProductRmbPriceHistories.RemoveRange(rmb);
            _db.ProductLandedCostHistories.RemoveRange(landed);
            _db.StockMovements.RemoveRange(movements);
            _db.Products.Remove(product);
            await _db.SaveChangesAsync(cancellationToken);
            await _audit.LogAsync("HardDelete", nameof(Product), id.ToString(), sku, cancellationToken: cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return (false, "This product is still used and cannot be deleted.");
        }

        if (!ProductImageFiles.TryDelete(_env, imagePath))
            return (true, "Product was deleted, but its image file could not be removed from the server.");

        return (true, null);
    }

    private async Task<(bool Ok, string? Error)> HardDeleteCustomerAsync(int id, CancellationToken cancellationToken)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (customer is null) return (false, "Customer not found.");
        if (customer.IsActive) return (false, "Deactivate the customer before deleting it.");
        var reason = await CustomerBlockReasonAsync(id, cancellationToken);
        if (reason is not null) return (false, reason);
        return await RemoveAndAuditAsync(customer, nameof(Customer), id, customer.CustomerCode, "This customer is still used and cannot be deleted.", cancellationToken);
    }

    private async Task<(bool Ok, string? Error)> HardDeleteSupplierAsync(int id, CancellationToken cancellationToken)
    {
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (supplier is null) return (false, "Supplier not found.");
        if (supplier.IsActive) return (false, "Deactivate the supplier before deleting it.");
        var reason = await SupplierBlockReasonAsync(id, cancellationToken);
        if (reason is not null) return (false, reason);
        return await RemoveAndAuditAsync(supplier, nameof(Supplier), id, supplier.SupplierCode, "This supplier is still used and cannot be deleted.", cancellationToken);
    }

    private async Task<(bool Ok, string? Error)> HardDeleteCategoryAsync(int id, CancellationToken cancellationToken)
    {
        var category = await _db.ProductCategories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null) return (false, "Category not found.");
        if (category.IsActive) return (false, "Deactivate the category before deleting it.");
        return await RemoveAndAuditAsync(category, nameof(ProductCategory), id, category.Name, "This category is still used and cannot be deleted.", cancellationToken);
    }

    private async Task<(bool Ok, string? Error)> HardDeleteUnitAsync(int id, CancellationToken cancellationToken)
    {
        var unit = await _db.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (unit is null) return (false, "Unit not found.");
        if (unit.IsActive) return (false, "Deactivate the unit before deleting it.");
        return await RemoveAndAuditAsync(unit, nameof(UnitOfMeasure), id, unit.Abbreviation, "This unit is still used and cannot be deleted.", cancellationToken);
    }

    private async Task<(bool Ok, string? Error)> HardDeleteExpenseCategoryAsync(int id, CancellationToken cancellationToken)
    {
        var category = await _db.ExpenseCategories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null) return (false, "Expense category not found.");
        if (category.IsActive) return (false, "Deactivate the expense category before deleting it.");
        if (await _db.OperatingExpenses.AnyAsync(e => e.CategoryId == id, cancellationToken))
            return (false, "Used on an expense.");
        return await RemoveAndAuditAsync(category, nameof(ExpenseCategory), id, category.Name, "This expense category is still used and cannot be deleted.", cancellationToken);
    }

    private async Task<(bool Ok, string? Error)> RemoveAndAuditAsync<T>(
        T entity, string entityType, int id, string? reference, string failure, CancellationToken cancellationToken)
        where T : class
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _db.Set<T>().Remove(entity);
            await _db.SaveChangesAsync(cancellationToken);
            await _audit.LogAsync("HardDelete", entityType, id.ToString(), reference, cancellationToken: cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return (true, null);
        }
        catch (DbUpdateException)
        {
            return (false, failure);
        }
    }

    private async Task<string?> ProductBlockReasonAsync(int id, CancellationToken cancellationToken)
    {
        var map = await ProductBlockReasonsAsync(new[] { id }, cancellationToken);
        return map.TryGetValue(id, out var reason) ? reason : null;
    }

    private async Task<Dictionary<int, string>> ProductBlockReasonsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken)
    {
        var reasons = new Dictionary<int, string>();
        if (ids.Count == 0) return reasons;
        await RememberAsync(reasons, _db.InvoiceItems.Where(x => ids.Contains(x.ProductId)).Select(x => x.ProductId), "Used on an invoice.", cancellationToken);
        await RememberAsync(reasons, _db.InvoiceSetComponents.Where(x => ids.Contains(x.ComponentProductId)).Select(x => x.ComponentProductId), "Used on a set invoice.", cancellationToken);
        await RememberAsync(reasons, _db.SalesReturnItems.Where(x => ids.Contains(x.ProductId)).Select(x => x.ProductId), "Used on a sales return.", cancellationToken);
        await RememberAsync(reasons, _db.PurchaseOrderItems.Where(x => ids.Contains(x.ProductId)).Select(x => x.ProductId), "Used on a purchase order.", cancellationToken);
        await RememberAsync(reasons, _db.ShipmentItems.Where(x => ids.Contains(x.ProductId)).Select(x => x.ProductId), "Used on a shipment.", cancellationToken);
        await RememberAsync(reasons, _db.InventoryBatches.Where(x => ids.Contains(x.ProductId)).Select(x => x.ProductId), "Has stock batches.", cancellationToken);
        await RememberAsync(reasons, _db.SupplierReturnItems.Where(x => ids.Contains(x.ProductId)).Select(x => x.ProductId), "Used on a supplier return.", cancellationToken);
        await RememberAsync(reasons, _db.ProductSetComponents.Where(x => ids.Contains(x.ComponentProductId)).Select(x => x.ComponentProductId), "Used inside a product set.", cancellationToken);
        return reasons;
    }

    private async Task<string?> CustomerBlockReasonAsync(int id, CancellationToken cancellationToken)
    {
        var map = await CustomerBlockReasonsAsync(new[] { id }, cancellationToken);
        return map.TryGetValue(id, out var reason) ? reason : null;
    }

    private async Task<Dictionary<int, string>> CustomerBlockReasonsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken)
    {
        var reasons = new Dictionary<int, string>();
        if (ids.Count == 0) return reasons;
        await RememberAsync(reasons, _db.Invoices.Where(x => ids.Contains(x.CustomerId)).Select(x => x.CustomerId), "Used on an invoice.", cancellationToken);
        await RememberAsync(reasons, _db.CustomerPayments.Where(x => ids.Contains(x.CustomerId)).Select(x => x.CustomerId), "Has a payment.", cancellationToken);
        await RememberAsync(reasons, _db.CustomerLedgerEntries.Where(x => ids.Contains(x.CustomerId)).Select(x => x.CustomerId), "Has a khata entry.", cancellationToken);
        await RememberAsync(reasons, _db.SalesReturns.Where(x => ids.Contains(x.CustomerId)).Select(x => x.CustomerId), "Used on a sales return.", cancellationToken);
        return reasons;
    }

    private async Task<string?> SupplierBlockReasonAsync(int id, CancellationToken cancellationToken)
    {
        var map = await SupplierBlockReasonsAsync(new[] { id }, cancellationToken);
        return map.TryGetValue(id, out var reason) ? reason : null;
    }

    private async Task<Dictionary<int, string>> SupplierBlockReasonsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken)
    {
        var reasons = new Dictionary<int, string>();
        if (ids.Count == 0) return reasons;
        await RememberAsync(reasons, _db.PurchaseOrders.Where(x => ids.Contains(x.SupplierId)).Select(x => x.SupplierId), "Used on a purchase order.", cancellationToken);
        await RememberAsync(reasons, _db.HawalaPayments.Where(x => ids.Contains(x.SupplierId)).Select(x => x.SupplierId), "Used on a hawala payment.", cancellationToken);
        await RememberAsync(reasons, _db.SupplierReturns.Where(x => ids.Contains(x.SupplierId)).Select(x => x.SupplierId), "Used on a supplier return.", cancellationToken);
        await RememberAsync(reasons, _db.ChinaWarehouseReceivingSlips.Where(x => ids.Contains(x.SupplierId)).Select(x => x.SupplierId), "Used on a shipment slip.", cancellationToken);
        var shipmentSuppliers = await _db.ShipmentItems.AsNoTracking()
            .Where(x => x.SupplierId != null && ids.Contains(x.SupplierId.Value))
            .Select(x => x.SupplierId)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var supplierId in shipmentSuppliers)
        {
            if (supplierId is int value)
                reasons.TryAdd(value, "Used on a shipment.");
        }
        return reasons;
    }

    private static async Task RememberAsync(Dictionary<int, string> reasons, IQueryable<int> ids, string reason, CancellationToken cancellationToken)
    {
        var hits = await ids.Distinct().ToListAsync(cancellationToken);
        foreach (var id in hits)
            reasons.TryAdd(id, reason);
    }

}
