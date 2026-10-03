using Microsoft.EntityFrameworkCore;
using UD_Autos.Data;
using UD_Autos.Models.Common;
using UD_Autos.Models.Customers;

namespace UD_Autos.Services;

public interface ICustomerService
{
    Task<PagedResult<Customer>> SearchAsync(string? search, CustomerType? type, string? sortBy, bool sortDesc, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error, Customer? Customer)> CreateAsync(Customer customer, string? userId, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> UpdateAsync(Customer customer, string? userId, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> SoftDeleteAsync(int id, string? userId, CancellationToken cancellationToken = default);
}

public class CustomerService : ICustomerService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICodeGeneratorService _codes;

    public CustomerService(ApplicationDbContext db, IAuditService audit, ICodeGeneratorService codes)
    {
        _db = db;
        _audit = audit;
        _codes = codes;
    }

    public async Task<PagedResult<Customer>> SearchAsync(
        string? search, CustomerType? type, string? sortBy, bool sortDesc, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);

        var query = _db.Customers.AsNoTracking().Where(c => c.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c =>
                c.CustomerCode.Contains(term) ||
                c.Name.Contains(term) ||
                (c.ShopName != null && c.ShopName.Contains(term)) ||
                (c.Phone != null && c.Phone.Contains(term)) ||
                (c.City != null && c.City.Contains(term)));
        }

        if (type.HasValue)
        {
            query = query.Where(c => c.CustomerType == type);
        }

        query = (sortBy?.ToLowerInvariant()) switch
        {
            "code" => sortDesc ? query.OrderByDescending(c => c.CustomerCode) : query.OrderBy(c => c.CustomerCode),
            "balance" => sortDesc ? query.OrderByDescending(c => c.CurrentBalance) : query.OrderBy(c => c.CurrentBalance),
            "city" => sortDesc ? query.OrderByDescending(c => c.City) : query.OrderBy(c => c.City),
            _ => sortDesc ? query.OrderByDescending(c => c.Id) : query.OrderBy(c => c.Name)
        };

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<Customer>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Search = search,
            SortBy = sortBy,
            SortDesc = sortDesc
        };
    }

    public Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => _db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<(bool Ok, string? Error, Customer? Customer)> CreateAsync(
        Customer customer, string? userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customer.CustomerCode))
        {
            customer.CustomerCode = await _codes.NextCustomerCodeAsync(cancellationToken);
        }

        customer.CustomerCode = customer.CustomerCode!.Trim().ToUpperInvariant();
        if (await _db.Customers.AnyAsync(c => c.CustomerCode == customer.CustomerCode, cancellationToken))
        {
            return (false, $"Customer code '{customer.CustomerCode}' already exists.", null);
        }

        // Opening balance seeds current balance for migration only; later phases use ledger.
        customer.CurrentBalance = customer.OpeningBalance;
        customer.CreatedAt = DateTime.UtcNow;
        customer.CreatedBy = userId;
        customer.IsActive = true;

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("Create", nameof(Customer), customer.Id.ToString(), customer.CustomerCode, cancellationToken: cancellationToken);
        return (true, null, customer);
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(Customer customer, string? userId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customer.Id, cancellationToken);
        if (existing is null) return (false, "Customer not found.");

        if (string.IsNullOrWhiteSpace(customer.CustomerCode))
        {
            return (false, "Customer code is required when editing.");
        }

        customer.CustomerCode = customer.CustomerCode!.Trim().ToUpperInvariant();
        if (await _db.Customers.AnyAsync(c => c.CustomerCode == customer.CustomerCode && c.Id != customer.Id, cancellationToken))
        {
            return (false, $"Customer code '{customer.CustomerCode}' already exists.");
        }

        existing.CustomerCode = customer.CustomerCode;
        existing.Name = customer.Name;
        existing.ShopName = customer.ShopName;
        existing.Phone = customer.Phone;
        existing.Address = customer.Address;
        existing.City = customer.City;
        existing.CustomerType = customer.CustomerType;
        existing.CreditLimit = customer.CreditLimit;
        existing.Notes = customer.Notes;
        existing.IsActive = customer.IsActive;
        // OpeningBalance / CurrentBalance / totals are not freely edited here after create.
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = userId;

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("Update", nameof(Customer), existing.Id.ToString(), existing.CustomerCode, cancellationToken: cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> SoftDeleteAsync(int id, string? userId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (existing is null) return (false, "Customer not found.");

        existing.IsActive = false;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = userId;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("SoftDelete", nameof(Customer), existing.Id.ToString(), existing.CustomerCode, cancellationToken: cancellationToken);
        return (true, null);
    }
}
