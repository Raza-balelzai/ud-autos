using Microsoft.EntityFrameworkCore;
using UD_Autos.Models.Products;
using UD_Autos.Models.Sales;

namespace UD_Autos.Data;

public static class Phase9ModelBuilderExtensions
{
    public static void ConfigurePhase9(this ModelBuilder builder)
    {
        builder.Entity<Product>(e =>
        {
            e.Property(x => x.ProductKind).HasDefaultValue(ProductKind.SingleItem);
            e.HasIndex(x => x.ProductKind);
        });

        builder.Entity<ProductSetComponent>(e =>
        {
            e.Property(x => x.RequiredQuantity).HasPrecision(18, 4);
            e.HasIndex(x => new { x.SetProductId, x.ComponentProductId })
                .IsUnique()
                .HasFilter("[IsActive] = 1");
            e.HasIndex(x => x.ComponentProductId);
            e.HasOne(x => x.SetProduct)
                .WithMany(x => x.SetComponents)
                .HasForeignKey(x => x.SetProductId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ComponentProduct)
                .WithMany(x => x.UsedInSets)
                .HasForeignKey(x => x.ComponentProductId)
                .OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_ProductSetComponents_RequiredQuantity", "[RequiredQuantity] > 0");
                t.HasCheckConstraint("CK_ProductSetComponents_NotSelf", "[SetProductId] <> [ComponentProductId]");
            });
        });

        builder.Entity<InvoiceSetComponent>(e =>
        {
            e.Property(x => x.RequiredQuantityPerSet).HasPrecision(18, 4);
            e.Property(x => x.QuantityConsumed).HasPrecision(18, 4);
            e.Property(x => x.ReturnedQuantity).HasPrecision(18, 4);
            e.Property(x => x.UnitCogsPkr).HasPrecision(18, 4);
            e.Property(x => x.LineCogsPkr).HasPrecision(18, 2);
            e.HasIndex(x => new { x.InvoiceItemId, x.ComponentProductId });
            e.HasOne(x => x.InvoiceItem)
                .WithMany(x => x.SetComponents)
                .HasForeignKey(x => x.InvoiceItemId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ComponentProduct)
                .WithMany()
                .HasForeignKey(x => x.ComponentProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<InvoiceSetComponentBatchAllocation>(e =>
        {
            e.Property(x => x.Quantity).HasPrecision(18, 4);
            e.Property(x => x.ReturnedQuantity).HasPrecision(18, 4);
            e.Property(x => x.UnitCostPkr).HasPrecision(18, 4);
            e.Property(x => x.LineCostPkr).HasPrecision(18, 2);
            e.HasIndex(x => new { x.InvoiceSetComponentId, x.InventoryBatchId });
            e.HasOne(x => x.InvoiceSetComponent)
                .WithMany(x => x.BatchAllocations)
                .HasForeignKey(x => x.InvoiceSetComponentId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.InventoryBatch)
                .WithMany()
                .HasForeignKey(x => x.InventoryBatchId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
