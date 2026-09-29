using Microsoft.EntityFrameworkCore;
using UD_Autos.Data;
using UD_Autos.Models.Common;
using UD_Autos.Models.Suppliers;

namespace UD_Autos.Services;

public interface ISupplierService
{
    Task<PagedResult<Supplier>> SearchAsync(string? search, string? sortBy, bool sortDesc, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Supplier?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error, Supplier? Supplier)> CreateAsync(Supplier supplier, string? userId, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> UpdateAsync(Supplier supplier, string? userId, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> SoftDeleteAsync(int id, string? userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Supplier>> GetActiveLookupAsync(CancellationToken cancellationToken = default);
}

public class SupplierService : ISupplierService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICodeGeneratorService _codes;

    public SupplierService(ApplicationDbContext db, IAuditService audit, ICodeGeneratorService codes)
    {
        _db = db;
        _audit = audit;
        _codes = codes;
    }

    public async Task<PagedResult<Supplier>> SearchAsync(
        string? search, string? sortBy, bool sortDesc, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);

        var query = _db.Suppliers.AsNoTracking().Where(s => s.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(s =>
                s.SupplierCode.Contains(term) ||
                s.Name.Contains(term) ||
                (s.ContactPerson != null && s.ContactPerson.Contains(term)) ||
                (s.Phone != null && s.Phone.Contains(term)) ||
                (s.WeChat != null && s.WeChat.Contains(term)));
        }

        query = (sortBy?.ToLowerInvariant()) switch
        {
            "code" => sortDesc ? query.OrderByDescending(s => s.SupplierCode) : query.OrderBy(s => s.SupplierCode),
            "balance" => sortDesc ? query.OrderByDescending(s => s.OutstandingBalancePkr) : query.OrderBy(s => s.OutstandingBalancePkr),
            _ => sortDesc ? query.OrderByDescending(s => s.Id) : query.OrderBy(s => s.Name)
        };

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<Supplier>
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

    public Task<Supplier?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => _db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<(bool Ok, string? Error, Supplier? Supplier)> CreateAsync(
        Supplier supplier, string? userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(supplier.SupplierCode))
        {
            supplier.SupplierCode = await _codes.NextSupplierCodeAsync(cancellationToken);
        }

        supplier.SupplierCode = supplier.SupplierCode.Trim().ToUpperInvariant();
        if (await _db.Suppliers.AnyAsync(s => s.SupplierCode == supplier.SupplierCode, cancellationToken))
        {
            return (false, $"Supplier code '{supplier.SupplierCode}' already exists.", null);
        }

        supplier.CreatedAt = DateTime.UtcNow;
        supplier.CreatedBy = userId;
        supplier.IsActive = true;
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("Create", nameof(Supplier), supplier.Id.ToString(), supplier.SupplierCode, cancellationToken: cancellationToken);
        return (true, null, supplier);
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(Supplier supplier, string? userId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplier.Id, cancellationToken);
        if (existing is null) return (false, "Supplier not found.");

        supplier.SupplierCode = supplier.SupplierCode.Trim().ToUpperInvariant();
        if (await _db.Suppliers.AnyAsync(s => s.SupplierCode == supplier.SupplierCode && s.Id != supplier.Id, cancellationToken))
        {
            return (false, $"Supplier code '{supplier.SupplierCode}' already exists.");
        }

        existing.SupplierCode = supplier.SupplierCode;
        existing.Name = supplier.Name;
        existing.ContactPerson = supplier.ContactPerson;
        existing.WeChat = supplier.WeChat;
        existing.Phone = supplier.Phone;
        existing.Address = supplier.Address;
        existing.City = supplier.City;
        existing.Country = supplier.Country;
        existing.Notes = supplier.Notes;
        existing.IsActive = supplier.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = userId;

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("Update", nameof(Supplier), existing.Id.ToString(), existing.SupplierCode, cancellationToken: cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> SoftDeleteAsync(int id, string? userId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (existing is null) return (false, "Supplier not found.");

        existing.IsActive = false;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = userId;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("SoftDelete", nameof(Supplier), existing.Id.ToString(), existing.SupplierCode, cancellationToken: cancellationToken);
        return (true, null);
    }

    public async Task<IReadOnlyList<Supplier>> GetActiveLookupAsync(CancellationToken cancellationToken = default)
        => await _db.Suppliers.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(cancellationToken);
}
