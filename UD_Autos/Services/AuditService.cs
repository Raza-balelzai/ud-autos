using System.Text.Json;
using UD_Autos.Data;
using UD_Autos.Models.Audit;

namespace UD_Autos.Services;

public interface IAuditService
{
    Task LogAsync(
        string action,
        string entityType,
        string? entityId,
        string? referenceNumber = null,
        string? reason = null,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken cancellationToken = default);
}

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(ApplicationDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(
        string action,
        string entityType,
        string? entityId,
        string? referenceNumber = null,
        string? reason = null,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken cancellationToken = default)
    {
        var http = _httpContextAccessor.HttpContext;
        var user = http?.User;

        var entry = new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            ReferenceNumber = referenceNumber,
            Reason = reason,
            UserId = user?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            UserName = user?.Identity?.Name,
            IpAddress = http?.Connection.RemoteIpAddress?.ToString(),
            OldValuesJson = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValuesJson = newValues is null ? null : JsonSerializer.Serialize(newValues),
            TimestampUtc = DateTime.UtcNow
        };

        _db.AuditLogs.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
