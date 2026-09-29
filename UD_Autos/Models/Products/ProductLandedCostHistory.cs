using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UD_Autos.Models.Common;

namespace UD_Autos.Models.Products;

public class ProductLandedCostHistory : AuditableEntity
{
    public long Id { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [Column(TypeName = "decimal(18,4)")]
    public decimal LandedUnitCostPkr { get; set; }

    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    public string? SourceReference { get; set; }

    [StringLength(100)]
    public string? ShipmentReference { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
