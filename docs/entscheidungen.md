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

## Runde 6 (24.11.2026, Umsetzung M2.1)

| Nr. | Thema | Entscheidung |
| ---: | --- | --- |
| 72 | Einstellungen | Die globalen Einstellungen liegen als ein JSON-Wert in `system_settings` (Schlüssel `admin:settings`). SMTP-Passwort und ntfy-Token sind darin mit Data Protection verschlüsselt und verlassen den Server nie; die Oberfläche zeigt nur „hinterlegt“. Neu ist die **Adresse der Instanz** für Links in Mails; ohne sie gilt die Adresse der aktuellen Anfrage. Die **Abmeldezeit** (Standard aus `MyLife__IdleTimeoutMinutes`, 5–240 Minuten) gilt ab der nächsten Anfrage auch für das Sitzungs-Cookie. Die **Aufbewahrung des Protokolls** ist einstellbar (Standard 365 Tage, 30–3650). |
| 73 | Mail-Outbox | Jede Mail geht über `mail_outbox`, gespeichert in derselben Transaktion wie die auslösende Änderung. Ein Hintergrunddienst verschickt sofort danach, sonst alle 30 Sekunden. Fehlschläge werden nach 1, 5, 15, 30 Minuten, 1, 2, 4, 8, 12 und 24 Stunden wiederholt und nach dem zehnten Versuch aufgegeben (Eintrag im System-Protokoll, Admin kann erneut anstoßen). Ohne Mailserver warten Mails, ohne dass Versuche zählen. Die Parameter (etwa Links mit Tokens) sind mit Data Protection verschlüsselt und werden nach dem Versand gelöscht. Mails gehen in der Sprache der Empfängerin oder des Empfängers, als Text und einfaches HTML. Die **Testmail** geht direkt an die eigene Adresse, damit ein Fehler sofort sichtbar ist. |
| 74 | ntfy | Der Admin trägt Server und optional ein Zugriffstoken ein. myLife veröffentlicht als JSON an die Wurzel des Servers, damit Umlaute funktionieren. Themen je Nutzer kommen mit M2.4; jetzt gibt es nur den Test an ein eingegebenes Thema. |
| 75 | Fristen | Standard und Grenzen je Frist: Lebenszeichen 30 Tage (7–180), Wartezeit 14 Tage (7–60), Frist nach einem Antrag 7 Tage (3–30); Erinnerungen 3, 7 und 14 Tage nach Ablauf. Der Admin wählt innerhalb fester Bereiche (Lebenszeichen 7–365, Wartezeit 3–90, Antrag 1–60, bis zu fünf Erinnerungen zwischen 1 und 60 Tagen). Die Werte wirken ab M2.4. |
| 76 | Benutzerverwaltung | Nicht am eigenen Konto; es bleibt immer mindestens ein aktiver Admin. **Sperren** beendet alle Sitzungen; die Sperrmeldung kommt erst nach dem richtigen Passwort. **2FA zurücksetzen** entfernt Authenticator-App und Passkeys; bei der nächsten Anmeldung richtet die Person nach dem Master-Passwort eine Authenticator-App neu ein. **Admin-Recht** geben oder nehmen beendet die Sitzungen der Person, damit die Rolle sofort gilt. Sperren, Entsperren, 2FA-Reset und Löschantrag schicken eine Mail. **Einladen** kommt mit M2.2. |
| 77 | Konto löschen | Der Admin beantragt, die Person bestätigt über einen Link (7 Tage gültig, Einmal-Token mit 256 Bit, in der Datenbank nur der SHA-256-Hash, im Fragment des Links und nur im Rumpf eines POST an den Server). Gelöscht werden Konto, Tresor, Dokumente samt Dateien, Passkeys und das eigene Protokoll, endgültig und nicht nur markiert, damit die Adresse wieder eingeladen werden kann. Der letzte aktive Admin lässt sich nicht löschen. Die Sperre bei freigegebenem Notfallzugang (44) folgt mit M2.4. |
| 78 | System-Protokoll und Datenschutz | Das System-Protokoll zeigt Ereignisse ohne Tresorbezug und alle Verwaltungsaktionen; eine Aktion an einem Konto erscheint zusätzlich im Protokoll der betroffenen Person. Die **Datenschutzseite** (`/privacy`) ist ohne Anmeldung erreichbar und verlinkt in Fußzeile und Anmeldung; ohne Text des Admins zeigt sie einen mitgelieferten Standardtext. Die Verwaltung hat das Anfragelimit des Tresors, die Löschbestätigung das der Anmeldung. |

## Runde 7 (24.11.2026, Umsetzung M2.2)

| Nr. | Thema | Entscheidung |
| ---: | --- | --- |
| 79 | Einladungslink | `/invite#t=<Token>&s=<Geheimnis>&f=<Fingerprint>`. Das Token (256 Bit, in der Datenbank nur als SHA-256) erkennt der Server; Geheimnis (256 Bit) und Fingerprint der einladenden Person stehen nur im Fragment. Das Geheimnis erzeugt der Browser der einladenden Person und legt es mit ihrem Tresorschlüssel verschlüsselt beim Server ab (Kontext `invite-secret:{ID}`). Der Link gilt 14 Tage und nur einmal. Ein neuer Link (erneut senden oder zum Weitergeben) macht den alten ungültig, das Geheimnis bleibt. Eine Adresse kann nur eine offene Einladung haben; für bestehende Konten gibt es keine. |
| 80 | Versand | Standard ist die Mail über die Outbox; dafür schickt der Browser Geheimnis und Fingerprint einmal mit, die Outbox löscht sie nach dem Versand (Grenze aus Architektur 4.3). Alternativ entsteht der Link nur im Browser und wird selbst weitergegeben. Die einladende Person bekommt eine Mail, sobald die Einladung angenommen ist. |
| 81 | Annahme | Der Einrichtungsassistent unter `/invite`: ohne Einrichtungscode, mit fester Adresse, sonst gleich (Passwort, Recovery-Kit, TOTP). Neue Konten sind keine Admins. Der Browser bindet den neuen Fingerprint per HMAC(Geheimnis, `invite:{ID}:` &#124; Fingerprint) an die Einladung und pinnt sofort den Fingerprint der einladenden Person aus dem Link. |
| 82 | Fingerprint, Pin, Gegenzeichnung | Fingerprint = SHA-256(öffentlicher ECDH-Schlüssel &#124; öffentlicher ECDSA-Schlüssel). Pins liegen mit dem eigenen Tresorschlüssel verschlüsselt in `pinned_keys` (Kontext `pin:{ID}`), damit der Server sie weder lesen noch austauschen kann. Gegenzeichnungen sind ECDSA-Signaturen über den Fingerprint (Kontext `endorsement:{ID}`) in `key_endorsements`; der Server nimmt nur solche an, die zum aktuellen Schlüssel passen. |
| 83 | Automatische Prüfung | Nach jedem Entsperren prüft der Browser angenommene Einladungen (HMAC), gegenzeichnet und pinnt bei Erfolg, meldet sonst „Schlüssel passt nicht“ ins eigene und ins System-Protokoll. Gepinnte, unveränderte Schlüssel gegenzeichnet er ebenfalls, so bestätigt auch die eingeladene Person die einladende. |
| 84 | Seite „Familie“ | Zustand je Person aus eigener Sicht: „Von dir bestätigt“ (Pin passt), „Bestätigt durch X“ (gültige Gegenzeichnung von jemandem mit passendem Pin), „Noch nicht bestätigt“ (sachlicher Hinweis, keine Sperre) und „Schlüssel geändert!“ (Pin passt nicht, deutliche Warnung; ab M2.3 keine Umschläge). Dazu die eigenen Einladungen; Admins sehen in der Benutzerverwaltung alle offenen Einladungen und können sie zurückziehen. |

## Runde 8 (24.11.2026, Umsetzung M2.3)

| Nr. | Thema | Entscheidung |
| ---: | --- | --- |
| 85 | Vertrauenspersonen | Jede andere Person mit aktivem Konto kann Vertrauensperson werden. Beziehung und Notiz liegen mit dem Tresorschlüssel verschlüsselt in `trust_relations` (Kontext `trustee-profile:{ID}`); das Antragsrecht steht im Klartext, weil der Server den Antrag ab M2.4 prüft. Die Person bekommt eine Mail, dass sie benannt wurde, ohne Einzelheiten. |
| 86 | Umschläge | Je Person, Bereich und Modus (verstorben, handlungsunfähig, sofort teilen) ein Umschlag: der Bereichsschlüssel per ECIES an den öffentlichen Schlüssel der Person (Kontext `envelope:area:{Eigentümer}:{n}`), dazu eine ECDSA-Signatur des Eigentümers über den Umschlag (Kontext `grant:{Person}:{Art}:{Ressource}:{Modus}`). Die Vertrauensperson prüft die Signatur gegen einen bestätigten Schlüssel, damit ein manipulierter Server keine eigenen Umschläge unterschieben kann. Speichern ersetzt alle Umschläge einer Person. |
| 87 | Schlüsselprüfung beim Versiegeln | Für eine Person mit geändertem Schlüssel entstehen keine Umschläge. Ist ihr Schlüssel noch unbestätigt, pinnt der Browser ihn beim ersten Speichern. Jeder Umschlag trägt den Fingerprint der Person; der Server nimmt nur Umschläge für ihren aktuellen Schlüssel an. Ändert sich der Schlüssel später, zeigt die Liste „Neu speichern nötig“. |
| 88 | Briefe | Jeder Brief hat einen eigenen Objektschlüssel, verpackt mit dem Bereichsschlüssel (Spalte `objects.wrapped_key`, Kontext `object-key:{ID}`); er verschlüsselt Inhalt und Dokumente des Briefs. Briefe aus der Zeit vor M2.3 bekommen ihn beim nächsten Speichern. Freigaben je Brief und Empfänger gibt es nur für verstorben und handlungsunfähig; ein freigegebener Bereich 11 enthält die Briefe nicht. |
| 89 | Sofort teilen | Live-Umschläge liefert der Server sofort aus; die Vertrauensperson liest unter „Mit mir geteilt“, nur Lesen, ohne Briefe und Papierkorb. Jeder Abruf von Inhalten und Dokumenten steht im Protokoll des Eigentümers. |
| 90 | Neuer Bereichsschlüssel | Endet das sofortige Teilen (oder wird die Person entfernt), erzeugt der Browser einen neuen Bereichsschlüssel, verschlüsselt alle Objekte des Bereichs neu (Briefe: nur ihren Schlüssel neu verpackt), packt die Dateischlüssel um und versiegelt alle verbleibenden Umschläge des Bereichs neu. Der Server übernimmt das nur vollständig und unverändert (sonst 409). Abgebrochene Uploads des Bereichs entfallen dabei. |
| 91 | Exportformat | Objekte tragen optional `wrappedKey` (Briefe). Ältere Exporte bleiben gültig; Version 1 bleibt. |

## Runde 9 (24.11.2026, Umsetzung M2.4)

| Nr. | Thema | Entscheidung |
| ---: | --- | --- |
| 92 | Wann der Notfallzugang läuft | Sobald ein Nutzer mindestens eine Vertrauensperson hat. Ohne Vertrauenspersonen gibt es keine Erinnerungen. Jede Anmeldung, der Knopf „Lebenszeichen geben“, das Speichern der eigenen Fristen und der Link in der Mail zählen als Lebenszeichen. |
| 93 | Ablauf ohne Lebenszeichen | Nach dem Intervall kommt zuerst die Mail „Lebenszeichen fällig“, dann die Erinnerungen des Admins (Standard 3, 7, 14 Tage), jeweils gerechnet ab dem tatsächlichen Versand der vorigen Mail. Nach der letzten zugestellten Erinnerung läuft die Wartezeit; danach sind beide Modi frei (32). Mit Standardwerten frühestens 58 Tage nach dem letzten Lebenszeichen. Eigentümer und alle betroffenen Vertrauenspersonen bekommen eine Mail. |
| 94 | Mailausfall | Keine Frist läuft, solange die zugehörige Mail nicht verschickt ist; das gilt für Erinnerungen und für die Antragsfrist. Gibt die Outbox eine Mail auf, schickt der Dienst dieselbe Stufe neu. So führt ein Ausfall nie zu einer Freigabe, und nach dem Ausfall geht es normal weiter. Eigentümer (Notfallzugang, Übersicht) und Admin (Übersicht) sehen einen Hinweis. |
| 95 | Links | Lebenszeichen- und Widerspruchslinks sind Einmal-Tokens (256 Bit, nur der Hash gespeichert, 120 Tage gültig) im Fragment des Links. Der Lebenszeichenlink setzt nur das Lebenszeichen. Der Widerspruchslink hält den Antrag an; nach der Anmeldung widerspricht der Eigentümer endgültig oder lässt ihn weiterlaufen, die Frist verlängert sich dann um die Zeit des Anhaltens. |
| 96 | Antrag | Nur mit Antragsrecht, nur für Modi, für die die Person Umschläge hat, je Eigentümer und Modus ein offener Antrag. Der Eigentümer bekommt Mail (und zweite Adresse, ntfy), alle anderen Vertrauenspersonen eine Information (33). Ein Antrag gibt nur seinen Modus frei. Ein Lebenszeichen beendet einen Antrag nicht; dafür gibt es den Widerspruch. |
| 97 | Eigene Einstellungen | Jeder Nutzer wählt Intervall, Wartezeit und Antragsfrist innerhalb der Grenzen des Admins (43), dazu optional eine zweite Mail-Adresse und ein ntfy-Thema. Die Erinnerungstage gibt der Admin vor. |
| 98 | Freigabe und Rücknahme | Freigegebene Umschläge liefert der Server unter „Mit mir geteilt“ aus. Eine spätere Anmeldung hebt eine Freigabe nicht auf; die Rücknahme mit neuen Bereichsschlüsseln folgt mit M2.6. Ein Konto mit Freigabe kann der Admin nicht löschen (44). |

## Runde 10 (24.11.2026, Umsetzung M2.5)

| Nr. | Thema | Entscheidung |
| ---: | --- | --- |
| 99 | Lesemodus | Eigener, ruhiger Rahmen unter `/reading` ohne Seitenleiste und Verwaltung, große Schrift (zuschaltbar noch größer), sachliche Farben. Zu sehen sind nur Personen, für die mindestens ein Modus (verstorben oder handlungsunfähig) freigegeben ist, und nur deren freigegebene Inhalte; sofort Geteiltes bleibt unter „Mit mir geteilt“. Nur Lesen, Dokumente mit Vorschau und Speichern. |
| 100 | Reihenfolge bis zur Checkliste | Bis die Checkliste mit M3 kommt, führt der Weg Schritt für Schritt entlang der Bereiche: Briefe an mich, Persönliche Wünsche, Wichtige Kontakte, Über mich, Urkunden & Dokumente, Versicherungen, Bankkonten, Kreditkarten, Kredite, Einkommen/Rente/Steuern, Verträge & Abos, Immobilien/Fahrzeuge, Digitaler Nachlass, Passwortmanager. Jeder Schritt hat einen kurzen, behutsamen Hinweis; der Einleitungstext unterscheidet verstorben und handlungsunfähig. |
| 101 | Laden und Protokoll | Ein Schritt wird erst beim Aufschlagen geöffnet, damit im Protokoll des Eigentümers nur steht, was wirklich gelesen wurde; die Schlüssel verlassen beim Verlassen der Seite das Krypto-Modul. Gelesen-Markierungen gibt es erst mit der Checkliste (M3). |

### Abweichungen vom MudBlazor-Projekt-Skill

Der Skill stammt aus smartCSV. Bei myLife gelten diese Abweichungen:

- **PostgreSQL** (Npgsql) statt SQL Server. Für die Concurrency nimmt myLife die Spalte `xmin` statt `RowVersion`.
- **Interactive WebAssembly** statt Blazor Server, damit die Zero-Knowledge-Zusage hält.
- **Mehrere Nutzer** (Familie) statt eines Hauptnutzers, Entscheidung 34. Es bleibt bei einer Instanz pro Familie, ohne öffentliche Registrierung und ohne Mandanten.
- JS-Interop ist für die Krypto ausdrücklich gewollt. Die Web Crypto API ist dort die sicherere Lösung.
- Datenbankzugriffe laufen nicht über Services in der Komponente. Sie laufen über eine HTTP-API, im Client kapseln typisierte API-Services die Aufrufe.

Alle übrigen Regeln gelten unverändert: MudBlazor-Komponenten, `MudTable` mit Pager oben und unten, Schalter „Auswählen“, Lokalisierung über Ressourcen-Dateien, Soft Delete, Audit-Trail, serverseitige Berechtigungsprüfung.
