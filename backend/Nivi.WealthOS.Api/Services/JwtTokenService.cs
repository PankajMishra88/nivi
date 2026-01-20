using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Nivi.WealthOS.Api.Options;

namespace Nivi.WealthOS.Api.Services;

public record JwtTokenResult(string Token, DateTime ExpiresAtUtc, string JwtId);

public class JwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public JwtTokenResult CreateToken(Guid tenantId, Guid userId, string email)
    {
        var jwtId = Guid.NewGuid().ToString("N");
        var expires = DateTime.UtcNow.AddMinutes(_options.ExpiresMinutes);
        var claims = new List<Claim>
        {
            new("tenant_id", tenantId.ToString()),
            new("user_id", userId.ToString()),
            new(ClaimTypes.Email, email),
            new(JwtRegisteredClaimNames.Jti, jwtId)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new JwtTokenResult(new JwtSecurityTokenHandler().WriteToken(token), expires, jwtId);
    }
}
