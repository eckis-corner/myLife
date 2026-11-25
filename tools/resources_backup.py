"""Texte für die Server-Sicherung (M3.4). Wird von tools/resources.py eingebunden. CLIENT: Verwaltung. MAIL: Fehlermail."""

CLIENT = {
    "AdminTabBackup": ("Sicherung", "Backup"),
    "AdminBackupText": (
        "Jeden Tag zur eingestellten Uhrzeit legt myLife eine verschlüsselte Sicherung im Ordner „backup“ ab: Datenbank, "
        "Schlüsselring und auf Wunsch die Dokumente. Das Synology-Backup übernimmt sie von dort. Alte Sicherungen werden "
        "nach Generationen aufgeräumt. Wie man zurücksichert, steht in der Installationsanleitung.",
        "Every day at the set time, myLife stores an encrypted backup in the \"backup\" folder: database, key ring and, if "
        "chosen, the documents. Synology backup takes it from there. Old backups are pruned by generations. How to "
        "restore is described in the installation guide.",
    ),
    "AdminBackupNotConfigured": (
        "Es ist kein Sicherungsschlüssel eingerichtet, deshalb gibt es keine automatische Sicherung. Trage MYLIFE_BACKUP_KEY "
        "in die .env ein (openssl rand -base64 32), bewahre den Schlüssel getrennt auf und starte das Projekt neu.",
        "No backup key is set up, so there are no automatic backups. Add MYLIFE_BACKUP_KEY to the .env file "
        "(openssl rand -base64 32), keep the key separately and restart the project.",
    ),
    "AdminBackupLast": ("Letzte Sicherung: {0}", "Last backup: {0}"),
    "AdminBackupNext": ("Nächste: {0}", "Next: {0}"),
    "AdminBackupError": ("Fehler am {0}: {1}", "Error on {0}: {1}"),
    "AdminBackupNow": ("Jetzt sichern", "Back up now"),
    "AdminBackupDone": ("Die Sicherung ist erstellt.", "The backup has been created."),
    "AdminBackupHour": ("Uhrzeit", "Time"),
    "AdminBackupKeepDaily": ("Tägliche behalten", "Keep daily"),
    "AdminBackupKeepWeekly": ("Wöchentliche behalten", "Keep weekly"),
    "AdminBackupKeepMonthly": ("Monatliche behalten", "Keep monthly"),
    "AdminBackupDocuments": ("Dokumente in jede Sicherung aufnehmen", "Include documents in every backup"),
    "AdminBackupDocumentsHint": (
        "Abschalten, wenn das Synology-Backup den Ordner „files“ ohnehin sichert: Dann bleiben die Sicherungen klein.",
        "Turn off if Synology backup already covers the \"files\" folder: backups then stay small.",
    ),
    "AdminBackupFile": ("Datei", "File"),
    "AdminBackupCreated": ("Erstellt", "Created"),
    "AdminBackupSize": ("Größe", "Size"),
    "UnitHour": ("Uhr", "h"),
    "AdminError_admin-backup-not-configured": ("Es ist kein Sicherungsschlüssel eingerichtet.", "No backup key is set up."),
    "AdminError_admin-backup-failed": ("Die Sicherung ist fehlgeschlagen; Einzelheiten stehen im Protokoll des Servers.", "The backup failed; details are in the server log."),
    "Audit_admin-backup-created": ("Sicherung erstellt", "Backup created"),
    "Audit_admin-backup-failed": ("Sicherung fehlgeschlagen", "Backup failed"),
}

MAIL = {
    "Mail_backup-failed_subject": ("myLife: Die Sicherung ist fehlgeschlagen", "myLife: The backup failed"),
    "Mail_backup-failed_body": (
        "Hallo {name},\n\ndie tägliche Sicherung von myLife ist fehlgeschlagen:\n{error}\n\nBitte sieh in der Verwaltung "
        "unter „Sicherung“ und im Protokoll des Containers nach. myLife versucht es weiter; diese Mail kommt höchstens "
        "einmal am Tag.",
        "Hello {name},\n\nthe daily myLife backup failed:\n{error}\n\nPlease check \"Backup\" in the administration and the "
        "container log. myLife keeps trying; this email is sent at most once a day.",
    ),
}
