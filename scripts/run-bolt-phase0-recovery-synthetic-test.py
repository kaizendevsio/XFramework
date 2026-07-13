#!/usr/bin/env python3
"""Contract and fail-closed tests for the local Phase 0 recovery synthetic."""

from __future__ import annotations

import base64
import datetime as dt
import hashlib
import importlib.util
import json
import os
import stat
import sys
import tempfile
import threading
import unittest
import uuid
from pathlib import Path
from types import SimpleNamespace
from typing import Any, Mapping
from unittest import mock


SCRIPT = Path(__file__).with_name("run-bolt-phase0-recovery-synthetic.py")
QUALIFICATION_SCRIPT = Path(__file__).with_name("verify-bolt-phase0-qualification.py")


def load_module(name: str, path: Path) -> Any:
    specification = importlib.util.spec_from_file_location(name, path)
    if specification is None or specification.loader is None:
        raise RuntimeError(f"cannot load {path}")
    module = importlib.util.module_from_spec(specification)
    sys.modules[name] = module
    specification.loader.exec_module(module)
    return module


MODULE = load_module("bolt_phase0_recovery_synthetic", SCRIPT)
QUALIFICATION = load_module("bolt_phase0_qualification_for_recovery_test", QUALIFICATION_SCRIPT)

UTC = dt.timezone.utc
NOW = dt.datetime(2026, 7, 13, 0, 0, 0, tzinfo=UTC)
CORE_STARTED = NOW + dt.timedelta(minutes=1)
PROBE_STARTED = NOW + dt.timedelta(minutes=2)
PROBE_COMPLETED = NOW + dt.timedelta(minutes=3)
CORE_COMPLETED = NOW + dt.timedelta(minutes=10)
PIN = "registry.local/xframework/bolt-phase0-synthetics@sha256:" + "a" * 64
CONTAINER = "b" * 64


def secure_directory(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)
    if os.name == "posix":
        path.chmod(0o700)


def secure_bytes(path: Path, value: bytes, *, executable: bool = False) -> None:
    path.write_bytes(value)
    if os.name == "posix":
        path.chmod(0o700 if executable else 0o600)


def encoded(value: Mapping[str, Any]) -> str:
    raw = json.dumps(value, separators=(",", ":"), sort_keys=True).encode("ascii")
    return base64.urlsafe_b64encode(raw).rstrip(b"=").decode("ascii")


def jwt(marker: str, expiration: dt.datetime, *, generation: str = "current") -> bytes:
    header = encoded({"alg": "RS256", "typ": "JWT"})
    payload = encoded(
        {
            "exp": int(expiration.timestamp()),
            "iss": "https://identity.local:8443",
            "jti": marker,
            "credential_generation_id": generation,
        }
    )
    return f"{header}.{payload}.test-signature-value\n".encode("ascii")


def timestamp(value: dt.datetime) -> str:
    return value.isoformat(timespec="seconds").replace("+00:00", "Z")


def write_json(path: Path, value: Mapping[str, Any]) -> None:
    secure_bytes(path, (json.dumps(value, sort_keys=True) + "\n").encode("utf-8"))


class QualificationFacade:
    def __init__(self) -> None:
        self.validated: list[dict[str, Any]] = []
        self.validated_modes: list[str] = []

    @staticmethod
    def qualification_evidence_for_recovery(_: Path, run_id: str, attempt: int) -> dict[str, Any]:
        if run_id != "123" or attempt != 1:
            raise ValueError("wrong run")
        return {
            "schema": MODULE.QUALIFICATION_SCHEMA,
            "status": "passed",
            "source_commit": "c" * 40,
        }

    @staticmethod
    def parse_timestamp(value: str, _: str) -> dt.datetime:
        return dt.datetime.fromisoformat(value.replace("Z", "+00:00"))

    @staticmethod
    def validate_image_pins(
        document: dict[str, Any], commit: str, _: dt.datetime, __: int
    ) -> tuple[dict[str, str], dt.datetime]:
        if document.get("schema") != MODULE.PINS_SCHEMA or commit != "c" * 40:
            raise ValueError("pins")
        return document["pins"], NOW

    @staticmethod
    def validate_override(document: dict[str, Any], pins: dict[str, str]) -> None:
        if document != {
            "services": {
                MODULE.SYNTHETIC_SERVICE: {"image": pins[MODULE.SYNTHETIC_SERVICE]}
            }
        }:
            raise ValueError("override")

    def validate_synthetic(
        self,
        document: dict[str, Any],
        stage: str,
        _: dt.datetime,
        __: int,
        *,
        not_before: dt.datetime,
        proxy_mode: str,
    ) -> Any:
        result = QUALIFICATION.validate_synthetic(
            document,
            stage,
            CORE_COMPLETED + dt.timedelta(minutes=1),
            3600,
            not_before=not_before,
            proxy_mode=proxy_mode,
        )
        self.validated.append(document)
        self.validated_modes.append(proxy_mode)
        return result


class MockRunner:
    def __init__(self, fixture: "RecoveryFixture") -> None:
        self.fixture = fixture
        self.commands: list[tuple[list[str], dict[str, str]]] = []
        self.manifest_sizes: dict[str, int] = {}
        self.probes = 0
        self.release_core = threading.Event()
        self.lock = threading.Lock()
        self.refresh_output = b""
        self.probe_output_kind: str | None = None
        self.bad_image = False
        self.fail_core = False
        self.fast_core = False
        self.secret_core = False
        self.bad_core_prefix = False
        self.bad_probe_kind: str | None = None
        self.proxy_receipt_mode: str | None = None
        self.mutate_artifact = False
        self.mutate_token = False

    def __call__(
        self,
        command: list[str],
        _: float,
        environment: Mapping[str, str],
        __: Path,
    ) -> Any:
        env = dict(environment)
        with self.lock:
            self.commands.append((list(command), env))
        executable = Path(command[0]).name
        if executable == "refresh-bolt-phase0-synthetic-tokens.py":
            self.fixture.refresh_tokens()
            self.fixture.write_refresh_receipt(Path(env["BOLT_SYNTHETIC_REFRESH_RECEIPT"]))
            return MODULE.ProcessResult(0, self.refresh_output, b"")
        if executable in {
            "run-bolt-phase0-marker-scan.py",
            "run-bolt-phase0-operational-probe.py",
        }:
            kind = env["BOLT_SYNTHETIC_PROBE_KIND"]
            manifest = json.loads(Path(env["BOLT_SYNTHETIC_TOKEN_MANIFEST"]).read_text())
            self.manifest_sizes[kind] = len(manifest["tokens"])
            receipt_kind = "plaintext-rejection" if self.bad_probe_kind == kind else kind
            self.fixture.write_probe_receipt(
                Path(env["BOLT_SYNTHETIC_PROBE_RECEIPT"]),
                receipt_kind,
                proxy_mode=(
                    self.proxy_receipt_mode
                    or self.fixture.values["BOLT_SYNTHETIC_PROXY_MODE"]
                ),
            )
            with self.lock:
                self.probes += 1
                if self.probes == 5:
                    if self.mutate_artifact:
                        secure_bytes(self.fixture.run / "docker-compose.yml", b"mutated\n")
                    self.release_core.set()
            output = b"unexpected" if self.probe_output_kind == kind else b""
            return MODULE.ProcessResult(0, output, b"")
        if command[1:3] == ["image", "inspect"]:
            digests = ["registry.local/wrong@sha256:" + "d" * 64] if self.bad_image else [PIN]
            return MODULE.ProcessResult(0, json.dumps(digests).encode(), b"")
        if "compose" in command and "run" in command:
            if not self.fast_core:
                self.release_core.wait(timeout=3)
            if self.fail_core:
                return MODULE.ProcessResult(1, b"", b"")
            if self.secret_core:
                return MODULE.ProcessResult(0, self.fixture.current_tokens["user"], b"")
            return MODULE.ProcessResult(0, self.fixture.core_report(self.bad_core_prefix), b"")
        if "compose" in command and command[-2:] == ["ps", "-q"]:
            return MODULE.ProcessResult(0, (CONTAINER + "\n").encode("ascii"), b"")
        if command[1:3] == ["logs", "--since"]:
            if self.mutate_token:
                secure_bytes(self.fixture.token_paths["user"], self.fixture.current_tokens["user"] + b"x")
                self.mutate_token = False
            return MODULE.ProcessResult(0, b"application log without credentials", b"")
        raise AssertionError(f"unexpected command: {command}")


class RecoveryFixture:
    def __init__(self, root: Path) -> None:
        self.root = root.resolve()
        self.run = self.root / "123-1"
        self.hooks = self.root / "hooks"
        self.secrets = self.root / "secrets"
        self.output_dir = self.root / "output"
        self.tmpfs = self.root / "tmpfs"
        for directory in (self.run, self.hooks, self.secrets, self.output_dir, self.tmpfs):
            secure_directory(directory)
        self.output = self.output_dir / "synthetic.json"
        secure_bytes(self.output, b"")
        self.env = self.secrets / "phase0.env"
        self.docker = self.root / "docker"
        secure_bytes(self.docker, b"#!/bin/sh\nexit 1\n", executable=True)
        if os.name == "posix":
            self.docker.chmod(0o755)

        self.refresh = self.hooks / "refresh-bolt-phase0-synthetic-tokens.py"
        self.marker = self.hooks / "run-bolt-phase0-marker-scan.py"
        self.operational = self.hooks / "run-bolt-phase0-operational-probe.py"
        for hook in (self.refresh, self.marker, self.operational):
            secure_bytes(hook, b"#!/usr/bin/env python3\n", executable=True)

        self.token_paths = {
            purpose: self.secrets / f"{purpose}.jwt"
            for purpose in (*MODULE.CURRENT_PURPOSES, *MODULE.RETIRED_PURPOSES)
        }
        self.current_tokens: dict[str, bytes] = {}
        self.retired_tokens = {
            "rejected_communications": jwt(
                "00000000-0000-4000-8000-000000000101", NOW - dt.timedelta(minutes=1), generation="old"
            ),
            "rejected_user": jwt(
                "00000000-0000-4000-8000-000000000102", NOW - dt.timedelta(minutes=1), generation="old"
            ),
        }
        for purpose in MODULE.CURRENT_PURPOSES:
            secure_bytes(
                self.token_paths[purpose],
                jwt(str(uuid.uuid4()), NOW + dt.timedelta(minutes=30)),
            )
        for purpose, value in self.retired_tokens.items():
            secure_bytes(self.token_paths[purpose], value)
        self.retired_secret = self.secrets / "retired-client-secret"
        secure_bytes(self.retired_secret, b"retired-secret-value-that-is-long-enough\n")

        self.override = {
            "services": {MODULE.SYNTHETIC_SERVICE: {"image": PIN}}
        }
        self.pins = {
            "schema": MODULE.PINS_SCHEMA,
            "generated_at_utc": timestamp(NOW),
            "pins": {MODULE.SYNTHETIC_SERVICE: PIN},
        }
        secure_bytes(self.run / "docker-compose.yml", b"services: {}\n")
        write_json(self.run / "pinned-compose.override.json", self.override)
        write_json(self.run / "image-pins.json", self.pins)
        write_json(self.run / "qualification-evidence.json", {"status": "passed"})
        secure_bytes(self.run / "qualified-commit", ("c" * 40 + "\n").encode())
        secure_bytes(self.run / "security-qualified", b"")
        write_json(self.run / "bolt-tls-evidence.json", {"status": "passed"})
        write_json(self.run / "identityserver-tls-evidence.json", {"status": "passed"})

        values = {
            "BOLT_SYNTHETIC_COMPOSE_PROJECT_NAME": "xframework",
            "BOLT_SYNTHETIC_PROXY_MODE": MODULE.PROXY_MODE_LOGS,
            "BOLT_SYNTHETIC_MIN_TOKEN_LIFETIME_SECONDS": "60",
            "BOLT_SYNTHETIC_TOKEN_REFRESH_COMMAND_PATH": self.refresh.as_posix(),
            "BOLT_SYNTHETIC_PROXY_MARKER_SCAN_COMMAND_PATH": self.marker.as_posix(),
            "BOLT_SYNTHETIC_SEQ_MARKER_SCAN_COMMAND_PATH": self.marker.as_posix(),
            "BOLT_SYNTHETIC_TRACE_MARKER_SCAN_COMMAND_PATH": self.marker.as_posix(),
            "BOLT_SYNTHETIC_PLAINTEXT_REJECTION_COMMAND_PATH": self.operational.as_posix(),
            "BOLT_SYNTHETIC_OLD_GENERATION_REJECTION_COMMAND_PATH": self.operational.as_posix(),
            "BOLT_SYNTHETIC_REJECTED_CLIENT_SECRET_PATH": self.retired_secret.as_posix(),
            "COMMUNICATIONS_SERVICE_IDENTITY_SECRET": "current-secret-value-that-is-long-enough",
            "BOLT_SYNTHETIC_USER_PASSWORD": "private-user-password",
        }
        for purpose, key in MODULE.TOKEN_PATH_KEYS.items():
            values[key] = self.token_paths[purpose].as_posix()
        self.values = values
        self.write_env()

    def write_env(self) -> None:
        secure_bytes(
            self.env,
            "".join(f"{key}={value}\n" for key, value in self.values.items()).encode("utf-8"),
        )

    def refresh_tokens(self) -> None:
        self.current_tokens = {
            "communications": jwt(
                "00000000-0000-4000-8000-000000000001", NOW + dt.timedelta(hours=1)
            ),
            "user": jwt(
                "00000000-0000-4000-8000-000000000002", NOW + dt.timedelta(hours=1)
            ),
            "expiry": jwt(
                "00000000-0000-4000-8000-000000000003", NOW + dt.timedelta(minutes=5)
            ),
        }
        for purpose, value in self.current_tokens.items():
            secure_bytes(self.token_paths[purpose], value)

    def write_refresh_receipt(self, path: Path) -> None:
        write_json(
            path,
            {
                "schemaVersion": MODULE.REFRESH_SCHEMA,
                "status": "passed",
                "issuerUri": "https://identity.local:8443",
                "principalReference": "phase0-deployment-gate",
                "refreshedAtUtc": timestamp(NOW),
                "tokenExpirationsUtc": {
                    "communications": timestamp(NOW + dt.timedelta(hours=1)),
                    "user": timestamp(NOW + dt.timedelta(hours=1)),
                    "expiry": timestamp(NOW + dt.timedelta(minutes=5)),
                },
            },
        )

    def write_probe_receipt(self, path: Path, kind: str, *, proxy_mode: str) -> None:
        assertions = MODULE._probe_assertions(proxy_mode).get(kind, {})
        write_json(
            path,
            {
                "schemaVersion": MODULE.PROBE_SCHEMA,
                "probe": kind,
                "status": "passed",
                "startedAtUtc": timestamp(PROBE_STARTED),
                "completedAtUtc": timestamp(PROBE_COMPLETED),
                "assertions": assertions,
            },
        )

    def core_report(self, bad_prefix: bool = False) -> bytes:
        prefixes = {
            purpose: hashlib.sha256(self.current_tokens[purpose].rstrip(b"\r\n")).hexdigest()[:12]
            for purpose in MODULE.CURRENT_PURPOSES
        }
        if bad_prefix:
            prefixes["user"] = "f" * 12
        operations = []
        for name in sorted(MODULE.REQUIRED_OPERATIONS):
            results = {"result": "true"}
            if name == "durable_ack":
                results = {
                    "duplicate_ack_idempotent": "true",
                    "out_of_order_ack_monotonic": "true",
                }
            operations.append(
                {
                    "name": name,
                    "startedAtUtc": timestamp(CORE_STARTED + dt.timedelta(seconds=1)),
                    "completedAtUtc": timestamp(CORE_COMPLETED - dt.timedelta(seconds=1)),
                    "status": "passed",
                    "timingMs": 1,
                    "results": results,
                }
            )
        report = {
            "schemaVersion": MODULE.CORE_SCHEMA,
            "runId": "11111111-1111-4111-8111-111111111111",
            "tokenSha256Prefixes": prefixes,
            "startedAtUtc": timestamp(CORE_STARTED),
            "completedAtUtc": timestamp(CORE_COMPLETED),
            "target": "wss://bolt-hub:8443/bolt/ws",
            "status": "passed",
            "timings": {"totalMs": 540000},
            "operations": operations,
        }
        return (json.dumps(report, sort_keys=True, separators=(",", ":")) + "\n").encode()


class RecoverySyntheticTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.fixture = RecoveryFixture(Path(self.temporary.name))
        self.qualification = QualificationFacade()
        self.runner = MockRunner(self.fixture)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def call(self) -> dict[str, Any]:
        return MODULE.run_recovery_synthetic(
            self.fixture.env,
            "xframework",
            self.fixture.run,
            "finalized",
            self.fixture.output,
            runner=self.runner,
            sleeper=lambda _: None,
            now_provider=lambda: NOW,
            tmpfs_root=self.fixture.tmpfs,
            require_tmpfs=False,
            qualification_module=self.qualification,
            docker_path=self.fixture.docker,
        )

    def assert_failure(self, code: str) -> None:
        with self.assertRaises(MODULE.RecoveryError) as raised:
            self.call()
        self.assertEqual(code, str(raised.exception))

    def test_success_uses_exact_pin_and_exact_finalized_probe_contract(self) -> None:
        evidence = self.call()
        self.assertEqual(MODULE.SYNTHETIC_SCHEMA, evidence["schemaVersion"])
        self.assertEqual("finalized", evidence["stage"])
        self.assertEqual(1, len(self.qualification.validated))
        self.assertEqual([MODULE.PROXY_MODE_LOGS], self.qualification.validated_modes)
        self.assertEqual(3, self.runner.manifest_sizes["proxy-marker-scan"])
        self.assertEqual(5, self.runner.manifest_sizes["old-generation-rejection"])
        commands = [command for command, _ in self.runner.commands]
        core = next(command for command in commands if "compose" in command and "run" in command)
        self.assertIn(str(self.fixture.run / "docker-compose.yml"), core)
        self.assertIn(str(self.fixture.run / "pinned-compose.override.json"), core)
        self.assertEqual("never", core[core.index("--pull") + 1])
        self.assertEqual(MODULE.SYNTHETIC_SERVICE, core[-1])
        self.assertFalse(any("redis-interruption" in env.values() for _, env in self.runner.commands))
        serialized = self.fixture.output.read_bytes()
        for token in (*self.fixture.current_tokens.values(), *self.fixture.retired_tokens.values()):
            self.assertNotIn(token.rstrip(b"\r\n"), serialized)

    def test_direct_kestrel_mode_accepts_only_proxy_not_applicable_receipt(self) -> None:
        self.fixture.values["BOLT_SYNTHETIC_PROXY_MODE"] = MODULE.PROXY_MODE_DIRECT_KESTREL
        self.fixture.write_env()

        evidence = self.call()

        receipts = evidence["postRunEvidence"]["probeReceipts"]
        self.assertEqual(
            "not_applicable", evidence["postRunEvidence"]["markerAbsence"]["proxy"]
        )
        self.assertEqual(
            {
                "retainedStoreQueried": False,
                "notApplicableReason": "direct-kestrel-publication",
                "matches": 0,
                "tokensSearched": 3,
                "markersSearched": 3,
            },
            receipts["proxyMarkerScan"]["assertions"],
        )
        for name in ("seqMarkerScan", "traceMarkerScan"):
            self.assertEqual(MODULE._retained_marker_assertions(), receipts[name]["assertions"])
        self.assertEqual(
            [MODULE.PROXY_MODE_DIRECT_KESTREL], self.qualification.validated_modes
        )

    def test_rejects_proxy_mode_receipt_mismatches_in_both_directions(self) -> None:
        self.runner.proxy_receipt_mode = MODULE.PROXY_MODE_DIRECT_KESTREL
        self.assert_failure("PROBE_RECEIPT")

        self.runner = MockRunner(self.fixture)
        self.fixture.values["BOLT_SYNTHETIC_PROXY_MODE"] = MODULE.PROXY_MODE_DIRECT_KESTREL
        self.fixture.write_env()
        self.runner.proxy_receipt_mode = MODULE.PROXY_MODE_LOGS
        self.assert_failure("PROBE_RECEIPT")

    def test_rejects_missing_or_non_exact_proxy_mode_before_children(self) -> None:
        del self.fixture.values["BOLT_SYNTHETIC_PROXY_MODE"]
        self.fixture.write_env()
        self.assert_failure("CONFIG_BOLT_SYNTHETIC_PROXY_MODE")
        self.assertEqual([], self.runner.commands)

        for value in ("LOGS", "direct_kestrel", "direct-kestrelx"):
            with self.subTest(value=value):
                self.fixture.values["BOLT_SYNTHETIC_PROXY_MODE"] = value
                self.fixture.write_env()
                self.assert_failure("PROXY_MODE")
                self.assertEqual([], self.runner.commands)

    def test_cli_accepts_only_each_required_option_once(self) -> None:
        args = MODULE.parse_args(
            [
                "--env-file", str(self.fixture.env),
                "--project-name", "xframework",
                "--run-directory", str(self.fixture.run),
                "--stage", "finalized",
                "--output", str(self.fixture.output),
            ]
        )
        self.assertEqual("finalized", args.stage)
        for invalid in (
            ["--stage", "finalized"],
            [
                "--env-file", "a", "--env-file", "b", "--project-name", "p",
                "--run-directory", "r", "--stage", "finalized", "--output", "o",
            ],
            [
                "--env-file", "a", "--project-name", "p", "--run-directory", "r",
                "--stage", "canary", "--output", "o",
            ],
        ):
            with self.assertRaises(MODULE.RecoveryError):
                MODULE.parse_args(invalid)

    def test_rejects_wrong_stage_project_and_run_identity_before_children(self) -> None:
        with self.assertRaises(MODULE.RecoveryError):
            MODULE.run_recovery_synthetic(
                self.fixture.env, "xframework", self.fixture.run, "canary", self.fixture.output
            )
        self.fixture.values["BOLT_SYNTHETIC_COMPOSE_PROJECT_NAME"] = "wrong"
        self.fixture.write_env()
        self.assert_failure("PROJECT_BINDING")
        self.assertEqual([], self.runner.commands)

    def test_rejects_mutable_or_mismatched_synthetic_image(self) -> None:
        self.fixture.pins["pins"][MODULE.SYNTHETIC_SERVICE] = "registry.local/image:latest"
        write_json(self.fixture.run / "image-pins.json", self.fixture.pins)
        self.fixture.override["services"][MODULE.SYNTHETIC_SERVICE]["image"] = "registry.local/image:latest"
        write_json(self.fixture.run / "pinned-compose.override.json", self.fixture.override)
        self.assert_failure("MUTABLE_IMAGE")
        self.assertEqual([], self.runner.commands)

    def test_rejects_local_image_not_bound_to_qualified_digest(self) -> None:
        self.runner.bad_image = True
        self.assert_failure("LOCAL_IMAGE")

    def test_rejects_nonempty_or_in_run_output(self) -> None:
        secure_bytes(self.fixture.output, b"unexpected")
        self.assert_failure("OUTPUT")
        secure_bytes(self.fixture.output, b"")
        with self.assertRaises(MODULE.RecoveryError) as raised:
            MODULE.run_recovery_synthetic(
                self.fixture.env,
                "xframework",
                self.fixture.run,
                "finalized",
                self.fixture.run / "new-output.json",
            )
        self.assertEqual("OUTPUT_IN_QUALIFIED_RUN", str(raised.exception))

    @unittest.skipUnless(os.name == "posix", "POSIX mode contract")
    def test_rejects_insecure_or_symlinked_installed_hook(self) -> None:
        self.fixture.refresh.chmod(0o755)
        self.assert_failure("HOOK")
        self.fixture.refresh.unlink()
        self.fixture.refresh.symlink_to(self.fixture.marker)
        self.assert_failure("CONFIG_BOLT_SYNTHETIC_TOKEN_REFRESH_COMMAND_PATH")

    def test_rejects_duplicate_token_destinations(self) -> None:
        self.fixture.values[MODULE.TOKEN_PATH_KEYS["expiry"]] = self.fixture.token_paths["user"].as_posix()
        self.fixture.write_env()
        self.assert_failure("TOKEN_PATH_ALIAS")

    def test_rejects_current_tokens_below_configured_minimum_lifetime(self) -> None:
        self.fixture.values["BOLT_SYNTHETIC_MIN_TOKEN_LIFETIME_SECONDS"] = "3600"
        self.fixture.write_env()

        def refresh_short_lived_tokens() -> None:
            self.fixture.current_tokens = {
                "communications": jwt(
                    "00000000-0000-4000-8000-000000000001",
                    NOW + dt.timedelta(minutes=30),
                ),
                "user": jwt(
                    "00000000-0000-4000-8000-000000000002",
                    NOW + dt.timedelta(minutes=30),
                ),
                "expiry": jwt(
                    "00000000-0000-4000-8000-000000000003",
                    NOW + dt.timedelta(minutes=5),
                ),
            }
            for purpose, value in self.fixture.current_tokens.items():
                secure_bytes(self.fixture.token_paths[purpose], value)

        self.fixture.refresh_tokens = refresh_short_lived_tokens  # type: ignore[method-assign]
        self.assert_failure("TOKEN_LIFETIME")

    def test_rejects_refresh_or_probe_output(self) -> None:
        self.runner.refresh_output = b"unexpected"
        self.assert_failure("UNEXPECTED_CHILD_OUTPUT")
        self.runner = MockRunner(self.fixture)
        self.runner.probe_output_kind = "seq-marker-scan"
        self.assert_failure("UNEXPECTED_CHILD_OUTPUT")

    def test_rejects_core_failure_secret_output_and_early_completion(self) -> None:
        self.runner.fail_core = True
        self.assert_failure("SUBPROCESS_FAILED")
        self.runner = MockRunner(self.fixture)
        self.runner.secret_core = True
        self.assert_failure("SECRET_OUTPUT")
        self.runner = MockRunner(self.fixture)
        self.runner.fast_core = True
        self.assert_failure("CORE_PROBE_INTERVAL")

    def test_rejects_core_token_prefix_or_probe_receipt_mismatch(self) -> None:
        self.runner.bad_core_prefix = True
        self.assert_failure("CORE_TOKEN_BINDING")
        self.runner = MockRunner(self.fixture)
        self.runner.bad_probe_kind = "old-generation-rejection"
        self.assert_failure("PROBE_RECEIPT")

    def test_rejects_qualified_artifact_or_token_mutation(self) -> None:
        self.runner.mutate_artifact = True
        self.assert_failure("ARTIFACT_MUTATION")
        self.runner = MockRunner(self.fixture)
        secure_bytes(self.fixture.run / "docker-compose.yml", b"services: {}\n")
        self.runner.mutate_token = True
        self.assert_failure("TOKEN_MUTATION")

    def test_requires_tmpfs_when_enforced(self) -> None:
        with self.assertRaises(MODULE.RecoveryError) as raised:
            MODULE.run_recovery_synthetic(
                self.fixture.env,
                "xframework",
                self.fixture.run,
                "finalized",
                self.fixture.output,
                runner=self.runner,
                sleeper=lambda _: None,
                now_provider=lambda: NOW,
                tmpfs_root=self.fixture.tmpfs,
                require_tmpfs=True,
                qualification_module=self.qualification,
                docker_path=self.fixture.docker,
            )
        self.assertEqual("TMPFS_REQUIRED", str(raised.exception))

    def test_atomic_success_leaves_no_temporary_output_or_workspace(self) -> None:
        self.call()
        if os.name == "posix":
            self.assertEqual(0o600, stat.S_IMODE(self.fixture.output.stat().st_mode))
        self.assertEqual([], list(self.fixture.tmpfs.iterdir()))
        self.assertEqual([], list(self.fixture.output_dir.glob(f".{self.fixture.output.name}.*")))

    def test_sealed_directory_rejects_wrong_owner_and_writable_modes(self) -> None:
        def metadata(uid: int, mode: int) -> Any:
            return SimpleNamespace(st_mode=stat.S_IFDIR | mode, st_uid=uid)

        with mock.patch.object(MODULE, "ENFORCE_POSIX", True):
            for uid, mode in ((1000, 0o550), (0, 0o750), (0, 0o570)):
                with self.subTest(uid=uid, mode=oct(mode)), mock.patch.object(
                    MODULE.os, "lstat", return_value=metadata(uid, mode)
                ):
                    with self.assertRaisesRegex(MODULE.RecoveryError, "SEALED_DIRECTORY"):
                        MODULE._validate_directory(
                            self.fixture.run, "SEALED_DIRECTORY", sealed=True
                        )

    def test_sealed_helper_rejects_wrong_owner_or_mode(self) -> None:
        def metadata(uid: int, mode: int) -> Any:
            return SimpleNamespace(
                st_mode=stat.S_IFREG | mode,
                st_uid=uid,
                st_nlink=1,
                st_size=16,
            )

        with mock.patch.object(MODULE, "ENFORCE_POSIX", True):
            for uid, mode in ((1000, 0o550), (0, 0o750), (0, 0o440)):
                with self.subTest(uid=uid, mode=oct(mode)), mock.patch.object(
                    MODULE.os, "lstat", return_value=metadata(uid, mode)
                ):
                    with self.assertRaisesRegex(MODULE.RecoveryError, "SEALED_HELPER"):
                        MODULE._file_metadata(
                            self.fixture.refresh,
                            "SEALED_HELPER",
                            maximum=1024,
                            executable=True,
                            sealed_mode=0o550,
                        )

    def test_sealed_hooks_must_be_bound_inside_qualified_run(self) -> None:
        with self.assertRaisesRegex(MODULE.RecoveryError, "HOOK_DIRECTORY"):
            MODULE._validate_hooks(self.fixture.values, self.fixture.run, sealed=True)

    def test_unsealed_private_helper_remains_owner_bound_mode_0700(self) -> None:
        metadata = SimpleNamespace(
            st_mode=stat.S_IFREG | 0o550,
            st_uid=1000,
            st_nlink=1,
            st_size=16,
        )
        with (
            mock.patch.object(MODULE, "ENFORCE_POSIX", True),
            mock.patch.object(MODULE.os, "geteuid", return_value=1000, create=True),
            mock.patch.object(MODULE.os, "lstat", return_value=metadata),
            self.assertRaisesRegex(MODULE.RecoveryError, "UNSEALED_HELPER"),
        ):
            MODULE._file_metadata(
                self.fixture.refresh,
                "UNSEALED_HELPER",
                maximum=1024,
                private=True,
                executable=True,
            )

    def test_sealed_integrity_recheck_preserves_root_owned_mode_contract(self) -> None:
        snapshot = MODULE.FileSnapshot(1, 2, 16, 3, 0o550, 0, "a" * 64)
        with mock.patch.object(
            MODULE,
            "_read_file",
            return_value=(b"x" * 16, snapshot),
        ) as read_file:
            MODULE._verify_file(
                self.fixture.refresh,
                snapshot,
                "SEALED_RECHECK",
                maximum=1024,
                private=False,
                sealed=True,
            )

        self.assertEqual(0o550, read_file.call_args.kwargs["sealed_mode"])


if __name__ == "__main__":
    unittest.main()
