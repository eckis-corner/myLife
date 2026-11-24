using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using MyLife.Client.Services;
using MyLife.Shared.Auth;

namespace MyLife.Client.Pages;

/// <summary>
/// Anmeldung in zwei Schritten (Passwort, dann TOTP oder Passkey) und Entsperren nach dem Neuladen.
/// Die Schlüssel leitet <see cref="SessionService"/> im Browser ab.
/// </summary>
public sealed partial class Login
{
    private static readonly Dictionary<string, object> EmailAttributes = new() { ["autocomplete"] = "username", ["data-testid"] = "login-email" };
    private static readonly Dictionary<string, object> CurrentPasswordAttributes = new() { ["autocomplete"] = "current-password", ["data-testid"] = "login-password" };
    private static readonly Dictionary<string, object> OneTimeCodeAttributes = new() { ["autocomplete"] = "one-time-code", ["data-testid"] = "login-code" };

    private MudForm? _form;
    private Mode _mode = Mode.Loading;
    private bool _busy;
    private string? _error;
    private string? _info;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _code = string.Empty;
    private LoginChallengeDto? _challenge;
    private TotpLoginSetupDto? _enrollment;
    private string? _enrollmentQr;

    private enum Mode
    {
        Loading,
        Credentials,
        SecondFactor,
        Unlock,
    }

    [Inject] private SessionService CurrentSession { get; set; } = null!;

    [Inject] private SetupClient SetupApi { get; set; } = null!;

    [Inject] private NavigationManager Navigation { get; set; } = null!;

    [SupplyParameterFromQuery] public string? ReturnUrl { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await CurrentSession.InitializeAsync();
        switch (CurrentSession.State)
        {
            case SessionState.Unlocked:
                NavigateBack();
                return;
            case SessionState.Locked:
                _mode = Mode.Unlock;
                return;
        }

        try
        {
            if (await SetupApi.IsRequiredAsync())
            {
                Navigation.NavigateTo("setup");
                return;
            }
        }
        catch (HttpRequestException)
        {
            _error = "AuthError_auth-failed";
        }

        _info = CurrentSession.LogoutReason switch
        {
            "timeout" => "LoginInfoTimeout",
            "expired" => "LoginInfoExpired",
            _ => null,
        };
        CurrentSession.ClearLogoutReason();
        _mode = Mode.Credentials;
    }

    private async Task SubmitOnEnterAsync(KeyboardEventArgs args)
    {
        if (args.Key is "Enter" or "NumpadEnter" && !_busy)
        {
            await SubmitAsync();
        }
    }

    private async Task SubmitAsync()
    {
        if (_form is not null)
        {
            await _form.ValidateAsync();
            if (!_form.IsValid)
            {
                return;
            }
        }

        await RunAsync(async () =>
        {
            switch (_mode)
            {
                case Mode.Credentials:
                    _challenge = await CurrentSession.BeginLoginAsync(_email, _password);
                    _password = string.Empty;
                    _code = string.Empty;
                    _enrollment = null;
                    if (_challenge.Enroll)
                    {
                        // Der Admin hat die 2FA zurückgesetzt: Authenticator-App jetzt neu einrichten.
                        _enrollment = await CurrentSession.BeginTotpEnrollmentAsync(_challenge.Pending);
                        _enrollmentQr = QrCodeService.ToDataUri(QrCodeService.TotpUri("myLife", _email.Trim(), _enrollment.Secret));
                    }

                    _mode = Mode.SecondFactor;
                    break;
                case Mode.SecondFactor when _challenge!.Enroll:
                    await CurrentSession.CompleteLoginWithEnrollmentAsync(_challenge.Pending, _enrollment!.Enrollment, _code);
                    NavigateBack();
                    break;
                case Mode.SecondFactor:
                    await CurrentSession.CompleteLoginWithTotpAsync(_challenge!.Pending, _code);
                    NavigateBack();
                    break;
                case Mode.Unlock:
                    await CurrentSession.UnlockAsync(_password);
                    _password = string.Empty;
                    NavigateBack();
                    break;
            }
        });
    }

    private Task PasskeyAsync() => RunAsync(async () =>
    {
        await CurrentSession.CompleteLoginWithPasskeyAsync(_challenge!.Pending);
        NavigateBack();
    });

    private async Task LogoutAsync()
    {
        await CurrentSession.LogoutAsync();
        _password = string.Empty;
        _mode = Mode.Credentials;
    }

    private void BackToCredentials()
    {
        _error = null;
        _challenge = null;
        _enrollment = null;
        _mode = Mode.Credentials;
    }

    private async Task RunAsync(Func<Task> action)
    {
        _error = null;
        _info = null;
        _busy = true;
        StateHasChanged();
        try
        {
            await action();
        }
        catch (AuthException exception)
        {
            _error = $"AuthError_{exception.Message}";
            if (exception.Message == AuthErrors.Expired)
            {
                BackToCredentials();
                _error = $"AuthError_{AuthErrors.Expired}";
            }
        }
        catch (HttpRequestException)
        {
            _error = $"AuthError_{AuthErrors.Failed}";
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            _error = $"AuthError_{ClientAuthErrors.CryptoFailed}";
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>Nur Ziele innerhalb von myLife, damit der Link nicht auf fremde Seiten umleiten kann.</summary>
    private void NavigateBack()
    {
        var target = ReturnUrl is { Length: > 1 } url && url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal) && !url.StartsWith("/\\", StringComparison.Ordinal)
            ? url.TrimStart('/')
            : string.Empty;
        Navigation.NavigateTo(target);
    }
}
