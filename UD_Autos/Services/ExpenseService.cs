using Microsoft.EntityFrameworkCore;
using UD_Autos.Data;
using UD_Autos.Models.Common;
using UD_Autos.Models.Finance;
using UD_Autos.Models.Sales;

namespace UD_Autos.Services;

public interface IExpenseService
{
    Task<PagedResult<OperatingExpense>> SearchAsync(string? search, int? categoryId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<OperatingExpense?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error, OperatingExpense? Expense)> CreateAsync(OperatingExpense expense, string? userId, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> SoftVoidAsync(int id, string? userId, CancellationToken cancellationToken = default);
    Task<decimal> SumAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
}

public class ExpenseService : IExpenseService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICodeGeneratorService _codes;
    private readonly ICashBankService _cash;

    public ExpenseService(ApplicationDbContext db, IAuditService audit, ICodeGeneratorService codes, ICashBankService cash)
    {
        _db = db;
        _audit = audit;
        _codes = codes;
        _cash = cash;
    }

    public async Task<PagedResult<OperatingExpense>> SearchAsync(
        string? search, int? categoryId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);
        var query = _db.OperatingExpenses.AsNoTracking().Include(e => e.Category).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(e => e.ExpenseNumber.Contains(term) || e.Category.Name.Contains(term)
                || (e.Reference != null && e.Reference.Contains(term)) || (e.Notes != null && e.Notes.Contains(term)));
        }
        if (categoryId.HasValue) query = query.Where(e => e.CategoryId == categoryId);
        if (from.HasValue) query = query.Where(e => e.ExpenseDate >= from.Value.Date);
        if (to.HasValue) query = query.Where(e => e.ExpenseDate <= to.Value.Date);

        query = query.OrderByDescending(e => e.ExpenseDate).ThenByDescending(e => e.Id);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<OperatingExpense> { Items = items, Page = page, PageSize = pageSize, TotalCount = total, Search = search };
    }

    public Task<OperatingExpense?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => _db.OperatingExpenses.Include(e => e.Category).FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<(bool Ok, string? Error, OperatingExpense? Expense)> CreateAsync(
        OperatingExpense expense, string? userId, CancellationToken cancellationToken = default)
    {
        if (expense.Amount <= 0) return (false, "Expense amount must be greater than zero.", null);
        var category = await _db.ExpenseCategories.FirstOrDefaultAsync(c => c.Id == expense.CategoryId && c.IsActive, cancellationToken);
        if (category is null) return (false, "Expense category not found.", null);

        if (category.IsDirectImportCost && !expense.AcknowledgedImportRisk)
            return (false, "This category is flagged as a direct import cost. Acknowledge to avoid double-counting with shipment landed cost, or use a different category.", null);

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        expense.ExpenseNumber = await _codes.NextExpenseNumberAsync(cancellationToken);
        expense.ExpenseDate = expense.ExpenseDate.Date;
        expense.Amount = Math.Round(expense.Amount, 2);
        expense.CreatedAt = DateTime.UtcNow;
        expense.CreatedBy = userId;
        _db.OperatingExpenses.Add(expense);
        await _db.SaveChangesAsync(cancellationToken);

        await _cash.PostLinkedAsync(
            CashBankMovementType.Expense, CashBankDirection.Out, expense.Account, expense.ExpenseDate, expense.Amount,
            nameof(OperatingExpense), expense.Id.ToString(), expense.ExpenseNumber,
            $"Expense {expense.ExpenseNumber} — {category.Name}", userId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync("Create", nameof(OperatingExpense), expense.Id.ToString(), expense.ExpenseNumber,
            category.IsDirectImportCost ? "Import-cost category acknowledged" : null, cancellationToken: cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return (true, null, expense);
    }

    public async Task<(bool Ok, string? Error)> SoftVoidAsync(int id, string? userId, CancellationToken cancellationToken = default)
    {
        // Soft void: reverse cash movement and mark notes; keep audit trail.
        var expense = await _db.OperatingExpenses.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (expense is null) return (false, "Expense not found.");

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var move = await _db.CashBankMovements.FirstOrDefaultAsync(
            m => m.ReferenceType == nameof(OperatingExpense) && m.ReferenceId == id.ToString(), cancellationToken);
        if (move is not null) _db.CashBankMovements.Remove(move);
        _db.OperatingExpenses.Remove(expense);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("Delete", nameof(OperatingExpense), id.ToString(), expense.ExpenseNumber, cancellationToken: cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return (true, null);
    }

    public async Task<decimal> SumAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
        => await _db.OperatingExpenses.AsNoTracking()
            .Where(e => e.ExpenseDate >= from.Date && e.ExpenseDate <= to.Date)
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;
}
