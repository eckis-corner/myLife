namespace MyLife.Shared.Vault;

/// <summary>Datentyp eines Felds. Werte liegen im verschlüsselten JSON immer als Zeichenkette in kanonischer Form vor.</summary>
public enum FieldType
{
    /// <summary>Einzeilig.</summary>
    Text,

    /// <summary>Mehrzeilig.</summary>
    LongText,

    /// <summary>Zahl, invariant mit Punkt („1234.5“).</summary>
    Number,

    /// <summary>Betrag in Euro, invariant mit Punkt.</summary>
    Money,

    /// <summary>Prozentsatz, invariant mit Punkt.</summary>
    Percent,

    /// <summary>Datum „yyyy-MM-dd“.</summary>
    Date,

    /// <summary>IBAN ohne Leerzeichen, groß geschrieben, mit Prüfziffer.</summary>
    Iban,

    Bic,

    Phone,

    Email,

    Url,

    /// <summary>Ein Wert aus einer Auswahlliste (<see cref="FieldDefinition.OptionSet"/>).</summary>
    Choice,

    /// <summary>„true“ oder „false“.</summary>
    YesNo,

    /// <summary>Verweis auf ein anderes Objekt (Guid).</summary>
    Reference,

    /// <summary>Genau vier Ziffern, etwa die letzten Ziffern einer Karte.</summary>
    Last4,

    /// <summary>
    /// Ablageort aus der Tabelle „Ablageorte“ (Bereich 14, Entscheidung 143): Der Wert ist der Name des Ablageorts,
    /// der Verweis steht in <see cref="VaultPayload.Locations"/>. Ein Wert ohne Verweis ist Freitext aus der Zeit davor.
    /// </summary>
    Location,
}

/// <summary>Ein Feld nur zeigen, wenn ein Auswahlfeld einen der Werte hat (etwa Kennzeichen nur bei KFZ-Versicherungen).</summary>
public sealed record FieldCondition(string FieldKey, IReadOnlyList<string> Values);

/// <summary>
/// Ein Feld eines Objekttyps. Die Bezeichnung kommt aus den Ressourcen: zuerst <c>Field_{Typ}_{Key}</c>,
/// sonst <c>Field_{Key}</c>; ein Hinweis aus <c>FieldHint_{Key}</c>, falls <see cref="HasHint"/>.
/// </summary>
public sealed record FieldDefinition(string Key, FieldType Type)
{
    public bool Required { get; init; }

    /// <summary>Gehört zu einem vollständigen Datensatz (Vollständigkeitsanzeige, M3).</summary>
    public bool Recommended { get; init; }

    /// <summary>Auswahlliste bei <see cref="FieldType.Choice"/>; Texte unter <c>Option_{OptionSet}_{Wert}</c>.</summary>
    public string? OptionSet { get; init; }

    /// <summary>Erlaubte Objekttypen bei <see cref="FieldType.Reference"/>; leer heißt jeder Typ.</summary>
    public IReadOnlyList<string> ReferenceTypes { get; init; } = [];

    public FieldCondition? VisibleWhen { get; init; }

    /// <summary>Vorschlag beim Anlegen, etwa die Sperr-Hotline 116 116.</summary>
    public string? DefaultValue { get; init; }

    public bool HasHint { get; init; }
}

/// <summary>Bereich, die Einheit für Verschlüsselung und Freigaben. Texte unter <c>Area_{Number}</c> und <c>AreaText_{Number}</c>.</summary>
public sealed record AreaDefinition(int Number, string Key)
{
    /// <summary>Ablage für Objekte; alle Bereiche sind es (seit M3.2 auch die Checkliste).</summary>
    public bool IsAvailable { get; init; } = true;

    /// <summary>Einträge, die fast jeder hier haben sollte (M3.3); fehlen sie, schlägt die Seite sie vor.</summary>
    public IReadOnlyList<TypicalEntry> Typical { get; init; } = [];
}

/// <summary>Typischer Eintrag eines Bereichs: ein Objekttyp, auf Wunsch mit einem bestimmten Wert einer Auswahl (etwa Urkunde „Patientenverfügung“).</summary>
public sealed record TypicalEntry(string TypeKey, string? FieldKey = null, string? Value = null);

/// <summary>
/// Regelmäßige Zahlung eines Objekttyps für die Übersicht „Zahlungen“ (Entscheidung 142): Betragsfeld, Rhythmus aus
/// einem Feld der Auswahlliste <c>frequency</c> oder fest, das belastete Konto und auf Wunsch eine Bedingung
/// (etwa nur Kredite, die ich schulde).
/// </summary>
public sealed record PaymentDefinition(string AmountField)
{
    /// <summary>Feld mit einem Wert der Auswahlliste <c>frequency</c>.</summary>
    public string? FrequencyField { get; init; }

    /// <summary>Fester Rhythmus, wenn der Typ kein Feld dafür hat (etwa die Jahresgebühr einer Karte).</summary>
    public string? FixedFrequency { get; init; }

    /// <summary>Verweis auf das Konto, von dem die Zahlung abgeht.</summary>
    public string? AccountField { get; init; }

    public FieldCondition? When { get; init; }
}

/// <summary>
/// Objekttyp (Entscheidung 29): Bereich, Felder, mögliche Wünsche. Aus dem Schema entstehen Formular, Datenblatt
/// und Suchindex. Name unter <c>Type_{Key}</c>, Bedeutung des Ablaufdatums unter <c>Expiry_{Key}</c> (sonst <c>Expiry_default</c>).
/// </summary>
public sealed record ObjectTypeDefinition(string Key, int Area, IReadOnlyList<FieldDefinition> Fields)
{
    /// <summary>Wünsche für die Hinterbliebenen, Werte der Auswahlliste <c>wish</c>.</summary>
    public IReadOnlyList<string> Wishes { get; init; } = [];

    /// <summary>Bei Urkunden ist der Ablageort des Originals Pflicht.</summary>
    public bool OriginalLocationRequired { get; init; }

    /// <summary>Eigene Bezeichnung für das Ablaufdatum (<c>Expiry_{Key}</c>).</summary>
    public bool HasExpiryLabel { get; init; }

    /// <summary>Das Ablaufdatum gehört zur Vollständigkeit (M3.3), etwa bei Ausweisen und Karten.</summary>
    public bool ExpiryRecommended { get; init; }

    /// <summary>
    /// Höchstens ein Eintrag je Tresor, angelegt und gepflegt auf einer eigenen Seite (das eigene Datenblatt);
    /// steht deshalb nicht in der Auswahl „Neuer Eintrag“ des Bereichs.
    /// </summary>
    public bool Singleton { get; init; }

    /// <summary>Regelmäßige Zahlung, die dieser Typ beschreibt; null, wenn er keine hat.</summary>
    public PaymentDefinition? Payment { get; init; }

    public int SchemaVersion { get; init; } = 1;

    public FieldDefinition? Field(string key) => Fields.FirstOrDefault(f => f.Key == key);
}
