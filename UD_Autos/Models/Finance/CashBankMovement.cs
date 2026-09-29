using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UD_Autos.Models.Common;
using UD_Autos.Models.Sales;

namespace UD_Autos.Models.Finance;

/// <summary>
/// Cash/bank book line. Linked source transactions (collections, hawala, expenses, refunds)
/// are referenced — not manually re-keyed. Manual OtherReceipt/OtherPayment are entered here.
/// </summary>
public class CashBankMovement : AuditableEntity
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string MovementNumber { get; set; } = string.Empty;

    public DateTime MovementDate { get; set; } = DateTime.UtcNow.Date;

    public PaymentAccount Account { get; set; } = PaymentAccount.Cash;

    public CashBankMovementType MovementType { get; set; }

    public CashBankDirection Direction { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [StringLength(80)]
    public string? ReferenceType { get; set; }

    [StringLength(40)]
    public string? ReferenceId { get; set; }

    [StringLength(80)]
    public string? ReferenceNumber { get; set; }

    [StringLength(300)]
    public string Detail { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Notes { get; set; }

    /// <summary>True when posted automatically from sales/payments/hawala/expense modules.</summary>
    public bool IsLinked { get; set; }
}
