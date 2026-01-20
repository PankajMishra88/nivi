using Nivi.WealthOS.Api.Domain.Enums;

namespace Nivi.WealthOS.Api.Domain.Entities;

public class UserRole : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public RoleName Role { get; set; }
    public RoleScope ScopeType { get; set; }
    public Guid? ScopeId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
