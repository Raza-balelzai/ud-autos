using Microsoft.EntityFrameworkCore;
using UD_Autos.Data;
using UD_Autos.Models.Common;
using UD_Autos.Models.Products;
using UD_Autos.Models.Shipments;

namespace UD_Autos.Services;

public class ShipmentItemInput
{
    public int ProductId { get; set; }
    public int? PurchaseOrderId { get; set; }
    public int? PurchaseOrderItemId { get; set; }
    public int? SupplierId { get; set; }
    public decimal ExpectedQuantity { get; set; }
    public decimal? UnitPriceRmb { get; set; }
    public string? Notes { get; set; }
}

public class ReceivingSlipInput
{
    public string? SlipNumber { get; set; }
    public DateTime ReceivingDate { get; set; } = DateTime.UtcNow.Date;
    public int SupplierId { get; set; }
    public string? AgentWarehouse { get; set; }
    public int Cartons { get; set; }
    public decimal WeightKg { get; set; }
    public decimal Cbm { get; set; }
    public string? TrackingReference { get; set; }
    public string? FilePath { get; set; }
    public string? Notes { get; set; }
}

public interface IShipmentService
{
    Task<PagedResult<Shipment>> SearchAsync(string? search, ShipmentStatus? status, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Shipment?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error, Shipment? Shipment)> CreateAsync(Shipment header, IReadOnlyList<ShipmentItemInput> items, string? userId, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> UpdateHeaderAsync(Shipment header, string? userId, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> ReplaceItemsAsync(int shipmentId, IReadOnlyList<ShipmentItemInput> items, string? userId, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> AddReceivingSlipAsync(int shipmentId, ReceivingSlipInput slip, string? userId, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> UpdateFreightAndKarachiAsync(Shipment header, string? userId, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> ChangeStatusAsync(int id, ShipmentStatus newStatus, string? userId, string? reason = null, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> AddTimelineEventAsync(int shipmentId, ShipmentTimelineEventType type, DateTime occurredAt, string? notes, string? userId, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> AddDocumentAsync(int shipmentId, ShipmentDocumentType type, string originalName, string storagePath, string? contentType, long size, string? notes, string? userId, CancellationToken cancellationToken = default);
}

public class ShipmentService : IShipmentService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICodeGeneratorService _codes;

    public ShipmentService(ApplicationDbContext db, IAuditService audit, ICodeGeneratorService codes)
    {
        _db = db;
        _audit = audit;
        _codes = codes;
    }

    public async Task<PagedResult<Shipment>> SearchAsync(
        string? search, ShipmentStatus? status, DateTime? from, DateTime? to, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);

        var query = _db.Shipments.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(s => s.ShipmentNumber.Contains(term) || s.ShippingAgent.Contains(term) || (s.BiltyNumber != null && s.BiltyNumber.Contains(term)));
        }

        if (status.HasValue) query = query.Where(s => s.Status == status);
        if (from.HasValue) query = query.Where(s => s.CreatedAt >= from.Value.Date);
        if (to.HasValue) query = query.Where(s => s.CreatedAt < to.Value.Date.AddDays(1));

        query = query.OrderByDescending(s => s.Id);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<Shipment>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Search = search
        };
    }

    public Task<Shipment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => _db.Shipments
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .Include(s => s.Items).ThenInclude(i => i.PurchaseOrder)
            .Include(s => s.Items).ThenInclude(i => i.Supplier)
            .Include(s => s.ReceivingSlips).ThenInclude(r => r.Supplier)
            .Include(s => s.Documents)
            .Include(s => s.TimelineEvents)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<(bool Ok, string? Error, Shipment? Shipment)> CreateAsync(
        Shipment header, IReadOnlyList<ShipmentItemInput> items, string? userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(header.ShippingAgent))
            return (false, "Shipping agent is required.", null);

        var itemError = await ValidateItemsAsync(items, cancellationToken);
        if (itemError is not null) return (false, itemError, null);

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(header.ShipmentNumber))
            header.ShipmentNumber = await _codes.NextShipmentNumberAsync(cancellationToken);

        header.ShipmentNumber = header.ShipmentNumber.Trim().ToUpperInvariant();
        if (await _db.Shipments.AnyAsync(s => s.ShipmentNumber == header.ShipmentNumber, cancellationToken))
            return (false, $"Shipment '{header.ShipmentNumber}' already exists.", null);

        header.CreatedAt = DateTime.UtcNow;
        header.CreatedBy = userId;
        header.IsFinalized = false;
        header.Items = BuildItems(items, userId);
        header.TotalUnits = header.Items.Sum(i => i.ExpectedQuantity);
        RecalculateFreight(header);
        RecalculateTransitDays(header);

        _db.Shipments.Add(header);
        await _db.SaveChangesAsync(cancellationToken);

        _db.ShipmentTimelineEvents.Add(new ShipmentTimelineEvent
        {
            ShipmentId = header.Id,
            EventType = ShipmentTimelineEventType.Created,
            OccurredAt = DateTime.UtcNow,
            RelatedStatus = header.Status,
            Notes = "Shipment created",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        });
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("Create", nameof(Shipment), header.Id.ToString(), header.ShipmentNumber, cancellationToken: cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return (true, null, header);
    }

    public async Task<(bool Ok, string? Error)> UpdateHeaderAsync(Shipment header, string? userId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Shipments.FirstOrDefaultAsync(s => s.Id == header.Id, cancellationToken);
        if (existing is null) return (false, "Shipment not found.");
        if (existing.IsFinalized) return (false, "Finalized shipments are locked until Phase 3 correction workflows.");

        existing.ShippingAgent = header.ShippingAgent.Trim();
        existing.ChinaWarehouseReceivedDate = header.ChinaWarehouseReceivedDate;
        existing.ChinaDepartureDate = header.ChinaDepartureDate;
        existing.KarachiArrivalDate = header.KarachiArrivalDate;
        existing.TarnolDispatchDate = header.TarnolDispatchDate;
        existing.TarnolArrivalDate = header.TarnolArrivalDate;
        existing.Notes = header.Notes;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = userId;
        RecalculateTransitDays(existing);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("Update", nameof(Shipment), existing.Id.ToString(), existing.ShipmentNumber, cancellationToken: cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> ReplaceItemsAsync(
        int shipmentId, IReadOnlyList<ShipmentItemInput> items, string? userId, CancellationToken cancellationToken = default)
    {
        var shipment = await _db.Shipments.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == shipmentId, cancellationToken);
        if (shipment is null) return (false, "Shipment not found.");
        if (shipment.IsFinalized) return (false, "Finalized shipments cannot change items.");

        var itemError = await ValidateItemsAsync(items, cancellationToken);
        if (itemError is not null) return (false, itemError);

        _db.ShipmentItems.RemoveRange(shipment.Items);
        shipment.Items = BuildItems(items, userId);
        shipment.TotalUnits = shipment.Items.Sum(i => i.ExpectedQuantity);
        shipment.UpdatedAt = DateTime.UtcNow;
        shipment.UpdatedBy = userId;
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> AddReceivingSlipAsync(
        int shipmentId, ReceivingSlipInput slip, string? userId, CancellationToken cancellationToken = default)
    {
        var shipment = await _db.Shipments.Include(s => s.ReceivingSlips).FirstOrDefaultAsync(s => s.Id == shipmentId, cancellationToken);
        if (shipment is null) return (false, "Shipment not found.");
        if (shipment.IsFinalized) return (false, "Finalized shipments cannot add slips.");
        if (slip.SupplierId <= 0) return (false, "Factory/supplier is required.");
        if (slip.Cartons < 0 || slip.WeightKg < 0 || slip.Cbm < 0) return (false, "Cartons/weight/CBM cannot be negative.");
        if (!await _db.Suppliers.AnyAsync(s => s.Id == slip.SupplierId && s.IsActive, cancellationToken))
            return (false, "Supplier not found.");

        var slipNumber = string.IsNullOrWhiteSpace(slip.SlipNumber)
            ? await _codes.NextSlipNumberAsync(shipmentId, cancellationToken)
            : slip.SlipNumber.Trim().ToUpperInvariant();

        if (shipment.ReceivingSlips.Any(s => s.SlipNumber == slipNumber))
            return (false, $"Slip '{slipNumber}' already exists on this shipment.");

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        shipment.ReceivingSlips.Add(new ChinaWarehouseReceivingSlip
        {
            SlipNumber = slipNumber,
            ReceivingDate = slip.ReceivingDate.Date,
            SupplierId = slip.SupplierId,
            AgentWarehouse = slip.AgentWarehouse,
            Cartons = slip.Cartons,
            WeightKg = slip.WeightKg,
            Cbm = slip.Cbm,
            TrackingReference = slip.TrackingReference,
            FilePath = slip.FilePath,
            Notes = slip.Notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        });

        RecalculateWarehouseTotals(shipment);
        if (shipment.ChinaWarehouseReceivedDate is null)
            shipment.ChinaWarehouseReceivedDate = slip.ReceivingDate.Date;
        if (shipment.Status == ShipmentStatus.Draft)
            shipment.Status = ShipmentStatus.ChinaWarehouse;

        shipment.UpdatedAt = DateTime.UtcNow;
        shipment.UpdatedBy = userId;

        _db.ShipmentTimelineEvents.Add(new ShipmentTimelineEvent
        {
            ShipmentId = shipment.Id,
            EventType = ShipmentTimelineEventType.ChinaWarehouseReceived,
            OccurredAt = slip.ReceivingDate.Date,
            RelatedStatus = shipment.Status,
            Notes = $"Receiving slip {slipNumber}",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        });

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("AddReceivingSlip", nameof(Shipment), shipment.Id.ToString(), shipment.ShipmentNumber, slipNumber, cancellationToken: cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> UpdateFreightAndKarachiAsync(
        Shipment header, string? userId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Shipments.FirstOrDefaultAsync(s => s.Id == header.Id, cancellationToken);
        if (existing is null) return (false, "Shipment not found.");
        if (existing.IsFinalized) return (false, "Finalized shipments are locked.");

        if (header.AgentBilledWeightKg is < 0 || header.AgentBilledCbm is < 0)
            return (false, "Billed measurements cannot be negative.");
        if (header.RatePerKg is < 0 || header.RatePerCbm is < 0)
            return (false, "Freight rates cannot be negative.");
        if (header.TransportCostPkr < 0 || header.LoadingCostPkr < 0 || header.UnloadingCostPkr < 0 || header.OtherDirectCostPkr < 0)
            return (false, "Direct costs cannot be negative.");

        existing.FreightMethod = header.FreightMethod;
        existing.AgentBilledWeightKg = header.AgentBilledWeightKg;
        existing.RatePerKg = header.RatePerKg;
        existing.AgentBilledCbm = header.AgentBilledCbm;
        existing.RatePerCbm = header.RatePerCbm;
        existing.Transporter = header.Transporter;
        existing.BiltyNumber = header.BiltyNumber;
        existing.TarnolDispatchDate = header.TarnolDispatchDate;
        existing.TarnolArrivalDate = header.TarnolArrivalDate;
        existing.TransportCostPkr = header.TransportCostPkr;
        existing.LoadingCostPkr = header.LoadingCostPkr;
        existing.UnloadingCostPkr = header.UnloadingCostPkr;
        existing.OtherDirectCostPkr = header.OtherDirectCostPkr;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = userId;

        RecalculateFreight(existing);
        RecalculateTransitDays(existing);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("UpdateFreight", nameof(Shipment), existing.Id.ToString(), existing.ShipmentNumber, cancellationToken: cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> ChangeStatusAsync(
        int id, ShipmentStatus newStatus, string? userId, string? reason = null, CancellationToken cancellationToken = default)
    {
        var shipment = await _db.Shipments.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (shipment is null) return (false, "Shipment not found.");
        if (shipment.IsFinalized) return (false, "Finalized shipments are locked.");
        if (newStatus == ShipmentStatus.Received)
            return (false, "Use Phase 3 Finalize & Receive for inventory posting. Status 'Received' is reserved for finalization.");

        var old = shipment.Status;
        if ((int)newStatus < (int)old && string.IsNullOrWhiteSpace(reason))
            return (false, "Status rollback requires a reason.");

        shipment.Status = newStatus;
        shipment.UpdatedAt = DateTime.UtcNow;
        shipment.UpdatedBy = userId;

        _db.ShipmentTimelineEvents.Add(new ShipmentTimelineEvent
        {
            ShipmentId = shipment.Id,
            EventType = MapStatusToEvent(newStatus),
            OccurredAt = DateTime.UtcNow,
            RelatedStatus = newStatus,
            Notes = reason ?? $"Status changed from {old} to {newStatus}",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        });

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("StatusChange", nameof(Shipment), shipment.Id.ToString(), shipment.ShipmentNumber, reason, new { old, newStatus }, cancellationToken: cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> AddTimelineEventAsync(
        int shipmentId, ShipmentTimelineEventType type, DateTime occurredAt, string? notes, string? userId, CancellationToken cancellationToken = default)
    {
        if (!await _db.Shipments.AnyAsync(s => s.Id == shipmentId, cancellationToken))
            return (false, "Shipment not found.");

        _db.ShipmentTimelineEvents.Add(new ShipmentTimelineEvent
        {
            ShipmentId = shipmentId,
            EventType = type,
            OccurredAt = occurredAt,
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        });
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> AddDocumentAsync(
        int shipmentId, ShipmentDocumentType type, string originalName, string storagePath, string? contentType, long size, string? notes, string? userId, CancellationToken cancellationToken = default)
    {
        if (!await _db.Shipments.AnyAsync(s => s.Id == shipmentId, cancellationToken))
            return (false, "Shipment not found.");

        _db.ShipmentDocuments.Add(new ShipmentDocument
        {
            ShipmentId = shipmentId,
            DocumentType = type,
            OriginalFileName = originalName,
            StoragePath = storagePath,
            ContentType = contentType,
            FileSizeBytes = size,
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        });
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    private async Task<string?> ValidateItemsAsync(IReadOnlyList<ShipmentItemInput> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0) return "Add at least one shipment item.";
        foreach (var item in items)
        {
            if (item.ProductId <= 0) return "Each item needs a ProductId/SKU link.";
            if (item.ExpectedQuantity <= 0) return "Expected quantity must be greater than zero.";
            if (!await _db.Products.AnyAsync(p => p.Id == item.ProductId && p.IsActive && p.ProductKind == ProductKind.SingleItem, cancellationToken))
            {
                var isSet = await _db.Products.AnyAsync(p => p.Id == item.ProductId && p.ProductKind == ProductKind.CompleteSet, cancellationToken);
                return isSet
                    ? "A complete set cannot be received on a shipment. Receive its items instead."
                    : $"Product {item.ProductId} not found.";
            }
            if (item.PurchaseOrderId.HasValue &&
                !await _db.PurchaseOrders.AnyAsync(p => p.Id == item.PurchaseOrderId.Value, cancellationToken))
                return $"PO {item.PurchaseOrderId} not found.";
        }
        return null;
    }

    private static List<ShipmentItem> BuildItems(IReadOnlyList<ShipmentItemInput> items, string? userId)
        => items.Select(i => new ShipmentItem
        {
            ProductId = i.ProductId,
            PurchaseOrderId = i.PurchaseOrderId,
            PurchaseOrderItemId = i.PurchaseOrderItemId,
            SupplierId = i.SupplierId,
            ExpectedQuantity = i.ExpectedQuantity,
            UnitPriceRmb = i.UnitPriceRmb,
            Notes = i.Notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        }).ToList();

    private static void RecalculateWarehouseTotals(Shipment shipment)
    {
        shipment.TotalCartons = shipment.ReceivingSlips.Sum(s => s.Cartons);
        shipment.WarehouseWeightKg = Math.Round(shipment.ReceivingSlips.Sum(s => s.WeightKg), 4);
        shipment.WarehouseCbm = Math.Round(shipment.ReceivingSlips.Sum(s => s.Cbm), 6);
    }

    public static void RecalculateFreight(Shipment shipment)
    {
        shipment.FreightAmountPkr = shipment.FreightMethod switch
        {
            FreightMethod.ByKg when shipment.AgentBilledWeightKg is > 0 && shipment.RatePerKg is > 0
                => Math.Round(shipment.AgentBilledWeightKg.Value * shipment.RatePerKg.Value, 2),
            FreightMethod.ByCbm when shipment.AgentBilledCbm is > 0 && shipment.RatePerCbm is > 0
                => Math.Round(shipment.AgentBilledCbm.Value * shipment.RatePerCbm.Value, 2),
            _ => 0m
        };
    }

    public static void RecalculateTransitDays(Shipment shipment)
    {
        shipment.ChinaToKarachiTransitDays =
            shipment.ChinaDepartureDate is not null && shipment.KarachiArrivalDate is not null
                ? (int?)(shipment.KarachiArrivalDate.Value.Date - shipment.ChinaDepartureDate.Value.Date).TotalDays
                : null;

        shipment.WarehouseToTarnolTransitDays =
            shipment.ChinaWarehouseReceivedDate is not null && shipment.TarnolArrivalDate is not null
                ? (int?)(shipment.TarnolArrivalDate.Value.Date - shipment.ChinaWarehouseReceivedDate.Value.Date).TotalDays
                : null;
    }

    private static ShipmentTimelineEventType MapStatusToEvent(ShipmentStatus status) => status switch
    {
        ShipmentStatus.ChinaWarehouse => ShipmentTimelineEventType.ChinaWarehouseReceived,
        ShipmentStatus.InTransit => ShipmentTimelineEventType.InTransit,
        ShipmentStatus.Karachi => ShipmentTimelineEventType.ArrivedKarachi,
        ShipmentStatus.Tarnol => ShipmentTimelineEventType.ReceivedTarnol,
        _ => ShipmentTimelineEventType.StatusChanged
    };
}
