using Microsoft.EntityFrameworkCore;
using UD_Autos.Data;

namespace UD_Autos.Services;

public class GlobalSearchHit
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public interface IGlobalSearchService
{
    Task<IReadOnlyList<GlobalSearchHit>> SuggestAsync(string query, bool includeShipments, CancellationToken cancellationToken = default);
}

public class GlobalSearchService : IGlobalSearchService
{
    private const int PerTypeLimit = 5;
    private readonly ApplicationDbContext _db;

    public GlobalSearchService(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<GlobalSearchHit>> SuggestAsync(
        string query,
        bool includeShipments,
        CancellationToken cancellationToken = default)
    {
        var q = (query ?? string.Empty).Trim();
        if (q.Length < 2)
            return Array.Empty<GlobalSearchHit>();

        var results = new List<GlobalSearchHit>();

        var products = await _db.Products.AsNoTracking()
            .Where(p => p.IsActive && (
                p.Sku.Contains(q) ||
                p.Name.Contains(q) ||
                (p.Barcode != null && p.Barcode.Contains(q)) ||
                (p.Brand != null && p.Brand.Contains(q))))
            .OrderBy(p => p.Sku)
            .Take(PerTypeLimit)
            .Select(p => new { p.Id, p.Sku, p.Name, p.Brand })
            .ToListAsync(cancellationToken);
        results.AddRange(products.Select(p => new GlobalSearchHit
        {
            Type = "Product",
            Title = p.Sku + " — " + p.Name,
            Subtitle = p.Brand ?? "Product",
            Url = "/Products/Details/" + p.Id
        }));

        var customers = await _db.Customers.AsNoTracking()
            .Where(c => c.IsActive && (
                c.CustomerCode.Contains(q) ||
                c.Name.Contains(q) ||
                (c.ShopName != null && c.ShopName.Contains(q)) ||
                (c.Phone != null && c.Phone.Contains(q))))
            .OrderBy(c => c.Name)
            .Take(PerTypeLimit)
            .Select(c => new { c.Id, c.CustomerCode, c.Name, c.ShopName, c.Phone, c.City })
            .ToListAsync(cancellationToken);
        results.AddRange(customers.Select(c => new GlobalSearchHit
        {
            Type = "Customer",
            Title = c.CustomerCode + " — " + (c.ShopName ?? c.Name),
            Subtitle = c.Phone ?? c.City ?? "Customer",
            Url = "/Customers/Details/" + c.Id
        }));

        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => !i.IsVoided && (
                i.InvoiceNumber.Contains(q) ||
                i.Customer.Name.Contains(q) ||
                (i.Customer.ShopName != null && i.Customer.ShopName.Contains(q))))
            .OrderByDescending(i => i.InvoiceDate)
            .Take(PerTypeLimit)
            .Select(i => new
            {
                i.Id,
                i.InvoiceNumber,
                i.InvoiceDate,
                CustomerName = i.Customer.ShopName ?? i.Customer.Name
            })
            .ToListAsync(cancellationToken);
        results.AddRange(invoices.Select(i => new GlobalSearchHit
        {
            Type = "Invoice",
            Title = i.InvoiceNumber,
            Subtitle = i.CustomerName + " · " + i.InvoiceDate.ToString("dd MMM yyyy"),
            Url = "/Sales/Details/" + i.Id
        }));

        if (includeShipments)
        {
            var shipments = await _db.Shipments.AsNoTracking()
                .Where(s =>
                    s.ShipmentNumber.Contains(q) ||
                    s.ShippingAgent.Contains(q) ||
                    (s.BiltyNumber != null && s.BiltyNumber.Contains(q)))
                .OrderByDescending(s => s.Id)
                .Take(PerTypeLimit)
                .Select(s => new { s.Id, s.ShipmentNumber, s.ShippingAgent, s.Status })
                .ToListAsync(cancellationToken);
            results.AddRange(shipments.Select(s => new GlobalSearchHit
            {
                Type = "Shipment",
                Title = s.ShipmentNumber,
                Subtitle = s.ShippingAgent + " · " + s.Status,
                Url = "/Shipments/Details/" + s.Id
            }));
        }

        return results;
    }
}
