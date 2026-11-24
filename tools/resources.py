#!/usr/bin/env python3
"""Erzeugt SharedResource.resx (Deutsch, neutral) und SharedResource.en.resx aus einer Quelle,
damit beide Sprachen immer dieselben Schlüssel haben.

Aufruf: python3 tools/resources.py
"""
import sys
from pathlib import Path
from xml.sax.saxutils import escape

sys.path.insert(0, str(Path(__file__).resolve().parent))
import resources_admin  # noqa: E402
import resources_vault  # noqa: E402

TEXTS: dict[str, tuple[str, str]] = {
    "AppName": ("myLife", "myLife"),
    "AppTagline": ("Dein digitaler Notfallordner", "Your digital emergency folder"),
    "NavHome": ("Übersicht", "Overview"),
    "OpenMenu": ("Menü öffnen", "Open menu"),
    "ToggleDarkMode": ("Hell/Dunkel umschalten", "Toggle light/dark mode"),
    "Language": ("Sprache", "Language"),
    "LanguageName_de": ("Deutsch", "Deutsch"),
    "LanguageName_en": ("English", "English"),
    "Loading": ("Wird geladen …", "Loading …"),
    "HomeTitle": ("Willkommen bei myLife", "Welcome to myLife"),
    "HomeIntro": (
        "Hier entsteht dein digitaler Notfallordner: Versicherungen, Konten, Verträge, Urkunden und "
        "Wünsche an einem sicheren Ort. Alles wird in deinem Browser verschlüsselt, sodass nur du "
        "und im Ernstfall deine Vertrauenspersonen die Daten lesen können.",
        "This is where your digital emergency folder takes shape: insurance policies, accounts, "
        "contracts, certificates and wishes in one safe place. Everything is encrypted in your "
        "browser, so only you and, in an emergency, your trusted persons can read it.",
    ),
    "HomeStatusTitle": ("Status der Instanz", "Instance status"),
    "HomeStatusReachable": ("Server erreichbar", "Server reachable"),
    "HomeStatusUnreachable": (
        "Der Server ist nicht erreichbar. Bitte versuche es später erneut.",
        "The server cannot be reached. Please try again later.",
    ),
    "HomeStatusVersion": ("Version", "Version"),
    "HomeStatusCryptoFormat": ("Verschlüsselungsformat", "Encryption format"),
    "HomeNextStepsTitle": ("Wie geht es weiter?", "What comes next?"),
    "HomeNextStepsText": (
        "Als Nächstes folgen die Objekttypen und der verschlüsselte Tresor.",
        "Next up: object types and the encrypted vault.",
    ),
    "NotFoundTitle": ("Seite nicht gefunden", "Page not found"),
    "NotFoundText": (
        "Die aufgerufene Seite gibt es nicht.",
        "The page you requested does not exist.",
    ),
    "BackToHome": ("Zur Übersicht", "Back to overview"),
    "ErrorUnhandled": ("Ein unerwarteter Fehler ist aufgetreten.", "An unexpected error has occurred."),
    "Reload": ("Neu laden", "Reload"),
    # Einrichtungsassistent (M1.2)
    "SetupTitle": ("Einrichtung", "Setup"),
    "SetupStepOf": ("Schritt {0} von {1}", "Step {0} of {1}"),
    "SetupBack": ("Zurück", "Back"),
    "SetupNext": ("Weiter", "Next"),
    "SetupFinish": ("Tresor anlegen", "Create vault"),
    "SetupAlreadyDone": ("myLife ist bereits eingerichtet.", "myLife has already been set up."),
    "SetupStepWelcome": ("Willkommen", "Welcome"),
    "SetupStepAccount": ("Dein Konto", "Your account"),
    "SetupStepNotInVault": ("Was nicht hineingehört", "What does not belong in it"),
    "SetupStepPassword": ("Master-Passwort", "Master password"),
    "SetupStepRecovery": ("Wiederherstellungs-Kit", "Recovery kit"),
    "SetupStepTwoFactor": ("Zweiter Faktor", "Second factor"),
    "SetupStepFinish": ("Abschluss", "Finish"),
    "SetupWelcomeText": (
        "Diese Instanz ist noch nicht eingerichtet. Du legst jetzt das erste Konto an. Es wird Administrator "
        "und bekommt einen eigenen Tresor. Alle Schlüssel entstehen in deinem Browser, der Server sieht nur "
        "verschlüsselte Daten.",
        "This instance has not been set up yet. You are about to create the first account. It becomes the "
        "administrator and gets its own vault. All keys are created in your browser; the server only ever "
        "sees encrypted data.",
    ),
    "SetupTokenLabel": ("Einrichtungscode", "Setup code"),
    "SetupTokenHelp": (
        "Steht im Protokoll des Containers, etwa im Container Manager unter mylife-app → Protokoll. "
        "Nach jedem Neustart gilt ein neuer Code.",
        "Shown in the container log, for example in Container Manager under mylife-app → Log. "
        "A new code is issued after every restart.",
    ),
    "SetupTokenRequired": ("Bitte den Einrichtungscode eingeben.", "Please enter the setup code."),
    "SetupAccountText": (
        "Als Administrator verwaltest du Einstellungen und Benutzer. In fremde Tresore kannst du trotzdem nicht "
        "hineinsehen.",
        "As administrator you manage settings and users. You still cannot look into anyone else's vault.",
    ),
    "SetupDisplayName": ("Anzeigename", "Display name"),
    "SetupDisplayNameRequired": ("Bitte einen Namen eingeben (höchstens 100 Zeichen).", "Please enter a name (at most 100 characters)."),
    "SetupEmail": ("E-Mail-Adresse", "Email address"),
    "SetupEmailHelp": (
        "Für die Anmeldung, für Erinnerungen und für den Notfallzugang.",
        "Used for signing in, for reminders and for emergency access.",
    ),
    "SetupEmailInvalid": ("Bitte eine gültige E-Mail-Adresse eingeben.", "Please enter a valid email address."),
    "NotInVaultTitle": ("Was gehört nicht in myLife?", "What does not belong in myLife?"),
    "NotInVaultIntro": (
        "myLife sagt deiner Familie, was es gibt und wo es liegt. Zugangsdaten, mit denen jemand sofort an "
        "Geld kommt, gehören nicht hinein:",
        "myLife tells your family what exists and where to find it. Credentials that give someone immediate "
        "access to money do not belong in it:",
    ),
    "NotInVaultPins": (
        "PINs, TANs und vollständige Kartennummern",
        "PINs, TANs and full card numbers",
    ),
    "NotInVaultPasswords": (
        "Einzelne Passwörter. Nenne stattdessen deinen Passwortmanager und wo sein Notfallzugang liegt.",
        "Individual passwords. Name your password manager instead and where its emergency access is kept.",
    ),
    "NotInVaultSeeds": (
        "Seed-Phrasen von Kryptowährungen, nur den Ort, an dem sie liegen",
        "Cryptocurrency seed phrases, only the place where they are kept",
    ),
    "NotInVaultOriginals": (
        "Originale von Urkunden, nur ihren Ablageort",
        "Original certificates, only where they are filed",
    ),
    "NotInVaultWhy": (
        "myLife warnt dich, wenn eine Eingabe danach aussieht.",
        "myLife warns you when something you enter looks like one of these.",
    ),
    "NotInVaultAcknowledge": ("Verstanden", "Understood"),
    "SetupPasswordText": (
        "Das Master-Passwort entschlüsselt deinen Tresor. Niemand kann es zurücksetzen, auch der Administrator "
        "nicht. Geht es verloren, hilft nur das Wiederherstellungs-Kit aus dem nächsten Schritt.",
        "The master password decrypts your vault. Nobody can reset it, not even the administrator. If you lose "
        "it, only the recovery kit from the next step can help.",
    ),
    "SetupPassword": ("Master-Passwort", "Master password"),
    "SetupPasswordRepeat": ("Passwort wiederholen", "Repeat password"),
    "SetupPasswordHelp": (
        "Mindestens {0} Zeichen. Gut merkbar und sicher ist ein Satz aus mehreren Wörtern.",
        "At least {0} characters. A sentence of several words is both memorable and secure.",
    ),
    "SetupPasswordTooShort": ("Mindestens {0} Zeichen", "At least {0} characters"),
    "SetupPasswordMismatch": ("Die Passwörter stimmen nicht überein.", "The passwords do not match."),
    "SetupPasswordAcknowledge": (
        "Ich habe verstanden, dass niemand mein Master-Passwort zurücksetzen kann.",
        "I understand that nobody can reset my master password.",
    ),
    "SetupPasswordStrength": ("Stärke: {0}", "Strength: {0}"),
    "SetupPasswordStrengthWeak": ("zu kurz", "too short"),
    "SetupPasswordStrengthFair": ("ausreichend", "fair"),
    "SetupPasswordStrengthStrong": ("stark", "strong"),
    "SetupShowPassword": ("Passwort anzeigen", "Show password"),
    "SetupRecoveryText": (
        "Mit diesen 24 Wörtern kommst du an deinen Tresor, wenn du das Master-Passwort vergisst. Drucke das Kit "
        "aus oder schreibe die Wörter ab und bewahre es getrennt vom Passwort auf, zum Beispiel im Schließfach.",
        "These 24 words get you into your vault if you forget the master password. Print the kit or write the "
        "words down and keep it separate from the password, for example in a safe deposit box.",
    ),
    "SetupRecoveryPrint": ("Kit drucken", "Print kit"),
    "SetupRecoveryConfirmText": (
        "Zur Kontrolle: Gib die Wörter an diesen Stellen ein.",
        "To check: enter the words at these positions.",
    ),
    "SetupRecoveryWordNumber": ("Wort {0}", "Word {0}"),
    "SetupRecoveryWordWrong": ("Stimmt nicht", "Does not match"),
    "SetupRecoveryStored": (
        "Ich habe das Kit ausgedruckt oder abgeschrieben und sicher verwahrt.",
        "I have printed or written down the kit and stored it safely.",
    ),
    "RecoveryKitTitle": ("myLife – Wiederherstellungs-Kit", "myLife – Recovery kit"),
    "RecoveryKitAccount": ("Konto", "Account"),
    "RecoveryKitInstance": ("Instanz", "Instance"),
    "RecoveryKitCreated": ("Erstellt am", "Created on"),
    "RecoveryKitWords": ("Wiederherstellungswörter", "Recovery words"),
    "RecoveryKitInstructions": (
        "So kommst du wieder an deinen Tresor: Auf der Anmeldeseite „Passwort vergessen“ wählen und die 24 Wörter "
        "eingeben oder den QR-Code scannen. Danach vergibst du ein neues Master-Passwort.",
        "To regain access to your vault: choose “Forgot password” on the sign-in page and enter the 24 words or "
        "scan the QR code. Then set a new master password.",
    ),
    "RecoveryKitWarning": (
        "Wer diese Wörter kennt, kann deinen Tresor öffnen. Nicht fotografieren und nicht digital speichern.",
        "Anyone who knows these words can open your vault. Do not photograph them or store them digitally.",
    ),
    "SetupTwoFactorText": (
        "Zur Anmeldung brauchst du zusätzlich einen Code aus einer Authenticator-App, zum Beispiel Aegis, "
        "Google Authenticator, Microsoft Authenticator oder deinem Passwortmanager. Scanne den QR-Code und gib "
        "den angezeigten Code ein.",
        "To sign in you also need a code from an authenticator app such as Aegis, Google Authenticator, "
        "Microsoft Authenticator or your password manager. Scan the QR code and enter the code shown.",
    ),
    "SetupTwoFactorSecret": ("Schlüssel zum manuellen Eingeben", "Key for manual entry"),
    "SetupTwoFactorCode": ("6-stelliger Code", "6-digit code"),
    "SetupTwoFactorCodeRequired": ("Bitte den 6-stelligen Code eingeben.", "Please enter the 6-digit code."),
    "SetupTwoFactorPasskeyLater": (
        "Einen Passkey kannst du nach der ersten Anmeldung unter „Sicherheit“ zusätzlich einrichten.",
        "You can add a passkey under “Security” after you first sign in.",
    ),
    "SetupFinishText": (
        "Alles bereit. Beim Anlegen erzeugt dein Browser die Schlüssel. Das dauert einige Sekunden.",
        "Everything is ready. Your browser creates the keys when you continue. This takes a few seconds.",
    ),
    "SetupFinishCreating": ("Schlüssel werden erzeugt …", "Creating keys …"),
    "SetupFinishDoneTitle": ("Dein Tresor ist angelegt", "Your vault has been created"),
    "SetupFinishDoneText": (
        "Ab jetzt meldest du dich mit deiner E-Mail-Adresse, dem Master-Passwort und dem Code aus der "
        "Authenticator-App an.",
        "From now on you sign in with your email address, your master password and the code from your "
        "authenticator app.",
    ),
    "SetupError_setup-invalid-token": (
        "Der Einrichtungscode stimmt nicht. Nach einem Neustart des Containers gilt ein neuer Code.",
        "The setup code is not correct. A new code is issued whenever the container restarts.",
    ),
    "SetupError_setup-already-completed": ("myLife ist bereits eingerichtet.", "myLife has already been set up."),
    "SetupError_setup-totp-expired": (
        "Die Einrichtung des zweiten Faktors ist abgelaufen. Bitte scanne den neuen QR-Code.",
        "Setting up the second factor has expired. Please scan the new QR code.",
    ),
    "SetupError_setup-totp-invalid-code": (
        "Der Code stimmt nicht. Bitte prüfe die Uhrzeit des Smartphones und versuche es erneut.",
        "The code is not correct. Please check the time on your phone and try again.",
    ),
    "SetupError_setup-rate-limited": ("Zu viele Versuche. Bitte warte eine Minute.", "Too many attempts. Please wait a minute."),
    "SetupError_setup-invalid-input": ("Einige Angaben sind ungültig. Bitte prüfe sie.", "Some entries are invalid. Please check them."),
    "SetupError_setup-failed": (
        "Die Einrichtung ist fehlgeschlagen. Bitte versuche es erneut.",
        "Setup failed. Please try again.",
    ),
    # Anmeldung und Konto (M1.3)
    "UserMenu": ("Konto", "Account"),
    "NavSecurity": ("Sicherheit", "Security"),
    "Logout": ("Abmelden", "Sign out"),
    "Delete": ("Löschen", "Delete"),
    "Cancel": ("Abbrechen", "Cancel"),
    "LoginTitle": ("Anmelden", "Sign in"),
    "UnlockTitle": ("Tresor entsperren", "Unlock vault"),
    "UnlockText": (
        "Hallo {0}, du bist noch angemeldet. Zum Entsperren des Tresors brauchen wir dein Master-Passwort.",
        "Hello {0}, you are still signed in. Enter your master password to unlock your vault.",
    ),
    "LoginPasswordRequired": ("Bitte das Master-Passwort eingeben.", "Please enter your master password."),
    "LoginSecondFactorText": (
        "Bestätige die Anmeldung mit dem Code aus deiner Authenticator-App oder mit einem Passkey.",
        "Confirm the sign-in with the code from your authenticator app or with a passkey.",
    ),
    "LoginWithPasskey": ("Mit Passkey anmelden", "Sign in with passkey"),
    "LoginChecking": ("Passwort wird geprüft …", "Checking password …"),
    "LoginOpening": ("Tresor wird geöffnet …", "Opening vault …"),
    "LoginForgotPassword": ("Passwort vergessen?", "Forgot password?"),
    "LoginSubmit": ("Anmelden", "Sign in"),
    "LoginConfirm": ("Bestätigen", "Confirm"),
    "UnlockSubmit": ("Entsperren", "Unlock"),
    "LoginInfoTimeout": (
        "Du wurdest nach längerer Inaktivität abgemeldet. Die Schlüssel wurden aus dem Speicher entfernt.",
        "You were signed out after a period of inactivity. The keys have been removed from memory.",
    ),
    "LoginInfoExpired": ("Deine Sitzung ist abgelaufen. Bitte melde dich erneut an.", "Your session has expired. Please sign in again."),
    "RecoverTitle": ("Zugang wiederherstellen", "Recover access"),
    "RecoverText": (
        "Mit den 24 Wörtern aus deinem Wiederherstellungs-Kit vergibst du ein neues Master-Passwort. Dein Tresor "
        "bleibt dabei unverändert. Zur Anmeldung brauchst du danach weiterhin deinen zweiten Faktor.",
        "Use the 24 words from your recovery kit to set a new master password. Your vault stays unchanged. You "
        "will still need your second factor to sign in afterwards.",
    ),
    "RecoverWordsHelp": (
        "Durch Leerzeichen oder Zeilen getrennt. Die ersten vier Buchstaben jedes Worts genügen.",
        "Separated by spaces or lines. The first four letters of each word are enough.",
    ),
    "RecoverWordsRequired": ("Bitte die 24 Wörter eingeben.", "Please enter the 24 words."),
    "RecoverNewPassword": ("Neues Master-Passwort", "New master password"),
    "RecoverBackToLogin": ("Zur Anmeldung", "Back to sign-in"),
    "RecoverSubmit": ("Neues Passwort setzen", "Set new password"),
    "RecoverDone": (
        "Dein neues Master-Passwort ist gesetzt. Melde dich jetzt damit und mit deinem zweiten Faktor an.",
        "Your new master password has been set. Sign in with it and your second factor now.",
    ),
    "SecurityPasswordTitle": ("Master-Passwort ändern", "Change master password"),
    "SecurityPasswordText": (
        "Dein Tresor wird dabei nur neu verpackt, an den Daten ändert sich nichts. Andere angemeldete Geräte werden abgemeldet.",
        "Only the key wrapping changes; your data stays the same. Other signed-in devices are signed out.",
    ),
    "SecurityCurrentPassword": ("Aktuelles Master-Passwort", "Current master password"),
    "SecurityChangePassword": ("Passwort ändern", "Change password"),
    "SecurityPasswordChanged": ("Das Master-Passwort wurde geändert.", "Your master password has been changed."),
    "SecurityPasskeysTitle": ("Passkeys", "Passkeys"),
    "SecurityPasskeysText": (
        "Ein Passkey ersetzt bei der Anmeldung den Code aus der Authenticator-App, etwa mit Fingerabdruck, Gesicht "
        "oder einem Sicherheitsschlüssel. Das Master-Passwort brauchst du weiterhin.",
        "A passkey replaces the authenticator code when signing in, for example with your fingerprint, face or a "
        "security key. You still need your master password.",
    ),
    "SecurityNoPasskeys": ("Noch kein Passkey eingerichtet.", "No passkey set up yet."),
    "SecurityPasskeyName": ("Bezeichnung", "Name"),
    "SecurityPasskeyNamePlaceholder": ("z. B. Laptop oder Smartphone", "e.g. laptop or phone"),
    "SecurityPasskeyCreated": ("Eingerichtet", "Added"),
    "SecurityPasskeyLastUsed": ("Zuletzt verwendet", "Last used"),
    "SecurityAddPasskey": ("Passkey hinzufügen", "Add passkey"),
    "SecurityPasskeyAdded": ("Der Passkey wurde hinzugefügt.", "The passkey has been added."),
    "SecurityPasskeyDeleted": ("Der Passkey wurde gelöscht.", "The passkey has been deleted."),
    "SecurityDeletePasskeyTitle": ("Passkey löschen", "Delete passkey"),
    "SecurityDeletePasskeyText": (
        "Soll der Passkey „{0}“ gelöscht werden? Mit ihm ist danach keine Anmeldung mehr möglich.",
        "Delete the passkey “{0}”? It can no longer be used to sign in.",
    ),
    "AuthError_auth-invalid-credentials": (
        "E-Mail-Adresse oder Master-Passwort stimmen nicht.",
        "The email address or master password is not correct.",
    ),
    "AuthError_auth-locked": (
        "Zu viele Fehlversuche. Das Konto ist für 15 Minuten gesperrt.",
        "Too many failed attempts. The account is locked for 15 minutes.",
    ),
    "AuthError_auth-expired": (
        "Die Anmeldung hat zu lange gedauert. Bitte beginne von vorn.",
        "The sign-in took too long. Please start again.",
    ),
    "AuthError_auth-invalid-code": (
        "Der Code stimmt nicht oder wurde schon verwendet. Bitte warte auf den nächsten Code.",
        "The code is not correct or has already been used. Please wait for the next code.",
    ),
    "AuthError_auth-passkey-failed": ("Der Passkey konnte nicht geprüft werden.", "The passkey could not be verified."),
    "AuthError_auth-passkey-cancelled": (
        "Der Passkey wurde nicht verwendet oder der Vorgang abgebrochen.",
        "The passkey was not used or the process was cancelled.",
    ),
    "AuthError_auth-passkey-unsupported": ("Dieser Browser unterstützt keine Passkeys.", "This browser does not support passkeys."),
    "AuthError_auth-invalid-input": ("Einige Angaben sind ungültig.", "Some entries are invalid."),
    "AuthError_auth-rate-limited": ("Zu viele Anfragen. Bitte warte eine Minute.", "Too many requests. Please wait a minute."),
    "AuthError_auth-failed": ("Das hat nicht geklappt. Bitte versuche es erneut.", "That did not work. Please try again."),
    "AuthError_auth-wrong-password": ("Das Master-Passwort stimmt nicht.", "The master password is not correct."),
    "AuthError_auth-invalid-recovery-words": (
        "Die Wörter ergeben keinen gültigen Wiederherstellungsschlüssel. Bitte prüfe Reihenfolge und Schreibweise.",
        "These words do not form a valid recovery key. Please check the order and spelling.",
    ),
    "AuthError_auth-crypto-failed": (
        "Die Schlüssel konnten im Browser nicht verarbeitet werden.",
        "The keys could not be processed in your browser.",
    ),
    "SetupErrorCrypto": (
        "Die Schlüssel konnten im Browser nicht erzeugt werden. Bitte verwende einen aktuellen Browser.",
        "The keys could not be created in your browser. Please use an up-to-date browser.",
    ),
}

_VAULT = resources_vault.build()
_DUPLICATES = sorted(set(TEXTS) & set(_VAULT))
if _DUPLICATES:
    raise SystemExit(f"Doppelte Schlüssel: {_DUPLICATES}")
TEXTS.update(_VAULT)
_DUPLICATES = sorted(set(TEXTS) & set(resources_admin.CLIENT))
if _DUPLICATES:
    raise SystemExit(f"Doppelte Schlüssel: {_DUPLICATES}")
TEXTS.update(resources_admin.CLIENT)

HEADER = """<?xml version="1.0" encoding="utf-8"?>
<!-- Automatisch erzeugt mit tools/resources.py. Nicht von Hand bearbeiten. -->
<root>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
"""


def write(path: Path, index: int, texts: dict[str, tuple[str, str]] = TEXTS) -> None:
    lines = [HEADER]
    for key in sorted(texts):
        value = escape(texts[key][index])
        lines.append(f'  <data name="{key}" xml:space="preserve"><value>{value}</value></data>\n')
    lines.append("</root>\n")
    path.write_text("".join(lines), encoding="utf-8")


if __name__ == "__main__":
    target = Path(__file__).resolve().parent.parent / "src" / "MyLife.Client" / "Resources"
    write(target / "SharedResource.resx", 0)
    write(target / "SharedResource.en.resx", 1)
    print(f"{len(TEXTS)} Texte geschrieben nach {target}")
    server = Path(__file__).resolve().parent.parent / "src" / "MyLife.Server" / "Resources"
    server.mkdir(exist_ok=True)
    write(server / "MailResource.resx", 0, resources_admin.MAIL)
    write(server / "MailResource.en.resx", 1, resources_admin.MAIL)
    print(f"{len(resources_admin.MAIL)} Mailtexte geschrieben nach {server}")
