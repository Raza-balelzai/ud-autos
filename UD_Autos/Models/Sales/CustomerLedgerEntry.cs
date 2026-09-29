using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UD_Autos.Models.Common;
using UD_Autos.Models.Customers;

namespace UD_Autos.Models.Sales;

public class CustomerLedgerEntry : AuditableEntity
{
    public long Id { get; set; }

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public DateTime EntryDate { get; set; } = DateTime.UtcNow.Date;

    public LedgerEntryType EntryType { get; set; }

    [Required, StringLength(250)]
    public string Detail { get; set; } = string.Empty;

    [StringLength(80)]
    public string? ReferenceType { get; set; }

    [StringLength(80)]
    public string? ReferenceId { get; set; }

    [StringLength(80)]
    public string? ReferenceNumber { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Debit { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Credit { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal RunningBalance { get; set; }
}
