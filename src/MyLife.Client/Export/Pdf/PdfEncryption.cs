using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MyLife.Client.Export.Pdf;

/// <summary>
/// Passwortschutz nach ISO 32000-2 (PDF 2.0), Standard Security Handler Revision 6 mit AES-256 (AESV3).
/// Algorithmen 2.B (Hash), 8 (U/UE), 9 (O/OE) und 10 (Perms). Das Besitzerpasswort ist zufällig und wird verworfen;
/// wer das Benutzerpasswort kennt, darf lesen, drucken und kopieren.
/// </summary>
public sealed class PdfEncryptor
{
    /// <summary>Alle Rechte (Bits 3–12 gesetzt, Bits 1–2 null).</summary>
    public const int Permissions = -4;

    private readonly IPdfCrypto _crypto;
    private readonly byte[] _fileKey;

    private PdfEncryptor(IPdfCrypto crypto, byte[] fileKey, byte[] u, byte[] ue, byte[] o, byte[] oe, byte[] perms)
    {
        _crypto = crypto;
        _fileKey = fileKey;
        U = u;
        UE = ue;
        O = o;
        OE = oe;
        Perms = perms;
    }

    public byte[] U { get; }

    public byte[] UE { get; }

    public byte[] O { get; }

    public byte[] OE { get; }

    public byte[] Perms { get; }

    public static Task<PdfEncryptor> CreateAsync(IPdfCrypto crypto, string userPassword) => CreateAsync(crypto, PasswordBytes(userPassword));

    public static async Task<PdfEncryptor> CreateAsync(IPdfCrypto crypto, byte[] user)
    {
        ArgumentNullException.ThrowIfNull(crypto);
        var fileKey = RandomNumberGenerator.GetBytes(32);
        var owner = PasswordBytes(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));

        // Algorithmus 8: U = Hash(Passwort, Prüf-Salt) | Prüf-Salt | Schlüssel-Salt, UE = AES-256(Hash(Passwort, Schlüssel-Salt), Dateischlüssel)
        var userValidationSalt = RandomNumberGenerator.GetBytes(8);
        var userKeySalt = RandomNumberGenerator.GetBytes(8);
        byte[] u = [.. await HashAsync(crypto, user, userValidationSalt, []), .. userValidationSalt, .. userKeySalt];
        var ue = await AesNoPaddingAsync(crypto, await HashAsync(crypto, user, userKeySalt, []), fileKey);

        // Algorithmus 9: wie 8, mit U als zusätzlichen Daten.
        var ownerValidationSalt = RandomNumberGenerator.GetBytes(8);
        var ownerKeySalt = RandomNumberGenerator.GetBytes(8);
        byte[] o = [.. await HashAsync(crypto, owner, ownerValidationSalt, u), .. ownerValidationSalt, .. ownerKeySalt];
        var oe = await AesNoPaddingAsync(crypto, await HashAsync(crypto, owner, ownerKeySalt, u), fileKey);

        // Algorithmus 10: Rechte, mit dem Dateischlüssel verschlüsselt (ein Block, ECB = CBC mit Null-IV).
        var permsBlock = new byte[16];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(permsBlock.AsSpan(0, 4), Permissions);
        permsBlock[4] = permsBlock[5] = permsBlock[6] = permsBlock[7] = 0xFF;
        permsBlock[8] = (byte)'T';
        permsBlock[9] = (byte)'a';
        permsBlock[10] = (byte)'d';
        permsBlock[11] = (byte)'b';
        RandomNumberGenerator.Fill(permsBlock.AsSpan(12));
        var perms = await AesNoPaddingAsync(crypto, fileKey, permsBlock);

        return new PdfEncryptor(crypto, fileKey, u, ue, o, oe, perms);
    }

    /// <summary>Zeichenkette oder Stream verschlüsseln (Algorithmus 1.A): 16 Byte IV, dann AES-256-CBC mit Auffüllung.</summary>
    public async Task<byte[]> EncryptAsync(byte[] data)
    {
        var iv = RandomNumberGenerator.GetBytes(16);
        return [.. iv, .. await _crypto.AesCbcEncryptAsync(_fileKey, iv, data)];
    }

    /// <summary>Das /Encrypt-Wörterbuch.</summary>
    public string Dictionary() => string.Create(
        CultureInfo.InvariantCulture,
        $"<< /Filter /Standard /V 5 /R 6 /Length 256 /CF << /StdCF << /AuthEvent /DocOpen /CFM /AESV3 /Length 32 >> >> /StmF /StdCF /StrF /StdCF " +
        $"/O <{Convert.ToHexString(O)}> /U <{Convert.ToHexString(U)}> /OE <{Convert.ToHexString(OE)}> /UE <{Convert.ToHexString(UE)}> " +
        $"/P {Permissions} /Perms <{Convert.ToHexString(Perms)}> /EncryptMetadata true >>");

    /// <summary>Passwort in Unicode NFC (SASLprep verlangt NFKC, das im Browser fehlt; für übliche Passwörter gleich), UTF-8, höchstens 127 Byte.</summary>
    public static byte[] PasswordBytes(string password)
    {
        var bytes = Encoding.UTF8.GetBytes((password ?? string.Empty).Normalize(NormalizationForm.FormC));
        return bytes.Length > 127 ? bytes[..127] : bytes;
    }

    /// <summary>Algorithmus 2.B: iterierter Hash aus SHA-256/384/512 und AES-128-CBC.</summary>
    public static async Task<byte[]> HashAsync(IPdfCrypto crypto, byte[] password, byte[] salt, byte[] userData)
    {
        ArgumentNullException.ThrowIfNull(crypto);
        var k = await crypto.DigestAsync(256, [.. password, .. salt, .. userData]);
        byte[] e = [0];
        var round = 0;
        while (round < 64 || e[^1] > round - 32)
        {
            byte[] block = [.. password, .. k, .. userData];
            var k1 = new byte[block.Length * 64];
            for (var i = 0; i < 64; i++)
            {
                block.CopyTo(k1, i * block.Length);
            }

            // Länge ist ein Vielfaches von 64, also ohne Auffüllung: den angehängten Füllblock verwerfen.
            e = (await crypto.AesCbcEncryptAsync(k[..16], k[16..32], k1))[..k1.Length];
            var remainder = 0;
            for (var i = 0; i < 16; i++)
            {
                remainder += e[i];
            }

            k = await crypto.DigestAsync((remainder % 3) switch { 0 => 256, 1 => 384, _ => 512 }, e);
            round++;
        }

        return k[..32];
    }

    /// <summary>AES-CBC mit Null-IV über ein Vielfaches von 16 Byte, ohne Auffüllung.</summary>
    private static async Task<byte[]> AesNoPaddingAsync(IPdfCrypto crypto, byte[] key, byte[] data) =>
        (await crypto.AesCbcEncryptAsync(key, new byte[16], data))[..data.Length];
}
