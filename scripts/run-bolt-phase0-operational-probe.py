#!/usr/bin/env python3
"""Run one silent, first-party Bolt Phase 0 operational probe."""

from __future__ import annotations

import base64
import datetime as dt
import http.client
import importlib.util
import json
import os
import re
import signal
import socket
import ssl
import stat
import subprocess
import tempfile
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable, Mapping
from urllib.parse import urlsplit


MAX_FILE_BYTES = 1024 * 1024
MAX_SECRET_BYTES = 16 * 1024
MAX_PROCESS_OUTPUT_BYTES = 64 * 1024
MAX_HTTP_RESPONSE_BYTES = 1024 * 1024
PROCESS_TIMEOUT_SECONDS = 20
HTTP_TIMEOUT_SECONDS = 15
RECOVERY_ATTEMPTS = 20
RECOVERY_INTERVAL_SECONDS = 1.0
RECEIPT_SCHEMA = "bolt-phase0-probe-receipt/v1"
DURABLE_RECEIPT_SCHEMA = "bolt-phase0-post-recovery-durable/v1"
COMPOSE_NAME = re.compile(r"^[a-z0-9][a-z0-9_-]{0,62}$")
SERVICE_NAME = re.compile(r"^[a-z0-9][a-z0-9_-]{0,62}$")
CONTAINER_ID = re.compile(r"^[0-9a-f]{64}$")
SAFE_PATH = re.compile(r"^/[A-Za-z0-9._~!$&'()+,;=:@%/-]+$")
JWT = re.compile(r"^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$")
ENFORCE_POSIX_PERMISSIONS = os.name == "posix" and hasattr(os, "geteuid")
DOCKER_INSPECT_FORMAT = (
    '{"id":{{json .Id}},"running":{{json .State.Running}},'
    '"paused":{{json .State.Paused}},"status":{{json .State.Status}},'
    '"health":{{with index .State "Health"}}{{json (index . "Status")}}{{else}}null{{end}},'
    '"labels":{{json .Config.Labels}}}'
)

ASSERTIONS = {
    "plaintext-rejection": {"plaintextRejected": True, "bearerSent": False},
    "redis-interruption": {
        "interruptionInduced": True,
        "recovered": True,
        "postRecoverySyntheticPassed": True,
        "dataLossObserved": False,
    },
    "old-generation-rejection": {
        "oldUserTokenRejected": True,
        "oldServiceTokenRejected": True,
        "oldClientSecretRejected": True,
        "currentHttpHealthPassed": True,
        "currentBoltHealthPassed": True,
    },
}


class ProbeError(Exception):
    """A redacted operational-probe failure."""


@dataclass(frozen=True)
class ProcessResult:
    returncode: int
    stdout: bytes
    stderr: bytes


@dataclass(frozen=True)
class HttpResult:
    status: int
    headers: Mapping[str, str]
    body: bytes


@dataclass(frozen=True)
class TlsTarget:
    host: str
    port: int
    ca_path: str


ProcessRunner = Callable[[list[str], float, Mapping[str, str] | None], ProcessResult]
HttpRequester = Callable[[TlsTarget, str, str, bytes | None, Mapping[str, str]], HttpResult]
Sleeper = Callable[[float], None]


def _fail(code: str) -> None:
    raise ProbeError(code)


def _validate_private_regular_file(
    path: str,
    *,
    maximum: int,
    executable: bool = False,
    allow_empty: bool = False,
) -> os.stat_result:
    if not path or not os.path.isabs(path) or os.path.realpath(path) != os.path.abspath(path):
        _fail("PRIVATE_FILE")
    try:
        metadata = os.lstat(path)
    except OSError:
        _fail("PRIVATE_FILE")
    if not stat.S_ISREG(metadata.st_mode) or stat.S_ISLNK(metadata.st_mode):
        _fail("PRIVATE_FILE")
    if metadata.st_nlink != 1 or metadata.st_size > maximum or (not allow_empty and metadata.st_size <= 0):
        _fail("PRIVATE_FILE")
    if ENFORCE_POSIX_PERMISSIONS:
        if metadata.st_uid != os.geteuid() or metadata.st_mode & (stat.S_IRWXG | stat.S_IRWXO):
            _fail("PRIVATE_FILE")
        if executable:
            if not metadata.st_mode & stat.S_IXUSR:
                _fail("PRIVATE_FILE")
        elif metadata.st_mode & stat.S_IXUSR or not metadata.st_mode & stat.S_IRUSR:
            _fail("PRIVATE_FILE")
    return metadata


def _read_private_file(path: str, *, maximum: int) -> bytes:
    before = _validate_private_regular_file(path, maximum=maximum)
    flags = os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0) | getattr(os, "O_BINARY", 0)
    try:
        descriptor = os.open(path, flags)
        try:
            current = os.fstat(descriptor)
            if (current.st_dev, current.st_ino) != (before.st_dev, before.st_ino):
                _fail("PRIVATE_FILE")
            data = os.read(descriptor, maximum + 1)
        finally:
            os.close(descriptor)
    except ProbeError:
        raise
    except OSError:
        _fail("PRIVATE_FILE")
    if len(data) != current.st_size or len(data) > maximum:
        _fail("PRIVATE_FILE")
    return data


def _load_env_parser(path: Path) -> Any:
    if not path.is_absolute() or path.resolve() != path or not path.is_file():
        _fail("ENV_PARSER")
    spec = importlib.util.spec_from_file_location("bolt_phase0_operational_env", path)
    if spec is None or spec.loader is None:
        _fail("ENV_PARSER")
    module = importlib.util.module_from_spec(spec)
    try:
        spec.loader.exec_module(module)
    except Exception:
        _fail("ENV_PARSER")
    if not callable(getattr(module, "parse_env", None)) or not callable(
        getattr(module, "typed_value", None)
    ):
        _fail("ENV_PARSER")
    return module


def load_protected_env(env_path: str, parser_path: Path | None = None) -> tuple[dict[str, str], Any]:
    before = _validate_private_regular_file(env_path, maximum=MAX_FILE_BYTES)
    parser = parser_path or Path(__file__).resolve().with_name("verify-bolt-phase0-env.py")
    module = _load_env_parser(parser)
    try:
        values = module.parse_env(Path(env_path))
    except (OSError, ValueError):
        _fail("ENV_SYNTAX")
    try:
        after = os.lstat(env_path)
    except OSError:
        _fail("ENV_FILE_CHANGED")
    if (
        (before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns)
        != (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns)
        or not stat.S_ISREG(after.st_mode)
    ):
        _fail("ENV_FILE_CHANGED")
    if not isinstance(values, dict) or any(
        not isinstance(key, str) or not isinstance(value, str) for key, value in values.items()
    ):
        _fail("ENV_SYNTAX")
    return values, module


def _required(values: Mapping[str, str], key: str) -> str:
    value = values.get(key)
    if value is None or value == "":
        _fail(f"CONFIG_{key}")
    return value


def _typed(module: Any, values: Mapping[str, str], key: str, value_type: str | None = None) -> str:
    value = _required(values, key)
    try:
        return module.typed_value(key, value, value_type)
    except ValueError:
        _fail(f"CONFIG_{key}")


def _safe_name(values: Mapping[str, str], key: str, pattern: re.Pattern[str]) -> str:
    value = _required(values, key)
    if not pattern.fullmatch(value):
        _fail(f"CONFIG_{key}")
    return value


def _absolute_path(module: Any, values: Mapping[str, str], key: str) -> str:
    return _typed(module, values, key, "absolute-path")


def _default_process_runner(
    command: list[str], timeout_seconds: float, environment: Mapping[str, str] | None
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
                env=dict(environment) if environment is not None else None,
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


def _run_checked(
    runner: ProcessRunner,
    command: list[str],
    *,
    timeout: float = PROCESS_TIMEOUT_SECONDS,
    environment: Mapping[str, str] | None = None,
    allow_stdout: bool = False,
) -> ProcessResult:
    result = runner(command, timeout, environment)
    if not isinstance(result, ProcessResult):
        _fail("SUBPROCESS_RESULT")
    if result.returncode != 0 or result.stderr or (result.stdout and not allow_stdout):
        _fail("SUBPROCESS_FAILED")
    if len(result.stdout) > MAX_PROCESS_OUTPUT_BYTES or len(result.stderr) > MAX_PROCESS_OUTPUT_BYTES:
        _fail("SUBPROCESS_OUTPUT")
    return result


def _parse_single_json(data: bytes, code: str) -> dict[str, Any]:
    if not data or len(data) > MAX_FILE_BYTES:
        _fail(code)
    try:
        value = json.loads(data.decode("utf-8", errors="strict"))
    except (UnicodeDecodeError, json.JSONDecodeError, ValueError):
        _fail(code)
    if not isinstance(value, dict):
        _fail(code)
    return value


def _resolve_container(
    project: str,
    service: str,
    runner: ProcessRunner,
    *,
    require_running: bool,
    require_healthy: bool,
) -> tuple[str, dict[str, Any]]:
    result = _run_checked(
        runner,
        [
            "docker",
            "container",
            "ls",
            "-aq",
            "--no-trunc",
            "--filter",
            f"label=com.docker.compose.project={project}",
            "--filter",
            f"label=com.docker.compose.service={service}",
        ],
        allow_stdout=True,
    )
    ids = result.stdout.decode("ascii", errors="strict").splitlines()
    if len(ids) != 1 or not CONTAINER_ID.fullmatch(ids[0]):
        _fail("COMPOSE_CONTAINER")
    container_id = ids[0]
    inspect = _run_checked(
        runner,
        ["docker", "inspect", "--format", DOCKER_INSPECT_FORMAT, container_id],
        allow_stdout=True,
    )
    document = _parse_single_json(inspect.stdout, "COMPOSE_INSPECT")
    labels = document.get("labels")
    if (
        document.get("id") != container_id
        or not isinstance(labels, dict)
        or labels.get("com.docker.compose.project") != project
        or labels.get("com.docker.compose.service") != service
    ):
        _fail("COMPOSE_IDENTITY")
    if require_running and (document.get("running") is not True or document.get("paused") is not False):
        _fail("COMPOSE_RUNTIME")
    if require_healthy and document.get("health") != "healthy":
        _fail("COMPOSE_HEALTH")
    return container_id, document


def run_plaintext_rejection(values: Mapping[str, str], runner: ProcessRunner) -> dict[str, bool]:
    project = _safe_name(values, "BOLT_SYNTHETIC_COMPOSE_PROJECT_NAME", COMPOSE_NAME)
    peer_service = _safe_name(values, "BOLT_SYNTHETIC_PLAINTEXT_PEER_SERVICE", SERVICE_NAME)
    if peer_service in {"bolt-hub", "redis", "bolt-phase0-synthetics"}:
        _fail("PLAINTEXT_PEER")
    peer_id, _ = _resolve_container(
        project, peer_service, runner, require_running=True, require_healthy=True
    )
    command = [
        "docker",
        "exec",
        peer_id,
        "curl",
        "--silent",
        "--show-error",
        "--output",
        "/dev/null",
        "--write-out",
        "%{http_code}",
        "--connect-timeout",
        "5",
        "--max-time",
        "10",
        "--http1.1",
        "--header",
        "Connection: Upgrade",
        "--header",
        "Upgrade: websocket",
        "--header",
        "Sec-WebSocket-Version: 13",
        "--header",
        "Sec-WebSocket-Key: MDEyMzQ1Njc4OWFiY2RlZg==",
        "http://bolt-hub:8080/bolt/ws",
    ]
    if any("authorization" in argument.lower() or "bearer" in argument.lower() for argument in command):
        _fail("PLAINTEXT_BEARER")
    result = runner(command, 15, None)
    if not isinstance(result, ProcessResult):
        _fail("SUBPROCESS_RESULT")
    if len(result.stdout) > MAX_PROCESS_OUTPUT_BYTES or len(result.stderr) > MAX_PROCESS_OUTPUT_BYTES:
        _fail("SUBPROCESS_OUTPUT")
    if result.returncode == 7 and not result.stdout:
        return dict(ASSERTIONS["plaintext-rejection"])
    if result.returncode != 0 or result.stderr:
        _fail("PLAINTEXT_RESPONSE")
    try:
        status = int(result.stdout.decode("ascii", errors="strict"))
    except (UnicodeDecodeError, ValueError):
        _fail("PLAINTEXT_RESPONSE")
    if status not in {400, 401, 403, 426}:
        _fail("PLAINTEXT_ACCEPTED")
    return dict(ASSERTIONS["plaintext-rejection"])


def _inspect_container(container_id: str, runner: ProcessRunner) -> dict[str, Any]:
    result = _run_checked(
        runner,
        ["docker", "inspect", "--format", DOCKER_INSPECT_FORMAT, container_id],
        allow_stdout=True,
    )
    return _parse_single_json(result.stdout, "COMPOSE_INSPECT")


def _recover_redis(
    project: str, container_id: str, runner: ProcessRunner, sleeper: Sleeper
) -> None:
    _run_checked(runner, ["docker", "start", container_id], allow_stdout=True)
    for attempt in range(RECOVERY_ATTEMPTS):
        resolved_id, state = _resolve_container(
            project, "redis", runner, require_running=False, require_healthy=False
        )
        if resolved_id != container_id:
            _fail("REDIS_IDENTITY_CHANGED")
        if (
            state.get("running") is True
            and state.get("paused") is False
            and state.get("health") == "healthy"
        ):
            return
        if attempt + 1 < RECOVERY_ATTEMPTS:
            sleeper(RECOVERY_INTERVAL_SECONDS)
    _fail("REDIS_RECOVERY")


def _validate_private_receipt(path: Path, schema: str, assertions: Mapping[str, bool]) -> None:
    _validate_private_regular_file(str(path), maximum=MAX_FILE_BYTES)
    document = _parse_single_json(_read_private_file(str(path), maximum=MAX_FILE_BYTES), "ATTESTATION")
    if set(document) != {"schemaVersion", "status", "assertions"}:
        _fail("ATTESTATION_SCHEMA")
    if (
        document.get("schemaVersion") != schema
        or document.get("status") != "passed"
        or document.get("assertions") != assertions
    ):
        _fail("ATTESTATION_FAILED")


def _run_post_recovery_probe(
    command_path: str,
    env_file: str,
    manifest_path: str,
    receipt_parent: Path,
    runner: ProcessRunner,
) -> None:
    _validate_private_regular_file(command_path, maximum=MAX_FILE_BYTES, executable=True)
    with tempfile.TemporaryDirectory(prefix=".redis-post-recovery-", dir=receipt_parent) as directory:
        os.chmod(directory, 0o700)
        nested_receipt = Path(directory) / "receipt.json"
        environment = {
            "PATH": "/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
            "HOME": os.environ.get("HOME", "/tmp"),
            "XFRAMEWORK_ENV_FILE": env_file,
            "BOLT_SYNTHETIC_TOKEN_MANIFEST": manifest_path,
            "BOLT_SYNTHETIC_DURABLE_PROBE_MODE": "post-recovery",
            "BOLT_SYNTHETIC_POST_RECOVERY_RECEIPT": str(nested_receipt),
        }
        result = _run_checked(
            runner,
            [command_path],
            timeout=120,
            environment=environment,
            allow_stdout=True,
        )
        if result.stdout:
            _fail("POST_RECOVERY_OUTPUT")
        _validate_private_receipt(
            nested_receipt,
            DURABLE_RECEIPT_SCHEMA,
            {"durableStateVerified": True, "dataLossObserved": False},
        )


def run_redis_interruption(
    values: Mapping[str, str],
    module: Any,
    env_file: str,
    manifest_path: str,
    receipt_parent: Path,
    runner: ProcessRunner,
    sleeper: Sleeper,
) -> dict[str, bool]:
    project = _safe_name(values, "BOLT_SYNTHETIC_COMPOSE_PROJECT_NAME", COMPOSE_NAME)
    post_recovery = _absolute_path(
        module, values, "BOLT_SYNTHETIC_REDIS_POST_RECOVERY_COMMAND_PATH"
    )
    container_id, _ = _resolve_container(
        project, "redis", runner, require_running=True, require_healthy=True
    )
    stop_attempted = False
    interruption_induced = False
    recovery_error: ProbeError | None = None
    try:
        stop_attempted = True
        _run_checked(
            runner,
            ["docker", "stop", "--time", "10", container_id],
            timeout=20,
            allow_stdout=True,
        )
        stopped = _inspect_container(container_id, runner)
        if stopped.get("running") is not False or stopped.get("paused") is not False:
            _fail("REDIS_INTERRUPTION")
        interruption_induced = True
    finally:
        if stop_attempted:
            try:
                _recover_redis(project, container_id, runner, sleeper)
            except ProbeError as error:
                recovery_error = error
    if recovery_error is not None:
        raise recovery_error
    if not interruption_induced:
        _fail("REDIS_INTERRUPTION")
    _run_post_recovery_probe(post_recovery, env_file, manifest_path, receipt_parent, runner)
    return dict(ASSERTIONS["redis-interruption"])


def _strict_tls_context(ca_path: str) -> ssl.SSLContext:
    try:
        context = ssl.create_default_context(purpose=ssl.Purpose.SERVER_AUTH, cafile=ca_path)
    except (OSError, ssl.SSLError, ValueError):
        _fail("TLS_CONFIGURATION")
    context.check_hostname = True
    context.verify_mode = ssl.CERT_REQUIRED
    context.minimum_version = ssl.TLSVersion.TLSv1_2
    return context


def _default_http_request(
    target: TlsTarget,
    method: str,
    path: str,
    body: bytes | None,
    headers: Mapping[str, str],
) -> HttpResult:
    context = _strict_tls_context(target.ca_path)
    connection = http.client.HTTPSConnection(
        target.host, target.port, context=context, timeout=HTTP_TIMEOUT_SECONDS
    )
    try:
        connection.request(method, path, body=body, headers=dict(headers))
        response = connection.getresponse()
        content_length = response.getheader("Content-Length")
        if content_length is not None:
            try:
                if int(content_length) < 0 or int(content_length) > MAX_HTTP_RESPONSE_BYTES:
                    _fail("HTTP_RESPONSE")
            except ValueError:
                _fail("HTTP_RESPONSE")
        data = response.read(MAX_HTTP_RESPONSE_BYTES + 1)
        if len(data) > MAX_HTTP_RESPONSE_BYTES:
            _fail("HTTP_RESPONSE")
        response_headers = {name.lower(): value for name, value in response.getheaders()}
        return HttpResult(response.status, response_headers, data)
    except ProbeError:
        raise
    except (OSError, socket.error, ssl.SSLError, http.client.HTTPException):
        _fail("HTTPS_CONNECTION")
    finally:
        try:
            connection.close()
        except Exception:
            pass


def _parse_https_origin(value: str) -> tuple[str, int]:
    try:
        parsed = urlsplit(value)
        port = parsed.port or 443
    except ValueError:
        _fail("HTTPS_ORIGIN")
    if (
        parsed.scheme.lower() != "https"
        or not parsed.hostname
        or parsed.username is not None
        or parsed.password is not None
        or parsed.query
        or parsed.fragment
        or parsed.path not in {"", "/"}
        or not 1 <= port <= 65535
    ):
        _fail("HTTPS_ORIGIN")
    return parsed.hostname.lower(), port


def _relative_path(value: str) -> str:
    if not SAFE_PATH.fullmatch(value) or "//" in value or any(
        part in {"", ".", ".."} for part in value.split("/")[1:]
    ):
        _fail("HTTP_PATH")
    return value


def _read_ca(path: str) -> None:
    if not os.path.isabs(path) or os.path.realpath(path) != os.path.abspath(path):
        _fail("CA_FILE")
    try:
        metadata = os.lstat(path)
    except OSError:
        _fail("CA_FILE")
    if not stat.S_ISREG(metadata.st_mode) or stat.S_ISLNK(metadata.st_mode):
        _fail("CA_FILE")
    if (
        metadata.st_size <= 0
        or metadata.st_size > MAX_FILE_BYTES
        or ENFORCE_POSIX_PERMISSIONS and metadata.st_mode & (stat.S_IWGRP | stat.S_IWOTH)
    ):
        _fail("CA_FILE")


def _manifest_tokens(manifest_path: str) -> dict[str, str]:
    document = _parse_single_json(
        _read_private_file(manifest_path, maximum=MAX_FILE_BYTES), "TOKEN_MANIFEST"
    )
    if document.get("schemaVersion") != "bolt-phase0-token-manifest/v1":
        _fail("TOKEN_MANIFEST")
    entries = document.get("tokens")
    if not isinstance(entries, list):
        _fail("TOKEN_MANIFEST")
    paths: dict[str, str] = {}
    for entry in entries:
        if not isinstance(entry, dict):
            _fail("TOKEN_MANIFEST")
        purpose = entry.get("purpose")
        path = entry.get("path")
        if not isinstance(purpose, str) or not isinstance(path, str) or purpose in paths:
            _fail("TOKEN_MANIFEST")
        paths[purpose] = path
    required = {"communications", "user", "rejected_communications", "rejected_user"}
    if not required.issubset(paths):
        _fail("TOKEN_MANIFEST")
    return paths


def _read_jwt(path: str) -> str:
    raw = _read_private_file(path, maximum=MAX_SECRET_BYTES).strip()
    try:
        value = raw.decode("ascii", errors="strict")
    except UnicodeDecodeError:
        _fail("TOKEN_FILE")
    if not JWT.fullmatch(value):
        _fail("TOKEN_FILE")
    return value


def _jwt_generation(token: str) -> str:
    segment = token.split(".")[1]
    try:
        payload = base64.urlsafe_b64decode(segment + "=" * (-len(segment) % 4))
        claims = json.loads(payload.decode("utf-8", errors="strict"))
    except (ValueError, UnicodeDecodeError, json.JSONDecodeError):
        _fail("TOKEN_CLAIMS")
    generation = claims.get("credential_generation") if isinstance(claims, dict) else None
    if not isinstance(generation, str) or not re.fullmatch(r"[A-Za-z0-9_.:-]{1,96}", generation):
        _fail("TOKEN_CLAIMS")
    return generation


def _websocket_upgrade(
    requester: HttpRequester, target: TlsTarget, token: str
) -> HttpResult:
    return requester(
        target,
        "GET",
        "/bolt/ws",
        None,
        {
            "Authorization": f"Bearer {token}",
            "Connection": "Upgrade",
            "Upgrade": "websocket",
            "Sec-WebSocket-Version": "13",
            "Sec-WebSocket-Key": "MDEyMzQ1Njc4OWFiY2RlZg==",
        },
    )


def _is_websocket_upgrade(result: HttpResult) -> bool:
    return (
        result.status == 101
        and result.headers.get("upgrade", "").lower() == "websocket"
        and "upgrade" in result.headers.get("connection", "").lower()
    )


def run_old_generation_rejection(
    values: Mapping[str, str],
    module: Any,
    manifest_path: str,
    requester: HttpRequester,
) -> dict[str, bool]:
    hub_ca = _absolute_path(module, values, "BOLT_HUB_TLS_CA_PATH")
    identity_ca = _absolute_path(module, values, "BOLT_SYNTHETIC_IDENTITYSERVER_CA_PATH")
    _read_ca(hub_ca)
    _read_ca(identity_ca)
    hub_target = TlsTarget(
        host=_typed(module, values, "BOLT_HUB_PUBLIC_HOSTNAME"),
        port=int(_typed(module, values, "BOLT_HUB_EXPOSE_PORT")),
        ca_path=hub_ca,
    )
    identity_host, identity_port = _parse_https_origin(
        _required(values, "BOLT_SYNTHETIC_IDENTITYSERVER_BASE_URL")
    )
    identity_target = TlsTarget(identity_host, identity_port, identity_ca)
    identity_path = _relative_path(
        _required(values, "IDENTITYSERVER_BOLT_TRANSPORT_TOKEN_PATH")
    )
    retired_secret_path = _absolute_path(
        module, values, "BOLT_SYNTHETIC_REJECTED_CLIENT_SECRET_PATH"
    )
    retired_secret_bytes = _read_private_file(retired_secret_path, maximum=MAX_SECRET_BYTES).strip()
    try:
        retired_secret = retired_secret_bytes.decode("utf-8", errors="strict")
    except UnicodeDecodeError:
        _fail("RETIRED_SECRET")
    current_secret = _required(values, "COMMUNICATIONS_SERVICE_IDENTITY_SECRET")
    if (
        len(retired_secret) < 32
        or len(retired_secret) > 4096
        or any(character.isspace() for character in retired_secret)
        or retired_secret == current_secret
    ):
        _fail("RETIRED_SECRET")

    token_paths = _manifest_tokens(manifest_path)
    current_user = _read_jwt(token_paths["user"])
    old_user = _read_jwt(token_paths["rejected_user"])
    old_service = _read_jwt(token_paths["rejected_communications"])
    current_generation = _jwt_generation(current_user)
    if (
        _jwt_generation(old_user) != _jwt_generation(old_service)
        or _jwt_generation(old_user) == current_generation
    ):
        _fail("TOKEN_GENERATION")

    old_user_response = _websocket_upgrade(requester, hub_target, old_user)
    old_service_response = _websocket_upgrade(requester, hub_target, old_service)
    if old_user_response.status not in {401, 403} or old_service_response.status not in {401, 403}:
        _fail("OLD_TOKEN_ACCEPTED")

    secret_body = json.dumps(
        {"clientId": "XFramework.Communications", "clientSecret": retired_secret},
        separators=(",", ":"),
        ensure_ascii=True,
    ).encode("utf-8")
    secret_response = requester(
        identity_target,
        "POST",
        identity_path,
        secret_body,
        {
            "Accept": "application/json",
            "Accept-Encoding": "identity",
            "Content-Type": "application/json; charset=utf-8",
            "Content-Length": str(len(secret_body)),
        },
    )
    if secret_response.status not in {401, 403}:
        _fail("OLD_CLIENT_SECRET_ACCEPTED")

    for health_path in ("/health/live", "/health/ready"):
        health = requester(
            hub_target,
            "GET",
            health_path,
            None,
            {"Accept": "application/json", "Accept-Encoding": "identity"},
        )
        if health.status != 200:
            _fail("CURRENT_HTTP_HEALTH")
    current_bolt = _websocket_upgrade(requester, hub_target, current_user)
    if not _is_websocket_upgrade(current_bolt):
        _fail("CURRENT_BOLT_HEALTH")
    return dict(ASSERTIONS["old-generation-rejection"])


def _utc_timestamp(value: dt.datetime) -> str:
    if value.tzinfo is None:
        _fail("CLOCK")
    return value.astimezone(dt.timezone.utc).isoformat(timespec="milliseconds").replace("+00:00", "Z")


def _validate_receipt_parent(path: Path) -> None:
    if not path.is_absolute() or path.parent.resolve() != path.parent:
        _fail("RECEIPT_PATH")
    try:
        metadata = os.lstat(path.parent)
    except OSError:
        _fail("RECEIPT_PATH")
    if not stat.S_ISDIR(metadata.st_mode) or stat.S_ISLNK(metadata.st_mode):
        _fail("RECEIPT_PATH")
    if ENFORCE_POSIX_PERMISSIONS and (
        metadata.st_uid != os.geteuid() or metadata.st_mode & (stat.S_IRWXG | stat.S_IRWXO)
    ):
        _fail("RECEIPT_PATH")


def write_atomic_receipt(path: Path, document: Mapping[str, Any]) -> None:
    _validate_receipt_parent(path)
    if path.exists() or path.is_symlink():
        _fail("RECEIPT_EXISTS")
    serialized = (json.dumps(document, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")
    if len(serialized) > MAX_FILE_BYTES:
        _fail("RECEIPT_SIZE")
    descriptor = -1
    temporary = ""
    try:
        descriptor, temporary = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
        os.fchmod(descriptor, 0o600)
        written = 0
        while written < len(serialized):
            written += os.write(descriptor, serialized[written:])
        os.fsync(descriptor)
        os.close(descriptor)
        descriptor = -1
        os.replace(temporary, path)
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
    _validate_private_regular_file(str(path), maximum=MAX_FILE_BYTES)


def run_probe(
    kind: str,
    stage: str,
    env_file: str,
    manifest_path: str,
    receipt_path: Path,
    *,
    runner: ProcessRunner = _default_process_runner,
    requester: HttpRequester = _default_http_request,
    sleeper: Sleeper = time.sleep,
    now: Callable[[], dt.datetime] = lambda: dt.datetime.now(dt.timezone.utc),
    parser_path: Path | None = None,
) -> None:
    if kind not in ASSERTIONS:
        _fail("PROBE_KIND")
    if kind == "redis-interruption" and stage != "canary":
        _fail("PROBE_STAGE")
    if kind == "old-generation-rejection" and stage != "finalized":
        _fail("PROBE_STAGE")
    _validate_receipt_parent(receipt_path)
    started = now()
    values, module = load_protected_env(env_file, parser_path)
    if kind == "plaintext-rejection":
        assertions = run_plaintext_rejection(values, runner)
    elif kind == "redis-interruption":
        assertions = run_redis_interruption(
            values,
            module,
            env_file,
            manifest_path,
            receipt_path.parent,
            runner,
            sleeper,
        )
    else:
        assertions = run_old_generation_rejection(values, module, manifest_path, requester)
    if assertions != ASSERTIONS[kind]:
        _fail("ASSERTIONS")
    completed = now()
    if completed < started:
        _fail("CLOCK")
    receipt = {
        "schemaVersion": RECEIPT_SCHEMA,
        "probe": kind,
        "status": "passed",
        "startedAtUtc": _utc_timestamp(started),
        "completedAtUtc": _utc_timestamp(completed),
        "assertions": assertions,
    }
    write_atomic_receipt(receipt_path, receipt)


def main() -> int:
    previous_handlers: dict[int, Any] = {}

    def terminate(_signum: int, _frame: Any) -> None:
        raise ProbeError("TERMINATED")

    try:
        supported_signals = tuple(
            value
            for name in ("SIGTERM", "SIGINT", "SIGHUP")
            if (value := getattr(signal, name, None)) is not None
        )
        for signal_number in supported_signals:
            previous_handlers[signal_number] = signal.signal(signal_number, terminate)
        kind = os.environ.get("BOLT_SYNTHETIC_PROBE_KIND", "")
        stage = os.environ.get("BOLT_SYNTHETIC_STAGE", "")
        env_file = os.environ.get("XFRAMEWORK_ENV_FILE", "")
        manifest_path = os.environ.get("BOLT_SYNTHETIC_TOKEN_MANIFEST", "")
        receipt = os.environ.get("BOLT_SYNTHETIC_PROBE_RECEIPT", "")
        if not env_file or not manifest_path or not receipt:
            _fail("HOOK_ENVIRONMENT")
        run_probe(kind, stage, env_file, manifest_path, Path(receipt))
        return 0
    except Exception:
        return 1
    finally:
        for signal_number, previous in previous_handlers.items():
            signal.signal(signal_number, previous)


if __name__ == "__main__":
    raise SystemExit(main())
