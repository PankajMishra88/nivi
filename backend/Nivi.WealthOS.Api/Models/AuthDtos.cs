namespace Nivi.WealthOS.Api.Models;

public record RegisterTenantRequest(
    string TenantName,
    string TenantSlug,
    string BaseCurrency,
    string Timezone,
    string AdminEmail,
    string AdminPassword,
    string AdminName);

public record RegisterTenantResponse(
    string TenantSlug,
    Guid UserId,
    string MfaSecret,
    string OtpAuthUrl,
    IReadOnlyList<string> RecoveryCodes);

public record ConfirmMfaRequest(string TenantSlug, Guid UserId, string Code);

public record LoginRequest(
    string TenantSlug,
    string Email,
    string Password,
    string? TotpCode,
    string? RecoveryCode);

public record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    UserSummary User);

public record UserSummary(Guid Id, string Email, string DisplayName);

public record RegenerateRecoveryCodesRequest(string TotpCode);
