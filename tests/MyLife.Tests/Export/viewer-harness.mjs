// Führt den Kern des OfflineViewers (Skript-Block „mylife-core“) aus einem erzeugten Notfallpaket in Node.js aus:
// genau der Code, der im Browser die Wörter prüft und entschlüsselt. Aufruf: node viewer-harness.mjs <html> <wörter> <falsche wörter>
import { readFileSync } from "node:fs";

const [htmlPath, phrase, wrongPhrase] = process.argv.slice(2);
const html = readFileSync(htmlPath, "utf8");
const block = id => {
    const match = html.match(new RegExp(`<script[^>]*id="${id}"[^>]*>([\\s\\S]*?)</script>`));
    if (!match) throw new Error(`Block ${id} fehlt`);
    return match[1];
};

new Function(block("mylife-core"))();
const wordList = JSON.parse(block("mylife-words"));
const pack = JSON.parse(block("mylife-package"));
const content = await globalThis.MyLifeViewer.open(pack, phrase, wordList);

const codeOf = async text => {
    try {
        await globalThis.MyLifeViewer.open(pack, text, wordList);
        return null;
    } catch (error) {
        return error.code ?? String(error);
    }
};

process.stdout.write(JSON.stringify({
    content,
    wrongKey: await codeOf(wrongPhrase),
    tooShort: await codeOf(wrongPhrase.split(" ").slice(0, 10).join(" ")),
    unknown: await codeOf(`zzzz ${wrongPhrase.split(" ").slice(1).join(" ")}`),
    swapped: await codeOf(wrongPhrase.split(" ").reverse().join(" "))
}));
