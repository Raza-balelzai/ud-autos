using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UD_Autos.Models.Common;
using UD_Autos.Models.Inventory;

namespace UD_Autos.Models.Shipments;

public class Shipment : AuditableEntity
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string ShipmentNumber { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string ShippingAgent { get; set; } = string.Empty;

    public ShipmentStatus Status { get; set; } = ShipmentStatus.Draft;

    public DateTime? ChinaWarehouseReceivedDate { get; set; }
    public DateTime? ChinaDepartureDate { get; set; }
    public DateTime? KarachiArrivalDate { get; set; }
    public DateTime? TarnolDispatchDate { get; set; }
    public DateTime? TarnolArrivalDate { get; set; }

    public int TotalCartons { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalUnits { get; set; }

    /// <summary>Sum of warehouse-slip weight (verification baseline).</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal WarehouseWeightKg { get; set; }

    /// <summary>Sum of warehouse-slip CBM (verification baseline).</summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal WarehouseCbm { get; set; }

    public FreightMethod FreightMethod { get; set; } = FreightMethod.ByKg;

    [Column(TypeName = "decimal(18,4)")]
    public decimal? AgentBilledWeightKg { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? RatePerKg { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? AgentBilledCbm { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? RatePerCbm { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal FreightAmountPkr { get; set; }

    [StringLength(150)]
    public string? Transporter { get; set; }

    [StringLength(80)]
    public string? BiltyNumber { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TransportCostPkr { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LoadingCostPkr { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnloadingCostPkr { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OtherDirectCostPkr { get; set; }

    /// <summary>Optional agent fees beyond China→Karachi freight.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal AgentChargesPkr { get; set; }

    /// <summary>RMB→PKR rate used for China product cost in landed-cost calculation.</summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal? CostingExchangeRate { get; set; }

    public LandedCostAllocationMethod? AllocationMethod { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ChinaProductCostPkr { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal HawalaDirectChargesPkr { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalDirectCostPkr { get; set; }

    public DateTime? LandedCostCalculatedAt { get; set; }

    public bool IsLandedCostLocked { get; set; }

    /// <summary>China departure → Karachi arrival days.</summary>
    public int? ChinaToKarachiTransitDays { get; set; }

    /// <summary>China warehouse received → Tarnol arrival days.</summary>
    public int? WarehouseToTarnolTransitDays { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    /// <summary>Phase 3 finalization lock — inventory posted once.</summary>
    public bool IsFinalized { get; set; }

    public DateTime? FinalizedAt { get; set; }

    [StringLength(450)]
    public string? FinalizedBy { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public ICollection<ChinaWarehouseReceivingSlip> ReceivingSlips { get; set; } = new List<ChinaWarehouseReceivingSlip>();
    public ICollection<ShipmentItem> Items { get; set; } = new List<ShipmentItem>();
    public ICollection<ShipmentDocument> Documents { get; set; } = new List<ShipmentDocument>();
    public ICollection<ShipmentTimelineEvent> TimelineEvents { get; set; } = new List<ShipmentTimelineEvent>();
}
