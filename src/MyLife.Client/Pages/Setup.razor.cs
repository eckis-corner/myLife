using System.Net.Mail;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MyLife.Client.Services;
using MyLife.Crypto;
using MyLife.Shared;
using MyLife.Shared.Setup;

namespace MyLife.Client.Pages;

/// <summary>
/// Einrichtungsassistent (M1.2): Einrichtungscode, Konto, Hinweis, Master-Passwort, Wiederherstellungs-Kit,
/// TOTP und Abschluss. Die Schlüssel erzeugt <see cref="VaultSetupService"/>, die Aufrufe macht <see cref="SetupClient"/>.
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

    private static readonly Dictionary<string, object> NewPasswordAttributes = new() { ["autocomplete"] = "new-password" };
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
    private string _passwordRepeat = string.Empty;
    private bool _showPassword;
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

    [Inject] private SetupClient SetupApi { get; set; } = null!;

    [Inject] private VaultSetupService VaultSetup { get; set; } = null!;

    [Inject] private IJSRuntime Js { get; set; } = null!;

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

    private int PasswordScore
    {
        get
        {
            var password = _password ?? string.Empty;
            if (password.Length < SetupLimits.MinPasswordLength)
            {
                return password.Length == 0 ? 0 : 1;
            }

            var classes = new[] { password.Any(char.IsLower), password.Any(char.IsUpper), password.Any(char.IsDigit), password.Any(c => !char.IsLetterOrDigit(c)) }.Count(x => x);
            return password.Length >= 20 || (password.Length >= 16 && classes >= 3) ? 3 : 2;
        }
    }

    private Color PasswordColor => PasswordScore switch { 3 => Color.Success, 2 => Color.Warning, _ => Color.Error };

    private string PasswordStrengthKey => PasswordScore switch
    {
        3 => "SetupPasswordStrengthStrong",
        2 => "SetupPasswordStrengthFair",
        _ => "SetupPasswordStrengthWeak",
    };

    protected override async Task OnInitializedAsync()
    {
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

    private void Back()
    {
        _error = null;
        if (_step > 0)
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
                case StepWelcome:
                    await SetupApi.VerifyTokenAsync(_token.Trim());
                    break;
                case StepTwoFactor:
                    _totpConfirmation = await SetupApi.VerifyTotpAsync(_token.Trim(), _enrollment!.Enrollment, _totpCode.Trim());
                    break;
            }

            _step++;
            await PrepareStepAsync();
        }
        catch (SetupException exception)
        {
            await HandleSetupErrorAsync(exception.Message);
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
            _enrollment = await SetupApi.CreateTotpAsync(_token.Trim());
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
            var request = await VaultSetup.CreateVaultAsync(account, _password, _recoveryKey, _token.Trim(), _totpConfirmation);
            await SetupApi.CompleteAsync(request);
            ForgetSecrets();
            _completed = true;
        }
        catch (SetupException exception)
        {
            await HandleSetupErrorAsync(exception.Message);
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
        _password = _passwordRepeat = string.Empty;
        if (_recoveryKey is not null)
        {
            CryptographicOperations.ZeroMemory(_recoveryKey);
        }

        _recoveryKey = null;
        _words = [];
        _confirmInputs = [];
        _token = string.Empty;
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

    private string? ValidatePassword(string value) =>
        (value?.Length ?? 0) < SetupLimits.MinPasswordLength ? L["SetupPasswordTooShort", SetupLimits.MinPasswordLength].Value : null;

    private string? ValidatePasswordRepeat(string value) =>
        value == _password ? null : L["SetupPasswordMismatch"].Value;

    private bool IsRecoveryWordCorrect(int slot, string? value) =>
        !string.IsNullOrWhiteSpace(value) && Bip39.IndexOf(value) is var index and >= 0 && Bip39.Words[index] == _words[_confirmPositions[slot]];

    private string? ValidateRecoveryWord(int slot, string value) =>
        string.IsNullOrWhiteSpace(value) || IsRecoveryWordCorrect(slot, value) ? null : L["SetupRecoveryWordWrong"].Value;

    private static string FormatSecret(string secret) => string.Join(' ', secret.Chunk(4).Select(chunk => new string(chunk)));
}
