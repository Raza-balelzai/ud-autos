using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UD_Autos.Models.Common;
using UD_Autos.Models.Inventory;
using UD_Autos.Models.Products;
using UD_Autos.Models.Shipments;
using UD_Autos.Models.Suppliers;

namespace UD_Autos.Models.Inventory;

public class SupplierReturn : AuditableEntity
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string ReturnNumber { get; set; } = string.Empty;

    public DateTime ReturnDate { get; set; } = DateTime.UtcNow.Date;

    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    public int? ShipmentId { get; set; }
    public Shipment? Shipment { get; set; }

    [Required, StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalQuantity { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public ICollection<SupplierReturnItem> Items { get; set; } = new List<SupplierReturnItem>();
}

public class SupplierReturnItem : AuditableEntity
{
    public int Id { get; set; }

    public int SupplierReturnId { get; set; }
    public SupplierReturn SupplierReturn { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int InventoryBatchId { get; set; }
    public InventoryBatch InventoryBatch { get; set; } = null!;

    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitCostPkr { get; set; }

    [StringLength(250)]
    public string? Notes { get; set; }
}
