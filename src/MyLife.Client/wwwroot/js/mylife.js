// Kleine Hilfsfunktionen für JS-Interop. Kein Inline-Skript, damit die Content Security Policy strikt bleiben kann.
window.myLife = {
    getStoredCulture: () => window.localStorage.getItem("mylife.culture"),
    setStoredCulture: (culture) => window.localStorage.setItem("mylife.culture", culture),
    getBrowserLanguage: () => navigator.language || "de",
    setDocumentLanguage: (culture) => { document.documentElement.lang = culture; },
    // Heller Modus: Orange als Schriftfarbe wird dunkler, damit der Kontrast reicht (M3.6, app.css).
    setLightMode: (light) => { document.documentElement.classList.toggle("mylife-light", light); },
    print: () => window.print(),
    copyText: async (text) => {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch {
            return false;
        }
    },
    // Letzte Aktivität für die automatische Abmeldung (IdleMonitor).
    lastActivity: Date.now(),
    getIdleSeconds: () => Math.floor((Date.now() - window.myLife.lastActivity) / 1000),
    // Entschlüsselte Dokumente existieren nur im Browser: als Blob-URL zur Vorschau oder als Download.
    createObjectUrl: (bytes, type) => URL.createObjectURL(new Blob([bytes], { type: type || "application/octet-stream" })),
    revokeObjectUrl: (url) => URL.revokeObjectURL(url),
    downloadBytes: (bytes, fileName, type) => {
        const url = URL.createObjectURL(new Blob([bytes], { type: type || "application/octet-stream" }));
        const link = document.createElement("a");
        link.href = url;
        link.download = fileName;
        link.rel = "noopener";
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(() => URL.revokeObjectURL(url), 60000);
    }
};

for (const type of ["pointerdown", "pointermove", "keydown", "wheel", "touchstart", "scroll"]) {
    window.addEventListener(type, () => { window.myLife.lastActivity = Date.now(); }, { passive: true, capture: true });
}
