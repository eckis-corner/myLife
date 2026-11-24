// Passkeys (WebAuthn) für myLife. Wandelt die JSON-Optionen von Fido2NetLib in die Objekte der Browser-API
// und die Antworten zurück in JSON (Base64url). Nutzt die eingebauten JSON-Helfer, wenn der Browser sie hat.

function decode(value) {
    const base64 = value.replace(/-/g, "+").replace(/_/g, "/").padEnd(Math.ceil(value.length / 4) * 4, "=");
    return Uint8Array.from(atob(base64), c => c.charCodeAt(0));
}

function encode(buffer) {
    if (!buffer) return undefined;
    const bytes = new Uint8Array(buffer instanceof ArrayBuffer ? buffer : buffer.buffer);
    let binary = "";
    for (const b of bytes) binary += String.fromCharCode(b);
    return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

/** Entfernt null-Werte; die Browser-API lehnt sie ab. */
function clean(value) {
    if (Array.isArray(value)) return value.map(clean);
    if (value && typeof value === "object") {
        return Object.fromEntries(Object.entries(value).filter(([, v]) => v !== null && v !== undefined).map(([k, v]) => [k, clean(v)]));
    }
    return value;
}

function credentialIds(list) {
    return (list ?? []).map(c => ({ ...c, id: decode(c.id) }));
}

function creationOptions(json) {
    const options = clean(JSON.parse(json));
    if (typeof PublicKeyCredential.parseCreationOptionsFromJSON === "function") {
        try { return PublicKeyCredential.parseCreationOptionsFromJSON(options); } catch { /* Rückfall unten */ }
    }
    return { ...options, challenge: decode(options.challenge), user: { ...options.user, id: decode(options.user.id) }, excludeCredentials: credentialIds(options.excludeCredentials) };
}

function requestOptions(json) {
    const options = clean(JSON.parse(json));
    if (typeof PublicKeyCredential.parseRequestOptionsFromJSON === "function") {
        try { return PublicKeyCredential.parseRequestOptionsFromJSON(options); } catch { /* Rückfall unten */ }
    }
    return { ...options, challenge: decode(options.challenge), allowCredentials: credentialIds(options.allowCredentials) };
}

function toJson(credential) {
    if (typeof credential.toJSON === "function") {
        try { return JSON.stringify(credential.toJSON()); } catch { /* Rückfall unten */ }
    }
    const r = credential.response;
    const response = r.attestationObject
        ? { attestationObject: encode(r.attestationObject), clientDataJSON: encode(r.clientDataJSON), transports: r.getTransports?.() ?? [] }
        : { authenticatorData: encode(r.authenticatorData), clientDataJSON: encode(r.clientDataJSON), signature: encode(r.signature), userHandle: encode(r.userHandle) };
    return JSON.stringify({ id: credential.id, rawId: encode(credential.rawId), type: credential.type, response, clientExtensionResults: credential.getClientExtensionResults?.() ?? {} });
}

export function isSupported() {
    return typeof window.PublicKeyCredential === "function" && !!navigator.credentials;
}

export async function create(optionsJson) {
    if (!isSupported()) throw new Error("mylife:unsupported");
    return toJson(await navigator.credentials.create({ publicKey: creationOptions(optionsJson) }));
}

export async function get(optionsJson) {
    if (!isSupported()) throw new Error("mylife:unsupported");
    return toJson(await navigator.credentials.get({ publicKey: requestOptions(optionsJson) }));
}
