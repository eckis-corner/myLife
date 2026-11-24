# myLife – Verschlüsselungsformat, Version 1

Stand: 24.11.2026 (M1.1). Ergänzt [architektur.md](architektur.md), Abschnitt 4. Dieses Dokument beschreibt die Bytes so genau, dass sich jedes Chiffrat auch ohne myLife entschlüsseln lässt.

Umsetzung an zwei Stellen, die byte-genau übereinstimmen müssen:

| Ort | Zweck |
| --- | --- |
| `src/MyLife.Client/wwwroot/js/mylife.crypto.js` | Browser, Web Crypto API und Argon2id über hash-wasm 4.13.0 (lokal unter `wwwroot/lib/hash-wasm/`) |
| `src/MyLife.Crypto/ReferenceMyLifeCrypto.cs` | .NET-Referenz für Tests und das spätere Werkzeug `mylife-decrypt` |
| `src/MyLife.Crypto/JsMyLifeCrypto.cs` | C#-Fassade über das JS-Modul, gemeinsame Schnittstelle `IMyLifeCrypto` |

## 1. Grundregeln

- Jeder Datensatz beginnt mit zwei Bytes: **Version** (`0x01`) und **Art**.
- **Kontext:** Jede Operation bekommt eine Zeichenkette, die den Zweck festhält, etwa `vmk`, `area:3`, `object:bank_account:<id>` oder `grant:<id>:death`. Sie steht als UTF-8 in der AAD bzw. in der signierten Nachricht. Ein Chiffrat lässt sich deshalb nicht in einen anderen Zusammenhang verschieben. Der Kontext darf nicht leer sein und höchstens 256 Byte UTF-8 lang.
- **Schlüssel verlassen das Modul nicht.** Aufrufer bekommen Handles. Im Browser sind alle Schlüssel im Register nicht exportierbar. Zum Neuverpacken oder für einen Umschlag entpackt das Modul den Schlüssel kurz als exportierbar aus seiner verpackten Form und verwirft ihn sofort wieder.
- Fehlermeldungen haben die Form `mylife:<code>`, etwa `mylife:decrypt-failed`, und enthalten keine Daten.

| Art | Wert | Inhalt |
| --- | ---: | --- |
| Data | `0x01` | Objekt-JSON und andere Daten |
| WrappedKey | `0x02` | verpackter AES-256-Schlüssel (32 Byte roh) |
| WrappedEcdhKey | `0x03` | verpackter privater ECDH-Schlüssel (PKCS#8) |
| WrappedEcdsaKey | `0x04` | verpackter privater ECDSA-Schlüssel (PKCS#8) |
| Envelope | `0x05` | Umschlag (ECIES) mit einem AES-256-Schlüssel |
| FileChunk | `0x06` | nur in der AAD von Dateiblöcken |
| Signature | `0x07` | nur in der signierten Nachricht |

Die Art steht auch in der AAD. Ein verpackter ECDH-Schlüssel lässt sich deshalb nicht als ECDSA-Schlüssel und ein verpackter Schlüssel nicht als Daten entschlüsseln.

## 2. Ableitung aus dem Passwort

```text
pw     = UTF-8(NFC(Master-Passwort))
mk     = Argon2id(pw, salt, m, t, p, Länge 32)          Version 0x13, ohne Secret und AD
auth   = HKDF-SHA-256(ikm = mk, salt = leer, info = "mylife/auth", 32)     → an den Server
kek    = HKDF-SHA-256(ikm = mk, salt = leer, info = "mylife/kek", 32)      → AES-256, nur wrap/unwrap
rk     = 32 Zufallsbytes (Recovery-Key, 24 Wörter)
rkKey  = HKDF-SHA-256(ikm = rk, salt = leer, info = "mylife/recovery", 32) → AES-256, nur wrap/unwrap
rkAuth = HKDF-SHA-256(ikm = rk, salt = leer, info = "mylife/recovery-auth", 32) → an den Server (Prüfwert bei der Wiederherstellung, seit M1.3)
```

- Standard: m = 65536 KiB (64 MiB), t = 3, p = 1, Salt 16 Byte. Erlaubt sind m 64 MiB bis 1 GiB, t 3 bis 20, p 1 bis 8 und ein Salt von 16 bis 64 Byte. Die **Untergrenzen** prüft der Browser, damit ein manipulierter Server keine schwachen Parameter unterschieben kann, die das Erraten des Passworts aus dem Anmeldehash erleichtern.
- Das Passwort wird nach Unicode NFC normalisiert. „ü“ als ein Zeichen und „u“ + Trema ergeben denselben Schlüssel.

## 3. AES-256-GCM (Daten und verpackte Schlüssel)

```text
Datensatz = 0x01 | Art | Nonce (12) | Chiffrat | Tag (16)
AAD       = 0x01 | Art | UTF-8(Kontext)
```

- Nonce: 12 Zufallsbytes je Verschlüsselung.
- Verpackte AES-Schlüssel (Art `0x02`): Klartext sind die 32 Rohbytes, Gesamtlänge 62 Byte.
- Verpackte private Schlüssel (Art `0x03`/`0x04`): Klartext ist PKCS#8 (P-256).
- Wer verpacken darf: KEK und Recovery-Schlüssel nur verpacken; Tresor-, Bereichs-, Brief- und Dateischlüssel verschlüsseln **und** verpacken (der VMK verpackt Bereichsschlüssel und private Schlüssel, ein Bereichsschlüssel verpackt Dateischlüssel).

## 4. Dokumente in Blöcken

```text
Block i   = Chiffrat | Tag (16)                    Klartext höchstens 1 MiB
Nonce     = 00 00 00 00 00 00 00 00 | uint32 BE (i)
AAD       = 0x01 | 0x06 | uint32 BE (i) | letzter Block (0x00/0x01) | UTF-8(Kontext)
```

- Weil die Nonce aus der Blocknummer kommt, gehört ein Dateischlüssel zu **genau einer Datei**. Jede neue oder geänderte Datei bekommt einen neuen Schlüssel.
- Vertauschte Blöcke fallen über die Nummer auf, eine abgeschnittene Datei über die Markierung des letzten Blocks.
- Den Dateikopf (Blockgröße, Metadaten) legt M1.6 fest.

## 5. Umschläge (ECIES, P-256)

```text
e, E      = ephemeres Schlüsselpaar P-256
Z         = ECDH(e, Empfänger-öffentlich)          x-Koordinate, 32 Byte
K         = HKDF-SHA-256(ikm = Z, salt = E (65 Byte), info = "mylife/ecies", 32)
Umschlag  = 0x01 | 0x05 | E (65) | Nonce (12) | AES-256-GCM(K, Schlüssel 32 Byte) | Tag (16)   = 127 Byte
AAD       = 0x01 | 0x05 | UTF-8(Kontext)
```

Öffentliche Schlüssel sind unkomprimierte Punkte (`0x04 | X | Y`, 65 Byte). Ungültige Punkte werden abgelehnt.

## 6. Signaturen (ECDSA P-256, SHA-256)

```text
Nachricht = 0x01 | 0x07 | uint32 BE (Länge Kontext) | UTF-8(Kontext) | Daten
Signatur  = r | s (IEEE P1363, 64 Byte)
```

## 7. Weitere Bausteine

- `sha256(daten)`, etwa für den Fingerprint eines öffentlichen Schlüssels.
- `hmacSha256(schlüssel ≥ 16 Byte, daten)` für die Bindung an das Einladungsgeheimnis (Architektur 4.3).

## 8. Tests

| Test | Prüft |
| --- | --- |
| `ReferenceCryptoTests` | Argon2id gegen RFC 9106 (5.3), NFC, Parametergrenzen, Aufbau, Manipulation, falscher Kontext, Schlüsselarten, Recovery, Neuverpacken, Blöcke, Umschläge, Signaturen |
| `CryptoCrossTests` | Das Browser-Modul läuft in Node.js gegen die .NET-Vektoren, .NET prüft alles, was JavaScript erzeugt hat |
| `tests/MyLife.Tests/Crypto/vectors-v1.json` | Fester Vektorsatz. Er hält das Format fest: Ändert sich ein Byte im Format, schlägt der Test fehl, und alte Tresore wären unlesbar. Nur bewusst neu erzeugen. |
| `JsMyLifeCryptoTests` | Fassade: jede aufgerufene Funktion existiert im Modul, Fehler werden zu `CryptographicException` |

Die Kreuztests brauchen Node.js ab Version 20. Fehlt Node lokal, werden sie übersprungen, in der CI (Variable `CI`) schlagen sie fehl.
