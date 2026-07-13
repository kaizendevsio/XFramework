#!/usr/bin/env python3
from __future__ import annotations

import base64
import contextlib
import importlib.util
import io
import json
import os
import stat
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


SCRIPT = Path(__file__).with_name("run-bolt-phase0-post-recovery-durable.py")
SPEC = importlib.util.spec_from_file_location("bolt_phase0_post_recovery_durable", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)

PROJECT = "xframework"
CONTAINER_ID = "a" * 64
PIN = "registry.example.test/xframework/bolt-phase0-synthetics@sha256:" + "b" * 64


def private_file(path: Path, data: bytes | str, mode: int = 0o600) -> Path:
    path.write_bytes(data.encode("utf-8") if isinstance(data, str) else data)
    path.chmod(mode)
    return path.resolve()


def jwt(subject: str, marker: str) -> bytes:
    def segment(value: dict[str, object]) -> bytes:
        raw = json.dumps(value, separators=(",", ":")).encode("ascii")
        return base64.urlsafe_b64encode(raw).rstrip(b"=")

    return b".".join(
        [segment({"alg": "HS512"}), segment({"sub": subject, "jti": marker}), b"c2ln"]
    )


def operation(name: str, results: dict[str, str] | None = None) -> dict[str, object]:
    return {
        "name": name,
        "startedAtUtc": "2026-07-13T12:00:00Z",
        "completedAtUtc": "2026-07-13T12:00:01Z",
        "status": "passed",
        "timingMs": 100,
        "results": results or {"outcome": "passed"},
    }


class Runner:
    def __init__(self, workspace: "Workspace") -> None:
        self.workspace = workspace
        self.commands: list[tuple[list[str], float, dict[str, str]]] = []
        self.returncode = 0
        self.stderr = b""
        self.stdout: bytes | None = None
        self.labels: dict[str, str] = {
            "com.docker.compose.project": PROJECT,
            "com.docker.compose.service": "bolt-hub",
            "com.docker.compose.project.config_files": (
                f"{workspace.compose},{workspace.override}"
            ),
        }
        self.mutate: Path | None = None

    def __call__(
        self, command: list[str], timeout: float, environment: dict[str, str]
    ) -> MODULE.ProcessResult:
        self.commands.append((list(command), timeout, dict(environment)))
        if command[:3] == ["docker", "container", "ls"]:
            return MODULE.ProcessResult(0, f"{CONTAINER_ID}\n".encode("ascii"), b"")
        if command[:2] == ["docker", "inspect"]:
            return MODULE.ProcessResult(0, json.dumps(self.labels).encode("ascii"), b"")
        if command[:2] == ["docker", "compose"]:
            if self.mutate is not None:
                private_file(self.mutate, self.mutate.read_bytes() + b"\n")
            return MODULE.ProcessResult(
                self.returncode,
                self.stdout if self.stdout is not None else self.workspace.report(),
                self.stderr,
            )
        raise AssertionError(command)


class Workspace:
    def __init__(self, root: Path) -> None:
        self.root = root
        self.root.chmod(0o700)
        self.run_dir = self.root / "run"
        self.run_dir.mkdir(mode=0o700)
        self.compose = private_file(self.run_dir / "docker-compose.yml", "services: {}\n")
        self.override = private_file(
            self.run_dir / "pinned-compose.override.json",
            json.dumps(
                {"services": {"bolt-phase0-synthetics": {"image": PIN}}},
                separators=(",", ":"),
            ),
        )
        self.communication_marker = "11111111-1111-4111-8111-111111111111"
        self.user_marker = "22222222-2222-4222-8222-222222222222"
        self.communication_token = jwt("communications", self.communication_marker)
        self.user_token = jwt("user", self.user_marker)
        self.communication_path = private_file(
            self.root / "communications.jwt", self.communication_token + b"\n"
        )
        self.user_path = private_file(self.root / "user.jwt", self.user_token + b"\n")
        self.env = private_file(
            self.root / "deployment.env",
            "\n".join(
                [
                    f"BOLT_SYNTHETIC_COMPOSE_PROJECT_NAME={PROJECT}",
                    "BOLT_SYNTHETIC_COMMUNICATIONS_TOKEN_PATH="
                    f"{self.communication_path.as_posix()}",
                    f"BOLT_SYNTHETIC_USER_TOKEN_PATH={self.user_path.as_posix()}",
                    "",
                ]
            ),
        )
        self.manifest = private_file(
            self.root / "manifest.json",
            json.dumps(
                {
                    "schemaVersion": MODULE.MANIFEST_SCHEMA,
                    "tokens": [
                        {
                            "purpose": "communications",
                            "path": str(self.communication_path),
                            "marker": self.communication_marker,
                        },
                        {
                            "purpose": "user",
                            "path": str(self.user_path),
                            "marker": self.user_marker,
                        },
                    ],
                },
                separators=(",", ":"),
            ),
        )
        self.receipt = self.root / "receipt.json"

    def report(self) -> bytes:
        operations = [operation(name) for name in sorted(MODULE.EXPECTED_OPERATIONS)]
        by_name = {item["name"]: item for item in operations}
        for name, results in MODULE.EXPECTED_DURABLE_RESULTS.items():
            by_name[name]["results"] = results
        document = {
            "schemaVersion": MODULE.REPORT_SCHEMA,
            "runId": "12345678-1234-4234-8234-123456789abc",
            "tokenSha256Prefixes": {
                "communications": MODULE.hashlib.sha256(self.communication_token).hexdigest()[:12],
                "user": MODULE.hashlib.sha256(self.user_token).hexdigest()[:12],
            },
            "startedAtUtc": "2026-07-13T12:00:00Z",
            "completedAtUtc": "2026-07-13T12:00:05Z",
            "target": "wss://bolt-hub:8443/bolt/ws",
            "status": "passed",
            "timings": {"totalMs": 5000},
            "operations": operations,
        }
        return json.dumps(document, separators=(",", ":")).encode("ascii")


class DurablePostRecoveryTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.workspace = Workspace(Path(self.temporary.name).resolve())
        self.runner = Runner(self.workspace)

    def run_probe(self) -> None:
        MODULE.run_probe(
            str(self.workspace.env),
            str(self.workspace.manifest),
            self.workspace.receipt,
            runner=self.runner,
        )

    def test_success_uses_exact_pinned_compose_command_and_receipt(self) -> None:
        self.run_probe()
        command, timeout, environment = self.runner.commands[-1]
        self.assertEqual(["docker", "compose"], command[:2])
        self.assertEqual(MODULE.PROCESS_TIMEOUT_SECONDS, timeout)
        self.assertEqual(
            ["-f", str(self.workspace.compose), "-f", str(self.workspace.override)],
            command[command.index("-f") : command.index("-f") + 4],
        )
        self.assertIn("--no-deps", command)
        self.assertIn("--quiet-pull", command)
        self.assertIn("BOLT_SYNTHETIC_OPERATION_TIMEOUT_SECONDS=10", command)
        self.assertIn("BOLT_SYNTHETIC_EXPIRY_TOKEN_FILE=", command)
        self.assertEqual("bolt-phase0-synthetics", command[-1])
        self.assertEqual({"PATH", "HOME"}, set(environment))
        serialized_command = "\0".join(command).encode("utf-8")
        for secret in (
            self.workspace.communication_token,
            self.workspace.user_token,
            self.workspace.communication_marker.encode(),
            self.workspace.user_marker.encode(),
        ):
            self.assertNotIn(secret, serialized_command)

        receipt = json.loads(self.workspace.receipt.read_text(encoding="ascii"))
        self.assertEqual(
            {
                "schemaVersion": "bolt-phase0-post-recovery-durable/v1",
                "status": "passed",
                "assertions": {
                    "durableStateVerified": True,
                    "dataLossObserved": False,
                },
            },
            receipt,
        )
        if MODULE.ENFORCE_POSIX_PERMISSIONS:
            self.assertEqual(0o600, stat.S_IMODE(self.workspace.receipt.stat().st_mode))
        self.assertEqual([], list(self.workspace.root.glob(".receipt.json.*")))

    def test_raw_token_or_jti_in_stdout_or_stderr_is_rejected(self) -> None:
        for secret, stderr in (
            (self.workspace.communication_token, False),
            (self.workspace.user_token, True),
            (self.workspace.communication_marker.encode(), False),
            (self.workspace.user_marker.encode(), True),
        ):
            with self.subTest(secret=secret[:12], stderr=stderr):
                self.runner.stdout = self.workspace.report()
                self.runner.stderr = b""
                if stderr:
                    self.runner.stderr = b"prefix " + secret
                else:
                    self.runner.stdout += b" prefix " + secret
                with self.assertRaises(MODULE.ProbeError):
                    self.run_probe()
                self.assertFalse(self.workspace.receipt.exists())

    def test_nonzero_stderr_oversized_or_invalid_runner_result_fails_closed(self) -> None:
        cases = [
            MODULE.ProcessResult(1, self.workspace.report(), b""),
            MODULE.ProcessResult(0, self.workspace.report(), b"docker warning"),
            MODULE.ProcessResult(0, b"x" * (MODULE.MAX_PROCESS_OUTPUT_BYTES + 1), b""),
            object(),
        ]
        for result in cases:
            with self.subTest(result=type(result).__name__):
                runner = mock.Mock(side_effect=[
                    MODULE.ProcessResult(0, f"{CONTAINER_ID}\n".encode(), b""),
                    MODULE.ProcessResult(0, json.dumps(self.runner.labels).encode(), b""),
                    result,
                ])
                with self.assertRaises(MODULE.ProbeError):
                    MODULE.run_probe(
                        str(self.workspace.env),
                        str(self.workspace.manifest),
                        self.workspace.receipt,
                        runner=runner,
                    )
                self.assertFalse(self.workspace.receipt.exists())

    def test_report_requires_every_exact_durable_result(self) -> None:
        for operation_name in MODULE.EXPECTED_DURABLE_RESULTS:
            with self.subTest(operation=operation_name):
                report = json.loads(self.workspace.report())
                target = next(
                    item for item in report["operations"] if item["name"] == operation_name
                )
                target["results"] = {"outcome": "passed"}
                self.runner.stdout = json.dumps(report).encode()
                with self.assertRaises(MODULE.ProbeError):
                    self.run_probe()
                self.assertFalse(self.workspace.receipt.exists())

    def test_report_rejects_missing_duplicate_failed_extra_or_unbound_evidence(self) -> None:
        mutations = []
        missing = json.loads(self.workspace.report())
        missing["operations"].pop()
        mutations.append(missing)
        duplicate = json.loads(self.workspace.report())
        duplicate["operations"].append(duplicate["operations"][0])
        mutations.append(duplicate)
        failed = json.loads(self.workspace.report())
        failed["status"] = "failed"
        mutations.append(failed)
        extra = json.loads(self.workspace.report())
        extra["unexpected"] = True
        mutations.append(extra)
        unbound = json.loads(self.workspace.report())
        unbound["tokenSha256Prefixes"]["user"] = "0" * 12
        mutations.append(unbound)
        for report in mutations:
            with self.subTest(keys=sorted(report)):
                self.runner.stdout = json.dumps(report).encode()
                with self.assertRaises(MODULE.ProbeError):
                    self.run_probe()
                self.assertFalse(self.workspace.receipt.exists())

    def test_compose_identity_paths_and_digest_are_strict(self) -> None:
        cases = [
            {"com.docker.compose.project": "attacker"},
            {"com.docker.compose.service": "redis"},
            {"com.docker.compose.project.config_files": str(self.workspace.compose)},
            {
                "com.docker.compose.project.config_files": (
                    f"{self.workspace.override},{self.workspace.compose}"
                )
            },
        ]
        for changes in cases:
            with self.subTest(changes=changes):
                original = dict(self.runner.labels)
                self.runner.labels.update(changes)
                with self.assertRaises(MODULE.ProbeError):
                    self.run_probe()
                self.runner.labels = original
                self.assertFalse(self.workspace.receipt.exists())

        private_file(
            self.workspace.override,
            json.dumps(
                {
                    "services": {
                        "bolt-phase0-synthetics": {
                            "image": (
                                "registry.example.test/xframework/"
                                "bolt-phase0-synthetics:latest"
                            )
                        }
                    }
                }
            ),
        )
        with self.assertRaises(MODULE.ProbeError):
            self.run_probe()

    def test_compose_manifest_env_or_token_change_during_run_is_rejected(self) -> None:
        for path in (
            self.workspace.compose,
            self.workspace.override,
            self.workspace.env,
            self.workspace.manifest,
            self.workspace.user_path,
        ):
            with self.subTest(path=path.name):
                original = path.read_bytes()
                self.runner.mutate = path
                with self.assertRaises(MODULE.ProbeError):
                    self.run_probe()
                private_file(path, original)
                self.runner.mutate = None
                self.assertFalse(self.workspace.receipt.exists())

    def test_manifest_requires_bound_unique_private_jwts_and_matching_jtis(self) -> None:
        documents = []
        missing = json.loads(self.workspace.manifest.read_text())
        missing["tokens"].pop()
        documents.append(missing)
        duplicate = json.loads(self.workspace.manifest.read_text())
        duplicate["tokens"][1]["purpose"] = "communications"
        documents.append(duplicate)
        wrong_marker = json.loads(self.workspace.manifest.read_text())
        wrong_marker["tokens"][0]["marker"] = self.workspace.user_marker
        documents.append(wrong_marker)
        wrong_path = json.loads(self.workspace.manifest.read_text())
        wrong_path["tokens"][0]["path"] = str(self.workspace.user_path)
        documents.append(wrong_path)
        for document in documents:
            with self.subTest(document=document):
                private_file(self.workspace.manifest, json.dumps(document))
                with self.assertRaises(MODULE.ProbeError):
                    self.run_probe()
                self.assertFalse(self.workspace.receipt.exists())

    @unittest.skipUnless(MODULE.ENFORCE_POSIX_PERMISSIONS, "POSIX permission checks")
    def test_group_world_access_on_every_security_boundary_is_rejected(self) -> None:
        for path in (
            self.workspace.env,
            self.workspace.manifest,
            self.workspace.communication_path,
            self.workspace.compose,
            self.workspace.override,
        ):
            with self.subTest(path=path.name):
                path.chmod(0o640)
                with self.assertRaises(MODULE.ProbeError):
                    self.run_probe()
                path.chmod(0o600)
        self.workspace.run_dir.chmod(0o750)
        with self.assertRaises(MODULE.ProbeError):
            self.run_probe()
        self.workspace.run_dir.chmod(0o700)
        self.workspace.root.chmod(0o750)
        with self.assertRaises(MODULE.ProbeError):
            self.run_probe()

    def test_symlink_and_existing_receipt_are_rejected_without_replacement(self) -> None:
        existing = private_file(self.workspace.receipt, "operator-owned")
        with self.assertRaises(MODULE.ProbeError):
            self.run_probe()
        self.assertEqual(b"operator-owned", existing.read_bytes())
        existing.unlink()

        target = private_file(self.workspace.root / "target.json", "target")
        try:
            self.workspace.receipt.symlink_to(target)
        except (OSError, NotImplementedError):
            self.skipTest("symlinks are unavailable")
        with self.assertRaises(MODULE.ProbeError):
            self.run_probe()
        self.assertEqual(b"target", target.read_bytes())

    def test_main_requires_exact_mode_and_is_silent_on_all_failures(self) -> None:
        environment = {
            "XFRAMEWORK_ENV_FILE": str(self.workspace.env),
            "BOLT_SYNTHETIC_TOKEN_MANIFEST": str(self.workspace.manifest),
            "BOLT_SYNTHETIC_POST_RECOVERY_RECEIPT": str(self.workspace.receipt),
            "BOLT_SYNTHETIC_DURABLE_PROBE_MODE": "wrong",
        }
        output = io.StringIO()
        with mock.patch.dict(os.environ, environment, clear=True), contextlib.redirect_stdout(
            output
        ), contextlib.redirect_stderr(output), mock.patch.object(MODULE, "run_probe") as probe:
            self.assertEqual(1, MODULE.main())
        probe.assert_not_called()
        self.assertEqual("", output.getvalue())

    def test_default_runner_uses_no_stdin_bounded_wait_and_kills_timeout(self) -> None:
        process = mock.Mock()
        process.wait.side_effect = [subprocess.TimeoutExpired("docker", 90), None]
        with mock.patch.object(MODULE.subprocess, "Popen", return_value=process) as popen:
            with self.assertRaises(MODULE.ProbeError):
                MODULE._default_process_runner(
                    ["docker", "compose"], 90, {"PATH": "/usr/bin", "HOME": "/tmp"}
                )
        self.assertIs(popen.call_args.kwargs["stdin"], subprocess.DEVNULL)
        self.assertTrue(popen.call_args.kwargs["close_fds"])
        self.assertEqual({"PATH": "/usr/bin", "HOME": "/tmp"}, popen.call_args.kwargs["env"])
        process.terminate.assert_called_once()


if __name__ == "__main__":
    unittest.main()
