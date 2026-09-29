using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UD_Autos.Models.Common;
using UD_Autos.Models.Products;

namespace UD_Autos.Models.Inventory;

public class StockMovement : AuditableEntity
{
    public long Id { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int? InventoryBatchId { get; set; }
    public InventoryBatch? InventoryBatch { get; set; }

    public StockMovementType MovementType { get; set; }

    /// <summary>Signed quantity: + inbound, − outbound.</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitCostPkr { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal QuantityAfter { get; set; }

    [StringLength(80)]
    public string? ReferenceType { get; set; }

    [StringLength(80)]
    public string? ReferenceId { get; set; }

    [StringLength(80)]
    public string? ReferenceNumber { get; set; }

    [Required, StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
