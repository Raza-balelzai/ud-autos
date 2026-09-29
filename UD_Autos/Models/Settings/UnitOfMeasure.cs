using System.ComponentModel.DataAnnotations;
using UD_Autos.Models.Common;

namespace UD_Autos.Models.Settings;

public class UnitOfMeasure : AuditableEntity
{
    public int Id { get; set; }

    [Required, StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Abbreviation { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
