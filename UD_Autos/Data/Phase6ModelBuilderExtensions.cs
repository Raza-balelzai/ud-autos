using Microsoft.EntityFrameworkCore;
using UD_Autos.Models.Finance;

namespace UD_Autos.Data;

public static class Phase6ModelBuilderExtensions
{
    public static void ConfigurePhase6(this ModelBuilder builder)
    {
        // Extra indexes for report/dashboard query patterns (idempotent if already present via earlier configs).
        builder.Entity<Models.Sales.CustomerPayment>(e =>
        {
            e.HasIndex(x => x.PaymentDate);
            e.HasIndex(x => x.Account);
        });

        builder.Entity<OperatingExpense>(e =>
        {
            e.HasIndex(x => x.Account);
        });

        builder.Entity<CashBankMovement>(e =>
        {
            e.HasIndex(x => new { x.MovementType, x.MovementDate });
        });

        builder.Entity<Models.Inventory.InventoryBatch>(e =>
        {
            e.HasIndex(x => new { x.ProductId, x.ReceivedAt });
        });

        builder.Entity<Models.Audit.AuditLog>(e =>
        {
            e.HasIndex(x => new { x.Action, x.TimestampUtc });
        });
    }
}
