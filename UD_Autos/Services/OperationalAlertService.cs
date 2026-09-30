using Microsoft.EntityFrameworkCore;
using UD_Autos.Data;
using UD_Autos.Models.Shipments;

namespace UD_Autos.Services;

public class OperationalAlertItem
{
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public interface IOperationalAlertService
{
    Task<IReadOnlyList<OperationalAlertItem>> GetAlertsAsync(CancellationToken cancellationToken = default);
}

public class OperationalAlertService : IOperationalAlertService
{
    private readonly ApplicationDbContext _db;

    public OperationalAlertService(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<OperationalAlertItem>> GetAlertsAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var overdueCutoff = today.AddDays(-30);
        var alerts = new List<OperationalAlertItem>();

        var lowStock = await _db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.ReorderLevel > 0 && p.CurrentStock <= p.ReorderLevel)
            .CountAsync(cancellationToken);
        if (lowStock > 0)
        {
            alerts.Add(new OperationalAlertItem
            {
                Title = "Low stock",
                Detail = $"{lowStock} product(s) at or below reorder level.",
                Url = "/Inventory?lowStock=true"
            });
        }

        var overdueCust = await _db.Invoices.AsNoTracking()
            .Where(i => !i.IsVoided && i.OutstandingAmount > 0 && i.InvoiceDate <= overdueCutoff)
            .Select(i => i.CustomerId)
            .Distinct()
            .CountAsync(cancellationToken);
        if (overdueCust > 0)
        {
            alerts.Add(new OperationalAlertItem
            {
                Title = "Overdue receivables",
                Detail = $"{overdueCust} customer(s) have invoices overdue 30+ days.",
                Url = "/Payments/Aging?days=30"
            });
        }

        var stuck = await _db.Shipments.AsNoTracking()
            .CountAsync(s => s.Status == ShipmentStatus.InTransit && s.UpdatedAt < today.AddDays(-14), cancellationToken);
        if (stuck > 0)
        {
            alerts.Add(new OperationalAlertItem
            {
                Title = "Shipments delayed",
                Detail = $"{stuck} shipment(s) in transit for 14+ days.",
                Url = "/Shipments?status=InTransit"
            });
        }

        return alerts;
    }
}
