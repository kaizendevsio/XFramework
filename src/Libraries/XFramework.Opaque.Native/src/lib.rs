//! Serialization/FFI adapter only. All protocol operations belong to opaque-ke.
use base64::{engine::general_purpose::URL_SAFE_NO_PAD as B64, Engine};
use opaque_ke::{CipherSuite, CredentialFinalization, CredentialRequest, Identifiers,
    RegistrationRequest, ServerLogin, ServerLoginParameters, ServerRegistration, ServerSetup};
use opaque_ke::rand::rngs::OsRng;
use serde_json::{json, Value};
use std::ffi::{c_char, CStr, CString};
use zeroize::{Zeroize, Zeroizing};

struct Suite;
impl CipherSuite for Suite {
    type OprfCs = opaque_ke::Ristretto255;
    type KeyExchange = opaque_ke::TripleDh<opaque_ke::Ristretto255, sha2::Sha512>;
    type Ksf = argon2::Argon2<'static>;
}
type Outcome = Result<Value, ()>;
fn field<'a>(v: &'a Value, name: &str) -> Result<&'a str, ()> {
    v.get(name).and_then(Value::as_str).filter(|s| s.len() <= 16384).ok_or(())
}
fn bytes(v: &Value, name: &str) -> Result<Zeroizing<Vec<u8>>, ()> {
    B64.decode(field(v, name)?).map(Zeroizing::new).map_err(|_| ())
}
fn parameters(v: &Value) -> Result<ServerLoginParameters<'_, '_>, ()> {
    Ok(ServerLoginParameters { context: None, identifiers: Identifiers {
        client: Some(field(v, "client")?.as_bytes()), server: Some(field(v, "server")?.as_bytes()) } })
}
fn execute(v: &Value) -> Outcome {
    match field(v, "operation")? {
        "setup" => Ok(json!({"setup": B64.encode(ServerSetup::<Suite>::new(&mut OsRng).serialize())})),
        "validate-setup" => {
            ServerSetup::<Suite>::deserialize(&bytes(v, "setup")?).map_err(|_| ())?;
            Ok(json!({"valid": true}))
        },
        "register" => {
            let setup = ServerSetup::<Suite>::deserialize(&bytes(v, "setup")?).map_err(|_| ())?;
            let request = RegistrationRequest::<Suite>::deserialize(&bytes(v, "request")?).map_err(|_| ())?;
            let result = ServerRegistration::start(&setup, request, field(v, "client")?.as_bytes()).map_err(|_| ())?;
            Ok(json!({"response": B64.encode(result.message.serialize())}))
        },
        "validate" => {
            ServerRegistration::<Suite>::deserialize(&bytes(v, "record")?).map_err(|_| ())?;
            Ok(json!({"valid": true}))
        },
        "start" => {
            let setup = ServerSetup::<Suite>::deserialize(&bytes(v, "setup")?).map_err(|_| ())?;
            let record = if v.get("record").and_then(Value::as_str).is_some() {
                Some(ServerRegistration::<Suite>::deserialize(&bytes(v, "record")?).map_err(|_| ())?)
            } else { None };
            let request = CredentialRequest::<Suite>::deserialize(&bytes(v, "request")?).map_err(|_| ())?;
            let result = ServerLogin::start(&mut OsRng, &setup, record, request,
                field(v, "client")?.as_bytes(), parameters(v)?).map_err(|_| ())?;
            Ok(json!({"response": B64.encode(result.message.serialize()), "state": B64.encode(result.state.serialize())}))
        },
        "finish" => {
            let state = ServerLogin::<Suite>::deserialize(&bytes(v, "state")?).map_err(|_| ())?;
            let request = CredentialFinalization::<Suite>::deserialize(&bytes(v, "request")?).map_err(|_| ())?;
            // The application needs proof of completion, never the shared session key.
            state.finish(request, parameters(v)?).map_err(|_| ())?;
            Ok(json!({"valid": true}))
        },
        _ => Err(())
    }
}
fn erase(v: &mut Value) {
    match v {
        Value::String(s) => s.zeroize(),
        Value::Array(a) => a.iter_mut().for_each(erase),
        Value::Object(o) => o.values_mut().for_each(erase),
        _ => ()
    }
}

/// Input must be a valid UTF-8, NUL-terminated allocation owned by the caller.
/// The caller owns the result and must release it with xfw_opaque_free.
#[no_mangle]
pub unsafe extern "C" fn xfw_opaque_execute(input: *const c_char) -> *mut c_char {
    let outcome = std::panic::catch_unwind(|| {
        if input.is_null() { return Err(()); }
        let raw = CStr::from_ptr(input).to_bytes();
        if raw.len() > 65536 { return Err(()); }
        let mut value: Value = serde_json::from_slice(raw).map_err(|_| ())?;
        let result = execute(&value);
        erase(&mut value);
        result
    });
    let mut result = outcome.unwrap_or(Err(())).unwrap_or(json!({"error":"Invalid OPAQUE exchange"}));
    let output = CString::new(result.to_string()).expect("JSON contains no literal NUL");
    erase(&mut result);
    output.into_raw()
}

#[no_mangle]
pub unsafe extern "C" fn xfw_opaque_free(output: *mut c_char) {
    if !output.is_null() {
        let value = CString::from_raw(output);
        let mut bytes = value.into_bytes_with_nul();
        bytes.zeroize();
    }
}
