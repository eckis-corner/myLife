namespace MyLife.Server.Infrastructure;

public sealed class MyLifeOptions
{
    public const string SectionName = "MyLife";

    /// <summary>Wurzelverzeichnis für Dokumente, Sicherungen und Data-Protection-Schlüssel (im Container /data).</summary>
    public string DataDirectory { get; set; } = "data-local";

    /// <summary>Netze, deren X-Forwarded-*-Header angenommen werden (Reverse Proxy). Standard: private Netze.</summary>
    public IList<string> TrustedProxyNetworks { get; set; } = ["10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "127.0.0.0/8", "::1/128"];
}
