using Microsoft.EntityFrameworkCore;
using UD_Autos.Data;
using UD_Autos.Models.Finance;
using UD_Autos.Models.Inventory;
using UD_Autos.Models.Sales;

namespace UD_Autos.Services;

public class ReturnLineInput
{
    public int InvoiceItemId { get; set; }
    public decimal Quantity { get; set; }
    public string? Notes { get; set; }
}

public interface ISalesReturnService
{
    Task<(bool Ok, string? Error, SalesReturn? Return)> CreateAsync(
        int invoiceId,
        DateTime returnDate,
        string reason,
        bool adjustKhata,
        PaymentAccount? refundAccount,
        IReadOnlyList<ReturnLineInput> lines,
        string? userId,
        CancellationToken cancellationToken = default);
}

public class SalesReturnService : ISalesReturnService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICodeGeneratorService _codes;
    private readonly ICashBankService _cash;

    public SalesReturnService(ApplicationDbContext db, IAuditService audit, ICodeGeneratorService codes, ICashBankService cash)
    {
        _db = db;
        _audit = audit;
        _codes = codes;
        _cash = cash;
    }

    public async Task<(bool Ok, string? Error, SalesReturn? Return)> CreateAsync(
        int invoiceId,
        DateTime returnDate,
        string reason,
        bool adjustKhata,
        PaymentAccount? refundAccount,
        IReadOnlyList<ReturnLineInput> lines,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) return (false, "Reason is required.", null);
        if (lines.Count == 0) return (false, "Select at least one item to return.", null);
        if (!adjustKhata && refundAccount is null) return (false, "Select refund account for cash/bank refund.", null);

        var invoice = await _db.Invoices
            .Include(i => i.Customer)
            .Include(i => i.Items).ThenInclude(x => x.Product)
            .Include(i => i.Items).ThenInclude(x => x.BatchAllocations)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && !i.IsVoided, cancellationToken);
        if (invoice is null) return (false, "Invoice not found.", null);

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var salesReturn = new SalesReturn
        {
            ReturnNumber = await _codes.NextReturnNumberAsync(cancellationToken),
            InvoiceId = invoice.Id,
            CustomerId = invoice.CustomerId,
            ReturnDate = returnDate.Date,
            Reason = reason.Trim(),
            AdjustKhata = adjustKhata,
            RefundAccount = refundAccount,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        decimal total = 0;
        foreach (var line in lines)
        {
            if (line.Quantity <= 0) return (false, "Return quantity must be greater than zero.", null);
            var item = invoice.Items.FirstOrDefault(i => i.Id == line.InvoiceItemId);
            if (item is null) return (false, "Invoice item not found.", null);

            var eligible = item.Quantity - item.ReturnedQuantity;
            if (line.Quantity > eligible)
                return (false, $"Cannot return more than eligible qty for {item.Product.Sku} ({eligible:N2}).", null);

            var lineTotal = Math.Round(line.Quantity * item.UnitPrice, 2);
            item.ReturnedQuantity += line.Quantity;
            total += lineTotal;

            // Restock using original COGS; prefer original batches if available.
            var product = item.Product;
            var qtyLeft = line.Quantity;
            foreach (var alloc in item.BatchAllocations.OrderByDescending(a => a.Id))
            {
                if (qtyLeft <= 0) break;
                var batch = await _db.InventoryBatches.FirstOrDefaultAsync(b => b.Id == alloc.InventoryBatchId, cancellationToken);
                if (batch is null) continue;
                var put = Math.Min(qtyLeft, alloc.Quantity);
                batch.RemainingQuantity += put;
                batch.UpdatedAt = DateTime.UtcNow;
                batch.UpdatedBy = userId;
                qtyLeft -= put;
            }

            product.CurrentStock += line.Quantity;
            product.StockValue = Math.Round(product.CurrentStock * (product.AverageCost ?? product.LatestLandedCost ?? item.UnitCogsPkr), 2);
            product.UpdatedAt = DateTime.UtcNow;
            product.UpdatedBy = userId;

            _db.StockMovements.Add(new StockMovement
            {
                ProductId = product.Id,
                MovementType = StockMovementType.CustomerReturn,
                Quantity = line.Quantity,
                UnitCostPkr = item.UnitCogsPkr,
                QuantityAfter = product.CurrentStock,
                ReferenceType = nameof(SalesReturn),
                ReferenceNumber = salesReturn.ReturnNumber,
                Reason = reason,
                OccurredAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            });

            salesReturn.Items.Add(new SalesReturnItem
            {
                InvoiceItemId = item.Id,
                ProductId = item.ProductId,
                Quantity = line.Quantity,
                UnitPrice = item.UnitPrice,
                LineTotal = lineTotal,
                UnitCogsPkr = item.UnitCogsPkr,
                Notes = line.Notes,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            });
        }

        salesReturn.TotalAmount = Math.Round(total, 2);

        // Financial: reduce receivable / refund.
        // Reduce invoice outstanding first (if any), remainder adjusts customer balance via ledger credit.
        var applyToOutstanding = Math.Min(invoice.OutstandingAmount, salesReturn.TotalAmount);
        invoice.OutstandingAmount = Math.Round(invoice.OutstandingAmount - applyToOutstanding, 2);
        invoice.BillTotal = invoice.BillTotal; // historical bill unchanged
        invoice.UpdatedAt = DateTime.UtcNow;
        invoice.UpdatedBy = userId;

        var customer = invoice.Customer;
        var lastBalance = await _db.CustomerLedgerEntries
            .Where(e => e.CustomerId == customer.Id)
            .OrderByDescending(e => e.EntryDate).ThenByDescending(e => e.Id)
            .Select(e => (decimal?)e.RunningBalance)
            .FirstOrDefaultAsync(cancellationToken) ?? customer.CurrentBalance;

        if (adjustKhata)
        {
            _db.CustomerLedgerEntries.Add(new CustomerLedgerEntry
            {
                CustomerId = customer.Id,
                EntryDate = salesReturn.ReturnDate,
                EntryType = LedgerEntryType.Return,
                Detail = $"Return {salesReturn.ReturnNumber} on {invoice.InvoiceNumber}",
                ReferenceType = nameof(SalesReturn),
                ReferenceNumber = salesReturn.ReturnNumber,
                Debit = 0,
                Credit = salesReturn.TotalAmount,
                RunningBalance = Math.Round(lastBalance - salesReturn.TotalAmount, 2),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            });
            customer.CurrentBalance = Math.Round(customer.CurrentBalance - salesReturn.TotalAmount, 2);
        }
        else
        {
            // Cash/bank refund — still ledger note with credit for audit of customer relation when originally paid.
            _db.CustomerLedgerEntries.Add(new CustomerLedgerEntry
            {
                CustomerId = customer.Id,
                EntryDate = salesReturn.ReturnDate,
                EntryType = LedgerEntryType.Return,
                Detail = $"Cash/Bank refund {salesReturn.ReturnNumber} ({refundAccount})",
                ReferenceType = nameof(SalesReturn),
                ReferenceNumber = salesReturn.ReturnNumber,
                Debit = 0,
                Credit = 0,
                RunningBalance = lastBalance,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            });
        }

        customer.UpdatedAt = DateTime.UtcNow;
        customer.UpdatedBy = userId;

        _db.SalesReturns.Add(salesReturn);
        await _db.SaveChangesAsync(cancellationToken);

        if (!adjustKhata && refundAccount.HasValue && salesReturn.TotalAmount > 0)
        {
            await _cash.PostLinkedAsync(
                CashBankMovementType.SalesRefund, CashBankDirection.Out, refundAccount.Value, salesReturn.ReturnDate, salesReturn.TotalAmount,
                nameof(SalesReturn), salesReturn.Id.ToString(), salesReturn.ReturnNumber,
                $"Refund {salesReturn.ReturnNumber}", userId, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        await _audit.LogAsync("SalesReturn", nameof(SalesReturn), salesReturn.Id.ToString(), salesReturn.ReturnNumber, reason, cancellationToken: cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return (true, null, salesReturn);
    }
}
