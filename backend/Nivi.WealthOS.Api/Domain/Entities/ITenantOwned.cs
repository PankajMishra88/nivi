namespace Nivi.WealthOS.Api.Domain.Entities;

public interface ITenantOwned
{
    Guid TenantId { get; set; }
}
