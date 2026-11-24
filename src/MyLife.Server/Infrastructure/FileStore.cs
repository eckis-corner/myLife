using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace MyLife.Server.Infrastructure;

/// <summary>
/// Ablage der verschlüsselten Dokumente unter <c>{DataDirectory}/files/{StorageKey}</c> (Architektur 6.3).
/// Der Server sieht nur Chiffrat; er prüft Länge und berechnet die SHA-256 zur Kontrolle.
/// </summary>
internal sealed class FileStore
{
    private readonly string _root;

    public FileStore(IOptions<MyLifeOptions> options)
    {
        _root = Path.Combine(Path.GetFullPath(options.Value.DataDirectory), "files");
        Directory.CreateDirectory(_root);
    }

    public static string NewStorageKey() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));

    /// <summary>Schreibt genau <paramref name="expectedLength"/> Byte. Liefert die SHA-256 oder null, wenn die Länge nicht stimmt.</summary>
    public async Task<byte[]?> WriteAsync(string storageKey, Stream content, long expectedLength, CancellationToken cancellationToken)
    {
        var path = PathFor(storageKey);
        var temporary = path + ".upload";
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long written = 0;
        try
        {
            await using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    written += read;
                    if (written > expectedLength)
                    {
                        break;
                    }

                    hash.AppendData(buffer, 0, read);
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }

            if (written != expectedLength)
            {
                File.Delete(temporary);
                return null;
            }

            File.Move(temporary, path, overwrite: true);
            return hash.GetHashAndReset();
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }
    }

    public Stream? OpenRead(string storageKey)
    {
        var path = PathFor(storageKey);
        return File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true) : null;
    }

    public void Delete(string storageKey)
    {
        var path = PathFor(storageKey);
        File.Delete(path);
        File.Delete(path + ".upload");
    }

    private string PathFor(string storageKey)
    {
        if (storageKey.Length != 32 || !storageKey.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("Ungültiger Ablageschlüssel.", nameof(storageKey));
        }

        return Path.Combine(_root, storageKey);
    }
}
