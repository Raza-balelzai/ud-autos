using System.ComponentModel.DataAnnotations;
using UD_Autos.Services;

namespace UD_Autos.Models.ViewModels;

public class SupplierReturnFormViewModel
{
    public int SupplierId { get; set; }
    public DateTime ReturnDate { get; set; } = DateTime.UtcNow.Date;
    public int? ShipmentId { get; set; }

    [Required, StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Notes { get; set; }

    public List<SupplierReturnLineInput> Lines { get; set; } = new() { new() };
}
