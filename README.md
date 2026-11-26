# myLife

**Dein digitaler Notfallordner – selbst gehostet und Ende-zu-Ende-verschlüsselt.**

myLife sammelt an einem Ort alles, was deine Familie im Ernstfall wissen muss: Versicherungen, Konten, Kredite, Verträge, Urkunden, digitale Zugänge, Kontakte und persönliche Wünsche. Solange du lebst, kann niemand die Daten lesen, auch nicht der Betreiber des Servers. Wirst du handlungsunfähig oder stirbst du, erhalten deine Vertrauenspersonen über einen Notfallzugang (Dead-Man's-Switch) genau die Bereiche, die du für sie freigegeben hast.

> **Status:** Version 1.1.0 (Meilensteine M1 bis M3, dazu Hilfe, Zahlungen und Ablageorte). Vor echten Daten fehlt die Abnahme auf der Synology ([Checkliste](docs/betrieb/abnahme.md)). Was sich geändert hat, steht unter [Releases](https://github.com/eckis-corner/myLife/releases).

## Merkmale

- **Zero-Knowledge:** Verschlüsselung im Browser (AES-256-GCM, Argon2id, ECDH P-256), der Server sieht nur Chiffrat
- **Objekte mit Datenblatt:** Bankkonto, Versicherung, Vertrag, Urkunde, Immobilie, Online-Konto … jeweils mit typischen Feldern und beliebigen Dokumenten
- **Ablageorte:** eine Liste der Orte, an denen Originale, Schlüssel und Unterlagen liegen; jeder Eintrag wählt seinen Ablageort daraus
- **Zahlungen:** Übersicht über alle regelmäßigen Zahlungen, je Monat, je Jahr und je Bereich
- **Familie:** jeder Nutzer hat einen eigenen Tresor und benennt andere als Vertrauenspersonen
- **Notfallzugang:** Lebenszeichen per Login oder Mail-Link, gestufte Erinnerungen, Wartezeit, Antrag mit Widerspruch
- **Notfallordner:** verschlüsseltes PDF, druckfertige Ansicht, offenes JSON-Format, Offline-Viewer
- **Self-hosted:** Docker Compose mit PostgreSQL, z. B. auf einer Synology
- **Hilfe in der App:** Tutorial und Anleitung, ohne Anmeldung erreichbar
- Deutsch und Englisch

## Dokumentation

| Dokument | Inhalt |
| --- | --- |
| [docs/architektur.md](docs/architektur.md) | Komponenten, Schlüsselkonzept, Notfallzugang, Datenmodell |
| [docs/objekttypen.md](docs/objekttypen.md) | Bereiche und Objekttypen mit Feldern |
| [docs/meilensteine.md](docs/meilensteine.md) | Umsetzungsplan |
| [docs/kryptoformat.md](docs/kryptoformat.md) | Verschlüsselungsformat Version 1, Byte für Byte |
| [docs/entscheidungen.md](docs/entscheidungen.md) | Entscheidungsprotokoll |
| [docs/format.md](docs/format.md) | Offene Formate: JSON-Export, Notfallpaket, Server-Sicherung |
| [docs/betrieb/installation.md](docs/betrieb/installation.md) | Installation auf Synology mit Nginx Proxy Manager, Updates, Sicherung und Rücksicherung |
| [docs/betrieb/abnahme.md](docs/betrieb/abnahme.md) | Checkliste für die Abnahme auf der Synology vor Version 1.1 |
| [docs/betrieb/nachfolge.md](docs/betrieb/nachfolge.md) | Übergabe an eine Nachfolgeperson, geordnetes Beenden |
| [docs/betrieb/mylife-decrypt.md](docs/betrieb/mylife-decrypt.md) | Exporte und Notfallpakete ohne Server öffnen |
| [PROMPT.md](PROMPT.md) | Ursprüngliche Anforderung |

## Entwickeln

Voraussetzungen: .NET SDK 10, Docker (für PostgreSQL und die Integrationstests).

```bash
docker compose -f docker-compose.yml -f compose.dev.yml up -d db   # PostgreSQL auf localhost:5432 (Passwort in .env)
dotnet run --project src/MyLife.Server       # http://localhost:5062
dotnet test --solution MyLife.slnx           # Tests (nutzen Testcontainers)
python3 tools/resources.py                   # Ressourcen-Dateien de/en neu erzeugen
```

**Ersteinrichtung:** Solange es keinen Nutzer gibt, schreibt der Server einen Einrichtungscode ins Protokoll (Terminal bzw. Debug-Konsole). Der Assistent unter `/setup` fragt danach.

**VS Code:** Mit F5 („myLife (Server)“) startet VS Code die Datenbank, baut den Server, startet ihn im Debugger und öffnet die App unter <http://localhost:5062>. Für Hot Reload gibt es die Aufgabe `watch`. Voraussetzung ist die Erweiterung C# Dev Kit.

Für die lokale Entwicklung erwartet `appsettings.json` PostgreSQL auf `localhost:5432` mit Benutzer und Passwort `mylife`. Andere Werte über `dotnet user-secrets` oder die Umgebungsvariable `ConnectionStrings__MyLife`.

## Projektstruktur

```text
src/
  MyLife.Client   Blazor WebAssembly (MudBlazor), gesamte Oberfläche, Verschlüsselung im Browser
  MyLife.Server   ASP.NET Core: Auslieferung, API, Hintergrunddienste, PostgreSQL
  MyLife.Shared   DTOs, Objekttyp-Schemata
  MyLife.Crypto   Verschlüsselungsformate (C#-Fassade und Referenzimplementierung)
  MyLife.Decrypt  mylife-decrypt: Exporte und Notfallpakete ohne Server öffnen
  MyLife.OfflineViewer  Vorlage des OfflineViewers für das Notfallpaket
tests/
  MyLife.Tests    xUnit, bUnit, Integrationstests mit Testcontainers
```

## Sicherheit

Bitte Sicherheitslücken **nicht** als öffentliches Issue melden, siehe [SECURITY.md](SECURITY.md).

## Lizenz

[GNU Affero General Public License v3.0](LICENSE) – wer myLife verändert als Dienst betreibt, muss den geänderten Quellcode offenlegen.
