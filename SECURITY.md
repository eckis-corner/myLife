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

## Unterstützte Versionen

Bis zur Version 1.0 wird nur der jeweils aktuelle Stand des Branches `main` gepflegt.
