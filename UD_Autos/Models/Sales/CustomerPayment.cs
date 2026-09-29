using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UD_Autos.Models.Common;
using UD_Autos.Models.Customers;

namespace UD_Autos.Models.Sales;

public class CustomerPayment : AuditableEntity
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string PaymentNumber { get; set; } = string.Empty;

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public DateTime PaymentDate { get; set; } = DateTime.UtcNow.Date;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public PaymentAccount Account { get; set; } = PaymentAccount.Cash;

    [StringLength(100)]
    public string? Reference { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    /// <summary>When created as part of an invoice save.</summary>
    public int? SourceInvoiceId { get; set; }
    public Invoice? SourceInvoice { get; set; }

    public bool IsManualAllocation { get; set; }

    public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();
}
