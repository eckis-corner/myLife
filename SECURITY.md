# Sicherheitsrichtlinie

myLife verwaltet hochsensible Daten. Sicherheitsmeldungen haben höchste Priorität.

## Lücken melden

Bitte melde Schwachstellen **vertraulich** über [GitHub Security Advisories](../../security/advisories/new) dieses Repositorys und **nicht** als öffentliches Issue. Hilfreich sind:

- betroffene Version oder Commit
- Beschreibung und Auswirkung
- Schritte zum Nachvollziehen

Wir bestätigen den Eingang so schnell wie möglich und stimmen eine Veröffentlichung mit dir ab.

## Sicherheitsmodell in Kürze

- Ver- und Entschlüsselung ausschließlich im Browser; der Server speichert nur Chiffrat (Zero-Knowledge).
- Bekannte Grenzen sind in [docs/architektur.md](docs/architektur.md) beschrieben, insbesondere: Der Server liefert den Code aus, der im Browser entschlüsselt; ein kompromittierter Server könnte manipulierten Code ausliefern.

## Was automatisch geprüft wird

Die Tests (`dotnet test`, auch in der CI) halten fest:

- **Anmeldung:** Jeder Endpunkt unter `/api` verlangt eine Anmeldung, außer einer fest im Test hinterlegten Liste (Anmeldung, Einrichtung, Einladung, Links aus Mails mit Einmal-Token, Info, Datenschutz). Ein neuer Endpunkt ohne Schutz lässt den Test scheitern. Die Verwaltung verlangt die Rolle Admin.
- **Header:** strikte Content-Security-Policy (keine Inline-Skripte, kein Nachladen fremder Quellen, `frame-ancestors 'none'`), `nosniff`, `no-referrer`, Cross-Origin-Isolation, kein `Server`-Header, API-Antworten ohne Cache.
- **Mitgelieferte Bibliotheken:** Die Prüfsumme von hash-wasm (Argon2id im Browser) muss zur Angabe in `VERSION` passen.
- **Krypto:** Kreuztests zwischen Browser-Modul und .NET-Referenz, feste Testvektoren, Manipulationen werden erkannt.
- **Notfallpaket:** Der OfflineViewer lädt nichts nach und setzt Inhalte nur als Text.

Abhängigkeiten mit bekannten Schwachstellen zeigt `dotnet list MyLife.slnx package --vulnerable --include-transitive`.

## Unterstützte Versionen

Gepflegt wird die jeweils neueste Version (Tag `v*`) und der Stand von `main`. Sicherheitskorrekturen kommen als neue Version; ein Update ist in [docs/betrieb/installation.md](docs/betrieb/installation.md) beschrieben.
