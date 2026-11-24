using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace MyLife.Crypto;

/// <summary>
/// Konstanten und Bausteine des Verschlüsselungsformats, Version 1. Beschreibung: docs/kryptoformat.md.
/// myLife.crypto.js (Browser) und <see cref="ReferenceMyLifeCrypto"/> (.NET) müssen byte-genau übereinstimmen.
/// </summary>
public static class CryptoFormat
{
    public const int CurrentVersion = 1;

    public const int NonceLength = 12;
    public const int TagLength = 16;
    public const int KeyLength = 32;
    public const int PublicKeyLength = 65;
    public const int SignatureLength = 64;
    public const int MaxContextBytes = 256;
    public const int MaxChunkBytes = 1024 * 1024;
    public const int MinSaltBytes = 16;
    public const int MaxSaltBytes = 64;

    /// <summary>Gesamtlänge eines Umschlags: Kopf, ephemerer Schlüssel, Nonce, verschlüsselter Schlüssel, Tag.</summary>
    public const int EnvelopeLength = 2 + PublicKeyLength + NonceLength + KeyLength + TagLength;

    internal const string InfoAuth = "mylife/auth";
    internal const string InfoKek = "mylife/kek";
    internal const string InfoRecovery = "mylife/recovery";
    internal const string InfoEcies = "mylife/ecies";

    internal static byte[] Header(CryptoKind kind) => [CurrentVersion, (byte)kind];

    internal static byte[] ContextBytes(string context)
    {
        if (string.IsNullOrEmpty(context))
        {
            throw new CryptographicException("mylife:invalid-context");
        }

        var bytes = Encoding.UTF8.GetBytes(context);
        return bytes.Length <= MaxContextBytes ? bytes : throw new CryptographicException("mylife:invalid-context");
    }

    /// <summary>AAD aller AES-GCM-Formate außer Dateiblöcken: Version | Art | Kontext (UTF-8).</summary>
    internal static byte[] Aad(CryptoKind kind, string context) => [.. Header(kind), .. ContextBytes(context)];

    internal static byte[] ChunkNonce(uint index)
    {
        var nonce = new byte[NonceLength];
        BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(NonceLength - 4), index);
        return nonce;
    }

    /// <summary>AAD eines Dateiblocks: Version | Art 0x06 | Blocknummer (uint32 BE) | letzter Block (0/1) | Kontext.</summary>
    internal static byte[] ChunkAad(uint index, bool isLast, string context)
    {
        var indexBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(indexBytes, index);
        return [.. Header(CryptoKind.FileChunk), .. indexBytes, isLast ? (byte)1 : (byte)0, .. ContextBytes(context)];
    }

    /// <summary>Signierte Nachricht: Version | Art 0x07 | Kontextlänge (uint32 BE) | Kontext | Daten.</summary>
    internal static byte[] SignedMessage(string context, byte[] data)
    {
        var contextBytes = ContextBytes(context);
        var length = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)contextBytes.Length);
        return [.. Header(CryptoKind.Signature), .. length, .. contextBytes, .. data];
    }
}

/// <summary>Art eines Datensatzes, zweites Byte jedes Formats.</summary>
public enum CryptoKind : byte
{
    Data = 0x01,
    WrappedKey = 0x02,
    WrappedEcdhKey = 0x03,
    WrappedEcdsaKey = 0x04,
    Envelope = 0x05,
    FileChunk = 0x06,
    Signature = 0x07,
}
