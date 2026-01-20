using Nivi.WealthOS.Api.Domain.Enums;

namespace Nivi.WealthOS.Api.Domain.Entities;

public class Account : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; }
    public string Currency { get; set; } = "INR";
    public decimal OpeningBalance { get; set; }
    public decimal OpeningBalanceBaseAmount { get; set; }
    public decimal? OpeningBalanceFxRateUsed { get; set; }
    public int? BillingCycleDay { get; set; }
    public int? DueDay { get; set; }
    public decimal BilledAmount { get; set; }
    public decimal UnbilledAmount { get; set; }
    public DateOnly? LastStatementDate { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
