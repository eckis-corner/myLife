// Kreuztest JavaScript ↔ .NET für myLife.crypto.js (Formatversion 1).
// Aufruf: node crypto-harness.mjs <Repository-Wurzel> <vectors.json>
//
// 1. Prüft die von .NET erzeugten Vektoren mit dem Browser-Modul (jeder Weg zum Klartext).
// 2. Prüft Fehlerfälle (falscher Kontext, manipuliertes Chiffrat, falsche Schlüsselart).
// 3. Erzeugt eigene Vektoren im selben Schema; .NET prüft sie anschließend mit der Referenzimplementierung.
// Ausgabe auf stdout: { checks: [...], produced: {...}, extra: {...} }. Bei einer Abweichung: Exit-Code 1.

import { createRequire } from "node:module";
import { readFile } from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";

const [repositoryRoot, vectorsPath] = process.argv.slice(2);
const wwwroot = path.join(repositoryRoot, "src", "MyLife.Client", "wwwroot");

// Im Browser lädt das Modul hash-wasm selbst per <script>; in Node über require.
globalThis.hashwasm = createRequire(import.meta.url)(path.join(wwwroot, "lib", "hash-wasm", "argon2.umd.min.js"));
const c = await import(pathToFileURL(path.join(wwwroot, "js", "mylife.crypto.js")).href);

const v = JSON.parse(await readFile(vectorsPath, "utf8"));
const checks = [];

const hex = value => Buffer.from(value).toString("hex");
const bin = value => new Uint8Array(Buffer.from(value, "hex"));
const utf8 = value => new TextEncoder().encode(value);

function check(name, condition) {
    if (!condition) throw new Error(`Prüfung fehlgeschlagen: ${name}`);
    checks.push(name);
}

async function rejects(name, code, action) {
    try {
        await action();
    } catch (error) {
        check(name, error.message === `mylife:${code}`);
        return;
    }
    check(name, false);
}

const VMK = "vmk", AREA = "area:3", ECDH = "private-key:ecdh", ECDSA = "private-key:ecdsa", SIGNATURE = "endorsement";

// ------------------------------------------------------------------ 1. .NET-Vektoren prüfen

check("format", v.format === 1);

const master = await c.deriveMasterKey(v.password, bin(v.salt), v.argon2);
check("argon2id+hkdf auth hash", hex(await c.deriveAuthHash(master)) === v.authHash);
const masterNfd = await c.deriveMasterKey(v.passwordDecomposed, bin(v.salt), v.argon2);
check("password NFC normalization", hex(await c.deriveAuthHash(masterNfd)) === v.authHash);

const kek = await c.deriveKeyEncryptionKey(master);
const recovery = await c.importRecoveryKey(bin(v.recoveryKey));
if (v.recoveryAuthHash) {
    check("recovery auth hash", hex(await c.deriveRecoveryAuthHash(bin(v.recoveryKey))) === v.recoveryAuthHash);
}

for (const [name, vmk] of [
    ["kek", await c.unwrapKey(kek, bin(v.wrappedVmkByKek), VMK)],
    ["recovery", await c.unwrapKey(recovery, bin(v.wrappedVmkByRecovery), VMK)]
]) {
    const area = await c.unwrapKey(vmk, bin(v.wrappedAreaKey), AREA);
    check(`${name}: decrypt object`, hex(await c.decrypt(area, bin(v.data.blob), v.data.context)) === v.data.plaintext);

    const file = await c.unwrapKey(area, bin(v.file.wrappedFileKey), v.file.keyContext);
    for (const chunk of v.file.chunks) {
        const plaintext = await c.decryptChunk(file, chunk.index, chunk.isLast, bin(chunk.ciphertext), v.file.context);
        check(`${name}: decrypt chunk ${chunk.index}`, hex(plaintext) === chunk.plaintext);
    }

    const ecdh = await c.unwrapPrivateKey("ecdh", vmk, bin(v.ecdh.wrappedPrivateKey), ECDH);
    const opened = await c.openSealedKey(ecdh, bin(v.envelope.envelope), v.envelope.context);
    check(`${name}: open envelope`, hex(await c.decrypt(opened, bin(v.data.blob), v.data.context)) === v.data.plaintext);

    const ecdsa = await c.unwrapPrivateKey("ecdsa", vmk, bin(v.ecdsa.wrappedPrivateKey), ECDSA);
    const signature = await c.sign(ecdsa, bin(v.signature.data), v.signature.context);
    check(`${name}: sign with unwrapped key`, await c.verify(bin(v.ecdsa.publicKey), bin(v.signature.data), signature, v.signature.context));
}

check("verify .NET signature", await c.verify(bin(v.ecdsa.publicKey), bin(v.signature.data), bin(v.signature.signature), v.signature.context));
check("reject signature for other data", !(await c.verify(bin(v.ecdsa.publicKey), utf8("anders"), bin(v.signature.signature), v.signature.context)));
check("reject signature for other context", !(await c.verify(bin(v.ecdsa.publicKey), bin(v.signature.data), bin(v.signature.signature), "other")));
check("hmac", hex(await c.hmacSha256(bin(v.hmac.key), bin(v.hmac.data))) === v.hmac.mac);
check("sha256", hex(await c.sha256(utf8("abc"))) === "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");

// ------------------------------------------------------------------ 2. Fehlerfälle

const vmk = await c.unwrapKey(kek, bin(v.wrappedVmkByKek), VMK);
const area = await c.unwrapKey(vmk, bin(v.wrappedAreaKey), AREA);
const tampered = bin(v.data.blob);
tampered[tampered.length - 1] ^= 1;
const wrongKind = bin(v.data.blob);
wrongKind[1] = 0x02;
const wrongVersion = bin(v.data.blob);
wrongVersion[0] = 0x02;

await rejects("reject wrong context", "decrypt-failed", () => c.decrypt(area, bin(v.data.blob), "object:other"));
await rejects("reject tampered ciphertext", "decrypt-failed", () => c.decrypt(area, tampered, v.data.context));
await rejects("reject wrong kind", "wrong-kind", () => c.decrypt(area, wrongKind, v.data.context));
await rejects("reject unknown version", "unsupported-version", () => c.decrypt(area, wrongVersion, v.data.context));
await rejects("reject wrong key", "decrypt-failed", () => c.unwrapKey(recovery, bin(v.wrappedVmkByKek), VMK));
await rejects("reject wrapped key as data", "wrong-kind", () => c.decrypt(vmk, bin(v.wrappedAreaKey), AREA));
await rejects("reject kek for data", "wrong-key-type", () => c.encrypt(kek, utf8("x"), "x"));
await rejects("reject master key for data", "wrong-key-type", () => c.encrypt(master, utf8("x"), "x"));
await rejects("reject empty context", "invalid-context", () => c.encrypt(area, utf8("x"), ""));
await rejects("reject long context", "invalid-context", () => c.encrypt(area, utf8("x"), "x".repeat(257)));
await rejects("reject weak argon2", "invalid-argon2-parameters", () => c.deriveMasterKey("pw", bin(v.salt), { memoryKiB: 1024, iterations: 1, parallelism: 1 }));
await rejects("reject short salt", "invalid-salt", () => c.deriveMasterKey("pw", new Uint8Array(8), v.argon2));
await rejects("reject envelope for other context", "decrypt-failed", async () => {
    const ecdh = await c.unwrapPrivateKey("ecdh", vmk, bin(v.ecdh.wrappedPrivateKey), ECDH);
    return c.openSealedKey(ecdh, bin(v.envelope.envelope), "grant:other");
});
await rejects("reject ecdh key wrapped as ecdsa", "wrong-kind", () => c.unwrapPrivateKey("ecdsa", vmk, bin(v.ecdh.wrappedPrivateKey), ECDH));
const file = await c.unwrapKey(area, bin(v.file.wrappedFileKey), v.file.keyContext);
const first = v.file.chunks[0], last = v.file.chunks.at(-1);
await rejects("reject reordered chunk", "decrypt-failed", () => c.decryptChunk(file, 1, first.isLast, bin(first.ciphertext), v.file.context));
await rejects("reject truncated file", "decrypt-failed", () => c.decryptChunk(file, last.index - 1, true, bin(v.file.chunks.at(-2).ciphertext), v.file.context));
await rejects("reject invalid public key", "invalid-public-key", () => c.sealKey(vmk, bin(v.wrappedAreaKey), AREA, new Uint8Array(65), "grant:x"));
await rejects("reject unknown handle", "unknown-key", () => c.encrypt("nicht-vorhanden", utf8("x"), "x"));

c.release(area);
await rejects("released key is gone", "unknown-key", () => c.encrypt(area, utf8("x"), "x"));
c.releaseAll();
check("release all", c.keyCount() === 0);

// ------------------------------------------------------------------ 3. Eigene Vektoren erzeugen

const password = v.password;
const salt = c.randomBytes(16);
const recoveryKey = c.randomBytes(32);
const jsMaster = await c.deriveMasterKey(password, salt, v.argon2);
const jsKek = await c.deriveKeyEncryptionKey(jsMaster);
const jsRecovery = await c.importRecoveryKey(recoveryKey);
const jsVmk = await c.createKey([{ wrapper: jsKek, context: VMK }, { wrapper: jsRecovery, context: VMK }]);
const jsArea = await c.createKey([{ wrapper: jsVmk.handle, context: AREA }]);

const objectId = globalThis.crypto.randomUUID();
const dataContext = `object:insurance:${objectId}`;
const plaintext = utf8(JSON.stringify({ schemaVersion: 1, insurer: "Württembergische", note: objectId }));
const blob = await c.encrypt(jsArea.handle, plaintext, dataContext);

const documentId = globalThis.crypto.randomUUID();
const fileKeyContext = `file:${documentId}`;
const fileContext = `document:${documentId}`;
const jsFile = await c.createKey([{ wrapper: jsArea.handle, context: fileKeyContext }]);
const chunks = [];
const sizes = [1024 * 1024, 5];
for (let index = 0; index < sizes.length; index++) {
    const chunk = new Uint8Array(sizes[index]).map((_, i) => (i * 31 + index) & 0xff);
    const isLast = index === sizes.length - 1;
    chunks.push({ index, isLast, plaintext: hex(chunk), ciphertext: hex(await c.encryptChunk(jsFile.handle, index, isLast, chunk, fileContext)) });
}

const jsEcdh = await c.createKeyPair("ecdh", jsVmk.handle, ECDH);
const jsEcdsa = await c.createKeyPair("ecdsa", jsVmk.handle, ECDSA);
const envelopeContext = `grant:${globalThis.crypto.randomUUID()}:incapacity`;
const envelope = await c.sealKey(jsVmk.handle, jsArea.wrappedKeys[0], AREA, jsEcdh.publicKey, envelopeContext);
const signature = await c.sign(jsEcdsa.handle, jsEcdh.publicKey, SIGNATURE);
const hmacKey = c.randomBytes(32);

const produced = {
    format: 1,
    password,
    passwordDecomposed: password.normalize("NFD"),
    salt: hex(salt),
    argon2: v.argon2,
    authHash: hex(await c.deriveAuthHash(jsMaster)),
    recoveryKey: hex(recoveryKey),
    recoveryAuthHash: hex(await c.deriveRecoveryAuthHash(recoveryKey)),
    wrappedVmkByKek: hex(jsVmk.wrappedKeys[0]),
    wrappedVmkByRecovery: hex(jsVmk.wrappedKeys[1]),
    wrappedAreaKey: hex(jsArea.wrappedKeys[0]),
    data: { context: dataContext, plaintext: hex(plaintext), blob: hex(blob) },
    file: { keyContext: fileKeyContext, wrappedFileKey: hex(jsFile.wrappedKeys[0]), context: fileContext, chunks },
    ecdh: { publicKey: hex(jsEcdh.publicKey), wrappedPrivateKey: hex(jsEcdh.wrappedPrivateKey) },
    ecdsa: { publicKey: hex(jsEcdsa.publicKey), wrappedPrivateKey: hex(jsEcdsa.wrappedPrivateKey) },
    envelope: { context: envelopeContext, envelope: hex(envelope) },
    signature: { context: SIGNATURE, data: hex(jsEcdh.publicKey), signature: hex(signature) },
    hmac: { key: hex(hmacKey), data: hex(jsEcdh.publicKey), mac: hex(await c.hmacSha256(hmacKey, jsEcdh.publicKey)) }
};

// Zusätzlich: Umschlag von JS an den .NET-Schlüssel und ein in JS neu verpackter .NET-VMK.
const netMaster = await c.deriveMasterKey(v.password, bin(v.salt), v.argon2);
const netKek = await c.deriveKeyEncryptionKey(netMaster);
const netRecovery = await c.importRecoveryKey(bin(v.recoveryKey));
const crossContext = `grant:${globalThis.crypto.randomUUID()}:death`;
const extra = {
    envelopeToNetKey: { context: crossContext, envelope: hex(await c.sealKey(jsVmk.handle, jsArea.wrappedKeys[0], AREA, bin(v.ecdh.publicKey), crossContext)) },
    rewrappedNetVmk: { context: "vmk:rewrapped", wrapped: hex(await c.rewrapKey(netKek, bin(v.wrappedVmkByKek), VMK, netRecovery, "vmk:rewrapped")) }
};
check("produced own vectors", true);
c.releaseAll();

process.stdout.write(JSON.stringify({ checks, produced, extra }));
