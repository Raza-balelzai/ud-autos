using System.Text;
using Microsoft.EntityFrameworkCore;
using UD_Autos.Data;
using UD_Autos.Models.Customers;
using UD_Autos.Models.Inventory;
using UD_Autos.Models.Sales;
using UD_Autos.Models.Shipments;

namespace UD_Autos.Services;

public class ReportRow
{
    public string Col1 { get; set; } = string.Empty;
    public string Col2 { get; set; } = string.Empty;
    public string Col3 { get; set; } = string.Empty;
    public string Col4 { get; set; } = string.Empty;
    public string Col5 { get; set; } = string.Empty;
    public string Col6 { get; set; } = string.Empty;
    public decimal? Amount1 { get; set; }
    public decimal? Amount2 { get; set; }
    public decimal? Amount3 { get; set; }
}

public class ReportResult
{
    public string Title { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public string FormulaNote { get; set; } = string.Empty;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public bool IncludesProfit { get; set; }
    public IReadOnlyList<string> Headers { get; set; } = Array.Empty<string>();
    public IReadOnlyList<ReportRow> Rows { get; set; } = Array.Empty<ReportRow>();
    public decimal Total1 { get; set; }
    public decimal Total2 { get; set; }
    public decimal Total3 { get; set; }
}

public interface IReportService
{
    Task<ReportResult> RunAsync(string family, string report, DateTime? from, DateTime? to, string? search, bool canViewProfit, CancellationToken cancellationToken = default);
    string ToCsv(ReportResult report);
}

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _db;

    public ReportService(ApplicationDbContext db) => _db = db;

    public async Task<ReportResult> RunAsync(
        string family, string report, DateTime? from, DateTime? to, string? search, bool canViewProfit, CancellationToken cancellationToken = default)
    {
        var f = (from ?? DateTime.UtcNow.Date.AddDays(-30)).Date;
        var t = (to ?? DateTime.UtcNow.Date).Date;
        family = (family ?? "sales").ToLowerInvariant();
        report = (report ?? "daily").ToLowerInvariant();

        return family switch
        {
            "profit" when !canViewProfit => Denied("Profit"),
            "profit" => await ProfitAsync(report, f, t, cancellationToken),
            "inventory" => await InventoryAsync(report, search, cancellationToken),
            "import" => await ImportAsync(report, f, t, search, canViewProfit, cancellationToken),
            "receivables" => await ReceivablesAsync(report, f, t, search, cancellationToken),
            _ => await SalesAsync(report, f, t, search, cancellationToken)
        };
    }

    private static ReportResult Denied(string title) => new()
    {
        Title = title,
        Family = "profit",
        FormulaNote = "Cost/profit reports require ViewProfit permission.",
        Headers = new[] { "Message" },
        Rows = new[] { new ReportRow { Col1 = "Not authorized to view profit data." } }
    };

    private async Task<ReportResult> SalesAsync(string report, DateTime from, DateTime to, string? search, CancellationToken ct)
    {
        var invoices = await _db.Invoices.AsNoTracking()
            .Include(i => i.Customer)
            .Include(i => i.Items).ThenInclude(ii => ii.Product)
            .Where(i => !i.IsVoided && i.InvoiceDate >= from && i.InvoiceDate <= to)
            .ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            invoices = invoices.Where(i =>
                i.InvoiceNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                i.Customer.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (i.Customer.ShopName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
        }

        return report switch
        {
            "monthly" => GroupSales(invoices, "Monthly Sales", i => i.InvoiceDate.ToString("yyyy-MM"),
                "Sum(BillTotal) by calendar month. Credit sales included in sales, not collections."),
            "yearly" => GroupSales(invoices, "Yearly Sales", i => i.InvoiceDate.Year.ToString(),
                "Sum(BillTotal) by year."),
            "product" => ProductSales(invoices),
            "customer" => CustomerSales(invoices),
            "channel" => ChannelSales(invoices),
            _ => DailySales(invoices, from, to)
        };
    }

    private static ReportResult DailySales(List<Invoice> invoices, DateTime from, DateTime to)
    {
        var rows = invoices.GroupBy(i => i.InvoiceDate.Date).OrderBy(g => g.Key)
            .Select(g => new ReportRow
            {
                Col1 = g.Key.ToString("dd MMM yyyy"),
                Amount1 = g.Sum(x => x.BillTotal),
                Amount2 = g.Where(x => x.PaymentMode is PaymentMode.Cash or PaymentMode.Bank).Sum(x => x.BillTotal),
                Amount3 = g.Where(x => x.PaymentMode is PaymentMode.Credit or PaymentMode.Partial).Sum(x => x.BillTotal - x.PaymentReceived)
            }).ToList();
        return new ReportResult
        {
            Title = "Daily Sales",
            Family = "sales",
            From = from,
            To = to,
            FormulaNote = "Total = invoice BillTotal. Cash/Bank = full paid modes. Credit portion = unpaid of Credit/Partial.",
            Headers = new[] { "Date", "Total Sales", "Cash/Bank Sales", "Credit Portion" },
            Rows = rows,
            Total1 = rows.Sum(r => r.Amount1 ?? 0),
            Total2 = rows.Sum(r => r.Amount2 ?? 0),
            Total3 = rows.Sum(r => r.Amount3 ?? 0)
        };
    }

    private static ReportResult GroupSales(List<Invoice> invoices, string title, Func<Invoice, string> key, string note)
    {
        var rows = invoices.GroupBy(key).OrderBy(g => g.Key)
            .Select(g => new ReportRow { Col1 = g.Key, Amount1 = g.Sum(x => x.BillTotal), Amount2 = g.Sum(x => x.PaymentReceived) }).ToList();
        return new ReportResult
        {
            Title = title, Family = "sales", FormulaNote = note,
            Headers = new[] { "Period", "Sales", "Collected on invoice" },
            Rows = rows, Total1 = rows.Sum(r => r.Amount1 ?? 0), Total2 = rows.Sum(r => r.Amount2 ?? 0)
        };
    }

    private static ReportResult ProductSales(List<Invoice> invoices)
    {
        var rows = invoices.SelectMany(i => i.Items)
            .GroupBy(ii => new { ii.Product.Sku, ii.Product.Name })
            .OrderByDescending(g => g.Sum(x => x.LineTotal))
            .Select(g => new ReportRow
            {
                Col1 = g.Key.Sku, Col2 = g.Key.Name,
                Amount1 = g.Sum(x => x.Quantity), Amount2 = g.Sum(x => x.LineTotal)
            }).ToList();
        return new ReportResult
        {
            Title = "Product-wise Sales", Family = "sales",
            FormulaNote = "Qty and LineTotal from invoice items in range.",
            Headers = new[] { "SKU", "Product", "Qty", "Revenue" },
            Rows = rows, Total1 = rows.Sum(r => r.Amount1 ?? 0), Total2 = rows.Sum(r => r.Amount2 ?? 0)
        };
    }

    private static ReportResult CustomerSales(List<Invoice> invoices)
    {
        var rows = invoices.GroupBy(i => i.Customer.ShopName ?? i.Customer.Name)
            .OrderByDescending(g => g.Sum(x => x.BillTotal))
            .Select(g => new ReportRow
            {
                Col1 = g.Key,
                Amount1 = g.Sum(x => x.BillTotal),
                Amount2 = g.Sum(x => x.PaymentReceived),
                Amount3 = g.Sum(x => x.OutstandingAmount)
            }).ToList();
        return new ReportResult
        {
            Title = "Customer-wise Sales", Family = "sales",
            FormulaNote = "Sales vs payment received on invoices vs remaining outstanding on those invoices.",
            Headers = new[] { "Customer", "Sales", "Paid on invoices", "Outstanding on invoices" },
            Rows = rows, Total1 = rows.Sum(r => r.Amount1 ?? 0), Total2 = rows.Sum(r => r.Amount2 ?? 0), Total3 = rows.Sum(r => r.Amount3 ?? 0)
        };
    }

    private static ReportResult ChannelSales(List<Invoice> invoices)
    {
        var rows = invoices.GroupBy(i => i.Customer.CustomerType.ToString())
            .Select(g => new ReportRow { Col1 = g.Key, Amount1 = g.Sum(x => x.BillTotal), Amount2 = g.Count() }).ToList();
        return new ReportResult
        {
            Title = "Wholesale vs Retail", Family = "sales",
            FormulaNote = "Grouped by Customer.CustomerType on invoices.",
            Headers = new[] { "Channel", "Sales", "Invoice count" },
            Rows = rows, Total1 = rows.Sum(r => r.Amount1 ?? 0), Total2 = rows.Sum(r => r.Amount2 ?? 0)
        };
    }

    private async Task<ReportResult> ProfitAsync(string report, DateTime from, DateTime to, CancellationToken ct)
    {
        var invoices = await _db.Invoices.AsNoTracking()
            .Include(i => i.Customer)
            .Include(i => i.Items).ThenInclude(ii => ii.Product).ThenInclude(p => p.Category)
            .Where(i => !i.IsVoided && i.InvoiceDate >= from && i.InvoiceDate <= to)
            .ToListAsync(ct);
        var expenses = await _db.OperatingExpenses.AsNoTracking()
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;

        return report switch
        {
            "product" => ProductProfit(invoices),
            "invoice" => InvoiceProfit(invoices),
            "customer" => CustomerProfit(invoices),
            "category" => CategoryProfit(invoices),
            "shipment" => await ShipmentProfitAsync(from, to, ct),
            _ => MonthlyPl(invoices, expenses, from, to)
        };
    }

    private static ReportResult MonthlyPl(List<Invoice> invoices, decimal expenses, DateTime from, DateTime to)
    {
        var gross = invoices.Sum(i => i.GrossProfit);
        var sales = invoices.Sum(i => i.BillTotal);
        var cogs = invoices.Sum(i => i.TotalCogs);
        var rows = new List<ReportRow>
        {
            new() { Col1 = "Sales (BillTotal)", Amount1 = sales },
            new() { Col1 = "COGS (historical FIFO)", Amount1 = cogs },
            new() { Col1 = "Gross Profit", Amount1 = gross },
            new() { Col1 = "Operating Expenses", Amount1 = expenses },
            new() { Col1 = "Net Profit", Amount1 = gross - expenses }
        };
        return new ReportResult
        {
            Title = "Monthly / Period P&L", Family = "profit", From = from, To = to, IncludesProfit = true,
            FormulaNote = "Gross = Invoice.GrossProfit (Net Revenue − historical COGS). Net = Gross − OperatingExpenses. Credit sales do not inflate cash.",
            Headers = new[] { "Line", "Amount" }, Rows = rows, Total1 = gross - expenses
        };
    }

    private static ReportResult ProductProfit(List<Invoice> invoices)
    {
        var rows = invoices.SelectMany(i => i.Items)
            .GroupBy(ii => new { ii.Product.Sku, ii.Product.Name })
            .Select(g => new ReportRow
            {
                Col1 = g.Key.Sku, Col2 = g.Key.Name,
                Amount1 = g.Sum(x => x.LineTotal),
                Amount2 = g.Sum(x => x.LineCogsPkr),
                Amount3 = g.Sum(x => x.LineTotal - x.LineCogsPkr)
            }).OrderByDescending(r => r.Amount3).ToList();
        return new ReportResult
        {
            Title = "Product Profit", Family = "profit", IncludesProfit = true,
            FormulaNote = "Product Profit ≈ LineTotal − LineCogsPkr (FIFO historical).",
            Headers = new[] { "SKU", "Product", "Revenue", "COGS", "Gross Profit" },
            Rows = rows, Total1 = rows.Sum(r => r.Amount1 ?? 0), Total2 = rows.Sum(r => r.Amount2 ?? 0), Total3 = rows.Sum(r => r.Amount3 ?? 0)
        };
    }

    private static ReportResult InvoiceProfit(List<Invoice> invoices)
    {
        var rows = invoices.OrderByDescending(i => i.InvoiceDate)
            .Select(i => new ReportRow
            {
                Col1 = i.InvoiceNumber, Col2 = i.InvoiceDate.ToString("dd MMM yyyy"),
                Col3 = i.Customer.ShopName ?? i.Customer.Name,
                Amount1 = i.BillTotal, Amount2 = i.TotalCogs, Amount3 = i.GrossProfit
            }).ToList();
        return new ReportResult
        {
            Title = "Invoice Gross Profit", Family = "profit", IncludesProfit = true,
            FormulaNote = "Invoice Gross Profit = stored GrossProfit from sale-time COGS.",
            Headers = new[] { "Invoice", "Date", "Customer", "Sales", "COGS", "Gross Profit" },
            Rows = rows, Total1 = rows.Sum(r => r.Amount1 ?? 0), Total2 = rows.Sum(r => r.Amount2 ?? 0), Total3 = rows.Sum(r => r.Amount3 ?? 0)
        };
    }

    private static ReportResult CustomerProfit(List<Invoice> invoices)
    {
        var rows = invoices.GroupBy(i => i.Customer.ShopName ?? i.Customer.Name)
            .Select(g => new ReportRow
            {
                Col1 = g.Key,
                Amount1 = g.Sum(x => x.BillTotal),
                Amount2 = g.Sum(x => x.GrossProfit)
            }).OrderByDescending(r => r.Amount2).ToList();
        return new ReportResult
        {
            Title = "Customer Profitability", Family = "profit", IncludesProfit = true,
            FormulaNote = "Revenue and GrossProfit attributable to customer invoices in range.",
            Headers = new[] { "Customer", "Revenue", "Gross Profit" },
            Rows = rows, Total1 = rows.Sum(r => r.Amount1 ?? 0), Total2 = rows.Sum(r => r.Amount2 ?? 0)
        };
    }

    private static ReportResult CategoryProfit(List<Invoice> invoices)
    {
        var rows = invoices.SelectMany(i => i.Items)
            .GroupBy(ii => ii.Product.Category?.Name ?? "Uncategorized")
            .Select(g => new ReportRow
            {
                Col1 = g.Key,
                Amount1 = g.Sum(x => x.LineTotal),
                Amount2 = g.Sum(x => x.LineCogsPkr),
                Amount3 = g.Sum(x => x.LineTotal - x.LineCogsPkr)
            }).OrderByDescending(r => r.Amount3).ToList();
        return new ReportResult
        {
            Title = "Category Profit", Family = "profit", IncludesProfit = true,
            FormulaNote = "Grouped by product category using line revenue and LineCogsPkr.",
            Headers = new[] { "Category", "Revenue", "COGS", "Gross Profit" },
            Rows = rows, Total1 = rows.Sum(r => r.Amount1 ?? 0), Total2 = rows.Sum(r => r.Amount2 ?? 0), Total3 = rows.Sum(r => r.Amount3 ?? 0)
        };
    }

    private async Task<ReportResult> ShipmentProfitAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var allocs = await _db.InvoiceItemBatchAllocations.AsNoTracking()
            .Include(a => a.InvoiceItem).ThenInclude(ii => ii.Invoice)
            .Include(a => a.InventoryBatch).ThenInclude(b => b.Shipment)
            .Where(a => !a.InvoiceItem.Invoice.IsVoided
                        && a.InvoiceItem.Invoice.InvoiceDate >= from
                        && a.InvoiceItem.Invoice.InvoiceDate <= to)
            .ToListAsync(ct);

        var rows = allocs.GroupBy(a => a.InventoryBatch.Shipment.ShipmentNumber)
            .Select(g =>
            {
                var cogs = g.Sum(x => x.Quantity * x.UnitCostPkr);
                var revenueShare = g.Sum(x =>
                {
                    var item = x.InvoiceItem;
                    if (item.Quantity <= 0) return 0m;
                    return item.LineTotal * (x.Quantity / item.Quantity);
                });
                return new ReportRow
                {
                    Col1 = g.Key,
                    Amount1 = Math.Round(revenueShare, 2),
                    Amount2 = Math.Round(cogs, 2),
                    Amount3 = Math.Round(revenueShare - cogs, 2)
                };
            }).OrderByDescending(r => r.Amount3).ToList();

        return new ReportResult
        {
            Title = "Shipment / Batch Profitability", Family = "profit", IncludesProfit = true, From = from, To = to,
            FormulaNote = "Revenue share ≈ LineTotal × (allocated qty / line qty). COGS from InvoiceItemBatchAllocation historical UnitCostPkr.",
            Headers = new[] { "Shipment", "Attributed Sales", "COGS", "Gross Profit" },
            Rows = rows, Total1 = rows.Sum(r => r.Amount1 ?? 0), Total2 = rows.Sum(r => r.Amount2 ?? 0), Total3 = rows.Sum(r => r.Amount3 ?? 0)
        };
    }

    private async Task<ReportResult> InventoryAsync(string report, string? search, CancellationToken ct)
    {
        var products = await _db.Products.AsNoTracking().Include(p => p.Category).Where(p => p.IsActive).ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            products = products.Where(p => p.Sku.Contains(term, StringComparison.OrdinalIgnoreCase)
                || p.Name.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return report switch
        {
            "value" => new ReportResult
            {
                Title = "Stock Value", Family = "inventory",
                FormulaNote = "Product.StockValue / CurrentStock×LatestLandedCost fallback.",
                Headers = new[] { "SKU", "Product", "Qty", "Stock Value" },
                Rows = products.Select(p => new ReportRow
                {
                    Col1 = p.Sku, Col2 = p.Name, Amount1 = p.CurrentStock,
                    Amount2 = p.StockValue > 0 ? p.StockValue : Math.Round(p.CurrentStock * (p.LatestLandedCost ?? p.AverageCost ?? 0), 2)
                }).ToList(),
                Total1 = products.Sum(p => p.CurrentStock),
                Total2 = products.Sum(p => p.StockValue > 0 ? p.StockValue : Math.Round(p.CurrentStock * (p.LatestLandedCost ?? p.AverageCost ?? 0), 2))
            },
            "low" => FilterStock(products.Where(p => p.ReorderLevel > 0 && p.CurrentStock <= p.ReorderLevel), "Low Stock"),
            "out" => FilterStock(products.Where(p => p.CurrentStock <= 0), "Out of Stock"),
            "dead" => await DeadStockAsync(products, ct),
            "fast" => await FastMovingAsync(ct),
            "aging" => await BatchAgingAsync(ct),
            "batches" => await BatchStockAsync(ct),
            _ => FilterStock(products, "Current Stock")
        };
    }

    private static ReportResult FilterStock(IEnumerable<Models.Products.Product> products, string title)
    {
        var list = products.OrderBy(p => p.Name).Select(p => new ReportRow
        {
            Col1 = p.Sku, Col2 = p.Name, Col3 = p.Category?.Name ?? "—",
            Amount1 = p.CurrentStock, Amount2 = p.ReorderLevel
        }).ToList();
        return new ReportResult
        {
            Title = title, Family = "inventory",
            FormulaNote = "From Product.CurrentStock / ReorderLevel.",
            Headers = new[] { "SKU", "Product", "Category", "Qty", "Reorder" },
            Rows = list, Total1 = list.Sum(r => r.Amount1 ?? 0)
        };
    }

    private async Task<ReportResult> DeadStockAsync(List<Models.Products.Product> products, CancellationToken ct)
    {
        var since = DateTime.UtcNow.Date.AddDays(-90);
        var soldIds = await _db.InvoiceItems.AsNoTracking()
            .Include(ii => ii.Invoice)
            .Where(ii => !ii.Invoice.IsVoided && ii.Invoice.InvoiceDate >= since)
            .Select(ii => ii.ProductId).Distinct().ToListAsync(ct);
        var dead = products.Where(p => p.CurrentStock > 0 && !soldIds.Contains(p.Id));
        return FilterStock(dead, "Dead Stock (no sales 90d)");
    }

    private async Task<ReportResult> FastMovingAsync(CancellationToken ct)
    {
        var since = DateTime.UtcNow.Date.AddDays(-30);
        var rows = await _db.InvoiceItems.AsNoTracking()
            .Include(ii => ii.Product).Include(ii => ii.Invoice)
            .Where(ii => !ii.Invoice.IsVoided && ii.Invoice.InvoiceDate >= since)
            .GroupBy(ii => new { ii.Product.Sku, ii.Product.Name })
            .Select(g => new ReportRow { Col1 = g.Key.Sku, Col2 = g.Key.Name, Amount1 = g.Sum(x => x.Quantity), Amount2 = g.Sum(x => x.LineTotal) })
            .OrderByDescending(r => r.Amount1).Take(50).ToListAsync(ct);
        return new ReportResult
        {
            Title = "Fast Moving (30d)", Family = "inventory",
            FormulaNote = "Top quantities sold in last 30 days.",
            Headers = new[] { "SKU", "Product", "Qty sold", "Revenue" },
            Rows = rows, Total1 = rows.Sum(r => r.Amount1 ?? 0), Total2 = rows.Sum(r => r.Amount2 ?? 0)
        };
    }

    private async Task<ReportResult> BatchAgingAsync(CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var rows = await _db.InventoryBatches.AsNoTracking()
            .Include(b => b.Product)
            .Where(b => b.RemainingQuantity > 0)
            .OrderBy(b => b.ReceivedAt)
            .Select(b => new ReportRow
            {
                Col1 = b.BatchNumber,
                Col2 = b.Product.Sku,
                Col3 = b.ReceivedAt.ToString("dd MMM yyyy"),
                Amount1 = b.RemainingQuantity,
                Amount2 = (decimal)(today - b.ReceivedAt.Date).TotalDays
            }).ToListAsync(ct);
        return new ReportResult
        {
            Title = "Stock Aging (Batches)", Family = "inventory",
            FormulaNote = "Age days = today − batch ReceivedAt.",
            Headers = new[] { "Batch", "SKU", "Received", "Remaining", "Age days" },
            Rows = rows, Total1 = rows.Sum(r => r.Amount1 ?? 0)
        };
    }

    private async Task<ReportResult> BatchStockAsync(CancellationToken ct)
    {
        var rows = await _db.InventoryBatches.AsNoTracking()
            .Include(b => b.Product)
            .Where(b => b.RemainingQuantity > 0)
            .OrderBy(b => b.Product.Sku).ThenBy(b => b.ReceivedAt)
            .Select(b => new ReportRow
            {
                Col1 = b.BatchNumber, Col2 = b.Product.Sku,
                Amount1 = b.RemainingQuantity, Amount2 = b.LandedUnitCostPkr,
                Amount3 = Math.Round(b.RemainingQuantity * b.LandedUnitCostPkr, 2)
            }).ToListAsync(ct);
        return new ReportResult
        {
            Title = "Batch Stock", Family = "inventory",
            FormulaNote = "Remaining × LandedUnitCostPkr.",
            Headers = new[] { "Batch", "SKU", "Remaining", "Unit cost", "Value" },
            Rows = rows, Total1 = rows.Sum(r => r.Amount1 ?? 0), Total3 = rows.Sum(r => r.Amount3 ?? 0)
        };
    }

    private async Task<ReportResult> ImportAsync(string report, DateTime from, DateTime to, string? search, bool canViewCosts, CancellationToken ct)
    {
        return report switch
        {
            "rmb" => await RmbHistoryAsync(from, to, ct),
            "rate" => await EffectiveRateAsync(from, to, ct),
            "freight" => await FreightHistoryAsync(from, to, ct),
            "landed" when !canViewCosts => Denied("Shipment Landed Cost"),
            "landed" => await LandedAsync(from, to, ct),
            _ => await SupplierPurchasesAsync(from, to, search, ct)
        };
    }

    private async Task<ReportResult> SupplierPurchasesAsync(DateTime from, DateTime to, string? search, CancellationToken ct)
    {
        var query = _db.PurchaseOrders.AsNoTracking().Include(p => p.Supplier)
            .Where(p => p.OrderDate >= from && p.OrderDate <= to);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p => p.PoNumber.Contains(term) || p.Supplier.Name.Contains(term));
        }
        var list = await query.OrderByDescending(p => p.OrderDate).ToListAsync(ct);
        var rows = list.Select(p => new ReportRow
        {
            Col1 = p.PoNumber, Col2 = p.OrderDate.ToString("dd MMM yyyy"), Col3 = p.Supplier.Name,
            Amount1 = p.TotalRmb, Amount2 = p.PaidRmb, Amount3 = p.TotalRmb - p.PaidRmb
        }).ToList();
        return new ReportResult
        {
            Title = "Supplier Purchases", Family = "import", From = from, To = to,
            FormulaNote = "PO TotalRmb / PaidRmb from China purchases.",
            Headers = new[] { "PO", "Date", "Supplier", "Total RMB", "Paid RMB", "Open RMB" },
            Rows = rows, Total1 = rows.Sum(r => r.Amount1 ?? 0), Total2 = rows.Sum(r => r.Amount2 ?? 0), Total3 = rows.Sum(r => r.Amount3 ?? 0)
        };
    }

    private async Task<ReportResult> RmbHistoryAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var rows = await _db.ProductRmbPriceHistories.AsNoTracking().Include(h => h.Product).Include(h => h.Supplier)
            .Where(h => h.EffectiveDate >= from && h.EffectiveDate <= to)
            .OrderByDescending(h => h.EffectiveDate)
            .Select(h => new ReportRow
            {
                Col1 = h.Product.Sku, Col2 = h.EffectiveDate.ToString("dd MMM yyyy"),
                Amount1 = h.UnitPriceRmb, Col3 = h.Supplier != null ? h.Supplier.Name : "—"
            }).ToListAsync(ct);
        return new ReportResult
        {
            Title = "RMB Price History", Family = "import", From = from, To = to,
            FormulaNote = "ProductRmbPriceHistory rows in range.",
            Headers = new[] { "SKU", "Effective", "Supplier", "RMB Price" },
            Rows = rows
        };
    }

    private async Task<ReportResult> EffectiveRateAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var rows = await _db.HawalaPayments.AsNoTracking()
            .Where(h => h.PaymentDate >= from && h.PaymentDate <= to)
            .OrderByDescending(h => h.PaymentDate)
            .Select(h => new ReportRow
            {
                Col1 = h.PaymentNumber, Col2 = h.PaymentDate.ToString("dd MMM yyyy"),
                Amount1 = h.RmbReceived, Amount2 = h.PkrPaid, Amount3 = h.EffectiveRmbRate
            }).ToListAsync(ct);
        return new ReportResult
        {
            Title = "Effective Exchange Rate", Family = "import", From = from, To = to,
            FormulaNote = "EffectiveRmbRate = PkrPaid / RmbReceived (Hawala).",
            Headers = new[] { "Hawala#", "Date", "RMB", "PKR", "Rate" },
            Rows = rows
        };
    }

    private async Task<ReportResult> FreightHistoryAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var rows = await _db.Shipments.AsNoTracking()
            .Where(s => (s.ChinaDepartureDate ?? s.CreatedAt) >= from && (s.ChinaDepartureDate ?? s.CreatedAt) <= to.AddDays(1))
            .OrderByDescending(s => s.Id)
            .Select(s => new ReportRow
            {
                Col1 = s.ShipmentNumber,
                Col2 = s.FreightMethod.ToString(),
                Amount1 = s.AgentBilledWeightKg ?? s.WarehouseWeightKg,
                Amount2 = s.AgentBilledCbm ?? s.WarehouseCbm,
                Amount3 = s.FreightAmountPkr
            }).ToListAsync(ct);
        return new ReportResult
        {
            Title = "Freight KG/CBM History", Family = "import", From = from, To = to,
            FormulaNote = "Shipment freight method, billed KG/CBM and FreightAmountPkr.",
            Headers = new[] { "Shipment", "Method", "KG", "CBM", "Freight PKR" },
            Rows = rows, Total3 = rows.Sum(r => r.Amount3 ?? 0)
        };
    }

    private async Task<ReportResult> LandedAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var rows = await _db.Shipments.AsNoTracking()
            .Where(s => s.IsFinalized && s.FinalizedAt >= from && s.FinalizedAt <= to.AddDays(1))
            .OrderByDescending(s => s.FinalizedAt)
            .Select(s => new ReportRow
            {
                Col1 = s.ShipmentNumber,
                Col2 = s.FinalizedAt!.Value.ToString("dd MMM yyyy"),
                Amount1 = s.ChinaProductCostPkr + s.TotalDirectCostPkr,
                Amount2 = s.TotalDirectCostPkr,
                Amount3 = s.ChinaProductCostPkr
            }).ToListAsync(ct);
        return new ReportResult
        {
            Title = "Shipment Landed Cost", Family = "import", IncludesProfit = true, From = from, To = to,
            FormulaNote = "Finalized shipments — ChinaProductCostPkr + TotalDirectCostPkr.",
            Headers = new[] { "Shipment", "Finalized", "Total Landed", "Direct costs", "China product PKR" },
            Rows = rows, Total1 = rows.Sum(r => r.Amount1 ?? 0), Total2 = rows.Sum(r => r.Amount2 ?? 0)
        };
    }

    private async Task<ReportResult> ReceivablesAsync(string report, DateTime from, DateTime to, string? search, CancellationToken ct)
    {
        return report switch
        {
            "overdue" => await OverdueAsync(ct),
            "aging" => await AgingBucketsAsync(ct),
            "oldkhata" => await OldKhataAsync(to, ct),
            "payments" => await PaymentHistoryAsync(from, to, search, ct),
            _ => await CustomerBalancesAsync(search, ct)
        };
    }

    private async Task<ReportResult> CustomerBalancesAsync(string? search, CancellationToken ct)
    {
        var q = _db.Customers.AsNoTracking().Where(c => c.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(c => c.Name.Contains(term) || c.CustomerCode.Contains(term) || (c.ShopName != null && c.ShopName.Contains(term)));
        }
        var list = await q.OrderByDescending(c => c.CurrentBalance).ToListAsync(ct);
        var rows = list.Select(c => new ReportRow
        {
            Col1 = c.CustomerCode, Col2 = c.ShopName ?? c.Name,
            Amount1 = c.CurrentBalance, Amount2 = c.TotalPurchases, Amount3 = c.TotalPayments
        }).ToList();
        return new ReportResult
        {
            Title = "Customer Balances", Family = "receivables",
            FormulaNote = "CurrentBalance maintained by Khata ledger (invoices − payments − returns).",
            Headers = new[] { "Code", "Customer", "Outstanding", "Purchases", "Payments" },
            Rows = rows, Total1 = rows.Sum(r => r.Amount1 ?? 0), Total2 = rows.Sum(r => r.Amount2 ?? 0), Total3 = rows.Sum(r => r.Amount3 ?? 0)
        };
    }

    private async Task<ReportResult> OverdueAsync(CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.Date.AddDays(-30);
        var rows = await _db.Invoices.AsNoTracking().Include(i => i.Customer)
            .Where(i => !i.IsVoided && i.OutstandingAmount > 0 && i.InvoiceDate <= cutoff)
            .OrderBy(i => i.InvoiceDate)
            .Select(i => new ReportRow
            {
                Col1 = i.InvoiceNumber, Col2 = i.Customer.ShopName ?? i.Customer.Name,
                Col3 = i.InvoiceDate.ToString("dd MMM yyyy"),
                Amount1 = i.OutstandingAmount,
                Amount2 = (decimal)(DateTime.UtcNow.Date - i.InvoiceDate.Date).TotalDays
            }).ToListAsync(ct);
        return new ReportResult
        {
            Title = "Overdue Receivables (30+)", Family = "receivables",
            FormulaNote = "Unpaid invoice OutstandingAmount with age ≥ 30 days.",
            Headers = new[] { "Invoice", "Customer", "Date", "Outstanding", "Age" },
            Rows = rows, Total1 = rows.Sum(r => r.Amount1 ?? 0)
        };
    }

    private async Task<ReportResult> AgingBucketsAsync(CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var open = await _db.Invoices.AsNoTracking()
            .Where(i => !i.IsVoided && i.OutstandingAmount > 0)
            .Select(i => new { i.OutstandingAmount, Age = (int)(today - i.InvoiceDate.Date).TotalDays })
            .ToListAsync(ct);
        ReportRow Bucket(string name, Func<int, bool> pred) => new()
        {
            Col1 = name,
            Amount1 = open.Where(x => pred(x.Age)).Sum(x => x.OutstandingAmount),
            Amount2 = open.Count(x => pred(x.Age))
        };
        var rows = new List<ReportRow>
        {
            Bucket("0–29", a => a < 30),
            Bucket("30–59", a => a is >= 30 and < 60),
            Bucket("60–89", a => a is >= 60 and < 90),
            Bucket("90–119", a => a is >= 90 and < 120),
            Bucket("120+", a => a >= 120)
        };
        return new ReportResult
        {
            Title = "Receivable Aging Buckets", Family = "receivables",
            FormulaNote = "Buckets on unpaid invoice age (not last purchase date).",
            Headers = new[] { "Bucket", "Outstanding", "Invoice count" },
            Rows = rows, Total1 = rows.Sum(r => r.Amount1 ?? 0), Total2 = rows.Sum(r => r.Amount2 ?? 0)
        };
    }

    private async Task<ReportResult> OldKhataAsync(DateTime before, CancellationToken ct)
    {
        var rows = await _db.Invoices.AsNoTracking().Include(i => i.Customer)
            .Where(i => !i.IsVoided && i.OutstandingAmount > 0 && i.InvoiceDate < before.Date)
            .OrderBy(i => i.InvoiceDate)
            .Select(i => new ReportRow
            {
                Col1 = i.InvoiceNumber, Col2 = i.Customer.ShopName ?? i.Customer.Name,
                Col3 = i.InvoiceDate.ToString("dd MMM yyyy"), Amount1 = i.OutstandingAmount
            }).ToListAsync(ct);
        return new ReportResult
        {
            Title = $"Old Khata before {before:dd MMM yyyy}", Family = "receivables", To = before,
            FormulaNote = "Outstanding on invoices dated before the chosen date.",
            Headers = new[] { "Invoice", "Customer", "Date", "Outstanding" },
            Rows = rows, Total1 = rows.Sum(r => r.Amount1 ?? 0)
        };
    }

    private async Task<ReportResult> PaymentHistoryAsync(DateTime from, DateTime to, string? search, CancellationToken ct)
    {
        var q = _db.CustomerPayments.AsNoTracking().Include(p => p.Customer)
            .Where(p => p.PaymentDate >= from && p.PaymentDate <= to);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(p => p.PaymentNumber.Contains(term) || p.Customer.Name.Contains(term)
                || (p.Customer.ShopName != null && p.Customer.ShopName.Contains(term)));
        }
        var list = await q.OrderByDescending(p => p.PaymentDate).ToListAsync(ct);
        var rows = list.Select(p => new ReportRow
        {
            Col1 = p.PaymentNumber, Col2 = p.PaymentDate.ToString("dd MMM yyyy"),
            Col3 = p.Customer.ShopName ?? p.Customer.Name, Col4 = p.Account.ToString(),
            Amount1 = p.Amount
        }).ToList();
        return new ReportResult
        {
            Title = "Payment History", Family = "receivables", From = from, To = to,
            FormulaNote = "CustomerPayment collections (cash book inflows).",
            Headers = new[] { "Receipt", "Date", "Customer", "Account", "Amount" },
            Rows = rows, Total1 = rows.Sum(r => r.Amount1 ?? 0)
        };
    }

    public string ToCsv(ReportResult report)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", report.Headers.Select(Escape)));
        foreach (var r in report.Rows)
        {
            var cells = new List<string>();
            foreach (var h in report.Headers)
            {
                // Map by header position heuristically using available fields in order
            }
            var values = new[] { r.Col1, r.Col2, r.Col3, r.Col4, r.Col5, r.Col6 }
                .Concat(new[] { r.Amount1, r.Amount2, r.Amount3 }.Select(a => a?.ToString("0.##") ?? ""))
                .Where((_, i) => i < report.Headers.Count)
                .Select(Escape);
            // Better: emit non-empty logical columns matching header count
            var ordered = BuildCsvCells(r, report.Headers.Count);
            sb.AppendLine(string.Join(",", ordered.Select(Escape)));
        }
        return sb.ToString();
    }

    private static IEnumerable<string> BuildCsvCells(ReportRow r, int count)
    {
        var all = new List<string?> { r.Col1, r.Col2, r.Col3, r.Col4, r.Col5, r.Col6,
            r.Amount1?.ToString("0.##"), r.Amount2?.ToString("0.##"), r.Amount3?.ToString("0.##") };
        // Prefer packing text cols then amounts to fill header count
        var texts = new[] { r.Col1, r.Col2, r.Col3, r.Col4, r.Col5, r.Col6 }.Where(s => !string.IsNullOrEmpty(s)).ToList();
        var amts = new[] { r.Amount1, r.Amount2, r.Amount3 }.Where(a => a.HasValue).Select(a => a!.Value.ToString("0.##")).ToList();
        var merged = texts.Concat(amts).ToList();
        while (merged.Count < count) merged.Add("");
        return merged.Take(count)!;
    }

    private static string Escape(string? value)
    {
        value ??= "";
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
