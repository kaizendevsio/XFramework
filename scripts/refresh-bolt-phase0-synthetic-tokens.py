#!/usr/bin/env python3
"""Refresh the private transport and actor tokens used by Bolt Phase 0 synthetics."""

from __future__ import annotations

import base64
import datetime as dt
import http.client
import json
import os
import re
import socket
import ssl
import stat
import tempfile
import unicodedata
import uuid
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable
from urllib.parse import urlsplit


MAX_ENV_BYTES = 1024 * 1024
MAX_RESPONSE_BYTES = 1024 * 1024
MAX_TOKEN_BYTES = 16 * 1024
HTTP_TIMEOUT_SECONDS = 20
COMMUNICATIONS_CLIENT_ID = "XFramework.Communications"
PORTAL_CLIENT_ID = "XFramework.Portal"
TRANSPORT_ISSUER = "XFramework.IdentityServer"
TRANSPORT_AUDIENCE = "XFramework.Bolt.Hub"
TRANSPORT_SCOPE = "bolt.service"
TRANSPORT_ALGORITHM = "RS256"
TRANSPORT_TOKEN_TYPE = "bolt+jwt"
ACTOR_ALGORITHM = "HS512"
ACTOR_TOKEN_TYPE = "JWT"
RECEIPT_SCHEMA = "bolt-phase0-token-refresh/v2"
PRINCIPAL_REFERENCE = "bolt-phase0-synthetic"
GENERATION_CLAIM = "credential_generation"
MAX_EXPIRY_REMAINING_SECONDS = 570
ALLOWED_AUTHORIZATION_TYPES = {
    "Default": 0,
    "UsernameEmailPhone": 1,
    "Username": 2,
    "Email": 3,
    "Phone": 4,
}
SERVICE_DATA_KEYS = {"accessToken", "tokenType", "expiresAtUtc"}
USER_DATA_KEYS = {
    "identity",
    "credential",
    "accessToken",
    "tokenType",
    "expiresIn",
    "refreshToken",
    "sessionId",
}
JWT_SEGMENT = re.compile(r"[A-Za-z0-9_-]+")
ENV_KEY = re.compile(r"[A-Za-z_][A-Za-z0-9_]*")
GUID = re.compile(r"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")
JTI = re.compile(r"(?:[0-9a-fA-F]{32}|[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12})")
ENFORCE_POSIX_PERMISSIONS = os.name == "posix" and hasattr(os, "geteuid")


class RefreshError(Exception):
    def __init__(self, code: str):
        super().__init__(code)
        self.code = code


@dataclass(frozen=True)
class Config:
    base_url: str
    host: str
    port: int
    actor_issuer: str
    actor_audience: str
    credential_generation: str
    communications_secret: str
    portal_secret: str
    tenant_id: str
    credential_id: str
    username: str
    password: str
    role_id: str
    authorization_type: int
    minimum_lifetime_seconds: int
    communications_transport_path: str
    portal_transport_path: str
    user_actor_path: str
    expiry_transport_path: str


@dataclass(frozen=True)
class TokenEvidence:
    value: str
    issuer: str
    expiration: int
    jti: str


def _raise(code: str) -> None:
    raise RefreshError(code)


def _read_regular_file(path: str, *, maximum: int, private: bool, code: str) -> bytes:
    if not path or not os.path.isabs(path) or os.path.realpath(path) != os.path.abspath(path):
        _raise(code)
    try:
        before = os.lstat(path)
        if not stat.S_ISREG(before.st_mode) or stat.S_ISLNK(before.st_mode):
            _raise(code)
        flags = os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0) | getattr(os, "O_BINARY", 0)
        descriptor = os.open(path, flags)
        try:
            current = os.fstat(descriptor)
            if (current.st_dev, current.st_ino) != (before.st_dev, before.st_ino):
                _raise(code)
            if private and ENFORCE_POSIX_PERMISSIONS:
                if current.st_uid != os.geteuid():
                    _raise(code)
                if current.st_nlink != 1:
                    _raise(code)
                if current.st_mode & (stat.S_IRWXG | stat.S_IRWXO | stat.S_IXUSR):
                    _raise(code)
                if not current.st_mode & stat.S_IRUSR:
                    _raise(code)
            if current.st_size <= 0 or current.st_size > maximum:
                _raise(code)
            data = os.read(descriptor, maximum + 1)
            if len(data) != current.st_size or len(data) > maximum:
                _raise(code)
            return data
        finally:
            os.close(descriptor)
    except RefreshError:
        raise
    except OSError:
        _raise(code)


def parse_protected_env(path: str) -> dict[str, str]:
    raw = _read_regular_file(path, maximum=MAX_ENV_BYTES, private=True, code="ENV_FILE")
    if raw.startswith(b"\xef\xbb\xbf") or b"\x00" in raw:
        _raise("ENV_SYNTAX")
    try:
        text = raw.decode("utf-8", errors="strict")
    except UnicodeDecodeError:
        _raise("ENV_SYNTAX")
    if "\r" in text.replace("\r\n", ""):
        _raise("ENV_SYNTAX")
    if any(
        unicodedata.category(character) in {"Cc", "Cf", "Cs", "Zl", "Zp"}
        and character not in "\r\n"
        for character in text
    ):
        _raise("ENV_SYNTAX")

    values: dict[str, str] = {}
    for raw_line in text.replace("\r\n", "\n").split("\n"):
        if not raw_line or not raw_line.strip():
            continue
        if raw_line.lstrip().startswith("#"):
            continue
        if raw_line[0].isspace() or "=" not in raw_line:
            _raise("ENV_SYNTAX")
        key, value = raw_line.split("=", 1)
        if not ENV_KEY.fullmatch(key) or key in values:
            _raise("ENV_SYNTAX")
        if (
            value != value.strip()
            or re.search(r"\s#", value)
            or any(ord(character) < 32 or ord(character) == 127 for character in value)
        ):
            _raise("ENV_SYNTAX")
        values[key] = value
    return values


def _required(values: dict[str, str], key: str) -> str:
    value = values.get(key)
    if value is None or value == "":
        _raise("CONFIGURATION")
    return value


def _canonical_guid(value: str) -> str:
    if not GUID.fullmatch(value):
        _raise("CONFIGURATION")
    try:
        parsed = uuid.UUID(value)
    except (ValueError, AttributeError):
        _raise("CONFIGURATION")
    if parsed.int == 0:
        _raise("CONFIGURATION")
    return str(parsed)


def _https_origin(value: str, *, code: str) -> tuple[str, str, int]:
    try:
        parsed = urlsplit(value)
        port = parsed.port or 443
    except ValueError:
        _raise(code)
    if (
        parsed.scheme.lower() != "https"
        or not parsed.hostname
        or parsed.username is not None
        or parsed.password is not None
        or parsed.query
        or parsed.fragment
        or parsed.path not in ("", "/")
        or not 1 <= port <= 65535
    ):
        _raise(code)
    hostname = parsed.hostname.lower()
    authority = hostname if port == 443 else f"{hostname}:{port}"
    return f"https://{authority}", hostname, port


def _authorization_type(value: str) -> int:
    if value in ALLOWED_AUTHORIZATION_TYPES:
        return ALLOWED_AUTHORIZATION_TYPES[value]
    if value.isascii() and value.isdigit():
        parsed = int(value)
        if parsed in ALLOWED_AUTHORIZATION_TYPES.values():
            return parsed
    _raise("CONFIGURATION")


def _bounded_integer(value: str, minimum: int, maximum: int) -> int:
    if not value.isascii() or not value.isdigit():
        _raise("CONFIGURATION")
    parsed = int(value)
    if parsed < minimum or parsed > maximum:
        _raise("CONFIGURATION")
    return parsed


def _validate_secret(value: str, *, minimum: int) -> str:
    if len(value) < minimum or len(value) > 4096 or any(character.isspace() for character in value):
        _raise("CONFIGURATION")
    return value


def load_config(values: dict[str, str]) -> Config:
    base_url, host, port = _https_origin(
        _required(values, "BOLT_SYNTHETIC_IDENTITYSERVER_BASE_URL"), code="CONFIGURATION"
    )
    raw_issuer = _required(values, "JWT_ISSUER")
    if (
        len(raw_issuer) > 512
        or raw_issuer != raw_issuer.strip()
        or any(ord(character) < 33 or ord(character) == 127 for character in raw_issuer)
    ):
        _raise("CONFIGURATION")
    audience = _required(values, "JWT_AUDIENCE")
    if len(audience) > 512 or audience != audience.strip() or any(ord(c) < 33 for c in audience):
        _raise("CONFIGURATION")
    generation = _required(values, "CREDENTIAL_GENERATION_ID")
    if not re.fullmatch(r"[A-Za-z0-9_.:-]{1,96}", generation):
        _raise("CONFIGURATION")

    username = _required(values, "BOLT_SYNTHETIC_USER_USERNAME")
    password = _required(values, "BOLT_SYNTHETIC_USER_PASSWORD")
    if len(username) > 256 or username != username.strip() or len(password) > 4096:
        _raise("CONFIGURATION")

    return Config(
        base_url=base_url,
        host=host,
        port=port,
        actor_issuer=raw_issuer,
        actor_audience=audience,
        credential_generation=generation,
        communications_secret=_validate_secret(
            _required(values, "COMMUNICATIONS_SERVICE_IDENTITY_SECRET"), minimum=32
        ),
        portal_secret=_validate_secret(
            _required(values, "PORTAL_SERVICE_IDENTITY_SECRET"), minimum=32
        ),
        tenant_id=_canonical_guid(_required(values, "BOLT_SYNTHETIC_TENANT_ID")),
        credential_id=_canonical_guid(_required(values, "BOLT_SYNTHETIC_CREDENTIAL_ID")),
        username=username,
        password=password,
        role_id=_canonical_guid(_required(values, "BOLT_SYNTHETIC_USER_ROLE_ID")),
        authorization_type=_authorization_type(
            _required(values, "BOLT_SYNTHETIC_USER_AUTHORIZATION_TYPE")
        ),
        minimum_lifetime_seconds=_bounded_integer(
            _required(values, "BOLT_SYNTHETIC_MIN_TOKEN_LIFETIME_SECONDS"), 60, 3600
        ),
        communications_transport_path=_required(
            values, "BOLT_SYNTHETIC_COMMUNICATIONS_TRANSPORT_TOKEN_PATH"
        ),
        portal_transport_path=_required(values, "BOLT_SYNTHETIC_PORTAL_TRANSPORT_TOKEN_PATH"),
        user_actor_path=_required(values, "BOLT_SYNTHETIC_USER_ACTOR_TOKEN_PATH"),
        expiry_transport_path=_required(
            values, "BOLT_SYNTHETIC_EXPIRY_TRANSPORT_TOKEN_PATH"
        ),
    )


def _json_object_no_duplicates(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            _raise("RESPONSE_JSON")
        result[key] = value
    return result


def _parse_json(data: bytes, *, code: str) -> dict[str, Any]:
    try:
        value = json.loads(
            data.decode("utf-8", errors="strict"),
            object_pairs_hook=_json_object_no_duplicates,
            parse_constant=lambda _: _raise(code),
        )
    except RefreshError:
        raise
    except (UnicodeDecodeError, json.JSONDecodeError, TypeError, ValueError):
        _raise(code)
    if not isinstance(value, dict):
        _raise(code)
    return value


def _response_data(document: dict[str, Any], expected_keys: set[str]) -> dict[str, Any]:
    if set(document) != expected_keys:
        _raise("RESPONSE_SCHEMA")
    return document


def _build_ssl_context() -> ssl.SSLContext:
    try:
        context = ssl.create_default_context(purpose=ssl.Purpose.SERVER_AUTH)
    except (OSError, ssl.SSLError, ValueError):
        _raise("TLS_CONFIGURATION")
    context.check_hostname = True
    context.verify_mode = ssl.CERT_REQUIRED
    context.minimum_version = ssl.TLSVersion.TLSv1_2
    return context


ConnectionFactory = Callable[..., http.client.HTTPSConnection]
ContextFactory = Callable[[], ssl.SSLContext]


def _post_json(
    config: Config,
    path: str,
    body: dict[str, Any],
    *,
    connection_factory: ConnectionFactory = http.client.HTTPSConnection,
    context_factory: ContextFactory = _build_ssl_context,
) -> dict[str, Any]:
    serialized = json.dumps(body, separators=(",", ":"), ensure_ascii=True).encode("utf-8")
    context = context_factory()
    if not context.check_hostname or context.verify_mode != ssl.CERT_REQUIRED:
        _raise("TLS_CONFIGURATION")
    connection = connection_factory(
        config.host,
        config.port,
        context=context,
        timeout=HTTP_TIMEOUT_SECONDS,
    )
    try:
        connection.request(
            "POST",
            path,
            body=serialized,
            headers={
                "Accept": "application/json",
                "Accept-Encoding": "identity",
                "Content-Type": "application/json; charset=utf-8",
                "Content-Length": str(len(serialized)),
            },
        )
        response = connection.getresponse()
        if response.status != 200:
            _raise("HTTP_STATUS")
        content_type = response.getheader("Content-Type", "")
        content_encoding = response.getheader("Content-Encoding", "identity")
        content_length = response.getheader("Content-Length")
        if not content_type.lower().split(";", 1)[0].strip() == "application/json":
            _raise("HTTP_RESPONSE")
        if content_encoding.lower().strip() not in ("", "identity"):
            _raise("HTTP_RESPONSE")
        if content_length is not None:
            try:
                if int(content_length) < 2 or int(content_length) > MAX_RESPONSE_BYTES:
                    _raise("HTTP_RESPONSE")
            except ValueError:
                _raise("HTTP_RESPONSE")
        response_bytes = response.read(MAX_RESPONSE_BYTES + 1)
        if len(response_bytes) < 2 or len(response_bytes) > MAX_RESPONSE_BYTES:
            _raise("HTTP_RESPONSE")
        return _parse_json(response_bytes, code="RESPONSE_JSON")
    except RefreshError:
        raise
    except (ssl.SSLError, ssl.CertificateError):
        _raise("TLS_CONNECTION")
    except (OSError, socket.error, http.client.HTTPException):
        _raise("HTTP_CONNECTION")
    finally:
        try:
            connection.close()
        except Exception:
            pass


def _base64url_decode(segment: str) -> bytes:
    if not JWT_SEGMENT.fullmatch(segment) or "=" in segment:
        _raise("TOKEN_SHAPE")
    try:
        decoded = base64.urlsafe_b64decode(segment + "=" * (-len(segment) % 4))
    except (ValueError, base64.binascii.Error):
        _raise("TOKEN_SHAPE")
    if base64.urlsafe_b64encode(decoded).rstrip(b"=").decode("ascii") != segment:
        _raise("TOKEN_SHAPE")
    return decoded


def _numeric_date(claims: dict[str, Any], name: str) -> int:
    value = claims.get(name)
    if isinstance(value, bool) or not isinstance(value, int):
        _raise("TOKEN_CLAIMS")
    return value


def _read_jwt(token: Any) -> tuple[str, dict[str, Any], dict[str, Any]]:
    if not isinstance(token, str) or len(token.encode("utf-8")) > MAX_TOKEN_BYTES:
        _raise("TOKEN_SHAPE")
    if not token or any(character.isspace() or ord(character) < 33 for character in token):
        _raise("TOKEN_SHAPE")
    segments = token.split(".")
    if len(segments) != 3:
        _raise("TOKEN_SHAPE")
    header = _parse_json(_base64url_decode(segments[0]), code="TOKEN_SHAPE")
    claims = _parse_json(_base64url_decode(segments[1]), code="TOKEN_SHAPE")
    signature = _base64url_decode(segments[2])
    if not signature or len(signature) > 1024:
        _raise("TOKEN_SHAPE")
    return token, header, claims


def _validate_temporal_claims(
    token: str,
    claims: dict[str, Any],
    *,
    expected_issuer: str,
    expected_audience: str,
    now: int,
    minimum_remaining: int,
    maximum_remaining: int | None = None,
    require_issued_at: bool = False,
) -> TokenEvidence:
    if claims.get("iss") != expected_issuer:
        _raise("TOKEN_CLAIMS")
    audience = claims.get("aud")
    if audience != expected_audience and audience != [expected_audience]:
        _raise("TOKEN_CLAIMS")
    expiration = _numeric_date(claims, "exp")
    not_before = _numeric_date(claims, "nbf")
    issued_at = _numeric_date(claims, "iat") if require_issued_at or "iat" in claims else not_before
    if (
        issued_at > now + 30
        or not_before > now + 30
        or expiration <= max(issued_at, not_before)
        or expiration < now + minimum_remaining
    ):
        _raise("TOKEN_LIFETIME")
    if maximum_remaining is not None and expiration > now + maximum_remaining:
        _raise("TOKEN_LIFETIME")
    jti = claims.get("jti")
    if not isinstance(jti, str) or not JTI.fullmatch(jti) or int(jti.replace("-", ""), 16) == 0:
        _raise("TOKEN_CLAIMS")
    return TokenEvidence(token, expected_issuer, expiration, jti)


def _validate_transport_jwt(
    token: Any,
    config: Config,
    *,
    expected_client_id: str,
    now: int,
    minimum_remaining: int,
    maximum_remaining: int | None = None,
) -> tuple[TokenEvidence, dict[str, Any]]:
    value, header, claims = _read_jwt(token)
    if set(header) != {"alg", "kid", "typ"}:
        _raise("TOKEN_HEADER")
    key_id = header.get("kid")
    if (
        header.get("alg") != TRANSPORT_ALGORITHM
        or header.get("typ") != TRANSPORT_TOKEN_TYPE
        or not isinstance(key_id, str)
        or not re.fullmatch(r"[A-Za-z0-9_.:-]{1,256}", key_id)
    ):
        _raise("TOKEN_HEADER")
    evidence = _validate_temporal_claims(
        value,
        claims,
        expected_issuer=TRANSPORT_ISSUER,
        expected_audience=TRANSPORT_AUDIENCE,
        now=now,
        minimum_remaining=minimum_remaining,
        maximum_remaining=maximum_remaining,
        require_issued_at=True,
    )
    if (
        claims.get("client_id") != expected_client_id
        or claims.get("service") != expected_client_id
        or claims.get("sub") != expected_client_id
        or claims.get("scope") != TRANSPORT_SCOPE
        or claims.get("client_credential_generation") != config.credential_generation
    ):
        _raise("TOKEN_IDENTITY")
    return evidence, claims


def _validate_actor_jwt(
    token: Any,
    config: Config,
    *,
    now: int,
    minimum_remaining: int,
) -> tuple[TokenEvidence, dict[str, Any]]:
    value, header, claims = _read_jwt(token)
    if set(header) != {"alg", "kid", "typ"} or header != {
        "alg": ACTOR_ALGORITHM,
        "kid": config.credential_generation,
        "typ": ACTOR_TOKEN_TYPE,
    }:
        _raise("TOKEN_HEADER")
    if claims.get(GENERATION_CLAIM) != config.credential_generation:
        _raise("TOKEN_CLAIMS")
    evidence = _validate_temporal_claims(
        value,
        claims,
        expected_issuer=config.actor_issuer,
        expected_audience=config.actor_audience,
        now=now,
        minimum_remaining=minimum_remaining,
        require_issued_at=False,
    )
    return evidence, claims


def _parse_transport_token(
    document: dict[str, Any],
    config: Config,
    *,
    expected_client_id: str,
    now: int,
    expiry: bool,
) -> TokenEvidence:
    data = _response_data(document, SERVICE_DATA_KEYS)
    if data.get("tokenType") != "Bearer" or not isinstance(data.get("expiresAtUtc"), str):
        _raise("RESPONSE_SCHEMA")
    evidence, _ = _validate_transport_jwt(
        data.get("accessToken"),
        config,
        expected_client_id=expected_client_id,
        now=now,
        minimum_remaining=2 if expiry else config.minimum_lifetime_seconds,
        maximum_remaining=MAX_EXPIRY_REMAINING_SECONDS if expiry else None,
    )
    try:
        response_expiration = dt.datetime.fromisoformat(
            data["expiresAtUtc"].replace("Z", "+00:00")
        )
    except ValueError:
        _raise("RESPONSE_SCHEMA")
    if response_expiration.tzinfo is None or int(response_expiration.timestamp()) != evidence.expiration:
        _raise("RESPONSE_SCHEMA")
    return evidence


def _parse_user_token(document: dict[str, Any], config: Config, *, now: int) -> TokenEvidence:
    data = _response_data(document, USER_DATA_KEYS)
    credential = data.get("credential")
    identity = data.get("identity")
    if not isinstance(credential, dict) or not isinstance(identity, dict):
        _raise("RESPONSE_SCHEMA")
    if (
        str(credential.get("id", "")).lower() != config.credential_id
        or str(credential.get("tenantId", "")).lower() != config.tenant_id
        or str(identity.get("tenantId", "")).lower() != config.tenant_id
        or credential.get("userName") != config.username
    ):
        _raise("TOKEN_IDENTITY")
    evidence, claims = _validate_actor_jwt(
        data.get("accessToken"),
        config,
        now=now,
        minimum_remaining=config.minimum_lifetime_seconds,
    )
    expires_in = data.get("expiresIn")
    refresh_token = data.get("refreshToken")
    session_id = data.get("sessionId")
    if (
        data.get("tokenType") != "Bearer"
        or isinstance(expires_in, bool)
        or not isinstance(expires_in, int)
        or expires_in <= 0
        or abs((evidence.expiration - now) - expires_in) > 30
        or not isinstance(refresh_token, str)
        or not refresh_token
        or len(refresh_token) > 4096
        or any(character.isspace() or ord(character) < 0x20 for character in refresh_token)
        or not isinstance(session_id, str)
    ):
        _raise("RESPONSE_SCHEMA")
    try:
        parsed_session_id = uuid.UUID(session_id)
    except (ValueError, AttributeError):
        _raise("RESPONSE_SCHEMA")
    if parsed_session_id.int == 0 or str(parsed_session_id) != session_id.lower():
        _raise("RESPONSE_SCHEMA")
    name_claim = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"
    role_claim = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
    try:
        roles = json.loads(claims.get(role_claim, ""))
        normalized_roles = {
            str(uuid.UUID(role))
            for role in roles
            if isinstance(role, str) and GUID.fullmatch(role)
        }
    except (TypeError, ValueError, json.JSONDecodeError):
        _raise("TOKEN_IDENTITY")
    if (
        str(claims.get("credential_id", "")).lower() != config.credential_id
        or str(claims.get(name_claim, "")).lower() != config.credential_id
        or str(claims.get("tenant_id", "")).lower() != config.tenant_id
        or str(claims.get("tenantId", "")).lower() != config.tenant_id
        or config.role_id not in normalized_roles
    ):
        _raise("TOKEN_IDENTITY")
    return evidence


def _validate_destination(path: str) -> tuple[str, str]:
    if not path or not os.path.isabs(path):
        _raise("DESTINATION")
    absolute = os.path.abspath(path)
    if path != absolute:
        _raise("DESTINATION")
    parent = os.path.dirname(absolute)
    name = os.path.basename(absolute)
    if not name or name in (".", "..") or os.path.realpath(parent) != parent:
        _raise("DESTINATION")
    try:
        parent_stat = os.lstat(parent)
        if not stat.S_ISDIR(parent_stat.st_mode) or stat.S_ISLNK(parent_stat.st_mode):
            _raise("DESTINATION")
        if ENFORCE_POSIX_PERMISSIONS:
            if parent_stat.st_uid != os.geteuid():
                _raise("DESTINATION")
            if parent_stat.st_mode & (stat.S_IRWXG | stat.S_IRWXO):
                _raise("DESTINATION")
        if os.path.lexists(absolute):
            target = os.lstat(absolute)
            if not stat.S_ISREG(target.st_mode) or stat.S_ISLNK(target.st_mode):
                _raise("DESTINATION")
            if ENFORCE_POSIX_PERMISSIONS and target.st_uid != os.geteuid():
                _raise("DESTINATION")
            if target.st_nlink != 1 or (
                ENFORCE_POSIX_PERMISSIONS
                and target.st_mode & (stat.S_IRWXG | stat.S_IRWXO | stat.S_IXUSR)
            ):
                _raise("DESTINATION")
    except RefreshError:
        raise
    except OSError:
        _raise("DESTINATION")
    return parent, name


def validate_destinations(paths: list[str]) -> None:
    normalized: set[str] = set()
    identities: set[tuple[int, int]] = set()
    for path in paths:
        _validate_destination(path)
        key = os.path.normcase(os.path.abspath(path))
        if key in normalized:
            _raise("DESTINATION_ALIAS")
        normalized.add(key)
        if os.path.exists(path):
            metadata = os.lstat(path)
            identity = (metadata.st_dev, metadata.st_ino)
            if identity in identities:
                _raise("DESTINATION_ALIAS")
            identities.add(identity)


def atomic_replace(path: str, data: bytes) -> None:
    parent, _ = _validate_destination(path)
    descriptor = -1
    temporary = ""
    try:
        descriptor, temporary = tempfile.mkstemp(prefix=".bolt-phase0-refresh-", dir=parent)
        os.chmod(temporary, stat.S_IRUSR | stat.S_IWUSR)
        with os.fdopen(descriptor, "wb", closefd=True) as stream:
            descriptor = -1
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
        temporary = ""
        os.chmod(path, stat.S_IRUSR | stat.S_IWUSR)
        if hasattr(os, "O_DIRECTORY"):
            parent_fd = os.open(parent, os.O_RDONLY | os.O_DIRECTORY)
            try:
                os.fsync(parent_fd)
            finally:
                os.close(parent_fd)
    except RefreshError:
        raise
    except OSError:
        _raise("ATOMIC_WRITE")
    finally:
        if descriptor >= 0:
            os.close(descriptor)
        if temporary:
            try:
                os.unlink(temporary)
            except OSError:
                pass


def _format_expiration(value: int) -> str:
    return dt.datetime.fromtimestamp(value, dt.timezone.utc).isoformat().replace("+00:00", "Z")


def execute(
    env_path: str,
    receipt_path: str,
    expiry_enabled: bool,
    *,
    connection_factory: ConnectionFactory = http.client.HTTPSConnection,
    context_factory: ContextFactory = _build_ssl_context,
    now_provider: Callable[[], int] = lambda: int(dt.datetime.now(dt.timezone.utc).timestamp()),
) -> None:
    values = parse_protected_env(env_path)
    config = load_config(values)
    validate_destinations(
        [
            config.communications_transport_path,
            config.portal_transport_path,
            config.user_actor_path,
            config.expiry_transport_path,
            receipt_path,
        ]
    )
    now = now_provider()
    communications_body = {
        "clientId": COMMUNICATIONS_CLIENT_ID,
        "clientSecret": config.communications_secret,
    }
    communications_transport = _parse_transport_token(
        _post_json(
            config,
            "/api/service-identity/bolt-transport-token",
            communications_body,
            connection_factory=connection_factory,
            context_factory=context_factory,
        ),
        config,
        expected_client_id=COMMUNICATIONS_CLIENT_ID,
        now=now,
        expiry=False,
    )
    portal_transport = _parse_transport_token(
        _post_json(
            config,
            "/api/service-identity/bolt-transport-token",
            {"clientId": PORTAL_CLIENT_ID, "clientSecret": config.portal_secret},
            connection_factory=connection_factory,
            context_factory=context_factory,
        ),
        config,
        expected_client_id=PORTAL_CLIENT_ID,
        now=now,
        expiry=False,
    )
    expiry_transport = _parse_transport_token(
        _post_json(
            config,
            "/api/service-identity/bolt-transport-token",
            communications_body,
            connection_factory=connection_factory,
            context_factory=context_factory,
        ),
        config,
        expected_client_id=COMMUNICATIONS_CLIENT_ID,
        now=now,
        expiry=True,
    )

    authentication_body = {
        "roleId": config.role_id,
        "authorizationType": config.authorization_type,
        "userName": config.username,
        "password": config.password,
        "generateToken": True,
        "rememberMe": False,
        "metadata": {
            "tenantId": config.tenant_id,
            "credentialId": config.credential_id,
            "name": PRINCIPAL_REFERENCE,
            "deviceName": PRINCIPAL_REFERENCE,
            "deviceAgent": PRINCIPAL_REFERENCE,
            "requestId": str(uuid.uuid4()),
        },
    }
    user_actor = _parse_user_token(
        _post_json(
            config,
            "/api/auth/authenticate",
            authentication_body,
            connection_factory=connection_factory,
            context_factory=context_factory,
        ),
        config,
        now=now,
    )
    tokens = [communications_transport, portal_transport, expiry_transport, user_actor]
    if len({token.value for token in tokens}) != len(tokens) or len({token.jti for token in tokens}) != len(tokens):
        _raise("TOKEN_DISTINCTNESS")

    final_validation_time = now_provider()
    if (
        communications_transport.expiration < final_validation_time + config.minimum_lifetime_seconds
        or portal_transport.expiration < final_validation_time + config.minimum_lifetime_seconds
        or user_actor.expiration < final_validation_time + config.minimum_lifetime_seconds
    ):
        _raise("TOKEN_LIFETIME")
    if expiry_enabled and (
        expiry_transport.expiration <= final_validation_time + 1
        or expiry_transport.expiration > final_validation_time + MAX_EXPIRY_REMAINING_SECONDS
    ):
        _raise("TOKEN_LIFETIME")

    atomic_replace(
        config.communications_transport_path,
        communications_transport.value.encode("ascii") + b"\n",
    )
    atomic_replace(
        config.portal_transport_path,
        portal_transport.value.encode("ascii") + b"\n",
    )
    atomic_replace(config.user_actor_path, user_actor.value.encode("ascii") + b"\n")
    atomic_replace(
        config.expiry_transport_path,
        expiry_transport.value.encode("ascii") + b"\n" if expiry_enabled else b"",
    )

    expirations = {
        "communicationsTransport": _format_expiration(communications_transport.expiration),
        "portalTransport": _format_expiration(portal_transport.expiration),
        "userActor": _format_expiration(user_actor.expiration),
    }
    if expiry_enabled:
        expirations["expiryTransport"] = _format_expiration(expiry_transport.expiration)
    refreshed_at = dt.datetime.fromtimestamp(now_provider(), dt.timezone.utc).replace(microsecond=0)
    receipt = {
        "schemaVersion": RECEIPT_SCHEMA,
        "status": "passed",
        "transportIssuer": TRANSPORT_ISSUER,
        "transportAudience": TRANSPORT_AUDIENCE,
        "actorIssuer": config.actor_issuer,
        "principalReference": PRINCIPAL_REFERENCE,
        "refreshedAtUtc": refreshed_at.isoformat().replace("+00:00", "Z"),
        "tokenExpirationsUtc": expirations,
    }
    atomic_replace(
        receipt_path,
        (json.dumps(receipt, separators=(",", ":"), sort_keys=True) + "\n").encode("utf-8"),
    )


def main() -> int:
    try:
        env_path = os.environ.get("XFRAMEWORK_ENV_FILE", "")
        receipt_path = os.environ.get("BOLT_SYNTHETIC_REFRESH_RECEIPT", "")
        expiry_raw = os.environ.get("BOLT_SYNTHETIC_EXPIRY_ENABLED", "")
        if expiry_raw not in ("true", "false"):
            _raise("PROCESS_ENVIRONMENT")
        execute(env_path, receipt_path, expiry_raw == "true")
        return 0
    except RefreshError as error:
        os.write(2, f"BOLT_PHASE0_REFRESH_{error.code}\n".encode("ascii"))
        return 1
    except BaseException:
        os.write(2, b"BOLT_PHASE0_REFRESH_INTERNAL\n")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
