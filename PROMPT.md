# Prompt: „myLife" – digitaler Notfallordner für meine Familie

## Name
Der Name der App soll **myLife** lauten.

## Rolle
Du bist ein erfahrener Software-Architekt und Full-Stack-Entwickler mit Schwerpunkt auf Sicherheit und Datenschutz (DSGVO). Du baust mit mir eine App von Grund auf.

## Ziel der App
Ich möchte an einem Ort alle wichtigen Informationen über mein Leben erfassen: Versicherungen, Bankkonten, Kredite, Kreditkarten, Urkunden, Verträge, digitale Zugänge und so weiter. **Wenn ich sterbe oder handlungsunfähig werde, soll meine Familie sicher und vollständig an diese Daten kommen** und wissen, was zu tun ist.

Solange ich lebe, gehören die Daten nur mir. Niemand sonst darf sie lesen, auch nicht der Server-Betreiber.

## Inhalte (Kategorien)
Jede Kategorie hat eigene, passende Felder, Freitext-Notizen und Datei-Anhänge (PDF, Fotos, Scans).

1. **Bankkonten & Depots**: Bank, IBAN, Kontoart, Kontoinhaber, Vollmachten, Ansprechpartner
2. **Kreditkarten**: Herausgeber, letzte 4 Ziffern, Ablaufdatum, Sperr-Hotline (keine vollständige Kartennummer, keine PIN)
3. **Kredite & Darlehen**: Gläubiger, Restschuld, Rate, Laufzeit, Restschuldversicherung ja/nein
4. **Versicherungen**: Art (Leben, Risiko-Leben, BU, Haftpflicht, Hausrat, KFZ, Kranken …), Versicherer, Policennummer, Begünstigte, Beitrag, Kündigungsfrist
5. **Verträge & Abos**: Strom, Gas, Internet, Handy, Streaming, Mitgliedschaften, Miete; mit dem Hinweis „kündigen / übernehmen / weiterlaufen lassen"
6. **Urkunden & Dokumente**: Geburts- und Heiratsurkunde, Testament (wo liegt das Original?), Vorsorgevollmacht, Patientenverfügung, Grundbuch, Fahrzeugbrief; jeweils mit Ablageort des Originals
7. **Immobilien & Fahrzeuge & Vermögen**
8. **Digitaler Nachlass**: E-Mail-Konten, Social Media, Cloud-Speicher, Domains, Krypto-Wallets (nur Hinweis auf den Aufbewahrungsort, keine Seeds im Klartext); jeweils mit dem Wunsch „löschen / Gedenkzustand / übergeben"
9. **Passwortmanager-Zugang**: Die App speichert **keine einzelnen Passwörter**. Sie hinterlegt, welcher Passwortmanager genutzt wird, wie der Notfallzugang funktioniert (z. B. Emergency Kit, Notfallkontakt-Funktion des Anbieters) und wo das Master-Passwort hinterlegt ist.
10. **Wichtige Kontakte**: Steuerberater, Notar, Anwalt, Arbeitgeber, Hausarzt, Bankberater
11. **Persönliche Wünsche**: Bestattung, Organspende, Haustiere, persönliche Briefe an einzelne Familienmitglieder
12. **Checkliste für die Hinterbliebenen**: Was ist in den ersten 24 Stunden, der ersten Woche und dem ersten Monat zu tun? (Sterbeurkunde, Arbeitgeber, Rentenversicherung, Krankenkasse, Banken, Versicherungen melden …)

## Kernfunktionen
- **Dashboard** mit Übersicht pro Kategorie, Vollständigkeitsanzeige und „Zuletzt geprüft am"
- **Erinnerungen**: regelmäßige Aufforderung (z. B. alle 6 Monate), die Daten zu prüfen; Hinweise auf ablaufende Dokumente und Karten
- **Volltextsuche** über alle Einträge
- **Begünstigte / Vertrauenspersonen** verwalten: Name, Beziehung, Kontakt, und welche Kategorien jede Person sehen darf
- **Notfallzugang (Dead-Man's-Switch)**:
  - Ich muss in einem frei wählbaren Intervall ein Lebenszeichen geben (Login oder Klick auf einen Link in einer E-Mail).
  - Bleibt es aus, folgen gestufte Erinnerungen an mich und danach eine Wartezeit (z. B. 14 Tage).
  - Alternativ: Eine Vertrauensperson beantragt den Zugang. Ich werde benachrichtigt und kann innerhalb der Wartezeit widersprechen.
  - Erst danach bekommen die Begünstigten Zugriff auf genau die Kategorien, die für sie freigegeben sind.
- **Export als „Notfallordner"**: verschlüsseltes PDF oder druckfertige Version für einen Papierordner bzw. ein Bankschließfach
- **Lesemodus für Hinterbliebene**: einfache, ruhige, verständliche Oberfläche, auch für technisch unerfahrene Menschen; mit der Checkliste als Leitfaden

## Sicherheit (höchste Priorität)
- **Ende-zu-Ende-Verschlüsselung / Zero-Knowledge**: Die Daten werden auf dem Client verschlüsselt (z. B. AES-256-GCM, Schlüssel aus dem Master-Passwort per Argon2id abgeleitet). Der Server sieht nur verschlüsselte Daten.
- Schlüsselverteilung an die Begünstigten über asymmetrische Kryptografie (jede Vertrauensperson hat ein eigenes Schlüsselpaar), damit beim Notfallzugang kein Klartext über den Server geht
- **Zwei-Faktor-Authentifizierung** (TOTP / Passkeys)
- **Wiederherstellungs-Kit** zum Ausdrucken, falls ich mein Master-Passwort vergesse
- Audit-Log: Wer hat wann worauf zugegriffen?
- Automatische Abmeldung, kein Speichern sensibler Daten in Logs
- Self-hosted, keine Daten bei Dritten; Ausfallsicherheit durch verschlüsselte, automatische Backups an einen zweiten Ort
- Klarer Hinweis in der App: Was gehört **nicht** hinein (vollständige Kartennummern, PINs, TANs, Seed-Phrasen im Klartext)

## Technische Vorgaben
- **Tech-Stack:** Blazor + MudBlazor (UI), ASP.NET Core / aktuelles .NET (Backend), PostgreSQL (Datenbank). Halte dich an die Konventionen aus meinem MudBlazor-Projekt-Skill.
- **Verschlüsselung im Browser:** Damit Zero-Knowledge wirklich gilt, muss die Ver- und Entschlüsselung auf dem Client laufen. Wähle das Blazor-Hosting-Modell deshalb passend dazu (z. B. Blazor WebAssembly oder Interactive WebAssembly, *nicht* reines Blazor Server). Begründe die Wahl und die Krypto-Bibliothek (z. B. Web Crypto API per JS-Interop).
- **Hosting:** Self-hosted auf meinem eigenen Server oder NAS, ausgeliefert als Docker / Docker Compose (App, Datenbank, Reverse Proxy mit HTTPS). Dazu eine Anleitung für Installation, Updates und Backups.
- **E-Mail-Versand** für Lebenszeichen und Benachrichtigungen über einen konfigurierbaren SMTP-Server. Der Dead-Man's-Switch läuft als Hintergrunddienst auf dem Server.
- **Plattform:** Web-App, responsive für Desktop und Smartphone. Keine native App.
- **Nutzer:** Ein Hauptnutzer (ich) und wenige Vertrauenspersonen aus der Familie. Keine öffentliche Registrierung, kein Mehrmandanten-Betrieb. Vertrauenspersonen werden von mir eingeladen.
- Sprache der Oberfläche: Deutsch (vorbereitet für weitere Sprachen)
- Saubere Architektur, automatisierte Tests, besonders für Verschlüsselung und Notfallzugang
- Datenexport und Backup in einem offenen Format (verschlüsseltes JSON), damit die Daten nie in der App gefangen sind

## Vorgehen
1. Stell mir zuerst **Rückfragen** zu allem, was noch unklar ist (z. B. Server-Hardware, Domain, SMTP-Anbieter, was passiert, wenn mein Server nach meinem Tod abgeschaltet wird).
2. Schlag danach eine **Architektur** vor: Komponenten, Datenmodell, Verschlüsselungs- und Schlüsselkonzept, Ablauf des Notfallzugangs als Diagramm.
3. Erstelle einen **Umsetzungsplan in Meilensteinen**:
   - MVP: Login, Verschlüsselung, Kategorien erfassen, Dateianhänge, PDF-Export
   - Danach: Begünstigte, Dead-Man's-Switch, Lesemodus
   - Danach: Erinnerungen, Checkliste, Feinschliff
4. Setz die Meilensteine nacheinander um und warte nach jedem auf mein Feedback.
