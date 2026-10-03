using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UD_Autos.Models.Common;

namespace UD_Autos.Models.Customers;

public class Customer : AuditableEntity
{
    public int Id { get; set; }

    /// <summary>Optional on create — auto-generated when blank.</summary>
    [StringLength(40)]
    public string? CustomerCode { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? ShopName { get; set; }

    [StringLength(50)]
    public string? Phone { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    public CustomerType CustomerType { get; set; } = CustomerType.Wholesale;

    [Column(TypeName = "decimal(18,2)")]
    public decimal? CreditLimit { get; set; }

    /// <summary>Opening balance used only for migration; not a freely editable live balance.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal OpeningBalance { get; set; }

    /// <summary>Derived summary — updated by ledger/payment workflows in later phases.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal CurrentBalance { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPurchases { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPayments { get; set; }

    public DateTime? LastPurchaseAt { get; set; }
    public DateTime? LastPaymentAt { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
}
