using Microsoft.EntityFrameworkCore;
using UD_Autos.Data;
using UD_Autos.Models.Common;
using UD_Autos.Models.Finance;
using UD_Autos.Models.Hawala;
using UD_Autos.Models.Purchases;

namespace UD_Autos.Services;

public interface IHawalaPaymentService
{
    Task<PagedResult<HawalaPayment>> SearchAsync(string? search, int? supplierId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<HawalaPayment?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error, HawalaPayment? Payment)> CreateAsync(HawalaPayment payment, string? userId, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> UpdateAsync(HawalaPayment payment, string? userId, CancellationToken cancellationToken = default);
}

public class HawalaPaymentService : IHawalaPaymentService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICodeGeneratorService _codes;
    private readonly ISupplierBalanceService _balances;
    private readonly ICashBankService _cash;

    public HawalaPaymentService(
        ApplicationDbContext db,
        IAuditService audit,
        ICodeGeneratorService codes,
        ISupplierBalanceService balances,
        ICashBankService cash)
    {
        _db = db;
        _audit = audit;
        _codes = codes;
        _balances = balances;
        _cash = cash;
    }

    public async Task<PagedResult<HawalaPayment>> SearchAsync(
        string? search, int? supplierId, DateTime? from, DateTime? to, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);

        var query = _db.HawalaPayments
            .AsNoTracking()
            .Include(h => h.Supplier)
            .Include(h => h.PurchaseOrder)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(h =>
                h.PaymentNumber.Contains(term) ||
                h.HawalaPerson.Contains(term) ||
                h.Supplier.Name.Contains(term) ||
                (h.Reference != null && h.Reference.Contains(term)));
        }

        if (supplierId.HasValue) query = query.Where(h => h.SupplierId == supplierId);
        if (from.HasValue) query = query.Where(h => h.PaymentDate >= from.Value.Date);
        if (to.HasValue) query = query.Where(h => h.PaymentDate <= to.Value.Date);

        query = query.OrderByDescending(h => h.PaymentDate).ThenByDescending(h => h.Id);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<HawalaPayment>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Search = search
        };
    }

    public Task<HawalaPayment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => _db.HawalaPayments
            .Include(h => h.Supplier)
            .Include(h => h.PurchaseOrder)
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken);

    public async Task<(bool Ok, string? Error, HawalaPayment? Payment)> CreateAsync(
        HawalaPayment payment, string? userId, CancellationToken cancellationToken = default)
    {
        var error = await ValidateAsync(payment, cancellationToken);
        if (error is not null) return (false, error, null);

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(payment.PaymentNumber))
            payment.PaymentNumber = await _codes.NextHawalaNumberAsync(cancellationToken);

        payment.PaymentNumber = payment.PaymentNumber.Trim().ToUpperInvariant();
        payment.EffectiveRmbRate = Math.Round(payment.PkrPaid / payment.RmbReceived, 6);
        payment.CreatedAt = DateTime.UtcNow;
        payment.CreatedBy = userId;

        _db.HawalaPayments.Add(payment);
        await ApplyToPurchaseOrderAsync(payment, isNew: true, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var outAmt = Math.Round(payment.PkrPaid + payment.HawalaFee, 2);
        await _cash.PostLinkedAsync(
            CashBankMovementType.Hawala, CashBankDirection.Out, payment.Account, payment.PaymentDate, outAmt,
            nameof(HawalaPayment), payment.Id.ToString(), payment.PaymentNumber,
            $"Hawala {payment.PaymentNumber}", userId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        await _balances.RecalculateAsync(payment.SupplierId, cancellationToken);
        await _audit.LogAsync("Create", nameof(HawalaPayment), payment.Id.ToString(), payment.PaymentNumber, cancellationToken: cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return (true, null, payment);
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(
        HawalaPayment payment, string? userId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.HawalaPayments.FirstOrDefaultAsync(h => h.Id == payment.Id, cancellationToken);
        if (existing is null) return (false, "Hawala payment not found.");

        var error = await ValidateAsync(payment, cancellationToken);
        if (error is not null) return (false, error);

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        // Reverse previous PO allocation effect, then apply new values.
        await ReversePurchaseOrderAllocationAsync(existing, cancellationToken);

        existing.PaymentDate = payment.PaymentDate.Date;
        existing.Account = payment.Account;
        existing.HawalaPerson = payment.HawalaPerson.Trim();
        existing.SupplierId = payment.SupplierId;
        existing.PurchaseOrderId = payment.PurchaseOrderId;
        existing.RmbReceived = payment.RmbReceived;
        existing.PkrPaid = payment.PkrPaid;
        existing.HawalaFee = payment.HawalaFee;
        existing.IsDirectImportCharge = payment.IsDirectImportCharge;
        existing.Reference = payment.Reference;
        existing.Notes = payment.Notes;
        existing.EffectiveRmbRate = Math.Round(payment.PkrPaid / payment.RmbReceived, 6);
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = userId;

        await ApplyToPurchaseOrderAsync(existing, isNew: false, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var outAmt = Math.Round(existing.PkrPaid + existing.HawalaFee, 2);
        await _cash.PostLinkedAsync(
            CashBankMovementType.Hawala, CashBankDirection.Out, existing.Account, existing.PaymentDate, outAmt,
            nameof(HawalaPayment), existing.Id.ToString(), existing.PaymentNumber,
            $"Hawala {existing.PaymentNumber}", userId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        await _balances.RecalculateAsync(existing.SupplierId, cancellationToken);
        await _audit.LogAsync("Update", nameof(HawalaPayment), existing.Id.ToString(), existing.PaymentNumber, cancellationToken: cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return (true, null);
    }

    private async Task<string?> ValidateAsync(HawalaPayment payment, CancellationToken cancellationToken)
    {
        if (payment.SupplierId <= 0) return "Supplier is required.";
        if (string.IsNullOrWhiteSpace(payment.HawalaPerson)) return "Hawala person/service is required.";
        if (payment.RmbReceived <= 0) return "RMB received must be greater than zero.";
        if (payment.PkrPaid <= 0) return "PKR paid must be greater than zero.";
        if (payment.HawalaFee < 0) return "Hawala fee cannot be negative.";
        if (!await _db.Suppliers.AnyAsync(s => s.Id == payment.SupplierId && s.IsActive, cancellationToken))
            return "Supplier not found or inactive.";

        if (payment.PurchaseOrderId.HasValue)
        {
            var po = await _db.PurchaseOrders.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == payment.PurchaseOrderId.Value, cancellationToken);
            if (po is null) return "Linked purchase order not found.";
            if (po.SupplierId != payment.SupplierId) return "PO supplier must match Hawala supplier.";
        }

        return null;
    }

    private async Task ApplyToPurchaseOrderAsync(HawalaPayment payment, bool isNew, CancellationToken cancellationToken)
    {
        if (!payment.PurchaseOrderId.HasValue) return;
        var po = await _db.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == payment.PurchaseOrderId.Value, cancellationToken);
        if (po is null) return;

        po.PaidRmb = Math.Round(po.PaidRmb + payment.RmbReceived, 4);
        po.PaidPkr = Math.Round(po.PaidPkr + payment.PkrPaid, 2);
        po.BalanceRmb = Math.Round(po.TotalRmb - po.PaidRmb, 4);
        _ = isNew;
    }

    private async Task ReversePurchaseOrderAllocationAsync(HawalaPayment existing, CancellationToken cancellationToken)
    {
        if (!existing.PurchaseOrderId.HasValue) return;
        var po = await _db.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == existing.PurchaseOrderId.Value, cancellationToken);
        if (po is null) return;

        po.PaidRmb = Math.Round(Math.Max(0, po.PaidRmb - existing.RmbReceived), 4);
        po.PaidPkr = Math.Round(Math.Max(0, po.PaidPkr - existing.PkrPaid), 2);
        po.BalanceRmb = Math.Round(po.TotalRmb - po.PaidRmb, 4);
    }
}
