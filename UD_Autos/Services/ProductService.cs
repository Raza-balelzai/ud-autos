using Microsoft.EntityFrameworkCore;
using UD_Autos.Data;
using UD_Autos.Models.Common;
using UD_Autos.Models.Products;
using UD_Autos.Models.ViewModels;

namespace UD_Autos.Services;

public interface IProductService
{
    Task<PagedResult<Product>> SearchAsync(string? search, int? categoryId, string? sortBy, bool sortDesc, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Product?> GetByIdAsync(int id, bool includeHistory = false, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<ProductPurchaseHistoryRow> Purchases, IReadOnlyList<ProductShipmentHistoryRow> Shipments, IReadOnlyList<ProductSalesHistoryRow> Sales)> GetHistoryAsync(int productId, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error, Product? Product)> CreateAsync(Product product, string? userId, CancellationToken cancellationToken = default, IReadOnlyList<SetComponentInput>? components = null);
    Task<(bool Ok, string? Error)> UpdateAsync(Product product, string? userId, string? priceChangeReason = null, CancellationToken cancellationToken = default, IReadOnlyList<SetComponentInput>? components = null);
    Task<(bool Ok, string? Error)> SoftDeleteAsync(int id, string? userId, CancellationToken cancellationToken = default);
}

public class ProductService : IProductService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICodeGeneratorService _codes;

    public ProductService(ApplicationDbContext db, IAuditService audit, ICodeGeneratorService codes)
    {
        _db = db;
        _audit = audit;
        _codes = codes;
    }

    public async Task<PagedResult<Product>> SearchAsync(
        string? search, int? categoryId, string? sortBy, bool sortDesc, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);

        var query = _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Unit)
            .Include(p => p.DefaultSupplier)
            .Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                p.Sku.Contains(term) ||
                p.Name.Contains(term) ||
                (p.Barcode != null && p.Barcode.Contains(term)) ||
                (p.Brand != null && p.Brand.Contains(term)) ||
                (p.VehicleBrand != null && p.VehicleBrand.Contains(term)) ||
                (p.VehicleModel != null && p.VehicleModel.Contains(term)));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId);
        }

        query = (sortBy?.ToLowerInvariant()) switch
        {
            "sku" => sortDesc ? query.OrderByDescending(p => p.Sku) : query.OrderBy(p => p.Sku),
            "name" => sortDesc ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
            "stock" => sortDesc ? query.OrderByDescending(p => p.CurrentStock) : query.OrderBy(p => p.CurrentStock),
            "price" => sortDesc ? query.OrderByDescending(p => p.WholesalePrice) : query.OrderBy(p => p.WholesalePrice),
            _ => sortDesc ? query.OrderByDescending(p => p.Id) : query.OrderBy(p => p.Name)
        };

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        await ProductSetAvailability.AttachAsync(_db, items, cancellationToken);

        return new PagedResult<Product>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Search = search,
            SortBy = sortBy,
            SortDesc = sortDesc
        };
    }

    public async Task<Product?> GetByIdAsync(int id, bool includeHistory = false, CancellationToken cancellationToken = default)
    {
        var query = _db.Products
            .Include(p => p.Category)
            .Include(p => p.Unit)
            .Include(p => p.DefaultSupplier)
            .Include(p => p.SetComponents.Where(c => c.IsActive))
                .ThenInclude(c => c.ComponentProduct)
            .AsQueryable();

        if (includeHistory)
        {
            query = query
                .Include(p => p.RmbPriceHistory.OrderByDescending(h => h.EffectiveDate))
                .Include(p => p.LandedCostHistory.OrderByDescending(h => h.EffectiveDate));
        }

        var product = await query.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is not null)
            await ProductSetAvailability.AttachAsync(_db, new[] { product }, cancellationToken);
        return product;
    }

    public async Task<(IReadOnlyList<ProductPurchaseHistoryRow> Purchases, IReadOnlyList<ProductShipmentHistoryRow> Shipments, IReadOnlyList<ProductSalesHistoryRow> Sales)> GetHistoryAsync(
        int productId, CancellationToken cancellationToken = default)
    {
        const int take = 50;

        var purchases = await _db.PurchaseOrderItems.AsNoTracking()
            .Where(i => i.ProductId == productId)
            .OrderByDescending(i => i.PurchaseOrder.OrderDate)
            .ThenByDescending(i => i.Id)
            .Take(take)
            .Select(i => new ProductPurchaseHistoryRow
            {
                PurchaseOrderId = i.PurchaseOrderId,
                PoNumber = i.PurchaseOrder.PoNumber,
                OrderDate = i.PurchaseOrder.OrderDate,
                SupplierName = i.PurchaseOrder.Supplier.Name,
                Quantity = i.Quantity,
                UnitPriceRmb = i.UnitPriceRmb,
                LineTotalRmb = i.LineTotalRmb
            })
            .ToListAsync(cancellationToken);

        var shipments = await _db.ShipmentItems.AsNoTracking()
            .Where(i => i.ProductId == productId)
            .OrderByDescending(i => i.Shipment.Id)
            .Take(take)
            .Select(i => new ProductShipmentHistoryRow
            {
                ShipmentId = i.ShipmentId,
                ShipmentNumber = i.Shipment.ShipmentNumber,
                Status = i.Shipment.Status,
                ExpectedQuantity = i.ExpectedQuantity,
                ActualQuantity = i.ActualQuantity,
                ChinaWarehouseReceivedDate = i.Shipment.ChinaWarehouseReceivedDate,
                TarnolArrivalDate = i.Shipment.TarnolArrivalDate,
                LandedUnitCostPkr = i.LandedUnitCostPkr
            })
            .ToListAsync(cancellationToken);

        var sales = await _db.InvoiceItems.AsNoTracking()
            .Where(i => i.ProductId == productId && !i.Invoice.IsVoided)
            .OrderByDescending(i => i.Invoice.InvoiceDate)
            .ThenByDescending(i => i.Id)
            .Take(take)
            .Select(i => new ProductSalesHistoryRow
            {
                InvoiceId = i.InvoiceId,
                InvoiceNumber = i.Invoice.InvoiceNumber,
                InvoiceDate = i.Invoice.InvoiceDate,
                CustomerName = i.Invoice.Customer.ShopName ?? i.Invoice.Customer.Name,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                LineTotal = i.LineTotal
            })
            .ToListAsync(cancellationToken);

        return (purchases, shipments, sales);
    }

    public async Task<(bool Ok, string? Error, Product? Product)> CreateAsync(
        Product product, string? userId, CancellationToken cancellationToken = default, IReadOnlyList<SetComponentInput>? components = null)
    {
        if (string.IsNullOrWhiteSpace(product.Name))
        {
            return (false, "Product name is required.", null);
        }

        if (string.IsNullOrWhiteSpace(product.Sku))
        {
            product.Sku = await _codes.NextSkuAsync(cancellationToken);
        }

        product.Sku = product.Sku!.Trim().ToUpperInvariant();
        if (await _db.Products.AnyAsync(p => p.Sku == product.Sku, cancellationToken))
        {
            return (false, $"SKU '{product.Sku}' already exists.", null);
        }

        product.Barcode = NormalizeBarcode(product.Barcode);
        if (product.Barcode is not null &&
            await _db.Products.AnyAsync(p => p.Barcode == product.Barcode, cancellationToken))
        {
            return (false, $"Barcode '{product.Barcode}' already exists.", null);
        }

        if (product.MinimumSalePrice > 0 &&
            (product.WholesalePrice < product.MinimumSalePrice || product.RetailPrice < product.MinimumSalePrice))
        {
            return (false, "Wholesale/Retail price cannot be below Minimum Sale Price.", null);
        }

        if (product.ProductKind == ProductKind.CompleteSet)
        {
            var componentError = await ValidateComponentsAsync(null, components, cancellationToken);
            if (componentError is not null)
                return (false, componentError, null);
            product.CurrentStock = 0;
            product.ReservedQuantity = 0;
            product.DamagedStock = 0;
        }

        RecalculateCbm(product);
        product.CreatedAt = DateTime.UtcNow;
        product.CreatedBy = userId;
        product.IsActive = true;
        product.StockValue = product.ProductKind == ProductKind.CompleteSet
            ? 0
            : Math.Round(product.CurrentStock * (product.AverageCost ?? product.LatestLandedCost ?? 0), 2);

        if (product.ProductKind == ProductKind.CompleteSet && components is not null)
        {
            foreach (var component in components)
            {
                product.SetComponents.Add(new ProductSetComponent
                {
                    ComponentProductId = component.ComponentProductId,
                    RequiredQuantity = component.RequiredQuantity,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId
                });
            }
        }

        _db.Products.Add(product);

        if (product.LatestRmbPrice is > 0)
        {
            _db.ProductRmbPriceHistories.Add(new ProductRmbPriceHistory
            {
                Product = product,
                SupplierId = product.DefaultSupplierId,
                UnitPriceRmb = product.LatestRmbPrice.Value,
                EffectiveDate = DateTime.UtcNow,
                SourceReference = "Initial",
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (product.LatestLandedCost is > 0)
        {
            _db.ProductLandedCostHistories.Add(new ProductLandedCostHistory
            {
                Product = product,
                LandedUnitCostPkr = product.LatestLandedCost.Value,
                EffectiveDate = DateTime.UtcNow,
                SourceReference = "Initial",
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("Create", nameof(Product), product.Id.ToString(), product.Sku, newValues: new { product.Sku, product.Name }, cancellationToken: cancellationToken);
        return (true, null, product);
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(
        Product product, string? userId, string? priceChangeReason = null, CancellationToken cancellationToken = default, IReadOnlyList<SetComponentInput>? components = null)
    {
        var existing = await _db.Products.FirstOrDefaultAsync(p => p.Id == product.Id, cancellationToken);
        if (existing is null)
        {
            return (false, "Product not found.");
        }

        if (string.IsNullOrWhiteSpace(product.Sku))
        {
            return (false, "SKU is required when editing a product.");
        }

        product.Sku = product.Sku!.Trim().ToUpperInvariant();
        if (await _db.Products.AnyAsync(p => p.Sku == product.Sku && p.Id != product.Id, cancellationToken))
        {
            return (false, $"SKU '{product.Sku}' already exists.");
        }

        product.Barcode = NormalizeBarcode(product.Barcode);
        if (product.Barcode is not null &&
            await _db.Products.AnyAsync(p => p.Barcode == product.Barcode && p.Id != product.Id, cancellationToken))
        {
            return (false, $"Barcode '{product.Barcode}' already exists.");
        }

        if (product.MinimumSalePrice > 0 &&
            (product.WholesalePrice < product.MinimumSalePrice || product.RetailPrice < product.MinimumSalePrice))
        {
            return (false, "Wholesale/Retail price cannot be below Minimum Sale Price.");
        }

        if (product.ProductKind == ProductKind.CompleteSet && existing.ProductKind != ProductKind.CompleteSet && existing.CurrentStock != 0)
            return (false, "This item has stock. A complete set does not keep its own stock.");

        if (product.ProductKind == ProductKind.CompleteSet)
        {
            var usedInSet = await _db.ProductSetComponents.AnyAsync(
                c => c.IsActive && c.ComponentProductId == existing.Id && c.SetProductId != existing.Id, cancellationToken);
            if (usedInSet)
                return (false, "This item is inside a complete set. Remove it there before making it a set.");

            var componentError = await ValidateComponentsAsync(existing.Id, components, cancellationToken);
            if (componentError is not null)
                return (false, componentError);
        }

        var oldRmb = existing.LatestRmbPrice;
        var oldLanded = existing.LatestLandedCost;
        var oldWholesale = existing.WholesalePrice;
        var oldRetail = existing.RetailPrice;
        var oldMin = existing.MinimumSalePrice;

        existing.Sku = product.Sku;
        existing.Barcode = product.Barcode;
        existing.Name = product.Name;
        existing.CategoryId = product.CategoryId;
        existing.Brand = product.Brand;
        existing.VehicleBrand = product.VehicleBrand;
        existing.VehicleModel = product.VehicleModel;
        existing.VariantYear = product.VariantYear;
        existing.UnitId = product.UnitId;
        existing.DefaultSupplierId = product.DefaultSupplierId;
        existing.ImagePath = product.ImagePath ?? existing.ImagePath;
        existing.UnitWeightKg = product.UnitWeightKg;
        existing.CartonWeightKg = product.CartonWeightKg;
        existing.PiecesPerCarton = product.PiecesPerCarton;
        existing.LengthCm = product.LengthCm;
        existing.WidthCm = product.WidthCm;
        existing.HeightCm = product.HeightCm;
        existing.WholesalePrice = product.WholesalePrice;
        existing.RetailPrice = product.RetailPrice;
        existing.MinimumSalePrice = product.MinimumSalePrice;
        existing.ReorderLevel = product.ReorderLevel;
        existing.Description = product.Description;
        existing.IsActive = product.IsActive;
        existing.ProductKind = product.ProductKind;
        RecalculateCbm(existing);

        var salePriceChanged = oldWholesale != product.WholesalePrice
            || oldRetail != product.RetailPrice
            || oldMin != product.MinimumSalePrice;
        if (salePriceChanged && string.IsNullOrWhiteSpace(priceChangeReason))
            return (false, "Price change reason is required when wholesale/retail/minimum prices change.");

        // Preserve history: only append when latest values change; never silently overwrite history rows.
        if (product.LatestRmbPrice is > 0 && product.LatestRmbPrice != oldRmb)
        {
            existing.PreviousRmbPrice = oldRmb;
            existing.LatestRmbPrice = product.LatestRmbPrice;
            _db.ProductRmbPriceHistories.Add(new ProductRmbPriceHistory
            {
                ProductId = existing.Id,
                SupplierId = existing.DefaultSupplierId,
                UnitPriceRmb = product.LatestRmbPrice.Value,
                EffectiveDate = DateTime.UtcNow,
                SourceReference = "ManualUpdate",
                Notes = priceChangeReason,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (product.LatestLandedCost is > 0 && product.LatestLandedCost != oldLanded)
        {
            existing.PreviousLandedCost = oldLanded;
            existing.LatestLandedCost = product.LatestLandedCost;
            _db.ProductLandedCostHistories.Add(new ProductLandedCostHistory
            {
                ProductId = existing.Id,
                LandedUnitCostPkr = product.LatestLandedCost.Value,
                EffectiveDate = DateTime.UtcNow,
                SourceReference = "ManualUpdate",
                Notes = priceChangeReason,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            });
        }

        existing.StockValue = existing.ProductKind == ProductKind.CompleteSet
            ? 0
            : Math.Round(existing.CurrentStock * (existing.AverageCost ?? existing.LatestLandedCost ?? 0), 2);
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = userId;

        if (existing.ProductKind == ProductKind.CompleteSet)
            await ReplaceComponentsAsync(existing.Id, components ?? Array.Empty<SetComponentInput>(), userId, cancellationToken);
        else if (existing.ProductKind == ProductKind.SingleItem)
            await DeactivateComponentsAsync(existing.Id, userId, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(
            salePriceChanged ? "PriceChange" : "Update",
            nameof(Product),
            existing.Id.ToString(),
            existing.Sku,
            priceChangeReason,
            oldValues: salePriceChanged ? new { oldWholesale, oldRetail, oldMin, oldRmb, oldLanded } : null,
            newValues: salePriceChanged
                ? new { existing.WholesalePrice, existing.RetailPrice, existing.MinimumSalePrice, existing.LatestRmbPrice, existing.LatestLandedCost }
                : null,
            cancellationToken: cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> SoftDeleteAsync(int id, string? userId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (existing is null)
        {
            return (false, "Product not found.");
        }

        existing.IsActive = false;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = userId;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("SoftDelete", nameof(Product), existing.Id.ToString(), existing.Sku, cancellationToken: cancellationToken);
        return (true, null);
    }

    private async Task<string?> ValidateComponentsAsync(int? setProductId, IReadOnlyList<SetComponentInput>? components, CancellationToken cancellationToken)
    {
        var list = components?.Where(c => c.ComponentProductId > 0).ToList() ?? new List<SetComponentInput>();
        var ids = list.Select(c => c.ComponentProductId).Distinct().ToList();
        var products = ids.Count == 0
            ? new List<(int Id, bool IsActive, ProductKind ProductKind)>()
            : (await _db.Products.AsNoTracking()
                .Where(p => ids.Contains(p.Id))
                .Select(p => new { p.Id, p.IsActive, p.ProductKind })
                .ToListAsync(cancellationToken))
                .Select(p => (p.Id, p.IsActive, p.ProductKind))
                .ToList();

        var active = products.Where(p => p.IsActive).Select(p => p.Id).ToHashSet();
        var kinds = products.ToDictionary(p => p.Id, p => p.ProductKind);
        return ProductSetPlanner.ValidateComposition(setProductId, list, kinds, active);
    }

    private async Task ReplaceComponentsAsync(int setProductId, IReadOnlyList<SetComponentInput> components, string? userId, CancellationToken cancellationToken)
    {
        var existing = await _db.ProductSetComponents.Where(c => c.SetProductId == setProductId).ToListAsync(cancellationToken);
        var incoming = components.Where(c => c.ComponentProductId > 0).ToList();
        var incomingIds = incoming.Select(c => c.ComponentProductId).ToHashSet();

        foreach (var row in existing.Where(c => c.IsActive && !incomingIds.Contains(c.ComponentProductId)))
        {
            row.IsActive = false;
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = userId;
        }

        foreach (var input in incoming)
        {
            var row = existing.FirstOrDefault(c => c.ComponentProductId == input.ComponentProductId);
            if (row is null)
            {
                _db.ProductSetComponents.Add(new ProductSetComponent
                {
                    SetProductId = setProductId,
                    ComponentProductId = input.ComponentProductId,
                    RequiredQuantity = input.RequiredQuantity,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId
                });
                continue;
            }

            row.RequiredQuantity = input.RequiredQuantity;
            row.IsActive = true;
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = userId;
        }
    }

    private async Task DeactivateComponentsAsync(int setProductId, string? userId, CancellationToken cancellationToken)
    {
        var rows = await _db.ProductSetComponents
            .Where(c => c.SetProductId == setProductId && c.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            row.IsActive = false;
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = userId;
        }
    }

    private static string? NormalizeBarcode(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return null;
        return barcode.Trim();
    }

    private static void RecalculateCbm(Product product)
    {
        if (product.LengthCm is > 0 && product.WidthCm is > 0 && product.HeightCm is > 0)
        {
            product.Cbm = Math.Round(
                (product.LengthCm.Value * product.WidthCm.Value * product.HeightCm.Value) / 1_000_000m,
                6);
        }
    }
}
