using System.ComponentModel.DataAnnotations.Schema;
using UD_Autos.Models.Common;
using UD_Autos.Models.Products;

namespace UD_Autos.Models.Sales;

/// <summary>Component quantities captured when a complete set was sold.</summary>
public class InvoiceSetComponent : AuditableEntity
{
    public int Id { get; set; }

    public int InvoiceItemId { get; set; }
    public InvoiceItem InvoiceItem { get; set; } = null!;

    public int ComponentProductId { get; set; }
    public Product ComponentProduct { get; set; } = null!;

    [Column(TypeName = "decimal(18,4)")]
    public decimal RequiredQuantityPerSet { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal QuantityConsumed { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ReturnedQuantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitCogsPkr { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineCogsPkr { get; set; }

    public ICollection<InvoiceSetComponentBatchAllocation> BatchAllocations { get; set; } = new List<InvoiceSetComponentBatchAllocation>();
}
