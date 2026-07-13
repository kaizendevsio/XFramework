#!/usr/bin/python3
"""Fail-closed lease and recovery controller for Bolt Phase 0 deployments."""

from __future__ import annotations

import argparse
import contextlib
import ctypes
import dataclasses
import datetime as dt
import hashlib
import json
import os
import re
import secrets
import select
import signal
import stat
import subprocess
import sys
import tempfile
import threading
import time
from pathlib import Path
from typing import Any, Callable, Iterator, Sequence


LEASE_SCHEMA = "xframework.bolt.phase0.deployment-lease.v1"
EVIDENCE_SCHEMA = "xframework.bolt.phase0.deployment-recovery.v1"
RECOVERY_GATE_SCHEMA = "xframework.bolt.phase0.recovery-gate.v1"
RUNTIME_SCHEMA = "xframework.bolt.phase0.runtime.v2"

DEPLOYMENT_ROOT = Path("/home/github-runner/xframework-deploy")
APPROVED_RUN_ROOT = DEPLOYMENT_ROOT / "runs"
APPROVED_STATE_ROOT = DEPLOYMENT_ROOT / "phase0-watchdog"
APPROVED_LKG_POINTER = DEPLOYMENT_ROOT / "phase0-last-known-good" / "current"
APPROVED_LEASE_LOCK = Path(
    "/usr/local/libexec/xframework-bolt-phase0/deployment-lease.lock"
)

PHASE0_SERVICES = (
    "migrate",
    "bolt-hub",
    "identityserver",
    "communications",
    "notifications",
    "storage",
    "attendance",
    "smsgateway",
    "wallets",
    "inventario",
    "pos",
    "portal",
    "operations-dashboard",
)
RESTORE_SERVICES = ("redis",) + tuple(
    service for service in PHASE0_SERVICES if service != "migrate"
)

LEASE_KEYS = {
    "schema",
    "run_id",
    "run_attempt",
    "run_directory",
    "project_name",
    "phase",
    "heartbeat_utc",
    "stale_timeout_seconds",
    "mutation_began",
}
GATE_KEYS = {
    "schema",
    "status",
    "qualified_run_id",
    "qualified_run_attempt",
    "project_name",
    "checks",
}
GATE_CHECK_KEYS = {"authenticated_synthetic", "readiness"}
QUALIFICATION_KEYS = {
    "schema",
    "status",
    "generated_at_utc",
    "run_id",
    "run_attempt",
    "source_commit",
    "credential_generation_id",
    "artifacts",
    "runtime_stages",
    "synthetic_stages",
    "checks",
    "errors",
}
QUALIFICATION_CHECK_KEYS = {
    "artifact_security",
    "schema_and_status",
    "identity_and_digest_binding",
    "rotation_and_convergence",
    "canary_observation",
    "rollback_drill",
}

RUN_ID = re.compile(r"[1-9][0-9]{0,31}")
PROJECT_NAME = re.compile(r"[a-z0-9][a-z0-9_-]{0,62}")
PHASE_NAME = re.compile(r"[a-z][a-z0-9-]{0,47}")
COMMIT_SHA = re.compile(r"[0-9a-f]{40}")
TIMESTAMP = re.compile(r"[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z")
SAFE_CONTAINER_NAME = re.compile(r"[a-zA-Z0-9][a-zA-Z0-9_.-]{0,127}")

MIN_STALE_SECONDS = 60
MAX_STALE_SECONDS = 86_400
MAX_JSON_BYTES = 64 * 1024
MAX_ARTIFACT_BYTES = 4 * 1024 * 1024
MIN_SUPERVISED_TIMEOUT_SECONDS = 10
MAX_SUPERVISED_TIMEOUT_SECONDS = 3_900
SUPERVISOR_HEARTBEAT_SECONDS = 30


class ControllerError(RuntimeError):
    def __init__(self, code: str):
        super().__init__(code)
        self.code = code


@dataclasses.dataclass(frozen=True)
class ControllerConfig:
    state_root: Path
    run_root: Path
    project_name: str
    deployment_uid: int
    enforce_production_paths: bool = False
    lkg_pointer: Path | None = None
    sealed_owner_uid: int = 0
    lock_file: Path = APPROVED_LEASE_LOCK
    lock_owner_uid: int = 0
    lock_owner_gid: int | None = None
    lock_parent_uid: int = 0

    @property
    def lease_file(self) -> Path:
        return self.state_root / "deployment-lease.json"

    @property
    def evidence_file(self) -> Path:
        return self.state_root / "deployment-recovery-evidence.json"

    @property
    def effective_lkg_pointer(self) -> Path:
        return self.lkg_pointer or APPROVED_LKG_POINTER


@dataclasses.dataclass(frozen=True)
class RecoveryConfig:
    lkg_pointer: Path
    env_file: Path
    rotation_state_file: Path
    rotation_manager: Path
    runtime_verifier: Path
    recovery_gate_hook: Path
    python_executable: Path
    docker_executable: Path
    services: tuple[str, ...]
    hub_container_name: str
    subprocess_timeout_seconds: int
    stop_timeout_seconds: int


@dataclasses.dataclass(frozen=True)
class Lease:
    run_id: str
    run_attempt: int
    run_directory: Path
    project_name: str
    phase: str
    heartbeat: dt.datetime
    stale_timeout_seconds: int
    mutation_began: bool

    def document(self) -> dict[str, Any]:
        return {
            "schema": LEASE_SCHEMA,
            "run_id": self.run_id,
            "run_attempt": self.run_attempt,
            "run_directory": str(self.run_directory),
            "project_name": self.project_name,
            "phase": self.phase,
            "heartbeat_utc": format_utc(self.heartbeat),
            "stale_timeout_seconds": self.stale_timeout_seconds,
            "mutation_began": self.mutation_began,
        }


@dataclasses.dataclass(frozen=True)
class LkgArtifacts:
    directory: Path
    run_id: str
    run_attempt: int
    compose_file: Path
    override_file: Path
    pins_file: Path
    fingerprints: dict[Path, tuple[int, int, str]]


Runner = Callable[[list[str], int, bool], subprocess.CompletedProcess[str]]
Clock = Callable[[], dt.datetime]


def utc_now() -> dt.datetime:
    return dt.datetime.now(dt.timezone.utc).replace(microsecond=0)


def format_utc(value: dt.datetime) -> str:
    return value.astimezone(dt.timezone.utc).replace(microsecond=0).strftime("%Y-%m-%dT%H:%M:%SZ")


def parse_utc(value: Any) -> dt.datetime:
    if not isinstance(value, str) or not TIMESTAMP.fullmatch(value):
        raise ControllerError("invalid-timestamp")
    try:
        parsed = dt.datetime.strptime(value, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=dt.timezone.utc)
    except ValueError as error:
        raise ControllerError("invalid-timestamp") from error
    if format_utc(parsed) != value:
        raise ControllerError("invalid-timestamp")
    return parsed


def reject_controls(value: Any, depth: int = 0) -> None:
    if depth > 12:
        raise ControllerError("invalid-json-depth")
    if isinstance(value, str):
        if any(ord(character) < 0x20 or ord(character) == 0x7F for character in value):
            raise ControllerError("invalid-control-character")
    elif isinstance(value, list):
        for item in value:
            reject_controls(item, depth + 1)
    elif isinstance(value, dict):
        for key, item in value.items():
            reject_controls(key, depth + 1)
            reject_controls(item, depth + 1)


def _object_pairs(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise ControllerError("duplicate-json-key")
        result[key] = value
    return result


def _reject_constant(_: str) -> None:
    raise ControllerError("invalid-json-number")


def decode_json(raw: bytes) -> Any:
    if not raw or len(raw) > MAX_JSON_BYTES or raw.startswith(b"\xef\xbb\xbf"):
        raise ControllerError("invalid-json-size")
    try:
        text = raw.decode("utf-8", errors="strict")
        document = json.loads(
            text,
            object_pairs_hook=_object_pairs,
            parse_constant=_reject_constant,
        )
    except ControllerError:
        raise
    except (UnicodeDecodeError, json.JSONDecodeError, RecursionError) as error:
        raise ControllerError("invalid-json") from error
    reject_controls(document)
    return document


def _path_has_controls(path: Path) -> bool:
    return any(ord(character) < 0x20 or ord(character) == 0x7F for character in str(path))


def _lexical_absolute(path: Path) -> Path:
    if not path.is_absolute() or _path_has_controls(path) or ".." in path.parts:
        raise ControllerError("invalid-path")
    return path


def _allowed_owner(path_stat: os.stat_result, deployment_uid: int) -> bool:
    return os.name == "nt" or path_stat.st_uid in {0, deployment_uid}


def validate_directory(path: Path, deployment_uid: int) -> Path:
    path = _lexical_absolute(path)
    current = Path(path.anchor)
    for part in path.parts[1:]:
        current /= part
        try:
            current_stat = current.lstat()
        except OSError as error:
            raise ControllerError("missing-directory") from error
        if stat.S_ISLNK(current_stat.st_mode):
            raise ControllerError("symlink-rejected")
    path_stat = path.stat()
    if not stat.S_ISDIR(path_stat.st_mode) or not _allowed_owner(path_stat, deployment_uid):
        raise ControllerError("insecure-directory")
    if os.name != "nt" and stat.S_IMODE(path_stat.st_mode) & 0o022:
        raise ControllerError("insecure-directory")
    return path


def validate_file(
    path: Path,
    deployment_uid: int,
    *,
    require_mode_600: bool = False,
    require_executable: bool = False,
    max_bytes: int | None = None,
) -> os.stat_result:
    path = _lexical_absolute(path)
    validate_directory(path.parent, deployment_uid)
    try:
        path_stat = path.lstat()
    except OSError as error:
        raise ControllerError("missing-file") from error
    if stat.S_ISLNK(path_stat.st_mode):
        raise ControllerError("symlink-rejected")
    if not stat.S_ISREG(path_stat.st_mode) or not _allowed_owner(path_stat, deployment_uid):
        raise ControllerError("insecure-file")
    mode = stat.S_IMODE(path_stat.st_mode)
    if os.name != "nt":
        if require_mode_600 and mode != 0o600:
            raise ControllerError("insecure-file-mode")
        if not require_mode_600 and mode & 0o022:
            raise ControllerError("insecure-file-mode")
        if require_executable and not mode & 0o100:
            raise ControllerError("nonexecutable-helper")
    if max_bytes is not None and path_stat.st_size > max_bytes:
        raise ControllerError("oversized-file")
    return path_stat


def validate_root_sealed_directory(path: Path, *, expected_mode: int = 0o550) -> os.stat_result:
    path = _lexical_absolute(path)
    try:
        validate_directory(path, 0)
    except ControllerError as error:
        raise ControllerError("unsealed-lkg-directory") from error
    path_stat = path.lstat()
    if os.name != "nt" and (
        path_stat.st_uid != 0 or stat.S_IMODE(path_stat.st_mode) != expected_mode
    ):
        raise ControllerError("unsealed-lkg-directory")
    return path_stat


def validate_root_sealed_file(
    path: Path,
    *,
    expected_mode: int,
    max_bytes: int | None = None,
) -> os.stat_result:
    try:
        path_stat = validate_file(path, 0, max_bytes=max_bytes)
    except ControllerError as error:
        raise ControllerError("unsealed-lkg-file") from error
    if os.name != "nt" and (
        path_stat.st_uid != 0 or stat.S_IMODE(path_stat.st_mode) != expected_mode
    ):
        raise ControllerError("unsealed-lkg-file")
    return path_stat


def validate_target(path: Path, deployment_uid: int) -> None:
    path = _lexical_absolute(path)
    validate_directory(path.parent, deployment_uid)
    if path.exists() or path.is_symlink():
        validate_file(path, deployment_uid, require_mode_600=True, max_bytes=MAX_JSON_BYTES)


def fsync_directory(path: Path) -> None:
    if os.name == "nt":
        return
    descriptor = os.open(path, os.O_RDONLY | getattr(os, "O_DIRECTORY", 0))
    try:
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


def atomic_write_json(path: Path, document: dict[str, Any], deployment_uid: int) -> None:
    reject_controls(document)
    validate_target(path, deployment_uid)
    encoded = (json.dumps(document, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")
    if len(encoded) > MAX_JSON_BYTES:
        raise ControllerError("invalid-json-size")
    temporary = path.parent / f".{path.name}.{secrets.token_hex(12)}.tmp"
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    flags |= getattr(os, "O_NOFOLLOW", 0)
    descriptor = os.open(temporary, flags, 0o600)
    try:
        with os.fdopen(descriptor, "wb", closefd=False) as stream:
            stream.write(encoded)
            stream.flush()
            os.fsync(stream.fileno())
        os.close(descriptor)
        descriptor = -1
        os.replace(temporary, path)
        if os.name != "nt":
            os.chmod(path, 0o600)
        fsync_directory(path.parent)
    finally:
        if descriptor >= 0:
            os.close(descriptor)
        with contextlib.suppress(OSError):
            temporary.unlink()


def secure_unlink(path: Path, deployment_uid: int) -> None:
    if not path.exists() and not path.is_symlink():
        return
    validate_file(path, deployment_uid, require_mode_600=True, max_bytes=MAX_JSON_BYTES)
    path.unlink()
    fsync_directory(path.parent)


_LOCKS_GUARD = threading.Lock()
_LOCKS: dict[str, threading.Lock] = {}


def _lease_lock_metadata(metadata: os.stat_result) -> tuple[int, ...]:
    return (
        metadata.st_dev,
        metadata.st_ino,
        metadata.st_mode,
        metadata.st_nlink,
        metadata.st_uid,
        metadata.st_gid,
        metadata.st_size,
    )


def _lease_lock_parent_metadata(metadata: os.stat_result) -> tuple[int, ...]:
    return (
        metadata.st_dev,
        metadata.st_ino,
        metadata.st_mode,
        metadata.st_uid,
        metadata.st_gid,
    )


def _validate_lease_lock_file(
    metadata: os.stat_result, owner_uid: int, owner_gid: int
) -> None:
    if (
        not stat.S_ISREG(metadata.st_mode)
        or metadata.st_nlink != 1
        or (os.name == "posix" and metadata.st_uid != owner_uid)
        or (os.name == "posix" and metadata.st_gid != owner_gid)
        or (os.name == "posix" and stat.S_IMODE(metadata.st_mode) != 0o440)
    ):
        raise ControllerError("insecure-lease-lock")


def _open_lease_lock_parent(path: Path, trusted_uid: int) -> tuple[int, tuple[int, ...]]:
    descriptor = os.open(
        path.anchor,
        os.O_RDONLY | getattr(os, "O_DIRECTORY", 0) | getattr(os, "O_NOFOLLOW", 0),
    )
    try:
        root_metadata = os.fstat(descriptor)
        if (
            not stat.S_ISDIR(root_metadata.st_mode)
            or root_metadata.st_uid != 0
            or stat.S_IMODE(root_metadata.st_mode) & 0o022
        ):
            raise ControllerError("insecure-lease-lock-parent")
        for component in path.parts[1:]:
            child = os.open(
                component,
                os.O_RDONLY
                | getattr(os, "O_DIRECTORY", 0)
                | getattr(os, "O_NOFOLLOW", 0),
                dir_fd=descriptor,
            )
            opened = os.fstat(child)
            current = os.stat(component, dir_fd=descriptor, follow_symlinks=False)
            os.close(descriptor)
            descriptor = child
            if (
                _lease_lock_parent_metadata(opened)
                != _lease_lock_parent_metadata(current)
                or not stat.S_ISDIR(opened.st_mode)
                or opened.st_uid not in {0, trusted_uid}
                or stat.S_IMODE(opened.st_mode) & 0o022
            ):
                raise ControllerError("insecure-lease-lock-parent")
        return descriptor, _lease_lock_parent_metadata(os.fstat(descriptor))
    except BaseException:
        os.close(descriptor)
        raise


@contextlib.contextmanager
def exclusive_lock(
    path: Path,
    *,
    owner_uid: int,
    owner_gid: int,
    trusted_parent_uid: int,
) -> Iterator[None]:
    path = _lexical_absolute(path)
    with _LOCKS_GUARD:
        process_lock = _LOCKS.setdefault(str(path), threading.Lock())
    with process_lock:
        parent_descriptor: int | None = None
        parent_metadata: tuple[int, ...] | None = None
        descriptor = -1
        locked = False
        try:
            if os.name == "posix":
                try:
                    parent_descriptor, parent_metadata = _open_lease_lock_parent(
                        path.parent, trusted_parent_uid
                    )
                except (OSError, ControllerError) as error:
                    raise ControllerError("insecure-lease-lock-parent") from error

            try:
                target: str | Path = path.name if parent_descriptor is not None else path
                descriptor = os.open(
                    target,
                    os.O_RDONLY
                    | getattr(os, "O_CLOEXEC", 0)
                    | getattr(os, "O_NOFOLLOW", 0),
                    dir_fd=parent_descriptor,
                )
            except OSError as error:
                raise ControllerError("insecure-lease-lock") from error

            opened_metadata = os.fstat(descriptor)
            _validate_lease_lock_file(opened_metadata, owner_uid, owner_gid)

            def validate_identity() -> None:
                descriptor_metadata = os.fstat(descriptor)
                try:
                    path_metadata = path.lstat()
                    if parent_descriptor is not None:
                        entry_metadata = os.stat(
                            path.name, dir_fd=parent_descriptor, follow_symlinks=False
                        )
                        reopened_parent, reopened_metadata = _open_lease_lock_parent(
                            path.parent, trusted_parent_uid
                        )
                        os.close(reopened_parent)
                    else:
                        entry_metadata = path_metadata
                        reopened_metadata = None
                except (OSError, ControllerError) as error:
                    raise ControllerError("lease-lock-replaced") from error
                expected = _lease_lock_metadata(opened_metadata)
                if (
                    _lease_lock_metadata(descriptor_metadata) != expected
                    or _lease_lock_metadata(entry_metadata) != expected
                    or _lease_lock_metadata(path_metadata) != expected
                    or (
                        parent_metadata is not None
                        and reopened_metadata != parent_metadata
                    )
                ):
                    raise ControllerError("lease-lock-replaced")
                _validate_lease_lock_file(descriptor_metadata, owner_uid, owner_gid)
                _validate_lease_lock_file(entry_metadata, owner_uid, owner_gid)
                _validate_lease_lock_file(path_metadata, owner_uid, owner_gid)

            validate_identity()
            try:
                if os.name == "posix":
                    import fcntl

                    fcntl.flock(descriptor, fcntl.LOCK_EX)
                else:
                    import msvcrt

                    os.lseek(descriptor, 0, os.SEEK_SET)
                    msvcrt.locking(descriptor, msvcrt.LK_LOCK, 1)
            except OSError as error:
                raise ControllerError("insecure-lease-lock") from error
            locked = True
            validate_identity()
            yield
        finally:
            if descriptor >= 0:
                if locked:
                    try:
                        validate_identity()
                    finally:
                        if os.name == "posix":
                            import fcntl

                            fcntl.flock(descriptor, fcntl.LOCK_UN)
                        else:
                            import msvcrt

                            os.lseek(descriptor, 0, os.SEEK_SET)
                            msvcrt.locking(descriptor, msvcrt.LK_UNLCK, 1)
                os.close(descriptor)
            if parent_descriptor is not None:
                os.close(parent_descriptor)


def _linux_prctl(option: int, value: int) -> None:
    libc = ctypes.CDLL(None, use_errno=True)
    if libc.prctl(option, value, 0, 0, 0) != 0:
        raise OSError(ctypes.get_errno(), f"prctl({option}) failed")


def _write_launcher_message(descriptor: int, document: dict[str, Any]) -> None:
    payload = json.dumps(document, separators=(",", ":"), sort_keys=True).encode("ascii") + b"\n"
    while payload:
        written = os.write(descriptor, payload)
        if written <= 0:
            raise OSError("launcher status pipe closed")
        payload = payload[written:]


def _owned_linux_children() -> tuple[int, ...]:
    children_path = Path(f"/proc/{os.getpid()}/task/{os.getpid()}/children")
    try:
        raw = children_path.read_text(encoding="ascii").strip()
    except OSError as error:
        raise ControllerError("launcher-child-inventory-failed") from error
    if not raw:
        return ()
    try:
        return tuple(int(value) for value in raw.split())
    except ValueError as error:
        raise ControllerError("launcher-child-inventory-failed") from error


def _linux_parent_pid(pid: int) -> int | None:
    try:
        raw = Path(f"/proc/{pid}/stat").read_text(encoding="ascii")
    except FileNotFoundError:
        return None
    except OSError as error:
        raise ControllerError("launcher-child-identity-failed") from error
    boundary = raw.rfind(") ")
    if boundary < 0:
        raise ControllerError("launcher-child-identity-failed")
    fields = raw[boundary + 2 :].split()
    try:
        return int(fields[1])
    except (IndexError, ValueError) as error:
        raise ControllerError("launcher-child-identity-failed") from error


def _signal_owned_linux_child(pid: int, signal_number: int) -> None:
    if _linux_parent_pid(pid) != os.getpid():
        return
    pidfd_open = getattr(os, "pidfd_open", None)
    pidfd_send_signal = getattr(signal, "pidfd_send_signal", None)
    if pidfd_open is not None and pidfd_send_signal is not None:
        try:
            descriptor = pidfd_open(pid)
        except ProcessLookupError:
            return
        try:
            if _linux_parent_pid(pid) == os.getpid():
                pidfd_send_signal(descriptor, signal_number)
        except ProcessLookupError:
            pass
        finally:
            os.close(descriptor)
        return
    if _linux_parent_pid(pid) == os.getpid():
        with contextlib.suppress(ProcessLookupError):
            os.kill(pid, signal_number)


def _reap_adopted_children(timeout_seconds: float = 5.0) -> None:
    deadline = time.monotonic() + timeout_seconds
    sent_term = False
    sent_kill = False
    while True:
        while True:
            try:
                child_pid, _ = os.waitpid(-1, os.WNOHANG)
            except ChildProcessError:
                return
            except InterruptedError:
                continue
            if child_pid == 0:
                break
        children = _owned_linux_children()
        if not children:
            try:
                os.waitpid(-1, os.WNOHANG)
            except ChildProcessError:
                return
        now = time.monotonic()
        if not sent_term:
            for child_pid in children:
                _signal_owned_linux_child(child_pid, signal.SIGTERM)
            sent_term = True
        elif now >= deadline - 2.0 and not sent_kill:
            for child_pid in children:
                _signal_owned_linux_child(child_pid, signal.SIGKILL)
            sent_kill = True
        if now >= deadline:
            raise ControllerError("launcher-descendant-cleanup-failed")
        time.sleep(0.02)


def _child_supervisor_main(parent_pid: int, control_fd: int, status_fd: int, command: list[str]) -> int:
    if not sys.platform.startswith("linux") or parent_pid <= 1 or not command:
        return 125
    termination_requested = False

    def request_termination(_signum: int, _frame: Any) -> None:
        nonlocal termination_requested
        termination_requested = True

    for signal_number in (signal.SIGTERM, signal.SIGINT, signal.SIGHUP):
        signal.signal(signal_number, request_termination)
    try:
        _linux_prctl(36, 1)  # PR_SET_CHILD_SUBREAPER
        _linux_prctl(1, int(signal.SIGTERM))  # PR_SET_PDEATHSIG
        if os.getppid() != parent_pid:
            termination_requested = True
        target = subprocess.Popen(
            command,
            stdin=subprocess.DEVNULL,
            stdout=None,
            stderr=None,
            close_fds=True,
            start_new_session=True,
        )
        _write_launcher_message(status_fd, {"state": "ready", "leader_pid": target.pid})
        while target.poll() is None and not termination_requested:
            readable, _, _ = select.select([control_fd], [], [], 0.1)
            if readable:
                try:
                    if not os.read(control_fd, 1):
                        termination_requested = True
                except OSError:
                    termination_requested = True
            if os.getppid() != parent_pid:
                termination_requested = True
        if termination_requested and target.poll() is None:
            with contextlib.suppress(ProcessLookupError):
                os.killpg(target.pid, signal.SIGTERM)
            try:
                target.wait(timeout=2)
            except subprocess.TimeoutExpired:
                if target.poll() is None:
                    with contextlib.suppress(ProcessLookupError):
                        os.killpg(target.pid, signal.SIGKILL)
                target.wait(timeout=2)
        leader_status = target.wait()
        _reap_adopted_children()
        _write_launcher_message(
            status_fd,
            {"state": "complete", "leader_returncode": leader_status},
        )
        return 0
    except BaseException:
        with contextlib.suppress(BaseException):
            if "target" in locals() and target.poll() is None:
                os.killpg(target.pid, signal.SIGKILL)
                target.wait(timeout=2)
        with contextlib.suppress(BaseException):
            _reap_adopted_children()
        with contextlib.suppress(OSError):
            _write_launcher_message(status_fd, {"state": "failed"})
        return 125
    finally:
        with contextlib.suppress(OSError):
            os.close(control_fd)
        with contextlib.suppress(OSError):
            os.close(status_fd)


class _LauncherHandle:
    def __init__(self, command: Sequence[str], *, capture: bool, inherit_output: bool) -> None:
        self.command = list(command)
        self.control_fd: int | None = None
        self.status_fd: int | None = None
        self.output_file = tempfile.TemporaryFile(mode="w+t", encoding="utf-8") if capture else None
        self._status_buffer = b""
        self._messages: list[dict[str, Any]] = []
        if sys.platform.startswith("linux"):
            control_read, control_write = os.pipe()
            status_read, status_write = os.pipe()
            try:
                self.process = subprocess.Popen(
                    [
                        sys.executable,
                        str(Path(__file__).resolve()),
                        "__child-supervisor",
                        str(os.getpid()),
                        str(control_read),
                        str(status_write),
                        "--",
                        *self.command,
                    ],
                    stdin=subprocess.DEVNULL,
                    stdout=self.output_file if capture else None if inherit_output else subprocess.DEVNULL,
                    stderr=None if inherit_output else subprocess.DEVNULL,
                    text=True,
                    close_fds=True,
                    pass_fds=(control_read, status_write),
                )
            except BaseException:
                os.close(control_write)
                os.close(status_read)
                if self.output_file is not None:
                    self.output_file.close()
                    self.output_file = None
                raise
            finally:
                os.close(control_read)
                os.close(status_write)
            self.control_fd = control_write
            self.status_fd = status_read
        else:
            self.process = subprocess.Popen(
                self.command,
                stdin=subprocess.DEVNULL,
                stdout=self.output_file if capture else None if inherit_output else subprocess.DEVNULL,
                stderr=None if inherit_output else subprocess.DEVNULL,
                text=True,
                close_fds=True,
                creationflags=getattr(subprocess, "CREATE_NEW_PROCESS_GROUP", 0),
            )

    def _read_messages(self, *, block_seconds: float = 0.0) -> None:
        if self.status_fd is None:
            return
        readable, _, _ = select.select([self.status_fd], [], [], block_seconds)
        if readable:
            chunk = os.read(self.status_fd, 4096)
            self._status_buffer += chunk
        while b"\n" in self._status_buffer:
            raw, self._status_buffer = self._status_buffer.split(b"\n", 1)
            try:
                document = json.loads(raw)
            except (UnicodeDecodeError, json.JSONDecodeError) as error:
                raise ControllerError("supervised-launcher-protocol-failed") from error
            if not isinstance(document, dict):
                raise ControllerError("supervised-launcher-protocol-failed")
            self._messages.append(document)

    def wait_ready(self, timeout_seconds: float = 5.0) -> None:
        if self.status_fd is None:
            return
        deadline = time.monotonic() + timeout_seconds
        while time.monotonic() < deadline:
            self._read_messages(block_seconds=min(0.1, deadline - time.monotonic()))
            if any(message.get("state") == "ready" for message in self._messages):
                return
            if self.process.poll() is not None:
                break
        self.stop()
        raise ControllerError("supervised-launcher-readiness-failed")

    def stop(self) -> None:
        if self.control_fd is not None:
            with contextlib.suppress(OSError):
                os.close(self.control_fd)
            self.control_fd = None
        if self.control_fd is None and not sys.platform.startswith("linux") and self.process.poll() is None:
            self.process.terminate()
        try:
            self.process.wait(timeout=8)
        except subprocess.TimeoutExpired:
            self.process.terminate()
            try:
                self.process.wait(timeout=2)
            except subprocess.TimeoutExpired:
                self.process.kill()
                self.process.wait(timeout=2)
            raise ControllerError("supervised-launcher-cleanup-failed")
        if self.status_fd is not None:
            self._read_messages()
            os.close(self.status_fd)
            self.status_fd = None
        if self.output_file is not None:
            self.output_file.close()
            self.output_file = None
        if sys.platform.startswith("linux") and self.process.returncode != 0:
            raise ControllerError("supervised-launcher-cleanup-failed")

    def complete(self) -> tuple[int, str]:
        if self.control_fd is not None:
            os.close(self.control_fd)
            self.control_fd = None
        self.process.wait()
        stdout = ""
        if self.output_file is not None:
            self.output_file.seek(0)
            stdout = self.output_file.read()
            self.output_file.close()
            self.output_file = None
        if self.status_fd is None:
            return self.process.returncode, stdout
        self._read_messages()
        os.close(self.status_fd)
        self.status_fd = None
        if self.process.returncode != 0:
            raise ControllerError("supervised-launcher-failed")
        completed = [message for message in self._messages if message.get("state") == "complete"]
        if len(completed) != 1 or not isinstance(completed[0].get("leader_returncode"), int):
            raise ControllerError("supervised-launcher-protocol-failed")
        return int(completed[0]["leader_returncode"]), stdout


def default_runner(command: list[str], timeout: int, capture: bool) -> subprocess.CompletedProcess[str]:
    try:
        launcher = _LauncherHandle(command, capture=capture, inherit_output=False)
        launcher.wait_ready()
        launcher.process.wait(timeout=timeout)
    except subprocess.TimeoutExpired as error:
        try:
            launcher.stop()
        except ControllerError:
            raise
        except Exception as cleanup_error:
            raise ControllerError("supervised-launcher-cleanup-failed") from cleanup_error
        raise subprocess.TimeoutExpired(command, timeout) from error
    except ControllerError:
        raise
    except Exception as error:
        raise ControllerError("supervised-launcher-start-failed") from error
    returncode, stdout = launcher.complete()
    return subprocess.CompletedProcess(command, returncode, stdout, "")


class DeploymentLeaseController:
    def __init__(
        self,
        config: ControllerConfig,
        *,
        runner: Runner = default_runner,
        clock: Clock = utc_now,
    ) -> None:
        self.config = config
        self.runner = runner
        self.clock = clock
        self._validate_config()

    def _validate_config(self) -> None:
        if not PROJECT_NAME.fullmatch(self.config.project_name):
            raise ControllerError("invalid-project-name")
        if self.config.deployment_uid < 0:
            raise ControllerError("invalid-deployment-uid")
        if self.config.sealed_owner_uid < 0:
            raise ControllerError("invalid-sealed-owner-uid")
        if self.config.lock_owner_uid < 0 or self.config.lock_parent_uid < 0:
            raise ControllerError("invalid-lock-owner")
        if self.config.lock_owner_gid is not None and self.config.lock_owner_gid < 0:
            raise ControllerError("invalid-lock-group")
        validate_directory(self.config.state_root, self.config.deployment_uid)
        validate_directory(self.config.run_root, self.config.deployment_uid)
        if self.config.enforce_production_paths:
            if (
                self.config.run_root != APPROVED_RUN_ROOT
                or self.config.state_root != APPROVED_STATE_ROOT
                or self.config.effective_lkg_pointer != APPROVED_LKG_POINTER
                or self.config.sealed_owner_uid != 0
                or self.config.lock_file != APPROVED_LEASE_LOCK
                or self.config.lock_owner_uid != 0
                or self.config.lock_parent_uid != 0
            ):
                raise ControllerError("unapproved-production-path")
        for target in (self.config.lease_file, self.config.evidence_file):
            validate_target(target, self.config.deployment_uid)

    def _now(self) -> dt.datetime:
        value = self.clock()
        if value.tzinfo is None:
            raise ControllerError("invalid-clock")
        return value.astimezone(dt.timezone.utc).replace(microsecond=0)

    def _expected_run_directory(self, run_id: str, run_attempt: int) -> Path:
        return self.config.run_root / f"{run_id}-{run_attempt}"

    def _validate_run_identity(self, run_id: str, run_attempt: int) -> None:
        if not RUN_ID.fullmatch(run_id) or not 1 <= run_attempt <= 100_000:
            raise ControllerError("invalid-run-identity")

    def _validate_phase(self, phase: str) -> None:
        if not PHASE_NAME.fullmatch(phase):
            raise ControllerError("invalid-phase")

    def _deployment_gid(self) -> int:
        if os.name == "nt":
            return 0
        try:
            import pwd

            return pwd.getpwuid(self.config.deployment_uid).pw_gid
        except (KeyError, ImportError) as error:
            raise ControllerError("invalid-deployment-identity") from error

    @contextlib.contextmanager
    def _lease_lock(self) -> Iterator[None]:
        with exclusive_lock(
            self.config.lock_file,
            owner_uid=self.config.lock_owner_uid,
            owner_gid=(
                self.config.lock_owner_gid
                if self.config.lock_owner_gid is not None
                else self._deployment_gid()
            ),
            trusted_parent_uid=self.config.lock_parent_uid,
        ):
            yield

    def _validate_exact_file(
        self,
        path: Path,
        *,
        owner_uid: int,
        owner_gid: int,
        mode: int,
        max_bytes: int,
    ) -> bytes:
        metadata = validate_file(path, owner_uid, max_bytes=max_bytes)
        if os.name != "nt" and (
            metadata.st_uid != owner_uid
            or metadata.st_gid != owner_gid
            or stat.S_IMODE(metadata.st_mode) != mode
            or metadata.st_nlink != 1
        ):
            raise ControllerError("invalid-sealed-run-file")
        return path.read_bytes()

    def _validate_post_activation_run(
        self,
        directory: Path,
        run_id: str,
        run_attempt: int,
        deployment_gid: int,
        *,
        require_pointer: bool = True,
    ) -> None:
        owner_uid = self.config.sealed_owner_uid
        if require_pointer:
            pointer_owner_gid = 0 if self.config.enforce_production_paths else deployment_gid
            pointer = self.config.effective_lkg_pointer
            raw_pointer = self._validate_exact_file(
                pointer,
                owner_uid=owner_uid,
                owner_gid=pointer_owner_gid,
                mode=0o644,
                max_bytes=4_096,
            )
            try:
                pointer_text = raw_pointer.decode("utf-8", errors="strict")
            except UnicodeDecodeError as error:
                raise ControllerError("invalid-lkg-pointer") from error
            if pointer_text != f"{directory}\n":
                raise ControllerError("sealed-run-pointer-mismatch")

        marker = self._validate_exact_file(
            directory / "security-qualified",
            owner_uid=owner_uid,
            owner_gid=deployment_gid,
            mode=0o440,
            max_bytes=0,
        )
        if marker:
            raise ControllerError("invalid-sealed-run-marker")
        commit = self._validate_exact_file(
            directory / "qualified-commit",
            owner_uid=owner_uid,
            owner_gid=deployment_gid,
            mode=0o440,
            max_bytes=64,
        )
        try:
            commit_text = commit[:-1].decode("ascii", errors="strict")
        except UnicodeDecodeError as error:
            raise ControllerError("invalid-sealed-run-commit") from error
        if not commit.endswith(b"\n") or not COMMIT_SHA.fullmatch(commit_text):
            raise ControllerError("invalid-sealed-run-commit")
        qualification = decode_json(
            self._validate_exact_file(
                directory / "qualification-evidence.json",
                owner_uid=owner_uid,
                owner_gid=deployment_gid,
                mode=0o440,
                max_bytes=MAX_JSON_BYTES,
            )
        )
        if (
            not isinstance(qualification, dict)
            or set(qualification) != QUALIFICATION_KEYS
            or qualification.get("schema") != "xframework.bolt.phase0.qualification.v1"
            or qualification.get("status") != "passed"
            or qualification.get("run_id") != run_id
            or qualification.get("run_attempt") != run_attempt
            or qualification.get("source_commit") != commit_text
            or qualification.get("errors") != []
            or not isinstance(qualification.get("artifacts"), dict)
            or not qualification["artifacts"]
            or not isinstance(qualification.get("runtime_stages"), dict)
            or not isinstance(qualification.get("synthetic_stages"), dict)
            or not isinstance(qualification.get("checks"), dict)
            or set(qualification["checks"]) != QUALIFICATION_CHECK_KEYS
            or any(value is not True for value in qualification["checks"].values())
        ):
            raise ControllerError("invalid-sealed-run-qualification")

    def _validate_lease_run_directory(
        self,
        directory: Path,
        run_id: str,
        run_attempt: int,
        *,
        allow_activation_transition: bool = False,
    ) -> None:
        if allow_activation_transition and not directory.exists() and not directory.is_symlink():
            return
        validate_directory(directory, self.config.deployment_uid)
        metadata = directory.lstat()
        deployment_gid = self._deployment_gid()
        if os.name == "nt":
            return
        mode = stat.S_IMODE(metadata.st_mode)
        if (
            metadata.st_uid == self.config.deployment_uid
            and metadata.st_gid == deployment_gid
            and mode == 0o700
        ):
            return
        if (
            metadata.st_uid == self.config.sealed_owner_uid
            and metadata.st_gid == deployment_gid
            and mode == 0o550
        ):
            self._validate_post_activation_run(
                directory,
                run_id,
                run_attempt,
                deployment_gid,
                require_pointer=not allow_activation_transition,
            )
            return
        raise ControllerError("invalid-lease-run-directory")

    def _validate_disarm_run(self, lease: Lease) -> None:
        directory = lease.run_directory
        validate_directory(directory, self.config.deployment_uid)
        metadata = directory.lstat()
        deployment_gid = self._deployment_gid()
        if os.name != "nt" and (
            metadata.st_uid != self.config.sealed_owner_uid
            or metadata.st_gid != deployment_gid
            or stat.S_IMODE(metadata.st_mode) != 0o550
        ):
            raise ControllerError("lease-run-not-activated")
        self._validate_post_activation_run(
            directory,
            lease.run_id,
            lease.run_attempt,
            deployment_gid,
            require_pointer=True,
        )

    def _read_lease(self, *, allow_activation_transition: bool = False) -> Lease | None:
        path = self.config.lease_file
        if not path.exists() and not path.is_symlink():
            return None
        validate_file(
            path,
            self.config.deployment_uid,
            require_mode_600=True,
            max_bytes=MAX_JSON_BYTES,
        )
        document = decode_json(path.read_bytes())
        if not isinstance(document, dict) or set(document) != LEASE_KEYS or document.get("schema") != LEASE_SCHEMA:
            raise ControllerError("invalid-lease-schema")
        run_id = document["run_id"]
        run_attempt = document["run_attempt"]
        phase = document["phase"]
        stale_timeout = document["stale_timeout_seconds"]
        mutation_began = document["mutation_began"]
        if not isinstance(run_id, str) or isinstance(run_attempt, bool) or not isinstance(run_attempt, int):
            raise ControllerError("invalid-run-identity")
        self._validate_run_identity(run_id, run_attempt)
        if not isinstance(phase, str):
            raise ControllerError("invalid-phase")
        self._validate_phase(phase)
        if (
            isinstance(stale_timeout, bool)
            or not isinstance(stale_timeout, int)
            or not MIN_STALE_SECONDS <= stale_timeout <= MAX_STALE_SECONDS
        ):
            raise ControllerError("invalid-stale-timeout")
        if not isinstance(mutation_began, bool):
            raise ControllerError("invalid-mutation-state")
        if document["project_name"] != self.config.project_name:
            raise ControllerError("lease-project-mismatch")
        expected_directory = self._expected_run_directory(run_id, run_attempt)
        if document["run_directory"] != str(expected_directory):
            raise ControllerError("lease-run-directory-mismatch")
        self._validate_lease_run_directory(
            expected_directory,
            run_id,
            run_attempt,
            allow_activation_transition=allow_activation_transition,
        )
        return Lease(
            run_id=run_id,
            run_attempt=run_attempt,
            run_directory=expected_directory,
            project_name=self.config.project_name,
            phase=phase,
            heartbeat=parse_utc(document["heartbeat_utc"]),
            stale_timeout_seconds=stale_timeout,
            mutation_began=mutation_began,
        )

    def _is_stale(self, lease: Lease, now: dt.datetime) -> bool:
        if lease.heartbeat > now:
            raise ControllerError("future-heartbeat")
        return now - lease.heartbeat >= dt.timedelta(seconds=lease.stale_timeout_seconds)

    def _evidence(
        self,
        *,
        now: dt.datetime,
        action: str,
        status: str,
        reason_code: str,
        lease: Lease | None,
        stale: bool | None,
        gates: dict[str, bool] | None = None,
    ) -> dict[str, Any]:
        lease_summary = None
        if lease is not None:
            lease_summary = {
                "run_id": lease.run_id,
                "run_attempt": lease.run_attempt,
                "project_name": lease.project_name,
                "phase": lease.phase,
                "mutation_began": lease.mutation_began,
                "stale": stale,
            }
        return {
            "schema": EVIDENCE_SCHEMA,
            "generated_at_utc": format_utc(now),
            "action": action,
            "status": status,
            "reason_code": reason_code,
            "lease": lease_summary,
            "gates": gates
            or {
                "rotation_aborted": False,
                "restore_applied": False,
                "runtime_verified": False,
                "recovery_gate_verified": False,
                "hub_stopped": False,
            },
        }

    def _write_evidence(self, evidence: dict[str, Any], *, preserve_existing: bool = False) -> None:
        if preserve_existing and self.config.evidence_file.exists():
            return
        atomic_write_json(self.config.evidence_file, evidence, self.config.deployment_uid)

    def arm(
        self,
        run_id: str,
        run_attempt: int,
        phase: str,
        stale_timeout_seconds: int,
    ) -> tuple[dict[str, Any], int]:
        self._validate_run_identity(run_id, run_attempt)
        self._validate_phase(phase)
        if not MIN_STALE_SECONDS <= stale_timeout_seconds <= MAX_STALE_SECONDS:
            raise ControllerError("invalid-stale-timeout")
        run_directory = self._expected_run_directory(run_id, run_attempt)
        validate_directory(run_directory, self.config.deployment_uid)
        with self._lease_lock():
            now = self._now()
            current = self._read_lease()
            lease = Lease(
                run_id,
                run_attempt,
                run_directory,
                self.config.project_name,
                phase,
                now,
                stale_timeout_seconds,
                False,
            )
            if current is not None:
                if (
                    current.run_id != run_id
                    or current.run_attempt != run_attempt
                    or current.mutation_began
                    or current.stale_timeout_seconds != stale_timeout_seconds
                ):
                    raise ControllerError("active-lease-exists")
                if self._is_stale(current, now):
                    raise ControllerError("stale-lease-requires-reconcile")
            secure_unlink(self.config.evidence_file, self.config.deployment_uid)
            atomic_write_json(self.config.lease_file, lease.document(), self.config.deployment_uid)
            evidence = self._evidence(
                now=now,
                action="armed",
                status="passed",
                reason_code="lease-armed",
                lease=lease,
                stale=False,
            )
            self._write_evidence(evidence)
            return evidence, 0

    def heartbeat(
        self,
        run_id: str,
        run_attempt: int,
        phase: str,
        mutation_began: bool,
    ) -> tuple[dict[str, Any], int]:
        self._validate_run_identity(run_id, run_attempt)
        self._validate_phase(phase)
        with self._lease_lock():
            now = self._now()
            current = self._read_lease()
            if current is None or (current.run_id, current.run_attempt) != (run_id, run_attempt):
                raise ControllerError("lease-owner-mismatch")
            if self._is_stale(current, now):
                raise ControllerError("stale-lease-cannot-heartbeat")
            lease = dataclasses.replace(
                current,
                phase=phase,
                heartbeat=now,
                mutation_began=current.mutation_began or mutation_began,
            )
            atomic_write_json(self.config.lease_file, lease.document(), self.config.deployment_uid)
            evidence = self._evidence(
                now=now,
                action="heartbeat",
                status="passed",
                reason_code="lease-renewed",
                lease=lease,
                stale=False,
            )
            self._write_evidence(evidence)
            return evidence, 0

    def disarm(self, run_id: str, run_attempt: int) -> tuple[dict[str, Any], int]:
        self._validate_run_identity(run_id, run_attempt)
        with self._lease_lock():
            now = self._now()
            lease = self._read_lease()
            if lease is None:
                raise ControllerError("no-active-lease")
            if (lease.run_id, lease.run_attempt) != (run_id, run_attempt):
                raise ControllerError("lease-owner-mismatch")
            stale = self._is_stale(lease, now)
            if stale:
                raise ControllerError("stale-lease-requires-reconcile")
            self._validate_disarm_run(lease)
            secure_unlink(self.config.lease_file, self.config.deployment_uid)
            evidence = self._evidence(
                now=now,
                action="disarmed",
                status="passed",
                reason_code="lease-disarmed",
                lease=lease,
                stale=False,
            )
            self._write_evidence(evidence)
            return evidence, 0

    def status(self) -> tuple[dict[str, Any], int]:
        with self._lease_lock():
            now = self._now()
            lease = self._read_lease()
            stale = self._is_stale(lease, now) if lease else None
            evidence = self._evidence(
                now=now,
                action="status",
                status="stale" if stale else "active" if lease else "noop",
                reason_code="lease-stale" if stale else "lease-active" if lease else "no-active-lease",
                lease=lease,
                stale=stale,
            )
            return evidence, 0

    def require_fresh(self) -> tuple[dict[str, Any], int]:
        """Succeed only while a fully validated deployment lease is fresh."""
        with self._lease_lock():
            now = self._now()
            lease = self._read_lease()
            if lease is None:
                raise ControllerError("no-active-lease")
            if self._is_stale(lease, now):
                raise ControllerError("lease-stale")
            return self._evidence(
                now=now,
                action="require-fresh",
                status="active",
                reason_code="lease-active",
                lease=lease,
                stale=False,
            ), 0

    def supervise(
        self,
        run_id: str,
        run_attempt: int,
        phase: str,
        mutation_began: bool,
        timeout_seconds: float,
        command: Sequence[str],
        *,
        heartbeat_seconds: float = SUPERVISOR_HEARTBEAT_SECONDS,
    ) -> tuple[dict[str, Any], int]:
        self._validate_run_identity(run_id, run_attempt)
        self._validate_phase(phase)
        if not command or not all(isinstance(value, str) and value for value in command):
            raise ControllerError("invalid-supervised-command")
        if timeout_seconds <= 0 or heartbeat_seconds <= 0:
            raise ControllerError("invalid-supervised-timeout")
        self.heartbeat(run_id, run_attempt, phase, mutation_began)
        try:
            launcher = _LauncherHandle(command, capture=False, inherit_output=True)
            launcher.wait_ready()
        except ControllerError:
            raise
        except Exception as error:
            raise ControllerError("supervised-operation-start-failed") from error
        deadline = time.monotonic() + timeout_seconds
        try:
            while True:
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    raise ControllerError("supervised-operation-timeout")
                try:
                    launcher.process.wait(timeout=min(heartbeat_seconds, remaining))
                    break
                except subprocess.TimeoutExpired:
                    self.heartbeat(run_id, run_attempt, phase, mutation_began)
        except BaseException as error:
            try:
                launcher.stop()
            except ControllerError as cleanup_error:
                raise cleanup_error from error
            except Exception as cleanup_error:
                raise ControllerError("supervised-launcher-cleanup-failed") from cleanup_error
            raise
        try:
            exit_code, _ = launcher.complete()
        except ControllerError:
            raise
        except Exception as error:
            raise ControllerError("supervised-launcher-cleanup-failed") from error
        evidence, _ = self.heartbeat(run_id, run_attempt, phase, mutation_began)
        evidence["action"] = "supervised-operation"
        evidence["reason_code"] = (
            "supervised-operation-completed"
            if exit_code == 0
            else "supervised-operation-failed"
        )
        return evidence, exit_code

    def _validate_recovery(self, recovery: RecoveryConfig) -> None:
        if self.config.enforce_production_paths and recovery.lkg_pointer != APPROVED_LKG_POINTER:
            raise ControllerError("unapproved-production-path")
        if recovery.services != PHASE0_SERVICES:
            raise ControllerError("invalid-service-inventory")
        if not SAFE_CONTAINER_NAME.fullmatch(recovery.hub_container_name):
            raise ControllerError("invalid-hub-container-name")
        if not 10 <= recovery.subprocess_timeout_seconds <= 3_600:
            raise ControllerError("invalid-subprocess-timeout")
        if not 1 <= recovery.stop_timeout_seconds <= 300:
            raise ControllerError("invalid-stop-timeout")
        validate_file(recovery.env_file, self.config.deployment_uid, require_mode_600=True)
        validate_target(recovery.rotation_state_file, self.config.deployment_uid)
        validate_file(recovery.python_executable, self.config.deployment_uid, require_executable=True)
        validate_file(recovery.docker_executable, self.config.deployment_uid, require_executable=True)
        validate_file(recovery.rotation_manager, self.config.deployment_uid, max_bytes=MAX_ARTIFACT_BYTES)
        validate_file(recovery.runtime_verifier, self.config.deployment_uid, max_bytes=MAX_ARTIFACT_BYTES)
        validate_file(
            recovery.recovery_gate_hook,
            self.config.deployment_uid,
            require_executable=True,
            max_bytes=MAX_ARTIFACT_BYTES,
        )
        _lexical_absolute(recovery.lkg_pointer)

    def _invoke(
        self,
        command: list[str],
        timeout: int,
        step: str,
        *,
        capture: bool = False,
    ) -> subprocess.CompletedProcess[str]:
        try:
            return self.runner(command, timeout, capture)
        except subprocess.TimeoutExpired as error:
            raise ControllerError(f"{step}-timeout") from error
        except OSError as error:
            raise ControllerError(f"{step}-execution-failed") from error

    def _abort_prepared(self, recovery: RecoveryConfig) -> None:
        result = self._invoke(
            [
                str(recovery.python_executable),
                str(recovery.rotation_manager),
                "abort-prepared",
                "--env-file",
                str(recovery.env_file),
                "--state-file",
                str(recovery.rotation_state_file),
            ],
            recovery.subprocess_timeout_seconds,
            "rotation-abort",
        )
        if result.returncode != 0:
            raise ControllerError("rotation-abort-failed")

    def _fingerprint(self, path: Path) -> tuple[int, int, str]:
        path_stat = validate_file(
            path,
            self.config.deployment_uid,
            max_bytes=MAX_ARTIFACT_BYTES,
        )
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        return path_stat.st_size, path_stat.st_mtime_ns, digest

    def _resolve_lkg(self, recovery: RecoveryConfig) -> LkgArtifacts:
        if self.config.enforce_production_paths:
            run_root_stat = self.config.run_root.lstat()
            if (
                os.name != "nt"
                and (
                    run_root_stat.st_uid != 0
                    or stat.S_IMODE(run_root_stat.st_mode) & 0o022
                )
            ):
                raise ControllerError("unsealed-run-root")
            validate_root_sealed_file(
                recovery.lkg_pointer, expected_mode=0o644, max_bytes=4_096
            )
        else:
            validate_file(
                recovery.lkg_pointer,
                self.config.deployment_uid,
                max_bytes=4_096,
            )
        try:
            pointer = recovery.lkg_pointer.read_text(encoding="utf-8", errors="strict")
        except (OSError, UnicodeDecodeError) as error:
            raise ControllerError("invalid-lkg-pointer") from error
        if pointer.count("\n") > 1 or not pointer.endswith("\n"):
            raise ControllerError("invalid-lkg-pointer")
        directory_text = pointer[:-1]
        reject_controls(directory_text)
        directory = _lexical_absolute(Path(directory_text))
        try:
            relative = directory.relative_to(self.config.run_root)
        except ValueError as error:
            raise ControllerError("unqualified-lkg") from error
        if len(relative.parts) != 1:
            raise ControllerError("unqualified-lkg")
        match = re.fullmatch(r"([1-9][0-9]{0,31})-([1-9][0-9]{0,5})", relative.name)
        if not match:
            raise ControllerError("unqualified-lkg")
        run_id, attempt_text = match.groups()
        run_attempt = int(attempt_text)
        if self.config.enforce_production_paths:
            validate_root_sealed_directory(directory)
        else:
            validate_directory(directory, self.config.deployment_uid)
        qualified = directory / "security-qualified"
        if self.config.enforce_production_paths:
            qualified_stat = validate_root_sealed_file(
                qualified, expected_mode=0o440, max_bytes=0
            )
        else:
            qualified_stat = validate_file(
                qualified, self.config.deployment_uid, max_bytes=0
            )
        if qualified_stat.st_size != 0:
            raise ControllerError("unqualified-lkg")
        commit_file = directory / "qualified-commit"
        if self.config.enforce_production_paths:
            validate_root_sealed_file(commit_file, expected_mode=0o440, max_bytes=64)
        else:
            validate_file(commit_file, self.config.deployment_uid, max_bytes=64)
        try:
            commit = commit_file.read_text(encoding="ascii", errors="strict")
        except (OSError, UnicodeDecodeError) as error:
            raise ControllerError("unqualified-lkg") from error
        if not commit.endswith("\n") or not COMMIT_SHA.fullmatch(commit[:-1]):
            raise ControllerError("unqualified-lkg")
        compose_file = directory / "docker-compose.yml"
        override_file = directory / "pinned-compose.override.json"
        pins_file = directory / "image-pins.json"
        if self.config.enforce_production_paths:
            for path in (compose_file, override_file, pins_file):
                validate_root_sealed_file(
                    path, expected_mode=0o440, max_bytes=MAX_ARTIFACT_BYTES
                )
        paths = (qualified, commit_file, compose_file, override_file, pins_file)
        fingerprints = {path: self._fingerprint(path) for path in paths}
        return LkgArtifacts(
            directory,
            run_id,
            run_attempt,
            compose_file,
            override_file,
            pins_file,
            fingerprints,
        )

    def _validate_bound_recovery_helpers(
        self, recovery: RecoveryConfig, artifacts: LkgArtifacts
    ) -> None:
        expected = {
            "rotation_manager": artifacts.directory / "manage-bolt-phase0-rotation.py",
            "runtime_verifier": artifacts.directory / "verify-bolt-phase0-runtime.py",
            "recovery_gate_hook": artifacts.directory / "verify-bolt-phase0-qualification.py",
        }
        if self.config.enforce_production_paths and any(
            getattr(recovery, field) != path for field, path in expected.items()
        ):
            raise ControllerError("unbound-recovery-helper")
        if self.config.enforce_production_paths:
            for path in expected.values():
                validate_root_sealed_file(
                    path, expected_mode=0o550, max_bytes=MAX_ARTIFACT_BYTES
                )

    def _assert_lkg_unchanged(self, artifacts: LkgArtifacts) -> None:
        for path, expected in artifacts.fingerprints.items():
            if self._fingerprint(path) != expected:
                raise ControllerError("lkg-artifact-changed")

    def _temporary_gate_file(self, prefix: str) -> Path:
        path = self.config.state_root / f".{prefix}.{secrets.token_hex(12)}.json"
        descriptor = os.open(
            path,
            os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0),
            0o600,
        )
        os.close(descriptor)
        return path

    def _restore(self, recovery: RecoveryConfig, artifacts: LkgArtifacts) -> None:
        self._assert_lkg_unchanged(artifacts)
        command = [
            str(recovery.docker_executable),
            "compose",
            "--env-file",
            str(recovery.env_file),
            "-f",
            str(artifacts.compose_file),
            "-f",
            str(artifacts.override_file),
            "--project-name",
            self.config.project_name,
            "up",
            "-d",
            "--no-build",
            "--no-deps",
            *RESTORE_SERVICES,
        ]
        result = self._invoke(command, recovery.subprocess_timeout_seconds, "restore")
        if result.returncode != 0:
            raise ControllerError("restore-failed")

    def _runtime_gate(self, recovery: RecoveryConfig, artifacts: LkgArtifacts) -> None:
        self._assert_lkg_unchanged(artifacts)
        output = self._temporary_gate_file("runtime")
        try:
            command = [
                str(recovery.python_executable),
                str(recovery.runtime_verifier),
                "--compose-file",
                str(artifacts.compose_file),
                "--compose-file",
                str(artifacts.override_file),
                "--env-file",
                str(recovery.env_file),
                "--project-name",
                self.config.project_name,
                "--output",
                str(output),
                "--pins-file",
                str(artifacts.pins_file),
                "--services",
                *PHASE0_SERVICES,
            ]
            result = self._invoke(command, recovery.subprocess_timeout_seconds, "runtime-gate")
            if result.returncode != 0:
                raise ControllerError("runtime-gate-failed")
            document = decode_json(output.read_bytes())
            if not isinstance(document, dict) or document.get("schema") != RUNTIME_SCHEMA or document.get("status") != "passed":
                raise ControllerError("runtime-gate-failed")
        except OSError as error:
            raise ControllerError("runtime-gate-failed") from error
        finally:
            with contextlib.suppress(OSError):
                output.unlink()

    def _recovery_gate(self, recovery: RecoveryConfig, artifacts: LkgArtifacts) -> None:
        self._assert_lkg_unchanged(artifacts)
        output = self._temporary_gate_file("recovery")
        try:
            command = [
                str(recovery.recovery_gate_hook),
                "--env-file",
                str(recovery.env_file),
                "--project-name",
                self.config.project_name,
                "--run-directory",
                str(artifacts.directory),
                "--qualified-run-id",
                artifacts.run_id,
                "--qualified-run-attempt",
                str(artifacts.run_attempt),
                "--output",
                str(output),
            ]
            result = self._invoke(command, recovery.subprocess_timeout_seconds, "recovery-gate")
            if result.returncode != 0:
                raise ControllerError("recovery-gate-failed")
            document = decode_json(output.read_bytes())
            if (
                not isinstance(document, dict)
                or set(document) != GATE_KEYS
                or document.get("schema") != RECOVERY_GATE_SCHEMA
                or document.get("status") != "passed"
                or document.get("qualified_run_id") != artifacts.run_id
                or document.get("qualified_run_attempt") != artifacts.run_attempt
                or document.get("project_name") != self.config.project_name
                or not isinstance(document.get("checks"), dict)
                or set(document["checks"]) != GATE_CHECK_KEYS
                or any(document["checks"].get(check) is not True for check in GATE_CHECK_KEYS)
            ):
                raise ControllerError("recovery-gate-failed")
        except OSError as error:
            raise ControllerError("recovery-gate-failed") from error
        finally:
            with contextlib.suppress(OSError):
                output.unlink()

    def _stop_hub(self, recovery: RecoveryConfig) -> bool:
        timeout = recovery.stop_timeout_seconds + 10
        stop: subprocess.CompletedProcess[str] | None = None
        inspect: subprocess.CompletedProcess[str] | None = None
        try:
            stop = self._invoke(
                [
                    str(recovery.docker_executable),
                    "stop",
                    "--time",
                    str(recovery.stop_timeout_seconds),
                    recovery.hub_container_name,
                ],
                timeout,
                "hub-stop",
            )
        except ControllerError:
            pass
        try:
            inspect = self._invoke(
                [
                    str(recovery.docker_executable),
                    "inspect",
                    "--format",
                    "{{.State.Running}}",
                    recovery.hub_container_name,
                ],
                30,
                "hub-stop-verification",
                capture=True,
            )
        except ControllerError:
            pass
        if inspect is not None and inspect.returncode == 0 and (inspect.stdout or "").strip() == "false":
            return True
        if stop is not None and stop.returncode == 0 and (inspect is None or inspect.returncode != 0):
            try:
                listing = self._invoke(
                    [
                        str(recovery.docker_executable),
                        "ps",
                        "--quiet",
                        "--filter",
                        f"name=^/{recovery.hub_container_name}$",
                    ],
                    30,
                    "hub-stop-verification",
                    capture=True,
                )
                if listing.returncode == 0 and not (listing.stdout or "").strip():
                    return True
            except ControllerError:
                pass
        try:
            kill = self._invoke(
                [str(recovery.docker_executable), "kill", recovery.hub_container_name],
                30,
                "hub-kill",
            )
        except ControllerError:
            return False
        if kill.returncode != 0:
            return False
        try:
            final = self._invoke(
                [
                    str(recovery.docker_executable),
                    "inspect",
                    "--format",
                    "{{.State.Running}}",
                    recovery.hub_container_name,
                ],
                30,
                "hub-stop-verification",
                capture=True,
            )
        except ControllerError:
            return False
        return final.returncode == 0 and (final.stdout or "").strip() == "false"

    def _failed_recovery(
        self,
        now: dt.datetime,
        lease: Lease | None,
        recovery: RecoveryConfig,
        reason_code: str,
        gates: dict[str, bool],
        *,
        stale: bool | None = True,
    ) -> tuple[dict[str, Any], int]:
        try:
            stopped = self._stop_hub(recovery)
        except ControllerError:
            stopped = False
        gates["hub_stopped"] = stopped
        if stopped:
            secure_unlink(self.config.lease_file, self.config.deployment_uid)
        evidence = self._evidence(
            now=now,
            action="hub-stopped" if stopped else "hub-stop-unverified",
            status="failed",
            reason_code=reason_code if stopped else "hub-stop-unverified",
            lease=lease,
            stale=stale,
            gates=gates,
        )
        self._write_evidence(evidence)
        return evidence, 1

    def reconcile_no_lkg(
        self,
        *,
        force: bool,
        env_file: Path,
        rotation_state_file: Path,
        python_executable: Path,
        docker_executable: Path,
        hub_container_name: str,
        stop_timeout_seconds: int,
    ) -> tuple[dict[str, Any], int]:
        if self.config.enforce_production_paths and (
            env_file != Path("/opt/xframework/xeon-dev.env")
            or rotation_state_file != DEPLOYMENT_ROOT / "phase0-rotation-state.json"
            or docker_executable != Path("/usr/bin/docker")
        ):
            raise ControllerError("unapproved-production-path")
        if not SAFE_CONTAINER_NAME.fullmatch(hub_container_name):
            raise ControllerError("invalid-hub-container-name")
        if not 1 <= stop_timeout_seconds <= 300:
            raise ControllerError("invalid-stop-timeout")
        recovery = RecoveryConfig(
            lkg_pointer=self.config.effective_lkg_pointer,
            env_file=env_file,
            rotation_state_file=rotation_state_file,
            rotation_manager=rotation_state_file,
            runtime_verifier=rotation_state_file,
            recovery_gate_hook=rotation_state_file,
            python_executable=python_executable,
            docker_executable=docker_executable,
            services=PHASE0_SERVICES,
            hub_container_name=hub_container_name,
            subprocess_timeout_seconds=900,
            stop_timeout_seconds=stop_timeout_seconds,
        )
        with self._lease_lock():
            now = self._now()
            lease = self._read_lease(allow_activation_transition=True)
            if lease is None:
                gates = {
                    "rotation_aborted": False,
                    "restore_applied": False,
                    "runtime_verified": False,
                    "recovery_gate_verified": False,
                    "hub_stopped": False,
                }
                try:
                    gates["hub_stopped"] = self._stop_hub(recovery)
                except ControllerError:
                    gates["hub_stopped"] = False
                evidence = self._evidence(
                    now=now,
                    action="hub-stopped" if gates["hub_stopped"] else "hub-stop-unverified",
                    status="passed" if gates["hub_stopped"] else "failed",
                    reason_code=(
                        "no-active-lease-no-lkg-hub-stopped"
                        if gates["hub_stopped"]
                        else "hub-stop-unverified"
                    ),
                    lease=None,
                    stale=None,
                    gates=gates,
                )
                self._write_evidence(evidence)
                return evidence, 0 if gates["hub_stopped"] else 1
            stale = self._is_stale(lease, now)
            if not force and not stale:
                evidence = self._evidence(
                    now=now,
                    action="noop",
                    status="noop",
                    reason_code="lease-fresh-no-lkg",
                    lease=lease,
                    stale=False,
                )
                self._write_evidence(evidence)
                return evidence, 0

            rotation_manager = lease.run_directory / "manage-bolt-phase0-rotation.py"
            recovery = dataclasses.replace(
                recovery,
                rotation_manager=rotation_manager,
                runtime_verifier=rotation_manager,
                recovery_gate_hook=rotation_manager,
            )
            gates = {
                "rotation_aborted": False,
                "restore_applied": False,
                "runtime_verified": False,
                "recovery_gate_verified": False,
                "hub_stopped": False,
            }
            try:
                gates["hub_stopped"] = self._stop_hub(recovery)
            except ControllerError:
                gates["hub_stopped"] = False
            if not gates["hub_stopped"]:
                evidence = self._evidence(
                    now=now,
                    action="hub-stop-unverified",
                    status="failed",
                    reason_code="hub-stop-unverified",
                    lease=lease,
                    stale=stale,
                    gates=gates,
                )
                self._write_evidence(evidence)
                return evidence, 1

            if lease.mutation_began:
                secure_unlink(self.config.lease_file, self.config.deployment_uid)
                evidence = self._evidence(
                    now=now,
                    action="hub-stopped",
                    status="failed",
                    reason_code="no-qualified-lkg-after-mutation",
                    lease=lease,
                    stale=stale,
                    gates=gates,
                )
                self._write_evidence(evidence)
                return evidence, 1

            try:
                metadata = validate_file(
                    rotation_manager,
                    self.config.deployment_uid,
                    max_bytes=MAX_ARTIFACT_BYTES,
                )
                if os.name != "nt" and (
                    metadata.st_uid != self.config.deployment_uid
                    or stat.S_IMODE(metadata.st_mode) != 0o700
                ):
                    raise ControllerError("invalid-no-lkg-rotation-manager")
                self._abort_prepared(recovery)
                gates["rotation_aborted"] = True
            except ControllerError as error:
                evidence = self._evidence(
                    now=now,
                    action="hub-stopped",
                    status="failed",
                    reason_code=error.code,
                    lease=lease,
                    stale=stale,
                    gates=gates,
                )
                self._write_evidence(evidence)
                return evidence, 1

            secure_unlink(self.config.lease_file, self.config.deployment_uid)
            evidence = self._evidence(
                now=now,
                action="aborted-prepared",
                status="passed",
                reason_code="no-lkg-pre-mutation-aborted",
                lease=lease,
                stale=stale,
                gates=gates,
            )
            self._write_evidence(evidence)
            return evidence, 0

    def reconcile(self, recovery: RecoveryConfig) -> tuple[dict[str, Any], int]:
        self._validate_recovery(recovery)
        with self._lease_lock():
            now = self._now()
            lease = self._read_lease(allow_activation_transition=True)
            if lease is None:
                evidence = self._evidence(
                    now=now,
                    action="noop",
                    status="noop",
                    reason_code="no-active-lease",
                    lease=None,
                    stale=None,
                )
                self._write_evidence(evidence, preserve_existing=True)
                return evidence, 0
            if not self._is_stale(lease, now):
                evidence = self._evidence(
                    now=now,
                    action="noop",
                    status="noop",
                    reason_code="lease-fresh",
                    lease=lease,
                    stale=False,
                )
                self._write_evidence(evidence)
                return evidence, 0

            gates = {
                "rotation_aborted": False,
                "restore_applied": False,
                "runtime_verified": False,
                "recovery_gate_verified": False,
                "hub_stopped": False,
            }
            try:
                artifacts = self._resolve_lkg(recovery)
                self._validate_bound_recovery_helpers(recovery, artifacts)
            except ControllerError as error:
                return self._failed_recovery(now, lease, recovery, error.code, gates)
            if not lease.mutation_began:
                try:
                    self._abort_prepared(recovery)
                    gates["rotation_aborted"] = True
                except ControllerError as error:
                    return self._failed_recovery(now, lease, recovery, error.code, gates)
                secure_unlink(self.config.lease_file, self.config.deployment_uid)
                evidence = self._evidence(
                    now=now,
                    action="aborted-prepared",
                    status="passed",
                    reason_code="stale-pre-mutation-aborted",
                    lease=lease,
                    stale=True,
                    gates=gates,
                )
                self._write_evidence(evidence)
                return evidence, 0

            try:
                self._restore(recovery, artifacts)
                gates["restore_applied"] = True
                self._runtime_gate(recovery, artifacts)
                gates["runtime_verified"] = True
                self._recovery_gate(recovery, artifacts)
                gates["recovery_gate_verified"] = True
            except ControllerError as error:
                return self._failed_recovery(now, lease, recovery, error.code, gates)

            secure_unlink(self.config.lease_file, self.config.deployment_uid)
            evidence = self._evidence(
                now=now,
                action="restored",
                status="passed",
                reason_code="security-qualified-lkg-restored",
                lease=lease,
                stale=True,
                gates=gates,
            )
            self._write_evidence(evidence)
            return evidence, 0

    def force_recovery(self, recovery: RecoveryConfig) -> tuple[dict[str, Any], int]:
        self._validate_recovery(recovery)
        with self._lease_lock():
            now = self._now()
            lease = self._read_lease(allow_activation_transition=True)
            gates = {
                "rotation_aborted": False,
                "restore_applied": False,
                "runtime_verified": False,
                "recovery_gate_verified": False,
                "hub_stopped": False,
            }
            try:
                artifacts = self._resolve_lkg(recovery)
                self._validate_bound_recovery_helpers(recovery, artifacts)
                if lease is not None and not lease.mutation_began:
                    self._abort_prepared(recovery)
                    gates["rotation_aborted"] = True
                self._restore(recovery, artifacts)
                gates["restore_applied"] = True
                self._runtime_gate(recovery, artifacts)
                gates["runtime_verified"] = True
                self._recovery_gate(recovery, artifacts)
                gates["recovery_gate_verified"] = True
            except ControllerError as error:
                return self._failed_recovery(
                    now,
                    lease,
                    recovery,
                    error.code,
                    gates,
                    stale=None if lease is None else self._is_stale(lease, now),
                )
            if lease is not None:
                secure_unlink(self.config.lease_file, self.config.deployment_uid)
            evidence = self._evidence(
                now=now,
                action="force-restored",
                status="passed",
                reason_code="forced-security-qualified-lkg-restored",
                lease=lease,
                stale=None if lease is None else self._is_stale(lease, now),
                gates=gates,
            )
            self._write_evidence(evidence)
            return evidence, 0


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project-name", required=True)
    parser.add_argument("--deployment-uid", required=True, type=int)
    subparsers = parser.add_subparsers(dest="command", required=True)

    arm = subparsers.add_parser("arm")
    arm.add_argument("--run-id", required=True)
    arm.add_argument("--run-attempt", required=True, type=int)
    arm.add_argument("--phase", required=True)
    arm.add_argument("--stale-timeout-seconds", required=True, type=int)

    heartbeat = subparsers.add_parser("heartbeat")
    heartbeat.add_argument("--run-id", required=True)
    heartbeat.add_argument("--run-attempt", required=True, type=int)
    heartbeat.add_argument("--phase", required=True)
    heartbeat.add_argument("--mutation-began", action="store_true")

    disarm = subparsers.add_parser("disarm")
    disarm.add_argument("--run-id", required=True)
    disarm.add_argument("--run-attempt", required=True, type=int)

    supervise = subparsers.add_parser("supervise")
    supervise.add_argument("--run-id", required=True)
    supervise.add_argument("--run-attempt", required=True, type=int)
    supervise.add_argument("--phase", required=True)
    supervise.add_argument("--mutation-began", action="store_true")
    def supervised_timeout(value: str) -> int:
        try:
            parsed = int(value)
        except ValueError as error:
            raise argparse.ArgumentTypeError("invalid supervised timeout") from error
        if not MIN_SUPERVISED_TIMEOUT_SECONDS <= parsed <= MAX_SUPERVISED_TIMEOUT_SECONDS:
            raise argparse.ArgumentTypeError("supervised timeout is outside the safe range")
        return parsed

    supervise.add_argument("--timeout-seconds", required=True, type=supervised_timeout)
    supervise.add_argument("--quiet", action="store_true")
    supervise.add_argument("supervised_command", nargs=argparse.REMAINDER)

    subparsers.add_parser("status")
    subparsers.add_parser("require-fresh")

    def add_no_lkg_arguments(command: argparse.ArgumentParser) -> None:
        command.add_argument("--env-file", required=True, type=Path)
        command.add_argument("--rotation-state-file", required=True, type=Path)
        command.add_argument("--python-executable", required=True, type=Path)
        command.add_argument("--docker-executable", required=True, type=Path)
        command.add_argument("--hub-container-name", default="xframework-bolt-hub")
        command.add_argument("--stop-timeout-seconds", type=int, default=30)

    add_no_lkg_arguments(subparsers.add_parser("watch-no-lkg"))
    add_no_lkg_arguments(subparsers.add_parser("force-no-lkg"))

    def add_recovery_arguments(command: argparse.ArgumentParser) -> None:
        command.add_argument("--env-file", required=True, type=Path)
        command.add_argument("--rotation-state-file", required=True, type=Path)
        command.add_argument("--rotation-manager", required=True, type=Path)
        command.add_argument("--runtime-verifier", required=True, type=Path)
        command.add_argument("--recovery-gate-hook", required=True, type=Path)
        command.add_argument("--python-executable", required=True, type=Path)
        command.add_argument("--docker-executable", required=True, type=Path)
        command.add_argument("--service", action="append", required=True)
        command.add_argument("--hub-container-name", default="xframework-bolt-hub")
        command.add_argument("--subprocess-timeout-seconds", type=int, default=900)
        command.add_argument("--stop-timeout-seconds", type=int, default=30)

    add_recovery_arguments(subparsers.add_parser("reconcile"))
    add_recovery_arguments(subparsers.add_parser("watch-once"))
    add_recovery_arguments(subparsers.add_parser("force-recovery"))
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv)
    config = ControllerConfig(
        state_root=APPROVED_STATE_ROOT,
        run_root=APPROVED_RUN_ROOT,
        project_name=args.project_name,
        deployment_uid=args.deployment_uid,
        enforce_production_paths=True,
        lkg_pointer=APPROVED_LKG_POINTER,
    )
    try:
        controller = DeploymentLeaseController(config)
        if args.command == "arm":
            evidence, exit_code = controller.arm(
                args.run_id,
                args.run_attempt,
                args.phase,
                args.stale_timeout_seconds,
            )
        elif args.command == "heartbeat":
            evidence, exit_code = controller.heartbeat(
                args.run_id,
                args.run_attempt,
                args.phase,
                args.mutation_began,
            )
        elif args.command == "disarm":
            evidence, exit_code = controller.disarm(args.run_id, args.run_attempt)
        elif args.command == "supervise":
            command = args.supervised_command
            if command[:1] == ["--"]:
                command = command[1:]
            evidence, exit_code = controller.supervise(
                args.run_id,
                args.run_attempt,
                args.phase,
                args.mutation_began,
                args.timeout_seconds,
                command,
            )
        elif args.command == "status":
            evidence, exit_code = controller.status()
        elif args.command == "require-fresh":
            evidence, exit_code = controller.require_fresh()
        elif args.command in {"watch-no-lkg", "force-no-lkg"}:
            evidence, exit_code = controller.reconcile_no_lkg(
                force=args.command == "force-no-lkg",
                env_file=args.env_file,
                rotation_state_file=args.rotation_state_file,
                python_executable=args.python_executable,
                docker_executable=args.docker_executable,
                hub_container_name=args.hub_container_name,
                stop_timeout_seconds=args.stop_timeout_seconds,
            )
        else:
            recovery = RecoveryConfig(
                lkg_pointer=APPROVED_LKG_POINTER,
                env_file=args.env_file,
                rotation_state_file=args.rotation_state_file,
                rotation_manager=args.rotation_manager,
                runtime_verifier=args.runtime_verifier,
                recovery_gate_hook=args.recovery_gate_hook,
                python_executable=args.python_executable,
                docker_executable=args.docker_executable,
                services=tuple(args.service),
                hub_container_name=args.hub_container_name,
                subprocess_timeout_seconds=args.subprocess_timeout_seconds,
                stop_timeout_seconds=args.stop_timeout_seconds,
            )
            evidence, exit_code = (
                controller.force_recovery(recovery)
                if args.command == "force-recovery"
                else controller.reconcile(recovery)
            )
    except ControllerError as error:
        print(
            json.dumps(
                {
                    "schema": EVIDENCE_SCHEMA,
                    "status": "failed",
                    "action": "rejected",
                    "reason_code": error.code,
                },
                sort_keys=True,
            )
        )
        return 1
    if not (args.command == "supervise" and args.quiet):
        print(json.dumps(evidence, sort_keys=True))
    return exit_code


if __name__ == "__main__":
    if len(sys.argv) >= 6 and sys.argv[1] == "__child-supervisor":
        try:
            separator = sys.argv.index("--", 5)
            if separator != 5:
                raise ValueError
            exit_code = _child_supervisor_main(
                int(sys.argv[2]),
                int(sys.argv[3]),
                int(sys.argv[4]),
                sys.argv[separator + 1 :],
            )
        except (ValueError, IndexError):
            exit_code = 125
        raise SystemExit(exit_code)
    raise SystemExit(main())
