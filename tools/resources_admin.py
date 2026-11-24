"""Texte für Verwaltung, Datenschutz, Kontolöschung und Mails (M2.1). Wird von tools/resources.py eingebunden.

CLIENT: Oberfläche (SharedResource.resx im Client).
MAIL: Mailvorlagen (MailResource.resx im Server); Platzhalter in geschweiften Klammern, etwa {name}.
"""

PRIVACY_DEFAULT = (
    "Diese myLife-Instanz wird privat für eine Familie betrieben. Verantwortlich ist, wer sie betreibt; "
    "die Kontaktdaten nennt dir der Administrator.\n\n"
    "Was gespeichert wird: deine E-Mail-Adresse, dein Anzeigename, deine Sprache, Zeitpunkte von Anmeldungen und "
    "Änderungen sowie ein Protokoll mit gekürzten IP-Adressen. Die Inhalte deines Tresors, also Einträge, "
    "Dokumente und Notizen, werden in deinem Browser verschlüsselt. Der Server und der Administrator können sie "
    "nicht lesen.\n\n"
    "Wozu: für die Anmeldung, den Notfallzugang deiner Vertrauenspersonen und Benachrichtigungen per E-Mail. "
    "Es gibt keine Werbung, keine Analyse und keine Weitergabe an Dritte; Mails laufen nur über den "
    "eingestellten Mailserver.\n\n"
    "Wie lange: Das Protokoll wird nach der eingestellten Frist gelöscht. Wird dein Konto gelöscht, verschwinden "
    "dein Tresor, deine Dokumente und dein Protokoll endgültig.\n\n"
    "Deine Rechte: Du kannst jederzeit Auskunft, Berichtigung und Löschung verlangen. Wende dich dafür an den "
    "Administrator dieser Instanz.",
    "This myLife instance is run privately for one family. The person who runs it is responsible; the "
    "administrator can give you their contact details.\n\n"
    "What is stored: your email address, your display name, your language, the times of sign-ins and changes, and "
    "a log with shortened IP addresses. The contents of your vault (entries, documents and notes) are encrypted in "
    "your browser. Neither the server nor the administrator can read them.\n\n"
    "What for: signing in, your trusted persons' emergency access and email notifications. There is no "
    "advertising, no analytics and no sharing with third parties; emails only go through the configured mail "
    "server.\n\n"
    "How long: The log is deleted after the configured period. If your account is deleted, your vault, your "
    "documents and your log are gone for good.\n\n"
    "Your rights: You can ask for access, correction and deletion at any time. Please contact the administrator "
    "of this instance.",
)

CLIENT = {
    "NavAdmin": ("Verwaltung", "Administration"),
    "SaveSuccessful": ("Gespeichert.", "Saved."),
    "UnitDays": ("Tage", "days"),
    "UnitMinutes": ("Minuten", "minutes"),
    # Einstellungen
    "AdminSettingsTitle": ("Einstellungen", "Settings"),
    "AdminSettingsText": (
        "Einstellungen für die ganze Instanz. Tresorinhalte siehst du hier nicht, auch als Administrator nicht.",
        "Settings for the whole instance. You cannot see vault contents here, not even as administrator.",
    ),
    "AdminSettingsInvalid": ("Bitte prüfe die markierten Felder, auch auf den anderen Reitern.", "Please check the highlighted fields, including on the other tabs."),
    "AdminTabGeneral": ("Allgemein", "General"),
    "AdminPublicUrl": ("Adresse der Instanz", "Address of this instance"),
    "AdminPublicUrlHint": ("Für Links in Mails, etwa https://mylife.example.org. Leer: die Adresse, unter der du gerade arbeitest.", "Used for links in emails, e.g. https://mylife.example.org. Empty: the address you are using right now."),
    "AdminIdleTimeout": ("Automatische Abmeldung", "Automatic sign-out"),
    "AdminIdleTimeoutHint": ("Nach so vielen Minuten ohne Aktivität. Gilt ab der nächsten Anfrage.", "After this many minutes without activity. Applies from the next request."),
    "AdminRetention": ("Aufbewahrung des Protokolls", "Log retention"),
    "AdminRetentionHint": ("Ältere Protokolleinträge werden automatisch gelöscht.", "Older log entries are deleted automatically."),
    "AdminTabMail": ("E-Mail", "Email"),
    "AdminMailText": (
        "myLife verschickt Mails für Benachrichtigungen und später für Lebenszeichen und den Notfallzugang. Ohne "
        "Mailversand warten Mails in der Warteschlange.",
        "myLife sends emails for notifications and later for life signs and emergency access. Without a mail "
        "server, emails wait in the queue.",
    ),
    "AdminSmtpHost": ("SMTP-Server", "SMTP server"),
    "AdminSmtpPort": ("Port", "Port"),
    "AdminSmtpSecurity": ("Verschlüsselung", "Encryption"),
    "AdminSmtpSecurity_StartTls": ("STARTTLS (meist Port 587)", "STARTTLS (usually port 587)"),
    "AdminSmtpSecurity_SslOnConnect": ("SSL/TLS (meist Port 465)", "SSL/TLS (usually port 465)"),
    "AdminSmtpSecurity_None": ("Keine (nur im eigenen Netz)", "None (local network only)"),
    "AdminSmtpUsername": ("Benutzername", "User name"),
    "AdminSmtpPassword": ("Passwort", "Password"),
    "AdminSecretStored": ("•••••• hinterlegt", "•••••• stored"),
    "AdminSecretHint": ("Leer lassen, um den hinterlegten Wert zu behalten.", "Leave empty to keep the stored value."),
    "AdminSecretRemove": ("Hinterlegten Wert entfernen", "Remove stored value"),
    "AdminSmtpFrom": ("Absenderadresse", "Sender address"),
    "AdminSmtpFromName": ("Absendername", "Sender name"),
    "AdminTestMail": ("Speichern und Testmail senden", "Save and send test email"),
    "AdminTestMailHint": ("Die Testmail geht an deine eigene Adresse.", "The test email goes to your own address."),
    "AdminTestMailSent": ("Testmail verschickt. Schau in dein Postfach.", "Test email sent. Check your inbox."),
    "AdminTestFailed": ("Versand fehlgeschlagen: {0}", "Sending failed: {0}"),
    "AdminQueueTitle": ("Warteschlange", "Queue"),
    "AdminQueuePending": ("{0} wartend", "{0} waiting"),
    "AdminQueueFailed": ("{0} aufgegeben", "{0} given up"),
    "AdminQueueLastSent": ("Zuletzt verschickt: {0}", "Last sent: {0}"),
    "AdminQueueNotConfigured": ("Mails warten, weil noch kein Mailserver eingerichtet ist.", "Emails are waiting because no mail server is set up yet."),
    "AdminQueueLastError": ("Letzter Fehler: {0}", "Last error: {0}"),
    "AdminQueueRetry": ("Jetzt erneut versuchen", "Retry now"),
    "AdminTabNtfy": ("ntfy", "ntfy"),
    "AdminNtfyText": (
        "Optional: Push-Nachrichten über einen ntfy-Server, zusätzlich zur Mail. Die Themen der einzelnen Nutzer "
        "kommen mit dem Notfallzugang.",
        "Optional: push notifications via an ntfy server, in addition to email. Topics for individual users come "
        "with emergency access.",
    ),
    "AdminNtfyServer": ("ntfy-Server", "ntfy server"),
    "AdminNtfyToken": ("Zugriffstoken (optional)", "Access token (optional)"),
    "AdminNtfyTopic": ("Thema für den Test", "Topic for the test"),
    "AdminNtfyTopicHint": ("Buchstaben, Ziffern, - und _; wird nicht gespeichert.", "Letters, digits, - and _; not stored."),
    "AdminTestNtfy": ("Speichern und Test senden", "Save and send test"),
    "AdminTestNtfySent": ("Testnachricht verschickt.", "Test notification sent."),
    "AdminTabDeadlines": ("Fristen", "Deadlines"),
    "AdminDeadlinesText": (
        "Standardwerte und Grenzen für den Notfallzugang. Jeder Nutzer wählt seine Werte später innerhalb der "
        "Grenzen. Kürzere Fristen eines Nutzers kannst du nicht erzwingen.",
        "Defaults and limits for emergency access. Each user later chooses their own values within the limits. "
        "You cannot force shorter deadlines on a user.",
    ),
    "AdminLifesign": ("Lebenszeichen", "Life sign"),
    "AdminLifesignHint": ("Wie oft sich jemand meldet, per Anmeldung oder Link in der Mail.", "How often someone checks in, by signing in or via the link in an email."),
    "AdminReminders": ("Erinnerungen (Tage nach Ablauf)", "Reminders (days after expiry)"),
    "AdminRemindersHint": ("Aufsteigend, durch Komma getrennt, höchstens fünf, etwa 3, 7, 14.", "Ascending, separated by commas, at most five, e.g. 3, 7, 14."),
    "AdminWaiting": ("Wartezeit", "Waiting period"),
    "AdminWaitingHint": ("Nach der letzten Erinnerung ohne Reaktion, bis der Zugang freigegeben wird.", "After the last unanswered reminder, until access is released."),
    "AdminRequestWaiting": ("Frist bei einem Antrag", "Waiting period after a request"),
    "AdminRequestWaitingHint": ("Zwischen dem Antrag einer Vertrauensperson und der Freigabe.", "Between a trusted person's request and the release."),
    "AdminRangeDefault": ("Standard", "Default"),
    "AdminRangeMin": ("Mindestens", "Minimum"),
    "AdminRangeMax": ("Höchstens", "Maximum"),
    "AdminRangeError": (
        "Mindestens ≤ Standard ≤ Höchstens, alles zwischen {0} und {1} Tagen.",
        "Minimum ≤ default ≤ maximum, all between {0} and {1} days.",
    ),
    "AdminTabPrivacy": ("Datenschutz", "Privacy"),
    "AdminPrivacyText": (
        "Dieser Text steht auf der Datenschutzseite, auch ohne Anmeldung. Leer lassen für den mitgelieferten "
        "Standardtext. Absätze trennst du mit einer Leerzeile.",
        "This text appears on the privacy page, also without signing in. Leave empty for the built-in default "
        "text. Separate paragraphs with an empty line.",
    ),
    "AdminPrivacyNotice": ("Datenschutzhinweis", "Privacy notice"),
    "AdminPrivacyOpen": ("Datenschutzseite ansehen", "View privacy page"),
    "SettingsError_required": ("Pflichtfeld", "Required"),
    "SettingsError_range": ("Wert außerhalb des erlaubten Bereichs", "Value out of range"),
    "SettingsError_order": ("Bitte aufsteigend angeben", "Please list in ascending order"),
    "SettingsError_email": ("Keine gültige E-Mail-Adresse", "Not a valid email address"),
    "SettingsError_url": ("Keine gültige Adresse (http:// oder https://)", "Not a valid address (http:// or https://)"),
    "SettingsError_too-long": ("Zu lang", "Too long"),
    "AdminError_admin-not-found": ("Das Konto gibt es nicht mehr.", "The account no longer exists."),
    "AdminError_admin-self": ("Das geht nicht mit deinem eigenen Konto.", "You cannot do this with your own account."),
    "AdminError_admin-last-admin": ("Es muss immer mindestens einen aktiven Administrator geben.", "There must always be at least one active administrator."),
    "AdminError_admin-mail-not-configured": ("Dafür muss zuerst der Mailversand eingerichtet sein.", "Email sending must be set up first."),
    "AdminError_admin-invalid": ("Die Eingaben sind unvollständig oder ungültig.", "The input is incomplete or invalid."),
    "AdminError_admin-token-invalid": ("Der Link ist ungültig, abgelaufen oder schon verwendet.", "The link is invalid, expired or already used."),
    "AdminError_admin-failed": ("Das hat nicht geklappt. Bitte versuche es erneut.", "That did not work. Please try again."),
    # Benutzer
    "AdminUsersTitle": ("Benutzer", "Users"),
    "AdminUsersText": (
        "Alle Konten dieser Instanz. Du siehst nur Namen, Adressen und Anmeldedaten, keine Tresorinhalte. "
        "Jede Aktion wird protokolliert, und die betroffene Person bekommt eine Mail.",
        "All accounts of this instance. You only see names, addresses and sign-in data, no vault contents. "
        "Every action is logged and the person concerned receives an email.",
    ),
    "AdminUsersEmpty": ("Keine Konten.", "No accounts."),
    "AdminUserName": ("Name", "Name"),
    "AdminUserEmail": ("E-Mail", "Email"),
    "AdminUserStatus": ("Status", "Status"),
    "AdminUserTwoFactor": ("Zweiter Faktor", "Second factor"),
    "AdminUserLastLogin": ("Letzte Anmeldung", "Last sign-in"),
    "AdminUserMe": ("du", "you"),
    "AdminUserActions": ("Aktionen", "Actions"),
    "AccountStatus_Invited": ("Eingeladen", "Invited"),
    "AccountStatus_Active": ("Aktiv", "Active"),
    "AccountStatus_Locked": ("Gesperrt", "Locked"),
    "AdminRole": ("Administrator", "Administrator"),
    "AdminDeletionPending": ("Löschung beantragt", "Deletion requested"),
    "AdminTwoFactorTotp": ("Authenticator-App", "Authenticator app"),
    "AdminTwoFactorPasskeys": ("{0} Passkey(s)", "{0} passkey(s)"),
    "AdminTwoFactorNone": ("keiner (wird bei der nächsten Anmeldung eingerichtet)", "none (set up at next sign-in)"),
    "AdminLock": ("Sperren", "Lock"),
    "AdminLockConfirm": (
        "{0} ({1}) sperren? Die Person wird sofort abgemeldet und kann sich nicht mehr anmelden, bis du sie "
        "entsperrst. Ihre Daten bleiben erhalten.",
        "Lock {0} ({1})? They are signed out immediately and cannot sign in until you unlock them. Their data is "
        "kept.",
    ),
    "AdminLocked": ("{0} ist gesperrt.", "{0} is locked."),
    "AdminUnlock": ("Entsperren", "Unlock"),
    "AdminUnlockConfirm": ("{0} ({1}) entsperren?", "Unlock {0} ({1})?"),
    "AdminUnlocked": ("{0} ist entsperrt.", "{0} is unlocked."),
    "AdminResetTwoFactor": ("Zwei-Faktor-Anmeldung zurücksetzen", "Reset two-factor sign-in"),
    "AdminResetTwoFactorConfirm": (
        "Zwei-Faktor-Anmeldung von {0} ({1}) zurücksetzen? Authenticator-App und Passkeys werden entfernt. Bei der "
        "nächsten Anmeldung richtet die Person nach dem Master-Passwort eine Authenticator-App neu ein. Nur tun, "
        "wenn du sicher weißt, dass sie es selbst ist.",
        "Reset two-factor sign-in for {0} ({1})? The authenticator app and passkeys are removed. At the next "
        "sign-in, after the master password, they set up an authenticator app again. Only do this if you are sure "
        "it is really them.",
    ),
    "AdminTwoFactorWasReset": ("Zwei-Faktor-Anmeldung von {0} ist zurückgesetzt.", "Two-factor sign-in for {0} has been reset."),
    "AdminGrantRole": ("Zum Administrator machen", "Make administrator"),
    "AdminGrantRoleConfirm": (
        "{0} ({1}) zum Administrator machen? Die Person wird abgemeldet und hat nach der nächsten Anmeldung die "
        "Verwaltung, aber keinen Zugriff auf fremde Tresore.",
        "Make {0} ({1}) an administrator? They are signed out and get administration after the next sign-in, but "
        "no access to other people's vaults.",
    ),
    "AdminRevokeRole": ("Administratorrecht entziehen", "Remove administrator rights"),
    "AdminRevokeRoleConfirm": ("{0} ({1}) das Administratorrecht entziehen? Die Person wird abgemeldet.", "Remove administrator rights from {0} ({1})? They are signed out."),
    "AdminRequestDeletion": ("Löschen beantragen", "Request deletion"),
    "AdminRequestDeletionConfirm": (
        "Löschen von {0} ({1}) beantragen? Die Person bekommt eine Mail mit einem Link und muss das Löschen selbst "
        "bestätigen. Erst dann werden Konto und Tresor endgültig gelöscht.",
        "Request deletion of {0} ({1})? They receive an email with a link and must confirm the deletion "
        "themselves. Only then are the account and vault deleted for good.",
    ),
    "AdminDeletionRequested": ("Mail an {0} ist unterwegs.", "Email to {0} is on its way."),
    "AdminCancelDeletion": ("Löschantrag zurückziehen", "Withdraw deletion request"),
    "AdminCancelDeletionConfirm": ("Löschantrag für {0} ({1}) zurückziehen? Der Link in der Mail wird ungültig.", "Withdraw the deletion request for {0} ({1})? The link in the email stops working."),
    "AdminInviteLater": (
        "Neue Konten entstehen über Einladungen. Das Einladen folgt mit dem nächsten Schritt (M2.2).",
        "New accounts are created through invitations. Inviting follows with the next step (M2.2).",
    ),
    # System-Protokoll
    "AdminAuditTitle": ("System-Protokoll", "System log"),
    "AdminAuditText": (
        "Verwaltungsaktionen und Systemereignisse. Was Nutzer in ihren Tresoren tun, steht nur in deren eigenem "
        "Protokoll.",
        "Administrative actions and system events. What users do in their vaults is only in their own log.",
    ),
    "AdminDeletedAccount": ("gelöschtes Konto", "deleted account"),
    "AdminMailTemplate": ("Mail „{0}“", "Email \"{0}\""),
    "Audit_totp-enrolled": ("Authenticator-App neu eingerichtet", "Authenticator app set up again"),
    "Audit_admin-settings-changed": ("Einstellungen geändert", "Settings changed"),
    "Audit_admin-mail-tested": ("Testmail verschickt", "Test email sent"),
    "Audit_admin-ntfy-tested": ("ntfy getestet", "ntfy tested"),
    "Audit_admin-mail-retried": ("Mailversand erneut angestoßen", "Email sending retried"),
    "Audit_admin-user-locked": ("Konto gesperrt", "Account locked"),
    "Audit_admin-user-unlocked": ("Konto entsperrt", "Account unlocked"),
    "Audit_admin-2fa-reset": ("Zwei-Faktor-Anmeldung zurückgesetzt", "Two-factor sign-in reset"),
    "Audit_admin-role-granted": ("Zum Administrator gemacht", "Made administrator"),
    "Audit_admin-role-revoked": ("Administratorrecht entzogen", "Administrator rights removed"),
    "Audit_admin-deletion-requested": ("Löschen beantragt", "Deletion requested"),
    "Audit_admin-deletion-cancelled": ("Löschantrag zurückgezogen", "Deletion request withdrawn"),
    "Audit_account-deleted": ("Konto gelöscht", "Account deleted"),
    "Audit_mail-failed": ("Mail nicht zustellbar", "Email could not be delivered"),
    # Datenschutz und Kontolöschung
    "PrivacyTitle": ("Datenschutz", "Privacy"),
    "PrivacyDefault": PRIVACY_DEFAULT,
    "DeletionTitle": ("Konto löschen", "Delete account"),
    "DeletionInvalid": (
        "Dieser Link ist ungültig, abgelaufen oder wurde schon verwendet. Dein Konto bleibt bestehen.",
        "This link is invalid, has expired or has already been used. Your account remains.",
    ),
    "DeletionText": (
        "Ein Administrator hat beantragt, das Konto von {0} ({1}) zu löschen.",
        "An administrator has requested to delete the account of {0} ({1}).",
    ),
    "DeletionWarning": (
        "Beim Löschen verschwinden dein Tresor mit allen Einträgen und Dokumenten endgültig. Das lässt sich nicht "
        "rückgängig machen. Exportiere vorher, was du behalten willst.",
        "Deleting removes your vault with all entries and documents for good. This cannot be undone. Export "
        "anything you want to keep first.",
    ),
    "DeletionExpires": ("Der Link gilt bis {0}.", "The link is valid until {0}."),
    "DeletionUnderstood": ("Ich habe verstanden, dass alles endgültig gelöscht wird.", "I understand that everything will be deleted for good."),
    "DeletionKeep": ("Konto behalten", "Keep account"),
    "DeletionConfirm": ("Endgültig löschen", "Delete for good"),
    "DeletionDone": ("Dein Konto und dein Tresor sind gelöscht.", "Your account and vault have been deleted."),
    # Anmeldung
    "LoginEnrollText": (
        "Der Administrator hat deine Zwei-Faktor-Anmeldung zurückgesetzt. Richte jetzt eine Authenticator-App neu "
        "ein: QR-Code scannen und den angezeigten Code eingeben.",
        "The administrator has reset your two-factor sign-in. Set up an authenticator app again now: scan the QR "
        "code and enter the code it shows.",
    ),
    "LoginEnrollSecret": ("Oder diesen Schlüssel von Hand eingeben:", "Or enter this key manually:"),
    "AuthError_auth-disabled": (
        "Dein Konto ist gesperrt. Bitte wende dich an den Administrator.",
        "Your account is locked. Please contact the administrator.",
    ),
}

MAIL = {
    "Mail_footer": (
        "Diese Nachricht hat myLife ({instance}) automatisch verschickt.",
        "This message was sent automatically by myLife ({instance}).",
    ),
    "Mail_test_subject": ("myLife: Testnachricht", "myLife: test message"),
    "Mail_test_body": (
        "Hallo {name},\n\ndiese Testnachricht zeigt, dass der Mailversand von myLife funktioniert.",
        "Hello {name},\n\nthis test message shows that myLife can send emails.",
    ),
    "Mail_account-locked_subject": ("Dein myLife-Konto wurde gesperrt", "Your myLife account has been locked"),
    "Mail_account-locked_body": (
        "Hallo {name},\n\nein Administrator hat dein Konto bei myLife gesperrt. Du kannst dich vorerst nicht "
        "anmelden. Deine Daten bleiben unverändert und verschlüsselt.\n\nWenn du nicht weißt, warum, wende dich "
        "bitte an den Administrator.",
        "Hello {name},\n\nan administrator has locked your myLife account. You cannot sign in for now. Your data "
        "remains unchanged and encrypted.\n\nIf you do not know why, please contact the administrator.",
    ),
    "Mail_account-unlocked_subject": ("Dein myLife-Konto ist wieder entsperrt", "Your myLife account has been unlocked"),
    "Mail_account-unlocked_body": (
        "Hallo {name},\n\nein Administrator hat dein Konto bei myLife wieder entsperrt. Du kannst dich wieder "
        "anmelden.",
        "Hello {name},\n\nan administrator has unlocked your myLife account. You can sign in again.",
    ),
    "Mail_twofactor-reset_subject": ("myLife: Zwei-Faktor-Anmeldung zurückgesetzt", "myLife: two-factor sign-in reset"),
    "Mail_twofactor-reset_body": (
        "Hallo {name},\n\nein Administrator hat die Zwei-Faktor-Anmeldung deines myLife-Kontos zurückgesetzt. "
        "Authenticator-App und Passkeys sind entfernt. Bei der nächsten Anmeldung richtest du nach dem "
        "Master-Passwort eine Authenticator-App neu ein.\n\nWenn du das nicht veranlasst hast, melde dich bitte "
        "sofort beim Administrator.",
        "Hello {name},\n\nan administrator has reset the two-factor sign-in of your myLife account. The "
        "authenticator app and passkeys have been removed. At your next sign-in, after the master password, you "
        "set up an authenticator app again.\n\nIf you did not ask for this, please contact the administrator "
        "immediately.",
    ),
    "Mail_deletion-request_subject": ("Bitte bestätige: myLife-Konto löschen", "Please confirm: delete myLife account"),
    "Mail_deletion-request_body": (
        "Hallo {name},\n\nein Administrator hat beantragt, dein Konto bei myLife zu löschen. Dabei werden dein "
        "Tresor mit allen Einträgen und Dokumenten endgültig gelöscht.\n\nNur wenn du einverstanden bist, öffne "
        "diesen Link und bestätige dort:\n{link}\n\nDer Link gilt {days} Tage. Wenn du nichts tust, bleibt dein "
        "Konto bestehen.",
        "Hello {name},\n\nan administrator has requested to delete your myLife account. This deletes your vault "
        "with all entries and documents for good.\n\nOnly if you agree, open this link and confirm there:\n{link}"
        "\n\nThe link is valid for {days} days. If you do nothing, your account remains.",
    ),
    "Mail_account-deleted_subject": ("Dein myLife-Konto wurde gelöscht", "Your myLife account has been deleted"),
    "Mail_account-deleted_body": (
        "Hallo {name},\n\nwie von dir bestätigt sind dein myLife-Konto und dein Tresor endgültig gelöscht.",
        "Hello {name},\n\nas you confirmed, your myLife account and vault have been deleted for good.",
    ),
}
