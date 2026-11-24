using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MyLife.Server.Infrastructure.Admin;
using MyLife.Shared.Admin;

namespace MyLife.Server.Infrastructure.Mail;

/// <summary>Zugangsdaten für einen Versand, aus den Einstellungen mit entschlüsseltem Passwort.</summary>
internal sealed record SmtpConfig(string Host, int Port, SmtpSecurity Security, string? Username, string? Password, string FromAddress, string FromName);

/// <summary>Verschickt eine Mail. In den Tests ersetzt durch eine Attrappe, die nur mitschreibt.</summary>
internal interface IMailTransport
{
    Task SendAsync(SmtpConfig config, MimeMessage message, CancellationToken cancellationToken);
}

/// <summary>Versand per SMTP mit MailKit.</summary>
internal sealed class SmtpMailTransport : IMailTransport
{
    public async Task SendAsync(SmtpConfig config, MimeMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        using var client = new SmtpClient { Timeout = 30_000 };
        var security = config.Security switch
        {
            SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
            SmtpSecurity.None => SecureSocketOptions.None,
            _ => SecureSocketOptions.StartTlsWhenAvailable,
        };
        await client.ConnectAsync(config.Host, config.Port, security, cancellationToken);
        if (!string.IsNullOrEmpty(config.Username))
        {
            await client.AuthenticateAsync(config.Username, config.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}

/// <summary>Baut aus Einstellungen und Vorlage die fertige Nachricht.</summary>
internal sealed class MailComposer(SettingsStore settings)
{
    public async Task<SmtpConfig?> ConfigAsync(CancellationToken cancellationToken)
    {
        var stored = await settings.GetAsync(cancellationToken);
        if (!SettingsStore.IsMailConfigured(stored))
        {
            return null;
        }

        var smtp = stored.Smtp;
        return new SmtpConfig(smtp.Host, smtp.Port, smtp.Security, smtp.Username, settings.SmtpPassword(stored), smtp.FromAddress, smtp.FromName);
    }

    /// <summary>Allgemeine Parameter: Adresse der Instanz für die Fußzeile.</summary>
    public async Task<Dictionary<string, string>> WithInstanceAsync(IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        var stored = await settings.GetAsync(cancellationToken);
        var all = new Dictionary<string, string>(parameters, StringComparer.Ordinal)
        {
            ["instance"] = stored.PublicUrl ?? "myLife",
        };
        return all;
    }

    public static MimeMessage Build(SmtpConfig config, string recipient, RenderedMail mail)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(mail);
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(config.FromName, config.FromAddress));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = mail.Subject;
        message.Body = new BodyBuilder { TextBody = mail.Text, HtmlBody = mail.Html }.ToMessageBody();
        return message;
    }
}
