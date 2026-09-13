//! Thin binding to RFC 9605, not a key exchange or a new encryption protocol.
use sframe::{
    CipherSuite,
    frame::{EncryptedFrameView, MediaFrameView, MonotonicCounter,
        validation::{FrameValidation, ReplayAttackProtection, Tolerance, UnvalidatedFrame}},
    key::{DecryptionKey, EncryptionKey},
};
use wasm_bindgen::prelude::*;
use zeroize::{Zeroize, Zeroizing};

const MAX_AUDIO_BYTES: usize = 4096;
const MAX_AAD_BYTES: usize = 1024;

fn check_input(payload: &[u8], aad: &[u8], encrypted: bool) -> Result<(), String> {
    if payload.is_empty() || payload.len() > MAX_AUDIO_BYTES + if encrypted { MAX_AAD_BYTES + 35 } else { 0 }
        || aad.is_empty() || aad.len() > MAX_AAD_BYTES {
        return Err("Invalid SFrame input size".into());
    }
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
    pub fn encrypt(&mut self, payload: &[u8], aad: &[u8]) -> Result<Vec<u8>, JsValue> {
        self.encrypt_frame(payload, aad).map_err(js_error)
    }
}

impl SFrameSender {
    fn create(kid: u64, base_key: &[u8]) -> Result<Self, String> {
        if base_key.len() != 32 { return Err("SFrame base key must be 32 bytes".into()); }
        Ok(Self {
            key: EncryptionKey::derive_from(CipherSuite::AesGcm256Sha512, kid, base_key)
                .map_err(|e| e.to_string())?,
            counter: MonotonicCounter::default(),
        })
    }
    fn encrypt_frame(&mut self, payload: &[u8], aad: &[u8]) -> Result<Vec<u8>, String> {
        check_input(payload, aad, false)?;
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
}

#[wasm_bindgen]
pub struct SFrameReceiver {
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
    pub fn decrypt(&mut self, payload: &[u8], aad: &[u8]) -> Result<Vec<u8>, JsValue> {
        self.decrypt_frame(payload, aad).map_err(js_error)
    }
}

impl SFrameReceiver {
    fn create(kid: u64, base_key: &[u8]) -> Result<Self, String> {
        if base_key.len() != 32 { return Err("SFrame base key must be 32 bytes".into()); }
        Ok(Self {
            key: DecryptionKey::derive_from(CipherSuite::AesGcm256Sha512, kid, base_key)
                .map_err(|e| e.to_string())?,
            replay: ReplayAttackProtection::new(kid, Tolerance::new(128)),
        })
    }
    fn decrypt_frame(&mut self, payload: &[u8], aad: &[u8]) -> Result<Vec<u8>, String> {
        check_input(payload, aad, true)?;
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
        let mut tx = SFrameSender::create(42, &[7; 32]).unwrap();
        let mut rx = SFrameReceiver::create(42, &[7; 32]).unwrap();
        let packet = tx.encrypt_frame(b"opus", AAD).unwrap();
        assert_eq!(rx.decrypt_frame(&packet, AAD).unwrap(), b"opus");
        assert!(rx.decrypt_frame(&packet, AAD).is_err());
    }
    #[cfg_attr(target_arch = "wasm32", wasm_bindgen_test::wasm_bindgen_test)]
    #[cfg_attr(not(target_arch = "wasm32"), test)]
    fn tamper_does_not_poison_replay_window() {
        let mut tx = SFrameSender::create(42, &[7; 32]).unwrap();
        let mut rx = SFrameReceiver::create(42, &[7; 32]).unwrap();
        let good = tx.encrypt_frame(b"opus", AAD).unwrap();
        let mut corrupt = good.clone();
        *corrupt.last_mut().unwrap() ^= 1;
        assert!(rx.decrypt_frame(&corrupt, AAD).is_err());
        assert_eq!(rx.decrypt_frame(&good, AAD).unwrap(), b"opus");
    }
    #[cfg_attr(target_arch = "wasm32", wasm_bindgen_test::wasm_bindgen_test)]
    #[cfg_attr(not(target_arch = "wasm32"), test)]
    fn context_key_and_sender_are_authenticated() {
        let mut tx = SFrameSender::create(42, &[7; 32]).unwrap();
        let packet = tx.encrypt_frame(b"opus", AAD).unwrap();
        assert!(SFrameReceiver::create(43, &[7;32]).unwrap().decrypt_frame(&packet, AAD).is_err());
        assert!(SFrameReceiver::create(42, &[8;32]).unwrap().decrypt_frame(&packet, AAD).is_err());
        let mut rx = SFrameReceiver::create(42, &[7;32]).unwrap();
        assert!(rx.decrypt_frame(&packet, b"another call").is_err());
        assert!(rx.decrypt_frame(&packet, AAD).is_ok());
    }
    #[cfg_attr(target_arch = "wasm32", wasm_bindgen_test::wasm_bindgen_test)]
    #[cfg_attr(not(target_arch = "wasm32"), test)]
    fn reordering_is_bounded() {
        let mut tx = SFrameSender::create(1, &[7;32]).unwrap();
        let mut rx = SFrameReceiver::create(1, &[7;32]).unwrap();
        let frames: Vec<_> = (0..130).map(|_| tx.encrypt_frame(b"opus", AAD).unwrap()).collect();
        assert!(rx.decrypt_frame(&frames[129], AAD).is_ok());
        assert!(rx.decrypt_frame(&frames[128], AAD).is_ok());
        assert!(rx.decrypt_frame(&frames[0], AAD).is_err());
    }
    #[cfg_attr(target_arch = "wasm32", wasm_bindgen_test::wasm_bindgen_test)]
    #[cfg_attr(not(target_arch = "wasm32"), test)]
    fn counter_exhaustion_fails_without_wrapping() {
        let mut tx = SFrameSender::create(1, &[7;32]).unwrap();
        tx.counter = MonotonicCounter::with_start_value(u64::MAX, u64::MAX);
        assert!(tx.encrypt_frame(b"opus", AAD).is_ok());
        assert!(tx.encrypt_frame(b"opus", AAD).is_err());
    }
    #[cfg_attr(target_arch = "wasm32", wasm_bindgen_test::wasm_bindgen_test)]
    #[cfg_attr(not(target_arch = "wasm32"), test)]
    fn input_size_is_bounded() {
        let mut tx = SFrameSender::create(1, &[7;32]).unwrap();
        assert!(tx.encrypt_frame(&[0;4097], AAD).is_err());
        assert!(tx.encrypt_frame(b"opus", &[0;1025]).is_err());
    }
}
