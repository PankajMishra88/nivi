namespace Nivi.WealthOS.Api.Models;

public record SessionResponse(
    Guid Id,
    Guid UserId,
    string JwtId,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? RevokedAtUtc,
    string? IpAddress,
    string? UserAgent);
