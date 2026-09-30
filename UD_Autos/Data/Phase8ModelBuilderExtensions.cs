using Microsoft.EntityFrameworkCore;
using UD_Autos.Models.Inventory;

namespace UD_Autos.Data;

public static class Phase8ModelBuilderExtensions
{
    public static void ConfigurePhase8(this ModelBuilder builder)
    {
        builder.Entity<SupplierReturn>(e =>
        {
            e.HasIndex(x => x.ReturnNumber).IsUnique();
            e.HasIndex(x => x.ReturnDate);
            e.HasIndex(x => x.SupplierId);
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Shipment).WithMany().HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Items).WithOne(i => i.SupplierReturn).HasForeignKey(i => i.SupplierReturnId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SupplierReturnItem>(e =>
        {
            e.HasIndex(x => new { x.SupplierReturnId, x.ProductId });
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.InventoryBatch).WithMany().HasForeignKey(x => x.InventoryBatchId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
