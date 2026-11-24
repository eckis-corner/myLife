# myLife – Exportformat (JSON), Version 1

Stand: 24.11.2026 (M1.9, Entscheidung 64). Die Bytes der Verschlüsselung beschreibt [kryptoformat.md](kryptoformat.md).

Der JSON-Export enthält das Chiffrat genau so, wie es auf dem Server liegt, und dazu die verpackten Schlüssel. Er lässt sich ohne myLife-Server öffnen, mit

- dem **Master-Passwort zum Zeitpunkt des Exports** (ein späterer Passwortwechsel ändert die Datei nicht) oder
- dem **Recovery-Key** (24 Wörter).

Byte-Felder sind Base64 (mit Auffüllung), Zeitpunkte ISO 8601.

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
