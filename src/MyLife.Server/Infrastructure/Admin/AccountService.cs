using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using MyLife.Server.Data;

namespace MyLife.Server.Infrastructure.Admin;

/// <summary>Einmal-Tokens für Links in Mails: 256 Bit, Base64url im Link, in der Datenbank nur der SHA-256-Hash.</summary>
internal static class OneTimeTokens
{
    public static (string Token, OneTimeToken Entity) Create(TokenPurpose purpose, Guid userId, Guid? createdBy, DateTimeOffset now, TimeSpan lifetime)
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = WebEncoders.Base64UrlEncode(bytes);
        return (token, new OneTimeToken
        {
            Id = Guid.CreateVersion7(),
            Purpose = purpose,
            TokenHash = SHA256.HashData(bytes),
            UserId = userId,
            CreatedBy = createdBy,
            CreatedAt = now,
            ExpiresAt = now.Add(lifetime),
        });
    }

    /// <summary>Findet ein gültiges, unbenutztes Token oder null.</summary>
    public static async Task<OneTimeToken?> FindAsync(MyLifeDbContext db, TokenPurpose purpose, string? token, DateTimeOffset now, CancellationToken cancellationToken)
    {
        byte[] bytes;
        try
        {
            bytes = WebEncoders.Base64UrlDecode(token ?? string.Empty);
        }
        catch (FormatException)
        {
            return null;
        }

        if (bytes.Length != 32)
        {
            return null;
        }

        var hash = SHA256.HashData(bytes);
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
