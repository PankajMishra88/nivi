namespace Nivi.WealthOS.Api.Models;

public record SmtpSettingsResponse(
    string Host,
    int Port,
    string? Username,
    bool UseSsl,
    string DefaultFrom);

public record UpdateSmtpSettingsRequest(
    string Host,
    int Port,
    string? Username,
    string? Password,
    bool UseSsl,
    string DefaultFrom);
