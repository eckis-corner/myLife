# Installation auf einer Synology mit Nginx Proxy Manager

Getestet für DSM 7.2 und neuer mit **Container Manager** (Synology DS1520+, x86-64). Für andere Docker-Hosts gelten dieselben Schritte sinngemäß.

> **Stand M1.3:** Einrichtung, Anmeldung mit zweitem Faktor (TOTP oder Passkey), Passwort ändern und Wiederherstellung funktionieren. Die Tresorinhalte folgen. Bitte noch keine echten Daten erfassen.

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

Die App nimmt `X-Forwarded-*`-Header nur aus privaten Netzen an (10.x, 172.16–31.x, 192.168.x). Läuft der Proxy in einem anderen Netz, dieses zusätzlich über die Umgebungsvariable `MyLife__TrustedProxyNetworks__5=<Netz/Präfix>` in der Compose-Datei eintragen.

Danach `https://mylife.eckis-corner.de` öffnen. Die Startseite zeigt „Server erreichbar“ und die Version.

## 6. Ersteinrichtung

Beim ersten Aufruf von `https://mylife.eckis-corner.de` startet der Einrichtungsassistent. Er fragt nach dem **Einrichtungscode**. Der Server schreibt ihn beim Start ins Protokoll:

**Container Manager → Container → `mylife-app-1` → Protokoll** (oder per SSH `sudo docker logs mylife-app-1`), Zeile:

```text
myLife ist noch nicht eingerichtet. Einrichtungscode für den Assistenten: ABCD-EFGH-JKLM-NPQR-STUV
```

Nach jedem Neustart des Containers gilt ein neuer Code. Sobald das erste Konto angelegt ist, gibt es keinen Code mehr und der Assistent ist gesperrt.

Der Assistent führt durch Konto, Master-Passwort, Wiederherstellungs-Kit (ausdrucken!) und die Authenticator-App. Die Schlüssel entstehen im Browser; das Anlegen dauert einige Sekunden.

## 7. Updates

Container Manager → Projekt `mylife` → **Aktion → Stoppen**, dann **Image** → `ghcr.io/eckis-corner/mylife` → **Aktualisieren**, dann das Projekt wieder **starten**. Alternativ per SSH:

```bash
cd /volume1/docker/mylife
sudo docker compose pull
sudo docker compose up -d
```

Datenbank-Migrationen laufen beim Start automatisch.

## 8. Sicherung

Bis zum Meilenstein M3 (automatische, konsistente Sicherung per `pg_dump`) gilt:

- Den **gesamten Ordner** `/volume1/docker/mylife` (inklusive `.env` und `data/`) in **Hyper Backup** aufnehmen.
- Für eine konsistente Datenbanksicherung das Projekt vor der Sicherung kurz stoppen oder die Sicherung zu einer ruhigen Zeit planen.
- **`data/app/keys`** enthält die Schlüssel, mit denen der Server Einstellungen wie das SMTP-Passwort schützt. Ohne diesen Ordner sind diese Einstellungen nach einer Rücksicherung neu einzugeben. Die Inhalte der Tresore hängen **nicht** davon ab, sie sind mit den Schlüsseln der Nutzer verschlüsselt.

## 9. Lokale Entwicklung und eigener Build

```bash
cp .env.example .env        # Passwort setzen
docker compose -f docker-compose.yml -f compose.dev.yml up -d --build
```
