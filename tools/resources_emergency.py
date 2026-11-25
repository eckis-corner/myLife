"""Texte für den Notfallzugang (M2.4): Lebenszeichen, Erinnerungen, Anträge, Widerspruch, Freigabe.
Wird von tools/resources.py eingebunden. CLIENT: Oberfläche. MAIL: Mailvorlagen.
"""

CLIENT = {
    "EmergencyTitle": ("Notfallzugang", "Emergency access"),
    "EmergencyText": (
        "Wenn du dich längere Zeit nicht meldest oder eine Vertrauensperson einen Zugang beantragt und du nicht "
        "widersprichst, gibt myLife frei, was du für diesen Fall hinterlegt hast. Jede Anmeldung zählt als "
        "Lebenszeichen.",
        "If you do not check in for a long time, or a trusted person requests access and you do not object, myLife "
        "releases what you set up for that case. Every sign-in counts as a life sign.",
    ),
    "EmergencyMine": ("Dein Notfallzugang", "Your emergency access"),
    "EmergencyState_Active": ("Aktiv", "Active"),
    "EmergencyState_Overdue": ("Lebenszeichen fällig", "Life sign due"),
    "EmergencyState_Waiting": ("Wartezeit läuft", "Waiting period running"),
    "EmergencyState_Released": ("Freigegeben", "Released"),
    "EmergencyState_Disabled": ("Nicht aktiv", "Not active"),
    "EmergencyDisabled": (
        "Der Notfallzugang wird aktiv, sobald du mindestens eine Vertrauensperson benannt hast. Bis dahin gibt es "
        "keine Erinnerungen.",
        "Emergency access becomes active as soon as you have named at least one trusted person. Until then there "
        "are no reminders.",
    ),
    "EmergencyLastLifesign": ("Letztes Lebenszeichen", "Last life sign"),
    "EmergencyNextDue": ("Nächstes Lebenszeichen fällig", "Next life sign due"),
    "EmergencyNextReminder": ("Nächste Erinnerung", "Next reminder"),
    "EmergencyEarliestRelease": ("Freigabe ohne Reaktion frühestens", "Release without a response, at the earliest"),
    "EmergencyMailDelayed": (
        "Die Erinnerungsmail konnte bisher nicht verschickt werden. Solange das so ist, laufen keine Fristen weiter; "
        "es wird nichts freigegeben. Der Administrator ist informiert.",
        "The reminder email could not be sent so far. As long as this is the case, no deadlines continue and nothing "
        "is released. The administrator has been notified.",
    ),
    "EmergencyLifesignButton": ("Lebenszeichen geben", "Confirm I am well"),
    "EmergencyLifesignHint": ("Jede Anmeldung zählt ebenfalls als Lebenszeichen.", "Every sign-in also counts as a life sign."),
    "EmergencyLifesignDone": ("Danke, dein Lebenszeichen ist gespeichert.", "Thank you, your life sign has been saved."),
    "EmergencySettings": ("Deine Fristen", "Your deadlines"),
    "EmergencySettingsText": (
        "Erinnerungen kommen {0} Tage nach Fälligkeit des Lebenszeichens. Die Werte wählst du innerhalb der Grenzen, "
        "die der Administrator festgelegt hat.",
        "Reminders arrive {0} days after the life sign is due. You choose the values within the limits set by the "
        "administrator.",
    ),
    "EmergencyRange": ("zwischen {0} und {1} Tagen, Standard {2}", "between {0} and {1} days, default {2}"),
    "EmergencySecondEmail": ("Zweite E-Mail-Adresse (optional)", "Second email address (optional)"),
    "EmergencySecondEmailHint": ("Erinnerungen und Anträge gehen zusätzlich dorthin.", "Reminders and requests are also sent there."),
    "EmergencyNtfyTopic": ("ntfy-Thema (optional)", "ntfy topic (optional)"),
    "EmergencyNtfyTopicHint": ("Push-Nachricht aufs Smartphone, zusätzlich zur Mail.", "Push notification to your phone, in addition to email."),
    "EmergencyReleasedOwner": (
        "Dein Notfallzugang ist freigegeben ({0}) seit {1}. Deine Vertrauenspersonen können lesen, was du für diesen "
        "Fall hinterlegt hast. Wenn es dir gut geht, nimm die Freigabe zurück.",
        "Your emergency access has been released ({0}) since {1}. Your trusted persons can read what you set up for "
        "this case. If you are well, revoke the release.",
    ),
    "EmergencyRequestPending": (
        "{0} hat Zugang beantragt ({1}). Ohne Widerspruch wird er am {2} freigegeben.",
        "{0} has requested access ({1}). Without an objection it will be released on {2}.",
    ),
    "EmergencyRequestPaused": (
        "Der Antrag von {0} ({1}) ist angehalten. Entscheide jetzt: endgültig widersprechen oder weiterlaufen lassen.",
        "{0}'s request ({1}) is paused. Decide now: object for good or let it continue.",
    ),
    "EmergencyObject": ("Endgültig widersprechen", "Object for good"),
    "EmergencyObjectConfirm": ("Dem Antrag von {0} endgültig widersprechen? Es wird nichts freigegeben.", "Object to {0}'s request for good? Nothing will be released."),
    "EmergencyContinue": ("Weiterlaufen lassen", "Let it continue"),
    "EmergencyForOthers": ("Für andere", "For others"),
    "EmergencyForOthersText": (
        "Personen, die dich als Vertrauensperson benannt haben. Einen Zugang beantragst du nur im Ernstfall; die "
        "Person und ihre anderen Vertrauenspersonen werden sofort informiert und können widersprechen.",
        "People who named you as a trusted person. Only request access in an emergency; the person and their other "
        "trusted persons are informed immediately and can object.",
    ),
    "EmergencyRequest": ("Zugang beantragen: {0}", "Request access: {0}"),
    "EmergencyRequestConfirm": (
        "Zugang zu {0} für den Fall „{1}“ beantragen? {0} und alle anderen Vertrauenspersonen bekommen sofort eine Mail.",
        "Request access to {0} for the case \"{1}\"? {0} and all other trusted persons will receive an email right away.",
    ),
    "EmergencyRequestSubmit": ("Beantragen", "Request"),
    "EmergencyRequested": ("Der Antrag ist gestellt.", "The request has been made."),
    "EmergencyMyRequestPending": ("Dein Antrag ({0}) läuft; Freigabe ohne Widerspruch am {1}.", "Your request ({0}) is running; release without objection on {1}."),
    "EmergencyMyRequestPaused": ("Dein Antrag ({0}) ist angehalten.", "Your request ({0}) is paused."),
    "EmergencyWithdraw": ("Zurückziehen", "Withdraw"),
    "EmergencyNoRequestRight": ("Du darfst für diese Person keinen Zugang beantragen.", "You may not request access for this person."),
    "EmergencyReleasedFor": ("Freigegeben ({0}) – ansehen", "Released ({0}) – view"),
    "EmergencyError_emergency-not-found": ("Nicht gefunden.", "Not found."),
    "EmergencyError_emergency-invalid": ("Die Angaben sind ungültig.", "The input is invalid."),
    "EmergencyError_emergency-not-allowed": ("Dafür fehlt dir das Recht.", "You are not allowed to do this."),
    "EmergencyError_emergency-pending": ("Es läuft schon ein Antrag für diesen Fall.", "A request for this case is already running."),
    "EmergencyError_emergency-already-released": ("Dieser Fall ist schon freigegeben.", "This case has already been released."),
    "EmergencyError_emergency-token-invalid": ("Der Link ist ungültig oder schon verwendet.", "The link is invalid or has already been used."),
    "EmergencyError_emergency-not-released": ("Dafür ist der Notfallzugang nicht freigegeben.", "Emergency access has not been released for this."),
    "EmergencyError_emergency-last-admin": (
        "Diese Person ist der einzige Administrator; ihr Tresor lässt sich so nicht löschen.",
        "This person is the only administrator; their vault cannot be deleted this way.",
    ),
    "EmergencyError_emergency-failed": ("Das hat nicht geklappt. Bitte versuche es erneut.", "That did not work. Please try again."),
    # Rücknahme und gemeinsames Löschen (M2.6)
    "EmergencyRevoke": ("Freigabe zurücknehmen", "Revoke release"),
    "EmergencyRevokeConfirm": (
        "Freigabe zurücknehmen? Deine Vertrauenspersonen können danach nichts mehr lesen, was nur für den Notfall "
        "bestimmt ist; sofort Geteiltes bleibt. Die betroffenen Bereiche und Briefe bekommen neue Schlüssel, das "
        "dauert einen Moment. Ein geplantes Löschen entfällt.",
        "Revoke the release? Your trusted persons will no longer be able to read what is meant only for an "
        "emergency; anything shared right away stays shared. The affected areas and letters get new keys, which takes "
        "a moment. A planned deletion is cancelled.",
    ),
    "EmergencyRevoked": ("Die Freigabe ist zurückgenommen.", "The release has been revoked."),
    "EmergencyRekeying": (
        "Neue Schlüssel werden erzeugt und alles betroffene neu verschlüsselt. Bitte die Seite nicht schließen.",
        "New keys are being created and everything affected is re-encrypted. Please do not close the page.",
    ),
    "EmergencyRekeyDone": ("Alle betroffenen Bereiche und Briefe haben neue Schlüssel.", "All affected areas and letters have new keys."),
    "EmergencyRekeyPending": (
        "Die Freigabe ist zurückgenommen, aber der Schlüsselwechsel ist noch nicht fertig ({0} Bereiche, {1} Briefe). "
        "Bis dahin kennen Vertrauenspersonen womöglich noch die alten Schlüssel.",
        "The release has been revoked, but the key change is not finished yet ({0} areas, {1} letters). Until then, "
        "trusted persons may still know the old keys.",
    ),
    "EmergencyRekeyContinue": ("Schlüsselwechsel fortsetzen", "Continue key change"),
    "EmergencyRekeyFailed": (
        "Der Schlüsselwechsel ist nicht ganz fertig geworden. Du kannst ihn hier fortsetzen.",
        "The key change did not quite finish. You can continue it here.",
    ),
    "EmergencyRekeyKeyChanged": (
        "Der Schlüssel einer Vertrauensperson hat sich geändert. Prüfe sie unter „Vertrauenspersonen“ und setze dann "
        "den Schlüsselwechsel hier fort.",
        "A trusted person's key has changed. Check them under \"Trusted persons\" and then continue the key change here.",
    ),
    "EmergencyDeletionOwnerVotes": (
        "Deine Vertrauenspersonen erwägen, deinen Tresor endgültig zu löschen. Zugestimmt haben: {0}.",
        "Your trusted persons are considering deleting your vault for good. Agreed so far: {0}.",
    ),
    "EmergencyDeletionOwnerScheduled": (
        "Alle Vertrauenspersonen haben zugestimmt: Dein Tresor wird am {0} endgültig gelöscht.",
        "All trusted persons have agreed: your vault will be deleted for good on {0}.",
    ),
    "EmergencyDeletionTitle": ("Tresor endgültig löschen", "Delete vault for good"),
    "EmergencyDeletionText": (
        "Wenn alles erledigt ist, könnt ihr den Tresor von {0} gemeinsam löschen. Alle Vertrauenspersonen für den Fall "
        "„verstorben“ müssen zustimmen; sieben Tage nach der letzten Zustimmung ist alles unwiderruflich gelöscht.",
        "When everything has been taken care of, you can delete {0}'s vault together. All trusted persons for the "
        "case \"deceased\" must agree; seven days after the last agreement everything is deleted irrevocably.",
    ),
    "EmergencyDeletionVotes": ("Zugestimmt: {0}. Es fehlt noch: {1}.", "Agreed: {0}. Still missing: {1}."),
    "EmergencyDeletionNobody": ("niemand", "nobody"),
    "EmergencyDeletionScheduled": (
        "Alle haben zugestimmt. Gelöscht wird am {0}; bis dahin kannst du deine Zustimmung zurückziehen.",
        "Everyone has agreed. Deletion takes place on {0}; until then you can withdraw your agreement.",
    ),
    "EmergencyDeletionAgree": ("Dem Löschen zustimmen", "Agree to deletion"),
    "EmergencyDeletionWithdraw": ("Zustimmung zurückziehen", "Withdraw agreement"),
    "EmergencyDeletionConfirm": (
        "Dem endgültigen Löschen des Tresors von {0} zustimmen? Sobald alle zugestimmt haben, wird nach sieben Tagen "
        "alles gelöscht: Daten, Dokumente, Briefe. Das lässt sich nicht rückgängig machen. Speichere vorher, was du "
        "behalten möchtest.",
        "Agree to deleting {0}'s vault for good? Once everyone has agreed, everything is deleted after seven days: "
        "data, documents, letters. This cannot be undone. Save anything you want to keep first.",
    ),
    "EmergencyDeletionBlocked": (
        "{0} ist der einzige Administrator dieser Instanz. Bevor der Tresor gelöscht werden kann, muss der Betreiber "
        "jemand anderen zum Administrator machen.",
        "{0} is the only administrator of this instance. Before the vault can be deleted, the operator must make "
        "someone else an administrator.",
    ),
    "Audit_emergency-revoked": ("Freigabe zurückgenommen", "Release revoked"),
    "Audit_letter-key-rotated": ("Neuer Briefschlüssel", "New letter key"),
    "Audit_vault-deletion-agreed": ("Dem Löschen zugestimmt", "Agreed to deletion"),
    "Audit_vault-deletion-withdrawn": ("Zustimmung zum Löschen zurückgezogen", "Agreement to deletion withdrawn"),
    "Audit_vault-deleted-by-trustees": ("Tresor von den Vertrauenspersonen gelöscht", "Vault deleted by trusted persons"),
    "LifesignTitle": ("Lebenszeichen", "Life sign"),
    "LifesignDone": ("Danke, {0}. Dein Lebenszeichen ist gespeichert.", "Thank you, {0}. Your life sign has been saved."),
    "LifesignInvalid": (
        "Dieser Link ist ungültig oder wurde schon verwendet. Melde dich einfach an; das zählt auch als Lebenszeichen.",
        "This link is invalid or has already been used. Simply sign in; that also counts as a life sign.",
    ),
    "ObjectionTitle": ("Antrag anhalten", "Pause request"),
    "ObjectionDone": ("Der Antrag von {0} ist angehalten. Es wird vorerst nichts freigegeben.", "{0}'s request is paused. Nothing will be released for now."),
    "ObjectionNext": (
        "Melde dich jetzt an und entscheide unter „Notfallzugang“, ob du endgültig widersprichst.",
        "Now sign in and decide under \"Emergency access\" whether to object for good.",
    ),
    "ObjectionInvalid": ("Dieser Link ist ungültig oder der Antrag ist schon entschieden.", "This link is invalid or the request has already been decided."),
    "BannerRequest": ("Achtung: Zugang zu deinen Daten wurde beantragt von {0}.", "Attention: access to your data has been requested by {0}."),
    "BannerReleased": ("Dein Notfallzugang ist freigegeben.", "Your emergency access has been released."),
    "BannerTrustedReleased": ("Freigegeben für dich: {0}.", "Released to you: {0}."),
    "BannerMailProblem": (
        "Der Mailversand hat Probleme. Solange Mails hängen, laufen die Fristen des Notfallzugangs nicht weiter.",
        "Email sending has problems. As long as emails are stuck, emergency access deadlines do not continue.",
    ),
    "Audit_lifesign": ("Lebenszeichen", "Life sign"),
    "Audit_lifesign-requested": ("Erinnerung an das Lebenszeichen", "Life sign reminder"),
    "Audit_emergency-waiting": ("Wartezeit begonnen", "Waiting period started"),
    "Audit_emergency-released": ("Notfallzugang freigegeben", "Emergency access released"),
    "Audit_emergency-settings-changed": ("Fristen geändert", "Deadlines changed"),
    "Audit_access-requested": ("Zugang beantragt", "Access requested"),
    "Audit_access-request-paused": ("Antrag angehalten", "Request paused"),
    "Audit_access-request-continued": ("Antrag läuft weiter", "Request continued"),
    "Audit_access-request-objected": ("Antrag widersprochen", "Request objected"),
    "Audit_access-request-withdrawn": ("Antrag zurückgezogen", "Request withdrawn"),
    "AdminError_admin-released": (
        "Für dieses Konto ist der Notfallzugang freigegeben; es lässt sich nicht löschen.",
        "Emergency access has been released for this account; it cannot be deleted.",
    ),
}

MAIL = {
    "Mail_mode_death": ("verstorben", "deceased"),
    "Mail_mode_incapacity": ("handlungsunfähig", "incapacitated"),
    "Mail_lifesign-due_subject": ("myLife: Bitte gib ein Lebenszeichen", "myLife: Please confirm you are well"),
    "Mail_lifesign-due_body": (
        "Hallo {name},\n\ndein Lebenszeichen bei myLife ist fällig. Ein Klick genügt, keine Anmeldung nötig:\n{link}\n\n"
        "Du kannst dich auch einfach bei myLife anmelden. Wenn du nicht reagierst, erinnert dich myLife noch "
        "{count} Mal. Erst danach und nach einer Wartezeit bekommen deine Vertrauenspersonen Zugang, frühestens am "
        "{date}.",
        "Hello {name},\n\nyour life sign on myLife is due. One click is enough, no sign-in needed:\n{link}\n\n"
        "You can also simply sign in to myLife. If you do not respond, myLife will remind you {count} more times. "
        "Only after that and a waiting period will your trusted persons get access, on {date} at the earliest.",
    ),
    "Mail_lifesign-reminder_subject": ("myLife: Erinnerung {stage} von {count} – bitte gib ein Lebenszeichen", "myLife: Reminder {stage} of {count} – please confirm you are well"),
    "Mail_lifesign-reminder_body": (
        "Hallo {name},\n\nwir haben noch kein Lebenszeichen von dir. Ein Klick genügt:\n{link}\n\n"
        "Ohne Reaktion bekommen deine Vertrauenspersonen frühestens am {date} Zugang zu dem, was du für sie "
        "freigegeben hast.",
        "Hello {name},\n\nwe have not had a life sign from you yet. One click is enough:\n{link}\n\n"
        "Without a response, your trusted persons will get access to what you shared with them on {date} at the "
        "earliest.",
    ),
    "Mail_released-owner_subject": ("myLife: Dein Notfallzugang wurde freigegeben", "myLife: Your emergency access has been released"),
    "Mail_released-owner_body": (
        "Hallo {name},\n\ndein Notfallzugang bei myLife wurde freigegeben ({mode}). Deine Vertrauenspersonen können "
        "jetzt lesen, was du für diesen Fall freigegeben hast.\n\nWenn das ein Irrtum ist, melde dich bei myLife an.",
        "Hello {name},\n\nyour emergency access on myLife has been released ({mode}). Your trusted persons can now "
        "read what you shared with them for this case.\n\nIf this is a mistake, sign in to myLife.",
    ),
    "Mail_released-trustee_subject": ("myLife: Zugang zu {owner} freigegeben", "myLife: Access to {owner} released"),
    "Mail_released-trustee_body": (
        "Hallo {name},\n\nder Notfallzugang von {owner} ist freigegeben ({mode}). Melde dich bei myLife an; unter "
        "„Mit mir geteilt“ findest du, was {owner} für dich hinterlegt hat.",
        "Hello {name},\n\n{owner}'s emergency access has been released ({mode}). Sign in to myLife; under \"Shared "
        "with me\" you will find what {owner} left for you.",
    ),
    "Mail_request-owner_subject": ("myLife: {trustee} hat Zugang beantragt", "myLife: {trustee} has requested access"),
    "Mail_request-owner_body": (
        "Hallo {name},\n\n{trustee} hat bei myLife Zugang zu deinen Daten beantragt, für den Fall „{mode}“. Ohne "
        "Widerspruch wird der Zugang am {date} freigegeben.\n\nWenn das nicht in deinem Sinn ist, öffne sofort diesen "
        "Link. Er hält den Antrag an:\n{link}\n\nDanach meldest du dich an und entscheidest, ob du endgültig "
        "widersprichst. Deine anderen Vertrauenspersonen wurden über den Antrag informiert.",
        "Hello {name},\n\n{trustee} has requested access to your data on myLife, for the case \"{mode}\". Without an "
        "objection, access will be released on {date}.\n\nIf this is not what you want, open this link right away. "
        "It pauses the request:\n{link}\n\nThen sign in and decide whether to object for good. Your other trusted "
        "persons have been informed about the request.",
    ),
    "Mail_request-info_subject": ("myLife: Antrag auf Zugang zu {owner}", "myLife: Access request for {owner}"),
    "Mail_request-info_body": (
        "Hallo {name},\n\n{trustee} hat bei myLife Zugang zu {owner} beantragt, für den Fall „{mode}“. {owner} kann "
        "widersprechen. Du musst nichts tun; wenn du Zweifel hast, sprich mit {trustee} oder {owner}.",
        "Hello {name},\n\n{trustee} has requested access to {owner} on myLife, for the case \"{mode}\". {owner} can "
        "object. You do not need to do anything; if in doubt, talk to {trustee} or {owner}.",
    ),
    "Mail_request-paused_subject": ("myLife: Dein Antrag ist angehalten", "myLife: Your request has been paused"),
    "Mail_request-paused_body": (
        "Hallo {name},\n\n{owner} hat deinen Antrag auf Zugang angehalten. Die Frist läuft vorerst nicht weiter.",
        "Hello {name},\n\n{owner} has paused your access request. The waiting period does not continue for now.",
    ),
    "Mail_request-objected_subject": ("myLife: {owner} hat dem Antrag widersprochen", "myLife: {owner} has objected to the request"),
    "Mail_request-objected_body": (
        "Hallo {name},\n\n{owner} hat dem Antrag von {trustee} auf Zugang endgültig widersprochen. Es wird nichts freigegeben.",
        "Hello {name},\n\n{owner} has objected to {trustee}'s access request for good. Nothing will be released.",
    ),
    "Mail_release-revoked_subject": ("myLife: {owner} hat den Notfallzugang zurückgenommen", "myLife: {owner} has revoked emergency access"),
    "Mail_release-revoked_body": (
        "Hallo {name},\n\n{owner} hat sich bei myLife zurückgemeldet und die Freigabe des Notfallzugangs "
        "zurückgenommen. Was nur für den Notfall bestimmt ist, ist für dich nicht mehr sichtbar.",
        "Hello {name},\n\n{owner} has checked back in on myLife and revoked the release of emergency access. What is "
        "meant only for an emergency is no longer visible to you.",
    ),
    "Mail_vault-deletion-proposed_subject": ("myLife: Tresor von {owner} löschen?", "myLife: Delete {owner}'s vault?"),
    "Mail_vault-deletion-proposed_body": (
        "Hallo {name},\n\n{trustee} schlägt vor, den Tresor von {owner} bei myLife endgültig zu löschen. Gelöscht wird "
        "nur, wenn alle Vertrauenspersonen zustimmen. Melde dich an und entscheide unter „Notfallzugang“. Speichere "
        "vorher, was du behalten möchtest.",
        "Hello {name},\n\n{trustee} suggests deleting {owner}'s vault on myLife for good. It will only be deleted if "
        "all trusted persons agree. Sign in and decide under \"Emergency access\". Save anything you want to keep first.",
    ),
    "Mail_vault-deletion-scheduled_subject": ("myLife: Tresor von {owner} wird am {date} gelöscht", "myLife: {owner}'s vault will be deleted on {date}"),
    "Mail_vault-deletion-scheduled_body": (
        "Hallo {name},\n\nalle Vertrauenspersonen haben zugestimmt: Der Tresor von {owner} wird am {date} endgültig "
        "gelöscht. Bis dahin kannst du deine Zustimmung unter „Notfallzugang“ zurückziehen.",
        "Hello {name},\n\nall trusted persons have agreed: {owner}'s vault will be deleted for good on {date}. Until "
        "then you can withdraw your agreement under \"Emergency access\".",
    ),
    "Mail_vault-deletion-scheduled-owner_subject": ("myLife: Dein Tresor wird am {date} gelöscht", "myLife: Your vault will be deleted on {date}"),
    "Mail_vault-deletion-scheduled-owner_body": (
        "Hallo {name},\n\ndeine Vertrauenspersonen haben zugestimmt, deinen Tresor bei myLife am {date} endgültig zu "
        "löschen. Wenn das ein Irrtum ist, melde dich sofort an und nimm unter „Notfallzugang“ die Freigabe zurück; "
        "dann wird nichts gelöscht.",
        "Hello {name},\n\nyour trusted persons have agreed to delete your vault on myLife for good on {date}. If this "
        "is a mistake, sign in right away and revoke the release under \"Emergency access\"; then nothing will be deleted.",
    ),
    "Mail_vault-deletion-cancelled_subject": ("myLife: Löschen des Tresors von {owner} abgebrochen", "myLife: Deletion of {owner}'s vault cancelled"),
    "Mail_vault-deletion-cancelled_body": (
        "Hallo {name},\n\n{trustee} hat die Zustimmung zurückgezogen. Der Tresor von {owner} wird vorerst nicht gelöscht.",
        "Hello {name},\n\n{trustee} has withdrawn their agreement. {owner}'s vault will not be deleted for now.",
    ),
    "Mail_vault-deleted_subject": ("myLife: Tresor von {owner} gelöscht", "myLife: {owner}'s vault deleted"),
    "Mail_vault-deleted_body": (
        "Hallo {name},\n\nwie mit allen Vertrauenspersonen vereinbart, ist der Tresor von {owner} bei myLife jetzt "
        "endgültig gelöscht.",
        "Hello {name},\n\nas agreed by all trusted persons, {owner}'s vault on myLife has now been deleted for good.",
    ),
}
