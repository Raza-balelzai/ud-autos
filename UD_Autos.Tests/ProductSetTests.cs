using Microsoft.EntityFrameworkCore;
using UD_Autos.Data;
using UD_Autos.Models.Customers;
using UD_Autos.Models.Inventory;
using UD_Autos.Models.Products;
using UD_Autos.Models.Sales;
using UD_Autos.Services;

namespace UD_Autos.Tests;

public class ProductSetTests
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

    private static (ProductService Products, SalesService Sales, SalesReturnService Returns) CreateServices(ApplicationDbContext db)
    {
        var audit = new NoopAudit();
        var codes = new StubCodes();
        var cash = new CashBankService(db, audit, codes);
        var payments = new PaymentAllocationService(db, audit, codes, cash);
        return (
            new ProductService(db, audit, codes),
            new SalesService(db, audit, codes, payments, cash),
            new SalesReturnService(db, audit, codes, cash));
    }

    private static async Task<Customer> SeedCustomerAsync(ApplicationDbContext db)
    {
        var customer = new Customer { CustomerCode = "C1", Name = "Shop A", IsActive = true };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer;
    }

    private static async Task<Product> SeedPartAsync(ApplicationDbContext db, string sku, string name, decimal stock)
    {
        var product = new Product
        {
            Sku = sku,
            Name = name,
            ProductKind = ProductKind.SingleItem,
            WholesalePrice = 50,
            RetailPrice = 60,
            CurrentStock = stock,
            AverageCost = 10,
            LatestLandedCost = 10,
            IsActive = true
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    private static CreateSaleRequest CreditSale(int customerId, params SaleLineInput[] lines) => new()
    {
        CustomerId = customerId,
        PaymentMode = PaymentMode.Credit,
        Items = lines.ToList()
    };

    [Fact]
    public void AvailableSets_UsesMinimumWholeSets()
    {
        var available = ProductSetPlanner.CalculateAvailableSets(new[]
        {
            (Available: 10m, Required: 1m),
            (Available: 8m, Required: 1m),
            (Available: 40m, Required: 4m)
        });

        Assert.Equal(8m, available);
    }

    [Fact]
    public void LockSql_UsesUpdateLockOnSqlServer()
    {
        var sql = ProductSetPlanner.BuildProductLockSql(new[] { 3, 1, 3 });
        Assert.Contains(ProductSetPlanner.SqlServerProductLockHint, sql);
        Assert.Contains("UPDLOCK", sql);
        Assert.Contains("1,3", sql);
    }

    [Fact]
    public void AggregateDemand_AddsSetAndDirectComponentTogether()
    {
        var demand = ProductSetPlanner.AggregateDemand(new[]
        {
            new SaleDemandLine
            {
                ProductId = 10,
                IsSet = true,
                Quantity = 1,
                Components = { (ComponentId: 2, PerSet: 1m) }
            },
            new SaleDemandLine { ProductId = 2, Quantity = 1 }
        });

        Assert.Equal(2m, demand[2]);
    }

    [Fact]
    public void Validate_RejectsDuplicateSelfAndNestedSet()
    {
        var kinds = new Dictionary<int, ProductKind>
        {
            [1] = ProductKind.SingleItem,
            [2] = ProductKind.CompleteSet
        };
        var active = new HashSet<int> { 1, 2, 9 };

        Assert.Equal("Each item can appear only once in a set.", ProductSetPlanner.ValidateComposition(9, new[]
        {
            new SetComponentInput { ComponentProductId = 1, RequiredQuantity = 1 },
            new SetComponentInput { ComponentProductId = 1, RequiredQuantity = 2 }
        }, kinds, active));

        Assert.Equal("A set cannot contain itself.", ProductSetPlanner.ValidateComposition(9, new[]
        {
            new SetComponentInput { ComponentProductId = 9, RequiredQuantity = 1 }
        }, kinds, active));

        Assert.Equal("A complete set can only contain single items.", ProductSetPlanner.ValidateComposition(9, new[]
        {
            new SetComponentInput { ComponentProductId = 2, RequiredQuantity = 1 }
        }, kinds, active));
    }

    [Fact]
    public async Task CreateSet_RequiresNameAndOneComponent()
    {
        await using var db = CreateDb();
        var svc = CreateServices(db);
        var bumper = await SeedPartAsync(db, "BMP", "Main Bumper", 10);

        var missing = await svc.Products.CreateAsync(new Product
        {
            Name = "Complete Truck Bumper Set",
            ProductKind = ProductKind.CompleteSet,
            WholesalePrice = 1000
        }, "u1", components: Array.Empty<SetComponentInput>());
        Assert.False(missing.Ok);

        var created = await svc.Products.CreateAsync(new Product
        {
            Name = "Complete Truck Bumper Set",
            ProductKind = ProductKind.CompleteSet,
            WholesalePrice = 1000
        }, "u1", components: new[]
        {
            new SetComponentInput { ComponentProductId = bumper.Id, RequiredQuantity = 1 }
        });

        Assert.True(created.Ok, created.Error);
        Assert.Equal(ProductKind.CompleteSet, created.Product!.ProductKind);
        Assert.Equal(0m, created.Product.CurrentStock);
        Assert.Equal(1, await db.ProductSetComponents.CountAsync(c => c.SetProductId == created.Product.Id && c.IsActive));
    }

    [Fact]
    public async Task EditSet_ChangesComposition_WithoutTouchingSoldSnapshot()
    {
        await using var db = CreateDb();
        var svc = CreateServices(db);
        var customer = await SeedCustomerAsync(db);
        var bumper = await SeedPartAsync(db, "BMP", "Main Bumper", 10);
        var bolt = await SeedPartAsync(db, "BLT", "Bolt", 40);
        var created = await svc.Products.CreateAsync(new Product
        {
            Name = "Complete Truck Bumper Set",
            ProductKind = ProductKind.CompleteSet,
            WholesalePrice = 500
        }, "u1", components: new[]
        {
            new SetComponentInput { ComponentProductId = bumper.Id, RequiredQuantity = 1 },
            new SetComponentInput { ComponentProductId = bolt.Id, RequiredQuantity = 4 }
        });
        Assert.True(created.Ok, created.Error);

        var sale = await svc.Sales.CreateSaleAsync(CreditSale(customer.Id,
            new SaleLineInput { ProductId = created.Product!.Id, Quantity = 1, UnitPrice = 500 }), "u1", true);
        Assert.True(sale.Ok, sale.Error);

        var edited = await svc.Products.UpdateAsync(new Product
        {
            Id = created.Product.Id,
            Sku = created.Product.Sku,
            Name = created.Product.Name,
            ProductKind = ProductKind.CompleteSet,
            WholesalePrice = 500,
            IsActive = true
        }, "u1", components: new[]
        {
            new SetComponentInput { ComponentProductId = bumper.Id, RequiredQuantity = 2 }
        });
        Assert.True(edited.Ok, edited.Error);

        var snapshot = await db.InvoiceSetComponents.AsNoTracking().ToListAsync();
        Assert.Equal(2, snapshot.Count);
        Assert.Contains(snapshot, r => r.ComponentProductId == bolt.Id && r.RequiredQuantityPerSet == 4 && r.QuantityConsumed == 4);
        Assert.Contains(snapshot, r => r.ComponentProductId == bumper.Id && r.RequiredQuantityPerSet == 1 && r.QuantityConsumed == 1);
        Assert.DoesNotContain(await db.ProductSetComponents.Where(c => c.SetProductId == created.Product.Id && c.IsActive).Select(c => c.ComponentProductId).ToListAsync(), id => id == bolt.Id);
    }

    [Fact]
    public async Task CreateSet_RejectsInactiveAndDuplicateComponents()
    {
        await using var db = CreateDb();
        var svc = CreateServices(db);
        var part = await SeedPartAsync(db, "P1", "Left Bracket", 5);
        part.IsActive = false;
        await db.SaveChangesAsync();

        var inactive = await svc.Products.CreateAsync(new Product
        {
            Name = "Set",
            ProductKind = ProductKind.CompleteSet
        }, "u1", components: new[] { new SetComponentInput { ComponentProductId = part.Id, RequiredQuantity = 1 } });
        Assert.False(inactive.Ok);
        Assert.Contains("not available", inactive.Error);

        part.IsActive = true;
        await db.SaveChangesAsync();
        var duplicate = await svc.Products.CreateAsync(new Product
        {
            Name = "Set",
            ProductKind = ProductKind.CompleteSet
        }, "u1", components: new[]
        {
            new SetComponentInput { ComponentProductId = part.Id, RequiredQuantity = 1 },
            new SetComponentInput { ComponentProductId = part.Id, RequiredQuantity = 2 }
        });
        Assert.False(duplicate.Ok);
        Assert.Contains("only once", duplicate.Error);
    }

    [Fact]
    public async Task SellOneSet_DeductsComponents_AndPostsOneLedgerLine()
    {
        await using var db = CreateDb();
        var (customer, set, bumper, bolt) = await SeedBumperSetAsync(db);
        var svc = CreateServices(db);

        var sale = await svc.Sales.CreateSaleAsync(CreditSale(customer.Id,
            new SaleLineInput { ProductId = set.Id, Quantity = 1, UnitPrice = 800 }), "u1", true);

        Assert.True(sale.Ok, sale.Error);
        Assert.Single(sale.Invoice!.Items);
        Assert.Equal(set.Id, sale.Invoice.Items.Single().ProductId);
        Assert.Equal(800m, sale.Invoice.BillTotal);
        Assert.Equal(9m, (await db.Products.FindAsync(bumper.Id))!.CurrentStock);
        Assert.Equal(36m, (await db.Products.FindAsync(bolt.Id))!.CurrentStock);
        Assert.Equal(0m, (await db.Products.FindAsync(set.Id))!.CurrentStock);
        Assert.Equal(2, await db.StockMovements.CountAsync(m => m.MovementType == StockMovementType.SetConsumption));
        Assert.Equal(0, await db.StockMovements.CountAsync(m => m.MovementType == StockMovementType.Sale));
        var ledger = await db.CustomerLedgerEntries.SingleAsync();
        Assert.Equal(800m, ledger.Debit);
        Assert.Equal(800m, (await db.Customers.FindAsync(customer.Id))!.CurrentBalance);
    }

    [Fact]
    public async Task SellMultipleSets_MultipliesComponentQuantities()
    {
        await using var db = CreateDb();
        var (customer, set, bumper, bolt) = await SeedBumperSetAsync(db);
        var svc = CreateServices(db);

        var sale = await svc.Sales.CreateSaleAsync(CreditSale(customer.Id,
            new SaleLineInput { ProductId = set.Id, Quantity = 2, UnitPrice = 800 }), "u1", true);

        Assert.True(sale.Ok, sale.Error);
        Assert.Equal(8m, (await db.Products.FindAsync(bumper.Id))!.CurrentStock);
        Assert.Equal(32m, (await db.Products.FindAsync(bolt.Id))!.CurrentStock);
        Assert.Equal(1600m, sale.Invoice!.BillTotal);
        Assert.Contains(sale.Invoice.Items.Single().SetComponents, c => c.ComponentProductId == bolt.Id && c.QuantityConsumed == 8);
    }

    [Fact]
    public async Task SellIndividualComponent_LeavesSetDefinitionAndRecalculatesAvailability()
    {
        await using var db = CreateDb();
        var (_, set, bumper, _) = await SeedBumperSetAsync(db);
        var customer = await db.Customers.FirstAsync();
        var svc = CreateServices(db);

        var sale = await svc.Sales.CreateSaleAsync(CreditSale(customer.Id,
            new SaleLineInput { ProductId = bumper.Id, Quantity = 1, UnitPrice = 50 }), "u1", true);
        Assert.True(sale.Ok, sale.Error);

        var definition = await db.ProductSetComponents.Where(c => c.SetProductId == set.Id && c.IsActive).ToListAsync();
        Assert.Equal(2, definition.Count);
        Assert.Equal(StockMovementType.Sale, (await db.StockMovements.SingleAsync()).MovementType);

        var loaded = (await svc.Products.SearchAsync(set.Name, null, "name", false, 1, 20)).Items.Single();
        Assert.Equal(9m, loaded.VirtualAvailableSets);
    }

    [Fact]
    public async Task MixedSale_AggregatesSetAndComponentBeforeDeducting()
    {
        await using var db = CreateDb();
        var (customer, set, bumper, bolt) = await SeedBumperSetAsync(db);
        var svc = CreateServices(db);

        var sale = await svc.Sales.CreateSaleAsync(CreditSale(customer.Id,
            new SaleLineInput { ProductId = set.Id, Quantity = 1, UnitPrice = 800 },
            new SaleLineInput { ProductId = bumper.Id, Quantity = 1, UnitPrice = 50 }), "u1", true);

        Assert.True(sale.Ok, sale.Error);
        Assert.Equal(2, sale.Invoice!.Items.Count);
        Assert.Equal(850m, sale.Invoice.BillTotal);
        Assert.Equal(8m, (await db.Products.FindAsync(bumper.Id))!.CurrentStock);
        Assert.Equal(36m, (await db.Products.FindAsync(bolt.Id))!.CurrentStock);
        Assert.Equal(850m, (await db.Customers.FindAsync(customer.Id))!.CurrentBalance);
    }

    [Fact]
    public async Task InsufficientComponent_RollsBackAndNamesTheItem()
    {
        await using var db = CreateDb();
        var (customer, set, bumper, bolt) = await SeedBumperSetAsync(db);
        bolt.CurrentStock = 3;
        await db.SaveChangesAsync();
        var svc = CreateServices(db);

        var sale = await svc.Sales.CreateSaleAsync(CreditSale(customer.Id,
            new SaleLineInput { ProductId = set.Id, Quantity = 1, UnitPrice = 800 }), "u1", true);

        Assert.False(sale.Ok);
        Assert.Contains("Not enough stock: Bolt", sale.Error);
        Assert.Equal(10m, (await db.Products.FindAsync(bumper.Id))!.CurrentStock);
        Assert.Equal(3m, (await db.Products.FindAsync(bolt.Id))!.CurrentStock);
        Assert.Equal(0, await db.Invoices.CountAsync());
    }

    [Fact]
    public async Task SecondSale_FailsWhenFirstConsumedTheStock()
    {
        await using var db = CreateDb();
        var (customer, set, _, bolt) = await SeedBumperSetAsync(db);
        bolt.CurrentStock = 4;
        await db.SaveChangesAsync();
        var svc = CreateServices(db);
        var request = CreditSale(customer.Id, new SaleLineInput { ProductId = set.Id, Quantity = 1, UnitPrice = 800 });

        var first = await svc.Sales.CreateSaleAsync(request, "u1", true);
        var second = await svc.Sales.CreateSaleAsync(request, "u1", true);

        Assert.True(first.Ok, first.Error);
        Assert.False(second.Ok);
        Assert.Contains("Not enough stock", second.Error);
        Assert.Equal(0m, (await db.Products.FindAsync(bolt.Id))!.CurrentStock);
        Assert.Equal(1, await db.Invoices.CountAsync());
    }

    [Fact]
    public async Task VoidSetSale_RestoresSnapshotComponents_NotTheSetRow()
    {
        await using var db = CreateDb();
        var (customer, set, bumper, bolt) = await SeedBumperSetAsync(db);
        var svc = CreateServices(db);
        var sale = await svc.Sales.CreateSaleAsync(CreditSale(customer.Id,
            new SaleLineInput { ProductId = set.Id, Quantity = 2, UnitPrice = 800 }), "u1", true);
        Assert.True(sale.Ok, sale.Error);

        var voided = await svc.Sales.VoidInvoiceAsync(sale.Invoice!.Id, "Customer cancelled order", "u1");
        Assert.True(voided.Ok, voided.Error);
        Assert.Equal(10m, (await db.Products.FindAsync(bumper.Id))!.CurrentStock);
        Assert.Equal(40m, (await db.Products.FindAsync(bolt.Id))!.CurrentStock);
        Assert.Equal(0m, (await db.Products.FindAsync(set.Id))!.CurrentStock);
        Assert.Equal(0m, (await db.Customers.FindAsync(customer.Id))!.CurrentBalance);

        var again = await svc.Sales.VoidInvoiceAsync(sale.Invoice.Id, "Customer cancelled order", "u1");
        Assert.False(again.Ok);
    }

    [Fact]
    public async Task ReturnSet_RestoresOnlyTheReturnedSets()
    {
        await using var db = CreateDb();
        var (customer, set, bumper, bolt) = await SeedBumperSetAsync(db);
        var svc = CreateServices(db);
        var sale = await svc.Sales.CreateSaleAsync(CreditSale(customer.Id,
            new SaleLineInput { ProductId = set.Id, Quantity = 2, UnitPrice = 800 }), "u1", true);
        Assert.True(sale.Ok, sale.Error);
        var itemId = sale.Invoice!.Items.Single().Id;

        var returned = await svc.Returns.CreateAsync(sale.Invoice.Id, DateTime.UtcNow.Date, "One set back", true, null,
            new[] { new ReturnLineInput { InvoiceItemId = itemId, Quantity = 1 } }, "u1");
        Assert.True(returned.Ok, returned.Error);
        Assert.Equal(9m, (await db.Products.FindAsync(bumper.Id))!.CurrentStock);
        Assert.Equal(36m, (await db.Products.FindAsync(bolt.Id))!.CurrentStock);
        Assert.Equal(0m, (await db.Products.FindAsync(set.Id))!.CurrentStock);

        var duplicate = await svc.Returns.CreateAsync(sale.Invoice.Id, DateTime.UtcNow.Date, "One set back again", true, null,
            new[] { new ReturnLineInput { InvoiceItemId = itemId, Quantity = 2 } }, "u1");
        Assert.False(duplicate.Ok);
        Assert.Equal(9m, (await db.Products.FindAsync(bumper.Id))!.CurrentStock);
    }

    [Fact]
    public async Task SoftDelete_HidesSetAndBlocksItAsANewComponent()
    {
        await using var db = CreateDb();
        var svc = CreateServices(db);
        var part = await SeedPartAsync(db, "P1", "Pad", 4);
        var created = await svc.Products.CreateAsync(new Product
        {
            Name = "Pad Set",
            ProductKind = ProductKind.CompleteSet,
            WholesalePrice = 10
        }, "u1", components: new[] { new SetComponentInput { ComponentProductId = part.Id, RequiredQuantity = 1 } });
        Assert.True(created.Ok, created.Error);

        var deleted = await svc.Products.SoftDeleteAsync(created.Product!.Id, "u1");
        Assert.True(deleted.Ok, deleted.Error);
        var search = await svc.Products.SearchAsync("Pad Set", null, "name", false, 1, 20);
        Assert.DoesNotContain(search.Items, p => p.Id == created.Product.Id);

        var removedPart = await svc.Products.SoftDeleteAsync(part.Id, "u1");
        Assert.True(removedPart.Ok, removedPart.Error);
        var blocked = await svc.Products.CreateAsync(new Product
        {
            Name = "Another Set",
            ProductKind = ProductKind.CompleteSet
        }, "u1", components: new[] { new SetComponentInput { ComponentProductId = part.Id, RequiredQuantity = 1 } });
        Assert.False(blocked.Ok);
    }

    [Fact]
    public async Task NormalProductSale_StillDeductsOnlyThatProduct()
    {
        await using var db = CreateDb();
        var customer = await SeedCustomerAsync(db);
        var part = await SeedPartAsync(db, "BRK", "Brake Pad", 10);
        var svc = CreateServices(db);

        var sale = await svc.Sales.CreateSaleAsync(CreditSale(customer.Id,
            new SaleLineInput { ProductId = part.Id, Quantity = 2, UnitPrice = 100 }), "u1", true);

        Assert.True(sale.Ok, sale.Error);
        Assert.Equal(8m, (await db.Products.FindAsync(part.Id))!.CurrentStock);
        Assert.Equal(200m, sale.Invoice!.BillTotal);
        Assert.Equal(200m, (await db.Customers.FindAsync(customer.Id))!.CurrentBalance);
        Assert.Equal(StockMovementType.Sale, (await db.StockMovements.SingleAsync()).MovementType);
        Assert.Empty(sale.Invoice.Items.Single().SetComponents);
    }

    [Fact]
    public async Task SetsToAdd_IncreasesItemStockOnlyWhenEntered()
    {
        await using var db = CreateDb();
        var svc = CreateServices(db);
        var bumper = await SeedPartAsync(db, "BMP", "Main Bumper", 10);
        var components = new[] { new SetComponentInput { ComponentProductId = bumper.Id, RequiredQuantity = 2 } };

        var created = await svc.Products.CreateAsync(new Product
        {
            Name = "Bumper Set",
            ProductKind = ProductKind.CompleteSet,
            WholesalePrice = 100
        }, "u1", components: components, setsToAdd: 2);
        Assert.True(created.Ok, created.Error);
        Assert.Equal(14m, (await db.Products.FindAsync(bumper.Id))!.CurrentStock);
        Assert.Equal(4m, await db.StockMovements.Where(m => m.ProductId == bumper.Id && m.Reason == "Added from complete set").Select(m => m.Quantity).SingleAsync());

        var unchanged = await svc.Products.UpdateAsync(new Product
        {
            Id = created.Product!.Id,
            Sku = created.Product.Sku,
            Name = created.Product.Name,
            ProductKind = ProductKind.CompleteSet,
            WholesalePrice = 100,
            IsActive = true
        }, "u1", components: components, setsToAdd: 0);
        Assert.True(unchanged.Ok, unchanged.Error);
        Assert.Equal(14m, (await db.Products.FindAsync(bumper.Id))!.CurrentStock);
    }

    [Fact]
    public async Task ShortSet_KeepsItsOwnStock_AndAddsQuantityOnTheSameName()
    {
        await using var db = CreateDb();
        var svc = CreateServices(db);
        var bumper = await SeedPartAsync(db, "BMP", "Main Bumper", 10);
        var bolt = await SeedPartAsync(db, "BLT", "Bolt", 40);
        var components = new[]
        {
            new SetComponentInput { ComponentProductId = bumper.Id, RequiredQuantity = 1 },
            new SetComponentInput { ComponentProductId = bolt.Id, RequiredQuantity = 1 }
        };
        var shortSet = new ShortSetInput
        {
            Name = "XYZ set short",
            Quantity = 2,
            WholesalePrice = 700,
            RetailPrice = 800,
            IncludedProductIds = new List<int> { bumper.Id }
        };

        var created = await svc.Products.CreateAsync(new Product
        {
            Name = "XYZ set",
            ProductKind = ProductKind.CompleteSet,
            WholesalePrice = 1000
        }, "u1", components: components, shortSet: shortSet);
        Assert.True(created.Ok, created.Error);
        Assert.Equal(0m, created.Product!.CurrentStock);
        Assert.Equal(10m, (await db.Products.FindAsync(bumper.Id))!.CurrentStock);

        var stored = await db.Products.SingleAsync(p => p.Name == "XYZ set short");
        Assert.Equal(ProductKind.SingleItem, stored.ProductKind);
        Assert.Equal(2m, stored.CurrentStock);
        Assert.Equal(700m, stored.WholesalePrice);
        Assert.Equal(800m, stored.RetailPrice);
        Assert.Contains("Main Bumper", stored.Description);
        Assert.DoesNotContain("Bolt", stored.Description);
        var listed = await svc.Products.SearchAsync("XYZ set short", null, "name", false, 1, 20);
        Assert.Equal("Main Bumper", ShortSetNotes.Includes(Assert.Single(listed.Items).Description));

        var again = await svc.Products.UpdateAsync(new Product
        {
            Id = created.Product.Id,
            Sku = created.Product.Sku,
            Name = created.Product.Name,
            ProductKind = ProductKind.CompleteSet,
            WholesalePrice = 1000,
            IsActive = true
        }, "u1", components: components, shortSet: new ShortSetInput
        {
            Name = "XYZ set short",
            Quantity = 3,
            WholesalePrice = 650,
            RetailPrice = 0,
            IncludedProductIds = new List<int> { bumper.Id }
        });
        Assert.True(again.Ok, again.Error);
        var updated = await db.Products.SingleAsync(p => p.Name == "XYZ set short");
        Assert.Equal(5m, updated.CurrentStock);
        Assert.Equal(650m, updated.WholesalePrice);
        Assert.Equal(800m, updated.RetailPrice);
        Assert.Equal(3m, await db.StockMovements.Where(m => m.ProductId == updated.Id && m.Reason == "Added short set").Select(m => m.Quantity).SingleAsync());
        Assert.Equal(10m, (await db.Products.FindAsync(bumper.Id))!.CurrentStock);
    }

    [Fact]
    public async Task Pair_CreatesPricedSides_AndEditsTheDifference()
    {
        await using var db = CreateDb();
        var svc = CreateServices(db);
        var unit = new UD_Autos.Models.Settings.UnitOfMeasure { Name = "Pair", Abbreviation = "Pair" };
        db.UnitsOfMeasure.Add(unit);
        await db.SaveChangesAsync();

        var created = await svc.Products.CreateAsync(new Product
        {
            Name = "Headlight pair",
            ProductKind = ProductKind.CompleteSet,
            UnitId = unit.Id,
            WholesalePrice = 500
        }, "u1", pairSides: new[]
        {
            new PairSideInput { Name = "Left", Quantity = 5, RetailPrice = 100 },
            new PairSideInput { Name = "Right", Quantity = 3, RetailPrice = 80 }
        });
        Assert.True(created.Ok, created.Error);
        Assert.Equal(0m, created.Product!.CurrentStock);

        var page = await svc.Products.SearchAsync("Headlight", null, "name", false, 1, 20);
        var pair = Assert.Single(page.Items, p => p.Id == created.Product.Id);
        Assert.Equal(3m, pair.VirtualAvailableSets);
        Assert.Contains("Left 5", pair.CompositionNote);
        Assert.Contains("Right 3", pair.CompositionNote);

        var sides = await svc.Products.SearchAsync(null, null, "name", false, 1, 20);
        Assert.DoesNotContain(sides.Items, p => p.Name == "Left" || p.Name == "Right");
        Assert.Contains(sides.Items, p => p.Id == created.Product.Id);

        var links = await db.ProductSetComponents.Include(c => c.ComponentProduct)
            .Where(c => c.SetProductId == created.Product.Id && c.IsActive)
            .ToListAsync();
        var left = links.Single(c => c.ComponentProduct.Name == "Left");
        var right = links.Single(c => c.ComponentProduct.Name == "Right");
        var edited = await svc.Products.UpdateAsync(new Product
        {
            Id = created.Product.Id,
            Sku = created.Product.Sku,
            Name = created.Product.Name,
            ProductKind = ProductKind.CompleteSet,
            UnitId = unit.Id,
            WholesalePrice = 500,
            IsActive = true
        }, "u1", pairSides: new[]
        {
            new PairSideInput { ComponentProductId = left.ComponentProductId, Name = "Left", Quantity = 4, RetailPrice = 100 },
            new PairSideInput { ComponentProductId = right.ComponentProductId, Name = "Right", Quantity = 3, RetailPrice = 80 }
        });
        Assert.True(edited.Ok, edited.Error);
        Assert.Equal(4m, (await db.Products.FindAsync(left.ComponentProductId))!.CurrentStock);
        Assert.Equal(3m, (await db.Products.FindAsync(right.ComponentProductId))!.CurrentStock);
        Assert.Equal(-1m, await db.StockMovements.Where(m => m.ProductId == left.ComponentProductId && m.Reason == "Pair side quantity").Select(m => m.Quantity).SingleAsync());
    }

    [Fact]
    public async Task Piece_EditQuantity_WritesOnlyTheDifference()
    {
        await using var db = CreateDb();
        var (products, _, _) = CreateServices(db);
        var created = await products.CreateAsync(new Product
        {
            Name = "Bolt",
            ProductKind = ProductKind.SingleItem,
            CurrentStock = 10,
            RetailPrice = 20
        }, "u1");
        Assert.True(created.Ok, created.Error);

        var edited = await products.UpdateAsync(new Product
        {
            Id = created.Product!.Id,
            Sku = created.Product.Sku,
            Name = "Bolt",
            ProductKind = ProductKind.SingleItem,
            CurrentStock = 14,
            RetailPrice = 20
        }, "u1", adjustStock: true);

        Assert.True(edited.Ok, edited.Error);
        Assert.Equal(14m, (await db.Products.FindAsync(created.Product.Id))!.CurrentStock);
        Assert.Equal(4m, await db.StockMovements.Where(m => m.ProductId == created.Product.Id && m.Reason == "Quantity").Select(m => m.Quantity).SingleAsync());
    }

    [Fact]
    public async Task InventoryList_ShowsAvailableSets_AndOutOfStockFilter()
    {
        await using var db = CreateDb();
        var (_, set, bumper, _) = await SeedBumperSetAsync(db);
        var inventory = new InventoryService(db, new NoopAudit());

        var page = await inventory.SearchStockAsync(null, false, false, 1, 20);
        var row = Assert.Single(page.Items, p => p.Id == set.Id);
        Assert.Equal(10m, row.VirtualAvailableSets);
        Assert.Contains(row.LimitingComponentName, new[] { "Main Bumper", "Bolt" });

        bumper.CurrentStock = 0;
        await db.SaveChangesAsync();
        var outOfStock = await inventory.SearchStockAsync(null, false, true, 1, 20);
        Assert.Contains(outOfStock.Items, p => p.Id == set.Id);
    }

    private static async Task<(Customer Customer, Product Set, Product Bumper, Product Bolt)> SeedBumperSetAsync(ApplicationDbContext db)
    {
        var customer = await SeedCustomerAsync(db);
        var bumper = await SeedPartAsync(db, "BMP", "Main Bumper", 10);
        var bolt = await SeedPartAsync(db, "BLT", "Bolt", 40);
        var audit = new NoopAudit();
        var codes = new StubCodes();
        var products = new ProductService(db, audit, codes);
        var created = await products.CreateAsync(new Product
        {
            Name = "Complete Truck Bumper Set",
            ProductKind = ProductKind.CompleteSet,
            WholesalePrice = 800
        }, "u1", components: new[]
        {
            new SetComponentInput { ComponentProductId = bumper.Id, RequiredQuantity = 1 },
            new SetComponentInput { ComponentProductId = bolt.Id, RequiredQuantity = 4 }
        });
        if (!created.Ok || created.Product is null)
            throw new InvalidOperationException(created.Error);
        return (customer, created.Product, bumper, bolt);
    }
}
