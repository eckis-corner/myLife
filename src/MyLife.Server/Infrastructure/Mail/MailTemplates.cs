using System.Globalization;
using System.Net;
using System.Resources;
using System.Text;

namespace MyLife.Server.Infrastructure.Mail;

/// <summary>Schlüssel der Mailvorlagen. Texte in <c>Resources/MailResource.resx</c> (Deutsch) und <c>.en.resx</c>.</summary>
internal static class MailTemplate
{
    public const string Test = "test";
    public const string AccountLocked = "account-locked";
    public const string AccountUnlocked = "account-unlocked";
    public const string TwoFactorReset = "twofactor-reset";
    public const string DeletionRequest = "deletion-request";
    public const string AccountDeleted = "account-deleted";
    public const string Invitation = "invitation";
    public const string InvitationAccepted = "invitation-accepted";
    public const string TrusteeAdded = "trustee-added";
    public const string LifesignDue = "lifesign-due";
    public const string LifesignReminder = "lifesign-reminder";
    public const string ReleasedOwner = "released-owner";
    public const string ReleasedTrustee = "released-trustee";
    public const string RequestOwner = "request-owner";
    public const string RequestInfo = "request-info";
    public const string RequestPaused = "request-paused";
    public const string RequestObjected = "request-objected";

    public static IReadOnlyList<string> All { get; } =
    [
        Test, AccountLocked, AccountUnlocked, TwoFactorReset, DeletionRequest, AccountDeleted, Invitation, InvitationAccepted, TrusteeAdded,
        LifesignDue, LifesignReminder, ReleasedOwner, ReleasedTrustee, RequestOwner, RequestInfo, RequestPaused, RequestObjected,
    ];
}

/// <summary>Fertig gesetzte Mail: Betreff, Text- und HTML-Fassung.</summary>
internal sealed record RenderedMail(string Subject, string Text, string Html);

/// <summary>
/// Setzt Mails in der Sprache der Empfängerin oder des Empfängers (Architektur 9). Platzhalter in geschweiften
/// Klammern, etwa <c>{name}</c>; der Parameter <c>link</c> wird in der HTML-Fassung zum Link.
/// </summary>
internal static class MailTemplates
{
    private static readonly ResourceManager Resources = new("MyLife.Server.Resources.MailResource", typeof(MailTemplates).Assembly);

    public static RenderedMail Render(string template, string language, IReadOnlyDictionary<string, string> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var culture = CultureInfo.GetCultureInfo(language is "en" ? "en" : "de");
        var subject = Fill(Text($"Mail_{template}_subject", culture), parameters);
        var body = Fill(Text($"Mail_{template}_body", culture), parameters);
        var footer = Fill(Text("Mail_footer", culture), parameters);
        var paragraphs = body.ReplaceLineEndings("\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Append(footer).ToList();

        var html = new StringBuilder("<!DOCTYPE html><html><body style=\"font-family:Arial,Helvetica,sans-serif;font-size:15px;line-height:1.5;color:#202d39\">");
        foreach (var paragraph in paragraphs)
        {
            var encoded = WebUtility.HtmlEncode(paragraph).ReplaceLineEndings("<br>");
            if (parameters.TryGetValue("link", out var link) && link.Length > 0)
            {
                var encodedLink = WebUtility.HtmlEncode(link);
                encoded = encoded.Replace(encodedLink, $"<a href=\"{encodedLink}\">{encodedLink}</a>", StringComparison.Ordinal);
            }

            var style = paragraph == footer ? " style=\"color:#6b7785;font-size:12px\"" : string.Empty;
            html.Append(CultureInfo.InvariantCulture, $"<p{style}>{encoded}</p>");
        }

        html.Append("</body></html>");
        return new RenderedMail(subject, string.Join("\n\n", paragraphs) + "\n", html.ToString());
    }

    /// <summary>Ein einzelnes Wort aus den Mailtexten in der Sprache der Mail, etwa ein Modus (<c>Mail_mode_death</c>).</summary>
    public static string Phrase(string key, string language) => Text(key, CultureInfo.GetCultureInfo(language is "en" ? "en" : "de"));

    /// <summary>Datum in der Sprache der Mail, lang ausgeschrieben.</summary>
    public static string Date(DateTimeOffset value, string language) =>
        value.ToLocalTime().ToString("D", CultureInfo.GetCultureInfo(language is "en" ? "en" : "de"));

    private static string Text(string key, CultureInfo culture) =>
        Resources.GetString(key, culture) ?? throw new InvalidOperationException($"Mailtext fehlt: {key}");

    private static string Fill(string text, IReadOnlyDictionary<string, string> parameters)
    {
        foreach (var (name, value) in parameters)
        {
            text = text.Replace("{" + name + "}", value, StringComparison.Ordinal);
        }

        return text;
    }
}
