"""Texte für die Vollständigkeitsanzeige (M3.3). Wird von tools/resources.py eingebunden."""

CLIENT = {
    "CompletenessTitle": ("Vollständigkeit: {0} %", "Completeness: {0} %"),
    "CompletenessSummary": (
        "{0} Einträge haben noch Lücken, {1} typische Einträge fehlen. Was genau, zeigen die Bereiche.",
        "{0} entries still have gaps, {1} typical entries are missing. The areas show exactly what.",
    ),
    "CompletenessPercent": ("{0} % vollständig", "{0} % complete"),
    "CompletenessTile": ("{0} % vollständig, {1} mit Lücken", "{0} % complete, {1} with gaps"),
    "CompletenessIncomplete": ("{0} Einträge haben noch Lücken; sie sind in der Liste markiert.", "{0} entries still have gaps; they are marked in the list."),
    "CompletenessTypical": ("Das haben die meisten hier, bei dir fehlt es noch:", "Most people have this here; yours is still missing:"),
    "CompletenessMissing": ("Es fehlt noch: {0}", "Still missing: {0}"),
    "CompletenessGaps": ("Lücken", "Gaps"),
}
