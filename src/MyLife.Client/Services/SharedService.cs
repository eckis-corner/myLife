using System.Security.Cryptography;
using System.Text.Json;
using MyLife.Crypto;
using MyLife.Shared.Trust;
using MyLife.Shared.Vault;

namespace MyLife.Client.Services;

/// <summary>Ein Eigentümer, der etwas mit mir teilt, und ob ich seinem Schlüssel traue.</summary>
public sealed record SharedOwner(Guid OwnerId, string Name, TrustState? KeyState, IReadOnlyList<SharedGrant> Grants);

/// <summary>Ein ausgelieferter Umschlag. <see cref="Verified"/>: Signatur des Eigentümers geprüft gegen einen bestätigten Schlüssel.</summary>
public sealed record SharedGrant(SharedGrantDto Dto, bool Verified);

/// <summary>Geöffneter Inhalt: entschlüsselte Objekte und Dokument-Metadaten.</summary>
public sealed record SharedContent(Guid OwnerId, IReadOnlyList<VaultItem> Items, IReadOnlyList<VaultFile> Files, CryptoKeyHandle Key);

/// <summary>
/// Was andere mit mir teilen (M2.3: „Sofort teilen“, ab M2.4 auch freigegebene Modi). Der Browser prüft die
/// Signatur jedes Umschlags gegen den Schlüssel des Eigentümers, öffnet ihn mit dem eigenen privaten Schlüssel
/// und entschlüsselt die Objekte. Nur Lesen.
/// </summary>
public sealed class SharedService(TrustClient api, KeyTrustService keys, VaultService vault, IMyLifeCrypto crypto, SessionService session)
{
    public async Task<IReadOnlyList<SharedOwner>> GetOwnersAsync()
    {
        var people = (await keys.GetPeopleAsync()).ToDictionary(p => p.Person.Id);
        var result = new List<SharedOwner>();
        foreach (var owner in await api.GetSharedAsync())
        {
            var person = people.GetValueOrDefault(owner.OwnerId);
            var trusted = person?.State is TrustState.Pinned or TrustState.Endorsed;
            var grants = new List<SharedGrant>();
            foreach (var grant in owner.Grants)
            {
                var resource = grant.Kind == GrantKind.Area ? TrustService.Resource(grant.Area!.Value) : TrustService.Resource(grant.ObjectId!.Value);
                var verified = trusted && await crypto.VerifyAsync(person!.Person.PublicSigningKey, grant.Envelope, grant.Signature,
                    TrustService.SignatureContext(session.Session!.UserId, grant.Kind, resource, grant.Mode));
                grants.Add(new SharedGrant(grant, verified));
            }

            result.Add(new SharedOwner(owner.OwnerId, owner.OwnerName, person?.State, grants));
        }

        return result;
    }

    /// <summary>Öffnet einen Umschlag und lädt den Inhalt. Der Schlüssel bleibt bis <see cref="CloseAsync"/> im Krypto-Modul.</summary>
    public async Task<SharedContent> OpenAsync(Guid ownerId, SharedGrantDto grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        var privateKey = session.EncryptionKey ?? throw new InvalidOperationException("Tresor ist gesperrt.");
        var (context, content) = grant.Kind == GrantKind.Area
            ? (KeyContexts.AreaEnvelope(ownerId, grant.Area!.Value), await api.GetSharedAreaAsync(ownerId, grant.Area.Value))
            : (KeyContexts.LetterEnvelope(ownerId, grant.ObjectId!.Value), await api.GetSharedLetterAsync(ownerId, grant.ObjectId.Value));
        var key = await crypto.OpenSealedKeyAsync(privateKey, grant.Envelope, context);
        var items = new List<VaultItem>();
        foreach (var dto in content.Objects)
        {
            if (ObjectCatalog.Find(dto.TypeKey) is not { } type)
            {
                continue;
            }

            try
            {
                var plain = await crypto.DecryptAsync(key, dto.Payload, KeyContexts.VaultObject(type.Key, dto.Id));
                items.Add(new VaultItem
                {
                    Id = dto.Id, Area = dto.Area, Type = type, Payload = VaultPayload.FromUtf8(plain), Status = dto.Status,
                    CreatedAt = dto.CreatedAt, UpdatedAt = dto.UpdatedAt, Version = dto.Version, WrappedKey = dto.WrappedKey,
                });
            }
            catch (Exception exception) when (exception is CryptographicException or JsonException)
            {
                // Unlesbar (etwa nach einem Schlüsselwechsel): überspringen.
            }
        }

        var files = new List<VaultFile>();
        foreach (var dto in content.Documents)
        {
            try
            {
                var plain = await crypto.DecryptAsync(key, dto.Metadata, KeyContexts.DocumentMetadata(dto.Id));
                files.Add(new VaultFile
                {
                    Id = dto.Id, ObjectId = dto.ObjectId, Metadata = DocumentMetadata.FromUtf8(plain), WrappedFileKey = dto.WrappedFileKey,
                    CipherSize = dto.CipherSize, CreatedAt = dto.CreatedAt,
                });
            }
            catch (Exception exception) when (exception is CryptographicException or JsonException)
            {
                // Unlesbar: überspringen.
            }
        }

        return new SharedContent(ownerId, [.. items.OrderBy(i => i.Title, StringComparer.CurrentCultureIgnoreCase)], files, key);
    }

    public async Task<byte[]> DownloadAsync(SharedContent content, VaultFile file)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(file);
        var cipher = await api.DownloadSharedDocumentAsync(content.OwnerId, file.Id);
        return await vault.DecryptFileAsync(content.Key, file.Id, file.WrappedFileKey, cipher);
    }

    public async Task CloseAsync(SharedContent? content)
    {
        if (content is not null)
        {
            await crypto.ReleaseAsync(content.Key);
        }
    }
}
