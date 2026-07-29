/**
 * Media encryption primitive using ECDH P-256 key exchange + AES-256-GCM.
 * Callers must authenticate peer keys; Bolt's built-in encrypted call flow remains disabled.
 *
 * Uses the Web Crypto API for all cryptographic operations (hardware-accelerated
 * on most platforms). Compatible with the .NET MediaEncryption implementation.
 *
 * Flow:
 * 1. Create instance → generates ECDH key pair
 * 2. Export public key → send via KeyExchange signal
 * 3. Receive remote public key → deriveKey()
 * 4. Encrypt/decrypt MediaFrame payloads
 *
 * Nonce: 12 bytes = streamId[0..7] + sequenceNumber (4 bytes LE)
 */

import { guidToBytes } from './protocol.js';

export class MediaCrypto {
  private keyPair: CryptoKeyPair | null = null;
  private aesKey: CryptoKey | null = null;
  private _publicKeyDer: ArrayBuffer | null = null;
  private _ready = false;

  /** True once deriveKey has been called and encryption is ready. */
  get isReady(): boolean { return this._ready; }

  /** Initialize ECDH key pair. Must be called before any other method. */
  async init(): Promise<void> {
    this.keyPair = await crypto.subtle.generateKey(
      { name: 'ECDH', namedCurve: 'P-256' },
      true,
      ['deriveKey', 'deriveBits']
    );
    this._publicKeyDer = await crypto.subtle.exportKey('spki', this.keyPair.publicKey);
  }

  /** Get the local ECDH public key in SubjectPublicKeyInfo DER format. */
  getPublicKey(): Uint8Array {
    if (!this._publicKeyDer) throw new Error('Not initialized');
    return new Uint8Array(this._publicKeyDer);
  }

  /**
   * Derive the shared AES-256-GCM key from the remote peer's SPKI public key.
   * Uses HKDF-SHA256 with the callId as salt.
   */
  async deriveKey(remotePublicKeyDer: Uint8Array, callId: string): Promise<void> {
    if (!this.keyPair) throw new Error('Not initialized');

    // Import remote public key
    const remoteKey = await crypto.subtle.importKey(
      'spki',
      toArrayBuffer(remotePublicKeyDer),
      { name: 'ECDH', namedCurve: 'P-256' },
      false,
      []
    );

    // Derive raw shared secret via ECDH
    const sharedBits = await crypto.subtle.deriveBits(
      { name: 'ECDH', public: remoteKey },
      this.keyPair.privateKey,
      256
    );

    // Import shared secret as HKDF base key
    const hkdfKey = await crypto.subtle.importKey(
      'raw',
      sharedBits,
      'HKDF',
      false,
      ['deriveKey']
    );

    // Derive AES-256-GCM key using HKDF with callId as salt
    const salt = guidToBytes(callId);
    const info = new TextEncoder().encode('bolt-media-e2e');

    this.aesKey = await crypto.subtle.deriveKey(
      { name: 'HKDF', hash: 'SHA-256', salt: toArrayBuffer(salt), info: toArrayBuffer(info) },
      hkdfKey,
      { name: 'AES-GCM', length: 256 },
      false,
      ['encrypt', 'decrypt']
    );

    this._ready = true;
  }

  /**
   * Encrypt a media frame payload. Returns ciphertext + 16-byte auth tag.
   */
  async encrypt(plaintext: Uint8Array, sequenceNumber: number, streamId: string): Promise<Uint8Array> {
    if (!this.aesKey) throw new Error('Key not derived');

    const iv = buildNonce(streamId, sequenceNumber);
    const encrypted = await crypto.subtle.encrypt(
      { name: 'AES-GCM', iv: toArrayBuffer(iv), tagLength: 128 },
      this.aesKey,
      toArrayBuffer(plaintext)
    );

    return new Uint8Array(encrypted);
  }

  /**
   * Decrypt a media frame payload (ciphertext + 16-byte tag).
   */
  async decrypt(ciphertextWithTag: Uint8Array, sequenceNumber: number, streamId: string): Promise<Uint8Array> {
    if (!this.aesKey) throw new Error('Key not derived');

    const iv = buildNonce(streamId, sequenceNumber);
    const decrypted = await crypto.subtle.decrypt(
      { name: 'AES-GCM', iv: toArrayBuffer(iv), tagLength: 128 },
      this.aesKey,
      toArrayBuffer(ciphertextWithTag)
    );

    return new Uint8Array(decrypted);
  }
}

/**
 * Build a 12-byte nonce from streamId (first 8 bytes) + sequenceNumber (4 bytes LE).
 * Matches the .NET MediaEncryption.BuildNonce exactly.
 */
function buildNonce(streamId: string, sequenceNumber: number): Uint8Array {
  const nonce = new Uint8Array(12);
  const guidBytes = guidToBytes(streamId);
  nonce.set(guidBytes.subarray(0, 8), 0);
  new DataView(nonce.buffer).setUint32(8, sequenceNumber >>> 0, true);
  return nonce;
}

function toArrayBuffer(bytes: Uint8Array): ArrayBuffer {
  const copy = bytes.slice();
  return copy.buffer as ArrayBuffer;
}
