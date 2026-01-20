using Isopoh.Cryptography.Argon2;

namespace Nivi.WealthOS.Api.Services;

public class PasswordHasher
{
    public string Hash(string password)
    {
        return Argon2.Hash(
            password,
            timeCost: 4,
            memoryCost: 1 << 16,
            parallelism: 2,
            type: Argon2Type.HybridAddressing,
            hashLength: 32);
    }

    public bool Verify(string encodedHash, string password)
    {
        return Argon2.Verify(encodedHash, password);
    }
}
