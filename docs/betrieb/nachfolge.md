# Übergabe an eine Nachfolgeperson

Für den Fall, dass der Betreiber der myLife-Instanz selbst ausfällt (Entscheidung 4c). Diese Anleitung richtet sich an die Person, die die Instanz dann weiter betreibt oder geordnet beendet. Sie braucht keine Programmierkenntnisse, aber Zugang zur Synology.

## 1. Was die Nachfolgeperson vorher bekommt

Der Betreiber legt diese Dinge **heute** bereit, ausgedruckt oder in einem Passwortmanager mit Notfallzugang:

| Was | Wozu |
| --- | --- |
| Zugang zur Synology (Benutzer mit Administratorrechten, bei 2FA auch den zweiten Faktor) | Container Manager, Ordner, Hyper Backup |
| Die Datei `/volume1/docker/mylife/.env`, vor allem `POSTGRES_PASSWORD` und **`MYLIFE_BACKUP_KEY`** | Datenbank und Rücksicherung; ohne den Sicherungsschlüssel ist jede Sicherung wertlos |
| Zugang zum Router bzw. DNS der Domain (`mylife.eckis-corner.de`) und zum Nginx Proxy Manager | Erreichbarkeit von außen, Zertifikat |
| Ein eigenes **Admin-Konto** in myLife | Mailversand, Benutzer, Sicherung; zudem kann der Tresor des einzigen Admins nicht gemeinsam gelöscht werden (106) |
| Wo das Hyper-Backup-Ziel liegt und wie man herankommt | zweiter Ort der Sicherungen |

Die Tresorinhalte bekommt die Nachfolgeperson damit **nicht**: Die liest niemand außer den Vertrauenspersonen, die ein Eigentümer benannt hat. Betrieb und Inhalt sind getrennt.

## 2. Regelmäßig nachsehen

Einmal im Monat genügt:

- **Verwaltung → Einstellungen → Sicherung:** Die letzte Sicherung ist von heute oder gestern, kein Fehler.
- **Verwaltung → Einstellungen → E-Mail:** Die Warteschlange ist leer, „Speichern und Testmail senden“ kommt an. Ohne Mailversand laufen keine Fristen des Notfallzugangs weiter, und es kommen keine Erinnerungen.
- **Updates** einspielen ([installation.md](installation.md), Abschnitt 7), vorher „Jetzt sichern“.
- In Hyper Backup: Die letzte Sicherung des Ordners `/volume1/docker/mylife` war erfolgreich.

## 3. Wenn ein Eigentümer stirbt

Die Instanz muss dafür nichts Besonderes tun. Der Notfallzugang läuft von selbst: Bleibt das Lebenszeichen aus, kommen Erinnerungen und nach der Wartezeit die Freigabe; oder eine Vertrauensperson beantragt den Zugang. Die Vertrauenspersonen lesen danach im **Lesemodus** und haken die **Checkliste** gemeinsam ab. Wenn alles erledigt ist, können sie den Tresor gemeinsam endgültig löschen.

Wichtig ist nur, dass die Instanz **läuft und Mails verschickt**, bis alle Vertrauenspersonen alles haben, was sie brauchen.

## 4. Die Instanz geordnet beenden

Soll myLife nicht weiter betrieben werden:

1. **Ankündigen:** Allen Nutzern mindestens vier Wochen vorher Bescheid geben.
2. **Jeder Nutzer sichert seine eigenen Daten:** unter „Export & Druck“ den **JSON-Export** (mit Dokumenten) und den **Notfallordner als PDF**, auf Wunsch ein **Notfallpaket** für die Familie. Der JSON-Export öffnet sich auch später ohne myLife mit [mylife-decrypt](mylife-decrypt.md) und dem eigenen Passwort oder Recovery-Kit.
3. **Vertrauenspersonen** von freigegebenen Tresoren speichern, was sie brauchen (Dokumente im Lesemodus speichern) oder lassen sich ein Notfallpaket geben, solange das noch geht.
4. **Letzte Sicherung** mit „Jetzt sichern“ erstellen und zusammen mit `.env` sicher aufbewahren, etwa ein Jahr lang.
5. Das Projekt im Container Manager stoppen und löschen, den Proxy-Host im Nginx Proxy Manager entfernen, danach den Ordner `/volume1/docker/mylife` löschen.

## 5. Auf einer neuen Synology weiterbetreiben

Wie in [installation.md](installation.md) installieren, mit **derselben** `.env` (gleicher `MYLIFE_BACKUP_KEY`), die neueste Sicherung nach `data/app/backup` kopieren und wie in Abschnitt 8 „Zurückspielen“ beschrieben zurücksichern. Danach den Proxy-Host und die Domain auf die neue Synology zeigen lassen. Die Nutzer melden sich wie gewohnt an; ihre Tresore, Vertrauenspersonen und Fristen sind unverändert.
