using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UD_Autos.Models.Common;
using UD_Autos.Models.Settings;
using UD_Autos.Models.Suppliers;

namespace UD_Autos.Models.Products;

public class Product : AuditableEntity
{
    public int Id { get; set; }

    [Required, StringLength(60)]
    public string Sku { get; set; } = string.Empty;

    [Required, StringLength(250)]
    public string Name { get; set; } = string.Empty;

    public int? CategoryId { get; set; }
    public ProductCategory? Category { get; set; }

    [StringLength(100)]
    public string? Brand { get; set; }

    [StringLength(100)]
    public string? VehicleBrand { get; set; }

    [StringLength(100)]
    public string? VehicleModel { get; set; }

    [StringLength(100)]
    public string? VariantYear { get; set; }

    public int? UnitId { get; set; }
    public UnitOfMeasure? Unit { get; set; }

    public int? DefaultSupplierId { get; set; }
    public Supplier? DefaultSupplier { get; set; }

    [StringLength(260)]
    public string? ImagePath { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? UnitWeightKg { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? CartonWeightKg { get; set; }

    public int? PiecesPerCarton { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? LengthCm { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? WidthCm { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? HeightCm { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? Cbm { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? PreviousRmbPrice { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? LatestRmbPrice { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? PreviousLandedCost { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? LatestLandedCost { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? AverageCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal WholesalePrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal RetailPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal MinimumSalePrice { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal CurrentStock { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ReservedQuantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal DamagedStock { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ReorderLevel { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal StockValue { get; set; }

    [NotMapped]
    public decimal AvailableQuantity => CurrentStock - ReservedQuantity;

    [StringLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<ProductRmbPriceHistory> RmbPriceHistory { get; set; } = new List<ProductRmbPriceHistory>();
    public ICollection<ProductLandedCostHistory> LandedCostHistory { get; set; } = new List<ProductLandedCostHistory>();
}
