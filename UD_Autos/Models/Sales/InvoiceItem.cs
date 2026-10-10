using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UD_Autos.Models.Common;
using UD_Autos.Models.Products;

namespace UD_Autos.Models.Sales;

public class InvoiceItem : AuditableEntity
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ReturnedQuantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineDiscount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; }

    /// <summary>Reproducible COGS from batch allocations at sale time.</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitCogsPkr { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineCogsPkr { get; set; }

    [StringLength(250)]
    public string? Notes { get; set; }

    public ICollection<InvoiceItemBatchAllocation> BatchAllocations { get; set; } = new List<InvoiceItemBatchAllocation>();
    public ICollection<InvoiceSetComponent> SetComponents { get; set; } = new List<InvoiceSetComponent>();
}
