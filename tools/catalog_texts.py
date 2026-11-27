"""Texte des Katalogs (Bereiche, Objekttypen, Felder, Hinweise, Auswahllisten) aus my-common (Entscheidung 144).

Quelle ist catalog-texts.json in MyCommon.Catalog. Gesucht wird in dieser Reihenfolge:
1. Umgebungsvariable MYCOMMON_DIR (Ordner des Repos my-common)
2. ../my-common neben myLife (eckis-corner/my-common)
3. das NuGet-Paket MyCommon.Catalog in der Version aus Directory.Packages.props im lokalen Paket-Cache
"""
import json
import os
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
RELATIVE = Path("src") / "MyCommon.Catalog" / "catalog-texts.json"


def _package_version() -> str:
    props = (ROOT / "Directory.Packages.props").read_text(encoding="utf-8")
    match = re.search(r'Include="MyCommon\.Catalog"\s+Version="([^"]+)"', props)
    if not match:
        raise SystemExit("MyCommon.Catalog fehlt in Directory.Packages.props")
    return match.group(1)


def _candidates() -> list[Path]:
    found = []
    if os.environ.get("MYCOMMON_DIR"):
        found.append(Path(os.environ["MYCOMMON_DIR"]) / RELATIVE)
    found.append(ROOT.parent / "my-common" / RELATIVE)
    packages = Path(os.environ.get("NUGET_PACKAGES", Path.home() / ".nuget" / "packages"))
    found.append(packages / "mycommon.catalog" / _package_version() / "content" / "catalog-texts.json")
    return found


def path() -> Path:
    for candidate in _candidates():
        if candidate.is_file():
            return candidate
    tried = "\n  ".join(str(c) for c in _candidates())
    raise SystemExit(f"catalog-texts.json nicht gefunden. Gesucht:\n  {tried}\n"
                     "my-common neben myLife auschecken oder 'dotnet restore' ausführen.")


def load() -> dict[str, tuple[str, str]]:
    """Schlüssel → (Deutsch, Englisch)."""
    data = json.loads(path().read_text(encoding="utf-8"))
    return {key: (value["de"], value["en"]) for key, value in data.items()}
