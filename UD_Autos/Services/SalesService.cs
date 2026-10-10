using Microsoft.EntityFrameworkCore;
using UD_Autos.Data;
using UD_Autos.Models.Common;
using UD_Autos.Models.Finance;
using UD_Autos.Models.Inventory;
using UD_Autos.Models.Products;
using UD_Autos.Models.Sales;

namespace UD_Autos.Services;

public class SaleLineInput
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineDiscount { get; set; }
    public string? Notes { get; set; }
}

public class CreateSaleRequest
{
    public int CustomerId { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow.Date;
    public PaymentMode PaymentMode { get; set; } = PaymentMode.Credit;
    public PaymentAccount? PaymentAccount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal DeliveryCharge { get; set; }
    public decimal OtherCharge { get; set; }
    public decimal PaymentReceived { get; set; }
    public string? PaymentReference { get; set; }
    public string? Notes { get; set; }
    public bool AllowBelowMinimum { get; set; }
    public List<SaleLineInput> Items { get; set; } = new();
    public string? IdempotencyKey { get; set; }
}

public class DailySalesSummary
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public decimal TotalSales { get; set; }
    public decimal CashSales { get; set; }
    public decimal CreditSales { get; set; }
    public decimal Collections { get; set; }
    public decimal Returns { get; set; }
    public decimal Cogs { get; set; }
    public decimal GrossProfit { get; set; }
    public IReadOnlyList<Invoice> Invoices { get; set; } = Array.Empty<Invoice>();
}

public interface ISalesService
{
    Task<(bool Ok, string? Error, Invoice? Invoice)> CreateSaleAsync(CreateSaleRequest request, string? userId, bool canViewCosts, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> VoidInvoiceAsync(int invoiceId, string reason, string? userId, CancellationToken cancellationToken = default);
    Task<Invoice?> GetInvoiceAsync(int id, CancellationToken cancellationToken = default);
    Task<PagedResult<Invoice>> SearchInvoicesAsync(string? search, int? customerId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken = default, bool includeItems = false);
    Task<DailySalesSummary> GetDailySalesAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
}

public class SalesService : ISalesService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICodeGeneratorService _codes;
    private readonly IPaymentAllocationService _allocations;
    private readonly ICashBankService _cash;

    public SalesService(
        ApplicationDbContext db,
        IAuditService audit,
        ICodeGeneratorService codes,
        IPaymentAllocationService allocations,
        ICashBankService cash)
    {
        _db = db;
        _audit = audit;
        _codes = codes;
        _allocations = allocations;
        _cash = cash;
    }

    public async Task<(bool Ok, string? Error, Invoice? Invoice)> CreateSaleAsync(
        CreateSaleRequest request, string? userId, bool canViewCosts, CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0) return (false, "Add at least one product line.", null);
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId && c.IsActive, cancellationToken);
        if (customer is null) return (false, "Customer not found.", null);

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await _db.Invoices.AsNoTracking()
                .FirstOrDefaultAsync(i => i.IdempotencyKey == request.IdempotencyKey, cancellationToken);
            if (existing is not null) return (true, null, await GetInvoiceAsync(existing.Id, cancellationToken));
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var invoice = new Invoice
        {
            InvoiceNumber = await _codes.NextInvoiceNumberAsync(cancellationToken),
            CustomerId = customer.Id,
            InvoiceDate = request.InvoiceDate.Date,
            PaymentMode = request.PaymentMode,
            PaymentAccount = request.PaymentAccount,
            DiscountAmount = Math.Max(0, request.DiscountAmount),
            DeliveryCharge = Math.Max(0, request.DeliveryCharge),
            OtherCharge = Math.Max(0, request.OtherCharge),
            PaymentReference = request.PaymentReference,
            Notes = request.Notes,
            IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim(),
            PreviousBalance = customer.CurrentBalance,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        decimal subtotal = 0;
        decimal totalCogs = 0;

        var prepared = new List<PreparedSaleLine>();
        foreach (var line in request.Items)
        {
            if (line.Quantity <= 0) return (false, "Quantity must be greater than zero.", null);
            if (line.UnitPrice < 0 || line.LineDiscount < 0) return (false, "Price/discount cannot be negative.", null);

            var product = await _db.Products.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == line.ProductId && p.IsActive, cancellationToken);
            if (product is null) return (false, $"Product {line.ProductId} not found.", null);

            if (!request.AllowBelowMinimum && product.MinimumSalePrice > 0 && line.UnitPrice < product.MinimumSalePrice)
                return (false, $"{product.Sku} is below minimum sale price Rs. {product.MinimumSalePrice:N2}.", null);

            var lineTotal = Math.Round((line.Quantity * line.UnitPrice) - line.LineDiscount, 2);
            if (lineTotal < 0) return (false, "Line total cannot be negative.", null);

            var preparedLine = new PreparedSaleLine
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Sku = product.Sku ?? product.Id.ToString(),
                IsSet = product.ProductKind == ProductKind.CompleteSet,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                LineDiscount = line.LineDiscount,
                LineTotal = lineTotal,
                Notes = line.Notes
            };

            if (preparedLine.IsSet)
            {
                var components = await _db.ProductSetComponents.AsNoTracking()
                    .Where(c => c.SetProductId == product.Id && c.IsActive)
                    .Select(c => new
                    {
                        c.ComponentProductId,
                        c.RequiredQuantity,
                        c.ComponentProduct.Name,
                        c.ComponentProduct.Sku,
                        c.ComponentProduct.IsActive,
                        c.ComponentProduct.ProductKind
                    })
                    .ToListAsync(cancellationToken);

                if (components.Count == 0)
                    return (false, $"{product.Name} has no items. Edit the set before selling.", null);

                foreach (var component in components)
                {
                    if (!component.IsActive || component.ProductKind != ProductKind.SingleItem)
                        return (false, $"{component.Name} cannot be used in {product.Name}. Edit the set before selling.", null);
                    if (component.RequiredQuantity <= 0)
                        return (false, $"{product.Name} has an invalid item quantity.", null);

                    preparedLine.Components.Add(new PreparedComponent
                    {
                        ProductId = component.ComponentProductId,
                        Name = component.Name,
                        Sku = component.Sku ?? component.ComponentProductId.ToString(),
                        PerSet = component.RequiredQuantity
                    });
                }
            }

            prepared.Add(preparedLine);
        }

        var demand = ProductSetPlanner.AggregateDemand(prepared.Select(p => new SaleDemandLine
        {
            ProductId = p.ProductId,
            IsSet = p.IsSet,
            Quantity = p.Quantity,
            Components = p.Components.Select(c => (c.ProductId, c.PerSet)).ToList()
        }));

        var lockIds = demand.Keys.Concat(prepared.Where(p => p.IsSet).Select(p => p.ProductId)).Distinct().ToList();
        await LockProductRowsAsync(lockIds, cancellationToken);

        var stockProducts = await _db.Products
            .Where(p => demand.Keys.Contains(p.Id))
            .ToListAsync(cancellationToken);
        var stockById = stockProducts.ToDictionary(p => p.Id);

        foreach (var (productId, required) in demand)
        {
            if (!stockById.TryGetValue(productId, out var stockProduct) || !stockProduct.IsActive)
                return (false, $"Product {productId} not found.", null);

            var available = stockProduct.CurrentStock - stockProduct.ReservedQuantity;
            if (required > available)
            {
                var setNames = prepared
                    .Where(p => p.IsSet && p.Components.Any(c => c.ProductId == productId))
                    .Select(p => p.ProductName)
                    .Distinct()
                    .ToList();
                var forSet = setNames.Count > 0 ? $" for {string.Join(", ", setNames)}" : "";
                return (false, $"Not enough stock: {stockProduct.Name}. Available {available:N2}, required {required:N2}{forSet}.", null);
            }
        }

        foreach (var line in prepared)
        {
            if (!line.IsSet)
            {
                var product = stockById[line.ProductId];
                var (okConsume, consumeError, takes, lineCogs) = await ConsumeStockFifoAsync(
                    product, line.Quantity, invoice.InvoiceNumber, userId,
                    StockMovementType.Sale, "Sale", cancellationToken);
                if (!okConsume) return (false, consumeError, null);

                invoice.Items.Add(BuildInvoiceItem(line, lineCogs, takes, userId));
                subtotal += line.LineTotal;
                totalCogs += lineCogs;
                continue;
            }

            decimal setCogs = 0;
            var setItem = BuildInvoiceItem(line, 0, Array.Empty<FifoTake>(), userId);
            foreach (var component in line.Components)
            {
                var need = line.Quantity * component.PerSet;
                var componentProduct = stockById[component.ProductId];
                var (okConsume, consumeError, takes, componentCogs) = await ConsumeStockFifoAsync(
                    componentProduct, need, invoice.InvoiceNumber, userId,
                    StockMovementType.SetConsumption, $"Complete set: {line.ProductName}", cancellationToken);
                if (!okConsume) return (false, consumeError, null);

                setCogs += componentCogs;
                setItem.SetComponents.Add(new InvoiceSetComponent
                {
                    ComponentProductId = component.ProductId,
                    RequiredQuantityPerSet = component.PerSet,
                    QuantityConsumed = need,
                    ReturnedQuantity = 0,
                    UnitCogsPkr = need > 0 ? Math.Round(componentCogs / need, 4) : 0,
                    LineCogsPkr = componentCogs,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId,
                    BatchAllocations = takes.Select(t => new InvoiceSetComponentBatchAllocation
                    {
                        InventoryBatchId = t.InventoryBatchId,
                        Quantity = t.Quantity,
                        ReturnedQuantity = 0,
                        UnitCostPkr = t.UnitCostPkr,
                        LineCostPkr = t.LineCostPkr,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = userId
                    }).ToList()
                });
            }

            setItem.UnitCogsPkr = line.Quantity > 0 ? Math.Round(setCogs / line.Quantity, 4) : 0;
            setItem.LineCogsPkr = Math.Round(setCogs, 2);
            invoice.Items.Add(setItem);
            subtotal += line.LineTotal;
            totalCogs += setItem.LineCogsPkr;
        }

        invoice.Subtotal = Math.Round(subtotal, 2);
        invoice.BillTotal = Math.Round(invoice.Subtotal - invoice.DiscountAmount + invoice.DeliveryCharge + invoice.OtherCharge, 2);
        if (invoice.BillTotal < 0) return (false, "Bill total cannot be negative.", null);

        invoice.TotalCogs = Math.Round(totalCogs, 2);
        invoice.GrossProfit = Math.Round(invoice.BillTotal - invoice.TotalCogs, 2);

        var paymentReceived = request.PaymentMode switch
        {
            PaymentMode.Cash or PaymentMode.Bank => invoice.BillTotal,
            PaymentMode.Partial => Math.Max(0, request.PaymentReceived),
            _ => 0m
        };

        if (request.PaymentMode == PaymentMode.Partial && paymentReceived <= 0)
            return (false, "Partial payment requires PaymentReceived > 0.", null);
        if (paymentReceived > invoice.BillTotal)
            return (false, "Payment received cannot exceed bill total.", null);
        if ((request.PaymentMode is PaymentMode.Cash or PaymentMode.Bank or PaymentMode.Partial) && request.PaymentAccount is null)
            return (false, "Select Cash or Bank account for collection.", null);

        if (request.PaymentMode is PaymentMode.Credit or PaymentMode.Partial)
        {
            var projected = customer.CurrentBalance + invoice.BillTotal - paymentReceived;
            if (customer.CreditLimit is > 0 && projected > customer.CreditLimit.Value)
                return (false, $"Credit limit exceeded. Limit Rs. {customer.CreditLimit:N2}, projected Rs. {projected:N2}.", null);
        }

        invoice.PaymentReceived = paymentReceived;
        invoice.AmountPaidOnInvoice = paymentReceived;
        invoice.OutstandingAmount = Math.Round(invoice.BillTotal - paymentReceived, 2);
        invoice.NewBalance = Math.Round(invoice.PreviousBalance + invoice.BillTotal - paymentReceived, 2);

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync(cancellationToken);

        // Ledger: debit sale
        await AddLedgerAsync(customer.Id, invoice.InvoiceDate, LedgerEntryType.Invoice,
            $"Invoice {invoice.InvoiceNumber}", nameof(Invoice), invoice.Id.ToString(), invoice.InvoiceNumber,
            debit: invoice.BillTotal, credit: 0, userId, cancellationToken);

        customer.TotalPurchases = Math.Round(customer.TotalPurchases + invoice.BillTotal, 2);
        customer.LastPurchaseAt = invoice.InvoiceDate;
        customer.CurrentBalance = invoice.NewBalance;
        customer.UpdatedAt = DateTime.UtcNow;
        customer.UpdatedBy = userId;

        if (paymentReceived > 0)
        {
            var payment = new CustomerPayment
            {
                PaymentNumber = await _codes.NextPaymentNumberAsync(cancellationToken),
                CustomerId = customer.Id,
                PaymentDate = invoice.InvoiceDate,
                Amount = paymentReceived,
                Account = request.PaymentAccount ?? PaymentAccount.Cash,
                Reference = request.PaymentReference,
                Notes = $"Auto from invoice {invoice.InvoiceNumber}",
                SourceInvoiceId = invoice.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            };
            _db.CustomerPayments.Add(payment);
            await _db.SaveChangesAsync(cancellationToken);

            await AddLedgerAsync(customer.Id, invoice.InvoiceDate, LedgerEntryType.Payment,
                $"Payment {payment.PaymentNumber}", nameof(CustomerPayment), payment.Id.ToString(), payment.PaymentNumber,
                debit: 0, credit: paymentReceived, userId, cancellationToken);

            // Outstanding/AmountPaid already reflect this payment — allocate directly (do not FIFO again).
            _db.PaymentAllocations.Add(new PaymentAllocation
            {
                CustomerPaymentId = payment.Id,
                InvoiceId = invoice.Id,
                Amount = paymentReceived,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            });

            await _cash.PostLinkedAsync(
                CashBankMovementType.Collection, CashBankDirection.In, payment.Account, payment.PaymentDate, payment.Amount,
                nameof(CustomerPayment), payment.Id.ToString(), payment.PaymentNumber,
                $"Collection {payment.PaymentNumber} (invoice {invoice.InvoiceNumber})", userId, cancellationToken);

            customer.TotalPayments = Math.Round(customer.TotalPayments + paymentReceived, 2);
            customer.LastPaymentAt = invoice.InvoiceDate;
            // Balance already set to NewBalance which includes payment.
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("CreateSale", nameof(Invoice), invoice.Id.ToString(), invoice.InvoiceNumber,
            newValues: new { invoice.BillTotal, invoice.PaymentMode, invoice.OutstandingAmount }, cancellationToken: cancellationToken);
        await tx.CommitAsync(cancellationToken);

        _ = canViewCosts;
        return (true, null, await GetInvoiceAsync(invoice.Id, cancellationToken));
    }

    public async Task<(bool Ok, string? Error)> VoidInvoiceAsync(
        int invoiceId, string reason, string? userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
            return (false, "Void reason is required (min 5 characters).");

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var invoice = await _db.Invoices
            .Include(i => i.Customer)
            .Include(i => i.Items).ThenInclude(ii => ii.BatchAllocations)
            .Include(i => i.Items).ThenInclude(ii => ii.Product)
            .Include(i => i.Items).ThenInclude(ii => ii.SetComponents).ThenInclude(c => c.BatchAllocations)
            .Include(i => i.Items).ThenInclude(ii => ii.SetComponents).ThenInclude(c => c.ComponentProduct)
            .Include(i => i.Returns)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

        if (invoice is null) return (false, "Invoice not found.");
        if (invoice.IsVoided) return (false, "Invoice is already voided.");
        if (invoice.Returns.Any())
            return (false, "Cannot void an invoice that has returns. Use return/correction workflow instead.");

        foreach (var item in invoice.Items)
        {
            if (item.SetComponents.Count > 0)
            {
                var restored = await RestoreSetSnapshotAsync(
                    item, item.Quantity - item.ReturnedQuantity, invoice.InvoiceNumber, $"Void invoice: {reason}",
                    StockMovementType.Adjustment, nameof(Invoice), invoice.Id.ToString(), userId, cancellationToken);
                if (!restored.Ok) return (false, restored.Error);
                continue;
            }

            foreach (var alloc in item.BatchAllocations)
            {
                var batch = await _db.InventoryBatches.FirstOrDefaultAsync(b => b.Id == alloc.InventoryBatchId, cancellationToken);
                if (batch is null) continue;
                batch.RemainingQuantity += alloc.Quantity;
                batch.UpdatedAt = DateTime.UtcNow;
                batch.UpdatedBy = userId;
            }

            var product = item.Product;
            product.CurrentStock += item.Quantity;
            product.StockValue = Math.Round(product.CurrentStock * (product.AverageCost ?? product.LatestLandedCost ?? 0), 2);
            product.UpdatedAt = DateTime.UtcNow;
            product.UpdatedBy = userId;

            _db.StockMovements.Add(new StockMovement
            {
                ProductId = product.Id,
                InventoryBatchId = item.BatchAllocations.FirstOrDefault()?.InventoryBatchId,
                MovementType = StockMovementType.Adjustment,
                Quantity = item.Quantity,
                UnitCostPkr = item.UnitCogsPkr,
                QuantityAfter = product.CurrentStock,
                ReferenceType = nameof(Invoice),
                ReferenceId = invoice.Id.ToString(),
                ReferenceNumber = invoice.InvoiceNumber,
                Reason = $"Void invoice: {reason}",
                OccurredAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            });
        }

        await AddLedgerAsync(invoice.CustomerId, DateTime.UtcNow.Date, LedgerEntryType.Adjustment,
            $"Void invoice {invoice.InvoiceNumber}", nameof(Invoice), invoice.Id.ToString(), invoice.InvoiceNumber,
            debit: 0, credit: invoice.BillTotal, userId, cancellationToken);

        if (invoice.PaymentReceived > 0)
        {
            await AddLedgerAsync(invoice.CustomerId, DateTime.UtcNow.Date, LedgerEntryType.Adjustment,
                $"Void reverse payment on {invoice.InvoiceNumber}", nameof(Invoice), invoice.Id.ToString(), invoice.InvoiceNumber,
                debit: invoice.PaymentReceived, credit: 0, userId, cancellationToken);

            var payments = await _db.CustomerPayments.Where(p => p.SourceInvoiceId == invoice.Id).ToListAsync(cancellationToken);
            foreach (var payment in payments)
            {
                await _cash.PostLinkedAsync(
                    CashBankMovementType.OtherPayment, CashBankDirection.Out, payment.Account, DateTime.UtcNow.Date, payment.Amount,
                    "VoidPayment", payment.Id.ToString(), payment.PaymentNumber,
                    $"Void reverse collection {payment.PaymentNumber} for {invoice.InvoiceNumber}", userId, cancellationToken);
            }
        }

        var customer = invoice.Customer;
        customer.CurrentBalance = Math.Round(customer.CurrentBalance - invoice.BillTotal + invoice.PaymentReceived, 2);
        customer.TotalPurchases = Math.Round(Math.Max(0, customer.TotalPurchases - invoice.BillTotal), 2);
        if (invoice.PaymentReceived > 0)
            customer.TotalPayments = Math.Round(Math.Max(0, customer.TotalPayments - invoice.PaymentReceived), 2);
        customer.UpdatedAt = DateTime.UtcNow;
        customer.UpdatedBy = userId;

        invoice.IsVoided = true;
        invoice.VoidReason = reason.Trim();
        invoice.VoidedAt = DateTime.UtcNow;
        invoice.VoidedBy = userId;
        invoice.OutstandingAmount = 0;
        invoice.UpdatedAt = DateTime.UtcNow;
        invoice.UpdatedBy = userId;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (false, "Invoice was modified by another user. Reload and try again.");
        }

        await _audit.LogAsync("VoidInvoice", nameof(Invoice), invoice.Id.ToString(), invoice.InvoiceNumber, reason,
            oldValues: new { invoice.BillTotal, invoice.PaymentReceived },
            newValues: new { Voided = true },
            cancellationToken: cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return (true, null);
    }

    public Task<Invoice?> GetInvoiceAsync(int id, CancellationToken cancellationToken = default)
        => _db.Invoices
            .Include(i => i.Customer)
            .Include(i => i.Items).ThenInclude(x => x.Product)
            .Include(i => i.Items).ThenInclude(x => x.BatchAllocations)
            .Include(i => i.Items).ThenInclude(x => x.SetComponents).ThenInclude(c => c.ComponentProduct)
            .Include(i => i.Allocations)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<PagedResult<Invoice>> SearchInvoicesAsync(
        string? search, int? customerId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken = default, bool includeItems = false)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);
        var query = _db.Invoices.AsNoTracking().Include(i => i.Customer).Where(i => !i.IsVoided);
        if (includeItems)
            query = query.Include(i => i.Items).ThenInclude(x => x.Product);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(i => i.InvoiceNumber.Contains(term) || i.Customer.Name.Contains(term) || (i.Customer.ShopName != null && i.Customer.ShopName.Contains(term)));
        }
        if (customerId.HasValue) query = query.Where(i => i.CustomerId == customerId);
        if (from.HasValue) query = query.Where(i => i.InvoiceDate >= from.Value.Date);
        if (to.HasValue) query = query.Where(i => i.InvoiceDate <= to.Value.Date);
        query = query.OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Invoice>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Search = search
        };
    }

    public async Task<DailySalesSummary> GetDailySalesAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        from = from.Date;
        to = to.Date;
        var invoices = await _db.Invoices.AsNoTracking()
            .Include(i => i.Customer)
            .Where(i => !i.IsVoided && i.InvoiceDate >= from && i.InvoiceDate <= to)
            .OrderByDescending(i => i.InvoiceDate)
            .ToListAsync(cancellationToken);

        var collections = await _db.CustomerPayments.AsNoTracking()
            .Where(p => p.PaymentDate >= from && p.PaymentDate <= to)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0;

        var returns = await _db.SalesReturns.AsNoTracking()
            .Where(r => r.ReturnDate >= from && r.ReturnDate <= to)
            .SumAsync(r => (decimal?)r.TotalAmount, cancellationToken) ?? 0;

        var totalSales = invoices.Sum(i => i.BillTotal);
        var cashSales = invoices.Where(i => i.PaymentMode is PaymentMode.Cash or PaymentMode.Bank).Sum(i => i.BillTotal);
        var creditSales = invoices.Where(i => i.PaymentMode is PaymentMode.Credit or PaymentMode.Partial)
            .Sum(i => i.BillTotal - i.PaymentReceived);

        return new DailySalesSummary
        {
            From = from,
            To = to,
            TotalSales = totalSales,
            CashSales = cashSales,
            CreditSales = creditSales,
            Collections = collections,
            Returns = returns,
            Cogs = invoices.Sum(i => i.TotalCogs),
            GrossProfit = invoices.Sum(i => i.GrossProfit),
            Invoices = invoices
        };
    }

    private async Task<(bool Ok, string? Error, List<FifoTake> Takes, decimal LineCogs)> ConsumeStockFifoAsync(
        Product product, decimal quantity, string invoiceNumber, string? userId,
        StockMovementType movementType, string reason, CancellationToken cancellationToken)
    {
        var batches = await _db.InventoryBatches
            .Where(b => b.ProductId == product.Id && b.RemainingQuantity > 0)
            .OrderBy(b => b.ReceivedAt).ThenBy(b => b.Id)
            .ToListAsync(cancellationToken);

        var remaining = quantity;
        var takes = new List<FifoTake>();
        decimal lineCogs = 0;

        foreach (var batch in batches)
        {
            if (remaining <= 0) break;
            var available = batch.RemainingQuantity - batch.ReservedQuantity;
            if (available <= 0) continue;
            var take = Math.Min(available, remaining);
            batch.RemainingQuantity -= take;
            batch.UpdatedAt = DateTime.UtcNow;
            batch.UpdatedBy = userId;

            var cost = Math.Round(take * batch.LandedUnitCostPkr, 2);
            lineCogs += cost;
            takes.Add(new FifoTake
            {
                InventoryBatchId = batch.Id,
                Quantity = take,
                UnitCostPkr = batch.LandedUnitCostPkr,
                LineCostPkr = cost
            });
            remaining -= take;
        }

        if (remaining > 0)
        {
            if (product.CurrentStock < quantity)
                return (false, $"Insufficient batch/stock for {product.Sku}.", takes, 0);

            var unitCost = product.AverageCost ?? product.LatestLandedCost ?? 0;
            lineCogs += Math.Round(remaining * unitCost, 2);
            remaining = 0;
        }

        product.CurrentStock -= quantity;
        if (product.CurrentStock < 0) return (false, $"Stock would go negative for {product.Sku}.", takes, 0);
        product.StockValue = Math.Round(product.CurrentStock * (product.AverageCost ?? product.LatestLandedCost ?? 0), 2);
        product.UpdatedAt = DateTime.UtcNow;
        product.UpdatedBy = userId;

        _db.StockMovements.Add(new StockMovement
        {
            ProductId = product.Id,
            InventoryBatchId = takes.FirstOrDefault()?.InventoryBatchId,
            MovementType = movementType,
            Quantity = -quantity,
            UnitCostPkr = quantity > 0 ? Math.Round(lineCogs / quantity, 4) : 0,
            QuantityAfter = product.CurrentStock,
            ReferenceType = nameof(Invoice),
            ReferenceNumber = invoiceNumber,
            Reason = reason,
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        });

        return (true, null, takes, Math.Round(lineCogs, 2));
    }

    private static InvoiceItem BuildInvoiceItem(PreparedSaleLine line, decimal lineCogs, IReadOnlyList<FifoTake> takes, string? userId)
        => new()
        {
            ProductId = line.ProductId,
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice,
            LineDiscount = line.LineDiscount,
            LineTotal = line.LineTotal,
            UnitCogsPkr = line.Quantity > 0 ? Math.Round(lineCogs / line.Quantity, 4) : 0,
            LineCogsPkr = lineCogs,
            Notes = line.Notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
            BatchAllocations = takes.Select(t => new InvoiceItemBatchAllocation
            {
                InventoryBatchId = t.InventoryBatchId,
                Quantity = t.Quantity,
                UnitCostPkr = t.UnitCostPkr,
                LineCostPkr = t.LineCostPkr,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            }).ToList()
        };

    private async Task LockProductRowsAsync(IReadOnlyCollection<int> productIds, CancellationToken cancellationToken)
    {
        if (productIds.Count == 0) return;
        var provider = _db.Database.ProviderName ?? string.Empty;
        if (!provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            return;

        var sql = ProductSetPlanner.BuildProductLockSql(productIds);
        await _db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task<(bool Ok, string? Error)> RestoreSetSnapshotAsync(
        InvoiceItem item, decimal setQuantity, string referenceNumber, string reason,
        StockMovementType movementType, string referenceType, string? referenceId, string? userId,
        CancellationToken cancellationToken)
    {
        if (setQuantity <= 0) return (true, null);

        foreach (var component in item.SetComponents)
        {
            var plan = ProductSetPlanner.PlanComponentRestore(
                setQuantity,
                component.RequiredQuantityPerSet,
                component.QuantityConsumed,
                component.ReturnedQuantity,
                component.BatchAllocations.Select(a => (a.InventoryBatchId, a.Quantity, a.ReturnedQuantity)));
            if (!plan.Ok) return (false, plan.Error);

            foreach (var slice in plan.Slices)
            {
                var batch = await _db.InventoryBatches.FirstOrDefaultAsync(b => b.Id == slice.BatchId, cancellationToken);
                if (batch is null) continue;
                batch.RemainingQuantity += slice.Quantity;
                batch.UpdatedAt = DateTime.UtcNow;
                batch.UpdatedBy = userId;

                var allocation = component.BatchAllocations.First(a => a.InventoryBatchId == slice.BatchId);
                allocation.ReturnedQuantity += slice.Quantity;
                allocation.UpdatedAt = DateTime.UtcNow;
                allocation.UpdatedBy = userId;
            }

            var product = component.ComponentProduct;
            product.CurrentStock += plan.Quantity;
            product.StockValue = Math.Round(product.CurrentStock * (product.AverageCost ?? product.LatestLandedCost ?? component.UnitCogsPkr), 2);
            product.UpdatedAt = DateTime.UtcNow;
            product.UpdatedBy = userId;
            component.ReturnedQuantity += plan.Quantity;
            component.UpdatedAt = DateTime.UtcNow;
            component.UpdatedBy = userId;

            _db.StockMovements.Add(new StockMovement
            {
                ProductId = product.Id,
                InventoryBatchId = plan.Slices.FirstOrDefault()?.BatchId,
                MovementType = movementType,
                Quantity = plan.Quantity,
                UnitCostPkr = component.UnitCogsPkr,
                QuantityAfter = product.CurrentStock,
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                ReferenceNumber = referenceNumber,
                Reason = reason,
                OccurredAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            });
        }

        return (true, null);
    }

    private async Task AddLedgerAsync(
        int customerId, DateTime date, LedgerEntryType type, string detail,
        string? refType, string? refId, string? refNumber,
        decimal debit, decimal credit, string? userId, CancellationToken cancellationToken)
    {
        var last = await _db.CustomerLedgerEntries
            .Where(e => e.CustomerId == customerId)
            .OrderByDescending(e => e.EntryDate).ThenByDescending(e => e.Id)
            .Select(e => (decimal?)e.RunningBalance)
            .FirstOrDefaultAsync(cancellationToken) ?? 0m;

        var running = Math.Round(last + debit - credit, 2);
        _db.CustomerLedgerEntries.Add(new CustomerLedgerEntry
        {
            CustomerId = customerId,
            EntryDate = date.Date,
            EntryType = type,
            Detail = detail,
            ReferenceType = refType,
            ReferenceId = refId,
            ReferenceNumber = refNumber,
            Debit = debit,
            Credit = credit,
            RunningBalance = running,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        });
    }

    private sealed class FifoTake
    {
        public int InventoryBatchId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCostPkr { get; set; }
        public decimal LineCostPkr { get; set; }
    }

    private sealed class PreparedComponent
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public decimal PerSet { get; set; }
    }

    private sealed class PreparedSaleLine
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public bool IsSet { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineDiscount { get; set; }
        public decimal LineTotal { get; set; }
        public string? Notes { get; set; }
        public List<PreparedComponent> Components { get; set; } = new();
    }
}
