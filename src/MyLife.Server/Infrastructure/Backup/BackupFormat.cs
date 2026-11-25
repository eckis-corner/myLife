using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MyLife.Server.Infrastructure.Backup;

/// <summary>Klartext-Kopf einer Sicherung; er verrät nur Zeitpunkt, Version und Salz, keine Inhalte.</summary>
internal sealed record BackupHeader(string Format, int Version, DateTimeOffset CreatedAt, string AppVersion, string Salt);

/// <summary>Arten der Einträge im verschlüsselten Teil.</summary>
internal enum BackupEntryKind : byte
{
    End = 0,
    Manifest = 1,
    Table = 2,
    Document = 3,
    Key = 4,
}

/// <summary>Inhaltsverzeichnis: Migrationsstand und Tabellen in der Reihenfolge, in der sie zurückgespielt werden.</summary>
internal sealed record BackupManifest(string Format, int Version, DateTimeOffset CreatedAt, string Migration, IReadOnlyList<string> Tables, int Documents, int Keys);

/// <summary>
/// Format einer Server-Sicherung (M3.4, docs/betrieb/installation.md, Abschnitt 8):
/// <code>
/// "MYLIFEBK" | Version (1) | Kopflänge (uint32 BE) | Kopf (JSON)
/// Blöcke: AES-256-GCM, Klartext je 1 MiB (der letzte kürzer), Chiffrat | Tag
///   Schlüssel = HKDF-SHA-256(Sicherungsschlüssel, Salz aus dem Kopf, "mylife/backup")
///   Nonce     = 00 × 8 | uint32 BE (Blocknummer)
///   AAD       = "mylife-backup" | SHA-256(Kopf) | uint32 BE (Blocknummer) | letzter Block (0/1)
/// Klartext: Einträge Art (1) | Namenslänge (uint16 BE) | Name (UTF-8) | Länge (int64 BE) | Daten, zuletzt Art 0.
/// </code>
/// Die Inhalte der Tresore sind ohnehin Chiffrat; die Sicherung schützt zusätzlich alles Übrige (Architektur 7).
/// </summary>
internal static class BackupFormat
{
    public const string FileExtension = ".mlbak";
    public const int ChunkSize = 1024 * 1024;
    public const int TagSize = 16;
    public const int KeySize = 32;

    private static readonly byte[] Magic = "MYLIFEBK"u8.ToArray();
    private static readonly byte[] AadPrefix = "mylife-backup"u8.ToArray();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Sicherungsschlüssel aus der Konfiguration: 32 Byte als Base64 (etwa <c>openssl rand -base64 32</c>).</summary>
    public static byte[]? ParseKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            var key = Convert.FromBase64String(value.Trim());
            return key.Length == KeySize ? key : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public static async Task<byte[]> WriteHeaderAsync(Stream output, BackupHeader header, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(header, Json);
        var prefix = new byte[Magic.Length + 1 + 4];
        Magic.CopyTo(prefix, 0);
        prefix[Magic.Length] = 1;
        BinaryPrimitives.WriteUInt32BigEndian(prefix.AsSpan(Magic.Length + 1), (uint)json.Length);
        await output.WriteAsync(prefix, cancellationToken);
        await output.WriteAsync(json, cancellationToken);
        return json;
    }

    public static async Task<(BackupHeader Header, byte[] Raw)> ReadHeaderAsync(Stream input, CancellationToken cancellationToken)
    {
        var prefix = new byte[Magic.Length + 1 + 4];
        await input.ReadExactlyAsync(prefix, cancellationToken);
        if (!prefix.AsSpan(0, Magic.Length).SequenceEqual(Magic) || prefix[Magic.Length] != 1)
        {
            throw new InvalidDataException("Keine myLife-Sicherung oder unbekannte Version.");
        }

        var length = BinaryPrimitives.ReadUInt32BigEndian(prefix.AsSpan(Magic.Length + 1));
        if (length is 0 or > 64 * 1024)
        {
            throw new InvalidDataException("Kopf der Sicherung ist beschädigt.");
        }

        var raw = new byte[length];
        await input.ReadExactlyAsync(raw, cancellationToken);
        var header = JsonSerializer.Deserialize<BackupHeader>(raw, Json) ?? throw new InvalidDataException("Kopf der Sicherung ist beschädigt.");
        return (header, raw);
    }

    public static byte[] DeriveKey(byte[] backupKey, byte[] salt) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, backupKey, KeySize, salt, "mylife/backup"u8.ToArray());

    public static byte[] Nonce(uint index)
    {
        var nonce = new byte[12];
        BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(8), index);
        return nonce;
    }

    public static byte[] Aad(byte[] headerHash, uint index, bool isLast)
    {
        var aad = new byte[AadPrefix.Length + headerHash.Length + 5];
        AadPrefix.CopyTo(aad, 0);
        headerHash.CopyTo(aad, AadPrefix.Length);
        BinaryPrimitives.WriteUInt32BigEndian(aad.AsSpan(AadPrefix.Length + headerHash.Length), index);
        aad[^1] = isLast ? (byte)1 : (byte)0;
        return aad;
    }

    public static async Task WriteEntryAsync(Stream output, BackupEntryKind kind, string name, long length, Func<Stream, Task> writeData, CancellationToken cancellationToken)
    {
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var head = new byte[1 + 2 + nameBytes.Length + 8];
        head[0] = (byte)kind;
        BinaryPrimitives.WriteUInt16BigEndian(head.AsSpan(1), (ushort)nameBytes.Length);
        nameBytes.CopyTo(head, 3);
        BinaryPrimitives.WriteInt64BigEndian(head.AsSpan(3 + nameBytes.Length), length);
        await output.WriteAsync(head, cancellationToken);
        await writeData(output);
    }

    public static async Task<(BackupEntryKind Kind, string Name, long Length)> ReadEntryHeadAsync(Stream input, CancellationToken cancellationToken)
    {
        var start = new byte[1];
        await input.ReadExactlyAsync(start, cancellationToken);
        var kind = (BackupEntryKind)start[0];
        if (kind == BackupEntryKind.End)
        {
            return (kind, string.Empty, 0);
        }

        var nameLength = new byte[2];
        await input.ReadExactlyAsync(nameLength, cancellationToken);
        var nameBytes = new byte[BinaryPrimitives.ReadUInt16BigEndian(nameLength)];
        await input.ReadExactlyAsync(nameBytes, cancellationToken);
        var length = new byte[8];
        await input.ReadExactlyAsync(length, cancellationToken);
        var value = BinaryPrimitives.ReadInt64BigEndian(length);
        if (!Enum.IsDefined(kind) || value < 0)
        {
            throw new InvalidDataException("Eintrag der Sicherung ist beschädigt.");
        }

        return (kind, Encoding.UTF8.GetString(nameBytes), value);
    }

    public static byte[] Serialize<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Json);

    public static T Deserialize<T>(byte[] json) => JsonSerializer.Deserialize<T>(json, Json) ?? throw new InvalidDataException("Inhaltsverzeichnis fehlt.");
}

/// <summary>Verschlüsselt alles, was hineingeschrieben wird, in Blöcken zu 1 MiB; <see cref="CompleteAsync"/> schreibt den letzten Block.</summary>
internal sealed class BackupEncryptingStream(Stream inner, byte[] key, byte[] headerHash) : Stream
{
    private readonly AesGcm _aes = new(key, BackupFormat.TagSize);
    private readonly byte[] _buffer = new byte[BackupFormat.ChunkSize];
    private int _filled;
    private uint _index;
    private bool _completed;

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        while (!buffer.IsEmpty)
        {
            // Ein voller Block geht erst raus, wenn danach noch etwas kommt: Der letzte Block trägt die Markierung.
            if (_filled == _buffer.Length)
            {
                await FlushChunkAsync(isLast: false, cancellationToken);
            }

            var count = Math.Min(buffer.Length, _buffer.Length - _filled);
            buffer[..count].CopyTo(_buffer.AsMemory(_filled));
            _filled += count;
            buffer = buffer[count..];
        }
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Write(byte[] buffer, int offset, int count) => WriteAsync(buffer, offset, count).GetAwaiter().GetResult();

    public async Task CompleteAsync(CancellationToken cancellationToken)
    {
        await FlushChunkAsync(isLast: true, cancellationToken);
        _completed = true;
        await inner.FlushAsync(cancellationToken);
    }

    private async Task FlushChunkAsync(bool isLast, CancellationToken cancellationToken)
    {
        if (_completed)
        {
            throw new InvalidOperationException("Sicherung ist schon abgeschlossen.");
        }

        var output = new byte[_filled + BackupFormat.TagSize];
        _aes.Encrypt(BackupFormat.Nonce(_index), _buffer.AsSpan(0, _filled), output.AsSpan(0, _filled), output.AsSpan(_filled), BackupFormat.Aad(headerHash, _index, isLast));
        await inner.WriteAsync(output, cancellationToken);
        _index++;
        _filled = 0;
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _aes.Dispose();
        }

        base.Dispose(disposing);
    }
}

/// <summary>Entschlüsselt die Blöcke einer Sicherung; erkennt Manipulation, vertauschte und fehlende Blöcke.</summary>
internal sealed class BackupDecryptingStream(Stream inner, long cipherLength, byte[] key, byte[] headerHash) : Stream
{
    private readonly AesGcm _aes = new(key, BackupFormat.TagSize);
    private readonly byte[] _cipher = new byte[BackupFormat.ChunkSize + BackupFormat.TagSize];
    private byte[] _plain = [];
    private int _offset;
    private long _remaining = cipherLength;
    private uint _index;
    private bool _finished;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_offset == _plain.Length)
        {
            if (_finished)
            {
                return 0;
            }

            await ReadChunkAsync(cancellationToken);
        }

        var count = Math.Min(buffer.Length, _plain.Length - _offset);
        _plain.AsMemory(_offset, count).CopyTo(buffer);
        _offset += count;
        return count;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer, offset, count).GetAwaiter().GetResult();

    private async Task ReadChunkAsync(CancellationToken cancellationToken)
    {
        var size = (int)Math.Min(_remaining, _cipher.Length);
        if (size < BackupFormat.TagSize)
        {
            throw new InvalidDataException("Sicherung ist unvollständig.");
        }

        await inner.ReadExactlyAsync(_cipher.AsMemory(0, size), cancellationToken);
        _remaining -= size;
        var isLast = _remaining == 0;
        var plain = new byte[size - BackupFormat.TagSize];
        try
        {
            _aes.Decrypt(BackupFormat.Nonce(_index), _cipher.AsSpan(0, plain.Length), _cipher.AsSpan(plain.Length, BackupFormat.TagSize), plain,
                BackupFormat.Aad(headerHash, _index, isLast));
        }
        catch (AuthenticationTagMismatchException exception)
        {
            throw new CryptographicException("Falscher Sicherungsschlüssel oder beschädigte Sicherung.", exception);
        }

        _plain = plain;
        _offset = 0;
        _index++;
        _finished = isLast;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _aes.Dispose();
        }

        base.Dispose(disposing);
    }
}
