#!/usr/bin/env python3
"""Safely manage the bounded dual-generation Bolt Phase 0 credential rotation."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import secrets
import stat
import sys
import tempfile
from contextlib import contextmanager
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any, Iterator


STATE_SCHEMA = "xframework.bolt.phase0.rotation-state.v1"
BOOTSTRAP_SCHEMA = "xframework.bolt.phase0.rotation-bootstrap.v1"
BOOTSTRAP_VALIDATION_SCHEMA = "xframework.bolt.phase0.rotation-bootstrap-validation.v1"
INVENTORY_SCHEMA = "xframework.bolt.phase0.credential-generation-inventory.v1"
CONVERGENCE_SCHEMA = "xframework.bolt.phase0.credential-convergence.v1"
MIN_VALID_FOR_SECONDS = 300
MAX_VALID_FOR_SECONDS = 86_400
DEFAULT_VALID_FOR_SECONDS = 3_600
MIN_ACTIVATION_REMAINING_SECONDS = MIN_VALID_FOR_SECONDS

NAME = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*$")
GENERATION_ID = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$")
SAFE_SECRET = re.compile(r"^[A-Za-z0-9_~.+/=-]{32,512}$")

SERVICE_SECRET_PREFIXES = (
    "IDENTITYSERVER",
    "BOLT_HUB",
    "COMMUNICATIONS",
    "NOTIFICATIONS",
    "STORAGE",
    "ATTENDANCE",
    "SMSGATEWAY",
    "WALLETS",
    "INVENTARIO",
    "POS",
    "PORTAL",
    "OPERATIONS_DASHBOARD",
)
REQUIRED_SERVICES = (
    "identityserver",
    "bolt-hub",
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

SECRET_PAIRS = (
    ("JWT_SECRET", "JWT_SECONDARY_SECRET"),
    ("BOLT_SIGNATURE", "BOLT_SIGNATURE_SECONDARY"),
    *tuple(
        (
            f"{prefix}_SERVICE_IDENTITY_SECRET",
            f"{prefix}_SERVICE_IDENTITY_SECRET_SECONDARY",
        )
        for prefix in SERVICE_SECRET_PREFIXES
    ),
)
PRIMARY_SECRET_NAMES = tuple(primary for primary, _ in SECRET_PAIRS)
SECONDARY_SECRET_NAMES = tuple(secondary for _, secondary in SECRET_PAIRS)
SECONDARY_STATE_NAMES = (
    "CREDENTIAL_SECONDARY_GENERATION_ID",
    "CREDENTIAL_SECONDARY_VALID_UNTIL_UTC",
    *SECONDARY_SECRET_NAMES,
)

STATE_KEYS = {
    "schema",
    "rotation_id",
    "phase",
    "previous_generation_id",
    "target_generation_id",
    "secondary_valid_until_utc",
    "prepared_at_utc",
    "activated_at_utc",
    "convergence",
    "finalized_at_utc",
}
CONVERGENCE_KEYS = {
    "schema",
    "verified_at_utc",
    "target_generation_id",
    "services",
    "inventory_sha256",
}


class RotationError(ValueError):
    """A fail-closed validation or state transition error."""


@dataclass(frozen=True)
class EnvLine:
    body: str
    ending: str
    name: str | None = None


@dataclass(frozen=True)
class EnvDocument:
    lines: tuple[EnvLine, ...]
    values: dict[str, str]
    newline: str
    has_bom: bool

    def render(self, updates: dict[str, str], removals: set[str] | None = None) -> bytes:
        removals = removals or set()
        pending = dict(updates)
        rendered: list[str] = []

        for line in self.lines:
            if line.name in removals:
                continue
            if line.name in pending:
                rendered.append(f"{line.name}={pending.pop(line.name)}{line.ending}")
            else:
                rendered.append(f"{line.body}{line.ending}")

        if pending:
            if rendered and not rendered[-1].endswith(("\n", "\r")):
                rendered[-1] += self.newline
            rendered.extend(f"{name}={value}{self.newline}" for name, value in pending.items())

        text = "".join(rendered)
        if self.has_bom:
            text = "\ufeff" + text
        return text.encode("utf-8")


def _read_regular_bytes(path: Path, description: str) -> bytes:
    try:
        metadata = path.lstat()
    except FileNotFoundError as error:
        raise RotationError(f"{description} does not exist: {path}") from error
    if stat.S_ISLNK(metadata.st_mode):
        raise RotationError(f"{description} must be a regular, non-symlink file: {path}")

    flags = os.O_RDONLY
    if hasattr(os, "O_BINARY"):
        flags |= os.O_BINARY
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    try:
        descriptor = os.open(path, flags)
    except FileNotFoundError as error:
        raise RotationError(f"{description} does not exist: {path}") from error
    except OSError as error:
        raise RotationError(f"could not safely open {description}: {path}") from error
    try:
        if not stat.S_ISREG(os.fstat(descriptor).st_mode):
            raise RotationError(f"{description} must be a regular, non-symlink file: {path}")
        with os.fdopen(descriptor, "rb", closefd=True) as stream:
            descriptor = -1
            return stream.read()
    finally:
        if descriptor >= 0:
            os.close(descriptor)


def parse_env(path: Path) -> EnvDocument:
    raw = _read_regular_bytes(path, "env file")
    has_bom = raw.startswith(b"\xef\xbb\xbf")
    try:
        text = raw.decode("utf-8-sig")
    except UnicodeDecodeError as error:
        raise RotationError("env file must be valid UTF-8") from error
    if "\x00" in text or "\ufeff" in text:
        raise RotationError("env file contains a NUL or embedded byte-order mark")
    if any(separator in text for separator in ("\v", "\f", "\x1c", "\x1d", "\x1e", "\x85", "\u2028", "\u2029")):
        raise RotationError("env file contains an unsupported line separator")

    values: dict[str, str] = {}
    lines: list[EnvLine] = []
    newline = "\n"
    for line_number, raw_line in enumerate(text.splitlines(keepends=True), start=1):
        if raw_line.endswith("\r\n"):
            body, ending = raw_line[:-2], "\r\n"
        elif raw_line.endswith("\n"):
            body, ending = raw_line[:-1], "\n"
        elif raw_line.endswith("\r"):
            raise RotationError(f"line {line_number}: bare carriage returns are not allowed")
        else:
            body, ending = raw_line, ""
        if ending and not lines:
            newline = ending
        if any(ord(character) < 32 for character in body):
            raise RotationError(f"line {line_number}: control characters are not allowed")
        if not body.strip() or body.lstrip().startswith("#"):
            lines.append(EnvLine(body, ending))
            continue

        name, separator, value = body.partition("=")
        if not separator or not NAME.fullmatch(name):
            raise RotationError(f"line {line_number}: expected unambiguous NAME=value syntax")
        if name in values:
            raise RotationError(f"line {line_number}: duplicate variable {name}")
        values[name] = value
        lines.append(EnvLine(body, ending, name))

    if text and not lines:
        raise RotationError("env file has unsupported line separators")
    return EnvDocument(tuple(lines), values, newline, has_bom)


def _reject_duplicate_json_keys(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise RotationError(f"duplicate JSON key: {key}")
        result[key] = value
    return result


def _reject_json_constant(value: str) -> None:
    raise RotationError(f"unsupported JSON constant: {value}")


def _parse_json_file(path: Path, description: str) -> Any:
    raw = _read_regular_bytes(path, description)
    try:
        return json.loads(
            raw.decode("utf-8-sig"),
            object_pairs_hook=_reject_duplicate_json_keys,
            parse_constant=_reject_json_constant,
        )
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise RotationError(f"{description} must be valid UTF-8 JSON") from error


def _atomic_write(path: Path, payload: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if os.path.lexists(path) and path.is_symlink():
        raise RotationError(f"refusing to replace symlink: {path}")

    descriptor, temporary_name = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
    temporary_path = Path(temporary_name)
    try:
        os.fchmod(descriptor, 0o600)
        with os.fdopen(descriptor, "wb", closefd=True) as stream:
            descriptor = -1
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary_path, path)
        os.chmod(path, 0o600)
        if os.name != "nt":
            directory_descriptor = os.open(path.parent, os.O_RDONLY)
            try:
                os.fsync(directory_descriptor)
            finally:
                os.close(directory_descriptor)
    finally:
        if descriptor >= 0:
            os.close(descriptor)
        try:
            temporary_path.unlink()
        except FileNotFoundError:
            pass


def _write_json(path: Path, document: dict[str, Any]) -> None:
    payload = (json.dumps(document, indent=2, sort_keys=True) + "\n").encode("utf-8")
    _atomic_write(path, payload)


@contextmanager
def rotation_lock(env_path: Path) -> Iterator[None]:
    lock_path = env_path.with_name(f".{env_path.name}.phase0-rotation.lock")
    lock_path.parent.mkdir(parents=True, exist_ok=True)
    flags = os.O_RDWR | os.O_CREAT
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    try:
        descriptor = os.open(lock_path, flags, 0o600)
    except OSError as error:
        raise RotationError(f"could not open rotation lock: {lock_path}") from error

    try:
        os.fchmod(descriptor, 0o600)
        if os.name == "nt":
            import msvcrt

            if os.fstat(descriptor).st_size == 0:
                os.write(descriptor, b"\0")
                os.fsync(descriptor)
            os.lseek(descriptor, 0, os.SEEK_SET)
            msvcrt.locking(descriptor, msvcrt.LK_LOCK, 1)
        else:
            import fcntl

            fcntl.flock(descriptor, fcntl.LOCK_EX)
        yield
    finally:
        if os.name == "nt":
            import msvcrt

            os.lseek(descriptor, 0, os.SEEK_SET)
            msvcrt.locking(descriptor, msvcrt.LK_UNLCK, 1)
        else:
            import fcntl

            fcntl.flock(descriptor, fcntl.LOCK_UN)
        os.close(descriptor)


def utc_now() -> datetime:
    return datetime.now(timezone.utc).replace(microsecond=0)


def format_utc(value: datetime) -> str:
    if value.tzinfo is None or value.utcoffset() != timedelta(0):
        raise RotationError("timestamp must be UTC")
    return value.replace(microsecond=0).strftime("%Y-%m-%dT%H:%M:%SZ")


def parse_utc(value: Any, field: str) -> datetime:
    if not isinstance(value, str) or not re.fullmatch(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z", value):
        raise RotationError(f"{field} must be a canonical UTC timestamp")
    try:
        parsed = datetime.strptime(value, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=timezone.utc)
    except ValueError as error:
        raise RotationError(f"{field} must be a valid UTC timestamp") from error
    return parsed


def _validate_generation_id(value: Any, field: str) -> str:
    if not isinstance(value, str) or not GENERATION_ID.fullmatch(value):
        raise RotationError(f"{field} is missing or invalid")
    return value


def _validate_secret(value: Any, name: str) -> str:
    if not isinstance(value, str) or not SAFE_SECRET.fullmatch(value):
        raise RotationError(f"{name} must be an unquoted, nonempty, safe high-entropy token")
    return value


def _validate_primary_secrets(values: dict[str, str]) -> None:
    for name in PRIMARY_SECRET_NAMES:
        _validate_secret(values.get(name), name)


def _validate_primary_values(values: dict[str, str]) -> None:
    _validate_generation_id(values.get("CREDENTIAL_GENERATION_ID"), "CREDENTIAL_GENERATION_ID")
    _validate_primary_secrets(values)


def _secondary_presence(values: dict[str, str]) -> set[str]:
    return set(SECONDARY_STATE_NAMES).intersection(values)


def _assert_no_secondary(values: dict[str, str]) -> None:
    present = _secondary_presence(values)
    if present:
        raise RotationError(f"untracked or partial secondary state is present: {', '.join(sorted(present))}")


def _new_generation_id(now: datetime) -> str:
    return f"gen-{now.strftime('%Y%m%dT%H%M%SZ')}-{secrets.token_hex(12)}"


def _new_secret_values(current_values: dict[str, str]) -> dict[str, str]:
    forbidden = {current_values[name] for name in PRIMARY_SECRET_NAMES}
    generated: dict[str, str] = {}
    for _, secondary in SECRET_PAIRS:
        while True:
            candidate = secrets.token_urlsafe(48)
            if candidate not in forbidden:
                forbidden.add(candidate)
                generated[secondary] = candidate
                break
    return generated


def _validate_convergence(convergence: Any, target_generation_id: str) -> dict[str, Any]:
    if not isinstance(convergence, dict) or set(convergence) != CONVERGENCE_KEYS:
        raise RotationError("convergence record is missing fields or contains unsupported fields")
    if convergence["schema"] != CONVERGENCE_SCHEMA:
        raise RotationError("convergence record schema is invalid")
    parse_utc(convergence["verified_at_utc"], "convergence.verified_at_utc")
    if convergence["target_generation_id"] != target_generation_id:
        raise RotationError("convergence target does not match the rotation target")
    services = convergence["services"]
    if not isinstance(services, dict) or set(services) != set(REQUIRED_SERVICES):
        raise RotationError("convergence record does not contain exactly the required services")
    if any(value != target_generation_id for value in services.values()):
        raise RotationError("convergence record contains a non-target generation")
    expected_digest = hashlib.sha256(_canonical_inventory(services)).hexdigest()
    if not secrets.compare_digest(str(convergence["inventory_sha256"]), expected_digest):
        raise RotationError("convergence inventory digest is invalid")
    return convergence


def _validate_state(document: Any) -> dict[str, Any]:
    if not isinstance(document, dict) or set(document) != STATE_KEYS:
        raise RotationError("rotation state is missing fields or contains unsupported fields")
    if document["schema"] != STATE_SCHEMA:
        raise RotationError("rotation state schema is invalid")
    _validate_generation_id(document["rotation_id"], "rotation_id")
    previous = _validate_generation_id(document["previous_generation_id"], "previous_generation_id")
    target = _validate_generation_id(document["target_generation_id"], "target_generation_id")
    if previous == target:
        raise RotationError("previous and target generation IDs must differ")
    expiry = parse_utc(document["secondary_valid_until_utc"], "secondary_valid_until_utc")
    phase = document["phase"]
    if phase not in {"preparing", "prepared", "activated", "converged", "finalized"}:
        raise RotationError("rotation phase is invalid")

    timestamp_requirements = {
        "prepared_at_utc": phase != "preparing",
        "activated_at_utc": phase in {"activated", "converged", "finalized"},
        "finalized_at_utc": phase == "finalized",
    }
    timestamps: dict[str, datetime | None] = {}
    for field, required in timestamp_requirements.items():
        value = document[field]
        if required:
            timestamps[field] = parse_utc(value, field)
        elif value is not None:
            timestamps[field] = parse_utc(value, field)
        else:
            timestamps[field] = None

    prepared_at = timestamps["prepared_at_utc"]
    activated_at = timestamps["activated_at_utc"]
    finalized_at = timestamps["finalized_at_utc"]
    if prepared_at is not None:
        window_seconds = int((expiry - prepared_at).total_seconds())
        if not MIN_VALID_FOR_SECONDS <= window_seconds <= MAX_VALID_FOR_SECONDS:
            raise RotationError("rotation state contains an out-of-bounds validity window")
    if activated_at is not None and (
        prepared_at is None or activated_at < prepared_at or activated_at >= expiry
    ):
        raise RotationError("activation timestamp is outside the prepared validity window")

    if phase in {"converged", "finalized"}:
        convergence = _validate_convergence(document["convergence"], target)
        verified_at = parse_utc(convergence["verified_at_utc"], "convergence.verified_at_utc")
        if activated_at is None or verified_at < activated_at:
            raise RotationError("convergence timestamp precedes activation")
        if finalized_at is not None and (finalized_at < expiry or finalized_at < verified_at):
            raise RotationError("finalization timestamp precedes expiry or convergence")
    elif document["convergence"] is not None:
        raise RotationError("convergence evidence exists before convergence")
    return document


def _read_state(path: Path) -> dict[str, Any]:
    return _validate_state(_parse_json_file(path, "rotation state file"))


def _write_state(path: Path, state: dict[str, Any]) -> None:
    _validate_state(state)
    _write_json(path, state)


def _canonical_inventory(services: dict[str, str]) -> bytes:
    return json.dumps(services, separators=(",", ":"), sort_keys=True).encode("utf-8")


def _env_shape(document: EnvDocument, state: dict[str, Any]) -> str:
    values = document.values
    _validate_primary_values(values)
    present = _secondary_presence(values)
    if not present:
        if values["CREDENTIAL_GENERATION_ID"] == state["target_generation_id"]:
            return "finalized"
        if values["CREDENTIAL_GENERATION_ID"] == state["previous_generation_id"]:
            return "unprepared"
        raise RotationError("env current generation does not match rotation state")
    if present != set(SECONDARY_STATE_NAMES):
        missing = sorted(set(SECONDARY_STATE_NAMES) - present)
        raise RotationError(f"partial secondary state; missing: {', '.join(missing)}")

    secondary_generation = _validate_generation_id(
        values["CREDENTIAL_SECONDARY_GENERATION_ID"],
        "CREDENTIAL_SECONDARY_GENERATION_ID",
    )
    expiry = values["CREDENTIAL_SECONDARY_VALID_UNTIL_UTC"]
    parse_utc(expiry, "CREDENTIAL_SECONDARY_VALID_UNTIL_UTC")
    if expiry != state["secondary_valid_until_utc"]:
        raise RotationError("env secondary expiry does not match rotation state")
    for name in SECONDARY_SECRET_NAMES:
        _validate_secret(values[name], name)

    current_generation = values["CREDENTIAL_GENERATION_ID"]
    if (
        current_generation == state["previous_generation_id"]
        and secondary_generation == state["target_generation_id"]
    ):
        return "prepared"
    if (
        current_generation == state["target_generation_id"]
        and secondary_generation == state["previous_generation_id"]
    ):
        return "active"
    raise RotationError("env generation arrangement does not match a valid rotation phase")


def _complete_preparation(
    env_path: Path,
    state_path: Path,
    document: EnvDocument,
    state: dict[str, Any],
    now: datetime,
) -> tuple[EnvDocument, dict[str, Any]]:
    expiry = parse_utc(state["secondary_valid_until_utc"], "secondary_valid_until_utc")
    remaining_seconds = int((expiry - now).total_seconds())
    if state["phase"] == "preparing" and not (
        MIN_VALID_FOR_SECONDS <= remaining_seconds <= MAX_VALID_FOR_SECONDS
    ):
        raise RotationError("too little bounded validity remains to complete preparation")
    shape = _env_shape(document, state)
    if shape == "unprepared":
        updates = {
            "CREDENTIAL_SECONDARY_GENERATION_ID": state["target_generation_id"],
            "CREDENTIAL_SECONDARY_VALID_UNTIL_UTC": state["secondary_valid_until_utc"],
            **_new_secret_values(document.values),
        }
        _atomic_write(env_path, document.render(updates))
        document = parse_env(env_path)
        shape = _env_shape(document, state)
    if shape != "prepared":
        raise RotationError("prepare cannot continue from the current env arrangement")
    if state["phase"] == "preparing":
        state = {**state, "phase": "prepared", "prepared_at_utc": format_utc(now)}
        _write_state(state_path, state)
    return document, state


def bootstrap(
    env_path: Path,
    state_path: Path,
    now: datetime | None = None,
) -> dict[str, Any]:
    """Add only a missing nonsecret generation marker before rotation staging."""
    now = now or utc_now()
    with rotation_lock(env_path):
        document = parse_env(env_path)
        if os.path.lexists(state_path):
            raise RotationError("bootstrap requires rotation state to be absent")

        _validate_primary_secrets(document.values)
        _assert_no_secondary(document.values)
        current_generation = document.values.get("CREDENTIAL_GENERATION_ID")
        if current_generation is None:
            current_generation = (
                f"legacy-{now.strftime('%Y%m%dT%H%M%SZ')}-{secrets.token_hex(4)}"
            )
            _atomic_write(
                env_path,
                document.render({"CREDENTIAL_GENERATION_ID": current_generation}),
            )
            document = parse_env(env_path)

        _validate_primary_values(document.values)
        _assert_no_secondary(document.values)
        return {
            "schema": BOOTSTRAP_SCHEMA,
            "current_generation_id": current_generation,
        }


def validate_bootstrap_inputs(env_path: Path, state_path: Path) -> dict[str, Any]:
    """Validate bootstrap prerequisites without creating a lock or changing either path."""
    document = parse_env(env_path)
    if os.path.lexists(state_path):
        raise RotationError("bootstrap validation requires rotation state to be absent")
    _validate_primary_secrets(document.values)
    _assert_no_secondary(document.values)
    current_generation = document.values.get("CREDENTIAL_GENERATION_ID")
    if current_generation is not None:
        _validate_generation_id(current_generation, "CREDENTIAL_GENERATION_ID")
    return {
        "schema": BOOTSTRAP_VALIDATION_SCHEMA,
        "generation_marker_present": current_generation is not None,
        "mutation_required": current_generation is None,
    }


def prepare(
    env_path: Path,
    state_path: Path,
    valid_for_seconds: int,
    now: datetime | None = None,
) -> dict[str, Any]:
    if not MIN_VALID_FOR_SECONDS <= valid_for_seconds <= MAX_VALID_FOR_SECONDS:
        raise RotationError(
            f"validity must be between {MIN_VALID_FOR_SECONDS} and {MAX_VALID_FOR_SECONDS} seconds"
        )
    now = now or utc_now()
    with rotation_lock(env_path):
        document = parse_env(env_path)
        if os.path.lexists(state_path):
            state = _read_state(state_path)
            if state["phase"] == "preparing":
                document, state = _complete_preparation(env_path, state_path, document, state, now)
            else:
                _env_shape(document, state)
            return status_document(document, state)

        _validate_primary_values(document.values)
        _assert_no_secondary(document.values)
        previous_generation = document.values["CREDENTIAL_GENERATION_ID"]
        target_generation = _new_generation_id(now)
        while target_generation == previous_generation:
            target_generation = _new_generation_id(now)
        state = {
            "schema": STATE_SCHEMA,
            "rotation_id": f"rotation-{secrets.token_hex(16)}",
            "phase": "preparing",
            "previous_generation_id": previous_generation,
            "target_generation_id": target_generation,
            "secondary_valid_until_utc": format_utc(now + timedelta(seconds=valid_for_seconds)),
            "prepared_at_utc": None,
            "activated_at_utc": None,
            "convergence": None,
            "finalized_at_utc": None,
        }
        _write_state(state_path, state)
        document, state = _complete_preparation(env_path, state_path, document, state, now)
        return status_document(document, state)


def activate(
    env_path: Path,
    state_path: Path,
    minimum_remaining_seconds: int,
    now: datetime | None = None,
) -> dict[str, Any]:
    if not MIN_ACTIVATION_REMAINING_SECONDS <= minimum_remaining_seconds <= MAX_VALID_FOR_SECONDS:
        raise RotationError(
            "minimum remaining validity must be between "
            f"{MIN_ACTIVATION_REMAINING_SECONDS} and {MAX_VALID_FOR_SECONDS} seconds"
        )
    now = now or utc_now()
    with rotation_lock(env_path):
        state = _read_state(state_path)
        document = parse_env(env_path)
        if state["phase"] == "preparing":
            document, state = _complete_preparation(env_path, state_path, document, state, now)
        if state["phase"] == "finalized":
            if _env_shape(document, state) != "finalized":
                raise RotationError("finalized rotation state does not match the env file")
            return status_document(document, state)

        shape = _env_shape(document, state)
        if state["phase"] == "prepared":
            expiry = parse_utc(state["secondary_valid_until_utc"], "secondary_valid_until_utc")
            prepared_at = parse_utc(state["prepared_at_utc"], "prepared_at_utc")
            if now < prepared_at:
                raise RotationError("current UTC time precedes preparation; activation is blocked")
            if now >= expiry:
                raise RotationError("prepared credential window has expired; activation is blocked")
            if shape == "prepared":
                remaining_seconds = int((expiry - now).total_seconds())
                if remaining_seconds < minimum_remaining_seconds:
                    raise RotationError(
                        "prepared credential window has insufficient remaining validity for "
                        "token lifetime, rollout, observation, and rollback"
                    )
                updates: dict[str, str] = {
                    "CREDENTIAL_GENERATION_ID": state["target_generation_id"],
                    "CREDENTIAL_SECONDARY_GENERATION_ID": state["previous_generation_id"],
                }
                for primary, secondary in SECRET_PAIRS:
                    updates[primary] = document.values[secondary]
                    updates[secondary] = document.values[primary]
                _atomic_write(env_path, document.render(updates))
                document = parse_env(env_path)
                shape = _env_shape(document, state)
            if shape != "active":
                raise RotationError("activate requires a prepared credential arrangement")
            state = {**state, "phase": "activated", "activated_at_utc": format_utc(now)}
            _write_state(state_path, state)
        elif shape != "active":
            raise RotationError("activated rotation state does not match the env file")
        return status_document(document, state)


def parse_inventory(path: Path) -> dict[str, str]:
    document = _parse_json_file(path, "runtime generation inventory")
    if not isinstance(document, dict):
        raise RotationError("runtime generation inventory must be a JSON object")

    if "services" in document:
        allowed = {"schema", "generated_at_utc", "services"}
        if not set(document).issubset(allowed):
            raise RotationError("runtime generation inventory contains unsupported root fields")
        if "schema" in document and document["schema"] != INVENTORY_SCHEMA:
            raise RotationError("runtime generation inventory schema is invalid")
        if "generated_at_utc" in document:
            parse_utc(document["generated_at_utc"], "generated_at_utc")
        services_value = document["services"]
    else:
        services_value = document

    if isinstance(services_value, dict):
        services = {}
        for service, item in services_value.items():
            if isinstance(item, str):
                services[service] = item
            elif isinstance(item, dict) and set(item) == {"credential_generation_id"}:
                services[service] = item["credential_generation_id"]
            else:
                raise RotationError(
                    f"inventory service {service} must be a generation string or exact generation object"
                )
    elif isinstance(services_value, list):
        services = {}
        for index, item in enumerate(services_value):
            if not isinstance(item, dict) or set(item) != {"service", "credential_generation_id"}:
                raise RotationError(f"inventory service item {index} has invalid fields")
            service = item["service"]
            generation = item["credential_generation_id"]
            if not isinstance(service, str) or service in services:
                raise RotationError(f"inventory service item {index} has a duplicate or invalid name")
            services[service] = generation
    else:
        raise RotationError("runtime generation inventory services must be an object or array")

    if set(services) != set(REQUIRED_SERVICES):
        missing = sorted(set(REQUIRED_SERVICES) - set(services))
        extra = sorted(set(services) - set(REQUIRED_SERVICES))
        details = []
        if missing:
            details.append(f"missing: {', '.join(missing)}")
        if extra:
            details.append(f"unexpected: {', '.join(extra)}")
        raise RotationError(f"inventory must contain exactly the required services ({'; '.join(details)})")
    if any(not isinstance(value, str) for value in services.values()):
        raise RotationError("every inventory generation ID must be a string")
    return dict(services)


def verify_convergence_input(
    env_path: Path,
    state_path: Path,
    inventory_path: Path,
    now: datetime | None = None,
) -> dict[str, Any]:
    now = now or utc_now()
    with rotation_lock(env_path):
        state = _read_state(state_path)
        document = parse_env(env_path)
        if state["phase"] not in {"activated", "converged"}:
            raise RotationError("convergence verification requires an activated rotation")
        if _env_shape(document, state) != "active":
            raise RotationError("activated rotation state does not match the env file")

        services = parse_inventory(inventory_path)
        target = state["target_generation_id"]
        activated_at = parse_utc(state["activated_at_utc"], "activated_at_utc")
        if now < activated_at:
            raise RotationError("current UTC time precedes activation; convergence is blocked")
        non_target = sorted(service for service, generation in services.items() if generation != target)
        if non_target:
            raise RotationError(
                "runtime inventory is not converged on the target generation for: "
                + ", ".join(non_target)
            )
        convergence = {
            "schema": CONVERGENCE_SCHEMA,
            "verified_at_utc": format_utc(now),
            "target_generation_id": target,
            "services": services,
            "inventory_sha256": hashlib.sha256(_canonical_inventory(services)).hexdigest(),
        }
        if state["phase"] == "converged":
            existing = _validate_convergence(state["convergence"], target)
            if existing["inventory_sha256"] != convergence["inventory_sha256"]:
                raise RotationError("convergence was already recorded from a different inventory")
        else:
            state = {**state, "phase": "converged", "convergence": convergence}
            _write_state(state_path, state)
        return status_document(document, state)


def finalize(
    env_path: Path,
    state_path: Path,
    now: datetime | None = None,
) -> dict[str, Any]:
    now = now or utc_now()
    with rotation_lock(env_path):
        state = _read_state(state_path)
        document = parse_env(env_path)
        if state["phase"] == "finalized":
            if _env_shape(document, state) != "finalized":
                raise RotationError("finalized rotation state does not match the env file")
            return status_document(document, state)
        if state["phase"] != "converged":
            raise RotationError("finalize requires a valid convergence record")
        convergence = _validate_convergence(
            state["convergence"],
            state["target_generation_id"],
        )
        expiry = parse_utc(state["secondary_valid_until_utc"], "secondary_valid_until_utc")
        if now < expiry:
            raise RotationError("retiring credentials have not expired; finalize is blocked")
        verified_at = parse_utc(convergence["verified_at_utc"], "convergence.verified_at_utc")
        if now < verified_at:
            raise RotationError("current UTC time precedes convergence; finalize is blocked")

        shape = _env_shape(document, state)
        if shape == "active":
            _atomic_write(env_path, document.render({}, set(SECONDARY_STATE_NAMES)))
            document = parse_env(env_path)
            shape = _env_shape(document, state)
        if shape != "finalized":
            raise RotationError("finalize requires the activated credential arrangement")
        state = {**state, "phase": "finalized", "finalized_at_utc": format_utc(now)}
        _write_state(state_path, state)
        return status_document(document, state)


def status_document(document: EnvDocument, state: dict[str, Any]) -> dict[str, Any]:
    _validate_state(state)
    shape = _env_shape(document, state)
    expected_shapes = {
        "preparing": {"unprepared", "prepared"},
        "prepared": {"prepared"},
        "activated": {"active"},
        "converged": {"active"},
        "finalized": {"finalized"},
    }
    if shape not in expected_shapes[state["phase"]]:
        raise RotationError(f"rotation phase {state['phase']} is inconsistent with env shape {shape}")
    return {
        "schema": STATE_SCHEMA,
        "rotation_id": state["rotation_id"],
        "phase": state["phase"],
        "previous_generation_id": state["previous_generation_id"],
        "target_generation_id": state["target_generation_id"],
        "secondary_valid_until_utc": state["secondary_valid_until_utc"],
        "prepared_at_utc": state["prepared_at_utc"],
        "activated_at_utc": state["activated_at_utc"],
        "convergence_verified_at_utc": (
            state["convergence"]["verified_at_utc"] if state["convergence"] else None
        ),
        "finalized_at_utc": state["finalized_at_utc"],
    }


def rotation_status(env_path: Path, state_path: Path) -> dict[str, Any]:
    with rotation_lock(env_path):
        state = _read_state(state_path)
        document = parse_env(env_path)
        return status_document(document, state)


def abort_prepared(env_path: Path, state_path: Path) -> dict[str, Any]:
    with rotation_lock(env_path):
        state = _read_state(state_path)
        if state["phase"] not in {"preparing", "prepared"}:
            raise RotationError("only an unactivated prepared rotation can be aborted")

        document = parse_env(env_path)
        shape = _env_shape(document, state)
        if shape == "prepared":
            _atomic_write(env_path, document.render({}, set(SECONDARY_STATE_NAMES)))
            document = parse_env(env_path)
            shape = _env_shape(document, state)
        if shape != "unprepared":
            raise RotationError("prepared rotation abort did not restore the current-only env shape")

        state_path.unlink(missing_ok=True)
        return {
            "schema": STATE_SCHEMA,
            "rotation_id": state["rotation_id"],
            "phase": "aborted",
            "current_generation_id": state["previous_generation_id"],
        }


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)

    def add_common(command_parser: argparse.ArgumentParser) -> None:
        command_parser.add_argument("--env-file", required=True, type=Path)
        command_parser.add_argument("--state-file", required=True, type=Path)

    bootstrap_parser = subparsers.add_parser(
        "bootstrap",
        help="add only a missing nonsecret current-generation marker",
    )
    add_common(bootstrap_parser)
    validate_bootstrap_parser = subparsers.add_parser(
        "validate-bootstrap",
        help="validate bootstrap inputs without locks or mutation",
    )
    add_common(validate_bootstrap_parser)

    prepare_parser = subparsers.add_parser("prepare", help="stage validation-only G+1 credentials")
    add_common(prepare_parser)
    prepare_parser.add_argument(
        "--valid-for-seconds",
        type=int,
        default=DEFAULT_VALID_FOR_SECONDS,
        help=f"bounded dual-generation window ({MIN_VALID_FOR_SECONDS}-{MAX_VALID_FOR_SECONDS})",
    )

    activate_parser = subparsers.add_parser("activate", help="promote G+1 and retain G as secondary")
    add_common(activate_parser)
    activate_parser.add_argument(
        "--minimum-remaining-seconds",
        required=True,
        type=int,
        help=(
            "required remaining validation overlap for maximum token lifetime, rollout, "
            "observation, and rollback"
        ),
    )

    verify_parser = subparsers.add_parser(
        "verify-convergence-input",
        help="validate an exact runtime generation inventory and record convergence",
        description=(
            "Validate UTF-8 JSON containing exactly the required services. Each service value is "
            "a generation string or an object with only credential_generation_id."
        ),
    )
    add_common(verify_parser)
    verify_parser.add_argument("--inventory-file", required=True, type=Path)

    finalize_parser = subparsers.add_parser("finalize", help="remove expired G after convergence")
    add_common(finalize_parser)

    status_parser = subparsers.add_parser("status", help="validate and report nonsecret rotation state")
    add_common(status_parser)
    abort_parser = subparsers.add_parser(
        "abort-prepared",
        help="remove an unactivated G+1 fallback after a pre-mutation failure",
    )
    add_common(abort_parser)
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    try:
        if args.env_file.resolve() == args.state_file.resolve():
            raise RotationError("env file and state file must be different paths")
        if args.command == "validate-bootstrap":
            result = validate_bootstrap_inputs(args.env_file, args.state_file)
        elif args.command == "bootstrap":
            result = bootstrap(args.env_file, args.state_file)
        elif args.command == "prepare":
            result = prepare(args.env_file, args.state_file, args.valid_for_seconds)
        elif args.command == "activate":
            result = activate(
                args.env_file,
                args.state_file,
                args.minimum_remaining_seconds,
            )
        elif args.command == "verify-convergence-input":
            result = verify_convergence_input(
                args.env_file,
                args.state_file,
                args.inventory_file,
            )
        elif args.command == "finalize":
            result = finalize(args.env_file, args.state_file)
        elif args.command == "abort-prepared":
            result = abort_prepared(args.env_file, args.state_file)
        else:
            result = rotation_status(args.env_file, args.state_file)
    except (OSError, RotationError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 1

    json.dump(result, sys.stdout, sort_keys=True)
    sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
