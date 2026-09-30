using UD_Autos.Models.Products;
using UD_Autos.Models.Shipments;

namespace UD_Autos.Models.ViewModels;

public class ProductDetailsViewModel
{
    public Product Product { get; set; } = null!;
    public bool CanViewCosts { get; set; }
    public IReadOnlyList<ProductPurchaseHistoryRow> PurchaseHistory { get; set; } = Array.Empty<ProductPurchaseHistoryRow>();
    public IReadOnlyList<ProductShipmentHistoryRow> ShipmentHistory { get; set; } = Array.Empty<ProductShipmentHistoryRow>();
    public IReadOnlyList<ProductSalesHistoryRow> SalesHistory { get; set; } = Array.Empty<ProductSalesHistoryRow>();
}

public class ProductPurchaseHistoryRow
{
    public int PurchaseOrderId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPriceRmb { get; set; }
    public decimal LineTotalRmb { get; set; }
}

public class ProductShipmentHistoryRow
{
    public int ShipmentId { get; set; }
    public string ShipmentNumber { get; set; } = string.Empty;
    public ShipmentStatus Status { get; set; }
    public decimal ExpectedQuantity { get; set; }
    public decimal? ActualQuantity { get; set; }
    public DateTime? ChinaWarehouseReceivedDate { get; set; }
    public DateTime? TarnolArrivalDate { get; set; }
    public decimal LandedUnitCostPkr { get; set; }
}

public class ProductSalesHistoryRow
{
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
