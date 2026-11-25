# mylife-decrypt – Daten ohne myLife öffnen

`mylife-decrypt` ist ein kleines Programm für Windows, macOS und Linux. Es öffnet myLife-Daten **ohne Server und ohne Internet**:

| Was | Womit |
| --- | --- |
| **JSON-Export** (Export & Druck → Datensicherung (JSON), Datei `myLife-Export-….mylife.json`) | Master-Passwort zum Zeitpunkt des Exports **oder** die 24 Wörter des Recovery-Kits |
| **Notfallpaket** (Export & Druck → Notfallpaket erstellen, Datei `myLife-Notfallpaket-….html`) | die 24 Wörter vom gedruckten Schlüsselblatt |

Heraus kommt ein Ordner mit einer Übersicht (`index.txt`), je Bereich einem Unterordner, je Eintrag einem lesbaren Datenblatt (`.txt`) und den Originaldokumenten (PDF, Fotos …). Beim JSON-Export liegt je Eintrag zusätzlich die vollständige JSON-Fassung daneben.

Ein Notfallpaket lässt sich auch ganz ohne dieses Programm öffnen: Doppelklick auf die HTML-Datei, die 24 Wörter eingeben. `mylife-decrypt` ist der zweite Weg, etwa um alle Dokumente auf einmal als Dateien zu bekommen.

## Herunterladen

Auf GitHub unter **Releases** des Projekts `eckis-corner/myLife` liegt für jede Version eine ZIP-Datei je System:

| System | Datei |
| --- | --- |
| Windows | `mylife-decrypt-win-x64.zip` |
| Mac mit Apple-Chip (M1 und neuer) | `mylife-decrypt-osx-arm64.zip` |
| Mac mit Intel-Chip | `mylife-decrypt-osx-x64.zip` |
| Linux | `mylife-decrypt-linux-x64.zip` |

Das Programm braucht keine Installation und kein .NET. Am besten eine Kopie zusammen mit dem Notfallordner aufbewahren (USB-Stick), falls GitHub einmal nicht erreichbar ist.

## Benutzen

ZIP entpacken, dann im selben Ordner eine Eingabeaufforderung (Windows: `cmd`) bzw. das Terminal (Mac, Linux) öffnen:

```text
mylife-decrypt export  myLife-Export-2026-11-25.mylife.json  Daten-Daniel
mylife-decrypt package myLife-Notfallpaket-2026-11-25.html   Paket-Daniel
```

Auf dem Mac und unter Linux `./mylife-decrypt` schreiben. Das Programm fragt nach dem Passwort bzw. den 24 Wörtern; die Eingabe ist nicht sichtbar. Bei den 24 Wörtern genügen die ersten vier Buchstaben jedes Worts, Nummern stören nicht. Mit `--en` sind die Beschriftungen englisch.

**Mac:** Weil das Programm nicht von Apple signiert ist, blockiert macOS es beim ersten Start. Abhilfe im Terminal: `xattr -d com.apple.quarantine mylife-decrypt`, oder in den Systemeinstellungen unter „Datenschutz & Sicherheit“ „Trotzdem öffnen“ wählen.

## Sicherheit

- Das Programm arbeitet nur auf dem eigenen Rechner und schickt nichts weg.
- Der Ergebnisordner ist **unverschlüsselt**. Er gehört nicht in eine Cloud oder auf einen fremden Rechner; nach Gebrauch löschen oder auf einem verschlüsselten Datenträger ablegen.
- Ohne Passwort, Recovery-Kit oder Schlüsselblatt lässt sich nichts öffnen. Das ist gewollt: Auch wer die Datei findet, liest nichts.

## Für Entwickler

Aus dem Quelltext: `dotnet run --project src/MyLife.Decrypt -- export <Datei> <Ordner>`. Das Geheimnis kann statt der Abfrage in der Umgebungsvariablen `MYLIFE_SECRET` stehen. Das Format beschreiben [format.md](../format.md) und [kryptoformat.md](../kryptoformat.md); die CI baut die Dateien für alle Systeme (Job `decrypt-tool`).
