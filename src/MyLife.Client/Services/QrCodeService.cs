using System.Text;
using QRCoder;

namespace MyLife.Client.Services;

/// <summary>QR-Codes im Browser, als SVG-Daten-URI (erlaubt durch <c>img-src data:</c>). Inhalte gehen nie an einen Dienst.</summary>
public static class QrCodeService
{
    public static string ToDataUri(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var svg = new SvgQRCode(data).GetGraphic(8);
        return "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
    }

    /// <summary>otpauth-URI für Authenticator-Apps (RFC 6238, SHA-1, 6 Ziffern, 30 Sekunden).</summary>
    public static string TotpUri(string issuer, string account, string secret) =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}" +
        $"?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits=6&period=30";
}
