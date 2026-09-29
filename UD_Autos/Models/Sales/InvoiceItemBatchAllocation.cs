using System.ComponentModel.DataAnnotations.Schema;
using UD_Autos.Models.Common;
using UD_Autos.Models.Inventory;

namespace UD_Autos.Models.Sales;

public class InvoiceItemBatchAllocation : AuditableEntity
{
    public long Id { get; set; }

    public int InvoiceItemId { get; set; }
    public InvoiceItem InvoiceItem { get; set; } = null!;

    public int InventoryBatchId { get; set; }
    public InventoryBatch InventoryBatch { get; set; } = null!;

    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitCostPkr { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineCostPkr { get; set; }
}
