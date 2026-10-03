using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UD_Autos.Models.Common;
using UD_Autos.Models.Products;

namespace UD_Autos.Models.Suppliers;

public class Supplier : AuditableEntity
{
    public int Id { get; set; }

    /// <summary>Optional on create — auto-generated when blank.</summary>
    [StringLength(40)]
    public string? SupplierCode { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(150)]
    public string? ContactPerson { get; set; }

    [StringLength(100)]
    public string? WeChat { get; set; }

    [StringLength(50)]
    public string? Phone { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? Country { get; set; } = "China";

    [StringLength(500)]
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Derived summary fields — not manually editable as source of truth.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPurchasesRmb { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPaymentsPkr { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OutstandingBalancePkr { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
