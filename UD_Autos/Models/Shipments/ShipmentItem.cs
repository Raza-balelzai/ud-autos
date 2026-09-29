using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UD_Autos.Models.Common;
using UD_Autos.Models.Products;
using UD_Autos.Models.Purchases;
using UD_Autos.Models.Suppliers;

namespace UD_Autos.Models.Shipments;

public class ShipmentItem : AuditableEntity
{
    public int Id { get; set; }

    public int ShipmentId { get; set; }
    public Shipment Shipment { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int? PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    public int? PurchaseOrderItemId { get; set; }
    public PurchaseOrderItem? PurchaseOrderItem { get; set; }

    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ExpectedQuantity { get; set; }

    /// <summary>Actual received quantity is owned by Phase 3 final receiving.</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? ActualQuantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? UnitPriceRmb { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? AllocationWeightKg { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? AllocationCbm { get; set; }

    /// <summary>Custom allocation share (0–100). Must total ~100 when method is Custom.</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? CustomAllocationPercent { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ChinaProductCostPkr { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AllocatedDirectCostPkr { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal LandedUnitCostPkr { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal AllocationBasisValue { get; set; }

    [StringLength(250)]
    public string? Notes { get; set; }
}
