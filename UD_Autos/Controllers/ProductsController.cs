using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UD_Autos.Authorization;
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

    public ProductsController(
        IProductService products,
        ISettingsService settings,
        ISupplierService suppliers,
        IWebHostEnvironment env)
    {
        _products = products;
        _settings = settings;
        _suppliers = suppliers;
        _env = env;
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

        ViewData["Title"] = product.Name;
        ViewData["Subtitle"] = product.Sku;
        ViewBag.CanViewCosts = User.IsInRole(AppRoles.Owner) || User.IsInRole(AppRoles.Manager) || User.IsInRole(AppRoles.Accountant);
        return View(product);
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
        if (!ModelState.IsValid)
        {
            return View("Form", await BuildFormAsync(model.Product, cancellationToken));
        }

        await ApplyImageAsync(model);
        var (ok, error, product) = await _products.CreateAsync(model.Product, User.GetUserId(), cancellationToken);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Could not create product.");
            return View("Form", await BuildFormAsync(model.Product, cancellationToken));
        }

        TempData["Success"] = $"Product {product!.Sku} created.";
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
            return View("Form", await BuildFormAsync(model.Product, cancellationToken));
        }

        await ApplyImageAsync(model);
        var (ok, error) = await _products.UpdateAsync(model.Product, User.GetUserId(), model.PriceChangeReason, cancellationToken);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Could not update product.");
            return View("Form", await BuildFormAsync(model.Product, cancellationToken));
        }

        TempData["Success"] = "Product updated.";
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

    private async Task<ProductFormViewModel> BuildFormAsync(Product product, CancellationToken cancellationToken)
    {
        return new ProductFormViewModel
        {
            Product = product,
            Categories = await _settings.GetCategoriesAsync(cancellationToken: cancellationToken),
            Units = await _settings.GetUnitsAsync(cancellationToken: cancellationToken),
            Suppliers = await _suppliers.GetActiveLookupAsync(cancellationToken)
        };
    }

    private async Task ApplyImageAsync(ProductFormViewModel model)
    {
        if (model.ImageFile is null || model.ImageFile.Length == 0) return;

        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        var ext = Path.GetExtension(model.ImageFile.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext) || model.ImageFile.Length > 2_000_000)
        {
            ModelState.AddModelError(nameof(model.ImageFile), "Image must be JPG/PNG/WEBP/GIF under 2 MB.");
            return;
        }

        var folder = Path.Combine(_env.WebRootPath, "uploads", "products");
        Directory.CreateDirectory(folder);
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(folder, fileName);
        await using var stream = System.IO.File.Create(path);
        await model.ImageFile.CopyToAsync(stream);
        model.Product.ImagePath = $"/uploads/products/{fileName}";
    }
}
