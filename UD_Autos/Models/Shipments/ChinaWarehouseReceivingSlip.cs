using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UD_Autos.Models.Common;
using UD_Autos.Models.Suppliers;

namespace UD_Autos.Models.Shipments;

public class ChinaWarehouseReceivingSlip : AuditableEntity
{
    public int Id { get; set; }

    public int ShipmentId { get; set; }
    public Shipment Shipment { get; set; } = null!;

    [Required, StringLength(40)]
    public string SlipNumber { get; set; } = string.Empty;

    public DateTime ReceivingDate { get; set; } = DateTime.UtcNow.Date;

    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    [StringLength(150)]
    public string? AgentWarehouse { get; set; }

    public int Cartons { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal WeightKg { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal Cbm { get; set; }

    [StringLength(100)]
    public string? TrackingReference { get; set; }

    [StringLength(260)]
    public string? FilePath { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
