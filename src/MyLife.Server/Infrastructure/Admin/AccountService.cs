using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using MyLife.Server.Data;

namespace MyLife.Server.Infrastructure.Admin;

/// <summary>Einmal-Tokens für Links in Mails: 256 Bit, Base64url im Link, in der Datenbank nur der SHA-256-Hash.</summary>
internal static class OneTimeTokens
{
    /// <summary>Neues Token für einen Link und sein Hash für die Datenbank.</summary>
    public static (string Token, byte[] Hash) NewToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return (WebEncoders.Base64UrlEncode(bytes), SHA256.HashData(bytes));
    }

    /// <summary>Hash eines Tokens aus einem Link oder null, wenn es nicht die Form eines Tokens hat.</summary>
    public static byte[]? HashOf(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 64)
        {
            return null;
        }

        try
        {
            var bytes = WebEncoders.Base64UrlDecode(token);
            return bytes.Length == 32 ? SHA256.HashData(bytes) : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public static (string Token, OneTimeToken Entity) Create(TokenPurpose purpose, Guid userId, Guid? createdBy, DateTimeOffset now, TimeSpan lifetime)
    {
        var (token, hash) = NewToken();
        return (token, new OneTimeToken
        {
            Id = Guid.CreateVersion7(),
            Purpose = purpose,
            TokenHash = hash,
            UserId = userId,
            CreatedBy = createdBy,
            CreatedAt = now,
            ExpiresAt = now.Add(lifetime),
        });
    }

    /// <summary>Findet ein gültiges, unbenutztes Token oder null.</summary>
    public static async Task<OneTimeToken?> FindAsync(MyLifeDbContext db, TokenPurpose purpose, string? token, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (HashOf(token) is not { } hash)
        {
            return null;
        }

        return await db.OneTimeTokens.SingleOrDefaultAsync(
            t => t.TokenHash == hash && t.Purpose == purpose && t.UsedAt == null && t.ExpiresAt > now, cancellationToken);
    }
}

/// <summary>Endgültiges Löschen eines Kontos mit Tresor (Entscheidung 44).</summary>
internal static class AccountDeletion
{
    /// <summary>
    /// Löscht Dateien, Tresor, Passkeys, Tokens und die Protokolleinträge des Kontos; die Zeilen in der Datenbank
    /// fallen über die Fremdschlüssel mit. Speichern muss der Aufrufer.
    /// </summary>
    public static async Task DeleteAsync(MyLifeDbContext db, FileStore files, User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(user);
        var storageKeys = await db.Documents.Where(d => d.OwnerId == user.Id).Select(d => d.StorageKey).ToListAsync(cancellationToken);
        foreach (var key in storageKeys)
        {
            files.Delete(key);
        }

        await db.AuditLog.Where(a => a.OwnerId == user.Id).ExecuteDeleteAsync(cancellationToken);
        db.Users.Remove(user);
    }
}
