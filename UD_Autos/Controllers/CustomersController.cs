using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UD_Autos.Authorization;
using UD_Autos.Models.Customers;
using UD_Autos.Services;

namespace UD_Autos.Controllers;

[Authorize(Policy = AppPolicies.ManageCustomers)]
public class CustomersController : Controller
{
    private readonly ICustomerService _customers;
    private readonly IReceivableAgingService _aging;
    private readonly ISalesService _sales;

    public CustomersController(ICustomerService customers, IReceivableAgingService aging, ISalesService sales)
    {
        _customers = customers;
        _aging = aging;
        _sales = sales;
    }

    public async Task<IActionResult> Index(string? search, CustomerType? type, string? sortBy, bool sortDesc = false, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Customers / Khata";
        ViewData["Subtitle"] = "Dukandar master — balances come from ledger workflows.";
        var result = await _customers.SearchAsync(search, type, sortBy, sortDesc, page, 20, cancellationToken);
        return View(result);
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken = default)
    {
        var customer = await _customers.GetByIdAsync(id, cancellationToken);
        if (customer is null) return NotFound();
        ViewData["Title"] = customer.ShopName ?? customer.Name;
        ViewData["Subtitle"] = customer.CustomerCode;
        return View(customer);
    }

    public async Task<IActionResult> Khata(int id, CancellationToken cancellationToken = default)
    {
        var customer = await _customers.GetByIdAsync(id, cancellationToken);
        if (customer is null) return NotFound();
        ViewData["Title"] = "Customer Khata";
        ViewData["Subtitle"] = $"{customer.CustomerCode} — {customer.ShopName ?? customer.Name}";
        ViewBag.Customer = customer;
        ViewBag.Ledger = await _aging.GetLedgerAsync(id, cancellationToken);
        ViewBag.Invoices = (await _sales.SearchInvoicesAsync(null, id, null, null, 1, 50, cancellationToken, includeItems: true)).Items;
        return View();
    }

    public IActionResult Create()
    {
        ViewData["Title"] = "Add Customer";
        return View("Form", new Customer());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Customer model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.CustomerCode))
            ModelState.Remove(nameof(Customer.CustomerCode));

        if (!ModelState.IsValid) return View("Form", model);

        var (ok, error, customer) = await _customers.CreateAsync(model, User.GetUserId(), cancellationToken);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Could not create customer.");
            return View("Form", model);
        }

        TempData["Success"] = $"Customer {customer!.CustomerCode} created.";
        return RedirectToAction(nameof(Details), new { id = customer.Id });
    }

    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken = default)
    {
        var customer = await _customers.GetByIdAsync(id, cancellationToken);
        if (customer is null) return NotFound();
        ViewData["Title"] = "Edit Customer";
        return View("Form", customer);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Customer model, CancellationToken cancellationToken = default)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) return View("Form", model);

        var (ok, error) = await _customers.UpdateAsync(model, User.GetUserId(), cancellationToken);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Could not update customer.");
            return View("Form", model);
        }

        TempData["Success"] = "Customer updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
    {
        var (ok, error) = await _customers.SoftDeleteAsync(id, User.GetUserId(), cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok ? "Customer deactivated." : error;
        return RedirectToAction(nameof(Index));
    }
}
