using System.ComponentModel.DataAnnotations;
using UD_Autos.Models.Common;

namespace UD_Autos.Models.Settings;

public class BusinessSetting : AuditableEntity
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string Value { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Description { get; set; }
}
