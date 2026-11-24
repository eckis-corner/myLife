"""Texte für Familie, Einladungen und Schlüsselprüfung (M2.2). Wird von tools/resources.py eingebunden.

CLIENT: Oberfläche (SharedResource.resx im Client). MAIL: Mailvorlagen (MailResource.resx im Server).
"""

CLIENT = {
    "FamilyTitle": ("Familie", "Family"),
    "FamilyText": (
        "Alle Personen dieser Instanz. myLife prüft ihre Schlüssel automatisch: Wen du eingeladen hast, bestätigt "
        "dein Browser nach der Annahme selbst; andere gelten als bestätigt, wenn jemand, dem du vertraust, sie "
        "gegengezeichnet hat. Nur an bestätigte Schlüssel gibt myLife später Freigaben heraus.",
        "Everyone on this instance. myLife checks their keys automatically: people you invited are confirmed by "
        "your browser after they accept; others count as confirmed when someone you trust has endorsed them. "
        "Later, myLife only hands out access to confirmed keys.",
    ),
    "FamilyEmpty": ("Noch niemand außer dir. Lade jemanden ein.", "Nobody besides you yet. Invite someone."),
    "FamilyKey": ("Schlüssel", "Key"),
    "FamilyFingerprint": ("Fingerprint", "Fingerprint"),
    "FamilyMyFingerprint": ("Dein eigener Fingerprint: {0}", "Your own fingerprint: {0}"),
    "FamilyTrust_Pinned": ("Von dir bestätigt", "Confirmed by you"),
    "FamilyTrust_Endorsed": ("Bestätigt durch {0}", "Confirmed by {0}"),
    "FamilyTrust_Unconfirmed": ("Noch nicht bestätigt", "Not yet confirmed"),
    "FamilyTrust_Changed": ("Schlüssel geändert!", "Key changed!"),
    "FamilyTrustHint_Pinned": (
        "Dein Browser hat diesen Schlüssel selbst geprüft und sich gemerkt. Er ist unverändert.",
        "Your browser checked this key itself and remembered it. It is unchanged.",
    ),
    "FamilyTrustHint_Endorsed": (
        "Jemand, dessen Schlüssel du selbst bestätigt hast, hat diesen Schlüssel gegengezeichnet.",
        "Someone whose key you confirmed yourself has endorsed this key.",
    ),
    "FamilyTrustHint_Unconfirmed": (
        "Noch hat niemand, dem du vertraust, diesen Schlüssel bestätigt. Das ist kein Fehler; es ändert sich, "
        "sobald jemand aus der Familie ihn gegenzeichnet.",
        "Nobody you trust has confirmed this key yet. This is not an error; it changes as soon as someone in the "
        "family endorses it.",
    ),
    "FamilyTrustHint_Changed": (
        "Der Server zeigt einen anderen Schlüssel als den, den dein Browser sich gemerkt hat. myLife erzeugt dafür "
        "keine Freigaben.",
        "The server shows a different key than the one your browser remembered. myLife will not create any "
        "access for it.",
    ),
    "FamilyKeyChangedWarning": (
        "Achtung: Bei mindestens einer Person stimmt der Schlüssel nicht mehr mit dem überein, den dein Browser "
        "bestätigt hat. Das kann ein Angriff auf den Server sein. Sprich mit der Person auf anderem Weg und "
        "wende dich an den Administrator.",
        "Warning: For at least one person, the key no longer matches the one your browser confirmed. This could "
        "be an attack on the server. Talk to the person through another channel and contact the administrator.",
    ),
    "FamilyInvitations": ("Deine Einladungen", "Your invitations"),
    "FamilyInvitationsEmpty": ("Keine Einladungen.", "No invitations."),
    "FamilyInvitationDate": ("Datum", "Date"),
    "FamilyValidUntil": ("gültig bis {0}", "valid until {0}"),
    "FamilyAcceptedAt": ("angenommen am {0}", "accepted on {0}"),
    "FamilyResend": ("Neuen Link per Mail senden", "Send a new link by email"),
    "FamilyNewLink": ("Neuen Link zum Weitergeben", "New link to share"),
    "FamilyRevoke": ("Einladung zurückziehen", "Withdraw invitation"),
    "FamilyRevokeConfirm": ("Einladung an {0} zurückziehen? Der Link funktioniert danach nicht mehr.", "Withdraw the invitation to {0}? The link will stop working."),
    "InvitationStatus_Pending": ("Offen", "Open"),
    "InvitationStatus_Accepted": ("Angenommen, Prüfung folgt", "Accepted, check pending"),
    "InvitationStatus_Verified": ("Angenommen und bestätigt", "Accepted and confirmed"),
    "InvitationStatus_Mismatch": ("Schlüssel passt nicht!", "Key does not match!"),
    "InvitationStatus_Expired": ("Abgelaufen", "Expired"),
    "InvitationStatus_Revoked": ("Zurückgezogen", "Withdrawn"),
    "InviteDialogTitle": ("Person einladen", "Invite someone"),
    "InviteDialogText": (
        "Die Person bekommt ein eigenes Konto mit eigenem Tresor. Später kannst du sie als Vertrauensperson "
        "einsetzen. Sie sieht von deinem Tresor nichts, solange du nichts freigibst.",
        "They get their own account with their own vault. Later you can make them a trusted person. They see "
        "nothing of your vault unless you share it.",
    ),
    "InviteLanguage": ("Sprache der Einladung", "Language of the invitation"),
    "InviteByMail": ("Per Mail von myLife senden", "Send by email from myLife"),
    "InviteByLink": ("Link selbst weitergeben (etwa per Messenger)", "Share the link yourself (e.g. via messenger)"),
    "InviteByMailHint": (
        "Bequem und der Normalfall. Der Server sieht den Link dabei einmal.",
        "Convenient and the usual way. The server sees the link once while sending.",
    ),
    "InviteByLinkHint": (
        "Am sichersten: Der Link mit dem Geheimnis entsteht nur in deinem Browser, der Server sieht ihn nie.",
        "Most secure: The link with the secret only exists in your browser; the server never sees it.",
    ),
    "InviteSubmit": ("Einladen", "Invite"),
    "InviteMailQueued": ("Die Einladung ist unterwegs.", "The invitation is on its way."),
    "InviteLinkText": (
        "Schicke diesen Link auf eigenem Weg an die Person. Wer ihn hat, kann das Konto anlegen; gib ihn also nur "
        "an sie weiter.",
        "Send this link to the person yourself. Whoever has it can create the account, so only share it with them.",
    ),
    "InviteCopy": ("Link kopieren", "Copy link"),
    "InviteCopied": ("Kopiert.", "Copied."),
    "InviteCopyFailed": ("Kopieren nicht erlaubt; bitte von Hand markieren.", "Copying not allowed; please select it manually."),
    "InviteExpires": ("Der Link gilt bis {0}.", "The link is valid until {0}."),
    "InviteTitle": ("Einladung annehmen", "Accept invitation"),
    "InviteWelcomeText": (
        "{0} lädt dich zu myLife ein, dem digitalen Notfallordner der Familie. Du richtest jetzt dein eigenes "
        "Konto mit einem eigenen, verschlüsselten Tresor ein. Dafür brauchst du etwa zehn Minuten, einen Drucker "
        "oder Stift und Papier und eine Authenticator-App auf dem Smartphone.",
        "{0} invites you to myLife, the family's digital emergency folder. You are now setting up your own "
        "account with your own encrypted vault. You need about ten minutes, a printer or pen and paper, and an "
        "authenticator app on your phone.",
    ),
    "InviteInvalid": (
        "Dieser Einladungslink ist ungültig, abgelaufen oder wurde schon verwendet. Bitte die einladende Person um "
        "einen neuen Link.",
        "This invitation link is invalid, has expired or has already been used. Please ask the person who invited "
        "you for a new link.",
    ),
    "AdminOpenInvitations": ("Offene Einladungen", "Open invitations"),
    "AdminInvitedBy": ("Eingeladen von", "Invited by"),
    "FamilyError_family-account-exists": ("Für diese Adresse gibt es schon ein Konto.", "An account already exists for this address."),
    "FamilyError_family-pending": ("Diese Adresse ist schon von jemand anderem eingeladen.", "Someone else has already invited this address."),
    "FamilyError_family-invalid": ("Die Angaben sind unvollständig oder ungültig.", "The input is incomplete or invalid."),
    "FamilyError_family-token-invalid": (
        "Der Einladungslink ist ungültig, abgelaufen oder wurde schon verwendet.",
        "The invitation link is invalid, has expired or has already been used.",
    ),
    "FamilyError_family-not-found": ("Die Einladung gibt es nicht mehr.", "The invitation no longer exists."),
    "FamilyError_family-mail-not-configured": (
        "Der Mailversand ist noch nicht eingerichtet. Gib den Link selbst weiter oder bitte den Administrator, "
        "den Mailversand einzurichten.",
        "Email sending is not set up yet. Share the link yourself or ask the administrator to set up email.",
    ),
    "FamilyError_family-fingerprint": ("Die Gegenzeichnung passt nicht zum aktuellen Schlüssel.", "The endorsement does not match the current key."),
    "FamilyError_family-totp-expired": ("Die Einrichtung der App hat zu lange gedauert. Bitte scanne den neuen QR-Code.", "Setting up the app took too long. Please scan the new QR code."),
    "FamilyError_family-totp-invalid": ("Der Code stimmt nicht. Prüfe die Uhrzeit auf dem Smartphone.", "The code is not correct. Check the time on your phone."),
    "FamilyError_family-failed": ("Das hat nicht geklappt. Bitte versuche es erneut.", "That did not work. Please try again."),
    "Audit_invitation-created": ("Person eingeladen", "Person invited"),
    "Audit_invitation-renewed": ("Einladungslink erneuert", "Invitation link renewed"),
    "Audit_invitation-revoked": ("Einladung zurückgezogen", "Invitation withdrawn"),
    "Audit_invitation-accepted": ("Einladung angenommen", "Invitation accepted"),
    "Audit_account-created": ("Konto angelegt", "Account created"),
    "Audit_key-verified": ("Schlüssel bestätigt", "Key confirmed"),
    "Audit_key-mismatch": ("Schlüssel passt nicht zur Einladung", "Key does not match the invitation"),
    "Audit_key-endorsed": ("Schlüssel gegengezeichnet", "Key endorsed"),
    "Audit_admin-invitation-revoked": ("Einladung zurückgezogen (Verwaltung)", "Invitation withdrawn (administration)"),
}

MAIL = {
    "Mail_invitation_subject": ("{inviter} lädt dich zu myLife ein", "{inviter} invites you to myLife"),
    "Mail_invitation_body": (
        "Hallo,\n\n{inviter} lädt dich zu myLife ein, dem digitalen Notfallordner der Familie. Darin hältst du "
        "fest, was deine Angehörigen im Ernstfall wissen müssen, verschlüsselt und nur für dich lesbar, bis du "
        "etwas freigibst.\n\nÖffne diesen Link, um dein Konto einzurichten:\n{link}\n\nDer Link gilt {days} Tage. "
        "Du brauchst etwa zehn Minuten, einen Drucker oder Stift und Papier und eine Authenticator-App auf dem "
        "Smartphone.\n\nWenn du {inviter} nicht kennst, ignoriere diese Mail einfach.",
        "Hello,\n\n{inviter} invites you to myLife, the family's digital emergency folder. In it you record what "
        "your relatives need to know in an emergency, encrypted and readable only by you until you share "
        "something.\n\nOpen this link to set up your account:\n{link}\n\nThe link is valid for {days} days. You "
        "need about ten minutes, a printer or pen and paper, and an authenticator app on your phone.\n\nIf you "
        "do not know {inviter}, simply ignore this email.",
    ),
    "Mail_invitation-accepted_subject": ("{invitee} hat deine Einladung angenommen", "{invitee} has accepted your invitation"),
    "Mail_invitation-accepted_body": (
        "Hallo {name},\n\n{invitee} hat deine Einladung angenommen und ein Konto bei myLife angelegt.\n\nMelde dich "
        "bei myLife an. Dabei prüft dein Browser automatisch, dass der Schlüssel von {invitee} wirklich zu deiner "
        "Einladung gehört.",
        "Hello {name},\n\n{invitee} has accepted your invitation and created a myLife account.\n\nSign in to "
        "myLife. Your browser then automatically checks that {invitee}'s key really belongs to your invitation.",
    ),
}
