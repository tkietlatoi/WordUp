using System.IO;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using WordUp.Models;

namespace WordUp.Services;

public sealed partial class LocalStorageService
{
    private static string HashPassword(string password)
    {
        const int iterations = 100_000;
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            32);

        return $"pbkdf2-sha256:{iterations}:{Convert.ToHexString(salt)}:{Convert.ToHexString(hash)}";
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 4
            || parts[0] != "pbkdf2-sha256"
            || !int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        var salt = Convert.FromHexString(parts[2]);
        var expectedHash = Convert.FromHexString(parts[3]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static string FormatDateTime(DateTime value)
    {
        return value.ToUniversalTime().ToString("O");
    }

    private static DateTime ParseDateTime(string value)
    {
        return DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed.ToLocalTime()
            : DateTime.Now;
    }
}
