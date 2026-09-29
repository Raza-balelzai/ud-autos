using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UD_Autos.Authorization;
using UD_Autos.Models.Identity;

namespace UD_Autos.Services;

public class UserAdminRow
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string Role { get; set; } = string.Empty;
}

public interface IUserAdminService
{
    Task<IReadOnlyList<UserAdminRow>> ListAsync(CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> CreateAsync(string email, string fullName, string password, string role, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> SetActiveAsync(string userId, bool isActive, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> SetRoleAsync(string userId, string role, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> ResetPasswordAsync(string userId, string newPassword, CancellationToken cancellationToken = default);
}

public class UserAdminService : IUserAdminService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAuditService _audit;

    public UserAdminService(UserManager<ApplicationUser> users, IAuditService audit)
    {
        _users = users;
        _audit = audit;
    }

    public async Task<IReadOnlyList<UserAdminRow>> ListAsync(CancellationToken cancellationToken = default)
    {
        var list = await _users.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync(cancellationToken);
        var rows = new List<UserAdminRow>();
        foreach (var u in list)
        {
            var roles = await _users.GetRolesAsync(u);
            rows.Add(new UserAdminRow
            {
                Id = u.Id,
                Email = u.Email ?? "",
                FullName = u.FullName,
                IsActive = u.IsActive,
                Role = roles.FirstOrDefault() ?? ""
            });
        }
        return rows;
    }

    public async Task<(bool Ok, string? Error)> CreateAsync(
        string email, string fullName, string password, string role, CancellationToken cancellationToken = default)
    {
        if (!AppRoles.All.Contains(role)) return (false, "Invalid role.");
        var user = new ApplicationUser
        {
            UserName = email.Trim(),
            Email = email.Trim(),
            EmailConfirmed = true,
            FullName = fullName.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var result = await _users.CreateAsync(user, password);
        if (!result.Succeeded) return (false, string.Join("; ", result.Errors.Select(e => e.Description)));
        await _users.AddToRoleAsync(user, role);
        await _audit.LogAsync("CreateUser", nameof(ApplicationUser), user.Id, user.Email, newValues: new { role }, cancellationToken: cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> SetActiveAsync(string userId, bool isActive, CancellationToken cancellationToken = default)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user is null) return (false, "User not found.");
        user.IsActive = isActive;
        var result = await _users.UpdateAsync(user);
        if (!result.Succeeded) return (false, string.Join("; ", result.Errors.Select(e => e.Description)));
        await _audit.LogAsync(isActive ? "ActivateUser" : "DeactivateUser", nameof(ApplicationUser), user.Id, user.Email, cancellationToken: cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> SetRoleAsync(string userId, string role, CancellationToken cancellationToken = default)
    {
        if (!AppRoles.All.Contains(role)) return (false, "Invalid role.");
        var user = await _users.FindByIdAsync(userId);
        if (user is null) return (false, "User not found.");
        var current = await _users.GetRolesAsync(user);
        await _users.RemoveFromRolesAsync(user, current);
        await _users.AddToRoleAsync(user, role);
        await _audit.LogAsync("SetRole", nameof(ApplicationUser), user.Id, user.Email, newValues: new { role }, cancellationToken: cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> ResetPasswordAsync(string userId, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user is null) return (false, "User not found.");
        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var result = await _users.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded) return (false, string.Join("; ", result.Errors.Select(e => e.Description)));
        await _audit.LogAsync("ResetPassword", nameof(ApplicationUser), user.Id, user.Email, cancellationToken: cancellationToken);
        return (true, null);
    }
}
