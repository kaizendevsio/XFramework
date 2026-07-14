#!/usr/bin/env python3
"""Strict Bolt Phase 0 qualification and post-restore recovery gates."""

from __future__ import annotations

import argparse
import contextlib
import datetime as dt
import hashlib
import ipaddress
import json
import math
import os
import re
import secrets
import stat
import subprocess
import sys
import tempfile
import uuid
from pathlib import Path
from typing import Any, Callable, Mapping, Sequence
from urllib.parse import urlsplit


QUALIFICATION_SCHEMA = "xframework.bolt.phase0.qualification.v1"
RECOVERY_GATE_SCHEMA = "xframework.bolt.phase0.recovery-gate.v1"
PINS_SCHEMA = "xframework.bolt.phase0.image-pins.v2"
PREFLIGHT_SCHEMA = "xframework.bolt.phase0.preflight.v2"
HUB_TLS_SCHEMA = "xframework.bolt.phase0.tls.v1"
IDENTITY_TLS_SCHEMA = "xframework.bolt.phase0.identityserver-tls.v1"
PROVENANCE_SCHEMA = "xframework.bolt.phase0.provenance.v1"
RUNTIME_SCHEMA = "xframework.bolt.phase0.runtime.v2"
ROTATION_SCHEMA = "xframework.bolt.phase0.rotation-state.v1"
GENERATION_INVENTORY_SCHEMA = "xframework.bolt.phase0.credential-generation-inventory.v1"
CREDENTIAL_CONVERGENCE_SCHEMA = "xframework.bolt.phase0.credential-convergence.v1"
OBSERVATION_SCHEMA = "xframework.bolt.phase0.observation.v1"
SYNTHETIC_SCHEMA = "bolt-phase0-synthetic-evidence/v1"
SYNTHETIC_CORE_SCHEMA = "bolt-phase0-synthetic-report/v1"
POST_RUN_SCHEMA = "bolt-phase0-post-run-evidence/v1"
PROBE_SCHEMA = "bolt-phase0-probe-receipt/v1"
CANDIDATE_RESTART_SCHEMA = "xframework.bolt.phase0.candidate-restart.v1"
PROXY_MODE_LOGS = "logs"
PROXY_MODE_DIRECT_KESTREL = "direct-kestrel"
SYNTHETIC_PROXY_MODES = frozenset({PROXY_MODE_LOGS, PROXY_MODE_DIRECT_KESTREL})
PROXY_MODES = frozenset({PROXY_MODE_DIRECT_KESTREL})
DIRECT_KESTREL_TARGET = "wss://bolt-hub:8443/bolt/ws"
DIRECT_PUBLICATION_ATTESTATION = "ATTEST_DIRECT_KESTREL_NO_INTERMEDIARY"
GITHUB_ACTOR = re.compile(r"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?$")
GITHUB_ACTOR_ID = re.compile(r"^[1-9][0-9]{0,31}$")
ALL_INTERFACE_BINDINGS = frozenset({"", "0.0.0.0", "::"})

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
IMAGE_SERVICES = (*PHASE0_SERVICES, "bolt-phase0-synthetics")
ROTATION_SERVICES = tuple(service for service in PHASE0_SERVICES if service != "migrate")
STAGED_RUNTIME_INVENTORIES = {
    "runtime-staged-hub.json": PHASE0_SERVICES[:2],
    "runtime-staged-canary.json": PHASE0_SERVICES[:4],
    "runtime-staged-batch-1.json": PHASE0_SERVICES[:8],
    "runtime-staged-batch-2.json": PHASE0_SERVICES[:11],
    "runtime-staged-batch-3.json": PHASE0_SERVICES,
}
ROTATION_RUNTIME_INVENTORIES = {
    "runtime-rotation-hub.json": PHASE0_SERVICES[:2],
    "runtime-rotation-canary.json": PHASE0_SERVICES[:4],
    "runtime-rotation-batch-1.json": PHASE0_SERVICES[:8],
    "runtime-rotation-batch-2.json": PHASE0_SERVICES[:11],
    "runtime-rotation-batch-3.json": PHASE0_SERVICES,
}
RUNTIME_INVENTORIES = {
    **STAGED_RUNTIME_INVENTORIES,
    **ROTATION_RUNTIME_INVENTORIES,
    "runtime-evidence.json": PHASE0_SERVICES,
    "rollback-runtime-evidence.json": PHASE0_SERVICES,
}
SYNTHETIC_STAGES = (
    "canary",
    "batch-1",
    "batch-2",
    "batch-3",
    "rotation-canary",
    "rotation-batch-1",
    "rotation-batch-2",
    "rotation-batch-3",
    "finalized",
)
SYNTHETIC_FILES = {f"synthetics-{stage}.json": stage for stage in SYNTHETIC_STAGES}
SYNTHETIC_FILES["rollback-synthetics-finalized.json"] = "finalized"

RECOVERY_EXECUTABLE_FILES = (
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
RECOVERY_CONFIG_FILES = (
    "xframework-bolt-phase0-watchdog.service",
    "xframework-bolt-phase0-watchdog.timer",
)
RECOVERY_TOOL_FILES = (*RECOVERY_EXECUTABLE_FILES, *RECOVERY_CONFIG_FILES)
RECOVERY_ARTIFACT_FILES = (
    "docker-compose.yml",
    "pinned-compose.override.json",
    "image-pins.json",
    "bolt-tls-evidence.json",
    "identityserver-tls-evidence.json",
    *RECOVERY_TOOL_FILES,
)
RECOVERY_ENV_TOOL_BINDINGS = {
    "BOLT_PHASE0_RECOVERY_SYNTHETIC_COMMAND_PATH": "run-bolt-phase0-recovery-synthetic.py",
    "BOLT_SYNTHETIC_TOKEN_REFRESH_COMMAND_PATH": "refresh-bolt-phase0-synthetic-tokens.py",
    "BOLT_SYNTHETIC_PROXY_MARKER_SCAN_COMMAND_PATH": "run-bolt-phase0-marker-scan.py",
    "BOLT_SYNTHETIC_SEQ_MARKER_SCAN_COMMAND_PATH": "run-bolt-phase0-marker-scan.py",
    "BOLT_SYNTHETIC_TRACE_MARKER_SCAN_COMMAND_PATH": "run-bolt-phase0-marker-scan.py",
    "BOLT_SYNTHETIC_PLAINTEXT_REJECTION_COMMAND_PATH": "run-bolt-phase0-operational-probe.py",
    "BOLT_SYNTHETIC_REDIS_INTERRUPTION_COMMAND_PATH": "run-bolt-phase0-operational-probe.py",
    "BOLT_SYNTHETIC_OLD_GENERATION_REJECTION_COMMAND_PATH": "run-bolt-phase0-operational-probe.py",
    "BOLT_SYNTHETIC_REDIS_POST_RECOVERY_COMMAND_PATH": "run-bolt-phase0-post-recovery-durable.py",
}

ARTIFACT_FILES = (
    "docker-compose.yml",
    "pinned-compose.override.json",
    "image-pins.json",
    "pinned-manifest-evidence.json",
    "bolt-tls-evidence.json",
    "identityserver-tls-evidence.json",
    "provenance-evidence.json",
    *RUNTIME_INVENTORIES,
    "rotation-prepare-evidence.json",
    "rotation-activate-evidence.json",
    "rotation-generation-inventory.json",
    "rotation-convergence-evidence.json",
    "rotation-finalized-evidence.json",
    "credential-convergence-dual-validation.json",
    "credential-convergence-finalized.json",
    "observation-evidence.json",
    *SYNTHETIC_FILES,
    "rollback-drill-evidence.json",
    *RECOVERY_TOOL_FILES,
)

JSON_ROOT_KEYS = {
    PINS_SCHEMA: {
        "schema", "generated_at_utc", "status", "source_commit", "approved_repositories",
        "registry_confirmed", "registry_manifests", "pins", "errors",
    },
    PREFLIGHT_SCHEMA: {
        "schema", "generated_at_utc", "status", "deployment_authorized", "checks", "errors",
        "redacted_manifest",
    },
    HUB_TLS_SCHEMA: {
        "schema", "generated_at_utc", "status", "internal_hostname", "published_hostname",
        "published_port", "certificate", "private_key",
    },
    IDENTITY_TLS_SCHEMA: {
        "schema", "generated_at_utc", "status", "internal_hostname", "published_hostname",
        "published_port", "token_path", "certificate", "private_key",
    },
    PROVENANCE_SCHEMA: {
        "schema", "generated_at_utc", "status", "source_commit", "dockerfile_digest", "bindings",
        "errors",
    },
    RUNTIME_SCHEMA: {
        "schema", "generated_at_utc", "status", "inventory_mode", "requested_services",
        "expected_images", "intentionally_inactive_services", "services", "errors",
    },
    CREDENTIAL_CONVERGENCE_SCHEMA: {
        "schema", "generated_at_utc", "observed_at_utc", "fallback_valid_until_utc", "phase",
        "target_generation_id", "retiring_generation_id", "service_count",
        "identityserver_client_count", "current_token_count", "retired_token_count", "status", "errors",
    },
    OBSERVATION_SCHEMA: {
        "schema", "generated_at_utc", "status", "observation", "thresholds", "health_aggregates",
        "synthetic_aggregates", "errors",
    },
}

ROTATION_KEYS = {
    "schema", "rotation_id", "phase", "previous_generation_id", "target_generation_id",
    "secondary_valid_until_utc", "prepared_at_utc", "activated_at_utc",
    "convergence_verified_at_utc", "finalized_at_utc",
}
RUNTIME_SERVICE_KEYS = {
    "service", "container_name", "container_id", "configured_image", "local_image_id",
    "repo_digests", "started_at", "running", "status", "exit_code", "health", "listeners",
    "published_port", "private_key_mounts",
}
SYNTHETIC_KEYS = {
    "schemaVersion", "runId", "stage", "status", "coreReportSha256", "synthetic", "postRunEvidence",
}
CORE_KEYS = {
    "schemaVersion", "runId", "tokenSha256Prefixes", "startedAtUtc", "completedAtUtc", "target",
    "status", "timings", "operations",
}
OPERATION_KEYS = {"name", "startedAtUtc", "completedAtUtc", "status", "timingMs", "results"}
POST_RUN_KEYS = {
    "schemaVersion", "tokenRefresh", "markerAbsence", "plaintextRejection", "expiryDisconnect",
    "redisInterruptionRecovery", "oldGenerationCredentialRejection", "tokenFilesStableForRun",
    "probeReceipts",
}
REQUIRED_OPERATIONS = {
    "user_registration", "hostile_reserved_registration", "communications_registration",
    "identity_health_check", "transient_presence", "durable_offline_registration",
    "durable_offline_publish", "durable_ordered_replay", "durable_ack", "durable_no_redelivery",
    "durable_unregister",
}
CANDIDATE_RESTART_KEYS = {
    "schema", "status", "run_id", "run_attempt", "source_commit", "project_name", "started_at_utc",
    "completed_at_utc", "credential_generation_id", "lkg_compatibility", "manifest_sha256", "override_sha256", "pins_sha256",
    "runtime_evidence_sha256", "synthetic_evidence_sha256", "checks", "errors",
}
CANDIDATE_RESTART_CHECK_KEYS = {
    "candidate_digest_recreate_applied", "full_runtime_verified", "authenticated_finalized_synthetic",
    "current_generation_preserved",
}
QUALIFICATION_CHECK_KEYS = {
    "artifact_security", "schema_and_status", "identity_and_digest_binding",
    "rotation_and_convergence", "canary_observation", "candidate_restart",
}

RUN_ID = re.compile(r"[1-9][0-9]{0,31}")
COMMIT = re.compile(r"[0-9a-f]{40}")
PROJECT_NAME = re.compile(r"[a-z0-9][a-z0-9_-]{0,62}")
GENERATION_ID = re.compile(r"[A-Za-z0-9][A-Za-z0-9._:-]{0,127}")
SHA256 = re.compile(r"sha256:[0-9a-f]{64}")
HEX_SHA256 = re.compile(r"[0-9a-f]{64}")
IMAGE_REFERENCE = re.compile(r"[a-z0-9][a-z0-9./:_-]*@sha256:[0-9a-f]{64}")
CONTAINER_ID = re.compile(r"[0-9a-f]{64}")
SAFE_NAME = re.compile(r"[a-z][a-z0-9_]{0,63}")
SAFE_RESULT = re.compile(r"[a-z0-9_./:-]{1,96}")
TIMESTAMP = re.compile(
    r"(?P<date>[0-9]{4}-[0-9]{2}-[0-9]{2})T(?P<time>[0-9]{2}:[0-9]{2}:[0-9]{2})"
    r"(?P<fraction>\.[0-9]{1,9})?(?P<zone>Z|\+00:00)"
)
JWT_SHAPE = re.compile(r"eyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}")

MAX_JSON_BYTES = 32 * 1024 * 1024
MAX_ARTIFACT_BYTES = 64 * 1024 * 1024
DEFAULT_MAXIMUM_AGE_SECONDS = 24 * 60 * 60
DEFAULT_RECOVERY_FRESHNESS_SECONDS = 300
MAX_CLOCK_SKEW_SECONDS = 5
APPROVED_RUN_ROOT = Path("/home/github-runner/xframework-deploy/runs")


class QualificationError(RuntimeError):
    def __init__(self, code: str):
        super().__init__(code)
        self.code = code


def require_proxy_mode(value: Any) -> str:
    if not isinstance(value, str) or value not in PROXY_MODES:
        raise QualificationError("invalid-proxy-mode")
    return value


def require_synthetic_proxy_mode(value: Any) -> str:
    if not isinstance(value, str) or value not in SYNTHETIC_PROXY_MODES:
        raise QualificationError("invalid-proxy-mode")
    return value


def require_proxy_configuration(values: Mapping[str, str]) -> str:
    proxy_mode = require_proxy_mode(values.get("BOLT_SYNTHETIC_PROXY_MODE"))
    has_proxy_log_paths = "BOLT_SYNTHETIC_PROXY_LOG_PATHS" in values
    proxy_log_paths = values.get("BOLT_SYNTHETIC_PROXY_LOG_PATHS", "")
    if (
        (proxy_mode == PROXY_MODE_LOGS and (
            not proxy_log_paths or proxy_log_paths != proxy_log_paths.strip()
        ))
        or (proxy_mode == PROXY_MODE_DIRECT_KESTREL and has_proxy_log_paths)
    ):
        raise QualificationError("invalid-proxy-configuration")
    return proxy_mode


def utc_now() -> dt.datetime:
    return dt.datetime.now(dt.timezone.utc)


def format_utc(value: dt.datetime) -> str:
    return value.astimezone(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%fZ")


def exact_object(value: Any, keys: set[str], code: str) -> dict[str, Any]:
    if not isinstance(value, dict) or set(value) != keys:
        raise QualificationError(code)
    return value


def require_string(value: Any, pattern: re.Pattern[str], code: str) -> str:
    if not isinstance(value, str) or not pattern.fullmatch(value):
        raise QualificationError(code)
    return value


def require_int(
    value: Any,
    code: str,
    *,
    minimum: int = 0,
    maximum: int | None = None,
) -> int:
    if (
        isinstance(value, bool)
        or not isinstance(value, int)
        or value < minimum
        or (maximum is not None and value > maximum)
    ):
        raise QualificationError(code)
    return value


def require_hostname(value: Any, code: str) -> str:
    if (
        not isinstance(value, str)
        or len(value) > 253
        or value.endswith(".")
        or len(value.split(".")) < 2
        or not all(
            re.fullmatch(
                r"[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?",
                label,
            )
            for label in value.split(".")
        )
    ):
        raise QualificationError(code)
    try:
        ipaddress.ip_address(value)
    except ValueError:
        return value
    raise QualificationError(code)


def require_host_addresses(value: Any, code: str, *, allow_empty: bool = False) -> list[str]:
    if not isinstance(value, list) or (not value and not allow_empty):
        raise QualificationError(code)
    normalized: list[str] = []
    for item in value:
        if not isinstance(item, str) or not item or "%" in item:
            raise QualificationError(code)
        try:
            address = ipaddress.ip_address(item)
        except ValueError as error:
            raise QualificationError(code) from error
        if address.is_loopback or address.is_link_local or address.is_unspecified or address.is_multicast:
            raise QualificationError(code)
        normalized.append(address.compressed)
    if normalized != sorted(set(normalized)):
        raise QualificationError(code)
    return normalized


def parse_timestamp(value: Any, code: str) -> dt.datetime:
    if not isinstance(value, str) or not TIMESTAMP.fullmatch(value):
        raise QualificationError(code)
    normalized = value.replace("Z", "+00:00")
    try:
        parsed = dt.datetime.fromisoformat(normalized)
    except ValueError as error:
        raise QualificationError(code) from error
    if parsed.tzinfo is None or parsed.utcoffset() != dt.timedelta(0):
        raise QualificationError(code)
    return parsed.astimezone(dt.timezone.utc)


def fresh_timestamp(
    value: Any,
    code: str,
    now: dt.datetime,
    maximum_age_seconds: int,
    *,
    not_before: dt.datetime | None = None,
) -> dt.datetime:
    parsed = parse_timestamp(value, code)
    if parsed > now + dt.timedelta(seconds=MAX_CLOCK_SKEW_SECONDS):
        raise QualificationError(code)
    if now - parsed > dt.timedelta(seconds=maximum_age_seconds):
        raise QualificationError("stale-evidence")
    if not_before is not None and parsed < not_before - dt.timedelta(seconds=MAX_CLOCK_SKEW_SECONDS):
        raise QualificationError(code)
    return parsed


def _unique_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise QualificationError("duplicate-json-key")
        result[key] = value
    return result


def _reject_constant(_: str) -> None:
    raise QualificationError("invalid-json-number")


def reject_controls(value: Any, depth: int = 0) -> None:
    if depth > 24:
        raise QualificationError("invalid-json-depth")
    if isinstance(value, str):
        if any(ord(character) < 0x20 or ord(character) == 0x7F for character in value):
            raise QualificationError("invalid-control-character")
    elif isinstance(value, list):
        for item in value:
            reject_controls(item, depth + 1)
    elif isinstance(value, dict):
        for key, item in value.items():
            reject_controls(key, depth + 1)
            reject_controls(item, depth + 1)


def decode_json(raw: bytes) -> Any:
    if not raw or len(raw) > MAX_JSON_BYTES or raw.startswith(b"\xef\xbb\xbf") or b"\x00" in raw:
        raise QualificationError("invalid-json")
    try:
        document = json.loads(
            raw.decode("utf-8", errors="strict"),
            object_pairs_hook=_unique_object,
            parse_constant=_reject_constant,
        )
    except QualificationError:
        raise
    except (UnicodeDecodeError, json.JSONDecodeError, RecursionError) as error:
        raise QualificationError("invalid-json") from error
    reject_controls(document)
    return document


def _path_components(path: Path) -> list[Path]:
    absolute = path.absolute()
    current = Path(absolute.anchor)
    result: list[Path] = []
    for part in absolute.parts[1:]:
        current /= part
        result.append(current)
    return result


def validate_no_symlink_path(path: Path, *, include_leaf: bool = True) -> None:
    if not path.is_absolute() or ".." in path.parts:
        raise QualificationError("invalid-path")
    components = _path_components(path)
    if not include_leaf:
        components = components[:-1]
    for component in components:
        try:
            metadata = component.lstat()
        except OSError as error:
            raise QualificationError("missing-path") from error
        if stat.S_ISLNK(metadata.st_mode):
            raise QualificationError("symlink-rejected")


def enforce_posix_metadata() -> bool:
    return os.name == "posix"


def allowed_owner(metadata: os.stat_result, *, root_only: bool = False) -> bool:
    if not enforce_posix_metadata():
        return True
    return metadata.st_uid == 0 if root_only else metadata.st_uid in {0, os.geteuid()}


def validate_private_directory(path: Path, *, sealed: bool = False) -> None:
    validate_no_symlink_path(path)
    metadata = path.stat()
    if not stat.S_ISDIR(metadata.st_mode) or not allowed_owner(metadata, root_only=sealed):
        raise QualificationError("insecure-run-directory")
    expected_mode = 0o550 if sealed else 0o700
    if enforce_posix_metadata() and stat.S_IMODE(metadata.st_mode) != expected_mode:
        raise QualificationError("insecure-run-directory")


def validate_private_file(
    path: Path,
    *,
    maximum_bytes: int = MAX_ARTIFACT_BYTES,
    expected_mode: int = 0o600,
    root_only: bool = False,
) -> os.stat_result:
    validate_no_symlink_path(path)
    try:
        before = path.lstat()
    except OSError as error:
        raise QualificationError("missing-artifact") from error
    if (
        stat.S_ISLNK(before.st_mode)
        or not stat.S_ISREG(before.st_mode)
        or not allowed_owner(before, root_only=root_only)
        or before.st_nlink != 1
        or before.st_size > maximum_bytes
    ):
        raise QualificationError("insecure-artifact")
    if enforce_posix_metadata() and stat.S_IMODE(before.st_mode) != expected_mode:
        raise QualificationError("insecure-artifact")
    return before


def read_private_file(
    path: Path,
    *,
    maximum_bytes: int = MAX_ARTIFACT_BYTES,
    expected_mode: int = 0o600,
    root_only: bool = False,
) -> bytes:
    before = validate_private_file(
        path,
        maximum_bytes=maximum_bytes,
        expected_mode=expected_mode,
        root_only=root_only,
    )
    try:
        raw = path.read_bytes()
        after = path.lstat()
    except OSError as error:
        raise QualificationError("artifact-read-failed") from error
    identity_before = (before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns)
    identity_after = (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns)
    if identity_before != identity_after or len(raw) != before.st_size:
        raise QualificationError("artifact-changed")
    return raw


def load_json(
    path: Path, *, expected_mode: int = 0o600, root_only: bool = False
) -> dict[str, Any]:
    document = decode_json(
        read_private_file(
            path,
            maximum_bytes=MAX_JSON_BYTES,
            expected_mode=expected_mode,
            root_only=root_only,
        )
    )
    if not isinstance(document, dict):
        raise QualificationError("invalid-json-root")
    return document


def sha256_bytes(raw: bytes) -> str:
    return f"sha256:{hashlib.sha256(raw).hexdigest()}"


def sha256_file(
    path: Path, *, expected_mode: int = 0o600, root_only: bool = False
) -> str:
    return sha256_bytes(
        read_private_file(path, expected_mode=expected_mode, root_only=root_only)
    )


def recovery_artifact_mode(name: str, *, sealed: bool) -> int:
    if name in RECOVERY_EXECUTABLE_FILES:
        return 0o550 if sealed else 0o700
    return 0o440 if sealed else 0o600


def is_production_run_directory(path: Path) -> bool:
    try:
        relative = path.relative_to(APPROVED_RUN_ROOT)
    except ValueError:
        return False
    return len(relative.parts) == 1


def exact_image_reference(value: Any, code: str) -> str:
    reference = require_string(value, IMAGE_REFERENCE, code)
    repository, separator, _ = reference.rpartition("@")
    if not separator or ":" in repository.rsplit("/", 1)[-1]:
        raise QualificationError(code)
    return reference


def fsync_directory(path: Path) -> None:
    if os.name != "posix":
        return
    descriptor = os.open(path, os.O_RDONLY | getattr(os, "O_DIRECTORY", 0))
    try:
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


def validate_write_target(path: Path) -> None:
    if not path.is_absolute():
        raise QualificationError("invalid-output-path")
    validate_no_symlink_path(path.parent)
    if path.exists() or path.is_symlink():
        validate_private_file(path, maximum_bytes=MAX_JSON_BYTES)


def atomic_write(path: Path, payload: bytes, mode: int = 0o600) -> None:
    validate_write_target(path)
    temporary = path.parent / f".{path.name}.{secrets.token_hex(12)}.tmp"
    descriptor = os.open(
        temporary,
        os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0),
        mode,
    )
    try:
        with os.fdopen(descriptor, "wb") as stream:
            descriptor = -1
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
        if os.name == "posix":
            os.chmod(path, mode)
        fsync_directory(path.parent)
    finally:
        if descriptor >= 0:
            os.close(descriptor)
        with contextlib.suppress(OSError):
            temporary.unlink()


def atomic_write_json(path: Path, document: dict[str, Any]) -> None:
    reject_controls(document)
    payload = (json.dumps(document, sort_keys=True, separators=(",", ":"), allow_nan=False) + "\n").encode()
    if len(payload) > MAX_JSON_BYTES:
        raise QualificationError("qualification-evidence-too-large")
    atomic_write(path, payload)


def require_fresh_root(
    document: dict[str, Any],
    schema: str,
    now: dt.datetime,
    maximum_age_seconds: int,
    code: str,
) -> dt.datetime:
    exact_object(document, JSON_ROOT_KEYS[schema], code)
    if (
        document["schema"] != schema
        or document.get("status") != "passed"
        or ("errors" in document and document["errors"] != [])
    ):
        raise QualificationError(code)
    return fresh_timestamp(document["generated_at_utc"], code, now, maximum_age_seconds)


def validate_image_pins(
    document: dict[str, Any], expected_commit: str, now: dt.datetime, maximum_age_seconds: int
) -> tuple[dict[str, str], dt.datetime]:
    generated = require_fresh_root(document, PINS_SCHEMA, now, maximum_age_seconds, "invalid-image-pins")
    if document["source_commit"] != expected_commit or document["registry_confirmed"] is not True:
        raise QualificationError("image-pin-binding-mismatch")
    pins = exact_object(document["pins"], set(IMAGE_SERVICES), "invalid-image-pin-inventory")
    repositories = exact_object(
        document["approved_repositories"], set(IMAGE_SERVICES), "invalid-image-pin-inventory"
    )
    manifests = exact_object(
        document["registry_manifests"], set(IMAGE_SERVICES), "invalid-image-pin-inventory"
    )
    result: dict[str, str] = {}
    for service in IMAGE_SERVICES:
        pin = exact_image_reference(pins[service], "invalid-image-pin")
        repository = repositories[service]
        if not isinstance(repository, str) or not pin.startswith(f"{repository}@"):
            raise QualificationError("image-repository-mismatch")
        manifest = exact_object(
            manifests[service], {"requested_ref", "manifest_sha256", "pin"}, "invalid-registry-manifest"
        )
        if manifest["pin"] != pin or not SHA256.fullmatch(str(manifest["manifest_sha256"])):
            raise QualificationError("registry-manifest-pin-mismatch")
        if manifest["requested_ref"] != f"{repository}:{expected_commit}":
            raise QualificationError("registry-manifest-commit-mismatch")
        result[service] = pin
    return result, generated


def validate_override(document: dict[str, Any], pins: dict[str, str]) -> None:
    root = exact_object(document, {"services"}, "invalid-pin-override")
    services = exact_object(root["services"], set(IMAGE_SERVICES), "invalid-pin-override-inventory")
    for service, pin in pins.items():
        entry = exact_object(services[service], {"image"}, "invalid-pin-override")
        if entry["image"] != pin:
            raise QualificationError("pin-override-mismatch")


def validate_preflight(
    document: dict[str, Any],
    pins: dict[str, str],
    now: dt.datetime,
    maximum_age_seconds: int,
    proxy_mode: str,
    expected_commit: str,
    expected_run_id: str,
    expected_attempt: int,
) -> tuple[dt.datetime, tuple[str, int] | None]:
    proxy_mode = require_proxy_mode(proxy_mode)
    generated = require_fresh_root(document, PREFLIGHT_SCHEMA, now, maximum_age_seconds, "invalid-compose-evidence")
    if document["deployment_authorized"] is not True:
        raise QualificationError("compose-not-authorized")
    checks = document["checks"]
    if not isinstance(checks, dict) or not checks or "digest-pinned-provenance-authorized-images" not in checks:
        raise QualificationError("invalid-compose-checks")
    for check in checks.values():
        exact_object(check, {"passed", "detail"}, "invalid-compose-check")
        if check["passed"] is not True:
            raise QualificationError("failed-compose-check")
    context = checks.get("publication-host-context")
    if not isinstance(context, dict):
        raise QualificationError("invalid-compose-authorization-context")
    context_detail = exact_object(
        context["detail"],
        {"context"},
        "invalid-compose-authorization-context",
    )
    if context_detail["context"] != "deployment-host":
        raise QualificationError("invalid-compose-authorization-context")
    manifest = exact_object(document["redacted_manifest"], {"services"}, "invalid-redacted-manifest")
    services = exact_object(manifest["services"], set(IMAGE_SERVICES), "invalid-compose-inventory")
    for service, pin in pins.items():
        entry = exact_object(
            services[service],
            {"image", "security_environment", "security_secrets", "ports", "healthcheck", "replicas"},
            "invalid-redacted-service",
        )
        if entry["image"] != pin or not isinstance(entry["security_environment"], dict):
            raise QualificationError("compose-pin-mismatch")
        rendered = json.dumps(entry, sort_keys=True)
        if "ws://" in rendered or JWT_SHAPE.search(rendered):
            raise QualificationError("insecure-compose-evidence")
    authorization = exact_object(
        checks["digest-pinned-provenance-authorized-images"]["detail"],
        {"authorization_requested", "authorized_services", "registry_confirmed", "provenance_verified", "services"},
        "invalid-compose-authorization-detail",
    )
    if (
        authorization["authorization_requested"] is not True
        or authorization["registry_confirmed"] is not True
        or authorization["provenance_verified"] is not True
        or authorization["authorized_services"] != sorted(IMAGE_SERVICES)
    ):
        raise QualificationError("invalid-compose-authorization-detail")
    authorized_services = exact_object(
        authorization["services"], set(IMAGE_SERVICES), "invalid-compose-authorization-inventory"
    )
    for service, pin in pins.items():
        if authorized_services[service] != {
            "image": pin,
            "expected": pin,
            "provenance_bound": True,
        }:
            raise QualificationError("compose-authorization-binding-mismatch")
    direct_publication: tuple[str, int] | None = None
    if proxy_mode == PROXY_MODE_DIRECT_KESTREL:
        publication = checks.get("hub-only-tls-publication")
        if not isinstance(publication, dict) or publication.get("passed") is not True:
            raise QualificationError("direct-kestrel-topology-unverified")
        detail = exact_object(
            publication.get("detail"), {"ports", "expected"},
            "direct-kestrel-topology-unverified",
        )
        expected_port = require_int(
            detail["expected"],
            "direct-kestrel-topology-unverified",
            minimum=1,
            maximum=65_535,
        )
        ports = detail["ports"]
        if not isinstance(ports, list) or len(ports) != 1:
            raise QualificationError("direct-kestrel-topology-unverified")
        port = exact_object(
            ports[0], {"target", "published", "protocol", "host_ip"},
            "direct-kestrel-topology-unverified",
        )
        if (
            port["target"] != 8443
            or port["published"] != expected_port
            or port["protocol"] != "tcp"
            or not isinstance(port["host_ip"], str)
            or any(ord(character) < 0x20 or ord(character) == 0x7F for character in port["host_ip"])
            or services["bolt-hub"]["ports"] != ports
        ):
            raise QualificationError("direct-kestrel-topology-unverified")
        host_inventory = checks.get("direct-publication-host-interface")
        if not isinstance(host_inventory, dict) or host_inventory.get("passed") is not True:
            raise QualificationError("direct-kestrel-host-interface-unverified")
        host_detail = exact_object(
            host_inventory.get("detail"),
            {"binding", "resolved_addresses", "host_interface_addresses", "matched_addresses"},
            "direct-kestrel-host-interface-unverified",
        )
        resolved_addresses = require_host_addresses(
            host_detail["resolved_addresses"], "direct-kestrel-host-interface-unverified"
        )
        host_addresses = require_host_addresses(
            host_detail["host_interface_addresses"], "direct-kestrel-host-interface-unverified"
        )
        matched_addresses = require_host_addresses(
            host_detail["matched_addresses"], "direct-kestrel-host-interface-unverified"
        )
        binding = host_detail["binding"]
        if binding not in ALL_INTERFACE_BINDINGS:
            try:
                binding = ipaddress.ip_address(binding).compressed
            except (TypeError, ValueError) as error:
                raise QualificationError("direct-kestrel-host-interface-unverified") from error
        if (
            host_detail["binding"] != port["host_ip"]
            or matched_addresses != sorted(set(resolved_addresses) & set(host_addresses))
            or matched_addresses != resolved_addresses
            or (binding not in ALL_INTERFACE_BINDINGS and binding not in matched_addresses)
        ):
            raise QualificationError("direct-kestrel-host-interface-unverified")
        topology = checks.get("operator-attested-direct-publication-topology")
        if not isinstance(topology, dict) or topology.get("passed") is not True:
            raise QualificationError("direct-kestrel-topology-unattested")
        topology_detail = exact_object(
            topology.get("detail"),
            {
                "attestation", "attested_by", "attested_by_id", "triggering_actor",
                "workflow_event", "run_id", "run_attempt",
                "source_commit", "published_hostname", "published_port", "mode",
                "intermediaries", "scope", "binding", "resolved_addresses",
                "host_interface_addresses", "matched_addresses",
            },
            "direct-kestrel-topology-unattested",
        )
        published_hostname = require_hostname(
            topology_detail["published_hostname"],
            "direct-kestrel-topology-unattested",
        )
        if (
            topology_detail["attestation"] != DIRECT_PUBLICATION_ATTESTATION
            or not isinstance(topology_detail["attested_by"], str)
            or GITHUB_ACTOR.fullmatch(topology_detail["attested_by"]) is None
            or not isinstance(topology_detail["attested_by_id"], str)
            or GITHUB_ACTOR_ID.fullmatch(topology_detail["attested_by_id"]) is None
            or topology_detail["triggering_actor"] != topology_detail["attested_by"]
            or topology_detail["workflow_event"] != "workflow_dispatch"
            or topology_detail["run_id"] != expected_run_id
            or topology_detail["run_attempt"] != 1
            or expected_attempt != 1
            or topology_detail["source_commit"] != expected_commit
            or topology_detail["published_port"] != expected_port
            or topology_detail["mode"] != PROXY_MODE_DIRECT_KESTREL
            or topology_detail["intermediaries"] != []
            or topology_detail["scope"]
            != ["host-reverse-proxy", "tailscale-serve", "load-balancer", "ingress"]
            or topology_detail["binding"] != host_detail["binding"]
            or topology_detail["resolved_addresses"] != resolved_addresses
            or topology_detail["host_interface_addresses"] != host_addresses
            or topology_detail["matched_addresses"] != matched_addresses
        ):
            raise QualificationError("direct-kestrel-topology-unattested")
        direct_publication = (published_hostname, expected_port)
    return generated, direct_publication


def validate_tls(
    document: dict[str, Any], schema: str, hostname: str, now: dt.datetime, maximum_age_seconds: int
) -> dt.datetime:
    generated = require_fresh_root(document, schema, now, maximum_age_seconds, "invalid-tls-evidence")
    if document["internal_hostname"] != hostname:
        raise QualificationError("tls-hostname-mismatch")
    require_hostname(document["published_hostname"], "invalid-tls-hostname")
    require_int(
        document["published_port"],
        "invalid-tls-port",
        minimum=1,
        maximum=65_535,
    )
    certificate = exact_object(
        document["certificate"],
        {
            "subject", "issuer", "serial", "not_before", "not_after", "sha256_fingerprint",
            "subject_alternative_name", "chain_verified", "hostname_verified", "currently_valid",
        },
        "invalid-certificate-evidence",
    )
    if any(certificate[field] is not True for field in ("chain_verified", "hostname_verified", "currently_valid")):
        raise QualificationError("failed-certificate-check")
    certificate_strings = (
        "subject", "issuer", "serial", "not_before", "not_after", "sha256_fingerprint",
        "subject_alternative_name",
    )
    for field in certificate_strings:
        if not isinstance(certificate[field], str) or not certificate[field]:
            raise QualificationError("invalid-certificate-evidence")
    private_key = exact_object(
        document["private_key"], {"value", "matches_certificate", "mode"}, "invalid-private-key-evidence"
    )
    if private_key["value"] != "<redacted>" or private_key["matches_certificate"] is not True:
        raise QualificationError("private-key-evidence-not-redacted")
    if str(private_key["mode"]).lstrip("0") != "600":
        raise QualificationError("insecure-private-key-mode")
    if schema == IDENTITY_TLS_SCHEMA and document["token_path"] != "/api/service-identity/bolt-transport-token":
        raise QualificationError("identity-token-path-mismatch")
    return generated


def validate_provenance(
    document: dict[str, Any],
    pins: dict[str, str],
    expected_commit: str,
    expected_run_id: str,
    expected_attempt: int,
    now: dt.datetime,
    maximum_age_seconds: int,
) -> dt.datetime:
    generated = require_fresh_root(document, PROVENANCE_SCHEMA, now, maximum_age_seconds, "invalid-provenance")
    dockerfile_digest = require_string(document["dockerfile_digest"], SHA256, "invalid-provenance-digest")
    if document["source_commit"] != expected_commit:
        raise QualificationError("provenance-commit-mismatch")
    bindings = exact_object(document["bindings"], set(IMAGE_SERVICES), "invalid-provenance-inventory")
    expected_invocation_suffix = f"/actions/runs/{expected_run_id}/attempts/{expected_attempt}"
    binding_keys = {
        "pin", "source_commit", "source_repository", "builder_id", "workflow_ref", "workflow_invocation",
        "dockerfile", "build", "base_images", "cosign_verification_sha256", "cosign_bundle_sha256",
        "dsse_payload_sha256", "certificate_identity_policy", "certificate_oidc_issuer_policy",
        "certificate_workflow_repository_policy", "certificate_workflow_sha_policy", "signature_verified",
        "transparency_log_verified", "verified_dsse_envelope",
    }
    for service, pin in pins.items():
        binding = exact_object(bindings[service], binding_keys, "invalid-provenance-binding")
        if (
            binding["pin"] != pin
            or binding["source_commit"] != expected_commit
            or binding["certificate_workflow_sha_policy"] != expected_commit
            or binding["signature_verified"] is not True
            or binding["transparency_log_verified"] is not True
            or not str(binding["workflow_invocation"]).endswith(expected_invocation_suffix)
            or binding["certificate_identity_policy"] != binding["workflow_ref"]
            or binding["certificate_oidc_issuer_policy"] != "https://token.actions.githubusercontent.com"
        ):
            raise QualificationError("provenance-binding-mismatch")
        dockerfile = exact_object(binding["dockerfile"], {"path", "digest"}, "invalid-provenance-dockerfile")
        if dockerfile != {"path": "Dockerfile", "digest": dockerfile_digest}:
            raise QualificationError("provenance-dockerfile-mismatch")
        for field in ("cosign_verification_sha256", "cosign_bundle_sha256", "dsse_payload_sha256"):
            require_string(binding[field], SHA256, "invalid-provenance-digest")
        build = exact_object(
            binding["build"], {"context", "dockerfile", "args", "target"}, "invalid-provenance-build"
        )
        args = exact_object(build["args"], {"PROJECT_PATH"}, "invalid-provenance-build")
        if (
            build["context"] != "."
            or build["dockerfile"] != "Dockerfile"
            or build["target"] is not None
            or not isinstance(args["PROJECT_PATH"], str)
            or not re.fullmatch(r"[A-Za-z0-9._/-]+\.csproj", args["PROJECT_PATH"])
            or not str(binding["workflow_invocation"]).startswith(f"{binding['source_repository']}/actions/runs/")
        ):
            raise QualificationError("invalid-provenance-build")
        bases = binding["base_images"]
        if not isinstance(bases, list) or len(bases) < 2 or len(bases) != len(set(bases)):
            raise QualificationError("invalid-provenance-base-images")
        for base in bases:
            exact_image_reference(base, "invalid-provenance-base-image")
        if not isinstance(binding["verified_dsse_envelope"], dict) or not binding["verified_dsse_envelope"]:
            raise QualificationError("missing-verified-dsse-envelope")
    return generated


def validate_runtime_service(
    service: str,
    value: Any,
    pin: str,
    expected_published_ports: dict[str, int],
) -> None:
    item = exact_object(value, RUNTIME_SERVICE_KEYS, "invalid-runtime-service")
    if item["service"] != service or item["configured_image"] != pin:
        raise QualificationError("runtime-image-mismatch")
    require_string(item["container_id"], CONTAINER_ID, "invalid-runtime-container")
    require_string(item["local_image_id"], SHA256, "invalid-runtime-image-id")
    if (
        not isinstance(item["repo_digests"], list)
        or pin not in item["repo_digests"]
        or any(not isinstance(value, str) or not IMAGE_REFERENCE.fullmatch(value) for value in item["repo_digests"])
    ):
        raise QualificationError("runtime-repository-digest-mismatch")
    parse_timestamp(item["started_at"], "invalid-runtime-start")
    if service == "migrate":
        if item["running"] is not False or item["status"] != "exited" or item["exit_code"] != 0:
            raise QualificationError("migration-runtime-not-successful")
    elif item["running"] is not True or item["status"] != "running" or item["health"] != "healthy":
        raise QualificationError("runtime-not-ready")
    if service in {"bolt-hub", "identityserver"}:
        listeners = item["listeners"]
        if not isinstance(listeners, list) or not listeners:
            raise QualificationError("missing-tls-service-listeners")
        normalized_listeners: list[tuple[int, str]] = []
        for entry in listeners:
            listener = exact_object(
                entry, {"family", "scope", "port"}, "invalid-tls-service-listeners"
            )
            if listener["family"] not in {"ipv4", "ipv6"}:
                raise QualificationError("invalid-tls-service-listeners")
            normalized_listeners.append((listener["port"], listener["scope"]))
        expected_http_scope = "loopback" if service == "bolt-hub" else "wildcard"
        expected_listeners = {(8080, expected_http_scope), (8443, "wildcard")}
        if len(normalized_listeners) != 2 or set(normalized_listeners) != expected_listeners:
            raise QualificationError("invalid-tls-service-listeners")
        publication = exact_object(
            item["published_port"], {"container_port", "published_port", "protocol"},
            "invalid-tls-service-publication",
        )
        if publication != {
            "container_port": 8443,
            "published_port": expected_published_ports[service],
            "protocol": "tcp",
        }:
            raise QualificationError("invalid-tls-service-publication")
        mounts = item["private_key_mounts"]
        expected_target = (
            "/run/secrets/bolt-hub-tls-private-key.pem"
            if service == "bolt-hub"
            else "/run/secrets/identityserver-tls-private-key.pem"
        )
        expected_mounts = [
            {
                "resolved_source": "<expected-private-key>",
                "relation": "exact",
                "target": expected_target,
                "read_only": True,
            }
        ]
        if service == "identityserver":
            expected_mounts.append(
                {
                    "resolved_source": "<identity-signing-key-volume>",
                    "relation": "persistent-volume",
                    "target": "/var/lib/xframework/identity",
                    "read_only": False,
                }
            )
        if not isinstance(mounts, list) or mounts != expected_mounts:
            raise QualificationError("invalid-tls-service-private-key-mount")
    elif item["listeners"] != [] or item["published_port"] is not None or item["private_key_mounts"] != []:
        raise QualificationError("unexpected-runtime-boundary-evidence")


def validate_runtime(
    document: dict[str, Any],
    expected_services: tuple[str, ...],
    pins: dict[str, str],
    mode: str,
    now: dt.datetime,
    maximum_age_seconds: int,
    expected_published_ports: dict[str, int],
    *,
    not_before: dt.datetime | None = None,
) -> dt.datetime:
    generated = require_fresh_root(document, RUNTIME_SCHEMA, now, maximum_age_seconds, "invalid-runtime-evidence")
    if not_before is not None and generated < not_before - dt.timedelta(seconds=MAX_CLOCK_SKEW_SECONDS):
        raise QualificationError("stale-runtime-evidence")
    if (
        document["inventory_mode"] != mode
        or document["requested_services"] != list(expected_services)
        or document["intentionally_inactive_services"] != ["bolt-phase0-synthetics"]
        or document["expected_images"] != pins
    ):
        raise QualificationError("runtime-inventory-mismatch")
    services = exact_object(document["services"], set(expected_services), "runtime-service-coverage-mismatch")
    for service in expected_services:
        validate_runtime_service(
            service, services[service], pins[service], expected_published_ports
        )
    return generated


def validate_rotation_document(
    document: dict[str, Any], phase: str, common: dict[str, str] | None = None
) -> tuple[dict[str, str], dict[str, dt.datetime | None]]:
    exact_object(document, ROTATION_KEYS, "invalid-rotation-evidence")
    if document["schema"] != ROTATION_SCHEMA or document["phase"] != phase:
        raise QualificationError("rotation-phase-mismatch")
    values = {
        "rotation_id": require_string(document["rotation_id"], GENERATION_ID, "invalid-rotation-id"),
        "previous_generation_id": require_string(
            document["previous_generation_id"], GENERATION_ID, "invalid-generation-id"
        ),
        "target_generation_id": require_string(
            document["target_generation_id"], GENERATION_ID, "invalid-generation-id"
        ),
    }
    if values["previous_generation_id"] == values["target_generation_id"]:
        raise QualificationError("identical-rotation-generations")
    if common is not None and values != common:
        raise QualificationError("rotation-binding-mismatch")
    timestamps: dict[str, dt.datetime | None] = {
        "expiry": parse_timestamp(document["secondary_valid_until_utc"], "invalid-rotation-expiry"),
        "prepared": parse_timestamp(document["prepared_at_utc"], "invalid-rotation-timestamp")
        if document["prepared_at_utc"] is not None else None,
        "activated": parse_timestamp(document["activated_at_utc"], "invalid-rotation-timestamp")
        if document["activated_at_utc"] is not None else None,
        "converged": parse_timestamp(document["convergence_verified_at_utc"], "invalid-rotation-timestamp")
        if document["convergence_verified_at_utc"] is not None else None,
        "finalized": parse_timestamp(document["finalized_at_utc"], "invalid-rotation-timestamp")
        if document["finalized_at_utc"] is not None else None,
    }
    required = {
        "prepared": {"prepared"},
        "activated": {"prepared", "activated"},
        "converged": {"prepared", "activated", "converged"},
        "finalized": {"prepared", "activated", "converged", "finalized"},
    }[phase]
    for name in ("prepared", "activated", "converged", "finalized"):
        if (name in required) != (timestamps[name] is not None):
            raise QualificationError("rotation-timestamp-shape-mismatch")
    ordered = [timestamps[name] for name in ("prepared", "activated", "converged", "finalized") if timestamps[name]]
    if ordered != sorted(ordered) or timestamps["prepared"] >= timestamps["expiry"]:
        raise QualificationError("rotation-timestamp-order-mismatch")
    if timestamps["finalized"] is not None and timestamps["finalized"] < timestamps["expiry"]:
        raise QualificationError("rotation-finalized-before-expiry")
    return values, timestamps


def validate_generation_inventory(document: dict[str, Any], target: str) -> dt.datetime:
    exact_object(document, {"schema", "generated_at_utc", "services"}, "invalid-generation-inventory")
    if document["schema"] != GENERATION_INVENTORY_SCHEMA:
        raise QualificationError("invalid-generation-inventory")
    services = exact_object(document["services"], set(ROTATION_SERVICES), "generation-inventory-mismatch")
    if any(generation != target for generation in services.values()):
        raise QualificationError("generation-not-converged")
    return parse_timestamp(document["generated_at_utc"], "invalid-generation-inventory-timestamp")


def validate_credential_convergence(
    document: dict[str, Any],
    phase: str,
    target: str,
    retiring: str,
    now: dt.datetime,
    maximum_age_seconds: int,
) -> dt.datetime:
    generated = require_fresh_root(
        document, CREDENTIAL_CONVERGENCE_SCHEMA, now, maximum_age_seconds, "invalid-credential-convergence"
    )
    current_count = require_int(
        document["current_token_count"], "invalid-current-token-coverage", minimum=2
    )
    retired_count = require_int(
        document["retired_token_count"], "invalid-retired-token-coverage"
    )
    if (
        document["phase"] != phase
        or document["target_generation_id"] != target
        or document["retiring_generation_id"] != retiring
        or document["service_count"] != len(ROTATION_SERVICES)
        or document["identityserver_client_count"] != len(ROTATION_SERVICES)
        or current_count < 2
        or (phase == "dual-validation" and retired_count < 2)
        or (phase == "finalized" and retired_count != 0)
    ):
        raise QualificationError("credential-convergence-binding-mismatch")
    observed = fresh_timestamp(
        document["observed_at_utc"], "invalid-convergence-observation", now,
        maximum_age_seconds,
    )
    fallback = document["fallback_valid_until_utc"]
    if phase == "dual-validation":
        if fallback is None or parse_timestamp(fallback, "invalid-fallback-expiry") <= observed:
            raise QualificationError("invalid-dual-validation-fallback")
    elif fallback is not None:
        raise QualificationError("finalized-fallback-residue")
    return generated


def validate_probe_receipt(
    receipt: Any,
    expected_probe: str,
    expected_assertions: dict[str, Any],
    synthetic_started: dt.datetime,
    synthetic_completed: dt.datetime,
) -> None:
    item = exact_object(
        receipt, {"schemaVersion", "probe", "status", "startedAtUtc", "completedAtUtc", "assertions"},
        "invalid-probe-receipt",
    )
    if item["schemaVersion"] != PROBE_SCHEMA or item["probe"] != expected_probe or item["status"] != "passed":
        raise QualificationError("invalid-probe-receipt")
    started = parse_timestamp(item["startedAtUtc"], "invalid-probe-timestamp")
    completed = parse_timestamp(item["completedAtUtc"], "invalid-probe-timestamp")
    if (
        completed < started
        or started < synthetic_started - dt.timedelta(seconds=MAX_CLOCK_SKEW_SECONDS)
        or completed > synthetic_completed + dt.timedelta(seconds=MAX_CLOCK_SKEW_SECONDS)
        or item["assertions"] != expected_assertions
    ):
        raise QualificationError("invalid-probe-receipt")


def retained_marker_assertions(marker_count: int) -> dict[str, Any]:
    return {
        "retainedStoreQueried": True,
        "matches": 0,
        "tokensSearched": marker_count,
        "markersSearched": marker_count,
    }


def proxy_marker_assertions(proxy_mode: str, marker_count: int) -> dict[str, Any]:
    proxy_mode = require_synthetic_proxy_mode(proxy_mode)
    if proxy_mode == PROXY_MODE_LOGS:
        return retained_marker_assertions(marker_count)
    return {
        "retainedStoreQueried": False,
        "notApplicableReason": "direct-kestrel-publication",
        "matches": 0,
        "tokensSearched": marker_count,
        "markersSearched": marker_count,
    }


def safe_url(value: Any, scheme: str, code: str) -> str:
    if not isinstance(value, str):
        raise QualificationError(code)
    parsed = urlsplit(value)
    if (
        parsed.scheme != scheme
        or not parsed.hostname
        or parsed.username
        or parsed.password
        or parsed.query
        or parsed.fragment
    ):
        raise QualificationError(code)
    return value


def validate_synthetic(
    document: dict[str, Any],
    expected_stage: str,
    now: dt.datetime,
    maximum_age_seconds: int,
    *,
    not_before: dt.datetime | None = None,
    proxy_mode: str,
) -> tuple[str, dt.datetime, dt.datetime]:
    proxy_mode = require_synthetic_proxy_mode(proxy_mode)
    exact_object(document, SYNTHETIC_KEYS, "invalid-synthetic-evidence")
    if (
        document["schemaVersion"] != SYNTHETIC_SCHEMA
        or document["stage"] != expected_stage
        or document["status"] != "passed"
        or not HEX_SHA256.fullmatch(str(document["coreReportSha256"]))
    ):
        raise QualificationError("synthetic-stage-or-status-mismatch")
    try:
        run_id = str(uuid.UUID(str(document["runId"])))
    except (ValueError, AttributeError) as error:
        raise QualificationError("invalid-synthetic-run-id") from error
    core = exact_object(document["synthetic"], CORE_KEYS, "invalid-synthetic-core")
    if core["schemaVersion"] != SYNTHETIC_CORE_SCHEMA or core["runId"] != run_id or core["status"] != "passed":
        raise QualificationError("synthetic-core-binding-mismatch")
    started = fresh_timestamp(
        core["startedAtUtc"], "invalid-synthetic-time", now, maximum_age_seconds,
        not_before=not_before,
    )
    completed = fresh_timestamp(
        core["completedAtUtc"], "invalid-synthetic-time", now, maximum_age_seconds,
        not_before=not_before,
    )
    if completed < started:
        raise QualificationError("synthetic-time-order-mismatch")
    if safe_url(core["target"], "wss", "invalid-synthetic-target") != DIRECT_KESTREL_TARGET:
        raise QualificationError("invalid-synthetic-target")
    timings = exact_object(core["timings"], {"totalMs"}, "invalid-synthetic-timings")
    require_int(timings["totalMs"], "invalid-synthetic-timings")
    prefixes = core["tokenSha256Prefixes"]
    if (
        not isinstance(prefixes, dict)
        or len(prefixes) < 2
        or any(
            not isinstance(key, str) or not re.fullmatch(r"[0-9a-f]{12}", str(value))
            for key, value in prefixes.items()
        )
    ):
        raise QualificationError("invalid-synthetic-token-prefixes")
    required_prefixes = {"communications", "user"}
    if expected_stage in {"canary", "finalized"}:
        required_prefixes.add("expiry")
    if not required_prefixes.issubset(prefixes):
        raise QualificationError("missing-synthetic-token-prefix")
    operations = core["operations"]
    if not isinstance(operations, list) or not operations:
        raise QualificationError("invalid-synthetic-operations")
    by_name: dict[str, dict[str, Any]] = {}
    for operation in operations:
        item = exact_object(operation, OPERATION_KEYS, "invalid-synthetic-operation")
        name = require_string(item["name"], SAFE_NAME, "invalid-synthetic-operation")
        if name in by_name or item["status"] != "passed" or not isinstance(item["results"], dict):
            raise QualificationError("invalid-synthetic-operation")
        require_int(item["timingMs"], "invalid-synthetic-operation")
        operation_start = parse_timestamp(item["startedAtUtc"], "invalid-synthetic-operation-time")
        operation_end = parse_timestamp(item["completedAtUtc"], "invalid-synthetic-operation-time")
        if (
            operation_start < started
            or operation_end < operation_start
            or operation_end > completed + dt.timedelta(seconds=1)
        ):
            raise QualificationError("invalid-synthetic-operation-time")
        if any(
            not isinstance(key, str)
            or not SAFE_NAME.fullmatch(key)
            or not isinstance(value, str)
            or not SAFE_RESULT.fullmatch(value)
            for key, value in item["results"].items()
        ):
            raise QualificationError("unsafe-synthetic-result")
        by_name[name] = item
    required = set(REQUIRED_OPERATIONS)
    if expected_stage in {"canary", "finalized"}:
        required.add("token_expiry_disconnect")
    if not required.issubset(by_name):
        raise QualificationError("missing-synthetic-operation")
    ack = by_name["durable_ack"]["results"]
    if ack.get("duplicate_ack_idempotent") != "true" or ack.get("out_of_order_ack_monotonic") != "true":
        raise QualificationError("incomplete-ack-evidence")

    post = exact_object(document["postRunEvidence"], POST_RUN_KEYS, "invalid-post-run-evidence")
    if post["schemaVersion"] != POST_RUN_SCHEMA:
        raise QualificationError("invalid-post-run-evidence")
    refresh = exact_object(
        post["tokenRefresh"],
        {
            "status", "issuerUri", "principalReferenceSha256Prefix", "refreshedAtUtc",
            "minimumRemainingLifetimeSeconds", "expiryTokenIssued",
        },
        "invalid-token-refresh-evidence",
    )
    if (
        refresh["status"] != "passed"
        or not re.fullmatch(r"[0-9a-f]{12}", str(refresh["principalReferenceSha256Prefix"]))
    ):
        raise QualificationError("invalid-token-refresh-evidence")
    safe_url(refresh["issuerUri"], "https", "invalid-token-issuer")
    refreshed = parse_timestamp(refresh["refreshedAtUtc"], "invalid-token-refresh-time")
    if refreshed > started + dt.timedelta(seconds=MAX_CLOCK_SKEW_SECONDS):
        raise QualificationError("invalid-token-refresh-time")
    require_int(refresh["minimumRemainingLifetimeSeconds"], "invalid-token-lifetime", minimum=60)
    expected_expiry = expected_stage in {"canary", "finalized"}
    if refresh["expiryTokenIssued"] is not expected_expiry:
        raise QualificationError("expiry-token-stage-mismatch")
    marker = exact_object(
        post["markerAbsence"], {"application", "proxy", "seq", "trace", "markerSha256Prefixes"},
        "invalid-marker-absence-evidence",
    )
    expected_proxy_marker = (
        "not_applicable" if proxy_mode == PROXY_MODE_DIRECT_KESTREL else "passed"
    )
    if (
        marker["application"] != "passed"
        or marker["proxy"] != expected_proxy_marker
        or marker["seq"] != "passed"
        or marker["trace"] != "passed"
    ):
        raise QualificationError("marker-absence-failed")
    if not isinstance(marker["markerSha256Prefixes"], dict) or any(
        not re.fullmatch(r"[0-9a-f]{12}", str(value)) for value in marker["markerSha256Prefixes"].values()
    ):
        raise QualificationError("invalid-marker-prefixes")
    if set(marker["markerSha256Prefixes"]) != required_prefixes:
        raise QualificationError("marker-prefix-coverage-mismatch")
    if post["plaintextRejection"] != "passed" or post["tokenFilesStableForRun"] != "passed":
        raise QualificationError("post-run-security-check-failed")
    if post["expiryDisconnect"] != ("passed" if expected_expiry else "not_required"):
        raise QualificationError("expiry-disconnect-stage-mismatch")
    if post["redisInterruptionRecovery"] != ("passed" if expected_stage == "canary" else "not_required"):
        raise QualificationError("redis-stage-mismatch")
    if post["oldGenerationCredentialRejection"] != ("passed" if expected_stage == "finalized" else "not_required"):
        raise QualificationError("old-generation-stage-mismatch")
    receipts = post["probeReceipts"]
    marker_count = len(required_prefixes)
    marker_assertions = retained_marker_assertions(marker_count)
    proxy_assertions = proxy_marker_assertions(proxy_mode, marker_count)
    expected_receipts: dict[str, tuple[str, dict[str, Any]]] = {
        "proxyMarkerScan": ("proxy-marker-scan", proxy_assertions),
        "seqMarkerScan": ("seq-marker-scan", marker_assertions),
        "traceMarkerScan": ("trace-marker-scan", marker_assertions),
        "plaintextRejection": (
            "plaintext-rejection", {"plaintextRejected": True, "bearerSent": False}
        ),
    }
    if expected_stage == "canary":
        expected_receipts["redisInterruption"] = (
            "redis-interruption",
            {
                "interruptionInduced": True,
                "recovered": True,
                "postRecoverySyntheticPassed": True,
                "dataLossObserved": False,
            },
        )
    if expected_stage == "finalized":
        expected_receipts["oldGenerationRejection"] = (
            "old-generation-rejection",
            {
                "oldUserTokenRejected": True,
                "oldServiceTokenRejected": True,
                "oldClientSecretRejected": True,
                "currentHttpHealthPassed": True,
                "currentBoltHealthPassed": True,
            },
        )
    exact_object(receipts, set(expected_receipts), "synthetic-probe-coverage-mismatch")
    for name, (probe, assertions) in expected_receipts.items():
        validate_probe_receipt(receipts[name], probe, assertions, started, completed)
    serialized = json.dumps(document, sort_keys=True)
    secret_key = re.compile(r'(?i)"(?:password|clientsecret|authorization|access_token)"')
    if JWT_SHAPE.search(serialized) or secret_key.search(serialized):
        raise QualificationError("secret-bearing-synthetic-evidence")
    return run_id, started, completed


def validate_observation(
    document: dict[str, Any],
    canary_interval: tuple[dt.datetime, dt.datetime],
    now: dt.datetime,
    maximum_age_seconds: int,
) -> dt.datetime:
    generated = require_fresh_root(document, OBSERVATION_SCHEMA, now, maximum_age_seconds, "invalid-observation")
    observation = exact_object(
        document["observation"], {"started_at_utc", "completed_at_utc", "duration_seconds", "sample_count"},
        "invalid-observation-summary",
    )
    started = parse_timestamp(observation["started_at_utc"], "invalid-observation-time")
    completed = parse_timestamp(observation["completed_at_utc"], "invalid-observation-time")
    duration = observation["duration_seconds"]
    samples = require_int(observation["sample_count"], "invalid-observation-samples", minimum=1)
    if (
        isinstance(duration, bool)
        or not isinstance(duration, (int, float))
        or not math.isfinite(duration)
        or duration <= 0
    ):
        raise QualificationError("invalid-observation-duration")
    if not (started <= canary_interval[0] <= canary_interval[1] <= completed) or samples < 2:
        raise QualificationError("observation-does-not-cover-canary")
    health = document["health_aggregates"]
    if (
        not isinstance(health, dict)
        or health.get("sample_count") != samples
        or health.get("transport_snapshot_count") != samples
    ):
        raise QualificationError("invalid-observation-health-aggregates")
    synthetics = exact_object(
        document["synthetic_aggregates"], {"report_count", "operation_latency"},
        "invalid-observation-synthetic-aggregates",
    )
    if require_int(synthetics["report_count"], "invalid-observation-report-count", minimum=1) < 1:
        raise QualificationError("invalid-observation-report-count")
    if not isinstance(synthetics["operation_latency"], dict) or not synthetics["operation_latency"]:
        raise QualificationError("invalid-observation-latency-aggregates")
    if not isinstance(document["thresholds"], dict) or not document["thresholds"]:
        raise QualificationError("invalid-observation-thresholds")
    return generated


def validate_candidate_restart(
    document: dict[str, Any],
    run_id: str,
    attempt: int,
    commit: str,
    target_generation: str,
    project_name: str,
    digests: dict[str, str],
    now: dt.datetime,
    maximum_age_seconds: int,
) -> tuple[dt.datetime, dt.datetime]:
    exact_object(document, CANDIDATE_RESTART_KEYS, "invalid-rollback-drill")
    if (
        document["schema"] != CANDIDATE_RESTART_SCHEMA
        or document["status"] != "passed"
        or document["errors"] != []
        or document["run_id"] != run_id
        or document["run_attempt"] != attempt
        or document["source_commit"] != commit
        or document["project_name"] != project_name
        or document["credential_generation_id"] != target_generation
        or document["lkg_compatibility"] not in {"rendered", "not_applicable_no_prior_lkg"}
    ):
        raise QualificationError("rollback-drill-binding-mismatch")
    started = fresh_timestamp(document["started_at_utc"], "invalid-rollback-time", now, maximum_age_seconds)
    completed = fresh_timestamp(document["completed_at_utc"], "invalid-rollback-time", now, maximum_age_seconds)
    if completed < started:
        raise QualificationError("invalid-rollback-time")
    expected_digests = {
        "manifest_sha256": digests["docker-compose.yml"],
        "override_sha256": digests["pinned-compose.override.json"],
        "pins_sha256": digests["image-pins.json"],
        "runtime_evidence_sha256": digests["rollback-runtime-evidence.json"],
        "synthetic_evidence_sha256": digests["rollback-synthetics-finalized.json"],
    }
    if any(document[field] != value for field, value in expected_digests.items()):
        raise QualificationError("rollback-drill-digest-mismatch")
    checks = exact_object(document["checks"], CANDIDATE_RESTART_CHECK_KEYS, "invalid-rollback-checks")
    if any(value is not True for value in checks.values()):
        raise QualificationError("rollback-drill-check-failed")
    return started, completed


def validate_expected_inventory(run_directory: Path) -> None:
    runtime_names = {path.name for path in run_directory.glob("*runtime*.json")}
    expected_runtime = set(RUNTIME_INVENTORIES)
    if runtime_names != expected_runtime:
        raise QualificationError("unexpected-runtime-inventory")
    synthetic_names = {path.name for path in run_directory.glob("*synthetics*.json")}
    expected_synthetics = set(SYNTHETIC_FILES)
    if synthetic_names != expected_synthetics:
        raise QualificationError("unexpected-synthetic-inventory")


def artifact_summary(path: Path, document: dict[str, Any] | None, digest: str) -> dict[str, Any]:
    schema = None
    generated = None
    if document is not None:
        schema = document.get("schema") or document.get("schemaVersion")
        generated = document.get("generated_at_utc") or document.get("completed_at_utc")
        if generated is None and isinstance(document.get("synthetic"), dict):
            generated = document["synthetic"].get("completedAtUtc")
    return {"path": path.name, "sha256": digest, "schema": schema, "generated_at_utc": generated}


def validate_run_identity(run_directory: Path, run_id: str, attempt: int, commit: str) -> None:
    require_string(run_id, RUN_ID, "invalid-run-id")
    require_int(attempt, "invalid-run-attempt", minimum=1)
    require_string(commit, COMMIT, "invalid-commit")
    if run_directory.name != f"{run_id}-{attempt}":
        raise QualificationError("run-directory-identity-mismatch")


def qualification_failure(
    run_id: str,
    attempt: int,
    commit: str,
    proxy_mode: str,
    now: dt.datetime,
    code: str,
) -> dict[str, Any]:
    return {
        "schema": QUALIFICATION_SCHEMA,
        "status": "failed",
        "generated_at_utc": format_utc(now),
        "run_id": run_id,
        "run_attempt": attempt,
        "source_commit": commit,
        "proxy_mode": proxy_mode,
        "credential_generation_id": None,
        "artifacts": {},
        "runtime_stages": {},
        "synthetic_stages": {},
        "checks": {
            "artifact_security": False,
            "schema_and_status": False,
            "identity_and_digest_binding": False,
            "rotation_and_convergence": False,
            "canary_observation": False,
            "candidate_restart": False,
        },
        "errors": [code],
    }


def verify_qualification(
    run_directory: Path,
    expected_commit: str,
    expected_run_id: str,
    expected_attempt: int,
    project_name: str,
    maximum_age_seconds: int,
    *,
    proxy_mode: str,
    now: dt.datetime | None = None,
) -> dict[str, Any]:
    now = (now or utc_now()).astimezone(dt.timezone.utc)
    proxy_mode = require_proxy_mode(proxy_mode)
    if not PROJECT_NAME.fullmatch(project_name):
        raise QualificationError("invalid-project-name")
    if not 60 <= maximum_age_seconds <= 7 * 24 * 60 * 60:
        raise QualificationError("invalid-maximum-evidence-age")
    validate_run_identity(run_directory, expected_run_id, expected_attempt, expected_commit)
    validate_private_directory(run_directory)
    validate_expected_inventory(run_directory)
    paths = {name: run_directory / name for name in ARTIFACT_FILES}
    raw = {
        name: read_private_file(
            path,
            expected_mode=recovery_artifact_mode(name, sealed=False)
            if name in RECOVERY_TOOL_FILES
            else 0o600,
        )
        for name, path in paths.items()
    }
    artifact_names = {path.name for path in run_directory.iterdir()}
    if artifact_names != set(ARTIFACT_FILES):
        raise QualificationError("unexpected-artifact-inventory")
    digests = {name: sha256_bytes(content) for name, content in raw.items()}
    documents = {
        name: decode_json(content)
        for name, content in raw.items()
        if name.endswith(".json")
    }
    if any(not isinstance(document, dict) for document in documents.values()):
        raise QualificationError("invalid-json-root")

    pins, _ = validate_image_pins(documents["image-pins.json"], expected_commit, now, maximum_age_seconds)
    validate_override(documents["pinned-compose.override.json"], pins)
    _, direct_publication = validate_preflight(
        documents["pinned-manifest-evidence.json"], pins, now, maximum_age_seconds, proxy_mode,
        expected_commit, expected_run_id, expected_attempt,
    )
    hub_tls = documents["bolt-tls-evidence.json"]
    validate_tls(hub_tls, HUB_TLS_SCHEMA, "bolt-hub", now, maximum_age_seconds)
    if (
        direct_publication is not None
        and (
            hub_tls["published_hostname"] != direct_publication[0]
            or hub_tls["published_port"] != direct_publication[1]
        )
    ):
        raise QualificationError("direct-kestrel-topology-unverified")
    validate_tls(
        documents["identityserver-tls-evidence.json"], IDENTITY_TLS_SCHEMA, "identityserver", now,
        maximum_age_seconds,
    )
    expected_published_ports = {
        "bolt-hub": documents["bolt-tls-evidence.json"]["published_port"],
        "identityserver": documents["identityserver-tls-evidence.json"]["published_port"],
    }
    validate_provenance(
        documents["provenance-evidence.json"], pins, expected_commit, expected_run_id, expected_attempt, now,
        maximum_age_seconds,
    )

    runtime_summary: dict[str, list[str]] = {}
    runtime_times: dict[str, dt.datetime] = {}
    for name, services in STAGED_RUNTIME_INVENTORIES.items():
        runtime_times[name] = validate_runtime(
            documents[name], services, pins, "staged", now, maximum_age_seconds,
            expected_published_ports,
        )
        runtime_summary[name] = list(services)
    for name, services in ROTATION_RUNTIME_INVENTORIES.items():
        runtime_times[name] = validate_runtime(
            documents[name], services, pins, "staged", now, maximum_age_seconds,
            expected_published_ports,
        )
        runtime_summary[name] = list(services)
    runtime_times["runtime-evidence.json"] = validate_runtime(
        documents["runtime-evidence.json"], PHASE0_SERVICES, pins, "complete", now,
        maximum_age_seconds, expected_published_ports,
    )
    runtime_summary["runtime-evidence.json"] = list(PHASE0_SERVICES)
    if [runtime_times[name] for name in (*STAGED_RUNTIME_INVENTORIES, "runtime-evidence.json")] != sorted(
        runtime_times[name] for name in (*STAGED_RUNTIME_INVENTORIES, "runtime-evidence.json")
    ):
        raise QualificationError("runtime-stage-order-mismatch")

    common, prepare_times = validate_rotation_document(documents["rotation-prepare-evidence.json"], "prepared")
    _, activate_times = validate_rotation_document(documents["rotation-activate-evidence.json"], "activated", common)
    _, convergence_times = validate_rotation_document(
        documents["rotation-convergence-evidence.json"], "converged", common
    )
    _, finalized_times = validate_rotation_document(
        documents["rotation-finalized-evidence.json"], "finalized", common
    )
    for current in (activate_times, convergence_times, finalized_times):
        if current["expiry"] != prepare_times["expiry"]:
            raise QualificationError("rotation-expiry-mismatch")
    inventory_time = validate_generation_inventory(
        documents["rotation-generation-inventory.json"], common["target_generation_id"]
    )
    if (
        inventory_time > convergence_times["converged"] + dt.timedelta(seconds=MAX_CLOCK_SKEW_SECONDS)
        or convergence_times["converged"] - inventory_time > dt.timedelta(minutes=5)
    ):
        raise QualificationError("generation-inventory-convergence-time-mismatch")
    dual_convergence_time = validate_credential_convergence(
        documents["credential-convergence-dual-validation.json"], "dual-validation",
        common["target_generation_id"], common["previous_generation_id"], now, maximum_age_seconds,
    )
    validate_credential_convergence(
        documents["credential-convergence-finalized.json"], "finalized",
        common["target_generation_id"], common["previous_generation_id"], now, maximum_age_seconds,
    )

    synthetic_summary: dict[str, dict[str, Any]] = {}
    synthetic_intervals: dict[str, tuple[dt.datetime, dt.datetime]] = {}
    seen_synthetic_runs: set[str] = set()
    for name, stage in SYNTHETIC_FILES.items():
        run, started, completed = validate_synthetic(
            documents[name], stage, now, maximum_age_seconds, proxy_mode=proxy_mode
        )
        if run in seen_synthetic_runs:
            raise QualificationError("duplicate-synthetic-run-id")
        seen_synthetic_runs.add(run)
        synthetic_intervals[name] = (started, completed)
        synthetic_summary[name] = {"stage": stage, "run_id": run, "completed_at_utc": format_utc(completed)}
    observation_time = validate_observation(
        documents["observation-evidence.json"], synthetic_intervals["synthetics-canary.json"],
        now, maximum_age_seconds,
    )
    if observation_time > runtime_times["runtime-staged-batch-1.json"]:
        raise QualificationError("canary-observation-overlaps-promotion")
    stage_runtime_bindings = {
        "synthetics-canary.json": "runtime-staged-canary.json",
        "synthetics-batch-1.json": "runtime-staged-batch-1.json",
        "synthetics-batch-2.json": "runtime-staged-batch-2.json",
        "synthetics-batch-3.json": "runtime-staged-batch-3.json",
    }
    for synthetic_name, runtime_name in stage_runtime_bindings.items():
        if synthetic_intervals[synthetic_name][0] < runtime_times[runtime_name]:
            raise QualificationError("synthetic-precedes-runtime-stage")
    initial_names = [
        "synthetics-canary.json", "synthetics-batch-1.json", "synthetics-batch-2.json",
        "synthetics-batch-3.json",
    ]
    rotation_names = [
        "synthetics-rotation-canary.json", "synthetics-rotation-batch-1.json",
        "synthetics-rotation-batch-2.json", "synthetics-rotation-batch-3.json",
    ]
    for names in (initial_names, rotation_names):
        starts = [synthetic_intervals[name][0] for name in names]
        if starts != sorted(starts):
            raise QualificationError("synthetic-stage-order-mismatch")
    if synthetic_intervals[initial_names[-1]][1] > activate_times["activated"]:
        raise QualificationError("preactivation-synthetic-after-activation")
    if (
        synthetic_intervals[rotation_names[0]][0] < activate_times["activated"]
        or synthetic_intervals[rotation_names[-1]][1] > convergence_times["converged"]
    ):
        raise QualificationError("rotation-synthetic-outside-convergence-window")
    rotation_runtime_names = list(ROTATION_RUNTIME_INVENTORIES)
    rotation_runtime_sequence = [runtime_times[name] for name in rotation_runtime_names]
    if any(
        current >= following
        for current, following in zip(rotation_runtime_sequence, rotation_runtime_sequence[1:])
    ):
        raise QualificationError("rotation-runtime-stage-order-mismatch")
    if any(value < activate_times["activated"] for value in rotation_runtime_sequence):
        raise QualificationError("rotation-runtime-precedes-activation")
    rotation_runtime_synthetics = {
        "runtime-rotation-canary.json": "synthetics-rotation-canary.json",
        "runtime-rotation-batch-1.json": "synthetics-rotation-batch-1.json",
        "runtime-rotation-batch-2.json": "synthetics-rotation-batch-2.json",
        "runtime-rotation-batch-3.json": "synthetics-rotation-batch-3.json",
    }
    for runtime_name, synthetic_name in rotation_runtime_synthetics.items():
        if runtime_times[runtime_name] >= synthetic_intervals[synthetic_name][0]:
            raise QualificationError("rotation-runtime-after-synthetic")
    if runtime_times["runtime-rotation-batch-3.json"] >= dual_convergence_time:
        raise QualificationError("rotation-runtime-after-dual-convergence")
    if synthetic_intervals["synthetics-rotation-batch-3.json"][1] > dual_convergence_time:
        raise QualificationError("rotation-synthetic-after-dual-convergence")
    if dual_convergence_time > convergence_times["converged"] + dt.timedelta(
        seconds=MAX_CLOCK_SKEW_SECONDS
    ):
        raise QualificationError("dual-convergence-after-rotation-convergence")
    if synthetic_intervals["synthetics-finalized.json"][0] < finalized_times["finalized"]:
        raise QualificationError("finalized-synthetic-precedes-finalization")
    if runtime_times["runtime-evidence.json"] < synthetic_intervals["synthetics-finalized.json"][1]:
        raise QualificationError("final-runtime-precedes-finalized-synthetic")

    validate_runtime(
        documents["rollback-runtime-evidence.json"], PHASE0_SERVICES, pins, "complete", now,
        maximum_age_seconds, expected_published_ports,
    )
    rollback_started, rollback_completed = validate_candidate_restart(
        documents["rollback-drill-evidence.json"], expected_run_id, expected_attempt, expected_commit,
        common["target_generation_id"], project_name, digests, now, maximum_age_seconds,
    )
    if rollback_started < runtime_times["runtime-evidence.json"]:
        raise QualificationError("rollback-drill-precedes-final-runtime")
    rollback_synthetic = synthetic_intervals["rollback-synthetics-finalized.json"]
    if not (rollback_started <= rollback_synthetic[0] <= rollback_synthetic[1] <= rollback_completed):
        raise QualificationError("rollback-synthetic-outside-drill")
    rollback_runtime_time = parse_timestamp(
        documents["rollback-runtime-evidence.json"]["generated_at_utc"], "invalid-rollback-runtime-time"
    )
    if not rollback_started <= rollback_runtime_time <= rollback_completed:
        raise QualificationError("rollback-runtime-outside-drill")

    artifacts = {
        name: artifact_summary(paths[name], documents.get(name), digests[name])
        for name in ARTIFACT_FILES
    }
    return {
        "schema": QUALIFICATION_SCHEMA,
        "status": "passed",
        "generated_at_utc": format_utc(now),
        "run_id": expected_run_id,
        "run_attempt": expected_attempt,
        "source_commit": expected_commit,
        "proxy_mode": proxy_mode,
        "credential_generation_id": common["target_generation_id"],
        "artifacts": artifacts,
        "runtime_stages": runtime_summary,
        "synthetic_stages": synthetic_summary,
        "checks": {
            "artifact_security": True,
            "schema_and_status": True,
            "identity_and_digest_binding": True,
            "rotation_and_convergence": True,
            "canary_observation": True,
            "candidate_restart": True,
        },
        "errors": [],
    }


def publish_qualification_metadata(
    run_directory: Path, evidence_path: Path, commit: str, lkg_pointer: Path
) -> None:
    marker = run_directory / "security-qualified"
    commit_path = run_directory / "qualified-commit"
    for target in (marker, commit_path):
        if target.exists() or target.is_symlink():
            raise QualificationError("qualification-metadata-already-exists")
    validate_private_directory(lkg_pointer.parent)
    evidence = load_json(evidence_path)
    if evidence.get("schema") != QUALIFICATION_SCHEMA or evidence.get("status") != "passed":
        raise QualificationError("qualification-evidence-not-passed")
    atomic_write(commit_path, f"{commit}\n".encode("ascii"))
    atomic_write(marker, b"")
    atomic_write(lkg_pointer, f"{run_directory}\n".encode("utf-8"))


def parse_env_file(path: Path) -> dict[str, str]:
    raw = read_private_file(path, maximum_bytes=1024 * 1024)
    if raw.startswith(b"\xef\xbb\xbf") or b"\x00" in raw:
        raise QualificationError("invalid-env-file")
    try:
        lines = raw.decode("utf-8", errors="strict").splitlines()
    except UnicodeDecodeError as error:
        raise QualificationError("invalid-env-file") from error
    result: dict[str, str] = {}
    name_pattern = re.compile(r"[A-Za-z_][A-Za-z0-9_]*")
    for line in lines:
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        name, separator, value = line.partition("=")
        if not separator or not name_pattern.fullmatch(name) or name in result:
            raise QualificationError("invalid-env-file")
        result[name] = value
    return result


def validate_private_executable(path: Path, *, sealed: bool = False) -> None:
    validate_no_symlink_path(path)
    try:
        metadata = path.lstat()
    except OSError as error:
        raise QualificationError("missing-recovery-synthetic-hook") from error
    if (
        stat.S_ISLNK(metadata.st_mode)
        or not stat.S_ISREG(metadata.st_mode)
        or not allowed_owner(metadata, root_only=sealed)
        or metadata.st_nlink != 1
        or metadata.st_size <= 0
        or metadata.st_size > MAX_ARTIFACT_BYTES
    ):
        raise QualificationError("insecure-recovery-synthetic-hook")
    if os.name == "posix":
        mode = stat.S_IMODE(metadata.st_mode)
        if mode != (0o550 if sealed else 0o700):
            raise QualificationError("insecure-recovery-synthetic-hook")


Runner = Callable[[list[str], int], subprocess.CompletedProcess[str]]


def default_runner(command: list[str], timeout: int) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        command,
        stdin=subprocess.DEVNULL,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        check=False,
        timeout=timeout,
        close_fds=True,
        text=False,
    )


def temporary_private_file(directory: Path, prefix: str) -> Path:
    descriptor, name = tempfile.mkstemp(prefix=f".{prefix}.", suffix=".json", dir=directory)
    os.close(descriptor)
    path = Path(name)
    if os.name == "posix":
        os.chmod(path, 0o600)
    return path


def qualification_evidence_for_recovery(
    run_directory: Path, run_id: str, attempt: int
) -> dict[str, Any]:
    sealed = is_production_run_directory(run_directory)
    evidence = load_json(
        run_directory / "qualification-evidence.json",
        expected_mode=0o440 if sealed else 0o600,
        root_only=sealed,
    )
    required_keys = {
        "schema", "status", "generated_at_utc", "run_id", "run_attempt", "source_commit",
        "proxy_mode", "credential_generation_id", "artifacts", "runtime_stages", "synthetic_stages",
        "checks", "errors",
    }
    exact_object(evidence, required_keys, "invalid-qualification-evidence")
    if (
        evidence["schema"] != QUALIFICATION_SCHEMA
        or evidence["status"] != "passed"
        or evidence["run_id"] != run_id
        or evidence["run_attempt"] != attempt
        or require_proxy_mode(evidence["proxy_mode"]) != evidence["proxy_mode"]
        or evidence["errors"] != []
        or not isinstance(evidence["checks"], dict)
        or set(evidence["checks"]) != QUALIFICATION_CHECK_KEYS
        or any(value is not True for value in evidence["checks"].values())
    ):
        raise QualificationError("invalid-qualification-evidence")
    commit = require_string(evidence["source_commit"], COMMIT, "invalid-qualified-commit")
    commit_raw = read_private_file(
        run_directory / "qualified-commit",
        maximum_bytes=64,
        expected_mode=0o440 if sealed else 0o600,
        root_only=sealed,
    )
    if commit_raw != f"{commit}\n".encode("ascii"):
        raise QualificationError("qualified-commit-mismatch")
    marker = validate_private_file(
        run_directory / "security-qualified",
        maximum_bytes=0,
        expected_mode=0o440 if sealed else 0o600,
        root_only=sealed,
    )
    if marker.st_size != 0:
        raise QualificationError("invalid-security-qualified-marker")
    artifacts = evidence["artifacts"]
    if not isinstance(artifacts, dict):
        raise QualificationError("invalid-qualification-artifacts")
    for name in RECOVERY_ARTIFACT_FILES:
        summary = artifacts.get(name)
        expected_mode = recovery_artifact_mode(name, sealed=sealed)
        if (
            not isinstance(summary, dict)
            or set(summary) != {"path", "sha256", "schema", "generated_at_utc"}
            or summary["path"] != name
            or summary["sha256"] != sha256_file(
                run_directory / name,
                expected_mode=expected_mode,
                root_only=sealed,
            )
        ):
            raise QualificationError("qualified-artifact-digest-mismatch")
    return evidence


def recovery_env_file(directory: Path, env: dict[str, str], run_directory: Path) -> Path:
    bound = dict(env)
    for key, name in RECOVERY_ENV_TOOL_BINDINGS.items():
        bound[key] = str(run_directory / name)
    payload = "".join(f"{key}={value}\n" for key, value in sorted(bound.items())).encode("utf-8")
    path = temporary_private_file(directory, "phase0-recovery-env")
    try:
        path.write_bytes(payload)
    except OSError:
        with contextlib.suppress(OSError):
            path.unlink()
        raise
    return path


def recovery_gate(
    env_file: Path,
    project_name: str,
    run_directory: Path,
    qualified_run_id: str,
    qualified_run_attempt: int,
    output: Path,
    freshness_seconds: int,
    timeout_seconds: int,
    *,
    runner: Runner = default_runner,
    now_provider: Callable[[], dt.datetime] = utc_now,
) -> dict[str, Any]:
    if not PROJECT_NAME.fullmatch(project_name):
        raise QualificationError("invalid-project-name")
    validate_run_identity(run_directory, qualified_run_id, qualified_run_attempt, "0" * 40)
    sealed = is_production_run_directory(run_directory)
    validate_private_directory(run_directory, sealed=sealed)
    qualification = qualification_evidence_for_recovery(run_directory, qualified_run_id, qualified_run_attempt)
    env = parse_env_file(env_file)
    hook_value = env.get("BOLT_PHASE0_RECOVERY_SYNTHETIC_COMMAND_PATH")
    if not hook_value:
        raise QualificationError("missing-recovery-synthetic-hook")
    if not Path(hook_value).is_absolute():
        raise QualificationError("invalid-recovery-synthetic-hook")
    proxy_mode = require_proxy_configuration(env)
    qualified_proxy_mode = require_proxy_mode(qualification["proxy_mode"])
    if proxy_mode != qualified_proxy_mode:
        raise QualificationError("qualified-proxy-mode-changed")
    for name in RECOVERY_EXECUTABLE_FILES:
        validate_private_executable(run_directory / name, sealed=sealed)
    hook = run_directory / "run-bolt-phase0-recovery-synthetic.py"
    runtime_verifier = run_directory / "verify-bolt-phase0-runtime.py"
    started = now_provider().astimezone(dt.timezone.utc)
    runtime_output = temporary_private_file(output.parent, "phase0-recovery-runtime")
    synthetic_output = temporary_private_file(output.parent, "phase0-recovery-synthetic")
    bound_env_file = recovery_env_file(output.parent, env, run_directory)
    protected = {
        name: sha256_file(
            run_directory / name,
            expected_mode=recovery_artifact_mode(name, sealed=sealed),
            root_only=sealed,
        )
        for name in RECOVERY_ARTIFACT_FILES
    }
    try:
        runtime_command = [
            sys.executable,
            str(runtime_verifier),
            "--compose-file", str(run_directory / "docker-compose.yml"),
            "--compose-file", str(run_directory / "pinned-compose.override.json"),
            "--env-file", str(bound_env_file),
            "--project-name", project_name,
            "--output", str(runtime_output),
            "--pins-file", str(run_directory / "image-pins.json"),
            "--services", *PHASE0_SERVICES,
        ]
        try:
            result = runner(runtime_command, timeout_seconds)
        except (OSError, subprocess.SubprocessError) as error:
            raise QualificationError("recovery-runtime-verifier-failed") from error
        if result.returncode != 0:
            raise QualificationError("recovery-runtime-verifier-failed")
        current = now_provider().astimezone(dt.timezone.utc)
        pin_document = load_json(
            run_directory / "image-pins.json",
            expected_mode=0o440 if sealed else 0o600,
            root_only=sealed,
        )
        pin_time = parse_timestamp(pin_document.get("generated_at_utc"), "invalid-image-pins")
        pins, _ = validate_image_pins(
            pin_document, qualification["source_commit"], pin_time, 60,
        )
        hub_tls = load_json(
            run_directory / "bolt-tls-evidence.json",
            expected_mode=0o440 if sealed else 0o600,
            root_only=sealed,
        )
        identity_tls = load_json(
            run_directory / "identityserver-tls-evidence.json",
            expected_mode=0o440 if sealed else 0o600,
            root_only=sealed,
        )
        expected_published_ports = {
            "bolt-hub": require_int(
                hub_tls.get("published_port"), "invalid-tls-port", minimum=1
            ),
            "identityserver": require_int(
                identity_tls.get("published_port"), "invalid-tls-port", minimum=1
            ),
        }
        validate_runtime(
            load_json(runtime_output), PHASE0_SERVICES, pins, "complete", current, freshness_seconds,
            expected_published_ports, not_before=started,
        )

        synthetic_command = [
            str(hook),
            "--env-file", str(bound_env_file),
            "--project-name", project_name,
            "--run-directory", str(run_directory),
            "--stage", "finalized",
            "--output", str(synthetic_output),
        ]
        try:
            result = runner(synthetic_command, timeout_seconds)
        except (OSError, subprocess.SubprocessError) as error:
            raise QualificationError("recovery-synthetic-hook-failed") from error
        if result.returncode != 0:
            raise QualificationError("recovery-synthetic-hook-failed")
        completed = now_provider().astimezone(dt.timezone.utc)
        validate_synthetic(
            load_json(synthetic_output), "finalized", completed, freshness_seconds,
            not_before=started, proxy_mode=qualified_proxy_mode,
        )
        if any(
            sha256_file(
                run_directory / name,
                expected_mode=recovery_artifact_mode(name, sealed=sealed),
                root_only=sealed,
            ) != digest
            for name, digest in protected.items()
        ):
            raise QualificationError("qualified-artifact-changed")
        return {
            "schema": RECOVERY_GATE_SCHEMA,
            "status": "passed",
            "qualified_run_id": qualified_run_id,
            "qualified_run_attempt": qualified_run_attempt,
            "project_name": project_name,
            "checks": {"authenticated_synthetic": True, "readiness": True},
        }
    finally:
        with contextlib.suppress(OSError):
            runtime_output.unlink()
        with contextlib.suppress(OSError):
            synthetic_output.unlink()
        with contextlib.suppress(OSError):
            bound_env_file.unlink()


def add_qualification_arguments(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--run-directory", required=True, type=Path)
    parser.add_argument("--expected-commit", required=True)
    parser.add_argument("--expected-run-id", required=True)
    parser.add_argument("--expected-run-attempt", required=True, type=int)
    parser.add_argument("--project-name", required=True)
    parser.add_argument("--proxy-mode", choices=sorted(PROXY_MODES), required=True)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--maximum-evidence-age-seconds", type=int, default=DEFAULT_MAXIMUM_AGE_SECONDS)


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    add_qualification_arguments(subparsers.add_parser("verify", help="write evidence without qualification metadata"))
    qualify = subparsers.add_parser("qualify", help="verify, write evidence, then atomically publish LKG metadata")
    add_qualification_arguments(qualify)
    qualify.add_argument("--lkg-pointer", required=True, type=Path)
    recovery = subparsers.add_parser("recovery-gate", help="prove post-restore readiness and fresh authentication")
    recovery.add_argument("--env-file", required=True, type=Path)
    recovery.add_argument("--project-name", required=True)
    recovery.add_argument("--run-directory", required=True, type=Path)
    recovery.add_argument("--qualified-run-id", required=True)
    recovery.add_argument("--qualified-run-attempt", required=True, type=int)
    recovery.add_argument("--output", required=True, type=Path)
    recovery.add_argument("--freshness-seconds", type=int, default=DEFAULT_RECOVERY_FRESHNESS_SECONDS)
    recovery.add_argument("--timeout-seconds", type=int, default=900)
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv)
    if args.command == "recovery-gate":
        try:
            if not 30 <= args.freshness_seconds <= 3600 or not 30 <= args.timeout_seconds <= 3600:
                raise QualificationError("invalid-recovery-policy")
            evidence = recovery_gate(
                args.env_file.absolute(), args.project_name, args.run_directory.absolute(),
                args.qualified_run_id, args.qualified_run_attempt, args.output.absolute(),
                args.freshness_seconds, args.timeout_seconds,
            )
            atomic_write_json(args.output.absolute(), evidence)
        except (QualificationError, OSError) as error:
            code = error.code if isinstance(error, QualificationError) else "recovery-gate-io-failed"
            print(f"ERROR: {code}", file=sys.stderr)
            return 1
        print(f"Bolt Phase 0 recovery gate passed; evidence: {args.output}")
        return 0

    run_directory = args.run_directory.absolute()
    output = (args.output or (run_directory / "qualification-evidence.json")).absolute()
    now = utc_now()
    try:
        if output.parent != run_directory or output.name != "qualification-evidence.json":
            raise QualificationError("invalid-qualification-output")
        evidence = verify_qualification(
            run_directory, args.expected_commit, args.expected_run_id, args.expected_run_attempt,
            args.project_name, args.maximum_evidence_age_seconds, proxy_mode=args.proxy_mode, now=now,
        )
        atomic_write_json(output, evidence)
        if args.command == "qualify":
            publish_qualification_metadata(run_directory, output, args.expected_commit, args.lkg_pointer.absolute())
    except (QualificationError, OSError) as error:
        code = error.code if isinstance(error, QualificationError) else "qualification-io-failed"
        evidence = qualification_failure(
            args.expected_run_id, args.expected_run_attempt, args.expected_commit,
            args.proxy_mode, now, code,
        )
        try:
            if output.parent == run_directory and output.name == "qualification-evidence.json":
                atomic_write_json(output, evidence)
        except (QualificationError, OSError):
            pass
        print(f"ERROR: {code}", file=sys.stderr)
        return 1
    print(f"Bolt Phase 0 qualification passed; evidence: {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
