"""Texte für Vertrauenspersonen, Freigaben und „Mit mir geteilt“ (M2.3). Wird von tools/resources.py eingebunden."""

CLIENT = {
    "TrustTitle": ("Vertrauenspersonen", "Trusted persons"),
    "TrustText": (
        "Lege fest, wer im Ernstfall welche Bereiche sehen darf: wenn du verstorben bist, wenn du handlungsunfähig "
        "bist, oder schon heute (sofort teilen, nur Lesen). Dein Browser verschlüsselt dafür die Schlüssel der "
        "Bereiche für jede Person einzeln; der Server gibt sie erst heraus, wenn der Fall eintritt.",
        "Decide who may see which areas in an emergency: if you have died, if you are incapacitated, or already "
        "today (share now, read only). Your browser encrypts the area keys for each person individually; the "
        "server only releases them when the case occurs.",
    ),
    "TrustAdd": ("Vertrauensperson hinzufügen", "Add trusted person"),
    "TrustNobody": (
        "Vertrauenspersonen sind Personen mit eigenem Konto auf dieser Instanz. Lade zuerst jemanden ein.",
        "Trusted persons are people with their own account on this instance. Invite someone first.",
    ),
    "TrustEmpty": ("Noch keine Vertrauenspersonen.", "No trusted persons yet."),
    "TrustRelationship": ("Beziehung", "Relationship"),
    "TrustRelationshipHint": ("Etwa Tochter, Bruder, Freundin. Nur für dich sichtbar.", "E.g. daughter, brother, friend. Only visible to you."),
    "TrustNote": ("Notiz", "Note"),
    "TrustProfileEncrypted": ("Beziehung und Notiz werden in deinem Tresor verschlüsselt; der Server sieht sie nicht.", "Relationship and note are encrypted in your vault; the server cannot see them."),
    "TrustCanRequestLabel": ("Darf einen Zugang beantragen", "May request access"),
    "TrustCanRequest": ("darf beantragen", "may request"),
    "TrustGrants": ("Freigaben", "Access"),
    "TrustStale": ("Neu speichern nötig", "Needs saving again"),
    "TrustAreasTitle": ("Bereiche", "Areas"),
    "TrustAreasText": (
        "Verstorben und handlungsunfähig sind getrennte Fälle; was du für einen Fall freigibst, sieht die Person nur in "
        "diesem Fall. Briefe gibst du unten einzeln frei.",
        "Deceased and incapacitated are separate cases; what you release for one case is only visible in that case. "
        "You release letters individually below.",
    ),
    "TrustArea": ("Bereich", "Area"),
    "TrustAll": ("alle", "all"),
    "GrantMode_Live": ("Sofort teilen", "Share now"),
    "GrantMode_Death": ("Verstorben", "Deceased"),
    "GrantMode_Incapacity": ("Handlungsunfähig", "Incapacitated"),
    "GrantModeHint_Live": ("Die Person kann den Bereich schon heute lesen, aber nichts ändern.", "The person can read the area today but cannot change anything."),
    "GrantModeHint_Death": ("Sichtbar, wenn der Notfallzugang im Modus „verstorben“ freigegeben ist.", "Visible once emergency access is released as \"deceased\"."),
    "GrantModeHint_Incapacity": (
        "Sichtbar, wenn der Notfallzugang im Modus „handlungsunfähig“ freigegeben ist. Du kannst ihn nach deiner Rückkehr wieder entziehen.",
        "Visible once emergency access is released as \"incapacitated\". You can revoke it after you return.",
    ),
    "TrustLiveHint": (
        "Sofort geteilte Bereiche sieht die Person ab dem Speichern unter „Mit mir geteilt“. Beendest du das Teilen, "
        "bekommt der Bereich einen neuen Schlüssel.",
        "The person sees areas shared now under \"Shared with me\" as soon as you save. If you stop sharing, the "
        "area gets a new key.",
    ),
    "TrustLettersTitle": ("Persönliche Briefe", "Personal letters"),
    "TrustLettersText": ("Jeder Brief hat einen eigenen Schlüssel und geht nur an die Personen, die du hier auswählst.", "Each letter has its own key and only goes to the people you select here."),
    "TrustLettersEmpty": ("Keine Briefe. Briefe legst du im Bereich „Persönliche Wünsche“ an.", "No letters. You create letters in the \"Personal wishes\" area."),
    "TrustLetter": ("Brief", "Letter"),
    "TrustSave": ("Speichern und verschlüsseln", "Save and encrypt"),
    "TrustSaving": ("Schlüssel werden für die Person verschlüsselt …", "Encrypting keys for this person …"),
    "TrustRemove": ("Als Vertrauensperson entfernen", "Remove as trusted person"),
    "TrustRemoveConfirm": (
        "{0} als Vertrauensperson entfernen? Alle Freigaben für diese Person werden gelöscht. Sofort geteilte Bereiche "
        "bekommen einen neuen Schlüssel.",
        "Remove {0} as a trusted person? All access for this person is deleted. Areas shared now get a new key.",
    ),
    "TrustLiveEndTitle": ("Teilen beenden", "Stop sharing"),
    "TrustLiveEndText": (
        "Für {0} endet das sofortige Teilen. Dein Browser erzeugt dafür einen neuen Schlüssel und verschlüsselt den "
        "Bereich neu. Das kann einen Moment dauern.",
        "Sharing now ends for {0}. Your browser creates a new key and re-encrypts the area. This may take a moment.",
    ),
    "TrusteeKeyChanged": (
        "Der Schlüssel dieser Person hat sich geändert. myLife verschlüsselt nichts für sie, bis das geklärt ist. "
        "Sprich mit der Person und dem Administrator.",
        "This person's key has changed. myLife will not encrypt anything for them until this is resolved. Talk to "
        "the person and the administrator.",
    ),
    "TrusteeUnconfirmed": (
        "Der Schlüssel dieser Person ist noch von niemandem bestätigt, dem du vertraust. Beim Speichern merkt sich "
        "dein Browser ihn; ein späterer Austausch fällt dann auf.",
        "This person's key has not yet been confirmed by anyone you trust. When you save, your browser remembers "
        "it so that a later swap is noticed.",
    ),
    "TrusteeStaleHint": (
        "Die Person hat inzwischen einen neuen Schlüssel. Speichere einmal neu, damit die Freigaben für ihn verschlüsselt sind.",
        "The person now has a new key. Save once more so that the access is encrypted for it.",
    ),
    "TrustError_trust-not-found": ("Die Person gibt es nicht (mehr).", "This person does not exist (any more)."),
    "TrustError_trust-invalid": ("Die Freigaben sind ungültig. Bitte lade die Seite neu.", "The access settings are invalid. Please reload the page."),
    "TrustError_trust-key-changed": (
        "Der Schlüssel der Person hat sich geändert. Es wurde nichts gespeichert.",
        "The person's key has changed. Nothing was saved.",
    ),
    "TrustError_trust-stale": ("Inzwischen hat sich etwas geändert. Bitte lade die Seite neu und versuche es erneut.", "Something has changed in the meantime. Please reload and try again."),
    "TrustError_trust-failed": ("Das hat nicht geklappt. Bitte versuche es erneut.", "That did not work. Please try again."),
    "SharedTitle": ("Mit mir geteilt", "Shared with me"),
    "SharedText": (
        "Was andere schon heute mit dir teilen, nur zum Lesen. Entschlüsselt wird in deinem Browser mit deinem eigenen Schlüssel.",
        "What others already share with you today, read only. Decryption happens in your browser with your own key.",
    ),
    "SharedEmpty": ("Niemand teilt gerade etwas mit dir.", "Nobody is sharing anything with you right now."),
    "SharedAreaEmpty": ("Dieser Bereich ist leer.", "This area is empty."),
    "SharedLetter": ("Persönlicher Brief", "Personal letter"),
    "SharedOpenFailed": ("Der Inhalt ließ sich nicht öffnen.", "The content could not be opened."),
    "SharedNotVerified": (
        "Die Freigabe ist nicht von einem bestätigten Schlüssel unterschrieben. Prüfe auf der Seite „Familie“, ob der "
        "Schlüssel dieser Person bestätigt ist.",
        "The share is not signed by a confirmed key. Check on the \"Family\" page whether this person's key is confirmed.",
    ),
    "SharedOwnerKeyChanged": (
        "Der Schlüssel dieser Person hat sich geändert. Vorsicht: Die Inhalte könnten nicht von ihr stammen.",
        "This person's key has changed. Careful: the content may not come from them.",
    ),
    "Audit_trustee-added": ("Vertrauensperson hinzugefügt", "Trusted person added"),
    "Audit_trustee-changed": ("Freigaben geändert", "Access changed"),
    "Audit_trustee-removed": ("Vertrauensperson entfernt", "Trusted person removed"),
    "Audit_area-key-rotated": ("Neuer Bereichsschlüssel", "New area key"),
    "Audit_shared-read": ("Geteilten Inhalt gelesen", "Shared content read"),
    "Audit_shared-document-read": ("Geteiltes Dokument geöffnet", "Shared document opened"),
}

MAIL = {
    "Mail_trustee-added_subject": ("{owner} hat dich als Vertrauensperson benannt", "{owner} has named you as a trusted person"),
    "Mail_trustee-added_body": (
        "Hallo {name},\n\n{owner} hat dich bei myLife als Vertrauensperson benannt. Was du im Ernstfall sehen darfst, "
        "bleibt verschlüsselt, bis es freigegeben wird. Falls {owner} etwas schon heute mit dir teilt, findest du es "
        "nach der Anmeldung unter „Mit mir geteilt“.",
        "Hello {name},\n\n{owner} has named you as a trusted person on myLife. What you may see in an emergency stays "
        "encrypted until it is released. If {owner} already shares something with you today, you will find it under "
        "\"Shared with me\" after signing in.",
    ),
}
