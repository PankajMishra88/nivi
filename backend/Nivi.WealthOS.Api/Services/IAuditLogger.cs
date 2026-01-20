using Nivi.WealthOS.Api.Domain.Entities;

namespace Nivi.WealthOS.Api.Services;

public interface IAuditLogger
{
    Task LogAsync(string action, Guid? actorUserId, Guid tenantId, string? entityType = null, Guid? entityId = null, object? metadata = null);
}
