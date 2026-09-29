using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UD_Autos.Models.Audit;
using UD_Autos.Models.Customers;
using UD_Autos.Models.Finance;
using UD_Autos.Models.Hawala;
using UD_Autos.Models.Identity;
using UD_Autos.Models.Inventory;
using UD_Autos.Models.Products;
using UD_Autos.Models.Purchases;
using UD_Autos.Models.Sales;
using UD_Autos.Models.Settings;
using UD_Autos.Models.Shipments;
using UD_Autos.Models.Suppliers;

namespace UD_Autos.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<BusinessSetting> BusinessSettings => Set<BusinessSetting>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductRmbPriceHistory> ProductRmbPriceHistories => Set<ProductRmbPriceHistory>();
    public DbSet<ProductLandedCostHistory> ProductLandedCostHistories => Set<ProductLandedCostHistory>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<HawalaPayment> HawalaPayments => Set<HawalaPayment>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ChinaWarehouseReceivingSlip> ChinaWarehouseReceivingSlips => Set<ChinaWarehouseReceivingSlip>();
    public DbSet<ShipmentItem> ShipmentItems => Set<ShipmentItem>();
    public DbSet<ShipmentDocument> ShipmentDocuments => Set<ShipmentDocument>();
    public DbSet<ShipmentTimelineEvent> ShipmentTimelineEvents => Set<ShipmentTimelineEvent>();
    public DbSet<InventoryBatch> InventoryBatches => Set<InventoryBatch>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<InvoiceItemBatchAllocation> InvoiceItemBatchAllocations => Set<InvoiceItemBatchAllocation>();
    public DbSet<CustomerLedgerEntry> CustomerLedgerEntries => Set<CustomerLedgerEntry>();
    public DbSet<CustomerPayment> CustomerPayments => Set<CustomerPayment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
    public DbSet<SalesReturn> SalesReturns => Set<SalesReturn>();
    public DbSet<SalesReturnItem> SalesReturnItems => Set<SalesReturnItem>();
    public DbSet<CashBankOpening> CashBankOpenings => Set<CashBankOpening>();
    public DbSet<CashBankMovement> CashBankMovements => Set<CashBankMovement>();
    public DbSet<OperatingExpense> OperatingExpenses => Set<OperatingExpense>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ConfigurePhase2();
        builder.ConfigurePhase3();
        builder.ConfigurePhase4();
        builder.ConfigurePhase5();
        builder.ConfigurePhase6();

        builder.Entity<ProductCategory>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
        });

        builder.Entity<UnitOfMeasure>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
            e.HasIndex(x => x.Abbreviation).IsUnique();
        });

        builder.Entity<ExpenseCategory>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
        });

        builder.Entity<BusinessSetting>(e =>
        {
            e.HasIndex(x => x.Key).IsUnique();
        });

        builder.Entity<Supplier>(e =>
        {
            e.HasIndex(x => x.SupplierCode).IsUnique();
            e.HasIndex(x => x.Name);
            e.Property(x => x.TotalPurchasesRmb).HasPrecision(18, 2);
            e.Property(x => x.TotalPaymentsPkr).HasPrecision(18, 2);
            e.Property(x => x.OutstandingBalancePkr).HasPrecision(18, 2);
        });

        builder.Entity<Customer>(e =>
        {
            e.HasIndex(x => x.CustomerCode).IsUnique();
            e.HasIndex(x => x.Phone);
            e.HasIndex(x => x.Name);
            e.Property(x => x.CreditLimit).HasPrecision(18, 2);
            e.Property(x => x.OpeningBalance).HasPrecision(18, 2);
            e.Property(x => x.CurrentBalance).HasPrecision(18, 2);
            e.Property(x => x.TotalPurchases).HasPrecision(18, 2);
            e.Property(x => x.TotalPayments).HasPrecision(18, 2);
        });

        builder.Entity<Product>(e =>
        {
            e.HasIndex(x => x.Sku).IsUnique();
            e.HasIndex(x => x.Name);
            e.HasIndex(x => x.CategoryId);
            e.HasIndex(x => x.DefaultSupplierId);

            e.HasOne(x => x.Category)
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(x => x.Unit)
                .WithMany()
                .HasForeignKey(x => x.UnitId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(x => x.DefaultSupplier)
                .WithMany(x => x.Products)
                .HasForeignKey(x => x.DefaultSupplierId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ProductRmbPriceHistory>(e =>
        {
            e.HasIndex(x => new { x.ProductId, x.EffectiveDate });
            e.HasOne(x => x.Product)
                .WithMany(x => x.RmbPriceHistory)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Supplier)
                .WithMany()
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ProductLandedCostHistory>(e =>
        {
            e.HasIndex(x => new { x.ProductId, x.EffectiveDate });
            e.HasOne(x => x.Product)
                .WithMany(x => x.LandedCostHistory)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AuditLog>(e =>
        {
            e.HasIndex(x => x.TimestampUtc);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
        });
    }
}
