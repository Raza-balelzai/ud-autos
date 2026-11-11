using System.ComponentModel.DataAnnotations;
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
    public List<SetComponentFormRow> Components { get; set; } = new();
    public decimal SetsToAdd { get; set; }
    public List<PairSideFormRow> PairSides { get; set; } = new();
    public string? ShortSetName { get; set; }
    public decimal ShortSetQuantity { get; set; }
    public decimal ShortSetWholesalePrice { get; set; }
    public decimal ShortSetRetailPrice { get; set; }
    public List<int> ShortSetItemIds { get; set; } = new();
    public bool ShortSetPosted { get; set; }
}

public class PairSideFormRow
{
    public int ComponentProductId { get; set; }
    public string? Name { get; set; }
    public decimal Quantity { get; set; }
    public decimal RetailPrice { get; set; }
}

public class SetComponentFormRow
{
    public int ComponentProductId { get; set; }
    public string? Sku { get; set; }
    public string? Name { get; set; }
    public decimal RequiredQuantity { get; set; } = 1;
    public decimal AvailableStock { get; set; }
}

public class SettingsIndexViewModel
{
    public IReadOnlyList<ProductCategory> Categories { get; set; } = Array.Empty<ProductCategory>();
    public IReadOnlyList<UnitOfMeasure> Units { get; set; } = Array.Empty<UnitOfMeasure>();
    public IReadOnlyList<ExpenseCategory> ExpenseCategories { get; set; } = Array.Empty<ExpenseCategory>();
    public IReadOnlyList<BusinessSetting> BusinessSettings { get; set; } = Array.Empty<BusinessSetting>();
    public MailSettingsForm Mail { get; set; } = new();
    public string ActiveTab { get; set; } = "categories";
}

public class MailSettingsForm
{
    [Required, StringLength(200)]
    public string Host { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Range(1, 65535)]
    public int Port { get; set; } = 587;

    [StringLength(200)]
    public string UserName { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Password { get; set; }

    public bool HasPassword { get; set; }

    [Required, EmailAddress, StringLength(200)]
    public string FromAddress { get; set; } = string.Empty;

    [StringLength(200)]
    public string FromName { get; set; } = string.Empty;

    public bool EnableSsl { get; set; } = true;
}
