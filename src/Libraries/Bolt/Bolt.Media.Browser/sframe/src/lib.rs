//! Thin binding to RFC 9605, not a key exchange or a new encryption protocol.
//!
//! Two frame formats share one key, one counter and one replay window per sender:
//!
//! - **legacy** (`bolt-sframe-v1`): the RFC frame API with empty metadata. The authenticated context travels
//!   inside the ciphertext (two length bytes, then about 250 bytes of JSON, then the media), so a frame costs
//!   about 278 bytes more than its media.
//! - **compact** (`bolt-sframe-v2`): an RFC 9605 header whose KID field is the format marker [`COMPACT_KID`],
//!   then the ciphertext and the full 16-byte AES-GCM tag. The context is AEAD associated data in the order RFC
//!   9605 section 4.4.3 gives (header, then metadata) and is never transmitted: both ends rebuild it from what
//!   they already know (call, epoch, roster binding, sender, stream, sequence, timestamp). A frame costs at most
//!   20 bytes (19 while the counter fits two bytes).
//!
//! In both formats the key is derived from the sender's real 64-bit KID, so the short wire marker names the
//! format, not the key. The receiver picks the key by the sender the relay stamped on the stream, as it always
//! did, and the tag decides whether that was right.
use sframe::{
    CipherSuite,
    crypto::{DecryptionBufferView, EncryptionBufferView},
    frame::{EncryptedFrameView, FrameCounter, MediaFrameView, MonotonicCounter,
        validation::{FrameValidation, ReplayAttackProtection, Tolerance, UnvalidatedFrame}},
    header::SframeHeader,
    key::{DecryptionKey, EncryptionKey},
};
use wasm_bindgen::prelude::*;
use zeroize::{Zeroize, Zeroizing};

const MAX_AUDIO_BYTES: usize = 4096;
const MAX_AAD_BYTES: usize = 1024;
const SUITE: CipherSuite = CipherSuite::AesGcm256Sha512;
/// The full AES-GCM tag. RFC 9605 shortens tags only in its AES-CTR + HMAC suites; a GCM tag is never cut.
const TAG_BYTES: usize = SUITE.auth_tag_len();
/// Header KID of a compact frame. Below 8, so the RFC header carries it in the config byte (no KID bytes).
pub const COMPACT_KID: u64 = 1;
/// Sender KIDs start here, so a legacy frame (which carries its real KID) can never look like a compact one.
pub const MIN_SENDER_KID: u64 = 8;
/// How far behind the newest counter a frame may arrive and still be accepted (once). One sender's audio
/// and video share a counter, a keyframe on a datagram path is up to 256 fragments, and the pacers on both
/// ends let audio overtake queued video, so a picture's last fragments can arrive several hundred counters
/// after newer audio. Duplicates are still rejected anywhere in the window; older frames are rejected.
const REPLAY_WINDOW: usize = 1024;

fn check_input(payload: &[u8], aad: &[u8], encrypted: bool) -> Result<(), String> {
    if payload.is_empty() || payload.len() > MAX_AUDIO_BYTES + if encrypted { MAX_AAD_BYTES + 35 } else { 0 }
        || aad.is_empty() || aad.len() > MAX_AAD_BYTES {
        return Err("Invalid SFrame input size".into());
    }
    Ok(())
}
fn check_kid(kid: u64) -> Result<(), String> {
    if kid < MIN_SENDER_KID { return Err("SFrame sender key ID is reserved".into()); }
    Ok(())
}
fn js_error(error: impl std::fmt::Display) -> JsValue { JsValue::from_str(&error.to_string()) }

#[wasm_bindgen]
pub struct SFrameSender {
    key: EncryptionKey,
    counter: MonotonicCounter,
}

#[wasm_bindgen]
impl SFrameSender {
    #[wasm_bindgen(constructor)]
    pub fn new(kid: u64, mut base_key: Vec<u8>) -> Result<SFrameSender, JsValue> {
        let result = Self::create(kid, &base_key);
        base_key.zeroize();
        result.map_err(js_error)
    }
    /// `compact` picks the frame format. Both draw on this sender's one counter, so no nonce repeats under its key.
    pub fn encrypt(&mut self, payload: &[u8], aad: &[u8], compact: bool) -> Result<Vec<u8>, JsValue> {
        self.encrypt_frame(payload, aad, compact).map_err(js_error)
    }
}

impl SFrameSender {
    fn create(kid: u64, base_key: &[u8]) -> Result<Self, String> {
        if base_key.len() != 32 { return Err("SFrame base key must be 32 bytes".into()); }
        check_kid(kid)?;
        Ok(Self {
            key: EncryptionKey::derive_from(SUITE, kid, base_key).map_err(|e| e.to_string())?,
            counter: MonotonicCounter::default(),
        })
    }
    fn encrypt_frame(&mut self, payload: &[u8], aad: &[u8], compact: bool) -> Result<Vec<u8>, String> {
        check_input(payload, aad, false)?;
        if compact { self.encrypt_compact(payload, aad) } else { self.encrypt_legacy(payload, aad) }
    }
    fn encrypt_legacy(&mut self, payload: &[u8], aad: &[u8]) -> Result<Vec<u8>, String> {
        // Use empty RFC metadata: upstream 2.0.0 reverses nonempty AAD ordering.
        // Context is ordinary application data inside the authenticated ciphertext.
        let mut envelope = Zeroizing::new(Vec::with_capacity(2 + aad.len() + payload.len()));
        envelope.extend_from_slice(&(aad.len() as u16).to_be_bytes());
        envelope.extend_from_slice(aad);
        envelope.extend_from_slice(payload);
        let frame = MediaFrameView::try_new(&mut self.counter, envelope.as_slice())
            .map_err(|e| e.to_string())?;
        let encrypted = frame.encrypt(&self.key).map_err(|e| e.to_string())?;
        Ok(encrypted.as_ref().to_vec())
    }
    /// header || ciphertext || tag, sealed with AAD = header || context (the RFC 9605 section 4.4.3 order), using
    /// the key's AEAD directly: the upstream frame API puts nonempty metadata before the header.
    fn encrypt_compact(&mut self, payload: &[u8], context: &[u8]) -> Result<Vec<u8>, String> {
        let counter = self.counter.try_next().map_err(|e| e.to_string())?;
        let header = SframeHeader::new(COMPACT_KID, counter);
        let header_len = header.len();
        // The plaintext is copied in and sealed in place; the buffer is wiped if anything fails on the way.
        let mut frame = Zeroizing::new(vec![0u8; header_len + payload.len() + TAG_BYTES]);
        header.serialize(&mut frame[..header_len]).map_err(|e| e.to_string())?;
        let mut aad = Vec::with_capacity(header_len + context.len());
        aad.extend_from_slice(&frame[..header_len]);
        aad.extend_from_slice(context);
        let (data, tag) = frame[header_len..].split_at_mut(payload.len());
        data.copy_from_slice(payload);
        self.key.encrypt(EncryptionBufferView { aad: &mut aad, data, tag }, counter).map_err(|e| e.to_string())?;
        Ok(frame.to_vec())
    }
}

#[wasm_bindgen]
pub struct SFrameReceiver {
    kid: u64,
    key: DecryptionKey,
    replay: ReplayAttackProtection,
}

#[wasm_bindgen]
impl SFrameReceiver {
    #[wasm_bindgen(constructor)]
    pub fn new(kid: u64, mut base_key: Vec<u8>) -> Result<SFrameReceiver, JsValue> {
        let result = Self::create(kid, &base_key);
        base_key.zeroize();
        result.map_err(js_error)
    }
    /// Either format: the header says which, and the tag decides whether it told the truth.
    pub fn decrypt(&mut self, payload: &[u8], aad: &[u8]) -> Result<Vec<u8>, JsValue> {
        self.decrypt_frame(payload, aad).map_err(js_error)
    }
}

impl SFrameReceiver {
    fn create(kid: u64, base_key: &[u8]) -> Result<Self, String> {
        if base_key.len() != 32 { return Err("SFrame base key must be 32 bytes".into()); }
        check_kid(kid)?;
        Ok(Self {
            kid,
            key: DecryptionKey::derive_from(SUITE, kid, base_key).map_err(|e| e.to_string())?,
            replay: ReplayAttackProtection::new(kid, Tolerance::new(REPLAY_WINDOW)),
        })
    }
    fn decrypt_frame(&mut self, payload: &[u8], aad: &[u8]) -> Result<Vec<u8>, String> {
        check_input(payload, aad, true)?;
        let header = SframeHeader::deserialize(payload).map_err(|e| e.to_string())?;
        if header.key_id() == COMPACT_KID { self.decrypt_compact(payload, &header, aad) } else { self.decrypt_legacy(payload, aad) }
    }
    fn decrypt_legacy(&mut self, payload: &[u8], aad: &[u8]) -> Result<Vec<u8>, String> {
        let frame = EncryptedFrameView::try_new(payload)
            .map_err(|e| e.to_string())?;
        let token = self.replay.screen(UnvalidatedFrame::new(frame.header(), &[]))
            .map_err(|e| e.to_string())?;
        let mut buffer = Zeroizing::new(Vec::new());
        let decrypted = frame.decrypt_into(&self.key, &mut *buffer).map_err(|e| e.to_string())?;
        let data = decrypted.payload();
        if data.len() < 2 { return Err("Invalid encrypted audio context".into()); }
        let context_len = u16::from_be_bytes([data[0], data[1]]) as usize;
        if context_len != aad.len() || data.len() <= 2 + context_len
            || data.len() > 2 + context_len + MAX_AUDIO_BYTES
            || &data[2..2 + context_len] != aad {
            return Err("Encrypted audio context mismatch".into());
        }
        let audio = data[2 + context_len..].to_vec();
        // Wrong-route authentic frames must not consume the valid route's replay window.
        self.replay.record(token);
        Ok(audio)
    }
    fn decrypt_compact(&mut self, payload: &[u8], header: &SframeHeader, context: &[u8]) -> Result<Vec<u8>, String> {
        let header_len = header.len();
        let body = &payload[header_len..];
        if body.len() <= TAG_BYTES || body.len() > MAX_AUDIO_BYTES + TAG_BYTES {
            return Err("Invalid compact SFrame size".into());
        }
        // One window per sender key for both formats: screened under the real KID, on the counter they share.
        let screened = SframeHeader::new(self.kid, header.counter());
        let token = self.replay.screen(UnvalidatedFrame::new(&screened, &[])).map_err(|e| e.to_string())?;
        // The header exactly as received is authenticated, so a re-encoded (non-minimal) header fails the tag.
        let mut aad = Vec::with_capacity(header_len + context.len());
        aad.extend_from_slice(&payload[..header_len]);
        aad.extend_from_slice(context);
        let mut data = Zeroizing::new(body.to_vec());
        self.key.decrypt(DecryptionBufferView { aad: &mut aad, data: &mut data }, header.counter())
            .map_err(|e| e.to_string())?;
        let media = data[..body.len() - TAG_BYTES].to_vec();
        // Only an authentic frame consumes its replay slot.
        self.replay.record(token);
        Ok(media)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    const AAD: &[u8] = b"call/epoch/sender/stream/sequence/timestamp";
    fn hex(value: &str) -> Vec<u8> {
        value.as_bytes().chunks_exact(2).map(|pair|
            u8::from_str_radix(std::str::from_utf8(pair).unwrap(), 16).unwrap()).collect()
    }
    #[cfg_attr(target_arch = "wasm32", wasm_bindgen_test::wasm_bindgen_test)]
    #[cfg_attr(not(target_arch = "wasm32"), test)]
    fn rfc9605_appendix_c3_aes256_vector_and_upstream_metadata_regression() {
        use sframe::crypto::EncryptionBufferView;
        use sframe::header::SframeHeader;
        let base = hex("000102030405060708090a0b0c0d0e0f");
        let key = EncryptionKey::derive_from(CipherSuite::AesGcm256Sha512, 0x123u64, &base).unwrap();
        let header = Vec::from(&SframeHeader::new(0x123, 0x4567));
        let metadata = hex("4945544620534672616d65205747");
        let plaintext = hex("64726166742d696574662d736672616d652d656e63");
        let expected = hex("990123456794f509d36e9beacb0e261d99c7d1e972f1fed787d4049f17ca21353c1cc24d56ceabced279");
        let mut aad = [header.clone(), metadata.clone()].concat();
        let mut data = plaintext.clone();
        let mut tag = [0;16];
        key.encrypt(EncryptionBufferView { aad: &mut aad, data: &mut data, tag: &mut tag }, 0x4567).unwrap();
        assert_eq!([header, data, tag.to_vec()].concat(), expected);
        // This documents why the adapter MUST use empty metadata, not this API.
        let dec = DecryptionKey::derive_from(CipherSuite::AesGcm256Sha512, 0x123u64, &base).unwrap();
        assert!(EncryptedFrameView::try_with_meta_data(&expected, &metadata).unwrap().decrypt(&dec).is_err());
    }
    #[cfg_attr(target_arch = "wasm32", wasm_bindgen_test::wasm_bindgen_test)]
    #[cfg_attr(not(target_arch = "wasm32"), test)]
    fn authenticated_roundtrip_rejects_duplicate() {
        for compact in [false, true] {
            let mut tx = SFrameSender::create(42, &[7; 32]).unwrap();
            let mut rx = SFrameReceiver::create(42, &[7; 32]).unwrap();
            let packet = tx.encrypt_frame(b"opus", AAD, compact).unwrap();
            assert_eq!(rx.decrypt_frame(&packet, AAD).unwrap(), b"opus");
            assert!(rx.decrypt_frame(&packet, AAD).is_err());
        }
    }
    #[cfg_attr(target_arch = "wasm32", wasm_bindgen_test::wasm_bindgen_test)]
    #[cfg_attr(not(target_arch = "wasm32"), test)]
    fn tamper_does_not_poison_replay_window() {
        for compact in [false, true] {
            let mut tx = SFrameSender::create(42, &[7; 32]).unwrap();
            let mut rx = SFrameReceiver::create(42, &[7; 32]).unwrap();
            let good = tx.encrypt_frame(b"opus", AAD, compact).unwrap();
            let mut corrupt = good.clone();
            *corrupt.last_mut().unwrap() ^= 1;
            assert!(rx.decrypt_frame(&corrupt, AAD).is_err());
            assert_eq!(rx.decrypt_frame(&good, AAD).unwrap(), b"opus");
        }
    }
    #[cfg_attr(target_arch = "wasm32", wasm_bindgen_test::wasm_bindgen_test)]
    #[cfg_attr(not(target_arch = "wasm32"), test)]
    fn context_key_and_sender_are_authenticated() {
        for compact in [false, true] {
            let mut tx = SFrameSender::create(42, &[7; 32]).unwrap();
            let packet = tx.encrypt_frame(b"opus", AAD, compact).unwrap();
            assert!(SFrameReceiver::create(43, &[7;32]).unwrap().decrypt_frame(&packet, AAD).is_err());
            assert!(SFrameReceiver::create(42, &[8;32]).unwrap().decrypt_frame(&packet, AAD).is_err());
            let mut rx = SFrameReceiver::create(42, &[7;32]).unwrap();
            assert!(rx.decrypt_frame(&packet, b"another call").is_err());
            assert!(rx.decrypt_frame(&packet, AAD).is_ok());
        }
    }
    #[cfg_attr(target_arch = "wasm32", wasm_bindgen_test::wasm_bindgen_test)]
    #[cfg_attr(not(target_arch = "wasm32"), test)]
    fn reordering_is_bounded() {
        for compact in [false, true] {
            let mut tx = SFrameSender::create(9, &[7;32]).unwrap();
            let mut rx = SFrameReceiver::create(9, &[7;32]).unwrap();
            let frames: Vec<_> = (0..REPLAY_WINDOW + 2).map(|_| tx.encrypt_frame(b"opus", AAD, compact).unwrap()).collect();
            assert!(rx.decrypt_frame(&frames[REPLAY_WINDOW + 1], AAD).is_ok());
            assert!(rx.decrypt_frame(&frames[REPLAY_WINDOW], AAD).is_ok());
            assert!(rx.decrypt_frame(&frames[0], AAD).is_err());
        }
    }
    #[cfg_attr(target_arch = "wasm32", wasm_bindgen_test::wasm_bindgen_test)]
    #[cfg_attr(not(target_arch = "wasm32"), test)]
    fn a_keyframe_overtaken_by_audio_still_decrypts_and_duplicates_do_not() {
        // A 256-fragment keyframe is encrypted, then 40 audio frames overtake its tail on the wire.
        for compact in [false, true] {
            let mut tx = SFrameSender::create(9, &[7;32]).unwrap();
            let mut rx = SFrameReceiver::create(9, &[7;32]).unwrap();
            let keyframe: Vec<_> = (0..256).map(|_| tx.encrypt_frame(b"fragment", AAD, compact).unwrap()).collect();
            let audio: Vec<_> = (0..40).map(|_| tx.encrypt_frame(b"opus", AAD, compact).unwrap()).collect();
            assert!(rx.decrypt_frame(&keyframe[0], AAD).is_ok());
            for frame in &audio { assert!(rx.decrypt_frame(frame, AAD).is_ok()); }
            for frame in keyframe.iter().skip(1).rev() { assert!(rx.decrypt_frame(frame, AAD).is_ok()); }
            assert!(rx.decrypt_frame(&keyframe[100], AAD).is_err(), "a duplicate inside the window is still a replay");
            assert!(rx.decrypt_frame(&audio[39], AAD).is_err());
        }
    }
    #[cfg_attr(target_arch = "wasm32", wasm_bindgen_test::wasm_bindgen_test)]
    #[cfg_attr(not(target_arch = "wasm32"), test)]
    fn counter_exhaustion_fails_without_wrapping() {
        for compact in [false, true] {
            let mut tx = SFrameSender::create(9, &[7;32]).unwrap();
            tx.counter = MonotonicCounter::with_start_value(u64::MAX, u64::MAX);
            assert!(tx.encrypt_frame(b"opus", AAD, compact).is_ok());
            assert!(tx.encrypt_frame(b"opus", AAD, compact).is_err());
            assert!(tx.encrypt_frame(b"opus", AAD, !compact).is_err(), "the formats share one counter");
        }
    }
    #[cfg_attr(target_arch = "wasm32", wasm_bindgen_test::wasm_bindgen_test)]
    #[cfg_attr(not(target_arch = "wasm32"), test)]
    fn input_size_is_bounded() {
        for compact in [false, true] {
            let mut tx = SFrameSender::create(9, &[7;32]).unwrap();
            assert!(tx.encrypt_frame(&[0;4097], AAD, compact).is_err());
            assert!(tx.encrypt_frame(b"opus", &[0;1025], compact).is_err());
        }
    }

    // ── Compact framing ──

    #[cfg_attr(target_arch = "wasm32", wasm_bindgen_test::wasm_bindgen_test)]
    #[cfg_attr(not(target_arch = "wasm32"), test)]
    fn compact_frames_cost_at_most_twenty_bytes_where_legacy_ones_cost_hundreds() {
        // The real context: ["bolt-sframe-v1", call, epoch, roster hash, sender, stream, sequence, timestamp].
        let context = br#"["bolt-sframe-v2","0f8fad5b-d9cb-469f-a165-70867728950e","12","e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855","yap-media-0f8fad5bd9cb469fa16570867728950e-7c9e6679742540de944be07fc1f90ae7","16fd2706-8baf-433b-82eb-8c7fada847da",123456,4294967295]"#;
        let opus = [0x5au8; 80];
        let mut tx = SFrameSender::create(u64::MAX - 7, &[7; 32]).unwrap();
        let legacy = tx.encrypt_frame(&opus, context, false).unwrap();
        assert!(legacy.len() - opus.len() > 250, "legacy overhead {}", legacy.len() - opus.len());
        let mut worst = 0;
        // Counters up to 2^16 (about 20 minutes of audio and 240p video): a 1-3 byte header plus the 16-byte tag.
        for _ in 0..70_000 {
            let frame = tx.encrypt_frame(&opus, context, true).unwrap();
            worst = worst.max(frame.len() - opus.len());
        }
        assert!(worst <= 20, "compact overhead {worst}");
        assert_eq!(worst, 1 + 3 + TAG_BYTES, "the counter has grown past two bytes");
    }
    #[cfg_attr(target_arch = "wasm32", wasm_bindgen_test::wasm_bindgen_test)]
    #[cfg_attr(not(target_arch = "wasm32"), test)]
    fn compact_header_is_rfc9605_with_the_format_marker_as_kid() {
        let mut tx = SFrameSender::create(42, &[7; 32]).unwrap();
        let first = tx.encrypt_frame(b"opus", AAD, true).unwrap();
        // Config byte: X=0, KID=1, Y=0, CTR=0, then ciphertext and tag; no KID or counter bytes at all.
        assert_eq!(first[0], 0x10);
        assert_eq!(first.len(), 1 + 4 + TAG_BYTES);
        let header = SframeHeader::deserialize(&first).unwrap();
        assert_eq!((header.key_id(), header.counter()), (COMPACT_KID, 0));
        // The context never travels: the frame is the same size whatever the context says.
        let mut other = SFrameSender::create(42, &[7; 32]).unwrap();
        assert_eq!(other.encrypt_frame(b"opus", &[b'x'; 1000], true).unwrap().len(), first.len());
    }
    #[cfg_attr(target_arch = "wasm32", wasm_bindgen_test::wasm_bindgen_test)]
    #[cfg_attr(not(target_arch = "wasm32"), test)]
    fn compact_header_edits_fail_authentication() {
        let mut tx = SFrameSender::create(42, &[7; 32]).unwrap();
        for _ in 0..300 { tx.encrypt_frame(b"x", AAD, true).unwrap(); }
        let frame = tx.encrypt_frame(b"opus", AAD, true).unwrap();
        let mut rx = SFrameReceiver::create(42, &[7; 32]).unwrap();
        // Another counter (a nonce the sender never used with this tag).
        let mut counter = frame.clone();
        counter[2] ^= 1;
        assert!(rx.decrypt_frame(&counter, AAD).is_err());
        // The same KID and counter re-encoded non-minimally: X=1, KLEN=0 then KID byte 1.
        let mut stretched = vec![0x80 | (frame[0] & 0x0f), 1];
        stretched.extend_from_slice(&frame[1..]);
        assert_eq!(SframeHeader::deserialize(&stretched).unwrap().key_id(), COMPACT_KID);
        assert!(rx.decrypt_frame(&stretched, AAD).is_err());
        // Truncated tag, or nothing but a header.
        assert!(rx.decrypt_frame(&frame[..frame.len() - 1], AAD).is_err());
        assert!(rx.decrypt_frame(&frame[..3 + TAG_BYTES], AAD).is_err());
        // None of that consumed the frame's replay slot.
        assert_eq!(rx.decrypt_frame(&frame, AAD).unwrap(), b"opus");
    }
    #[cfg_attr(target_arch = "wasm32", wasm_bindgen_test::wasm_bindgen_test)]
    #[cfg_attr(not(target_arch = "wasm32"), test)]
    fn both_formats_share_one_counter_and_one_replay_window() {
        let mut tx = SFrameSender::create(42, &[7; 32]).unwrap();
        let mut rx = SFrameReceiver::create(42, &[7; 32]).unwrap();
        let frames: Vec<_> = (0..64).map(|i| tx.encrypt_frame(b"opus", AAD, i % 3 == 0).unwrap()).collect();
        let counters: std::collections::HashSet<_> = frames.iter()
            .map(|frame| SframeHeader::deserialize(frame).unwrap().counter()).collect();
        assert_eq!(counters.len(), frames.len(), "no counter, so no nonce, is used twice under one key");
        for frame in frames.iter().rev() { assert_eq!(rx.decrypt_frame(frame, AAD).unwrap(), b"opus"); }
        for frame in &frames { assert!(rx.decrypt_frame(frame, AAD).is_err()); }
    }
    #[cfg_attr(target_arch = "wasm32", wasm_bindgen_test::wasm_bindgen_test)]
    #[cfg_attr(not(target_arch = "wasm32"), test)]
    fn an_old_receiver_refuses_a_compact_frame_instead_of_misreading_it() {
        // Mixed-version calls only send compact frames to receivers that announced them; this is the backstop.
        let mut tx = SFrameSender::create(42, &[7; 32]).unwrap();
        let compact = tx.encrypt_frame(b"opus", AAD, true).unwrap();
        let mut old = SFrameReceiver::create(42, &[7; 32]).unwrap();
        assert!(old.decrypt_legacy(&compact, AAD).is_err());
        assert_eq!(old.decrypt_frame(&compact, AAD).unwrap(), b"opus");
    }
    #[cfg_attr(target_arch = "wasm32", wasm_bindgen_test::wasm_bindgen_test)]
    #[cfg_attr(not(target_arch = "wasm32"), test)]
    fn sender_kids_below_eight_are_reserved_for_the_format_marker() {
        for kid in 0..MIN_SENDER_KID {
            assert!(SFrameSender::create(kid, &[7; 32]).is_err());
            assert!(SFrameReceiver::create(kid, &[7; 32]).is_err());
        }
        assert!(SFrameSender::create(MIN_SENDER_KID, &[7; 32]).is_ok());
    }
}
