# Entscheidungsprotokoll myLife

Fortlaufend nummeriert. Jede Rückfrage behält ihre Nummer, die Antwort steht dahinter.

## Runde 1 (24.11.2026)

| Nr. | Thema | Entscheidung |
| ---: | --- | --- |
| 1 | Hardware | Docker auf einer Synology |
| 2 | Erreichbarkeit | Internet unter `https://mylife.eckis-corner.de` (Domain stellt der Betreiber bereit) |
| 3 | Reverse Proxy | Vorhandener **Nginx Proxy Manager**, den der Betreiber selbst einrichtet. Das Compose-Paket enthält keinen eigenen Proxy. |
| 4 | Server nach dem Tod | Alle drei Absicherungen: gedruckter Notfallordner, verschlüsseltes Export-Paket mit Offline-Entschlüsselung als HTML-Datei, Betriebsanleitung für eine Nachfolgeperson |
| 5 | Backups | Fügen sich in das bestehende Synology-Backup ein. myLife legt verschlüsselte Sicherungen in einem Volume ab, das Backup sichert dieses Volume. |
| 6 | SMTP | Der Administrator konfiguriert den SMTP-Zugang in der Oberfläche |
| 7 | Hosting/Krypto | Blazor WebAssembly mit Web Crypto API per JS-Interop, Argon2id über eine WASM-Bibliothek |
| 8 | 2. Faktor | Anmeldung mit Passwort und TOTP **oder** Passwort und Passkey |
| 9 | Vertrauenspersonen | Eigenes Passwort, 2FA und Wiederherstellungs-Kit (9a) |
| 10 | Schlüsselverteilung | Umschläge mit dem öffentlichen Schlüssel jeder Vertrauensperson, der Server hält sie bis zur Freigabe zurück (10a). Shamir als spätere Option. |
| 11 | Wiederherstellung | Ausgedruckter Recovery-Key (24 Wörter und QR-Code) |
| 12 | Standardwerte | Lebenszeichen alle 30 Tage, Erinnerungen nach 3, 7 und 14 Tagen, danach 14 Tage Wartezeit. Alle Werte änderbar. |
| 13 | Kanäle | E-Mail, eine zweite Adresse optional, ntfy optional. Der Lebenszeichen-Link in der Mail genügt, keine Anmeldung nötig. |
| 14 | Zugangsantrag | Der Eigentümer legt fest, welche Vertrauenspersonen einen Antrag stellen dürfen (einzelne oder alle) |
| 15 | Widerspruch | Ein Link in der Mail hält den Antrag sofort an. Endgültig widersprechen geht nur nach Anmeldung. |
| 16 | Handlungsunfähigkeit | Eigener Modus mit eigenen Freigaben, der Zugriff lässt sich nach der Rückkehr wieder entziehen |
| 17 | Freigaben | Pro Bereich (früher „Kategorie“), für persönliche Briefe zusätzlich pro Empfänger |
| 18 | Anhänge | 25 MB pro Datei, verschlüsselt im Dateisystem abgelegt |
| 19 | Suche | Nur im Browser über die entschlüsselten Einträge. Der Inhalt von PDFs wird im MVP nicht durchsucht. |
| 20 | PDF | Wird im Browser erzeugt, zusätzlich eine druckfertige HTML-Ansicht |
| 21 | Checkliste | Standardliste für Deutschland, anpassbar. Die Hinterbliebenen haken gemeinsam ab. |
| 22 | Sprachen | Deutsch und Englisch ab dem ersten Release, weitere Sprachen über Ressourcen-Dateien vorbereitet |
| 23 | Repository | `eckis-corner/myLife`, Git, **öffentlich auf GitHub** (Open Source) |
| 24 | Stack | .NET 10, Projekte Client/Server/Shared/Crypto/Tests, xUnit, bUnit, Playwright |
| 25 | Vorgehen | Architektur und Plan in `docs/`, Umsetzung erst nach Freigabe des Plans |

## Runde 2 (24.11.2026)

| Nr. | Thema | Entscheidung |
| ---: | --- | --- |
| 26 | Hardware | Synology DS1520+ (Intel Celeron J4125, **x86-64**) mit DSM 7.x und Container Manager. Images werden nur für `linux/amd64` gebaut. |
| 27 | Lizenz | **AGPL-3.0** |
| 28 | Updates | GitHub Actions bauen die Images nach `ghcr.io/eckis-corner/mylife`, auf der Synology genügt `docker compose pull && docker compose up -d` |
| 29 | Objektmodell | Jede Information ist ein **eigenes Objekt mit festem Objekttyp** (Versicherung, Bankkonto, Vertrag …) und typischen Feldern. Jedes Objekt hat ein **Datenblatt** und beliebig viele hochgeladene Dokumente. Der Katalog steht in [objekttypen.md](objekttypen.md). Die ursprüngliche Frage zu Ablauf-Erinnerungen ist offen geblieben und kommt als Rückfrage 39 wieder. |
| 30 | Metadaten | Akzeptiert: Der Server kennt belegte Bereiche und Objekttypen, Anzahl und Größen, aber keine Inhalte |
| 31 | Schlüsselprüfung | **Kein Telefonat und kein manueller Code.** Die öffentlichen Schlüssel werden automatisch geprüft: Bindung über ein Einladungsgeheimnis und Warnung, wenn sich der Schlüssel einer Person später ändert (Architektur 3.2) |
| 32 | Kein Lebenszeichen | Läuft der Dead-Man's-Switch ab, werden **beide Modi** freigegeben (verstorben und handlungsunfähig). Bei einem Antrag wählt die Vertrauensperson den Modus. |
| 33 | Antrag | Bei einem Antrag werden der Eigentümer **und alle anderen Vertrauenspersonen** des Eigentümers informiert |
| 34 | Mehrere Nutzer | **Jeder Nutzer hat einen eigenen Tresor** und kann andere Nutzer der Familie gegenseitig als Vertrauenspersonen berechtigen. Ein **Administrator** verwaltet die globalen Einstellungen (Zeiten, E-Mail, Benutzer usw.) und sieht keine Tresorinhalte. |
| 35 | Datenmodell | Mehrere Eigentümer von Anfang an, siehe 34 |
| 36 | Zugriffsdauer | Unbegrenzt nach der Freigabe im Modus „verstorben“. Die Vertrauenspersonen können den Tresor gemeinsam endgültig löschen. |
| 37 | GitHub | Öffentliches Repository `eckis-corner/myLife`, Commit und Push nach jedem Meilenstein. `PROMPT.md` kommt mit ins Repo. |
| 38 | Prüfung | Anforderung vollständig gegen Architektur und Plan geprüft, Ergebnis im Abschnitt „Abgleich mit PROMPT.md“ in [meilensteine.md](meilensteine.md) |

## Runde 3 (24.11.2026)

| Nr. | Thema | Entscheidung |
| ---: | --- | --- |
| 39 | Ablaufdatum | **Jedes Objekt** bekommt ein Feld „Ablaufdatum“, nicht nur Ausweise und Karten. Für die Erinnerungsmail bekommt der Server nur den nächsten fälligen Termin ohne Zusammenhang (39b, angenommen, siehe Rückfrage 50). |
| 40 | Verweise | Objekte verweisen aufeinander, im Datenblatt in beide Richtungen sichtbar |
| 41 | Bereiche | Bereich 0 „Über mich“, Bereich 13 „Einkommen, Rente & Steuern“, Objekttypen Gerät und Schließfach übernommen |
| 42 | Einladen | Jeder Nutzer lädt seine Vertrauenspersonen selbst ein, dabei entsteht ein Konto. Der Admin sieht alle Konten und kann sie sperren. |
| 43 | Fristen | Der Admin legt Standardwerte und Grenzen fest, jeder Nutzer wählt seine Werte innerhalb dieser Grenzen |
| 44 | Admin | Sperren, Löschen und 2FA-Reset mit Mail an den Betroffenen. Löschen braucht die Bestätigung des Nutzers per Mail-Link. Nutzer mit freigegebenem Notfallzugang kann der Admin nicht löschen. |
| 45 | Gemeinsame Objekte | Jeder erfasst sie im eigenen Tresor, echtes Teilen kommt später |
| 46 | Teilen zu Lebzeiten | **46b:** Zusätzlich zu den Notfall-Freigaben gibt es je Bereich „Sofort teilen“ an ausgewählte Vertrauenspersonen (nur Lesen). Kommt mit M2. |
| 47 | Felderkatalog | Offen, der Betreiber prüft [objekttypen.md](objekttypen.md) noch |
| 48 | Datenblatt | Wie vorgeschlagen: strukturierte Ansicht aller Felder, einzeln druckbar und als PDF, Teil des Notfallordners |
| 49 | Planfreigabe | Plan freigegeben, Umsetzung M0 gestartet |

## Runde 4 (24.11.2026, Umsetzung M1)

| Nr. | Thema | Entscheidung |
| ---: | --- | --- |
| 50 | Wortliste Recovery-Key | **Englische BIP-39-Standardliste** für alle Sprachen. Eine offizielle deutsche Liste gibt es nicht; die Oberfläche drumherum ist deutsch bzw. englisch. Beim Eingeben genügen die ersten vier Buchstaben jedes Worts. |
| 51 | Schutz der Ersteinrichtung | Solange es keinen Nutzer gibt, verlangt der Assistent einen **Einrichtungscode**. Der Server erzeugt ihn bei jedem Start neu und schreibt ihn ins Container-Protokoll. Sonst würde Administrator, wer die offene URL zuerst aufruft. |
| 52 | Passkey | Die Einrichtung eines Passkeys kommt mit der Anmeldung in **M1.3** (Registrierung und Anmeldung zusammen, damit beides gemeinsam getestet wird). Der Assistent richtet TOTP ein. |
| 53 | Anmeldehash auf dem Server | Argon2id mit m = 19 MiB, t = 2, p = 1 über den Anmeldehash aus dem Browser. Die Eingabe hat bereits 256 Bit Entropie; moderate Parameter schonen die Synology. |

### Abweichungen vom MudBlazor-Projekt-Skill

Der Skill stammt aus smartCSV. Bei myLife gelten diese Abweichungen:

- **PostgreSQL** (Npgsql) statt SQL Server. Für die Concurrency nimmt myLife die Spalte `xmin` statt `RowVersion`.
- **Interactive WebAssembly** statt Blazor Server, damit die Zero-Knowledge-Zusage hält.
- **Mehrere Nutzer** (Familie) statt eines Hauptnutzers, Entscheidung 34. Es bleibt bei einer Instanz pro Familie, ohne öffentliche Registrierung und ohne Mandanten.
- JS-Interop ist für die Krypto ausdrücklich gewollt. Die Web Crypto API ist dort die sicherere Lösung.
- Datenbankzugriffe laufen nicht über Services in der Komponente. Sie laufen über eine HTTP-API, im Client kapseln typisierte API-Services die Aufrufe.

Alle übrigen Regeln gelten unverändert: MudBlazor-Komponenten, `MudTable` mit Pager oben und unten, Schalter „Auswählen“, Lokalisierung über Ressourcen-Dateien, Soft Delete, Audit-Trail, serverseitige Berechtigungsprüfung.
