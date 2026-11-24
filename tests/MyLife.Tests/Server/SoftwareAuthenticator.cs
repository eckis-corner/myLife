using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.JSInterop;

namespace MyLife.Tests.Server;

/// <summary>
/// Authenticator in Software für Tests: erzeugt WebAuthn-Antworten wie ein echtes Gerät (Attestation „none“,
/// COSE-Schlüssel ES256, Signatur über authenticatorData und den Hash von clientDataJSON). So prüft Fido2NetLib
/// die vollständige Zeremonie, ohne Browser.
/// </summary>
internal sealed class SoftwareAuthenticator(string origin) : IDisposable
{
    private static readonly string[] Transports = ["internal"];
    private readonly Dictionary<string, (ECDsa Key, byte[] UserHandle)> _credentials = [];
    private uint _signCount;

    /// <summary>Nächste Anmeldung mit einem fremden Schlüssel signieren (Manipulation simulieren).</summary>
    public bool ForgeNextSignature { get; set; }

    public string Create(string optionsJson)
    {
        var options = JsonNode.Parse(optionsJson)!;
        var rpId = options["rp"]!["id"]!.GetValue<string>();
        var userHandle = Base64Url(options["user"]!["id"]!.GetValue<string>());
        var clientData = ClientData("webauthn.create", options["challenge"]!.GetValue<string>());

        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var credentialId = RandomNumberGenerator.GetBytes(16);
        _credentials[Base64Url(credentialId)] = (key, userHandle);

        var parameters = key.ExportParameters(false);
        var cose = new CborWriter(CborConformanceMode.Lax);
        cose.WriteStartMap(5);
        cose.WriteInt32(1); cose.WriteInt32(2);     // kty: EC2
        cose.WriteInt32(3); cose.WriteInt32(-7);    // alg: ES256
        cose.WriteInt32(-1); cose.WriteInt32(1);    // crv: P-256
        cose.WriteInt32(-2); cose.WriteByteString(parameters.Q.X!);
        cose.WriteInt32(-3); cose.WriteByteString(parameters.Q.Y!);
        cose.WriteEndMap();

        byte[] credentialLength = [(byte)(credentialId.Length >> 8), (byte)credentialId.Length];
        byte[] authData = [.. SHA256.HashData(Encoding.UTF8.GetBytes(rpId)), 0x45, 0, 0, 0, 0, .. new byte[16], .. credentialLength, .. credentialId, .. cose.Encode()];

        var attestation = new CborWriter(CborConformanceMode.Lax);
        attestation.WriteStartMap(3);
        attestation.WriteTextString("fmt"); attestation.WriteTextString("none");
        attestation.WriteTextString("attStmt"); attestation.WriteStartMap(0); attestation.WriteEndMap();
        attestation.WriteTextString("authData"); attestation.WriteByteString(authData);
        attestation.WriteEndMap();

        return JsonSerializer.Serialize(new
        {
            id = Base64Url(credentialId),
            rawId = Base64Url(credentialId),
            type = "public-key",
            response = new { attestationObject = Base64Url(attestation.Encode()), clientDataJSON = Base64Url(clientData), transports = Transports },
            clientExtensionResults = new { },
        });
    }

    public string Get(string optionsJson)
    {
        var options = JsonNode.Parse(optionsJson)!;
        var rpId = options["rpId"]!.GetValue<string>();
        var allowed = options["allowCredentials"]!.AsArray().Select(c => c!["id"]!.GetValue<string>()).ToList();
        var (credentialId, (key, userHandle)) = _credentials.First(c => allowed.Contains(c.Key));
        var clientData = ClientData("webauthn.get", options["challenge"]!.GetValue<string>());

        _signCount++;
        byte[] counter = [(byte)(_signCount >> 24), (byte)(_signCount >> 16), (byte)(_signCount >> 8), (byte)_signCount];
        byte[] authData = [.. SHA256.HashData(Encoding.UTF8.GetBytes(rpId)), 0x05, .. counter];

        using var forged = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signer = ForgeNextSignature ? forged : key;
        ForgeNextSignature = false;
        var signature = signer.SignData([.. authData, .. SHA256.HashData(clientData)], HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        return JsonSerializer.Serialize(new
        {
            id = credentialId,
            rawId = credentialId,
            type = "public-key",
            response = new
            {
                authenticatorData = Base64Url(authData),
                clientDataJSON = Base64Url(clientData),
                signature = Base64Url(signature),
                userHandle = Base64Url(userHandle),
            },
            clientExtensionResults = new { },
        });
    }

    public void Dispose()
    {
        foreach (var (key, _) in _credentials.Values)
        {
            key.Dispose();
        }
    }

    private byte[] ClientData(string type, string challenge) =>
        JsonSerializer.SerializeToUtf8Bytes(new { type, challenge, origin, crossOrigin = false });

    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '='));
    }

    /// <summary>Ersetzt mylife.webauthn.js für <see cref="MyLife.Client.Services.WebAuthnService"/>.</summary>
    public IJSRuntime AsJsRuntime() => new Runtime(this);

    private sealed class Runtime(SoftwareAuthenticator authenticator) : IJSRuntime, IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            object result = identifier switch
            {
                "import" => this,
                "isSupported" => true,
                "create" => authenticator.Create((string)args![0]!),
                "get" => authenticator.Get((string)args![0]!),
                _ => throw new InvalidOperationException(identifier),
            };
            return ValueTask.FromResult((TValue)result);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
