"""Texte für Erinnerungen (M3.1): Einstellungen, Übersicht, Mail.
Wird von tools/resources.py eingebunden. CLIENT: Oberfläche. MAIL: Mailvorlagen.
"""

CLIENT = {
    "RemindersTitle": ("Erinnerungen", "Reminders"),
    "RemindersIntro": (
        "myLife erinnert dich per Mail, wenn Bereiche wieder geprüft werden sollten und bevor etwas abläuft, etwa ein "
        "Ausweis, eine Karte oder ein Vertrag, und wenn eine Wiedervorlage ansteht. Die Mail nennt keine Einzelheiten; "
        "was ansteht, siehst du nach der Anmeldung auf der Übersicht.",
        "myLife reminds you by email when areas should be reviewed again and before something expires, such as an ID, "
        "a card or a contract, and when a follow-up is due. The email contains no details; you see what is due on the "
        "overview after signing in.",
    ),
    "RemindersReviewMonths": ("Bereiche prüfen alle", "Review areas every"),
    "RemindersReviewMonthsHelp": ("0 schaltet die Erinnerung zum Prüfen aus. Standard: 6 Monate.", "0 turns off the review reminder. Default: 6 months."),
    "RemindersLeadDays": ("Vor einem Ablaufdatum erinnern", "Remind before an expiry date"),
    "RemindersLeadDaysHelp": (
        "So viele Tage vorher, zwischen 7 und 365. Standard: 60 Tage. So lange vorher steht es auch auf der Übersicht.",
        "This many days before, between 7 and 365. Default: 60 days. It also appears on the overview this far ahead.",
    ),
    "RemindersMail": ("Erinnerungen per Mail", "Reminders by email"),
    "RemindersServerKnows": (
        "Der Server kennt von deinen Einträgen nur ein Datum: {0}. Wozu es gehört, weiß er nicht.",
        "Of your entries, the server only knows one date: {0}. It does not know what it belongs to.",
    ),
    "RemindersServerKnowsNothing": (
        "Der Server kennt derzeit keinen Termin aus deinen Einträgen.",
        "The server currently knows no date from your entries.",
    ),
    "UnitMonths": ("Monate", "months"),
    "HomeReviewDue": (
        "{0} Bereiche wurden seit mehr als {1} Monaten nicht geprüft. Sieh sie dir an und markiere sie dann als geprüft.",
        "{0} areas have not been reviewed for more than {1} months. Look through them and then mark them as reviewed.",
    ),
    "HomeReviewDueChip": ("Prüfung fällig", "Review due"),
    "ReminderError_reminder-invalid": ("Die Angaben sind ungültig.", "The input is invalid."),
    "ReminderError_reminder-failed": ("Das hat nicht geklappt. Bitte versuche es erneut.", "That did not work. Please try again."),
    "Audit_reminder-sent": ("Erinnerung verschickt", "Reminder sent"),
    "Audit_reminder-settings-changed": ("Erinnerungen geändert", "Reminders changed"),
}

MAIL = {
    "Mail_reminder_subject": ("myLife: Zeit für einen Blick in deinen Notfallordner", "myLife: Time to look at your emergency folder"),
    "Mail_reminder_body": (
        "Hallo {name},\n\nbei myLife steht etwas an:\n{items}\n\nMelde dich an; auf der Übersicht siehst du, worum es "
        "geht:\n{link}\n\nDiese Mail nennt absichtlich keine Einzelheiten. Erinnerungen stellst du nach der Anmeldung "
        "unter „Erinnerungen“ ein.",
        "Hello {name},\n\nsomething is due on myLife:\n{items}\n\nSign in; the overview shows what it is about:\n{link}"
        "\n\nThis email deliberately contains no details. You can adjust reminders under \"Reminders\" after signing in.",
    ),
    "Mail_reminder_review": (
        "{count} Bereiche wurden seit mehr als {months} Monaten nicht geprüft.",
        "{count} areas have not been reviewed for more than {months} months.",
    ),
    "Mail_reminder_dates": (
        "Ein Ablaufdatum oder eine Wiedervorlage rückt näher.",
        "An expiry date or a follow-up is coming up.",
    ),
}
