using System.ComponentModel.DataAnnotations.Schema;
using UD_Autos.Models.Common;

namespace UD_Autos.Models.Products;

public class ProductSetComponent : AuditableEntity
{
    public int Id { get; set; }

    public int SetProductId { get; set; }
    public Product SetProduct { get; set; } = null!;

    public int ComponentProductId { get; set; }
    public Product ComponentProduct { get; set; } = null!;

    [Column(TypeName = "decimal(18,4)")]
    public decimal RequiredQuantity { get; set; }

    public bool IsActive { get; set; } = true;
}
