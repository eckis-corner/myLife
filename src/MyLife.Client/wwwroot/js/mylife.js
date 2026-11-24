// Kleine Hilfsfunktionen für JS-Interop. Kein Inline-Skript, damit die Content Security Policy strikt bleiben kann.
window.myLife = {
    getStoredCulture: () => window.localStorage.getItem("mylife.culture"),
    setStoredCulture: (culture) => window.localStorage.setItem("mylife.culture", culture),
    getBrowserLanguage: () => navigator.language || "de",
    setDocumentLanguage: (culture) => { document.documentElement.lang = culture; },
    print: () => window.print(),
    // Letzte Aktivität für die automatische Abmeldung (IdleMonitor).
    lastActivity: Date.now(),
    getIdleSeconds: () => Math.floor((Date.now() - window.myLife.lastActivity) / 1000)
};

for (const type of ["pointerdown", "pointermove", "keydown", "wheel", "touchstart", "scroll"]) {
    window.addEventListener(type, () => { window.myLife.lastActivity = Date.now(); }, { passive: true, capture: true });
}
