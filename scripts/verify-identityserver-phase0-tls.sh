#!/usr/bin/env bash
set -euo pipefail
umask 077

if [ "$#" -ne 7 ]; then
  echo "usage: $0 IDENTITYSERVER_TLS_FULLCHAIN_PATH IDENTITYSERVER_TLS_PRIVATE_KEY_PATH IDENTITYSERVER_TLS_CA_PATH IDENTITYSERVER_PUBLIC_HOSTNAME IDENTITYSERVER_PUBLIC_HTTPS_PORT IDENTITYSERVER_BOLT_TRANSPORT_TOKEN_PATH EVIDENCE_FILE" >&2
  exit 64
fi

fullchain_file="$1"
private_key_file="$2"
ca_file="$3"
published_hostname="$4"
published_port="$5"
token_path="$6"
evidence_file="$7"

fail() {
  echo "ERROR: $1" >&2
  exit 1
}

validate_absolute_path() {
  local name="$1"
  local value="$2"
  [[ "$value" =~ ^/[A-Za-z0-9_./+,:@%=-]+$ ]] || fail "$name must be a shell-safe absolute POSIX path"
  [[ "$value" != "/" && "$value" != *"//"* && "$value" != *"/./"* && "$value" != *"/../"* ]]
  [[ "$value" != */. && "$value" != */.. ]] || fail "$name must be canonical"
}

validate_hostname() {
  local value="$1"
  [[ ${#value} -le 253 && "$value" != *. ]] || fail "IDENTITYSERVER_PUBLIC_HOSTNAME must be canonical"
  [[ ! "$value" =~ ^[0-9a-fA-F:.]+$ ]] || fail "IDENTITYSERVER_PUBLIC_HOSTNAME must be a DNS hostname, not an IP address"
  local labels=()
  IFS='.' read -r -a labels <<< "$value"
  [[ ${#labels[@]} -gt 1 ]] || fail "IDENTITYSERVER_PUBLIC_HOSTNAME must be a fully qualified DNS hostname"
  local label
  for label in "${labels[@]}"; do
    [[ "$label" =~ ^[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?$ ]] \
      || fail "IDENTITYSERVER_PUBLIC_HOSTNAME contains an invalid DNS label"
  done
}

validate_absolute_path IDENTITYSERVER_TLS_FULLCHAIN_PATH "$fullchain_file"
validate_absolute_path IDENTITYSERVER_TLS_PRIVATE_KEY_PATH "$private_key_file"
validate_absolute_path IDENTITYSERVER_TLS_CA_PATH "$ca_file"
validate_absolute_path EVIDENCE_FILE "$evidence_file"
validate_hostname "$published_hostname"
[[ "$published_port" =~ ^[1-9][0-9]{0,4}$ ]] || fail "IDENTITYSERVER_PUBLIC_HTTPS_PORT must be a canonical decimal TCP port"
test "$published_port" -le 65535 || fail "IDENTITYSERVER_PUBLIC_HTTPS_PORT must be at most 65535"
test "$token_path" = "/api/service-identity/bolt-transport-token" \
  || fail "IDENTITYSERVER_BOLT_TRANSPORT_TOKEN_PATH is not the approved endpoint"

for file in "$fullchain_file" "$private_key_file" "$ca_file"; do
  test -f "$file"
  test -s "$file"
  test -r "$file"
done

fullchain_real="$(realpath -e "$fullchain_file")"
private_key_real="$(realpath -e "$private_key_file")"
ca_real="$(realpath -e "$ca_file")"
test "$fullchain_real" != "$private_key_real"
test "$fullchain_real" != "$ca_real"
test "$private_key_real" != "$ca_real"
if [[ "$(uname -s)" == Linux* ]]; then
  fullchain_identity="$(stat -c '%d:%i' "$fullchain_file")"
  private_key_identity="$(stat -c '%d:%i' "$private_key_file")"
  ca_identity="$(stat -c '%d:%i' "$ca_file")"
  test "$fullchain_identity" != "$private_key_identity"
  test "$fullchain_identity" != "$ca_identity"
  test "$private_key_identity" != "$ca_identity"
fi

key_mode="platform-managed"
if [[ "$(uname -s)" == Linux* ]]; then
  key_mode="$(stat -c '%a' "$private_key_file")"
  key_permissions="${key_mode: -3}"
  test $((8#$key_permissions & 077)) -eq 0
fi

openssl x509 -in "$fullchain_file" -noout >/dev/null
openssl pkey -in "$private_key_file" -noout >/dev/null
openssl crl2pkcs7 -nocrl -certfile "$ca_file" | openssl pkcs7 -print_certs -noout >/dev/null
openssl x509 -in "$fullchain_file" -noout -checkend 86400
openssl x509 -in "$fullchain_file" -noout -checkhost identityserver
openssl x509 -in "$fullchain_file" -noout -checkhost "$published_hostname"
openssl verify -x509_strict -purpose sslserver -verify_hostname identityserver \
  -CAfile "$ca_file" -untrusted "$fullchain_file" "$fullchain_file"
openssl verify -x509_strict -purpose sslserver -verify_hostname "$published_hostname" \
  -CAfile "$ca_file" -untrusted "$fullchain_file" "$fullchain_file"

tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT
openssl x509 -in "$fullchain_file" -pubkey -noout \
  | openssl pkey -pubin -outform DER > "$tmp_dir/cert-public-key.der"
openssl pkey -in "$private_key_file" -pubout -outform DER > "$tmp_dir/private-key-public-key.der"
cmp -s "$tmp_dir/cert-public-key.der" "$tmp_dir/private-key-public-key.der"

export PHASE0_IDENTITY_CERT_SUBJECT="$(openssl x509 -in "$fullchain_file" -noout -subject | sed 's/^subject=//')"
export PHASE0_IDENTITY_CERT_ISSUER="$(openssl x509 -in "$fullchain_file" -noout -issuer | sed 's/^issuer=//')"
export PHASE0_IDENTITY_CERT_SERIAL="$(openssl x509 -in "$fullchain_file" -noout -serial | sed 's/^serial=//')"
export PHASE0_IDENTITY_CERT_NOT_BEFORE="$(openssl x509 -in "$fullchain_file" -noout -startdate | sed 's/^notBefore=//')"
export PHASE0_IDENTITY_CERT_NOT_AFTER="$(openssl x509 -in "$fullchain_file" -noout -enddate | sed 's/^notAfter=//')"
export PHASE0_IDENTITY_CERT_SHA256="$(openssl x509 -in "$fullchain_file" -noout -fingerprint -sha256 | sed 's/^sha256 Fingerprint=//I')"
export PHASE0_IDENTITY_CERT_SAN="$(openssl x509 -in "$fullchain_file" -noout -ext subjectAltName | tail -n +2 | tr '\n' ' ' | xargs)"
export PHASE0_IDENTITY_PUBLISHED_PORT="$published_port"
export PHASE0_IDENTITY_PUBLISHED_HOSTNAME="$published_hostname"
export PHASE0_IDENTITY_TOKEN_PATH="$token_path"
export PHASE0_IDENTITY_KEY_MODE="$key_mode"

mkdir -p "$(dirname "$evidence_file")"
python3 - "$evidence_file" <<'PY'
import json
import os
import sys
from datetime import datetime, timezone
from pathlib import Path

evidence = {
    "schema": "xframework.bolt.phase0.identityserver-tls.v1",
    "generated_at_utc": datetime.now(timezone.utc).isoformat(),
    "status": "passed",
    "internal_hostname": "identityserver",
    "published_hostname": os.environ["PHASE0_IDENTITY_PUBLISHED_HOSTNAME"],
    "published_port": int(os.environ["PHASE0_IDENTITY_PUBLISHED_PORT"]),
    "token_path": os.environ["PHASE0_IDENTITY_TOKEN_PATH"],
    "certificate": {
        "subject": os.environ["PHASE0_IDENTITY_CERT_SUBJECT"],
        "issuer": os.environ["PHASE0_IDENTITY_CERT_ISSUER"],
        "serial": os.environ["PHASE0_IDENTITY_CERT_SERIAL"],
        "not_before": os.environ["PHASE0_IDENTITY_CERT_NOT_BEFORE"],
        "not_after": os.environ["PHASE0_IDENTITY_CERT_NOT_AFTER"],
        "sha256_fingerprint": os.environ["PHASE0_IDENTITY_CERT_SHA256"],
        "subject_alternative_name": os.environ["PHASE0_IDENTITY_CERT_SAN"],
        "chain_verified": True,
        "hostname_verified": True,
        "currently_valid": True,
    },
    "private_key": {
        "value": "<redacted>",
        "matches_certificate": True,
        "mode": os.environ["PHASE0_IDENTITY_KEY_MODE"],
    },
}
Path(sys.argv[1]).write_text(json.dumps(evidence, indent=2, sort_keys=True) + "\n", encoding="utf-8")
PY
chmod 600 "$evidence_file"

echo "IdentityServer Phase 0 TLS preflight passed; redacted evidence: $evidence_file"
