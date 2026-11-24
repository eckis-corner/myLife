using System.Globalization;
using System.Security.Cryptography;
using MyLife.Crypto;
using MyLife.Shared.Family;

namespace MyLife.Client.Services;

/// <summary>Wie gut der aktuelle Schlüssel einer Person bestätigt ist (Architektur 4.3).</summary>
public enum TrustState
{
    /// <summary>Selbst geprüft und gepinnt; der Schlüssel ist unverändert.</summary>
    Pinned,

    /// <summary>Gegengezeichnet von jemandem, dessen Schlüssel ich selbst gepinnt habe.</summary>
    Endorsed,

    /// <summary>Noch von niemandem bestätigt, den ich kenne.</summary>
    Unconfirmed,

    /// <summary>Der Server zeigt einen anderen Schlüssel als den gepinnten: Warnung, keine Umschläge.</summary>
    Changed,
}

/// <summary>Eine Person mit dem Ergebnis der Prüfung im Browser.</summary>
public sealed record PersonTrust(PersonDto Person, byte[] Fingerprint, TrustState State, string? ConfirmedBy);

/// <summary>Ein neuer Einladungslink; das Geheimnis steht nur im Fragment.</summary>
public sealed record InvitationLink(Guid Id, string Link, bool MailQueued, DateTimeOffset ExpiresAt);

/// <summary>
/// Schlüsselprüfung ohne Telefon (Architektur 4.3), vollständig im Browser: Einladungen mit Geheimnis anlegen,
/// angenommene Einladungen über die HMAC-Bindung prüfen, Schlüssel gegenzeichnen und pinnen, und den Zustand
/// aller Personen gegen die eigenen Pins und die Gegenzeichnungen bewerten. Der Server sieht dabei nur Chiffrat,
/// öffentliche Schlüssel und Signaturen.
/// </summary>
public sealed class KeyTrustService(FamilyClient api, IMyLifeCrypto crypto, SessionService session) : IDisposable
{
    private readonly SemaphoreSlim _sync = new(1, 1);

    public void Dispose() => _sync.Dispose();

    public async Task<byte[]> FingerprintAsync(byte[] publicEncryptionKey, byte[] publicSigningKey) =>
        await crypto.Sha256Async(KeyBinding.FingerprintData(publicEncryptionKey, publicSigningKey));

    /// <summary>Neue Einladung: Geheimnis erzeugen, im eigenen Tresor verschlüsselt ablegen, Link bauen.</summary>
    public async Task<InvitationLink> InviteAsync(string email, string language, bool sendMail, string baseUri)
    {
        var (vaultKey, _, session1) = Keys();
        var id = Guid.CreateVersion7();
        var secret = await crypto.RandomBytesAsync(FamilyLimits.SecretBytes);
        var encrypted = await crypto.EncryptAsync(vaultKey, secret, KeyContexts.InviteSecret(id));
        var fragment = await FragmentAsync(secret, session1);
        var created = await api.CreateInvitationAsync(new CreateInvitationRequest(id, email.Trim(), language, encrypted, sendMail ? fragment : null));
        return new InvitationLink(created.Id, Link(baseUri, created.Token, fragment), created.MailQueued, created.ExpiresAt);
    }

    /// <summary>Neuer Link für eine offene Einladung; das Geheimnis bleibt, der alte Link wird ungültig.</summary>
    public async Task<InvitationLink> RenewAsync(InvitationDto invitation, bool sendMail, string baseUri)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        var (vaultKey, _, session1) = Keys();
        var secret = await crypto.DecryptAsync(vaultKey, invitation.EncryptedSecret, KeyContexts.InviteSecret(invitation.Id));
        var fragment = await FragmentAsync(secret, session1);
        var renewed = await api.RenewInvitationAsync(invitation.Id, sendMail ? fragment : null);
        return new InvitationLink(renewed.Id, Link(baseUri, renewed.Token, fragment), renewed.MailQueued, renewed.ExpiresAt);
    }

    /// <summary>
    /// Prüft angenommene Einladungen und gegenzeichnet gepinnte Personen. Läuft nach dem Entsperren und auf der
    /// Seite „Familie“; liefert die Zahl der Änderungen. Eine falsche Bindung meldet sie dem Server als Warnung.
    /// </summary>
    public async Task<int> SyncAsync()
    {
        if (session.State != SessionState.Unlocked)
        {
            return 0;
        }

        await _sync.WaitAsync();
        try
        {
            var (vaultKey, signingKey, _) = Keys();
            var changes = 0;
            foreach (var pending in await api.GetVerificationsAsync())
            {
                var fingerprint = await FingerprintAsync(pending.PublicEncryptionKey, pending.PublicSigningKey);
                var matches = false;
                try
                {
                    var secret = await crypto.DecryptAsync(vaultKey, pending.EncryptedSecret, KeyContexts.InviteSecret(pending.InvitationId));
                    var expected = await crypto.HmacSha256Async(secret, KeyBinding.InviteBindingData(pending.InvitationId, fingerprint));
                    matches = CryptographicOperations.FixedTimeEquals(expected, pending.Binding);
                }
                catch (CryptographicException)
                {
                    // Geheimnis nicht lesbar: wie eine falsche Bindung behandeln.
                }

                var request = matches
                    ? new VerifyInvitationRequest(
                        true,
                        await crypto.SignAsync(signingKey, fingerprint, KeyContexts.Endorsement(pending.InviteeId)),
                        await crypto.EncryptAsync(vaultKey, fingerprint, KeyContexts.Pin(pending.InviteeId)))
                    : new VerifyInvitationRequest(false, null, null);
                await api.VerifyAsync(pending.InvitationId, request);
                changes++;
            }

            // Gepinnte Personen, deren Schlüssel unverändert ist, gegenzeichnen (auch die einladende Person).
            var me = session.Session!.UserId;
            var pins = await ReadPinsAsync(vaultKey);
            foreach (var person in await api.GetPeopleAsync())
            {
                if (pins.TryGetValue(person.Id, out var pinned) && pinned is not null)
                {
                    var fingerprint = await FingerprintAsync(person.PublicEncryptionKey, person.PublicSigningKey);
                    var endorsed = person.Endorsements.Any(e => e.SignerId == me && e.Fingerprint.AsSpan().SequenceEqual(fingerprint));
                    if (!endorsed && pinned.AsSpan().SequenceEqual(fingerprint))
                    {
                        await api.EndorseAsync(person.Id, await crypto.SignAsync(signingKey, fingerprint, KeyContexts.Endorsement(person.Id)));
                        changes++;
                    }
                }
            }

            return changes;
        }
        finally
        {
            _sync.Release();
        }
    }

    /// <summary>Alle anderen Personen mit dem Zustand ihres Schlüssels aus meiner Sicht.</summary>
    public async Task<IReadOnlyList<PersonTrust>> GetPeopleAsync()
    {
        var (vaultKey, _, _) = Keys();
        var people = await api.GetPeopleAsync();
        var pins = await ReadPinsAsync(vaultKey);
        var fingerprints = new Dictionary<Guid, byte[]>();
        foreach (var person in people)
        {
            fingerprints[person.Id] = await FingerprintAsync(person.PublicEncryptionKey, person.PublicSigningKey);
        }

        // Wem ich selbst vertraue: gepinnt und unverändert.
        var trusted = people.Where(p => pins.TryGetValue(p.Id, out var pin) && pin is not null && pin.AsSpan().SequenceEqual(fingerprints[p.Id]))
            .ToDictionary(p => p.Id);
        var result = new List<PersonTrust>();
        foreach (var person in people)
        {
            var fingerprint = fingerprints[person.Id];
            if (pins.TryGetValue(person.Id, out var pin))
            {
                result.Add(new PersonTrust(person, fingerprint, trusted.ContainsKey(person.Id) ? TrustState.Pinned : TrustState.Changed, null));
                continue;
            }

            string? confirmedBy = null;
            foreach (var endorsement in person.Endorsements.Where(e => trusted.ContainsKey(e.SignerId) && e.Fingerprint.AsSpan().SequenceEqual(fingerprint)))
            {
                var signer = trusted[endorsement.SignerId];
                if (await crypto.VerifyAsync(signer.PublicSigningKey, fingerprint, endorsement.Signature, KeyContexts.Endorsement(person.Id)))
                {
                    confirmedBy = signer.DisplayName;
                    break;
                }
            }

            result.Add(new PersonTrust(person, fingerprint, confirmedBy is null ? TrustState.Unconfirmed : TrustState.Endorsed, confirmedBy));
        }

        return result;
    }

    /// <summary>Fingerprint aus einem Einladungslink: 64 Hex-Zeichen oder null.</summary>
    public static byte[]? ParseFingerprint(string? hex)
    {
        if (hex is not { Length: 64 })
        {
            return null;
        }

        try
        {
            return Convert.FromHexString(hex);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public static string ToBase64Url(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[]? FromBase64Url(string? text)
    {
        if (string.IsNullOrEmpty(text) || text.Length > 100)
        {
            return null;
        }

        var padded = text.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch { 2 => "==", 3 => "=", _ => string.Empty };
        try
        {
            return Convert.FromBase64String(padded);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>Pins entschlüsseln; ein Pin, der sich nicht öffnen lässt, gilt als geändert (Wert null).</summary>
    private async Task<Dictionary<Guid, byte[]?>> ReadPinsAsync(CryptoKeyHandle vaultKey)
    {
        var pins = new Dictionary<Guid, byte[]?>();
        foreach (var pin in await api.GetPinsAsync())
        {
            try
            {
                pins[pin.SubjectId] = await crypto.DecryptAsync(vaultKey, pin.EncryptedPin, KeyContexts.Pin(pin.SubjectId));
            }
            catch (CryptographicException)
            {
                pins[pin.SubjectId] = null;
            }
        }

        return pins;
    }

    private async Task<string> FragmentAsync(byte[] secret, Shared.Auth.SessionDto current)
    {
        var fingerprint = await FingerprintAsync(current.PublicEncryptionKey, current.PublicSigningKey);
        return string.Create(CultureInfo.InvariantCulture, $"s={ToBase64Url(secret)}&f={Convert.ToHexStringLower(fingerprint)}");
    }

    private static string Link(string baseUri, string token, string fragment) => $"{baseUri.TrimEnd('/')}/invite#t={token}&{fragment}";

    private (CryptoKeyHandle VaultKey, CryptoKeyHandle SigningKey, Shared.Auth.SessionDto Session) Keys() =>
        session is { State: SessionState.Unlocked, VaultKey: { } vault, SigningKey: { } signing, Session: { } current }
            ? (vault, signing, current)
            : throw new InvalidOperationException("Tresor ist gesperrt.");
}
