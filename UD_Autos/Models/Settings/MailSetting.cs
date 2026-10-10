using System.ComponentModel.DataAnnotations;
using UD_Autos.Models.Common;

namespace UD_Autos.Models.Settings;

public class MailSetting : AuditableEntity
{
    public int Id { get; set; }

    [StringLength(200)]
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    [StringLength(200)]
    public string UserName { get; set; } = string.Empty;

    public string? ProtectedPassword { get; set; }

    [StringLength(200)]
    public string FromAddress { get; set; } = string.Empty;

    [StringLength(200)]
    public string FromName { get; set; } = string.Empty;

    public bool EnableSsl { get; set; } = true;
}
