using Microsoft.EntityFrameworkCore;
using UD_Autos.Data;
using UD_Autos.Models.Finance;
using UD_Autos.Models.Sales;

namespace UD_Autos.Services;

public class CashBankStatement
{
    public PaymentAccount? AccountFilter { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public decimal Opening { get; set; }
    public decimal Collections { get; set; }
    public decimal OtherReceipts { get; set; }
    public decimal Hawala { get; set; }
    public decimal Expenses { get; set; }
    public decimal OtherPayments { get; set; }
    public decimal SalesRefunds { get; set; }
    public decimal Closing { get; set; }
    public IReadOnlyList<CashBankMovement> Movements { get; set; } = Array.Empty<CashBankMovement>();
}

public interface ICashBankService
{
    Task PostLinkedAsync(
        CashBankMovementType type,
        CashBankDirection direction,
        PaymentAccount account,
        DateTime date,
        decimal amount,
        string referenceType,
        string referenceId,
        string? referenceNumber,
        string detail,
        string? userId,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, string? Error)> SetOpeningAsync(PaymentAccount account, DateTime effectiveDate, decimal amount, string? notes, string? userId, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error, CashBankMovement? Movement)> AddManualAsync(PaymentAccount account, DateTime date, CashBankMovementType type, decimal amount, string detail, string? notes, string? userId, CancellationToken cancellationToken = default);
    Task<CashBankStatement> GetStatementAsync(DateTime from, DateTime to, PaymentAccount? account, CancellationToken cancellationToken = default);
    Task SyncMissingLinkedAsync(string? userId, CancellationToken cancellationToken = default);
}

public class CashBankService : ICashBankService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICodeGeneratorService _codes;

    public CashBankService(ApplicationDbContext db, IAuditService audit, ICodeGeneratorService codes)
    {
        _db = db;
        _audit = audit;
        _codes = codes;
    }

    public async Task PostLinkedAsync(
        CashBankMovementType type,
        CashBankDirection direction,
        PaymentAccount account,
        DateTime date,
        decimal amount,
        string referenceType,
        string referenceId,
        string? referenceNumber,
        string detail,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0) return;

        var existing = await _db.CashBankMovements
            .FirstOrDefaultAsync(m => m.ReferenceType == referenceType && m.ReferenceId == referenceId, cancellationToken);

        if (existing is not null)
        {
            existing.MovementDate = date.Date;
            existing.Account = account;
            existing.MovementType = type;
            existing.Direction = direction;
            existing.Amount = Math.Round(amount, 2);
            existing.ReferenceNumber = referenceNumber;
            existing.Detail = detail;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = userId;
            return;
        }

        _db.CashBankMovements.Add(new CashBankMovement
        {
            MovementNumber = await _codes.NextCashMovementNumberAsync(cancellationToken),
            MovementDate = date.Date,
            Account = account,
            MovementType = type,
            Direction = direction,
            Amount = Math.Round(amount, 2),
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            ReferenceNumber = referenceNumber,
            Detail = detail,
            IsLinked = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        });
    }

    public async Task<(bool Ok, string? Error)> SetOpeningAsync(
        PaymentAccount account, DateTime effectiveDate, decimal amount, string? notes, string? userId, CancellationToken cancellationToken = default)
    {
        var day = effectiveDate.Date;
        var row = await _db.CashBankOpenings.FirstOrDefaultAsync(o => o.Account == account && o.EffectiveDate == day, cancellationToken);
        if (row is null)
        {
            row = new CashBankOpening
            {
                Account = account,
                EffectiveDate = day,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            };
            _db.CashBankOpenings.Add(row);
        }

        row.Amount = Math.Round(amount, 2);
        row.Notes = notes;
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = userId;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("SetOpening", nameof(CashBankOpening), row.Id.ToString(), $"{account} {day:yyyy-MM-dd}", cancellationToken: cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error, CashBankMovement? Movement)> AddManualAsync(
        PaymentAccount account, DateTime date, CashBankMovementType type, decimal amount, string detail, string? notes, string? userId, CancellationToken cancellationToken = default)
    {
        if (amount <= 0) return (false, "Amount must be greater than zero.", null);
        if (type is not (CashBankMovementType.OtherReceipt or CashBankMovementType.OtherPayment))
            return (false, "Manual entries are limited to Other Receipt or Other Payment.", null);

        var movement = new CashBankMovement
        {
            MovementNumber = await _codes.NextCashMovementNumberAsync(cancellationToken),
            MovementDate = date.Date,
            Account = account,
            MovementType = type,
            Direction = type == CashBankMovementType.OtherReceipt ? CashBankDirection.In : CashBankDirection.Out,
            Amount = Math.Round(amount, 2),
            Detail = string.IsNullOrWhiteSpace(detail) ? type.ToString() : detail.Trim(),
            Notes = notes,
            IsLinked = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        _db.CashBankMovements.Add(movement);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("ManualCash", nameof(CashBankMovement), movement.Id.ToString(), movement.MovementNumber, cancellationToken: cancellationToken);
        return (true, null, movement);
    }

    public async Task<CashBankStatement> GetStatementAsync(DateTime from, DateTime to, PaymentAccount? account, CancellationToken cancellationToken = default)
    {
        from = from.Date;
        to = to.Date;

        var openingsQuery = _db.CashBankOpenings.AsNoTracking().Where(o => o.EffectiveDate <= from);
        if (account.HasValue) openingsQuery = openingsQuery.Where(o => o.Account == account);

        // Latest opening on or before From per account, then sum.
        var openings = await openingsQuery.ToListAsync(cancellationToken);
        var openingAmount = openings
            .GroupBy(o => o.Account)
            .Select(g => g.OrderByDescending(x => x.EffectiveDate).First().Amount)
            .DefaultIfEmpty(0)
            .Sum();

        // Movements before From that affect opening cash position when no/partial opening exists:
        // Use opening as base, then add net of linked+manual from day after opening through day before From.
        // Simpler Phase 5 rule: Opening = sum of latest openings per account as of From;
        // then period movements only between From and To.
        // Prior activity after opening dates still needs inclusion for true closing.
        var priorQuery = _db.CashBankMovements.AsNoTracking().Where(m => m.MovementDate < from);
        if (account.HasValue) priorQuery = priorQuery.Where(m => m.Account == account);
        var prior = await priorQuery.ToListAsync(cancellationToken);

        // For each account, start from opening effective date; only count prior movements on/after that opening.
        decimal priorNet = 0;
        var accounts = account.HasValue
            ? new[] { account.Value }
            : Enum.GetValues<PaymentAccount>();
        foreach (var acc in accounts)
        {
            var open = openings.Where(o => o.Account == acc).OrderByDescending(o => o.EffectiveDate).FirstOrDefault();
            var openDate = open?.EffectiveDate ?? DateTime.MinValue;
            var openAmt = open?.Amount ?? 0m;
            var net = prior.Where(m => m.Account == acc && m.MovementDate >= openDate)
                .Sum(m => m.Direction == CashBankDirection.In ? m.Amount : -m.Amount);
            priorNet += openAmt + net;
        }

        // When filtering one account, openingAmount was that account only; rebuild opening from priorNet.
        var trueOpening = priorNet;

        var moveQuery = _db.CashBankMovements.AsNoTracking()
            .Where(m => m.MovementDate >= from && m.MovementDate <= to);
        if (account.HasValue) moveQuery = moveQuery.Where(m => m.Account == account);
        var movements = await moveQuery.OrderBy(m => m.MovementDate).ThenBy(m => m.Id).ToListAsync(cancellationToken);

        decimal Sum(CashBankMovementType t) => movements.Where(m => m.MovementType == t).Sum(m => m.Amount);

        var collections = Sum(CashBankMovementType.Collection);
        var otherIn = Sum(CashBankMovementType.OtherReceipt);
        var hawala = Sum(CashBankMovementType.Hawala);
        var expenses = Sum(CashBankMovementType.Expense);
        var otherOut = Sum(CashBankMovementType.OtherPayment);
        var refunds = Sum(CashBankMovementType.SalesRefund);

        var closing = trueOpening + collections + otherIn - hawala - expenses - otherOut - refunds;

        return new CashBankStatement
        {
            AccountFilter = account,
            From = from,
            To = to,
            Opening = trueOpening,
            Collections = collections,
            OtherReceipts = otherIn,
            Hawala = hawala,
            Expenses = expenses,
            OtherPayments = otherOut,
            SalesRefunds = refunds,
            Closing = closing,
            Movements = movements
        };
    }

    public async Task SyncMissingLinkedAsync(string? userId, CancellationToken cancellationToken = default)
    {
        var payments = await _db.CustomerPayments.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var p in payments)
        {
            await PostLinkedAsync(CashBankMovementType.Collection, CashBankDirection.In, p.Account, p.PaymentDate, p.Amount,
                nameof(CustomerPayment), p.Id.ToString(), p.PaymentNumber, $"Collection {p.PaymentNumber}", userId, cancellationToken);
        }

        var hawalas = await _db.HawalaPayments.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var h in hawalas)
        {
            var outAmt = Math.Round(h.PkrPaid + h.HawalaFee, 2);
            await PostLinkedAsync(CashBankMovementType.Hawala, CashBankDirection.Out, h.Account, h.PaymentDate, outAmt,
                nameof(Models.Hawala.HawalaPayment), h.Id.ToString(), h.PaymentNumber, $"Hawala {h.PaymentNumber}", userId, cancellationToken);
        }

        var expenses = await _db.OperatingExpenses.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var e in expenses)
        {
            await PostLinkedAsync(CashBankMovementType.Expense, CashBankDirection.Out, e.Account, e.ExpenseDate, e.Amount,
                nameof(OperatingExpense), e.Id.ToString(), e.ExpenseNumber, $"Expense {e.ExpenseNumber}", userId, cancellationToken);
        }

        var refunds = await _db.SalesReturns.AsNoTracking()
            .Where(r => !r.AdjustKhata && r.RefundAccount != null)
            .ToListAsync(cancellationToken);
        foreach (var r in refunds)
        {
            await PostLinkedAsync(CashBankMovementType.SalesRefund, CashBankDirection.Out, r.RefundAccount!.Value, r.ReturnDate, r.TotalAmount,
                nameof(SalesReturn), r.Id.ToString(), r.ReturnNumber, $"Refund {r.ReturnNumber}", userId, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
