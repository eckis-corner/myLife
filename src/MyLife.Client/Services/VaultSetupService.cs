using MyLife.Crypto;
using MyLife.Shared.Setup;

namespace MyLife.Client.Services;

/// <summary>Angaben zum ersten Konto aus dem Assistenten.</summary>
public sealed record SetupAccount(string DisplayName, string Email, string Language);

/// <summary>
/// Erzeugt im Browser die Schlüsselkette eines neuen Tresors (Architektur 4.2): Stammschlüssel aus dem Passwort,
/// Anmeldehash, KEK, Tresorschlüssel (doppelt verpackt mit KEK und Recovery-Key) und die beiden Schlüsselpaare.
/// Zum Server gehen nur der Anmeldehash und verpackte bzw. öffentliche Schlüssel.
/// </summary>
public sealed class VaultSetupService(IMyLifeCrypto crypto)
{
    /// <summary>Neuer Recovery-Key: 32 Zufallsbytes, angezeigt als 24 Wörter.</summary>
    public async Task<byte[]> CreateRecoveryKeyAsync() => await crypto.RandomBytesAsync(Bip39.EntropyBytes);

    public async Task<SetupCompleteRequest> CreateVaultAsync(
        SetupAccount account, string password, byte[] recoveryKey, string token, string totpConfirmation)
    {
        ArgumentNullException.ThrowIfNull(account);
        var parameters = Argon2Parameters.Default;
        var salt = await crypto.RandomBytesAsync(16);
        try
        {
            var master = await crypto.DeriveMasterKeyAsync(password, salt, parameters);
            var authHash = await crypto.DeriveAuthHashAsync(master);
            var kek = await crypto.DeriveKeyEncryptionKeyAsync(master);
            var recovery = await crypto.ImportRecoveryKeyAsync(recoveryKey);
            await crypto.ReleaseAsync(master);

            var vmk = await crypto.CreateKeyAsync([new(kek, KeyContexts.Vmk), new(recovery, KeyContexts.Vmk)]);
            var encryption = await crypto.CreateKeyPairAsync(KeyPairKind.Ecdh, vmk.Handle, KeyContexts.EncryptionPrivateKey);
            var signing = await crypto.CreateKeyPairAsync(KeyPairKind.Ecdsa, vmk.Handle, KeyContexts.SigningPrivateKey);

            return new SetupCompleteRequest
            {
                Token = token,
                Email = account.Email.Trim(),
                DisplayName = account.DisplayName.Trim(),
                Language = account.Language,
                KdfSalt = salt,
                KdfMemoryKiB = parameters.MemoryKiB,
                KdfIterations = parameters.Iterations,
                KdfParallelism = parameters.Parallelism,
                AuthHash = authHash,
                WrappedVmk = vmk.WrappedKeys[0],
                WrappedVmkByRecovery = vmk.WrappedKeys[1],
                RecoveryAuthHash = await crypto.DeriveRecoveryAuthHashAsync(recoveryKey),
                PublicEncryptionKey = encryption.PublicKey,
                WrappedEncryptionPrivateKey = encryption.WrappedPrivateKey,
                PublicSigningKey = signing.PublicKey,
                WrappedSigningPrivateKey = signing.WrappedPrivateKey,
                TotpConfirmation = totpConfirmation,
            };
        }
        finally
        {
            // Nach der Einrichtung meldet man sich neu an; bis dahin bleibt kein Schlüssel im Speicher.
            await crypto.ReleaseAllAsync();
        }
    }
}
