# Abnahme auf der Synology (vor Version 1.0)

Checkliste für die Abnahme von M1 und M2 auf der DS1520+ (Meilensteine). Zum Abhaken: `[ ]` durch `[x]` ersetzen. Wenn etwas nicht wie beschrieben funktioniert: notieren, was du getan hast und was passiert ist, dann weiter mit dem nächsten Punkt.

Gebraucht werden zwei Personen bzw. zwei Konten mit eigenen Mail-Adressen, im Folgenden **A** (Eigentümer, Admin) und **B** (Vertrauensperson). Mit den kürzesten Fristen dauert der Antrag in Teil 4 etwa einen Tag; der Weg ohne Lebenszeichen (Teil 5) mindestens 11 Tage.

## 1. Installation

- [ ] Ordner, `.env` und Rechte wie in [installation.md](installation.md), Abschnitte 1–3
- [ ] `MYLIFE_BACKUP_KEY` erzeugt (`openssl rand -base64 32`), in die `.env` eingetragen, **ausgedruckt** und getrennt von der NAS abgelegt
- [ ] Projekt im Container Manager gestartet, `https://mylife.eckis-corner.de` erreichbar, Zertifikat gültig
- [ ] Einrichtung mit dem Einrichtungscode aus dem Container-Protokoll: Konto A, Master-Passwort, Recovery-Kit **ausgedruckt**, Authenticator-App
- [ ] Optional: Passkey unter „Sicherheit“ hinzugefügt und Anmeldung damit getestet
- [ ] Konto → „Eigenes Datenblatt“: Name und Vorname gespeichert, ein Bild (JPEG, PNG oder WebP bis 5 MB) hochgeladen und angezeigt; ein zweites Bild ersetzt das erste (das alte liegt im Papierkorb); unter „Über mich“ lässt sich kein zweites Datenblatt dieser Art anlegen

## 2. Verwaltung (als A)

- [ ] Einstellungen → Allgemein: Adresse der Instanz `https://mylife.eckis-corner.de` eingetragen
- [ ] Einstellungen → E-Mail: SMTP eingetragen, „Speichern und Testmail senden“ – Mail kommt an
- [ ] Einstellungen → Sicherung: kein Hinweis „kein Sicherungsschlüssel“; „Jetzt sichern“ – Sicherung erscheint in der Liste und unter `data/app/backup`
- [ ] Einstellungen → Fristen für den Test auf die kleinsten Werte: Lebenszeichen 7 Tage, Erinnerungen `1, 2`, Wartezeit 3 Tage, Antragsfrist 1 Tag (jeweils Minimum und Standard)
- [ ] Optional: ntfy eingerichtet, Test-Nachricht kommt aufs Smartphone

## 3. Tresor mit echten Daten (Abnahme M1)

- [ ] Je Bereich mindestens ein echter Eintrag, etwa Girokonto, Krankenversicherung, Testament (mit Ablageort), Ausweis (mit Ablaufdatum), Persönliche Wünsche
- [ ] Ein Dokument hochgeladen (PDF) und eines direkt mit dem Smartphone fotografiert; Vorschau funktioniert
- [ ] Suche findet einen Eintrag über ein Feld (etwa die Bank)
- [ ] Übersicht: Vollständigkeit, Kacheln, „Als geprüft markieren“
- [ ] Checkliste: „Vorlage übernehmen“, die Aufgabe „Verträge kündigen“ listet einen Vertrag mit dem Wunsch „kündigen“
- [ ] Erinnerungen: Seite „Erinnerungen“ zeigt ein Datum, das der Server kennt
- [ ] Abmelden, neu laden, wieder anmelden: alles da
- [ ] **In der Datenbank steht nur Chiffrat.** Per SSH im Projektordner:

  ```bash
  sudo docker compose exec db psql -U mylife -d mylife -c "SELECT type_key, left(encode(payload, 'escape'), 60) FROM objects LIMIT 5;"
  sudo docker compose exec db psql -U mylife -d mylife -c "SELECT left(encode(metadata, 'escape'), 60) FROM documents LIMIT 3;"
  ```

  Erwartet: unlesbare Zeichen, keine Titel, Banken, Dateinamen oder Notizen. Lesbar sind nur die Typen (`bankAccount` …), das ist so vorgesehen (Entscheidung 30).
- [ ] Unter `data/app/files` sind die Dokumente ebenfalls unlesbar (etwa ein PDF oder das Bild aus dem eigenen Datenblatt lässt sich nicht öffnen)

## 4. Familie und Notfallzugang (Abnahme M2)

**Einladen**

- [ ] A: Familie → B einladen (per Mail oder Link); B nimmt an, richtet Passwort, Recovery-Kit und Authenticator ein
- [ ] A und B sehen sich gegenseitig unter „Familie“ als bestätigt

**Vertrauensperson**

- [ ] A: Vertrauenspersonen → B mit Antragsrecht; Bereiche für „verstorben“ und „handlungsunfähig“ freigeben, dazu die Checkliste und einen Brief an B
- [ ] A: einen Bereich „sofort teilen“; B sieht ihn unter „Mit mir geteilt“, aber nichts anderes
- [ ] B bekommt die Mail „Vertrauensperson“ ohne Angabe, wofür

**Antrag mit Widerspruch**

- [ ] B: Notfallzugang → Zugang beantragen „handlungsunfähig“; A bekommt die Mail mit dem Link „Widersprechen“
- [ ] A: Link in der Mail öffnen (ohne Anmeldung) – Antrag ist angehalten; nach der Anmeldung „Endgültig widersprechen“ – B bekommt eine Mail, es wird nichts freigegeben

**Antrag mit Freigabe**

- [ ] B: erneut beantragen, diesmal „verstorben“; A reagiert nicht
- [ ] Nach der Antragsfrist (1 Tag): A und B bekommen die Mail „freigegeben“
- [ ] B: Lesemodus – Briefe zuerst, dann die Checkliste, dann die Bereiche; nur Freigegebenes ist zu sehen; ein Dokument ansehen und speichern
- [ ] B: in der Checkliste eine Aufgabe abhaken; A sieht auf der Seite „Checkliste“, wer sie wann abgehakt hat
- [ ] A: im Protokoll steht, was B gelesen hat

**Rücknahme**

- [ ] A: Notfallzugang → „Freigabe zurücknehmen“ – der Schlüsselwechsel läuft durch, kein Hinweis „Schlüsselwechsel fortsetzen“
- [ ] B bekommt die Mail „zurückgenommen“ und sieht im Lesemodus nichts mehr; „sofort geteilt“ bleibt sichtbar

**Gemeinsames Löschen** (optional, braucht ein drittes Testkonto als Eigentümer, damit A nicht gelöscht wird)

- [ ] Freigabe „verstorben“ für das Testkonto; B stimmt zu – Löschen in 7 Tagen geplant, der Eigentümer bekommt eine Mail
- [ ] B zieht die Zustimmung zurück – Termin entfällt

## 5. Ohne Lebenszeichen (Dauertest, optional)

- [ ] B ist Vertrauensperson eines Testkontos, das sich nicht mehr anmeldet: nach 7 Tagen Mail „Lebenszeichen fällig“, dann die Erinnerungen, nach der Wartezeit die Freigabe beider Modi
- [ ] Der Link „Lebenszeichen“ aus einer Erinnerung setzt alles zurück, ohne Anmeldung

## 6. Notfallpaket und Exporte

- [ ] Export & Druck → Notfallordner als PDF mit Passwort; öffnet sich nur mit dem Passwort
- [ ] Export & Druck → Datensicherung (JSON) mit Dokumenten
- [ ] Export & Druck → Notfallpaket erstellen; Schlüsselblatt drucken
- [ ] Die HTML-Datei per Doppelklick öffnen und mit den 24 Wörtern entsperren – in **Chrome**, **Safari** und **Firefox**, auch auf dem Smartphone
- [ ] `mylife-decrypt` (aus dem Artefakt der CI, siehe [mylife-decrypt.md](mylife-decrypt.md)): JSON-Export mit dem Master-Passwort öffnen, Notfallpaket mit den 24 Wörtern; Datenblätter und Dokumente stimmen

## 7. Sicherung und Rücksicherung

- [ ] Hyper Backup nimmt `/volume1/docker/mylife` auf, Ziel außerhalb der NAS, erste Sicherung erfolgreich
- [ ] Am nächsten Morgen: Verwaltung → Sicherung zeigt die nächtliche Sicherung
- [ ] **Rücksicherung geprobt:** auf einem anderen Rechner mit Docker oder in einem zweiten Projektordner mit eigener Datenbank und derselben `.env` installieren, eine Sicherung nach `data/app/backup` kopieren, `restore … --confirm` ([installation.md](installation.md), Abschnitt 8); Anmeldung von A klappt, Einträge und Dokumente sind da
- [ ] Update geprobt: „Jetzt sichern“, `docker compose pull`, `up -d`; danach Anmeldung und Verwaltung ohne Fehler

## 8. Abschluss

- [ ] Fristen in der Verwaltung wieder auf sinnvolle Werte (Standard: Lebenszeichen 30 Tage, Erinnerungen 3, 7, 14, Wartezeit 14, Antragsfrist 7)
- [ ] Testkonten gelöscht oder gesperrt; Testeinträge entfernt
- [ ] Zweite Person als Admin eingetragen ([nachfolge.md](nachfolge.md), Abschnitt 1)
- [ ] Unterlagen für die Nachfolge zusammengestellt (`.env`, Sicherungsschlüssel, Zugänge)
- [ ] Notizen zu allem, was nicht wie beschrieben funktioniert hat, an die Entwicklung gegeben
- [ ] Danach: Tag `v1.0.0` setzen (Entscheidung 136)
