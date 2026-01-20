namespace Nivi.WealthOS.Api.Domain.Entities;

public class Subscription : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OriginalCurrency { get; set; } = "INR";
    public decimal OriginalAmount { get; set; }
    public decimal? FxRateUsed { get; set; }
    public decimal BaseAmount { get; set; }
    public DateOnly NextDueDate { get; set; }
    public string Frequency { get; set; } = "Monthly";
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
