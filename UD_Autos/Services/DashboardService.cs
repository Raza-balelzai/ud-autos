using Microsoft.EntityFrameworkCore;
using UD_Autos.Data;
using UD_Autos.Models.Products;
using UD_Autos.Models.Sales;
using UD_Autos.Models.Shipments;

namespace UD_Autos.Services;

public class DashboardVm
{
    public DateTime AsOf { get; set; }
    public decimal TodaySales { get; set; }
    public decimal TodayCollections { get; set; }
    public decimal TodayExpenses { get; set; }
    public decimal TodayGrossProfit { get; set; }
    public decimal TodayNetProfit { get; set; }
    public decimal InventoryValue { get; set; }
    public decimal InventoryQty { get; set; }
    public decimal TotalReceivable { get; set; }
    public decimal OverdueReceivable { get; set; }
    public decimal MonthSales { get; set; }
    public decimal MonthGrossProfit { get; set; }
    public decimal MonthNetProfit { get; set; }
    public decimal FlowChinaProduction { get; set; }
    public decimal FlowChinaWarehouse { get; set; }
    public decimal FlowInTransit { get; set; }
    public decimal FlowKarachi { get; set; }
    public decimal FlowTarnol { get; set; }
    public bool CanViewProfit { get; set; }
    public IReadOnlyList<string> Alerts { get; set; } = Array.Empty<string>();
    public IReadOnlyList<ChartPoint> SalesTrend { get; set; } = Array.Empty<ChartPoint>();
    public IReadOnlyList<ChartPoint> ProfitTrend { get; set; } = Array.Empty<ChartPoint>();
    public IReadOnlyList<NamedAmount> TopProducts { get; set; } = Array.Empty<NamedAmount>();
    public IReadOnlyList<NamedAmount> TopCustomers { get; set; } = Array.Empty<NamedAmount>();
    public IReadOnlyList<NamedAmount> IncomingShipments { get; set; } = Array.Empty<NamedAmount>();
}

public class ChartPoint
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public class NamedAmount
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Meta { get; set; }
}

public interface IDashboardService
{
    Task<DashboardVm> GetAsync(bool canViewProfit, CancellationToken cancellationToken = default);
}

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _db;

    public DashboardService(ApplicationDbContext db) => _db = db;

    public async Task<DashboardVm> GetAsync(bool canViewProfit, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var trendFrom = today.AddDays(-29);

        var todayInvoices = await _db.Invoices.AsNoTracking()
            .Where(i => !i.IsVoided && i.InvoiceDate == today)
            .Select(i => new { i.BillTotal, i.GrossProfit })
            .ToListAsync(cancellationToken);

        var monthInvoices = await _db.Invoices.AsNoTracking()
            .Where(i => !i.IsVoided && i.InvoiceDate >= monthStart && i.InvoiceDate <= today)
            .Select(i => new { i.BillTotal, i.GrossProfit, i.InvoiceDate })
            .ToListAsync(cancellationToken);

        var todayCollections = await _db.CustomerPayments.AsNoTracking()
            .Where(p => p.PaymentDate == today)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        var todayExpenses = await _db.OperatingExpenses.AsNoTracking()
            .Where(e => e.ExpenseDate == today)
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;

        var monthExpenses = await _db.OperatingExpenses.AsNoTracking()
            .Where(e => e.ExpenseDate >= monthStart && e.ExpenseDate <= today)
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;

        var batches = await _db.InventoryBatches.AsNoTracking()
            .Where(b => b.RemainingQuantity > 0)
            .Select(b => new { b.RemainingQuantity, b.LandedUnitCostPkr })
            .ToListAsync(cancellationToken);

        var inventoryQty = batches.Sum(b => b.RemainingQuantity);
        var inventoryValue = batches.Sum(b => b.RemainingQuantity * b.LandedUnitCostPkr);

        var totalReceivable = await _db.Customers.AsNoTracking()
            .Where(c => c.IsActive)
            .SumAsync(c => (decimal?)c.CurrentBalance, cancellationToken) ?? 0m;

        var overdueCutoff = today.AddDays(-30);
        var overdueReceivable = await _db.Invoices.AsNoTracking()
            .Where(i => !i.IsVoided && i.OutstandingAmount > 0 && i.InvoiceDate <= overdueCutoff)
            .SumAsync(i => (decimal?)i.OutstandingAmount, cancellationToken) ?? 0m;

        var shipmentItems = await _db.ShipmentItems.AsNoTracking()
            .Include(si => si.Shipment)
            .Where(si => si.Shipment.Status != ShipmentStatus.Received && si.Shipment.Status != ShipmentStatus.Draft)
            .Select(si => new { si.Shipment.Status, Qty = si.ActualQuantity ?? si.ExpectedQuantity })
            .ToListAsync(cancellationToken);

        decimal Flow(ShipmentStatus s) => shipmentItems.Where(x => x.Status == s).Sum(x => x.Qty);
        var draftQty = await _db.ShipmentItems.AsNoTracking()
            .Include(si => si.Shipment)
            .Where(si => si.Shipment.Status == ShipmentStatus.Draft)
            .SumAsync(si => (decimal?)si.ExpectedQuantity, cancellationToken) ?? 0m;

        var trendInvoices = await _db.Invoices.AsNoTracking()
            .Where(i => !i.IsVoided && i.InvoiceDate >= trendFrom && i.InvoiceDate <= today)
            .Select(i => new { i.InvoiceDate, i.BillTotal, i.GrossProfit })
            .ToListAsync(cancellationToken);

        var salesTrend = Enumerable.Range(0, 30)
            .Select(i => trendFrom.AddDays(i))
            .Select(d => new ChartPoint
            {
                Label = d.ToString("dd MMM"),
                Value = trendInvoices.Where(x => x.InvoiceDate == d).Sum(x => x.BillTotal)
            }).ToList();

        var profitTrend = Enumerable.Range(0, 30)
            .Select(i => trendFrom.AddDays(i))
            .Select(d => new ChartPoint
            {
                Label = d.ToString("dd MMM"),
                Value = trendInvoices.Where(x => x.InvoiceDate == d).Sum(x => x.GrossProfit)
            }).ToList();

        var topProducts = await _db.InvoiceItems.AsNoTracking()
            .Include(ii => ii.Product)
            .Include(ii => ii.Invoice)
            .Where(ii => !ii.Invoice.IsVoided && ii.Invoice.InvoiceDate >= monthStart)
            .GroupBy(ii => new { ii.ProductId, ii.Product.Name, ii.Product.Sku })
            .Select(g => new NamedAmount
            {
                Name = g.Key.Sku + " — " + g.Key.Name,
                Amount = g.Sum(x => x.LineTotal)
            })
            .OrderByDescending(x => x.Amount)
            .Take(5)
            .ToListAsync(cancellationToken);

        var topCustomers = await _db.Invoices.AsNoTracking()
            .Include(i => i.Customer)
            .Where(i => !i.IsVoided && i.InvoiceDate >= monthStart)
            .GroupBy(i => new { i.CustomerId, Name = i.Customer.ShopName ?? i.Customer.Name })
            .Select(g => new NamedAmount { Name = g.Key.Name, Amount = g.Sum(x => x.BillTotal) })
            .OrderByDescending(x => x.Amount)
            .Take(5)
            .ToListAsync(cancellationToken);

        var incoming = await _db.Shipments.AsNoTracking()
            .Where(s => s.Status == ShipmentStatus.InTransit || s.Status == ShipmentStatus.Karachi || s.Status == ShipmentStatus.ChinaWarehouse)
            .OrderByDescending(s => s.Id)
            .Take(5)
            .Select(s => new NamedAmount
            {
                Name = s.ShipmentNumber,
                Amount = s.Items.Sum(i => i.ActualQuantity ?? i.ExpectedQuantity),
                Meta = s.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        var alerts = new List<string>();
        var lowStock = await _db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.ReorderLevel > 0 && p.CurrentStock <= p.ReorderLevel)
            .CountAsync(cancellationToken);
        if (lowStock > 0) alerts.Add($"{lowStock} product(s) at or below reorder level.");
        var overdueCust = await _db.Invoices.AsNoTracking()
            .Where(i => !i.IsVoided && i.OutstandingAmount > 0 && i.InvoiceDate <= overdueCutoff)
            .Select(i => i.CustomerId).Distinct().CountAsync(cancellationToken);
        if (overdueCust > 0) alerts.Add($"{overdueCust} customer(s) have invoices overdue 30+ days.");
        var stuck = await _db.Shipments.AsNoTracking()
            .CountAsync(s => s.Status == ShipmentStatus.InTransit && s.UpdatedAt < today.AddDays(-14), cancellationToken);
        if (stuck > 0) alerts.Add($"{stuck} shipment(s) in transit for 14+ days.");

        var todayGross = todayInvoices.Sum(i => i.GrossProfit);
        var monthGross = monthInvoices.Sum(i => i.GrossProfit);

        return new DashboardVm
        {
            AsOf = today,
            TodaySales = todayInvoices.Sum(i => i.BillTotal),
            TodayCollections = todayCollections,
            TodayExpenses = todayExpenses,
            TodayGrossProfit = todayGross,
            TodayNetProfit = todayGross - todayExpenses,
            InventoryValue = Math.Round(inventoryValue, 2),
            InventoryQty = inventoryQty,
            TotalReceivable = totalReceivable,
            OverdueReceivable = overdueReceivable,
            MonthSales = monthInvoices.Sum(i => i.BillTotal),
            MonthGrossProfit = monthGross,
            MonthNetProfit = monthGross - monthExpenses,
            FlowChinaProduction = draftQty,
            FlowChinaWarehouse = Flow(ShipmentStatus.ChinaWarehouse),
            FlowInTransit = Flow(ShipmentStatus.InTransit),
            FlowKarachi = Flow(ShipmentStatus.Karachi),
            FlowTarnol = Flow(ShipmentStatus.Tarnol),
            CanViewProfit = canViewProfit,
            Alerts = alerts,
            SalesTrend = salesTrend,
            ProfitTrend = canViewProfit ? profitTrend : Array.Empty<ChartPoint>(),
            TopProducts = topProducts,
            TopCustomers = topCustomers,
            IncomingShipments = incoming
        };
    }
}
