using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UD_Autos.Models.Common;
using UD_Autos.Models.Products;
using UD_Autos.Models.Shipments;

namespace UD_Autos.Models.Inventory;

public class InventoryBatch : AuditableEntity
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string BatchNumber { get; set; } = string.Empty;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int ShipmentId { get; set; }
    public Shipment Shipment { get; set; } = null!;

    public int ShipmentItemId { get; set; }
    public ShipmentItem ShipmentItem { get; set; } = null!;

    [Column(TypeName = "decimal(18,4)")]
    public decimal ReceivedQuantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal RemainingQuantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ReservedQuantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal DamagedQuantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal LandedUnitCostPkr { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    public bool IsLocked { get; set; } = true;

    [StringLength(250)]
    public string? Notes { get; set; }
}
