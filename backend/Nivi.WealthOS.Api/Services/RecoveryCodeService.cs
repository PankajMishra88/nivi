using System.Security.Cryptography;
using System.Text;

namespace Nivi.WealthOS.Api.Services;

public class RecoveryCodeService
{
    private const int CodeLength = 10;
    private const int DefaultCount = 8;

    public IReadOnlyList<string> GenerateCodes(int count = DefaultCount)
    {
        var codes = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            codes.Add(GenerateCode());
        }

        return codes;
    }

    public string HashCode(string code)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(code));
        return Convert.ToBase64String(bytes);
    }

    public bool VerifyHashedCode(string storedHash, string providedCode)
    {
        var hash = HashCode(providedCode);
        var storedBytes = Convert.FromBase64String(storedHash);
        var providedBytes = Convert.FromBase64String(hash);
        return CryptographicOperations.FixedTimeEquals(storedBytes, providedBytes);
    }

    private static string GenerateCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var buffer = new char[CodeLength];
        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        }

        return new string(buffer);
    }
}
