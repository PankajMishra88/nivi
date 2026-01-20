namespace Nivi.WealthOS.Api.Domain.Entities;

public class FxRate : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public DateOnly Date { get; set; }
    public string BaseCurrency { get; set; } = "INR";
    public string QuoteCurrency { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
