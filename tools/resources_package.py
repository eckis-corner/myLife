"""Texte für das Notfallpaket (M2.7): Erzeugen, Schlüsselblatt, Protokoll.
Wird von tools/resources.py eingebunden. Die Texte des OfflineViewers stehen in der HTML-Datei selbst
(src/MyLife.OfflineViewer/OfflineViewer.html), weil sie ohne myLife laufen muss.
"""

CLIENT = {
    "PackageTitle": ("Notfallpaket", "Emergency package"),
    "PackageTeaser": (
        "Eine einzelne Datei für deine Familie, die auch ohne myLife-Server und ohne Internet funktioniert. Öffnen lässt "
        "sie sich nur mit einem gedruckten Schlüsselblatt, das du getrennt aufbewahrst.",
        "A single file for your family that works even without the myLife server and without internet. It can only be "
        "opened with a printed key sheet that you keep separately.",
    ),
    "PackageOpen": ("Notfallpaket erstellen", "Create emergency package"),
    "PackageIntro": (
        "Das Notfallpaket besteht aus zwei Teilen: einer HTML-Datei mit deinen Datenblättern und Dokumenten, "
        "verschlüsselt, und einem Schlüsselblatt mit 24 Wörtern zum Ausdrucken. Gib die Datei deiner Familie, etwa auf "
        "einem USB-Stick, und lege das Blatt an einen anderen sicheren Ort, etwa ins Schließfach oder zum Notar. Nur "
        "beides zusammen öffnet das Paket, in jedem aktuellen Browser, ohne Server und ohne Internet.",
        "The emergency package has two parts: an HTML file with your datasheets and documents, encrypted, and a key "
        "sheet with 24 words to print. Give the file to your family, for example on a USB stick, and keep the sheet in "
        "another safe place, such as a safe deposit box or with a notary. Only both together open the package, in any "
        "current browser, without a server and without internet.",
    ),
    "PackageWithDocuments": ("Dokumente einschließen ({0} MB)", "Include documents ({0} MB)"),
    "PackageHow": (
        "Alles entsteht hier in deinem Browser. Der Server sieht weder den Inhalt noch den Schlüssel.",
        "Everything is created here in your browser. The server sees neither the content nor the key.",
    ),
    "PackageCreate": ("Paket erzeugen und speichern", "Create and save package"),
    "PackageSnapshot": (
        "Das Paket ist ein Schnappschuss von heute. Erstelle nach wichtigen Änderungen ein neues und tausche Datei und "
        "Blatt gemeinsam aus; jedes Paket hat seinen eigenen Schlüssel.",
        "The package is a snapshot of today. After important changes, create a new one and replace file and sheet "
        "together; every package has its own key.",
    ),
    "PackageFileName": ("myLife-Notfallpaket", "myLife-emergency-package"),
    "PackageDone": (
        "Die Datei {0} ist gespeichert: {1} Einträge, {2} Dokumente.",
        "The file {0} has been saved: {1} entries, {2} documents.",
    ),
    "PackageWordsOnce": (
        "Drucke jetzt das Schlüsselblatt. Die 24 Wörter siehst du nur hier; myLife speichert sie nirgends. Ohne sie "
        "lässt sich das Paket nicht öffnen.",
        "Print the key sheet now. You will only see the 24 words here; myLife does not store them anywhere. Without "
        "them the package cannot be opened.",
    ),
    "PackageSaveAgain": ("Datei erneut speichern", "Save file again"),
    "PackagePrintSheet": ("Schlüsselblatt drucken", "Print key sheet"),
    "PackageNew": ("Weiteres Paket erstellen", "Create another package"),
    "PackageSheetTitle": ("myLife – Schlüssel zum Notfallpaket", "myLife – Key to the emergency package"),
    "PackageSheetOwner": ("Von", "From"),
    "PackageSheetFile": ("Datei", "File"),
    "PackageSheetCreated": ("Erstellt am", "Created on"),
    "PackageSheetWords": ("Die 24 Wörter", "The 24 words"),
    "PackageSheetInstructions": (
        "So öffnest du das Paket: Die Datei {0} in einem Browser öffnen (Doppelklick), die 24 Wörter in dieser "
        "Reihenfolge eingeben und „Öffnen“ wählen. Es braucht weder Internet noch ein Konto.",
        "How to open the package: open the file {0} in a browser (double-click), enter the 24 words in this order and "
        "choose \"Open\". No internet and no account are needed.",
    ),
    "PackageSheetWarning": (
        "Bewahre dieses Blatt getrennt von der Datei auf. Wer beides hat, kann alles im Paket lesen.",
        "Keep this sheet separate from the file. Anyone who has both can read everything in the package.",
    ),
    "Audit_export-package": ("Notfallpaket erstellt", "Emergency package created"),
}
