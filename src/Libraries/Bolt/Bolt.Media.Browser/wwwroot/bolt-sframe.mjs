import initialize, { SFrameSender, SFrameReceiver } from './sframe/bolt_sframe.js';

const encoder = new TextEncoder();
const maxEpochs = 256;
let initialized;

export async function initializeSFrame(wasmBytes) {
    initialized ??= initialize(wasmBytes ? { module_or_path: wasmBytes } : undefined);
    await initialized;
}

export async function createSession(callId, localSenderId) {
    await initializeSFrame();
    return new SFrameSession(callId, localSenderId);
}

function identifier(value) {
    if (typeof value !== 'string' || value.length < 1 || value.length > 128)
        throw new Error('Invalid SFrame identity');
    return value;
}
/// Sender KIDs below 8 are reserved: a compact frame's RFC 9605 header uses KID 1 as its format marker.
const minSenderKid = 8n;
function keyId(value) {
    if (typeof value !== 'string' || !/^(0|[1-9][0-9]{0,19})$/.test(value)
        || BigInt(value) > 18446744073709551615n || BigInt(value) < minSenderKid) throw new Error('Invalid SFrame key ID');
    return BigInt(value);
}
/// A compact frame: RFC 9605 config byte with X = 0 and KID = 1 (the format marker), whatever its counter.
function isCompact(payload) { return (payload[0] & 0xf0) === 0x10; }
function keyBytes(value) {
    if (!(value instanceof Uint8Array) || value.length !== 32)
        throw new Error('Invalid SFrame base key');
    return value;
}
function sameKey(left, right) { return left.every((byte, index) => byte === right[index]); }

// Install only keys obtained from verified, expected-signer OpenPGP envelopes.
// This class cannot authenticate the directory or membership on its own.
export class SFrameSession {
    #callId; #localSenderId; #epoch; #sender; #receivers = new Map();
    #usedKids = new Set(); #epochs = 0; #active = false; #disposed = false; #compact = false;

    constructor(callId, localSenderId) {
        this.#callId = identifier(callId);
        this.#localSenderId = identifier(localSenderId);
    }

    // Pause immediately on any membership change, before asynchronous key exchange.
    pause() { this.#active = false; }

    /// compact: send compact frames (about 20 bytes of SFrame per frame instead of about 278). Set it only when every
    /// remote member of this epoch said, in its authenticated key envelope, that it reads them. Receiving takes both.
    installEpoch({ epochId, rosterBinding, local, remote, compact = false }) {
        this.pause();
        if (this.#disposed || this.#epochs >= maxEpochs) throw new Error('SFrame session ended');
        identifier(epochId);
        // Binding is the hash of the complete canonical authenticated participant roster.
        if (typeof rosterBinding !== 'string' || !/^[a-f0-9]{64}$/.test(rosterBinding))
            throw new Error('Invalid SFrame roster binding');
        if (this.#epoch?.epochId === epochId) throw new Error('SFrame epoch already installed');
        if (!Array.isArray(remote) || remote.length < 1 || remote.length > 7)
            throw new Error('SFrame supports two to eight participants');
        const entries = [local, ...remote];
        const ids = new Set(); const kids = new Set();
        for (const entry of entries) {
            identifier(entry.senderId); keyId(entry.kid); keyBytes(entry.key);
            if (ids.has(entry.senderId) || kids.has(entry.kid) || this.#usedKids.has(entry.kid))
                throw new Error('SFrame sender or key ID reuse');
            ids.add(entry.senderId); kids.add(entry.kid);
        }
        if (local.senderId !== this.#localSenderId) throw new Error('Wrong SFrame local sender');
        for (let i = 0; i < entries.length; ++i)
            for (let j = 0; j < i; ++j)
                if (sameKey(entries[i].key, entries[j].key)) throw new Error('Shared sender keys forbidden');

        let sender; const receivers = new Map();
        try {
            sender = new SFrameSender(keyId(local.kid), local.key);
            for (const entry of remote)
                receivers.set(entry.senderId, new SFrameReceiver(keyId(entry.kid), entry.key));
        } catch (error) {
            sender?.free(); for (const receiver of receivers.values()) receiver.free();
            throw error;
        }
        this.#clearKeys();
        this.#sender = sender; this.#receivers = receivers;
        this.#epoch = { epochId, rosterBinding };
        this.#compact = compact === true;
        for (const kid of kids) this.#usedKids.add(kid);
        ++this.#epochs;
        // Caller wipes envelope plaintext/base-key arrays after installing them.
    }

    // Call only after all current participants acknowledged this exact epoch/roster.
    activateEpoch(epochId, rosterBinding) {
        if (this.#disposed || this.#epoch?.epochId !== epochId || this.#epoch?.rosterBinding !== rosterBinding)
            throw new Error('SFrame epoch is not installed');
        this.#active = true;
    }

    /// The authenticated context. Legacy frames carry it inside the ciphertext; compact frames authenticate it as
    /// associated data and never send it. The label separates the two formats' contexts.
    #aad(senderId, streamId, sequence, timestamp, compact) {
        if (!Number.isInteger(sequence) || sequence < 0 || sequence > 0xffffffff
            || !Number.isInteger(timestamp) || timestamp < 0 || timestamp > 0xffffffff)
            throw new Error('Invalid SFrame media routing');
        return encoder.encode(JSON.stringify([compact ? 'bolt-sframe-v2' : 'bolt-sframe-v1', this.#callId, this.#epoch.epochId,
            this.#epoch.rosterBinding, senderId, identifier(streamId), sequence, timestamp]));
    }

    /// Whether this epoch sends compact frames.
    get compact() { return this.#compact; }

    encrypt(payload, streamId, sequence, timestamp) {
        if (!this.#active || this.#disposed) throw new Error('SFrame sending paused');
        if (!(payload instanceof Uint8Array) || payload.length < 1 || payload.length > 4096)
            throw new Error('Invalid audio payload');
        const compact = this.#compact;
        return this.#sender.encrypt(payload, this.#aad(this.#localSenderId, streamId, sequence, timestamp, compact), compact);
    }

    decrypt(senderId, payload, streamId, sequence, timestamp) {
        if (!this.#active || this.#disposed) throw new Error('SFrame receiving paused');
        if (!(payload instanceof Uint8Array) || payload.length < 1 || payload.length > 5155)
            throw new Error('Invalid encrypted audio payload');
        const receiver = this.#receivers.get(senderId);
        if (!receiver) throw new Error('Unknown SFrame sender');
        // The header names the format; a frame that lies about it fails authentication like any other edit.
        return receiver.decrypt(payload, this.#aad(senderId, streamId, sequence, timestamp, isCompact(payload)));
    }

    #clearKeys() {
        this.#sender?.free(); this.#sender = undefined;
        for (const receiver of this.#receivers.values()) receiver.free();
        this.#receivers.clear();
    }
    dispose() {
        this.pause(); this.#clearKeys(); this.#epoch = undefined; this.#compact = false;
        this.#usedKids.clear(); this.#disposed = true;
    }
}
