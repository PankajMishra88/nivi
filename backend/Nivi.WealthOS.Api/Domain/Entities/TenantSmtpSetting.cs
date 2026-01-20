namespace Nivi.WealthOS.Api.Domain.Entities;

public class TenantSmtpSetting : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseSsl { get; set; }
    public string DefaultFrom { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
}
