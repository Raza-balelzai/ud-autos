using System.ComponentModel.DataAnnotations;
using UD_Autos.Models.Common;

namespace UD_Autos.Models.Settings;

public class ProductCategory : AuditableEntity
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}
