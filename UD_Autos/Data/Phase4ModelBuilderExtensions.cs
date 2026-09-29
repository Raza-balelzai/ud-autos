using Microsoft.EntityFrameworkCore;
using UD_Autos.Models.Sales;

namespace UD_Autos.Data;

public static class Phase4ModelBuilderExtensions
{
    public static void ConfigurePhase4(this ModelBuilder builder)
    {
        builder.Entity<Invoice>(e =>
        {
            e.HasIndex(x => x.InvoiceNumber).IsUnique();
            e.HasIndex(x => new { x.CustomerId, x.InvoiceDate });
            e.HasIndex(x => x.OutstandingAmount);
            e.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<InvoiceItem>(e =>
        {
            e.HasIndex(x => new { x.InvoiceId, x.ProductId });
            e.HasOne(x => x.Invoice).WithMany(x => x.Items).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<InvoiceItemBatchAllocation>(e =>
        {
            e.HasIndex(x => new { x.InvoiceItemId, x.InventoryBatchId });
            e.HasOne(x => x.InvoiceItem).WithMany(x => x.BatchAllocations).HasForeignKey(x => x.InvoiceItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.InventoryBatch).WithMany().HasForeignKey(x => x.InventoryBatchId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CustomerLedgerEntry>(e =>
        {
            e.HasIndex(x => new { x.CustomerId, x.EntryDate, x.Id });
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CustomerPayment>(e =>
        {
            e.HasIndex(x => x.PaymentNumber).IsUnique();
            e.HasIndex(x => new { x.CustomerId, x.PaymentDate });
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.SourceInvoice).WithMany().HasForeignKey(x => x.SourceInvoiceId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PaymentAllocation>(e =>
        {
            e.HasIndex(x => new { x.CustomerPaymentId, x.InvoiceId }).IsUnique();
            e.HasOne(x => x.CustomerPayment).WithMany(x => x.Allocations).HasForeignKey(x => x.CustomerPaymentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Invoice).WithMany(x => x.Allocations).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SalesReturn>(e =>
        {
            e.HasIndex(x => x.ReturnNumber).IsUnique();
            e.HasIndex(x => x.InvoiceId);
            e.HasOne(x => x.Invoice).WithMany(x => x.Returns).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SalesReturnItem>(e =>
        {
            e.HasOne(x => x.SalesReturn).WithMany(x => x.Items).HasForeignKey(x => x.SalesReturnId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.InvoiceItem).WithMany().HasForeignKey(x => x.InvoiceItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
