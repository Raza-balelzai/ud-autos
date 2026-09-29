using Microsoft.EntityFrameworkCore;
using UD_Autos.Models.Inventory;

namespace UD_Autos.Data;

public static class Phase3ModelBuilderExtensions
{
    public static void ConfigurePhase3(this ModelBuilder builder)
    {
        builder.Entity<InventoryBatch>(e =>
        {
            e.HasIndex(x => x.BatchNumber).IsUnique();
            e.HasIndex(x => new { x.ProductId, x.ShipmentId });
            e.HasIndex(x => x.ShipmentItemId).IsUnique();
            e.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Shipment)
                .WithMany()
                .HasForeignKey(x => x.ShipmentId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ShipmentItem)
                .WithMany()
                .HasForeignKey(x => x.ShipmentItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StockMovement>(e =>
        {
            e.HasIndex(x => new { x.ProductId, x.OccurredAt });
            e.HasIndex(x => new { x.ReferenceType, x.ReferenceId });
            e.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.InventoryBatch)
                .WithMany()
                .HasForeignKey(x => x.InventoryBatchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Models.Shipments.Shipment>(e =>
        {
            e.Property(x => x.RowVersion).IsRowVersion();
        });
    }
}
