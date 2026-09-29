using Microsoft.EntityFrameworkCore;
using UD_Autos.Models.Hawala;
using UD_Autos.Models.Purchases;
using UD_Autos.Models.Shipments;

namespace UD_Autos.Data;

public static class Phase2ModelBuilderExtensions
{
    public static void ConfigurePhase2(this ModelBuilder builder)
    {
        builder.Entity<PurchaseOrder>(e =>
        {
            e.HasIndex(x => x.PoNumber).IsUnique();
            e.HasIndex(x => x.SupplierId);
            e.HasIndex(x => x.OrderDate);
            e.HasIndex(x => x.Status);
            e.HasOne(x => x.Supplier)
                .WithMany()
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PurchaseOrderItem>(e =>
        {
            e.HasIndex(x => new { x.PurchaseOrderId, x.ProductId });
            e.HasOne(x => x.PurchaseOrder)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<HawalaPayment>(e =>
        {
            e.HasIndex(x => x.PaymentNumber).IsUnique();
            e.HasIndex(x => x.PaymentDate);
            e.HasIndex(x => x.SupplierId);
            e.HasOne(x => x.Supplier)
                .WithMany()
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PurchaseOrder)
                .WithMany()
                .HasForeignKey(x => x.PurchaseOrderId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Shipment>(e =>
        {
            e.HasIndex(x => x.ShipmentNumber).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.ChinaDepartureDate);
        });

        builder.Entity<ChinaWarehouseReceivingSlip>(e =>
        {
            e.HasIndex(x => new { x.ShipmentId, x.SlipNumber }).IsUnique();
            e.HasOne(x => x.Shipment)
                .WithMany(x => x.ReceivingSlips)
                .HasForeignKey(x => x.ShipmentId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Supplier)
                .WithMany()
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ShipmentItem>(e =>
        {
            e.HasIndex(x => new { x.ShipmentId, x.ProductId });
            e.HasOne(x => x.Shipment)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.ShipmentId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PurchaseOrder)
                .WithMany()
                .HasForeignKey(x => x.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PurchaseOrderItem)
                .WithMany()
                .HasForeignKey(x => x.PurchaseOrderItemId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Supplier)
                .WithMany()
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ShipmentDocument>(e =>
        {
            e.HasIndex(x => x.ShipmentId);
            e.HasOne(x => x.Shipment)
                .WithMany(x => x.Documents)
                .HasForeignKey(x => x.ShipmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ShipmentTimelineEvent>(e =>
        {
            e.HasIndex(x => new { x.ShipmentId, x.OccurredAt });
            e.HasOne(x => x.Shipment)
                .WithMany(x => x.TimelineEvents)
                .HasForeignKey(x => x.ShipmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
