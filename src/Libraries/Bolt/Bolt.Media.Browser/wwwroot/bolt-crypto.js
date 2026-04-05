// Bolt Media — Web Crypto encryption module
// ECDH P-256 key exchange + AES-256-GCM frame encryption
// Compatible with .NET MediaEncryption (same nonce/HKDF params)

class BoltCrypto {
    constructor() {
        this.keyPair = null;
        this.aesKey = null;
        this.publicKeyDer = null;
        this.ready = false;
    }

    async init() {
        this.keyPair = await crypto.subtle.generateKey(
            { name: 'ECDH', namedCurve: 'P-256' },
            false,
            ['deriveBits']
        );
        const exported = await crypto.subtle.exportKey('spki', this.keyPair.publicKey);
        this.publicKeyDer = new Uint8Array(exported);
    }

    getPublicKey() {
        return this.publicKeyDer;
    }

    async deriveKey(remotePublicKeyDer, callIdBytes) {
        const remotePubKey = await crypto.subtle.importKey(
            'spki', remotePublicKeyDer,
            { name: 'ECDH', namedCurve: 'P-256' },
            false, []
        );

        const sharedBits = await crypto.subtle.deriveBits(
            { name: 'ECDH', public: remotePubKey },
            this.keyPair.privateKey,
            256
        );

        const hkdfKey = await crypto.subtle.importKey(
            'raw', sharedBits, 'HKDF', false, ['deriveKey']
        );

        const encoder = new TextEncoder();
        this.aesKey = await crypto.subtle.deriveKey(
            {
                name: 'HKDF',
                hash: 'SHA-256',
                salt: callIdBytes,
                info: encoder.encode('bolt-media-e2e')
            },
            hkdfKey,
            { name: 'AES-GCM', length: 256 },
            false,
            ['encrypt', 'decrypt']
        );
        this.ready = true;
    }

    async encrypt(plaintext, sequenceNumber, streamIdBytes) {
        const nonce = this._buildNonce(streamIdBytes, sequenceNumber);
        const encrypted = await crypto.subtle.encrypt(
            { name: 'AES-GCM', iv: nonce, tagLength: 128 },
            this.aesKey,
            plaintext
        );
        return new Uint8Array(encrypted);
    }

    async decrypt(ciphertextWithTag, sequenceNumber, streamIdBytes) {
        const nonce = this._buildNonce(streamIdBytes, sequenceNumber);
        const decrypted = await crypto.subtle.decrypt(
            { name: 'AES-GCM', iv: nonce, tagLength: 128 },
            this.aesKey,
            ciphertextWithTag
        );
        return new Uint8Array(decrypted);
    }

    _buildNonce(streamIdBytes, sequenceNumber) {
        // 12 bytes: streamId[0..7] + sequenceNumber (4 bytes LE)
        const nonce = new Uint8Array(12);
        nonce.set(streamIdBytes.slice(0, 8), 0);
        const view = new DataView(nonce.buffer);
        view.setUint32(8, sequenceNumber, true); // little-endian
        return nonce;
    }

    dispose() {
        this.keyPair = null;
        this.aesKey = null;
        this.publicKeyDer = null;
        this.ready = false;
    }
}

export function create() {
    return new BoltCrypto();
}
