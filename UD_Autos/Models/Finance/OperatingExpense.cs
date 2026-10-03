using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UD_Autos.Models.Common;
using UD_Autos.Models.Sales;
using UD_Autos.Models.Settings;

namespace UD_Autos.Models.Finance;

public class OperatingExpense : AuditableEntity
{
    public int Id { get; set; }

    /// <summary>Auto-generated on create.</summary>
    [StringLength(40)]
    public string? ExpenseNumber { get; set; }

    public DateTime ExpenseDate { get; set; } = DateTime.UtcNow.Date;

    public int CategoryId { get; set; }
    public ExpenseCategory Category { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public PaymentAccount Account { get; set; } = PaymentAccount.Cash;

    [StringLength(100)]
    public string? Reference { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    /// When category is marked direct-import, posting requires explicit acknowledgement
    /// so capitalized shipment costs are not double-counted as opex.
    /// </summary>
    public bool AcknowledgedImportRisk { get; set; }
}
