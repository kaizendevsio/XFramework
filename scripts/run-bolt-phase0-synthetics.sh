#!/usr/bin/env bash
set -euo pipefail

stage="${1:-}"
case "$stage" in
  ''|*[!a-zA-Z0-9-]*)
    echo "synthetic stage must contain only letters, numbers, and hyphens" >&2
    exit 2
    ;;
esac
if [ "${#stage}" -gt 37 ]; then
  echo "synthetic stage is too long for the deployment lease phase" >&2
  exit 2
fi

required=(
  DEPLOY_SSH_KEY
  DEPLOY_HOST
  XFRAMEWORK_ENV_FILE
  REMOTE_ENV_PARSER
  REMOTE_RUN_DIR
  REMOTE_COMPOSE_FILE
  REMOTE_PIN_OVERRIDE
  REMOTE_LEASE_MANAGER
  BOLT_PHASE0_LEASE_RUN_ID
  BOLT_PHASE0_LEASE_RUN_ATTEMPT
  BOLT_PHASE0_LEASE_HEARTBEAT_SECONDS
  COMPOSE_PROJECT_NAME
  RUNNER_TEMP
)
for variable in "${required[@]}"; do
  if [ -z "${!variable:-}" ]; then
    echo "missing required environment variable: $variable" >&2
    exit 2
  fi
done

remote_report="$REMOTE_RUN_DIR/synthetics-${stage}.json"
printf -v remote_env \
  'XFRAMEWORK_ENV_FILE=%q ENV_PARSER=%q REMOTE_RUN_DIR=%q REMOTE_COMPOSE_FILE=%q REMOTE_PIN_OVERRIDE=%q REMOTE_LEASE_MANAGER=%q LEASE_RUN_ID=%q LEASE_RUN_ATTEMPT=%q LEASE_HEARTBEAT_SECONDS=%q COMPOSE_PROJECT_NAME=%q STAGE=%q REPORT=%q' \
  "$XFRAMEWORK_ENV_FILE" "$REMOTE_ENV_PARSER" "$REMOTE_RUN_DIR" \
  "$REMOTE_COMPOSE_FILE" "$REMOTE_PIN_OVERRIDE" "$REMOTE_LEASE_MANAGER" \
  "$BOLT_PHASE0_LEASE_RUN_ID" "$BOLT_PHASE0_LEASE_RUN_ATTEMPT" \
  "$BOLT_PHASE0_LEASE_HEARTBEAT_SECONDS" "$COMPOSE_PROJECT_NAME" "$stage" "$remote_report"

ssh -i "$DEPLOY_SSH_KEY" -o BatchMode=yes "$DEPLOY_HOST" \
  "$remote_env bash -s" <<'REMOTE_SCRIPT'
set -euo pipefail
umask 077

install -d -m 700 "$REMOTE_RUN_DIR"
if [ "$(stat -f -c '%T' /dev/shm)" != "tmpfs" ]; then
  echo "Phase 0 synthetic quarantined output requires /dev/shm tmpfs" >&2
  exit 1
fi
work_dir="$(mktemp -d "/dev/shm/bolt-phase0-synthetics-${STAGE}.XXXXXXXX")"
expiry_token=""
expiry_enabled="false"
heartbeat_pid=""
heartbeat_failed="$work_dir/lease-heartbeat.failed"
heartbeat_ready="$work_dir/lease-heartbeat.ready"
synthetic_parent_pid="$BASHPID"
destroy_expiry_token() {
  [ "$expiry_enabled" = "true" ] && [ -n "$expiry_token" ] || return 0
  python3 - "$expiry_token" <<'PY' >/dev/null 2>&1 || true
import os
import stat
import sys

path = sys.argv[1]
try:
    metadata = os.lstat(path)
    if stat.S_ISREG(metadata.st_mode) and metadata.st_uid == os.geteuid():
        descriptor = os.open(path, os.O_WRONLY | getattr(os, "O_NOFOLLOW", 0))
        try:
            os.ftruncate(descriptor, 0)
        finally:
            os.close(descriptor)
        os.unlink(path)
except OSError:
    pass
PY
}
cleanup() {
  local status=$?
  local heartbeat_was_alive=false
  trap - EXIT TERM INT HUP
  if [ -n "$heartbeat_pid" ]; then
    if kill -0 "$heartbeat_pid" >/dev/null 2>&1; then
      heartbeat_was_alive=true
      kill "$heartbeat_pid" >/dev/null 2>&1 || true
    fi
    wait "$heartbeat_pid" 2>/dev/null || true
    if [ "$status" -eq 0 ] && { [ "$heartbeat_was_alive" != true ] || [ -e "$heartbeat_failed" ]; }; then
      echo "Phase 0 synthetic lease heartbeat stopped unexpectedly" >&2
      status=1
    fi
  fi
  destroy_expiry_token
  rm -rf -- "$work_dir"
  rm -f -- "$REPORT.tmp"
  exit "$status"
}
trap cleanup EXIT TERM INT HUP
rm -f -- "$REPORT" "$REPORT.tmp"

case "$LEASE_RUN_ID:$LEASE_RUN_ATTEMPT:$LEASE_HEARTBEAT_SECONDS" in
  *[!0-9:]*|:*|*::*|*:) echo "invalid Phase 0 synthetic lease identity" >&2; exit 1 ;;
esac
if [ "$LEASE_HEARTBEAT_SECONDS" -lt 10 ] || [ "$LEASE_HEARTBEAT_SECONDS" -gt 60 ]; then
  echo "Phase 0 synthetic lease heartbeat interval is outside the safe range" >&2
  exit 1
fi
if [ "$REMOTE_LEASE_MANAGER" != /usr/local/libexec/xframework-bolt-phase0/manage-bolt-phase0-deployment-lease.py ] || \
   [ ! -f "$REMOTE_LEASE_MANAGER" ] || [ -L "$REMOTE_LEASE_MANAGER" ] || \
   [ "$(stat -c '%u:%a' "$REMOTE_LEASE_MANAGER")" != 0:555 ]; then
  echo "fixed Phase 0 lease manager validation failed" >&2
  exit 1
fi
lease_python="$(readlink -f -- /usr/bin/python3)"
if [ -z "$lease_python" ] || [ ! -f "$lease_python" ] || [ -L "$lease_python" ] || \
   [ ! -x "$lease_python" ] || [ "$(stat -c '%u' "$lease_python")" != 0 ]; then
  echo "fixed Phase 0 lease interpreter validation failed" >&2
  exit 1
fi
lease_python_mode="$(stat -c '%a' "$lease_python")"
case "$lease_python_mode" in ''|*[!0-7]*) echo "invalid Phase 0 lease interpreter mode" >&2; exit 1 ;; esac
if [ "$((8#$lease_python_mode & 8#022))" -ne 0 ]; then
  echo "fixed Phase 0 lease interpreter is writable by a non-root identity" >&2
  exit 1
fi

lease_heartbeat_once() {
  "$lease_python" "$REMOTE_LEASE_MANAGER" \
    --project-name "$COMPOSE_PROJECT_NAME" \
    --deployment-uid "$(id -u)" \
    heartbeat \
    --run-id "$LEASE_RUN_ID" \
    --run-attempt "$LEASE_RUN_ATTEMPT" \
    --phase "synthetic-$STAGE" \
    --mutation-began >/dev/null
}

lease_heartbeat_loop() {
  trap 'exit 0' TERM INT HUP
  while true; do
    if ! lease_heartbeat_once; then
      : > "$heartbeat_failed"
      kill -TERM "$synthetic_parent_pid" >/dev/null 2>&1 || true
      return 1
    fi
    : > "$heartbeat_ready"
    sleep "$LEASE_HEARTBEAT_SECONDS" &
    wait $! || return 0
  done
}

lease_heartbeat_loop &
heartbeat_pid=$!
for _ in $(seq 1 100); do
  if [ -e "$heartbeat_ready" ]; then
    break
  fi
  if [ -e "$heartbeat_failed" ] || ! kill -0 "$heartbeat_pid" >/dev/null 2>&1; then
    echo "Phase 0 synthetic lease heartbeat failed to start" >&2
    exit 1
  fi
  sleep 0.1
done
if [ ! -e "$heartbeat_ready" ]; then
  echo "Phase 0 synthetic lease heartbeat did not become ready" >&2
  exit 1
fi

lock_file="$REMOTE_RUN_DIR/.synthetics.lock"
exec 9>"$lock_file"
chmod 600 "$lock_file"
if ! flock -w 60 9; then
  echo "another Phase 0 synthetic run holds the evidence lock" >&2
  exit 1
fi

read_required_path() {
  python3 "$ENV_PARSER" --file "$XFRAMEWORK_ENV_FILE" --key "$1" --type absolute-path
}

read_optional_value() {
  python3 - "$ENV_PARSER" "$XFRAMEWORK_ENV_FILE" "$1" "${2:-raw}" <<'PY'
import importlib.util
import sys
from pathlib import Path

module_path, env_path, key, value_type = sys.argv[1:]
spec = importlib.util.spec_from_file_location("bolt_phase0_env", module_path)
if spec is None or spec.loader is None:
    raise SystemExit("shared Phase 0 environment parser is unavailable")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
try:
    values = module.parse_env(Path(env_path))
    if key not in values or not values[key]:
        raise SystemExit(3)
    value = values[key] if value_type == "raw" else module.typed_value(key, values[key], value_type)
except ValueError as error:
    raise SystemExit(f"invalid protected setting: {key}: {error}")
sys.stdout.write(value)
PY
}

read_optional_path() { read_optional_value "$1" absolute-path; }

read_optional_path_into() {
  local key="$1"
  local destination="$2"
  local value
  local status
  if value="$(read_optional_path "$key")"; then
    printf -v "$destination" '%s' "$value"
    return 0
  else
    status=$?
  fi
  if [ "$status" -eq 3 ]; then
    printf -v "$destination" '%s' ""
    return 0
  fi
  return "$status"
}

proxy_mode=""
if ! proxy_mode="$(read_optional_value BOLT_SYNTHETIC_PROXY_MODE raw)"; then
  echo "synthetic proxy mode is missing or invalid" >&2
  exit 1
fi
case "$proxy_mode" in
  logs|direct-kestrel) ;;
  *) echo "synthetic proxy mode is missing or invalid" >&2; exit 1 ;;
esac

proxy_log_paths=""
proxy_log_paths_present="$(python3 - "$ENV_PARSER" "$XFRAMEWORK_ENV_FILE" <<'PY'
import importlib.util
import sys
from pathlib import Path

module_path, env_path = sys.argv[1:]
spec = importlib.util.spec_from_file_location("bolt_phase0_env_presence", module_path)
if spec is None or spec.loader is None:
    raise SystemExit("shared Phase 0 environment parser is unavailable")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
try:
    values = module.parse_env(Path(env_path))
except ValueError as error:
    raise SystemExit(f"invalid protected setting: BOLT_SYNTHETIC_PROXY_LOG_PATHS: {error}")
sys.stdout.write("true" if "BOLT_SYNTHETIC_PROXY_LOG_PATHS" in values else "false")
PY
)"
if proxy_log_paths="$(read_optional_value BOLT_SYNTHETIC_PROXY_LOG_PATHS raw)"; then
  :
else
  status=$?
  if [ "$status" -ne 3 ]; then
    echo "synthetic proxy log path configuration is invalid" >&2
    exit 1
  fi
  proxy_log_paths=""
fi
if { [ "$proxy_mode" = logs ] && [ -z "$proxy_log_paths" ]; } || \
   { [ "$proxy_mode" = direct-kestrel ] && [ "$proxy_log_paths_present" = true ]; }; then
  echo "synthetic proxy mode and log path configuration are inconsistent" >&2
  exit 1
fi

validate_private_executable() {
  python3 - "$1" <<'PY'
import os
import stat
import sys

path = sys.argv[1]
try:
    metadata = os.lstat(path)
except OSError:
    raise SystemExit("required synthetic evidence hook is unavailable")
if os.path.realpath(path) != path or not stat.S_ISREG(metadata.st_mode) or stat.S_ISLNK(metadata.st_mode):
    raise SystemExit("synthetic evidence hook must be a regular non-linked file")
if metadata.st_uid != os.geteuid():
    raise SystemExit("synthetic evidence hook must be owned by the deployment identity")
if metadata.st_mode & (stat.S_IRWXG | stat.S_IRWXO):
    raise SystemExit("synthetic evidence hook must not grant group or other permissions")
if not metadata.st_mode & stat.S_IXUSR:
    raise SystemExit("synthetic evidence hook must be owner-executable")
PY
}

refresh_hook="$(read_required_path BOLT_SYNTHETIC_TOKEN_REFRESH_COMMAND_PATH)"
proxy_scan_hook="$(read_required_path BOLT_SYNTHETIC_PROXY_MARKER_SCAN_COMMAND_PATH)"
seq_scan_hook="$(read_required_path BOLT_SYNTHETIC_SEQ_MARKER_SCAN_COMMAND_PATH)"
trace_scan_hook="$(read_required_path BOLT_SYNTHETIC_TRACE_MARKER_SCAN_COMMAND_PATH)"
plaintext_hook="$(read_required_path BOLT_SYNTHETIC_PLAINTEXT_REJECTION_COMMAND_PATH)"
for hook in "$refresh_hook" "$proxy_scan_hook" "$seq_scan_hook" "$trace_scan_hook" "$plaintext_hook"; do
  validate_private_executable "$hook"
done

redis_hook=""
if [ "$STAGE" = "canary" ]; then
  redis_hook="$(read_required_path BOLT_SYNTHETIC_REDIS_INTERRUPTION_COMMAND_PATH)"
  validate_private_executable "$redis_hook"
fi

old_generation_hook=""
if [ "$STAGE" = "finalized" ]; then
  old_generation_hook="$(read_required_path BOLT_SYNTHETIC_OLD_GENERATION_REJECTION_COMMAND_PATH)"
  validate_private_executable "$old_generation_hook"
fi

communications_token="$(read_required_path BOLT_SYNTHETIC_COMMUNICATIONS_TOKEN_PATH)"
user_token="$(read_required_path BOLT_SYNTHETIC_USER_TOKEN_PATH)"
expiry_token="$(read_required_path BOLT_SYNTHETIC_EXPIRY_TOKEN_PATH)"
case "$STAGE" in
  canary|finalized) expiry_enabled="true" ;;
esac
rejected_communications_token=""
rejected_user_token=""
if [ "$STAGE" = "finalized" ]; then
  read_optional_path_into BOLT_SYNTHETIC_REJECTED_COMMUNICATIONS_TOKEN_PATH rejected_communications_token
  read_optional_path_into BOLT_SYNTHETIC_REJECTED_USER_TOKEN_PATH rejected_user_token
fi

refresh_receipt="$work_dir/token-refresh-receipt.json"
hook_stdout="$work_dir/hook.stdout"
hook_stderr="$work_dir/hook.stderr"
refresh_started_epoch="$(date +%s)"
if ! (ulimit -f 128; env -i \
  PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin \
  HOME="${HOME:-/tmp}" \
  XFRAMEWORK_ENV_FILE="$XFRAMEWORK_ENV_FILE" \
  BOLT_SYNTHETIC_REFRESH_RECEIPT="$refresh_receipt" \
  BOLT_SYNTHETIC_EXPIRY_ENABLED="$expiry_enabled" \
  BOLT_SYNTHETIC_STAGE="$STAGE" \
  timeout --signal=TERM --kill-after=5s 120s "$refresh_hook" </dev/null >"$hook_stdout" 2>"$hook_stderr"); then
  echo "synthetic token refresh failed; hook output was suppressed" >&2
  exit 1
fi
if [ -s "$hook_stdout" ] || [ -s "$hook_stderr" ]; then
  echo "synthetic token refresh hook emitted output; output was suppressed" >&2
  exit 1
fi

minimum_lifetime="$(read_optional_value BOLT_SYNTHETIC_MIN_TOKEN_LIFETIME_SECONDS raw 2>/dev/null || printf '60')"
case "$minimum_lifetime" in
  ''|*[!0-9]*) echo "invalid synthetic minimum token lifetime" >&2; exit 1 ;;
esac
if [ "$minimum_lifetime" -lt 60 ] || [ "$minimum_lifetime" -gt 3600 ]; then
  echo "synthetic minimum token lifetime must be between 60 and 3600 seconds" >&2
  exit 1
fi

token_manifest="$work_dir/token-manifest.json"
python3 - "$token_manifest" "$refresh_receipt" "$refresh_started_epoch" "$minimum_lifetime" \
  "$communications_token" "$user_token" "$expiry_token" "$expiry_enabled" \
  "$rejected_communications_token" "$rejected_user_token" <<'PY'
import base64
import datetime as dt
import hashlib
import json
import os
import re
import stat
import sys
from pathlib import Path

manifest_path, receipt_path, refresh_started_raw, minimum_lifetime_raw, communications_path, user_path, expiry_path, expiry_enabled_raw, rejected_communications_path, rejected_user_path = sys.argv[1:]
now = dt.datetime.now(dt.timezone.utc)
refresh_started = int(refresh_started_raw)
minimum_lifetime = int(minimum_lifetime_raw)
expiry_enabled = expiry_enabled_raw == "true"

def read_token(path: str, *, refreshed: bool) -> tuple[bytes, dict]:
    if os.path.realpath(path) != path:
        raise SystemExit("synthetic token path must not traverse symbolic links")
    flags = os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)
    try:
        descriptor = os.open(path, flags)
    except OSError:
        raise SystemExit("synthetic token file is unavailable")
    try:
        metadata = os.fstat(descriptor)
        if not stat.S_ISREG(metadata.st_mode) or metadata.st_uid != os.geteuid():
            raise SystemExit("synthetic token file must be a deployment-owned regular file")
        if metadata.st_mode & (stat.S_IRWXG | stat.S_IRWXO | stat.S_IXUSR):
            raise SystemExit("synthetic token file permissions are too broad")
        if not metadata.st_mode & stat.S_IRUSR or metadata.st_size <= 32 or metadata.st_size > 16 * 1024:
            raise SystemExit("synthetic token file size or mode is invalid")
        if refreshed and metadata.st_mtime < refresh_started - 1:
            raise SystemExit("synthetic token refresh did not replace every current token file")
        value = os.read(descriptor, 16 * 1024 + 1).strip()
    finally:
        os.close(descriptor)
    if not value or len(value) > 16 * 1024 or any(character in b" \t\r\n\0" for character in value):
        raise SystemExit("synthetic token file content is invalid")
    parts = value.split(b".")
    if len(parts) != 3:
        raise SystemExit("synthetic token must be a JWT")
    try:
        payload = parts[1] + b"=" * (-len(parts[1]) % 4)
        claims = json.loads(base64.urlsafe_b64decode(payload))
        expiration = int(claims["exp"])
        issuer = claims["iss"]
        marker = claims["jti"]
    except (KeyError, TypeError, ValueError, json.JSONDecodeError):
        raise SystemExit("synthetic token claims are invalid")
    if (not isinstance(issuer, str) or not issuer or len(issuer) > 512 or issuer != issuer.strip() or
            any(ord(character) < 33 or ord(character) == 127 for character in issuer)):
        raise SystemExit("synthetic token issuer identifier is invalid")
    compact_marker = marker.replace("-", "") if isinstance(marker, str) else ""
    if not re.fullmatch(r"[0-9a-fA-F]{32}", compact_marker) or int(compact_marker, 16) == 0:
        raise SystemExit("synthetic token marker is invalid")
    return value, {
        "sha256Prefix": hashlib.sha256(value).hexdigest()[:12],
        "expiresAtUtc": dt.datetime.fromtimestamp(expiration, dt.timezone.utc).isoformat().replace("+00:00", "Z"),
        "issuerUri": issuer,
        "marker": marker,
        "markerSha256Prefix": hashlib.sha256(marker.encode()).hexdigest()[:12],
        "identity": [metadata.st_dev, metadata.st_ino, metadata.st_size, metadata.st_mtime_ns, hashlib.sha256(value).hexdigest()],
    }

purposes = {
    "communications": read_token(communications_path, refreshed=True),
    "user": read_token(user_path, refreshed=True),
}
if expiry_enabled:
    purposes["expiry"] = read_token(expiry_path, refreshed=True)
else:
    try:
        placeholder = os.lstat(expiry_path)
    except OSError:
        raise SystemExit("disabled expiry token placeholder is unavailable")
    if (not stat.S_ISREG(placeholder.st_mode) or placeholder.st_uid != os.geteuid() or
            placeholder.st_size != 0 or placeholder.st_mode & (stat.S_IRWXG | stat.S_IRWXO | stat.S_IXUSR)):
        raise SystemExit("disabled expiry token placeholder must be an empty private regular file")
for purpose in ("communications", "user"):
    expiration = dt.datetime.fromisoformat(purposes[purpose][1]["expiresAtUtc"].replace("Z", "+00:00"))
    if expiration < now + dt.timedelta(seconds=minimum_lifetime):
        raise SystemExit("current synthetic token lifetime is insufficient for the suite")
if expiry_enabled:
    expiry_at = dt.datetime.fromisoformat(purposes["expiry"][1]["expiresAtUtc"].replace("Z", "+00:00"))
    if expiry_at <= now + dt.timedelta(seconds=1) or expiry_at > now + dt.timedelta(seconds=570):
        raise SystemExit("expiry synthetic token is outside the bounded observation window")
if len({entry[1]["issuerUri"] for entry in purposes.values()}) != 1:
    raise SystemExit("synthetic tokens do not share one issuer identifier")

try:
    receipt_metadata = os.lstat(receipt_path)
    if not stat.S_ISREG(receipt_metadata.st_mode) or stat.S_ISLNK(receipt_metadata.st_mode):
        raise ValueError
    if receipt_metadata.st_uid != os.geteuid() or receipt_metadata.st_mode & (stat.S_IRWXG | stat.S_IRWXO):
        raise ValueError
    receipt = json.loads(Path(receipt_path).read_text(encoding="utf-8"))
except (OSError, ValueError, json.JSONDecodeError):
    raise SystemExit("synthetic refresh receipt is invalid")
if set(receipt) != {"schemaVersion", "status", "issuerUri", "principalReference", "refreshedAtUtc", "tokenExpirationsUtc"}:
    raise SystemExit("synthetic refresh receipt schema is invalid")
if receipt["schemaVersion"] != "bolt-phase0-token-refresh/v1" or receipt["status"] != "passed":
    raise SystemExit("synthetic refresh receipt did not pass")
if receipt["issuerUri"] != purposes["user"][1]["issuerUri"]:
    raise SystemExit("synthetic refresh receipt issuer does not match issued tokens")
if not isinstance(receipt["principalReference"], str) or not re.fullmatch(r"[A-Za-z0-9_.:-]{1,96}", receipt["principalReference"]):
    raise SystemExit("synthetic refresh principal reference is invalid")
try:
    refreshed_at = dt.datetime.fromisoformat(receipt["refreshedAtUtc"].replace("Z", "+00:00"))
except (AttributeError, ValueError):
    raise SystemExit("synthetic refresh timestamp is invalid")
if refreshed_at.tzinfo is None or refreshed_at.timestamp() < refresh_started - 5 or refreshed_at > now + dt.timedelta(seconds=5):
    raise SystemExit("synthetic refresh timestamp is outside the execution window")
expected_expirations = {name: evidence[1]["expiresAtUtc"] for name, evidence in purposes.items()}
if receipt["tokenExpirationsUtc"] != expected_expirations:
    raise SystemExit("synthetic refresh receipt expirations do not match issued tokens")

entries = []
current_paths = [("communications", communications_path), ("user", user_path)]
if expiry_enabled:
    current_paths.append(("expiry", expiry_path))
for purpose, path in current_paths:
    entries.append({"purpose": purpose, "path": path, **purposes[purpose][1]})
for purpose, path in (("rejected_communications", rejected_communications_path), ("rejected_user", rejected_user_path)):
    if path:
        value, evidence = read_token(path, refreshed=False)
        entries.append({"purpose": purpose, "path": path, **evidence})
if len({entry["marker"] for entry in entries}) != len(entries):
    raise SystemExit("synthetic tokens must use unique markers")

manifest = {
    "schemaVersion": "bolt-phase0-token-manifest/v1",
    "issuerUri": receipt["issuerUri"],
    "principalReference": receipt["principalReference"],
    "refreshedAtUtc": receipt["refreshedAtUtc"],
    "minimumRemainingLifetimeSeconds": minimum_lifetime,
    "expiryEnabled": expiry_enabled,
    "tokens": entries,
}
Path(manifest_path).write_text(json.dumps(manifest, sort_keys=True), encoding="utf-8")
os.chmod(manifest_path, 0o600)
PY

scan_for_tokens() {
  python3 - "$token_manifest" "$@" <<'PY'
import json
import sys
from pathlib import Path

manifest_path, *evidence_paths = sys.argv[1:]
manifest = json.loads(Path(manifest_path).read_text(encoding="utf-8"))
tokens = [Path(entry["path"]).read_bytes().strip() for entry in manifest["tokens"]]
markers = [entry["marker"].encode() for entry in manifest["tokens"]]
for evidence_path in evidence_paths:
    data = Path(evidence_path).read_bytes()
    if any(needle and needle in data for needle in tokens + markers):
        raise SystemExit("a synthetic token or marker appeared in quarantined output or retained application logs")
PY
}

scan_stream_for_tokens() {
  python3 -c '
import json
import sys
from pathlib import Path

manifest = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
tokens = [Path(entry["path"]).read_bytes().strip() for entry in manifest["tokens"]]
markers = [entry["marker"].encode() for entry in manifest["tokens"]]
needles = tokens + markers
longest = max(map(len, needles), default=1)
overlap = b""
while True:
    chunk = sys.stdin.buffer.read(64 * 1024)
    if not chunk:
        break
    data = overlap + chunk
    if any(needle and needle in data for needle in needles):
        raise SystemExit("a synthetic token or marker appeared in retained application logs")
    overlap = data[-(longest - 1):] if longest > 1 else b""
' "$token_manifest"
}

core_report="$work_dir/core-report.json"
synthetic_stderr="$work_dir/synthetic.stderr"
compose=(docker compose --profile phase0-verification --env-file "$XFRAMEWORK_ENV_FILE" \
  -f "$REMOTE_COMPOSE_FILE" -f "$REMOTE_PIN_OVERRIDE" --project-name "$COMPOSE_PROJECT_NAME")
synthetic_run=(run --rm --no-deps)
if [ "$expiry_enabled" = "true" ]; then
  synthetic_run+=(-e BOLT_SYNTHETIC_EXPIRY_MAX_WAIT_SECONDS=600)
else
  synthetic_run+=(-e BOLT_SYNTHETIC_EXPIRY_TOKEN_FILE=)
fi
synthetic_run+=(bolt-phase0-synthetics)
if ! (ulimit -f 2048; timeout --signal=TERM --kill-after=10s 900s \
  "${compose[@]}" "${synthetic_run[@]}" >"$core_report" 2>"$synthetic_stderr"); then
  scan_for_tokens "$core_report" "$synthetic_stderr"
  echo "Phase 0 synthetic container failed; output was quarantined" >&2
  exit 1
fi
scan_for_tokens "$core_report" "$synthetic_stderr"
if [ -s "$synthetic_stderr" ]; then
  echo "Phase 0 synthetic container emitted stderr; output was quarantined" >&2
  exit 1
fi

while IFS= read -r container_id; do
  [ -n "$container_id" ] || continue
  /usr/bin/timeout --foreground --kill-after=10s 60s docker logs --since "$refresh_started_epoch" "$container_id" 2>&1 | scan_stream_for_tokens || {
    echo "failed to read retained logs for a Compose container" >&2
    exit 1
  }
done < <(/usr/bin/timeout --foreground --kill-after=10s 30s "${compose[@]}" ps -q)

run_silent_hook() {
  local label="$1"
  local hook="$2"
  local kind="$3"
  local receipt="$work_dir/probe-${kind}.json"
  local probe_started_epoch
  probe_started_epoch="$(date +%s)"
  rm -f -- "$receipt"
  : >"$hook_stdout"
  : >"$hook_stderr"
  if ! (
    ulimit -f 128
    probe_command=(env -i \
      PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin \
      HOME="${HOME:-/tmp}" \
      XFRAMEWORK_ENV_FILE="$XFRAMEWORK_ENV_FILE" \
      BOLT_SYNTHETIC_PROBE_KIND="$kind" \
      BOLT_SYNTHETIC_PROBE_RECEIPT="$receipt" \
      BOLT_SYNTHETIC_TOKEN_MANIFEST="$token_manifest" \
      BOLT_SYNTHETIC_STAGE="$STAGE" \
      /usr/bin/timeout --signal=TERM --kill-after=5s 300s "$hook")
    if [ "$kind" = redis-interruption ]; then
      "$lease_python" "$REMOTE_LEASE_MANAGER" --project-name "$COMPOSE_PROJECT_NAME" --deployment-uid "$(id -u)" \
        supervise --run-id "$LEASE_RUN_ID" --run-attempt "$LEASE_RUN_ATTEMPT" --phase "redis-$STAGE" --mutation-began \
        --timeout-seconds 360 --quiet -- "${probe_command[@]}"
    else
      "${probe_command[@]}"
    fi
  ) </dev/null >"$hook_stdout" 2>"$hook_stderr"; then
    scan_for_tokens "$hook_stdout" "$hook_stderr"
    echo "$label failed; hook output was suppressed" >&2
    exit 1
  fi
  scan_for_tokens "$hook_stdout" "$hook_stderr"
  if [ -s "$hook_stdout" ] || [ -s "$hook_stderr" ]; then
    echo "$label emitted output; output was suppressed" >&2
    exit 1
  fi
  python3 - "$receipt" "$kind" "$probe_started_epoch" "$token_manifest" "$proxy_mode" <<'PY'
import datetime as dt
import json
import os
import stat
import sys
from pathlib import Path

receipt_path, expected_kind, started_epoch_raw, manifest_path, proxy_mode = sys.argv[1:]
proxy_assertions = {
    "logs": {"retainedStoreQueried": True, "matches": 0},
    "direct-kestrel": {
        "retainedStoreQueried": False,
        "notApplicableReason": "direct-kestrel-publication",
        "matches": 0,
    },
}
if proxy_mode not in proxy_assertions:
    raise SystemExit("synthetic proxy mode is invalid")
expected_assertions = {
    "proxy-marker-scan": proxy_assertions[proxy_mode],
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
}
try:
    metadata = os.lstat(receipt_path)
    if (os.path.realpath(receipt_path) != receipt_path or not stat.S_ISREG(metadata.st_mode) or
            metadata.st_uid != os.geteuid() or metadata.st_mode & (stat.S_IRWXG | stat.S_IRWXO)):
        raise ValueError
    receipt = json.loads(Path(receipt_path).read_text(encoding="utf-8"))
except (OSError, ValueError, json.JSONDecodeError):
    raise SystemExit("synthetic probe receipt is invalid")
if set(receipt) != {"schemaVersion", "probe", "status", "startedAtUtc", "completedAtUtc", "assertions"}:
    raise SystemExit("synthetic probe receipt schema is invalid")
if (receipt["schemaVersion"] != "bolt-phase0-probe-receipt/v1" or
        receipt["probe"] != expected_kind or receipt["status"] != "passed"):
    raise SystemExit("synthetic probe receipt identity or status is invalid")
assertions = receipt["assertions"]
expected = dict(expected_assertions[expected_kind])
if expected_kind.endswith("marker-scan"):
    token_count = len(json.loads(Path(manifest_path).read_text(encoding="utf-8"))["tokens"])
    expected.update({"tokensSearched": token_count, "markersSearched": token_count})
if assertions != expected:
    raise SystemExit("synthetic probe receipt assertions are incomplete")
try:
    started = dt.datetime.fromisoformat(receipt["startedAtUtc"].replace("Z", "+00:00"))
    completed = dt.datetime.fromisoformat(receipt["completedAtUtc"].replace("Z", "+00:00"))
except (AttributeError, ValueError):
    raise SystemExit("synthetic probe receipt timestamps are invalid")
now = dt.datetime.now(dt.timezone.utc)
minimum_started = dt.datetime.fromtimestamp(int(started_epoch_raw) - 5, dt.timezone.utc)
if (started.tzinfo is None or completed.tzinfo is None or started < minimum_started or
        completed < started or completed > now + dt.timedelta(seconds=5)):
    raise SystemExit("synthetic probe receipt timestamps are outside the execution window")
PY
  last_probe_receipt="$receipt"
}

last_probe_receipt=""
run_silent_hook "proxy marker-absence probe" "$proxy_scan_hook" "proxy-marker-scan"
proxy_receipt="$last_probe_receipt"
run_silent_hook "Seq marker-absence query" "$seq_scan_hook" "seq-marker-scan"
seq_receipt="$last_probe_receipt"
run_silent_hook "trace marker-absence query" "$trace_scan_hook" "trace-marker-scan"
trace_receipt="$last_probe_receipt"
run_silent_hook "plaintext rejection probe" "$plaintext_hook" "plaintext-rejection"
plaintext_receipt="$last_probe_receipt"
redis_status="not_required"
redis_receipt=""
if [ -n "$redis_hook" ]; then
  run_silent_hook "Redis interruption/recovery probe" "$redis_hook" "redis-interruption"
  redis_receipt="$last_probe_receipt"
  redis_status="passed"
fi
old_generation_status="not_required"
old_generation_receipt=""
if [ -n "$old_generation_hook" ]; then
  run_silent_hook "old-generation credential rejection probe" "$old_generation_hook" "old-generation-rejection"
  old_generation_receipt="$last_probe_receipt"
  old_generation_status="passed"
fi

python3 - "$token_manifest" "$expiry_token" "$expiry_enabled" <<'PY'
import hashlib
import json
import os
import stat
import sys
from pathlib import Path

manifest_path, expiry_path, expiry_enabled_raw = sys.argv[1:]
manifest = json.loads(Path(manifest_path).read_text(encoding="utf-8"))
for entry in manifest["tokens"]:
    path = entry["path"]
    metadata = os.stat(path, follow_symlinks=False)
    descriptor = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
    try:
        digest = hashlib.sha256(os.read(descriptor, 16 * 1024 + 1).strip()).hexdigest()
    finally:
        os.close(descriptor)
    identity = [metadata.st_dev, metadata.st_ino, metadata.st_size, metadata.st_mtime_ns, digest]
    if identity != entry["identity"]:
        raise SystemExit("synthetic token files changed while the evidence suite was running")
if expiry_enabled_raw != "true":
    metadata = os.stat(expiry_path, follow_symlinks=False)
    if (not stat.S_ISREG(metadata.st_mode) or metadata.st_uid != os.geteuid() or metadata.st_size != 0 or
            metadata.st_mode & (stat.S_IRWXG | stat.S_IRWXO | stat.S_IXUSR)):
        raise SystemExit("disabled expiry token placeholder changed while the evidence suite was running")
PY

temporary_report="$REPORT.tmp"
python3 - "$core_report" "$token_manifest" "$temporary_report" "$STAGE" "$redis_status" \
  "$old_generation_status" "$expiry_enabled" "$proxy_receipt" "$seq_receipt" "$trace_receipt" \
  "$plaintext_receipt" "$redis_receipt" "$old_generation_receipt" "$proxy_mode" <<'PY'
import datetime as dt
import hashlib
import json
import re
import sys
import uuid
from pathlib import Path
from urllib.parse import urlsplit

(
    core_path, manifest_path, report_path, stage, redis_status, old_generation_status,
    expiry_enabled_raw, proxy_receipt_path, seq_receipt_path, trace_receipt_path,
    plaintext_receipt_path, redis_receipt_path, old_generation_receipt_path, proxy_mode,
) = sys.argv[1:]
if proxy_mode not in {"logs", "direct-kestrel"}:
    raise SystemExit("synthetic proxy mode is invalid")
expiry_enabled = expiry_enabled_raw == "true"
core_bytes = Path(core_path).read_bytes()
if not core_bytes or len(core_bytes) > 1024 * 1024:
    raise SystemExit("synthetic core report size is invalid")
try:
    core = json.loads(core_bytes)
except json.JSONDecodeError:
    raise SystemExit("synthetic core report is not one JSON document")

expected_top_level = {
    "schemaVersion", "runId", "tokenSha256Prefixes", "startedAtUtc", "completedAtUtc",
    "target", "status", "timings", "operations"
}
if set(core) != expected_top_level or core["schemaVersion"] != "bolt-phase0-synthetic-report/v1" or core["status"] != "passed":
    raise SystemExit("synthetic core report schema or status is invalid")
try:
    run_id = uuid.UUID(core["runId"])
except (ValueError, TypeError, AttributeError):
    raise SystemExit("synthetic run identifier is invalid")
if run_id.int == 0:
    raise SystemExit("synthetic run identifier is invalid")

def timestamp(value, name):
    try:
        parsed = dt.datetime.fromisoformat(value.replace("Z", "+00:00"))
    except (AttributeError, ValueError):
        raise SystemExit(f"synthetic {name} timestamp is invalid")
    if parsed.tzinfo is None:
        raise SystemExit(f"synthetic {name} timestamp is invalid")
    return parsed.astimezone(dt.timezone.utc)

started_at = timestamp(core["startedAtUtc"], "start")
completed_at = timestamp(core["completedAtUtc"], "completion")
if completed_at < started_at:
    raise SystemExit("synthetic report timestamp order is invalid")
target = urlsplit(core["target"])
if target.scheme.lower() != "wss" or not target.hostname or target.username or target.password or target.query or target.fragment:
    raise SystemExit("synthetic report target is invalid")
if not isinstance(core["timings"], dict) or set(core["timings"]) != {"totalMs"} or not isinstance(core["timings"]["totalMs"], int) or core["timings"]["totalMs"] < 0:
    raise SystemExit("synthetic timing evidence is invalid")
if core["timings"]["totalMs"] > int((completed_at - started_at).total_seconds() * 1000) + 1000:
    raise SystemExit("synthetic total timing exceeds the report interval")
if not isinstance(core["tokenSha256Prefixes"], dict) or any(
        not isinstance(key, str) or not isinstance(value, str) or not re.fullmatch(r"[0-9a-f]{12}", value)
        for key, value in core["tokenSha256Prefixes"].items()):
    raise SystemExit("synthetic token evidence is invalid")

safe_name = re.compile(r"^[a-z][a-z0-9_]{0,63}$")
safe_value = re.compile(r"^[a-z0-9_./:-]{1,96}$")
operations = {}
for operation in core["operations"]:
    if not isinstance(operation, dict) or set(operation) != {"name", "startedAtUtc", "completedAtUtc", "status", "timingMs", "results"}:
        raise SystemExit("synthetic operation schema is invalid")
    name = operation["name"]
    if not isinstance(name, str) or not safe_name.fullmatch(name) or name in operations or operation["status"] != "passed":
        raise SystemExit("synthetic operation status or name is invalid")
    if not isinstance(operation["timingMs"], int) or operation["timingMs"] < 0 or not isinstance(operation["results"], dict):
        raise SystemExit("synthetic operation timing or results are invalid")
    operation_started = timestamp(operation["startedAtUtc"], "operation start")
    operation_completed = timestamp(operation["completedAtUtc"], "operation completion")
    if (operation_started < started_at or operation_completed < operation_started or
            operation_completed > completed_at + dt.timedelta(seconds=1)):
        raise SystemExit("synthetic operation timestamp order is invalid")
    if any(not safe_name.fullmatch(key) or not isinstance(value, str) or not safe_value.fullmatch(value)
           for key, value in operation["results"].items()):
        raise SystemExit("synthetic operation result is unsafe")
    operations[name] = operation

required_operations = {
    "user_registration", "hostile_reserved_registration", "communications_registration",
    "identity_health_check", "transient_presence", "durable_offline_registration",
    "durable_offline_publish", "durable_ordered_replay", "durable_ack",
    "durable_no_redelivery", "durable_unregister"
}
if expiry_enabled:
    required_operations.add("token_expiry_disconnect")
if not required_operations.issubset(operations):
    raise SystemExit("synthetic core report is missing a required operation")
if not expiry_enabled and "token_expiry_disconnect" in operations:
    raise SystemExit("expiry-disconnect operation ran in a stage where it is disabled")
ack_results = operations["durable_ack"]["results"]
if ack_results.get("duplicate_ack_idempotent") != "true" or ack_results.get("out_of_order_ack_monotonic") != "true":
    raise SystemExit("synthetic acknowledgement evidence is incomplete")

manifest = json.loads(Path(manifest_path).read_text(encoding="utf-8"))
if manifest.get("expiryEnabled") is not expiry_enabled:
    raise SystemExit("synthetic token manifest stage policy is invalid")
manifest_prefixes = {
    entry["purpose"]: entry["sha256Prefix"]
    for entry in manifest["tokens"]
    if entry["purpose"] in {"communications", "user", "expiry", "rejected_communications", "rejected_user"}
}
expected_prefixes = {
    purpose: manifest_prefixes[purpose]
    for purpose in ({"communications", "user", "expiry"} if expiry_enabled else {"communications", "user"})
}
for purpose in ("rejected_communications", "rejected_user"):
    if purpose in core["tokenSha256Prefixes"]:
        if purpose not in manifest_prefixes:
            raise SystemExit("synthetic core report contains unbound retired-token evidence")
        expected_prefixes[purpose] = manifest_prefixes[purpose]
if core["tokenSha256Prefixes"] != expected_prefixes:
    raise SystemExit("synthetic core report is not bound to the refreshed token set")
for purpose, operation_name in (
    ("rejected_communications", "old_generation_communications_token_rejection"),
    ("rejected_user", "old_generation_user_token_rejection"),
):
    if purpose in expected_prefixes and operation_name not in operations:
        raise SystemExit("synthetic retired-token evidence is missing its rejection operation")

probe_receipts = {}
for name, path in (
    ("proxyMarkerScan", proxy_receipt_path),
    ("seqMarkerScan", seq_receipt_path),
    ("traceMarkerScan", trace_receipt_path),
    ("plaintextRejection", plaintext_receipt_path),
    ("redisInterruption", redis_receipt_path),
    ("oldGenerationRejection", old_generation_receipt_path),
):
    if path:
        probe_receipts[name] = json.loads(Path(path).read_text(encoding="utf-8"))

evidence = {
    "schemaVersion": "bolt-phase0-synthetic-evidence/v1",
    "runId": str(run_id),
    "stage": stage,
    "status": "passed",
    "coreReportSha256": hashlib.sha256(core_bytes).hexdigest(),
    "synthetic": core,
    "postRunEvidence": {
        "schemaVersion": "bolt-phase0-post-run-evidence/v1",
        "tokenRefresh": {
            "status": "passed",
            "issuerUri": manifest["issuerUri"],
            "principalReferenceSha256Prefix": hashlib.sha256(
                manifest["principalReference"].encode()).hexdigest()[:12],
            "refreshedAtUtc": manifest["refreshedAtUtc"],
            "minimumRemainingLifetimeSeconds": manifest["minimumRemainingLifetimeSeconds"],
            "expiryTokenIssued": manifest["expiryEnabled"],
        },
        "markerAbsence": {
            "application": "passed",
            "proxy": "not_applicable" if proxy_mode == "direct-kestrel" else "passed",
            "seq": "passed",
            "trace": "passed",
            "markerSha256Prefixes": {
                entry["purpose"]: entry["markerSha256Prefix"] for entry in manifest["tokens"]
            },
        },
        "plaintextRejection": "passed",
        "expiryDisconnect": "passed" if expiry_enabled else "not_required",
        "redisInterruptionRecovery": redis_status,
        "oldGenerationCredentialRejection": old_generation_status,
        "tokenFilesStableForRun": "passed",
        "probeReceipts": probe_receipts,
    },
}
serialized = json.dumps(evidence, indent=2, sort_keys=True) + "\n"
Path(report_path).write_text(serialized, encoding="utf-8", newline="\n")
PY

chmod 600 "$temporary_report"
scan_for_tokens "$temporary_report"
mv -f -- "$temporary_report" "$REPORT"
REMOTE_SCRIPT

local_evidence="$RUNNER_TEMP/bolt-phase0-evidence"
mkdir -p "$local_evidence"
scp -i "$DEPLOY_SSH_KEY" -o BatchMode=yes \
  "$DEPLOY_HOST:$remote_report" "$local_evidence/synthetics-${stage}.json"
