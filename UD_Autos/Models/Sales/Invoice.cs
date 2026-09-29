using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UD_Autos.Models.Common;
using UD_Autos.Models.Customers;

namespace UD_Autos.Models.Sales;

public class Invoice : AuditableEntity
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string InvoiceNumber { get; set; } = string.Empty;

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow.Date;

    public PaymentMode PaymentMode { get; set; } = PaymentMode.Credit;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Subtotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DeliveryCharge { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OtherCharge { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BillTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PreviousBalance { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PaymentReceived { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal NewBalance { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountPaidOnInvoice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OutstandingAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCogs { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal GrossProfit { get; set; }

    public PaymentAccount? PaymentAccount { get; set; }

    [StringLength(100)]
    public string? PaymentReference { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    /// <summary>Client-generated key to prevent duplicate invoice posts (unique when set).</summary>
    [StringLength(64)]
    public string? IdempotencyKey { get; set; }

    public bool IsVoided { get; set; }

    [StringLength(500)]
    public string? VoidReason { get; set; }

    public DateTime? VoidedAt { get; set; }

    [StringLength(450)]
    public string? VoidedBy { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
    public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();
    public ICollection<SalesReturn> Returns { get; set; } = new List<SalesReturn>();
}
