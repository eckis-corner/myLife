# Installation auf einer Synology mit Nginx Proxy Manager

Getestet für DSM 7.2 und neuer mit **Container Manager** (Synology DS1520+, x86-64). Für andere Docker-Hosts gelten dieselben Schritte sinngemäß.

> **Stand Version 1.1:** Einrichtung, Anmeldung, alle Objekttypen, Dokumente, Suche, Vollständigkeitsanzeige, Erinnerungen per Mail, die Checkliste mit Vorlage für Deutschland, Notfallordner-PDF, JSON-Export, Notfallpaket mit OfflineViewer, Protokoll, die Verwaltung mit Mailversand, Einladungen, Vertrauenspersonen, „Sofort teilen“, der Notfallzugang mit Rücknahme und gemeinsamem Löschen der Lesemodus für Hinterbliebene, die Hilfe (Tutorial und Anleitung), die Übersicht „Zahlungen“ und die Tabelle „Ablageorte“ funktionieren. Mehr als ein Admin ist empfehlenswert: Den Tresor des einzigen Admins können die Vertrauenspersonen nicht löschen. Der Notfallzugang braucht einen funktionierenden Mailversand und die Adresse der Instanz (Verwaltung → Einstellungen). Vor echten Daten fehlt die Abnahme auf der DS1520+ ([abnahme.md](abnahme.md)). Dokumente liegen verschlüsselt unter `/data/files`; myLife sichert täglich nach `/data/backup`, wenn `MYLIFE_BACKUP_KEY` gesetzt ist (Abschnitt 8).

## 1. Ordner anlegen

In der File Station im freigegebenen Ordner `docker` den Ordner `mylife` anlegen und darin:

```text
/volume1/docker/mylife/
├── docker-compose.yml     ← aus dem Repository
├── .env                   ← aus .env.example, angepasst
└── data/
    ├── app/               ← Dokumente, Schlüssel, Sicherungen der App
    └── postgres/          ← Datenbank
```

`docker-compose.yml` und `.env.example` liegen im Repository im Hauptverzeichnis. Lade sie herunter und benenne `.env.example` in `.env` um.

## 2. `.env` anpassen

| Variable | Wert |
| --- | --- |
| `POSTGRES_PASSWORD` | Langes Zufallspasswort, z. B. per SSH `openssl rand -hex 24`. Danach nicht mehr ändern. |
| `MYLIFE_VERSION` | `latest` oder eine feste Version |
| `MYLIFE_PORT` | Freier Port auf der Synology, z. B. `8089` |
| `MYLIFE_DATA_DIR` | `./data` (relativ zum Projektordner) |
| `MYLIFE_BACKUP_KEY` | Schlüssel der täglichen Sicherung: per SSH `openssl rand -base64 32`. **Getrennt von der NAS aufbewahren** (ausdrucken, beim Notfallordner); ohne ihn lässt sich keine Sicherung zurückspielen. Leer = keine automatische Sicherung. |
| `TZ` | `Europe/Berlin` |

## 3. Rechte für den App-Ordner setzen (einmalig, per SSH)

Die App läuft im Container aus Sicherheitsgründen nicht als root, sondern als Benutzer mit der UID **1654**. Der Ordner `data/app` muss ihm gehören:

```bash
sudo mkdir -p /volume1/docker/mylife/data/app /volume1/docker/mylife/data/postgres
sudo chown -R 1654:1654 /volume1/docker/mylife/data/app
```

(SSH aktivieren: Systemsteuerung → Terminal & SNMP → SSH-Dienst aktivieren.)

## 4. Projekt im Container Manager anlegen

1. **Container Manager → Projekt → Erstellen**
2. Projektname `mylife`, Pfad `/volume1/docker/mylife`
3. Quelle: **Vorhandene docker-compose.yml verwenden**
4. Web-Portal-Einstellungen überspringen (das übernimmt der Nginx Proxy Manager)
5. Fertigstellen. Der Container Manager lädt `ghcr.io/eckis-corner/mylife` und `postgres:17-alpine` und startet beide.

Kontrolle im LAN: `http://<IP-der-Synology>:8089/health/ready` liefert `Healthy`.

> Meldet der Download des Images `unauthorized`, ist das Paket auf GitHub noch privat: GitHub → Profil → Packages → `mylife` → Package settings → Change visibility → Public.

## 5. Nginx Proxy Manager

**Hosts → Proxy Hosts → Add Proxy Host**

| Reiter | Einstellung |
| --- | --- |
| Details | Domain Names: `mylife.eckis-corner.de` · Scheme: `http` · Forward Hostname/IP: IP der Synology · Forward Port: `8089` · *Block Common Exploits* an · *Websockets Support* an |
| SSL | *Request a new SSL Certificate* (Let's Encrypt) · *Force SSL* an · *HTTP/2 Support* an · *HSTS Enabled* an |
| Advanced | `client_max_body_size 30m;` – Dokumente sind bis 25 MB groß, verschlüsselt etwas mehr. Ohne diese Zeile lehnt nginx größere Uploads ab. |

Die App nimmt `X-Forwarded-*`-Header nur aus privaten Netzen an (10.x, 172.16–31.x, 192.168.x). Läuft der Proxy in einem anderen Netz, dieses zusätzlich über die Umgebungsvariable `MyLife__TrustedProxyNetworks__5=<Netz/Präfix>` in der Compose-Datei eintragen.

Danach `https://mylife.eckis-corner.de` öffnen. Ohne Konto startet der Einrichtungsassistent (Abschnitt 6).

## 6. Ersteinrichtung

Beim ersten Aufruf von `https://mylife.eckis-corner.de` startet der Einrichtungsassistent. Er fragt nach dem **Einrichtungscode**. Der Server schreibt ihn beim Start ins Protokoll:

**Container Manager → Container → `mylife-app-1` → Protokoll** (oder per SSH `sudo docker logs mylife-app-1`), Zeile:

```text
myLife ist noch nicht eingerichtet. Einrichtungscode für den Assistenten: ABCD-EFGH-JKLM-NPQR-STUV
```

Nach jedem Neustart des Containers gilt ein neuer Code. Sobald das erste Konto angelegt ist, gibt es keinen Code mehr und der Assistent ist gesperrt.

Der Assistent führt durch Konto, Master-Passwort, Wiederherstellungs-Kit (ausdrucken!) und die Authenticator-App. Die Schlüssel entstehen im Browser; das Anlegen dauert einige Sekunden.

### Mailversand einrichten

Nach der Anmeldung als Administrator: **Verwaltung → Einstellungen**.

1. Reiter **Allgemein:** *Adresse der Instanz* `https://mylife.eckis-corner.de` eintragen. Sie steht in den Links der Mails.
2. Reiter **E-Mail:** SMTP-Server, Port, Verschlüsselung (STARTTLS auf 587 oder SSL/TLS auf 465), Benutzername, Passwort und Absenderadresse eintragen, dann **Speichern und Testmail senden**. Die Testmail geht an deine eigene Adresse; ein Fehler erscheint sofort mit der Meldung des Mailservers.
3. Optional Reiter **ntfy:** Server (etwa `https://ntfy.sh`) und Zugriffstoken für Push-Nachrichten.

Das SMTP-Passwort liegt verschlüsselt mit den Schlüsseln aus `data/app/keys` in der Datenbank. Ohne diesen Ordner muss es nach einer Rücksicherung neu eingegeben werden.

## 7. Updates

Vor jedem Update in der Verwaltung unter **Einstellungen → Sicherung** auf **Jetzt sichern** klicken.

Container Manager → Projekt `mylife` → **Aktion → Stoppen**, dann **Image** → `ghcr.io/eckis-corner/mylife` → **Aktualisieren**, dann das Projekt wieder **starten**. Alternativ per SSH:

```bash
cd /volume1/docker/mylife
sudo docker compose pull
sudo docker compose up -d
```

Datenbank-Migrationen laufen beim Start automatisch. Danach kurz anmelden und unter **Verwaltung** nachsehen, ob Mailversand und Sicherung keinen Fehler zeigen.

- **Feste Version:** Statt `latest` lässt sich in der `.env` eine Version wie `MYLIFE_VERSION=1.1.0` eintragen; dann kommt ein Update nur, wenn du die Zahl änderst. Was sich geändert hat, steht unter **Releases** auf GitHub.
- **Von 1.0.0 auf 1.1.0:** Es gibt keine Datenbank-Migration, es genügt, das Image zu tauschen. Neu ist der Bereich **Ablageorte** (Bereich 14). Bestehende Freigaben für Vertrauenspersonen enthalten ihn nicht. Die Namen der Ablageorte sehen sie trotzdem bei jedem freigegebenen Eintrag. Wer auch Beschreibung, Anschrift und Zugang sehen soll, braucht unter **Vertrauenspersonen** die Freigabe dieses Bereichs.
- **Zurück auf die alte Version:** Migrationen laufen nur vorwärts. Also `MYLIFE_VERSION` auf die alte Version setzen und die Sicherung von vor dem Update zurückspielen (Abschnitt 8).

## 8. Sicherung

### Was myLife selbst sichert

Ist `MYLIFE_BACKUP_KEY` gesetzt, legt myLife **jeden Tag** zur eingestellten Uhrzeit (Standard 3 Uhr) eine verschlüsselte Sicherung unter `data/app/backup/mylife-<Datum>-<Uhrzeit>.mlbak` ab:

- die **Datenbank** als konsistenter Schnappschuss,
- den **Schlüsselring** aus `data/app/keys` (schützt TOTP-Geheimnisse und SMTP-Passwort),
- auf Wunsch die **Dokumente** aus `data/app/files`.

Aufbewahrt werden die Generationen: die neueste Sicherung je Tag (7), je Woche (4) und je Monat (6). Uhrzeit, Generationen und ob Dokumente mitkommen, stellt der Admin unter **Verwaltung → Einstellungen → Sicherung** ein. Dort stehen auch die letzte Sicherung, der nächste Lauf, ein Fehler und der Knopf **Jetzt sichern**. Schlägt eine Sicherung fehl, bekommen die Admins eine Mail (höchstens einmal am Tag).

Die Inhalte der Tresore sind ohnehin mit den Schlüsseln der Nutzer verschlüsselt; der Sicherungsschlüssel schützt zusätzlich alles Übrige, etwa Mail-Adressen und den Schlüsselring.

### Hyper Backup

Den **gesamten Ordner** `/volume1/docker/mylife` (inklusive `.env` und `data/`) in **Hyper Backup** aufnehmen, am besten auf ein Ziel außerhalb der NAS. Damit sind die Sicherungen aus `data/app/backup` an einem zweiten Ort. Sichert Hyper Backup den Ordner `data/app/files` ohnehin, kann der Admin „Dokumente in jede Sicherung aufnehmen“ abschalten; die Sicherungen bleiben dann klein.

Die laufende Datenbank (`data/postgres`) ist in Hyper Backup nicht unbedingt konsistent; zurückgespielt wird deshalb aus einer myLife-Sicherung.

### Zurückspielen

Die Rücksicherung **ersetzt die gesamte Datenbank** und, soweit in der Sicherung enthalten, die Dokumente und den Schlüsselring.

1. In Container Manager das Projekt **stoppen** (die Datenbank läuft weiter oder wird mit gestartet).
2. Per SSH im Projektordner:

   ```bash
   cd /volume1/docker/mylife
   sudo docker compose up -d db
   sudo docker compose run --rm app restore /data/backup/mylife-20261125-030000.mlbak --confirm
   ```

   Ohne `--confirm` passiert nichts. Die Sicherung muss mit demselben `MYLIFE_BACKUP_KEY` erstellt worden sein. Sie lässt sich mit derselben oder einer neueren myLife-Version zurückspielen; ausstehende Migrationen laufen danach automatisch.
3. Das Projekt wieder **starten** und sich anmelden.

Auf einer neuen NAS: zuerst wie oben installieren (dieselbe `.env` mit demselben Sicherungsschlüssel), die Sicherungsdatei nach `data/app/backup` kopieren, dann Schritt 2.

Weitere Anleitungen: [Übergabe an eine Nachfolgeperson](nachfolge.md) und [mylife-decrypt](mylife-decrypt.md) zum Öffnen von Exporten ohne Server.

Das Format beschreibt [format.md](../format.md), Abschnitt „Server-Sicherung“, und der Quelltext in `src/MyLife.Server/Infrastructure/Backup/BackupFormat.cs`: AES-256-GCM in Blöcken zu 1 MiB, Schlüssel aus `MYLIFE_BACKUP_KEY` und einem Salz je Sicherung (HKDF).

## 9. Lokale Entwicklung und eigener Build

```bash
cp .env.example .env        # Passwort setzen
docker compose -f docker-compose.yml -f compose.dev.yml up -d --build
```
