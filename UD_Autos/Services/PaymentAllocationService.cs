using Microsoft.EntityFrameworkCore;
using UD_Autos.Data;
using UD_Autos.Models.Common;
using UD_Autos.Models.Finance;
using UD_Autos.Models.Sales;

namespace UD_Autos.Services;

public class ManualAllocationInput
{
    public int InvoiceId { get; set; }
    public decimal Amount { get; set; }
}

public interface IPaymentAllocationService
{
    Task<(bool Ok, string? Error, CustomerPayment? Payment)> ReceivePaymentAsync(
        int customerId,
        DateTime paymentDate,
        decimal amount,
        PaymentAccount account,
        string? reference,
        string? notes,
        IReadOnlyList<ManualAllocationInput>? manualAllocations,
        string? userId,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, string? Error)> AllocateFifoAsync(int paymentId, string? userId, CancellationToken cancellationToken = default);
}

public class PaymentAllocationService : IPaymentAllocationService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICodeGeneratorService _codes;
    private readonly ICashBankService _cash;

    public PaymentAllocationService(ApplicationDbContext db, IAuditService audit, ICodeGeneratorService codes, ICashBankService cash)
    {
        _db = db;
        _audit = audit;
        _codes = codes;
        _cash = cash;
    }

    public async Task<(bool Ok, string? Error, CustomerPayment? Payment)> ReceivePaymentAsync(
        int customerId,
        DateTime paymentDate,
        decimal amount,
        PaymentAccount account,
        string? reference,
        string? notes,
        IReadOnlyList<ManualAllocationInput>? manualAllocations,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0) return (false, "Payment amount must be greater than zero.", null);
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId && c.IsActive, cancellationToken);
        if (customer is null) return (false, "Customer not found.", null);

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var payment = new CustomerPayment
        {
            PaymentNumber = await _codes.NextPaymentNumberAsync(cancellationToken),
            CustomerId = customerId,
            PaymentDate = paymentDate.Date,
            Amount = Math.Round(amount, 2),
            Account = account,
            Reference = reference,
            Notes = notes,
            IsManualAllocation = manualAllocations is { Count: > 0 },
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        _db.CustomerPayments.Add(payment);
        await _db.SaveChangesAsync(cancellationToken);

        var lastBalance = await _db.CustomerLedgerEntries
            .Where(e => e.CustomerId == customerId)
            .OrderByDescending(e => e.EntryDate).ThenByDescending(e => e.Id)
            .Select(e => (decimal?)e.RunningBalance)
            .FirstOrDefaultAsync(cancellationToken) ?? customer.CurrentBalance;

        _db.CustomerLedgerEntries.Add(new CustomerLedgerEntry
        {
            CustomerId = customerId,
            EntryDate = payment.PaymentDate,
            EntryType = LedgerEntryType.Payment,
            Detail = $"Payment {payment.PaymentNumber}",
            ReferenceType = nameof(CustomerPayment),
            ReferenceId = payment.Id.ToString(),
            ReferenceNumber = payment.PaymentNumber,
            Debit = 0,
            Credit = payment.Amount,
            RunningBalance = Math.Round(lastBalance - payment.Amount, 2),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        });

        if (payment.IsManualAllocation)
        {
            var (ok, error) = await AllocateManualAsync(payment, manualAllocations!, userId, cancellationToken);
            if (!ok) return (false, error, null);
        }
        else
        {
            var (ok, error) = await AllocateFifoInternalAsync(payment, userId, cancellationToken);
            if (!ok) return (false, error, null);
        }

        customer.CurrentBalance = Math.Round(customer.CurrentBalance - payment.Amount, 2);
        customer.TotalPayments = Math.Round(customer.TotalPayments + payment.Amount, 2);
        customer.LastPaymentAt = payment.PaymentDate;
        customer.UpdatedAt = DateTime.UtcNow;
        customer.UpdatedBy = userId;

        await _cash.PostLinkedAsync(
            CashBankMovementType.Collection, CashBankDirection.In, payment.Account, payment.PaymentDate, payment.Amount,
            nameof(CustomerPayment), payment.Id.ToString(), payment.PaymentNumber,
            $"Collection {payment.PaymentNumber}", userId, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("ReceivePayment", nameof(CustomerPayment), payment.Id.ToString(), payment.PaymentNumber, cancellationToken: cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return (true, null, payment);
    }

    public async Task<(bool Ok, string? Error)> AllocateFifoAsync(int paymentId, string? userId, CancellationToken cancellationToken = default)
    {
        var payment = await _db.CustomerPayments.Include(p => p.Allocations).FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
        if (payment is null) return (false, "Payment not found.");
        return await AllocateFifoInternalAsync(payment, userId, cancellationToken);
    }

    private async Task<(bool Ok, string? Error)> AllocateFifoInternalAsync(CustomerPayment payment, string? userId, CancellationToken cancellationToken)
    {
        var openInvoices = await _db.Invoices
            .Where(i => i.CustomerId == payment.CustomerId && !i.IsVoided && i.OutstandingAmount > 0)
            .OrderBy(i => i.InvoiceDate).ThenBy(i => i.Id)
            .ToListAsync(cancellationToken);

        var remaining = payment.Amount;
        foreach (var invoice in openInvoices)
        {
            if (remaining <= 0) break;
            var apply = Math.Min(invoice.OutstandingAmount, remaining);
            invoice.OutstandingAmount = Math.Round(invoice.OutstandingAmount - apply, 2);
            invoice.AmountPaidOnInvoice = Math.Round(invoice.AmountPaidOnInvoice + apply, 2);
            invoice.UpdatedAt = DateTime.UtcNow;
            invoice.UpdatedBy = userId;

            _db.PaymentAllocations.Add(new PaymentAllocation
            {
                CustomerPaymentId = payment.Id,
                InvoiceId = invoice.Id,
                Amount = apply,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            });
            remaining = Math.Round(remaining - apply, 2);
        }

        // Unallocated remainder still reduces customer balance (advance); no over-allocation to invoices.
        return (true, null);
    }

    private async Task<(bool Ok, string? Error)> AllocateManualAsync(
        CustomerPayment payment, IReadOnlyList<ManualAllocationInput> manual, string? userId, CancellationToken cancellationToken)
    {
        var total = Math.Round(manual.Sum(m => m.Amount), 2);
        if (total > payment.Amount) return (false, "Manual allocation exceeds payment amount.");
        if (manual.Any(m => m.Amount <= 0)) return (false, "Allocation amounts must be positive.");

        foreach (var row in manual)
        {
            var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == row.InvoiceId && i.CustomerId == payment.CustomerId, cancellationToken);
            if (invoice is null) return (false, $"Invoice {row.InvoiceId} not found for customer.");
            if (row.Amount > invoice.OutstandingAmount + 0.009m)
                return (false, $"Allocation for {invoice.InvoiceNumber} exceeds outstanding.");

            invoice.OutstandingAmount = Math.Round(invoice.OutstandingAmount - row.Amount, 2);
            invoice.AmountPaidOnInvoice = Math.Round(invoice.AmountPaidOnInvoice + row.Amount, 2);
            invoice.UpdatedAt = DateTime.UtcNow;
            invoice.UpdatedBy = userId;

            _db.PaymentAllocations.Add(new PaymentAllocation
            {
                CustomerPaymentId = payment.Id,
                InvoiceId = invoice.Id,
                Amount = row.Amount,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            });
        }

        return (true, null);
    }
}
