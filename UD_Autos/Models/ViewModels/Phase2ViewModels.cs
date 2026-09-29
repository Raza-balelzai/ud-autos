using System.ComponentModel.DataAnnotations;
using UD_Autos.Models.Hawala;
using UD_Autos.Models.Products;
using UD_Autos.Models.Purchases;
using UD_Autos.Models.Shipments;
using UD_Autos.Models.Suppliers;
using UD_Autos.Services;

namespace UD_Autos.Models.ViewModels;

public class PurchaseOrderFormViewModel
{
    public PurchaseOrder PurchaseOrder { get; set; } = new();
    public List<PurchaseOrderItemInput> Items { get; set; } = new();
    public IReadOnlyList<Supplier> Suppliers { get; set; } = Array.Empty<Supplier>();
    public IReadOnlyList<Product> Products { get; set; } = Array.Empty<Product>();
}

public class HawalaFormViewModel
{
    public HawalaPayment Payment { get; set; } = new();
    public IReadOnlyList<Supplier> Suppliers { get; set; } = Array.Empty<Supplier>();
    public IReadOnlyList<PurchaseOrder> PurchaseOrders { get; set; } = Array.Empty<PurchaseOrder>();
}

public class ShipmentFormViewModel
{
    public Shipment Shipment { get; set; } = new();
    public List<ShipmentItemInput> Items { get; set; } = new();
    public IReadOnlyList<Product> Products { get; set; } = Array.Empty<Product>();
    public IReadOnlyList<PurchaseOrder> PurchaseOrders { get; set; } = Array.Empty<PurchaseOrder>();
    public IReadOnlyList<Supplier> Suppliers { get; set; } = Array.Empty<Supplier>();
}

public class ReceivingSlipFormViewModel
{
    public int ShipmentId { get; set; }
    public string ShipmentNumber { get; set; } = string.Empty;
    public ReceivingSlipInput Slip { get; set; } = new();
    public IReadOnlyList<Supplier> Suppliers { get; set; } = Array.Empty<Supplier>();
    public IFormFile? SlipFile { get; set; }
}

public class ShipmentDocumentUploadViewModel
{
    public int ShipmentId { get; set; }
    public ShipmentDocumentType DocumentType { get; set; } = ShipmentDocumentType.Other;
    public string? Notes { get; set; }

    [Required]
    public IFormFile? File { get; set; }
}
