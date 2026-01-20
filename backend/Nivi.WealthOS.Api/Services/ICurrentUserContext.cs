using System.Security.Claims;

namespace Nivi.WealthOS.Api.Services;

public interface ICurrentUserContext
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
}
