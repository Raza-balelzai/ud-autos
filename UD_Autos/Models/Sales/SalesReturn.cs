using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UD_Autos.Models.Common;
using UD_Autos.Models.Customers;

namespace UD_Autos.Models.Sales;

public class SalesReturn : AuditableEntity
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string ReturnNumber { get; set; } = string.Empty;

    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public DateTime ReturnDate { get; set; } = DateTime.UtcNow.Date;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    /// <summary>True = reduce khata; false = cash/bank refund.</summary>
    public bool AdjustKhata { get; set; } = true;

    public PaymentAccount? RefundAccount { get; set; }

    [Required, StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    public ICollection<SalesReturnItem> Items { get; set; } = new List<SalesReturnItem>();
}
