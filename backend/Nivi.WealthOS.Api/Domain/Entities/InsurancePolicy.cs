namespace Nivi.WealthOS.Api.Domain.Entities;

public class InsurancePolicy : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string OriginalCurrency { get; set; } = "INR";
    public decimal OriginalPremium { get; set; }
    public decimal? FxRateUsed { get; set; }
    public decimal BasePremium { get; set; }
    public DateOnly RenewalDate { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
