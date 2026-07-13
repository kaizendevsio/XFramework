#!/usr/bin/env python3
"""Verify that Cosign-verified DSSE provenance binds every Phase 0 image pin."""

from __future__ import annotations

import argparse
import base64
import binascii
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


SHA = re.compile(r"^[0-9a-f]{40}$")
DIGEST = re.compile(r"^sha256:[0-9a-f]{64}$")
EXACT_IMAGE = re.compile(r"^[a-z0-9][a-z0-9./:_-]*@sha256:[0-9a-f]{64}$")
TRUST_VALUE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9./:@_+?=&%-]*$")
BUILD_INPUT_SCHEMA = "xframework.bolt.phase0.build-inputs.v1"
OUTPUT_SCHEMA = "xframework.bolt.phase0.provenance.v1"
BUILD_TYPE = "https://xframework.dev/build-types/compose-dotnet-v1"
PREDICATE_TYPE = "https://slsa.dev/provenance/v1"


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
STATEMENT_TYPE = "https://in-toto.io/Statement/v1"
DSSE_PAYLOAD_TYPE = "application/vnd.in-toto+json"
TRUSTED_OIDC_ISSUER = "https://token.actions.githubusercontent.com"
FROM_INSTRUCTION = re.compile(r"^\s*FROM\s+(?:--platform=\S+\s+)?(\S+)", re.IGNORECASE)
PROJECT_PATH = re.compile(r"^[A-Za-z0-9._/-]+\.csproj$")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return f"sha256:{digest.hexdigest()}"


def sha256_bytes(value: bytes) -> str:
    return f"sha256:{hashlib.sha256(value).hexdigest()}"


def exact_image_reference(value: str) -> bool:
    repository, separator, digest = value.rpartition("@")
    return bool(
        separator
        and EXACT_IMAGE.fullmatch(value)
        and DIGEST.fullmatch(digest)
        and ":" not in repository.rsplit("/", maxsplit=1)[-1]
    )


def normalize_dockerfile_base(value: str) -> str:
    repository_with_tag, separator, digest = value.rpartition("@")
    if not separator or not DIGEST.fullmatch(digest):
        raise ValueError("Dockerfile base images must be pinned by an exact lowercase sha256 digest")
    prefix, slash, final = repository_with_tag.rpartition("/")
    if ":" in final:
        final = final.rsplit(":", 1)[0]
    repository = f"{prefix}{slash}{final}"
    normalized = f"{repository}@{digest}"
    if not exact_image_reference(normalized):
        raise ValueError("Dockerfile base image repository is not canonical")
    return normalized


def dockerfile_base_images(path: Path) -> list[str]:
    images: list[str] = []
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        match = FROM_INSTRUCTION.match(line)
        if match:
            images.append(normalize_dockerfile_base(match.group(1)))
    if not images:
        raise ValueError("reviewed Dockerfile contains no base images")
    return images


def load_pin_document(path: Path, source_commit: str) -> dict[str, str]:
    document = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(document, dict):
        raise ValueError("pin evidence must be an object")
    if document.get("schema") != "xframework.bolt.phase0.image-pins.v2":
        raise ValueError("pin evidence has an unsupported schema")
    if document.get("status") != "passed" or document.get("registry_confirmed") is not True:
        raise ValueError("pin evidence did not pass registry confirmation")
    if document.get("source_commit") != source_commit:
        raise ValueError("pin evidence source commit does not match the reviewed commit")
    pins = document.get("pins")
    repositories = document.get("approved_repositories")
    if not isinstance(pins, dict) or not isinstance(repositories, dict) or set(pins) != set(repositories):
        raise ValueError("pin evidence repository coverage is invalid")
    result: dict[str, str] = {}
    for service, reference in pins.items():
        repository = str(repositories.get(service, ""))
        exact_reference = str(reference)
        if not exact_image_reference(exact_reference) or not exact_reference.startswith(f"{repository}@"):
            raise ValueError(f"{service}: pin is not in its approved repository")
        result[str(service)] = exact_reference
    if not result:
        raise ValueError("pin evidence contains no services")
    return result


def require_exact_keys(value: dict[str, Any], expected: set[str], description: str) -> None:
    if set(value) != expected:
        raise ValueError(f"{description} fields do not match the required schema")


def load_build_inputs(path: Path, source_commit: str, services: set[str]) -> dict[str, dict[str, Any]]:
    document = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(document, dict) or document.get("schema") != BUILD_INPUT_SCHEMA:
        raise ValueError("build inputs have an unsupported schema")
    if document.get("source_commit") != source_commit:
        raise ValueError("build inputs source commit does not match")
    entries = document.get("services")
    if not isinstance(entries, dict) or set(entries) != services:
        raise ValueError("build-input service coverage does not match image pins")

    result: dict[str, dict[str, Any]] = {}
    for service, entry in entries.items():
        if not isinstance(entry, dict):
            raise ValueError(f"{service}: build input must be an object")
        require_exact_keys(entry, {"context", "dockerfile", "args", "target"}, f"{service}: build input")
        if entry["context"] != "." or entry["dockerfile"] != "Dockerfile" or entry["target"] is not None:
            raise ValueError(f"{service}: build context, Dockerfile, or target is not approved")
        args = entry["args"]
        if (
            not isinstance(args, dict)
            or set(args) != {"PROJECT_PATH"}
            or not isinstance(args["PROJECT_PATH"], str)
            or not PROJECT_PATH.fullmatch(args["PROJECT_PATH"])
        ):
            raise ValueError(f"{service}: build args must match the nonsecret PROJECT_PATH contract")
        result[str(service)] = entry
    return result


def load_json_values(path: Path) -> list[Any]:
    text = path.read_text(encoding="utf-8-sig").strip()
    if not text:
        raise ValueError(f"{path}: Cosign evidence is empty")
    try:
        value = json.loads(text)
        return value if isinstance(value, list) else [value]
    except json.JSONDecodeError:
        values = [json.loads(line) for line in text.splitlines() if line.strip()]
        if not values:
            raise ValueError(f"{path}: Cosign evidence contains no JSON values")
        return values


def normalize_envelope(value: Any) -> dict[str, Any] | None:
    if not isinstance(value, dict):
        return None
    candidate = value.get("dsseEnvelope", value)
    if not isinstance(candidate, dict) or not {"payload", "payloadType", "signatures"}.issubset(candidate):
        return None
    require_exact_keys(candidate, {"payload", "payloadType", "signatures"}, "DSSE envelope")
    if candidate["payloadType"] != DSSE_PAYLOAD_TYPE or not isinstance(candidate["payload"], str):
        raise ValueError("DSSE envelope payload type or payload is invalid")
    signatures = candidate["signatures"]
    if not isinstance(signatures, list) or not signatures:
        raise ValueError("DSSE envelope contains no signatures")
    normalized_signatures: list[dict[str, str]] = []
    for signature in signatures:
        if not isinstance(signature, dict) or set(signature) not in ({"sig"}, {"keyid", "sig"}):
            raise ValueError("DSSE signature has an unsupported shape")
        key_id = signature.get("keyid", "")
        if not isinstance(key_id, str) or not isinstance(signature["sig"], str):
            raise ValueError("DSSE signature fields must be strings")
        try:
            if not base64.b64decode(signature["sig"], validate=True):
                raise ValueError("DSSE signature is empty")
        except (binascii.Error, TypeError) as error:
            raise ValueError("DSSE signature is not valid base64") from error
        normalized_signatures.append({"keyid": key_id, "sig": signature["sig"]})
    if len({(item["keyid"], item["sig"]) for item in normalized_signatures}) != len(normalized_signatures):
        raise ValueError("DSSE envelope contains duplicate signatures")
    return {
        "payload": candidate["payload"],
        "payloadType": candidate["payloadType"],
        "signatures": normalized_signatures,
    }


def load_envelopes(path: Path) -> list[dict[str, Any]]:
    envelopes: list[dict[str, Any]] = []
    for value in load_json_values(path):
        envelope = normalize_envelope(value)
        if envelope is None:
            raise ValueError(f"{path}: Cosign evidence does not contain a DSSE envelope")
        envelopes.append(envelope)
    if not envelopes:
        raise ValueError(f"{path}: Cosign evidence contains no DSSE envelopes")
    return envelopes


def decode_statement(envelope: dict[str, Any]) -> tuple[dict[str, Any], bytes]:
    try:
        payload = base64.b64decode(envelope["payload"], validate=True)
        statement = json.loads(payload)
    except (binascii.Error, UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ValueError("DSSE payload is not valid base64-encoded JSON") from error
    if not isinstance(statement, dict):
        raise ValueError("DSSE payload must be an in-toto statement object")
    return statement, payload


def dependency_reference(value: Any) -> str:
    if not isinstance(value, dict) or set(value) != {"uri", "digest"}:
        raise ValueError("resolved dependency has an unsupported shape")
    uri = value["uri"]
    digest = value["digest"]
    if not isinstance(uri, str) or not isinstance(digest, dict) or set(digest) != {"sha256"}:
        raise ValueError("resolved dependency requires uri and one sha256 digest")
    digest_value = digest["sha256"]
    reference = f"{uri}@sha256:{digest_value}"
    if not isinstance(digest_value, str) or not exact_image_reference(reference):
        raise ValueError("resolved dependency is not an exact image digest")
    return reference


def verify_statement(
    service: str,
    statement: dict[str, Any],
    expected_pin: str,
    source_commit: str,
    source_repository: str,
    dockerfile_digest: str,
    dockerfile_name: str,
    build_input: dict[str, Any],
    expected_base_images: set[str],
    trusted_builders: set[str],
    trusted_workflows: set[str],
    invocation_id: str,
) -> tuple[str, str]:
    require_exact_keys(statement, {"_type", "subject", "predicateType", "predicate"}, f"{service}: statement")
    if statement["_type"] != STATEMENT_TYPE or statement["predicateType"] != PREDICATE_TYPE:
        raise ValueError(f"{service}: statement or predicate type is not the required SLSA v1 type")

    repository, _, digest = expected_pin.rpartition("@")
    subject = statement["subject"]
    expected_subject = [{"name": repository, "digest": {"sha256": digest.removeprefix("sha256:")}}]
    if subject != expected_subject:
        raise ValueError(f"{service}: verified provenance subject does not equal the registry-confirmed pin")

    predicate = statement["predicate"]
    if not isinstance(predicate, dict):
        raise ValueError(f"{service}: provenance predicate must be an object")
    require_exact_keys(predicate, {"buildDefinition", "runDetails"}, f"{service}: predicate")

    definition = predicate["buildDefinition"]
    if not isinstance(definition, dict):
        raise ValueError(f"{service}: build definition is missing")
    require_exact_keys(
        definition,
        {"buildType", "externalParameters", "resolvedDependencies"},
        f"{service}: build definition",
    )
    if definition["buildType"] != BUILD_TYPE:
        raise ValueError(f"{service}: build type does not match")

    external = definition["externalParameters"]
    if not isinstance(external, dict):
        raise ValueError(f"{service}: external build parameters are missing")
    require_exact_keys(
        external,
        {"service", "source_repository", "source_commit", "workflow_ref", "dockerfile", "build"},
        f"{service}: external build parameters",
    )
    if external["service"] != service:
        raise ValueError(f"{service}: signed service binding does not match")
    if external["source_repository"] != source_repository or external["source_commit"] != source_commit:
        raise ValueError(f"{service}: signed source repository or commit does not match")
    workflow_ref = external["workflow_ref"]
    if workflow_ref not in trusted_workflows:
        raise ValueError(f"{service}: signed workflow identity is not trusted")
    if external["build"] != build_input:
        raise ValueError(f"{service}: signed build context, args, or target do not match the actual Compose build")
    if external["dockerfile"] != {"path": dockerfile_name, "digest": dockerfile_digest}:
        raise ValueError(f"{service}: signed Dockerfile binding does not match the reviewed file")

    dependencies = definition["resolvedDependencies"]
    if not isinstance(dependencies, list):
        raise ValueError(f"{service}: resolved dependencies must be a list")
    actual_base_images = [dependency_reference(item) for item in dependencies]
    if len(actual_base_images) != len(set(actual_base_images)) or set(actual_base_images) != expected_base_images:
        raise ValueError(f"{service}: signed base-image materials do not match the reviewed digests")

    run_details = predicate["runDetails"]
    if not isinstance(run_details, dict):
        raise ValueError(f"{service}: run details are missing")
    require_exact_keys(run_details, {"builder", "metadata"}, f"{service}: run details")
    builder = run_details["builder"]
    metadata = run_details["metadata"]
    if not isinstance(builder, dict) or set(builder) != {"id"} or builder["id"] not in trusted_builders:
        raise ValueError(f"{service}: signed builder identity is not trusted")
    if not isinstance(metadata, dict) or metadata != {"invocationId": invocation_id}:
        raise ValueError(f"{service}: signed workflow invocation does not match this run")
    return str(builder["id"]), str(workflow_ref)


def envelope_identity(statement: dict[str, Any]) -> tuple[Any, Any, Any, Any]:
    try:
        external = statement["predicate"]["buildDefinition"]["externalParameters"]
        metadata = statement["predicate"]["runDetails"]["metadata"]
        return external["service"], external["source_commit"], external["workflow_ref"], metadata["invocationId"]
    except (KeyError, TypeError):
        return None, None, None, None


def verify_inputs(
    pins: dict[str, str],
    source_commit: str,
    source_repository: str,
    dockerfile: Path,
    build_inputs_file: Path,
    expected_base_images: list[str],
    trusted_builders: list[str],
    trusted_workflows: list[str],
    invocation_id: str,
    record_paths: list[tuple[str, str, str]],
) -> tuple[dict[str, Any], str]:
    if not SHA.fullmatch(source_commit):
        raise ValueError("source commit must be a lowercase full 40-character SHA")
    if not dockerfile.is_file():
        raise ValueError("reviewed Dockerfile does not exist")
    if not source_repository.startswith("https://github.com/") or not TRUST_VALUE.fullmatch(source_repository):
        raise ValueError("source repository must be a safe canonical GitHub URL")
    if not invocation_id.startswith(f"{source_repository}/actions/runs/") or not TRUST_VALUE.fullmatch(invocation_id):
        raise ValueError("workflow invocation ID must be a safe URL for the source repository")
    if not trusted_builders or not trusted_workflows:
        raise ValueError("trusted builder and workflow allowlists must be non-empty")
    trust_values = [*trusted_builders, *trusted_workflows]
    if len(trust_values) != len(set(trust_values)) or not all(TRUST_VALUE.fullmatch(item) for item in trust_values):
        raise ValueError("trusted builder and workflow values must be unique safe identifiers")
    if (
        len(expected_base_images) < 2
        or len(expected_base_images) != len(set(expected_base_images))
        or not all(exact_image_reference(item) for item in expected_base_images)
    ):
        raise ValueError("expected base images must contain at least two unique exact image digests")
    dockerfile_bases = dockerfile_base_images(dockerfile)
    if len(dockerfile_bases) != len(set(dockerfile_bases)) or set(dockerfile_bases) != set(expected_base_images):
        raise ValueError("reviewed Dockerfile base images do not match the expected signed materials")

    build_inputs = load_build_inputs(build_inputs_file, source_commit, set(pins))
    dockerfile_digest = sha256_file(dockerfile)
    records: dict[str, tuple[Path, Path]] = {}
    for service, verification_path, bundle_path in record_paths:
        if service in records:
            raise ValueError(f"duplicate provenance input for {service}")
        records[service] = (Path(verification_path), Path(bundle_path))
    if set(records) != set(pins):
        raise ValueError(
            f"provenance coverage mismatch; missing={sorted(set(pins) - set(records))}, "
            f"extra={sorted(set(records) - set(pins))}"
        )

    bindings: dict[str, Any] = {}
    for service, pin in sorted(pins.items()):
        verification_path, bundle_path = records[service]
        envelopes = load_envelopes(verification_path)
        decoded = [(envelope, *decode_statement(envelope)) for envelope in envelopes]
        candidates = [
            item
            for item in decoded
            if (identity := envelope_identity(item[1]))[0] == service
            and identity[1] == source_commit
            and identity[2] in set(trusted_workflows)
            and identity[3] == invocation_id
        ]
        if not candidates and len(decoded) == 1:
            candidates = decoded
        if len(candidates) != 1:
            raise ValueError(f"{service}: Cosign output has no unique attestation for this workflow invocation")
        envelope, statement, payload = candidates[0]

        builder_id, workflow_ref = verify_statement(
            service,
            statement,
            pin,
            source_commit,
            source_repository,
            dockerfile_digest,
            dockerfile.name,
            build_inputs[service],
            set(expected_base_images),
            set(trusted_builders),
            set(trusted_workflows),
            invocation_id,
        )

        bundle_envelopes = load_envelopes(bundle_path)
        if len(bundle_envelopes) != 1 or bundle_envelopes[0] != envelope:
            raise ValueError(f"{service}: retained Cosign bundle does not match the verified DSSE envelope")

        bindings[service] = {
            "pin": pin,
            "source_commit": source_commit,
            "source_repository": source_repository,
            "builder_id": builder_id,
            "workflow_ref": workflow_ref,
            "workflow_invocation": invocation_id,
            "dockerfile": {"path": dockerfile.name, "digest": dockerfile_digest},
            "build": build_inputs[service],
            "base_images": sorted(expected_base_images),
            "cosign_verification_sha256": sha256_file(verification_path),
            "cosign_bundle_sha256": sha256_file(bundle_path),
            "dsse_payload_sha256": sha256_bytes(payload),
            "certificate_identity_policy": workflow_ref,
            "certificate_oidc_issuer_policy": TRUSTED_OIDC_ISSUER,
            "certificate_workflow_repository_policy": source_repository.removeprefix("https://github.com/"),
            "certificate_workflow_sha_policy": source_commit,
            "signature_verified": True,
            "transparency_log_verified": True,
            "verified_dsse_envelope": envelope,
        }
    return bindings, dockerfile_digest


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pins-file", required=True)
    parser.add_argument("--source-commit", required=True)
    parser.add_argument("--source-repository", required=True)
    parser.add_argument("--dockerfile", required=True)
    parser.add_argument("--build-inputs-file", required=True)
    parser.add_argument("--expected-base-image", action="append", default=[])
    parser.add_argument("--trusted-builder-id", action="append", default=[])
    parser.add_argument("--trusted-workflow-ref", action="append", default=[])
    parser.add_argument("--workflow-invocation", required=True)
    parser.add_argument(
        "--provenance-record",
        action="append",
        nargs=3,
        metavar=("SERVICE", "COSIGN_VERIFICATION_JSON", "COSIGN_BUNDLE_JSON"),
        default=[],
    )
    parser.add_argument("--output", required=True)
    args = parser.parse_args()

    errors: list[str] = []
    bindings: dict[str, Any] = {}
    dockerfile_digest: str | None = None
    try:
        pins = load_pin_document(Path(args.pins_file), args.source_commit)
        bindings, dockerfile_digest = verify_inputs(
            pins,
            args.source_commit,
            args.source_repository,
            Path(args.dockerfile),
            Path(args.build_inputs_file),
            args.expected_base_image,
            args.trusted_builder_id,
            args.trusted_workflow_ref,
            args.workflow_invocation,
            args.provenance_record,
        )
    except (OSError, ValueError, json.JSONDecodeError) as error:
        errors.append(str(error))

    evidence = {
        "schema": OUTPUT_SCHEMA,
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "status": "passed" if not errors else "failed",
        "source_commit": args.source_commit,
        "dockerfile_digest": dockerfile_digest,
        "bindings": bindings,
        "errors": errors,
    }
    output = Path(args.output)
    write_private_json(output, evidence)

    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        return 1
    print(f"Verified signed DSSE provenance bindings for {len(bindings)} image pins")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
