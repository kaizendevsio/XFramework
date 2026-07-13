#!/usr/bin/env bash
set -euo pipefail

runner="${1:?pass run-bolt-phase0-synthetics.sh as the first argument}"
env_parser="${2:?pass verify-bolt-phase0-env.py as the second argument}"
scenario="${3:-pass}"
stage="${4:-canary}"
proxy_mode="${5:-direct-kestrel}"
case "$scenario" in
  pass|refresh-output|application-token-leak|application-marker-leak|tampered-report|hook-output|token-changed|missing-receipt) ;;
  *) echo "unknown harness scenario" >&2; exit 2 ;;
esac
case "$proxy_mode" in
  logs|direct-kestrel) ;;
  *) echo "unknown harness proxy mode" >&2; exit 2 ;;
esac
root="$(mktemp -d /tmp/bolt-phase0-synthetic-harness.XXXXXXXX)"
cleanup() { rm -rf -- "$root"; }
trap cleanup EXIT
mkdir -p "$root/bin" "$root/run"

env_file="$root/protected.env"
communications_token="$root/communications.jwt"
user_token="$root/user.jwt"
expiry_token="$root/expiry.jwt"
refresh_hook="$root/refresh"

cat >"$root/probe-template" <<'HOOK'
#!/usr/bin/env bash
set -euo pipefail
scenario="$(sed -n 's/^HARNESS_SCENARIO=//p' "$XFRAMEWORK_ENV_FILE")"
if [ "$BOLT_SYNTHETIC_PROBE_KIND" = "proxy-marker-scan" ] && [ "$scenario" = "missing-receipt" ]; then
  exit 0
fi
if [ "$BOLT_SYNTHETIC_PROBE_KIND" = "proxy-marker-scan" ] && [ "$scenario" = "hook-output" ]; then
  python3 - "$BOLT_SYNTHETIC_TOKEN_MANIFEST" <<'PY'
import json, pathlib, sys
manifest = json.loads(pathlib.Path(sys.argv[1]).read_text())
print(pathlib.Path(manifest["tokens"][0]["path"]).read_text())
PY
elif [ "$BOLT_SYNTHETIC_PROBE_KIND" = "proxy-marker-scan" ] && [ "$scenario" = "token-changed" ]; then
  python3 - "$BOLT_SYNTHETIC_TOKEN_MANIFEST" <<'PY'
import json, pathlib, sys
manifest = json.loads(pathlib.Path(sys.argv[1]).read_text())
path = pathlib.Path(manifest["tokens"][0]["path"])
path.write_text(path.read_text() + "x")
PY
fi
python3 - "$BOLT_SYNTHETIC_PROBE_KIND" "$BOLT_SYNTHETIC_PROBE_RECEIPT" "$BOLT_SYNTHETIC_TOKEN_MANIFEST" <<'PY'
import datetime as dt
import json
import os
import pathlib
import sys

kind, receipt_path, manifest_path = sys.argv[1:]
values = dict(
    line.split("=", 1)
    for line in pathlib.Path(os.environ["XFRAMEWORK_ENV_FILE"]).read_text().splitlines()
    if line.strip() and not line.lstrip().startswith("#")
)
proxy_assertions = (
    {
        "retainedStoreQueried": False,
        "notApplicableReason": "direct-kestrel-publication",
        "matches": 0,
    }
    if values.get("BOLT_SYNTHETIC_PROXY_MODE") == "direct-kestrel"
    else {"retainedStoreQueried": True, "matches": 0}
)
assertions = {
    "proxy-marker-scan": proxy_assertions,
    "seq-marker-scan": {"retainedStoreQueried": True, "matches": 0},
    "trace-marker-scan": {"retainedStoreQueried": True, "matches": 0},
    "plaintext-rejection": {"plaintextRejected": True, "bearerSent": False},
    "redis-interruption": {
        "interruptionInduced": True,
        "recovered": True,
        "postRecoverySyntheticPassed": True,
        "dataLossObserved": False,
    },
    "old-generation-rejection": {
        "oldUserTokenRejected": True,
        "oldServiceTokenRejected": True,
        "oldClientSecretRejected": True,
        "currentHttpHealthPassed": True,
        "currentBoltHealthPassed": True,
    },
}[kind]
if kind.endswith("marker-scan"):
    count = len(json.loads(pathlib.Path(manifest_path).read_text())["tokens"])
    assertions.update({"tokensSearched": count, "markersSearched": count})
now = dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z")
receipt = {
    "schemaVersion": "bolt-phase0-probe-receipt/v1",
    "probe": kind,
    "status": "passed",
    "startedAtUtc": now,
    "completedAtUtc": now,
    "assertions": assertions,
}
pathlib.Path(receipt_path).write_text(json.dumps(receipt))
os.chmod(receipt_path, 0o600)
PY
HOOK
for hook in proxy seq trace plaintext redis old-generation; do
  cp "$root/probe-template" "$root/$hook"
  chmod 700 "$root/$hook"
done

cat >"$refresh_hook" <<'HOOK'
#!/usr/bin/env bash
set -euo pipefail
python3 - "$XFRAMEWORK_ENV_FILE" "$BOLT_SYNTHETIC_REFRESH_RECEIPT" <<'PY'
import base64
import datetime as dt
import json
import os
import pathlib
import sys
import time
import uuid

env_path, receipt_path = sys.argv[1:]
values = dict(
    line.split("=", 1)
    for line in pathlib.Path(env_path).read_text().splitlines()
    if line.strip() and not line.lstrip().startswith("#"))
now = int(time.time())
issuer = "https://identity.example.test"

def token(expiration, subject):
    encode = lambda value: base64.urlsafe_b64encode(
        json.dumps(value, separators=(",", ":")).encode()).rstrip(b"=").decode()
    claims = {"iss": issuer, "sub": subject, "exp": expiration, "jti": uuid.uuid4().hex}
    return f"{encode({'alg': 'none', 'typ': 'JWT'})}.{encode(claims)}.signature"

expirations = {}
definitions = [
    ("BOLT_SYNTHETIC_COMMUNICATIONS_TOKEN_PATH", "communications", 900),
    ("BOLT_SYNTHETIC_USER_TOKEN_PATH", "user", 900),
]
if os.environ["BOLT_SYNTHETIC_EXPIRY_ENABLED"] == "true":
    definitions.append(("BOLT_SYNTHETIC_EXPIRY_TOKEN_PATH", "expiry", 120))
else:
    expiry_path = pathlib.Path(values["BOLT_SYNTHETIC_EXPIRY_TOKEN_PATH"])
    expiry_path.write_bytes(b"")
    os.chmod(expiry_path, 0o600)
for key, purpose, lifetime in definitions:
    path = pathlib.Path(values[key])
    expiration = now + lifetime
    path.write_text(token(expiration, purpose))
    os.chmod(path, 0o600)
    expirations[purpose] = dt.datetime.fromtimestamp(
        expiration, dt.timezone.utc).isoformat().replace("+00:00", "Z")

receipt = {
    "schemaVersion": "bolt-phase0-token-refresh/v1",
    "status": "passed",
    "issuerUri": issuer,
    "principalReference": "phase0-harness",
    "refreshedAtUtc": dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z"),
    "tokenExpirationsUtc": expirations,
}
pathlib.Path(receipt_path).write_text(json.dumps(receipt))
os.chmod(receipt_path, 0o600)
if values.get("HARNESS_SCENARIO") == "refresh-output":
    print(pathlib.Path(values["BOLT_SYNTHETIC_USER_TOKEN_PATH"]).read_text())
PY
HOOK
chmod 700 "$refresh_hook"

cat >"$root/bin/docker" <<'DOCKER'
#!/usr/bin/env bash
set -euo pipefail
if [ "$1" = "compose" ]; then
  shift
  for argument in "$@"; do
    if [ "$argument" = "run" ]; then
      python3 - "$XFRAMEWORK_ENV_FILE" <<'PY'
import datetime as dt
import hashlib
import json
import pathlib
import sys
import uuid

values = dict(
    line.split("=", 1)
    for line in pathlib.Path(sys.argv[1]).read_text().splitlines()
    if line.strip() and not line.lstrip().startswith("#"))
now = dt.datetime.now(dt.timezone.utc)
names = (
    "user_registration", "hostile_reserved_registration", "communications_registration",
    "identity_health_check", "transient_presence", "durable_offline_registration",
    "durable_offline_publish", "durable_ordered_replay", "durable_ack",
    "durable_no_redelivery", "durable_unregister",
)
if values["HARNESS_STAGE"] in {"canary", "finalized"}:
    names += ("token_expiry_disconnect",)
operations = []
for name in names:
    results = {"outcome": "passed"}
    if name == "durable_ack":
        results = {
            "cumulative_acknowledged": "true",
            "duplicate_ack_idempotent": "true",
            "out_of_order_ack_monotonic": "true",
        }
        if values.get("HARNESS_SCENARIO") == "tampered-report":
            results.pop("duplicate_ack_idempotent")
    operations.append({
        "name": name,
        "startedAtUtc": now.isoformat(),
        "completedAtUtc": now.isoformat(),
        "status": "passed",
        "timingMs": 0,
        "results": results,
    })
prefixes = {}
token_definitions = [
    ("communications", "BOLT_SYNTHETIC_COMMUNICATIONS_TOKEN_PATH"),
    ("user", "BOLT_SYNTHETIC_USER_TOKEN_PATH"),
]
if values["HARNESS_STAGE"] in {"canary", "finalized"}:
    token_definitions.append(("expiry", "BOLT_SYNTHETIC_EXPIRY_TOKEN_PATH"))
for purpose, key in token_definitions:
    prefixes[purpose] = hashlib.sha256(
        pathlib.Path(values[key]).read_bytes().strip()).hexdigest()[:12]
print(json.dumps({
    "schemaVersion": "bolt-phase0-synthetic-report/v1",
    "runId": str(uuid.uuid4()),
    "tokenSha256Prefixes": prefixes,
    "startedAtUtc": now.isoformat(),
    "completedAtUtc": now.isoformat(),
    "target": "wss://bolt-hub:8443/bolt/ws",
    "status": "passed",
    "timings": {"totalMs": 0},
    "operations": operations,
}, separators=(",", ":")))
PY
      exit 0
    fi
    if [ "$argument" = "ps" ]; then
      printf 'fake-container\n'
      exit 0
    fi
  done
fi
if [ "$1" = "logs" ]; then
  if grep -q '^HARNESS_SCENARIO=application-token-leak$' "$XFRAMEWORK_ENV_FILE"; then
    token_path="$(sed -n 's/^BOLT_SYNTHETIC_USER_TOKEN_PATH=//p' "$XFRAMEWORK_ENV_FILE")"
    cat "$token_path"
    exit 0
  fi
  if grep -q '^HARNESS_SCENARIO=application-marker-leak$' "$XFRAMEWORK_ENV_FILE"; then
    token_path="$(sed -n 's/^BOLT_SYNTHETIC_USER_TOKEN_PATH=//p' "$XFRAMEWORK_ENV_FILE")"
    python3 - "$token_path" <<'PY'
import base64, json, pathlib, sys
payload = pathlib.Path(sys.argv[1]).read_text().split(".")[1]
payload += "=" * (-len(payload) % 4)
print(json.loads(base64.urlsafe_b64decode(payload))["jti"])
PY
    exit 0
  fi
  printf 'safe retained log line\n'
  exit 0
fi
exit 1
DOCKER
chmod 700 "$root/bin/docker"

{
printf '# harness comment with CRLF acceptance\r\n'
cat <<ENV
BOLT_SYNTHETIC_TOKEN_REFRESH_COMMAND_PATH=$refresh_hook
BOLT_SYNTHETIC_PROXY_MODE=$proxy_mode
BOLT_SYNTHETIC_PROXY_MARKER_SCAN_COMMAND_PATH=$root/proxy
BOLT_SYNTHETIC_SEQ_MARKER_SCAN_COMMAND_PATH=$root/seq
BOLT_SYNTHETIC_TRACE_MARKER_SCAN_COMMAND_PATH=$root/trace
BOLT_SYNTHETIC_PLAINTEXT_REJECTION_COMMAND_PATH=$root/plaintext
BOLT_SYNTHETIC_REDIS_INTERRUPTION_COMMAND_PATH=$root/redis
BOLT_SYNTHETIC_OLD_GENERATION_REJECTION_COMMAND_PATH=$root/old-generation
BOLT_SYNTHETIC_COMMUNICATIONS_TOKEN_PATH=$communications_token
BOLT_SYNTHETIC_USER_TOKEN_PATH=$user_token
BOLT_SYNTHETIC_EXPIRY_TOKEN_PATH=$expiry_token
BOLT_SYNTHETIC_MIN_TOKEN_LIFETIME_SECONDS=300
HARNESS_SCENARIO=$scenario
HARNESS_STAGE=$stage
ENV
if [ "$proxy_mode" = "logs" ]; then
  printf 'BOLT_SYNTHETIC_PROXY_LOG_PATHS=%s\n' "$root/proxy.log"
fi
} >"$env_file"

remote_body="$root/remote-body.sh"
awk '
  index($0, "<<'"'"'REMOTE_SCRIPT'"'"'") { copying = 1; next }
  copying && /^REMOTE_SCRIPT$/ { exit }
  copying { print }
' "$runner" >"$remote_body"
if [ ! -s "$remote_body" ]; then
  echo "failed to extract remote synthetic runner" >&2
  exit 1
fi

export PATH="$root/bin:$PATH"
export XFRAMEWORK_ENV_FILE="$env_file"
export ENV_PARSER="$env_parser"
export REMOTE_RUN_DIR="$root/run"
export REMOTE_COMPOSE_FILE="$root/compose.yml"
export REMOTE_PIN_OVERRIDE="$root/pins.yml"
export REMOTE_LEASE_MANAGER="/usr/local/libexec/xframework-bolt-phase0/manage-bolt-phase0-deployment-lease.py"
export LEASE_RUN_ID="${GITHUB_RUN_ID:-$$}"
export LEASE_RUN_ATTEMPT="${GITHUB_RUN_ATTEMPT:-1}"
export LEASE_HEARTBEAT_SECONDS=10
export COMPOSE_PROJECT_NAME="phase0-harness"
export STAGE="$stage"
export REPORT="$root/run/synthetics-$stage.json"
snapshot_before="$(find /dev/shm -maxdepth 1 -type d -name "bolt-phase0-synthetics-${stage}.*" -printf '%p\n' | sort)"
if [ "$scenario" != "pass" ]; then
  printf '{"status":"passed","stale":true}\n' >"$REPORT"
  set +e
  failure_output="$(bash "$remote_body" 2>&1)"
  exit_code=$?
  set -e
  if [ "$exit_code" -eq 0 ] || [ -e "$REPORT" ]; then
    echo "negative harness scenario did not fail closed" >&2
    exit 1
  fi
  snapshot_after="$(find /dev/shm -maxdepth 1 -type d -name "bolt-phase0-synthetics-${stage}.*" -printf '%p\n' | sort)"
  if [ "$snapshot_after" != "$snapshot_before" ]; then
    echo "negative harness scenario retained a quarantined snapshot" >&2
    exit 1
  fi
  for token_path in "$communications_token" "$user_token" "$expiry_token"; do
    [ -f "$token_path" ] || continue
    token="$(cat "$token_path")"
    if [[ "$failure_output" == *"$token"* ]]; then
      echo "negative harness scenario exposed a token in wrapper output" >&2
      exit 1
    fi
  done
  printf 'mock Phase 0 synthetic evidence negative scenario passed: %s\n' "$scenario"
  exit 0
fi

bash "$remote_body"
snapshot_after="$(find /dev/shm -maxdepth 1 -type d -name "bolt-phase0-synthetics-${stage}.*" -printf '%p\n' | sort)"
if [ "$snapshot_after" != "$snapshot_before" ]; then
  echo "passing harness scenario retained a quarantined snapshot" >&2
  exit 1
fi
if [ "$stage" = "canary" ] || [ "$stage" = "finalized" ]; then
  [ ! -e "$expiry_token" ] || { echo "one-use expiry token was retained" >&2; exit 1; }
else
  [ -f "$expiry_token" ] && [ ! -s "$expiry_token" ] || {
    echo "disabled expiry token placeholder is invalid" >&2
    exit 1
  }
fi

python3 - "$REPORT" "$stage" "$proxy_mode" <<'PY'
import json
import pathlib
import sys

report = json.loads(pathlib.Path(sys.argv[1]).read_text())
stage, proxy_mode = sys.argv[2:]
assert report["schemaVersion"] == "bolt-phase0-synthetic-evidence/v1"
assert report["status"] == "passed"
marker_absence = report["postRunEvidence"]["markerAbsence"]
for source in ("application", "seq", "trace"):
    assert marker_absence[source] == "passed"
assert marker_absence["proxy"] == (
    "not_applicable" if proxy_mode == "direct-kestrel" else "passed"
)
assert set(marker_absence["markerSha256Prefixes"]) == (
    {"communications", "user", "expiry"}
    if stage in {"canary", "finalized"}
    else {"communications", "user"})
assert report["postRunEvidence"]["redisInterruptionRecovery"] == (
    "passed" if stage == "canary" else "not_required")
assert report["postRunEvidence"]["expiryDisconnect"] == (
    "passed" if stage in {"canary", "finalized"} else "not_required")
assert len(report["coreReportSha256"]) == 64
print("mock Phase 0 synthetic evidence run passed")
PY
