#!/usr/bin/env python3
"""Verify the Tailscale-owned Bolt and IdentityServer HTTPS boundary."""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import os
import re
import sys
from pathlib import Path
from typing import Any, Iterable, Mapping


EVIDENCE_SCHEMA = "xframework.bolt.tailscale-boundary.v1"
MAGICDNS_SUFFIX = ".ts.net"
HOST_LABEL = re.compile(r"^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$")
TLS_MATERIAL = re.compile(
    r"(?:^|[/_.-])(?:ca|cert|certificate|certificates|fullchain|https|tls)"
    r"(?:$|[/_.-])|private[-_.]?key|\.(?:cer|crt|key|p12|pfx|pem)(?:$|[/_.-])",
    re.IGNORECASE,
)
OWNED_LISTENERS = {
    "bolt-hub": {
        "serve_port": 7000,
        "backend": "http://127.0.0.1:7000",
        "container_port": 8080,
    },
    "identityserver": {
        "serve_port": 8261,
        "backend": "http://127.0.0.1:8261",
        "container_port": 8080,
    },
}


JsonObject = dict[str, Any]


def validate_magicdns_host(value: str) -> str:
    if (
        not value
        or value != value.lower()
        or value.endswith(".")
        or len(value) > 253
        or not value.endswith(MAGICDNS_SUFFIX)
        or any(character in value for character in "*/:@\\")
    ):
        raise ValueError("MagicDNS host must be a canonical lowercase .ts.net hostname")
    if not all(HOST_LABEL.fullmatch(label) for label in value.split(".")):
        raise ValueError("MagicDNS host contains an invalid DNS label")
    return value


def _mapping(value: Any) -> Mapping[str, Any] | None:
    return value if isinstance(value, dict) else None


def _canonical_port(value: Any) -> int | None:
    if isinstance(value, bool):
        return None
    if isinstance(value, int):
        return value if 1 <= value <= 65535 else None
    if isinstance(value, str) and value.isascii() and value.isdigit():
        port = int(value)
        if value == str(port) and 1 <= port <= 65535:
            return port
    return None


def _owned_host_port(key: str, magicdns_host: str) -> int | None:
    host, separator, port_text = key.rpartition(":")
    if not separator or host.rstrip(".").lower() != magicdns_host:
        return None
    port = _canonical_port(port_text)
    owned_ports = {listener["serve_port"] for listener in OWNED_LISTENERS.values()}
    return port if port in owned_ports else None


def _walk_allow_funnel(value: Any) -> Iterable[Mapping[str, Any]]:
    if isinstance(value, dict):
        allow_funnel = value.get("AllowFunnel")
        if isinstance(allow_funnel, dict):
            yield allow_funnel
        for nested in value.values():
            yield from _walk_allow_funnel(nested)
    elif isinstance(value, list):
        for nested in value:
            yield from _walk_allow_funnel(nested)


def validate_no_owned_funnel(
    config: Any, magicdns_host: str, source: str = "serve"
) -> list[str]:
    errors: list[str] = []
    if not isinstance(config, dict):
        return [f"{source}.config.invalid"]
    for allow_funnel in _walk_allow_funnel(config):
        for key, enabled in allow_funnel.items():
            if not isinstance(key, str):
                continue
            port = _owned_host_port(key, magicdns_host)
            if port is not None and enabled is not False:
                errors.append(f"{source}.allow_funnel.enabled:{port}")
    return sorted(set(errors))


def validate_serve_status(config: Any, magicdns_host: str) -> list[str]:
    if not isinstance(config, dict):
        return ["serve.config.invalid"]

    errors = validate_no_owned_funnel(config, magicdns_host)
    tcp = _mapping(config.get("TCP"))
    web = _mapping(config.get("Web"))
    if tcp is None:
        tcp = {}
    if web is None:
        web = {}

    for listener in OWNED_LISTENERS.values():
        port = listener["serve_port"]
        tcp_handler = _mapping(tcp.get(str(port)))
        if (
            tcp_handler is None
            or tcp_handler.get("HTTPS") is not True
            or tcp_handler.get("HTTP") not in (None, False)
            or tcp_handler.get("TCPForward") not in (None, "")
        ):
            errors.append(f"serve.https_listener.invalid:{port}")

        host_port = f"{magicdns_host}:{port}"
        web_config = _mapping(web.get(host_port))
        handlers = _mapping(web_config.get("Handlers")) if web_config else None
        root_handler = _mapping(handlers.get("/")) if handlers else None
        if root_handler is None:
            errors.append(f"serve.root_proxy.missing:{port}")
            continue
        if set(handlers) != {"/"}:
            errors.append(f"serve.extra_handler.forbidden:{port}")
        if root_handler.get("Proxy") != listener["backend"]:
            errors.append(f"serve.root_proxy.wrong_target:{port}")
        if any(
            root_handler.get(field) not in (None, "", False)
            for field in ("Path", "Text", "TCPForward")
        ):
            errors.append(f"serve.root_proxy.conflicting_handler:{port}")

    return sorted(set(errors))


def _mask_owned_serve_state(config: Any, magicdns_host: str) -> Any:
    if not isinstance(config, dict):
        return config
    masked = copy.deepcopy(config)
    owned_ports = [listener["serve_port"] for listener in OWNED_LISTENERS.values()]

    tcp = masked.get("TCP")
    if isinstance(tcp, dict):
        for port in owned_ports:
            tcp.pop(str(port), None)
        if not tcp:
            masked.pop("TCP", None)

    web = masked.get("Web")
    if isinstance(web, dict):
        for port in owned_ports:
            host_port = f"{magicdns_host}:{port}"
            web_config = web.get(host_port)
            if not isinstance(web_config, dict):
                continue
            handlers = web_config.get("Handlers")
            if isinstance(handlers, dict):
                handlers.pop("/", None)
                if not handlers:
                    web_config.pop("Handlers", None)
            if not web_config:
                web.pop(host_port, None)
        if not web:
            masked.pop("Web", None)

    allow_funnel = masked.get("AllowFunnel")
    if isinstance(allow_funnel, dict):
        for port in owned_ports:
            allow_funnel.pop(f"{magicdns_host}:{port}", None)
        if not allow_funnel:
            masked.pop("AllowFunnel", None)
    return masked


def validate_unrelated_serve_preserved(
    before: Any, after: Any, magicdns_host: str
) -> list[str]:
    if not isinstance(before, dict) or not isinstance(after, dict):
        return ["serve.preservation.input_invalid"]
    if _mask_owned_serve_state(before, magicdns_host) != _mask_owned_serve_state(
        after, magicdns_host
    ):
        return ["serve.preservation.unrelated_changed"]
    return []


def _flatten_strings(value: Any) -> Iterable[str]:
    if isinstance(value, str):
        yield value
    elif isinstance(value, dict):
        for key, nested in value.items():
            if isinstance(key, str):
                yield key
            yield from _flatten_strings(nested)
    elif isinstance(value, list):
        for nested in value:
            yield from _flatten_strings(nested)


def _secret_source(reference: Any) -> str | None:
    if isinstance(reference, str):
        return reference
    if isinstance(reference, dict) and isinstance(reference.get("source"), str):
        return reference["source"]
    return None


def _has_tls_secret(
    references: Any, top_level_secrets: Mapping[str, Any]
) -> bool:
    if references in (None, []):
        return False
    if not isinstance(references, list):
        return True
    for reference in references:
        candidates = list(_flatten_strings(reference))
        source = _secret_source(reference)
        if source is not None and source in top_level_secrets:
            candidates.extend(_flatten_strings(top_level_secrets[source]))
        if any(TLS_MATERIAL.search(candidate) for candidate in candidates):
            return True
    return False


def _has_tls_volume(
    mounts: Any, top_level_volumes: Mapping[str, Any]
) -> bool:
    if mounts in (None, []):
        return False
    if not isinstance(mounts, list):
        return True
    for mount in mounts:
        candidates = list(_flatten_strings(mount))
        source = _secret_source(mount)
        if source is not None and source in top_level_volumes:
            candidates.extend(_flatten_strings(top_level_volumes[source]))
        if any(TLS_MATERIAL.search(candidate) for candidate in candidates):
            return True
    return False


def _environment_items(environment: Any) -> list[tuple[str, Any]] | None:
    if environment is None:
        return []
    if isinstance(environment, dict):
        return [(key, value) for key, value in environment.items() if isinstance(key, str)]
    if isinstance(environment, list):
        result: list[tuple[str, Any]] = []
        for item in environment:
            if not isinstance(item, str) or "=" not in item:
                return None
            key, value = item.split("=", 1)
            result.append((key, value))
        return result
    return None


def _has_kestrel_https_environment(environment: Any) -> bool:
    items = _environment_items(environment)
    if items is None:
        return True
    for key, value in items:
        upper_key = key.upper()
        lower_value = "" if value is None else str(value).lower()
        if upper_key in {"ASPNETCORE_HTTPS_PORT", "ASPNETCORE_HTTPS_PORTS"}:
            return True
        if upper_key == "ASPNETCORE_URLS" and "https://" in lower_value:
            return True
        if "KESTREL" in upper_key and (
            any(marker in upper_key for marker in ("HTTPS", "CERTIFICATE", "TLS"))
            or "https://" in lower_value
        ):
            return True
    return False


def _uses_shared_dockerfile(service: Mapping[str, Any]) -> bool:
    build = _mapping(service.get("build"))
    if build is None:
        return False
    dockerfile = build.get("dockerfile")
    if not isinstance(dockerfile, str):
        return False
    normalized = dockerfile.replace("\\", "/").rstrip("/")
    return normalized.rsplit("/", 1)[-1] == "Dockerfile"


def validate_compose(config: Any) -> list[str]:
    if not isinstance(config, dict):
        return ["compose.config.invalid"]
    services = _mapping(config.get("services"))
    if services is None:
        return ["compose.services.invalid"]
    top_level_secrets = _mapping(config.get("secrets")) or {}
    top_level_volumes = _mapping(config.get("volumes")) or {}
    errors: list[str] = []

    for service_name, listener in OWNED_LISTENERS.items():
        service = _mapping(services.get(service_name))
        if service is None:
            errors.append(f"compose.service.missing:{service_name}")
            continue
        if str(service.get("network_mode", "")).lower() == "host":
            errors.append(f"compose.host_network.forbidden:{service_name}")

        ports = service.get("ports")
        valid_publication = isinstance(ports, list) and len(ports) == 1
        if valid_publication:
            publication = _mapping(ports[0])
            valid_publication = (
                publication is not None
                and publication.get("host_ip") == "127.0.0.1"
                and _canonical_port(publication.get("published"))
                == listener["serve_port"]
                and _canonical_port(publication.get("target"))
                == listener["container_port"]
                and publication.get("protocol") == "tcp"
                and publication.get("mode") == "ingress"
            )
        if not valid_publication:
            errors.append(f"compose.loopback_publication.invalid:{service_name}")

    for service_name, service_value in services.items():
        service = _mapping(service_value)
        if service is None or not _uses_shared_dockerfile(service):
            continue
        if _has_tls_secret(service.get("secrets"), top_level_secrets):
            errors.append(f"compose.tls_secret.forbidden:{service_name}")
        if _has_tls_volume(service.get("volumes"), top_level_volumes):
            errors.append(f"compose.tls_volume.forbidden:{service_name}")
        if _has_kestrel_https_environment(service.get("environment")):
            errors.append(f"compose.kestrel_https_env.forbidden:{service_name}")

    return sorted(set(errors))


def verify_boundary(
    serve_status: Any,
    compose_config: Any,
    magicdns_host: str,
    *,
    funnel_config: Any | None = None,
    previous_serve_status: Any | None = None,
) -> list[str]:
    magicdns_host = validate_magicdns_host(magicdns_host)
    errors = validate_serve_status(serve_status, magicdns_host)
    errors.extend(validate_compose(compose_config))
    if funnel_config is not None:
        errors.extend(
            validate_no_owned_funnel(funnel_config, magicdns_host, source="funnel")
        )
    if previous_serve_status is not None:
        errors.extend(
            validate_unrelated_serve_preserved(
                previous_serve_status, serve_status, magicdns_host
            )
        )
    return sorted(set(errors))


def _digest(value: Any) -> str:
    encoded = json.dumps(
        value, ensure_ascii=True, separators=(",", ":"), sort_keys=True
    ).encode("utf-8")
    return "sha256:" + hashlib.sha256(encoded).hexdigest()


def build_evidence(
    *,
    errors: list[str],
    serve_status: Any,
    compose_config: Any,
    magicdns_host: str,
    funnel_config: Any | None,
    previous_serve_status: Any | None,
) -> JsonObject:
    documents = {
        "compose": _digest(compose_config),
        "magicdns_host": _digest(magicdns_host),
        "serve_status": _digest(serve_status),
    }
    if funnel_config is not None:
        documents["funnel_config"] = _digest(funnel_config)
    if previous_serve_status is not None:
        documents["previous_serve_status"] = _digest(previous_serve_status)
    evidence: JsonObject = {
        "schema": EVIDENCE_SCHEMA,
        "status": "failed" if errors else "passed",
        "expected": {
            "container_http_port": 8080,
            "https_serve_ports": [7000, 8261],
            "host_bind": "127.0.0.1",
        },
        "input_digests": documents,
    }
    if errors:
        evidence["errors"] = errors
    return evidence


def _load_json(path: Path) -> Any:
    with path.open("r", encoding="utf-8-sig") as stream:
        return json.load(stream)


def _write_evidence(path: str, evidence: JsonObject) -> None:
    payload = json.dumps(evidence, ensure_ascii=True, separators=(",", ":"), sort_keys=True)
    if path == "-":
        sys.stdout.write(payload + "\n")
        return

    destination = Path(path)
    temporary = destination.with_name(f".{destination.name}.tmp-{os.getpid()}")
    descriptor = os.open(temporary, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    try:
        with os.fdopen(descriptor, "w", encoding="ascii", newline="\n") as stream:
            stream.write(payload + "\n")
        os.replace(temporary, destination)
    except BaseException:
        temporary.unlink(missing_ok=True)
        raise


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--serve-status-json", required=True)
    parser.add_argument("--compose-json", required=True)
    parser.add_argument("--magicdns-host", required=True)
    parser.add_argument("--funnel-config-json")
    parser.add_argument("--previous-serve-status-json")
    parser.add_argument("--evidence", default="-")
    args = parser.parse_args()

    try:
        magicdns_host = validate_magicdns_host(args.magicdns_host)
        serve_status = _load_json(Path(args.serve_status_json))
        compose_config = _load_json(Path(args.compose_json))
        funnel_config = (
            _load_json(Path(args.funnel_config_json))
            if args.funnel_config_json
            else None
        )
        previous_serve_status = (
            _load_json(Path(args.previous_serve_status_json))
            if args.previous_serve_status_json
            else None
        )
        errors = verify_boundary(
            serve_status,
            compose_config,
            magicdns_host,
            funnel_config=funnel_config,
            previous_serve_status=previous_serve_status,
        )
        evidence = build_evidence(
            errors=errors,
            serve_status=serve_status,
            compose_config=compose_config,
            magicdns_host=magicdns_host,
            funnel_config=funnel_config,
            previous_serve_status=previous_serve_status,
        )
        _write_evidence(args.evidence, evidence)
    except (OSError, ValueError, json.JSONDecodeError) as error:
        print(f"ERROR: boundary verification input is invalid: {error}", file=sys.stderr)
        return 2

    if errors:
        print("ERROR: " + ", ".join(errors), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
