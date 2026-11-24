using System.Net.Mail;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MyLife.Client.Services;
using MyLife.Crypto;
using MyLife.Shared;
using MyLife.Shared.Family;
using MyLife.Shared.Setup;

namespace MyLife.Client.Pages;

/// <summary>
/// Einrichtungsassistent (M1.2): Einrichtungscode, Konto, Hinweis, Master-Passwort, Wiederherstellungs-Kit,
/// TOTP und Abschluss. Die Schlüssel erzeugt <see cref="VaultSetupService"/>, die Aufrufe macht <see cref="SetupClient"/>.
/// Unter <c>/invite</c> derselbe Ablauf zum Annehmen einer Einladung (M2.2): Statt des Einrichtungscodes gilt das
/// Token aus dem Link; dazu bindet der Browser den neuen Schlüssel per HMAC an das Einladungsgeheimnis und pinnt
/// den Fingerprint der einladenden Person (Architektur 4.3).
/// </summary>
public sealed partial class Setup
{
    private const int StepWelcome = 0;
    private const int StepAccount = 1;
    private const int StepNotInVault = 2;
    private const int StepPassword = 3;
    private const int StepRecovery = 4;
    private const int StepTwoFactor = 5;
    private const int StepFinish = 6;
    private const int RecoveryChecks = 3;

    private static readonly string[] StepTitles =
    [
        "SetupStepWelcome", "SetupStepAccount", "SetupStepNotInVault", "SetupStepPassword",
        "SetupStepRecovery", "SetupStepTwoFactor", "SetupStepFinish",
    ];

    private static readonly Dictionary<string, object> NoAutofillAttributes = new() { ["autocomplete"] = "off", ["spellcheck"] = "false" };
    private static readonly Dictionary<string, object> OneTimeCodeAttributes = new() { ["autocomplete"] = "one-time-code" };

    private MudForm? _form;
    private bool _isLoading = true;
    private bool _statusError;
    private bool _required;
    private bool _completed;
    private bool _busy;
    private string? _error;
    private int _step;

    private string _token = string.Empty;
    private string _displayName = string.Empty;
    private string _email = string.Empty;
    private string _language = CultureService.Current;
    private bool _notInVaultAcknowledged;
    private string _password = string.Empty;
    private bool _passwordAcknowledged;
    private byte[]? _recoveryKey;
    private IReadOnlyList<string> _words = [];
    private int[] _confirmPositions = [];
    private string[] _confirmInputs = [];
    private bool _recoveryStored;
    private TotpEnrollmentDto? _enrollment;
    private string? _totpQrCode;
    private string _totpCode = string.Empty;
    private string? _totpConfirmation;
    private InvitationInfoDto? _invite;
    private byte[]? _inviteSecret;
    private byte[]? _inviterFingerprint;
    private bool _inviteInvalid;

    [Inject] private SetupClient SetupApi { get; set; } = null!;

    [Inject] private VaultSetupService VaultSetup { get; set; } = null!;

    [Inject] private IJSRuntime Js { get; set; } = null!;

    [Inject] private FamilyClient FamilyApi { get; set; } = null!;

    [Inject] private IMyLifeCrypto Crypto { get; set; } = null!;

    [Inject] private NavigationManager Navigation { get; set; } = null!;

    /// <summary>Bei einer Einladung entfällt der Schritt mit dem Einrichtungscode.</summary>
    private int StepOffset => _invite is null ? 0 : StepAccount;

    private bool IsInvite => Navigation.ToBaseRelativePath(Navigation.Uri).StartsWith("invite", StringComparison.OrdinalIgnoreCase);

    private bool CanContinue => _step switch
    {
        StepNotInVault => _notInVaultAcknowledged,
        StepPassword => _passwordAcknowledged,
        StepRecovery => _recoveryStored && RecoveryWordsConfirmed,
        StepTwoFactor => _enrollment is not null,
        _ => true,
    };

    private bool RecoveryWordsConfirmed =>
        _confirmPositions.Length == RecoveryChecks && Enumerable.Range(0, RecoveryChecks).All(i => IsRecoveryWordCorrect(i, _confirmInputs[i]));

    protected override async Task OnInitializedAsync()
    {
        if (IsInvite)
        {
            await LoadInvitationAsync();
            return;
        }

        try
        {
            _required = await SetupApi.IsRequiredAsync();
        }
        catch (HttpRequestException)
        {
            _statusError = true;
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>Link: <c>/invite#t=Token&amp;s=Geheimnis&amp;f=Fingerprint</c>. Das Fragment erreicht den Server nie.</summary>
    private async Task LoadInvitationAsync()
    {
        var fragment = new Uri(Navigation.Uri).Fragment.TrimStart('#');
        var values = fragment.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2)).Where(pair => pair.Length == 2)
            .GroupBy(pair => pair[0], StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First()[1], StringComparer.Ordinal);
        _inviteSecret = KeyTrustService.FromBase64Url(values.GetValueOrDefault("s"));
        _inviterFingerprint = KeyTrustService.ParseFingerprint(values.GetValueOrDefault("f"));
        _token = values.GetValueOrDefault("t") ?? string.Empty;
        try
        {
            if (_token.Length == 0 || _inviteSecret is not { Length: FamilyLimits.SecretBytes })
            {
                _inviteInvalid = true;
                return;
            }

            _invite = await FamilyApi.GetInviteInfoAsync(_token);
            _email = _invite.Email;
            _language = _invite.Language;
            _required = true;
            // Ohne Einrichtungscode beginnt die Annahme direkt beim Konto.
            _step = StepAccount;
        }
        catch (FamilyException)
        {
            _inviteInvalid = true;
        }
        catch (HttpRequestException)
        {
            _statusError = true;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void Back()
    {
        _error = null;
        if (_step > (_invite is null ? StepWelcome : StepAccount))
        {
            _step--;
        }
    }

    private async Task NextAsync()
    {
        _error = null;
        if (_form is not null)
        {
            await _form.ValidateAsync();
            if (!_form.IsValid || !CanContinue)
            {
                return;
            }
        }

        _busy = true;
        try
        {
            switch (_step)
            {
                case StepWelcome when _invite is null:
                    await SetupApi.VerifyTokenAsync(_token.Trim());
                    break;
                case StepTwoFactor:
                    _totpConfirmation = _invite is null
                        ? await SetupApi.VerifyTotpAsync(_token.Trim(), _enrollment!.Enrollment, _totpCode.Trim())
                        : await FamilyApi.VerifyInviteTotpAsync(_token, _enrollment!.Enrollment, _totpCode.Trim());
                    break;
            }

            _step++;
            await PrepareStepAsync();
        }
        catch (SetupException exception)
        {
            await HandleSetupErrorAsync(exception.Message);
        }
        catch (FamilyException exception)
        {
            await HandleInviteErrorAsync(exception.Message);
        }
        catch (HttpRequestException)
        {
            _error = "SetupError_setup-failed";
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>Bereitet den neuen Schritt vor: Recovery-Key einmal erzeugen, TOTP-Geheimnis einmal holen.</summary>
    private async Task PrepareStepAsync()
    {
        if (_step == StepRecovery && _recoveryKey is null)
        {
            _recoveryKey = await VaultSetup.CreateRecoveryKeyAsync();
            _words = Bip39.Encode(_recoveryKey);
            _confirmPositions = [.. Enumerable.Range(0, Bip39.WordCount).OrderBy(_ => Random.Shared.Next()).Take(RecoveryChecks).Order()];
            _confirmInputs = new string[RecoveryChecks];
        }

        if (_step == StepTwoFactor && _enrollment is null)
        {
            _enrollment = _invite is null ? await SetupApi.CreateTotpAsync(_token.Trim()) : await FamilyApi.CreateInviteTotpAsync(_token);
            _totpQrCode = QrCodeService.ToDataUri(QrCodeService.TotpUri("myLife", _email.Trim(), _enrollment.Secret));
            _totpCode = string.Empty;
        }
    }

    private async Task HandleSetupErrorAsync(string code)
    {
        _error = $"SetupError_{code}";
        switch (code)
        {
            case "setup-invalid-token":
                _step = StepWelcome;
                break;
            case "setup-already-completed":
                _required = false;
                break;
            case "setup-totp-expired":
                _enrollment = null;
                _totpConfirmation = null;
                _step = StepTwoFactor;
                await PrepareStepAsync();
                break;
        }
    }

    private async Task HandleInviteErrorAsync(string code)
    {
        _error = $"FamilyError_{code}";
        switch (code)
        {
            case FamilyErrors.TokenInvalid or FamilyErrors.NotFound or FamilyErrors.AccountExists:
                _inviteInvalid = true;
                break;
            case FamilyErrors.TotpExpired:
                _enrollment = null;
                _totpConfirmation = null;
                _step = StepTwoFactor;
                await PrepareStepAsync();
                break;
        }
    }

    /// <summary>
    /// Einladung annehmen: Tresor wie bei der Einrichtung erzeugen, dabei den Fingerprint der einladenden Person
    /// mit dem neuen Tresorschlüssel pinnen, danach den eigenen Fingerprint per HMAC an das Geheimnis binden.
    /// </summary>
    private async Task AcceptInvitationAsync(SetupAccount account)
    {
        byte[]? inviterPin = null;
        var request = await VaultSetup.CreateVaultAsync(account, _password, _recoveryKey!, _token, _totpConfirmation!, async vaultKey =>
        {
            if (_inviterFingerprint is not null)
            {
                inviterPin = await Crypto.EncryptAsync(vaultKey, _inviterFingerprint, KeyContexts.Pin(_invite!.InviterId));
            }
        });
        var fingerprint = await Crypto.Sha256Async(KeyBinding.FingerprintData(request.PublicEncryptionKey, request.PublicSigningKey));
        var binding = await Crypto.HmacSha256Async(_inviteSecret!, KeyBinding.InviteBindingData(_invite!.Id, fingerprint));
        await FamilyApi.AcceptAsync(new AcceptInvitationRequest(request, binding, inviterPin));
    }

    private async Task FinishAsync()
    {
        if (_recoveryKey is null || _totpConfirmation is null)
        {
            return;
        }

        _error = null;
        _busy = true;
        StateHasChanged();
        try
        {
            var account = new SetupAccount(_displayName, _email, _language);
            if (_invite is null)
            {
                var request = await VaultSetup.CreateVaultAsync(account, _password, _recoveryKey, _token.Trim(), _totpConfirmation);
                await SetupApi.CompleteAsync(request);
            }
            else
            {
                await AcceptInvitationAsync(account);
            }

            ForgetSecrets();
            _completed = true;
        }
        catch (SetupException exception)
        {
            await HandleSetupErrorAsync(exception.Message);
        }
        catch (FamilyException exception)
        {
            await HandleInviteErrorAsync(exception.Message);
        }
        catch (Exception exception) when (exception is CryptographicException or JSException)
        {
            _error = "SetupErrorCrypto";
        }
        catch (HttpRequestException)
        {
            _error = "SetupError_setup-failed";
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>Passwort, Recovery-Key und Wörter nach dem Anlegen aus dem Speicher der Seite entfernen.</summary>
    private void ForgetSecrets()
    {
        _password = string.Empty;
        if (_recoveryKey is not null)
        {
            CryptographicOperations.ZeroMemory(_recoveryKey);
        }

        _recoveryKey = null;
        _words = [];
        _confirmInputs = [];
        _token = string.Empty;
        if (_inviteSecret is not null)
        {
            CryptographicOperations.ZeroMemory(_inviteSecret);
        }
    }

    private async Task PrintAsync() => await Js.InvokeVoidAsync("myLife.print");

    private string? ValidateDisplayName(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Length > SetupLimits.MaxDisplayNameLength ? L["SetupDisplayNameRequired"].Value : null;

    private string? ValidateEmail(string value)
    {
        var email = value?.Trim() ?? string.Empty;
        var valid = email.Length <= SetupLimits.MaxEmailLength && MailAddress.TryCreate(email, out var address)
                    && address.Address == email && email.Contains('.', StringComparison.Ordinal);
        return valid ? null : L["SetupEmailInvalid"].Value;
    }

    private bool IsRecoveryWordCorrect(int slot, string? value) =>
        !string.IsNullOrWhiteSpace(value) && Bip39.IndexOf(value) is var index and >= 0 && Bip39.Words[index] == _words[_confirmPositions[slot]];

    private string? ValidateRecoveryWord(int slot, string value) =>
        string.IsNullOrWhiteSpace(value) || IsRecoveryWordCorrect(slot, value) ? null : L["SetupRecoveryWordWrong"].Value;

    private static string FormatSecret(string secret) => string.Join(' ', secret.Chunk(4).Select(chunk => new string(chunk)));
}
