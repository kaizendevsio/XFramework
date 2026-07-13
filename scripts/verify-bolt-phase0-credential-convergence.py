#!/usr/bin/env python3
"""Verify Phase 0 credential convergence from readiness metadata and private JWTs."""

from __future__ import annotations

import argparse
import base64
import binascii
import json
import math
import os
import re
import stat
import sys
import tempfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


INPUT_SCHEMA = "xframework.bolt.phase0.credential-convergence-input.v1"
OUTPUT_SCHEMA = "xframework.bolt.phase0.credential-convergence.v1"
PHASES = {"dual-validation", "finalized"}
TOKEN_KINDS = {"jwt", "service"}
GENERATION_ID = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")
INVENTORY_ID = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,255}$")
JWT_SEGMENT = re.compile(r"^[A-Za-z0-9_-]+$")
UTC_TIMESTAMP = re.compile(
    r"^(?P<date>\d{4}-\d{2}-\d{2})T(?P<time>\d{2}:\d{2}:\d{2})"
    r"(?P<fraction>\.\d{1,7})?Z$"
)
MAX_INPUT_BYTES = 32 * 1024 * 1024
MAX_TOKEN_BYTES = 16 * 1024
MAX_SERVICES = 256
MAX_CLIENTS = 2048
MAX_CHECKS_PER_SERVICE = 1024
MAX_TOKEN_FILES = 4096
MAX_ERRORS = 64
EPOCH = datetime(1970, 1, 1, tzinfo=timezone.utc)
ROOT_FIELDS = {
    "schema",
    "collected_at_utc",
    "target_generation_id",
    "retiring_generation_id",
    "phase",
    "identityserver_service",
    "expected_services",
    "expected_identityserver_clients",
    "services",
}
SERVICE_FIELDS = {"name", "http_status", "health"}
HEALTH_FIELDS = {"status", "duration", "timestamp", "checks"}
CHECK_FIELDS = {"name", "status", "description", "duration", "tags", "data", "exception"}
CREDENTIAL_DATA_FIELDS = {"jwt", "serviceCredential", "identityServerClients"}
BASE_DIAGNOSTIC_FIELDS = {
    "configured",
    "currentGenerationId",
    "validationFallbackConfigured",
}
FALLBACK_DIAGNOSTIC_FIELDS = {
    "validationFallbackGenerationId",
    "validationFallbackValidUntilUtc",
    "validationFallbackActive",
}
ENFORCE_POSIX_PERMISSIONS = os.name == "posix" and hasattr(os, "geteuid")


class DuplicateKeyError(ValueError):
    pass


class SafeValidationError(ValueError):
    def __init__(self, code: str):
        super().__init__(code)
        self.code = code


class Timestamp:
    __slots__ = ("text", "ticks")

    def __init__(self, text: str, ticks: int):
        self.text = text
        self.ticks = ticks


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--maximum-health-age-seconds", required=True, type=int)
    parser.add_argument(
        "--current-jwt",
        action="append",
        nargs=2,
        metavar=("KIND", "FILE"),
        default=[],
        help="private current token; KIND is jwt (kid/signing claim) or service (client claim)",
    )
    parser.add_argument(
        "--retired-jwt",
        action="append",
        nargs=2,
        metavar=("KIND", "FILE"),
        default=[],
        help="private retiring-generation token; KIND is jwt or service",
    )
    return parser.parse_args(argv)


def reject_constant(_: str) -> None:
    raise ValueError("non-finite JSON numbers are forbidden")


def unique_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise DuplicateKeyError("duplicate JSON object key")
        result[key] = value
    return result


def load_json_file(path: Path, maximum_bytes: int) -> Any:
    try:
        before = path.lstat()
        if not stat.S_ISREG(before.st_mode) or path.is_symlink():
            raise SafeValidationError("INPUT_INVALID")
        if before.st_size <= 0 or before.st_size > maximum_bytes:
            raise SafeValidationError("INPUT_INVALID")
        raw = path.read_bytes()
        after = path.lstat()
        if (before.st_dev, before.st_ino, before.st_size) != (
            after.st_dev,
            after.st_ino,
            after.st_size,
        ):
            raise SafeValidationError("INPUT_INVALID")
    except SafeValidationError:
        raise
    except OSError as error:
        raise SafeValidationError("INPUT_INVALID") from error
    if raw.startswith(b"\xef\xbb\xbf") or b"\x00" in raw:
        raise SafeValidationError("INPUT_INVALID")
    try:
        text = raw.decode("utf-8", errors="strict")
        return json.loads(text, object_pairs_hook=unique_object, parse_constant=reject_constant)
    except (UnicodeDecodeError, json.JSONDecodeError, DuplicateKeyError, ValueError, RecursionError) as error:
        raise SafeValidationError("INPUT_INVALID") from error


def require_object(value: Any, fields: set[str], code: str) -> dict[str, Any]:
    if not isinstance(value, dict) or set(value) != fields:
        raise SafeValidationError(code)
    return value


def require_string(value: Any, pattern: re.Pattern[str], code: str) -> str:
    if not isinstance(value, str) or not pattern.fullmatch(value):
        raise SafeValidationError(code)
    return value


def require_bool(value: Any, code: str) -> bool:
    if not isinstance(value, bool):
        raise SafeValidationError(code)
    return value


def require_nonnegative_number(value: Any, code: str) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise SafeValidationError(code)
    result = float(value)
    if not math.isfinite(result) or result < 0:
        raise SafeValidationError(code)
    return result


def parse_timestamp(value: Any, code: str) -> Timestamp:
    if not isinstance(value, str):
        raise SafeValidationError(code)
    match = UTC_TIMESTAMP.fullmatch(value)
    if not match:
        raise SafeValidationError(code)
    try:
        second = datetime.fromisoformat(f"{match.group('date')}T{match.group('time')}").replace(
            tzinfo=timezone.utc
        )
    except ValueError as error:
        raise SafeValidationError(code) from error
    fraction = (match.group("fraction") or "").removeprefix(".")
    fractional_ticks = int(fraction.ljust(7, "0")) if fraction else 0
    delta = second - EPOCH
    return Timestamp(value, (delta.days * 86_400 + delta.seconds) * 10_000_000 + fractional_ticks)


def validate_inventory(value: Any, maximum: int, code: str) -> list[str]:
    if not isinstance(value, list) or not 1 <= len(value) <= maximum:
        raise SafeValidationError(code)
    result = [require_string(item, INVENTORY_ID, code) for item in value]
    if len(result) != len(set(result)):
        raise SafeValidationError(code)
    return result


def validate_health_shape(health: Any) -> dict[str, Any]:
    result = require_object(health, HEALTH_FIELDS, "HEALTH_SCHEMA")
    if result["status"] != "Healthy":
        raise SafeValidationError("HEALTH_STATUS")
    require_nonnegative_number(result["duration"], "HEALTH_SCHEMA")
    parse_timestamp(result["timestamp"], "HEALTH_TIMESTAMP")
    checks = result["checks"]
    if not isinstance(checks, list) or not 1 <= len(checks) <= MAX_CHECKS_PER_SERVICE:
        raise SafeValidationError("HEALTH_SCHEMA")
    names: list[str] = []
    for raw_check in checks:
        check = require_object(raw_check, CHECK_FIELDS, "HEALTH_SCHEMA")
        names.append(require_string(check["name"], INVENTORY_ID, "HEALTH_SCHEMA"))
        if not isinstance(check["status"], str):
            raise SafeValidationError("HEALTH_SCHEMA")
        if check["description"] is not None and not isinstance(check["description"], str):
            raise SafeValidationError("HEALTH_SCHEMA")
        require_nonnegative_number(check["duration"], "HEALTH_SCHEMA")
        if (
            not isinstance(check["tags"], list)
            or not all(isinstance(tag, str) for tag in check["tags"])
            or len(check["tags"]) != len(set(check["tags"]))
            or not isinstance(check["data"], dict)
            or (check["exception"] is not None and not isinstance(check["exception"], str))
        ):
            raise SafeValidationError("HEALTH_SCHEMA")
    if len(names) != len(set(names)):
        raise SafeValidationError("HEALTH_SCHEMA")
    return result


def validate_diagnostic_shape(value: Any, phase: str) -> dict[str, Any]:
    fields = BASE_DIAGNOSTIC_FIELDS | (FALLBACK_DIAGNOSTIC_FIELDS if phase == "dual-validation" else set())
    return require_object(value, fields, "CREDENTIAL_DATA_SCHEMA")


def validate_diagnostic(
    value: Any,
    phase: str,
    target: str,
    retiring: str,
    collected: Timestamp,
    expiries: list[str],
) -> None:
    diagnostic = validate_diagnostic_shape(value, phase)
    if require_bool(diagnostic["configured"], "CREDENTIAL_DATA_SCHEMA") is not True:
        raise SafeValidationError("CURRENT_GENERATION")
    if diagnostic["currentGenerationId"] != target:
        raise SafeValidationError("CURRENT_GENERATION")
    fallback_configured = require_bool(
        diagnostic["validationFallbackConfigured"], "CREDENTIAL_DATA_SCHEMA"
    )
    if phase == "finalized":
        if fallback_configured:
            raise SafeValidationError("FALLBACK_RESIDUE")
        return
    if not fallback_configured:
        raise SafeValidationError("FALLBACK_STATE")
    if diagnostic["validationFallbackGenerationId"] != retiring:
        raise SafeValidationError("FALLBACK_GENERATION")
    if require_bool(diagnostic["validationFallbackActive"], "CREDENTIAL_DATA_SCHEMA") is not True:
        raise SafeValidationError("FALLBACK_STATE")
    expiry = parse_timestamp(diagnostic["validationFallbackValidUntilUtc"], "FALLBACK_EXPIRY")
    if expiry.ticks <= collected.ticks:
        raise SafeValidationError("FALLBACK_EXPIRY")
    expiries.append(expiry.text)


def validate_credential_check(
    health: dict[str, Any],
    phase: str,
    target: str,
    retiring: str,
    collected: Timestamp,
    identityserver: bool,
    expected_clients: set[str],
    expiries: list[str],
) -> None:
    matches = [check for check in health["checks"] if check["name"] == "credential-generations"]
    if len(matches) != 1:
        raise SafeValidationError("CREDENTIAL_CHECK_COVERAGE")
    check = matches[0]
    if (
        check["status"] != "Healthy"
        or check["exception"] is not None
        or not {"ready", "security", "credentials"}.issubset(set(check["tags"]))
    ):
        raise SafeValidationError("CREDENTIAL_CHECK_STATUS")
    data = require_object(check["data"], CREDENTIAL_DATA_FIELDS, "CREDENTIAL_DATA_SCHEMA")
    validate_diagnostic(data["jwt"], phase, target, retiring, collected, expiries)
    validate_diagnostic(data["serviceCredential"], phase, target, retiring, collected, expiries)
    clients = data["identityServerClients"]
    if not isinstance(clients, dict) or not all(
        isinstance(key, str) and INVENTORY_ID.fullmatch(key) for key in clients
    ):
        raise SafeValidationError("CREDENTIAL_DATA_SCHEMA")
    if identityserver:
        if set(clients) != expected_clients:
            raise SafeValidationError("CLIENT_COVERAGE")
        for diagnostic in clients.values():
            validate_diagnostic(diagnostic, phase, target, retiring, collected, expiries)
    elif clients:
        raise SafeValidationError("CLIENT_COVERAGE")


def decode_jwt_segment(segment: str) -> Any:
    if not JWT_SEGMENT.fullmatch(segment):
        raise SafeValidationError("TOKEN_INVALID")
    padding = "=" * (-len(segment) % 4)
    try:
        decoded = base64.b64decode(segment + padding, altchars=b"-_", validate=True)
    except (binascii.Error, ValueError) as error:
        raise SafeValidationError("TOKEN_INVALID") from error
    if base64.urlsafe_b64encode(decoded).rstrip(b"=").decode("ascii") != segment:
        raise SafeValidationError("TOKEN_INVALID")
    try:
        return json.loads(
            decoded.decode("utf-8", errors="strict"),
            object_pairs_hook=unique_object,
            parse_constant=reject_constant,
        )
    except (UnicodeDecodeError, json.JSONDecodeError, DuplicateKeyError, ValueError, RecursionError) as error:
        raise SafeValidationError("TOKEN_INVALID") from error


def read_private_token(path_text: str) -> str:
    path = Path(path_text)
    if not path.is_absolute():
        raise SafeValidationError("TOKEN_INVALID")
    try:
        before = path.lstat()
        if not stat.S_ISREG(before.st_mode) or path.is_symlink():
            raise SafeValidationError("TOKEN_INVALID")
        if ENFORCE_POSIX_PERMISSIONS:
            if (
                before.st_uid != os.geteuid()
                or before.st_mode & (stat.S_IRWXG | stat.S_IRWXO | stat.S_IXUSR)
                or not before.st_mode & stat.S_IRUSR
            ):
                raise SafeValidationError("TOKEN_INVALID")
        if before.st_size <= 0 or before.st_size > MAX_TOKEN_BYTES:
            raise SafeValidationError("TOKEN_INVALID")
        flags = os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)
        descriptor = os.open(path, flags)
        try:
            current = os.fstat(descriptor)
            if (before.st_dev, before.st_ino, before.st_size) != (
                current.st_dev,
                current.st_ino,
                current.st_size,
            ):
                raise SafeValidationError("TOKEN_INVALID")
            raw = os.read(descriptor, MAX_TOKEN_BYTES + 1)
            after = os.fstat(descriptor)
            if (
                len(raw) != current.st_size
                or (current.st_dev, current.st_ino, current.st_size, current.st_mtime_ns)
                != (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns)
            ):
                raise SafeValidationError("TOKEN_INVALID")
        finally:
            os.close(descriptor)
    except SafeValidationError:
        raise
    except OSError as error:
        raise SafeValidationError("TOKEN_INVALID") from error
    try:
        token = raw.decode("ascii", errors="strict")
    except UnicodeDecodeError as error:
        raise SafeValidationError("TOKEN_INVALID") from error
    if token != token.strip() or any(character.isspace() for character in token):
        raise SafeValidationError("TOKEN_INVALID")
    return token


def validate_token(kind: str, path: str, expected_generation: str) -> None:
    if kind not in TOKEN_KINDS:
        raise SafeValidationError("TOKEN_INVALID")
    token = read_private_token(path)
    parts = token.split(".")
    if len(parts) != 3 or not parts[2] or not JWT_SEGMENT.fullmatch(parts[2]):
        raise SafeValidationError("TOKEN_INVALID")
    header = decode_jwt_segment(parts[0])
    claims = decode_jwt_segment(parts[1])
    if not isinstance(header, dict) or not isinstance(claims, dict):
        raise SafeValidationError("TOKEN_INVALID")
    kid = header.get("kid")
    if not isinstance(kid, str) or not INVENTORY_ID.fullmatch(kid):
        raise SafeValidationError("TOKEN_INVALID")
    signing_generation = claims.get("credential_generation")
    client_generation = claims.get("client_credential_generation")
    if kind == "jwt":
        if kid != expected_generation or signing_generation != expected_generation:
            raise SafeValidationError("TOKEN_GENERATION")
        if client_generation is not None and client_generation != expected_generation:
            raise SafeValidationError("TOKEN_GENERATION")
    else:
        if client_generation != expected_generation:
            raise SafeValidationError("TOKEN_GENERATION")
        if signing_generation is not None and signing_generation != expected_generation:
            raise SafeValidationError("TOKEN_GENERATION")


def validate_token_inputs(
    current: list[list[str]],
    retired: list[list[str]],
    target: str,
    retiring: str,
    phase: str,
) -> tuple[int, int]:
    if len(current) + len(retired) > MAX_TOKEN_FILES:
        raise SafeValidationError("TOKEN_COVERAGE")
    references: list[str] = []
    for pair in [*current, *retired]:
        if not isinstance(pair, list) or len(pair) != 2:
            raise SafeValidationError("TOKEN_COVERAGE")
        kind, path = pair
        if kind not in TOKEN_KINDS or not isinstance(path, str):
            raise SafeValidationError("TOKEN_COVERAGE")
        try:
            references.append(str(Path(path).resolve(strict=True)))
        except OSError as error:
            raise SafeValidationError("TOKEN_INVALID") from error
    if len(references) != len(set(references)):
        raise SafeValidationError("TOKEN_COVERAGE")
    for kind, path in current:
        try:
            validate_token(kind, path, target)
        except SafeValidationError as error:
            raise SafeValidationError(
                "CURRENT_TOKEN_GENERATION" if error.code == "TOKEN_GENERATION" else "CURRENT_TOKEN_INVALID"
            ) from error
    for kind, path in retired:
        try:
            validate_token(kind, path, retiring)
        except SafeValidationError as error:
            raise SafeValidationError(
                "RETIRED_TOKEN_GENERATION" if error.code == "TOKEN_GENERATION" else "RETIRED_TOKEN_INVALID"
            ) from error
    if sorted(pair[0] for pair in current) != sorted(TOKEN_KINDS):
        raise SafeValidationError("TOKEN_COVERAGE")
    expected_retired_kinds = sorted(TOKEN_KINDS) if phase == "dual-validation" else []
    if sorted(pair[0] for pair in retired) != expected_retired_kinds:
        raise SafeValidationError("TOKEN_COVERAGE")
    return len(current), len(retired)


def validate_document(
    document: Any,
    maximum_health_age_seconds: int,
) -> tuple[dict[str, Any], list[str]]:
    if not 1 <= maximum_health_age_seconds <= 3600:
        raise SafeValidationError("POLICY_INVALID")
    root = require_object(document, ROOT_FIELDS, "INPUT_SCHEMA")
    if root["schema"] != INPUT_SCHEMA:
        raise SafeValidationError("INPUT_SCHEMA")
    phase = root["phase"]
    if phase not in PHASES:
        raise SafeValidationError("INPUT_SCHEMA")
    target = require_string(root["target_generation_id"], GENERATION_ID, "INPUT_SCHEMA")
    retiring = require_string(root["retiring_generation_id"], GENERATION_ID, "INPUT_SCHEMA")
    if target == retiring:
        raise SafeValidationError("INPUT_SCHEMA")
    collected = parse_timestamp(root["collected_at_utc"], "INPUT_SCHEMA")
    expected_services = validate_inventory(root["expected_services"], MAX_SERVICES, "SERVICE_COVERAGE")
    expected_clients = validate_inventory(
        root["expected_identityserver_clients"], MAX_CLIENTS, "CLIENT_COVERAGE"
    )
    identityserver = require_string(root["identityserver_service"], INVENTORY_ID, "INPUT_SCHEMA")
    if identityserver not in expected_services:
        raise SafeValidationError("IDENTITYSERVER_SERVICE")
    raw_services = root["services"]
    if not isinstance(raw_services, list) or not 1 <= len(raw_services) <= MAX_SERVICES:
        raise SafeValidationError("SERVICE_COVERAGE")
    services: dict[str, dict[str, Any]] = {}
    for raw_service in raw_services:
        service = require_object(raw_service, SERVICE_FIELDS, "HEALTH_SCHEMA")
        name = require_string(service["name"], INVENTORY_ID, "HEALTH_SCHEMA")
        if name in services:
            raise SafeValidationError("SERVICE_COVERAGE")
        if isinstance(service["http_status"], bool) or service["http_status"] != 200:
            raise SafeValidationError("HEALTH_STATUS")
        services[name] = validate_health_shape(service["health"])
    if set(services) != set(expected_services):
        raise SafeValidationError("SERVICE_COVERAGE")

    expiries: list[str] = []
    for name, health in services.items():
        health_time = parse_timestamp(health["timestamp"], "HEALTH_TIMESTAMP")
        age_ticks = collected.ticks - health_time.ticks
        if age_ticks < 0 or age_ticks > maximum_health_age_seconds * 10_000_000:
            raise SafeValidationError("HEALTH_TIMESTAMP")
        validate_credential_check(
            health,
            phase,
            target,
            retiring,
            collected,
            name == identityserver,
            set(expected_clients),
            expiries,
        )
    fallback_expiry: str | None = None
    if phase == "dual-validation":
        if not expiries or len(set(expiries)) != 1:
            raise SafeValidationError("FALLBACK_EXPIRY")
        fallback_expiry = expiries[0]
    elif expiries:
        raise SafeValidationError("FALLBACK_RESIDUE")
    metadata = {
        "phase": phase,
        "target_generation_id": target,
        "retiring_generation_id": retiring,
        "observed_at_utc": collected.text,
        "fallback_valid_until_utc": fallback_expiry,
        "service_count": len(expected_services),
        "identityserver_client_count": len(expected_clients),
    }
    return metadata, []


def canonical_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%fZ")


def evidence_document(
    metadata: dict[str, Any] | None,
    status: str,
    errors: list[str],
    current_count: int = 0,
    retired_count: int = 0,
) -> dict[str, Any]:
    safe = metadata or {}
    return {
        "schema": OUTPUT_SCHEMA,
        "generated_at_utc": canonical_now(),
        "observed_at_utc": safe.get("observed_at_utc"),
        "fallback_valid_until_utc": safe.get("fallback_valid_until_utc"),
        "phase": safe.get("phase"),
        "target_generation_id": safe.get("target_generation_id"),
        "retiring_generation_id": safe.get("retiring_generation_id"),
        "service_count": safe.get("service_count", 0),
        "identityserver_client_count": safe.get("identityserver_client_count", 0),
        "current_token_count": current_count,
        "retired_token_count": retired_count,
        "status": status,
        "errors": errors[:MAX_ERRORS],
    }


def atomic_write_json(path: Path, document: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    payload = (json.dumps(document, indent=2, sort_keys=True, allow_nan=False) + "\n").encode("utf-8")
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=f".{path.name}.", suffix=".tmp", dir=path.parent
    )
    temporary = Path(temporary_name)
    try:
        os.chmod(temporary, stat.S_IRUSR | stat.S_IWUSR)
        with os.fdopen(descriptor, "wb") as stream:
            descriptor = -1
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
        os.chmod(path, stat.S_IRUSR | stat.S_IWUSR)
        try:
            directory_descriptor = os.open(path.parent, os.O_RDONLY)
        except OSError:
            directory_descriptor = None
        if directory_descriptor is not None:
            try:
                os.fsync(directory_descriptor)
            except OSError:
                pass
            finally:
                os.close(directory_descriptor)
    except Exception:
        if descriptor >= 0:
            try:
                os.close(descriptor)
            except OSError:
                pass
        temporary.unlink(missing_ok=True)
        raise


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    output = Path(args.output)
    metadata: dict[str, Any] | None = None
    current_count = 0
    retired_count = 0
    try:
        document = load_json_file(Path(args.input), MAX_INPUT_BYTES)
        metadata, _ = validate_document(document, args.maximum_health_age_seconds)
        current_count, retired_count = validate_token_inputs(
            args.current_jwt,
            args.retired_jwt,
            metadata["target_generation_id"],
            metadata["retiring_generation_id"],
            metadata["phase"],
        )
        evidence = evidence_document(
            metadata, "passed", [], current_count=current_count, retired_count=retired_count
        )
    except SafeValidationError as error:
        evidence = evidence_document(
            metadata,
            "failed",
            [error.code],
            current_count=current_count,
            retired_count=retired_count,
        )
    except (OSError, ValueError, json.JSONDecodeError, RecursionError):
        evidence = evidence_document(metadata, "failed", ["INPUT_INVALID"])

    try:
        atomic_write_json(output, evidence)
    except OSError:
        print("ERROR: credential convergence evidence could not be written atomically", file=sys.stderr)
        return 1
    if evidence["status"] != "passed":
        for code in evidence["errors"]:
            print(f"ERROR: {code}", file=sys.stderr)
        print("Bolt Phase 0 credential convergence verification failed", file=sys.stderr)
        return 1
    print(
        "Bolt Phase 0 credential convergence verification passed "
        f"for {evidence['service_count']} services"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
