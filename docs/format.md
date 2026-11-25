# myLife – Exportformat (JSON), Version 1

Stand: 24.11.2026 (M1.9, Entscheidung 64). Die Bytes der Verschlüsselung beschreibt [kryptoformat.md](kryptoformat.md).

Der JSON-Export enthält das Chiffrat genau so, wie es auf dem Server liegt, und dazu die verpackten Schlüssel. Er lässt sich ohne myLife-Server öffnen, mit

- dem **Master-Passwort zum Zeitpunkt des Exports** (ein späterer Passwortwechsel ändert die Datei nicht) oder
- dem **Recovery-Key** (24 Wörter).

Byte-Felder sind Base64 (mit Auffüllung), Zeitpunkte ISO 8601. Das Werkzeug [mylife-decrypt](betrieb/mylife-decrypt.md) setzt die Schritte unten um und schreibt lesbare Datenblätter und die Dokumente in einen Ordner.

```json
{
  "format": "mylife-export",
  "version": 1,
  "cryptoFormat": 1,
  "createdAt": "2026-11-24T15:00:00+00:00",
  "email": "…",
  "displayName": "…",
  "kdf": { "algorithm": "argon2id", "salt": "…", "memoryKiB": 65536, "iterations": 3, "parallelism": 1 },
  "wrappedVmk": "…",
  "wrappedVmkByRecovery": "…",
  "areas": [ { "area": 1, "wrappedKey": "…" } ],
  "objects": [ { "id": "…", "area": 1, "type": "bankAccount", "schemaVersion": 1, "status": "active", "createdAt": "…", "updatedAt": "…", "payload": "…", "wrappedKey": "… (nur Briefe)" } ],
  "documents": [ { "id": "…", "objectId": "…", "wrappedFileKey": "…", "metadata": "…", "content": "…" } ]
}
```

## Entschlüsseln, Schritt für Schritt

1. **Tresorschlüssel (VMK):**
   - Mit Passwort: `mk = Argon2id(Passwort, kdf.salt, …)`, `kek = HKDF(mk, "mylife/kek")`, VMK = AES-GCM-Entpacken von `wrappedVmk` mit `kek`, Kontext `vmk`.
   - Mit Recovery-Key: die 24 Wörter nach BIP-39 in 32 Byte wandeln, `rkKey = HKDF(rk, "mylife/recovery")`, VMK = Entpacken von `wrappedVmkByRecovery`, Kontext `vmk`.
2. **Bereichsschlüssel:** `areas[].wrappedKey` mit dem VMK entpacken, Kontext `area:{area}`.
3. **Objekte:** `payload` mit dem Bereichsschlüssel entschlüsseln, Kontext `object:{type}:{id}`. Ergebnis ist JSON (siehe unten). Hat ein Objekt `wrappedKey` (Briefe, seit M2.3), zuerst diesen Objektschlüssel mit dem Bereichsschlüssel entpacken (Kontext `object-key:{id}`) und damit `payload` und die Dateischlüssel seiner Dokumente öffnen.
4. **Dokumente:**
   - `wrappedFileKey` mit dem Bereichsschlüssel des Objekts entpacken (bei Briefen mit dem Objektschlüssel), Kontext `file-key:{id}`.
   - `metadata` mit demselben Schlüssel entschlüsseln, Kontext `document-meta:{id}`.
   - `content` blockweise entschlüsseln: Blöcke zu je 1 MiB + 16 Byte, der letzte kürzer, Kontext `document:{id}` (kryptoformat.md, Abschnitt 4). Bei einem Export ohne Dokumente fehlt `content`.

## Inhalt eines Objekts

```json
{
  "schemaVersion": 1,
  "title": "Girokonto Sparkasse",
  "fields": { "bank": "Sparkasse", "iban": "DE89370400440532013000", "accountKind": "checking" },
  "wish": "close",
  "wishNote": "…",
  "contactId": "…",
  "links": ["…"],
  "originalLocation": "…",
  "expiresOn": "2030-12-31",
  "notes": "…",
  "customFields": [ { "label": "…", "value": "…" } ],
  "reviewedOn": "2026-11-24",
  "followUpOn": "2027-01-15",
  "followUpNote": "…"
}
```

- `fields` enthält nur ausgefüllte Felder. Schlüssel und Auswahlwerte stehen in `src/MyLife.Shared/Vault/ObjectCatalog.cs`, ihre Bezeichnungen in `tools/resources_vault.py`.
- Fehlt ein Wert (etwa keine Wiedervorlage), fehlt auch der Schlüssel.
- Werte: Zahlen und Beträge invariant mit Punkt, Datum `yyyy-MM-dd`, Ja/Nein als `true`/`false`, Verweise als ID eines anderen Objekts.

## Metadaten eines Dokuments

```json
{ "label": "Police 2026", "fileName": "police.pdf", "contentType": "application/pdf", "size": 123456, "date": "2026-01-15" }
```

## Notfallpaket (seit M2.7)

Das Notfallpaket ist eine einzelne HTML-Datei: der OfflineViewer (`src/MyLife.OfflineViewer/OfflineViewer.html`) mit
zwei eingebetteten JSON-Blöcken. Es braucht keinen Server und kein Netz. Geöffnet wird es mit den 24 Wörtern vom
gedruckten Schlüsselblatt.

- `<script type="application/json" id="mylife-words">`: die englische BIP-39-Wortliste (2048 Wörter).
- `<script type="application/json" id="mylife-package">`: der Kopf mit dem Chiffrat.

```json
{ "format": "mylife-package", "version": 1, "cryptoFormat": 1, "language": "de", "createdAt": "2026-11-25T10:00:00+01:00", "data": "…" }
```

**Entschlüsseln:**

1. Die 24 Wörter nach BIP-39 in 32 Byte `pk` wandeln und die Prüfsumme prüfen (erstes Byte von SHA-256).
2. `pkKey = HKDF-SHA-256(pk, info "mylife/package")`, siehe [kryptoformat.md](kryptoformat.md).
3. `data` (Base64) ist ein Datensatz der Art `Data` mit dem Kontext `package`.

Der Klartext ist JSON. Datenblätter stehen darin fertig beschriftet in der Sprache beim Erzeugen, damit der Viewer kein
Schema kennen muss:

```json
{
  "format": "mylife-package-content", "version": 1, "owner": "Daniel", "createdAt": "…", "language": "de",
  "areas": [ {
    "area": 1, "title": "Bankkonten & Depots", "text": "…",
    "items": [ {
      "title": "Girokonto", "kind": "Bankkonto",
      "rows": [ { "label": "IBAN", "value": "DE89 3704 0044 0532 0130 00" }, { "label": "Notizen", "value": "…", "multiline": true } ],
      "links": ["…"],
      "documents": [ { "label": "Kontoauszug", "fileName": "auszug.pdf", "contentType": "application/pdf", "size": 12345, "data": "… (Base64, Klartext der Datei)" } ]
    } ]
  } ]
}
```

## Server-Sicherung (seit M3.4)

Die tägliche Sicherung des Servers (`data/app/backup/mylife-<Datum>-<Uhrzeit>.mlbak`) ist für das Zurückspielen mit myLife gedacht (`restore`, siehe [installation.md](betrieb/installation.md), Abschnitt 8), nicht zum Lesen. Aufbau:

```text
"MYLIFEBK" | Version 0x01 | Kopflänge (uint32 BE) | Kopf (JSON: format, version, createdAt, appVersion, salt)
Blöcke:    AES-256-GCM, Klartext je 1 MiB (der letzte kürzer), jeweils Chiffrat | Tag (16)
  Schlüssel = HKDF-SHA-256(MYLIFE_BACKUP_KEY, salt, info "mylife/backup")
  Nonce     = 00 × 8 | uint32 BE (Blocknummer)
  AAD       = "mylife-backup" | SHA-256(Kopf) | uint32 BE (Blocknummer) | letzter Block (0x00/0x01)
Klartext:  Einträge Art (1 Byte) | Namenslänge (uint16 BE) | Name (UTF-8) | Länge (int64 BE) | Daten
  Art 1 Inhaltsverzeichnis (JSON: Migrationsstand, Tabellen in Ladereihenfolge, Zahl der Dokumente und Schlüssel)
  Art 2 Tabelle als PostgreSQL-COPY im Binärformat, Art 3 Dokument (Chiffrat wie in data/app/files), Art 4 Datei des Schlüsselrings
  Art 0 Ende
```

Die Tresorinhalte bleiben auch in der Sicherung mit den Schlüsseln der Nutzer verschlüsselt; wer sie lesen will, braucht zusätzlich deren Passwort oder Recovery-Kit.
