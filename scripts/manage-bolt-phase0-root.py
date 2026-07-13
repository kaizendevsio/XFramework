#!/usr/bin/python3
"""Fixed root bootstrap and activation boundary for Bolt Phase 0 recovery."""

from __future__ import annotations

import argparse
import contextlib
import datetime as dt
import hashlib
import json
import os
import re
import secrets
import shutil
import stat
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Iterator, Sequence

try:
    import grp
    import pwd
except ImportError:  # pragma: no cover - Windows test import
    grp = None  # type: ignore[assignment]
    pwd = None  # type: ignore[assignment]


RUN_ID = re.compile(r"[1-9][0-9]{0,31}")
ATTEMPT = re.compile(r"[1-9][0-9]{0,5}")
COMMIT = re.compile(r"[0-9a-f]{40}")
PROJECT = re.compile(r"[a-z0-9][a-z0-9_-]{0,62}")
PHASE = re.compile(r"[a-z][a-z0-9-]{0,47}")
ENV_NAME = re.compile(r"[A-Za-z_][A-Za-z0-9_]*")
PROXY_MODES = frozenset({"direct-kestrel"})
QUALIFICATION_SCHEMA = "xframework.bolt.phase0.qualification.v1"
MAX_FILE_BYTES = 64 * 1024 * 1024
MAX_PROTECTED_ENV_BYTES = 1024 * 1024
MAX_TOTAL_BYTES = 1024 * 1024 * 1024
MAX_FILES = 256
WATCHDOG_TIMEOUT_SECONDS = 4_200
WATCHDOG_TIMEOUT_SYSTEMD = "1h 10min"
LEASE_SCHEMA = "xframework.bolt.phase0.deployment-lease.v1"
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
MIN_STALE_SECONDS = 60
MAX_STALE_SECONDS = 86_400

RECOVERY_EXECUTABLES = {
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
FIXED_COMPONENTS = {
    "manage-bolt-phase0-deployment-lease.py": Path(
        "/usr/local/libexec/xframework-bolt-phase0/manage-bolt-phase0-deployment-lease.py"
    ),
    "run-bolt-phase0-watchdog.sh": Path("/usr/local/sbin/xframework-bolt-phase0-watchdog"),
    "verify-bolt-phase0-qualification.py": Path(
        "/usr/local/libexec/xframework-bolt-phase0/verify-bolt-phase0-qualification.py"
    ),
    "xframework-bolt-phase0-watchdog.service": Path(
        "/etc/systemd/system/xframework-bolt-phase0-watchdog.service"
    ),
    "xframework-bolt-phase0-watchdog.timer": Path(
        "/etc/systemd/system/xframework-bolt-phase0-watchdog.timer"
    ),
}
FIXED_COMPONENT_MODES = {
    "manage-bolt-phase0-deployment-lease.py": 0o555,
    "run-bolt-phase0-watchdog.sh": 0o555,
    "verify-bolt-phase0-qualification.py": 0o444,
    "xframework-bolt-phase0-watchdog.service": 0o644,
    "xframework-bolt-phase0-watchdog.timer": 0o644,
}


class RootBoundaryError(RuntimeError):
    def __init__(self, code: str):
        super().__init__(code)
        self.code = code


@dataclass(frozen=True)
class RootPaths:
    deploy_root: Path = Path("/home/github-runner/xframework-deploy")
    run_root: Path = Path("/home/github-runner/xframework-deploy/runs")
    quarantine_root: Path = Path("/home/github-runner/xframework-deploy/quarantine")
    lkg_root: Path = Path("/home/github-runner/xframework-deploy/phase0-last-known-good")
    state_root: Path = Path("/home/github-runner/xframework-deploy/phase0-watchdog")
    hooks_root: Path = Path("/home/github-runner/xframework-deploy/hooks")
    pointer: Path = Path("/home/github-runner/xframework-deploy/phase0-last-known-good/current")
    root_helper: Path = Path("/usr/local/sbin/xframework-bolt-phase0-root")
    watchdog: Path = Path("/usr/local/sbin/xframework-bolt-phase0-watchdog")
    qualifier: Path = Path(
        "/usr/local/libexec/xframework-bolt-phase0/verify-bolt-phase0-qualification.py"
    )
    lease_manager: Path = Path(
        "/usr/local/libexec/xframework-bolt-phase0/manage-bolt-phase0-deployment-lease.py"
    )
    lease_lock: Path = Path(
        "/usr/local/libexec/xframework-bolt-phase0/deployment-lease.lock"
    )
    service_fragment: Path = Path(
        "/etc/systemd/system/xframework-bolt-phase0-watchdog.service"
    )
    timer_fragment: Path = Path(
        "/etc/systemd/system/xframework-bolt-phase0-watchdog.timer"
    )
    python_link: Path = Path("/usr/bin/python3")
    docker: Path = Path("/usr/bin/docker")
    systemctl: Path = Path("/usr/bin/systemctl")
    protected_env: Path = Path("/opt/xframework/xeon-dev.env")

Runner = Callable[[list[str], bool], subprocess.CompletedProcess[str]]
SyncPath = Callable[[Path, bool], None]
OperationTrace = Callable[[str, Path], None]
Clock = Callable[[], dt.datetime]


def utc_now() -> dt.datetime:
    return dt.datetime.now(dt.timezone.utc).replace(microsecond=0)


def default_runner(command: list[str], capture: bool) -> subprocess.CompletedProcess[str]:
    timeout = (
        60
        if len(command) > 1 and command[1] in {"inspect", "info", "stop", "kill", "container"}
        else 1800
    )
    return subprocess.run(
        command,
        stdin=subprocess.DEVNULL,
        stdout=subprocess.PIPE if capture else subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        text=True,
        check=False,
        timeout=timeout,
        close_fds=True,
    )


def durable_sync(path: Path, is_directory: bool) -> None:
    if os.name != "posix":
        return
    flags = os.O_RDONLY
    if is_directory:
        flags |= getattr(os, "O_DIRECTORY", 0)
    descriptor = os.open(path, flags | getattr(os, "O_NOFOLLOW", 0))
    try:
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


def _absolute(path: Path) -> Path:
    if not path.is_absolute() or ".." in path.parts or any(ord(c) < 0x20 for c in str(path)):
        raise RootBoundaryError("invalid-path")
    return path


def _no_symlink_components(path: Path) -> None:
    path = _absolute(path)
    current = Path(path.anchor)
    for part in path.parts[1:]:
        current /= part
        try:
            metadata = current.lstat()
        except OSError as error:
            raise RootBoundaryError("missing-path") from error
        if stat.S_ISLNK(metadata.st_mode):
            raise RootBoundaryError("symlink-path")


def _directory(path: Path, uid: int, mode: int) -> os.stat_result:
    _no_symlink_components(path)
    metadata = path.lstat()
    if (
        not stat.S_ISDIR(metadata.st_mode)
        or (os.name == "posix" and metadata.st_uid != uid)
        or (os.name == "posix" and stat.S_IMODE(metadata.st_mode) != mode)
    ):
        raise RootBoundaryError("insecure-directory")
    return metadata


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
        raise RootBoundaryError("insecure-lease-lock")


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
            raise RootBoundaryError("insecure-lease-lock-parent")
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
                raise RootBoundaryError("insecure-lease-lock-parent")
        return descriptor, _lease_lock_parent_metadata(os.fstat(descriptor))
    except BaseException:
        os.close(descriptor)
        raise


@contextlib.contextmanager
def exclusive_lease_lock(
    path: Path,
    *,
    owner_uid: int,
    owner_gid: int,
    trusted_parent_uid: int,
) -> Iterator[None]:
    path = _absolute(path)
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
            except (OSError, RootBoundaryError) as error:
                raise RootBoundaryError("insecure-lease-lock-parent") from error

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
            raise RootBoundaryError("insecure-lease-lock") from error

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
            except (OSError, RootBoundaryError) as error:
                raise RootBoundaryError("lease-lock-replaced") from error
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
                raise RootBoundaryError("lease-lock-replaced")
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
            raise RootBoundaryError("insecure-lease-lock") from error
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


def _file(path: Path, uid: int, mode: int, maximum: int = MAX_FILE_BYTES) -> bytes:
    _no_symlink_components(path)
    metadata = path.lstat()
    if (
        not stat.S_ISREG(metadata.st_mode)
        or stat.S_ISLNK(metadata.st_mode)
        or metadata.st_nlink != 1
        or metadata.st_size > maximum
        or (os.name == "posix" and metadata.st_uid != uid)
        or (os.name == "posix" and stat.S_IMODE(metadata.st_mode) != mode)
    ):
        raise RootBoundaryError("insecure-file")
    descriptor = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
    try:
        current = os.fstat(descriptor)
        if (current.st_dev, current.st_ino) != (metadata.st_dev, metadata.st_ino):
            raise RootBoundaryError("file-replaced")
        raw = bytearray()
        while len(raw) <= maximum:
            chunk = os.read(descriptor, min(64 * 1024, maximum + 1 - len(raw)))
            if not chunk:
                break
            raw.extend(chunk)
        after = os.fstat(descriptor)
    finally:
        os.close(descriptor)
    if (
        len(raw) != current.st_size
        or len(raw) > maximum
        or (current.st_size, current.st_mtime_ns) != (after.st_size, after.st_mtime_ns)
    ):
        raise RootBoundaryError("file-changed")
    return bytes(raw)


def resolve_system_python(path: Path, *, require_root: bool = True) -> Path:
    path = _absolute(path)
    try:
        resolved = Path(os.path.realpath(path, strict=True))
        metadata = resolved.lstat()
    except (OSError, TypeError) as error:
        raise RootBoundaryError("invalid-python") from error
    if (
        not resolved.is_absolute()
        or stat.S_ISLNK(metadata.st_mode)
        or not stat.S_ISREG(metadata.st_mode)
        or (os.name == "posix" and not stat.S_IMODE(metadata.st_mode) & 0o111)
        or (os.name == "posix" and stat.S_IMODE(metadata.st_mode) & 0o022)
        or (require_root and os.name == "posix" and metadata.st_uid != 0)
    ):
        raise RootBoundaryError("invalid-python")
    _no_symlink_components(resolved)
    return resolved


class RootBoundary:
    def __init__(
        self,
        paths: RootPaths = RootPaths(),
        *,
        runner: Runner = default_runner,
        deployment_user: str = "github-runner",
        enforce_root: bool = True,
        after_quarantine: Callable[[Path, Path], None] | None = None,
        after_artifact_lstat: Callable[[Path], None] | None = None,
        sync_path: SyncPath = durable_sync,
        operation_trace: OperationTrace | None = None,
        clock: Clock = utc_now,
    ) -> None:
        self.paths = paths
        self.runner = runner
        self.enforce_root = enforce_root
        self.after_quarantine = after_quarantine
        self.after_artifact_lstat = after_artifact_lstat
        self.sync_path = sync_path
        self.operation_trace = operation_trace
        self.clock = clock
        if enforce_root and os.name == "posix" and os.geteuid() != 0:
            raise RootBoundaryError("root-required")
        if not enforce_root:
            self.deployment_uid = os.geteuid() if hasattr(os, "geteuid") else 0
            self.deployment_gid = os.getegid() if hasattr(os, "getegid") else 0
        else:
            try:
                if pwd is None or grp is None:
                    raise KeyError(deployment_user)
                self.deployment_uid = pwd.getpwnam(deployment_user).pw_uid
                self.deployment_gid = grp.getgrnam(deployment_user).gr_gid
            except KeyError as error:
                raise RootBoundaryError("missing-deployment-user") from error
        self.python = resolve_system_python(paths.python_link, require_root=enforce_root)

    def _sync(self, path: Path, is_directory: bool = False) -> None:
        self.sync_path(path, is_directory)
        if self.operation_trace is not None:
            self.operation_trace("fsync-dir" if is_directory else "fsync-file", path)

    def _trace(self, operation: str, path: Path) -> None:
        if self.operation_trace is not None:
            self.operation_trace(operation, path)

    def _run(self, command: list[str], code: str, *, capture: bool = False) -> str:
        try:
            result = self.runner(command, capture)
        except (OSError, subprocess.SubprocessError) as error:
            raise RootBoundaryError(code) from error
        if result.returncode != 0:
            raise RootBoundaryError(code)
        return result.stdout or ""

    def _show(self, unit: str, prop: str) -> str:
        return self._run(
            [str(self.paths.systemctl), "show", unit, f"--property={prop}", "--value"],
            "systemd-contract",
            capture=True,
        ).strip()

    def _validate_systemd(self, *, require_active: bool = True) -> None:
        service = "xframework-bolt-phase0-watchdog.service"
        timer = "xframework-bolt-phase0-watchdog.timer"
        expected = {
            (service, "DropInPaths"): "",
            (timer, "DropInPaths"): "",
            (service, "FragmentPath"): str(self.paths.service_fragment),
            (timer, "FragmentPath"): str(self.paths.timer_fragment),
            (timer, "Unit"): service,
            (service, "ExecCondition"): "",
            (service, "ExecStartPre"): "",
            (service, "ExecStartPost"): "",
            (service, "User"): "github-runner",
            (service, "Group"): "github-runner",
            (service, "SupplementaryGroups"): "docker",
            (service, "NoNewPrivileges"): "yes",
            (service, "PrivateTmp"): "yes",
            (service, "ProtectSystem"): "strict",
            (service, "ProtectHome"): "read-only",
            (service, "LockPersonality"): "yes",
            (service, "MemoryDenyWriteExecute"): "yes",
            (service, "ProtectKernelTunables"): "yes",
            (service, "ProtectKernelModules"): "yes",
            (service, "ProtectControlGroups"): "yes",
            (service, "RestrictSUIDSGID"): "yes",
            (service, "UMask"): "0077",
            (service, "ReadWritePaths"): f"{self.paths.deploy_root} {self.paths.protected_env.parent}",
            (service, "RestrictAddressFamilies"): "AF_UNIX AF_INET AF_INET6",
            (service, "TimeoutStartUSec"): WATCHDOG_TIMEOUT_SYSTEMD,
            (timer, "AccuracyUSec"): "1s",
            (timer, "Persistent"): "yes",
            (timer, "UnitFileState"): "enabled",
            (timer, "ActiveState"): "active" if require_active else "inactive",
        }
        for (unit, prop), value in expected.items():
            if self._show(unit, prop) != value:
                raise RootBoundaryError("systemd-contract")
        exec_start = self._show(service, "ExecStart")
        launcher = re.escape(str(self.paths.watchdog))
        if len(re.findall(r"\{\s*path=", exec_start)) != 1 or not re.search(
            rf"\{{\s*path={launcher}\s*;\s*argv\[\]={launcher}\s*;", exec_start
        ):
            raise RootBoundaryError("systemd-contract")
        timers = self._show(timer, "TimersMonotonic")
        if "OnBootUSec=30s" not in timers or "OnUnitActiveUSec=30s" not in timers:
            raise RootBoundaryError("systemd-contract")

    def _hub_state(self) -> str:
        try:
            result = self.runner(
                [
                    str(self.paths.docker),
                    "inspect",
                    "--format",
                    "{{.State.Running}}",
                    "xframework-bolt-hub",
                ],
                True,
            )
        except (OSError, subprocess.SubprocessError) as error:
            raise RootBoundaryError("docker-inspection-failed") from error
        if result.returncode == 0:
            state = (result.stdout or "").strip()
            if state == "true":
                return "running"
            if state == "false":
                return "stopped"
            raise RootBoundaryError("docker-inspection-failed")
        try:
            containers = self.runner(
                [
                    str(self.paths.docker),
                    "container",
                    "ls",
                    "-a",
                    "--no-trunc",
                    "--filter",
                    "name=^/xframework-bolt-hub$",
                    "--format",
                    "{{.Names}}",
                ],
                True,
            )
        except (OSError, subprocess.SubprocessError) as error:
            raise RootBoundaryError("docker-inspection-failed") from error
        if containers.returncode != 0 or (containers.stdout or "") != "":
            raise RootBoundaryError("docker-inspection-failed")
        try:
            daemon = self.runner(
                [str(self.paths.docker), "info", "--format", "{{.ServerVersion}}"],
                True,
            )
        except (OSError, subprocess.SubprocessError) as error:
            raise RootBoundaryError("docker-inspection-failed") from error
        if daemon.returncode != 0 or not (daemon.stdout or "").strip():
            raise RootBoundaryError("docker-inspection-failed")
        return "absent"

    def _hub_stopped(self) -> bool:
        return self._hub_state() in {"stopped", "absent"}

    def stop_hub(self) -> bool:
        if self._hub_stopped():
            return True
        try:
            stop = self.runner(
                [str(self.paths.docker), "stop", "--time", "30", "xframework-bolt-hub"],
                False,
            )
        except (OSError, subprocess.SubprocessError) as error:
            raise RootBoundaryError("docker-stop-failed") from error
        if stop.returncode == 0 and self._hub_stopped():
            return True
        try:
            kill = self.runner(
                [str(self.paths.docker), "kill", "xframework-bolt-hub"], False
            )
        except (OSError, subprocess.SubprocessError) as error:
            raise RootBoundaryError("docker-stop-failed") from error
        return kill.returncode == 0 and self._hub_stopped()

    @contextlib.contextmanager
    def _lease_lock(self) -> Iterator[None]:
        path = self.paths.lease_lock
        trusted_uid = 0 if self.enforce_root else self.deployment_uid
        with exclusive_lease_lock(
            path,
            owner_uid=trusted_uid,
            owner_gid=self.deployment_gid,
            trusted_parent_uid=trusted_uid,
        ):
            self._trace("lease-lock-acquired", path)
            try:
                yield
            finally:
                self._trace("lease-lock-releasing", path)

    def _validate_activation_lease(
        self, run_id: str, run_attempt: int, run_directory: Path, project: str
    ) -> None:
        lease_path = self.paths.state_root / "deployment-lease.json"
        raw = _file(lease_path, self.deployment_uid, 0o600, 64 * 1024)

        def exact_object(pairs: list[tuple[str, object]]) -> dict[str, object]:
            document: dict[str, object] = {}
            for key, value in pairs:
                if key in document:
                    raise ValueError("duplicate-key")
                document[key] = value
            return document

        def reject_constant(_: str) -> object:
            raise ValueError("invalid-constant")

        try:
            document = json.loads(
                raw.decode("utf-8", errors="strict"),
                object_pairs_hook=exact_object,
                parse_constant=reject_constant,
            )
        except (UnicodeDecodeError, json.JSONDecodeError, ValueError) as error:
            raise RootBoundaryError("invalid-activation-lease") from error
        if not isinstance(document, dict) or set(document) != LEASE_KEYS:
            raise RootBoundaryError("invalid-activation-lease")
        stale_timeout = document.get("stale_timeout_seconds")
        mutation_began = document.get("mutation_began")
        if (
            document.get("schema") != LEASE_SCHEMA
            or document.get("run_id") != run_id
            or type(document.get("run_attempt")) is not int
            or document.get("run_attempt") != run_attempt
            or document.get("run_directory") != str(run_directory)
            or document.get("project_name") != project
            or not isinstance(document.get("phase"), str)
            or PHASE.fullmatch(document["phase"]) is None
            or isinstance(stale_timeout, bool)
            or not isinstance(stale_timeout, int)
            or not MIN_STALE_SECONDS <= stale_timeout <= MAX_STALE_SECONDS
            or mutation_began is not True
        ):
            raise RootBoundaryError("invalid-activation-lease")
        heartbeat_text = document.get("heartbeat_utc")
        if not isinstance(heartbeat_text, str):
            raise RootBoundaryError("invalid-activation-lease")
        try:
            heartbeat = dt.datetime.strptime(
                heartbeat_text, "%Y-%m-%dT%H:%M:%SZ"
            ).replace(tzinfo=dt.timezone.utc)
        except ValueError as error:
            raise RootBoundaryError("invalid-activation-lease") from error
        now = self.clock().astimezone(dt.timezone.utc).replace(microsecond=0)
        if heartbeat > now or now - heartbeat >= dt.timedelta(seconds=stale_timeout):
            raise RootBoundaryError("stale-activation-lease")

    def _validate_fixed_files(self) -> None:
        root_uid = 0 if self.enforce_root else self.deployment_uid
        for parent in {
            self.paths.root_helper.parent,
            self.paths.watchdog.parent,
            self.paths.lease_manager.parent,
            self.paths.qualifier.parent,
            self.paths.service_fragment.parent,
            self.paths.timer_fragment.parent,
        }:
            _directory(parent, root_uid, 0o755)
        _file(self.paths.root_helper, root_uid, 0o555)
        _file(self.paths.watchdog, root_uid, 0o555)
        _file(self.paths.lease_manager, root_uid, 0o555)
        _file(self.paths.qualifier, root_uid, 0o444)
        _file(self.paths.service_fragment, root_uid, 0o644, 1024 * 1024)
        _file(self.paths.timer_fragment, root_uid, 0o644, 1024 * 1024)
        with exclusive_lease_lock(
            self.paths.lease_lock,
            owner_uid=root_uid,
            owner_gid=self.deployment_gid,
            trusted_parent_uid=root_uid,
        ):
            pass

    def _validate_roots(self) -> None:
        root_uid = 0 if self.enforce_root else self.deployment_uid
        _directory(self.paths.deploy_root, root_uid, 0o755)
        _directory(self.paths.run_root, root_uid, 0o755)
        _directory(self.paths.quarantine_root, root_uid, 0o700)
        _directory(self.paths.lkg_root, root_uid, 0o755)
        _directory(self.paths.state_root, self.deployment_uid, 0o700)
        _directory(self.paths.hooks_root, self.deployment_uid, 0o700)
        protected_parent = _directory(
            self.paths.protected_env.parent,
            root_uid,
            0o1770,
        )
        if os.name == "posix" and protected_parent.st_gid != self.deployment_gid:
            raise RootBoundaryError("insecure-protected-env-directory")
        _file(
            self.paths.protected_env,
            self.deployment_uid,
            0o600,
            MAX_PROTECTED_ENV_BYTES,
        )
        protected_env = self.paths.protected_env.lstat()
        if os.name == "posix" and protected_env.st_gid != self.deployment_gid:
            raise RootBoundaryError("insecure-protected-env-file")

    def _protected_proxy_mode(self) -> str:
        raw = _file(
            self.paths.protected_env,
            self.deployment_uid,
            0o600,
            MAX_PROTECTED_ENV_BYTES,
        )
        if raw.startswith(b"\xef\xbb\xbf") or b"\x00" in raw:
            raise RootBoundaryError("invalid-protected-env")
        try:
            text = raw.decode("utf-8", errors="strict")
        except UnicodeDecodeError as error:
            raise RootBoundaryError("invalid-protected-env") from error

        values: dict[str, str] = {}
        for raw in text.splitlines():
            if not raw.strip() or raw.lstrip().startswith("#"):
                continue
            name, separator, value = raw.partition("=")
            if (
                not separator
                or not ENV_NAME.fullmatch(name)
                or name in values
                or value != value.strip()
                or any(ord(character) < 0x20 or ord(character) == 0x7F for character in value)
            ):
                raise RootBoundaryError("invalid-protected-env")
            values[name] = value

        proxy_mode = values.get("BOLT_SYNTHETIC_PROXY_MODE")
        if proxy_mode not in PROXY_MODES:
            raise RootBoundaryError("invalid-proxy-mode")
        if "BOLT_SYNTHETIC_PROXY_LOG_PATHS" in values:
            raise RootBoundaryError("invalid-proxy-configuration")
        return proxy_mode

    def _read_pointer(self) -> Path | None:
        if not self.paths.pointer.exists() and not self.paths.pointer.is_symlink():
            return None
        root_uid = 0 if self.enforce_root else self.deployment_uid
        raw = _file(self.paths.pointer, root_uid, 0o644, 4096)
        try:
            text = raw.decode("utf-8", errors="strict")
        except UnicodeDecodeError as error:
            raise RootBoundaryError("invalid-pointer") from error
        if text.count("\n") != 1 or not text.endswith("\n"):
            raise RootBoundaryError("invalid-pointer")
        run = Path(text[:-1])
        try:
            relative = run.relative_to(self.paths.run_root)
        except ValueError as error:
            raise RootBoundaryError("invalid-pointer") from error
        if len(relative.parts) != 1 or not re.fullmatch(
            r"[1-9][0-9]{0,31}-[1-9][0-9]{0,5}", relative.name
        ):
            raise RootBoundaryError("invalid-pointer")
        return run

    def _validate_sealed_run(self, run: Path) -> None:
        root_uid = 0 if self.enforce_root else self.deployment_uid
        _directory(run, root_uid, 0o550)
        protected_proxy_mode = self._protected_proxy_mode()
        evidence = json.loads(
            _file(run / "qualification-evidence.json", root_uid, 0o440).decode("utf-8")
        )
        if (
            evidence.get("schema") != QUALIFICATION_SCHEMA
            or evidence.get("status") != "passed"
            or evidence.get("proxy_mode") != protected_proxy_mode
        ):
            raise RootBoundaryError("invalid-qualification")
        artifacts = evidence.get("artifacts")
        if not isinstance(artifacts, dict) or not artifacts:
            raise RootBoundaryError("invalid-qualification")
        for name, summary in artifacts.items():
            if not isinstance(name, str) or Path(name).name != name or not isinstance(summary, dict):
                raise RootBoundaryError("invalid-qualification")
            mode = 0o550 if name in RECOVERY_EXECUTABLES else 0o440
            raw = _file(run / name, root_uid, mode)
            if (
                summary.get("path") != name
                or summary.get("sha256") != "sha256:" + hashlib.sha256(raw).hexdigest()
            ):
                raise RootBoundaryError("artifact-digest")
        for name, fixed in FIXED_COMPONENTS.items():
            if _file(run / name, root_uid, 0o550 if name in RECOVERY_EXECUTABLES else 0o440) != _file(
                fixed, root_uid, FIXED_COMPONENT_MODES[name]
            ):
                raise RootBoundaryError("fixed-component-mismatch")

    def verify_bootstrap(self) -> dict[str, object]:
        self._validate_roots()
        self._validate_fixed_files()
        self._validate_systemd()
        pointer = self._read_pointer()
        if pointer is None:
            self._protected_proxy_mode()
            if not self._hub_stopped():
                raise RootBoundaryError("bootstrap-hub-running")
            return {"status": "passed", "state": "bootstrap-no-lkg-hub-stopped"}
        self._validate_sealed_run(pointer)
        return {"status": "passed", "state": "qualified-lkg-active"}

    def ensure_watchdog(self) -> dict[str, object]:
        self._validate_roots()
        self._validate_fixed_files()
        self._run(
            [
                str(self.paths.systemctl),
                "enable",
                "--now",
                "xframework-bolt-phase0-watchdog.timer",
            ],
            "timer-start-failed",
        )
        self._validate_systemd()
        return {"status": "passed", "state": "watchdog-active"}

    @staticmethod
    def _identity(run_id: str, attempt: str) -> str:
        if not RUN_ID.fullmatch(run_id) or not ATTEMPT.fullmatch(attempt) or attempt != "1":
            raise RootBoundaryError("invalid-run-identity")
        return f"{run_id}-{attempt}"

    def prepare_run(self, run_id: str, attempt: str) -> dict[str, object]:
        self.verify_bootstrap()
        name = self._identity(run_id, attempt)
        target = self.paths.run_root / name
        if target.exists() or target.is_symlink():
            raise RootBoundaryError("run-exists")
        os.mkdir(target, 0o700)
        if os.name == "posix":
            os.chown(target, self.deployment_uid, self.deployment_gid)
            os.chmod(target, 0o700)
        return {"status": "passed", "state": "candidate-run-prepared", "run": name}

    def _copy_quarantined(self, source: Path, destination: Path) -> None:
        source_stat = source.lstat()
        if (
            not stat.S_ISDIR(source_stat.st_mode)
            or (os.name == "posix" and source_stat.st_uid != self.deployment_uid)
            or (os.name == "posix" and stat.S_IMODE(source_stat.st_mode) != 0o700)
        ):
            raise RootBoundaryError("invalid-candidate-directory")
        os.mkdir(destination, 0o700)
        self._sync(destination.parent, True)
        source_directory_fd: int | None = None
        destination_directory_fd: int | None = None
        use_directory_fds = (
            os.open in os.supports_dir_fd
            and os.stat in os.supports_dir_fd
            and os.listdir in os.supports_fd
        )
        try:
            if use_directory_fds:
                directory_flags = (
                    os.O_RDONLY
                    | getattr(os, "O_DIRECTORY", 0)
                    | getattr(os, "O_NOFOLLOW", 0)
                )
                source_directory_fd = os.open(source, directory_flags)
                opened_source = os.fstat(source_directory_fd)
                source_identity = (
                    source_stat.st_dev,
                    source_stat.st_ino,
                    source_stat.st_mode,
                    source_stat.st_uid,
                )
                if (
                    opened_source.st_dev,
                    opened_source.st_ino,
                    opened_source.st_mode,
                    opened_source.st_uid,
                ) != source_identity:
                    raise RootBoundaryError("candidate-directory-replaced")
                destination_directory_fd = os.open(destination, directory_flags)

            names = list(os.listdir(source_directory_fd if source_directory_fd is not None else source))
            if not names or len(names) > MAX_FILES:
                raise RootBoundaryError("invalid-candidate-inventory")
            reserved = {"security-qualified", "qualified-commit", "qualification-evidence.json"}
            total = 0
            for name in sorted(names):
                if name in reserved or Path(name).name != name:
                    raise RootBoundaryError("candidate-metadata-present")
                artifact = source / name
                metadata = (
                    os.stat(name, dir_fd=source_directory_fd, follow_symlinks=False)
                    if source_directory_fd is not None
                    else artifact.lstat()
                )
                if (
                    not stat.S_ISREG(metadata.st_mode)
                    or stat.S_ISLNK(metadata.st_mode)
                    or metadata.st_nlink != 1
                    or metadata.st_size > MAX_FILE_BYTES
                    or (os.name == "posix" and metadata.st_uid != self.deployment_uid)
                ):
                    raise RootBoundaryError("invalid-candidate-artifact")
                expected_mode = 0o700 if name in RECOVERY_EXECUTABLES else 0o600
                if os.name == "posix" and stat.S_IMODE(metadata.st_mode) != expected_mode:
                    raise RootBoundaryError("invalid-candidate-mode")
                if self.after_artifact_lstat is not None:
                    self.after_artifact_lstat(artifact)

                source_fd = (
                    os.open(
                        name,
                        os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0),
                        dir_fd=source_directory_fd,
                    )
                    if source_directory_fd is not None
                    else os.open(artifact, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
                )
                target_fd = -1
                try:
                    before = os.fstat(source_fd)
                    validated_metadata = (
                        metadata.st_dev,
                        metadata.st_ino,
                        metadata.st_mode,
                        metadata.st_nlink,
                        metadata.st_size,
                        metadata.st_uid,
                    )
                    opened_metadata = (
                        before.st_dev,
                        before.st_ino,
                        before.st_mode,
                        before.st_nlink,
                        before.st_size,
                        before.st_uid,
                    )
                    if opened_metadata != validated_metadata:
                        raise RootBoundaryError("candidate-artifact-replaced")

                    target = destination / name
                    target_flags = (
                        os.O_WRONLY
                        | os.O_CREAT
                        | os.O_EXCL
                        | getattr(os, "O_NOFOLLOW", 0)
                    )
                    target_fd = (
                        os.open(name, target_flags, expected_mode, dir_fd=destination_directory_fd)
                        if destination_directory_fd is not None
                        else os.open(target, target_flags, expected_mode)
                    )
                    copied = 0
                    while copied <= MAX_FILE_BYTES:
                        chunk = os.read(
                            source_fd, min(64 * 1024, MAX_FILE_BYTES + 1 - copied)
                        )
                        if not chunk:
                            break
                        view = memoryview(chunk)
                        while view:
                            written = os.write(target_fd, view)
                            view = view[written:]
                        copied += len(chunk)
                    os.fsync(target_fd)
                    after = os.fstat(source_fd)
                finally:
                    os.close(source_fd)
                    if target_fd >= 0:
                        os.close(target_fd)
                total += copied
                after_metadata = (
                    after.st_dev,
                    after.st_ino,
                    after.st_mode,
                    after.st_nlink,
                    after.st_size,
                    after.st_uid,
                )
                if (
                    copied != before.st_size
                    or copied > MAX_FILE_BYTES
                    or total > MAX_TOTAL_BYTES
                    or after_metadata != opened_metadata
                    or (before.st_mtime_ns, before.st_ctime_ns)
                    != (after.st_mtime_ns, after.st_ctime_ns)
                ):
                    raise RootBoundaryError("candidate-artifact-changed")

            if source_directory_fd is not None:
                final_source = os.fstat(source_directory_fd)
                if (
                    final_source.st_dev,
                    final_source.st_ino,
                    final_source.st_mode,
                    final_source.st_uid,
                ) != source_identity or sorted(os.listdir(source_directory_fd)) != sorted(names):
                    raise RootBoundaryError("candidate-directory-changed")
            self._sync(destination, True)
        finally:
            if destination_directory_fd is not None:
                os.close(destination_directory_fd)
            if source_directory_fd is not None:
                os.close(source_directory_fd)

    def _write_marker(self, path: Path, content: bytes) -> None:
        descriptor = os.open(
            path,
            os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0),
            0o600,
        )
        try:
            view = memoryview(content)
            while view:
                written = os.write(descriptor, view)
                view = view[written:]
            os.fsync(descriptor)
        finally:
            os.close(descriptor)
        self._trace("marker-written", path)
        self._sync(path.parent, True)

    def _qualify(self, run: Path, run_id: str, attempt: str, commit: str, project: str) -> None:
        output = run / "qualification-evidence.json"
        proxy_mode = self._protected_proxy_mode()
        self._run(
            [
                str(self.python),
                str(self.paths.qualifier),
                "verify",
                "--run-directory",
                str(run),
                "--expected-commit",
                commit,
                "--expected-run-id",
                run_id,
                "--expected-run-attempt",
                attempt,
                "--project-name",
                project,
                "--proxy-mode",
                proxy_mode,
                "--output",
                str(output),
            ],
            "root-qualification-failed",
        )
        evidence = json.loads(_file(output, 0 if self.enforce_root else self.deployment_uid, 0o600).decode())
        if (
            evidence.get("schema") != QUALIFICATION_SCHEMA
            or evidence.get("status") != "passed"
            or evidence.get("run_id") != run_id
            or evidence.get("run_attempt") != int(attempt)
            or evidence.get("source_commit") != commit
            or evidence.get("proxy_mode") != proxy_mode
            or evidence.get("errors") != []
        ):
            raise RootBoundaryError("invalid-qualification")
        artifacts = evidence.get("artifacts")
        if not isinstance(artifacts, dict) or not artifacts:
            raise RootBoundaryError("invalid-qualification")
        for name, summary in artifacts.items():
            if Path(name).name != name or not isinstance(summary, dict):
                raise RootBoundaryError("invalid-qualification")
            mode = 0o700 if name in RECOVERY_EXECUTABLES else 0o600
            raw = _file(run / name, 0 if self.enforce_root else self.deployment_uid, mode)
            if summary.get("path") != name or summary.get("sha256") != "sha256:" + hashlib.sha256(raw).hexdigest():
                raise RootBoundaryError("artifact-digest")
        for name, fixed in FIXED_COMPONENTS.items():
            candidate = _file(
                run / name,
                0 if self.enforce_root else self.deployment_uid,
                0o700 if name in RECOVERY_EXECUTABLES else 0o600,
            )
            fixed_raw = _file(
                fixed,
                0 if self.enforce_root else self.deployment_uid,
                FIXED_COMPONENT_MODES[name],
            )
            if candidate != fixed_raw:
                raise RootBoundaryError("fixed-component-mismatch")
        self._sync(output)
        self._write_marker(run / "qualified-commit", (commit + "\n").encode("ascii"))
        self._write_marker(run / "security-qualified", b"")

    def _seal(self, run: Path) -> None:
        owner_uid = 0 if self.enforce_root else self.deployment_uid
        with os.scandir(run) as entries:
            for entry in entries:
                mode = 0o550 if entry.name in RECOVERY_EXECUTABLES else 0o440
                if os.name == "posix":
                    os.chown(entry.path, owner_uid, self.deployment_gid)
                os.chmod(entry.path, mode)
                self._sync(Path(entry.path))
        if os.name == "posix":
            os.chown(run, owner_uid, self.deployment_gid)
        os.chmod(run, 0o550)
        self._sync(run, True)
        self._sync(run.parent, True)
        self._trace("run-sealed", run)

    def _publish_pointer(self, run: Path) -> None:
        temporary = self.paths.lkg_root / f".current.{secrets.token_hex(12)}.tmp"
        descriptor = os.open(
            temporary,
            os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0),
            0o644,
        )
        try:
            with os.fdopen(descriptor, "wb") as stream:
                descriptor = -1
                stream.write((str(run) + "\n").encode("utf-8"))
                stream.flush()
                os.fsync(stream.fileno())
            if os.name == "posix":
                os.chown(
                    temporary,
                    0 if self.enforce_root else self.deployment_uid,
                    0 if self.enforce_root else self.deployment_gid,
                )
            os.chmod(temporary, 0o644)
            self._sync(temporary)
            os.replace(temporary, self.paths.pointer)
            self._trace("pointer-replaced", self.paths.pointer)
            self._sync(self.paths.lkg_root, True)
        finally:
            if descriptor >= 0:
                os.close(descriptor)
            with contextlib.suppress(FileNotFoundError):
                temporary.unlink()

    def activate(
        self, run_id: str, attempt: str, commit: str, project: str
    ) -> dict[str, object]:
        name = self._identity(run_id, attempt)
        if not COMMIT.fullmatch(commit) or not PROJECT.fullmatch(project) or project != "xframework":
            raise RootBoundaryError("invalid-activation-identity")
        self._validate_roots()
        self._validate_fixed_files()
        self._validate_systemd()
        source = self.paths.run_root / name
        quarantine = self.paths.quarantine_root / f"candidate-{name}-{secrets.token_hex(8)}"
        staging_parent = self.paths.quarantine_root / f"staging-{secrets.token_hex(8)}"
        staging_run = staging_parent / name
        try:
            with self._lease_lock():
                self._validate_activation_lease(
                    run_id, int(attempt), source, project
                )
                os.rename(source, quarantine)
                self._trace("candidate-quarantined", quarantine)
                self._sync(self.paths.run_root, True)
                self._sync(self.paths.quarantine_root, True)
                os.mkdir(source, 0o700)
                if os.name == "posix":
                    os.chown(source, self.deployment_uid, self.deployment_gid)
                    os.chmod(source, 0o700)
                placeholder = source.lstat()
                self._sync(source, True)
                self._sync(self.paths.run_root, True)
                self._trace("lease-placeholder-created", source)
            if self.after_quarantine is not None:
                self.after_quarantine(source, quarantine)
            os.mkdir(staging_parent, 0o700)
            self._sync(self.paths.quarantine_root, True)
            self._copy_quarantined(quarantine, staging_run)
            self._qualify(staging_run, run_id, attempt, commit, project)
            self._seal(staging_run)
            with self._lease_lock():
                self._validate_activation_lease(
                    run_id, int(attempt), source, project
                )
                current = source.lstat()
                with os.scandir(source) as entries:
                    placeholder_not_empty = next(entries, None) is not None
                if (
                    not stat.S_ISDIR(current.st_mode)
                    or (current.st_dev, current.st_ino) != (placeholder.st_dev, placeholder.st_ino)
                    or placeholder_not_empty
                ):
                    raise RootBoundaryError("candidate-path-recreated")
                os.rmdir(source)
                os.rename(staging_run, source)
                self._trace("sealed-run-installed", source)
                self._sync(source, True)
                self._sync(staging_parent, True)
                self._sync(self.paths.run_root, True)
                self._publish_pointer(source)
                self._validate_sealed_run(source)
            os.rmdir(staging_parent)
            self._sync(self.paths.quarantine_root, True)
            self._validate_systemd()
        except BaseException:
            try:
                self.stop_hub()
            finally:
                with contextlib.suppress(RootBoundaryError):
                    self.ensure_watchdog()
            raise
        return {"status": "passed", "state": "qualified-lkg-activated", "run": name}


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__, allow_abbrev=False)
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("verify-bootstrap")
    commands.add_parser("ensure-watchdog")
    prepare = commands.add_parser("prepare-run")
    prepare.add_argument("run_id")
    prepare.add_argument("run_attempt")
    activate = commands.add_parser("activate")
    activate.add_argument("run_id")
    activate.add_argument("run_attempt")
    activate.add_argument("expected_commit")
    activate.add_argument("project_name")
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    boundary: RootBoundary | None = None
    try:
        args = parse_args(argv)
        boundary = RootBoundary()
        if args.command == "verify-bootstrap":
            evidence = boundary.verify_bootstrap()
        elif args.command == "ensure-watchdog":
            evidence = boundary.ensure_watchdog()
        elif args.command == "prepare-run":
            evidence = boundary.prepare_run(args.run_id, args.run_attempt)
        else:
            evidence = boundary.activate(
                args.run_id,
                args.run_attempt,
                args.expected_commit,
                args.project_name,
            )
    except (RootBoundaryError, OSError, ValueError, json.JSONDecodeError) as error:
        if boundary is not None:
            with contextlib.suppress(BaseException):
                boundary.stop_hub()
        code = error.code if isinstance(error, RootBoundaryError) else "root-boundary-failed"
        print(json.dumps({"status": "failed", "reason_code": code}, sort_keys=True))
        return 1
    print(json.dumps(evidence, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
