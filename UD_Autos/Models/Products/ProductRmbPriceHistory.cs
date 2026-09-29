using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UD_Autos.Models.Common;
using UD_Autos.Models.Suppliers;

namespace UD_Autos.Models.Products;

public class ProductRmbPriceHistory : AuditableEntity
{
    public long Id { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitPriceRmb { get; set; }

    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    public string? SourceReference { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
