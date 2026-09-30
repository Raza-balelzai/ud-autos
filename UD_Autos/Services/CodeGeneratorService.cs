using Microsoft.EntityFrameworkCore;
using UD_Autos.Data;
using UD_Autos.Models.Settings;

namespace UD_Autos.Services;

public interface ICodeGeneratorService
{
    Task<string> NextSkuAsync(CancellationToken cancellationToken = default);
    Task<string> NextCustomerCodeAsync(CancellationToken cancellationToken = default);
    Task<string> NextSupplierCodeAsync(CancellationToken cancellationToken = default);
    Task<string> NextPoNumberAsync(CancellationToken cancellationToken = default);
    Task<string> NextHawalaNumberAsync(CancellationToken cancellationToken = default);
    Task<string> NextShipmentNumberAsync(CancellationToken cancellationToken = default);
    Task<string> NextSlipNumberAsync(int shipmentId, CancellationToken cancellationToken = default);
    Task<string> NextInvoiceNumberAsync(CancellationToken cancellationToken = default);
    Task<string> NextPaymentNumberAsync(CancellationToken cancellationToken = default);
    Task<string> NextReturnNumberAsync(CancellationToken cancellationToken = default);
    Task<string> NextSupplierReturnNumberAsync(CancellationToken cancellationToken = default);
    Task<string> NextExpenseNumberAsync(CancellationToken cancellationToken = default);
    Task<string> NextCashMovementNumberAsync(CancellationToken cancellationToken = default);
}

public class CodeGeneratorService : ICodeGeneratorService
{
    private readonly ApplicationDbContext _db;

    public CodeGeneratorService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<string> NextSkuAsync(CancellationToken cancellationToken = default)
    {
        var prefix = await GetSettingAsync("SkuPrefix", "SKU", cancellationToken);
        var max = await _db.Products
            .AsNoTracking()
            .Where(p => p.Sku.StartsWith(prefix + "-"))
            .Select(p => p.Sku)
            .ToListAsync(cancellationToken);

        var next = NextNumber(max, prefix);
        return $"{prefix}-{next:D5}";
    }

    public async Task<string> NextCustomerCodeAsync(CancellationToken cancellationToken = default)
    {
        var prefix = await GetSettingAsync("CustomerCodePrefix", "CUS", cancellationToken);
        var max = await _db.Customers
            .AsNoTracking()
            .Where(c => c.CustomerCode.StartsWith(prefix + "-"))
            .Select(c => c.CustomerCode)
            .ToListAsync(cancellationToken);

        var next = NextNumber(max, prefix);
        return $"{prefix}-{next:D5}";
    }

    public async Task<string> NextSupplierCodeAsync(CancellationToken cancellationToken = default)
    {
        var prefix = await GetSettingAsync("SupplierCodePrefix", "SUP", cancellationToken);
        var max = await _db.Suppliers
            .AsNoTracking()
            .Where(s => s.SupplierCode.StartsWith(prefix + "-"))
            .Select(s => s.SupplierCode)
            .ToListAsync(cancellationToken);

        var next = NextNumber(max, prefix);
        return $"{prefix}-{next:D5}";
    }

    public async Task<string> NextPoNumberAsync(CancellationToken cancellationToken = default)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"PO-{year}";
        var codes = await _db.PurchaseOrders
            .AsNoTracking()
            .Where(p => p.PoNumber.StartsWith(prefix + "-"))
            .Select(p => p.PoNumber)
            .ToListAsync(cancellationToken);
        return $"{prefix}-{NextNumber(codes, prefix):D3}";
    }

    public async Task<string> NextHawalaNumberAsync(CancellationToken cancellationToken = default)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"HWL-{year}";
        var codes = await _db.HawalaPayments
            .AsNoTracking()
            .Where(p => p.PaymentNumber.StartsWith(prefix + "-"))
            .Select(p => p.PaymentNumber)
            .ToListAsync(cancellationToken);
        return $"{prefix}-{NextNumber(codes, prefix):D3}";
    }

    public async Task<string> NextShipmentNumberAsync(CancellationToken cancellationToken = default)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"SHP-{year}";
        var codes = await _db.Shipments
            .AsNoTracking()
            .Where(s => s.ShipmentNumber.StartsWith(prefix + "-"))
            .Select(s => s.ShipmentNumber)
            .ToListAsync(cancellationToken);
        return $"{prefix}-{NextNumber(codes, prefix):D3}";
    }

    public async Task<string> NextSlipNumberAsync(int shipmentId, CancellationToken cancellationToken = default)
    {
        var count = await _db.ChinaWarehouseReceivingSlips
            .CountAsync(s => s.ShipmentId == shipmentId, cancellationToken);
        return $"SLIP-{(count + 1):D3}";
    }

    public async Task<string> NextInvoiceNumberAsync(CancellationToken cancellationToken = default)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"INV-{year}";
        var codes = await _db.Invoices.AsNoTracking()
            .Where(i => i.InvoiceNumber.StartsWith(prefix + "-"))
            .Select(i => i.InvoiceNumber)
            .ToListAsync(cancellationToken);
        return $"{prefix}-{NextNumber(codes, prefix):D4}";
    }

    public async Task<string> NextPaymentNumberAsync(CancellationToken cancellationToken = default)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"RCV-{year}";
        var codes = await _db.CustomerPayments.AsNoTracking()
            .Where(p => p.PaymentNumber.StartsWith(prefix + "-"))
            .Select(p => p.PaymentNumber)
            .ToListAsync(cancellationToken);
        return $"{prefix}-{NextNumber(codes, prefix):D4}";
    }

    public async Task<string> NextReturnNumberAsync(CancellationToken cancellationToken = default)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"SRN-{year}";
        var codes = await _db.SalesReturns.AsNoTracking()
            .Where(r => r.ReturnNumber.StartsWith(prefix + "-"))
            .Select(r => r.ReturnNumber)
            .ToListAsync(cancellationToken);
        return $"{prefix}-{NextNumber(codes, prefix):D4}";
    }

    public async Task<string> NextSupplierReturnNumberAsync(CancellationToken cancellationToken = default)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"SPR-{year}";
        var codes = await _db.SupplierReturns.AsNoTracking()
            .Where(r => r.ReturnNumber.StartsWith(prefix + "-"))
            .Select(r => r.ReturnNumber)
            .ToListAsync(cancellationToken);
        return $"{prefix}-{NextNumber(codes, prefix):D4}";
    }

    public async Task<string> NextExpenseNumberAsync(CancellationToken cancellationToken = default)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"EXP-{year}";
        var codes = await _db.OperatingExpenses.AsNoTracking()
            .Where(e => e.ExpenseNumber.StartsWith(prefix + "-"))
            .Select(e => e.ExpenseNumber)
            .ToListAsync(cancellationToken);
        return $"{prefix}-{NextNumber(codes, prefix):D4}";
    }

    public async Task<string> NextCashMovementNumberAsync(CancellationToken cancellationToken = default)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"CBM-{year}";
        var codes = await _db.CashBankMovements.AsNoTracking()
            .Where(m => m.MovementNumber.StartsWith(prefix + "-"))
            .Select(m => m.MovementNumber)
            .ToListAsync(cancellationToken);
        return $"{prefix}-{NextNumber(codes, prefix):D4}";
    }

    private async Task<string> GetSettingAsync(string key, string fallback, CancellationToken cancellationToken)
    {
        var value = await _db.BusinessSettings
            .AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static int NextNumber(IEnumerable<string> codes, string prefix)
    {
        var highest = 0;
        var needle = prefix + "-";
        foreach (var code in codes)
        {
            if (code.Length <= needle.Length) continue;
            if (int.TryParse(code.AsSpan(needle.Length), out var n) && n > highest)
            {
                highest = n;
            }
        }
        return highest + 1;
    }
}
