using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UD_Autos.Models.Common;
using UD_Autos.Models.Products;

namespace UD_Autos.Models.Sales;

public class SalesReturnItem : AuditableEntity
{
    public int Id { get; set; }

    public int SalesReturnId { get; set; }
    public SalesReturn SalesReturn { get; set; } = null!;

    public int InvoiceItemId { get; set; }
    public InvoiceItem InvoiceItem { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitCogsPkr { get; set; }

    [StringLength(250)]
    public string? Notes { get; set; }
}
