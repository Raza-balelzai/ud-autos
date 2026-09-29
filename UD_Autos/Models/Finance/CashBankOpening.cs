using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UD_Autos.Models.Common;
using UD_Autos.Models.Sales;

namespace UD_Autos.Models.Finance;

/// <summary>Opening balance per cash/bank account as of a date (inclusive starting point for statements).</summary>
public class CashBankOpening : AuditableEntity
{
    public int Id { get; set; }

    public PaymentAccount Account { get; set; } = PaymentAccount.Cash;

    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow.Date;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
