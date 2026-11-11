using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UD_Autos.Authorization;
using UD_Autos.Data;
using UD_Autos.Models.Products;
using UD_Autos.Models.ViewModels;
using UD_Autos.Services;

namespace UD_Autos.Controllers;

[Authorize(Policy = AppPolicies.ManageProducts)]
public class ProductsController : Controller
{
    private readonly IProductService _products;
    private readonly ISettingsService _settings;
    private readonly ISupplierService _suppliers;
    private readonly IWebHostEnvironment _env;
    private readonly ApplicationDbContext _db;

    public ProductsController(
        IProductService products,
        ISettingsService settings,
        ISupplierService suppliers,
        IWebHostEnvironment env,
        ApplicationDbContext db)
    {
        _products = products;
        _settings = settings;
        _suppliers = suppliers;
        _env = env;
        _db = db;
    }

    public async Task<IActionResult> Index(string? search, int? categoryId, string? sortBy, bool sortDesc = false, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Products";
        ViewData["Subtitle"] = "Product master — create once, reference everywhere.";
        var result = await _products.SearchAsync(search, categoryId, sortBy, sortDesc, page, 20, cancellationToken);
        ViewBag.Categories = await _settings.GetCategoriesAsync(cancellationToken: cancellationToken);
        ViewBag.CanViewCosts = User.IsInRole(AppRoles.Owner) || User.IsInRole(AppRoles.Manager) || User.IsInRole(AppRoles.Accountant);
        return View(result);
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken = default)
    {
        var product = await _products.GetByIdAsync(id, includeHistory: true, cancellationToken);
        if (product is null) return NotFound();

        var (purchases, shipments, sales) = await _products.GetHistoryAsync(id, cancellationToken);
        var canViewCosts = User.IsInRole(AppRoles.Owner) || User.IsInRole(AppRoles.Manager) || User.IsInRole(AppRoles.Accountant);

        ViewData["Title"] = product.Name;
        ViewData["Subtitle"] = product.Sku;
        return View(new ProductDetailsViewModel
        {
            Product = product,
            CanViewCosts = canViewCosts,
            PurchaseHistory = purchases,
            ShipmentHistory = shipments,
            SalesHistory = sales
        });
    }

    public async Task<IActionResult> Create(CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Add Product";
        return View("Form", await BuildFormAsync(new Product(), cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductFormViewModel model, CancellationToken cancellationToken = default)
    {
        // SKU is optional on create (auto-generated); ignore NRT-driven required validation noise.
        if (string.IsNullOrWhiteSpace(model.Product.Sku))
            ModelState.Remove("Product.Sku");

        IgnoreBlankOptionalNumbers();
        var unitKey = await ShapeMeasureFieldsAsync(model.Product, isCreate: true, cancellationToken);
        var isPair = unitKey == "pair";
        var isSet = UnitRoles.IsCompleteSet(unitKey);
        if (!isSet)
            model.SetsToAdd = 0;
        if (model.SetsToAdd < 0)
            ModelState.AddModelError(nameof(model.SetsToAdd), "Sets to add cannot be negative.");
        if (isSet && model.ShortSetQuantity < 0)
            ModelState.AddModelError(nameof(model.ShortSetQuantity), "Short set quantity cannot be negative.");
        if (model.Product.ProductKind != ProductKind.CompleteSet && model.Product.CurrentStock < 0)
            ModelState.AddModelError("Product.CurrentStock", "Quantity cannot be negative.");

        if (!ModelState.IsValid)
        {
            return View("Form", await BuildFormAsync(model.Product, cancellationToken, model.Components, model.PairSides, usePostedSides: true, setsToAdd: model.SetsToAdd, carry: model));
        }

        var uploaded = await ApplyImageAsync(model);
        if (!ModelState.IsValid)
        {
            if (uploaded)
                ProductImageFiles.TryDelete(_env, model.Product.ImagePath);
            return View("Form", await BuildFormAsync(model.Product, cancellationToken, model.Components, model.PairSides, usePostedSides: true, setsToAdd: model.SetsToAdd, carry: model));
        }

        var components = isSet ? ComponentsOf(model) : null;
        var pairSides = isPair ? PairSidesOf(model) : null;
        var shortSet = ShortSetOf(model, isSet);
        var (ok, error, product) = await _products.CreateAsync(model.Product, User.GetUserId(), cancellationToken, components, isSet ? model.SetsToAdd : 0, pairSides, shortSet);
        if (!ok)
        {
            if (uploaded)
                ProductImageFiles.TryDelete(_env, model.Product.ImagePath);
            AddServiceError(error);
            return View("Form", await BuildFormAsync(model.Product, cancellationToken, model.Components, model.PairSides, usePostedSides: true, setsToAdd: model.SetsToAdd, carry: model));
        }

        TempData["Success"] = isPair
            ? $"Pair {product!.Sku} saved."
            : isSet
                ? shortSet is null
                    ? $"Complete set {product!.Sku} saved."
                    : $"Complete set {product!.Sku} saved. Short set stock updated."
                : $"Product {product!.Sku} created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken = default)
    {
        var product = await _products.GetByIdAsync(id, cancellationToken: cancellationToken);
        if (product is null) return NotFound();
        ViewData["Title"] = "Edit Product";
        return View("Form", await BuildFormAsync(product, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProductFormViewModel model, CancellationToken cancellationToken = default)
    {
        if (id != model.Product.Id) return BadRequest();

        var stockPosted = ModelState.TryGetValue("Product.CurrentStock", out var stockEntry)
            && !string.IsNullOrWhiteSpace(stockEntry.AttemptedValue);
        IgnoreBlankOptionalNumbers();
        var unitKey = await ShapeMeasureFieldsAsync(model.Product, isCreate: false, cancellationToken);
        var isPair = unitKey == "pair";
        var isSet = UnitRoles.IsCompleteSet(unitKey);
        var adjustStock = stockPosted && unitKey is "piece" or "kilogram" or "carton";
        if (!isSet)
            model.SetsToAdd = 0;
        if (model.SetsToAdd < 0)
            ModelState.AddModelError(nameof(model.SetsToAdd), "Sets to add cannot be negative.");
        if (isSet && model.ShortSetQuantity < 0)
            ModelState.AddModelError(nameof(model.ShortSetQuantity), "Short set quantity cannot be negative.");
        if (adjustStock && model.Product.CurrentStock < 0)
            ModelState.AddModelError("Product.CurrentStock", "Quantity cannot be negative.");

        if (!ModelState.IsValid)
        {
            return View("Form", await BuildFormAsync(model.Product, cancellationToken, model.Components, model.PairSides, usePostedSides: true, setsToAdd: model.SetsToAdd, carry: model));
        }

        string? previousImage = null;
        if (model.ImageFile is { Length: > 0 })
        {
            previousImage = await _db.Products.AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => p.ImagePath)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var uploaded = await ApplyImageAsync(model);
        if (!ModelState.IsValid)
        {
            if (uploaded)
                ProductImageFiles.TryDelete(_env, model.Product.ImagePath);
            return View("Form", await BuildFormAsync(model.Product, cancellationToken, model.Components, model.PairSides, usePostedSides: true, setsToAdd: model.SetsToAdd, carry: model));
        }

        var components = isSet ? ComponentsOf(model) : null;
        var pairSides = isPair ? PairSidesOf(model) : null;
        var shortSet = ShortSetOf(model, isSet);
        var (ok, error) = await _products.UpdateAsync(model.Product, User.GetUserId(), model.PriceChangeReason, cancellationToken, components, isSet ? model.SetsToAdd : 0, pairSides, shortSet, adjustStock);
        if (!ok)
        {
            if (uploaded)
                ProductImageFiles.TryDelete(_env, model.Product.ImagePath);
            AddServiceError(error);
            return View("Form", await BuildFormAsync(model.Product, cancellationToken, model.Components, model.PairSides, usePostedSides: true, setsToAdd: model.SetsToAdd, carry: model));
        }

        if (uploaded
            && !string.IsNullOrWhiteSpace(previousImage)
            && !string.Equals(previousImage, model.Product.ImagePath, StringComparison.OrdinalIgnoreCase))
        {
            ProductImageFiles.TryDelete(_env, previousImage);
        }

        TempData["Success"] = isPair ? "Pair updated." : isSet ? shortSet is null ? "Complete set updated." : "Complete set updated. Short set stock updated." : "Product updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
    {
        var (ok, error) = await _products.SoftDeleteAsync(id, User.GetUserId(), cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok ? "Product deactivated." : error;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> SearchComponents(string? q, CancellationToken cancellationToken = default)
    {
        q = (q ?? string.Empty).Trim();
        var query = _db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.ProductKind == ProductKind.SingleItem);
        if (q.Length > 0)
        {
            query = query.Where(p =>
                p.Sku.Contains(q) ||
                p.Name.Contains(q) ||
                (p.Barcode != null && p.Barcode.Contains(q)));
        }

        var rows = await query.OrderBy(p => p.Name).Take(20)
            .Select(p => new
            {
                p.Id,
                p.Sku,
                p.Name,
                Available = p.CurrentStock - p.ReservedQuantity
            })
            .ToListAsync(cancellationToken);
        return Json(rows);
    }

    private static readonly string[] OptionalNumberKeys =
    [
        "Product.WholesalePrice",
        "Product.RetailPrice",
        "Product.MinimumSalePrice",
        "Product.ReorderLevel",
        "Product.CurrentStock",
        "Product.UnitWeightKg",
        "Product.CartonWeightKg",
        "Product.PiecesPerCarton",
        "Product.LengthCm",
        "Product.WidthCm",
        "Product.HeightCm",
        "Product.LatestRmbPrice",
        "Product.LatestLandedCost",
        "Product.CategoryId",
        "Product.UnitId",
        "Product.DefaultSupplierId",
        "SetsToAdd",
        "ShortSetQuantity",
        "ShortSetWholesalePrice",
        "ShortSetRetailPrice"
    ];

    private void IgnoreBlankOptionalNumbers()
    {
        foreach (var key in OptionalNumberKeys)
        {
            if (!ModelState.TryGetValue(key, out var entry)) continue;
            if (string.IsNullOrWhiteSpace(entry.AttemptedValue))
                ModelState.Remove(key);
        }

        foreach (var key in ModelState.Keys.Where(k => k.StartsWith("PairSides[", StringComparison.Ordinal) && (k.EndsWith(".Quantity", StringComparison.Ordinal) || k.EndsWith(".RetailPrice", StringComparison.Ordinal))).ToList())
        {
            if (ModelState.TryGetValue(key, out var entry) && string.IsNullOrWhiteSpace(entry.AttemptedValue))
                ModelState.Remove(key);
        }
    }

    private async Task<string> ShapeMeasureFieldsAsync(Product product, bool isCreate, CancellationToken cancellationToken)
    {
        var unitName = product.UnitId is int unitId
            ? await _db.UnitsOfMeasure.AsNoTracking()
                .Where(u => u.Id == unitId)
                .Select(u => u.Name)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var key = UnitRoles.Key(unitName);
        product.ProductKind = UnitRoles.IsGrouped(unitName) ? ProductKind.CompleteSet : ProductKind.SingleItem;
        if (key != "carton")
        {
            product.UnitWeightKg = null;
            product.CartonWeightKg = null;
            product.PiecesPerCarton = null;
            product.LengthCm = null;
            product.WidthCm = null;
            product.HeightCm = null;
        }

        if (isCreate)
        {
            var countable = key is "piece" or "kilogram" or "carton";
            if (UnitRoles.IsGrouped(unitName) || !countable)
                product.CurrentStock = 0;
        }

        return key;
    }

    private void AddServiceError(string? error)
    {
        var message = error ?? "Could not save the product.";
        var key = string.Empty;
        if (message.Contains("Product name", StringComparison.OrdinalIgnoreCase))
            key = "Product.Name";
        else if (message.Contains("Barcode", StringComparison.OrdinalIgnoreCase))
            key = "Product.Barcode";
        else if (message.Contains("SKU", StringComparison.OrdinalIgnoreCase))
            key = "Product.Sku";
        else if (message.Contains("Minimum Sale Price", StringComparison.OrdinalIgnoreCase))
            key = "Product.WholesalePrice";
        else if (message.Contains("Price change reason", StringComparison.OrdinalIgnoreCase))
            key = "PriceChangeReason";
        else if (message.Contains("Quantity", StringComparison.OrdinalIgnoreCase))
            key = "Product.CurrentStock";

        ModelState.AddModelError(key, message);
    }

    private async Task<ProductFormViewModel> BuildFormAsync(
        Product product,
        CancellationToken cancellationToken,
        List<SetComponentFormRow>? posted = null,
        List<PairSideFormRow>? postedSides = null,
        bool usePostedSides = false,
        decimal setsToAdd = 0,
        ProductFormViewModel? carry = null)
    {
        var units = await _settings.GetUnitsAsync(cancellationToken: cancellationToken);
        var unitName = units.FirstOrDefault(u => u.Id == product.UnitId)?.Name;
        var isPair = UnitRoles.IsPair(unitName);

        var components = posted ?? (isPair
            ? new List<SetComponentFormRow>()
            : product.SetComponents
                .Where(c => c.IsActive)
                .Select(c => new SetComponentFormRow
                {
                    ComponentProductId = c.ComponentProductId,
                    Sku = c.ComponentProduct?.Sku,
                    Name = c.ComponentProduct?.Name,
                    RequiredQuantity = c.RequiredQuantity,
                    AvailableStock = c.ComponentProduct is null ? 0 : c.ComponentProduct.CurrentStock - c.ComponentProduct.ReservedQuantity
                })
                .ToList());

        if (posted is not null)
        {
            var ids = components.Where(c => c.ComponentProductId > 0 && string.IsNullOrEmpty(c.Name)).Select(c => c.ComponentProductId).ToList();
            if (ids.Count > 0)
            {
                var names = await _db.Products.AsNoTracking()
                    .Where(p => ids.Contains(p.Id))
                    .Select(p => new { p.Id, p.Sku, p.Name, Available = p.CurrentStock - p.ReservedQuantity })
                    .ToListAsync(cancellationToken);
                foreach (var row in components)
                {
                    var match = names.FirstOrDefault(n => n.Id == row.ComponentProductId);
                    if (match is null) continue;
                    row.Sku = match.Sku;
                    row.Name = match.Name;
                    row.AvailableStock = match.Available;
                }
            }
        }

        List<PairSideFormRow> sides;
        if (isPair && usePostedSides)
            sides = postedSides ?? new List<PairSideFormRow>();
        else if (isPair)
            sides = product.SetComponents
                .Where(c => c.IsActive)
                .Select(c => new PairSideFormRow
                {
                    ComponentProductId = c.ComponentProductId,
                    Name = c.ComponentProduct?.Name,
                    Quantity = c.ComponentProduct?.CurrentStock ?? 0,
                    RetailPrice = c.ComponentProduct?.RetailPrice ?? 0
                })
                .ToList();
        else
            sides = new List<PairSideFormRow>();

        if (!isPair || sides.Count == 0 && !usePostedSides)
        {
            if (sides.Count == 0)
            {
                sides = new List<PairSideFormRow>
                {
                    new() { Name = "Left" },
                    new() { Name = "Right" }
                };
            }
        }

        return new ProductFormViewModel
        {
            Product = product,
            Categories = await _settings.GetCategoriesAsync(cancellationToken: cancellationToken),
            Units = units,
            Suppliers = await _suppliers.GetActiveLookupAsync(cancellationToken),
            Components = components,
            PairSides = sides,
            SetsToAdd = setsToAdd,
            ShortSetName = carry?.ShortSetName,
            ShortSetQuantity = carry?.ShortSetQuantity ?? 0,
            ShortSetWholesalePrice = carry?.ShortSetWholesalePrice ?? 0,
            ShortSetRetailPrice = carry?.ShortSetRetailPrice ?? 0,
            ShortSetItemIds = carry?.ShortSetItemIds ?? new List<int>(),
            ShortSetPosted = carry is not null
        };
    }

    private static List<SetComponentInput>? ComponentsOf(ProductFormViewModel model)
    {
        return model.Components
            .Where(c => c.ComponentProductId > 0)
            .Select(c => new SetComponentInput
            {
                ComponentProductId = c.ComponentProductId,
                RequiredQuantity = c.RequiredQuantity
            })
            .ToList();
    }

    private static ShortSetInput? ShortSetOf(ProductFormViewModel model, bool isSet)
    {
        if (!isSet || model.ShortSetQuantity == 0)
            return null;
        return new ShortSetInput
        {
            Name = model.ShortSetName ?? string.Empty,
            Quantity = model.ShortSetQuantity,
            WholesalePrice = model.ShortSetWholesalePrice,
            RetailPrice = model.ShortSetRetailPrice,
            IncludedProductIds = model.ShortSetItemIds ?? new List<int>()
        };
    }

    private static List<PairSideInput> PairSidesOf(ProductFormViewModel model)
    {
        return model.PairSides
            .Select(s => new PairSideInput
            {
                ComponentProductId = s.ComponentProductId,
                Name = s.Name ?? string.Empty,
                Quantity = s.Quantity,
                RetailPrice = s.RetailPrice
            })
            .ToList();
    }

    private async Task<bool> ApplyImageAsync(ProductFormViewModel model)
    {
        if (model.ImageFile is null || model.ImageFile.Length == 0) return false;

        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        var ext = Path.GetExtension(model.ImageFile.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext) || model.ImageFile.Length > 2_000_000)
        {
            ModelState.AddModelError(nameof(model.ImageFile), "Image must be JPG/PNG/WEBP/GIF under 2 MB.");
            return false;
        }

        var folder = Path.Combine(_env.WebRootPath, "uploads", "products");
        Directory.CreateDirectory(folder);
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(folder, fileName);
        await using var stream = System.IO.File.Create(path);
        await model.ImageFile.CopyToAsync(stream);
        model.Product.ImagePath = $"/uploads/products/{fileName}";
        return true;
    }
}
