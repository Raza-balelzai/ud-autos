using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UD_Autos.Models.Common;
using UD_Autos.Models.Purchases;
using UD_Autos.Models.Sales;
using UD_Autos.Models.Suppliers;

namespace UD_Autos.Models.Hawala;

public class HawalaPayment : AuditableEntity
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string PaymentNumber { get; set; } = string.Empty;

    public DateTime PaymentDate { get; set; } = DateTime.UtcNow.Date;

    /// <summary>Local cash/bank account that paid PKR (Phase 5 cash book).</summary>
    public PaymentAccount Account { get; set; } = PaymentAccount.Cash;

    [Required, StringLength(150)]
    public string HawalaPerson { get; set; } = string.Empty;

    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    public int? PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    /// <summary>RMB received by factory in China.</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal RmbReceived { get; set; }

    /// <summary>PKR paid locally for this transfer.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal PkrPaid { get; set; }

    /// <summary>Effective rate = PkrPaid / RmbReceived.</summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal EffectiveRmbRate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal HawalaFee { get; set; }

    /// <summary>Marks charge as identifiable for later landed-cost inclusion.</summary>
    public bool IsDirectImportCharge { get; set; } = true;

    [StringLength(100)]
    public string? Reference { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
