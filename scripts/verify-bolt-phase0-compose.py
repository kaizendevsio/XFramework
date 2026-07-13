#!/usr/bin/env python3
"""Fail-closed semantic verification for the Bolt Phase 0 Compose manifest."""

from __future__ import annotations

import argparse
import ipaddress
import json
import os
import re
import runpy
import shutil
import socket
import stat
import subprocess
import sys
import tempfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any
from urllib.parse import urlparse


CLIENT_SERVICES = (
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
SERVICE_IDENTITY_CLIENT_SCOPE_MATRIX = {
    "XFramework.IdentityServer": ("bolt.service",),
    "XFramework.Portal": (
        "bolt.service",
        "datacontext.query",
        "datacontext.mutate",
        "identity.admin",
    ),
    "XFramework.Bolt.Hub": ("bolt.service",),
    "XFramework.Communications": ("bolt.service",),
    "XFramework.Notifications": ("bolt.service",),
    "XFramework.Storage": ("bolt.service",),
    "XFramework.Attendance": ("bolt.service",),
    "XFramework.SmsGateway": ("bolt.service",),
    "XFramework.Wallets": ("bolt.service",),
    "XFramework.Inventario": ("bolt.service",),
    "XFramework.POS": ("bolt.service",),
    "XFramework.Operations.Dashboard": ("bolt.service",),
}
SERVICE_IDENTITY_RUNTIME_DEFAULT_SCOPES = {
    "identityserver": ("bolt.service",),
    "portal": ("bolt.service", "identity.admin"),
    "bolt-hub": ("bolt.service",),
    "communications": ("bolt.service",),
    "notifications": ("bolt.service",),
    "storage": ("bolt.service",),
    "attendance": ("bolt.service",),
    "smsgateway": ("bolt.service",),
    "wallets": ("bolt.service",),
    "inventario": ("bolt.service",),
    "pos": ("bolt.service",),
    "operations-dashboard": ("bolt.service",),
}
SERVICE_IDENTITY_DEFAULT_SCOPE_KEY = re.compile(r"^ServiceIdentity__DefaultScopes__(\d+)$")


def write_private_json(path: Path, value: Any) -> None:
    payload = (json.dumps(value, indent=2, sort_keys=True) + "\n").encode("utf-8")
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
    temporary = Path(temporary_name)
    try:
        os.fchmod(descriptor, stat.S_IRUSR | stat.S_IWUSR)
        with os.fdopen(descriptor, "wb") as stream:
            descriptor = -1
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
        os.chmod(path, stat.S_IRUSR | stat.S_IWUSR)
    finally:
        if descriptor >= 0:
            os.close(descriptor)
        temporary.unlink(missing_ok=True)
INACTIVE_SERVICES = ("bolt-phase0-synthetics",)
SECURE_URL = "wss://bolt-hub:8443/bolt/ws"
DIGEST = re.compile(r"^sha256:[0-9a-f]{64}$")
COMMIT_SHA = re.compile(r"^[0-9a-f]{40}$")
WORKFLOW_RUN_ID = re.compile(r"^[1-9][0-9]{0,31}$")
GITHUB_ACTOR = re.compile(r"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?$")
GITHUB_ACTOR_ID = re.compile(r"^[1-9][0-9]{0,31}$")
DIRECT_PUBLICATION_ATTESTATION = "ATTEST_DIRECT_KESTREL_NO_INTERMEDIARY"
ALL_INTERFACE_BINDINGS = frozenset({"", "0.0.0.0", "::"})
CA_SECRET = "bolt-hub-ca"
FULLCHAIN_SECRET = "bolt-hub-tls-fullchain"
PRIVATE_KEY_SECRET = "bolt-hub-tls-private-key"
IDENTITY_CA_SECRET = "identityserver-ca"
IDENTITY_FULLCHAIN_SECRET = "identityserver-tls-fullchain"
IDENTITY_PRIVATE_KEY_SECRET = "identityserver-tls-private-key"
IDENTITY_TOKEN_PATH = "/api/service-identity/bolt-transport-token"
EXPECTED_ENDPOINT_ENV = {
    "Kestrel__Endpoints__Http__Url": "http://127.0.0.1:8080",
    "Kestrel__Endpoints__Https__Url": "https://0.0.0.0:8443",
    "Kestrel__Endpoints__Https__Certificate__Path": "/run/secrets/bolt-hub-tls-fullchain.pem",
    "Kestrel__Endpoints__Https__Certificate__KeyPath": "/run/secrets/bolt-hub-tls-private-key.pem",
}
EXPECTED_ASPNETCORE_URLS = {"http://127.0.0.1:8080", "https://+:8443"}
IDENTITY_EXPECTED_ENDPOINT_ENV = {
    "Kestrel__Endpoints__Http__Url": "http://127.0.0.1:8080",
    "Kestrel__Endpoints__Https__Url": "https://0.0.0.0:8443",
    "Kestrel__Endpoints__Https__Certificate__Path": "/run/secrets/identityserver-tls-fullchain.pem",
    "Kestrel__Endpoints__Https__Certificate__KeyPath": "/run/secrets/identityserver-tls-private-key.pem",
}
IDENTITY_EXPECTED_ASPNETCORE_URLS = {"http://127.0.0.1:8080", "https://+:8443"}
IDENTITY_ISSUER_ENV = {
    "ServiceIdentity__BoltTransportTokenIssuer__Enabled": "true",
    "ServiceIdentity__BoltTransportTokenIssuer__LifetimeSeconds": "120",
}
PHASE0_QUOTAS = {
    "BoltConfiguration__QueueDepth": "256",
    "BoltConfiguration__RpcTimeoutSeconds": "30",
    "BoltConfiguration__MaxFrameBytes": "8388608",
    "BoltConfiguration__SendQueueCapacity": "0",
    "BoltConfiguration__SendEnqueueTimeoutMs": "0",
    "BoltConfiguration__MaxPendingRpcCalls": "1000",
    "BoltConfiguration__MaxPendingRpcCallsPerPrincipal": "128",
    "BoltConfiguration__MaxConnectionsPerPrincipal": "16",
    "BoltConfiguration__MaxActiveStreamsPerPrincipal": "64",
    "BoltConfiguration__MaxMediaStreamsPerPrincipal": "8",
    "BoltConfiguration__MaxSubscriptionsPerPrincipal": "128",
    "BoltConfiguration__MaxDurableSubscribersPerTopic": "128",
    "BoltConfiguration__MaxConnectionLifetimeSeconds": "1800",
}
PHASE0_EFFECTIVE_QUOTAS = {
    "CleanupIntervalSeconds": 10,
    "InvocationTimeoutMs": 30000,
    "MaxFrameBytes": 8388608,
    "SendQueueCapacity": 256,
    "SendEnqueueTimeoutMs": 30000,
    "TransportCloseTimeoutMs": 5000,
    "MaxPendingRpcCalls": 1000,
    "MaxPendingRpcCallsPerPrincipal": 128,
    "MaxConnectionsPerPrincipal": 16,
    "MaxActiveStreamsPerPrincipal": 64,
    "MaxMediaStreamsPerPrincipal": 8,
    "MaxSubscriptionsPerPrincipal": 128,
    "MaxDurableSubscribersPerTopic": 128,
    "MaxConnectionLifetimeSeconds": 1800,
}


class Gate:
    def __init__(self) -> None:
        self.checks: dict[str, dict[str, Any]] = {}
        self.errors: list[str] = []

    def check(self, name: str, condition: bool, detail: Any) -> None:
        self.checks[name] = {"passed": bool(condition), "detail": detail}
        if not condition:
            self.errors.append(f"{name}: {detail}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", help="Rendered Compose JSON path, or - for stdin")
    parser.add_argument("--compose-file", action="append", help="Compose file; repeat for overrides")
    parser.add_argument("--env-file", help="Environment file used while rendering Compose")
    parser.add_argument("--output", required=True, help="Redacted JSON evidence output path")
    parser.add_argument("--expected-internal-url", default=SECURE_URL)
    parser.add_argument("--expected-ca-path")
    parser.add_argument("--expected-fullchain-path")
    parser.add_argument("--expected-private-key-path")
    parser.add_argument("--expected-published-port", type=int)
    parser.add_argument("--expected-public-hostname")
    parser.add_argument("--expected-identityserver-ca-path")
    parser.add_argument("--expected-identityserver-fullchain-path")
    parser.add_argument("--expected-identityserver-private-key-path")
    parser.add_argument("--expected-identityserver-published-port", type=int)
    parser.add_argument("--expected-identityserver-public-hostname")
    parser.add_argument("--expected-identityserver-token-path", default=IDENTITY_TOKEN_PATH)
    parser.add_argument("--pins-file", help="Registry-confirmed image-pin evidence")
    parser.add_argument("--provenance-file", help="Passed Phase 0 provenance evidence")
    parser.add_argument("--authorized-service", action="append", default=[])
    parser.add_argument("--authorize-deployment", action="store_true")
    parser.add_argument("--publication-topology-attestation")
    parser.add_argument("--publication-topology-attested-by")
    parser.add_argument("--publication-topology-attested-by-id")
    parser.add_argument("--publication-topology-triggering-actor")
    parser.add_argument("--publication-topology-run-id")
    parser.add_argument("--publication-topology-run-attempt", type=int)
    return parser.parse_args()


def render_compose(args: argparse.Namespace) -> dict[str, Any]:
    if args.input:
        raw = sys.stdin.read() if args.input == "-" else Path(args.input).read_text(encoding="utf-8-sig")
    else:
        suffix: list[str] = []
        if args.env_file:
            suffix.extend(("--env-file", args.env_file))
        for compose_file in args.compose_file or []:
            suffix.extend(("-f", compose_file))
        suffix.extend(("--profile", "*", "config", "--format", "json"))
        commands = [["docker", "compose", *suffix]]
        if shutil.which("docker-compose"):
            commands.append(["docker-compose", *suffix])

        failures: list[str] = []
        raw = ""
        for command in commands:
            result = subprocess.run(command, check=False, capture_output=True, text=True)
            if result.returncode == 0:
                raw = result.stdout
                break
            failures.append(result.stderr.strip() or result.stdout.strip())
        if not raw:
            raise RuntimeError("; ".join(item for item in failures if item) or "docker compose config failed")

    manifest = json.loads(raw)
    if not isinstance(manifest, dict):
        raise ValueError("rendered Compose JSON must be an object")
    return manifest


def environment(service: dict[str, Any]) -> dict[str, str]:
    value = service.get("environment") or {}
    if isinstance(value, dict):
        return {str(key): "" if item is None else str(item) for key, item in value.items()}
    result: dict[str, str] = {}
    if isinstance(value, list):
        for item in value:
            key, separator, entry = str(item).partition("=")
            if key in result:
                raise ValueError(f"duplicate rendered environment key: {key}")
            result[key] = entry if separator else ""
    return result


def parse_scope_scalar(value: str) -> tuple[str, ...]:
    return tuple(item for item in re.split(r"[,;\s]+", value.strip()) if item)


def configured_client_scope_matrix(
    environment_values: dict[str, str],
) -> tuple[dict[str, tuple[str, ...]], list[str]]:
    records: dict[int, dict[str, Any]] = {}
    errors: list[str] = []
    configuration_paths: dict[str, list[str]] = {}

    for key, value in environment_values.items():
        path = key.replace("__", ":")
        segments = path.split(":")
        normalized_segments = [segment.lower() for segment in segments]
        if normalized_segments[:2] != ["serviceidentity", "clients"]:
            continue

        normalized_path = ":".join(normalized_segments)
        configuration_paths.setdefault(normalized_path, []).append(key)
        if len(segments) < 4 or not segments[2].isdigit() or str(int(segments[2])) != segments[2]:
            errors.append(f"unapproved service client configuration key: {key}")
            continue

        index = int(segments[2])
        field = normalized_segments[3]
        record = records.setdefault(
            index,
            {"client_id": None, "scope_scalar": None, "scope_children": {}},
        )
        if field == "clientid":
            if len(segments) != 4:
                errors.append(f"unapproved ClientId descendant: {key}")
                continue
            canonical_key = f"ServiceIdentity__Clients__{index}__ClientId"
            if key != canonical_key:
                errors.append(f"noncanonical service client configuration key: {key}")
            if record["client_id"] is None:
                record["client_id"] = value
            continue

        if field != "allowedscopes":
            continue
        if len(segments) == 4:
            canonical_key = f"ServiceIdentity__Clients__{index}__AllowedScopes"
            if key != canonical_key:
                errors.append(f"noncanonical service client configuration key: {key}")
            if record["scope_scalar"] is None:
                record["scope_scalar"] = value
            continue
        if (
            len(segments) != 5
            or not segments[4].isdigit()
            or str(int(segments[4])) != segments[4]
        ):
            errors.append(f"unapproved AllowedScopes descendant: {key}")
            continue

        scope_index = int(segments[4])
        canonical_key = f"ServiceIdentity__Clients__{index}__AllowedScopes__{scope_index}"
        if key != canonical_key:
            errors.append(f"noncanonical service client configuration key: {key}")
        record["scope_children"].setdefault(scope_index, value)

    for path, keys in sorted(configuration_paths.items()):
        if len(keys) > 1:
            errors.append(
                f"configuration path {path} is supplied by multiple environment keys: "
                f"{', '.join(sorted(keys))}"
            )

    observed: dict[str, tuple[str, ...]] = {}
    configured_client_ids: set[str] = set()
    for index, record in sorted(records.items()):
        client_id = (record["client_id"] or "").strip()
        scope_scalar = record["scope_scalar"]
        scope_children = record["scope_children"]
        if scope_scalar is not None and scope_children:
            errors.append(f"client index {index} mixes scalar and indexed AllowedScopes")
        if scope_children:
            scopes = tuple(
                value.strip()
                for _, value in sorted(scope_children.items())
                if value.strip()
            )
        else:
            scopes = parse_scope_scalar(scope_scalar or "")
        if not client_id or not scopes:
            errors.append(f"client index {index} is missing ClientId or AllowedScopes")
            continue
        normalized = tuple(sorted(scope.lower() for scope in scopes))
        if len(normalized) != len(set(normalized)):
            errors.append(f"client {client_id} contains duplicate scopes")
        if client_id in observed:
            errors.append(f"client {client_id} is configured more than once")
        elif client_id.lower() in configured_client_ids:
            errors.append(f"client {client_id} duplicates another ClientId by case")
        observed[client_id] = normalized
        configured_client_ids.add(client_id.lower())
    return observed, errors


def configured_default_scopes(environment_values: dict[str, str]) -> tuple[str, ...]:
    indexed = sorted(
        (int(match.group(1)), value.strip().lower())
        for key, value in environment_values.items()
        if (match := SERVICE_IDENTITY_DEFAULT_SCOPE_KEY.fullmatch(key))
    )
    return tuple(value for _, value in indexed if value)


def truthy(value: Any) -> bool:
    return str(value).strip().lower() in {"1", "true", "yes", "on"}


def configuration_key(value: str) -> str:
    return value.replace("__", ":").lower()


def noncanonical_overrides(environment_values: dict[str, str], protected_keys: set[str]) -> list[str]:
    canonical = {configuration_key(key): key for key in protected_keys}
    return sorted(
        key
        for key in environment_values
        if configuration_key(key) in canonical and key != canonical[configuration_key(key)]
    )


def resolve_host_path(value: str | None) -> Path:
    if not value:
        raise ValueError("required host path is missing")
    path = Path(os.path.expanduser(value))
    if not path.is_absolute():
        raise ValueError("required host path is not absolute")
    return path.resolve(strict=True)


def file_identity(path: Path) -> tuple[int, int]:
    metadata = path.stat()
    return metadata.st_dev, metadata.st_ino


def canonical_hostname(value: str | None) -> bool:
    if not value or len(value) > 253 or value.endswith("."):
        return False
    labels = value.split(".")
    if len(labels) < 2 or not all(
        re.fullmatch(r"[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?", label) for label in labels
    ):
        return False
    try:
        ipaddress.ip_address(value)
    except ValueError:
        return True
    return False


def canonical_host_address(value: Any) -> str | None:
    if not isinstance(value, str) or not value or "%" in value:
        return None
    try:
        address = ipaddress.ip_address(value)
    except ValueError:
        return None
    if address.is_loopback or address.is_link_local or address.is_unspecified or address.is_multicast:
        return None
    return address.compressed


def inspect_publication_topology(hostname: str) -> dict[str, list[str]]:
    resolved = {
        address
        for result in socket.getaddrinfo(hostname, None, type=socket.SOCK_STREAM)
        if (address := canonical_host_address(result[4][0])) is not None
    }
    ip_executable = next(
        (candidate for candidate in (Path("/usr/sbin/ip"), Path("/usr/bin/ip")) if candidate.is_file()),
        None,
    )
    if ip_executable is None:
        raise RuntimeError("trusted-ip-command-unavailable")
    completed = subprocess.run(
        [str(ip_executable), "-json", "address", "show", "up", "scope", "global"],
        check=True,
        capture_output=True,
        text=True,
        timeout=10,
    )
    interfaces = json.loads(completed.stdout)
    if not isinstance(interfaces, list):
        raise ValueError("invalid-host-interface-inventory")
    host_addresses: set[str] = set()
    for interface in interfaces:
        if not isinstance(interface, dict) or not isinstance(interface.get("addr_info"), list):
            raise ValueError("invalid-host-interface-inventory")
        for entry in interface["addr_info"]:
            if not isinstance(entry, dict):
                raise ValueError("invalid-host-interface-inventory")
            address = canonical_host_address(entry.get("local"))
            if address is not None:
                host_addresses.add(address)
    matched = resolved & host_addresses
    return {
        "resolved_addresses": sorted(resolved),
        "host_interface_addresses": sorted(host_addresses),
        "matched_addresses": sorted(matched),
    }


def load_identityserver_environment(args: argparse.Namespace) -> tuple[dict[str, str | int | None], dict[str, Any]]:
    expected = {
        "IDENTITYSERVER_TLS_CA_PATH": getattr(args, "expected_identityserver_ca_path", None),
        "IDENTITYSERVER_TLS_FULLCHAIN_PATH": getattr(args, "expected_identityserver_fullchain_path", None),
        "IDENTITYSERVER_TLS_PRIVATE_KEY_PATH": getattr(args, "expected_identityserver_private_key_path", None),
        "IDENTITYSERVER_PUBLIC_HOSTNAME": getattr(args, "expected_identityserver_public_hostname", None),
        "IDENTITYSERVER_PUBLIC_HTTPS_PORT": getattr(args, "expected_identityserver_published_port", None),
        "IDENTITYSERVER_BOLT_TRANSPORT_TOKEN_PATH": getattr(args, "expected_identityserver_token_path", IDENTITY_TOKEN_PATH),
    }
    detail: dict[str, Any] = {"source": "expected-arguments", "keys": sorted(expected)}
    env_file = getattr(args, "env_file", None)
    if env_file:
        parser_path = Path(__file__).with_name("verify-bolt-phase0-env.py")
        parser = runpy.run_path(str(parser_path))
        values = parser["parse_env"](Path(env_file))
        missing = [key for key in expected if key not in values]
        if missing:
            raise ValueError(f"protected env is missing IdentityServer keys: {', '.join(missing)}")
        typed = {
            "IDENTITYSERVER_TLS_CA_PATH": parser["typed_value"](
                "IDENTITYSERVER_TLS_CA_PATH", values["IDENTITYSERVER_TLS_CA_PATH"], "absolute-path"
            ),
            "IDENTITYSERVER_TLS_FULLCHAIN_PATH": parser["typed_value"](
                "IDENTITYSERVER_TLS_FULLCHAIN_PATH", values["IDENTITYSERVER_TLS_FULLCHAIN_PATH"], "absolute-path"
            ),
            "IDENTITYSERVER_TLS_PRIVATE_KEY_PATH": parser["typed_value"](
                "IDENTITYSERVER_TLS_PRIVATE_KEY_PATH", values["IDENTITYSERVER_TLS_PRIVATE_KEY_PATH"], "absolute-path"
            ),
            "IDENTITYSERVER_PUBLIC_HOSTNAME": parser["typed_value"](
                "IDENTITYSERVER_PUBLIC_HOSTNAME", values["IDENTITYSERVER_PUBLIC_HOSTNAME"], "hostname"
            ),
            "IDENTITYSERVER_PUBLIC_HTTPS_PORT": int(parser["typed_value"](
                "IDENTITYSERVER_PUBLIC_HTTPS_PORT", values["IDENTITYSERVER_PUBLIC_HTTPS_PORT"], "port"
            )),
            "IDENTITYSERVER_BOLT_TRANSPORT_TOKEN_PATH": values["IDENTITYSERVER_BOLT_TRANSPORT_TOKEN_PATH"],
        }
        conflicts = {
            key: {"expected": expected_value, "env": typed[key]}
            for key, expected_value in expected.items()
            if expected_value is not None and str(expected_value) != str(typed[key])
        }
        if conflicts:
            raise ValueError(f"IdentityServer expected values do not match protected env: {sorted(conflicts)}")
        expected = typed
        detail["source"] = "protected-env"

    path_value = str(expected["IDENTITYSERVER_BOLT_TRANSPORT_TOKEN_PATH"] or "")
    route = urlparse(path_value)
    route_ok = (
        path_value == IDENTITY_TOKEN_PATH
        and route.scheme == ""
        and route.netloc == ""
        and route.path == path_value
        and not route.query
        and not route.fragment
    )
    port = expected["IDENTITYSERVER_PUBLIC_HTTPS_PORT"]
    port_ok = isinstance(port, int) and 1 <= port <= 65535
    hostname = str(expected["IDENTITYSERVER_PUBLIC_HOSTNAME"] or "")
    detail.update({"hostname": hostname, "port": port, "token_path": path_value})
    detail["valid"] = canonical_hostname(hostname) and port_ok and route_ok
    return expected, detail


def volumes(service: dict[str, Any]) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    for item in service.get("volumes") or []:
        if isinstance(item, dict):
            result.append(
                {
                    "type": str(item.get("type", "volume")),
                    "source": str(item.get("source", "")),
                    "target": str(item.get("target", "")),
                    "read_only": bool(item.get("read_only", False)),
                }
            )
            continue
        parts = str(item).split(":")
        if len(parts) >= 2:
            result.append(
                {
                    "type": "bind" if parts[0].startswith(("/", ".")) else "volume",
                    "source": parts[0],
                    "target": parts[1],
                    "read_only": "ro" in parts[2:],
                }
            )
    return result


def ports(service: dict[str, Any]) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    for item in service.get("ports") or []:
        if isinstance(item, dict):
            result.append(
                {
                    "target": int(item.get("target", 0)),
                    "published": int(item.get("published", 0)),
                    "protocol": str(item.get("protocol", "tcp")),
                    "host_ip": str(item.get("host_ip", "")),
                }
            )
            continue
        text = str(item).split("/", maxsplit=1)[0]
        parts = text.rsplit(":", maxsplit=2)
        if len(parts) >= 2:
            result.append(
                {
                    "target": int(parts[-1]),
                    "published": int(parts[-2]),
                    "protocol": "tcp",
                    "host_ip": parts[-3] if len(parts) == 3 else "",
                }
            )
    return result


def digest_reference(reference: str) -> bool:
    repository, separator, digest = reference.rpartition("@")
    if not separator or not repository or not DIGEST.fullmatch(digest):
        return False
    return ":" not in repository.rsplit("/", maxsplit=1)[-1]


def load_pins(path: str | None) -> tuple[dict[str, str], str | None]:
    if not path:
        return {}, None
    document = json.loads(Path(path).read_text(encoding="utf-8-sig"))
    if not isinstance(document, dict) or document.get("schema") != "xframework.bolt.phase0.image-pins.v2":
        raise ValueError("pins file has an unsupported schema")
    if document.get("status") != "passed" or document.get("registry_confirmed") is not True:
        raise ValueError("pins file did not pass registry confirmation")
    pins = document.get("pins")
    repositories = document.get("approved_repositories")
    if not isinstance(pins, dict) or not isinstance(repositories, dict) or set(pins) != set(repositories):
        raise ValueError("pins file has invalid repository coverage")
    normalized: dict[str, str] = {}
    for service, reference in pins.items():
        exact = str(reference)
        repository = str(repositories.get(service, ""))
        if not digest_reference(exact) or not exact.startswith(f"{repository}@"):
            raise ValueError(f"{service}: pin is not in its approved repository")
        normalized[str(service)] = exact
    return normalized, str(document.get("source_commit", ""))


def load_provenance(path: str | None) -> tuple[dict[str, dict[str, Any]], str | None, bool]:
    if not path:
        return {}, None, False
    document = json.loads(Path(path).read_text(encoding="utf-8-sig"))
    if not isinstance(document, dict) or document.get("schema") != "xframework.bolt.phase0.provenance.v1":
        raise ValueError("provenance file has an unsupported schema")
    bindings = document.get("bindings")
    if document.get("status") != "passed" or not isinstance(bindings, dict):
        raise ValueError("provenance file did not pass verification")
    return {str(key): value for key, value in bindings.items() if isinstance(value, dict)}, str(
        document.get("source_commit", "")
    ), True


def health_command(service: dict[str, Any]) -> str:
    test = (service.get("healthcheck") or {}).get("test") or []
    return " ".join(str(item) for item in test) if isinstance(test, list) else str(test)


def secrets(service: dict[str, Any]) -> list[dict[str, Any]]:
    result = []
    for item in service.get("secrets") or []:
        if isinstance(item, dict):
            result.append(
                {"source": str(item.get("source", "")), "target": str(item.get("target", "")), "mode": str(item.get("mode", ""))}
            )
        else:
            result.append({"source": str(item), "target": "", "mode": ""})
    return result


def secret_file(manifest: dict[str, Any], name: str) -> str:
    definition = (manifest.get("secrets") or {}).get(name) or {}
    return str(definition.get("file", "")) if isinstance(definition, dict) else ""


def source_exposes_path(source: str, protected_path: Path) -> str | None:
    try:
        resolved = resolve_host_path(source)
    except (OSError, ValueError):
        return None
    if resolved == protected_path or resolved.samefile(protected_path):
        return "exact"
    if resolved.is_dir() and protected_path.is_relative_to(resolved):
        return "parent-directory"
    return None


def verify(manifest: dict[str, Any], args: argparse.Namespace, gate: Gate) -> dict[str, Any]:
    services = manifest.get("services") or {}
    required = ("bolt-hub", *CLIENT_SERVICES, *INACTIVE_SERVICES)
    missing = [name for name in required if name not in services]
    gate.check("required-services", not missing, {"missing": missing, "client_count": len(CLIENT_SERVICES)})
    if missing:
        return {}

    hub = services["bolt-hub"]
    hub_env = environment(hub)
    identityserver = services["identityserver"]
    identityserver_env = environment(identityserver)
    try:
        identity_inputs, identity_input_detail = load_identityserver_environment(args)
    except (OSError, ValueError) as error:
        identity_inputs = {}
        identity_input_detail = {"valid": False, "error": str(error)}
    gate.check(
        "identityserver-public-token-refresh-configuration",
        bool(identity_input_detail.get("valid")),
        identity_input_detail,
    )
    hub_protected_keys = {
        "ASPNETCORE_URLS",
        "ASPNETCORE_HTTP_PORTS",
        "ASPNETCORE_HTTPS_PORTS",
        "DOTNET_URLS",
        "BoltConfiguration__RequireSecureTransport",
        "BoltConfiguration__MediaEnabled",
        "BoltConfiguration__RegistrationIdentityBindingMode",
        "ServiceIdentity__DefaultScopes__0",
        *EXPECTED_ENDPOINT_ENV,
        *PHASE0_QUOTAS,
    }
    identityserver_protected_keys = {
        "ASPNETCORE_URLS",
        "ASPNETCORE_HTTP_PORTS",
        "ASPNETCORE_HTTPS_PORTS",
        "DOTNET_URLS",
        *IDENTITY_EXPECTED_ENDPOINT_ENV,
        *IDENTITY_ISSUER_ENV,
        *(
            f"ServiceIdentity__Clients__{index}__{field}"
            for index in range(len(SERVICE_IDENTITY_CLIENT_SCOPE_MATRIX))
            for field in ("ClientId", "AllowedScopes")
        ),
    }
    noncanonical_environment = {
        "bolt-hub": noncanonical_overrides(hub_env, hub_protected_keys),
        "identityserver-boundary": noncanonical_overrides(identityserver_env, identityserver_protected_keys),
        **{
            name: noncanonical_overrides(
                environment(services[name]),
                {
                    "BoltConfiguration__RequireSecureTransport",
                    "BoltConfiguration__ServerUrls__0",
                    *(
                        f"ServiceIdentity__DefaultScopes__{index}"
                        for index in range(len(SERVICE_IDENTITY_RUNTIME_DEFAULT_SCOPES[name]))
                    ),
                },
            )
            for name in CLIENT_SERVICES
        },
        "bolt-phase0-synthetics": noncanonical_overrides(
            environment(services["bolt-phase0-synthetics"]), {"BOLT_SYNTHETIC_TARGET"}
        ),
    }
    gate.check(
        "canonical-protected-environment-keys",
        not any(noncanonical_environment.values()),
        noncanonical_environment,
    )
    replicas = (hub.get("deploy") or {}).get("replicas")
    scale = hub.get("scale")
    gate.check(
        "hub-single-replica",
        str(replicas) == "1" and scale in {None, 1, "1"},
        {"deploy_replicas": replicas, "scale": scale},
    )
    gate.check(
        "hub-media-disabled",
        hub_env.get("BoltConfiguration__MediaEnabled", "").strip().lower() == "false",
        {"value": hub_env.get("BoltConfiguration__MediaEnabled")},
    )
    gate.check(
        "hub-registration-enforcement",
        hub_env.get("BoltConfiguration__RegistrationIdentityBindingMode") == "Enforce",
        {"value": hub_env.get("BoltConfiguration__RegistrationIdentityBindingMode")},
    )

    quota_evidence = {
        key: {"value": hub_env.get(key, expected), "source": "compose" if key in hub_env else "phase0-baseline"}
        for key, expected in PHASE0_QUOTAS.items()
    }
    quota_ok = all(quota_evidence[key]["value"] == expected for key, expected in PHASE0_QUOTAS.items())
    gate.check(
        "hub-exact-phase0-quotas",
        quota_ok,
        {"configuration": quota_evidence, "effective_server_options": PHASE0_EFFECTIVE_QUOTAS},
    )

    secure_transport_services = ("bolt-hub", *CLIENT_SERVICES)
    secure_transport = {
        name: truthy(environment(services[name]).get("BoltConfiguration__RequireSecureTransport"))
        for name in secure_transport_services
    }
    gate.check("secure-transport-required", all(secure_transport.values()), secure_transport)

    endpoint_values = {key: hub_env.get(key) for key in EXPECTED_ENDPOINT_ENV}
    endpoint_keys = {key for key in hub_env if configuration_key(key).startswith("kestrel:endpoints:")}
    aspnetcore_urls = {value for value in hub_env.get("ASPNETCORE_URLS", "").split(";") if value}
    override_names = {"ASPNETCORE_HTTP_PORTS", "ASPNETCORE_HTTPS_PORTS", "DOTNET_URLS"}
    override_channels = {key: value for key, value in hub_env.items() if key.upper() in override_names}
    command = hub.get("command")
    command_text = " ".join(map(str, command)) if isinstance(command, list) else str(command or "")
    entrypoint = hub.get("entrypoint")
    entrypoint_text = " ".join(map(str, entrypoint)) if isinstance(entrypoint, list) else str(entrypoint or "")
    process_override = bool(
        re.search(r"(?i)(--urls|kestrel(?:__|:)endpoints|aspnetcore_urls)", f"{entrypoint_text} {command_text}")
    )
    endpoints_ok = (
        endpoint_values == EXPECTED_ENDPOINT_ENV
        and endpoint_keys == set(EXPECTED_ENDPOINT_ENV)
        and aspnetcore_urls == EXPECTED_ASPNETCORE_URLS
        and not override_channels
        and not process_override
    )
    gate.check(
        "hub-effective-kestrel-endpoints",
        endpoints_ok,
        {
            "named_endpoints": endpoint_values,
            "aspnetcore_urls": sorted(aspnetcore_urls),
            "extra_named_endpoint_keys": sorted(endpoint_keys - set(EXPECTED_ENDPOINT_ENV)),
            "other_override_channels": sorted(override_channels),
            "process_argument_override": process_override,
        },
    )

    identity_endpoint_values = {key: identityserver_env.get(key) for key in IDENTITY_EXPECTED_ENDPOINT_ENV}
    identity_endpoint_keys = {
        key for key in identityserver_env if configuration_key(key).startswith("kestrel:endpoints:")
    }
    identity_aspnetcore_urls = {
        value for value in identityserver_env.get("ASPNETCORE_URLS", "").split(";") if value
    }
    identity_override_channels = {
        key: value for key, value in identityserver_env.items() if key.upper() in override_names
    }
    identity_command = identityserver.get("command")
    identity_command_text = (
        " ".join(map(str, identity_command)) if isinstance(identity_command, list) else str(identity_command or "")
    )
    identity_entrypoint = identityserver.get("entrypoint")
    identity_entrypoint_text = (
        " ".join(map(str, identity_entrypoint))
        if isinstance(identity_entrypoint, list)
        else str(identity_entrypoint or "")
    )
    identity_process_override = bool(
        re.search(
            r"(?i)(--urls|kestrel(?:__|:)endpoints|aspnetcore_urls)",
            f"{identity_entrypoint_text} {identity_command_text}",
        )
    )
    identity_endpoints_ok = (
        identity_endpoint_values == IDENTITY_EXPECTED_ENDPOINT_ENV
        and identity_endpoint_keys == set(IDENTITY_EXPECTED_ENDPOINT_ENV)
        and identity_aspnetcore_urls == IDENTITY_EXPECTED_ASPNETCORE_URLS
        and not identity_override_channels
        and not identity_process_override
    )
    gate.check(
        "identityserver-effective-kestrel-endpoints",
        identity_endpoints_ok,
        {
            "named_endpoints": identity_endpoint_values,
            "aspnetcore_urls": sorted(identity_aspnetcore_urls),
            "extra_named_endpoint_keys": sorted(identity_endpoint_keys - set(IDENTITY_EXPECTED_ENDPOINT_ENV)),
            "other_override_channels": sorted(identity_override_channels),
            "process_argument_override": identity_process_override,
        },
    )

    issuer_values = {key: identityserver_env.get(key) for key in IDENTITY_ISSUER_ENV}
    gate.check(
        "identityserver-phase0-transport-token-issuer",
        issuer_values == IDENTITY_ISSUER_ENV,
        issuer_values,
    )

    observed_scope_matrix, scope_matrix_errors = configured_client_scope_matrix(identityserver_env)
    expected_scope_matrix = {
        client_id: tuple(sorted(scope.lower() for scope in scopes))
        for client_id, scopes in SERVICE_IDENTITY_CLIENT_SCOPE_MATRIX.items()
    }
    scope_matrix_ok = not scope_matrix_errors and observed_scope_matrix == expected_scope_matrix
    gate.check(
        "identityserver-exact-client-scope-matrix",
        scope_matrix_ok,
        {
            "expected": expected_scope_matrix,
            "observed": observed_scope_matrix,
            "configuration_errors": scope_matrix_errors,
        },
    )

    observed_runtime_defaults = {
        name: configured_default_scopes(environment(services[name]))
        for name in SERVICE_IDENTITY_RUNTIME_DEFAULT_SCOPES
    }
    unexpected_runtime_scope_keys = {
        name: sorted(
            key
            for key in environment(services[name])
            if configuration_key(key).startswith("serviceidentity:defaultscopes")
            and not SERVICE_IDENTITY_DEFAULT_SCOPE_KEY.fullmatch(key)
        )
        for name in SERVICE_IDENTITY_RUNTIME_DEFAULT_SCOPES
    }
    unexpected_runtime_scope_keys = {
        name: keys for name, keys in unexpected_runtime_scope_keys.items() if keys
    }
    runtime_defaults_ok = (
        observed_runtime_defaults == SERVICE_IDENTITY_RUNTIME_DEFAULT_SCOPES
        and not unexpected_runtime_scope_keys
    )
    gate.check(
        "service-identity-exact-runtime-default-scopes",
        runtime_defaults_ok,
        {
            "expected": SERVICE_IDENTITY_RUNTIME_DEFAULT_SCOPES,
            "observed": observed_runtime_defaults,
            "unexpected_keys": unexpected_runtime_scope_keys,
        },
    )

    hub_ports = ports(hub)
    only_tls_port = (
        len(hub_ports) == 1
        and hub_ports[0]["target"] == 8443
        and hub_ports[0]["protocol"] == "tcp"
        and hub_ports[0]["published"] > 0
    )
    if args.expected_published_port is not None and hub_ports:
        only_tls_port = only_tls_port and hub_ports[0]["published"] == args.expected_published_port
    gate.check("hub-only-tls-publication", only_tls_port, {"ports": hub_ports, "expected": args.expected_published_port})

    if args.authorize_deployment:
        try:
            live_topology = inspect_publication_topology(args.expected_public_hostname)
        except (OSError, RuntimeError, ValueError, socket.gaierror, subprocess.SubprocessError, json.JSONDecodeError):
            live_topology = {
                "resolved_addresses": [],
                "host_interface_addresses": [],
                "matched_addresses": [],
            }
        host_binding = hub_ports[0]["host_ip"] if len(hub_ports) == 1 else None
        canonical_binding = canonical_host_address(host_binding)
        binding_is_direct = (
            host_binding in ALL_INTERFACE_BINDINGS
            or canonical_binding in live_topology["matched_addresses"]
        )
        live_topology_verified = (
            bool(live_topology["resolved_addresses"])
            and live_topology["matched_addresses"] == live_topology["resolved_addresses"]
            and binding_is_direct
        )
        gate.check(
            "direct-publication-host-interface",
            live_topology_verified,
            {"binding": host_binding, **live_topology},
        )
        topology_detail = {
            "attestation": args.publication_topology_attestation,
            "attested_by": args.publication_topology_attested_by,
            "attested_by_id": args.publication_topology_attested_by_id,
            "triggering_actor": args.publication_topology_triggering_actor,
            "workflow_event": "workflow_dispatch",
            "run_id": args.publication_topology_run_id,
            "run_attempt": args.publication_topology_run_attempt,
            "source_commit": args.expected_source_commit,
            "published_hostname": args.expected_public_hostname,
            "published_port": args.expected_published_port,
            "mode": "direct-kestrel",
            "intermediaries": [],
            "scope": ["host-reverse-proxy", "tailscale-serve", "load-balancer", "ingress"],
            "binding": host_binding,
            **live_topology,
        }
        topology_attested = (
            args.publication_topology_attestation == DIRECT_PUBLICATION_ATTESTATION
            and isinstance(args.publication_topology_attested_by, str)
            and GITHUB_ACTOR.fullmatch(args.publication_topology_attested_by) is not None
            and isinstance(args.publication_topology_attested_by_id, str)
            and GITHUB_ACTOR_ID.fullmatch(args.publication_topology_attested_by_id) is not None
            and args.publication_topology_triggering_actor == args.publication_topology_attested_by
            and isinstance(args.publication_topology_run_id, str)
            and WORKFLOW_RUN_ID.fullmatch(args.publication_topology_run_id) is not None
            and args.publication_topology_run_attempt == 1
            and isinstance(args.expected_source_commit, str)
            and COMMIT_SHA.fullmatch(args.expected_source_commit) is not None
            and canonical_hostname(args.expected_public_hostname)
            and isinstance(args.expected_published_port, int)
            and 1 <= args.expected_published_port <= 65_535
            and live_topology_verified
        )
        gate.check(
            "operator-attested-direct-publication-topology",
            topology_attested,
            topology_detail,
        )

    identity_ports = ports(identityserver)
    expected_identity_port = identity_inputs.get("IDENTITYSERVER_PUBLIC_HTTPS_PORT")
    identity_only_tls_port = (
        len(identity_ports) == 1
        and identity_ports[0]["target"] == 8443
        and identity_ports[0]["protocol"] == "tcp"
        and identity_ports[0]["published"] > 0
        and isinstance(expected_identity_port, int)
        and identity_ports[0]["published"] == expected_identity_port
    )
    gate.check(
        "identityserver-only-tls-publication",
        identity_only_tls_port,
        {"ports": identity_ports, "expected": expected_identity_port},
    )

    health = health_command(hub)
    lower_health = health.lower()
    health_ok = (
        "http://127.0.0.1:8080/health/live" in lower_health
        and "http://127.0.0.1:8080/health/ready" in lower_health
        and "--insecure" not in lower_health
        and " -k" not in lower_health
    )
    gate.check("hub-live-and-ready-healthcheck", health_ok, {"command": health})

    identity_health = health_command(identityserver)
    lower_identity_health = identity_health.lower()
    identity_health_ok = (
        "http://127.0.0.1:8080/health/live" in lower_identity_health
        and "https://" not in lower_identity_health
        and "http://localhost" not in lower_identity_health
        and "--insecure" not in lower_identity_health
        and " -k" not in lower_identity_health
    )
    gate.check(
        "identityserver-loopback-http-healthcheck",
        identity_health_ok,
        {"command": identity_health},
    )

    expected_url = args.expected_internal_url
    parsed_url = urlparse(expected_url)
    expected_url_valid = (
        parsed_url.scheme == "wss" and parsed_url.hostname == "bolt-hub" and parsed_url.port == 8443 and parsed_url.path == "/bolt/ws"
    )
    gate.check("expected-internal-url", expected_url_valid, {"url": expected_url})
    client_urls = {name: environment(services[name]).get("BoltConfiguration__ServerUrls__0") for name in CLIENT_SERVICES}
    gate.check("all-clients-use-wss", all(value == expected_url for value in client_urls.values()), client_urls)

    synthetics = services["bolt-phase0-synthetics"]
    synthetics_env = environment(synthetics)
    synthetics_inactive = (
        synthetics.get("profiles") == ["phase0-verification"]
        and str(synthetics.get("restart", "")).lower() == "no"
        and synthetics_env.get("BOLT_SYNTHETIC_TARGET") == expected_url
    )
    gate.check(
        "synthetics-profile-is-inactive-and-secure",
        synthetics_inactive,
        {
            "profiles": synthetics.get("profiles"),
            "restart": synthetics.get("restart"),
            "target": synthetics_env.get("BOLT_SYNTHETIC_TARGET"),
        },
    )

    expected_paths = {
        CA_SECRET: args.expected_ca_path,
        FULLCHAIN_SECRET: args.expected_fullchain_path,
        PRIVATE_KEY_SECRET: args.expected_private_key_path,
        IDENTITY_CA_SECRET: identity_inputs.get("IDENTITYSERVER_TLS_CA_PATH"),
        IDENTITY_FULLCHAIN_SECRET: identity_inputs.get("IDENTITYSERVER_TLS_FULLCHAIN_PATH"),
        IDENTITY_PRIVATE_KEY_SECRET: identity_inputs.get("IDENTITYSERVER_TLS_PRIVATE_KEY_PATH"),
    }
    resolved_expected: dict[str, Path] = {}
    paths_ok = True
    for name, value in expected_paths.items():
        try:
            resolved = resolve_host_path(value)
            if not resolved.is_file():
                raise ValueError("not a file")
            resolved_expected[name] = resolved
        except (OSError, ValueError):
            paths_ok = False
    resolved_identities = [file_identity(path) for path in resolved_expected.values()]
    secret_paths_ok = (
        paths_ok
        and len(set(resolved_expected.values())) == len(expected_paths)
        and len(set(resolved_identities)) == len(expected_paths)
    )
    for name in expected_paths:
        try:
            secret_paths_ok = secret_paths_ok and resolve_host_path(secret_file(manifest, name)) == resolved_expected[name]
        except (OSError, ValueError, KeyError):
            secret_paths_ok = False
    gate.check(
        "dual-tls-secret-files-resolved-and-distinct",
        secret_paths_ok,
        {name: "<resolved-file>" if name in resolved_expected else "<missing-or-unresolved>" for name in expected_paths},
    )

    trust_consumers = (*CLIENT_SERVICES, *INACTIVE_SERVICES)
    ca_mounts = {name: [item for item in secrets(services[name]) if item["source"] == CA_SECRET] for name in trust_consumers}
    ca_ok = all(
        len(items) == 1
        and items[0]["target"] == "/usr/local/share/ca-certificates/xframework-bolt-hub-ca.crt"
        and items[0]["mode"] in {"0444", "292"}
        for items in ca_mounts.values()
    )
    gate.check("all-clients-mount-ca-read-only", ca_ok, ca_mounts)

    key_owners: dict[str, list[dict[str, str]]] = {}
    if PRIVATE_KEY_SECRET in resolved_expected:
        protected_key = resolved_expected[PRIVATE_KEY_SECRET]
        for name, service in services.items():
            matches: list[dict[str, str]] = []
            for item in secrets(service):
                relation = source_exposes_path(secret_file(manifest, item["source"]), protected_key)
                if relation:
                    matches.append({"kind": "secret", "relation": relation, "target": item["target"]})
            for item in volumes(service):
                if item["type"] != "bind":
                    continue
                relation = source_exposes_path(item["source"], protected_key)
                if relation:
                    matches.append({"kind": "bind", "relation": relation, "target": item["target"]})
            if matches:
                key_owners[name] = matches
    hub_key = [item for item in secrets(hub) if item["source"] == PRIVATE_KEY_SECRET]
    hub_matches = key_owners.get("bolt-hub", [])
    key_ok = (
        list(key_owners) == ["bolt-hub"]
        and len(hub_matches) == 1
        and hub_matches[0]["kind"] == "secret"
        and hub_matches[0]["relation"] == "exact"
        and len(hub_key) == 1
        and hub_key[0]["target"] == "/run/secrets/bolt-hub-tls-private-key.pem"
        and hub_key[0]["mode"] in {"0400", "256"}
    )
    gate.check(
        "resolved-private-key-mounted-only-by-hub",
        key_ok,
        {"owners": sorted(key_owners), "mounts": key_owners, "hub_mode": hub_key[0]["mode"] if hub_key else None},
    )

    identity_key_owners: dict[str, list[dict[str, str]]] = {}
    if IDENTITY_PRIVATE_KEY_SECRET in resolved_expected:
        protected_identity_key = resolved_expected[IDENTITY_PRIVATE_KEY_SECRET]
        for name, service in services.items():
            matches = []
            for item in secrets(service):
                relation = source_exposes_path(secret_file(manifest, item["source"]), protected_identity_key)
                if relation:
                    matches.append({"kind": "secret", "relation": relation, "target": item["target"]})
            for item in volumes(service):
                if item["type"] != "bind":
                    continue
                relation = source_exposes_path(item["source"], protected_identity_key)
                if relation:
                    matches.append({"kind": "bind", "relation": relation, "target": item["target"]})
            if matches:
                identity_key_owners[name] = matches
    identity_key = [
        item for item in secrets(identityserver) if item["source"] == IDENTITY_PRIVATE_KEY_SECRET
    ]
    identity_matches = identity_key_owners.get("identityserver", [])
    identity_key_ok = (
        list(identity_key_owners) == ["identityserver"]
        and len(identity_matches) == 1
        and identity_matches[0]["kind"] == "secret"
        and identity_matches[0]["relation"] == "exact"
        and len(identity_key) == 1
        and identity_key[0]["target"] == "/run/secrets/identityserver-tls-private-key.pem"
        and identity_key[0]["mode"] in {"0400", "256"}
    )
    gate.check(
        "resolved-private-key-mounted-only-by-identityserver",
        identity_key_ok,
        {
            "owners": sorted(identity_key_owners),
            "mounts": identity_key_owners,
            "identityserver_mode": identity_key[0]["mode"] if identity_key else None,
        },
    )

    fullchain_owners = [
        name for name, service in services.items() if any(item["source"] == FULLCHAIN_SECRET for item in secrets(service))
    ]
    gate.check("fullchain-mounted-only-by-hub", fullchain_owners == ["bolt-hub"], {"owners": fullchain_owners})

    identity_fullchain_owners = [
        name
        for name, service in services.items()
        if any(item["source"] == IDENTITY_FULLCHAIN_SECRET for item in secrets(service))
    ]
    identity_fullchain = [
        item for item in secrets(identityserver) if item["source"] == IDENTITY_FULLCHAIN_SECRET
    ]
    identity_fullchain_ok = (
        identity_fullchain_owners == ["identityserver"]
        and len(identity_fullchain) == 1
        and identity_fullchain[0]["target"] == "/run/secrets/identityserver-tls-fullchain.pem"
        and identity_fullchain[0]["mode"] in {"0444", "292"}
    )
    gate.check(
        "fullchain-mounted-only-by-identityserver",
        identity_fullchain_ok,
        {"owners": identity_fullchain_owners, "mount": identity_fullchain},
    )

    identity_ca_owners = [
        name
        for name, service in services.items()
        if any(item["source"] == IDENTITY_CA_SECRET for item in secrets(service))
    ]
    identity_ca = [item for item in secrets(identityserver) if item["source"] == IDENTITY_CA_SECRET]
    identity_ca_ok = (
        identity_ca_owners == ["identityserver"]
        and len(identity_ca) == 1
        and identity_ca[0]["target"] == "/run/secrets/identityserver-ca.crt"
        and identity_ca[0]["mode"] in {"0444", "292"}
    )
    gate.check(
        "identityserver-ca-mounted-read-only",
        identity_ca_ok,
        {"owners": identity_ca_owners, "mount": identity_ca},
    )

    image_evidence: dict[str, dict[str, Any]] = {}
    image_ok = True
    expected_pins = getattr(args, "expected_image_pins", {})
    pin_commit = getattr(args, "expected_source_commit", None)
    provenance_bindings = getattr(args, "provenance_bindings", {})
    provenance_commit = getattr(args, "provenance_source_commit", None)
    provenance_verified = bool(getattr(args, "provenance_verified", False))
    authorized_services = set(args.authorized_service)
    if args.authorize_deployment:
        image_ok = (
            bool(authorized_services)
            and len(authorized_services) == len(args.authorized_service)
            and set(expected_pins) == authorized_services
            and set(provenance_bindings) == authorized_services
            and provenance_verified
            and bool(pin_commit)
            and pin_commit == provenance_commit
        )
        for name in sorted(authorized_services):
            reference = str((services.get(name) or {}).get("image", ""))
            expected = expected_pins.get(name, "")
            binding = provenance_bindings.get(name, {})
            matches = bool(
                digest_reference(expected)
                and reference == expected
                and binding.get("pin") == expected
                and binding.get("source_commit") == pin_commit
                and binding.get("signature_verified") is True
            )
            image_evidence[name] = {"image": reference, "expected": expected, "provenance_bound": matches}
            image_ok = image_ok and matches
    gate.check(
        "digest-pinned-provenance-authorized-images",
        image_ok,
        {
            "authorization_requested": args.authorize_deployment,
            "authorized_services": sorted(authorized_services),
            "registry_confirmed": bool(expected_pins),
            "provenance_verified": provenance_verified,
            "services": image_evidence,
        },
    )

    return {
        name: {
            "image": str(services[name].get("image", "")),
            "security_environment": {
                key: value
                for key, value in environment(services[name]).items()
                if key in {"ASPNETCORE_URLS", "BOLT_SYNTHETIC_TARGET", "BoltConfiguration__MediaEnabled", "BoltConfiguration__RegistrationIdentityBindingMode", "BoltConfiguration__RequireSecureTransport", "BoltConfiguration__ServerUrls__0", *EXPECTED_ENDPOINT_ENV, *IDENTITY_EXPECTED_ENDPOINT_ENV, *IDENTITY_ISSUER_ENV, *PHASE0_QUOTAS}
            },
            "security_secrets": [
                {"source": item["source"], "target": item["target"], "mode": item["mode"]}
                for item in secrets(services[name])
                if item["source"] in {CA_SECRET, FULLCHAIN_SECRET, PRIVATE_KEY_SECRET, IDENTITY_CA_SECRET, IDENTITY_FULLCHAIN_SECRET, IDENTITY_PRIVATE_KEY_SECRET}
            ],
            "ports": ports(services[name]) if name in {"bolt-hub", "identityserver"} else [],
            "healthcheck": health_command(services[name]) if name in {"bolt-hub", "identityserver"} else None,
            "replicas": (services[name].get("deploy") or {}).get("replicas") if name == "bolt-hub" else None,
        }
        for name in required
    }


def main() -> int:
    args = parse_args()
    gate = Gate()
    redacted_manifest: dict[str, Any] = {}
    try:
        if bool(args.input) == bool(args.compose_file):
            raise ValueError("provide exactly one of --input or --compose-file")
        args.expected_image_pins, args.expected_source_commit = load_pins(args.pins_file)
        args.provenance_bindings, args.provenance_source_commit, args.provenance_verified = load_provenance(
            args.provenance_file
        )
        manifest = render_compose(args)
        redacted_manifest = verify(manifest, args, gate)
    except Exception as error:
        gate.check("compose-render", False, str(error))

    evidence = {
        "schema": "xframework.bolt.phase0.preflight.v2",
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "status": "passed" if not gate.errors else "failed",
        "deployment_authorized": args.authorize_deployment and not gate.errors,
        "checks": gate.checks,
        "errors": gate.errors,
        "redacted_manifest": {"services": redacted_manifest},
    }
    output = Path(args.output)
    write_private_json(output, evidence)

    if gate.errors:
        for error in gate.errors:
            print(f"ERROR: {error}", file=sys.stderr)
        print(f"Bolt Phase 0 preflight failed; redacted evidence: {output}", file=sys.stderr)
        return 1
    print(f"Bolt Phase 0 preflight passed; redacted evidence: {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
