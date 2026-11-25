"""Texte für die Checkliste (M3.2): Objekttyp „Aufgabe“, Seite, Lesemodus und die Vorlage für Deutschland.
Wird von tools/resources.py eingebunden. Die Vorlage ist keine Rechtsberatung; Fristen sind Richtwerte.
"""

AREA_LABELS = {
    "aboutMe": ("Über mich", "About me"),
    "banking": ("Bankkonten & Depots", "Bank accounts & securities"),
    "cards": ("Kreditkarten", "Payment cards"),
    "loans": ("Kredite & Darlehen", "Loans"),
    "insurance": ("Versicherungen", "Insurance"),
    "contracts": ("Verträge & Abos", "Contracts & subscriptions"),
    "documents": ("Urkunden & Dokumente", "Certificates & legal documents"),
    "property": ("Immobilien, Fahrzeuge & Vermögen", "Property, vehicles & assets"),
    "digital": ("Digitaler Nachlass", "Digital legacy"),
    "passwordManager": ("Passwortmanager", "Password manager"),
    "contacts": ("Wichtige Kontakte", "Important contacts"),
    "wishes": ("Persönliche Wünsche", "Personal wishes"),
    "income": ("Einkommen, Rente & Steuern", "Income, pension & taxes"),
}

# Vorlage für Deutschland: Id → (Titel, Beschreibung), je (Deutsch, Englisch).
TEMPLATE = {
    # Erste 24 Stunden
    "doctor": (
        ("Ärztin oder Arzt rufen", "Call a doctor"),
        ("Bei einem Tod zu Hause die Hausarztpraxis oder den ärztlichen Bereitschaftsdienst (116 117) rufen; sie stellen "
         "die Todesbescheinigung aus. Im Krankenhaus oder Pflegeheim kümmert sich das Personal darum. Es gibt keinen "
         "Grund zur Eile: Abschied nehmen ist erlaubt.",
         "If someone dies at home, call the family doctor or the medical on-call service (116 117 in Germany); they issue "
         "the death certificate. In a hospital or care home, the staff take care of it. There is no need to rush: taking "
         "time to say goodbye is allowed."),
    ),
    "family": (
        ("Angehörige und enge Freunde benachrichtigen", "Notify family and close friends"),
        ("Wer benachrichtigt werden soll, steht bei den wichtigen Kontakten. Holt euch Unterstützung; ihr müsst das nicht "
         "allein tun.",
         "The important contacts list who should be notified. Get support; you do not have to do this alone."),
    ),
    "undertaker": (
        ("Bestattungsinstitut beauftragen", "Engage a funeral home"),
        ("Das Bestattungsinstitut übernimmt die Überführung und viele Formalitäten, oft auch die Sterbeurkunde. Wünsche "
         "zur Bestattung und eine Bestattungsvorsorge stehen bei den persönlichen Wünschen.",
         "The funeral home handles the transfer and many formalities, often the death certificate too. Wishes for the "
         "funeral and any funeral provision are listed under personal wishes."),
    ),
    "lifeInsurance": (
        ("Lebens- und Unfallversicherungen melden", "Notify life and accident insurers"),
        ("Diese Versicherungen wollen den Todesfall sehr schnell wissen, oft innerhalb von 24 bis 72 Stunden, bei "
         "Unfallversicherungen meist 48 Stunden. Ein kurzer Anruf oder eine Mail genügt zunächst; Unterlagen folgen später.",
         "These insurers need to know very quickly, often within 24 to 72 hours, usually 48 hours for accident insurance. "
         "A short call or email is enough at first; documents can follow later."),
    ),
    "home": (
        ("Wohnung, Haustiere und Pflanzen versorgen", "Look after the home, pets and plants"),
        ("Wohnung sichern, Haustiere versorgen, verderbliche Lebensmittel entsorgen, Post sammeln.",
         "Secure the home, look after pets, dispose of perishable food, collect the mail."),
    ),
    # Erste Woche
    "documents": (
        ("Wichtige Unterlagen zusammentragen", "Gather important documents"),
        ("Personalausweis, Geburtsurkunde, gegebenenfalls Heiratsurkunde, Testament und Vollmachten. Wo die Originale "
         "liegen, steht bei den Urkunden.",
         "ID card, birth certificate, marriage certificate if applicable, will and powers of attorney. Where the originals "
         "are kept is noted with the certificates."),
    ),
    "deathCertificate": (
        ("Sterbeurkunde beantragen", "Apply for the death certificate"),
        ("Beim Standesamt des Sterbeorts, meist innerhalb von drei Werktagen; oft erledigt das das Bestattungsinstitut. "
         "Mehrere Exemplare bestellen: Banken, Versicherungen und Behörden brauchen sie.",
         "At the registry office of the place of death, usually within three working days; the funeral home often does "
         "this. Order several copies: banks, insurers and authorities need them."),
    ),
    "will": (
        ("Testament beim Nachlassgericht abgeben", "Hand in the will at the probate court"),
        ("Wer ein Testament findet, muss es unverzüglich beim Nachlassgericht (Amtsgericht) abgeben, auch wenn es "
         "ungültig erscheint. Ein hinterlegtes Testament eröffnet das Gericht von sich aus.",
         "Anyone who finds a will must hand it in to the probate court without delay, even if it seems invalid. A "
         "deposited will is opened by the court on its own."),
    ),
    "employer": (
        ("Arbeitgeber oder Dienstherrn informieren", "Inform the employer"),
        ("Arbeitgeber, Personalnummer und Gehaltskonto stehen bei Einkommen, Rente & Steuern.",
         "Employer, personnel number and salary account are listed under income, pension & taxes."),
    ),
    "healthInsurance": (
        ("Krankenkasse und Pflegekasse informieren", "Inform health and long-term care insurance"),
        ("Die Krankenkasse beendet die Versicherung; mitversicherte Angehörige brauchen gegebenenfalls eine eigene.",
         "The health insurer ends the policy; family members who were co-insured may need their own."),
    ),
    "banks": (
        ("Banken informieren", "Inform the banks"),
        ("Mit Sterbeurkunde und Vollmacht oder Erbnachweis. Daueraufträge prüfen; laufende Kosten wie Miete müssen weiter "
         "bezahlt werden.",
         "With the death certificate and a power of attorney or proof of inheritance. Check standing orders; ongoing costs "
         "such as rent still need to be paid."),
    ),
    "passwordManager": (
        ("Zugang zum Passwortmanager", "Access the password manager"),
        ("Wie ihr an die Zugänge kommt, steht beim Passwortmanager. Viele Anbieter haben eine Wartezeit für den Notfallzugang.",
         "How to get access is described with the password manager. Many providers have a waiting period for emergency access."),
    ),
    "funeral": (
        ("Trauerfeier und Traueranzeige planen", "Plan the memorial service and death notice"),
        ("Wünsche zu Trauerfeier, Musik, Traueranzeige und Gästen stehen bei den persönlichen Wünschen.",
         "Wishes for the service, music, death notice and guests are listed under personal wishes."),
    ),
    # Erster Monat
    "pension": (
        ("Rentenversicherung und Versorgungswerke informieren", "Inform the pension providers"),
        ("Hinterbliebenenrente beantragen. Wer innerhalb von 30 Tagen beim Renten Service einen Vorschuss beantragt, "
         "bekommt für drei Monate die Rente der verstorbenen Person weiter.",
         "Apply for a survivor's pension. In Germany, applying for an advance at the pension service within 30 days "
         "continues the deceased's pension for three months."),
    ),
    "inheritance": (
        ("Erbe annehmen oder ausschlagen", "Accept or disclaim the inheritance"),
        ("Die Frist zum Ausschlagen beträgt in der Regel sechs Wochen ab Kenntnis. Bei Schulden oder Unklarheiten früh "
         "beraten lassen, etwa beim Nachlassgericht oder Notariat. Für Banken und Grundbuch braucht ihr oft einen Erbschein.",
         "The deadline to disclaim is usually six weeks from becoming aware. If there are debts or doubts, get advice early, "
         "for example from the probate court or a notary. Banks and the land registry often require a certificate of inheritance."),
    ),
    "cancelContracts": (
        ("Verträge kündigen", "Cancel contracts"),
        ("Diese Verträge sollen gekündigt werden. Viele haben ein Sonderkündigungsrecht im Todesfall; die Sterbeurkunde beilegen.",
         "These contracts should be cancelled. Many allow special termination on death; enclose the death certificate."),
    ),
    "takeOverContracts": (
        ("Verträge übernehmen", "Take over contracts"),
        ("Diese Verträge sollen übernommen werden, etwa Strom, Internet oder Miete.",
         "These contracts should be taken over, such as electricity, internet or rent."),
    ),
    "cancelInsurance": (
        ("Versicherungen kündigen oder umschreiben", "Cancel or transfer insurance policies"),
        ("Sachversicherungen wie Hausrat oder Haftpflicht gehen oft auf die Erben über; prüfen, was bleiben soll.",
         "Property insurance such as household or liability often passes to the heirs; check what should remain."),
    ),
    "cards": (
        ("Kreditkarten kündigen", "Cancel payment cards"),
        ("Bei Verlust oder Missbrauch sofort sperren lassen: Sperr-Notruf 116 116.",
         "If lost or misused, block them immediately: in Germany call 116 116."),
    ),
    "loans": (
        ("Kredite und Verbindlichkeiten klären", "Settle loans and liabilities"),
        ("Gläubiger informieren; eine Restschuldversicherung kann einspringen. Bei hohen Schulden an das Ausschlagen denken.",
         "Inform the creditors; a credit life insurance may pay. With high debts, consider disclaiming the inheritance."),
    ),
    # Später
    "digital": (
        ("Digitalen Nachlass regeln", "Settle the digital legacy"),
        ("Konten löschen, in den Gedenkzustand versetzen oder übergeben, wie gewünscht; Geräte zurücksetzen oder Daten sichern.",
         "Delete accounts, memorialise or hand them over as wished; reset devices or back up data."),
    ),
    "property": (
        ("Immobilien und Fahrzeuge", "Property and vehicles"),
        ("Grundbuch berichtigen lassen, Fahrzeuge ummelden oder abmelden, Wertgegenstände wie gewünscht verteilen.",
         "Have the land register corrected, re-register or deregister vehicles, distribute valuables as wished."),
    ),
    "taxes": (
        ("Finanzamt und Steuern", "Tax office and taxes"),
        ("Für das Todesjahr ist meist noch eine Steuererklärung nötig. Eine Erbschaft dem Finanzamt innerhalb von drei "
         "Monaten melden, wenn das nicht schon über Gericht oder Notariat geschieht.",
         "A tax return is usually still needed for the year of death. Report an inheritance to the tax office within three "
         "months unless the court or notary already does so."),
    ),
    "grave": (
        ("Grabstätte und Grabpflege", "Grave and its care"),
        ("Grabstein, Grabpflege und Nutzungsrechte der Grabstätte klären.",
         "Arrange the headstone, grave care and the right to use the grave."),
    ),
}

CLIENT = {
    "Type_task": ("Aufgabe", "Task"),
    "Field_checklistPhase": ("Wann", "When"),
    "Field_taskText": ("Was zu tun ist", "What to do"),
    "Field_responsible": ("Zuständig", "Responsible"),
    "Field_linkedArea": ("Zugehörige Einträge aus dem Bereich", "Related entries from area"),
    "Field_linkedWish": ("Nur Einträge mit dem Wunsch", "Only entries with the wish"),
    "FieldHint_linkedWish": (
        "Zum Beispiel „Verträge & Abos“ mit „kündigen“: Die Aufgabe listet dann alle Verträge auf, die gekündigt werden sollen.",
        "For example \"Contracts & subscriptions\" with \"cancel\": the task then lists all contracts that should be cancelled.",
    ),
    "Option_checklistPhase_first24h": ("Erste 24 Stunden", "First 24 hours"),
    "Option_checklistPhase_firstWeek": ("Erste Woche", "First week"),
    "Option_checklistPhase_firstMonth": ("Erster Monat", "First month"),
    "Option_checklistPhase_later": ("Später", "Later"),
    "ChecklistIntro": (
        "Was ist zu tun, wenn dir etwas zustößt? Die Checkliste führt deine Vertrauenspersonen Schritt für Schritt: "
        "erste 24 Stunden, erste Woche, erster Monat, später. Jede Aufgabe zeigt die passenden Einträge, etwa alle "
        "Verträge, die gekündigt werden sollen. Deine Vertrauenspersonen haken gemeinsam ab.",
        "What needs to be done if something happens to you? The checklist guides your trusted persons step by step: "
        "first 24 hours, first week, first month, later. Each task shows the matching entries, such as all contracts "
        "that should be cancelled. Your trusted persons tick them off together.",
    ),
    "ChecklistNewTask": ("Neue Aufgabe", "New task"),
    "ChecklistTemplateTitle": ("Vorlage für Deutschland", "Template for Germany"),
    "ChecklistTemplateText": (
        "{0} Aufgaben von der Ärztin bis zum Finanzamt, mit Fristen als Richtwert. Sie landen in deinem Tresor, und du "
        "änderst, ergänzt oder löschst sie wie jeden anderen Eintrag. Keine Rechtsberatung.",
        "{0} tasks from the doctor to the tax office, with deadlines as a guide. They are added to your vault, and you "
        "change, add or delete them like any other entry. Not legal advice.",
    ),
    "ChecklistStartEmpty": ("Ohne Vorlage beginnen", "Start without template"),
    "ChecklistAdopt": ("Vorlage übernehmen", "Use template"),
    "ChecklistAdopted": ("{0} Aufgaben übernommen.", "{0} tasks added."),
    "ChecklistShareHint": (
        "Damit deine Vertrauenspersonen die Checkliste im Lesemodus sehen, gib sie ihnen unter „Vertrauenspersonen“ frei "
        "wie jeden anderen Bereich. Verknüpfte Einträge sehen sie nur aus Bereichen, die sie auch sehen dürfen.",
        "For your trusted persons to see the checklist in reading mode, share it with them under \"Trusted persons\" like "
        "any other area. They only see related entries from areas they may see as well.",
    ),
    "ChecklistDone": ("Erledigt", "Done"),
    "ChecklistResponsible": ("Zuständig: {0}", "Responsible: {0}"),
    "ChecklistLinkedArea": ("Dazu: {0}", "Related: {0}"),
    "ChecklistLinkedAreaWish": ("Dazu: {0} mit dem Wunsch „{1}“", "Related: {0} with the wish \"{1}\""),
    "ChecklistOpenArea": ("Ansehen", "View"),
    "ChecklistDoneBy": ("Erledigt von {0} am {1}", "Done by {0} on {1}"),
    "ChecklistError_checklist-not-found": ("Nicht gefunden.", "Not found."),
    "ChecklistError_checklist-failed": ("Das hat nicht geklappt. Bitte versuche es erneut.", "That did not work. Please try again."),
    "ReadingHint_12": (
        "Was zu tun ist, Schritt für Schritt. Hakt gemeinsam ab, was erledigt ist; alle sehen denselben Stand.",
        "What needs to be done, step by step. Tick off together what is done; everyone sees the same progress.",
    ),
    "Audit_checklist-done": ("Aufgabe abgehakt", "Task ticked off"),
    "Audit_checklist-undone": ("Haken zurückgenommen", "Tick removed"),
}

for key, label in AREA_LABELS.items():
    CLIENT[f"Option_checklistArea_{key}"] = label

for task_id, (title, text) in TEMPLATE.items():
    CLIENT[f"ChecklistTemplate_{task_id}_title"] = title
    CLIENT[f"ChecklistTemplate_{task_id}_text"] = text
