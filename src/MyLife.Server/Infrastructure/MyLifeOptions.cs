namespace MyLife.Server.Infrastructure;

public sealed class MyLifeOptions
{
    public const string SectionName = "MyLife";

    /// <summary>Wurzelverzeichnis für Dokumente, Sicherungen und Data-Protection-Schlüssel (im Container /data).</summary>
    public string DataDirectory { get; set; } = "data-local";

    /// <summary>
    /// Sicherungsschlüssel (M3.4): 32 Byte als Base64, aus <c>MYLIFE_BACKUP_KEY</c> in der <c>.env</c>. Ohne ihn
    /// gibt es keine automatische Sicherung. Er gehört getrennt von den Sicherungen aufbewahrt.
    /// </summary>
    public string? BackupKey { get; set; }

    /// <summary>Netze, deren X-Forwarded-*-Header angenommen werden (Reverse Proxy). Standard: private Netze.</summary>
    public IList<string> TrustedProxyNetworks { get; set; } = ["10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "127.0.0.0/8", "::1/128"];

    /// <summary>Automatische Abmeldung nach Inaktivität in Minuten (ab M2 vom Administrator einstellbar).</summary>
    public int IdleTimeoutMinutes { get; set; } = 15;

    /// <summary>Fehlversuche, nach denen ein Konto vorübergehend gesperrt wird.</summary>
    public int LockoutThreshold { get; set; } = 5;

    /// <summary>Dauer der Sperre nach zu vielen Fehlversuchen in Minuten.</summary>
    public int LockoutMinutes { get; set; } = 15;

    /// <summary>Anfragen je Minute und IP an Anmeldung und Konto.</summary>
    public int AuthRequestsPerMinute { get; set; } = 60;

    /// <summary>Anfragen je Minute und IP an den Tresor (Objekte, Dokumente, Audit-Log).</summary>
    public int VaultRequestsPerMinute { get; set; } = 600;
}
