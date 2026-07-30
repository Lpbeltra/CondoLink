using System.Security.Cryptography;

namespace CondoLink.Api.Features.WhatsApp;

public interface IPhoneVerificationCodeGenerator
{
    string Generate();
}

internal sealed class PhoneVerificationCodeGenerator
    : IPhoneVerificationCodeGenerator
{
    public string Generate() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}

internal static class PhoneVerificationCodeHasher
{
    private const int Iterations = 100_000;
    private const int HashSize = 32;

    public static (byte[] Hash, byte[] Salt) Hash(string code)
    {
        var salt = RandomNumberGenerator.GetBytes(32);
        return (Derive(code, salt), salt);
    }

    public static bool Verify(string code, byte[] expectedHash, byte[] salt)
    {
        var actual = Derive(code, salt);
        return CryptographicOperations.FixedTimeEquals(actual, expectedHash);
    }

    private static byte[] Derive(string code, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(
            code, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
}
