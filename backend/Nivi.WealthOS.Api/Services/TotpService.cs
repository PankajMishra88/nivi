using System.Security.Cryptography;
using OtpNet;

namespace Nivi.WealthOS.Api.Services;

public class TotpService
{
    public string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(20);
        return Base32Encoding.ToString(bytes);
    }

    public bool VerifyCode(string base32Secret, string code)
    {
        var totp = new Totp(Base32Encoding.ToBytes(base32Secret));
        return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
    }

    public string BuildOtpAuthUrl(string issuer, string email, string base32Secret)
    {
        var escapedIssuer = Uri.EscapeDataString(issuer);
        var escapedEmail = Uri.EscapeDataString(email);
        return $"otpauth://totp/{escapedIssuer}:{escapedEmail}?secret={base32Secret}&issuer={escapedIssuer}&digits=6";
    }
}
