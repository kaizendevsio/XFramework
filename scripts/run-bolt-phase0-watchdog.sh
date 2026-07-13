#!/usr/bin/env bash
set -euo pipefail

deploy_root=/home/github-runner/xframework-deploy
python_link=/usr/bin/python3
docker=/usr/bin/docker
timeout=/usr/bin/timeout
expected_user=github-runner
lkg_pointer="$deploy_root/phase0-last-known-good/current"
installed_launcher=/usr/local/sbin/xframework-bolt-phase0-watchdog
fixed_lease_manager=/usr/local/libexec/xframework-bolt-phase0/manage-bolt-phase0-deployment-lease.py
hub_container=xframework-bolt-hub
controller_completed=false
mode=watch-once
services=(
  migrate
  bolt-hub
  identityserver
  communications
  notifications
  storage
  attendance
  smsgateway
  wallets
  inventario
  pos
  portal
  operations-dashboard
)

inspect_hub_state() {
  local state inspect_status container_names container_status daemon_version
  inspect_status=0
  state="$({ /usr/bin/timeout --signal=TERM --kill-after=5s 30s /usr/bin/docker inspect --format '{{.State.Running}}' "$hub_container"; } 2>/dev/null)" || inspect_status=$?
  if [ "$inspect_status" -eq 0 ]; then
    case "$state" in true|false) printf '%s\n' "$state"; return 0 ;; *) return 1 ;; esac
  fi

  container_status=0
  container_names="$({ /usr/bin/timeout --signal=TERM --kill-after=5s 30s /usr/bin/docker container ls -a --no-trunc --filter 'name=^/xframework-bolt-hub$' --format '{{.Names}}'; } 2>/dev/null)" || container_status=$?
  [ "$container_status" -eq 0 ] || return 1
  [ -z "$container_names" ] || return 1
  daemon_version="$({ /usr/bin/timeout --signal=TERM --kill-after=5s 30s /usr/bin/docker info --format '{{.ServerVersion}}'; } 2>/dev/null)" || return 1
  [ -n "$daemon_version" ] || return 1
  printf '%s\n' absent
}

fail_closed() {
  local state stop_status kill_status
  stop_status=0
  /usr/bin/timeout --signal=TERM --kill-after=5s 40s \
    /usr/bin/docker stop --time 30 "$hub_container" >/dev/null 2>&1 || stop_status=$?
  state="$(inspect_hub_state)" || state=unknown
  if [ "$stop_status" -eq 0 ]; then
    case "$state" in false|absent) return 0 ;; esac
  fi

  kill_status=0
  /usr/bin/timeout --signal=TERM --kill-after=5s 10s \
    /usr/bin/docker kill "$hub_container" >/dev/null 2>&1 || kill_status=$?
  state="$(inspect_hub_state)" || state=unknown
  case "$state" in false|absent) return 0 ;; *) return 1 ;; esac
}

on_exit() {
  local status=$?
  trap - EXIT
  if [ "$controller_completed" != true ] && ! fail_closed; then
    status=1
  fi
  exit "$status"
}
trap on_exit EXIT

if [ "$#" -gt 1 ]; then
  exit 64
fi
if [ "$#" -eq 1 ]; then
  case "$1" in
    verify-bootstrap|force-recovery) mode="$1" ;;
    *) exit 64 ;;
  esac
fi

test "$(id -un)" = "$expected_user"
python="$(/usr/bin/readlink -f -- "$python_link")"
test -n "$python"
test -f "$python"
test ! -L "$python"
test -x "$python"
test "$(stat -c '%u' "$python")" = 0
python_mode="$(stat -c '%a' "$python")"
case "$python_mode" in ''|*[!0-7]*) exit 1 ;; esac
test "$((8#$python_mode & 8#022))" -eq 0
test -x "$docker"
test -x "$timeout"

test -f "$fixed_lease_manager"
test ! -L "$fixed_lease_manager"
test -x "$fixed_lease_manager"
test "$(stat -c '%u' "$fixed_lease_manager")" = 0
test "$(stat -c '%a' "$fixed_lease_manager")" = 555
test "$(stat -c '%u:%a' "$(dirname "$fixed_lease_manager")")" = 0:755

if [ ! -e "$lkg_pointer" ] && [ ! -L "$lkg_pointer" ]; then
  hub_running="$(inspect_hub_state)" || exit 1
  if [ "$mode" = verify-bootstrap ]; then
    test "$hub_running" != true
    controller_completed=true
    exit 0
  fi
  controller_command=watch-no-lkg
  if [ "$mode" = force-recovery ]; then
    controller_command=force-no-lkg
  fi
  status=0
  "$python" "$fixed_lease_manager" \
    --project-name xframework \
    --deployment-uid "$(id -u)" \
    "$controller_command" \
    --env-file /opt/xframework/xeon-dev.env \
    --python-executable "$python" \
    --docker-executable "$docker" \
    --hub-container-name "$hub_container" \
    --stop-timeout-seconds 30 || status=$?
  test "$status" -eq 0
  controller_completed=true
  exit 0
fi

lkg_run="$("$python" - "$lkg_pointer" "$deploy_root/runs" "$installed_launcher" "$fixed_lease_manager" "$deploy_root" <<'PY'
import hashlib
import json
import os
import pathlib
import re
import stat
import sys

pointer = pathlib.Path(sys.argv[1])
run_root = pathlib.Path(sys.argv[2])
installed_launcher = pathlib.Path(sys.argv[3])
fixed_lease_manager = pathlib.Path(sys.argv[4])
deploy_root = pathlib.Path(sys.argv[5])
executables = (
    "manage-bolt-phase0-deployment-lease.py",
    "manage-bolt-phase0-rotation.py",
    "verify-bolt-phase0-runtime.py",
    "verify-bolt-phase0-env.py",
    "verify-bolt-phase0-qualification.py",
    "run-bolt-phase0-recovery-synthetic.py",
    "refresh-bolt-phase0-synthetic-tokens.py",
    "run-bolt-phase0-marker-scan.py",
    "run-bolt-phase0-operational-probe.py",
    "run-bolt-phase0-post-recovery-durable.py",
    "run-bolt-phase0-watchdog.sh",
)
configurations = (
    "xframework-bolt-phase0-watchdog.service",
    "xframework-bolt-phase0-watchdog.timer",
)


def fail() -> None:
    raise SystemExit(1)


def no_symlink_components(path: pathlib.Path) -> None:
    current = pathlib.Path(path.anchor)
    for part in path.parts[1:]:
        current /= part
        try:
            metadata = current.lstat()
        except OSError:
            fail()
        if stat.S_ISLNK(metadata.st_mode):
            fail()


def root_directory(path: pathlib.Path, mode: int) -> None:
    no_symlink_components(path)
    try:
        metadata = path.lstat()
    except OSError:
        fail()
    if not stat.S_ISDIR(metadata.st_mode):
        fail()
    if os.name == "posix" and (
        metadata.st_uid != 0 or stat.S_IMODE(metadata.st_mode) != mode
    ):
        fail()


def root_file(path: pathlib.Path, mode: int, maximum: int) -> bytes:
    no_symlink_components(path)
    try:
        metadata = path.lstat()
    except OSError:
        fail()
    if (
        stat.S_ISLNK(metadata.st_mode)
        or not stat.S_ISREG(metadata.st_mode)
        or metadata.st_nlink != 1
        or metadata.st_size > maximum
        or (os.name == "posix" and metadata.st_uid != 0)
        or (os.name == "posix" and stat.S_IMODE(metadata.st_mode) != mode)
    ):
        fail()
    try:
        raw = path.read_bytes()
        after = path.lstat()
    except OSError:
        fail()
    if (
        (metadata.st_dev, metadata.st_ino, metadata.st_size, metadata.st_mtime_ns)
        != (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns)
        or len(raw) != metadata.st_size
    ):
        fail()
    return raw


root_directory(deploy_root, 0o755)
root_directory(run_root, 0o755)
root_directory(pointer.parent, 0o755)
raw_pointer = root_file(pointer, 0o644, 4096)
try:
    pointer_text = raw_pointer.decode("utf-8", errors="strict")
except UnicodeDecodeError:
    fail()
if pointer_text.count("\n") != 1 or not pointer_text.endswith("\n"):
    fail()
run = pathlib.Path(pointer_text[:-1])
if not run.is_absolute() or ".." in run.parts:
    fail()
try:
    relative = run.relative_to(run_root)
except ValueError:
    fail()
match = re.fullmatch(r"([1-9][0-9]{0,31})-([1-9][0-9]{0,5})", relative.as_posix())
if not match or len(relative.parts) != 1:
    fail()
root_directory(run, 0o550)

try:
    evidence = json.loads(
        root_file(run / "qualification-evidence.json", 0o440, 32 * 1024 * 1024).decode(
            "utf-8", errors="strict"
        )
    )
except (UnicodeDecodeError, json.JSONDecodeError):
    fail()
if (
    not isinstance(evidence, dict)
    or evidence.get("schema") != "xframework.bolt.phase0.qualification.v1"
    or evidence.get("status") != "passed"
    or evidence.get("run_id") != match.group(1)
    or evidence.get("run_attempt") != int(match.group(2))
    or evidence.get("proxy_mode") != "direct-kestrel"
    or evidence.get("errors") != []
    or not isinstance(evidence.get("artifacts"), dict)
):
    fail()
commit = evidence.get("source_commit")
if not isinstance(commit, str) or not re.fullmatch(r"[0-9a-f]{40}", commit):
    fail()
if root_file(run / "qualified-commit", 0o440, 64) != f"{commit}\n".encode("ascii"):
    fail()
if root_file(run / "security-qualified", 0o440, 0) != b"":
    fail()

raw_by_name = {}
for name in executables:
    raw_by_name[name] = root_file(run / name, 0o550, 64 * 1024 * 1024)
for name in configurations:
    raw_by_name[name] = root_file(run / name, 0o440, 1024 * 1024)
for name, raw in raw_by_name.items():
    summary = evidence["artifacts"].get(name)
    if (
        not isinstance(summary, dict)
        or set(summary) != {"path", "sha256", "schema", "generated_at_utc"}
        or summary.get("path") != name
        or summary.get("sha256") != "sha256:" + hashlib.sha256(raw).hexdigest()
    ):
        fail()

installed = root_file(installed_launcher, 0o555, 64 * 1024 * 1024)
if hashlib.sha256(installed).digest() != hashlib.sha256(
    raw_by_name["run-bolt-phase0-watchdog.sh"]
).digest():
    fail()
fixed_manager = root_file(fixed_lease_manager, 0o555, 64 * 1024 * 1024)
if hashlib.sha256(fixed_manager).digest() != hashlib.sha256(
    raw_by_name["manage-bolt-phase0-deployment-lease.py"]
).digest():
    fail()

print(run)
PY
)"

if [ "$mode" = verify-bootstrap ]; then
  controller_completed=true
  exit 0
fi

manager="$lkg_run/manage-bolt-phase0-deployment-lease.py"
arguments=()
for service in "${services[@]}"; do
  arguments+=(--service "$service")
done

controller_command=watch-once
if [ "$mode" = force-recovery ]; then
  controller_command=force-recovery
fi
status=0
"$python" "$manager" \
  --project-name xframework \
  --deployment-uid "$(id -u)" \
  "$controller_command" \
  --env-file /opt/xframework/xeon-dev.env \
  --rotation-manager "$lkg_run/manage-bolt-phase0-rotation.py" \
  --runtime-verifier "$lkg_run/verify-bolt-phase0-runtime.py" \
  --recovery-gate-hook "$lkg_run/verify-bolt-phase0-qualification.py" \
  --python-executable "$python" \
  --docker-executable "$docker" \
  --hub-container-name "$hub_container" \
  --subprocess-timeout-seconds 900 \
  --stop-timeout-seconds 30 \
  "${arguments[@]}" || status=$?
if [ "$status" -ne 0 ]; then
  exit "$status"
fi
controller_completed=true
