#!/usr/bin/env python3
"""Erzeugt SharedResource.resx (Deutsch, neutral) und SharedResource.en.resx aus einer Quelle,
damit beide Sprachen immer dieselben Schlüssel haben.

Aufruf: python3 tools/resources.py
"""
from pathlib import Path
from xml.sax.saxutils import escape

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
        "Als Nächstes folgen die Einrichtung, die Anmeldung mit zweitem Faktor und der verschlüsselte Tresor.",
        "Next up: initial setup, sign-in with a second factor and the encrypted vault.",
    ),
    "NotFoundTitle": ("Seite nicht gefunden", "Page not found"),
    "NotFoundText": (
        "Die aufgerufene Seite gibt es nicht.",
        "The page you requested does not exist.",
    ),
    "BackToHome": ("Zur Übersicht", "Back to overview"),
    "ErrorUnhandled": ("Ein unerwarteter Fehler ist aufgetreten.", "An unexpected error has occurred."),
    "Reload": ("Neu laden", "Reload"),
}

HEADER = """<?xml version="1.0" encoding="utf-8"?>
<!-- Automatisch erzeugt mit tools/resources.py. Nicht von Hand bearbeiten. -->
<root>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
"""


def write(path: Path, index: int) -> None:
    lines = [HEADER]
    for key in sorted(TEXTS):
        value = escape(TEXTS[key][index])
        lines.append(f'  <data name="{key}" xml:space="preserve"><value>{value}</value></data>\n')
    lines.append("</root>\n")
    path.write_text("".join(lines), encoding="utf-8")


if __name__ == "__main__":
    target = Path(__file__).resolve().parent.parent / "src" / "MyLife.Client" / "Resources"
    write(target / "SharedResource.resx", 0)
    write(target / "SharedResource.en.resx", 1)
    print(f"{len(TEXTS)} Texte geschrieben nach {target}")
