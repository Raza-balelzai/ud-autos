using System.ComponentModel.DataAnnotations;
using UD_Autos.Models.Common;

namespace UD_Autos.Models.Settings;

public class ExpenseCategory : AuditableEntity
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Description { get; set; }

    /// <summary>
    /// When true, this category may represent direct import costs that must not double-count with landed cost.
    /// </summary>
    public bool IsDirectImportCost { get; set; }

    public bool IsActive { get; set; } = true;
}
