#!/usr/bin/env python3
"""Silently prove Bolt durable state after the Phase 0 Redis recovery."""

from __future__ import annotations

import base64
import datetime as dt
import hashlib
import json
import os
import re
import stat
import subprocess
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable, Mapping


MAX_FILE_BYTES = 1024 * 1024
MAX_TOKEN_BYTES = 16 * 1024
MAX_PROCESS_OUTPUT_BYTES = 1024 * 1024
PROCESS_TIMEOUT_SECONDS = 90
OPERATION_TIMEOUT_SECONDS = 10
RECEIPT_SCHEMA = "bolt-phase0-post-recovery-durable/v1"
REPORT_SCHEMA = "bolt-phase0-synthetic-report/v1"
MANIFEST_SCHEMA = "bolt-phase0-token-manifest/v1"
PROJECT_NAME = re.compile(r"^[a-z0-9][a-z0-9_-]{0,62}$")
CONTAINER_ID = re.compile(r"^[0-9a-f]{64}$")
DIGEST_IMAGE = re.compile(
    r"^[a-z0-9][a-z0-9._:/-]*[a-z0-9]@sha256:[0-9a-f]{64}$"
)
JWT = re.compile(rb"^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$")
ENV_NAME = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*$")
SAFE_ENV_VALUE = re.compile(r"^[A-Za-z0-9_./,:@%+=+-]*$")
FORBIDDEN_ENV_CHARACTERS = frozenset("`'\"#$\\;&|<>(){}[]*?!")
ENFORCE_POSIX_PERMISSIONS = os.name == "posix" and hasattr(os, "geteuid")
INSPECT_LABELS_FORMAT = "{{json .Config.Labels}}"

EXPECTED_DURABLE_RESULTS = {
    "durable_offline_registration": {
        "registered": "true",
        "detached": "true",
        "offline": "true",
    },
    "durable_offline_publish": {
        "published_while_offline": "true",
        "batch_ordered": "true",
    },
    "durable_ordered_replay": {
        "reconnected": "true",
        "ordered_replay": "true",
        "replayed_all": "true",
    },
    "durable_ack": {
        "cumulative_acknowledged": "true",
        "duplicate_ack_idempotent": "true",
        "out_of_order_ack_monotonic": "true",
    },
    "durable_no_redelivery": {
        "reconnected": "true",
        "no_redelivery": "true",
    },
}
EXPECTED_OPERATIONS = {
    "user_registration",
    "hostile_reserved_registration",
    "communications_registration",
    "identity_health_check",
    "transient_presence",
    *EXPECTED_DURABLE_RESULTS,
    "durable_unregister",
}
RECEIPT_ASSERTIONS = {
    "durableStateVerified": True,
    "dataLossObserved": False,
}


class ProbeError(Exception):
    """A deliberately non-secret probe failure."""


@dataclass(frozen=True)
class ProcessResult:
    returncode: int
    stdout: bytes
    stderr: bytes


@dataclass(frozen=True)
class FileSnapshot:
    device: int
    inode: int
    size: int
    modified_ns: int
    sha256: str


@dataclass(frozen=True)
class TokenEvidence:
    purpose: str
    path: Path
    value: bytes
    marker: bytes
    snapshot: FileSnapshot


ProcessRunner = Callable[[list[str], float, Mapping[str, str]], ProcessResult]


def _fail(code: str) -> None:
    raise ProbeError(code)


def _canonical_path(value: str, code: str) -> Path:
    if not value or not os.path.isabs(value):
        _fail(code)
    path = Path(value)
    if os.path.abspath(value) != os.path.realpath(value):
        _fail(code)
    return path


def _validate_private_directory(path: Path, code: str) -> os.stat_result:
    if not path.is_absolute() or path.resolve() != path:
        _fail(code)
    try:
        metadata = os.lstat(path)
    except OSError:
        _fail(code)
    if not stat.S_ISDIR(metadata.st_mode) or stat.S_ISLNK(metadata.st_mode):
        _fail(code)
    if ENFORCE_POSIX_PERMISSIONS and (
        metadata.st_uid != os.geteuid()
        or metadata.st_mode & (stat.S_IRWXG | stat.S_IRWXO)
    ):
        _fail(code)
    return metadata


def _validate_private_file(
    path: Path,
    *,
    maximum: int,
    code: str,
) -> os.stat_result:
    if not path.is_absolute() or os.path.abspath(path) != os.path.realpath(path):
        _fail(code)
    try:
        metadata = os.lstat(path)
    except OSError:
        _fail(code)
    if (
        not stat.S_ISREG(metadata.st_mode)
        or stat.S_ISLNK(metadata.st_mode)
        or metadata.st_nlink != 1
        or metadata.st_size <= 0
        or metadata.st_size > maximum
    ):
        _fail(code)
    if ENFORCE_POSIX_PERMISSIONS and (
        metadata.st_uid != os.geteuid()
        or metadata.st_mode & (stat.S_IRWXG | stat.S_IRWXO | stat.S_IXUSR)
        or not metadata.st_mode & stat.S_IRUSR
    ):
        _fail(code)
    return metadata


def _read_private_file(path: Path, *, maximum: int, code: str) -> tuple[bytes, FileSnapshot]:
    before = _validate_private_file(path, maximum=maximum, code=code)
    flags = os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0) | getattr(os, "O_BINARY", 0)
    try:
        descriptor = os.open(path, flags)
        try:
            current = os.fstat(descriptor)
            if (current.st_dev, current.st_ino) != (before.st_dev, before.st_ino):
                _fail(code)
            data = os.read(descriptor, maximum + 1)
        finally:
            os.close(descriptor)
    except ProbeError:
        raise
    except OSError:
        _fail(code)
    if len(data) != current.st_size or len(data) > maximum:
        _fail(code)
    return data, FileSnapshot(
        current.st_dev,
        current.st_ino,
        current.st_size,
        current.st_mtime_ns,
        hashlib.sha256(data).hexdigest(),
    )


def _verify_snapshot(path: Path, expected: FileSnapshot, *, maximum: int, code: str) -> None:
    _, current = _read_private_file(path, maximum=maximum, code=code)
    if current != expected:
        _fail(code)


def _single_json(data: bytes, code: str) -> dict[str, Any]:
    if not data or len(data) > MAX_FILE_BYTES:
        _fail(code)
    try:
        value = json.loads(data.decode("utf-8", errors="strict"))
    except (UnicodeDecodeError, json.JSONDecodeError, ValueError):
        _fail(code)
    if not isinstance(value, dict):
        _fail(code)
    return value


def load_protected_env(path_value: str) -> tuple[dict[str, str], FileSnapshot]:
    path = _canonical_path(path_value, "ENV_FILE")
    raw, snapshot = _read_private_file(path, maximum=MAX_FILE_BYTES, code="ENV_FILE")
    try:
        text = raw.decode("utf-8-sig", errors="strict")
    except UnicodeDecodeError:
        _fail("ENV_SYNTAX")
    if "\r" in text.replace("\r\n", ""):
        _fail("ENV_SYNTAX")
    lines = text.replace("\r\n", "\n").split("\n")
    values: dict[str, str] = {}
    for line in lines:
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


def _decode_marker(token: bytes) -> bytes:
    if not JWT.fullmatch(token):
        _fail("TOKEN_FILE")
    try:
        segment = token.split(b".")[1]
        payload = base64.urlsafe_b64decode(segment + b"=" * (-len(segment) % 4))
        claims = json.loads(payload.decode("utf-8", errors="strict"))
    except (UnicodeDecodeError, ValueError, json.JSONDecodeError):
        _fail("TOKEN_CLAIMS")
    marker = claims.get("jti") if isinstance(claims, dict) else None
    compact_marker = marker.replace("-", "") if isinstance(marker, str) else ""
    if not re.fullmatch(r"[0-9a-fA-F]{32}", compact_marker) or int(compact_marker, 16) == 0:
        _fail("TOKEN_CLAIMS")
    return marker.encode("ascii")


def load_token_manifest(
    manifest_value: str,
    values: Mapping[str, str],
) -> tuple[list[TokenEvidence], FileSnapshot]:
    manifest_path = _canonical_path(manifest_value, "TOKEN_MANIFEST")
    raw, manifest_snapshot = _read_private_file(
        manifest_path, maximum=MAX_FILE_BYTES, code="TOKEN_MANIFEST"
    )
    document = _single_json(raw, "TOKEN_MANIFEST")
    entries = document.get("tokens")
    if document.get("schemaVersion") != MANIFEST_SCHEMA or not isinstance(entries, list):
        _fail("TOKEN_MANIFEST")

    evidence: list[TokenEvidence] = []
    purposes: set[str] = set()
    for entry in entries:
        if not isinstance(entry, dict):
            _fail("TOKEN_MANIFEST")
        purpose = entry.get("purpose")
        path_value = entry.get("path")
        if (
            not isinstance(purpose, str)
            or not isinstance(path_value, str)
            or purpose in purposes
        ):
            _fail("TOKEN_MANIFEST")
        purposes.add(purpose)
        path = _canonical_path(path_value, "TOKEN_FILE")
        raw_token, snapshot = _read_private_file(
            path, maximum=MAX_TOKEN_BYTES, code="TOKEN_FILE"
        )
        token = raw_token.rstrip(b"\r\n")
        if not token or raw_token not in {token, token + b"\n", token + b"\r\n"}:
            _fail("TOKEN_FILE")
        marker = _decode_marker(token)
        manifest_marker = entry.get("marker")
        if manifest_marker is not None and manifest_marker != marker.decode("ascii"):
            _fail("TOKEN_MANIFEST")
        evidence.append(TokenEvidence(purpose, path, token, marker, snapshot))

    by_purpose = {item.purpose: item for item in evidence}
    expected_paths = {
        "communications": _required(values, "BOLT_SYNTHETIC_COMMUNICATIONS_TOKEN_PATH"),
        "user": _required(values, "BOLT_SYNTHETIC_USER_TOKEN_PATH"),
    }
    if not set(expected_paths).issubset(by_purpose):
        _fail("TOKEN_MANIFEST")
    for purpose, expected_path in expected_paths.items():
        if by_purpose[purpose].path != _canonical_path(expected_path, "TOKEN_FILE"):
            _fail("TOKEN_BINDING")
    if len({item.value for item in evidence}) != len(evidence) or len(
        {item.marker for item in evidence}
    ) != len(evidence):
        _fail("TOKEN_MANIFEST")
    return evidence, manifest_snapshot


def _default_process_runner(
    command: list[str], timeout_seconds: float, environment: Mapping[str, str]
) -> ProcessResult:
    if not command or timeout_seconds <= 0:
        _fail("SUBPROCESS_CONFIGURATION")
    with tempfile.TemporaryFile() as stdout, tempfile.TemporaryFile() as stderr:
        try:
            process = subprocess.Popen(
                command,
                stdin=subprocess.DEVNULL,
                stdout=stdout,
                stderr=stderr,
                env=dict(environment),
                close_fds=True,
            )
            try:
                returncode = process.wait(timeout=timeout_seconds)
            except subprocess.TimeoutExpired:
                process.terminate()
                try:
                    process.wait(timeout=2)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=2)
                _fail("SUBPROCESS_TIMEOUT")
        except ProbeError:
            raise
        except (OSError, subprocess.SubprocessError):
            _fail("SUBPROCESS")
        stdout.seek(0)
        stderr.seek(0)
        stdout_bytes = stdout.read(MAX_PROCESS_OUTPUT_BYTES + 1)
        stderr_bytes = stderr.read(MAX_PROCESS_OUTPUT_BYTES + 1)
    if len(stdout_bytes) > MAX_PROCESS_OUTPUT_BYTES or len(stderr_bytes) > MAX_PROCESS_OUTPUT_BYTES:
        _fail("SUBPROCESS_OUTPUT")
    return ProcessResult(returncode, stdout_bytes, stderr_bytes)


def _run(
    runner: ProcessRunner,
    command: list[str],
    *,
    timeout: float = 20,
    allow_stderr: bool = False,
) -> ProcessResult:
    environment = {
        "PATH": "/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
        "HOME": os.environ.get("HOME", "/tmp"),
    }
    result = runner(command, timeout, environment)
    if (
        not isinstance(result, ProcessResult)
        or result.returncode != 0
        or (result.stderr and not allow_stderr)
        or len(result.stdout) > MAX_PROCESS_OUTPUT_BYTES
        or len(result.stderr) > MAX_PROCESS_OUTPUT_BYTES
    ):
        _fail("SUBPROCESS_FAILED")
    return result


def resolve_compose_configuration(
    project: str, runner: ProcessRunner
) -> tuple[Path, Path, FileSnapshot, FileSnapshot]:
    result = _run(
        runner,
        [
            "docker",
            "container",
            "ls",
            "-q",
            "--no-trunc",
            "--filter",
            f"label=com.docker.compose.project={project}",
            "--filter",
            "label=com.docker.compose.service=bolt-hub",
        ],
    )
    try:
        identifiers = result.stdout.decode("ascii", errors="strict").splitlines()
    except UnicodeDecodeError:
        _fail("COMPOSE_CONTAINER")
    if len(identifiers) != 1 or not CONTAINER_ID.fullmatch(identifiers[0]):
        _fail("COMPOSE_CONTAINER")

    inspect = _run(
        runner,
        ["docker", "inspect", "--format", INSPECT_LABELS_FORMAT, identifiers[0]],
    )
    labels = _single_json(inspect.stdout, "COMPOSE_LABELS")
    if (
        labels.get("com.docker.compose.project") != project
        or labels.get("com.docker.compose.service") != "bolt-hub"
    ):
        _fail("COMPOSE_IDENTITY")
    config_files = labels.get("com.docker.compose.project.config_files")
    if not isinstance(config_files, str):
        _fail("COMPOSE_PATHS")
    parts = config_files.split(",")
    if len(parts) != 2 or any(not part for part in parts):
        _fail("COMPOSE_PATHS")
    compose_path = _canonical_path(parts[0], "COMPOSE_PATHS")
    override_path = _canonical_path(parts[1], "COMPOSE_PATHS")
    if (
        compose_path.name != "docker-compose.yml"
        or override_path.name != "pinned-compose.override.json"
        or compose_path.parent != override_path.parent
    ):
        _fail("COMPOSE_PATHS")
    _validate_private_directory(compose_path.parent, "COMPOSE_PATHS")
    compose_raw, compose_snapshot = _read_private_file(
        compose_path, maximum=MAX_FILE_BYTES, code="COMPOSE_FILE"
    )
    override_raw, override_snapshot = _read_private_file(
        override_path, maximum=MAX_FILE_BYTES, code="COMPOSE_OVERRIDE"
    )
    if not compose_raw:
        _fail("COMPOSE_FILE")
    override = _single_json(override_raw, "COMPOSE_OVERRIDE")
    services = override.get("services")
    synthetic = services.get("bolt-phase0-synthetics") if isinstance(services, dict) else None
    image = synthetic.get("image") if isinstance(synthetic, dict) else None
    if not isinstance(image, str) or not DIGEST_IMAGE.fullmatch(image):
        _fail("SYNTHETIC_IMAGE_PIN")
    return compose_path, override_path, compose_snapshot, override_snapshot


def _reject_secret_output(output: bytes, evidence: list[TokenEvidence]) -> None:
    if any(item.value in output or item.marker in output for item in evidence):
        _fail("SECRET_OUTPUT")


def _validate_report(raw: bytes, evidence: list[TokenEvidence]) -> None:
    report = _single_json(raw, "SYNTHETIC_REPORT")
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
        set(report) != expected_keys
        or report.get("schemaVersion") != REPORT_SCHEMA
        or report.get("status") != "passed"
        or report.get("target") != "wss://bolt-hub:8443/bolt/ws"
    ):
        _fail("SYNTHETIC_REPORT")
    run_id = report.get("runId")
    if not isinstance(run_id, str) or not re.fullmatch(
        r"[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}",
        run_id,
        re.IGNORECASE,
    ):
        _fail("SYNTHETIC_REPORT")
    prefixes = report.get("tokenSha256Prefixes")
    current = {
        item.purpose: item
        for item in evidence
        if item.purpose in {"communications", "user"}
    }
    expected_prefixes = {
        purpose: hashlib.sha256(item.value).hexdigest()[:12]
        for purpose, item in current.items()
    }
    if prefixes != expected_prefixes:
        _fail("TOKEN_EVIDENCE")
    timings = report.get("timings")
    if (
        not isinstance(timings, dict)
        or set(timings) != {"totalMs"}
        or not isinstance(timings.get("totalMs"), int)
        or timings["totalMs"] < 0
        or timings["totalMs"] > PROCESS_TIMEOUT_SECONDS * 1000
    ):
        _fail("SYNTHETIC_TIMING")
    try:
        started = dt.datetime.fromisoformat(report["startedAtUtc"].replace("Z", "+00:00"))
        completed = dt.datetime.fromisoformat(report["completedAtUtc"].replace("Z", "+00:00"))
    except (AttributeError, KeyError, ValueError):
        _fail("SYNTHETIC_TIMING")
    if (
        started.tzinfo is None
        or completed.tzinfo is None
        or completed < started
        or completed - started > dt.timedelta(seconds=PROCESS_TIMEOUT_SECONDS)
        or timings["totalMs"] > int((completed - started).total_seconds() * 1000) + 1000
    ):
        _fail("SYNTHETIC_TIMING")

    operations = report.get("operations")
    if not isinstance(operations, list):
        _fail("SYNTHETIC_OPERATIONS")
    by_name: dict[str, dict[str, Any]] = {}
    for operation in operations:
        if not isinstance(operation, dict) or set(operation) != {
            "name",
            "startedAtUtc",
            "completedAtUtc",
            "status",
            "timingMs",
            "results",
        }:
            _fail("SYNTHETIC_OPERATIONS")
        name = operation.get("name")
        if (
            not isinstance(name, str)
            or name in by_name
            or operation.get("status") != "passed"
            or not isinstance(operation.get("timingMs"), int)
            or operation["timingMs"] < 0
            or operation["timingMs"] > OPERATION_TIMEOUT_SECONDS * 1000 + 1000
            or not isinstance(operation.get("results"), dict)
        ):
            _fail("SYNTHETIC_OPERATIONS")
        try:
            operation_started = dt.datetime.fromisoformat(
                operation["startedAtUtc"].replace("Z", "+00:00")
            )
            operation_completed = dt.datetime.fromisoformat(
                operation["completedAtUtc"].replace("Z", "+00:00")
            )
        except (AttributeError, KeyError, ValueError):
            _fail("SYNTHETIC_OPERATIONS")
        if (
            operation_started.tzinfo is None
            or operation_completed.tzinfo is None
            or operation_started < started
            or operation_completed < operation_started
            or operation_completed > completed + dt.timedelta(seconds=1)
        ):
            _fail("SYNTHETIC_OPERATIONS")
        by_name[name] = operation
    if set(by_name) != EXPECTED_OPERATIONS:
        _fail("SYNTHETIC_OPERATIONS")
    for name, expected_results in EXPECTED_DURABLE_RESULTS.items():
        if by_name[name]["results"] != expected_results:
            _fail("DURABLE_EVIDENCE")


def _validate_receipt_target(path: Path) -> None:
    if not path.is_absolute() or path.parent.resolve() != path.parent:
        _fail("RECEIPT_PATH")
    _validate_private_directory(path.parent, "RECEIPT_PATH")
    if path.exists() or path.is_symlink():
        _fail("RECEIPT_EXISTS")


def write_atomic_receipt(path: Path) -> None:
    _validate_receipt_target(path)
    document = {
        "schemaVersion": RECEIPT_SCHEMA,
        "status": "passed",
        "assertions": RECEIPT_ASSERTIONS,
    }
    serialized = (json.dumps(document, sort_keys=True, separators=(",", ":")) + "\n").encode(
        "ascii"
    )
    descriptor = -1
    temporary = ""
    try:
        descriptor, temporary = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
        os.fchmod(descriptor, 0o600)
        written = 0
        while written < len(serialized):
            count = os.write(descriptor, serialized[written:])
            if count <= 0:
                _fail("RECEIPT_WRITE")
            written += count
        os.fsync(descriptor)
        os.close(descriptor)
        descriptor = -1
        os.link(temporary, path, follow_symlinks=False)
        os.unlink(temporary)
        temporary = ""
        os.chmod(path, 0o600)
        if hasattr(os, "O_DIRECTORY"):
            directory = os.open(path.parent, os.O_RDONLY | os.O_DIRECTORY)
            try:
                os.fsync(directory)
            finally:
                os.close(directory)
    except ProbeError:
        raise
    except OSError:
        _fail("RECEIPT_WRITE")
    finally:
        if descriptor >= 0:
            os.close(descriptor)
        if temporary:
            try:
                os.unlink(temporary)
            except OSError:
                pass
    raw, _ = _read_private_file(path, maximum=MAX_FILE_BYTES, code="RECEIPT_WRITE")
    if raw != serialized:
        _fail("RECEIPT_WRITE")


def run_probe(
    env_value: str,
    manifest_value: str,
    receipt: Path,
    *,
    runner: ProcessRunner = _default_process_runner,
) -> None:
    _validate_receipt_target(receipt)
    values, env_snapshot = load_protected_env(env_value)
    project = _required(values, "BOLT_SYNTHETIC_COMPOSE_PROJECT_NAME")
    if not PROJECT_NAME.fullmatch(project):
        _fail("COMPOSE_PROJECT")
    evidence, manifest_snapshot = load_token_manifest(manifest_value, values)
    compose_path, override_path, compose_snapshot, override_snapshot = (
        resolve_compose_configuration(project, runner)
    )

    command = [
        "docker",
        "compose",
        "--profile",
        "phase0-verification",
        "--env-file",
        env_value,
        "-f",
        str(compose_path),
        "-f",
        str(override_path),
        "--project-name",
        project,
        "run",
        "--rm",
        "--no-deps",
        "--quiet-pull",
        "-e",
        f"BOLT_SYNTHETIC_OPERATION_TIMEOUT_SECONDS={OPERATION_TIMEOUT_SECONDS}",
        "-e",
        "BOLT_SYNTHETIC_EXPIRY_TOKEN_FILE=",
        "bolt-phase0-synthetics",
    ]
    joined_command = "\0".join(command).encode("utf-8")
    if any(item.value in joined_command or item.marker in joined_command for item in evidence):
        _fail("SECRET_ARGUMENT")
    result = _run(runner, command, timeout=PROCESS_TIMEOUT_SECONDS)
    _reject_secret_output(result.stdout + result.stderr, evidence)
    if result.stderr:
        _fail("SYNTHETIC_STDERR")
    _validate_report(result.stdout, evidence)

    _verify_snapshot(
        _canonical_path(env_value, "ENV_FILE"),
        env_snapshot,
        maximum=MAX_FILE_BYTES,
        code="ENV_CHANGED",
    )
    _verify_snapshot(
        _canonical_path(manifest_value, "TOKEN_MANIFEST"),
        manifest_snapshot,
        maximum=MAX_FILE_BYTES,
        code="MANIFEST_CHANGED",
    )
    _verify_snapshot(
        compose_path,
        compose_snapshot,
        maximum=MAX_FILE_BYTES,
        code="COMPOSE_CHANGED",
    )
    _verify_snapshot(
        override_path,
        override_snapshot,
        maximum=MAX_FILE_BYTES,
        code="COMPOSE_CHANGED",
    )
    for item in evidence:
        _verify_snapshot(item.path, item.snapshot, maximum=MAX_TOKEN_BYTES, code="TOKEN_CHANGED")
    write_atomic_receipt(receipt)


def main() -> int:
    try:
        if os.environ.get("BOLT_SYNTHETIC_DURABLE_PROBE_MODE") != "post-recovery":
            _fail("HOOK_MODE")
        env_value = os.environ.get("XFRAMEWORK_ENV_FILE", "")
        manifest_value = os.environ.get("BOLT_SYNTHETIC_TOKEN_MANIFEST", "")
        receipt_value = os.environ.get("BOLT_SYNTHETIC_POST_RECOVERY_RECEIPT", "")
        if not env_value or not manifest_value or not receipt_value:
            _fail("HOOK_ENVIRONMENT")
        run_probe(env_value, manifest_value, Path(receipt_value))
        return 0
    except BaseException:
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
