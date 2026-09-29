using Microsoft.EntityFrameworkCore;
using UD_Autos.Data;
using UD_Autos.Models.Sales;

namespace UD_Autos.Services;

public class AgingRow
{
    public int CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public decimal Original { get; set; }
    public decimal Paid { get; set; }
    public decimal Outstanding { get; set; }
    public int AgeDays { get; set; }
}

public interface IReceivableAgingService
{
    Task<IReadOnlyList<AgingRow>> GetAgingAsync(int? minDays, DateTime? beforeDate, int? customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerLedgerEntry>> GetLedgerAsync(int customerId, CancellationToken cancellationToken = default);
}

public class ReceivableAgingService : IReceivableAgingService
{
    private readonly ApplicationDbContext _db;

    public ReceivableAgingService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AgingRow>> GetAgingAsync(
        int? minDays, DateTime? beforeDate, int? customerId, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var query = _db.Invoices.AsNoTracking()
            .Include(i => i.Customer)
            .Where(i => !i.IsVoided && i.OutstandingAmount > 0);

        if (customerId.HasValue) query = query.Where(i => i.CustomerId == customerId);
        if (beforeDate.HasValue) query = query.Where(i => i.InvoiceDate < beforeDate.Value.Date);

        var list = await query.OrderBy(i => i.InvoiceDate).ToListAsync(cancellationToken);
        var rows = list.Select(i => new AgingRow
        {
            CustomerId = i.CustomerId,
            CustomerCode = i.Customer.CustomerCode,
            CustomerName = i.Customer.ShopName ?? i.Customer.Name,
            InvoiceId = i.Id,
            InvoiceNumber = i.InvoiceNumber,
            InvoiceDate = i.InvoiceDate,
            Original = i.BillTotal,
            Paid = i.AmountPaidOnInvoice,
            Outstanding = i.OutstandingAmount,
            AgeDays = (int)(today - i.InvoiceDate.Date).TotalDays
        });

        if (minDays.HasValue)
            rows = rows.Where(r => r.AgeDays >= minDays.Value);

        return rows.ToList();
    }

    public async Task<IReadOnlyList<CustomerLedgerEntry>> GetLedgerAsync(int customerId, CancellationToken cancellationToken = default)
        => await _db.CustomerLedgerEntries.AsNoTracking()
            .Where(e => e.CustomerId == customerId)
            .OrderBy(e => e.EntryDate).ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);
}
