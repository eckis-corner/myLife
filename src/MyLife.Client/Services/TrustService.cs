using System.Globalization;
using System.Text.Json;
using MyLife.Crypto;
using MyLife.Shared.Trust;

namespace MyLife.Client.Services;

/// <summary>Eine Vertrauensperson aus Sicht des Eigentümers, mit entschlüsseltem Profil.</summary>
public sealed record Trustee(PersonTrust Person, TrusteeProfile Profile, bool CanRequestAccess, DateTimeOffset CreatedAt, IReadOnlyList<GrantDto> Grants)
{
    /// <summary>Umschläge für einen älteren Schlüssel der Person: neu speichern versiegelt sie für den aktuellen.</summary>
    public bool Stale => Grants.Any(g => !g.Fingerprint.AsSpan().SequenceEqual(Person.Fingerprint));

    public IEnumerable<(int Area, GrantMode Mode)> Areas => Grants.Where(g => g.Kind == GrantKind.Area).Select(g => (g.Area!.Value, g.Mode));

    public IEnumerable<(Guid Letter, GrantMode Mode)> Letters => Grants.Where(g => g.Kind == GrantKind.Letter).Select(g => (g.ObjectId!.Value, g.Mode));
}

/// <summary>
/// Vertrauenspersonen und Freigaben im Browser des Eigentümers (M2.3, Architektur 4.2): Profil verschlüsseln,
/// Bereichs- und Briefschlüssel per ECIES an die Person versiegeln und jeden Umschlag signieren. Für Personen mit
/// geändertem Schlüssel entstehen keine Umschläge; ein noch unbestätigter Schlüssel wird dabei gepinnt.
/// Endet „Sofort teilen“, bekommt der Bereich einen neuen Schlüssel.
/// </summary>
public sealed class TrustService(TrustClient api, FamilyClient family, KeyTrustService keys, VaultService vault, IMyLifeCrypto crypto, SessionService session)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<Trustee>> GetTrusteesAsync()
    {
        var (vmk, _, _) = Keys();
        var people = (await keys.GetPeopleAsync()).ToDictionary(p => p.Person.Id);
        var result = new List<Trustee>();
        foreach (var dto in await api.GetTrusteesAsync())
        {
            if (!people.TryGetValue(dto.TrusteeId, out var person))
            {
                continue;
            }

            TrusteeProfile profile;
            try
            {
                profile = JsonSerializer.Deserialize<TrusteeProfile>(
                    await crypto.DecryptAsync(vmk, dto.EncryptedProfile, KeyContexts.TrusteeProfile(dto.TrusteeId)), Json) ?? new TrusteeProfile(string.Empty, null);
            }
            catch (Exception exception) when (exception is System.Security.Cryptography.CryptographicException or JsonException)
            {
                profile = new TrusteeProfile("?", null);
            }

            result.Add(new Trustee(person, profile, dto.CanRequestAccess, dto.CreatedAt, dto.Grants));
        }

        return result;
    }

    /// <summary>
    /// Speichert eine Vertrauensperson mit genau diesen Freigaben. Alle Umschläge der Person werden neu versiegelt;
    /// für Bereiche, die nicht mehr sofort geteilt sind, wechselt danach der Bereichsschlüssel.
    /// </summary>
    public async Task SaveAsync(
        PersonTrust person, TrusteeProfile profile, bool canRequestAccess, IReadOnlySet<(int Area, GrantMode Mode)> areas,
        IReadOnlySet<(Guid Letter, GrantMode Mode)> letters, Trustee? existing)
    {
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(areas);
        ArgumentNullException.ThrowIfNull(letters);
        if (person.State == TrustState.Changed)
        {
            throw new TrustException(TrustErrors.KeyChanged);
        }

        var (vmk, signingKey, me) = Keys();
        await vault.EnsureLoadedAsync();
        if (person.State == TrustState.Unconfirmed)
        {
            // Erster Kontakt ohne Bestätigung: den heutigen Schlüssel merken, damit ein späterer Austausch auffällt.
            await family.PinAsync(person.Person.Id, await crypto.EncryptAsync(vmk, person.Fingerprint, KeyContexts.Pin(person.Person.Id)));
        }

        var grants = new List<GrantRequest>();
        foreach (var (area, mode) in areas.OrderBy(a => a.Area).ThenBy(a => a.Mode))
        {
            await vault.AreaKeyAsync(area);
            var envelope = await crypto.SealKeyAsync(vmk, vault.WrappedAreaKey(area)!, KeyContexts.Area(area), person.Person.PublicEncryptionKey,
                KeyContexts.AreaEnvelope(me, area));
            grants.Add(new GrantRequest(GrantKind.Area, area, null, mode, envelope,
                await SignAsync(signingKey, envelope, person.Person.Id, GrantKind.Area, Resource(area), mode), person.Fingerprint));
        }

        foreach (var (letterId, mode) in letters.OrderBy(l => l.Letter).ThenBy(l => l.Mode))
        {
            var letter = vault.Find(letterId) ?? throw new TrustException(TrustErrors.NotFound);
            if (letter.WrappedKey is null)
            {
                // Brief aus der Zeit vor M2.3: Speichern gibt ihm seinen eigenen Schlüssel.
                letter = await vault.UpdateAsync(letter, letter.Payload, letter.Status);
            }

            var envelope = await crypto.SealKeyAsync(await vault.AreaKeyAsync(letter.Area), letter.WrappedKey!, KeyContexts.ObjectKey(letter.Id),
                person.Person.PublicEncryptionKey, KeyContexts.LetterEnvelope(me, letter.Id));
            grants.Add(new GrantRequest(GrantKind.Letter, null, letter.Id, mode, envelope,
                await SignAsync(signingKey, envelope, person.Person.Id, GrantKind.Letter, Resource(letter.Id), mode), person.Fingerprint));
        }

        var encryptedProfile = await crypto.EncryptAsync(vmk, JsonSerializer.SerializeToUtf8Bytes(profile, Json), KeyContexts.TrusteeProfile(person.Person.Id));
        await api.SaveTrusteeAsync(person.Person.Id, new SaveTrusteeRequest(encryptedProfile, canRequestAccess, grants));

        foreach (var area in LiveAreas(existing).Except(areas.Where(a => a.Mode == GrantMode.Live).Select(a => a.Area)))
        {
            await RotateAsync(area);
        }
    }

    /// <summary>Vertrauensperson entfernen; sofort geteilte Bereiche bekommen danach einen neuen Schlüssel.</summary>
    public async Task RemoveAsync(Trustee trustee)
    {
        ArgumentNullException.ThrowIfNull(trustee);
        await api.RemoveTrusteeAsync(trustee.Person.Person.Id);
        foreach (var area in LiveAreas(trustee))
        {
            await RotateAsync(area);
        }
    }

    /// <summary>Neuer Schlüssel für einen Bereich; alle verbleibenden Umschläge des Bereichs werden neu versiegelt.</summary>
    public async Task RotateAsync(int area)
    {
        var (vmk, signingKey, me) = Keys();
        var grants = await api.GetAreaGrantsAsync(area);
        var people = (await keys.GetPeopleAsync()).ToDictionary(p => p.Person.Id);
        await vault.RotateAreaAsync(area, async newWrappedKey =>
        {
            var resealed = new List<RotatedGrant>();
            foreach (var grant in grants)
            {
                if (!people.TryGetValue(grant.TrusteeId, out var person) || person.State == TrustState.Changed)
                {
                    throw new TrustException(TrustErrors.KeyChanged);
                }

                var envelope = await crypto.SealKeyAsync(vmk, newWrappedKey, KeyContexts.Area(area), person.Person.PublicEncryptionKey, KeyContexts.AreaEnvelope(me, area));
                resealed.Add(new RotatedGrant(grant.Id, envelope, await SignAsync(signingKey, envelope, grant.TrusteeId, GrantKind.Area, Resource(area), grant.Mode)));
            }

            return resealed;
        });
    }

    /// <summary>
    /// Schlüsselwechsel nach einer Rücknahme (M2.6): neue Bereichsschlüssel und neue Briefschlüssel, alle verbleibenden
    /// Umschläge neu versiegelt. Jeder Schritt gilt für sich; schlägt einer fehl, laufen die anderen trotzdem, und
    /// der Server merkt sich, was noch fehlt.
    /// </summary>
    public async Task CompleteRekeyAsync(MyLife.Shared.Emergency.RekeyDto pending)
    {
        ArgumentNullException.ThrowIfNull(pending);
        Exception? first = null;
        foreach (var area in pending.Areas)
        {
            try
            {
                await RotateAsync(area);
            }
            catch (Exception exception) when (exception is TrustException or VaultException)
            {
                first ??= exception;
            }
        }

        foreach (var letter in pending.Letters)
        {
            try
            {
                await RekeyLetterAsync(letter);
            }
            catch (Exception exception) when (exception is TrustException or VaultException)
            {
                first ??= exception;
            }
        }

        if (first is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(first);
        }
    }

    /// <summary>Neuer Schlüssel für einen Brief; alle Umschläge des Briefs werden neu versiegelt.</summary>
    public async Task RekeyLetterAsync(Guid letterId)
    {
        var (_, signingKey, me) = Keys();
        var grants = await api.GetLetterGrantsAsync(letterId);
        var people = (await keys.GetPeopleAsync()).ToDictionary(p => p.Person.Id);
        await vault.RekeyObjectAsync(letterId, async (areaKey, newWrappedKey) =>
        {
            var resealed = new List<RotatedGrant>();
            foreach (var grant in grants)
            {
                if (!people.TryGetValue(grant.TrusteeId, out var person) || person.State == TrustState.Changed)
                {
                    throw new TrustException(TrustErrors.KeyChanged);
                }

                var envelope = await crypto.SealKeyAsync(areaKey, newWrappedKey, KeyContexts.ObjectKey(letterId), person.Person.PublicEncryptionKey,
                    KeyContexts.LetterEnvelope(me, letterId));
                resealed.Add(new RotatedGrant(grant.Id, envelope, await SignAsync(signingKey, envelope, grant.TrusteeId, GrantKind.Letter, Resource(letterId), grant.Mode)));
            }

            return resealed;
        });
    }

    internal static string Resource(int area) => area.ToString(CultureInfo.InvariantCulture);

    internal static string Resource(Guid objectId) => objectId.ToString("D");

    internal static string SignatureContext(Guid trusteeId, GrantKind kind, string resource, GrantMode mode) =>
        KeyContexts.GrantSignature(trusteeId, (int)kind, resource, (int)mode);

    private Task<byte[]> SignAsync(CryptoKeyHandle signingKey, byte[] envelope, Guid trusteeId, GrantKind kind, string resource, GrantMode mode) =>
        crypto.SignAsync(signingKey, envelope, SignatureContext(trusteeId, kind, resource, mode));

    private static IEnumerable<int> LiveAreas(Trustee? trustee) =>
        trustee?.Areas.Where(a => a.Mode == GrantMode.Live).Select(a => a.Area).Distinct() ?? [];

    private (CryptoKeyHandle VaultKey, CryptoKeyHandle SigningKey, Guid Me) Keys() =>
        session is { State: SessionState.Unlocked, VaultKey: { } vmk, SigningKey: { } signing, Session: { } current }
            ? (vmk, signing, current.UserId)
            : throw new InvalidOperationException("Tresor ist gesperrt.");
}
