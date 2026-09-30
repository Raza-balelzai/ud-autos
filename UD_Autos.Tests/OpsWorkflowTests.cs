using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UD_Autos.Authorization;
using UD_Autos.Data;
using UD_Autos.Extensions;
using UD_Autos.Models.Customers;
using UD_Autos.Models.Finance;
using UD_Autos.Models.Inventory;
using UD_Autos.Models.Products;
using UD_Autos.Models.Sales;
using UD_Autos.Models.Shipments;
using UD_Autos.Models.Suppliers;
using UD_Autos.Services;
using System.Security.Claims;

namespace UD_Autos.Tests;

public class OpsWorkflowTests
{
    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task LogAsync(string action, string entityType, string? entityId, string? referenceNumber = null, string? reason = null, object? oldValues = null, object? newValues = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubCodes : ICodeGeneratorService
    {
        private int _n;
        private string Next(string prefix) => $"{prefix}-{++_n:D4}";
        public Task<string> NextSkuAsync(CancellationToken cancellationToken = default) => Task.FromResult(Next("SKU"));
        public Task<string> NextCustomerCodeAsync(CancellationToken cancellationToken = default) => Task.FromResult(Next("CUS"));
        public Task<string> NextSupplierCodeAsync(CancellationToken cancellationToken = default) => Task.FromResult(Next("SUP"));
        public Task<string> NextPoNumberAsync(CancellationToken cancellationToken = default) => Task.FromResult(Next("PO"));
        public Task<string> NextHawalaNumberAsync(CancellationToken cancellationToken = default) => Task.FromResult(Next("HWL"));
        public Task<string> NextShipmentNumberAsync(CancellationToken cancellationToken = default) => Task.FromResult(Next("SHP"));
        public Task<string> NextSlipNumberAsync(int shipmentId, CancellationToken cancellationToken = default) => Task.FromResult(Next("SLIP"));
        public Task<string> NextInvoiceNumberAsync(CancellationToken cancellationToken = default) => Task.FromResult(Next("INV"));
        public Task<string> NextPaymentNumberAsync(CancellationToken cancellationToken = default) => Task.FromResult(Next("RCV"));
        public Task<string> NextReturnNumberAsync(CancellationToken cancellationToken = default) => Task.FromResult(Next("SRN"));
        public Task<string> NextSupplierReturnNumberAsync(CancellationToken cancellationToken = default) => Task.FromResult(Next("SPR"));
        public Task<string> NextExpenseNumberAsync(CancellationToken cancellationToken = default) => Task.FromResult(Next("EXP"));
        public Task<string> NextCashMovementNumberAsync(CancellationToken cancellationToken = default) => Task.FromResult(Next("CBM"));
    }

    private static (SalesService Sales, SalesReturnService Returns, PaymentAllocationService Payments, InventoryService Inventory, SupplierReturnService SupplierReturns, StubCodes Codes)
        CreateServices(ApplicationDbContext db)
    {
        var audit = new NoopAudit();
        var codes = new StubCodes();
        var cash = new CashBankService(db, audit, codes);
        var payments = new PaymentAllocationService(db, audit, codes, cash);
        var sales = new SalesService(db, audit, codes, payments, cash);
        var returns = new SalesReturnService(db, audit, codes, cash);
        var inventory = new InventoryService(db, audit);
        var supplierReturns = new SupplierReturnService(db, audit, codes);
        return (sales, returns, payments, inventory, supplierReturns, codes);
    }

    private static async Task<(Customer Customer, Product Product, InventoryBatch Batch)> SeedStockAsync(ApplicationDbContext db, decimal qty = 10m)
    {
        var customer = new Customer { CustomerCode = "C1", Name = "Shop A", IsActive = true };
        var supplier = new Supplier { SupplierCode = "S1", Name = "Factory", IsActive = true };
        var product = new Product
        {
            Sku = "P1",
            Name = "Part",
            WholesalePrice = 100,
            RetailPrice = 120,
            MinimumSalePrice = 80,
            CurrentStock = qty,
            AverageCost = 40,
            LatestLandedCost = 40,
            IsActive = true
        };
        db.Customers.Add(customer);
        db.Suppliers.Add(supplier);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var shipment = new Shipment
        {
            ShipmentNumber = "SHP-SEED-1",
            ShippingAgent = "Agent",
            Status = ShipmentStatus.Received,
            IsFinalized = true
        };
        db.Shipments.Add(shipment);
        await db.SaveChangesAsync();

        var item = new ShipmentItem
        {
            ShipmentId = shipment.Id,
            ProductId = product.Id,
            ExpectedQuantity = qty,
            ActualQuantity = qty,
            LandedUnitCostPkr = 40
        };
        db.ShipmentItems.Add(item);
        await db.SaveChangesAsync();

        var batch = new InventoryBatch
        {
            BatchNumber = "B1",
            ProductId = product.Id,
            ShipmentId = shipment.Id,
            ShipmentItemId = item.Id,
            ReceivedQuantity = qty,
            RemainingQuantity = qty,
            LandedUnitCostPkr = 40,
            ReceivedAt = DateTime.UtcNow.Date.AddDays(-5)
        };
        db.InventoryBatches.Add(batch);
        await db.SaveChangesAsync();
        return (customer, product, batch);
    }

    [Fact]
    public async Task CashSale_ReducesStock_AndPostsCash()
    {
        await using var db = CreateDb();
        var (customer, product, _) = await SeedStockAsync(db, 10);
        var svc = CreateServices(db);

        var result = await svc.Sales.CreateSaleAsync(new CreateSaleRequest
        {
            CustomerId = customer.Id,
            PaymentMode = PaymentMode.Cash,
            PaymentAccount = PaymentAccount.Cash,
            Items = { new SaleLineInput { ProductId = product.Id, Quantity = 2, UnitPrice = 100 } }
        }, "u1", true);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(8m, (await db.Products.FindAsync(product.Id))!.CurrentStock);
        Assert.Equal(0m, result.Invoice!.OutstandingAmount);
        Assert.Contains(db.CashBankMovements, m => m.Amount == 200m && m.Account == PaymentAccount.Cash);
    }

    [Fact]
    public async Task CreditSale_IncreasesReceivable_NoCash()
    {
        await using var db = CreateDb();
        var (customer, product, _) = await SeedStockAsync(db, 10);
        var svc = CreateServices(db);

        var result = await svc.Sales.CreateSaleAsync(new CreateSaleRequest
        {
            CustomerId = customer.Id,
            PaymentMode = PaymentMode.Credit,
            Items = { new SaleLineInput { ProductId = product.Id, Quantity = 3, UnitPrice = 100 } }
        }, "u1", true);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(300m, result.Invoice!.OutstandingAmount);
        Assert.Equal(300m, (await db.Customers.FindAsync(customer.Id))!.CurrentBalance);
        Assert.Empty(db.CashBankMovements.Where(m => m.ReferenceType == "Invoice"));
    }

    [Fact]
    public async Task PartialSale_SplitsCashAndOutstanding()
    {
        await using var db = CreateDb();
        var (customer, product, _) = await SeedStockAsync(db, 10);
        var svc = CreateServices(db);

        var result = await svc.Sales.CreateSaleAsync(new CreateSaleRequest
        {
            CustomerId = customer.Id,
            PaymentMode = PaymentMode.Partial,
            PaymentAccount = PaymentAccount.Bank,
            PaymentReceived = 40,
            Items = { new SaleLineInput { ProductId = product.Id, Quantity = 1, UnitPrice = 100 } }
        }, "u1", true);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(40m, result.Invoice!.PaymentReceived);
        Assert.Equal(60m, result.Invoice.OutstandingAmount);
        Assert.Equal(60m, (await db.Customers.FindAsync(customer.Id))!.CurrentBalance);
    }

    [Fact]
    public async Task Hold_BlocksSale_UntilReleased()
    {
        await using var db = CreateDb();
        var (customer, product, batch) = await SeedStockAsync(db, 5);
        var svc = CreateServices(db);

        var hold = await svc.Inventory.HoldStockAsync(product.Id, batch.Id, 5, "Customer hold", "u1");
        Assert.True(hold.Ok, hold.Error);

        var blocked = await svc.Sales.CreateSaleAsync(new CreateSaleRequest
        {
            CustomerId = customer.Id,
            PaymentMode = PaymentMode.Cash,
            PaymentAccount = PaymentAccount.Cash,
            Items = { new SaleLineInput { ProductId = product.Id, Quantity = 1, UnitPrice = 100 } }
        }, "u1", true);
        Assert.False(blocked.Ok);

        var release = await svc.Inventory.ReleaseStockAsync(product.Id, batch.Id, 5, "Released", "u1");
        Assert.True(release.Ok, release.Error);

        var ok = await svc.Sales.CreateSaleAsync(new CreateSaleRequest
        {
            CustomerId = customer.Id,
            PaymentMode = PaymentMode.Cash,
            PaymentAccount = PaymentAccount.Cash,
            Items = { new SaleLineInput { ProductId = product.Id, Quantity = 1, UnitPrice = 100 } }
        }, "u1", true);
        Assert.True(ok.Ok, ok.Error);
    }

    [Fact]
    public async Task SalesReturn_RestoresStock_AndAdjustsKhata()
    {
        await using var db = CreateDb();
        var (customer, product, _) = await SeedStockAsync(db, 10);
        var svc = CreateServices(db);

        var sale = await svc.Sales.CreateSaleAsync(new CreateSaleRequest
        {
            CustomerId = customer.Id,
            PaymentMode = PaymentMode.Credit,
            Items = { new SaleLineInput { ProductId = product.Id, Quantity = 2, UnitPrice = 100 } }
        }, "u1", true);
        Assert.True(sale.Ok, sale.Error);

        var invoice = await svc.Sales.GetInvoiceAsync(sale.Invoice!.Id);
        var line = invoice!.Items.First();

        var ret = await svc.Returns.CreateAsync(
            invoice.Id,
            DateTime.UtcNow.Date,
            "Damaged on delivery",
            adjustKhata: true,
            refundAccount: null,
            new[] { new ReturnLineInput { InvoiceItemId = line.Id, Quantity = 1 } },
            "u1");

        Assert.True(ret.Ok, ret.Error);
        Assert.Equal(9m, (await db.Products.FindAsync(product.Id))!.CurrentStock);
        Assert.Equal(100m, (await db.Customers.FindAsync(customer.Id))!.CurrentBalance);
    }

    [Fact]
    public async Task PaymentFifo_ClearsOldestOutstandingFirst()
    {
        await using var db = CreateDb();
        var (customer, product, _) = await SeedStockAsync(db, 20);
        var svc = CreateServices(db);

        var first = await svc.Sales.CreateSaleAsync(new CreateSaleRequest
        {
            CustomerId = customer.Id,
            InvoiceDate = DateTime.UtcNow.Date.AddDays(-10),
            PaymentMode = PaymentMode.Credit,
            AllowBelowMinimum = true,
            Items = { new SaleLineInput { ProductId = product.Id, Quantity = 1, UnitPrice = 50 } }
        }, "u1", true);
        Assert.True(first.Ok, first.Error);

        var second = await svc.Sales.CreateSaleAsync(new CreateSaleRequest
        {
            CustomerId = customer.Id,
            InvoiceDate = DateTime.UtcNow.Date,
            PaymentMode = PaymentMode.Credit,
            AllowBelowMinimum = true,
            Items = { new SaleLineInput { ProductId = product.Id, Quantity = 1, UnitPrice = 80 } }
        }, "u1", true);
        Assert.True(second.Ok, second.Error);

        var pay = await svc.Payments.ReceivePaymentAsync(
            customer.Id, DateTime.UtcNow.Date, 50m, PaymentAccount.Cash, null, null, null, "u1");
        Assert.True(pay.Ok, pay.Error);

        var inv1 = await db.Invoices.FindAsync(first.Invoice!.Id);
        var inv2 = await db.Invoices.FindAsync(second.Invoice!.Id);
        Assert.Equal(0m, inv1!.OutstandingAmount);
        Assert.Equal(80m, inv2!.OutstandingAmount);
        Assert.Equal(80m, (await db.Customers.FindAsync(customer.Id))!.CurrentBalance);
    }

    [Fact]
    public async Task SupplierReturn_ReducesBatchAndWritesMovement()
    {
        await using var db = CreateDb();
        var (_, product, batch) = await SeedStockAsync(db, 10);
        var supplier = await db.Suppliers.FirstAsync();
        var svc = CreateServices(db);

        var result = await svc.SupplierReturns.CreateAsync(
            supplier.Id,
            DateTime.UtcNow.Date,
            "Wrong goods",
            null,
            null,
            new[] { new SupplierReturnLineInput { InventoryBatchId = batch.Id, Quantity = 3 } },
            "u1");

        Assert.True(result.Ok, result.Error);
        Assert.Equal(7m, (await db.InventoryBatches.FindAsync(batch.Id))!.RemainingQuantity);
        Assert.Equal(7m, (await db.Products.FindAsync(product.Id))!.CurrentStock);
        Assert.Contains(db.StockMovements, m => m.MovementType == StockMovementType.SupplierReturn && m.Quantity == -3m);
    }

    [Fact]
    public async Task Salesman_CannotSatisfy_ManageSettings_Or_ViewProfit_Policies()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        services.AddAppAuthorization();
        await using var provider = services.BuildServiceProvider();
        var auth = provider.GetRequiredService<IAuthorizationService>();

        var salesman = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "sales"),
            new Claim(ClaimTypes.Role, AppRoles.Salesman)
        }, "test"));

        var settings = await auth.AuthorizeAsync(salesman, null, AppPolicies.ManageSettings);
        var profit = await auth.AuthorizeAsync(salesman, null, AppPolicies.ViewProfit);
        var sales = await auth.AuthorizeAsync(salesman, null, AppPolicies.ManageSales);

        Assert.False(settings.Succeeded);
        Assert.False(profit.Succeeded);
        Assert.True(sales.Succeeded);
    }

    [Fact]
    public void ReportService_ToExcel_ProducesWorkbookBytes()
    {
        var report = new ReportResult
        {
            Title = "Daily Sales",
            Headers = new[] { "Date", "Sales", "Collected" },
            Rows = new[]
            {
                new ReportRow { Col1 = "30 Sep", Amount1 = 100, Amount2 = 40 }
            }
        };

        // Use a throwaway context-backed service instance isn't needed for ToExcel
        var svc = new ReportService(CreateDb());
        var bytes = svc.ToExcel(report);
        Assert.True(bytes.Length > 100);
        // XLSX zip header PK
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }
}
