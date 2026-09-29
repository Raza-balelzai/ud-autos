using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UD_Autos.Models.Common;
using UD_Autos.Models.Suppliers;

namespace UD_Autos.Models.Purchases;

public class PurchaseOrder : AuditableEntity
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string PoNumber { get; set; } = string.Empty;

    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    public DateTime OrderDate { get; set; } = DateTime.UtcNow.Date;

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalRmb { get; set; }

    /// <summary>Estimated PKR conversion for planning (not landed cost).</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedPkr { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? EstimatedExchangeRate { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal PaidRmb { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PaidPkr { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal BalanceRmb { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
}
