using System.Globalization;
using System.Security.Cryptography;
using MyLife.Crypto;

namespace MyLife.Server.Infrastructure;

/// <summary>
/// Hasht den Anmeldehash aus dem Browser ein zweites Mal mit Argon2id (Architektur 4.4). Ein Abzug der Datenbank
/// reicht damit nicht, um sich anzumelden. Weil die Eingabe bereits 256 Bit Entropie hat, genügen moderate
/// Parameter, die auch die Synology (Celeron J4125) schnell rechnet. Format: PHC-String.
/// </summary>
internal static class AuthHashHasher
{
    private const int MemoryKiB = 19456;
    private const int Iterations = 2;
    private const int Parallelism = 1;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    public static async Task<string> HashAsync(byte[] authHash)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = await Argon2.HashAsync(authHash, salt, MemoryKiB, Iterations, Parallelism, HashBytes);
        return string.Create(CultureInfo.InvariantCulture,
            $"$argon2id$v=19$m={MemoryKiB},t={Iterations},p={Parallelism}${Encode(salt)}${Encode(hash)}");
    }

    public static async Task<bool> VerifyAsync(string phc, byte[] authHash)
    {
        ArgumentNullException.ThrowIfNull(phc);
        var parts = phc.Split('$');
        if (parts is not ["", "argon2id", "v=19", var parameters, var salt, var expected])
        {
            return false;
        }

        var values = parameters.Split(',').Select(p => p.Split('=')).ToDictionary(p => p[0], p => int.Parse(p[1], CultureInfo.InvariantCulture));
        var expectedBytes = Decode(expected);
        var actual = await Argon2.HashAsync(authHash, Decode(salt), values["m"], values["t"], values["p"], expectedBytes.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expectedBytes);
    }

    private static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=');

    private static byte[] Decode(string value) => Convert.FromBase64String(value.PadRight(value.Length + ((4 - (value.Length % 4)) % 4), '='));
}
