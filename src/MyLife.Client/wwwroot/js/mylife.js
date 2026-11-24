// Kleine Hilfsfunktionen für JS-Interop. Kein Inline-Skript, damit die Content Security Policy strikt bleiben kann.
window.myLife = {
    getStoredCulture: () => window.localStorage.getItem("mylife.culture"),
    setStoredCulture: (culture) => window.localStorage.setItem("mylife.culture", culture),
    getBrowserLanguage: () => navigator.language || "de",
    setDocumentLanguage: (culture) => { document.documentElement.lang = culture; },
    print: () => window.print()
};
