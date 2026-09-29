using Microsoft.EntityFrameworkCore;
using UD_Autos.Data;

namespace UD_Autos.Services;

public interface ISupplierBalanceService
{
    Task RecalculateAsync(int supplierId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Derives supplier activity summaries from linked POs and Hawala payments — no duplicate manual ledger rows.
/// </summary>
public class SupplierBalanceService : ISupplierBalanceService
{
    private readonly ApplicationDbContext _db;

    public SupplierBalanceService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task RecalculateAsync(int supplierId, CancellationToken cancellationToken = default)
    {
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId, cancellationToken);
        if (supplier is null) return;

        var purchasesRmb = await _db.PurchaseOrders
            .Where(p => p.SupplierId == supplierId && p.Status != Models.Purchases.PurchaseOrderStatus.Draft)
            .SumAsync(p => (decimal?)p.TotalRmb, cancellationToken) ?? 0m;

        var paidPkr = await _db.HawalaPayments
            .Where(h => h.SupplierId == supplierId)
            .SumAsync(h => (decimal?)h.PkrPaid, cancellationToken) ?? 0m;

        var estimatedPkr = await _db.PurchaseOrders
            .Where(p => p.SupplierId == supplierId && p.Status != Models.Purchases.PurchaseOrderStatus.Draft)
            .SumAsync(p => (decimal?)p.EstimatedPkr, cancellationToken) ?? 0m;

        supplier.TotalPurchasesRmb = purchasesRmb;
        supplier.TotalPaymentsPkr = paidPkr;
        supplier.OutstandingBalancePkr = Math.Max(0, estimatedPkr - paidPkr);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
