namespace Nivi.WealthOS.Api.Domain.Entities;

public class TaxDeadline : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly DueDate { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
