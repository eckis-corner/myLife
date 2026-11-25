# myLife – Bereiche und Objekttypen

Stand: Entwurf vom 24.11.2026 zu Entscheidung 29.

Jede Information ist ein **Objekt** mit einem festen **Objekttyp**, zum Beispiel ein Bankkonto, eine Versicherung oder ein Vertrag. Jeder Objekttyp hat seine typischen Felder. Die Objekttypen sind in **Bereiche** gruppiert. Die Bereiche sind die Einheit für Freigaben an Vertrauenspersonen und für die Verschlüsselung (ein Schlüssel je Bereich).

## 1. Was jedes Objekt hat

| Baustein | Beschreibung |
| --- | --- |
| **Datenblatt** | Die strukturierte Ansicht aller Felder des Objekts. Sie lässt sich als Seite drucken oder als PDF ausgeben und ist auch Teil des Notfallordners. |
| **Dokumente** | Beliebig viele Dateien (PDF, Fotos, Scans, Office), je bis 25 MB. Auf dem Smartphone direkt fotografieren. Jede Datei hat eine Bezeichnung und ein optionales Datum. |
| Titel | Freier Name, zum Beispiel „Girokonto Sparkasse“ |
| Status | aktiv / beendet (beendete Objekte bleiben als Historie, ausgeblendet) |
| **Wunsch für die Hinterbliebenen** | Auswahl je Objekttyp, zum Beispiel *kündigen / übernehmen / weiterlaufen lassen / löschen / Gedenkzustand / übergeben an …*, dazu ein freier Hinweis |
| Ansprechpartner | Verweis auf ein Objekt vom Typ *Kontakt* |
| Verknüpfungen | Verweise auf andere Objekte, zum Beispiel Kredit ↔ Immobilie ↔ Gebäudeversicherung, im Datenblatt in beide Richtungen sichtbar (40) |
| Ablageort des Originals | Wo liegt das Papier? (bei Urkunden Pflicht, sonst optional) |
| **Ablaufdatum** | Bei jedem Objekt vorhanden (39). Bedeutung je Typ, zum Beispiel Vertragsende, Gültigkeit oder Zinsbindung. Grundlage der Erinnerungen. |
| Wiedervorlage | Optional: Datum und Hinweis, wann und warum das Objekt wieder angesehen werden soll (69). Fällige Wiedervorlagen stehen auf der Übersicht. |
| Notizen | Freitext |
| Zusatzfelder | Frei benannte Felder mit Bezeichnung und Wert für alles, was der Katalog nicht abdeckt (Felderkatalog: Durchsicht offen, 47) |
| Zuletzt geprüft | Datum, wird mit „Als geprüft markieren“ gesetzt |
| Vollständigkeit | Pflicht- und **empfohlene** Felder, Wunsch, Ablageort (Urkunden) und Ablaufdatum (Ausweise, Karten). Welche Felder empfohlen sind und welche Einträge je Bereich typisch sind, steht in `ObjectCatalog.cs` (M3.3, 120–122). |

## 2. Katalog

Felder in *kursiv* sind Auswahllisten.

### Bereich 0 – Über mich (41)

| Objekttyp | Felder |
| --- | --- |
| Persönliche Daten | Name, Geburtsname, Geburtsdatum und -ort, Anschrift, *Familienstand*, Staatsangehörigkeit, Konfession (für die Bestattung), Steuer-ID, Renten-Versicherungsnummer, Krankenversichertennummer |
| Ausweisdokument | *Art* (Personalausweis, Reisepass, Führerschein, Aufenthaltstitel), Nummer, ausstellende Behörde, gültig bis, Ablageort |

### Bereich 1 – Bankkonten & Depots

| Objekttyp | Felder |
| --- | --- |
| Bankkonto | Bank, IBAN, BIC, *Kontoart* (Giro, Tagesgeld, Festgeld, Sparbuch, Gemeinschaftskonto, Geschäftskonto), Kontoinhaber, Mitinhaber, Vollmachten (wer, *über den Tod hinaus ja/nein*), Online-Banking vorhanden, Ansprechpartner |
| Depot | Bank/Broker, Depotnummer, Verrechnungskonto (Verweis), Inhaber, Vollmachten, ungefährer Wert mit Stichtag |
| Schließfach | Bank, Filiale, Fachnummer, Ablageort des Schlüssels, Zugangsberechtigte, Inhalt (Stichworte) |

### Bereich 2 – Kreditkarten

| Objekttyp | Felder |
| --- | --- |
| Karte | *Kartentyp* (Visa, Mastercard, Amex, Girocard, Prepaid), Herausgeber, Karteninhaber, **letzte 4 Ziffern**, Ablauf (MM/JJ), Referenzkonto (Verweis), Sperr-Hotline (Vorschlag 116 116), Zusatzkarten, Jahresgebühr, enthaltene Versicherungen. *Keine vollständige Nummer, keine PIN, keine Prüfziffer* (mit Prüfung bei der Eingabe) |

### Bereich 3 – Kredite & Darlehen

| Objekttyp | Felder |
| --- | --- |
| Kredit / Darlehen | *Richtung* (ich schulde / mir wird geschuldet), *Art* (Immobiliendarlehen, Ratenkredit, Autokredit, Leasing, Privatdarlehen, Dispo, Sonstiges), Gläubiger bzw. Schuldner, Vertragsnummer, Kreditnehmer, Ursprungsbetrag, Restschuld mit Stichtag, Rate und *Rhythmus*, Zinssatz, Zinsbindung bis, Laufzeit bis, **Restschuldversicherung ja/nein** (Verweis auf die Versicherung), Sicherheiten (Verweis, zum Beispiel Immobilie), Lastschriftkonto |

### Bereich 4 – Versicherungen

| Objekttyp | Felder |
| --- | --- |
| Versicherung | *Art* (Lebens-, Risikolebens-, Berufsunfähigkeits-, Unfall-, Privathaftpflicht-, Hausrat-, Wohngebäude-, KFZ-, Kranken-, Pflege-, Zahnzusatz-, Rechtsschutz-, Reise-, Tierhalterhaftpflicht-, Sterbegeld-, Rentenversicherung, Sonstige), Versicherer, Policennummer, Versicherungsnehmer, versicherte Person, **Bezugsberechtigte**, Versicherungssumme, Beitrag und *Rhythmus*, Beginn, Ablauf, Hauptfälligkeit, **Kündigungsfrist**, Zahlungskonto (Verweis), Makler (Kontakt), **Meldefrist im Todesfall** (Vorschlag je Art, etwa 24–72 Stunden bei Lebensversicherungen). Je nach Art kommen Felder dazu: KFZ (Fahrzeug, Kennzeichen, SF-Klasse), Kranken (*GKV/PKV*, Versichertennummer), BU (Rentenhöhe), Wohngebäude und Hausrat (Immobilie) |

### Bereich 5 – Verträge & Abos

| Objekttyp | Felder |
| --- | --- |
| Vertrag / Abo | *Art* (Strom, Gas, Wasser, Fernwärme, Internet, Mobilfunk, Festnetz, Streaming, Zeitung/Zeitschrift, Mitgliedschaft/Verein, Fitnessstudio, Software/Cloud-Abo, Miete, Leasing, Sonstiges), Anbieter, Kunden- und Vertragsnummer, Vertragsinhaber, Kosten und *Rhythmus*, Zahlungskonto (Verweis), Mindestlaufzeit bis, Kündigungsfrist, Sonderkündigungsrecht im Todesfall, **Wunsch: kündigen / übernehmen / weiterlaufen lassen**. Bei Miete zusätzlich Vermieter (Kontakt), Kaution (Höhe, Konto) |

### Bereich 6 – Urkunden & Dokumente

| Objekttyp | Felder |
| --- | --- |
| Urkunde / Dokument | *Art* (Geburtsurkunde, Heiratsurkunde, Scheidungsurteil, Testament, Erbvertrag, Vorsorgevollmacht, Generalvollmacht, Betreuungsverfügung, Patientenverfügung, Grundbuchauszug, Zulassungsbescheinigung Teil II, Rentenbescheid, Steuerbescheid, Zeugnis, Sonstiges), ausstellende Stelle, Nummer, Datum, gültig bis, **Ablageort des Originals (Pflicht)**, Kopien bei, Bevollmächtigte (bei Vollmachten), *registriert im Zentralen Testamentsregister bzw. Zentralen Vorsorgeregister* mit Registriernummer |

### Bereich 7 – Immobilien, Fahrzeuge & Vermögen

| Objekttyp | Felder |
| --- | --- |
| Immobilie | *Art* (Haus, Wohnung, Grundstück, Garage), Anschrift, Eigentümer und Anteile, Grundbuch (Amtsgericht, Blatt), Erwerbsjahr, ungefährer Wert, Finanzierung (Verweis Kredit), Versicherungen (Verweis), vermietet (Mieter, Miete), Hausverwaltung (Kontakt), Ablageort der Schlüssel |
| Fahrzeug | *Art* (PKW, Motorrad, Wohnmobil, Anhänger, Boot), Marke/Modell, Kennzeichen, FIN, Halter, Eigentümer, Ablageort der Zulassungsbescheinigung Teil II, Finanzierung/Leasing (Verweis), Versicherung (Verweis), Ersatzschlüssel, Werkstatt (Kontakt) |
| Wertgegenstand / Vermögen | *Art* (Schmuck, Kunst, Edelmetall, Sammlung, Unternehmensbeteiligung, Bargeld, Sonstiges), Beschreibung, ungefährer Wert mit Stichtag, Aufbewahrungsort, Nachweis |

### Bereich 8 – Digitaler Nachlass

| Objekttyp | Felder |
| --- | --- |
| Online-Konto | *Art* (E-Mail, Social Media, Cloud-Speicher, Messenger, Online-Shop, Bezahldienst, Gaming, Sonstiges), Dienst, Benutzername/Konto-ID (**kein Passwort**), Zugang im Passwortmanager unter …, 2FA-Gerät, *Nachlasskontakt beim Anbieter eingerichtet* (etwa Google-Kontoinaktivität, Apple Nachlasskontakt, Facebook Nachlasskontakt), **Wunsch: löschen / Gedenkzustand / übergeben an …** |
| Domain | Domain, Registrar, Kundennummer, Ablauf, Wunsch |
| Krypto-Wallet | *Art* (Hardware-Wallet, Software-Wallet, Börsenkonto), Anbieter/Gerät, **Aufbewahrungsort der Seed-Phrase** (nur der Ort, **nie die Seed**), ungefährer Wert, Wunsch |
| Gerät | *Art* (Smartphone, Laptop, Tablet, PC, NAS, Smart Home), Bezeichnung, Entsperrung über den Passwortmanager-Eintrag …, Wunsch (zurücksetzen / übergeben / Daten sichern) |

### Bereich 9 – Passwortmanager

| Objekttyp | Felder |
| --- | --- |
| Passwortmanager | Produkt, Konto/E-Mail, *Notfallzugang* (Emergency Kit, Notfallkontakt-Funktion des Anbieters, Familienkonto, Sonstiges), eingetragene Notfallkontakte beim Anbieter, Wartezeit beim Anbieter, **Ablageort des Master-Passworts bzw. Emergency Kits**, Schritt-für-Schritt-Anleitung für die Hinterbliebenen. *Speichert keine einzelnen Passwörter.* |

### Bereich 10 – Wichtige Kontakte

| Objekttyp | Felder |
| --- | --- |
| Kontakt | *Rolle* (Steuerberater, Notar, Anwalt, Arbeitgeber, Hausarzt, Facharzt, Bankberater, Versicherungsmakler, Vermieter, Hausverwaltung, Bestatter, Seelsorger, Freund/Bekannter, Sonstiges), Name, Firma, Telefon, E-Mail, Anschrift, **im Todesfall benachrichtigen ja/nein**, Notiz |

### Bereich 11 – Persönliche Wünsche

| Objekttyp | Felder |
| --- | --- |
| Bestattung | *Art* (Erd-, Feuer-, See-, Baum-, anonyme Bestattung), Ort, Bestattungsvorsorge (Verweis Vertrag/Versicherung), Trauerfeier, Musik, Traueranzeige, wer eingeladen werden soll |
| Organspende | *Entscheidung* (ja, nein, teilweise, Entscheidung durch …), Ablageort des Ausweises, im Organspende-Register eingetragen |
| Haustier | Name, Tierart, Tierarzt (Kontakt), wer es übernehmen soll, Besonderheiten |
| Brief | **Empfänger** (eine oder mehrere Vertrauenspersonen, je mit eigener Freigabe), Titel, Text, Dokumente |
| Sonstiger Wunsch | Titel, Text |

### Bereich 12 – Checkliste für die Hinterbliebenen

| Objekttyp | Felder |
| --- | --- |
| Aufgabe | *Phase* (erste 24 Stunden, erste Woche, erster Monat, später), Titel, Beschreibung, zuständig, **zugehöriger Bereich** und optional **Wunsch** (zum Beispiel listet „Verträge kündigen“ alle Verträge mit dem Wunsch *kündigen* auf), dazu einzelne Verweise; erledigt von/am steht beim Server, für alle gleich (M3.2, 115–118) |

### Bereich 13 – Einkommen, Rente & Steuern (41)

| Objekttyp | Felder |
| --- | --- |
| Arbeitgeber / Einkommen | Arbeitgeber (Kontakt), Personalnummer, *Art* (Angestellt, Beamtet, Selbstständig, Rente/Pension), Gehalts- oder Rentenkonto (Verweis) |
| Altersvorsorge | *Art* (gesetzliche Rente, Betriebsrente, Riester, Rürup, Beamtenversorgung, berufsständisches Versorgungswerk), Träger, Vertrags-/Versicherungsnummer, Hinterbliebenenleistung ja/nein |
| Steuern | Finanzamt, Steuernummer, Steuerberater (Kontakt), Ablageort der Unterlagen |
