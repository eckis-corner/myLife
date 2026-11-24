namespace MyLife.Server.Data;

/// <summary>Globale Einstellung als Schlüssel/Wert (vom Administrator gepflegt, ab M2 über die Oberfläche).</summary>
public sealed class SystemSetting
{
    public required string Key { get; set; }

    public string? Value { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
