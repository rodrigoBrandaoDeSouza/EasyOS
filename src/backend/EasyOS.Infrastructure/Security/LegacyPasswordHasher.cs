using System.Security.Cryptography;
using System.Text;

namespace EasyOS.Infrastructure.Security;

/// <summary>
/// Reproduz o hash de senha do legado: MD5 dos bytes ASCII, em hex maiúsculo.
/// Fraco, mas obrigatório enquanto o app mobile autenticar na API antiga contra os mesmos bancos.
/// Quando o app migrar, trocar por um hash forte com rehash no login.
/// </summary>
public static class LegacyPasswordHasher
{
    public static string Hash(string plainPassword)
    {
        var bytes = MD5.HashData(Encoding.ASCII.GetBytes(plainPassword));
        return Convert.ToHexString(bytes);
    }

    public static bool Verify(string plainPassword, string? storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash)) return false;
        var computed = Encoding.ASCII.GetBytes(Hash(plainPassword));
        var stored = Encoding.ASCII.GetBytes(storedHash.Trim().ToUpperInvariant());
        return CryptographicOperations.FixedTimeEquals(computed, stored);
    }

    /// <summary>Gera uma senha aleatória (usada no "esqueci minha senha", como no legado).</summary>
    public static string GenerateRandomPassword(int length = 10)
    {
        const string alphabet = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return RandomNumberGenerator.GetString(alphabet, length);
    }
}
