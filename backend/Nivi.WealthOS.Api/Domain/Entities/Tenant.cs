namespace Nivi.WealthOS.Api.Domain.Entities;

public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string BaseCurrency { get; set; } = "INR";
    public string Timezone { get; set; } = "Asia/Kolkata";
    public bool AllowMemberDelete { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
