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

        if (!ModelState.IsValid)
        {
            return View("Form", await BuildFormAsync(model.Product, cancellationToken, model.Components));
        }

        var uploaded = await ApplyImageAsync(model);
        var components = ComponentsOf(model);
        var (ok, error, product) = await _products.CreateAsync(model.Product, User.GetUserId(), cancellationToken, components);
        if (!ok)
        {
            if (uploaded)
                ProductImageFiles.TryDelete(_env, model.Product.ImagePath);
            ModelState.AddModelError(string.Empty, error ?? "Could not create product.");
            return View("Form", await BuildFormAsync(model.Product, cancellationToken, model.Components));
        }

        TempData["Success"] = product!.ProductKind == ProductKind.CompleteSet
            ? $"Complete set {product.Sku} saved."
            : $"Product {product.Sku} created.";
        return RedirectToAction(nameof(Details), new { id = product.Id });
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
        if (!ModelState.IsValid)
        {
            return View("Form", await BuildFormAsync(model.Product, cancellationToken, model.Components));
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
        var components = ComponentsOf(model);
        var (ok, error) = await _products.UpdateAsync(model.Product, User.GetUserId(), model.PriceChangeReason, cancellationToken, components);
        if (!ok)
        {
            if (uploaded)
                ProductImageFiles.TryDelete(_env, model.Product.ImagePath);
            ModelState.AddModelError(string.Empty, error ?? "Could not update product.");
            return View("Form", await BuildFormAsync(model.Product, cancellationToken, model.Components));
        }

        if (uploaded
            && !string.IsNullOrWhiteSpace(previousImage)
            && !string.Equals(previousImage, model.Product.ImagePath, StringComparison.OrdinalIgnoreCase))
        {
            ProductImageFiles.TryDelete(_env, previousImage);
        }

        TempData["Success"] = model.Product.ProductKind == ProductKind.CompleteSet ? "Complete set updated." : "Product updated.";
        return RedirectToAction(nameof(Details), new { id });
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

    private async Task<ProductFormViewModel> BuildFormAsync(Product product, CancellationToken cancellationToken, List<SetComponentFormRow>? posted = null)
    {
        var components = posted ?? product.SetComponents
            .Where(c => c.IsActive)
            .Select(c => new SetComponentFormRow
            {
                ComponentProductId = c.ComponentProductId,
                Sku = c.ComponentProduct?.Sku,
                Name = c.ComponentProduct?.Name,
                RequiredQuantity = c.RequiredQuantity,
                AvailableStock = c.ComponentProduct is null ? 0 : c.ComponentProduct.CurrentStock - c.ComponentProduct.ReservedQuantity
            })
            .ToList();

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

        return new ProductFormViewModel
        {
            Product = product,
            Categories = await _settings.GetCategoriesAsync(cancellationToken: cancellationToken),
            Units = await _settings.GetUnitsAsync(cancellationToken: cancellationToken),
            Suppliers = await _suppliers.GetActiveLookupAsync(cancellationToken),
            Components = components
        };
    }

    private static List<SetComponentInput>? ComponentsOf(ProductFormViewModel model)
    {
        if (model.Product.ProductKind != ProductKind.CompleteSet)
            return null;
        return model.Components
            .Where(c => c.ComponentProductId > 0)
            .Select(c => new SetComponentInput
            {
                ComponentProductId = c.ComponentProductId,
                RequiredQuantity = c.RequiredQuantity
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
