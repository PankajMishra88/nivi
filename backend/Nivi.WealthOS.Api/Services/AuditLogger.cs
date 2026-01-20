using System.Text.Json;
using Nivi.WealthOS.Api.Data;
using Nivi.WealthOS.Api.Domain.Entities;

namespace Nivi.WealthOS.Api.Services;

public class AuditLogger : IAuditLogger
{
    private readonly AppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogger(AppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(string action, Guid? actorUserId, Guid tenantId, string? entityType = null, Guid? entityId = null, object? metadata = null)
    {
        var context = _httpContextAccessor.HttpContext;
        var log = new AuditLog
        {
            TenantId = tenantId,
            ActorUserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            MetadataJson = metadata == null ? null : JsonSerializer.Serialize(metadata),
            IpAddress = context?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context?.Request.Headers.UserAgent.ToString(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.AuditLogs.Add(log);
        await _dbContext.SaveChangesAsync();
    }
}
