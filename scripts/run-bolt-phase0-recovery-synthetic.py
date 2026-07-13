#!/usr/bin/env python3
"""Run the local finalized Bolt Phase 0 recovery synthetic fail-closed."""

from __future__ import annotations

import argparse
import base64
import concurrent.futures
import contextlib
import datetime as dt
import hashlib
import importlib.util
import json
import math
import os
import re
import shutil
import signal
import stat
import subprocess
import sys
import tempfile
import threading
import time
import types
import uuid
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable, Mapping, Sequence

try:
    import resource
except ImportError:  # pragma: no cover - exercised by Windows test imports
    resource = None  # type: ignore[assignment]


SAFE_PATH = "/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin"
FINAL_STAGE = "finalized"
SYNTHETIC_SERVICE = "bolt-phase0-synthetics"
SYNTHETIC_SCHEMA = "bolt-phase0-synthetic-evidence/v1"
CORE_SCHEMA = "bolt-phase0-synthetic-report/v1"
POST_RUN_SCHEMA = "bolt-phase0-post-run-evidence/v1"
REFRESH_SCHEMA = "bolt-phase0-token-refresh/v1"
MANIFEST_SCHEMA = "bolt-phase0-token-manifest/v1"
PROBE_SCHEMA = "bolt-phase0-probe-receipt/v1"
QUALIFICATION_SCHEMA = "xframework.bolt.phase0.qualification.v1"
PINS_SCHEMA = "xframework.bolt.phase0.image-pins.v2"

MAX_ENV_BYTES = 1024 * 1024
MAX_JSON_BYTES = 2 * 1024 * 1024
MAX_ARTIFACT_BYTES = 64 * 1024 * 1024
MAX_RUN_BYTES = 256 * 1024 * 1024
MAX_TOKEN_BYTES = 16 * 1024
MAX_CHILD_OUTPUT_BYTES = 8 * 1024 * 1024
MAX_RUN_FILES = 256
REFRESH_TIMEOUT_SECONDS = 120
CORE_TIMEOUT_SECONDS = 900
PROBE_TIMEOUT_SECONDS = 300
DOCKER_TIMEOUT_SECONDS = 60
PROBE_START_DELAY_SECONDS = 10
RECOVERY_TOTAL_TIMEOUT_SECONDS = 840
MAX_CLOCK_SKEW_SECONDS = 5

PROJECT_NAME = re.compile(r"^[a-z0-9][a-z0-9_-]{0,62}$")
RUN_DIRECTORY = re.compile(r"^([1-9][0-9]{0,31})-([1-9][0-9]{0,8})$")
ENV_NAME = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*$")
SAFE_ENV_VALUE = re.compile(r"^[A-Za-z0-9_./,:@%+=+-]*$")
FORBIDDEN_ENV_CHARACTERS = frozenset("`'\"#$\\;&|<>(){}[]*?!")
DIGEST_IMAGE = re.compile(r"^[a-z0-9][a-z0-9._:/-]*[a-z0-9]@sha256:[0-9a-f]{64}$")
HEX_SHA256 = re.compile(r"^[0-9a-f]{64}$")
MARKER = re.compile(
    r"^(?:[0-9a-fA-F]{32}|[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-"
    r"[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})$"
)
SAFE_PRINCIPAL = re.compile(r"^[A-Za-z0-9_.:-]{1,96}$")
CONTAINER_ID = re.compile(r"^[0-9a-f]{64}$")
SENSITIVE_ENV_NAME = re.compile(r"(?:SECRET|PASSWORD|API_KEY|TOKEN)$", re.IGNORECASE)

CURRENT_PURPOSES = ("communications", "user", "expiry")
RETIRED_PURPOSES = ("rejected_communications", "rejected_user")
REQUIRED_OPERATIONS = {
    "user_registration",
    "hostile_reserved_registration",
    "communications_registration",
    "identity_health_check",
    "transient_presence",
    "durable_offline_registration",
    "durable_offline_publish",
    "durable_ordered_replay",
    "durable_ack",
    "durable_no_redelivery",
    "durable_unregister",
    "token_expiry_disconnect",
}
PROBE_ASSERTIONS: dict[str, dict[str, Any]] = {
    "proxy-marker-scan": {
        "retainedStoreQueried": True,
        "matches": 0,
        "tokensSearched": 3,
        "markersSearched": 3,
    },
    "seq-marker-scan": {
        "retainedStoreQueried": True,
        "matches": 0,
        "tokensSearched": 3,
        "markersSearched": 3,
    },
    "trace-marker-scan": {
        "retainedStoreQueried": True,
        "matches": 0,
        "tokensSearched": 3,
        "markersSearched": 3,
    },
    "plaintext-rejection": {"plaintextRejected": True, "bearerSent": False},
    "old-generation-rejection": {
        "oldUserTokenRejected": True,
        "oldServiceTokenRejected": True,
        "oldClientSecretRejected": True,
        "currentHttpHealthPassed": True,
        "currentBoltHealthPassed": True,
    },
}

HOOK_KEYS = {
    "refresh": "BOLT_SYNTHETIC_TOKEN_REFRESH_COMMAND_PATH",
    "proxy": "BOLT_SYNTHETIC_PROXY_MARKER_SCAN_COMMAND_PATH",
    "seq": "BOLT_SYNTHETIC_SEQ_MARKER_SCAN_COMMAND_PATH",
    "trace": "BOLT_SYNTHETIC_TRACE_MARKER_SCAN_COMMAND_PATH",
    "plaintext": "BOLT_SYNTHETIC_PLAINTEXT_REJECTION_COMMAND_PATH",
    "old_generation": "BOLT_SYNTHETIC_OLD_GENERATION_REJECTION_COMMAND_PATH",
}
TOKEN_PATH_KEYS = {
    "communications": "BOLT_SYNTHETIC_COMMUNICATIONS_TOKEN_PATH",
    "user": "BOLT_SYNTHETIC_USER_TOKEN_PATH",
    "expiry": "BOLT_SYNTHETIC_EXPIRY_TOKEN_PATH",
    "rejected_communications": "BOLT_SYNTHETIC_REJECTED_COMMUNICATIONS_TOKEN_PATH",
    "rejected_user": "BOLT_SYNTHETIC_REJECTED_USER_TOKEN_PATH",
}
QUALIFIED_ARTIFACTS = (
    "docker-compose.yml",
    "pinned-compose.override.json",
    "image-pins.json",
    "qualification-evidence.json",
    "qualified-commit",
    "security-qualified",
    "bolt-tls-evidence.json",
    "identityserver-tls-evidence.json",
)
QUALIFIED_EXECUTABLES = {
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
}
APPROVED_RUN_ROOT = Path("/home/github-runner/xframework-deploy/runs")

ENFORCE_POSIX = os.name == "posix" and hasattr(os, "geteuid")


class RecoveryError(RuntimeError):
    """A deliberately non-secret recovery-hook failure."""


@dataclass(frozen=True)
class FileSnapshot:
    device: int
    inode: int
    size: int
    modified_ns: int
    mode: int
    uid: int
    sha256: str


@dataclass(frozen=True)
class TokenEvidence:
    purpose: str
    path: Path
    value: bytes
    marker: bytes
    expires_at: dt.datetime
    issuer: str
    snapshot: FileSnapshot

    def manifest_entry(self) -> dict[str, Any]:
        return {
            "purpose": self.purpose,
            "path": str(self.path),
            "sha256Prefix": hashlib.sha256(self.value).hexdigest()[:12],
            "expiresAtUtc": _timestamp(self.expires_at),
            "issuerUri": self.issuer,
            "marker": self.marker.decode("ascii"),
            "markerSha256Prefix": hashlib.sha256(self.marker).hexdigest()[:12],
            "identity": [
                self.snapshot.device,
                self.snapshot.inode,
                self.snapshot.size,
                self.snapshot.modified_ns,
                hashlib.sha256(self.value).hexdigest(),
            ],
        }


@dataclass(frozen=True)
class ProcessResult:
    returncode: int
    stdout: bytes
    stderr: bytes


ProcessRunner = Callable[[list[str], float, Mapping[str, str], Path], ProcessResult]
Sleeper = Callable[[float], None]
NowProvider = Callable[[], dt.datetime]


def _fail(code: str) -> None:
    raise RecoveryError(code)


def _timestamp(value: dt.datetime) -> str:
    if value.tzinfo is None:
        _fail("CLOCK")
    return value.astimezone(dt.timezone.utc).isoformat(timespec="milliseconds").replace("+00:00", "Z")


def _parse_timestamp(value: Any, code: str) -> dt.datetime:
    if not isinstance(value, str) or len(value) > 64:
        _fail(code)
    try:
        parsed = dt.datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError:
        _fail(code)
    if parsed.tzinfo is None or not math.isfinite(parsed.timestamp()):
        _fail(code)
    return parsed.astimezone(dt.timezone.utc)


def _canonical_path(value: str | Path, code: str) -> Path:
    raw = os.fspath(value)
    if not raw or not os.path.isabs(raw):
        _fail(code)
    absolute = os.path.abspath(raw)
    parts = Path(raw).parts
    if (
        any(part in {".", ".."} for part in parts)
        or os.path.normcase(os.path.normpath(raw)) != os.path.normcase(absolute)
        or os.path.normcase(os.path.realpath(raw)) != os.path.normcase(absolute)
    ):
        _fail(code)
    return Path(absolute)


def _validate_directory(
    path: Path,
    code: str,
    *,
    private: bool = True,
    sealed: bool = False,
) -> os.stat_result:
    path = _canonical_path(path, code)
    try:
        metadata = os.lstat(path)
    except OSError:
        _fail(code)
    if not stat.S_ISDIR(metadata.st_mode) or stat.S_ISLNK(metadata.st_mode):
        _fail(code)
    if ENFORCE_POSIX:
        mode = stat.S_IMODE(metadata.st_mode)
        if sealed:
            if metadata.st_uid != 0 or mode != 0o550:
                _fail(code)
        elif metadata.st_uid != os.geteuid():
            _fail(code)
        elif private and mode != 0o700:
            _fail(code)
        elif not private and mode & 0o022:
            _fail(code)
    return metadata


def _file_metadata(
    path: Path,
    code: str,
    *,
    maximum: int,
    allow_empty: bool = False,
    private: bool = True,
    executable: bool = False,
    sealed_mode: int | None = None,
) -> os.stat_result:
    path = _canonical_path(path, code)
    try:
        metadata = os.lstat(path)
    except OSError:
        _fail(code)
    if (
        not stat.S_ISREG(metadata.st_mode)
        or stat.S_ISLNK(metadata.st_mode)
        or metadata.st_nlink != 1
        or metadata.st_size > maximum
        or (metadata.st_size == 0 and not allow_empty)
    ):
        _fail(code)
    if ENFORCE_POSIX:
        mode = stat.S_IMODE(metadata.st_mode)
        if sealed_mode is not None:
            if metadata.st_uid != 0 or mode != sealed_mode:
                _fail(code)
        elif metadata.st_uid != os.geteuid():
            _fail(code)
        elif private and mode & 0o077:
            _fail(code)
        if sealed_mode is None and executable:
            if private and mode != 0o700:
                _fail(code)
            if not private and not mode & 0o111:
                _fail(code)
        elif sealed_mode is None and private and mode & 0o111:
            _fail(code)
    return metadata


def _read_file(
    path: Path,
    code: str,
    *,
    maximum: int,
    allow_empty: bool = False,
    private: bool = True,
    executable: bool = False,
    sealed_mode: int | None = None,
) -> tuple[bytes, FileSnapshot]:
    before = _file_metadata(
        path,
        code,
        maximum=maximum,
        allow_empty=allow_empty,
        private=private,
        executable=executable,
        sealed_mode=sealed_mode,
    )
    flags = os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0) | getattr(os, "O_BINARY", 0)
    try:
        descriptor = os.open(path, flags)
        try:
            current = os.fstat(descriptor)
            if (current.st_dev, current.st_ino) != (before.st_dev, before.st_ino):
                _fail(code)
            data = bytearray()
            while len(data) <= maximum:
                chunk = os.read(descriptor, min(64 * 1024, maximum + 1 - len(data)))
                if not chunk:
                    break
                data.extend(chunk)
        finally:
            os.close(descriptor)
    except RecoveryError:
        raise
    except OSError:
        _fail(code)
    raw = bytes(data)
    if len(raw) != current.st_size or len(raw) > maximum or (not raw and not allow_empty):
        _fail(code)
    return raw, FileSnapshot(
        current.st_dev,
        current.st_ino,
        current.st_size,
        current.st_mtime_ns,
        stat.S_IMODE(current.st_mode),
        current.st_uid,
        hashlib.sha256(raw).hexdigest(),
    )


def _verify_file(
    path: Path,
    expected: FileSnapshot,
    code: str,
    *,
    maximum: int,
    private: bool = True,
    sealed: bool = False,
) -> None:
    _, current = _read_file(
        path,
        code,
        maximum=maximum,
        allow_empty=expected.size == 0,
        private=private,
        executable=bool(expected.mode & 0o100),
        sealed_mode=expected.mode if sealed else None,
    )
    if current != expected:
        _fail(code)


def _json_pairs(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            _fail("JSON_DUPLICATE")
        result[key] = value
    return result


def _reject_constant(_: str) -> None:
    _fail("JSON_CONSTANT")


def _decode_json(raw: bytes, code: str) -> dict[str, Any]:
    if not raw or len(raw) > MAX_JSON_BYTES or raw.startswith(b"\xef\xbb\xbf") or b"\x00" in raw:
        _fail(code)
    try:
        value = json.loads(
            raw.decode("utf-8", errors="strict"),
            object_pairs_hook=_json_pairs,
            parse_constant=_reject_constant,
        )
    except (UnicodeDecodeError, json.JSONDecodeError, ValueError, RecoveryError):
        _fail(code)
    if not isinstance(value, dict):
        _fail(code)
    return value


def _load_private_json(
    path: Path, code: str, *, sealed: bool = False
) -> tuple[dict[str, Any], FileSnapshot]:
    raw, snapshot = _read_file(
        path,
        code,
        maximum=MAX_JSON_BYTES,
        sealed_mode=0o440 if sealed else None,
    )
    return _decode_json(raw, code), snapshot


def _parse_env(path: Path) -> tuple[dict[str, str], FileSnapshot]:
    raw, snapshot = _read_file(path, "ENV_FILE", maximum=MAX_ENV_BYTES)
    if raw.startswith(b"\xef\xbb\xbf") or b"\x00" in raw:
        _fail("ENV_FILE")
    try:
        text = raw.decode("utf-8", errors="strict")
    except UnicodeDecodeError:
        _fail("ENV_FILE")
    if "\r" in text.replace("\r\n", ""):
        _fail("ENV_SYNTAX")
    values: dict[str, str] = {}
    for line in text.replace("\r\n", "\n").split("\n"):
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        name, separator, value = line.partition("=")
        if (
            not separator
            or not ENV_NAME.fullmatch(name)
            or name in values
            or "#" in value
            or any(character in FORBIDDEN_ENV_CHARACTERS for character in value)
            or not SAFE_ENV_VALUE.fullmatch(value)
        ):
            _fail("ENV_SYNTAX")
        values[name] = value
    return values, snapshot


def _required(values: Mapping[str, str], key: str) -> str:
    value = values.get(key)
    if not value:
        _fail(f"CONFIG_{key}")
    return value


def _env_path(values: Mapping[str, str], key: str) -> Path:
    return _canonical_path(_required(values, key), f"CONFIG_{key}")


def _is_sealed_production_run(path: Path) -> bool:
    try:
        relative = path.relative_to(APPROVED_RUN_ROOT)
    except ValueError:
        return False
    return len(relative.parts) == 1


def _snapshot_run_directory(path: Path, *, sealed: bool) -> dict[str, FileSnapshot]:
    _validate_directory(path, "RUN_DIRECTORY", sealed=sealed)
    snapshots: dict[str, FileSnapshot] = {}
    total = 0
    try:
        entries = sorted(os.scandir(path), key=lambda item: item.name)
    except OSError:
        _fail("RUN_DIRECTORY")
    if not entries or len(entries) > MAX_RUN_FILES:
        _fail("RUN_INVENTORY")
    for entry in entries:
        if entry.name in snapshots or "/" in entry.name or "\\" in entry.name:
            _fail("RUN_INVENTORY")
        entry_path = path / entry.name
        _, snapshot = _read_file(
            entry_path,
            "RUN_ARTIFACT",
            maximum=MAX_ARTIFACT_BYTES,
            allow_empty=True,
            sealed_mode=(0o550 if entry.name in QUALIFIED_EXECUTABLES else 0o440)
            if sealed
            else None,
        )
        total += snapshot.size
        if total > MAX_RUN_BYTES:
            _fail("RUN_INVENTORY")
        snapshots[entry.name] = snapshot
    if not set(QUALIFIED_ARTIFACTS).issubset(snapshots):
        _fail("RUN_INVENTORY")
    return snapshots


def _verify_run_directory(
    path: Path, expected: Mapping[str, FileSnapshot], *, sealed: bool
) -> None:
    current_names = {entry.name for entry in os.scandir(path)}
    if current_names != set(expected):
        _fail("ARTIFACT_MUTATION")
    for name, snapshot in expected.items():
        _verify_file(
            path / name,
            snapshot,
            "ARTIFACT_MUTATION",
            maximum=MAX_ARTIFACT_BYTES,
            sealed=sealed,
        )


def _validate_output_target(path: Path, run_directory: Path) -> FileSnapshot | None:
    path = _canonical_path(path, "OUTPUT")
    parent = path.parent
    _validate_directory(parent, "OUTPUT_PARENT")
    try:
        path.relative_to(run_directory)
    except ValueError:
        pass
    else:
        _fail("OUTPUT_IN_QUALIFIED_RUN")
    if not os.path.lexists(path):
        return None
    _, snapshot = _read_file(path, "OUTPUT", maximum=0, allow_empty=True)
    return snapshot


def _verify_output_target(path: Path, expected: FileSnapshot | None) -> None:
    if expected is None:
        if os.path.lexists(path):
            _fail("OUTPUT_CHANGED")
        return
    _verify_file(path, expected, "OUTPUT_CHANGED", maximum=0)


def _mount_path(value: str) -> str:
    return value.replace("\\040", " ").replace("\\011", "\t").replace("\\134", "\\")


def _is_tmpfs(path: Path) -> bool:
    try:
        lines = Path("/proc/self/mountinfo").read_text(encoding="utf-8").splitlines()
    except OSError:
        return False
    selected: tuple[int, str] | None = None
    candidate = str(path)
    for line in lines:
        before, separator, after = line.partition(" - ")
        if not separator:
            continue
        fields = before.split()
        tail = after.split()
        if len(fields) < 5 or not tail:
            continue
        mount_point = _mount_path(fields[4])
        if candidate == mount_point or candidate.startswith(mount_point.rstrip("/") + "/"):
            choice = (len(mount_point), tail[0])
            if selected is None or choice[0] > selected[0]:
                selected = choice
    return selected is not None and selected[1] == "tmpfs"


@contextlib.contextmanager
def _private_workspace(root: Path, *, require_tmpfs: bool) -> Any:
    root = _canonical_path(root, "TMPFS_ROOT")
    if require_tmpfs and not _is_tmpfs(root):
        _fail("TMPFS_REQUIRED")
    if not root.is_dir():
        _fail("TMPFS_ROOT")
    directory = Path(tempfile.mkdtemp(prefix="bolt-phase0-recovery-", dir=root))
    os.chmod(directory, 0o700)
    _validate_directory(directory, "WORK_DIRECTORY")
    try:
        yield directory
    finally:
        with contextlib.suppress(OSError):
            for child in directory.iterdir():
                if child.is_file() and not child.is_symlink():
                    with child.open("r+b", buffering=0) as stream:
                        stream.truncate(0)
            shutil.rmtree(directory)


class QuarantinedRunner:
    def __init__(self, workspace: Path) -> None:
        self.workspace = workspace
        self._lock = threading.Lock()
        self._counter = 0

    def __call__(
        self,
        command: list[str],
        timeout_seconds: float,
        environment: Mapping[str, str],
        cwd: Path,
    ) -> ProcessResult:
        if not command or timeout_seconds <= 0:
            _fail("SUBPROCESS_CONFIGURATION")
        with self._lock:
            self._counter += 1
            prefix = f"child-{self._counter}"
        stdout_path = self.workspace / f"{prefix}.stdout"
        stderr_path = self.workspace / f"{prefix}.stderr"

        def limit_child() -> None:
            os.umask(0o077)
            if resource is not None:
                resource.setrlimit(
                    resource.RLIMIT_FSIZE,
                    (MAX_CHILD_OUTPUT_BYTES, MAX_CHILD_OUTPUT_BYTES),
                )

        try:
            with stdout_path.open("xb") as stdout, stderr_path.open("xb") as stderr:
                os.chmod(stdout_path, 0o600)
                os.chmod(stderr_path, 0o600)
                process = subprocess.Popen(
                    command,
                    stdin=subprocess.DEVNULL,
                    stdout=stdout,
                    stderr=stderr,
                    env=dict(environment),
                    cwd=cwd,
                    close_fds=True,
                    start_new_session=True,
                    preexec_fn=limit_child if ENFORCE_POSIX else None,
                )
                try:
                    returncode = process.wait(timeout=timeout_seconds)
                except subprocess.TimeoutExpired:
                    if ENFORCE_POSIX:
                        with contextlib.suppress(ProcessLookupError):
                            os.killpg(process.pid, signal.SIGTERM)
                    else:
                        process.terminate()
                    try:
                        process.wait(timeout=5)
                    except subprocess.TimeoutExpired:
                        if ENFORCE_POSIX:
                            with contextlib.suppress(ProcessLookupError):
                                os.killpg(process.pid, signal.SIGKILL)
                        else:
                            process.kill()
                        process.wait(timeout=5)
                    _fail("SUBPROCESS_TIMEOUT")
            stdout_raw = stdout_path.read_bytes()
            stderr_raw = stderr_path.read_bytes()
        except RecoveryError:
            raise
        except (OSError, subprocess.SubprocessError):
            _fail("SUBPROCESS")
        if len(stdout_raw) > MAX_CHILD_OUTPUT_BYTES or len(stderr_raw) > MAX_CHILD_OUTPUT_BYTES:
            _fail("SUBPROCESS_OUTPUT")
        return ProcessResult(returncode, stdout_raw, stderr_raw)


class DeadlineRunner:
    def __init__(self, inner: ProcessRunner, maximum_seconds: float) -> None:
        self.inner = inner
        self.deadline = time.monotonic() + maximum_seconds

    def __call__(
        self,
        command: list[str],
        timeout_seconds: float,
        environment: Mapping[str, str],
        cwd: Path,
    ) -> ProcessResult:
        remaining = self.deadline - time.monotonic()
        if remaining <= 0:
            _fail("RECOVERY_TIMEOUT")
        return self.inner(command, min(timeout_seconds, remaining), environment, cwd)


def _clean_environment(**extra: str) -> dict[str, str]:
    home = os.path.expanduser("~")
    result = {
        "PATH": SAFE_PATH,
        "HOME": home if os.path.isabs(home) else "/tmp",
        "LANG": "C",
        "LC_ALL": "C",
        "COMPOSE_ANSI": "never",
    }
    result.update(extra)
    return result


def _run(
    runner: ProcessRunner,
    command: list[str],
    timeout: float,
    environment: Mapping[str, str],
    workspace: Path,
    sensitive: Sequence[bytes],
    *,
    allow_stdout: bool,
    allow_stderr: bool = False,
) -> ProcessResult:
    encoded_command = "\0".join(command).encode("utf-8", errors="strict")
    if any(value and value in encoded_command for value in sensitive):
        _fail("SECRET_ARGUMENT")
    result = runner(command, timeout, environment, workspace)
    if not isinstance(result, ProcessResult):
        _fail("SUBPROCESS_RESULT")
    if result.returncode != 0:
        _fail("SUBPROCESS_FAILED")
    if (result.stdout and not allow_stdout) or (result.stderr and not allow_stderr):
        _fail("UNEXPECTED_CHILD_OUTPUT")
    if len(result.stdout) > MAX_CHILD_OUTPUT_BYTES or len(result.stderr) > MAX_CHILD_OUTPUT_BYTES:
        _fail("SUBPROCESS_OUTPUT")
    return result


def _validate_program(
    path: Path, code: str, *, private: bool, sealed: bool = False
) -> FileSnapshot:
    _, snapshot = _read_file(
        path,
        code,
        maximum=MAX_ARTIFACT_BYTES,
        private=private,
        executable=True,
        sealed_mode=0o550 if sealed else None,
    )
    if ENFORCE_POSIX and not private:
        metadata = os.lstat(path)
        if metadata.st_uid not in {0, os.geteuid()} or stat.S_IMODE(metadata.st_mode) & 0o022:
            _fail(code)
        if not stat.S_IMODE(metadata.st_mode) & 0o111:
            _fail(code)
    return snapshot


def _resolve_docker() -> Path:
    candidate = shutil.which("docker", path=SAFE_PATH)
    if not candidate:
        _fail("DOCKER")
    path = _canonical_path(candidate, "DOCKER")
    _validate_program(path, "DOCKER", private=False)
    return path


def _load_qualification_module(
    run_directory: Path, *, sealed: bool
) -> tuple[types.ModuleType, Path, FileSnapshot]:
    script = Path(__file__).absolute()
    candidates = (
        script.with_name("verify-bolt-phase0-qualification.py"),
        script.parent.parent / "verify-bolt-phase0-qualification.py",
    )
    existing = [candidate for candidate in candidates if candidate.is_file()]
    if len(existing) != 1:
        _fail("QUALIFICATION_VALIDATOR")
    path = _canonical_path(existing[0], "QUALIFICATION_VALIDATOR")
    if sealed and path != run_directory / "verify-bolt-phase0-qualification.py":
        _fail("QUALIFICATION_VALIDATOR")
    raw, snapshot = _read_file(
        path,
        "QUALIFICATION_VALIDATOR",
        maximum=MAX_ARTIFACT_BYTES,
        private=False,
        sealed_mode=0o550 if sealed else None,
    )
    if not raw.startswith(b"#!/usr/bin/env python3"):
        _fail("QUALIFICATION_VALIDATOR")
    if ENFORCE_POSIX:
        metadata = os.lstat(path)
        if metadata.st_uid not in {0, os.geteuid()} or stat.S_IMODE(metadata.st_mode) & 0o022:
            _fail("QUALIFICATION_VALIDATOR")
    name = f"bolt_phase0_qualification_{snapshot.sha256[:16]}"
    specification = importlib.util.spec_from_file_location(name, path)
    if specification is None or specification.loader is None:
        _fail("QUALIFICATION_VALIDATOR")
    module = importlib.util.module_from_spec(specification)
    sys.modules[name] = module
    try:
        specification.loader.exec_module(module)
    except BaseException:
        sys.modules.pop(name, None)
        _fail("QUALIFICATION_VALIDATOR")
    return module, path, snapshot


def _validate_hooks(
    values: Mapping[str, str], run_directory: Path, *, sealed: bool
) -> tuple[dict[str, Path], dict[Path, FileSnapshot]]:
    hooks = {name: _env_path(values, key) for name, key in HOOK_KEYS.items()}
    if hooks["proxy"] != hooks["seq"] or hooks["proxy"] != hooks["trace"]:
        _fail("MARKER_HOOK_IDENTITY")
    if hooks["plaintext"] != hooks["old_generation"]:
        _fail("OPERATIONAL_HOOK_IDENTITY")
    expected_names = {
        "refresh": "refresh-bolt-phase0-synthetic-tokens.py",
        "proxy": "run-bolt-phase0-marker-scan.py",
        "plaintext": "run-bolt-phase0-operational-probe.py",
    }
    for name, expected in expected_names.items():
        if hooks[name].name != expected:
            _fail("HOOK_IDENTITY")
    parents = {hooks[name].parent for name in expected_names}
    if len(parents) != 1 or (sealed and parents != {run_directory}):
        _fail("HOOK_DIRECTORY")
    _validate_directory(next(iter(parents)), "HOOK_DIRECTORY", sealed=sealed)
    snapshots: dict[Path, FileSnapshot] = {}
    for path in set(hooks.values()):
        snapshots[path] = _validate_program(
            path, "HOOK", private=not sealed, sealed=sealed
        )
    return hooks, snapshots


def _read_token(
    path: Path,
    purpose: str,
    now: dt.datetime,
    *,
    minimum_lifetime: int = 60,
) -> TokenEvidence:
    raw, snapshot = _read_file(path, "TOKEN_FILE", maximum=MAX_TOKEN_BYTES)
    token = raw.rstrip(b"\r\n")
    if raw not in {token, token + b"\n", token + b"\r\n"} or len(token) <= 32:
        _fail("TOKEN_FILE")
    try:
        text = token.decode("ascii", errors="strict")
    except UnicodeDecodeError:
        _fail("TOKEN_FILE")
    parts = text.split(".")
    if len(parts) != 3 or any(not part for part in parts):
        _fail("TOKEN_SHAPE")
    try:
        payload = parts[1] + "=" * (-len(parts[1]) % 4)
        claims = _decode_json(base64.urlsafe_b64decode(payload.encode("ascii")), "TOKEN_CLAIMS")
    except (ValueError, UnicodeEncodeError):
        _fail("TOKEN_CLAIMS")
    expiration = claims.get("exp")
    issuer = claims.get("iss")
    marker = claims.get("jti")
    if type(expiration) is not int or expiration <= 0:
        _fail("TOKEN_CLAIMS")
    if not isinstance(issuer, str) or len(issuer) > 512 or not issuer.startswith("https://"):
        _fail("TOKEN_CLAIMS")
    if not isinstance(marker, str) or not MARKER.fullmatch(marker) or int(marker.replace("-", ""), 16) == 0:
        _fail("TOKEN_CLAIMS")
    expires_at = dt.datetime.fromtimestamp(expiration, dt.timezone.utc)
    if purpose in {"communications", "user"} and expires_at < now + dt.timedelta(
        seconds=minimum_lifetime
    ):
        _fail("TOKEN_LIFETIME")
    if purpose == "expiry" and not (
        now + dt.timedelta(seconds=1) < expires_at <= now + dt.timedelta(seconds=570)
    ):
        _fail("TOKEN_LIFETIME")
    return TokenEvidence(purpose, path, token, marker.encode("ascii"), expires_at, issuer, snapshot)


def _write_private_json(path: Path, document: Mapping[str, Any]) -> None:
    payload = (json.dumps(document, sort_keys=True, separators=(",", ":"), allow_nan=False) + "\n").encode()
    if len(payload) > MAX_JSON_BYTES:
        _fail("JSON_SIZE")
    descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    try:
        with os.fdopen(descriptor, "wb", closefd=True) as stream:
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
    except BaseException:
        with contextlib.suppress(OSError):
            os.unlink(path)
        raise


def _load_refresh_receipt(
    path: Path,
    tokens: Mapping[str, TokenEvidence],
    refresh_started: dt.datetime,
    now: dt.datetime,
) -> dict[str, Any]:
    receipt, _ = _load_private_json(path, "REFRESH_RECEIPT")
    expected_keys = {
        "schemaVersion",
        "status",
        "issuerUri",
        "principalReference",
        "refreshedAtUtc",
        "tokenExpirationsUtc",
    }
    if set(receipt) != expected_keys or receipt.get("schemaVersion") != REFRESH_SCHEMA or receipt.get("status") != "passed":
        _fail("REFRESH_RECEIPT")
    issuers = {token.issuer for token in tokens.values()}
    if len(issuers) != 1 or receipt.get("issuerUri") != next(iter(issuers)):
        _fail("REFRESH_BINDING")
    principal = receipt.get("principalReference")
    if not isinstance(principal, str) or not SAFE_PRINCIPAL.fullmatch(principal):
        _fail("REFRESH_RECEIPT")
    refreshed = _parse_timestamp(receipt.get("refreshedAtUtc"), "REFRESH_RECEIPT")
    if refreshed < refresh_started - dt.timedelta(seconds=MAX_CLOCK_SKEW_SECONDS) or refreshed > now + dt.timedelta(seconds=MAX_CLOCK_SKEW_SECONDS):
        _fail("REFRESH_RECEIPT")
    expirations = receipt.get("tokenExpirationsUtc")
    if not isinstance(expirations, dict) or set(expirations) != set(tokens):
        _fail("REFRESH_BINDING")
    for purpose, token in tokens.items():
        if _parse_timestamp(expirations.get(purpose), "REFRESH_RECEIPT") != token.expires_at:
            _fail("REFRESH_BINDING")
    return receipt


def _manifest(
    receipt: Mapping[str, Any],
    tokens: Sequence[TokenEvidence],
    minimum_lifetime: int,
) -> dict[str, Any]:
    return {
        "schemaVersion": MANIFEST_SCHEMA,
        "issuerUri": receipt["issuerUri"],
        "principalReference": receipt["principalReference"],
        "refreshedAtUtc": receipt["refreshedAtUtc"],
        "minimumRemainingLifetimeSeconds": minimum_lifetime,
        "expiryEnabled": True,
        "tokens": [token.manifest_entry() for token in tokens],
    }


def _scan_sensitive(data: bytes, sensitive: Sequence[bytes], code: str) -> None:
    if any(value and value in data for value in sensitive):
        _fail(code)


def _validate_core(raw: bytes, tokens: Mapping[str, TokenEvidence]) -> tuple[dict[str, Any], dt.datetime, dt.datetime]:
    core = _decode_json(raw, "CORE_REPORT")
    expected_keys = {
        "schemaVersion",
        "runId",
        "tokenSha256Prefixes",
        "startedAtUtc",
        "completedAtUtc",
        "target",
        "status",
        "timings",
        "operations",
    }
    if (
        set(core) != expected_keys
        or core.get("schemaVersion") != CORE_SCHEMA
        or core.get("status") != "passed"
        or core.get("target") != "wss://bolt-hub:8443/bolt/ws"
    ):
        _fail("CORE_REPORT")
    try:
        run_id = uuid.UUID(str(core.get("runId")))
    except (ValueError, AttributeError):
        _fail("CORE_REPORT")
    if run_id.int == 0 or str(run_id) != core.get("runId"):
        _fail("CORE_REPORT")
    expected_prefixes = {
        purpose: hashlib.sha256(tokens[purpose].value).hexdigest()[:12]
        for purpose in CURRENT_PURPOSES
    }
    if core.get("tokenSha256Prefixes") != expected_prefixes:
        _fail("CORE_TOKEN_BINDING")
    started = _parse_timestamp(core.get("startedAtUtc"), "CORE_TIME")
    completed = _parse_timestamp(core.get("completedAtUtc"), "CORE_TIME")
    if completed < started:
        _fail("CORE_TIME")
    operations = core.get("operations")
    if not isinstance(operations, list):
        _fail("CORE_OPERATIONS")
    names = [item.get("name") for item in operations if isinstance(item, dict)]
    if len(names) != len(set(names)) or not REQUIRED_OPERATIONS.issubset(names):
        _fail("CORE_OPERATIONS")
    return core, started, completed


def _load_probe_receipt(
    path: Path,
    kind: str,
    core_started: dt.datetime,
    core_completed: dt.datetime,
) -> dict[str, Any]:
    receipt, _ = _load_private_json(path, "PROBE_RECEIPT")
    if (
        set(receipt) != {"schemaVersion", "probe", "status", "startedAtUtc", "completedAtUtc", "assertions"}
        or receipt.get("schemaVersion") != PROBE_SCHEMA
        or receipt.get("probe") != kind
        or receipt.get("status") != "passed"
        or receipt.get("assertions") != PROBE_ASSERTIONS[kind]
    ):
        _fail("PROBE_RECEIPT")
    started = _parse_timestamp(receipt.get("startedAtUtc"), "PROBE_TIME")
    completed = _parse_timestamp(receipt.get("completedAtUtc"), "PROBE_TIME")
    if (
        completed < started
        or started < core_started - dt.timedelta(seconds=MAX_CLOCK_SKEW_SECONDS)
        or completed > core_completed + dt.timedelta(seconds=MAX_CLOCK_SKEW_SECONDS)
    ):
        _fail("PROBE_TIME")
    return receipt


def _atomic_write(path: Path, payload: bytes, expected: FileSnapshot | None) -> None:
    if len(payload) > MAX_JSON_BYTES:
        _fail("OUTPUT_SIZE")
    _verify_output_target(path, expected)
    descriptor = -1
    temporary = ""
    try:
        descriptor, temporary = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
        os.fchmod(descriptor, 0o600)
        written = 0
        while written < len(payload):
            count = os.write(descriptor, payload[written:])
            if count <= 0:
                _fail("OUTPUT_WRITE")
            written += count
        os.fsync(descriptor)
        os.close(descriptor)
        descriptor = -1
        _verify_output_target(path, expected)
        os.replace(temporary, path)
        temporary = ""
        os.chmod(path, 0o600)
        if hasattr(os, "O_DIRECTORY"):
            directory = os.open(path.parent, os.O_RDONLY | os.O_DIRECTORY)
            try:
                os.fsync(directory)
            finally:
                os.close(directory)
    except RecoveryError:
        raise
    except OSError:
        _fail("OUTPUT_WRITE")
    finally:
        if descriptor >= 0:
            os.close(descriptor)
        if temporary:
            with contextlib.suppress(OSError):
                os.unlink(temporary)


def _compose_command(
    docker: Path,
    env_file: Path,
    run_directory: Path,
    project_name: str,
) -> list[str]:
    return [
        str(docker),
        "compose",
        "--profile",
        "phase0-verification",
        "--env-file",
        str(env_file),
        "-f",
        str(run_directory / "docker-compose.yml"),
        "-f",
        str(run_directory / "pinned-compose.override.json"),
        "--project-name",
        project_name,
    ]


def _probe_environment(
    env_file: Path,
    manifest: Path,
    receipt: Path,
    kind: str,
) -> dict[str, str]:
    return _clean_environment(
        XFRAMEWORK_ENV_FILE=str(env_file),
        BOLT_SYNTHETIC_TOKEN_MANIFEST=str(manifest),
        BOLT_SYNTHETIC_PROBE_RECEIPT=str(receipt),
        BOLT_SYNTHETIC_PROBE_KIND=kind,
        BOLT_SYNTHETIC_STAGE=FINAL_STAGE,
    )


def run_recovery_synthetic(
    env_file: Path,
    project_name: str,
    run_directory: Path,
    stage: str,
    output: Path,
    *,
    runner: ProcessRunner | None = None,
    sleeper: Sleeper = time.sleep,
    now_provider: NowProvider = lambda: dt.datetime.now(dt.timezone.utc),
    tmpfs_root: Path = Path("/dev/shm"),
    require_tmpfs: bool = True,
    qualification_module: types.ModuleType | Any | None = None,
    docker_path: Path | None = None,
) -> dict[str, Any]:
    if stage != FINAL_STAGE:
        _fail("STAGE")
    if not PROJECT_NAME.fullmatch(project_name):
        _fail("PROJECT")
    env_file = _canonical_path(env_file, "ENV_FILE")
    run_directory = _canonical_path(run_directory, "RUN_DIRECTORY")
    output = _canonical_path(output, "OUTPUT")
    identity = RUN_DIRECTORY.fullmatch(run_directory.name)
    if identity is None:
        _fail("RUN_IDENTITY")
    run_id, attempt_raw = identity.groups()
    attempt = int(attempt_raw)
    sealed_run = _is_sealed_production_run(run_directory)
    output_snapshot = _validate_output_target(output, run_directory)
    values, env_snapshot = _parse_env(env_file)
    if _required(values, "BOLT_SYNTHETIC_COMPOSE_PROJECT_NAME") != project_name:
        _fail("PROJECT_BINDING")

    loaded_validator = qualification_module is None
    if loaded_validator:
        qualification_module, validator_path, validator_snapshot = _load_qualification_module(
            run_directory, sealed=sealed_run
        )
    else:
        validator_path = None
        validator_snapshot = None
    try:
        qualification = qualification_module.qualification_evidence_for_recovery(
            run_directory, run_id, attempt
        )
    except BaseException:
        _fail("QUALIFIED_RUN")
    if qualification.get("schema") != QUALIFICATION_SCHEMA or qualification.get("status") != "passed":
        _fail("QUALIFIED_RUN")
    run_snapshot = _snapshot_run_directory(run_directory, sealed=sealed_run)

    pins_document, _ = _load_private_json(
        run_directory / "image-pins.json", "IMAGE_PINS", sealed=sealed_run
    )
    override_document, _ = _load_private_json(
        run_directory / "pinned-compose.override.json", "PIN_OVERRIDE", sealed=sealed_run
    )
    try:
        pin_time = qualification_module.parse_timestamp(
            pins_document.get("generated_at_utc"), "invalid-image-pins"
        )
        pins, _ = qualification_module.validate_image_pins(
            pins_document, qualification["source_commit"], pin_time, 60
        )
        qualification_module.validate_override(override_document, pins)
    except BaseException:
        _fail("IMAGE_BINDING")
    synthetic_pin = pins.get(SYNTHETIC_SERVICE)
    if not isinstance(synthetic_pin, str) or not DIGEST_IMAGE.fullmatch(synthetic_pin):
        _fail("MUTABLE_IMAGE")
    override_image = (
        (override_document.get("services") or {}).get(SYNTHETIC_SERVICE) or {}
    ).get("image")
    if override_image != synthetic_pin:
        _fail("IMAGE_BINDING")

    hooks, hook_snapshots = _validate_hooks(values, run_directory, sealed=sealed_run)
    token_paths = {purpose: _env_path(values, key) for purpose, key in TOKEN_PATH_KEYS.items()}
    if len(set(token_paths.values())) != len(token_paths):
        _fail("TOKEN_PATH_ALIAS")
    pre_refresh: dict[str, FileSnapshot | None] = {}
    for purpose in CURRENT_PURPOSES:
        path = token_paths[purpose]
        if os.path.lexists(path):
            _, pre_refresh[purpose] = _read_file(
                path, "TOKEN_DESTINATION", maximum=MAX_TOKEN_BYTES, allow_empty=True
            )
        else:
            _validate_directory(path.parent, "TOKEN_PARENT")
            pre_refresh[purpose] = None
    retired_secret_path = _env_path(values, "BOLT_SYNTHETIC_REJECTED_CLIENT_SECRET_PATH")
    retired_secret, retired_secret_snapshot = _read_file(
        retired_secret_path, "RETIRED_SECRET", maximum=MAX_TOKEN_BYTES
    )

    minimum_lifetime_raw = _required(values, "BOLT_SYNTHETIC_MIN_TOKEN_LIFETIME_SECONDS")
    if not minimum_lifetime_raw.isdigit() or not 60 <= int(minimum_lifetime_raw) <= 3600:
        _fail("TOKEN_LIFETIME")
    minimum_lifetime = int(minimum_lifetime_raw)
    docker = _canonical_path(docker_path, "DOCKER") if docker_path else _resolve_docker()
    if docker_path:
        _validate_program(docker, "DOCKER", private=False)

    started_at = now_provider().astimezone(dt.timezone.utc)
    with _private_workspace(tmpfs_root, require_tmpfs=require_tmpfs) as workspace:
        active_runner = DeadlineRunner(
            runner or QuarantinedRunner(workspace), RECOVERY_TOTAL_TIMEOUT_SECONDS
        )
        refresh_receipt_path = workspace / "refresh-receipt.json"
        refresh_environment = _clean_environment(
            XFRAMEWORK_ENV_FILE=str(env_file),
            BOLT_SYNTHETIC_REFRESH_RECEIPT=str(refresh_receipt_path),
            BOLT_SYNTHETIC_EXPIRY_ENABLED="true",
            BOLT_SYNTHETIC_STAGE=FINAL_STAGE,
        )
        refresh_started = now_provider().astimezone(dt.timezone.utc)
        _run(
            active_runner,
            [str(hooks["refresh"])],
            REFRESH_TIMEOUT_SECONDS,
            refresh_environment,
            workspace,
            (),
            allow_stdout=False,
        )
        token_now = now_provider().astimezone(dt.timezone.utc)
        current_tokens = {
            purpose: _read_token(
                token_paths[purpose],
                purpose,
                token_now,
                minimum_lifetime=minimum_lifetime,
            )
            for purpose in CURRENT_PURPOSES
        }
        for purpose, token in current_tokens.items():
            if pre_refresh[purpose] is not None and token.snapshot == pre_refresh[purpose]:
                _fail("TOKEN_NOT_REFRESHED")
        refresh_receipt = _load_refresh_receipt(
            refresh_receipt_path, current_tokens, refresh_started, token_now
        )
        retired_tokens = {
            purpose: _read_token(token_paths[purpose], purpose, token_now)
            for purpose in RETIRED_PURPOSES
        }
        all_tokens = {**current_tokens, **retired_tokens}
        if len({token.value for token in all_tokens.values()}) != len(all_tokens) or len(
            {token.marker for token in all_tokens.values()}
        ) != len(all_tokens):
            _fail("TOKEN_DISTINCTNESS")

        current_manifest_path = workspace / "current-token-manifest.json"
        full_manifest_path = workspace / "full-token-manifest.json"
        _write_private_json(
            current_manifest_path,
            _manifest(refresh_receipt, [current_tokens[name] for name in CURRENT_PURPOSES], minimum_lifetime),
        )
        _write_private_json(
            full_manifest_path,
            _manifest(
                refresh_receipt,
                [all_tokens[name] for name in (*CURRENT_PURPOSES, *RETIRED_PURPOSES)],
                minimum_lifetime,
            ),
        )

        sensitive: list[bytes] = [
            *(token.value for token in all_tokens.values()),
            *(token.marker for token in all_tokens.values()),
            retired_secret.strip(),
        ]
        for name, value in values.items():
            if SENSITIVE_ENV_NAME.search(name) and not name.endswith("_PATH") and value:
                sensitive.append(value.encode("utf-8"))

        inspect = _run(
            active_runner,
            [str(docker), "image", "inspect", "--format", "{{json .RepoDigests}}", synthetic_pin],
            DOCKER_TIMEOUT_SECONDS,
            _clean_environment(),
            workspace,
            sensitive,
            allow_stdout=True,
        )
        try:
            repo_digests = json.loads(inspect.stdout.decode("utf-8", errors="strict"))
        except (UnicodeDecodeError, json.JSONDecodeError):
            _fail("LOCAL_IMAGE")
        if not isinstance(repo_digests, list) or synthetic_pin not in repo_digests:
            _fail("LOCAL_IMAGE")

        compose = _compose_command(docker, env_file, run_directory, project_name)
        core_command = [
            *compose,
            "run",
            "--rm",
            "--no-deps",
            "--pull",
            "never",
            "-e",
            "BOLT_SYNTHETIC_EXPIRY_MAX_WAIT_SECONDS=600",
            SYNTHETIC_SERVICE,
        ]
        core_environment = _clean_environment()
        receipt_paths = {
            kind: workspace / f"probe-{kind}.json" for kind in PROBE_ASSERTIONS
        }

        with concurrent.futures.ThreadPoolExecutor(max_workers=1) as core_pool:
            core_future = core_pool.submit(
                _run,
                active_runner,
                core_command,
                CORE_TIMEOUT_SECONDS,
                core_environment,
                workspace,
                sensitive,
                allow_stdout=True,
            )
            sleeper(PROBE_START_DELAY_SECONDS)
            if core_future.done():
                core_future.result()
                _fail("CORE_PROBE_INTERVAL")

            probe_specs = {
                "proxy-marker-scan": (hooks["proxy"], current_manifest_path),
                "seq-marker-scan": (hooks["seq"], current_manifest_path),
                "trace-marker-scan": (hooks["trace"], current_manifest_path),
                "plaintext-rejection": (hooks["plaintext"], current_manifest_path),
                "old-generation-rejection": (hooks["old_generation"], full_manifest_path),
            }
            with concurrent.futures.ThreadPoolExecutor(max_workers=len(probe_specs)) as probe_pool:
                probe_futures = {
                    kind: probe_pool.submit(
                        _run,
                        active_runner,
                        [str(hook)],
                        PROBE_TIMEOUT_SECONDS,
                        _probe_environment(env_file, manifest, receipt_paths[kind], kind),
                        workspace,
                        sensitive,
                        allow_stdout=False,
                    )
                    for kind, (hook, manifest) in probe_specs.items()
                }
                for future in probe_futures.values():
                    future.result()
            core_result = core_future.result()

        _scan_sensitive(core_result.stdout + core_result.stderr, sensitive, "SECRET_OUTPUT")
        if core_result.stderr:
            _fail("CORE_STDERR")
        core, core_started, core_completed = _validate_core(core_result.stdout, current_tokens)
        receipts = {
            kind: _load_probe_receipt(path, kind, core_started, core_completed)
            for kind, path in receipt_paths.items()
        }

        ps_result = _run(
            active_runner,
            [*compose, "ps", "-q"],
            DOCKER_TIMEOUT_SECONDS,
            _clean_environment(),
            workspace,
            sensitive,
            allow_stdout=True,
        )
        try:
            container_ids = ps_result.stdout.decode("ascii", errors="strict").splitlines()
        except UnicodeDecodeError:
            _fail("CONTAINER_INVENTORY")
        if (
            not container_ids
            or len(container_ids) > 64
            or len(container_ids) != len(set(container_ids))
            or any(not CONTAINER_ID.fullmatch(identifier) for identifier in container_ids)
        ):
            _fail("CONTAINER_INVENTORY")
        for container_id in container_ids:
            logs = _run(
                active_runner,
                [str(docker), "logs", "--since", _timestamp(refresh_started), container_id],
                DOCKER_TIMEOUT_SECONDS,
                _clean_environment(),
                workspace,
                sensitive,
                allow_stdout=True,
                allow_stderr=True,
            )
            _scan_sensitive(logs.stdout + logs.stderr, sensitive, "RETAINED_SECRET")

        for token in all_tokens.values():
            _verify_file(token.path, token.snapshot, "TOKEN_MUTATION", maximum=MAX_TOKEN_BYTES)
        _verify_file(env_file, env_snapshot, "ENV_MUTATION", maximum=MAX_ENV_BYTES)
        _verify_file(
            retired_secret_path,
            retired_secret_snapshot,
            "RETIRED_SECRET_MUTATION",
            maximum=MAX_TOKEN_BYTES,
        )
        for hook, snapshot in hook_snapshots.items():
            _verify_file(
                hook,
                snapshot,
                "HOOK_MUTATION",
                maximum=MAX_ARTIFACT_BYTES,
                private=not sealed_run,
                sealed=sealed_run,
            )
        if validator_path is not None and validator_snapshot is not None:
            _verify_file(
                validator_path,
                validator_snapshot,
                "VALIDATOR_MUTATION",
                maximum=MAX_ARTIFACT_BYTES,
                private=False,
                sealed=sealed_run,
            )

        evidence = {
            "schemaVersion": SYNTHETIC_SCHEMA,
            "runId": core["runId"],
            "stage": FINAL_STAGE,
            "status": "passed",
            "coreReportSha256": hashlib.sha256(core_result.stdout).hexdigest(),
            "synthetic": core,
            "postRunEvidence": {
                "schemaVersion": POST_RUN_SCHEMA,
                "tokenRefresh": {
                    "status": "passed",
                    "issuerUri": refresh_receipt["issuerUri"],
                    "principalReferenceSha256Prefix": hashlib.sha256(
                        refresh_receipt["principalReference"].encode("utf-8")
                    ).hexdigest()[:12],
                    "refreshedAtUtc": refresh_receipt["refreshedAtUtc"],
                    "minimumRemainingLifetimeSeconds": minimum_lifetime,
                    "expiryTokenIssued": True,
                },
                "markerAbsence": {
                    "application": "passed",
                    "proxy": "passed",
                    "seq": "passed",
                    "trace": "passed",
                    "markerSha256Prefixes": {
                        purpose: hashlib.sha256(current_tokens[purpose].marker).hexdigest()[:12]
                        for purpose in CURRENT_PURPOSES
                    },
                },
                "plaintextRejection": "passed",
                "expiryDisconnect": "passed",
                "redisInterruptionRecovery": "not_required",
                "oldGenerationCredentialRejection": "passed",
                "tokenFilesStableForRun": "passed",
                "probeReceipts": {
                    "proxyMarkerScan": receipts["proxy-marker-scan"],
                    "seqMarkerScan": receipts["seq-marker-scan"],
                    "traceMarkerScan": receipts["trace-marker-scan"],
                    "plaintextRejection": receipts["plaintext-rejection"],
                    "oldGenerationRejection": receipts["old-generation-rejection"],
                },
            },
        }
        completed_at = now_provider().astimezone(dt.timezone.utc)
        try:
            qualification_module.validate_synthetic(
                evidence,
                FINAL_STAGE,
                completed_at,
                CORE_TIMEOUT_SECONDS + PROBE_TIMEOUT_SECONDS,
                not_before=started_at,
            )
        except BaseException:
            _fail("SYNTHETIC_SCHEMA")

        payload = (
            json.dumps(evidence, sort_keys=True, separators=(",", ":"), allow_nan=False) + "\n"
        ).encode("utf-8")
        _scan_sensitive(payload, sensitive, "SECRET_EVIDENCE")
        _verify_run_directory(run_directory, run_snapshot, sealed=sealed_run)
        _verify_output_target(output, output_snapshot)
        _atomic_write(output, payload, output_snapshot)
        return evidence


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    values = list(sys.argv[1:] if argv is None else argv)
    expected = ("--env-file", "--project-name", "--run-directory", "--stage", "--output")
    if len(values) != len(expected) * 2:
        _fail("ARGUMENTS")
    if any(values.count(option) != 1 for option in expected):
        _fail("ARGUMENTS")
    if any(value.startswith("--") and value not in expected for value in values[::2]):
        _fail("ARGUMENTS")
    parser = argparse.ArgumentParser(add_help=False, allow_abbrev=False, exit_on_error=False)
    parser.add_argument("--env-file", required=True, type=Path)
    parser.add_argument("--project-name", required=True)
    parser.add_argument("--run-directory", required=True, type=Path)
    parser.add_argument("--stage", required=True, choices=(FINAL_STAGE,))
    parser.add_argument("--output", required=True, type=Path)
    try:
        args = parser.parse_args(values)
    except (argparse.ArgumentError, SystemExit):
        _fail("ARGUMENTS")
    return args


def main(argv: Sequence[str] | None = None) -> int:
    try:
        args = parse_args(argv)
        run_recovery_synthetic(
            args.env_file,
            args.project_name,
            args.run_directory,
            args.stage,
            args.output,
        )
        return 0
    except BaseException:
        os.write(2, b"BOLT_PHASE0_RECOVERY_SYNTHETIC_FAILED\n")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
