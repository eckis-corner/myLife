using System.Net.Mail;
using System.Security.Cryptography;
using MyLife.Crypto;
using MyLife.Shared.Auth;
using MyLife.Shared.Setup;

namespace MyLife.Server.Infrastructure;

/// <summary>
/// Prüft Eingaben und die Form des Schlüsselmaterials aus dem Browser. Inhalte kann der Server nicht prüfen,
/// nur Kopfbytes, Längen und ob öffentliche Schlüssel gültige Punkte auf P-256 sind.
/// </summary>
internal static class KeyMaterialValidation
{
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static bool IsEmail(string? value)
    {
        var email = value?.Trim() ?? string.Empty;
        return email.Length <= SetupLimits.MaxEmailLength && MailAddress.TryCreate(email, out var address)
               && address.Address == email && email.Contains('.', StringComparison.Ordinal);
    }

    public static bool IsSalt(byte[]? salt) => salt is { Length: >= CryptoFormat.MinSaltBytes and <= CryptoFormat.MaxSaltBytes };

    public static bool IsAuthHash(byte[]? hash) => hash is { Length: CryptoFormat.KeyLength };

    public static bool IsWrappedSecretKey(byte[]? data) =>
        HasHeader(data, CryptoKind.WrappedKey) && data!.Length == 2 + CryptoFormat.NonceLength + CryptoFormat.KeyLength + CryptoFormat.TagLength;

    // PKCS#8 für P-256 ist je nach Implementierung etwa 67 bis 138 Byte lang.
    public static bool IsWrappedPrivateKey(byte[]? data, CryptoKind kind) =>
        HasHeader(data, kind) && data!.Length is >= 2 + CryptoFormat.NonceLength + CryptoFormat.TagLength + 60 and <= 2 + CryptoFormat.NonceLength + CryptoFormat.TagLength + 200;

    public static bool IsPublicKey(byte[]? key)
    {
        if (key is not { Length: CryptoFormat.PublicKeyLength } || key[0] != 0x04)
        {
            return false;
        }

        try
        {
            using var ecdh = ECDiffieHellman.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = key[1..33], Y = key[33..65] },
            });
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>Prüft ein neues Passwort (Passwortwechsel, Wiederherstellung).</summary>
    public static Dictionary<string, string[]> Validate(PasswordMaterial? material)
    {
        var errors = new Dictionary<string, string[]>();
        if (material is null)
        {
            errors["NewPassword"] = ["required"];
            return errors;
        }

        if (!IsSalt(material.KdfSalt))
        {
            errors[nameof(material.KdfSalt)] = ["invalid-salt"];
        }

        if (!new Argon2Parameters(material.KdfMemoryKiB, material.KdfIterations, material.KdfParallelism).IsValid)
        {
            errors[nameof(material.KdfMemoryKiB)] = ["invalid-kdf-parameters"];
        }

        if (!IsAuthHash(material.AuthHash))
        {
            errors[nameof(material.AuthHash)] = ["invalid-auth-hash"];
        }

        if (!IsWrappedSecretKey(material.WrappedVmk))
        {
            errors[nameof(material.WrappedVmk)] = ["invalid-wrapped-key"];
        }

        return errors;
    }

    private static bool HasHeader(byte[]? data, CryptoKind kind) =>
        data is { Length: > 2 } && data[0] == CryptoFormat.CurrentVersion && data[1] == (byte)kind;
}

/// <summary>Einheitliche Problem-Antworten mit festem Code im Titel (Ressourcenschlüssel im Client).</summary>
internal static class ApiProblems
{
    public static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult Create(int status, string code) =>
        TypedResults.Problem(statusCode: status, title: code, type: $"urn:mylife:problem:{code}");
}
