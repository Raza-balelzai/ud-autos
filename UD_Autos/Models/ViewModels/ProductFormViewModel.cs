using UD_Autos.Models.Customers;
using UD_Autos.Models.Products;
using UD_Autos.Models.Settings;
using UD_Autos.Models.Suppliers;

namespace UD_Autos.Models.ViewModels;

public class ProductFormViewModel
{
    public Product Product { get; set; } = new();
    public IReadOnlyList<ProductCategory> Categories { get; set; } = Array.Empty<ProductCategory>();
    public IReadOnlyList<UnitOfMeasure> Units { get; set; } = Array.Empty<UnitOfMeasure>();
    public IReadOnlyList<Supplier> Suppliers { get; set; } = Array.Empty<Supplier>();
    public string? PriceChangeReason { get; set; }
    public IFormFile? ImageFile { get; set; }
}

public class SettingsIndexViewModel
{
    public IReadOnlyList<ProductCategory> Categories { get; set; } = Array.Empty<ProductCategory>();
    public IReadOnlyList<UnitOfMeasure> Units { get; set; } = Array.Empty<UnitOfMeasure>();
    public IReadOnlyList<ExpenseCategory> ExpenseCategories { get; set; } = Array.Empty<ExpenseCategory>();
    public IReadOnlyList<BusinessSetting> BusinessSettings { get; set; } = Array.Empty<BusinessSetting>();
    public string ActiveTab { get; set; } = "categories";
}
