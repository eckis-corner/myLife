// Bausteine für den Passwortschutz von PDF-Dateien (PDF 2.0, Standard Security Handler Revision 6, AES-256).
// Die PDF-Struktur erzeugt C# (MyLife.Client/Export/Pdf); hier nur die Web Crypto API, weil AES in .NET im Browser fehlt.
// Die Schlüssel sind kurzlebig und gehören nur zur erzeugten Datei, nicht zum Tresor.

const subtle = globalThis.crypto.subtle;

/** SHA-256, -384 oder -512. */
export async function digest(bits, data) {
    if (![256, 384, 512].includes(bits)) {
        throw new Error("mylife:invalid-input");
    }
    return new Uint8Array(await subtle.digest(`SHA-${bits}`, data));
}

/** AES-CBC mit PKCS#7-Auffüllung (Schlüssel 16 oder 32 Byte, IV 16 Byte). */
export async function aesCbcEncrypt(key, iv, data) {
    if (!(key.length === 16 || key.length === 32) || iv.length !== 16) {
        throw new Error("mylife:invalid-input");
    }
    const cryptoKey = await subtle.importKey("raw", key, { name: "AES-CBC" }, false, ["encrypt"]);
    return new Uint8Array(await subtle.encrypt({ name: "AES-CBC", iv }, cryptoKey, data));
}
