using Nivi.WealthOS.Api.Domain.Enums;

namespace Nivi.WealthOS.Api.Domain.Entities;

public class Goal : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OriginalCurrency { get; set; } = "INR";
    public decimal OriginalTargetAmount { get; set; }
    public decimal? FxRateUsed { get; set; }
    public decimal BaseTargetAmount { get; set; }
    public DateOnly CheckInDate { get; set; }
    public GoalStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
