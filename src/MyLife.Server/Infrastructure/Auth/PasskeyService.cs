using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using MyLife.Server.Data;

namespace MyLife.Server.Infrastructure.Auth;

/// <summary>
/// Passkeys über Fido2NetLib (Entscheidungen 8 und 52). Relying Party ist der Host der Anfrage, hinter dem Proxy
/// über die vertrauenswürdigen X-Forwarded-Header, also etwa <c>mylife.eckis-corner.de</c>.
/// Die Optionen jeder Zeremonie reisen geschützt mit dem Browser; der Server hält keinen Zustand.
/// </summary>
internal sealed class PasskeyService(PendingTokens tokens)
{
    public (string OptionsJson, string OptionsToken) BeginRegistration(HttpContext context, User user)
    {
        var options = Create(context).RequestNewCredential(new RequestNewCredentialParams
        {
            User = new Fido2User { Id = user.Id.ToByteArray(), Name = user.Email, DisplayName = user.DisplayName },
            ExcludeCredentials = [.. user.Passkeys.Select(p => new PublicKeyCredentialDescriptor(p.CredentialId))],
            AuthenticatorSelection = new AuthenticatorSelection
            {
                ResidentKey = ResidentKeyRequirement.Preferred,
                UserVerification = UserVerificationRequirement.Preferred,
            },
            AttestationPreference = AttestationConveyancePreference.None,
        });
        var json = options.ToJson();
        return (json, tokens.Issue(PendingPurpose.PasskeyRegistration, user, json));
    }

    /// <summary>Prüft die Antwort des Authenticators. Liefert den neuen Passkey oder null.</summary>
    public async Task<Passkey?> CompleteRegistrationAsync(
        HttpContext context, User user, string optionsToken, string attestationJson, Func<byte[], CancellationToken, Task<bool>> isUnique,
        CancellationToken cancellationToken)
    {
        if (tokens.Read(PendingPurpose.PasskeyRegistration, optionsToken) is not { } pending
            || pending.UserId != user.Id || pending.Stamp != user.SecurityStamp)
        {
            return null;
        }

        try
        {
            var response = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(attestationJson);
            if (response is null)
            {
                return null;
            }

            var credential = await Create(context).MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = response,
                OriginalOptions = CredentialCreateOptions.FromJson(pending.Payload),
                IsCredentialIdUniqueToUserCallback = (args, ct) => isUnique(args.CredentialId, ct),
            }, cancellationToken);

            return new Passkey
            {
                Id = Guid.CreateVersion7(),
                UserId = user.Id,
                CredentialId = credential.Id,
                PublicKey = credential.PublicKey,
                SignCount = credential.SignCount,
                Name = string.Empty,
            };
        }
        catch (Exception exception) when (exception is Fido2VerificationException or JsonException or FormatException or ArgumentException)
        {
            return null;
        }
    }

    public (string OptionsJson, string OptionsToken) BeginLogin(HttpContext context, User user)
    {
        var options = Create(context).GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = [.. user.Passkeys.Select(p => new PublicKeyCredentialDescriptor(p.CredentialId))],
            UserVerification = UserVerificationRequirement.Preferred,
        });
        var json = options.ToJson();
        return (json, tokens.Issue(PendingPurpose.PasskeyLogin, user, json));
    }

    /// <summary>Prüft die Signatur des Passkeys. Liefert den verwendeten Passkey (mit neuem Zähler) oder null.</summary>
    public async Task<Passkey?> CompleteLoginAsync(HttpContext context, User user, string optionsToken, string assertionJson, CancellationToken cancellationToken)
    {
        if (tokens.Read(PendingPurpose.PasskeyLogin, optionsToken) is not { } pending
            || pending.UserId != user.Id || pending.Stamp != user.SecurityStamp)
        {
            return null;
        }

        try
        {
            var response = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(assertionJson);
            var passkey = response is null ? null : user.Passkeys.SingleOrDefault(p => p.CredentialId.AsSpan().SequenceEqual(response.RawId));
            if (passkey is null)
            {
                return null;
            }

            var result = await Create(context).MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = response!,
                OriginalOptions = AssertionOptions.FromJson(pending.Payload),
                StoredPublicKey = passkey.PublicKey,
                StoredSignatureCounter = passkey.SignCount,
                IsUserHandleOwnerOfCredentialIdCallback = (args, _) =>
                    Task.FromResult(args.UserHandle is not { Length: 16 } handle || new Guid(handle) == user.Id),
            }, cancellationToken);

            passkey.SignCount = result.SignCount;
            return passkey;
        }
        catch (Exception exception) when (exception is Fido2VerificationException or JsonException or FormatException or ArgumentException)
        {
            return null;
        }
    }

    private static Fido2 Create(HttpContext context)
    {
        var request = context.Request;
        return new Fido2(new Fido2Configuration
        {
            RPID = request.Host.Host,
            RPName = "myLife",
            Origins = new HashSet<string>([$"{request.Scheme}://{request.Host}"]),
            TimestampDriftTolerance = 300_000,
        });
    }
}
