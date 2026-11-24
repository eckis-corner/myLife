using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using MyLife.Crypto;

namespace MyLife.Tests.Crypto;

/// <summary>
/// Prüft, dass .NET und myLife.crypto.js dieselben Formate sprechen. Das Browser-Modul läuft dafür in Node.js
/// (gleiche Web Crypto API). Ohne Node wird lokal übersprungen; in der CI (Variable CI gesetzt) schlägt der Test fehl.
/// </summary>
public sealed class CryptoCrossTests
{
    internal static readonly string StoredVectorsPath = Path.Combine(
        ResourceParityTests.FindRepositoryRoot(), "tests", "MyLife.Tests", "Crypto", "vectors-v1.json");

    [Fact]
    public async Task DotNetReadsStoredVectors()
    {
        await using var crypto = new ReferenceMyLifeCrypto();
        await LoadStoredVectors().VerifyAsync(crypto);
    }

    [Fact]
    public async Task JavaScriptReadsStoredVectorsAndDotNetReadsJavaScriptOutput()
    {
        await RunHarnessAsync(StoredVectorsPath, LoadStoredVectors());
    }

    [Fact]
    public async Task JavaScriptReadsFreshDotNetVectors()
    {
        await using var crypto = new ReferenceMyLifeCrypto();
        var vectors = await CryptoVectors.CreateAsync(crypto);
        var path = Path.Combine(Path.GetTempPath(), $"mylife-vectors-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(vectors, CryptoVectors.JsonOptions), TestContext.Current.CancellationToken);
        try
        {
            await RunHarnessAsync(path, vectors);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Erzeugt vectors-v1.json neu. Nur bewusst ausführen: Die Datei hält das Format fest, damit alte Tresore lesbar bleiben.
    /// <c>dotnet test --project tests/MyLife.Tests -- --filter-method "*WriteStoredVectors" --explicit only</c>
    /// </summary>
    [Fact(Explicit = true)]
    public async Task WriteStoredVectors()
    {
        await using var crypto = new ReferenceMyLifeCrypto();
        var vectors = await CryptoVectors.CreateAsync(crypto);
        await File.WriteAllTextAsync(StoredVectorsPath, JsonSerializer.Serialize(vectors, CryptoVectors.JsonOptions) + "\n", TestContext.Current.CancellationToken);
    }

    private static CryptoVectors LoadStoredVectors() =>
        JsonSerializer.Deserialize<CryptoVectors>(File.ReadAllText(StoredVectorsPath), CryptoVectors.JsonOptions)
        ?? throw new InvalidOperationException("vectors-v1.json ist leer.");

    private static async Task RunHarnessAsync(string vectorsPath, CryptoVectors input)
    {
        var output = await RunNodeAsync(vectorsPath);
        var result = JsonSerializer.Deserialize<HarnessResult>(output, CryptoVectors.JsonOptions)
                     ?? throw new InvalidOperationException("Keine Ausgabe vom Harness.");

        Assert.Contains("argon2id+hkdf auth hash", result.Checks);
        Assert.Contains("password NFC normalization", result.Checks);
        Assert.Contains("recovery: open envelope", result.Checks);
        Assert.Contains("reject tampered ciphertext", result.Checks);
        Assert.Contains("produced own vectors", result.Checks);

        // Alles, was JavaScript erzeugt hat, muss .NET lesen können.
        await using var crypto = new ReferenceMyLifeCrypto();
        await result.Produced.VerifyAsync(crypto);
        await VerifyExtraAsync(crypto, input, result);
    }

    private static async Task VerifyExtraAsync(ReferenceMyLifeCrypto crypto, CryptoVectors net, HarnessResult result)
    {
        static byte[] Bin(string value) => CryptoVectors.Hex(value);
        var master = await crypto.DeriveMasterKeyAsync(net.Password, Bin(net.Salt), net.Argon2);
        var kek = await crypto.DeriveKeyEncryptionKeyAsync(master);
        var vmk = await crypto.UnwrapKeyAsync(kek, Bin(net.WrappedVmkByKek), CryptoVectors.VmkContext);

        // Umschlag aus JavaScript an den .NET-Schlüssel öffnet den Bereichsschlüssel der JavaScript-Daten.
        var ecdh = await crypto.UnwrapPrivateKeyAsync(KeyPairKind.Ecdh, vmk, Bin(net.Ecdh.WrappedPrivateKey), CryptoVectors.EcdhContext);
        var envelope = result.Extra.EnvelopeToNetKey;
        var jsArea = await crypto.OpenSealedKeyAsync(ecdh, Bin(envelope.Envelope), envelope.Context);
        var jsData = result.Produced.Data;
        Assert.Equal(jsData.Plaintext, CryptoVectors.Hex(await crypto.DecryptAsync(jsArea, Bin(jsData.Blob), jsData.Context)));

        // In JavaScript neu verpackter .NET-VMK lässt sich mit dem Recovery-Schlüssel öffnen.
        var recovery = await crypto.ImportRecoveryKeyAsync(Bin(net.RecoveryKey));
        var rewrapped = await crypto.UnwrapKeyAsync(recovery, Bin(result.Extra.RewrappedNetVmk.Wrapped), result.Extra.RewrappedNetVmk.Context);
        var area = await crypto.UnwrapKeyAsync(rewrapped, Bin(net.WrappedAreaKey), CryptoVectors.AreaContext);
        Assert.Equal(net.Data.Plaintext, CryptoVectors.Hex(await crypto.DecryptAsync(area, Bin(net.Data.Blob), net.Data.Context)));
    }

    private static async Task<string> RunNodeAsync(string vectorsPath)
    {
        var root = ResourceParityTests.FindRepositoryRoot();
        var harness = Path.Combine(root, "tests", "MyLife.Tests", "Crypto", "crypto-harness.mjs");
        var start = new ProcessStartInfo("node")
        {
            ArgumentList = { harness, root, vectorsPath },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        Process process;
        try
        {
            process = Process.Start(start) ?? throw new InvalidOperationException("node konnte nicht gestartet werden.");
        }
        catch (Win32Exception) when (Environment.GetEnvironmentVariable("CI") is null)
        {
            Assert.Skip("Node.js ist nicht installiert; die Kreuztests JS ↔ .NET laufen in der CI.");
            throw;
        }

        using (process)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);

            Assert.True(process.ExitCode == 0, $"crypto-harness.mjs ist fehlgeschlagen:\n{await stderr}");
            return await stdout;
        }
    }

    private sealed record HarnessResult(IReadOnlyList<string> Checks, CryptoVectors Produced, HarnessExtra Extra);

    private sealed record HarnessExtra(EnvelopeOutput EnvelopeToNetKey, RewrapOutput RewrappedNetVmk);

    private sealed record EnvelopeOutput(string Context, string Envelope);

    private sealed record RewrapOutput(string Context, string Wrapped);
}
