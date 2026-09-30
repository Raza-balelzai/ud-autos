using Microsoft.EntityFrameworkCore;
using UD_Autos.Data;
using UD_Autos.Models.Inventory;
using UD_Autos.Models.Products;
using UD_Autos.Models.Shipments;
using UD_Autos.Services;

namespace UD_Autos.Tests;

public class ShipmentFinalizationTests
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
        public Task<string> NextSkuAsync(CancellationToken cancellationToken = default) => Task.FromResult("SKU-1");
        public Task<string> NextCustomerCodeAsync(CancellationToken cancellationToken = default) => Task.FromResult("CUS-1");
        public Task<string> NextSupplierCodeAsync(CancellationToken cancellationToken = default) => Task.FromResult("SUP-1");
        public Task<string> NextPoNumberAsync(CancellationToken cancellationToken = default) => Task.FromResult("PO-1");
        public Task<string> NextHawalaNumberAsync(CancellationToken cancellationToken = default) => Task.FromResult("HWL-1");
        public Task<string> NextShipmentNumberAsync(CancellationToken cancellationToken = default) => Task.FromResult("SHP-1");
        public Task<string> NextSlipNumberAsync(int shipmentId, CancellationToken cancellationToken = default) => Task.FromResult("SLIP-1");
        public Task<string> NextInvoiceNumberAsync(CancellationToken cancellationToken = default) => Task.FromResult("INV-1");
        public Task<string> NextPaymentNumberAsync(CancellationToken cancellationToken = default) => Task.FromResult("RCV-1");
        public Task<string> NextReturnNumberAsync(CancellationToken cancellationToken = default) => Task.FromResult("SRN-1");
        public Task<string> NextSupplierReturnNumberAsync(CancellationToken cancellationToken = default) => Task.FromResult("SPR-1");
        public Task<string> NextExpenseNumberAsync(CancellationToken cancellationToken = default) => Task.FromResult("EXP-1");
        public Task<string> NextCashMovementNumberAsync(CancellationToken cancellationToken = default) => Task.FromResult("CBM-1");
    }

    [Fact]
    public async Task Finalize_BlocksDoubleFinalization()
    {
        await using var db = CreateDb();
        var product = new Product { Sku = "P1", Name = "Part", WholesalePrice = 10, RetailPrice = 12, CurrentStock = 0 };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var shipment = new Shipment
        {
            ShipmentNumber = "SHP-TEST-1",
            ShippingAgent = "Agent",
            Status = ShipmentStatus.Tarnol,
            FreightAmountPkr = 100,
            TotalDirectCostPkr = 500,
            ChinaProductCostPkr = 400,
            LandedCostCalculatedAt = DateTime.UtcNow,
            Items =
            {
                new ShipmentItem
                {
                    ProductId = product.Id,
                    ExpectedQuantity = 10,
                    ActualQuantity = 10,
                    LandedUnitCostPkr = 50,
                    UnitPriceRmb = 1
                }
            }
        };
        db.Shipments.Add(shipment);
        await db.SaveChangesAsync();

        var svc = new ShipmentFinalizationService(db, new NoopAudit(), new StubCodes());
        var first = await svc.FinalizeAndReceiveAsync(shipment.Id, "test");
        Assert.True(first.Ok, first.Error);

        var second = await svc.FinalizeAndReceiveAsync(shipment.Id, "test");
        Assert.False(second.Ok);
        Assert.Contains("already finalized", second.Error!, StringComparison.OrdinalIgnoreCase);
    }
}
