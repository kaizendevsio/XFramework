#!/usr/bin/env python3
"""Collect redacted post-deploy Bolt Phase 0 runtime boundary evidence."""

from __future__ import annotations

import argparse
import ipaddress
import json
import os
import re
import stat
import subprocess
import sys
import tempfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable


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
INACTIVE_IMAGE_SERVICES = ("bolt-phase0-synthetics",)
PHASE0_IMAGE_SERVICES = (*PHASE0_SERVICES, *INACTIVE_IMAGE_SERVICES)
IMAGE_ID = re.compile(r"^sha256:[0-9a-f]{64}$")
REPO_DIGEST = re.compile(r"^[a-z0-9][a-z0-9./:_-]*@sha256:[0-9a-f]{64}$")
CONTAINER_ID = re.compile(r"^[0-9a-f]{64}$")
PROJECT_NAME = re.compile(r"^[a-z0-9][a-z0-9_-]*$")
PROCESS_SOCKET = re.compile(r"^socket:\[([0-9]+)\]$")
CONTAINER_FORMAT = (
    '{"container_name":{{json .Name}},"container_id":{{json .Id}},'
    '"configured_image":{{json .Config.Image}},"local_image_id":{{json .Image}},'
    '"started_at":{{json .State.StartedAt}},"running":{{json .State.Running}},'
    '"status":{{json .State.Status}},"exit_code":{{json .State.ExitCode}},'
    '"health":{{with index .State "Health"}}{{json (index . "Status")}}{{else}}null{{end}},'
    '"labels":{{json .Config.Labels}},"ports":{{json .NetworkSettings.Ports}},'
    '"port_bindings":{{json .HostConfig.PortBindings}},"mounts":{{json .Mounts}}}'
)
IMAGE_FORMAT = '{"local_image_id":{{json .Id}},"repo_digests":{{json .RepoDigests}}}'
SUBPROCESS_TIMEOUT_SECONDS = 30
DOCKER_EMBEDDED_DNS_ADDRESS = "127.0.0.11"


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


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--compose-file", action="append", required=True)
    parser.add_argument("--env-file", required=True)
    parser.add_argument("--project-name", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--pins-file", required=True)
    parser.add_argument("--expected-private-key-path")
    parser.add_argument("--expected-published-port", type=int)
    parser.add_argument("--expected-identityserver-private-key-path")
    parser.add_argument("--expected-identityserver-published-port", type=int)
    parser.add_argument("--services", nargs="+", required=True)
    parser.add_argument(
        "--allow-staged-inventory",
        action="store_true",
        help="verify an approved deployed subset while retaining complete image-pin coverage",
    )
    return parser.parse_args()


def run(command: list[str]) -> subprocess.CompletedProcess[str]:
    try:
        return subprocess.run(
            command,
            check=False,
            capture_output=True,
            text=True,
            stdin=subprocess.DEVNULL,
            timeout=SUBPROCESS_TIMEOUT_SECONDS,
            close_fds=True,
        )
    except subprocess.TimeoutExpired:
        return subprocess.CompletedProcess(
            command,
            124,
            "",
            "runtime verifier subprocess timed out",
        )


def typed_env_value(env_file: str, key: str) -> str:
    parser = Path(__file__).with_name("verify-bolt-phase0-env.py")
    if not parser.is_file():
        raise ValueError("typed Phase 0 env reader is unavailable")
    result = run([sys.executable, str(parser), "--file", env_file, "--key", key])
    if result.returncode != 0 or not result.stdout:
        raise ValueError(f"could not read typed {key} from the deployment env file")
    return result.stdout


def project_container_ids(
    project_name: str,
    service: str,
    runner: Callable[[list[str]], subprocess.CompletedProcess[str]] = run,
) -> list[str]:
    command = [
        "docker",
        "ps",
        "--all",
        "--no-trunc",
        "--quiet",
        "--filter",
        f"label=com.docker.compose.project={project_name}",
        "--filter",
        f"label=com.docker.compose.service={service}",
    ]
    result = runner(command)
    if result.returncode != 0:
        raise RuntimeError(result.stderr.strip() or result.stdout.strip() or f"could not resolve {service}")
    ids = [item.strip() for item in result.stdout.splitlines() if item.strip()]
    if not all(CONTAINER_ID.fullmatch(item) for item in ids):
        raise RuntimeError(f"{service} returned an invalid container ID")
    return ids


def inspect_json(command: list[str]) -> dict[str, Any]:
    result = run(command)
    if result.returncode != 0:
        raise RuntimeError(result.stderr.strip() or result.stdout.strip() or "Docker inspect failed")
    value = json.loads(result.stdout)
    if not isinstance(value, dict):
        raise RuntimeError("Docker formatted inspect output was not an object")
    return value


def decode_address(family: str, encoded: str) -> ipaddress.IPv4Address | ipaddress.IPv6Address:
    raw = bytes.fromhex(encoded)
    if family == "ipv4":
        return ipaddress.IPv4Address(raw[::-1])
    normalized = b"".join(raw[index : index + 4][::-1] for index in range(0, len(raw), 4))
    return ipaddress.IPv6Address(normalized)


def parse_proc_net(content: str, family: str) -> list[dict[str, Any]]:
    listeners: list[dict[str, Any]] = []
    for line in content.splitlines()[1:]:
        fields = line.split()
        if not fields:
            continue
        if len(fields) < 10:
            raise ValueError("malformed /proc TCP record")
        if fields[3] != "0A":
            continue
        address_hex, separator, port_hex = fields[1].partition(":")
        if not separator:
            raise ValueError("malformed /proc TCP listener record")
        address = decode_address(family, address_hex)
        scope = "wildcard" if address.is_unspecified else "loopback" if address.is_loopback else "other"
        listeners.append(
            {
                "address": str(address),
                "family": family,
                "inode": int(fields[9]),
                "scope": scope,
                "port": int(port_hex, 16),
            }
        )
    return listeners


def docker_embedded_dns_is_configured(content: str) -> bool:
    for line in content.splitlines():
        fields = line.partition("#")[0].split()
        if (
            len(fields) >= 2
            and fields[0] == "nameserver"
            and fields[1] == DOCKER_EMBEDDED_DNS_ADDRESS
        ):
            return True
    return False


def parse_process_socket_inodes(content: str) -> set[int]:
    inodes: set[int] = set()
    for line in content.splitlines():
        match = PROCESS_SOCKET.fullmatch(line.strip())
        if not match:
            raise ValueError("malformed process socket record")
        inodes.add(int(match.group(1)))
    return inodes


def collect_listeners(
    container_id: str,
    runner: Callable[[list[str]], subprocess.CompletedProcess[str]] = run,
) -> list[dict[str, Any]]:
    listeners: list[dict[str, Any]] = []
    for proc_file, family in (("/proc/net/tcp", "ipv4"), ("/proc/net/tcp6", "ipv6")):
        result = runner(["docker", "exec", container_id, "cat", proc_file])
        if result.returncode != 0:
            raise RuntimeError(f"could not inspect Hub {family} listeners")
        listeners.extend(parse_proc_net(result.stdout, family))
    resolver = runner(["docker", "exec", container_id, "cat", "/etc/resolv.conf"])
    if resolver.returncode != 0:
        raise RuntimeError("could not inspect container DNS configuration")
    owned_sockets = runner(
        [
            "docker",
            "exec",
            container_id,
            "sh",
            "-c",
            'for fd in /proc/[0-9]*/fd/*; do target=$(readlink "$fd" 2>/dev/null) || continue; '
            'case "$target" in socket:\\[*\\]) printf \'%s\\n\' "$target";; esac; done',
        ]
    )
    if owned_sockets.returncode != 0:
        raise RuntimeError("could not inspect process-owned container sockets")
    process_socket_inodes = parse_process_socket_inodes(owned_sockets.stdout)
    embedded_dns = [
        item
        for item in listeners
        if item["address"] == DOCKER_EMBEDDED_DNS_ADDRESS
        and item["inode"] not in process_socket_inodes
    ]
    if docker_embedded_dns_is_configured(resolver.stdout):
        if len(embedded_dns) != 1:
            raise RuntimeError("Docker embedded DNS listener topology is not exact")
        listeners.remove(embedded_dns[0])
    return sorted(
        listeners,
        key=lambda item: (item["port"], item["family"], item["address"], item["inode"]),
    )


def verify_tls_service_listeners(service: str, listeners: list[dict[str, Any]]) -> list[str]:
    errors: list[str] = []
    normalized = [
        {
            "address": item["address"],
            "family": item["family"],
            "scope": item["scope"],
            "port": item["port"],
        }
        for item in listeners
    ]
    expected_health = {
        "address": "127.0.0.1",
        "family": "ipv4",
        "scope": "loopback",
        "port": 8080,
    }
    expected_tls = {
        "address": "0.0.0.0",
        "family": "ipv4",
        "scope": "wildcard",
        "port": 8443,
    }
    health = [item for item in normalized if item["port"] == 8080]
    tls = [item for item in normalized if item["port"] == 8443]
    if health != [expected_health]:
        errors.append(f"{service}: actual port 8080 listener is not exactly IPv4 127.0.0.1")
    if tls != [expected_tls]:
        errors.append(f"{service}: actual port 8443 listener is not exactly IPv4 wildcard TLS")
    if len(normalized) != 2 or any(
        item not in (expected_health, expected_tls) for item in normalized
    ):
        errors.append(f"{service}: unexpected actual TCP listener topology is present")
    return errors


def binding_ports(
    value: Any,
    expected_host_ips: set[str],
    *,
    allow_unbound_exposed_ports: bool = False,
) -> tuple[set[str], bool]:
    if not isinstance(value, dict) or "8443/tcp" not in value:
        return set(), False
    other_ports = {key: bindings for key, bindings in value.items() if key != "8443/tcp"}
    if (
        any(bindings is not None for bindings in other_ports.values())
        if allow_unbound_exposed_ports
        else bool(other_ports)
    ):
        return set(), False
    bindings = value.get("8443/tcp")
    if not isinstance(bindings, list) or not bindings:
        return set(), False
    if any(not isinstance(item, dict) or set(item) != {"HostIp", "HostPort"} for item in bindings):
        return set(), False
    normalized = [(str(item["HostIp"]), str(item["HostPort"])) for item in bindings]
    ports = {port for _, port in normalized}
    host_ips = {host_ip for host_ip, _ in normalized}
    return (
        ports,
        len(normalized) == len(expected_host_ips)
        and len(set(normalized)) == len(normalized)
        and host_ips == expected_host_ips,
    )


def verify_published_port(
    service: str, container: dict[str, Any], expected_port: int
) -> tuple[dict[str, Any] | None, list[str]]:
    network_ports, network_ok = binding_ports(
        container.get("ports"),
        {"0.0.0.0", "::"},
        allow_unbound_exposed_ports=True,
    )
    host_ports, host_ok = binding_ports(container.get("port_bindings"), {""})
    expected = {str(expected_port)}
    errors: list[str] = []
    if not network_ok or network_ports != expected:
        errors.append(f"{service}: runtime network publication is not exactly the expected 8443/tcp host port")
    if not host_ok or host_ports != expected:
        errors.append(f"{service}: configured port binding is not exactly the expected 8443/tcp host port")
    evidence = (
        {"container_port": 8443, "published_port": expected_port, "protocol": "tcp"}
        if not errors
        else None
    )
    return evidence, errors


def resolve_path(value: str) -> Path:
    path = Path(os.path.expanduser(value))
    if not path.is_absolute():
        raise ValueError("path is not absolute")
    return path.resolve(strict=True)


def private_key_mounts(container: dict[str, Any], private_key: Path) -> tuple[list[dict[str, Any]], list[str]]:
    evidence: list[dict[str, Any]] = []
    errors: list[str] = []
    mounts = container.get("mounts")
    if not isinstance(mounts, list):
        return [], ["runtime mount inspection was unavailable"]
    for mount in mounts:
        if not isinstance(mount, dict) or str(mount.get("Type", "")).lower() != "bind":
            continue
        try:
            source = resolve_path(str(mount.get("Source", "")))
        except (OSError, ValueError):
            errors.append("a runtime bind-mount source could not be resolved")
            continue
        relation = None
        if source == private_key or source.samefile(private_key):
            relation = "exact"
        elif source.is_dir() and private_key.is_relative_to(source):
            relation = "parent-directory"
        if relation:
            evidence.append(
                {
                    "resolved_source": "<expected-private-key>" if relation == "exact" else "<private-key-parent>",
                    "relation": relation,
                    "target": str(mount.get("Destination", "")),
                    "read_only": not bool(mount.get("RW", True)),
                }
            )
    return evidence, errors


def collect_service(
    service: str,
    container_id: str,
    expected_reference: str,
    project_name: str,
    private_key: Path,
    expected_published_port: int,
    inspector: Callable[[list[str]], dict[str, Any]] = inspect_json,
    command_runner: Callable[[list[str]], subprocess.CompletedProcess[str]] = run,
    identity_private_key: Path | None = None,
    identity_expected_published_port: int | None = None,
) -> tuple[dict[str, Any], list[str]]:
    errors: list[str] = []
    container = inspector(["docker", "inspect", "--format", CONTAINER_FORMAT, container_id])
    local_image_id = str(container.get("local_image_id", ""))
    image = inspector(["docker", "image", "inspect", "--format", IMAGE_FORMAT, local_image_id])

    configured_image = str(container.get("configured_image", ""))
    repo_digests = [str(item) for item in image.get("repo_digests") or []]
    health = container.get("health")
    started_at = str(container.get("started_at", ""))
    inspected_container_id = str(container.get("container_id", ""))
    inspected_image_id = str(image.get("local_image_id", ""))
    labels = container.get("labels") if isinstance(container.get("labels"), dict) else {}
    running = container.get("running") is True
    status = str(container.get("status", ""))
    exit_code = container.get("exit_code")

    if not CONTAINER_ID.fullmatch(inspected_container_id) or inspected_container_id != container_id:
        errors.append(f"{service}: invalid or mismatched container ID")
    if labels.get("com.docker.compose.project") != project_name or labels.get("com.docker.compose.service") != service:
        errors.append(f"{service}: container Compose identity labels do not match")
    if configured_image != expected_reference:
        errors.append(f"{service}: configured image does not equal the authorized repository digest")
    if not IMAGE_ID.fullmatch(local_image_id) or inspected_image_id != local_image_id:
        errors.append(f"{service}: invalid or mismatched local image ID")
    if expected_reference not in repo_digests:
        errors.append(f"{service}: RepoDigests does not contain the exact authorized repository digest")
    if not repo_digests or not all(REPO_DIGEST.fullmatch(item) for item in repo_digests):
        errors.append(f"{service}: registry RepoDigests are missing or invalid")
    if not started_at or started_at.startswith("0001-01-01"):
        errors.append(f"{service}: container has no valid started time")
    if service == "migrate":
        if running or status != "exited" or exit_code != 0:
            errors.append("migrate: migration container did not complete successfully")
    elif not running or status != "running" or health != "healthy":
        errors.append(f"{service}: container is not running and healthy")

    hub_mounts, mount_errors = private_key_mounts(container, private_key)
    errors.extend(f"{service}: Hub key {error}" for error in mount_errors)
    identity_mounts: list[dict[str, Any]] = []
    if identity_private_key is not None:
        identity_mounts, identity_mount_errors = private_key_mounts(container, identity_private_key)
        errors.extend(f"{service}: IdentityServer key {error}" for error in identity_mount_errors)
    if service == "bolt-hub":
        key_mount_ok = (
            len(hub_mounts) == 1
            and hub_mounts[0]["relation"] == "exact"
            and hub_mounts[0]["target"] == "/run/secrets/bolt-hub-tls-private-key.pem"
            and hub_mounts[0]["read_only"] is True
            and not identity_mounts
        )
        if not key_mount_ok:
            errors.append("bolt-hub: resolved private-key mount is missing, broad, duplicated, writable, or mis-targeted")
    elif service == "identityserver":
        key_mount_ok = (
            identity_private_key is not None
            and len(identity_mounts) == 1
            and identity_mounts[0]["relation"] == "exact"
            and identity_mounts[0]["target"] == "/run/secrets/identityserver-tls-private-key.pem"
            and identity_mounts[0]["read_only"] is True
            and not hub_mounts
        )
        if not key_mount_ok:
            errors.append("identityserver: resolved private-key mount is missing, broad, duplicated, writable, cross-mounted, or mis-targeted")
    elif hub_mounts or identity_mounts:
        errors.append(f"{service}: resolved runtime mount exposes a TLS private key")

    listener_evidence: list[dict[str, Any]] = []
    publication_evidence: dict[str, Any] | None = None
    if service in {"bolt-hub", "identityserver"}:
        observed_listeners = collect_listeners(container_id, command_runner)
        errors.extend(verify_tls_service_listeners(service, observed_listeners))
        # The sealed runtime.v2 contract retains redacted topology after exact raw validation.
        listener_evidence = [
            {"family": item["family"], "scope": item["scope"], "port": item["port"]}
            for item in observed_listeners
        ]
        service_published_port = (
            expected_published_port
            if service == "bolt-hub"
            else identity_expected_published_port
        )
        if service_published_port is None:
            errors.append(f"{service}: expected TLS publication port is unavailable")
        else:
            publication_evidence, publication_errors = verify_published_port(
                service, container, service_published_port
            )
            errors.extend(publication_errors)

    evidence = {
        "service": service,
        "container_name": str(container.get("container_name", "")).lstrip("/"),
        "container_id": inspected_container_id,
        "configured_image": configured_image,
        "local_image_id": local_image_id,
        "repo_digests": repo_digests,
        "started_at": started_at,
        "running": running,
        "status": status,
        "exit_code": exit_code if service == "migrate" else None,
        "health": health,
        "listeners": listener_evidence,
        "published_port": publication_evidence,
        "private_key_mounts": [*hub_mounts, *identity_mounts],
    }
    return evidence, errors


def load_pins(path: Path) -> dict[str, str]:
    document = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(document, dict) or document.get("schema") != "xframework.bolt.phase0.image-pins.v2":
        raise ValueError("pins file has an unsupported schema")
    if document.get("status") != "passed" or document.get("registry_confirmed") is not True:
        raise ValueError("pins file did not pass registry confirmation")
    pins = document.get("pins")
    repositories = document.get("approved_repositories")
    if (
        not isinstance(pins, dict)
        or not isinstance(repositories, dict)
        or set(pins) != set(PHASE0_IMAGE_SERVICES)
        or set(repositories) != set(PHASE0_IMAGE_SERVICES)
    ):
        raise ValueError("runtime pin coverage does not exactly match the Phase 0 service inventory")
    result = {str(service): str(reference) for service, reference in pins.items()}
    for service, reference in result.items():
        repository, separator, digest = reference.rpartition("@")
        if (
            not separator
            or not REPO_DIGEST.fullmatch(reference)
            or ":" in repository.rsplit("/", maxsplit=1)[-1]
            or not reference.startswith(f"{repositories.get(service)}@")
        ):
            raise ValueError(f"{service}: runtime pin is not in its approved repository")
    return result


def validate_runtime_services(services: list[str], allow_staged_inventory: bool) -> None:
    requested_services = set(services)
    if len(services) != len(requested_services):
        raise ValueError("runtime services must be unique")
    if allow_staged_inventory:
        if (
            not requested_services
            or not requested_services.issubset(PHASE0_SERVICES)
            or not {"migrate", "bolt-hub"}.issubset(requested_services)
        ):
            raise ValueError(
                "staged runtime services must be an approved subset containing migrate and bolt-hub"
            )
    elif requested_services != set(PHASE0_SERVICES):
        raise ValueError("runtime services must be the unique complete Phase 0 service inventory")


def main() -> int:
    args = parse_args()
    errors: list[str] = []
    services: dict[str, Any] = {}
    pins: dict[str, str] = {}

    try:
        if not PROJECT_NAME.fullmatch(args.project_name):
            raise ValueError("project name is not a safe canonical Compose project name")
        validate_runtime_services(args.services, args.allow_staged_inventory)
        private_key_value = args.expected_private_key_path or typed_env_value(
            args.env_file, "BOLT_HUB_TLS_PRIVATE_KEY_PATH"
        )
        identity_private_key_value = (
            args.expected_identityserver_private_key_path
            or typed_env_value(args.env_file, "IDENTITYSERVER_TLS_PRIVATE_KEY_PATH")
        )
        published_port = args.expected_published_port
        if published_port is None:
            published_port = int(typed_env_value(args.env_file, "BOLT_HUB_EXPOSE_PORT"))
        if not 1 <= published_port <= 65535:
            raise ValueError("expected published port is outside the TCP port range")
        identity_published_port = args.expected_identityserver_published_port
        if identity_published_port is None:
            identity_published_port = int(
                typed_env_value(args.env_file, "IDENTITYSERVER_PUBLIC_HTTPS_PORT")
            )
        if not 1 <= identity_published_port <= 65535:
            raise ValueError("expected IdentityServer published port is outside the TCP port range")
        for compose_file in args.compose_file:
            if not Path(compose_file).is_file():
                raise ValueError("a Compose file does not exist")
        if not Path(args.env_file).is_file():
            raise ValueError("env file does not exist")
        private_key = resolve_path(private_key_value)
        if not private_key.is_file():
            raise ValueError("expected private-key path is not a file")
        identity_private_key = resolve_path(identity_private_key_value)
        if not identity_private_key.is_file():
            raise ValueError("expected IdentityServer private-key path is not a file")
        if private_key.samefile(identity_private_key):
            raise ValueError("Hub and IdentityServer private keys must be distinct files")
        pins = load_pins(Path(args.pins_file))
    except (OSError, ValueError, json.JSONDecodeError) as error:
        private_key = Path("/")
        identity_private_key = Path("/")
        identity_published_port = 0
        errors.append(str(error))

    if not errors:
        for service in args.services:
            try:
                container_ids = project_container_ids(args.project_name, service)
                if len(container_ids) != 1:
                    raise RuntimeError(f"resolved to {len(container_ids)} containers; expected exactly one")
                evidence, service_errors = collect_service(
                    service,
                    container_ids[0],
                    pins[service],
                    args.project_name,
                    private_key,
                    published_port,
                    identity_private_key=identity_private_key,
                    identity_expected_published_port=identity_published_port,
                )
                services[service] = evidence
                errors.extend(service_errors)
            except Exception as error:
                errors.append(f"{service}: {error}")

    evidence = {
        "schema": "xframework.bolt.phase0.runtime.v2",
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "status": "passed" if not errors else "failed",
        "inventory_mode": "staged" if args.allow_staged_inventory else "complete",
        "requested_services": list(args.services),
        "expected_images": {service: pins.get(service) for service in PHASE0_IMAGE_SERVICES},
        "intentionally_inactive_services": list(INACTIVE_IMAGE_SERVICES),
        "services": services,
        "errors": errors,
    }
    output = Path(args.output)
    write_private_json(output, evidence)

    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        print(f"Bolt Phase 0 runtime evidence failed; evidence: {output}", file=sys.stderr)
        return 1
    print(f"Bolt Phase 0 runtime evidence passed; evidence: {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
