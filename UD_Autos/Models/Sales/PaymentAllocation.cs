using System.ComponentModel.DataAnnotations.Schema;
using UD_Autos.Models.Common;

namespace UD_Autos.Models.Sales;

public class PaymentAllocation : AuditableEntity
{
    public long Id { get; set; }

    public int CustomerPaymentId { get; set; }
    public CustomerPayment CustomerPayment { get; set; } = null!;

    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }
}
