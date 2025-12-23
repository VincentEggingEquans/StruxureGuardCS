using System;
using System.Security.Cryptography;

namespace StruxureGuard.Core.Security;

public static class PasswordVerifier
{
    // PBKDF2 params
    private const int Iterations = 100_000;

    // Salt + hash voor password "Vincent"
    // (PBKDF2-HMACSHA256, 32 bytes output)
    private static readonly byte[] Salt = Convert.FromBase64String("gA+uCTUxxq7lc+TVWUkqVA==");
    private static readonly byte[] ExpectedHash = Convert.FromBase64String("QGMSx4RZ6sjNzRlIRHNRny75e3pcldwKPQiglRcSEDY=");

    public static bool Verify(string password)
    {
        if (string.IsNullOrEmpty(password))
            return false;

        using var pbkdf2 = new Rfc2898DeriveBytes(password, Salt, Iterations, HashAlgorithmName.SHA256);
        var computed = pbkdf2.GetBytes(ExpectedHash.Length);

        return FixedTimeEquals(computed, ExpectedHash);
    }

    private static bool FixedTimeEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;

        int diff = 0;
        for (int i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];

        return diff == 0;
    }
}
