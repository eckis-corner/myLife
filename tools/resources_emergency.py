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
        "Fall hinterlegt hast. Die Rücknahme kommt mit dem nächsten Schritt (M2.6).",
        "Your emergency access has been released ({0}) since {1}. Your trusted persons can read what you set up for "
        "this case. Revoking it follows with the next step (M2.6).",
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
    "EmergencyError_emergency-failed": ("Das hat nicht geklappt. Bitte versuche es erneut.", "That did not work. Please try again."),
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
}
