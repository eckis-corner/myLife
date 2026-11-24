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
| 54 | Wiederherstellung | Aus dem Recovery-Key leitet der Browser einen **Prüfwert** ab (HKDF, `mylife/recovery-auth`), den der Server wie den Anmeldehash nur als Argon2id-Hash speichert. Die Wiederherstellung setzt ein neues Master-Passwort; **der zweite Faktor bleibt**, und der Recovery-Key bleibt gültig. Einen neuen Recovery-Key auf Wunsch gibt es später in den Sicherheitseinstellungen. Konten aus der Zeit vor M1.3 haben keinen Prüfwert. |
| 55 | Neuladen der Seite | Die Sitzung beim Server bleibt bestehen, der Tresor ist danach **gesperrt**. Entsperren verlangt nur das Master-Passwort und läuft im Browser: Es ist richtig, wenn sich der Tresorschlüssel entpacken lässt. |
| 56 | CSRF | Zusätzlich zu `SameSite=Strict` verlangt jeder schreibende API-Aufruf den Header `X-MyLife-Request: 1`. Fremde Seiten können ihn ohne CORS-Freigabe nicht setzen. |
| 57 | Fehlversuche und Abmeldung | Nach **5 Fehlversuchen** (Passwort, Code, Passkey oder Recovery-Key) ist das Konto **15 Minuten** gesperrt. Die Sperrmeldung verrät, dass es das Konto gibt; für eine Familieninstanz akzeptiert. Automatische Abmeldung nach **15 Minuten** Inaktivität, bis M2 über `MyLife__IdleTimeoutMinutes` einstellbar. |
| 58 | Seiten ohne Serverprüfung | Die Seiten sind ohne Prerendering nur eine leere Hülle. Der Server liefert sie jedem aus (`AllowAnonymous`), `[Authorize]` prüft der Client. Geschützt sind die Daten: Jeder API-Endpunkt verlangt die Anmeldung selbst. |

## Runde 5 (24.11.2026, Umsetzung M1.4 bis M1.10)

| Nr. | Thema | Entscheidung |
| ---: | --- | --- |
| 59 | Schema und Feldwerte | Die Objekttypen stehen als Katalog in `MyLife.Shared/Vault/ObjectCatalog.cs`, die Texte in den Ressourcen. Feldwerte liegen im Chiffrat als Zeichenketten in fester Form (Zahl mit Punkt, Datum `yyyy-MM-dd`, IBAN ohne Leerzeichen). Neue Felder brauchen deshalb keine Migration. Schlüssel von Feldern und Auswahlwerten werden nie umbenannt, nur ergänzt. Bereich 12 (Checkliste) folgt mit M3. Beim Brief stehen die Empfänger bis M2 als Text. |
| 60 | Objekt-IDs | Die ID eines Objekts vergibt der Browser, weil sie im Kontext der Verschlüsselung steckt (`object:{Typ}:{ID}`). Der Typ eines Objekts lässt sich deshalb nicht ändern. Gleichzeitige Änderungen erkennt der Server über `xmin` und antwortet mit 409; die Oberfläche bietet „Neu laden“ an. |
| 61 | Dokumente | Kein eigener Dateikopf: Die Blockgröße ist fest 1 MiB, Bezeichnung, Dateiname, Typ, Größe und Datum liegen als eigenes Chiffrat mit dem Bereichsschlüssel vor. Der Inhalt eines Dokuments lässt sich nur einmal hochladen. Gelöschte Objekte und Dokumente bleiben 30 Tage im Papierkorb, abgebrochene Uploads verschwinden nach einem Tag (`TrashPurgeService`, stündlich). |
| 62 | Audit-Log | Protokolliert werden Anmeldung, Fehlversuch, Abmeldung, Passwort, Wiederherstellung, Passkeys, alle Änderungen an Bereichen, Objekten und Dokumenten sowie Exporte (die meldet der Browser, nur aus einer festen Liste). Das eigene Öffnen von Dokumenten wird nicht protokolliert; Abrufe durch Vertrauenspersonen kommen mit M2. IP-Adressen werden gekürzt (IPv4 /24, IPv6 /48). |
| 63 | PDF | Eigener, kleiner PDF-Erzeuger im Browser statt pdf-lib und qpdf-wasm: nur Text mit den Standardschriften Helvetica, keine Bilder; Design nach Entscheidung 68. Der Passwortschutz folgt PDF 2.0 (AES-256, Revision 6). PDF-Passwörter dürfen nur ASCII-Zeichen enthalten, weil Apple Vorschau Umlaute anders kodiert als die Norm und andere Programme. Ohne Passwort geht es nur nach ausdrücklicher Bestätigung. |
| 64 | JSON-Export | Der Export enthält das Chiffrat genau so, wie es auf dem Server liegt, dazu die verpackten Schlüssel ([format.md](format.md)). Öffnen lässt er sich mit dem Master-Passwort zum Zeitpunkt des Exports oder mit dem Recovery-Key. Der Papierkorb gehört nicht dazu. Beim Import behalten Objekte und Dokumente ihre IDs, Vorhandenes wird übersprungen. Objekte werden mit den eigenen Bereichsschlüsseln neu verschlüsselt, Dateischlüssel nur umgepackt. |
| 65 | Suche | Ein Objekt trifft, wenn alle Suchbegriffe irgendwo vorkommen: Titel, Typ, Bereich, Felder, Wunsch, Notizen, Zusatzfelder, Bezeichnungen und Dateinamen der Dokumente. Groß- und Kleinschreibung und Akzente spielen keine Rolle. |
| 66 | „Gehört nicht hinein“ | Geprüft werden alle Freitexte beim Speichern: Kartennummern mit gültiger Luhn-Prüfziffer, die Begriffe PIN, TAN, PUK, CVC und CVV, „Passwort:“ mit einem Wert dahinter und ab 12 aufeinanderfolgende BIP-39-Wörter. Es bleibt bei einer Warnung mit „Trotzdem speichern“. |
| 67 | Als geprüft markieren | Je Bereich auf dem Dashboard (Zeitpunkt beim Server, eine Metadate wie „zuletzt geändert“) und je Objekt im Datenblatt (Datum im Chiffrat). |
| 68 | PDF-Design | Die PDFs (Notfallordner, Datenblatt) folgen der Vorlage [`design/Vorlage - eckis-corner.docx`](<design/Vorlage - eckis-corner.docx>): Seitenränder, Titel 28 pt fett zentriert zwischen zwei feinen Linien, Untertitel 18 pt zentriert, Überschriften 18/16/14 pt fett, Akzentfarbe #355269, Fließtext 11 pt im Blocksatz, Fußzeile linksbündig. Statt Tahoma bleibt Helvetica: Tahoma ist eine Microsoft-Schrift und darf nicht eingebettet weitergegeben werden. Der Farbverlauf-Balken der Kopfzeile liegt in der Vorlage außerhalb der Seite und wird nicht übernommen. |
| 69 | Wiedervorlage | Jedes Objekt hat eine optionale Wiedervorlage aus Datum und Hinweis; beides darf leer bleiben. Sie liegt im Chiffrat, der Server kennt sie nicht. Die Übersicht zeigt Wiedervorlagen aktiver Objekte ab 14 Tagen vor dem Datum, fällige hervorgehoben; die Bereichsliste markiert Objekte mit Wiedervorlage. „Wiedervorlage erledigt“ leert Datum und Hinweis. Erinnerungen per Mail folgen mit M3.1. |
| 70 | Logo und Farben | Logo: Ordner mit Benutzersymbol auf abgerundetem Quadrat, Schriftzug „myLife“ („my“ Petrol, „Life“ fett #355269); Quellen und Rastergrößen unter [`design/logo`](design/logo). Das MudBlazor-Theme übernimmt die Farben der Vorlage: Primär und App-Leiste #355269, Sekundär Petrol #008080 (Kontrast auf Weiß 4,8 : 1), Tertiär #5B7A90; im dunklen Modus aufgehellte Töne (#9FB6CA, #4DB8B8, #8FA9BE). In den PDFs steht das Logo mit Schriftzug auf dem Deckblatt des Notfallordners und als kleines Icon rechts in jeder Fußzeile; es ist als Vektorgrafik gezeichnet (Fläche statt Farbverlauf), nicht als eingebettetes Bild. Farben abgelöst durch Entscheidung 71. |
| 71 | rixio-Design | Oberfläche, Logo, Icons und PDFs folgen dem rixio-Template (Repo rixio.de, wie Plex2Jellyfin): Graphit-Flächen (dunkel #111519/#1A2026, hell #F3F6F9/#FFFFFF), Stahlblau #587894 als Zweitfarbe, riXio-Orange #FAA500 für Hauptaktionen; als Textakzent im hellen Modus Bernstein #865500 (Kontrast). Flache Kopfzeile mit Trennlinie, Karten mit Rahmen statt Schatten, aktive Navigation mit Orange-Rahmen. Schrift Titillium Web (OFL), selbst gehostet; Arista 2.0 (Wortmarke riXio) ist nicht frei weitergebbar und wird nicht verwendet. Logo: Ordner mit Benutzersymbol, Graphit/Stahlblau/Orange. PDFs: Satz bleibt nach Entscheidung 68, Überschriften Graphit #202D39, Linien Orange. Hell/dunkel folgt weiter dem System. |

### Abweichungen vom MudBlazor-Projekt-Skill

Der Skill stammt aus smartCSV. Bei myLife gelten diese Abweichungen:

- **PostgreSQL** (Npgsql) statt SQL Server. Für die Concurrency nimmt myLife die Spalte `xmin` statt `RowVersion`.
- **Interactive WebAssembly** statt Blazor Server, damit die Zero-Knowledge-Zusage hält.
- **Mehrere Nutzer** (Familie) statt eines Hauptnutzers, Entscheidung 34. Es bleibt bei einer Instanz pro Familie, ohne öffentliche Registrierung und ohne Mandanten.
- JS-Interop ist für die Krypto ausdrücklich gewollt. Die Web Crypto API ist dort die sicherere Lösung.
- Datenbankzugriffe laufen nicht über Services in der Komponente. Sie laufen über eine HTTP-API, im Client kapseln typisierte API-Services die Aufrufe.

Alle übrigen Regeln gelten unverändert: MudBlazor-Komponenten, `MudTable` mit Pager oben und unten, Schalter „Auswählen“, Lokalisierung über Ressourcen-Dateien, Soft Delete, Audit-Trail, serverseitige Berechtigungsprüfung.
