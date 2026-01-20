using Nivi.WealthOS.Api.Domain.Enums;

namespace Nivi.WealthOS.Api.Domain.Entities;

public class WealthEntity : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid GroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public EntityType Type { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
