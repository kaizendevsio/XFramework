#!/usr/bin/env python3
"""Create Phase 0 pins from unambiguous registry manifest inspection results."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import stat
import sys
import tempfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


SERVICE = re.compile(r"^[a-z0-9][a-z0-9-]*$")
SHA_TAG = re.compile(r"^[0-9a-f]{40}$")
DIGEST = re.compile(r"^sha256:[0-9a-f]{64}$")
REPOSITORY_COMPONENT = re.compile(r"^[a-z0-9]+(?:[._-][a-z0-9]+)*$")
REGISTRY_COMPONENT = re.compile(r"^[a-z0-9]+(?:[.-][a-z0-9]+)*(?::[1-9][0-9]{0,4})?$")

# Repository ownership is intentionally explicit. A service can never authorize a
# sibling service's image even when both images happen to have the same digest.
APPROVED_REPOSITORY_NAMES = {
    "migrate": "migrate",
    "bolt-phase0-synthetics": "bolt-phase0-synthetics",
    "bolt-hub": "bolt-hub",
    "identityserver": "identityserver",
    "communications": "communications",
    "notifications": "notifications",
    "storage": "storage",
    "attendance": "attendance",
    "smsgateway": "smsgateway",
    "wallets": "wallets",
    "inventario": "inventario",
    "pos": "pos",
    "portal": "portal",
    "operations-dashboard": "operations-dashboard",
}


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


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    return f"sha256:{digest}"


def validate_repository_prefix(value: str) -> str:
    if not value or value != value.lower() or value.endswith("/") or "://" in value or "@" in value:
        raise ValueError("approved repository prefix must be a canonical lowercase registry/namespace")
    parts = value.split("/")
    if any(not part for part in parts) or not REGISTRY_COMPONENT.fullmatch(parts[0]):
        raise ValueError("approved repository prefix has an invalid registry component")
    if not all(REPOSITORY_COMPONENT.fullmatch(part) for part in parts[1:]):
        raise ValueError("approved repository prefix has an invalid namespace component")
    return value


def approved_repositories(prefix: str, services: list[str]) -> dict[str, str]:
    normalized = validate_repository_prefix(prefix)
    unknown = sorted(set(services) - set(APPROVED_REPOSITORY_NAMES))
    if unknown:
        raise ValueError(f"services have no approved repository mapping: {unknown}")
    return {service: f"{normalized}/{APPROVED_REPOSITORY_NAMES[service]}" for service in services}


def select_manifest_digest(tagged_image: str, manifest: Any) -> str:
    records = manifest if isinstance(manifest, list) else [manifest]
    if not records or not all(isinstance(item, dict) for item in records):
        raise ValueError("registry manifest must contain one or more manifest records")

    candidates: list[str] = []
    identities: set[tuple[str, str | None, str | None, str | None]] = set()
    for record in records:
        descriptor = record.get("Descriptor")
        reference = record.get("Ref")
        if not isinstance(descriptor, dict) or not isinstance(reference, str):
            raise ValueError("registry manifest record requires Ref and Descriptor")

        digest = descriptor.get("digest")
        if not isinstance(digest, str) or not DIGEST.fullmatch(digest):
            raise ValueError("registry manifest Descriptor.digest must be an exact lowercase sha256 digest")
        if reference != f"{tagged_image}@{digest}":
            raise ValueError("registry manifest Ref does not bind the requested tagged image to Descriptor.digest")

        platform = descriptor.get("platform")
        if platform is None:
            if len(records) != 1:
                raise ValueError("multi-record registry manifests require an explicit platform on every record")
            os_name = architecture = variant = None
        elif isinstance(platform, dict):
            os_name = platform.get("os")
            architecture = platform.get("architecture")
            variant = platform.get("variant")
            if not isinstance(os_name, str) or not isinstance(architecture, str):
                raise ValueError("registry manifest platform requires string os and architecture values")
            if variant is not None and not isinstance(variant, str):
                raise ValueError("registry manifest platform variant must be a string")
        else:
            raise ValueError("registry manifest platform must be an object")

        identity = (reference, os_name, architecture, variant)
        if identity in identities:
            raise ValueError("registry manifest contains a duplicate manifest record")
        identities.add(identity)

        if platform is None or (os_name == "linux" and architecture == "amd64" and variant in {None, ""}):
            candidates.append(digest)

    if len(candidates) != 1:
        raise ValueError("registry manifest must resolve to exactly one linux/amd64 image")
    return candidates[0]


def build_pins(
    expected_tag: str,
    expected_services: list[str],
    registry_records: list[tuple[str, str, Any]],
    repository_prefix: str,
) -> dict[str, str]:
    if not SHA_TAG.fullmatch(expected_tag):
        raise ValueError("expected image tag must be a lowercase full 40-character commit SHA")
    if (
        not expected_services
        or len(expected_services) != len(set(expected_services))
        or not all(SERVICE.fullmatch(item) for item in expected_services)
    ):
        raise ValueError("expected services must be non-empty, unique valid Compose service names")

    repositories = approved_repositories(repository_prefix, expected_services)
    pins: dict[str, str] = {}
    for service, tagged_image, registry_manifest in registry_records:
        if not SERVICE.fullmatch(service) or service in pins:
            raise ValueError(f"duplicate or invalid registry-confirmed service: {service}")
        if service not in repositories:
            raise ValueError(f"unexpected registry-confirmed service: {service}")
        expected_repository = repositories[service]
        if tagged_image != f"{expected_repository}:{expected_tag}":
            raise ValueError(f"{service}: tagged image does not match its approved repository and source commit")
        digest = select_manifest_digest(tagged_image, registry_manifest)
        pins[service] = f"{expected_repository}@{digest}"

    missing = sorted(set(expected_services) - set(pins))
    extra = sorted(set(pins) - set(expected_services))
    if missing or extra:
        raise ValueError(f"pin coverage mismatch; missing={missing}, extra={extra}")
    return pins


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--expected-image-tag", required=True)
    parser.add_argument("--expected-service", action="append", default=[])
    parser.add_argument("--approved-repository-prefix", required=True)
    parser.add_argument(
        "--registry-manifest",
        action="append",
        nargs=3,
        metavar=("SERVICE", "TAGGED_IMAGE", "MANIFEST_JSON"),
        default=[],
    )
    parser.add_argument("--output-override", required=True)
    parser.add_argument("--output-evidence", required=True)
    args = parser.parse_args()

    errors: list[str] = []
    pins: dict[str, str] = {}
    repositories: dict[str, str] = {}
    manifest_records: list[tuple[str, str, Any]] = []
    manifest_evidence: dict[str, dict[str, str]] = {}
    try:
        repositories = approved_repositories(args.approved_repository_prefix, args.expected_service)
        for service, tagged_image, manifest_path_value in args.registry_manifest:
            manifest_path = Path(manifest_path_value)
            manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
            manifest_records.append((service, tagged_image, manifest))
            manifest_evidence[service] = {
                "requested_ref": tagged_image,
                "manifest_sha256": sha256_file(manifest_path),
            }
        pins = build_pins(
            args.expected_image_tag,
            args.expected_service,
            manifest_records,
            args.approved_repository_prefix,
        )
        for service, pin in pins.items():
            manifest_evidence[service]["pin"] = pin
    except (OSError, ValueError, json.JSONDecodeError) as error:
        errors.append(str(error))

    evidence = {
        "schema": "xframework.bolt.phase0.image-pins.v2",
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "status": "passed" if not errors else "failed",
        "source_commit": args.expected_image_tag,
        "approved_repositories": repositories,
        "registry_confirmed": not errors,
        "registry_manifests": manifest_evidence,
        "pins": pins,
        "errors": errors,
    }
    evidence_path = Path(args.output_evidence)
    write_private_json(evidence_path, evidence)

    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        return 1

    override = {"services": {service: {"image": reference} for service, reference in sorted(pins.items())}}
    override_path = Path(args.output_override)
    write_private_json(override_path, override)
    print(f"Created registry-confirmed digest pins for {len(pins)} services")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
