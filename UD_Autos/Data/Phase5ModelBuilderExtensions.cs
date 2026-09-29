using Microsoft.EntityFrameworkCore;
using UD_Autos.Models.Finance;

namespace UD_Autos.Data;

public static class Phase5ModelBuilderExtensions
{
    public static void ConfigurePhase5(this ModelBuilder builder)
    {
        builder.Entity<CashBankOpening>(e =>
        {
            e.HasIndex(x => new { x.Account, x.EffectiveDate });
            e.Property(x => x.Amount).HasPrecision(18, 2);
        });

        builder.Entity<CashBankMovement>(e =>
        {
            e.HasIndex(x => x.MovementNumber).IsUnique();
            e.HasIndex(x => new { x.Account, x.MovementDate });
            e.HasIndex(x => new { x.ReferenceType, x.ReferenceId });
            e.Property(x => x.Amount).HasPrecision(18, 2);
        });

        builder.Entity<OperatingExpense>(e =>
        {
            e.HasIndex(x => x.ExpenseNumber).IsUnique();
            e.HasIndex(x => new { x.ExpenseDate, x.CategoryId });
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
