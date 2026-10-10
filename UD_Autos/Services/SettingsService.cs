using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using UD_Autos.Data;
using UD_Autos.Models.Settings;
using UD_Autos.Models.ViewModels;

namespace UD_Autos.Services;

public interface ISettingsService
{
    Task<IReadOnlyList<ProductCategory>> GetCategoriesAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> SaveCategoryAsync(ProductCategory category, string? userId, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> SoftDeleteCategoryAsync(int id, string? userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UnitOfMeasure>> GetUnitsAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> SaveUnitAsync(UnitOfMeasure unit, string? userId, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> SoftDeleteUnitAsync(int id, string? userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExpenseCategory>> GetExpenseCategoriesAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> SaveExpenseCategoryAsync(ExpenseCategory category, string? userId, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> SoftDeleteExpenseCategoryAsync(int id, string? userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BusinessSetting>> GetBusinessSettingsAsync(CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> SaveBusinessSettingAsync(BusinessSetting setting, string? userId, CancellationToken cancellationToken = default);

    Task<MailSettingsForm> GetMailSettingsAsync(CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> SaveMailSettingsAsync(MailSettingsForm form, string? userId, CancellationToken cancellationToken = default);
    Task<(bool Ok, OutboundMail? Mail, string? Error)> GetOutboundMailAsync(CancellationToken cancellationToken = default);
}

public sealed class OutboundMail
{
    public string Host { get; init; } = "";
    public int Port { get; init; }
    public string UserName { get; init; } = "";
    public string Password { get; init; } = "";
    public string FromAddress { get; init; } = "";
    public string FromName { get; init; } = "";
    public bool EnableSsl { get; init; }
}

public class SettingsService : ISettingsService
{
    private const string PasswordPurpose = "UD_Autos.MailSetting.Password";
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly IDataProtector _passwords;

    public SettingsService(ApplicationDbContext db, IAuditService audit, IDataProtectionProvider protection)
    {
        _db = db;
        _audit = audit;
        _passwords = protection.CreateProtector(PasswordPurpose);
    }

    public async Task<IReadOnlyList<ProductCategory>> GetCategoriesAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var q = _db.ProductCategories.AsNoTracking().AsQueryable();
        if (activeOnly) q = q.Where(c => c.IsActive);
        return await q.OrderBy(c => c.Name).ToListAsync(cancellationToken);
    }

    public async Task<(bool Ok, string? Error)> SaveCategoryAsync(ProductCategory category, string? userId, CancellationToken cancellationToken = default)
    {
        category.Name = category.Name.Trim();
        if (await _db.ProductCategories.AnyAsync(c => c.Name == category.Name && c.Id != category.Id, cancellationToken))
            return (false, "Category name already exists.");

        if (category.Id == 0)
        {
            category.CreatedAt = DateTime.UtcNow;
            category.CreatedBy = userId;
            _db.ProductCategories.Add(category);
        }
        else
        {
            var existing = await _db.ProductCategories.FirstOrDefaultAsync(c => c.Id == category.Id, cancellationToken);
            if (existing is null) return (false, "Category not found.");
            existing.Name = category.Name;
            existing.Description = category.Description;
            existing.IsActive = category.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = userId;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(category.Id == 0 ? "Create" : "Update", nameof(ProductCategory), category.Id.ToString(), category.Name, cancellationToken: cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> SoftDeleteCategoryAsync(int id, string? userId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.ProductCategories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (existing is null) return (false, "Category not found.");
        existing.IsActive = false;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = userId;
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<IReadOnlyList<UnitOfMeasure>> GetUnitsAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var q = _db.UnitsOfMeasure.AsNoTracking().AsQueryable();
        if (activeOnly) q = q.Where(u => u.IsActive);
        return await q.OrderBy(u => u.Name).ToListAsync(cancellationToken);
    }

    public async Task<(bool Ok, string? Error)> SaveUnitAsync(UnitOfMeasure unit, string? userId, CancellationToken cancellationToken = default)
    {
        unit.Name = unit.Name.Trim();
        unit.Abbreviation = unit.Abbreviation.Trim();
        if (await _db.UnitsOfMeasure.AnyAsync(u => (u.Name == unit.Name || u.Abbreviation == unit.Abbreviation) && u.Id != unit.Id, cancellationToken))
            return (false, "Unit name or abbreviation already exists.");

        if (unit.Id == 0)
        {
            unit.CreatedAt = DateTime.UtcNow;
            unit.CreatedBy = userId;
            _db.UnitsOfMeasure.Add(unit);
        }
        else
        {
            var existing = await _db.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Id == unit.Id, cancellationToken);
            if (existing is null) return (false, "Unit not found.");
            existing.Name = unit.Name;
            existing.Abbreviation = unit.Abbreviation;
            existing.IsActive = unit.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = userId;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(unit.Id == 0 ? "Create" : "Update", nameof(UnitOfMeasure), unit.Id.ToString(), unit.Name, cancellationToken: cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> SoftDeleteUnitAsync(int id, string? userId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (existing is null) return (false, "Unit not found.");
        existing.IsActive = false;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = userId;
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<IReadOnlyList<ExpenseCategory>> GetExpenseCategoriesAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var q = _db.ExpenseCategories.AsNoTracking().AsQueryable();
        if (activeOnly) q = q.Where(c => c.IsActive);
        return await q.OrderBy(c => c.Name).ToListAsync(cancellationToken);
    }

    public async Task<(bool Ok, string? Error)> SaveExpenseCategoryAsync(ExpenseCategory category, string? userId, CancellationToken cancellationToken = default)
    {
        category.Name = category.Name.Trim();
        if (await _db.ExpenseCategories.AnyAsync(c => c.Name == category.Name && c.Id != category.Id, cancellationToken))
            return (false, "Expense category already exists.");

        if (category.Id == 0)
        {
            category.CreatedAt = DateTime.UtcNow;
            category.CreatedBy = userId;
            _db.ExpenseCategories.Add(category);
        }
        else
        {
            var existing = await _db.ExpenseCategories.FirstOrDefaultAsync(c => c.Id == category.Id, cancellationToken);
            if (existing is null) return (false, "Expense category not found.");
            existing.Name = category.Name;
            existing.Description = category.Description;
            existing.IsDirectImportCost = category.IsDirectImportCost;
            existing.IsActive = category.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = userId;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(category.Id == 0 ? "Create" : "Update", nameof(ExpenseCategory), category.Id.ToString(), category.Name, cancellationToken: cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> SoftDeleteExpenseCategoryAsync(int id, string? userId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.ExpenseCategories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (existing is null) return (false, "Expense category not found.");
        existing.IsActive = false;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = userId;
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<IReadOnlyList<BusinessSetting>> GetBusinessSettingsAsync(CancellationToken cancellationToken = default)
        => await _db.BusinessSettings.AsNoTracking().OrderBy(s => s.Key).ToListAsync(cancellationToken);

    public async Task<(bool Ok, string? Error)> SaveBusinessSettingAsync(BusinessSetting setting, string? userId, CancellationToken cancellationToken = default)
    {
        setting.Key = setting.Key.Trim();
        if (await _db.BusinessSettings.AnyAsync(s => s.Key == setting.Key && s.Id != setting.Id, cancellationToken))
            return (false, "Setting key already exists.");

        if (setting.Id == 0)
        {
            setting.CreatedAt = DateTime.UtcNow;
            setting.CreatedBy = userId;
            _db.BusinessSettings.Add(setting);
        }
        else
        {
            var existing = await _db.BusinessSettings.FirstOrDefaultAsync(s => s.Id == setting.Id, cancellationToken);
            if (existing is null) return (false, "Setting not found.");
            existing.Value = setting.Value;
            existing.Description = setting.Description;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = userId;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("Update", nameof(BusinessSetting), setting.Id.ToString(), setting.Key, cancellationToken: cancellationToken);
        return (true, null);
    }

    public async Task<MailSettingsForm> GetMailSettingsAsync(CancellationToken cancellationToken = default)
    {
        var row = await _db.MailSettings.AsNoTracking().OrderBy(m => m.Id).FirstOrDefaultAsync(cancellationToken);
        if (row is null)
            return new MailSettingsForm();

        return new MailSettingsForm
        {
            Host = row.Host,
            Port = row.Port == 0 ? 587 : row.Port,
            UserName = row.UserName,
            FromAddress = row.FromAddress,
            FromName = row.FromName,
            EnableSsl = row.EnableSsl,
            HasPassword = !string.IsNullOrEmpty(row.ProtectedPassword)
        };
    }

    public async Task<(bool Ok, string? Error)> SaveMailSettingsAsync(MailSettingsForm form, string? userId, CancellationToken cancellationToken = default)
    {
        var row = await _db.MailSettings.OrderBy(m => m.Id).FirstOrDefaultAsync(cancellationToken);
        var created = row is null;
        row ??= new MailSetting { CreatedAt = DateTime.UtcNow, CreatedBy = userId };
        if (created)
            _db.MailSettings.Add(row);

        row.Host = form.Host.Trim();
        row.Port = form.Port;
        row.UserName = form.UserName.Trim();
        row.FromAddress = form.FromAddress.Trim();
        row.FromName = form.FromName.Trim();
        row.EnableSsl = form.EnableSsl;
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = userId;

        var passwordChanged = !string.IsNullOrWhiteSpace(form.Password);
        if (passwordChanged)
            row.ProtectedPassword = _passwords.Protect(form.Password!.Trim());
        else if (string.IsNullOrEmpty(row.ProtectedPassword))
            return (false, "SMTP password is required the first time you save mail settings.");

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(
            "Update",
            nameof(MailSetting),
            row.Id.ToString(),
            "SMTP",
            newValues: new { row.Host, row.Port, row.UserName, row.FromAddress, row.FromName, row.EnableSsl, PasswordChanged = passwordChanged },
            cancellationToken: cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, OutboundMail? Mail, string? Error)> GetOutboundMailAsync(CancellationToken cancellationToken = default)
    {
        var row = await _db.MailSettings.AsNoTracking().OrderBy(m => m.Id).FirstOrDefaultAsync(cancellationToken);
        if (row is null || string.IsNullOrWhiteSpace(row.Host) || string.IsNullOrWhiteSpace(row.FromAddress))
            return (false, null, "Email is not configured. Ask the owner to set it in Settings.");
        if (string.IsNullOrEmpty(row.ProtectedPassword))
            return (false, null, "Email is not configured. Ask the owner to set it in Settings.");

        string password;
        try
        {
            password = _passwords.Unprotect(row.ProtectedPassword);
        }
        catch (Exception)
        {
            return (false, null, "The saved mail password could not be read. Ask the owner to enter it again in Settings.");
        }

        return (true, new OutboundMail
        {
            Host = row.Host,
            Port = row.Port == 0 ? 587 : row.Port,
            UserName = row.UserName,
            Password = password,
            FromAddress = row.FromAddress,
            FromName = string.IsNullOrWhiteSpace(row.FromName) ? row.FromAddress : row.FromName,
            EnableSsl = row.EnableSsl
        }, null);
    }
}
