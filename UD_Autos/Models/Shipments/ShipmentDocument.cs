using System.ComponentModel.DataAnnotations;
using UD_Autos.Models.Common;

namespace UD_Autos.Models.Shipments;

public class ShipmentDocument : AuditableEntity
{
    public int Id { get; set; }

    public int ShipmentId { get; set; }
    public Shipment Shipment { get; set; } = null!;

    public ShipmentDocumentType DocumentType { get; set; } = ShipmentDocumentType.Other;

    [Required, StringLength(200)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required, StringLength(260)]
    public string StoragePath { get; set; } = string.Empty;

    [StringLength(100)]
    public string? ContentType { get; set; }

    public long FileSizeBytes { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
