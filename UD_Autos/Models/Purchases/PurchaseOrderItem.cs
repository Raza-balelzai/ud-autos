using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UD_Autos.Models.Common;
using UD_Autos.Models.Products;

namespace UD_Autos.Models.Purchases;

public class PurchaseOrderItem : AuditableEntity
{
    public int Id { get; set; }

    public int PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitPriceRmb { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal LineTotalRmb { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedLinePkr { get; set; }

    [StringLength(250)]
    public string? Notes { get; set; }
}
