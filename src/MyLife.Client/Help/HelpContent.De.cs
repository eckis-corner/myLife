using MudBlazor;

namespace MyLife.Client.Help;

/// <summary>Tutorial und Anleitung auf Deutsch.</summary>
public static partial class HelpContent
{
    private static HelpTexts German() => new(GermanTutorial(), GermanGuide());

    private static TutorialChapter[] GermanTutorial() =>
    [
        new("willkommen", "myLife in fünf Minuten", Icons.Material.Outlined.RocketLaunch,
            """
            myLife ist der digitale Notfallordner deiner Familie. Du sammelst an einem Ort, was deine Angehörigen wissen müssen, wenn dir etwas zustößt: Konten, Versicherungen, Verträge, Urkunden, digitale Zugänge, Kontakte und persönliche Wünsche. **Solange es dir gut geht, kann niemand außer dir die Daten lesen**, auch nicht der Betreiber des Servers.
            """,
            """
            **So funktioniert es**

            1. Du richtest dein Konto ein und bekommst einen eigenen, verschlüsselten **Tresor**.
            2. Du füllst ihn nach und nach: je Bereich die Einträge mit ihren Datenblättern und Dokumenten.
            3. Du lädst deine Familie ein und legst fest, wer im Ernstfall welche Bereiche sehen darf.
            4. Meldest du dich längere Zeit nicht oder beantragt eine Vertrauensperson den Zugang, ohne dass du widersprichst, gibt myLife genau das frei, was du für diesen Fall bestimmt hast.

            **Die Seitenleiste**

            | Menüeintrag | Wofür |
            | --- | --- |
            | Übersicht | Alle Bereiche mit Anzahl, Vollständigkeit und „Zuletzt geprüft“, dazu was bald abläuft |
            | Suche | Volltextsuche über deine Einträge |
            | Zahlungen | Was regelmäßig von deinen Konten abgeht, je Monat, je Jahr und je Bereich |
            | Bereiche | Deine Einträge, von „Über mich“ bis zur Checkliste |
            | Familie | Alle Personen dieser Instanz, Einladungen |
            | Vertrauenspersonen | Wer im Ernstfall was sehen darf |
            | Mit mir geteilt | Was andere schon heute mit dir teilen |
            | Notfallzugang | Lebenszeichen, Fristen, Anträge |
            | Lesemodus | Was andere dir für den Ernstfall hinterlassen haben |
            | Export & Druck | Notfallordner als PDF, Datensicherung, Notfallpaket |
            | Verwaltung | Protokoll und Papierkorb; für Administratoren auch Einstellungen und Benutzer |
            | Tutorial, Anleitung | Diese Hilfe |
            | Konto | Erinnerungen, Sicherheit, Details, Abmelden |

            > Verschlüsselt und entschlüsselt wird ausschließlich in deinem Browser. Der Server speichert nur Daten, die er selbst nicht lesen kann.
            """,
            ["anmelden", "gehoert-nicht-hinein"]),

        new("einrichten", "Einrichten", Icons.Material.Outlined.Key,
            """
            Wie du zu deinem Konto kommst, hängt davon ab, ob myLife schon läuft: Die **erste Person** richtet die Instanz mit dem Einrichtungsassistenten ein und wird Administrator. **Alle anderen** bekommen eine Einladung.
            """,
            """
            **Was du bereithalten solltest**

            - etwa zehn Minuten Zeit,
            - einen Drucker oder Stift und Papier für das **Wiederherstellungs-Kit**,
            - eine **Authenticator-App** auf dem Smartphone, etwa Aegis, Google oder Microsoft Authenticator, oder deinen Passwortmanager.

            **Die erste Person (Administrator)**

            1. Die Instanz ist installiert (siehe Anleitung, Teil „Verwaltung“). Beim ersten Aufruf startet der **Einrichtungsassistent**.
            2. Den **Einrichtungscode** aus dem Protokoll des Containers eingeben.
            3. Name und E-Mail-Adresse angeben, ein **Master-Passwort** wählen, das **Wiederherstellungs-Kit** drucken und zur Kontrolle einige Wörter eingeben, die Authenticator-App einrichten.
            4. **Tresor anlegen**: Dein Browser erzeugt die Schlüssel, das dauert einige Sekunden.

            **Alle anderen (Einladung)**

            1. Den Link aus der Einladungsmail oder aus der Nachricht der einladenden Person öffnen.
            2. Master-Passwort wählen, Wiederherstellungs-Kit drucken, Authenticator-App einrichten, fertig.

            > Das Master-Passwort kann niemand zurücksetzen, auch der Administrator nicht. Geht es verloren, hilft nur das Wiederherstellungs-Kit. Bewahre es getrennt vom Passwort auf, etwa im Schließfach.
            """,
            ["einrichtungsassistent", "einladung-annehmen", "installation"]),

        new("anmelden", "Anmelden und sicher bleiben", Icons.Material.Outlined.Login,
            """
            Zur Anmeldung brauchst du drei Dinge: deine **E-Mail-Adresse**, dein **Master-Passwort** und einen **zweiten Faktor**, also den Code aus der Authenticator-App oder einen Passkey.
            """,
            """
            **Im Alltag**

            - Nach einer Weile ohne Aktivität meldet myLife dich ab und entfernt alle Schlüssel aus dem Speicher. Wie lange, legt der Administrator fest, üblich sind 15 Minuten.
            - Lädst du die Seite neu, bleibst du angemeldet, aber der Tresor ist **gesperrt**. Zum Entsperren genügt das Master-Passwort.
            - Unter **Konto → Sicherheit** änderst du dein Master-Passwort und richtest **Passkeys** ein, etwa Fingerabdruck oder Gesichtserkennung statt des Codes.

            **Passwort vergessen?**

            Auf der Anmeldeseite **Passwort vergessen?** wählen und die 24 Wörter aus dem Wiederherstellungs-Kit eingeben. Danach vergibst du ein neues Master-Passwort; dein Tresor bleibt unverändert.
            """,
            ["anmelden", "entsperren", "passwort-vergessen", "passkeys"]),

        new("erfassen", "Die ersten Einträge", Icons.Material.Outlined.Inventory2,
            """
            Dein Tresor ist in **Bereiche** gegliedert, von „Über mich“ über Bankkonten und Versicherungen bis zum digitalen Nachlass. In jedem Bereich legst du **Einträge** an. Jeder Eintrag hat ein Datenblatt mit passenden Feldern und beliebig viele **Dokumente**.
            """,
            """
            **Ein Beispiel: dein Girokonto**

            1. In der Seitenleiste unter **Bereiche** auf **Bankkonten & Depots** klicken.
            2. **Bankkonto anlegen** wählen. Über den kleinen Pfeil daneben gibt es die anderen Typen des Bereichs, etwa Depot oder Schließfach.
            3. Bank, IBAN, Kontoinhaber und Vollmachten eintragen. Pflichtfelder sind markiert; die IBAN wird auf ihre Prüfziffer geprüft.
            4. Unter **Wunsch für die Hinterbliebenen** festlegen, was mit dem Konto geschehen soll, etwa „auflösen“ oder „weiterlaufen lassen“.
            5. **Speichern**. Danach unter **Dokumente** den Kontovertrag als PDF oder Foto hinzufügen.

            **Gute Reihenfolge für den Anfang**

            - **Konto → Details**: Name, Vorname und ein Bild.
            - **Über mich**: persönliche Daten und Ausweise.
            - **Urkunden & Dokumente**: Testament, Vollmachten, Patientenverfügung, jeweils mit dem **Ablageort des Originals**.
            - **Versicherungen**: vor allem Lebens- und Unfallversicherungen; sie haben kurze Meldefristen im Todesfall.
            - **Passwortmanager**: welcher es ist und wie der Notfallzugang funktioniert.

            **Was nicht hineingehört**

            PINs, TANs, vollständige Kartennummern, einzelne Passwörter und Seed-Phrasen. myLife warnt dich beim Speichern, wenn ein Text danach aussieht.
            """,
            ["eintrag-anlegen", "dokumente", "wuensche-verweise", "gehoert-nicht-hinein", "eigenes-datenblatt"]),

        new("ueberblick", "Den Überblick behalten", Icons.Material.Outlined.Dashboard,
            """
            Ein Notfallordner hilft nur, wenn er stimmt. Die **Übersicht** zeigt dir deshalb, wie vollständig jeder Bereich ist und wann du ihn zuletzt geprüft hast, und myLife **erinnert** dich per Mail.
            """,
            """
            **Auf der Übersicht**

            - Jede Kachel zeigt die Anzahl der Einträge, wie vollständig der Bereich ist und wann er zuletzt geprüft wurde.
            - Hast du einen Bereich durchgesehen, klickst du auf **Als geprüft markieren**.
            - Oben steht, was **bald abläuft** (Ausweis, Karte, Vertrag) und welche **Wiedervorlagen** anstehen.

            **Erinnerungen einstellen**

            Unter **Konto → Erinnerungen** legst du fest, wie oft du die Bereiche prüfen willst (Standard: alle 6 Monate) und wie viele Tage vor einem Ablaufdatum myLife dich erinnert (Standard: 60 Tage). Die Mails nennen keine Einzelheiten; was ansteht, siehst du nach der Anmeldung.

            **Finden**

            Die **Suche** durchsucht alle deine Einträge. Sie läuft nur in deinem Browser; den Inhalt von PDFs durchsucht sie nicht.
            """,
            ["uebersicht", "erinnerungen", "wiedervorlage", "suche"]),

        new("familie", "Die Familie einladen", Icons.Material.Outlined.Diversity3,
            """
            Vertrauenspersonen sind immer Personen mit eigenem Konto auf dieser Instanz. Deshalb lädst du sie zuerst ein. Jede eingeladene Person bekommt einen **eigenen Tresor** und sieht von deinem nichts, solange du nichts freigibst.
            """,
            """
            **Jemanden einladen**

            1. In der Seitenleiste **Familie** öffnen und **Person einladen** wählen.
            2. E-Mail-Adresse und Sprache der Einladung angeben.
            3. Entscheiden: **Per Mail von myLife senden** (bequem, der Normalfall) oder **Link selbst weitergeben**, etwa per Messenger (am sichersten, der Server sieht den Link nie).
            4. Die Person nimmt die Einladung an und richtet ihr Konto ein.

            **Schlüssel prüfen, ohne Telefon**

            Nimmt die Person die Einladung an, prüft dein Browser ihren Schlüssel automatisch und merkt sich ihn. Unter **Familie** steht danach „Von dir bestätigt“. Personen, die jemand anderes eingeladen hat, gelten als bestätigt, wenn jemand, dem du vertraust, sie gegengezeichnet hat.

            > Steht irgendwo **Schlüssel geändert!**, gibt myLife für diese Person nichts frei. Sprich mit ihr auf anderem Weg und wende dich an den Administrator.
            """,
            ["einladen", "schluessel"]),

        new("vertrauenspersonen", "Vertrauenspersonen und Freigaben", Icons.Material.Outlined.VolunteerActivism,
            """
            Hier legst du fest, **wer im Ernstfall welche Bereiche sehen darf**. Es gibt zwei Fälle, die du getrennt freigibst: **verstorben** und **handlungsunfähig**. Zusätzlich kannst du einzelne Bereiche **sofort teilen**, nur zum Lesen.
            """,
            """
            **Eine Vertrauensperson einsetzen**

            1. **Vertrauenspersonen** öffnen und **Vertrauensperson hinzufügen** wählen.
            2. Die Person auswählen und die **Beziehung** eintragen, etwa Tochter oder Bruder. Beziehung und Notiz sind verschlüsselt; der Server sieht sie nicht.
            3. Festlegen, ob sie **einen Zugang beantragen** darf.
            4. In der Tabelle je Bereich ankreuzen: **Verstorben**, **Handlungsunfähig** und, wenn gewünscht, **Sofort teilen**. Mit **alle** kreuzt du eine ganze Spalte an.
            5. Unten die **persönlichen Briefe** auswählen, die die Person bekommen soll.
            6. **Speichern und verschlüsseln**. Dein Browser verschlüsselt die Schlüssel der Bereiche für diese Person; der Server gibt sie erst heraus, wenn der Fall eintritt.

            **Beispiel**

            | Bereich | Partnerin | Sohn |
            | --- | --- | --- |
            | Bankkonten & Depots | verstorben, handlungsunfähig | verstorben |
            | Versicherungen | verstorben, handlungsunfähig | verstorben |
            | Persönliche Wünsche | verstorben | verstorben |
            | Checkliste | verstorben, handlungsunfähig | verstorben |

            > Gib die **Checkliste** frei wie jeden anderen Bereich, sonst sehen deine Vertrauenspersonen sie im Lesemodus nicht.
            """,
            ["vertrauenspersonen", "briefe", "sofort-teilen"]),

        new("checkliste", "Die Checkliste", Icons.Material.Outlined.Checklist,
            """
            Die **Checkliste** führt deine Vertrauenspersonen Schritt für Schritt durch das, was zu tun ist: in den ersten 24 Stunden, in der ersten Woche, im ersten Monat und später. Jede Aufgabe zeigt die passenden Einträge, etwa alle Verträge, die gekündigt werden sollen.
            """,
            """
            **Mit der Vorlage beginnen**

            1. In der Seitenleiste unter **Bereiche** die **Checkliste** öffnen.
            2. **Vorlage übernehmen** wählen. Die Aufgaben der Vorlage für Deutschland landen in deinem Tresor, von der Ärztin bis zum Finanzamt.
            3. Aufgaben ändern, ergänzen oder löschen wie jeden anderen Eintrag. Mit **Neue Aufgabe** legst du eigene an.

            **Aufgaben mit Einträgen verknüpfen**

            Eine Aufgabe kann alle Einträge eines Bereichs auflisten, auf Wunsch nur die mit einem bestimmten **Wunsch**. Beispiel: „Verträge & Abos“ mit dem Wunsch „kündigen“ listet genau die Verträge, die gekündigt werden sollen. Pflege deshalb bei deinen Einträgen den Wunsch für die Hinterbliebenen.

            **Im Ernstfall**

            Deine Vertrauenspersonen haken die Aufgaben im Lesemodus gemeinsam ab; alle sehen denselben Stand und wer was erledigt hat.
            """,
            ["checkliste", "wuensche-verweise"]),

        new("notfallzugang", "Der Notfallzugang", Icons.Material.Outlined.HealthAndSafety,
            """
            Der Notfallzugang sorgt dafür, dass deine Vertrauenspersonen im Ernstfall an die Daten kommen, **aber nicht vorher**. Er wird aktiv, sobald du mindestens eine Vertrauensperson benannt hast.
            """,
            """
            **Weg 1: Kein Lebenszeichen**

            1. Jede Anmeldung zählt als **Lebenszeichen**. Alternativ klickst du auf den Link in der Erinnerungsmail oder unter **Notfallzugang** auf **Lebenszeichen geben**.
            2. Bleibt das Lebenszeichen länger als dein Intervall aus (Standard: 30 Tage), kommen gestufte Erinnerungen, üblicherweise nach 3, 7 und 14 Tagen.
            3. Reagierst du auf keine, beginnt die **Wartezeit** (Standard: 14 Tage).
            4. Danach sind beide Fälle freigegeben, verstorben und handlungsunfähig.

            **Weg 2: Antrag einer Vertrauensperson**

            1. Eine Vertrauensperson mit Antragsrecht beantragt den Zugang für einen Fall.
            2. Du bekommst sofort eine Mail mit dem Link **Widersprechen**, und alle anderen Vertrauenspersonen werden informiert.
            3. Ein Klick auf den Link hält den Antrag an. Danach meldest du dich an und entscheidest: **Endgültig widersprechen** oder **Weiterlaufen lassen**.
            4. Ohne Widerspruch wird der beantragte Fall nach der Antragsfrist freigegeben (Standard: 7 Tage).

            **Deine Fristen**

            Unter **Notfallzugang → Deine Fristen** wählst du Intervall, Wartezeit und Antragsfrist innerhalb der Grenzen, die der Administrator festgelegt hat. Eine zweite E-Mail-Adresse und ein ntfy-Thema für Push-Nachrichten sind möglich.

            > Hängt der Mailversand, laufen keine Fristen weiter. Ein Mailausfall führt nie zu einer Freigabe.

            **Wieder da?**

            Wurde freigegeben, obwohl es dir gut geht, meldest du dich an und wählst **Freigabe zurücknehmen**. Dein Browser erzeugt dann neue Schlüssel für alles Betroffene.
            """,
            ["lebenszeichen", "fristen", "antrag-widersprechen", "freigabe-zuruecknehmen"]),

        new("ernstfall", "Als Vertrauensperson im Ernstfall", Icons.Material.Outlined.MenuBook,
            """
            Hat dich jemand als Vertrauensperson benannt, findest du im Ernstfall alles im **Lesemodus**: ruhig, Schritt für Schritt, mit großer Schrift auf Wunsch und entlang der Checkliste.
            """,
            """
            **Den Zugang beantragen**

            1. **Notfallzugang** öffnen. Unter **Für andere** stehen alle, die dich als Vertrauensperson benannt haben.
            2. Bei der Person **Zugang beantragen** wählen und den Fall angeben: verstorben oder handlungsunfähig.
            3. Die Person und ihre anderen Vertrauenspersonen bekommen sofort eine Mail. Widerspricht niemand, wird der Zugang nach der Antragsfrist freigegeben, und du bekommst eine Mail.

            **Lesen**

            1. **Lesemodus** öffnen und die Person wählen.
            2. Mit **Weiter** und **Zurück** gehst du Schritt für Schritt durch die Checkliste und die freigegebenen Bereiche. **Schrift größer** hilft beim Lesen.
            3. Erledigte Aufgaben hakst du ab; die anderen Vertrauenspersonen sehen das.
            4. **Briefe an dich** stehen am Ende.

            **Wenn alles erledigt ist**

            Alle Vertrauenspersonen für den Fall „verstorben“ können den Tresor **gemeinsam endgültig löschen**. Gelöscht wird sieben Tage nach der letzten Zustimmung; bis dahin kann jede Person ihre Zustimmung zurückziehen.

            > Was jemand schon heute mit dir teilt, steht unter **Mit mir geteilt**, auch ohne Ernstfall.
            """,
            ["zugang-beantragen", "lesemodus", "gemeinsam-loeschen", "mit-mir-geteilt"]),

        new("papier", "Auf Papier und offline", Icons.Material.Outlined.IosShare,
            """
            myLife hält deine Daten nicht gefangen. Unter **Export & Druck** erstellst du einen **Notfallordner** zum Ausdrucken, eine **Datensicherung** im offenen Format und ein **Notfallpaket**, das auch ohne Server und ohne Internet funktioniert.
            """,
            """
            **Was es gibt**

            | Export | Wofür | Schutz |
            | --- | --- | --- |
            | Notfallordner (PDF) | Alle oder ausgewählte Bereiche zum Ausdrucken oder Aufbewahren | PDF-Passwort (AES-256) |
            | Druckansicht | Papierordner, Bankschließfach | keiner, Klartext |
            | Datensicherung (JSON) | Alle Einträge und Dokumente, offenes Format | dein Master-Passwort oder Wiederherstellungs-Kit |
            | Notfallpaket | Eine HTML-Datei für die Familie, etwa auf einem USB-Stick | gedrucktes Schlüsselblatt mit 24 Wörtern |

            **Das Notfallpaket**

            1. Unter **Export & Druck** **Notfallpaket erstellen** wählen.
            2. Bereiche auswählen, auf Wunsch mit Dokumenten, und **Paket erzeugen und speichern**.
            3. Sofort das **Schlüsselblatt drucken**: Die 24 Wörter siehst du nur dieses eine Mal.
            4. Die Datei der Familie geben, das Blatt getrennt davon aufbewahren, etwa im Schließfach oder beim Notar.

            > Jeder Ausdruck und jeder Export ohne Passwort enthält deine Daten im Klartext. Bewahre ihn so sicher auf wie die Originale.
            """,
            ["notfallordner", "datensicherung", "notfallpaket", "mylife-decrypt"]),

        new(AdministrationChapter, "Für Administratoren", Icons.Material.Outlined.AdminPanelSettings,
            """
            Administratoren kümmern sich um die Instanz: Mailversand, Fristen, Sicherung und Benutzer. **In fremde Tresore sehen sie trotzdem nicht hinein**, und sie können keine Freigabe auslösen oder Fristen eines Nutzers abkürzen.
            """,
            """
            **Nach der Einrichtung**

            1. **Verwaltung → Einstellungen → Allgemein**: die **Adresse der Instanz** eintragen, etwa `https://mylife.example.org`. Sie steht in den Links der Mails.
            2. Reiter **E-Mail**: SMTP-Server eintragen und **Speichern und Testmail senden**. Ohne Mailversand laufen keine Fristen des Notfallzugangs, und es kommen keine Erinnerungen.
            3. Reiter **Sicherung**: prüfen, dass die tägliche Sicherung eingerichtet ist.
            4. Einen **zweiten Administrator** ernennen. Den Tresor des einzigen Administrators können die Vertrauenspersonen nicht gemeinsam löschen.

            **Regelmäßig, etwa einmal im Monat**

            - Die letzte Sicherung ist von heute oder gestern, ohne Fehler.
            - Die Warteschlange der Mails ist leer.
            - Updates einspielen, vorher **Jetzt sichern**.
            """,
            ["mailversand", "fristen-grenzen", "sicherung", "benutzer", "update"]),
    ];

    private static GuidePart[] GermanGuide() =>
    [
        new("einrichten", "Einrichten",
            "Vom ersten Aufruf bis zum eigenen Konto.",
        [
            new("einrichtungsassistent", "Den Einrichtungsassistenten durchlaufen", Icons.Material.Outlined.Key,
                "Die erste Person einer neuen Instanz",
                "Solange es noch kein Konto gibt, startet beim Aufruf von myLife der **Einrichtungsassistent**. Das erste Konto wird Administrator und bekommt einen eigenen Tresor. Alle Schlüssel entstehen im Browser.",
                [
                    "myLife im Browser öffnen. Der Assistent beginnt mit **Willkommen**.",
                    "Den **Einrichtungscode** eingeben. Er steht im Protokoll des Containers, etwa im Container Manager unter `mylife-app` → **Protokoll**, in der Zeile „Einrichtungscode für den Assistenten“.",
                    "**Dein Konto**: Anzeigename und E-Mail-Adresse. Die Adresse dient der Anmeldung, den Erinnerungen und dem Notfallzugang.",
                    "**Was nicht hineingehört** lesen und bestätigen.",
                    "**Master-Passwort** wählen, wiederholen und bestätigen, dass niemand es zurücksetzen kann. Gut merkbar und sicher ist ein Satz aus mehreren Wörtern.",
                    "**Wiederherstellungs-Kit**: **Kit drucken** oder die 24 Wörter abschreiben, dann zur Kontrolle die verlangten Wörter eingeben.",
                    "**Zweiter Faktor**: den QR-Code mit der Authenticator-App scannen und den 6-stelligen Code eingeben.",
                    "**Tresor anlegen**. Danach meldest du dich mit E-Mail-Adresse, Master-Passwort und Code an.",
                ],
                [
                    "Nach jedem Neustart des Containers gilt ein neuer Einrichtungscode. Sobald das erste Konto angelegt ist, gibt es keinen Code mehr, und der Assistent ist gesperrt.",
                    "Einen Passkey richtest du nach der ersten Anmeldung unter **Konto → Sicherheit** zusätzlich ein.",
                ],
                "setup", "Einrichtung"),

            new("einladung-annehmen", "Eine Einladung annehmen", Icons.Material.Outlined.MarkEmailRead,
                "Eingeladene Personen",
                "Wer eingeladen wird, bekommt einen Link per Mail oder auf anderem Weg, etwa per Messenger. Damit richtest du ein eigenes Konto mit eigenem, verschlüsseltem Tresor ein.",
                [
                    "Den Einladungslink öffnen. Die Seite nennt, wer dich einlädt.",
                    "Ein **Master-Passwort** wählen und bestätigen, dass niemand es zurücksetzen kann.",
                    "Das **Wiederherstellungs-Kit** drucken oder abschreiben und die verlangten Wörter zur Kontrolle eingeben.",
                    "Die **Authenticator-App** einrichten: QR-Code scannen, Code eingeben.",
                    "Fertig. Dein Browser hat deine Schlüssel erzeugt; der Browser der einladenden Person prüft sie bei ihrer nächsten Anmeldung automatisch.",
                ],
                [
                    "Der Link gilt nur begrenzt und nur einmal. Ist er abgelaufen, bitte die einladende Person um einen neuen.",
                    "Von fremden Tresoren siehst du nichts, solange dir niemand etwas freigibt.",
                ]),
        ]),

        new("anmelden", "Anmelden und Sicherheit",
            "Anmeldung, Entsperren, Passwort und zweiter Faktor.",
        [
            new("anmelden", "Anmelden und abmelden", Icons.Material.Outlined.Login,
                "Alle",
                "Die Anmeldung hat zwei Schritte: Master-Passwort, dann der zweite Faktor. Das Passwort verlässt deinen Browser nie; er leitet daraus den Schlüssel zu deinem Tresor ab.",
                [
                    "E-Mail-Adresse und **Master-Passwort** eingeben und **Anmelden** wählen.",
                    "Den 6-stelligen Code aus der Authenticator-App eingeben und **Bestätigen**, oder **Mit Passkey anmelden**.",
                    "Zum Abmelden oben rechts auf dein Bild oder das Kontosymbol klicken und **Abmelden** wählen, oder in der Seitenleiste **Konto → Abmelden**.",
                ],
                [
                    "Nach fünf Fehlversuchen ist das Konto für 15 Minuten gesperrt.",
                    "Nach längerer Inaktivität meldet myLife dich ab und entfernt alle Schlüssel aus dem Speicher. Die Zeit legt der Administrator fest.",
                    "Jede Anmeldung zählt als Lebenszeichen für deinen Notfallzugang.",
                ],
                "login", "Anmelden"),

            new("entsperren", "Den Tresor entsperren", Icons.Material.Outlined.LockOpen,
                "Alle",
                "Lädst du die Seite neu, bleibst du angemeldet, aber die Schlüssel sind aus dem Speicher verschwunden. Der Tresor ist dann **gesperrt**.",
                [
                    "Die Seite **Tresor entsperren** erscheint von selbst.",
                    "Das **Master-Passwort** eingeben und **Entsperren** wählen. Einen zweiten Faktor braucht es dafür nicht.",
                ],
                [
                    "Schlüssel liegen nur im Arbeitsspeicher des Browsers, nie im Local Storage.",
                ]),

            new("passwort-vergessen", "Passwort vergessen", Icons.Material.Outlined.Restore,
                "Alle mit Wiederherstellungs-Kit",
                "Mit den 24 Wörtern aus dem Wiederherstellungs-Kit vergibst du ein neues Master-Passwort. Dein Tresor bleibt dabei unverändert.",
                [
                    "Auf der Anmeldeseite **Passwort vergessen?** wählen.",
                    "E-Mail-Adresse und die **24 Wörter** eingeben, durch Leerzeichen oder Zeilen getrennt. Die ersten vier Buchstaben jedes Worts genügen.",
                    "Ein **neues Master-Passwort** wählen und **Neues Passwort setzen**.",
                    "Mit dem neuen Passwort und deinem zweiten Faktor anmelden.",
                ],
                [
                    "Ohne Master-Passwort und ohne Kit kommt niemand mehr an den Tresor, auch der Administrator nicht.",
                    "Der zweite Faktor bleibt. Ist auch er verloren, kann der Administrator ihn zurücksetzen.",
                ],
                "recover", "Zugang wiederherstellen"),

            new("passwort-aendern", "Master-Passwort ändern", Icons.Material.Outlined.Password,
                "Alle",
                "Beim Ändern wird nur der Schlüssel deines Tresors neu verpackt; an den Daten ändert sich nichts.",
                [
                    "**Konto → Sicherheit** öffnen.",
                    "Unter **Master-Passwort ändern** das aktuelle und zweimal das neue Passwort eingeben.",
                    "**Passwort ändern** wählen. Andere angemeldete Geräte werden abgemeldet.",
                ],
                [
                    "Das Wiederherstellungs-Kit bleibt gültig.",
                    "Eine Datensicherung (JSON) öffnet sich weiterhin mit dem Passwort, das beim Export galt.",
                ],
                "account/security", "Sicherheit"),

            new("passkeys", "Passkeys einrichten", Icons.Material.Outlined.Fingerprint,
                "Alle",
                "Ein Passkey ersetzt bei der Anmeldung den Code aus der Authenticator-App, etwa mit Fingerabdruck, Gesicht oder einem Sicherheitsschlüssel. Das Master-Passwort brauchst du weiterhin.",
                [
                    "**Konto → Sicherheit** öffnen.",
                    "Unter **Passkeys** eine Bezeichnung eingeben, etwa „Laptop“, und **Passkey hinzufügen** wählen.",
                    "Den Dialog des Browsers oder Betriebssystems bestätigen.",
                    "Zum Entfernen beim Passkey auf Löschen klicken.",
                ],
                [
                    "Die Authenticator-App bleibt als Ausweichweg eingerichtet.",
                ],
                "account/security", "Sicherheit"),
        ]),

        new("tresor", "Den Tresor füllen",
            "Einträge, Dokumente, Wünsche und was nicht hineingehört.",
        [
            new("eintrag-anlegen", "Einen Eintrag anlegen", Icons.Material.Outlined.NoteAdd,
                "Alle",
                "Jeder Eintrag gehört zu einem **Bereich** und hat einen **Typ**, etwa Bankkonto, Versicherung oder Vertrag. Der Typ bestimmt die Felder des Datenblatts.",
                [
                    "In der Seitenleiste unter **Bereiche** den Bereich öffnen.",
                    "Den großen Knopf **… anlegen** wählen, oder über den Pfeil daneben einen anderen Typ des Bereichs.",
                    "Die Felder ausfüllen. Pflichtfelder sind markiert; IBAN, BIC, E-Mail-Adressen und Datumsangaben werden geprüft.",
                    "Unter **Für alle Einträge** bei Bedarf Titel, Notizen, Ablageort des Originals, Ablaufdatum und Wiedervorlage ergänzen.",
                    "**Speichern**. Der Eintrag wird in deinem Browser verschlüsselt.",
                ],
                [
                    "Fehlen in einem Bereich typische Einträge, zeigt er sie als Knöpfe an. Ein Klick legt den passenden Eintrag an.",
                    "Manche Felder verweisen auf einen anderen Eintrag, etwa der Kredit auf die Restschuldversicherung. Im Datenblatt führt ein Klick dorthin.",
                ]),

            new("bearbeiten-beenden", "Bearbeiten, beenden und löschen", Icons.Material.Outlined.EditNote,
                "Alle",
                "Ein Eintrag, den es nicht mehr gibt, etwa ein gekündigter Vertrag, lässt sich **beenden** statt löschen. Er bleibt dann für die Geschichte erhalten, erscheint aber nicht mehr in der Liste.",
                [
                    "Den Eintrag öffnen und **Bearbeiten** wählen, ändern, **Speichern**.",
                    "Zum Beenden **Weitere Aktionen** (drei Punkte) → **Beenden**. **Reaktivieren** holt ihn zurück.",
                    "Zum Löschen **Weitere Aktionen** → **Löschen**. Der Eintrag kommt mit seinen Dokumenten in den **Papierkorb**.",
                    "In der Liste eines Bereichs kannst du mit **Auswählen** mehrere Einträge auf einmal beenden oder löschen.",
                ],
                [
                    "Beendete Einträge zeigt die Liste mit **Beendete anzeigen**.",
                    "Hat jemand den Eintrag inzwischen an anderer Stelle geändert, meldet myLife das beim Speichern; lade dann neu und übernimm deine Änderung erneut.",
                ]),

            new("papierkorb", "Den Papierkorb nutzen", Icons.Material.Outlined.Delete,
                "Alle",
                "Gelöschte Einträge und Dokumente bleiben 30 Tage im Papierkorb und werden dann endgültig entfernt.",
                [
                    "**Verwaltung → Papierkorb** öffnen.",
                    "Beim Eintrag **Wiederherstellen** wählen, oder **Endgültig löschen**, wenn er sofort verschwinden soll.",
                ],
                [],
                "trash", "Papierkorb"),

            new("dokumente", "Dokumente hinzufügen", Icons.Material.Outlined.AttachFile,
                "Alle",
                "An jeden Eintrag kannst du Dateien hängen: PDFs, Fotos, Scans. Sie werden in deinem Browser in Blöcken verschlüsselt, bevor sie den Rechner verlassen.",
                [
                    "Den Eintrag öffnen und zu **Dokumente** gehen.",
                    "**Datei hinzufügen** wählen, oder auf dem Smartphone **Foto aufnehmen**.",
                    "Bezeichnung und Datum ergänzen und **Verschlüsseln und hochladen**.",
                    "Zum Ansehen **Vorschau** (PDF und Bilder), zum Herunterladen **Speichern**.",
                ],
                [
                    "Eine Datei darf bis zu 25 MB groß sein.",
                    "Zeigt dein Browser keine Vorschau, speichere die Datei stattdessen.",
                    "Die Suche durchsucht den Inhalt von PDFs nicht. Wichtige Angaben gehören deshalb auch ins Datenblatt.",
                ]),

            new("wuensche-verweise", "Wünsche, Verknüpfungen und Zusatzfelder", Icons.Material.Outlined.Lightbulb,
                "Alle",
                "Neben den Feldern hat jeder Eintrag einen **Wunsch für die Hinterbliebenen**, etwa „kündigen“, „übernehmen“, „auflösen“ oder „in den Gedenkzustand versetzen“. Die Checkliste nutzt diese Wünsche, um die passenden Einträge zu einer Aufgabe zu zeigen.",
                [
                    "Den Eintrag bearbeiten und unter **Wunsch für die Hinterbliebenen** eine Möglichkeit wählen.",
                    "Unter **Hinweis zum Wunsch** ergänzen, was dazu wichtig ist, etwa an wen etwas übergeben werden soll.",
                    "Unter **Verknüpft mit** andere Einträge auswählen, die dazugehören. Die Verknüpfung ist im Datenblatt in beide Richtungen sichtbar.",
                    "Passt ein Feld nicht ins Schema, **Zusatzfeld hinzufügen** und Bezeichnung und Wert eintragen.",
                ],
                []),

            new("eigenes-datenblatt", "Das eigene Datenblatt", Icons.Material.Outlined.Badge,
                "Alle",
                "Name, Vorname und ein Bild von dir liegen verschlüsselt im Bereich „Über mich“. Wer diesen Bereich freigegeben bekommt, sieht sie auch.",
                [
                    "**Konto → Details** öffnen.",
                    "Name und Vorname eintragen und **Speichern**.",
                    "**Bild wählen**: JPEG, PNG oder WebP bis 5 MB. Ein neues Bild legt das alte in den Papierkorb.",
                ],
                [],
                "account/profile", "Details"),

            new("gehoert-nicht-hinein", "Was nicht hineingehört", Icons.Material.Outlined.Block,
                "Alle",
                "myLife sagt deiner Familie, **was es gibt und wo es liegt**. Zugangsdaten, mit denen jemand sofort an Geld kommt, gehören nicht hinein.",
                [
                    "Keine **PINs**, **TANs** und **vollständigen Kartennummern**; bei Karten nur die letzten vier Ziffern.",
                    "Keine **einzelnen Passwörter**. Nenne stattdessen im Bereich „Passwortmanager“, welchen du nutzt und wo sein Notfallzugang liegt.",
                    "Keine **Seed-Phrasen**, nur den Ort, an dem sie liegen.",
                    "Keine **Originale** von Urkunden, nur ihren Ablageort.",
                ],
                [
                    "Beim Speichern prüft dein Browser die Freitexte und warnt dich, etwa bei einer Kartennummer mit gültiger Prüfziffer oder einer Folge von 12 bis 24 Wörtern. Gespeichert wird nur, wenn du **Trotzdem speichern** wählst.",
                ],
                "hints/not-in-vault", "Was gehört nicht in myLife?"),
        ]),

        new("ueberblick", "Den Überblick behalten",
            "Übersicht, Vollständigkeit, Erinnerungen, Suche, Zahlungen und Protokoll.",
        [
            new("uebersicht", "Übersicht und Vollständigkeit", Icons.Material.Outlined.Dashboard,
                "Alle",
                "Die **Übersicht** ist der Einstieg. Sie zeigt je Bereich eine Kachel mit der Anzahl der Einträge, wie vollständig der Bereich ist und wann du ihn zuletzt geprüft hast.",
                [
                    "**Übersicht** öffnen.",
                    "Steht bei einer Kachel **Prüfung fällig**, den Bereich öffnen und durchsehen.",
                    "Fehlende Pflicht- und empfohlene Felder sind in der Liste markiert; typische fehlende Einträge stehen als Knöpfe darüber.",
                    "Ist alles aktuell, auf der Kachel **Als geprüft markieren**. Einzelne Einträge markierst du in ihrem Datenblatt.",
                ],
                [
                    "Oben auf der Übersicht steht, was bald abläuft, und welche Wiedervorlagen anstehen.",
                ],
                "./", "Übersicht"),

            new("erinnerungen", "Erinnerungen einstellen", Icons.Material.Outlined.NotificationsActive,
                "Alle",
                "myLife erinnert dich per Mail, wenn Bereiche wieder geprüft werden sollten, bevor etwas abläuft und wenn eine Wiedervorlage ansteht.",
                [
                    "**Konto → Erinnerungen** öffnen.",
                    "**Bereiche prüfen alle** … Monate festlegen. 0 schaltet diese Erinnerung aus; Standard sind 6 Monate.",
                    "**Vor einem Ablaufdatum erinnern** … Tage festlegen, zwischen 7 und 365; Standard sind 60 Tage.",
                    "**Erinnerungen per Mail** ein- oder ausschalten und **Speichern**.",
                ],
                [
                    "Ein Ablaufdatum hat jeder Eintrag unter **Für alle Einträge**; bei Ausweisen und Karten heißt es „Gültig bis“.",
                    "Der Server kennt von deinen Einträgen nur den nächsten Termin, nicht wozu er gehört. Die Mail nennt deshalb keine Einzelheiten.",
                ],
                "account/reminders", "Erinnerungen"),

            new("wiedervorlage", "Wiedervorlage", Icons.Material.Outlined.EventRepeat,
                "Alle",
                "Mit einer Wiedervorlage erinnerst du dich an einen einzelnen Eintrag, etwa „Beitrag prüfen“ oder „Vertrag vergleichen“.",
                [
                    "Den Eintrag bearbeiten und unter **Für alle Einträge** **Wiedervorlage am** und einen Hinweis eintragen.",
                    "Ab 14 Tagen vorher steht die Wiedervorlage auf der Übersicht; zum Termin kommt eine Mail.",
                    "Ist sie erledigt, im Datenblatt **Wiedervorlage erledigt** wählen.",
                ],
                []),

            new("suche", "Suchen", Icons.Material.Outlined.Search,
                "Alle",
                "Die **Suche** findet Einträge über alle Felder, Notizen und Dokumentbezeichnungen.",
                [
                    "**Suche** öffnen.",
                    "Einen Suchbegriff eingeben, etwa eine Bank, eine Policennummer oder einen Namen.",
                    "Einen Treffer anklicken, um den Eintrag zu öffnen.",
                ],
                [
                    "Die Suche läuft nur in deinem Browser über die entschlüsselten Einträge. Den Inhalt von PDFs durchsucht sie nicht.",
                ],
                "search", "Suche"),

            new("zahlungen", "Zahlungen im Blick", Icons.Material.Outlined.Payments,
                "Alle",
                "Die Seite **Zahlungen** sammelt aus allen Bereichen, was regelmäßig abgeht: Kreditraten, Versicherungsbeiträge, Verträge und Abos sowie Jahresgebühren von Karten.",
                [
                    "**Zahlungen** öffnen.",
                    "Oben stehen die Summen **je Monat**, **je Jahr** und für **einmalige** Zahlungen, darunter dieselben Summen **nach Bereich**.",
                    "In **Alle Zahlungen** stehen die einzelnen Zahlungen mit Betrag, Rhythmus und Konto. **Nach Bereich gruppieren** zeigt sie je Bereich mit Zwischensumme; ausgeschaltet ist es eine Liste zum Sortieren.",
                    "Eine Zahlung anklicken, um den Eintrag zu öffnen.",
                ],
                [
                    "Es zählen nur laufende Einträge mit Betrag. Beendete Einträge und Kredite, die dir geschuldet werden, fehlen.",
                    "Ohne **Rhythmus** zählt eine Zahlung nicht in den Summen; die Seite weist darauf hin.",
                ],
                "payments", "Zahlungen"),

            new("protokoll", "Das Protokoll ansehen", Icons.Material.Outlined.History,
                "Alle",
                "Das **Protokoll** zeigt, wer wann was in deinem Tresor getan hat: Anmeldungen, Änderungen, Exporte, Freigaben und jeden Abruf durch Vertrauenspersonen.",
                [
                    "**Verwaltung → Protokoll** öffnen.",
                    "Die Einträge sind nach Zeit sortiert; Titel ergänzt dein Browser.",
                ],
                [
                    "Das Protokoll enthält keine Inhalte und nur gekürzte IP-Adressen.",
                ],
                "account/audit", "Protokoll"),
        ]),

        new("familie", "Familie und Vertrauenspersonen",
            "Einladen, Schlüssel prüfen, Freigaben, Briefe und die Checkliste.",
        [
            new("einladen", "Jemanden einladen", Icons.Material.Outlined.PersonAdd,
                "Alle",
                "Jede Person bekommt ein eigenes Konto mit eigenem Tresor. Später kannst du sie als Vertrauensperson einsetzen. Eine öffentliche Registrierung gibt es nicht.",
                [
                    "**Familie** öffnen und **Person einladen** wählen.",
                    "E-Mail-Adresse und **Sprache der Einladung** angeben.",
                    "**Per Mail von myLife senden** oder **Link selbst weitergeben**. Beim Link: **Link kopieren** und auf eigenem Weg verschicken.",
                    "**Einladen**. Unter **Deine Einladungen** siehst du den Stand.",
                ],
                [
                    "Am sichersten ist der Link auf eigenem Weg: Das Geheimnis im Link entsteht nur in deinem Browser, der Server sieht ihn nie.",
                    "Ist ein Link abgelaufen, bei der Einladung **Neuen Link per Mail senden** oder **Neuen Link zum Weitergeben** wählen. **Einladung zurückziehen** macht den Link ungültig.",
                    "Ohne eingerichteten Mailversand geht nur der Link zum Weitergeben.",
                ],
                "family", "Familie"),

            new("schluessel", "Schlüssel prüfen", Icons.Material.Outlined.VerifiedUser,
                "Alle",
                "Damit niemand einen falschen Schlüssel unterschieben kann, prüft myLife die Schlüssel aller Personen automatisch. Nur an bestätigte Schlüssel gibt myLife Freigaben heraus.",
                [
                    "**Familie** öffnen. In der Spalte **Schlüssel** steht je Person der Stand.",
                    "**Von dir bestätigt**: Dein Browser hat den Schlüssel bei der Einladung selbst geprüft und sich gemerkt.",
                    "**Bestätigt durch …**: Jemand, dem du vertraust, hat ihn gegengezeichnet.",
                    "**Noch nicht bestätigt**: Das ist kein Fehler. Beim Einsetzen als Vertrauensperson merkt sich dein Browser den Schlüssel; ein späterer Austausch fällt dann auf.",
                ],
                [
                    "**Schlüssel geändert!** bedeutet: Der Server zeigt einen anderen Schlüssel als den, den dein Browser sich gemerkt hat. myLife erzeugt dafür keine Freigaben. Sprich mit der Person auf anderem Weg und wende dich an den Administrator.",
                    "Deinen eigenen Fingerprint siehst du oben auf der Seite.",
                ],
                "family", "Familie"),

            new("vertrauenspersonen", "Vertrauenspersonen einsetzen und Bereiche freigeben", Icons.Material.Outlined.VolunteerActivism,
                "Alle",
                "Eine Vertrauensperson sieht im Ernstfall genau die Bereiche, die du ihr für diesen Fall freigegeben hast. **Verstorben** und **handlungsunfähig** sind getrennte Fälle.",
                [
                    "**Vertrauenspersonen** öffnen und **Vertrauensperson hinzufügen** wählen, oder eine vorhandene anklicken.",
                    "**Beziehung** und bei Bedarf eine **Notiz** eintragen.",
                    "**Darf einen Zugang beantragen** einschalten, wenn die Person im Ernstfall selbst den Zugang beantragen können soll.",
                    "Unter **Bereiche** je Bereich die Fälle ankreuzen: **Verstorben**, **Handlungsunfähig**, **Sofort teilen**. **alle** kreuzt eine ganze Spalte an.",
                    "**Speichern und verschlüsseln**.",
                ],
                [
                    "Zum Entfernen **Als Vertrauensperson entfernen**. Alle Freigaben für diese Person werden gelöscht.",
                    "Steht **Neu speichern nötig**, hat die Person einen neuen Schlüssel. Öffne sie und speichere einmal neu.",
                    "Der Notfallzugang wird aktiv, sobald du die erste Vertrauensperson eingesetzt hast.",
                ],
                "trust", "Vertrauenspersonen"),

            new("briefe", "Persönliche Briefe", Icons.Material.Outlined.Mail,
                "Alle",
                "Ein Brief ist ein Eintrag im Bereich „Persönliche Wünsche“. Jeder Brief hat einen eigenen Schlüssel und geht nur an die Personen, die du dafür auswählst.",
                [
                    "Unter **Persönliche Wünsche** einen **Brief** anlegen, mit Empfänger und Text.",
                    "**Vertrauenspersonen** öffnen und die Person anklicken.",
                    "Unter **Persönliche Briefe** den Brief für den passenden Fall ankreuzen und **Speichern und verschlüsseln**.",
                ],
                [
                    "Das Feld „Empfänger“ im Brief ist nur ein Hinweis. Wer ihn lesen darf, legst du ausschließlich bei den Vertrauenspersonen fest.",
                    "Im Lesemodus stehen Briefe unter **Briefe an dich**.",
                ]),

            new("sofort-teilen", "Sofort teilen", Icons.Material.Outlined.FolderShared,
                "Alle",
                "Manches soll die Familie schon heute wissen, etwa wichtige Kontakte. Mit **Sofort teilen** kann die Person einen Bereich ab sofort lesen, aber nichts ändern.",
                [
                    "**Vertrauenspersonen** öffnen und die Person anklicken.",
                    "Beim Bereich **Sofort teilen** ankreuzen und **Speichern und verschlüsseln**.",
                    "Die Person sieht den Bereich unter **Mit mir geteilt**.",
                ],
                [
                    "Beendest du das Teilen, erzeugt dein Browser einen neuen Schlüssel für den Bereich und verschlüsselt ihn neu. Das kann einen Moment dauern.",
                ],
                "trust", "Vertrauenspersonen"),

            new("checkliste", "Die Checkliste pflegen", Icons.Material.Outlined.Checklist,
                "Alle",
                "Die Checkliste sagt deinen Vertrauenspersonen, was wann zu tun ist. Jede Aufgabe ist ein Eintrag mit Zeitpunkt, Zuständigkeit und verknüpften Einträgen.",
                [
                    "Unter **Bereiche** die **Checkliste** öffnen.",
                    "Beim ersten Mal **Vorlage übernehmen** (Vorlage für Deutschland) oder **Ohne Vorlage beginnen**.",
                    "Eine Aufgabe öffnen und bearbeiten: **Wann** (erste 24 Stunden, erste Woche, erster Monat, später), Text, **Zuständig**.",
                    "Unter **Zugehörige Einträge aus dem Bereich** einen Bereich wählen und bei Bedarf **Nur Einträge mit dem Wunsch**, etwa „kündigen“.",
                    "Mit **Neue Aufgabe** eigene Aufgaben ergänzen.",
                ],
                [
                    "Gib die Checkliste unter **Vertrauenspersonen** frei wie jeden anderen Bereich. Verknüpfte Einträge sehen die Personen nur aus Bereichen, die sie auch sehen dürfen.",
                    "Die Vorlage ist keine Rechtsberatung; die Fristen sind Richtwerte.",
                ],
                "checklist", "Checkliste"),
        ]),

        new("notfall", "Notfallzugang",
            "Lebenszeichen, Fristen, Anträge, Widerspruch und Rücknahme.",
        [
            new("lebenszeichen", "Ein Lebenszeichen geben", Icons.Material.Outlined.Favorite,
                "Alle mit Vertrauenspersonen",
                "Solange du dich regelmäßig meldest, passiert nichts. **Jede Anmeldung zählt als Lebenszeichen.**",
                [
                    "Dich einfach anmelden, oder",
                    "in der Erinnerungsmail auf den Link klicken, oder",
                    "unter **Notfallzugang** **Lebenszeichen geben** wählen.",
                ],
                [
                    "Unter **Dein Notfallzugang** stehen das letzte Lebenszeichen, wann das nächste fällig ist und wann frühestens ohne Reaktion freigegeben würde.",
                    "Der Link in der Mail setzt ausschließlich das Lebenszeichen und gilt nur einmal.",
                ],
                "emergency", "Notfallzugang"),

            new("fristen", "Die eigenen Fristen wählen", Icons.Material.Outlined.Timer,
                "Alle mit Vertrauenspersonen",
                "Du bestimmst, wie lange myLife wartet, bevor es freigibt, innerhalb der Grenzen, die der Administrator festgelegt hat.",
                [
                    "**Notfallzugang** öffnen und zu **Deine Fristen** gehen.",
                    "**Lebenszeichen**: wie oft du dich melden musst (Standard: 30 Tage).",
                    "**Wartezeit**: wie lange nach der letzten Erinnerung ohne Reaktion gewartet wird (Standard: 14 Tage).",
                    "**Frist bei einem Antrag**: wie lange du einem Antrag widersprechen kannst (Standard: 7 Tage).",
                    "Optional eine **zweite E-Mail-Adresse** und ein **ntfy-Thema** für Push-Nachrichten aufs Smartphone eintragen und **Speichern**.",
                ],
                [
                    "Die Erinnerungen kommen in Stufen nach Fälligkeit, üblicherweise nach 3, 7 und 14 Tagen; die Stufen legt der Administrator fest.",
                    "Hängt der Mailversand, laufen keine Fristen weiter. Ein Mailausfall führt nie zu einer Freigabe.",
                ],
                "emergency", "Notfallzugang"),

            new("antrag-widersprechen", "Einem Antrag widersprechen", Icons.Material.Outlined.FrontHand,
                "Alle, für die jemand einen Zugang beantragt hat",
                "Beantragt eine Vertrauensperson den Zugang, bekommst du sofort eine Mail mit dem Link **Widersprechen**. Alle anderen Vertrauenspersonen werden ebenfalls informiert.",
                [
                    "In der Mail auf **Widersprechen** klicken. Der Antrag ist damit **angehalten**; es wird vorerst nichts freigegeben.",
                    "Dich anmelden und **Notfallzugang** öffnen.",
                    "**Endgültig widersprechen**, wenn es dir gut geht, oder **Weiterlaufen lassen**, wenn der Zugang in Ordnung ist.",
                ],
                [
                    "Solange ein Antrag läuft, zeigt die **Übersicht** oben einen deutlichen Hinweis.",
                    "Ohne Widerspruch wird nach der Antragsfrist nur der beantragte Fall freigegeben.",
                ],
                "emergency", "Notfallzugang"),

            new("freigabe-zuruecknehmen", "Eine Freigabe zurücknehmen", Icons.Material.Outlined.Undo,
                "Alle, deren Notfallzugang freigegeben ist",
                "Wurde dein Notfallzugang freigegeben, obwohl es dir gut geht, etwa nach einer Krankheit, nimmst du die Freigabe zurück.",
                [
                    "Anmelden und **Notfallzugang** öffnen.",
                    "**Freigabe zurücknehmen** wählen und bestätigen.",
                    "Die Seite offen lassen, bis die Meldung erscheint, dass alle Bereiche und Briefe neue Schlüssel haben.",
                ],
                [
                    "Deine Vertrauenspersonen können danach nichts mehr lesen, was nur für den Notfall bestimmt ist. Sofort Geteiltes bleibt.",
                    "Bricht der Schlüsselwechsel ab, setzt du ihn unter **Notfallzugang** mit **Schlüsselwechsel fortsetzen** fort.",
                    "Ein geplantes gemeinsames Löschen entfällt mit der Rücknahme.",
                ],
                "emergency", "Notfallzugang"),
        ]),

        new("vertrauensperson", "Als Vertrauensperson",
            "Geteiltes lesen, den Zugang beantragen, den Lesemodus nutzen.",
        [
            new("mit-mir-geteilt", "Mit mir geteilt", Icons.Material.Outlined.FolderShared,
                "Vertrauenspersonen",
                "Was andere schon heute mit dir teilen, steht unter **Mit mir geteilt**, nur zum Lesen. Entschlüsselt wird in deinem Browser mit deinem eigenen Schlüssel.",
                [
                    "**Mit mir geteilt** öffnen.",
                    "Die Person und den Bereich aufklappen, einen Eintrag öffnen.",
                ],
                [
                    "Steht dort, dass der Schlüssel nicht bestätigt ist oder sich geändert hat, prüfe auf der Seite **Familie**, ob alles in Ordnung ist.",
                ],
                "shared", "Mit mir geteilt"),

            new("zugang-beantragen", "Den Zugang beantragen", Icons.Material.Outlined.HealthAndSafety,
                "Vertrauenspersonen mit Antragsrecht",
                "Beantrage einen Zugang nur im Ernstfall. Die Person und ihre anderen Vertrauenspersonen werden sofort informiert und können widersprechen.",
                [
                    "**Notfallzugang** öffnen und zu **Für andere** gehen.",
                    "Bei der Person **Zugang beantragen** wählen und den Fall angeben: **Verstorben** oder **Handlungsunfähig**.",
                    "**Beantragen** bestätigen.",
                    "Ohne Widerspruch wird der Zugang nach der Antragsfrist freigegeben. Du bekommst eine Mail und findest die Person im **Lesemodus**.",
                ],
                [
                    "Solange der Antrag läuft, kannst du ihn **zurückziehen**.",
                    "Fehlt der Knopf, hat dir die Person kein Antragsrecht gegeben. Der Zugang kommt dann nur, wenn ihr Lebenszeichen ausbleibt.",
                ],
                "emergency", "Notfallzugang"),

            new("lesemodus", "Im Lesemodus lesen", Icons.Material.Outlined.MenuBook,
                "Vertrauenspersonen nach einer Freigabe",
                "Der Lesemodus ist eine ruhige Oberfläche für den Ernstfall. Er führt dich entlang der Checkliste durch alles, was für dich freigegeben ist.",
                [
                    "**Lesemodus** öffnen und bei der Person **Öffnen** wählen.",
                    "Die Einleitung lesen und mit **Weiter** Schritt für Schritt vorgehen; **Zurück** geht einen Schritt zurück.",
                    "Bei Aufgaben der Checkliste die passenden Einträge ansehen und **Erledigt** abhaken. Alle Vertrauenspersonen sehen denselben Stand.",
                    "Dokumente ansehen oder speichern.",
                    "**Schrift größer** und **Schrift kleiner** ändern die Schriftgröße; **Lesemodus beenden** führt zurück.",
                ],
                [
                    "Du kannst jederzeit aufhören und später weitermachen.",
                    "Im Fall „handlungsunfähig“ kann die Person den Zugang später wieder entziehen.",
                ],
                "reading", "Lesemodus"),

            new("gemeinsam-loeschen", "Einen Tresor gemeinsam löschen", Icons.Material.Outlined.DeleteForever,
                "Vertrauenspersonen nach der Freigabe „verstorben“",
                "Wenn alles erledigt ist, könnt ihr den Tresor gemeinsam endgültig löschen. Alle Vertrauenspersonen für den Fall „verstorben“ müssen zustimmen.",
                [
                    "Vorher speichern, was du behalten möchtest, etwa Dokumente im Lesemodus.",
                    "**Notfallzugang** öffnen und bei der Person unter **Tresor endgültig löschen** **Dem Löschen zustimmen**.",
                    "Haben alle zugestimmt, wird sieben Tage später alles gelöscht: Daten, Dokumente, Briefe.",
                ],
                [
                    "Bis zum Löschen kann jede Person mit **Zustimmung zurückziehen** abbrechen.",
                    "Den Tresor des einzigen Administrators kann man so nicht löschen; vorher muss jemand anderes Administrator werden.",
                ],
                "emergency", "Notfallzugang"),
        ]),

        new("export", "Export und Papier",
            "Notfallordner, Datenblatt, Datensicherung, Notfallpaket und mylife-decrypt.",
        [
            new("notfallordner", "Den Notfallordner erstellen", Icons.Material.Outlined.PictureAsPdf,
                "Alle",
                "Der Notfallordner fasst alle oder ausgewählte Bereiche zusammen: Inhaltsverzeichnis, Checkliste, Datenblätter und eine Liste der Dokumente. Er entsteht vollständig in deinem Browser.",
                [
                    "**Export & Druck** öffnen.",
                    "Unter **Notfallordner** die **Bereiche** auswählen, bei Bedarf **Beendete Einträge aufnehmen**.",
                    "**PDF erstellen**, ein Passwort für das PDF vergeben (mindestens 8 Zeichen, ohne Umlaute, nicht dein Master-Passwort) und speichern.",
                    "Oder **Druckansicht** für den Papierordner und dort drucken.",
                ],
                [
                    "Das PDF ist mit AES-256 verschlüsselt. myLife speichert das Passwort nicht.",
                    "Ein PDF ohne Passwort und jeder Ausdruck enthält deine Daten im Klartext.",
                ],
                "export", "Export & Druck"),

            new("datenblatt", "Ein Datenblatt drucken", Icons.Material.Outlined.Print,
                "Alle",
                "Jeden einzelnen Eintrag kannst du als Datenblatt drucken oder als PDF speichern, etwa für die Bank.",
                [
                    "Den Eintrag öffnen.",
                    "**Weitere Aktionen** → **Drucken** oder **Datenblatt als PDF**.",
                ],
                []),

            new("datensicherung", "Datensicherung exportieren und einspielen", Icons.Material.Outlined.DataObject,
                "Alle",
                "Die Datensicherung ist ein offenes, verschlüsseltes JSON-Format mit allen Einträgen und Dokumenten. Damit sind deine Daten nie in myLife gefangen.",
                [
                    "**Export & Druck** öffnen und zu **Datensicherung (JSON)** gehen.",
                    "Bei Bedarf **Dokumente einschließen** und **Export herunterladen**.",
                    "Zum Einspielen unter **Datensicherung einspielen** **Datei auswählen**, das damalige Master-Passwort oder die 24 Wörter eingeben und **Einspielen**.",
                ],
                [
                    "Die Datei öffnet sich mit dem Master-Passwort zum Zeitpunkt des Exports oder mit deinem Wiederherstellungs-Kit, auch ohne Server.",
                    "Beim Einspielen wird übersprungen, was schon vorhanden ist.",
                ],
                "export", "Export & Druck"),

            new("notfallpaket", "Ein Notfallpaket erstellen", Icons.Material.Outlined.Inventory,
                "Alle",
                "Das Notfallpaket ist eine einzelne HTML-Datei für deine Familie, die ohne myLife-Server und ohne Internet funktioniert. Öffnen lässt sie sich nur mit einem gedruckten Schlüsselblatt.",
                [
                    "**Export & Druck** öffnen und **Notfallpaket erstellen** wählen.",
                    "Die Bereiche auswählen, bei Bedarf **Dokumente einschließen**.",
                    "**Paket erzeugen und speichern**.",
                    "Sofort **Schlüsselblatt drucken**. Die 24 Wörter siehst du nur hier; myLife speichert sie nirgends.",
                    "Die Datei der Familie geben, etwa auf einem USB-Stick, und das Blatt getrennt davon aufbewahren, etwa im Schließfach oder beim Notar.",
                ],
                [
                    "Zum Öffnen die Datei im Browser öffnen (Doppelklick), die 24 Wörter eingeben und **Öffnen** wählen.",
                    "Das Paket ist ein Schnappschuss. Erstelle nach wichtigen Änderungen ein neues und tausche Datei und Blatt gemeinsam aus; jedes Paket hat seinen eigenen Schlüssel.",
                ],
                "export/package", "Notfallpaket"),

            new("mylife-decrypt", "Exporte ohne Server öffnen", Icons.Material.Outlined.Terminal,
                "Angehörige, Nachfolgepersonen",
                "Das Werkzeug **mylife-decrypt** öffnet Datensicherungen und Notfallpakete auch dann, wenn es den Server nicht mehr gibt. Es braucht weder Internet noch .NET.",
                [
                    "Die passende Datei für Windows, macOS oder Linux beim GitHub-Release von myLife herunterladen; am besten liegt eine Kopie schon beim Notfallordner.",
                    "Das Werkzeug mit der Exportdatei aufrufen und das Master-Passwort, die 24 Wörter des Wiederherstellungs-Kits oder die 24 Wörter des Paketschlüssels eingeben.",
                    "Es schreibt die Einträge und Dokumente lesbar in einen Ordner.",
                ],
                [
                    "Die genaue Bedienung steht in der Betriebsdokumentation unter `docs/betrieb/mylife-decrypt.md`.",
                ]),
        ]),

        new(AdministrationPart, "Verwaltung",
            "Für Administratoren: Installation, Mailversand, Fristen, Sicherung, Benutzer und Updates.",
        [
            new("installation", "myLife installieren", Icons.Material.Outlined.Dns,
                "Betreiber der Instanz",
                "myLife läuft selbst gehostet mit Docker Compose: die App und eine PostgreSQL-Datenbank. Davor sorgt ein Reverse Proxy für HTTPS, etwa der Nginx Proxy Manager auf einer Synology.",
                [
                    "Einen Projektordner anlegen und `docker-compose.yml` und `.env.example` aus dem Repository hineinlegen; `.env.example` in `.env` umbenennen.",
                    "In der `.env` mindestens `POSTGRES_PASSWORD`, `MYLIFE_PORT` und `MYLIFE_BACKUP_KEY` setzen. Den Sicherungsschlüssel getrennt von der NAS aufbewahren.",
                    "Den Ordner `data/app` dem Benutzer mit der UID 1654 übergeben.",
                    "Das Projekt starten, etwa im Container Manager. `/health/ready` liefert danach `Healthy`.",
                    "Im Reverse Proxy die Domain mit Let's-Encrypt-Zertifikat auf den Port der App leiten und Uploads bis 30 MB erlauben.",
                    "Die Adresse aufrufen und den **Einrichtungsassistenten** durchlaufen.",
                ],
                [
                    "Die ausführliche Anleitung steht unter `docs/betrieb/installation.md`, die Übergabe an eine Nachfolgeperson unter `docs/betrieb/nachfolge.md`.",
                ]),

            new("mailversand", "Mailversand einrichten", Icons.Material.Outlined.Email,
                "Administratoren",
                "myLife verschickt Mails für Einladungen, Lebenszeichen, Erinnerungen und den Notfallzugang. **Ohne Mailversand laufen die Fristen des Notfallzugangs nicht weiter.**",
                [
                    "**Verwaltung → Einstellungen** öffnen.",
                    "Reiter **Allgemein**: **Adresse der Instanz** eintragen, etwa `https://mylife.example.org`.",
                    "Reiter **E-Mail**: SMTP-Server, Port, Verschlüsselung (STARTTLS meist auf 587, SSL/TLS meist auf 465), Benutzername, Passwort, Absenderadresse und Absendername.",
                    "**Speichern und Testmail senden**. Die Testmail geht an deine eigene Adresse; ein Fehler erscheint sofort.",
                    "Optional Reiter **ntfy**: Server und Zugriffstoken für Push-Nachrichten, dann **Speichern und Test senden**.",
                ],
                [
                    "Unter **Warteschlange** siehst du wartende und aufgegebene Mails und kannst mit **Jetzt erneut versuchen** nachhelfen.",
                    "Gespeicherte Passwörter erscheinen als „hinterlegt“. Leer lassen behält den hinterlegten Wert.",
                ],
                "admin/settings", "Einstellungen"),

            new("fristen-grenzen", "Fristen und Abmeldezeit festlegen", Icons.Material.Outlined.Tune,
                "Administratoren",
                "Du gibst Standardwerte und Grenzen für den Notfallzugang vor. Jeder Nutzer wählt seine eigenen Werte innerhalb dieser Grenzen; kürzere Fristen kannst du nicht erzwingen.",
                [
                    "**Verwaltung → Einstellungen → Fristen** öffnen.",
                    "Für **Lebenszeichen**, **Wartezeit** und **Frist bei einem Antrag** je **Mindestens**, **Standard** und **Höchstens** festlegen.",
                    "Unter **Erinnerungen (Tage nach Ablauf)** die Stufen eintragen, aufsteigend und durch Komma getrennt, etwa `3, 7, 14`.",
                    "Im Reiter **Allgemein** die **Automatische Abmeldung** in Minuten und die **Aufbewahrung des Protokolls** einstellen.",
                    "**Speichern**.",
                ],
                []),

            new("sicherung", "Sicherung prüfen und zurückspielen", Icons.Material.Outlined.Backup,
                "Administratoren, Betreiber",
                "Ist `MYLIFE_BACKUP_KEY` gesetzt, legt myLife jeden Tag eine verschlüsselte Sicherung im Ordner `backup` ab: Datenbank, Schlüsselring und auf Wunsch die Dokumente.",
                [
                    "**Verwaltung → Einstellungen → Sicherung** öffnen.",
                    "Letzte Sicherung, nächsten Lauf und Fehler ansehen. **Jetzt sichern** erstellt sofort eine.",
                    "Uhrzeit, Anzahl der täglichen, wöchentlichen und monatlichen Sicherungen und **Dokumente in jede Sicherung aufnehmen** einstellen.",
                    "Zum Zurückspielen das Projekt stoppen und per SSH `docker compose run --rm app restore /data/backup/<Datei>.mlbak --confirm` ausführen.",
                ],
                [
                    "Ohne den Sicherungsschlüssel lässt sich keine Sicherung zurückspielen. Bewahre ihn getrennt von der NAS auf.",
                    "Die Sicherungen gehören zusätzlich an einen zweiten Ort, etwa mit Hyper Backup.",
                    "Schlägt eine Sicherung fehl, bekommen die Administratoren eine Mail.",
                ],
                "admin/settings", "Einstellungen"),

            new("benutzer", "Benutzer verwalten", Icons.Material.Outlined.People,
                "Administratoren",
                "Unter **Benutzer** siehst du alle Konten mit Namen, Adressen und Anmeldedaten, aber keine Tresorinhalte. Jede Aktion wird protokolliert, und die betroffene Person bekommt eine Mail.",
                [
                    "**Verwaltung → Benutzer** öffnen.",
                    "Unter **Aktionen** je Konto: **Sperren** und **Entsperren**, **Zwei-Faktor-Anmeldung zurücksetzen**, **Zum Administrator machen** oder **Administratorrecht entziehen**, **Löschen beantragen**.",
                    "Unter **Offene Einladungen** Einladungen zurückziehen.",
                ],
                [
                    "Die Zwei-Faktor-Anmeldung nur zurücksetzen, wenn du sicher weißt, dass die Person es selbst ist.",
                    "Gelöscht wird erst, wenn die Person den Link in der Mail selbst bestätigt.",
                    "Es muss immer mindestens einen aktiven Administrator geben. Besser sind zwei.",
                    "Das **System-Protokoll** unter **Verwaltung** zeigt Verwaltungsaktionen und Systemereignisse.",
                ],
                "admin/users", "Benutzer"),

            new("update", "Updates einspielen", Icons.Material.Outlined.SystemUpdateAlt,
                "Betreiber",
                "Updates kommen als neues Image. Datenbank-Migrationen laufen beim Start automatisch.",
                [
                    "Unter **Verwaltung → Einstellungen → Sicherung** **Jetzt sichern**.",
                    "Das Projekt stoppen, das Image aktualisieren und das Projekt wieder starten, oder per SSH `docker compose pull` und `docker compose up -d`.",
                    "Kurz anmelden und unter **Verwaltung** prüfen, dass Mailversand und Sicherung keinen Fehler zeigen.",
                ],
                [
                    "Mit einer festen Version in `MYLIFE_VERSION` kommt ein Update nur, wenn du die Zahl änderst.",
                    "Migrationen laufen nur vorwärts. Zurück geht es mit der alten Version und der Sicherung von vor dem Update.",
                ]),
        ]),
    ];
}
