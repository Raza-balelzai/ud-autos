using System.ComponentModel.DataAnnotations;
using UD_Autos.Models.Common;

namespace UD_Autos.Models.Shipments;

public class ShipmentTimelineEvent : AuditableEntity
{
    public long Id { get; set; }

    public int ShipmentId { get; set; }
    public Shipment Shipment { get; set; } = null!;

    public ShipmentTimelineEventType EventType { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    [StringLength(500)]
    public string? Notes { get; set; }

    public ShipmentStatus? RelatedStatus { get; set; }
}
