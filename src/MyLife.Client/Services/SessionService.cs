using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Components.Authorization;
using MyLife.Crypto;
using MyLife.Shared.Auth;

namespace MyLife.Client.Services;

public enum SessionState
{
    /// <summary>Noch nicht beim Server nachgefragt.</summary>
    Unknown,

    /// <summary>Niemand angemeldet.</summary>
    Anonymous,

    /// <summary>Sitzung beim Server besteht (etwa nach dem Neuladen), die Schlüssel fehlen: Passwort nötig.</summary>
    Locked,

    /// <summary>Angemeldet und Tresor entsperrt.</summary>
    Unlocked,
}

/// <summary>
/// Sitzung und Schlüssel im Browser (Architektur 4.4). Die Schlüssel liegen nur als Handles im Krypto-Modul und
/// nur im Arbeitsspeicher; beim Abmelden und beim Neuladen der Seite sind sie weg.
/// </summary>
public sealed class SessionService(AuthClient api, IMyLifeCrypto crypto, WebAuthnService webAuthn) : IDisposable
{
    private readonly SemaphoreSlim _initialize = new(1, 1);
    private CryptoKeyHandle? _pendingKek;

    public event Action? Changed;

    public SessionState State { get; private set; } = SessionState.Unknown;

    public SessionDto? Session { get; private set; }

    public CryptoKeyHandle? VaultKey { get; private set; }

    public CryptoKeyHandle? EncryptionKey { get; private set; }

    public CryptoKeyHandle? SigningKey { get; private set; }

    /// <summary>Grund der letzten Abmeldung, etwa <c>timeout</c>, für den Hinweis auf der Anmeldeseite.</summary>
    public string? LogoutReason { get; private set; }

    public async Task InitializeAsync()
    {
        if (State != SessionState.Unknown)
        {
            return;
        }

        await _initialize.WaitAsync();
        try
        {
            if (State == SessionState.Unknown)
            {
                try
                {
                    Session = await api.GetSessionAsync();
                }
                catch (HttpRequestException)
                {
                    Session = null;
                }

                State = Session is null ? SessionState.Anonymous : SessionState.Locked;
            }
        }
        finally
        {
            _initialize.Release();
        }

        Changed?.Invoke();
    }

    /// <summary>Erster Schritt: Passwort prüfen lassen. Der KEK bleibt bis zum zweiten Faktor im Krypto-Modul.</summary>
    public async Task<LoginChallengeDto> BeginLoginAsync(string email, string password)
    {
        await DiscardPendingAsync();
        var prelogin = await api.PreloginAsync(email.Trim());
        var (authHash, kek) = await DerivePasswordKeysAsync(password, prelogin.KdfSalt,
            new Argon2Parameters(prelogin.KdfMemoryKiB, prelogin.KdfIterations, prelogin.KdfParallelism));
        try
        {
            var challenge = await api.LoginAsync(email.Trim(), authHash);
            _pendingKek = kek;
            return challenge;
        }
        catch
        {
            await crypto.ReleaseAsync(kek);
            throw;
        }
    }

    public async Task CompleteLoginWithTotpAsync(string pending, string code) =>
        await OpenVaultAsync(await api.LoginTotpAsync(pending, code.Trim()));

    public async Task CompleteLoginWithPasskeyAsync(string pending)
    {
        var options = await api.PasskeyLoginOptionsAsync(pending);
        var assertion = await webAuthn.GetAsync(options.OptionsJson);
        await OpenVaultAsync(await api.LoginPasskeyAsync(pending, options.OptionsToken, assertion));
    }

    /// <summary>Entsperrt nach dem Neuladen: Das Passwort ist richtig, wenn sich der Tresorschlüssel entpacken lässt.</summary>
    public async Task UnlockAsync(string password)
    {
        var session = Session ?? throw new InvalidOperationException("Keine Sitzung.");
        await DiscardPendingAsync();
        var (_, kek) = await DerivePasswordKeysAsync(password, session.KdfSalt,
            new Argon2Parameters(session.KdfMemoryKiB, session.KdfIterations, session.KdfParallelism));
        _pendingKek = kek;
        await OpenVaultAsync(session);
    }

    /// <summary>
    /// Neues Master-Passwort: Der Tresorschlüssel wird im Krypto-Modul vom alten auf den neuen KEK umgepackt.
    /// An den Daten ändert sich nichts.
    /// </summary>
    public async Task ChangePasswordAsync(string currentPassword, string newPassword)
    {
        var session = Session ?? throw new InvalidOperationException("Keine Sitzung.");
        var (currentAuthHash, currentKek) = await DerivePasswordKeysAsync(currentPassword, session.KdfSalt,
            new Argon2Parameters(session.KdfMemoryKiB, session.KdfIterations, session.KdfParallelism));
        CryptoKeyHandle? newKek = null;
        try
        {
            var parameters = Argon2Parameters.Default;
            var salt = await crypto.RandomBytesAsync(16);
            (var newAuthHash, newKek) = await DerivePasswordKeysAsync(newPassword, salt, parameters);
            byte[] wrappedVmk;
            try
            {
                wrappedVmk = await crypto.RewrapKeyAsync(currentKek, session.WrappedVmk, KeyContexts.Vmk, newKek.Value, KeyContexts.Vmk);
            }
            catch (CryptographicException exception)
            {
                throw new AuthException(ClientAuthErrors.WrongPassword, exception);
            }

            var material = new PasswordMaterial(salt, parameters.MemoryKiB, parameters.Iterations, parameters.Parallelism, newAuthHash, wrappedVmk);
            await api.ChangePasswordAsync(currentAuthHash, material);
            Session = session with
            {
                KdfSalt = salt,
                KdfMemoryKiB = parameters.MemoryKiB,
                KdfIterations = parameters.Iterations,
                KdfParallelism = parameters.Parallelism,
                WrappedVmk = wrappedVmk,
            };
        }
        finally
        {
            await crypto.ReleaseAsync(currentKek);
            if (newKek is { } handle)
            {
                await crypto.ReleaseAsync(handle);
            }
        }
    }

    /// <summary>Abmelden: alle Schlüssel verwerfen und die Sitzung beim Server beenden.</summary>
    public async Task LogoutAsync(string? reason = null)
    {
        await crypto.ReleaseAllAsync();
        _pendingKek = null;
        VaultKey = EncryptionKey = SigningKey = null;
        Session = null;
        State = SessionState.Anonymous;
        LogoutReason = reason;
        try
        {
            await api.LogoutAsync();
        }
        catch (Exception exception) when (exception is HttpRequestException or AuthException)
        {
            // Die Schlüssel sind bereits verworfen; das Cookie läuft spätestens mit der Abmeldezeit ab.
        }

        Changed?.Invoke();
    }

    /// <summary>Hält die gleitende Sitzung beim Server am Leben, solange der Nutzer aktiv ist.</summary>
    public async Task<bool> RefreshAsync()
    {
        try
        {
            return await api.GetSessionAsync() is not null;
        }
        catch (HttpRequestException)
        {
            // Netzwerk kurz weg: nicht abmelden, beim nächsten Mal erneut versuchen.
            return true;
        }
    }

    public void ClearLogoutReason() => LogoutReason = null;

    public void Dispose() => _initialize.Dispose();

    public ClaimsPrincipal ToPrincipal()
    {
        if (State != SessionState.Unlocked || Session is null)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, Session.UserId.ToString()),
            new(ClaimTypes.Name, Session.DisplayName),
            new(ClaimTypes.Email, Session.Email),
        ];
        if (Session.IsAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "admin"));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "mylife"));
    }

    private async Task<(byte[] AuthHash, CryptoKeyHandle Kek)> DerivePasswordKeysAsync(string password, byte[] salt, Argon2Parameters parameters)
    {
        var master = await crypto.DeriveMasterKeyAsync(password, salt, parameters);
        try
        {
            return (await crypto.DeriveAuthHashAsync(master), await crypto.DeriveKeyEncryptionKeyAsync(master));
        }
        finally
        {
            await crypto.ReleaseAsync(master);
        }
    }

    private async Task OpenVaultAsync(SessionDto session)
    {
        var kek = _pendingKek ?? throw new InvalidOperationException("Kein Schlüssel aus dem Passwort.");
        _pendingKek = null;
        try
        {
            var vmk = await crypto.UnwrapKeyAsync(kek, session.WrappedVmk, KeyContexts.Vmk);
            EncryptionKey = await crypto.UnwrapPrivateKeyAsync(KeyPairKind.Ecdh, vmk, session.WrappedEncryptionPrivateKey, KeyContexts.EncryptionPrivateKey);
            SigningKey = await crypto.UnwrapPrivateKeyAsync(KeyPairKind.Ecdsa, vmk, session.WrappedSigningPrivateKey, KeyContexts.SigningPrivateKey);
            VaultKey = vmk;
        }
        catch (CryptographicException exception)
        {
            throw new AuthException(ClientAuthErrors.WrongPassword, exception);
        }
        finally
        {
            await crypto.ReleaseAsync(kek);
        }

        Session = session;
        State = SessionState.Unlocked;
        LogoutReason = null;
        Changed?.Invoke();
    }

    private async Task DiscardPendingAsync()
    {
        if (_pendingKek is { } kek)
        {
            _pendingKek = null;
            await crypto.ReleaseAsync(kek);
        }
    }
}

/// <summary>Angemeldet im Sinne von [Authorize] ist nur, wer den Tresor entsperrt hat.</summary>
public sealed class MyLifeAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private readonly SessionService _session;

    public MyLifeAuthenticationStateProvider(SessionService session)
    {
        _session = session;
        _session.Changed += OnChanged;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        await _session.InitializeAsync();
        return new AuthenticationState(_session.ToPrincipal());
    }

    public void Dispose() => _session.Changed -= OnChanged;

    private void OnChanged() => NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_session.ToPrincipal())));
}

/// <summary>
/// Wiederherstellung mit dem Recovery-Key: Die 24 Wörter beweisen dem Server den Besitz (Prüfwert), der Browser
/// packt den Tresorschlüssel auf ein neues Passwort um. Der zweite Faktor bleibt unverändert.
/// </summary>
public sealed class RecoveryService(AuthClient api, IMyLifeCrypto crypto)
{
    public async Task RecoverAsync(string email, string words, string newPassword)
    {
        if (!Bip39.TryDecode(words, out var recoveryKey))
        {
            throw new AuthException(ClientAuthErrors.InvalidRecoveryWords);
        }

        var handles = new List<CryptoKeyHandle>();
        try
        {
            var challenge = await api.StartRecoveryAsync(email.Trim(), await crypto.DeriveRecoveryAuthHashAsync(recoveryKey));
            var recovery = await crypto.ImportRecoveryKeyAsync(recoveryKey);
            handles.Add(recovery);

            var parameters = Argon2Parameters.Default;
            var salt = await crypto.RandomBytesAsync(16);
            var master = await crypto.DeriveMasterKeyAsync(newPassword, salt, parameters);
            handles.Add(master);
            var authHash = await crypto.DeriveAuthHashAsync(master);
            var kek = await crypto.DeriveKeyEncryptionKeyAsync(master);
            handles.Add(kek);

            byte[] wrappedVmk;
            try
            {
                wrappedVmk = await crypto.RewrapKeyAsync(recovery, challenge.WrappedVmkByRecovery, KeyContexts.Vmk, kek, KeyContexts.Vmk);
            }
            catch (CryptographicException exception)
            {
                throw new AuthException(ClientAuthErrors.CryptoFailed, exception);
            }

            await api.CompleteRecoveryAsync(challenge.Pending,
                new PasswordMaterial(salt, parameters.MemoryKiB, parameters.Iterations, parameters.Parallelism, authHash, wrappedVmk));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(recoveryKey);
            foreach (var handle in handles)
            {
                await crypto.ReleaseAsync(handle);
            }
        }
    }
}
