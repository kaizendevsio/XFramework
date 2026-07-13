#!/usr/bin/env python3
"""Tests for strict Bolt Phase 0 qualification and recovery gating."""

from __future__ import annotations

import copy
import datetime as dt
import hashlib
import importlib.util
import json
import os
import stat
import subprocess
import tempfile
import unittest
import uuid
from pathlib import Path
from types import SimpleNamespace
from unittest import mock


SCRIPT = Path(__file__).with_name("verify-bolt-phase0-qualification.py")
SPEC = importlib.util.spec_from_file_location("bolt_phase0_qualification", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def timestamp(value: dt.datetime) -> str:
    return value.astimezone(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%fZ")


def digest(value: str) -> str:
    return "sha256:" + hashlib.sha256(value.encode()).hexdigest()


def secure_directory(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)
    if os.name == "posix":
        path.chmod(0o700)


def secure_bytes(path: Path, value: bytes) -> None:
    path.write_bytes(value)
    if os.name == "posix":
        path.chmod(0o600)


def secure_executable(path: Path, value: bytes = b"#!/usr/bin/env python3\n") -> None:
    path.write_bytes(value)
    if os.name == "posix":
        path.chmod(0o700)


def secure_json(path: Path, value: dict) -> None:
    secure_bytes(path, (json.dumps(value, indent=2, sort_keys=True) + "\n").encode())


class EvidenceFactory:
    def __init__(self, root: Path, now: dt.datetime | None = None) -> None:
        self.now = (now or dt.datetime.now(dt.timezone.utc)).replace(microsecond=0)
        self.run_id = "123456789"
        self.attempt = 2
        self.commit = "a" * 40
        self.project = "xframework"
        self.run = root / f"{self.run_id}-{self.attempt}"
        secure_directory(self.run)
        self.pins = {
            service: f"registry.example/xframework/{service}@{digest(service)}"
            for service in MODULE.IMAGE_SERVICES
        }

    def at(self, minutes: float) -> str:
        return timestamp(self.now + dt.timedelta(minutes=minutes))

    def pins_document(self) -> dict:
        repositories = {
            service: f"registry.example/xframework/{service}" for service in MODULE.IMAGE_SERVICES
        }
        return {
            "schema": MODULE.PINS_SCHEMA,
            "generated_at_utc": self.at(-110),
            "status": "passed",
            "source_commit": self.commit,
            "approved_repositories": repositories,
            "registry_confirmed": True,
            "registry_manifests": {
                service: {
                    "requested_ref": f"{repositories[service]}:{self.commit}",
                    "manifest_sha256": digest(f"manifest-{service}"),
                    "pin": self.pins[service],
                }
                for service in MODULE.IMAGE_SERVICES
            },
            "pins": self.pins,
            "errors": [],
        }

    def preflight(self) -> dict:
        services = {
            service: {
                "image": self.pins[service],
                "security_environment": {
                    "BoltConfiguration__RequireSecureTransport": "true",
                    "BoltConfiguration__MediaEnabled": "false",
                },
                "security_secrets": [],
                "ports": [],
                "healthcheck": None,
                "replicas": 1 if service == "bolt-hub" else None,
            }
            for service in MODULE.IMAGE_SERVICES
        }
        return {
            "schema": MODULE.PREFLIGHT_SCHEMA,
            "generated_at_utc": self.at(-105),
            "status": "passed",
            "deployment_authorized": True,
            "checks": {
                "digest-pinned-provenance-authorized-images": {
                    "passed": True,
                    "detail": {
                        "authorization_requested": True,
                        "authorized_services": sorted(MODULE.IMAGE_SERVICES),
                        "registry_confirmed": True,
                        "provenance_verified": True,
                        "services": {
                            service: {
                                "image": self.pins[service],
                                "expected": self.pins[service],
                                "provenance_bound": True,
                            }
                            for service in MODULE.IMAGE_SERVICES
                        },
                    },
                },
                "secure-transport": {"passed": True, "detail": "verified"},
            },
            "errors": [],
            "redacted_manifest": {"services": services},
        }

    def tls(self, identity: bool = False) -> dict:
        document = {
            "schema": MODULE.IDENTITY_TLS_SCHEMA if identity else MODULE.HUB_TLS_SCHEMA,
            "generated_at_utc": self.at(-104),
            "status": "passed",
            "internal_hostname": "identityserver" if identity else "bolt-hub",
            "published_hostname": "identity.example.internal" if identity else "bolt.example.internal",
            "published_port": 7443 if identity else 7000,
            "certificate": {
                "subject": "CN=phase0",
                "issuer": "CN=test-ca",
                "serial": "01",
                "not_before": "Jul 13 00:00:00 2026 GMT",
                "not_after": "Oct 13 00:00:00 2026 GMT",
                "sha256_fingerprint": "AA:BB",
                "subject_alternative_name": "DNS:phase0.example.internal",
                "chain_verified": True,
                "hostname_verified": True,
                "currently_valid": True,
            },
            "private_key": {"value": "<redacted>", "matches_certificate": True, "mode": "600"},
        }
        if identity:
            document["token_path"] = "/api/service-identity/bolt-transport-token"
        return document

    def provenance(self) -> dict:
        dockerfile = digest("Dockerfile")
        invocation = (
            "https://github.com/example/XFramework/actions/runs/"
            f"{self.run_id}/attempts/{self.attempt}"
        )
        workflow = "https://github.com/example/XFramework/.github/workflows/deploy.yml@refs/heads/develop"
        bindings = {}
        for service in MODULE.IMAGE_SERVICES:
            bindings[service] = {
                "pin": self.pins[service],
                "source_commit": self.commit,
                "source_repository": "https://github.com/example/XFramework",
                "builder_id": workflow,
                "workflow_ref": workflow,
                "workflow_invocation": invocation,
                "dockerfile": {"path": "Dockerfile", "digest": dockerfile},
                "build": {
                    "context": ".",
                    "dockerfile": "Dockerfile",
                    "args": {"PROJECT_PATH": f"src/{service}.csproj"},
                    "target": None,
                },
                "base_images": [
                    f"mcr.microsoft.com/dotnet/sdk@{digest('sdk')}",
                    f"mcr.microsoft.com/dotnet/aspnet@{digest('aspnet')}",
                ],
                "cosign_verification_sha256": digest(f"verification-{service}"),
                "cosign_bundle_sha256": digest(f"bundle-{service}"),
                "dsse_payload_sha256": digest(f"payload-{service}"),
                "certificate_identity_policy": workflow,
                "certificate_oidc_issuer_policy": "https://token.actions.githubusercontent.com",
                "certificate_workflow_repository_policy": "example/XFramework",
                "certificate_workflow_sha_policy": self.commit,
                "signature_verified": True,
                "transparency_log_verified": True,
                "verified_dsse_envelope": {"payloadType": "application/vnd.in-toto+json"},
            }
        return {
            "schema": MODULE.PROVENANCE_SCHEMA,
            "generated_at_utc": self.at(-103),
            "status": "passed",
            "source_commit": self.commit,
            "dockerfile_digest": dockerfile,
            "bindings": bindings,
            "errors": [],
        }

    def runtime_service(self, service: str) -> dict:
        item = {
            "service": service,
            "container_name": f"xframework-{service}",
            "container_id": hashlib.sha256(f"container-{service}".encode()).hexdigest(),
            "configured_image": self.pins[service],
            "local_image_id": digest(f"local-{service}"),
            "repo_digests": [self.pins[service]],
            "started_at": self.at(-120),
            "running": service != "migrate",
            "status": "running" if service != "migrate" else "exited",
            "exit_code": 0 if service == "migrate" else None,
            "health": "healthy" if service != "migrate" else None,
            "listeners": [],
            "published_port": None,
            "private_key_mounts": [],
        }
        if service == "bolt-hub":
            item["listeners"] = [
                {"family": "ipv4", "scope": "loopback", "port": 8080},
                {"family": "ipv4", "scope": "wildcard", "port": 8443},
            ]
            item["published_port"] = {"container_port": 8443, "published_port": 7000, "protocol": "tcp"}
            item["private_key_mounts"] = [{
                "resolved_source": "<expected-private-key>",
                "relation": "exact",
                "target": "/run/secrets/bolt-hub-tls-private-key.pem",
                "read_only": True,
            }]
        elif service == "identityserver":
            item["listeners"] = [
                {"family": "ipv4", "scope": "loopback", "port": 8080},
                {"family": "ipv4", "scope": "wildcard", "port": 8443},
            ]
            item["published_port"] = {
                "container_port": 8443,
                "published_port": 7443,
                "protocol": "tcp",
            }
            item["private_key_mounts"] = [{
                "resolved_source": "<expected-private-key>",
                "relation": "exact",
                "target": "/run/secrets/identityserver-tls-private-key.pem",
                "read_only": True,
            }]
        return item

    def runtime(self, services: tuple[str, ...], minute: float, mode: str) -> dict:
        return {
            "schema": MODULE.RUNTIME_SCHEMA,
            "generated_at_utc": self.at(minute),
            "status": "passed",
            "inventory_mode": mode,
            "requested_services": list(services),
            "expected_images": self.pins,
            "intentionally_inactive_services": ["bolt-phase0-synthetics"],
            "services": {service: self.runtime_service(service) for service in services},
            "errors": [],
        }

    def rotation(self, phase: str) -> dict:
        required = {
            "prepared": (self.at(-90), None, None, None),
            "activated": (self.at(-90), self.at(-80), None, None),
            "converged": (self.at(-90), self.at(-80), self.at(-70), None),
            "finalized": (self.at(-90), self.at(-80), self.at(-70), self.at(-20)),
        }[phase]
        return {
            "schema": MODULE.ROTATION_SCHEMA,
            "rotation_id": "rotation-0123456789abcdef",
            "phase": phase,
            "previous_generation_id": "generation-g",
            "target_generation_id": "generation-g1",
            "secondary_valid_until_utc": self.at(-30),
            "prepared_at_utc": required[0],
            "activated_at_utc": required[1],
            "convergence_verified_at_utc": required[2],
            "finalized_at_utc": required[3],
        }

    def credential_convergence(self, phase: str, minute: float) -> dict:
        return {
            "schema": MODULE.CREDENTIAL_CONVERGENCE_SCHEMA,
            "generated_at_utc": self.at(minute),
            "observed_at_utc": self.at(minute - 0.1),
            "fallback_valid_until_utc": self.at(-30) if phase == "dual-validation" else None,
            "phase": phase,
            "target_generation_id": "generation-g1",
            "retiring_generation_id": "generation-g",
            "service_count": len(MODULE.ROTATION_SERVICES),
            "identityserver_client_count": len(MODULE.ROTATION_SERVICES),
            "current_token_count": 2,
            "retired_token_count": 2 if phase == "dual-validation" else 0,
            "status": "passed",
            "errors": [],
        }

    def operation(self, name: str, started: dt.datetime, completed: dt.datetime) -> dict:
        results = {"result": "passed"}
        if name == "durable_ack":
            results = {"duplicate_ack_idempotent": "true", "out_of_order_ack_monotonic": "true"}
        return {
            "name": name,
            "startedAtUtc": timestamp(started),
            "completedAtUtc": timestamp(completed),
            "status": "passed",
            "timingMs": 10,
            "results": results,
        }

    def probe(
        self, probe: str, started: dt.datetime, completed: dt.datetime, assertions: dict
    ) -> dict:
        return {
            "schemaVersion": MODULE.PROBE_SCHEMA,
            "probe": probe,
            "status": "passed",
            "startedAtUtc": timestamp(started),
            "completedAtUtc": timestamp(completed),
            "assertions": assertions,
        }

    def synthetic(self, stage: str, minute: float, run_id: str | None = None) -> dict:
        started = self.now + dt.timedelta(minutes=minute)
        completed = started + dt.timedelta(seconds=30)
        run_id = run_id or str(uuid.uuid4())
        names = set(MODULE.REQUIRED_OPERATIONS)
        if stage in {"canary", "finalized"}:
            names.add("token_expiry_disconnect")
        operations = [self.operation(name, started, completed) for name in sorted(names)]
        marker_count = 3 if stage in {"canary", "finalized"} else 2
        marker_assertions = {
            "retainedStoreQueried": True,
            "matches": 0,
            "tokensSearched": marker_count,
            "markersSearched": marker_count,
        }
        receipts = {
            "proxyMarkerScan": self.probe("proxy-marker-scan", started, completed, marker_assertions),
            "seqMarkerScan": self.probe("seq-marker-scan", started, completed, marker_assertions),
            "traceMarkerScan": self.probe("trace-marker-scan", started, completed, marker_assertions),
            "plaintextRejection": self.probe(
                "plaintext-rejection", started, completed,
                {"plaintextRejected": True, "bearerSent": False},
            ),
        }
        if stage == "canary":
            receipts["redisInterruption"] = self.probe(
                "redis-interruption", started, completed,
                {
                    "interruptionInduced": True,
                    "recovered": True,
                    "postRecoverySyntheticPassed": True,
                    "dataLossObserved": False,
                },
            )
        if stage == "finalized":
            receipts["oldGenerationRejection"] = self.probe(
                "old-generation-rejection", started, completed,
                {
                    "oldUserTokenRejected": True,
                    "oldServiceTokenRejected": True,
                    "oldClientSecretRejected": True,
                    "currentHttpHealthPassed": True,
                    "currentBoltHealthPassed": True,
                },
            )
        prefixes = {"communications": "a" * 12, "user": "b" * 12}
        markers = {"communications": "d" * 12, "user": "e" * 12}
        if stage in {"canary", "finalized"}:
            prefixes["expiry"] = "f" * 12
            markers["expiry"] = "0" * 12
        core = {
            "schemaVersion": MODULE.SYNTHETIC_CORE_SCHEMA,
            "runId": run_id,
            "tokenSha256Prefixes": prefixes,
            "startedAtUtc": timestamp(started),
            "completedAtUtc": timestamp(completed),
            "target": "wss://bolt.example.internal:7000/bolt/ws",
            "status": "passed",
            "timings": {"totalMs": 30000},
            "operations": operations,
        }
        return {
            "schemaVersion": MODULE.SYNTHETIC_SCHEMA,
            "runId": run_id,
            "stage": stage,
            "status": "passed",
            "coreReportSha256": hashlib.sha256(json.dumps(core).encode()).hexdigest(),
            "synthetic": core,
            "postRunEvidence": {
                "schemaVersion": MODULE.POST_RUN_SCHEMA,
                "tokenRefresh": {
                    "status": "passed",
                    "issuerUri": "https://identity.example.internal:7443",
                    "principalReferenceSha256Prefix": "c" * 12,
                    "refreshedAtUtc": timestamp(started - dt.timedelta(seconds=5)),
                    "minimumRemainingLifetimeSeconds": 60,
                    "expiryTokenIssued": stage in {"canary", "finalized"},
                },
                "markerAbsence": {
                    "application": "passed",
                    "proxy": "passed",
                    "seq": "passed",
                    "trace": "passed",
                    "markerSha256Prefixes": markers,
                },
                "plaintextRejection": "passed",
                "expiryDisconnect": "passed" if stage in {"canary", "finalized"} else "not_required",
                "redisInterruptionRecovery": "passed" if stage == "canary" else "not_required",
                "oldGenerationCredentialRejection": "passed" if stage == "finalized" else "not_required",
                "tokenFilesStableForRun": "passed",
                "probeReceipts": receipts,
            },
        }

    def observation(self) -> dict:
        return {
            "schema": MODULE.OBSERVATION_SCHEMA,
            "generated_at_utc": self.at(-89),
            "status": "passed",
            "observation": {
                "started_at_utc": self.at(-95),
                "completed_at_utc": self.at(-90),
                "duration_seconds": 300,
                "sample_count": 10,
            },
            "thresholds": {"minimum_duration_seconds": 300},
            "health_aggregates": {"sample_count": 10, "transport_snapshot_count": 10},
            "synthetic_aggregates": {
                "report_count": 1,
                "operation_latency": {"user_registration": {"maximum_ms": 10}},
            },
            "errors": [],
        }

    def create(self) -> None:
        secure_bytes(self.run / "docker-compose.yml", b"services:\n  bolt-hub: {}\n")
        secure_json(
            self.run / "pinned-compose.override.json",
            {"services": {service: {"image": pin} for service, pin in self.pins.items()}},
        )
        secure_json(self.run / "image-pins.json", self.pins_document())
        secure_json(self.run / "pinned-manifest-evidence.json", self.preflight())
        secure_json(self.run / "bolt-tls-evidence.json", self.tls())
        secure_json(self.run / "identityserver-tls-evidence.json", self.tls(identity=True))
        secure_json(self.run / "provenance-evidence.json", self.provenance())
        runtime_minutes = [-105, -100, -88, -86, -84]
        for (name, services), minute in zip(MODULE.STAGED_RUNTIME_INVENTORIES.items(), runtime_minutes):
            secure_json(self.run / name, self.runtime(services, minute, "staged"))
        rotation_runtime_minutes = [-79.5, -79.25, -75.5, -74.5, -73.5]
        for (name, services), minute in zip(
            MODULE.ROTATION_RUNTIME_INVENTORIES.items(), rotation_runtime_minutes
        ):
            secure_json(self.run / name, self.runtime(services, minute, "staged"))
        secure_json(
            self.run / "runtime-evidence.json",
            self.runtime(MODULE.PHASE0_SERVICES, -15, "complete"),
        )
        secure_json(self.run / "rotation-prepare-evidence.json", self.rotation("prepared"))
        secure_json(self.run / "rotation-activate-evidence.json", self.rotation("activated"))
        secure_json(
            self.run / "rotation-generation-inventory.json",
            {
                "schema": MODULE.GENERATION_INVENTORY_SCHEMA,
                "generated_at_utc": self.at(-71),
                "services": {service: "generation-g1" for service in MODULE.ROTATION_SERVICES},
            },
        )
        secure_json(self.run / "rotation-convergence-evidence.json", self.rotation("converged"))
        secure_json(self.run / "rotation-finalized-evidence.json", self.rotation("finalized"))
        secure_json(
            self.run / "credential-convergence-dual-validation.json",
            self.credential_convergence("dual-validation", -70.5),
        )
        secure_json(
            self.run / "credential-convergence-finalized.json",
            self.credential_convergence("finalized", -19),
        )
        secure_json(self.run / "observation-evidence.json", self.observation())
        synthetic_minutes = {
            "canary": -94,
            "batch-1": -87,
            "batch-2": -85,
            "batch-3": -83,
            "rotation-canary": -79,
            "rotation-batch-1": -75,
            "rotation-batch-2": -74,
            "rotation-batch-3": -73,
            "finalized": -18,
        }
        for name, stage in MODULE.SYNTHETIC_FILES.items():
            if name == "rollback-synthetics-finalized.json":
                continue
            secure_json(self.run / name, self.synthetic(stage, synthetic_minutes[stage]))
        secure_json(
            self.run / "rollback-runtime-evidence.json",
            self.runtime(MODULE.PHASE0_SERVICES, -5.5, "complete"),
        )
        secure_json(
            self.run / "rollback-synthetics-finalized.json",
            self.synthetic("finalized", -5),
        )
        digests = {
            name: MODULE.sha256_file(self.run / name)
            for name in (
                "docker-compose.yml",
                "pinned-compose.override.json",
                "image-pins.json",
                "rollback-runtime-evidence.json",
                "rollback-synthetics-finalized.json",
            )
        }
        secure_json(
            self.run / "rollback-drill-evidence.json",
            {
                "schema": MODULE.ROLLBACK_DRILL_SCHEMA,
                "status": "passed",
                "run_id": self.run_id,
                "run_attempt": self.attempt,
                "source_commit": self.commit,
                "project_name": self.project,
                "started_at_utc": self.at(-6),
                "completed_at_utc": self.at(-4),
                "credential_generation_id": "generation-g1",
                "manifest_sha256": digests["docker-compose.yml"],
                "override_sha256": digests["pinned-compose.override.json"],
                "pins_sha256": digests["image-pins.json"],
                "runtime_evidence_sha256": digests["rollback-runtime-evidence.json"],
                "synthetic_evidence_sha256": digests["rollback-synthetics-finalized.json"],
                "checks": {name: True for name in MODULE.ROLLBACK_CHECK_KEYS},
                "errors": [],
            },
        )
        for name in MODULE.RECOVERY_EXECUTABLE_FILES:
            secure_executable(self.run / name)
        for name in MODULE.RECOVERY_CONFIG_FILES:
            secure_bytes(self.run / name, f"# {name}\n".encode())

    def verify(self) -> dict:
        return MODULE.verify_qualification(
            self.run,
            self.commit,
            self.run_id,
            self.attempt,
            self.project,
            MODULE.DEFAULT_MAXIMUM_AGE_SECONDS,
            now=self.now,
        )

    def mutate(self, name: str, change) -> None:
        path = self.run / name
        document = json.loads(path.read_text(encoding="utf-8"))
        change(document)
        secure_json(path, document)


class QualificationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.factory = EvidenceFactory(self.root)
        self.factory.create()

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def assert_code(self, code: str) -> None:
        with self.assertRaises(MODULE.QualificationError) as raised:
            self.factory.verify()
        self.assertEqual(code, raised.exception.code)

    def test_accepts_complete_bound_evidence(self) -> None:
        evidence = self.factory.verify()
        self.assertEqual("passed", evidence["status"])
        self.assertEqual(set(MODULE.ARTIFACT_FILES), set(evidence["artifacts"]))
        self.assertNotIn("configured_image", json.dumps(evidence))

    def test_sealed_json_reads_require_root_owner_and_mode_0440(self) -> None:
        path = self.factory.run / "sealed.json"
        raw = b'{"status":"passed"}\n'
        secure_bytes(path, raw)
        metadata = SimpleNamespace(
            st_mode=stat.S_IFREG | 0o440,
            st_uid=0,
            st_nlink=1,
            st_size=len(raw),
            st_dev=1,
            st_ino=2,
            st_mtime_ns=3,
        )
        with (
            mock.patch.object(MODULE, "enforce_posix_metadata", return_value=True),
            mock.patch.object(Path, "lstat", return_value=metadata),
        ):
            self.assertEqual(
                "passed",
                MODULE.load_json(path, expected_mode=0o440, root_only=True)["status"],
            )
        insecure = SimpleNamespace(**{**metadata.__dict__, "st_mode": stat.S_IFREG | 0o640})
        with (
            mock.patch.object(MODULE, "enforce_posix_metadata", return_value=True),
            mock.patch.object(Path, "lstat", return_value=insecure),
            self.assertRaisesRegex(MODULE.QualificationError, "insecure-artifact"),
        ):
            MODULE.load_json(path, expected_mode=0o440, root_only=True)

    def test_rejects_generic_status_only_impostor(self) -> None:
        secure_json(
            self.factory.run / "provenance-evidence.json",
            {"schema": MODULE.PROVENANCE_SCHEMA, "status": "passed"},
        )
        self.assert_code("invalid-provenance")

    def test_rejects_duplicate_json_keys(self) -> None:
        secure_bytes(
            self.factory.run / "image-pins.json",
            b'{"schema":"xframework.bolt.phase0.image-pins.v2","schema":"duplicate"}\n',
        )
        self.assert_code("duplicate-json-key")

    def test_rejects_stale_evidence(self) -> None:
        self.factory.mutate(
            "image-pins.json",
            lambda document: document.update(generated_at_utc=self.factory.at(-25 * 60)),
        )
        self.assert_code("stale-evidence")

    def test_rejects_commit_and_run_mismatches(self) -> None:
        for field, value, code in (
            ("source_commit", "b" * 40, "image-pin-binding-mismatch"),
            ("source_commit", self.factory.commit, None),
        ):
            self.factory.mutate("image-pins.json", lambda document, f=field, v=value: document.update({f: v}))
            if code:
                self.assert_code(code)
                self.factory.create()
        self.factory.mutate(
            "provenance-evidence.json",
            lambda document: next(iter(document["bindings"].values())).update(
                workflow_invocation="https://github.com/example/XFramework/actions/runs/999/attempts/2"
            ),
        )
        self.assert_code("provenance-binding-mismatch")

    def test_rejects_runtime_digest_or_inventory_tampering(self) -> None:
        self.factory.mutate(
            "runtime-staged-canary.json",
            lambda document: document["services"]["bolt-hub"].update(
                configured_image=self.factory.pins["identityserver"]
            ),
        )
        self.assert_code("runtime-image-mismatch")
        self.factory.create()
        self.factory.mutate(
            "runtime-staged-batch-1.json",
            lambda document: document["requested_services"].append("wallets"),
        )
        self.assert_code("runtime-inventory-mismatch")

    def test_requires_identityserver_tls_boundary_and_rejects_it_elsewhere(self) -> None:
        self.factory.mutate(
            "runtime-staged-canary.json",
            lambda document: document["services"]["identityserver"].update(
                listeners=[], published_port=None, private_key_mounts=[]
            ),
        )
        self.assert_code("missing-tls-service-listeners")
        self.factory.create()
        identity_mount = self.factory.runtime_service("identityserver")["private_key_mounts"]
        self.factory.mutate(
            "runtime-staged-batch-1.json",
            lambda document: document["services"]["communications"].update(
                private_key_mounts=identity_mount
            ),
        )
        self.assert_code("unexpected-runtime-boundary-evidence")

    def test_rejects_identityserver_publication_or_key_target_mismatch(self) -> None:
        self.factory.mutate(
            "runtime-evidence.json",
            lambda document: document["services"]["identityserver"]["published_port"].update(
                published_port=7000
            ),
        )
        self.assert_code("invalid-tls-service-publication")
        self.factory.create()
        self.factory.mutate(
            "runtime-evidence.json",
            lambda document: document["services"]["identityserver"]["private_key_mounts"][0].update(
                target="/run/secrets/bolt-hub-tls-private-key.pem"
            ),
        )
        self.assert_code("invalid-tls-service-private-key-mount")

    def test_rejects_missing_and_unexpected_runtime_inventory(self) -> None:
        (self.factory.run / "runtime-staged-batch-3.json").unlink()
        self.assert_code("unexpected-runtime-inventory")
        self.factory.create()
        secure_json(self.factory.run / "runtime-surprise.json", {"status": "passed"})
        self.assert_code("unexpected-runtime-inventory")

    def test_rejects_missing_rotation_runtime_inventory(self) -> None:
        (self.factory.run / "runtime-rotation-canary.json").unlink()
        self.assert_code("unexpected-runtime-inventory")

    def test_rejects_wrong_rotation_runtime_cohort_mode_and_identity_boundary(self) -> None:
        self.factory.mutate(
            "runtime-rotation-canary.json",
            lambda document: document.update(inventory_mode="complete"),
        )
        self.assert_code("runtime-inventory-mismatch")
        self.factory.create()
        self.factory.mutate(
            "runtime-rotation-batch-1.json",
            lambda document: document["requested_services"].append("wallets"),
        )
        self.assert_code("runtime-inventory-mismatch")
        self.factory.create()
        self.factory.mutate(
            "runtime-rotation-canary.json",
            lambda document: document["services"]["identityserver"].update(
                listeners=[], published_port=None, private_key_mounts=[]
            ),
        )
        self.assert_code("missing-tls-service-listeners")

    def test_rejects_rotation_runtime_order_and_pre_activation_capture(self) -> None:
        self.factory.mutate(
            "runtime-rotation-batch-2.json",
            lambda document: document.update(generated_at_utc=self.factory.at(-76)),
        )
        self.assert_code("rotation-runtime-stage-order-mismatch")
        self.factory.create()
        self.factory.mutate(
            "runtime-rotation-hub.json",
            lambda document: document.update(generated_at_utc=self.factory.at(-81)),
        )
        self.assert_code("rotation-runtime-precedes-activation")

    def test_rejects_rotation_runtime_after_synthetic_or_dual_convergence(self) -> None:
        self.factory.mutate(
            "runtime-rotation-batch-1.json",
            lambda document: document.update(generated_at_utc=self.factory.at(-74.9)),
        )
        self.assert_code("rotation-runtime-after-synthetic")
        self.factory.create()
        self.factory.mutate(
            "credential-convergence-dual-validation.json",
            lambda document: document.update(
                generated_at_utc=self.factory.at(-74),
                observed_at_utc=self.factory.at(-74.1),
            ),
        )
        self.assert_code("rotation-runtime-after-dual-convergence")

    def test_rejects_missing_redis_and_old_generation_probes(self) -> None:
        self.factory.mutate(
            "synthetics-canary.json",
            lambda document: document["postRunEvidence"].update(redisInterruptionRecovery="not_required"),
        )
        self.assert_code("redis-stage-mismatch")
        self.factory.create()
        self.factory.mutate(
            "synthetics-finalized.json",
            lambda document: document["postRunEvidence"].update(oldGenerationCredentialRejection="not_required"),
        )
        self.assert_code("old-generation-stage-mismatch")

    def test_rejects_duplicate_synthetic_run_ids(self) -> None:
        canary = json.loads((self.factory.run / "synthetics-canary.json").read_text(encoding="utf-8"))
        self.factory.mutate(
            "synthetics-batch-1.json",
            lambda document: (
                document.update(runId=canary["runId"]),
                document["synthetic"].update(runId=canary["runId"]),
            ),
        )
        self.assert_code("duplicate-synthetic-run-id")

    def test_rejects_rotation_and_convergence_mismatch(self) -> None:
        self.factory.mutate(
            "rotation-activate-evidence.json",
            lambda document: document.update(target_generation_id="another-generation"),
        )
        self.assert_code("rotation-binding-mismatch")
        self.factory.create()
        self.factory.mutate(
            "credential-convergence-finalized.json",
            lambda document: document.update(fallback_valid_until_utc=self.factory.at(-10)),
        )
        self.assert_code("finalized-fallback-residue")

    def test_rejects_observation_that_does_not_cover_canary_synthetic(self) -> None:
        self.factory.mutate(
            "observation-evidence.json",
            lambda document: document["observation"].update(started_at_utc=self.factory.at(-93)),
        )
        self.assert_code("observation-does-not-cover-canary")

    def test_rejects_rollback_digest_tampering(self) -> None:
        self.factory.mutate(
            "rollback-drill-evidence.json",
            lambda document: document.update(runtime_evidence_sha256=digest("impostor")),
        )
        self.assert_code("rollback-drill-digest-mismatch")
        self.factory.create()
        self.factory.mutate(
            "rollback-drill-evidence.json",
            lambda document: document["checks"].update(restore_applied=False),
        )
        self.assert_code("rollback-drill-check-failed")

    def test_rejects_tampered_recovery_tool(self) -> None:
        secure_executable(
            self.factory.run / "verify-bolt-phase0-runtime.py",
            b"#!/usr/bin/env python3\nraise SystemExit('candidate replacement')\n",
        )
        evidence = self.factory.verify()
        self.assertEqual(
            MODULE.sha256_file(
                self.factory.run / "verify-bolt-phase0-runtime.py", expected_mode=0o700
            ),
            evidence["artifacts"]["verify-bolt-phase0-runtime.py"]["sha256"],
        )

    @unittest.skipIf(not hasattr(os, "symlink"), "symbolic links are unavailable")
    def test_rejects_symlinked_artifact(self) -> None:
        source = self.factory.run / "pins-copy.json"
        secure_bytes(source, (self.factory.run / "image-pins.json").read_bytes())
        (self.factory.run / "image-pins.json").unlink()
        try:
            os.symlink(source, self.factory.run / "image-pins.json")
        except OSError as error:
            self.skipTest(f"symbolic links are unavailable: {error}")
        self.assert_code("symlink-rejected")

    @unittest.skipUnless(os.name == "posix", "POSIX mode enforcement")
    def test_rejects_group_readable_artifact(self) -> None:
        (self.factory.run / "image-pins.json").chmod(0o640)
        self.assert_code("insecure-artifact")

    def test_rejects_hardlinked_artifact(self) -> None:
        source = self.factory.run / "pins-hardlink-source.json"
        target = self.factory.run / "image-pins.json"
        target.replace(source)
        try:
            os.link(source, target)
        except OSError as error:
            self.skipTest(f"hard links are unavailable: {error}")
        self.assert_code("insecure-artifact")

    def test_failed_qualification_does_not_publish_metadata(self) -> None:
        self.factory.mutate("image-pins.json", lambda document: document.update(status="failed"))
        lkg = self.root / "lkg"
        secure_directory(lkg)
        exit_code = MODULE.main([
            "qualify",
            "--run-directory", str(self.factory.run),
            "--expected-commit", self.factory.commit,
            "--expected-run-id", self.factory.run_id,
            "--expected-run-attempt", str(self.factory.attempt),
            "--project-name", self.factory.project,
            "--lkg-pointer", str(lkg / "current"),
        ])
        self.assertEqual(1, exit_code)
        self.assertFalse((self.factory.run / "security-qualified").exists())
        self.assertFalse((self.factory.run / "qualified-commit").exists())
        self.assertFalse((lkg / "current").exists())
        evidence = json.loads((self.factory.run / "qualification-evidence.json").read_text())
        self.assertEqual("failed", evidence["status"])

    def test_successful_qualification_publishes_private_metadata(self) -> None:
        lkg = self.root / "lkg"
        secure_directory(lkg)
        exit_code = MODULE.main([
            "qualify",
            "--run-directory", str(self.factory.run),
            "--expected-commit", self.factory.commit,
            "--expected-run-id", self.factory.run_id,
            "--expected-run-attempt", str(self.factory.attempt),
            "--project-name", self.factory.project,
            "--lkg-pointer", str(lkg / "current"),
        ])
        self.assertEqual(0, exit_code)
        self.assertEqual(b"", (self.factory.run / "security-qualified").read_bytes())
        self.assertEqual(self.factory.commit + "\n", (self.factory.run / "qualified-commit").read_text())
        self.assertEqual(str(self.factory.run) + "\n", (lkg / "current").read_text())
        evidence = json.loads(
            (self.factory.run / "qualification-evidence.json").read_text()
        )
        self.assertEqual("passed", evidence["status"])


class RecoveryGateTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.factory = EvidenceFactory(self.root)
        self.factory.create()
        evidence = self.factory.verify()
        secure_json(self.factory.run / "qualification-evidence.json", evidence)
        secure_bytes(self.factory.run / "qualified-commit", (self.factory.commit + "\n").encode())
        secure_bytes(self.factory.run / "security-qualified", b"")
        self.hook = self.root / "recovery-hook"
        secure_bytes(self.hook, b"#!/bin/sh\nexit 0\n")
        if os.name == "posix":
            self.hook.chmod(0o700)
        self.env = self.root / "phase0.env"
        secure_bytes(
            self.env,
            f"BOLT_PHASE0_RECOVERY_SYNTHETIC_COMMAND_PATH={self.hook}\n".encode(),
        )
        self.output = self.root / "recovery.json"

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def runner(self, stale_synthetic: bool = False, fail_runtime: bool = False):
        def run(command: list[str], _: int) -> subprocess.CompletedProcess:
            self.commands.append(command)
            output = Path(command[command.index("--output") + 1])
            if "--services" in command:
                if fail_runtime:
                    return subprocess.CompletedProcess(command, 1)
                secure_json(
                    output,
                    self.factory.runtime(MODULE.PHASE0_SERVICES, -0.01, "complete"),
                )
            else:
                minute = -10 if stale_synthetic else -0.005
                secure_json(output, self.factory.synthetic("finalized", minute))
            return subprocess.CompletedProcess(command, 0)

        return run

    def call(self, runner=None, freshness: int = 300) -> dict:
        self.commands = []
        times = iter((self.factory.now, self.factory.now, self.factory.now + dt.timedelta(minutes=1)))
        return MODULE.recovery_gate(
            self.env,
            self.factory.project,
            self.factory.run,
            self.factory.run_id,
            self.factory.attempt,
            self.output,
            freshness,
            30,
            runner=runner or self.runner(),
            now_provider=lambda: next(times),
        )

    def test_recovery_gate_proves_runtime_and_fresh_finalized_synthetic(self) -> None:
        evidence = self.call()
        self.assertEqual(
            {
                "schema": MODULE.RECOVERY_GATE_SCHEMA,
                "status": "passed",
                "qualified_run_id": self.factory.run_id,
                "qualified_run_attempt": self.factory.attempt,
                "project_name": self.factory.project,
                "checks": {"authenticated_synthetic": True, "readiness": True},
            },
            evidence,
        )
        self.assertEqual(
            self.factory.run / "verify-bolt-phase0-runtime.py",
            Path(self.commands[0][1]),
        )
        self.assertEqual(
            self.factory.run / "run-bolt-phase0-recovery-synthetic.py",
            Path(self.commands[1][0]),
        )

    def test_recovery_gate_ignores_candidate_global_hook_and_binds_private_env(self) -> None:
        secure_executable(self.hook, b"#!/bin/sh\nexit 99\n")

        def runner(command: list[str], _: int) -> subprocess.CompletedProcess:
            bound_env = Path(command[command.index("--env-file") + 1])
            values = MODULE.parse_env_file(bound_env)
            for key, name in MODULE.RECOVERY_ENV_TOOL_BINDINGS.items():
                self.assertEqual(str(self.factory.run / name), values[key])
            output = Path(command[command.index("--output") + 1])
            if "--services" in command:
                secure_json(output, self.factory.runtime(MODULE.PHASE0_SERVICES, -0.01, "complete"))
            else:
                self.assertNotEqual(self.hook, Path(command[0]))
                secure_json(output, self.factory.synthetic("finalized", -0.005))
            return subprocess.CompletedProcess(command, 0)

        self.call(runner)

    def test_recovery_gate_rejects_stale_synthetic(self) -> None:
        with self.assertRaises(MODULE.QualificationError) as raised:
            self.call(self.runner(stale_synthetic=True), freshness=60)
        self.assertIn(raised.exception.code, {"stale-evidence", "invalid-synthetic-time"})

    def test_recovery_gate_rejects_failed_runtime_verifier(self) -> None:
        with self.assertRaises(MODULE.QualificationError) as raised:
            self.call(self.runner(fail_runtime=True))
        self.assertEqual("recovery-runtime-verifier-failed", raised.exception.code)

    def test_recovery_gate_rejects_missing_private_hook(self) -> None:
        secure_bytes(self.env, b"UNRELATED=value\n")
        with self.assertRaises(MODULE.QualificationError) as raised:
            self.call()
        self.assertEqual("missing-recovery-synthetic-hook", raised.exception.code)

    def test_recovery_gate_rejects_tampered_qualified_artifact(self) -> None:
        evidence = json.loads((self.factory.run / "qualification-evidence.json").read_text())
        evidence["artifacts"]["image-pins.json"]["sha256"] = digest("tampered")
        secure_json(self.factory.run / "qualification-evidence.json", evidence)
        with self.assertRaises(MODULE.QualificationError) as raised:
            self.call()
        self.assertEqual("qualified-artifact-digest-mismatch", raised.exception.code)

    def test_recovery_gate_rejects_tampered_qualified_tool(self) -> None:
        secure_executable(
            self.factory.run / "run-bolt-phase0-recovery-synthetic.py",
            b"#!/usr/bin/env python3\nraise SystemExit(99)\n",
        )
        with self.assertRaises(MODULE.QualificationError) as raised:
            self.call()
        self.assertEqual("qualified-artifact-digest-mismatch", raised.exception.code)


if __name__ == "__main__":
    unittest.main()
