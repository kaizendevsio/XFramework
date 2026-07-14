#!/usr/bin/env bash
set -euo pipefail

umask 077

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
repo_root="$(cd -- "$script_dir/.." && pwd -P)"
verifier="$script_dir/verify-bolt-tailscale-boundary.py"

tailscale_bin="${TAILSCALE_BIN:-tailscale}"
docker_bin="${DOCKER_BIN:-docker}"
python_bin="${PYTHON_BIN:-python3}"
compose_file="$repo_root/docker-compose.yml"
compose_json=""
evidence="-"

usage() {
  cat <<'EOF'
Usage: configure-bolt-tailscale-boundary.sh [options]

Options:
  --compose-file PATH  Compose file to render (default: repository docker-compose.yml)
  --compose-json PATH  Use an already rendered Docker Compose JSON document
  --evidence PATH      Write compact verification evidence to PATH (default: stdout)
  -h, --help           Show this help

TAILSCALE_BIN, DOCKER_BIN, and PYTHON_BIN can override tool paths.
EOF
}

fail() {
  printf 'ERROR: %s\n' "$1" >&2
  exit 1
}

require_executable() {
  local executable="$1"
  if [[ "$executable" == */* ]]; then
    [[ -x "$executable" ]] || fail "required executable is unavailable"
  else
    command -v -- "$executable" >/dev/null 2>&1 || fail "required executable is unavailable"
  fi
}

while (($#)); do
  case "$1" in
    --compose-file)
      (($# >= 2)) || fail "--compose-file requires a path"
      compose_file="$2"
      shift 2
      ;;
    --compose-json)
      (($# >= 2)) || fail "--compose-json requires a path"
      compose_json="$2"
      shift 2
      ;;
    --evidence)
      (($# >= 2)) || fail "--evidence requires a path or -"
      evidence="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      fail "unknown argument"
      ;;
  esac
done

require_executable "$tailscale_bin"
require_executable "$python_bin"
[[ -f "$verifier" ]] || fail "boundary verifier is unavailable"

temporary_directory="$(mktemp -d "${TMPDIR:-/tmp}/xframework-tailscale-boundary.XXXXXXXX")"
restore_required=0
previous_owned_7000=0
previous_owned_8261=0

restore_owned_listeners() {
  local failed=0
  if ((previous_owned_7000)); then
    "$tailscale_bin" serve --bg --yes --https=7000 http://127.0.0.1:7000 >&2 || failed=1
  else
    "$tailscale_bin" serve --bg --yes --https=7000 off >&2 || failed=1
  fi
  if ((previous_owned_8261)); then
    "$tailscale_bin" serve --bg --yes --https=8261 http://127.0.0.1:8261 >&2 || failed=1
  else
    "$tailscale_bin" serve --bg --yes --https=8261 off >&2 || failed=1
  fi
  return "$failed"
}

cleanup() {
  local status=$?
  trap - EXIT HUP INT TERM
  if ((restore_required)) && ! restore_owned_listeners; then
    printf 'ERROR: failed to restore the previous owned Serve listeners\n' >&2
    status=1
  fi
  rm -f -- \
    "$temporary_directory/compose.json" \
    "$temporary_directory/tailscale-version.json" \
    "$temporary_directory/tailscale-status.json" \
    "$temporary_directory/serve-before.json" \
    "$temporary_directory/serve-after.json" \
    "$temporary_directory/funnel-after.json"
  rmdir -- "$temporary_directory" 2>/dev/null || true
  exit "$status"
}
trap cleanup EXIT HUP INT TERM

if [[ -n "$compose_json" ]]; then
  [[ -f "$compose_json" ]] || fail "rendered Compose JSON is unavailable"
  rendered_compose="$compose_json"
else
  require_executable "$docker_bin"
  [[ -f "$compose_file" ]] || fail "Compose file is unavailable"
  rendered_compose="$temporary_directory/compose.json"
  "$docker_bin" compose -f "$compose_file" config --format json >"$rendered_compose"
fi

version_json="$temporary_directory/tailscale-version.json"
node_status_json="$temporary_directory/tailscale-status.json"
before_serve_json="$temporary_directory/serve-before.json"
after_serve_json="$temporary_directory/serve-after.json"
funnel_status_json="$temporary_directory/funnel-after.json"

"$tailscale_bin" version --json >"$version_json"
if ! "$python_bin" -B - "$version_json" <<'PY'
import json
import re
import sys

with open(sys.argv[1], encoding="utf-8-sig") as stream:
    version = json.load(stream).get("majorMinorPatch")
match = re.fullmatch(
    r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)"
    r"(?:-([0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?",
    version if isinstance(version, str) else "",
)
if match is None:
    raise SystemExit(1)
version_core = tuple(int(part) for part in match.groups()[:3])
minimum = (1, 98, 0)
if version_core < minimum or (version_core == minimum and match.group(4) is not None):
    raise SystemExit(1)
PY
then
  fail "Tailscale 1.98.0 or newer is required"
fi

"$tailscale_bin" status --json >"$node_status_json"
magicdns_host="$("$python_bin" -B - "$node_status_json" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8-sig") as stream:
    status = json.load(stream)
dns_name = status.get("Self", {}).get("DNSName")
if not isinstance(dns_name, str) or not dns_name:
    raise SystemExit("missing local MagicDNS hostname")
print(dns_name.rstrip(".").lower())
PY
)"

"$tailscale_bin" serve status --json >"$before_serve_json"

readarray -t previous_owned < <("$python_bin" -B - "$before_serve_json" "$magicdns_host" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8-sig") as stream:
    config = json.load(stream)
host = sys.argv[2]
tcp = config.get("TCP") or {}
web = config.get("Web") or {}
allow_funnel = config.get("AllowFunnel") or {}
for port in (7000, 8261):
    tcp_entry = tcp.get(str(port))
    web_entry = web.get(f"{host}:{port}")
    funnel = allow_funnel.get(f"{host}:{port}")
    if funnel not in (None, False):
        raise SystemExit("owned Serve listener has Funnel enabled")
    if tcp_entry is None and web_entry is None:
        print(0)
        continue
    expected_tcp = {"HTTPS": True}
    expected_web = {"Handlers": {"/": {"Proxy": f"http://127.0.0.1:{port}"}}}
    if tcp_entry != expected_tcp or web_entry != expected_web:
        raise SystemExit("owned Serve listener has unexpected pre-existing state")
    print(1)
PY
)
[[ "${#previous_owned[@]}" -eq 2 ]] || fail "owned Serve pre-state is invalid"
previous_owned_7000="${previous_owned[0]}"
previous_owned_8261="${previous_owned[1]}"
[[ "$previous_owned_7000" =~ ^[01]$ && "$previous_owned_8261" =~ ^[01]$ ]] || \
  fail "owned Serve pre-state is invalid"

apply_failed=0
restore_required=1
if ! "$tailscale_bin" serve --bg --yes --https=7000 http://127.0.0.1:7000 >&2; then
  apply_failed=1
fi
if ! "$tailscale_bin" serve --bg --yes --https=8261 http://127.0.0.1:8261 >&2; then
  apply_failed=1
fi

"$tailscale_bin" serve status --json >"$after_serve_json"
"$tailscale_bin" funnel status --json >"$funnel_status_json"

verification_status=0
"$python_bin" -B "$verifier" \
  --serve-status-json "$after_serve_json" \
  --previous-serve-status-json "$before_serve_json" \
  --funnel-config-json "$funnel_status_json" \
  --compose-json "$rendered_compose" \
  --magicdns-host "$magicdns_host" \
  --evidence "$evidence" || verification_status=$?

if ((verification_status != 0)); then
  exit "$verification_status"
fi
restore_required=0
if ((apply_failed != 0)); then
  printf 'WARNING: an apply command failed, but the captured final boundary is valid\n' >&2
fi
