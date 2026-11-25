# myLife – Umsetzungsplan

Nach jedem Meilenstein halte ich an, committe, pushe und warte auf Feedback (Vorgehen, Punkt 4; Entscheidung 37).

## M0 – Grundgerüst ✅ (24.11.2026)

- Solution `MyLife.sln` mit den Projekten Client, Server, Shared, Crypto und Tests, .NET 10, Nullable aktiv, zentrale Paketversionen
- MudBlazor-Layout (hell und dunkel, responsiv), Lokalisierung de/en, Sprachumschalter
- PostgreSQL über EF Core mit Npgsql, erste Migration, Health-Endpoint
- `Dockerfile` (linux/amd64), `docker-compose.yml`, `.env.example`
- GitHub Actions: Build, Tests, Image nach `ghcr.io/eckis-corner/mylife`
- README, Lizenz AGPL-3.0, `SECURITY.md`, `PROMPT.md`
- **Installationsanleitung** für Container Manager und Nginx Proxy Manager (erste Fassung)

**Abnahme:** Die leere App läuft auf der DS1520+ unter `https://mylife.eckis-corner.de`

## M1 – MVP: Tresor

1. **Krypto-Kern:** `myLife.crypto.js` (Argon2id, HKDF, AES-GCM, ECIES, ECDSA), C#-Fassade, .NET-Referenzimplementierung, Testvektoren, Kreuztests ✅ (24.11.2026, [kryptoformat.md](kryptoformat.md))
2. **Einrichtungsassistent:** erster Nutzer als Admin, Master-Passwort, Recovery-Kit (24 Wörter und QR-Code, druckbar), TOTP und/oder Passkey, Hinweis „Was gehört nicht hinein“ ✅ (24.11.2026; Einrichtungscode 51, Passkey mit M1.3, 52)
3. **Anmeldung** mit zweitem Faktor, automatische Abmeldung, Passwort ändern, Wiederherstellung per Recovery-Key ✅ (24.11.2026; dazu Passkeys, Entscheidungen 54–58)
4. **Schema-Motor:** Objekttyp-Schemata → Formular, Datenblatt, Suchindex ✅ (24.11.2026; 59)
5. **Alle Objekttypen der Bereiche 0–11 und 13** (siehe [objekttypen.md](objekttypen.md)): anlegen, bearbeiten, beenden, löschen (Papierkorb). Dazu Wünsche, Verweise, Zusatzfelder und die Prüfungen „Gehört nicht hinein“. ✅ (24.11.2026; 60, 66)
6. **Dokumente** an jedem Objekt, bis 25 MB, verschlüsselt, Vorschau für PDF und Bilder, Foto direkt vom Smartphone ✅ (24.11.2026; 61)
7. **Dashboard:** Kacheln je Bereich mit Anzahl und „Zuletzt geprüft am“, Knopf „Als geprüft markieren“ ✅ (24.11.2026; 67)
8. **Volltextsuche** im Browser ✅ (24.11.2026; 65)
9. **Exporte:** Datenblatt einzeln, Notfallordner-PDF mit Passwort, druckfertige HTML-Ansicht, JSON-Export und JSON-Import ✅ (24.11.2026; 63, 64, [format.md](format.md))
10. **Audit-Log** (eigener Tresor) und Sicherheits-Header ✅ (24.11.2026; 62)

**Abnahme:** Echte Daten auf der DS1520+ erfasst. In der Datenbank steht nachweislich nur Chiffrat.

## M2 – Familie, Vertrauenspersonen, Notfallzugang

1. **Admin-Bereich:** SMTP und ntfy mit Testmail, Mail-Outbox mit Wiederholung, Standard- und Grenzwerte der Fristen, Abmeldezeit, Benutzerverwaltung (einladen, sperren, löschen, 2FA zurücksetzen), System-Audit-Log, Datenschutzhinweis ✅ (24.11.2026; Entscheidungen 72–78; Einladen mit M2.2)
2. **Nutzer einladen:** Einladungslink mit Geheimnis, Einladung annehmen (Passwort, 2FA, Recovery-Kit), automatische Schlüsselprüfung mit Pinning (Architektur 4.3) ✅ (24.11.2026; Entscheidungen 79–84)
3. **Vertrauenspersonen** im eigenen Tresor festlegen: Beziehung, Antragsrecht, **Freigaben je Bereich und Modus**, Briefe je Empfänger, **„Sofort teilen“ je Bereich** (46b). Die Umschläge erzeugt der Browser. ✅ (24.11.2026; Entscheidungen 85–91)
4. **Notfallzugang:** Zustandsautomat, Hintergrunddienst, Lebenszeichen per Login oder Link, gestufte Erinnerungen (zweite Mail-Adresse und ntfy optional), Antrag mit Information an alle Vertrauenspersonen, Widerspruch (Link hält an, Anmeldung entscheidet), Schutz bei Mailausfall ✅ (24.11.2026; Entscheidungen 92–98)
5. **Lesemodus** für Hinterbliebene: Auswahl der verstorbenen bzw. handlungsunfähigen Person, ruhige Oberfläche, große Schrift, nur freigegebene Inhalte, Navigation entlang der Checkliste ✅ (24.11.2026; Entscheidungen 99–101; bis zur Checkliste in M3 entlang der Bereiche)
6. **Rücknahme** im Modus Handlungsunfähigkeit mit neuen Bereichsschlüsseln, **gemeinsames endgültiges Löschen** nach dem Tod ✅ (25.11.2026; Entscheidungen 102–106; Rücknahme auch für „verstorben“, dazu neue Briefschlüssel)
7. **Notfallpaket** (OfflineViewer.html und gedruckter Paketschlüssel) ✅ (25.11.2026; Entscheidungen 107–110)

**Abnahme:** Testlauf mit zwei Nutzern und verkürzten Fristen, von der Einladung bis zur Vertrauensperson, die liest.

## M3 – Erinnerungen, Checkliste, Feinschliff

1. **Erinnerungen:** Prüfintervall (Standard 6 Monate) und das Ablaufdatum jedes Objekts (39). Der Server kennt nur den nächsten Termin. ✅ (25.11.2026; Entscheidungen 111–114; dazu Wiedervorlagen per Mail)
2. **Checkliste (Bereich 12):** Vorlage für Deutschland (24 Stunden, 1 Woche, 1 Monat, später), anpassbar, Aufgaben listen verknüpfte Objekte auf (zum Beispiel alle Verträge mit „kündigen“), gemeinsames Abhaken ✅ (25.11.2026; Entscheidungen 115–119; Lesemodus folgt der Checkliste)
3. **Vollständigkeitsanzeige** je Bereich und Objekt (Pflicht- und empfohlene Felder, fehlende typische Objekte) ✅ (25.11.2026; Entscheidungen 120–123)
4. **Server-Sicherung:** täglicher verschlüsselter Dump im Volume `backup`, Rücksicherung getestet ✅ (25.11.2026; Entscheidungen 124–129; Schnappschuss per COPY statt pg_dump)
5. **Betriebsdoku:** Update, Sicherung, Rücksicherung, Übergabe an die Nachfolgeperson; Formatbeschreibung `docs/format.md`; Werkzeug `mylife-decrypt` ✅ (25.11.2026; Entscheidungen 130–132)
6. Barrierefreiheit, Durchsicht der englischen Texte, Prüfung der Sicherheit (CSP, Header, Abhängigkeiten), Release v1.0

## Später (nicht geplant)

- Shamir Secret Sharing (10b), Passkey-PRF zum Entsperren ohne Master-Passwort, weitere Sprachen, Volltextsuche in PDFs, gemeinsame Objekte zwischen Tresoren (45)

## Abgleich mit PROMPT.md (38)

Jede Anforderung aus `PROMPT.md` mit der Stelle, die sie abdeckt. ⚠ = offen oder geändert.

| Anforderung | Abgedeckt durch |
| --- | --- |
| Name „myLife“ | überall |
| Familie kommt im Notfall sicher und vollständig an die Daten | Architektur 4–5, Exporte 7, Notfallpaket |
| Zu Lebzeiten liest niemand mit, auch nicht der Betreiber | Zero-Knowledge, Architektur 1, 4 (Restrisiko 3 und 5.2 benannt) |
| Kategorien 1–12 mit eigenen Feldern, Notizen, Anhängen | Objekttypen-Katalog, M1.4–6, erweitert um Bereich 0 und 13 (41); ⚠ Felder-Durchsicht offen (47) |
| Kreditkarte ohne volle Nummer und PIN | Schema Karte, Luhn-Prüfung, M1.5 |
| Verträge: kündigen/übernehmen/weiterlaufen | Wunsch je Objekt, Checkliste verknüpft |
| Urkunden mit Ablageort des Originals | Pflichtfeld Urkunde |
| Digitaler Nachlass mit Wunsch löschen/Gedenkzustand/übergeben, Krypto nur Ort | Schemata Bereich 8 |
| Passwortmanager ohne einzelne Passwörter | Schema Bereich 9 |
| Kontakte, persönliche Wünsche, Briefe an Einzelne | Bereiche 10 und 11, Briefe mit eigener Freigabe |
| Checkliste 24 h / Woche / Monat | M3.2 |
| Dashboard mit Vollständigkeit und „Zuletzt geprüft“ | M1.7, M3.3 |
| Erinnerungen (6 Monate, ablaufende Dokumente/Karten) | M3.1, Ablaufdatum an jedem Objekt (39) |
| Volltextsuche | M1.8 |
| Begünstigte mit Beziehung, Kontakt, Kategorien | M2.3 (Kontakt kommt aus dem Nutzerkonto) |
| Dead-Man's-Switch mit Intervall, Stufen, Wartezeit, Antrag, Widerspruch | Architektur 5, M2.4 |
| Nur freigegebene Kategorien nach der Freigabe | Umschläge je Bereich und Modus |
| Export verschlüsseltes PDF / druckfertig | M1.9 |
| Lesemodus für Hinterbliebene | M2.5 |
| AES-256-GCM, Argon2id, Verschlüsselung im Client | Architektur 4.1 |
| Asymmetrische Schlüsselverteilung | Architektur 4.2, 4.3 |
| 2FA TOTP / Passkeys | Architektur 4.4 |
| Wiederherstellungs-Kit | M1.2 |
| Audit-Log „wer, wann, worauf“ | Architektur 9, M1.10, M2.1 |
| Automatische Abmeldung, keine sensiblen Daten in Logs | Architektur 4.4, 9 |
| Self-hosted, verschlüsselte Backups an zweiten Ort | M3.4 und Synology-Backup (5) |
| Hinweis „Was gehört nicht hinein“ | Hinweisseite, Assistent, Prüfungen bei der Eingabe |
| Blazor + MudBlazor, .NET, PostgreSQL, Skill-Konventionen | M0; Abweichungen in entscheidungen.md |
| WebAssembly statt Blazor Server, begründet; Krypto-Bibliothek begründet | Architektur 3 |
| Docker Compose mit App, DB, Reverse Proxy | M0; ⚠ Proxy ist der vorhandene Nginx Proxy Manager (3) |
| Anleitung Installation, Updates, Backups | M0 (Installation), M3.5 |
| SMTP konfigurierbar, Dead-Man's-Switch als Hintergrunddienst | M2.1, M2.4 |
| Responsive Web-App, keine native App | M0 Layout, Foto-Upload mobil |
| Ein Hauptnutzer, wenige Vertrauenspersonen, keine Registrierung, Einladung | ⚠ geändert durch 34: mehrere Nutzer mit eigenem Tresor, Admin, nur Einladung |
| Deutsch, vorbereitet für weitere Sprachen | de + en, `.resx` (22) |
| Saubere Architektur, automatisierte Tests (Krypto, Notfallzugang) | Architektur 3, 9 |
| Offenes, verschlüsseltes JSON | M1.9, `docs/format.md` |
| Vorgehen: Rückfragen → Architektur → Meilensteine → Feedback | dieses Dokument |

Beim Abgleich zusätzlich ergänzt: die Schlüsselprüfung ohne Telefon (4.3), Benachrichtigung bei Admin-Aktionen, Löschen des eigenen Kontos, gemeinsames Löschen nach dem Tod, Schließfach und Ausweise als Objekttypen, Meldefristen bei Versicherungen, Personen, die im Todesfall benachrichtigt werden sollen.
