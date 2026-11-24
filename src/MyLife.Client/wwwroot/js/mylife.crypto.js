// myLife.crypto.js – Krypto-Kern im Browser (Formatversion 1).
// Beschreibung der Formate: docs/kryptoformat.md. Die .NET-Referenzimplementierung in MyLife.Crypto
// muss byte-genau dieselben Formate erzeugen und lesen; die Kreuztests prüfen das.
//
// Schlüssel verlassen dieses Modul nie: Aufrufer bekommen nur Handles (Zeichenketten). Alle Schlüssel
// im Register sind nicht exportierbar. Wo ein Schlüssel neu verpackt oder in einen Umschlag gelegt
// werden muss, entpackt das Modul ihn kurzzeitig aus seiner verpackten Form und verwirft ihn danach.
//
// Läuft im Browser (ES-Modul per JS-Interop) und in Node.js ≥ 20 (Kreuztests).

const FORMAT_VERSION = 1;

/** Art eines Datensatzes, zweites Byte jedes Formats. */
export const Kind = Object.freeze({
    Data: 0x01,
    WrappedKey: 0x02,
    WrappedEcdhKey: 0x03,
    WrappedEcdsaKey: 0x04,
    Envelope: 0x05,
    FileChunk: 0x06,
    Signature: 0x07
});

const NONCE_LENGTH = 12;
const TAG_LENGTH = 16;
const KEY_LENGTH = 32;
const PUBLIC_KEY_LENGTH = 65;
const MAX_CONTEXT_BYTES = 256;
const MAX_CHUNK_BYTES = 1024 * 1024;
const MIN_SALT_BYTES = 16;
const MAX_SALT_BYTES = 64;

const INFO_AUTH = "mylife/auth";
const INFO_KEK = "mylife/kek";
const INFO_RECOVERY = "mylife/recovery";
const INFO_RECOVERY_AUTH = "mylife/recovery-auth";
const INFO_ECIES = "mylife/ecies";

/** Untergrenzen schützen vor einem Server, der schwache Parameter unterschiebt; Obergrenzen vor Überlast. */
export const Argon2Limits = Object.freeze({
    minMemoryKiB: 65536, maxMemoryKiB: 1048576,
    minIterations: 3, maxIterations: 20,
    minParallelism: 1, maxParallelism: 8
});

const AES = { name: "AES-GCM", length: 256 };
const P256_ECDH = { name: "ECDH", namedCurve: "P-256" };
const P256_ECDSA = { name: "ECDSA", namedCurve: "P-256" };
const AES_USAGES = ["encrypt", "decrypt", "wrapKey", "unwrapKey"];
const WRAP_USAGES = ["wrapKey", "unwrapKey"];

/** Schlüsselarten im Register. Jede Operation nimmt nur die Arten an, für die sie gedacht ist. */
const KeyType = Object.freeze({
    Master: "master",   // Stammschlüssel (HKDF-Basis), nur ableiten
    Wrap: "wrap",       // KEK oder Recovery-Schlüssel, nur ver- und entpacken
    Aes: "aes",         // Tresor-, Bereichs-, Brief- und Dateischlüssel
    Ecdh: "ecdh",       // privater Schlüssel für Umschläge
    Ecdsa: "ecdsa"      // privater Signaturschlüssel
});

const subtle = globalThis.crypto.subtle;
const utf8 = new TextEncoder();
const keys = new Map();

// ---------------------------------------------------------------- Hilfsfunktionen

function fail(code) {
    throw new Error(`mylife:${code}`);
}

function concat(...parts) {
    const length = parts.reduce((sum, p) => sum + p.length, 0);
    const result = new Uint8Array(length);
    let offset = 0;
    for (const part of parts) {
        result.set(part, offset);
        offset += part.length;
    }
    return result;
}

function bytes(value, name) {
    if (value instanceof Uint8Array) return value;
    if (value instanceof ArrayBuffer) return new Uint8Array(value);
    fail(`invalid-${name}`);
}

function uint32(value) {
    const result = new Uint8Array(4);
    new DataView(result.buffer).setUint32(0, value, false);
    return result;
}

function header(kind) {
    return new Uint8Array([FORMAT_VERSION, kind]);
}

function contextBytes(context) {
    if (typeof context !== "string" || context.length === 0) fail("invalid-context");
    const encoded = utf8.encode(context);
    if (encoded.length > MAX_CONTEXT_BYTES) fail("invalid-context");
    return encoded;
}

/** AAD aller AES-GCM-Formate außer Dateiblöcken: Version, Art, Kontext. */
function aad(kind, context) {
    return concat(header(kind), contextBytes(context));
}

function gcm(nonce, additionalData) {
    return { name: "AES-GCM", iv: nonce, additionalData, tagLength: TAG_LENGTH * 8 };
}

function random(length) {
    return globalThis.crypto.getRandomValues(new Uint8Array(length));
}

/** Zerlegt Version | Art | Nonce | Chiffrat+Tag und prüft den Kopf. */
function parseSealed(blob, kind) {
    const data = bytes(blob, "blob");
    if (data.length < 2 + NONCE_LENGTH + TAG_LENGTH) fail("format");
    if (data[0] !== FORMAT_VERSION) fail("unsupported-version");
    if (data[1] !== kind) fail("wrong-kind");
    return { nonce: data.subarray(2, 2 + NONCE_LENGTH), ciphertext: data.subarray(2 + NONCE_LENGTH) };
}

function checkPublicKey(publicKey) {
    const data = bytes(publicKey, "public-key");
    if (data.length !== PUBLIC_KEY_LENGTH || data[0] !== 0x04) fail("invalid-public-key");
    return data;
}

/** Übersetzt Fehler der Web Crypto API (OperationError) in eine einheitliche Meldung. */
async function guard(code, action) {
    try {
        return await action();
    } catch (error) {
        if (error instanceof Error && error.message.startsWith("mylife:")) throw error;
        fail(code);
    }
}

// ---------------------------------------------------------------- Schlüsselregister

function store(key, type) {
    const handle = globalThis.crypto.randomUUID();
    keys.set(handle, { key, type });
    return handle;
}

function lookup(handle, ...types) {
    const entry = keys.get(handle);
    if (!entry) fail("unknown-key");
    if (!types.includes(entry.type)) fail("wrong-key-type");
    return entry.key;
}

/** Verwirft einen Schlüssel aus dem Register. */
export function release(handle) {
    keys.delete(handle);
}

/** Verwirft alle Schlüssel, etwa bei der Abmeldung. */
export function releaseAll() {
    keys.clear();
}

/** Anzahl der Schlüssel im Register (für Tests und Diagnose). */
export function keyCount() {
    return keys.size;
}

// ---------------------------------------------------------------- Argon2id (hash-wasm)

let argon2Loading = null;

async function argon2id() {
    if (globalThis.hashwasm?.argon2id) return globalThis.hashwasm.argon2id;
    if (typeof document === "undefined") fail("argon2-unavailable");
    argon2Loading ??= new Promise((resolve, reject) => {
        const script = document.createElement("script");
        script.src = new URL("../lib/hash-wasm/argon2.umd.min.js", import.meta.url).href;
        script.onload = resolve;
        script.onerror = () => reject(new Error("mylife:argon2-unavailable"));
        document.head.appendChild(script);
    });
    await argon2Loading;
    if (!globalThis.hashwasm?.argon2id) fail("argon2-unavailable");
    return globalThis.hashwasm.argon2id;
}

function checkArgon2Parameters(parameters) {
    const { memoryKiB, iterations, parallelism } = parameters ?? {};
    const inRange = (value, min, max) => Number.isInteger(value) && value >= min && value <= max;
    if (!inRange(memoryKiB, Argon2Limits.minMemoryKiB, Argon2Limits.maxMemoryKiB)
        || !inRange(iterations, Argon2Limits.minIterations, Argon2Limits.maxIterations)
        || !inRange(parallelism, Argon2Limits.minParallelism, Argon2Limits.maxParallelism)) {
        fail("invalid-argon2-parameters");
    }
}

// ---------------------------------------------------------------- Öffentliche Funktionen

/** Kryptografisch sichere Zufallsbytes (1–1024). */
export function randomBytes(length) {
    if (!Number.isInteger(length) || length < 1 || length > 1024) fail("invalid-length");
    return random(length);
}

/**
 * Stammschlüssel aus dem Master-Passwort: Argon2id über das Passwort in Unicode-NFC als UTF-8.
 * Das Ergebnis bleibt als HKDF-Schlüssel im Register.
 */
export async function deriveMasterKey(password, salt, parameters) {
    if (typeof password !== "string" || password.length === 0) fail("invalid-password");
    const saltBytes = bytes(salt, "salt");
    if (saltBytes.length < MIN_SALT_BYTES || saltBytes.length > MAX_SALT_BYTES) fail("invalid-salt");
    checkArgon2Parameters(parameters);

    const hash = await argon2id();
    const raw = await hash({
        password: utf8.encode(password.normalize("NFC")),
        salt: saltBytes,
        memorySize: parameters.memoryKiB,
        iterations: parameters.iterations,
        parallelism: parameters.parallelism,
        hashLength: KEY_LENGTH,
        outputType: "binary"
    });
    try {
        const key = await subtle.importKey("raw", raw, "HKDF", false, ["deriveBits", "deriveKey"]);
        return store(key, KeyType.Master);
    } finally {
        raw.fill(0);
    }
}

function hkdf(info) {
    return { name: "HKDF", hash: "SHA-256", salt: new Uint8Array(0), info: utf8.encode(info) };
}

/** Anmeldehash für den Server: HKDF(Stammschlüssel, info "mylife/auth"), 32 Byte. */
export async function deriveAuthHash(masterKey) {
    const key = lookup(masterKey, KeyType.Master);
    return new Uint8Array(await subtle.deriveBits(hkdf(INFO_AUTH), key, KEY_LENGTH * 8));
}

/** Key-Encryption-Key: HKDF(Stammschlüssel, info "mylife/kek"), nur zum Ver- und Entpacken. */
export async function deriveKeyEncryptionKey(masterKey) {
    const key = lookup(masterKey, KeyType.Master);
    const kek = await subtle.deriveKey(hkdf(INFO_KEK), key, AES, false, WRAP_USAGES);
    return store(kek, KeyType.Wrap);
}

/** Schlüssel aus dem Recovery-Key (32 Byte): HKDF(info "mylife/recovery"), nur zum Ver- und Entpacken. */
export async function importRecoveryKey(recoveryKey) {
    const raw = bytes(recoveryKey, "recovery-key");
    if (raw.length !== KEY_LENGTH) fail("invalid-recovery-key");
    const base = await subtle.importKey("raw", raw, "HKDF", false, ["deriveKey"]);
    const key = await subtle.deriveKey(hkdf(INFO_RECOVERY), base, AES, false, WRAP_USAGES);
    return store(key, KeyType.Wrap);
}

/**
 * Prüfwert des Recovery-Keys für den Server: HKDF(Recovery-Key, info "mylife/recovery-auth"), 32 Byte.
 * Damit beweist der Browser bei der Wiederherstellung, dass er die 24 Wörter kennt, ohne sie preiszugeben.
 */
export async function deriveRecoveryAuthHash(recoveryKey) {
    const raw = bytes(recoveryKey, "recovery-key");
    if (raw.length !== KEY_LENGTH) fail("invalid-recovery-key");
    const base = await subtle.importKey("raw", raw, "HKDF", false, ["deriveBits"]);
    return new Uint8Array(await subtle.deriveBits(hkdf(INFO_RECOVERY_AUTH), base, KEY_LENGTH * 8));
}

async function wrapSecretKey(extractableKey, wrapper, context) {
    const nonce = random(NONCE_LENGTH);
    const ciphertext = await subtle.wrapKey("raw", extractableKey, wrapper, gcm(nonce, aad(Kind.WrappedKey, context)));
    return concat(header(Kind.WrappedKey), nonce, new Uint8Array(ciphertext));
}

async function unwrapSecretKey(wrapper, wrappedKey, context, extractable) {
    const { nonce, ciphertext } = parseSealed(wrappedKey, Kind.WrappedKey);
    return guard("decrypt-failed", () => subtle.unwrapKey(
        "raw", ciphertext, wrapper, gcm(nonce, aad(Kind.WrappedKey, context)), AES, extractable, AES_USAGES));
}

/**
 * Erzeugt einen neuen AES-256-Schlüssel (Tresor-, Bereichs-, Brief- oder Dateischlüssel) und verpackt ihn
 * mit jedem angegebenen Schlüssel. wrappings: [{ wrapper: handle, context: string }], mindestens einer.
 * Ergebnis: { handle, wrappedKeys: [Uint8Array, …] } in derselben Reihenfolge.
 */
export async function createKey(wrappings) {
    if (!Array.isArray(wrappings) || wrappings.length === 0) fail("no-wrapping");
    const wrappers = wrappings.map(w => ({ key: lookup(w.wrapper, KeyType.Wrap, KeyType.Aes), context: w.context }));
    wrappers.forEach(w => contextBytes(w.context));

    const extractable = await subtle.generateKey(AES, true, AES_USAGES);
    const wrappedKeys = [];
    for (const w of wrappers) {
        wrappedKeys.push(await wrapSecretKey(extractable, w.key, w.context));
    }
    const key = await unwrapSecretKey(wrappers[0].key, wrappedKeys[0], wrappers[0].context, false);
    return { handle: store(key, KeyType.Aes), wrappedKeys };
}

/** Entpackt einen verpackten AES-Schlüssel als nicht exportierbaren Schlüssel. */
export async function unwrapKey(wrapper, wrappedKey, context) {
    const key = await unwrapSecretKey(lookup(wrapper, KeyType.Wrap, KeyType.Aes), wrappedKey, context, false);
    return store(key, KeyType.Aes);
}

/** Verpackt einen Schlüssel neu, etwa nach einer Passwortänderung. Der Klartextschlüssel verlässt das Modul nicht. */
export async function rewrapKey(oldWrapper, wrappedKey, oldContext, newWrapper, newContext) {
    const source = lookup(oldWrapper, KeyType.Wrap, KeyType.Aes);
    const target = lookup(newWrapper, KeyType.Wrap, KeyType.Aes);
    contextBytes(newContext);
    const extractable = await unwrapSecretKey(source, wrappedKey, oldContext, true);
    return wrapSecretKey(extractable, target, newContext);
}

/** AES-256-GCM: Version | Art 0x01 | Nonce (12) | Chiffrat | Tag (16). */
export async function encrypt(key, plaintext, context) {
    const aesKey = lookup(key, KeyType.Aes);
    const nonce = random(NONCE_LENGTH);
    const ciphertext = await subtle.encrypt(gcm(nonce, aad(Kind.Data, context)), aesKey, bytes(plaintext, "plaintext"));
    return concat(header(Kind.Data), nonce, new Uint8Array(ciphertext));
}

export async function decrypt(key, blob, context) {
    const aesKey = lookup(key, KeyType.Aes);
    const { nonce, ciphertext } = parseSealed(blob, Kind.Data);
    const additionalData = aad(Kind.Data, context);
    return new Uint8Array(await guard("decrypt-failed", () => subtle.decrypt(gcm(nonce, additionalData), aesKey, ciphertext)));
}

function chunkParameters(index, isLast, context) {
    if (!Number.isInteger(index) || index < 0 || index > 0xFFFFFFFF) fail("invalid-chunk-index");
    if (typeof isLast !== "boolean") fail("invalid-chunk-flag");
    const nonce = concat(new Uint8Array(NONCE_LENGTH - 4), uint32(index));
    const additionalData = concat(header(Kind.FileChunk), uint32(index), new Uint8Array([isLast ? 1 : 0]), contextBytes(context));
    return gcm(nonce, additionalData);
}

/**
 * Ein Block eines Dokuments (höchstens 1 MiB). Nonce = Blocknummer, die AAD markiert den letzten Block.
 * Ein Dateischlüssel darf nur für die Blöcke genau einer Datei verwendet werden.
 */
export async function encryptChunk(fileKey, index, isLast, data, context) {
    const key = lookup(fileKey, KeyType.Aes);
    const plaintext = bytes(data, "chunk");
    if (plaintext.length > MAX_CHUNK_BYTES) fail("chunk-too-large");
    return new Uint8Array(await subtle.encrypt(chunkParameters(index, isLast, context), key, plaintext));
}

export async function decryptChunk(fileKey, index, isLast, data, context) {
    const key = lookup(fileKey, KeyType.Aes);
    const ciphertext = bytes(data, "chunk");
    if (ciphertext.length < TAG_LENGTH || ciphertext.length > MAX_CHUNK_BYTES + TAG_LENGTH) fail("format");
    const parameters = chunkParameters(index, isLast, context);
    return new Uint8Array(await guard("decrypt-failed", () => subtle.decrypt(parameters, key, ciphertext)));
}

function keyPairAlgorithm(kind) {
    switch (kind) {
        case "ecdh": return { algorithm: P256_ECDH, usages: ["deriveBits"], wrapKind: Kind.WrappedEcdhKey, type: KeyType.Ecdh };
        case "ecdsa": return { algorithm: P256_ECDSA, usages: ["sign"], wrapKind: Kind.WrappedEcdsaKey, type: KeyType.Ecdsa };
        default: fail("invalid-key-pair-kind");
    }
}

/**
 * Erzeugt ein Schlüsselpaar P-256 ("ecdh" für Umschläge, "ecdsa" für Signaturen). Der private Schlüssel
 * wird als PKCS#8 mit dem Tresorschlüssel verpackt. Ergebnis: { handle, publicKey (65 Byte, unkomprimiert), wrappedPrivateKey }.
 */
export async function createKeyPair(kind, wrapper, context) {
    const spec = keyPairAlgorithm(kind);
    const wrapKey = lookup(wrapper, KeyType.Wrap, KeyType.Aes);
    contextBytes(context);
    const usages = kind === "ecdh" ? ["deriveBits"] : ["sign", "verify"];
    const pair = await subtle.generateKey(spec.algorithm, true, usages);
    const publicKey = new Uint8Array(await subtle.exportKey("raw", pair.publicKey));
    const nonce = random(NONCE_LENGTH);
    const ciphertext = await subtle.wrapKey("pkcs8", pair.privateKey, wrapKey, gcm(nonce, aad(spec.wrapKind, context)));
    const wrappedPrivateKey = concat(header(spec.wrapKind), nonce, new Uint8Array(ciphertext));
    const handle = await unwrapPrivateKey(kind, wrapper, wrappedPrivateKey, context);
    return { handle, publicKey, wrappedPrivateKey };
}

/** Entpackt einen privaten Schlüssel als nicht exportierbaren Schlüssel. */
export async function unwrapPrivateKey(kind, wrapper, wrappedPrivateKey, context) {
    const spec = keyPairAlgorithm(kind);
    const wrapKey = lookup(wrapper, KeyType.Wrap, KeyType.Aes);
    const { nonce, ciphertext } = parseSealed(wrappedPrivateKey, spec.wrapKind);
    const additionalData = aad(spec.wrapKind, context);
    const key = await guard("decrypt-failed", () => subtle.unwrapKey(
        "pkcs8", ciphertext, wrapKey, gcm(nonce, additionalData), spec.algorithm, false, spec.usages));
    return store(key, spec.type);
}

async function eciesKey(sharedSecret, ephemeralPublicKey) {
    const base = await subtle.importKey("raw", sharedSecret, "HKDF", false, ["deriveKey"]);
    return subtle.deriveKey(
        { name: "HKDF", hash: "SHA-256", salt: ephemeralPublicKey, info: utf8.encode(INFO_ECIES) },
        base, AES, false, ["encrypt", "decrypt"]);
}

/**
 * Legt einen verpackten AES-Schlüssel in einen Umschlag für den öffentlichen Schlüssel einer Vertrauensperson (ECIES):
 * Version | Art 0x05 | ephemerer öffentlicher Schlüssel (65) | Nonce (12) | Chiffrat (32) | Tag (16).
 */
export async function sealKey(wrapper, wrappedKey, wrappedContext, recipientPublicKey, envelopeContext) {
    const wrapKey = lookup(wrapper, KeyType.Wrap, KeyType.Aes);
    const recipientBytes = checkPublicKey(recipientPublicKey);
    const additionalData = aad(Kind.Envelope, envelopeContext);
    const recipient = await guard("invalid-public-key", () => subtle.importKey("raw", recipientBytes, P256_ECDH, false, []));

    const extractable = await unwrapSecretKey(wrapKey, wrappedKey, wrappedContext, true);
    const raw = new Uint8Array(await subtle.exportKey("raw", extractable));
    try {
        const ephemeral = await subtle.generateKey(P256_ECDH, false, ["deriveBits"]);
        const ephemeralPublicKey = new Uint8Array(await subtle.exportKey("raw", ephemeral.publicKey));
        const sharedSecret = await subtle.deriveBits({ name: "ECDH", public: recipient }, ephemeral.privateKey, 256);
        const key = await eciesKey(sharedSecret, ephemeralPublicKey);
        const nonce = random(NONCE_LENGTH);
        const ciphertext = await subtle.encrypt(gcm(nonce, additionalData), key, raw);
        return concat(header(Kind.Envelope), ephemeralPublicKey, nonce, new Uint8Array(ciphertext));
    } finally {
        raw.fill(0);
    }
}

/** Öffnet einen Umschlag mit dem eigenen privaten ECDH-Schlüssel. Ergebnis: Handle des enthaltenen AES-Schlüssels. */
export async function openSealedKey(privateKey, envelope, context) {
    const key = lookup(privateKey, KeyType.Ecdh);
    const data = bytes(envelope, "envelope");
    if (data.length !== 2 + PUBLIC_KEY_LENGTH + NONCE_LENGTH + KEY_LENGTH + TAG_LENGTH) fail("format");
    if (data[0] !== FORMAT_VERSION) fail("unsupported-version");
    if (data[1] !== Kind.Envelope) fail("wrong-kind");
    const ephemeralPublicKey = checkPublicKey(data.subarray(2, 2 + PUBLIC_KEY_LENGTH));
    const nonce = data.subarray(2 + PUBLIC_KEY_LENGTH, 2 + PUBLIC_KEY_LENGTH + NONCE_LENGTH);
    const ciphertext = data.subarray(2 + PUBLIC_KEY_LENGTH + NONCE_LENGTH);
    const additionalData = aad(Kind.Envelope, context);

    return guard("decrypt-failed", async () => {
        const ephemeral = await subtle.importKey("raw", ephemeralPublicKey, P256_ECDH, false, []);
        const sharedSecret = await subtle.deriveBits({ name: "ECDH", public: ephemeral }, key, 256);
        const aesKey = await eciesKey(sharedSecret, ephemeralPublicKey);
        const raw = new Uint8Array(await subtle.decrypt(gcm(nonce, additionalData), aesKey, ciphertext));
        try {
            const opened = await subtle.importKey("raw", raw, AES, false, AES_USAGES);
            return store(opened, KeyType.Aes);
        } finally {
            raw.fill(0);
        }
    });
}

function signedMessage(context, data) {
    const contextData = contextBytes(context);
    return concat(header(Kind.Signature), uint32(contextData.length), contextData, bytes(data, "data"));
}

/** ECDSA P-256 mit SHA-256, Signatur im Format IEEE P1363 (r | s, 64 Byte). */
export async function sign(signingKey, data, context) {
    const key = lookup(signingKey, KeyType.Ecdsa);
    return new Uint8Array(await subtle.sign({ name: "ECDSA", hash: "SHA-256" }, key, signedMessage(context, data)));
}

export async function verify(publicKey, data, signature, context) {
    const message = signedMessage(context, data);
    const signatureBytes = bytes(signature, "signature");
    const key = await guard("invalid-public-key", () => subtle.importKey("raw", checkPublicKey(publicKey), P256_ECDSA, false, ["verify"]));
    if (signatureBytes.length !== 64) return false;
    return subtle.verify({ name: "ECDSA", hash: "SHA-256" }, key, signatureBytes, message);
}

export async function sha256(data) {
    return new Uint8Array(await subtle.digest("SHA-256", bytes(data, "data")));
}

/** HMAC-SHA-256, etwa für die Bindung des öffentlichen Schlüssels an das Einladungsgeheimnis. */
export async function hmacSha256(key, data) {
    const keyBytes = bytes(key, "key");
    if (keyBytes.length < 16) fail("invalid-key");
    const hmacKey = await subtle.importKey("raw", keyBytes, { name: "HMAC", hash: "SHA-256" }, false, ["sign"]);
    return new Uint8Array(await subtle.sign("HMAC", hmacKey, bytes(data, "data")));
}
