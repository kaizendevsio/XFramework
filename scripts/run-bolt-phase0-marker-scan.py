#!/usr/bin/env python3
"""First-party marker absence hook for Bolt Phase 0."""

from __future__ import annotations

import base64
import contextlib
import datetime as dt
import hashlib
import http.client
import importlib.util
import ipaddress
import json
import os
import re
import ssl
import stat
import sys
import tempfile
import time
import urllib.parse
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable, Mapping, Sequence


PROBE_KINDS = frozenset({"proxy-marker-scan", "seq-marker-scan", "trace-marker-scan"})
PROXY_VARIABLES = frozenset({"http_proxy", "https_proxy", "all_proxy", "no_proxy"})
MANIFEST_KEYS = {
    "schemaVersion",
    "issuerUri",
    "principalReference",
    "refreshedAtUtc",
    "minimumRemainingLifetimeSeconds",
    "expiryEnabled",
    "tokens",
}
TOKEN_KEYS = {
    "purpose",
    "path",
    "sha256Prefix",
    "expiresAtUtc",
    "issuerUri",
    "marker",
    "markerSha256Prefix",
    "identity",
}
PURPOSE = re.compile(r"^[a-z][a-z0-9_]{0,63}$")
MARKER = re.compile(r"^(?:[0-9a-fA-F]{32}|[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})$")

MAX_ENV_BYTES = 256 * 1024
MAX_MANIFEST_BYTES = 256 * 1024
MAX_TOKEN_BYTES = 16 * 1024
MAX_PROXY_FILE_BYTES = 64 * 1024 * 1024
MAX_PROXY_TOTAL_BYTES = 256 * 1024 * 1024
MAX_HTTP_RESPONSE_BYTES = 8 * 1024 * 1024
MAX_HTTP_TOTAL_BYTES = 32 * 1024 * 1024
MAX_HTTP_REQUESTS = 130
MAX_SEQ_EVENTS = 1000
MAX_JAEGER_SERVICES = 128
MAX_JAEGER_TRACES = 1000
QUERY_GRACE_SECONDS = 300
OVERALL_TIMEOUT_SECONDS = 120.0
HTTP_TIMEOUT_SECONDS = 10.0
READ_CHUNK_BYTES = 64 * 1024
ENFORCE_POSIX_PERMISSIONS = os.name == "posix"

ConnectionFactory = Callable[[urllib.parse.SplitResult, float], http.client.HTTPConnection]


class ProbeError(Exception):
    """A secret-free internal failure signal that is never emitted by the hook."""


@dataclass(frozen=True)
class ManifestEvidence:
    tokens: tuple[bytes, ...]
    markers: tuple[bytes, ...]
    refreshed_at: dt.datetime

    @property
    def needles(self) -> tuple[bytes, ...]:
        return self.tokens + self.markers


@dataclass(frozen=True)
class HttpEndpoint:
    parsed: urllib.parse.SplitResult
    port: int


@dataclass
class HttpBudget:
    requests: int = 0
    bytes_read: int = 0


class Deadline:
    def __init__(self, seconds: float = OVERALL_TIMEOUT_SECONDS) -> None:
        self._end = time.monotonic() + seconds

    def remaining(self, maximum: float | None = None) -> float:
        remaining = self._end - time.monotonic()
        if remaining <= 0:
            raise ProbeError("TIMEOUT")
        return remaining if maximum is None else min(remaining, maximum)


def _fail(code: str) -> None:
    raise ProbeError(code)


def _canonical_local_path(value: str, code: str) -> str:
    if not isinstance(value, str) or not value:
        _fail(code)
    native = value.replace(os.altsep, os.sep) if os.altsep else value
    normalized = os.path.normpath(native)
    if not os.path.isabs(native) or normalized != native:
        _fail(code)
    absolute = os.path.abspath(native)
    if absolute != native or os.path.realpath(native) != absolute:
        _fail(code)
    return absolute


def _validate_regular_metadata(metadata: os.stat_result, *, private: bool, code: str) -> None:
    if not stat.S_ISREG(metadata.st_mode) or metadata.st_nlink != 1:
        _fail(code)
    if ENFORCE_POSIX_PERMISSIONS:
        if metadata.st_uid != os.geteuid():
            _fail(code)
        if private and metadata.st_mode & (stat.S_IRWXG | stat.S_IRWXO | stat.S_IXUSR):
            _fail(code)
        if private and not metadata.st_mode & stat.S_IRUSR:
            _fail(code)


def _open_regular(path: str, *, maximum: int, private: bool, code: str) -> tuple[int, os.stat_result]:
    canonical = _canonical_local_path(path, code)
    try:
        before = os.lstat(canonical)
        if stat.S_ISLNK(before.st_mode):
            _fail(code)
        descriptor = os.open(canonical, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
    except (OSError, ValueError):
        _fail(code)
    try:
        current = os.fstat(descriptor)
        if (before.st_dev, before.st_ino) != (current.st_dev, current.st_ino):
            _fail(code)
        _validate_regular_metadata(current, private=private, code=code)
        if current.st_size < 0 or current.st_size > maximum:
            _fail(code)
        return descriptor, current
    except BaseException:
        os.close(descriptor)
        raise


def _read_regular_file(path: str, *, maximum: int, private: bool, code: str) -> tuple[bytes, os.stat_result]:
    descriptor, metadata = _open_regular(path, maximum=maximum, private=private, code=code)
    try:
        data = bytearray()
        while len(data) <= maximum:
            chunk = os.read(descriptor, min(READ_CHUNK_BYTES, maximum + 1 - len(data)))
            if not chunk:
                break
            data.extend(chunk)
        if len(data) > maximum:
            _fail(code)
        after = os.fstat(descriptor)
        if (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns) != (
            metadata.st_dev,
            metadata.st_ino,
            metadata.st_size,
            metadata.st_mtime_ns,
        ):
            _fail(code)
        return bytes(data), metadata
    except OSError:
        _fail(code)
    finally:
        os.close(descriptor)


def _load_env_parser() -> Any:
    parser_path = Path(__file__).with_name("verify-bolt-phase0-env.py")
    spec = importlib.util.spec_from_file_location("bolt_phase0_shared_env", parser_path)
    if spec is None or spec.loader is None:
        _fail("ENV_PARSER")
    module = importlib.util.module_from_spec(spec)
    try:
        spec.loader.exec_module(module)
    except BaseException:
        _fail("ENV_PARSER")
    if not callable(getattr(module, "parse_env", None)):
        _fail("ENV_PARSER")
    return module


def parse_protected_env(path: str) -> dict[str, str]:
    canonical = _canonical_local_path(path, "ENV_FILE")
    descriptor, before = _open_regular(
        canonical,
        maximum=MAX_ENV_BYTES,
        private=True,
        code="ENV_FILE",
    )
    os.close(descriptor)
    parser = _load_env_parser()
    try:
        values = parser.parse_env(Path(canonical))
        after = os.lstat(canonical)
    except (OSError, ValueError):
        _fail("ENV_FILE")
    if (before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns) != (
        after.st_dev,
        after.st_ino,
        after.st_size,
        after.st_mtime_ns,
    ):
        _fail("ENV_FILE")
    if not isinstance(values, dict) or not all(isinstance(key, str) and isinstance(value, str) for key, value in values.items()):
        _fail("ENV_FILE")
    return values


def _required(values: Mapping[str, str], key: str) -> str:
    value = values.get(key)
    if not isinstance(value, str) or not value:
        _fail("CONFIGURATION")
    return value


def _parse_utc(value: Any, code: str) -> dt.datetime:
    if not isinstance(value, str) or not value.endswith("Z"):
        _fail(code)
    try:
        parsed = dt.datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError:
        _fail(code)
    if parsed.tzinfo is None or parsed.utcoffset() != dt.timedelta(0):
        _fail(code)
    return parsed


def _decode_jti(token: bytes) -> str:
    parts = token.split(b".")
    if len(parts) != 3:
        _fail("TOKEN_FILE")
    try:
        payload = parts[1] + b"=" * (-len(parts[1]) % 4)
        claims = json.loads(base64.urlsafe_b64decode(payload).decode("utf-8"))
    except (UnicodeDecodeError, ValueError, json.JSONDecodeError):
        _fail("TOKEN_FILE")
    marker = claims.get("jti") if isinstance(claims, dict) else None
    if not isinstance(marker, str):
        _fail("TOKEN_FILE")
    return marker


def load_manifest(path: str, *, now: dt.datetime | None = None) -> ManifestEvidence:
    raw, _ = _read_regular_file(
        path,
        maximum=MAX_MANIFEST_BYTES,
        private=True,
        code="MANIFEST",
    )
    try:
        document = json.loads(raw.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError):
        _fail("MANIFEST")
    if not isinstance(document, dict) or set(document) != MANIFEST_KEYS:
        _fail("MANIFEST")
    if document.get("schemaVersion") != "bolt-phase0-token-manifest/v1":
        _fail("MANIFEST")
    if not isinstance(document.get("issuerUri"), str) or not document["issuerUri"]:
        _fail("MANIFEST")
    if not isinstance(document.get("principalReference"), str) or not document["principalReference"]:
        _fail("MANIFEST")
    if type(document.get("minimumRemainingLifetimeSeconds")) is not int or not 60 <= document["minimumRemainingLifetimeSeconds"] <= 3600:
        _fail("MANIFEST")
    if type(document.get("expiryEnabled")) is not bool:
        _fail("MANIFEST")

    refreshed_at = _parse_utc(document.get("refreshedAtUtc"), "MANIFEST")
    current = now or dt.datetime.now(dt.timezone.utc)
    if refreshed_at > current + dt.timedelta(seconds=5):
        _fail("MANIFEST")
    entries = document.get("tokens")
    if not isinstance(entries, list) or not 1 <= len(entries) <= 16:
        _fail("MANIFEST")

    tokens: list[bytes] = []
    markers: list[bytes] = []
    purposes: set[str] = set()
    identities: set[tuple[int, int]] = set()
    for entry in entries:
        if not isinstance(entry, dict) or set(entry) != TOKEN_KEYS:
            _fail("MANIFEST")
        purpose = entry.get("purpose")
        marker = entry.get("marker")
        identity = entry.get("identity")
        if not isinstance(purpose, str) or not PURPOSE.fullmatch(purpose) or purpose in purposes:
            _fail("MANIFEST")
        if not isinstance(marker, str) or not MARKER.fullmatch(marker) or int(marker.replace("-", ""), 16) == 0:
            _fail("MANIFEST")
        if not isinstance(identity, list) or len(identity) != 5:
            _fail("MANIFEST")
        if not all(type(value) is int and value >= 0 for value in identity[:4]):
            _fail("MANIFEST")
        if not isinstance(identity[4], str) or not re.fullmatch(r"[0-9a-f]{64}", identity[4]):
            _fail("MANIFEST")
        if entry.get("issuerUri") != document["issuerUri"]:
            _fail("MANIFEST")
        _parse_utc(entry.get("expiresAtUtc"), "MANIFEST")

        token_raw, metadata = _read_regular_file(
            entry.get("path"),
            maximum=MAX_TOKEN_BYTES,
            private=True,
            code="TOKEN_FILE",
        )
        token = token_raw.strip()
        if len(token) <= 32 or len(token) > MAX_TOKEN_BYTES or any(character in b" \t\r\n\0" for character in token):
            _fail("TOKEN_FILE")
        try:
            token.decode("ascii")
        except UnicodeDecodeError:
            _fail("TOKEN_FILE")
        digest = hashlib.sha256(token).hexdigest()
        expected_identity = [metadata.st_dev, metadata.st_ino, metadata.st_size, metadata.st_mtime_ns, digest]
        if identity != expected_identity:
            _fail("TOKEN_FILE")
        if entry.get("sha256Prefix") != digest[:12]:
            _fail("MANIFEST")
        marker_digest = hashlib.sha256(marker.encode("ascii")).hexdigest()
        if entry.get("markerSha256Prefix") != marker_digest[:12] or _decode_jti(token) != marker:
            _fail("MANIFEST")
        file_identity = (metadata.st_dev, metadata.st_ino)
        if file_identity in identities or token in tokens or marker.encode("ascii") in markers:
            _fail("MANIFEST")
        purposes.add(purpose)
        identities.add(file_identity)
        tokens.append(token)
        markers.append(marker.encode("ascii"))

    return ManifestEvidence(tuple(tokens), tuple(markers), refreshed_at)


def _contains_needle(data: bytes, needles: Sequence[bytes]) -> bool:
    return any(needle in data for needle in needles)


def scan_proxy_logs(paths_value: str, needles: Sequence[bytes], deadline: Deadline) -> None:
    paths = paths_value.split(",")
    if not paths or len(paths) > 32 or any(not path for path in paths) or len(set(paths)) != len(paths):
        _fail("PROXY_PATHS")
    longest = max((len(needle) for needle in needles), default=1)
    total = 0
    for path in paths:
        descriptor, metadata = _open_regular(
            path,
            maximum=MAX_PROXY_FILE_BYTES,
            private=False,
            code="PROXY_FILE",
        )
        overlap = b""
        read_count = 0
        try:
            while True:
                deadline.remaining()
                chunk = os.read(descriptor, READ_CHUNK_BYTES)
                if not chunk:
                    break
                read_count += len(chunk)
                total += len(chunk)
                if read_count > MAX_PROXY_FILE_BYTES or total > MAX_PROXY_TOTAL_BYTES:
                    _fail("PROXY_LIMIT")
                data = overlap + chunk
                if _contains_needle(data, needles):
                    _fail("MATCH")
                overlap = data[-(longest - 1):] if longest > 1 else b""
            after = os.fstat(descriptor)
            if (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns) != (
                metadata.st_dev,
                metadata.st_ino,
                metadata.st_size,
                metadata.st_mtime_ns,
            ):
                _fail("PROXY_FILE")
        except OSError:
            _fail("PROXY_FILE")
        finally:
            os.close(descriptor)


def parse_http_endpoint(value: str, *, expected_path: str) -> HttpEndpoint:
    try:
        parsed = urllib.parse.urlsplit(value)
        port = parsed.port
    except ValueError:
        _fail("URL")
    if (
        parsed.scheme not in {"http", "https"}
        or not parsed.hostname
        or parsed.username is not None
        or parsed.password is not None
        or parsed.query
        or parsed.fragment
        or parsed.path != expected_path
        or not parsed.hostname.isascii()
        or parsed.hostname.endswith(".")
    ):
        _fail("URL")
    host = parsed.hostname.lower()
    if parsed.scheme == "http":
        loopback = host == "localhost"
        try:
            loopback = loopback or ipaddress.ip_address(host).is_loopback
        except ValueError:
            pass
        if not loopback:
            _fail("URL")
    selected_port = port or (443 if parsed.scheme == "https" else 80)
    if not 1 <= selected_port <= 65535:
        _fail("URL")
    return HttpEndpoint(parsed, selected_port)


def _default_connection(endpoint: urllib.parse.SplitResult, timeout: float) -> http.client.HTTPConnection:
    connection_type: type[http.client.HTTPConnection]
    options: dict[str, Any] = {"timeout": timeout}
    if endpoint.scheme == "https":
        connection_type = http.client.HTTPSConnection
        options["context"] = ssl.create_default_context()
    else:
        connection_type = http.client.HTTPConnection
    return connection_type(endpoint.hostname, endpoint.port, **options)


def _request_json(
    endpoint: HttpEndpoint,
    target: str,
    headers: Mapping[str, str],
    needles: Sequence[bytes],
    deadline: Deadline,
    budget: HttpBudget,
    connection_factory: ConnectionFactory,
) -> tuple[Any, Mapping[str, str]]:
    if not target.startswith(endpoint.parsed.path) or "#" in target:
        _fail("HTTP_REQUEST")
    budget.requests += 1
    if budget.requests > MAX_HTTP_REQUESTS:
        _fail("HTTP_LIMIT")
    connection = connection_factory(endpoint.parsed, deadline.remaining(HTTP_TIMEOUT_SECONDS))
    try:
        connection.request(
            "GET",
            target,
            headers={"Accept": "application/json", "Accept-Encoding": "identity", **headers},
        )
        response = connection.getresponse()
        if response.status != 200:
            _fail("HTTP_STATUS")
        content_type = response.getheader("Content-Type", "")
        content_encoding = response.getheader("Content-Encoding", "identity")
        content_length = response.getheader("Content-Length")
        if content_type.lower().split(";", 1)[0].strip() != "application/json":
            _fail("HTTP_RESPONSE")
        if content_encoding.lower().strip() not in {"", "identity"}:
            _fail("HTTP_RESPONSE")
        if content_length is not None:
            try:
                declared = int(content_length)
            except ValueError:
                _fail("HTTP_RESPONSE")
            if declared < 2 or declared > MAX_HTTP_RESPONSE_BYTES:
                _fail("HTTP_LIMIT")
        body = bytearray()
        while len(body) <= MAX_HTTP_RESPONSE_BYTES:
            deadline.remaining()
            chunk = response.read(min(READ_CHUNK_BYTES, MAX_HTTP_RESPONSE_BYTES + 1 - len(body)))
            if not chunk:
                break
            body.extend(chunk)
        if len(body) > MAX_HTTP_RESPONSE_BYTES:
            _fail("HTTP_LIMIT")
        budget.bytes_read += len(body)
        if budget.bytes_read > MAX_HTTP_TOTAL_BYTES:
            _fail("HTTP_LIMIT")
        raw = bytes(body)
        if _contains_needle(raw, needles):
            _fail("MATCH")
        try:
            document = json.loads(raw.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError):
            _fail("HTTP_RESPONSE")
        response_headers = {name.lower(): value for name, value in response.getheaders()}
        if "location" in response_headers:
            _fail("HTTP_RESPONSE")
        return document, response_headers
    except (OSError, TimeoutError, http.client.HTTPException, ssl.SSLError):
        _fail("HTTP_FAILURE")
    finally:
        connection.close()


def _query_window(evidence: ManifestEvidence, now: dt.datetime) -> tuple[str, str, int, int]:
    start = evidence.refreshed_at - dt.timedelta(seconds=QUERY_GRACE_SECONDS)
    end = now + dt.timedelta(seconds=5)
    return (
        start.isoformat().replace("+00:00", "Z"),
        end.isoformat().replace("+00:00", "Z"),
        int(start.timestamp() * 1_000_000),
        int(end.timestamp() * 1_000_000),
    )


def scan_seq(
    url: str,
    api_key: str,
    evidence: ManifestEvidence,
    deadline: Deadline,
    *,
    now: dt.datetime,
    connection_factory: ConnectionFactory,
) -> None:
    endpoint = parse_http_endpoint(url, expected_path="/api/events")
    if not api_key.isascii() or api_key != api_key.strip() or not 16 <= len(api_key) <= 512:
        _fail("CONFIGURATION")
    from_utc, to_utc, _, _ = _query_window(evidence, now)
    query = urllib.parse.urlencode(
        {
            "count": str(MAX_SEQ_EVENTS),
            "render": "true",
            "fromDateUtc": from_utc,
            "toDateUtc": to_utc,
        }
    )
    document, _ = _request_json(
        endpoint,
        f"{endpoint.parsed.path}?{query}",
        {"X-Seq-ApiKey": api_key},
        evidence.needles,
        deadline,
        HttpBudget(),
        connection_factory,
    )
    if isinstance(document, list):
        events = document
    elif isinstance(document, dict):
        upper = document.get("Events")
        lower = document.get("events")
        events = upper if isinstance(upper, list) else lower
    else:
        events = None
    if not isinstance(events, list) or len(events) >= MAX_SEQ_EVENTS:
        _fail("SEQ_RESPONSE")


def _jaeger_data(document: Any, code: str) -> list[Any]:
    if not isinstance(document, dict) or not isinstance(document.get("data"), list):
        _fail(code)
    errors = document.get("errors")
    if errors not in (None, []):
        _fail(code)
    return document["data"]


def scan_jaeger(
    url: str,
    evidence: ManifestEvidence,
    deadline: Deadline,
    *,
    now: dt.datetime,
    connection_factory: ConnectionFactory,
) -> None:
    endpoint = parse_http_endpoint(url, expected_path="/api")
    budget = HttpBudget()
    services_document, _ = _request_json(
        endpoint,
        f"{endpoint.parsed.path}/services",
        {},
        evidence.needles,
        deadline,
        budget,
        connection_factory,
    )
    services = _jaeger_data(services_document, "JAEGER_RESPONSE")
    if len(services) > MAX_JAEGER_SERVICES:
        _fail("JAEGER_LIMIT")
    if len(set(services)) != len(services):
        _fail("JAEGER_RESPONSE")
    _, _, start, end = _query_window(evidence, now)
    for service in services:
        if (
            not isinstance(service, str)
            or not service
            or len(service) > 256
            or service != service.strip()
            or any(ord(character) < 32 or ord(character) == 127 for character in service)
        ):
            _fail("JAEGER_RESPONSE")
        query = urllib.parse.urlencode(
            {
                "service": service,
                "start": str(start),
                "end": str(end),
                "limit": str(MAX_JAEGER_TRACES),
            }
        )
        traces_document, _ = _request_json(
            endpoint,
            f"{endpoint.parsed.path}/traces?{query}",
            {},
            evidence.needles,
            deadline,
            budget,
            connection_factory,
        )
        traces = _jaeger_data(traces_document, "JAEGER_RESPONSE")
        if len(traces) >= MAX_JAEGER_TRACES:
            _fail("JAEGER_LIMIT")


def _prepare_receipt_target(path: str) -> str:
    canonical = _canonical_local_path(path, "RECEIPT")
    parent = os.path.dirname(canonical)
    try:
        parent_metadata = os.lstat(parent)
    except OSError:
        _fail("RECEIPT")
    if not stat.S_ISDIR(parent_metadata.st_mode) or stat.S_ISLNK(parent_metadata.st_mode):
        _fail("RECEIPT")
    if ENFORCE_POSIX_PERMISSIONS:
        if parent_metadata.st_uid != os.geteuid() or parent_metadata.st_mode & (stat.S_IRWXG | stat.S_IRWXO):
            _fail("RECEIPT")
    if os.path.lexists(canonical):
        try:
            metadata = os.lstat(canonical)
            _validate_regular_metadata(metadata, private=True, code="RECEIPT")
            os.unlink(canonical)
        except OSError:
            _fail("RECEIPT")
    return canonical


def write_receipt(
    path: str,
    probe_kind: str,
    assertions: Mapping[str, Any],
    started_at: dt.datetime,
    completed_at: dt.datetime,
) -> None:
    receipt = {
        "schemaVersion": "bolt-phase0-probe-receipt/v1",
        "probe": probe_kind,
        "status": "passed",
        "startedAtUtc": started_at.isoformat().replace("+00:00", "Z"),
        "completedAtUtc": completed_at.isoformat().replace("+00:00", "Z"),
        "assertions": dict(assertions),
    }
    payload = (json.dumps(receipt, separators=(",", ":"), sort_keys=True) + "\n").encode("utf-8")
    parent = os.path.dirname(path)
    descriptor = -1
    temporary = ""
    try:
        descriptor, temporary = tempfile.mkstemp(prefix=".bolt-marker-receipt-", dir=parent)
        os.fchmod(descriptor, 0o600)
        with os.fdopen(descriptor, "wb", closefd=True) as stream:
            descriptor = -1
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
        if os.path.lexists(path):
            _fail("RECEIPT")
        os.replace(temporary, path)
        temporary = ""
        os.chmod(path, 0o600)
        if hasattr(os, "O_DIRECTORY"):
            directory = os.open(parent, os.O_RDONLY | os.O_DIRECTORY)
            try:
                os.fsync(directory)
            finally:
                os.close(directory)
    except OSError:
        _fail("RECEIPT")
    finally:
        if descriptor >= 0:
            os.close(descriptor)
        if temporary:
            with contextlib.suppress(OSError):
                os.unlink(temporary)


def reject_proxy_environment(environ: Mapping[str, str]) -> None:
    if any(name.lower() in PROXY_VARIABLES for name in environ):
        _fail("PROXY_ENVIRONMENT")


def run_hook(
    environ: Mapping[str, str],
    *,
    connection_factory: ConnectionFactory = _default_connection,
    now_factory: Callable[[], dt.datetime] = lambda: dt.datetime.now(dt.timezone.utc),
) -> int:
    receipt_path: str | None = None
    try:
        started_at = now_factory()
        receipt_path = _prepare_receipt_target(_required(environ, "BOLT_SYNTHETIC_PROBE_RECEIPT"))
        reject_proxy_environment(environ)
        probe_kind = _required(environ, "BOLT_SYNTHETIC_PROBE_KIND")
        if probe_kind not in PROBE_KINDS:
            _fail("PROBE_KIND")
        values = parse_protected_env(_required(environ, "XFRAMEWORK_ENV_FILE"))
        evidence = load_manifest(_required(environ, "BOLT_SYNTHETIC_TOKEN_MANIFEST"), now=started_at)
        deadline = Deadline()
        assertions = {
            "retainedStoreQueried": True,
            "matches": 0,
            "tokensSearched": len(evidence.tokens),
            "markersSearched": len(evidence.markers),
        }

        if probe_kind == "proxy-marker-scan":
            proxy_mode = _required(values, "BOLT_SYNTHETIC_PROXY_MODE")
            proxy_log_paths = values.get("BOLT_SYNTHETIC_PROXY_LOG_PATHS", "")
            if proxy_mode == "logs":
                scan_proxy_logs(_required(values, "BOLT_SYNTHETIC_PROXY_LOG_PATHS"), evidence.needles, deadline)
            elif proxy_mode == "direct-kestrel":
                if proxy_log_paths:
                    _fail("CONFIGURATION")
                assertions = {
                    "retainedStoreQueried": False,
                    "notApplicableReason": "direct-kestrel-publication",
                    "matches": 0,
                    "tokensSearched": len(evidence.tokens),
                    "markersSearched": len(evidence.markers),
                }
            else:
                _fail("CONFIGURATION")
        elif probe_kind == "seq-marker-scan":
            scan_seq(
                _required(values, "BOLT_SYNTHETIC_SEQ_API_URL"),
                _required(values, "BOLT_SYNTHETIC_SEQ_API_KEY"),
                evidence,
                deadline,
                now=started_at,
                connection_factory=connection_factory,
            )
        else:
            scan_jaeger(
                _required(values, "BOLT_SYNTHETIC_JAEGER_QUERY_API_URL"),
                evidence,
                deadline,
                now=started_at,
                connection_factory=connection_factory,
            )

        completed_at = now_factory()
        if completed_at < started_at:
            _fail("CLOCK")
        write_receipt(receipt_path, probe_kind, assertions, started_at, completed_at)
        return 0
    except BaseException:
        if receipt_path and os.path.lexists(receipt_path):
            with contextlib.suppress(OSError):
                metadata = os.lstat(receipt_path)
                if stat.S_ISREG(metadata.st_mode) and not stat.S_ISLNK(metadata.st_mode):
                    os.unlink(receipt_path)
        return 1


def main() -> int:
    return run_hook(os.environ)


if __name__ == "__main__":
    with open(os.devnull, "w", encoding="utf-8") as sink:
        with contextlib.redirect_stdout(sink), contextlib.redirect_stderr(sink):
            try:
                exit_code = main()
            except BaseException:
                exit_code = 1
    raise SystemExit(exit_code)
