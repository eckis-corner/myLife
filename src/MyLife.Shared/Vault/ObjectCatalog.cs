namespace MyLife.Shared.Vault;

/// <summary>
/// Katalog der Bereiche und Objekttypen nach docs/objekttypen.md. Ein neuer Objekttyp ist nur ein neuer Eintrag
/// hier und seine Texte in tools/resources_vault.py, kein neuer Oberflächencode.
/// Schlüssel und Werte von Auswahllisten landen im verschlüsselten JSON: nur ergänzen, nie umbenennen.
/// </summary>
public static class ObjectCatalog
{
    public const int ChecklistArea = 12;

    /// <summary>Auswahllisten: Name → Werte in Anzeigereihenfolge.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> OptionSets { get; } = new Dictionary<string, IReadOnlyList<string>>
    {
        ["maritalStatus"] = ["single", "married", "partnership", "divorced", "widowed"],
        ["idDocumentKind"] = ["idCard", "passport", "driversLicense", "residencePermit"],
        ["accountKind"] = ["checking", "overnight", "fixedDeposit", "savingsBook", "joint", "business"],
        ["frequency"] = ["monthly", "quarterly", "halfYearly", "yearly", "once"],
        ["cardKind"] = ["visa", "mastercard", "amex", "girocard", "prepaid", "other"],
        ["loanDirection"] = ["iOwe", "owedToMe"],
        ["loanKind"] = ["mortgage", "installment", "car", "leasing", "private", "overdraft", "other"],
        ["insuranceKind"] =
        [
            "life", "termLife", "disability", "accident", "liability", "household", "building", "car", "health", "care",
            "dental", "legal", "travel", "petLiability", "funeral", "pension", "other",
        ],
        ["healthSystem"] = ["statutory", "private"],
        ["contractKind"] =
        [
            "electricity", "gas", "water", "districtHeating", "internet", "mobile", "landline", "streaming", "newspaper",
            "membership", "gym", "software", "rent", "leasing", "other",
        ],
        ["documentKind"] =
        [
            "birth", "marriage", "divorce", "will", "inheritanceContract", "healthcareProxy", "generalProxy", "careDirective",
            "livingWill", "landRegister", "vehicleTitle", "pensionNotice", "taxNotice", "certificate", "other",
        ],
        ["register"] = ["none", "testamentRegister", "provisionRegister"],
        ["propertyKind"] = ["house", "apartment", "land", "garage"],
        ["vehicleKind"] = ["car", "motorcycle", "camper", "trailer", "boat"],
        ["assetKind"] = ["jewelry", "art", "preciousMetal", "collection", "businessShare", "cash", "other"],
        ["onlineAccountKind"] = ["email", "social", "cloud", "messenger", "shop", "payment", "gaming", "other"],
        ["walletKind"] = ["hardware", "software", "exchange"],
        ["deviceKind"] = ["smartphone", "laptop", "tablet", "pc", "nas", "smartHome"],
        ["pmEmergency"] = ["emergencyKit", "emergencyContact", "familyAccount", "other"],
        ["contactRole"] =
        [
            "taxAdvisor", "notary", "lawyer", "employer", "gp", "specialist", "bankAdvisor", "insuranceBroker", "landlord",
            "propertyManager", "undertaker", "pastor", "friend", "other",
        ],
        ["burialKind"] = ["earth", "cremation", "sea", "tree", "anonymous"],
        ["organDecision"] = ["yes", "no", "partial", "delegated"],
        ["employmentKind"] = ["employed", "civilServant", "selfEmployed", "retired"],
        ["pensionKind"] = ["statutory", "company", "riester", "ruerup", "civilService", "professional"],
        ["checklistPhase"] = ["first24h", "firstWeek", "firstMonth", "later"],
        ["checklistArea"] =
        [
            "aboutMe", "banking", "cards", "loans", "insurance", "contracts", "documents", "property", "digital", "passwordManager",
            "contacts", "wishes", "income",
        ],
        ["wish"] =
        [
            "close", "cancel", "takeOver", "keepRunning", "claim", "sell", "handOver", "keep", "delete", "memorialize",
            "reset", "backup",
        ],
    };

    /// <summary>
    /// Empfohlene Felder je Objekttyp (M3.3): was die Hinterbliebenen besonders brauchen. Mit den Pflichtfeldern und dem
    /// Wunsch ergeben sie die Vollständigkeit eines Eintrags.
    /// </summary>
    private static readonly Dictionary<string, string[]> RecommendedFields = new(StringComparer.Ordinal)
    {
        ["personal"] = ["birthDate", "address", "taxId", "pensionInsuranceNumber", "healthInsuranceNumber"],
        ["idDocument"] = ["number"],
        ["bankAccount"] = ["holder", "powerOfAttorney"],
        ["depot"] = ["depotNumber", "holder"],
        ["safeDeposit"] = ["boxNumber", "keyLocation"],
        ["card"] = ["issuer", "last4"],
        ["loan"] = ["contractNumber", "outstandingAmount"],
        ["insurance"] = ["policyNumber"],
        ["contract"] = ["customerNumber", "noticePeriod"],
        ["property"] = ["owners", "landRegistry"],
        ["vehicle"] = ["licensePlate", "titleLocation"],
        ["asset"] = ["storageLocation"],
        ["onlineAccount"] = ["username"],
        ["domain"] = ["registrar"],
        ["cryptoWallet"] = ["seedLocation"],
        ["device"] = ["unlockEntry"],
        ["passwordManager"] = ["pmEmergency", "instructions"],
        ["contact"] = ["phone"],
        ["funeral"] = ["burialKind"],
        ["organDonation"] = ["cardLocation"],
        ["pet"] = ["caretaker"],
        ["letter"] = ["recipients"],
        ["task"] = ["taskText"],
        ["employment"] = ["employer", "personnelNumber"],
        ["pension"] = ["contractNumber"],
        ["taxes"] = ["taxNumber"],
    };

    /// <summary>Bei diesen Typen gehört das Ablaufdatum zur Vollständigkeit (Ausweise, Karten).</summary>
    private static readonly HashSet<string> ExpiryRecommendedTypes = new(StringComparer.Ordinal) { "idDocument", "card" };

    public static IReadOnlyList<AreaDefinition> Areas { get; } =
    [
        new(0, "aboutMe") { Typical = [new("personal"), new("idDocument")] },
        new(1, "banking") { Typical = [new("bankAccount")] },
        new(2, "cards"),
        new(3, "loans"),
        new(4, "insurance") { Typical = [new("insurance", "insuranceKind", "health"), new("insurance", "insuranceKind", "liability")] },
        new(5, "contracts"),
        new(6, "documents")
        {
            Typical =
            [
                new("legalDocument", "documentKind", "birth"), new("legalDocument", "documentKind", "will"),
                new("legalDocument", "documentKind", "livingWill"), new("legalDocument", "documentKind", "healthcareProxy"),
            ],
        },
        new(7, "property"),
        new(8, "digital"),
        new(9, "passwordManager"),
        new(10, "contacts") { Typical = [new("contact")] },
        new(11, "wishes") { Typical = [new("funeral")] },
        new(ChecklistArea, "checklist") { Typical = [new("task")] },
        new(13, "income") { Typical = [new("pension")] },
    ];

    public static IReadOnlyList<ObjectTypeDefinition> Types { get; } = [.. BuildTypes().Select(Recommend)];

    private static readonly Dictionary<string, ObjectTypeDefinition> ByKey = Types.ToDictionary(t => t.Key, StringComparer.Ordinal);

    public static ObjectTypeDefinition? Find(string? key) => key is not null && ByKey.TryGetValue(key, out var type) ? type : null;

    public static AreaDefinition? FindArea(int number) => Areas.FirstOrDefault(a => a.Number == number);

    public static IEnumerable<ObjectTypeDefinition> TypesIn(int area) => Types.Where(t => t.Area == area);

    /// <summary>Bereich zu einem Wert der Auswahlliste <c>checklistArea</c> (dem Schlüssel des Bereichs).</summary>
    public static int? AreaByKey(string? key) => Areas.FirstOrDefault(a => a.Key == key)?.Number;

    /// <summary>Gilt ein Bereich als Ablage für Objekte (0–13, seit M3.2 auch die Checkliste)?</summary>
    public static bool IsUsableArea(int area) => FindArea(area) is { IsAvailable: true };

    private static ObjectTypeDefinition Recommend(ObjectTypeDefinition type)
    {
        var recommended = RecommendedFields.GetValueOrDefault(type.Key) ?? [];
        return type with
        {
            Fields = [.. type.Fields.Select(f => recommended.Contains(f.Key) ? f with { Recommended = true } : f)],
            ExpiryRecommended = ExpiryRecommendedTypes.Contains(type.Key),
        };
    }

    private static FieldDefinition Text(string key, bool required = false) => new(key, FieldType.Text) { Required = required };

    private static FieldDefinition Long(string key, bool required = false) => new(key, FieldType.LongText) { Required = required };

    private static FieldDefinition Choice(string key, string optionSet, bool required = false) =>
        new(key, FieldType.Choice) { OptionSet = optionSet, Required = required };

    private static FieldDefinition Ref(string key, params string[] types) => new(key, FieldType.Reference) { ReferenceTypes = types };

    private static FieldDefinition Of(string key, FieldType type) => new(key, type);

    private static FieldCondition When(string field, params string[] values) => new(field, values);

    private static List<ObjectTypeDefinition> BuildTypes() =>
    [
        // Bereich 0 – Über mich
        new("personal", 0,
        [
            Text("fullName", true), Text("birthName"), Of("birthDate", FieldType.Date), Text("birthPlace"), Long("address"),
            Choice("maritalStatus", "maritalStatus"), Text("nationality"), Text("religion"), Text("taxId"),
            Text("pensionInsuranceNumber"), Text("healthInsuranceNumber"),
        ]),
        new("idDocument", 0,
        [
            Choice("idDocumentKind", "idDocumentKind", true), Text("number"), Text("authority"),
        ]) { HasExpiryLabel = true },

        // Bereich 1 – Bankkonten & Depots
        new("bankAccount", 1,
        [
            Text("bank", true), Of("iban", FieldType.Iban) with { Recommended = true }, Of("bic", FieldType.Bic),
            Choice("accountKind", "accountKind"), Text("holder"), Text("coHolder"), Text("powerOfAttorney"),
            Of("poaBeyondDeath", FieldType.YesNo), Of("onlineBanking", FieldType.YesNo),
        ]) { Wishes = ["close", "takeOver", "keepRunning"] },
        new("depot", 1,
        [
            Text("broker", true), Text("depotNumber"), Ref("settlementAccount", "bankAccount"), Text("holder"), Text("powerOfAttorney"),
            Of("approxValue", FieldType.Money), Of("valueDate", FieldType.Date),
        ]) { Wishes = ["sell", "takeOver", "keepRunning"] },
        new("safeDeposit", 1,
        [
            Text("bank", true), Text("branch"), Text("boxNumber"), Text("keyLocation"), Text("authorizedPersons"), Long("contents"),
        ]) { Wishes = ["close", "takeOver"] },

        // Bereich 2 – Kreditkarten
        new("card", 2,
        [
            Choice("cardKind", "cardKind", true), Text("issuer"), Text("cardholder"), Of("last4", FieldType.Last4) with { HasHint = true },
            Ref("referenceAccount", "bankAccount"), new("blockHotline", FieldType.Phone) { DefaultValue = "116 116" },
            Text("additionalCards"), Of("annualFee", FieldType.Money), Long("includedInsurances"),
        ]) { Wishes = ["cancel", "keepRunning"], HasExpiryLabel = true },

        // Bereich 3 – Kredite & Darlehen
        new("loan", 3,
        [
            Choice("loanDirection", "loanDirection", true), Choice("loanKind", "loanKind"), Text("counterparty", true), Text("contractNumber"),
            Text("borrower"), Of("originalAmount", FieldType.Money), Of("outstandingAmount", FieldType.Money), Of("outstandingDate", FieldType.Date),
            Of("installment", FieldType.Money), Choice("frequency", "frequency"), Of("interestRate", FieldType.Percent),
            Of("termUntil", FieldType.Date), Of("creditLifeInsurance", FieldType.YesNo),
            Ref("creditLifeInsuranceRef", "insurance") with { VisibleWhen = When("creditLifeInsurance", "true") },
            Ref("collateral", "property", "vehicle", "asset"), Ref("debitAccount", "bankAccount"),
        ]) { Wishes = ["takeOver", "claim", "cancel"], HasExpiryLabel = true },

        // Bereich 4 – Versicherungen
        new("insurance", 4,
        [
            Choice("insuranceKind", "insuranceKind", true), Text("insurer", true), Text("policyNumber"), Text("policyholder"), Text("insuredPerson"),
            Text("beneficiaries"), Of("sumInsured", FieldType.Money), Of("premium", FieldType.Money), Choice("frequency", "frequency"),
            Of("startDate", FieldType.Date), Text("mainDueDate"), Text("noticePeriod"), Ref("paymentAccount", "bankAccount"),
            Ref("broker", "contact"), Text("deathNotificationPeriod") with { HasHint = true },
            Ref("vehicle", "vehicle") with { VisibleWhen = When("insuranceKind", "car") },
            Text("licensePlate") with { VisibleWhen = When("insuranceKind", "car") },
            Text("noClaimsClass") with { VisibleWhen = When("insuranceKind", "car") },
            Choice("healthSystem", "healthSystem") with { VisibleWhen = When("insuranceKind", "health", "care") },
            Text("insuredNumber") with { VisibleWhen = When("insuranceKind", "health", "care", "dental") },
            Of("disabilityPension", FieldType.Money) with { VisibleWhen = When("insuranceKind", "disability") },
            Ref("property", "property") with { VisibleWhen = When("insuranceKind", "household", "building") },
        ]) { Wishes = ["claim", "cancel", "takeOver", "keepRunning"], HasExpiryLabel = true },

        // Bereich 5 – Verträge & Abos
        new("contract", 5,
        [
            Choice("contractKind", "contractKind", true), Text("provider", true), Text("customerNumber"), Text("contractNumber"), Text("holder"),
            Of("cost", FieldType.Money), Choice("frequency", "frequency"), Ref("paymentAccount", "bankAccount"), Text("noticePeriod"),
            Of("specialTerminationOnDeath", FieldType.YesNo),
            Ref("landlord", "contact") with { VisibleWhen = When("contractKind", "rent") },
            Of("deposit", FieldType.Money) with { VisibleWhen = When("contractKind", "rent") },
            Ref("depositAccount", "bankAccount") with { VisibleWhen = When("contractKind", "rent") },
        ]) { Wishes = ["cancel", "takeOver", "keepRunning"], HasExpiryLabel = true },

        // Bereich 6 – Urkunden & Dokumente
        new("legalDocument", 6,
        [
            Choice("documentKind", "documentKind", true), Text("issuer"), Text("number"), Of("issueDate", FieldType.Date), Text("copiesWith"),
            Text("authorizedPersons") with { VisibleWhen = When("documentKind", "healthcareProxy", "generalProxy", "careDirective") },
            Choice("register", "register"),
            Text("registrationNumber") with { VisibleWhen = When("register", "testamentRegister", "provisionRegister") },
        ]) { OriginalLocationRequired = true, HasExpiryLabel = true },

        // Bereich 7 – Immobilien, Fahrzeuge & Vermögen
        new("property", 7,
        [
            Choice("propertyKind", "propertyKind", true), Long("address", true), Text("owners"), Text("landRegistry"),
            Of("acquisitionYear", FieldType.Number), Of("approxValue", FieldType.Money), Ref("financing", "loan"), Ref("buildingInsurance", "insurance"),
            Of("rented", FieldType.YesNo), Text("tenant") with { VisibleWhen = When("rented", "true") },
            Of("rentIncome", FieldType.Money) with { VisibleWhen = When("rented", "true") },
            Ref("propertyManager", "contact"), Text("keyLocation"),
        ]) { Wishes = ["sell", "handOver", "keep"] },
        new("vehicle", 7,
        [
            Choice("vehicleKind", "vehicleKind", true), Text("makeModel", true), Text("licensePlate"), Text("vin"), Text("keeper"), Text("owner"),
            Text("titleLocation"), Ref("financing", "loan", "contract"), Ref("vehicleInsurance", "insurance"), Text("spareKey"), Ref("garage", "contact"),
        ]) { Wishes = ["sell", "handOver", "keep"] },
        new("asset", 7,
        [
            Choice("assetKind", "assetKind", true), Long("description"), Of("approxValue", FieldType.Money), Of("valueDate", FieldType.Date),
            Text("storageLocation"), Text("proof"),
        ]) { Wishes = ["sell", "handOver", "keep"] },

        // Bereich 8 – Digitaler Nachlass
        new("onlineAccount", 8,
        [
            Choice("onlineAccountKind", "onlineAccountKind", true), Text("service", true), Text("username") with { HasHint = true },
            Text("passwordManagerEntry"), Text("twoFactorDevice"), Of("legacyContactSet", FieldType.YesNo),
        ]) { Wishes = ["delete", "memorialize", "handOver"] },
        new("domain", 8,
        [
            Text("domainName", true), Text("registrar"), Text("customerNumber"),
        ]) { Wishes = ["cancel", "handOver", "keepRunning"], HasExpiryLabel = true },
        new("cryptoWallet", 8,
        [
            Choice("walletKind", "walletKind", true), Text("provider"), Text("seedLocation") with { HasHint = true }, Of("approxValue", FieldType.Money),
        ]) { Wishes = ["handOver", "sell"] },
        new("device", 8,
        [
            Choice("deviceKind", "deviceKind", true), Text("deviceName", true), Text("unlockEntry"),
        ]) { Wishes = ["reset", "handOver", "backup"] },

        // Bereich 9 – Passwortmanager
        new("passwordManager", 9,
        [
            Text("product", true), Text("account"), Choice("pmEmergency", "pmEmergency"), Text("emergencyContacts"),
            Text("providerWaitingPeriod"), Text("masterPasswordLocation") with { HasHint = true }, Long("instructions"),
        ]),

        // Bereich 10 – Wichtige Kontakte
        new("contact", 10,
        [
            Choice("contactRole", "contactRole"), Text("name", true), Text("company"), Of("phone", FieldType.Phone), Of("email", FieldType.Email),
            Long("address"), Of("notifyOnDeath", FieldType.YesNo),
        ]),

        // Bereich 11 – Persönliche Wünsche
        new("funeral", 11,
        [
            Choice("burialKind", "burialKind"), Text("place"), Ref("funeralProvision", "contract", "insurance"), Long("ceremony"),
            Long("music"), Long("obituary"), Long("invitees"),
        ]),
        new("organDonation", 11,
        [
            Choice("organDecision", "organDecision", true), Text("decidedBy") with { VisibleWhen = When("organDecision", "delegated") },
            Text("cardLocation"), Of("registered", FieldType.YesNo),
        ]),
        new("pet", 11,
        [
            Text("petName", true), Text("species"), Ref("vet", "contact"), Text("caretaker"), Long("particulars"),
        ]),
        new("letter", 11,
        [
            Text("recipients") with { HasHint = true }, Long("letterText", true),
        ]),
        new("otherWish", 11,
        [
            Long("wishText", true),
        ]),

        // Bereich 12 – Checkliste für die Hinterbliebenen (M3.2): Aufgabe mit Phase und Regel für verknüpfte Einträge
        new("task", ChecklistArea,
        [
            Choice("checklistPhase", "checklistPhase", true), Long("taskText"), Text("responsible"),
            Choice("linkedArea", "checklistArea"), Choice("linkedWish", "wish") with { HasHint = true },
        ]),

        // Bereich 13 – Einkommen, Rente & Steuern
        new("employment", 13,
        [
            Ref("employer", "contact"), Text("personnelNumber"), Choice("employmentKind", "employmentKind"), Ref("salaryAccount", "bankAccount"),
        ]),
        new("pension", 13,
        [
            Choice("pensionKind", "pensionKind", true), Text("provider"), Text("contractNumber"), Of("survivorBenefit", FieldType.YesNo),
        ]),
        new("taxes", 13,
        [
            Text("taxOffice", true), Text("taxNumber"), Ref("taxAdvisor", "contact"), Text("recordsLocation"),
        ]),
    ];
}
