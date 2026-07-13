#!/usr/bin/env python3
from __future__ import annotations

import datetime as dt
import importlib.util
import json
import os
import stat
import subprocess
import sys
import tempfile
import threading
import time
import unittest
from pathlib import Path
from typing import Any
from unittest import mock


SCRIPT = Path(__file__).with_name("manage-bolt-phase0-deployment-lease.py")
SPEC = importlib.util.spec_from_file_location("phase0_deployment_lease", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)

NOW = dt.datetime(2026, 7, 13, 10, 0, 0, tzinfo=dt.timezone.utc)


def secure_directory(path: Path) -> Path:
    path.mkdir(parents=True, exist_ok=True)
    if os.name != "nt":
        path.chmod(0o700)
    return path


def secure_file(path: Path, content: str = "helper\n", *, executable: bool = False) -> Path:
    path.write_text(content, encoding="utf-8")
    if os.name != "nt":
        path.chmod(0o700 if executable else 0o600)
    return path


class FakeRunner:
    def __init__(self, fixture: "LeaseFixture") -> None:
        self.fixture = fixture
        self.commands: list[list[str]] = []
        self.fail_step: str | None = None
        self.timeout_step: str | None = None
        self.delay_restore = 0.0
        self.stop_running = False
        self.stop_failure = False
        self.lock = threading.Lock()

    def _step(self, command: list[str]) -> str:
        if len(command) > 1 and command[1] == str(self.fixture.rotation_manager):
            return "rotation"
        if len(command) > 1 and command[1] == str(self.fixture.runtime_verifier):
            return "runtime"
        if command[0] == str(self.fixture.recovery_hook):
            return "recovery"
        if command[0] == str(self.fixture.docker):
            if len(command) > 1 and command[1] == "compose":
                return "restore"
            if len(command) > 1 and command[1] == "stop":
                return "stop"
            if len(command) > 1 and command[1] == "kill":
                return "kill"
            if len(command) > 1 and command[1] in {"inspect", "ps"}:
                return "inspect"
        return "unknown"

    def __call__(
        self,
        command: list[str],
        timeout: int,
        capture: bool,
    ) -> subprocess.CompletedProcess[str]:
        with self.lock:
            self.commands.append(command.copy())
        step = self._step(command)
        if self.timeout_step == step:
            raise subprocess.TimeoutExpired(command, timeout)
        if step == "restore" and self.delay_restore:
            time.sleep(self.delay_restore)
        if step == "runtime" and self.fail_step != "runtime-output":
            output = Path(command[command.index("--output") + 1])
            output.write_text(
                json.dumps({"schema": MODULE.RUNTIME_SCHEMA, "status": "passed"}),
                encoding="utf-8",
            )
        if step == "recovery" and self.fail_step != "recovery-output":
            output = Path(command[command.index("--output") + 1])
            output.write_text(
                json.dumps(
                    {
                        "schema": MODULE.RECOVERY_GATE_SCHEMA,
                        "status": "passed",
                        "qualified_run_id": command[command.index("--qualified-run-id") + 1],
                        "qualified_run_attempt": int(
                            command[command.index("--qualified-run-attempt") + 1]
                        ),
                        "project_name": command[command.index("--project-name") + 1],
                        "checks": {"authenticated_synthetic": True, "readiness": True},
                    }
                ),
                encoding="utf-8",
            )
        if step == "inspect":
            output = "true\n" if self.stop_running else "false\n"
            return subprocess.CompletedProcess(command, 0, output if capture else "", "")
        if step == "stop" and not self.stop_failure:
            self.stop_running = False
        if step == "kill":
            self.stop_running = False
        return subprocess.CompletedProcess(
            command,
            1 if self.fail_step == step or (step == "stop" and self.stop_failure) else 0,
            "suppressed-secret-output" if capture else "",
            "suppressed-secret-error",
        )

    def count(self, step: str) -> int:
        return sum(1 for command in self.commands if self._step(command) == step)


class LeaseFixture(unittest.TestCase):
    def setUp(self) -> None:
        temporary_parent = None
        if os.name == "posix":
            temporary_parent = Path("/root") if os.geteuid() == 0 else Path.home()
        self.temporary = tempfile.TemporaryDirectory(
            dir=temporary_parent
        )
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name).resolve()
        self.state_root = secure_directory(self.root / "state")
        self.run_root = secure_directory(self.root / "runs")
        self.trust_root = secure_directory(self.root / "trust")
        self.lock_file = secure_file(self.trust_root / "deployment-lease.lock", "0")
        if os.name != "nt":
            self.lock_file.chmod(0o440)
        self.owner_uid = os.getuid() if hasattr(os, "getuid") else 0
        self.owner_gid = os.getgid() if hasattr(os, "getgid") else 0
        self.lkg_parent = secure_directory(self.root / "last-known-good")
        self.run_id = "123456789"
        self.run_attempt = 2
        self.run_directory = secure_directory(self.run_root / f"{self.run_id}-{self.run_attempt}")
        self.lkg_run_id = "123456700"
        self.lkg_attempt = 1
        self.lkg_directory = secure_directory(
            self.run_root / f"{self.lkg_run_id}-{self.lkg_attempt}"
        )
        secure_file(self.lkg_directory / "security-qualified", "")
        secure_file(self.lkg_directory / "qualified-commit", "a" * 40 + "\n")
        secure_file(self.lkg_directory / "docker-compose.yml", "services: {}\n")
        secure_file(self.lkg_directory / "pinned-compose.override.json", "{}\n")
        secure_file(self.lkg_directory / "image-pins.json", "{}\n")
        self.lkg_pointer = secure_file(
            self.lkg_parent / "current", str(self.lkg_directory) + "\n"
        )
        self.env_file = secure_file(self.root / "deployment.env", "SECRET=not-emitted\n")
        self.rotation_state = self.root / "rotation-state.json"
        self.rotation_manager = secure_file(self.root / "rotation.py")
        self.runtime_verifier = secure_file(self.root / "runtime.py")
        self.recovery_hook = secure_file(self.root / "recovery-hook", executable=True)
        self.python = secure_file(self.root / "python", executable=True)
        self.docker = secure_file(self.root / "docker", executable=True)
        self.current_time = NOW
        self.config = MODULE.ControllerConfig(
            state_root=self.state_root,
            run_root=self.run_root,
            project_name="xframework",
            deployment_uid=self.owner_uid,
            lock_file=self.lock_file,
            lock_owner_uid=self.owner_uid,
            lock_owner_gid=self.owner_gid,
            lock_parent_uid=self.owner_uid,
        )
        self.runner = FakeRunner(self)
        self.controller = MODULE.DeploymentLeaseController(
            self.config,
            runner=self.runner,
            clock=lambda: self.current_time,
        )
        self.recovery = MODULE.RecoveryConfig(
            lkg_pointer=self.lkg_pointer,
            env_file=self.env_file,
            rotation_state_file=self.rotation_state,
            rotation_manager=self.rotation_manager,
            runtime_verifier=self.runtime_verifier,
            recovery_gate_hook=self.recovery_hook,
            python_executable=self.python,
            docker_executable=self.docker,
            services=MODULE.PHASE0_SERVICES,
            hub_container_name="xframework-bolt-hub",
            subprocess_timeout_seconds=120,
            stop_timeout_seconds=10,
        )

    def arm(self, *, mutation: bool = False) -> None:
        self.controller.arm(self.run_id, self.run_attempt, "preflight", 60)
        if mutation:
            self.current_time += dt.timedelta(seconds=1)
            self.controller.heartbeat(
                self.run_id,
                self.run_attempt,
                "hub-deployed",
                True,
            )

    def make_stale(self) -> None:
        self.current_time += dt.timedelta(seconds=61)

    def read_evidence(self) -> dict[str, Any]:
        return json.loads(self.config.evidence_file.read_text(encoding="utf-8"))

    def assert_hub_stop_invoked(self) -> None:
        self.assertGreaterEqual(self.runner.count("stop"), 1)

    def exclusive_lock(self):
        return MODULE.exclusive_lock(
            self.config.lock_file,
            owner_uid=self.config.lock_owner_uid,
            owner_gid=self.owner_gid,
            trusted_parent_uid=self.config.lock_parent_uid,
        )

    def _seal_active_run(self, *, bind_pointer: bool = True) -> MODULE.DeploymentLeaseController:
        owner_uid = os.getuid() if hasattr(os, "getuid") else 0
        deployment_gid = os.getgid() if hasattr(os, "getgid") else 0
        qualification = {
            "schema": "xframework.bolt.phase0.qualification.v1",
            "status": "passed",
            "generated_at_utc": "2026-07-13T10:00:00Z",
            "run_id": self.run_id,
            "run_attempt": self.run_attempt,
            "source_commit": "a" * 40,
            "credential_generation_id": "generation-test",
            "errors": [],
            "artifacts": {"docker-compose.yml": {"path": "docker-compose.yml"}},
            "runtime_stages": {},
            "synthetic_stages": {},
            "checks": {key: True for key in MODULE.QUALIFICATION_CHECK_KEYS},
        }
        secure_file(self.run_directory / "security-qualified", "")
        secure_file(self.run_directory / "qualified-commit", "a" * 40 + "\n")
        secure_file(
            self.run_directory / "qualification-evidence.json",
            json.dumps(qualification),
        )
        if os.name != "nt":
            for path in self.run_directory.iterdir():
                path.chmod(0o440)
            self.run_directory.chmod(0o550)
        if bind_pointer:
            self.lkg_pointer.write_text(str(self.run_directory) + "\n", encoding="utf-8")
        if os.name != "nt":
            self.lkg_pointer.chmod(0o644)
        config = MODULE.ControllerConfig(
            state_root=self.state_root,
            run_root=self.run_root,
            project_name="xframework",
            deployment_uid=owner_uid,
            lkg_pointer=self.lkg_pointer,
            sealed_owner_uid=owner_uid,
            lock_file=self.lock_file,
            lock_owner_uid=self.owner_uid,
            lock_owner_gid=self.owner_gid,
            lock_parent_uid=self.owner_uid,
        )
        self.assertEqual(deployment_gid, MODULE.DeploymentLeaseController(config)._deployment_gid())
        return MODULE.DeploymentLeaseController(
            config, runner=self.runner, clock=lambda: self.current_time
        )


class LifecycleTests(LeaseFixture):
    @unittest.skipUnless(sys.platform.startswith("linux"), "Linux flock identity contract")
    def test_lock_replacement_after_flock_fails_closed(self) -> None:
        import fcntl

        lock_path = self.config.lock_file
        original_flock = fcntl.flock
        replacement = secure_file(self.trust_root / "lock-replacement", "0")
        replacement.chmod(0o440)
        replaced = False
        entered = False

        def replace_after_flock(descriptor: int, operation: int) -> None:
            nonlocal replaced
            original_flock(descriptor, operation)
            if operation == fcntl.LOCK_EX and not replaced:
                os.replace(replacement, lock_path)
                replaced = True

        with mock.patch.object(fcntl, "flock", side_effect=replace_after_flock):
            with self.assertRaisesRegex(MODULE.ControllerError, "lease-lock-replaced"):
                with self.exclusive_lock():
                    entered = True

        self.assertTrue(replaced)
        self.assertFalse(entered)

    @unittest.skipUnless(sys.platform.startswith("linux"), "Linux exit identity contract")
    def test_lock_replacement_before_unlock_fails_closed(self) -> None:
        replacement = secure_file(self.trust_root / "exit-replacement", "0")
        replacement.chmod(0o440)
        with self.assertRaisesRegex(MODULE.ControllerError, "lease-lock-replaced"):
            with self.exclusive_lock():
                os.replace(replacement, self.config.lock_file)

    def test_missing_lock_fails_without_creation(self) -> None:
        self.config.lock_file.unlink()
        with self.assertRaisesRegex(MODULE.ControllerError, "insecure-lease-lock"):
            self.controller.require_fresh()
        self.assertFalse(self.config.lock_file.exists())

    @unittest.skipUnless(os.name == "posix", "POSIX lock metadata contract")
    def test_lock_rejects_symlink_hardlink_and_wrong_mode(self) -> None:
        lock_path = self.config.lock_file
        target = secure_file(self.trust_root / "lock-target", "0")
        target.chmod(0o440)

        lock_path.unlink()
        lock_path.symlink_to(target.name)
        with self.assertRaisesRegex(MODULE.ControllerError, "insecure-lease-lock"):
            with self.exclusive_lock():
                self.fail("symlink lock was acquired")

        lock_path.unlink()
        os.link(target, lock_path)
        with self.assertRaisesRegex(MODULE.ControllerError, "insecure-lease-lock"):
            with self.exclusive_lock():
                self.fail("hard-linked lock was acquired")

        lock_path.unlink()
        secure_file(lock_path, "0").chmod(0o400)
        with self.assertRaisesRegex(MODULE.ControllerError, "insecure-lease-lock"):
            with self.exclusive_lock():
                self.fail("wrong-mode lock was acquired")

    @unittest.skipUnless(
        os.name == "posix" and hasattr(os, "geteuid") and os.geteuid() == 0,
        "requires root to create a wrong-owner lock",
    )
    def test_lock_rejects_wrong_owner(self) -> None:
        os.chown(self.config.lock_file, 65534, -1)
        with self.assertRaisesRegex(MODULE.ControllerError, "insecure-lease-lock"):
            with self.exclusive_lock():
                self.fail("wrong-owner lock was acquired")

    @unittest.skipUnless(
        os.name == "posix" and hasattr(os, "geteuid") and os.geteuid() == 0,
        "requires root to create a wrong-group lock",
    )
    def test_lock_rejects_wrong_group(self) -> None:
        wrong_gid = 65534 if self.owner_gid != 65534 else 65533
        os.chown(self.config.lock_file, -1, wrong_gid)
        with self.assertRaisesRegex(MODULE.ControllerError, "insecure-lease-lock"):
            with self.exclusive_lock():
                self.fail("wrong-group lock was acquired")

    @unittest.skipUnless(os.name == "posix", "POSIX lock parent contract")
    def test_lock_requires_nonwritable_parent(self) -> None:
        self.trust_root.chmod(0o775)
        with self.assertRaisesRegex(
            MODULE.ControllerError, "insecure-lease-lock-parent"
        ):
            with self.exclusive_lock():
                self.fail("lock under a wrong-mode parent was acquired")

    def test_existing_lock_is_preserved_with_exact_metadata(self) -> None:
        before = self.config.lock_file.stat()
        self.arm()
        metadata = self.config.lock_file.stat()
        self.assertTrue(stat.S_ISREG(metadata.st_mode))
        self.assertEqual(1, metadata.st_nlink)
        self.assertEqual((before.st_dev, before.st_ino), (metadata.st_dev, metadata.st_ino))
        if os.name != "nt":
            self.assertEqual(self.config.lock_owner_uid, metadata.st_uid)
            self.assertEqual(self.owner_gid, metadata.st_gid)
            self.assertEqual(0o440, stat.S_IMODE(metadata.st_mode))

    def test_process_supervision_uses_ready_subreaper_launcher(self) -> None:
        source = SCRIPT.read_text(encoding="utf-8")
        for required in (
            "__child-supervisor",
            "PR_SET_CHILD_SUBREAPER",
            "PR_SET_PDEATHSIG",
            '"state": "ready"',
            "_reap_adopted_children()",
            "pidfd_send_signal",
            "launcher.wait_ready()",
        ):
            self.assertIn(required, source)
        self.assertNotIn("_start_process_group_reaper", source)
        self.assertNotIn("reaper_pid = os.fork()", source)

    def test_require_fresh_accepts_only_a_valid_active_lease(self) -> None:
        with self.assertRaisesRegex(MODULE.ControllerError, "no-active-lease"):
            self.controller.require_fresh()

        self.arm()
        evidence, exit_code = self.controller.require_fresh()
        self.assertEqual(0, exit_code)
        self.assertEqual("lease-active", evidence["reason_code"])

        self.make_stale()
        with self.assertRaisesRegex(MODULE.ControllerError, "lease-stale"):
            self.controller.require_fresh()

    def test_any_future_dated_heartbeat_fails_closed(self) -> None:
        self.arm()
        document = json.loads(self.config.lease_file.read_text(encoding="utf-8"))
        document["heartbeat_utc"] = MODULE.format_utc(self.current_time + dt.timedelta(seconds=1))
        self.config.lease_file.write_text(json.dumps(document), encoding="utf-8")
        if os.name != "nt":
            self.config.lease_file.chmod(0o600)
        with self.assertRaisesRegex(MODULE.ControllerError, "future-heartbeat"):
            self.controller.require_fresh()

    @unittest.skipIf(os.name == "nt", "POSIX ownership and mode contract")
    def test_pointer_bound_sealed_run_remains_fresh_and_can_disarm(self) -> None:
        self.arm(mutation=True)
        sealed = self._seal_active_run()
        evidence, exit_code = sealed.require_fresh()
        self.assertEqual(0, exit_code)
        self.assertEqual("lease-active", evidence["reason_code"])
        evidence, exit_code = sealed.reconcile(self.recovery)
        self.assertEqual(0, exit_code)
        self.assertEqual("lease-fresh", evidence["reason_code"])
        evidence, exit_code = sealed.heartbeat(
            self.run_id, self.run_attempt, "post-activation", True
        )
        self.assertEqual(0, exit_code)
        self.assertEqual("lease-renewed", evidence["reason_code"])
        evidence, exit_code = sealed.disarm(self.run_id, self.run_attempt)
        self.assertEqual(0, exit_code)
        self.assertEqual("lease-disarmed", evidence["reason_code"])
        self.assertFalse(self.config.lease_file.exists())

    @unittest.skipIf(os.name == "nt", "POSIX ownership and mode contract")
    def test_unbound_or_incomplete_sealed_run_is_rejected(self) -> None:
        self.arm(mutation=True)
        unbound = self._seal_active_run(bind_pointer=False)
        with self.assertRaisesRegex(MODULE.ControllerError, "sealed-run-pointer-mismatch"):
            unbound.require_fresh()

        self.lkg_pointer.write_text(str(self.run_directory) + "\n", encoding="utf-8")
        if os.name != "nt":
            self.lkg_pointer.chmod(0o644)
            (self.run_directory / "security-qualified").chmod(0o600)
        with self.assertRaisesRegex(MODULE.ControllerError, "invalid-sealed-run-file"):
            unbound.require_fresh()

    def test_require_fresh_rejects_invalid_lease_document(self) -> None:
        self.arm()
        document = json.loads(self.config.lease_file.read_text(encoding="utf-8"))
        document["run_directory"] = str(self.config.run_root / "attacker")
        self.config.lease_file.write_text(json.dumps(document) + "\n", encoding="utf-8")
        if os.name != "nt":
            self.config.lease_file.chmod(0o600)
        with self.assertRaisesRegex(MODULE.ControllerError, "lease-run-directory-mismatch"):
            self.controller.require_fresh()

    def test_fresh_lease_is_noop_and_mode_600(self) -> None:
        self.arm()
        evidence, exit_code = self.controller.reconcile(self.recovery)

        self.assertEqual(0, exit_code)
        self.assertEqual("lease-fresh", evidence["reason_code"])
        self.assertEqual(0, self.runner.count("restore"))
        self.assertTrue(self.config.lease_file.exists())
        if os.name != "nt":
            self.assertEqual(0o600, stat.S_IMODE(self.config.lease_file.stat().st_mode))
            self.assertEqual(0o600, stat.S_IMODE(self.config.evidence_file.stat().st_mode))

    def test_heartbeat_is_owner_bound_and_mutation_is_monotonic(self) -> None:
        self.arm()
        self.current_time += dt.timedelta(seconds=1)
        self.controller.heartbeat(self.run_id, self.run_attempt, "migration", True)
        self.current_time += dt.timedelta(seconds=1)
        self.controller.heartbeat(self.run_id, self.run_attempt, "canary", False)
        lease = self.controller._read_lease()
        assert lease
        self.assertTrue(lease.mutation_began)
        with self.assertRaisesRegex(MODULE.ControllerError, "lease-owner-mismatch"):
            self.controller.heartbeat("999", 1, "canary", False)

    def test_stale_pre_mutation_calls_fixed_abort_and_disarms(self) -> None:
        self.arm()
        self.make_stale()
        evidence, exit_code = self.controller.reconcile(self.recovery)

        self.assertEqual(0, exit_code)
        self.assertEqual("aborted-prepared", evidence["action"])
        self.assertTrue(evidence["gates"]["rotation_aborted"])
        self.assertFalse(self.config.lease_file.exists())
        self.assertEqual(1, self.runner.count("rotation"))
        command = next(command for command in self.runner.commands if self.runner._step(command) == "rotation")
        self.assertEqual(str(self.python), command[0])
        self.assertEqual(str(self.rotation_manager), command[1])
        self.assertNotIn("SECRET=not-emitted", " ".join(command))

    def test_stale_heartbeat_cannot_resurrect_failed_runner(self) -> None:
        self.arm()
        self.make_stale()
        with self.assertRaisesRegex(MODULE.ControllerError, "stale-lease-cannot-heartbeat"):
            self.controller.heartbeat(self.run_id, self.run_attempt, "canary", True)

    def test_stale_lease_cannot_be_rearmed_or_disarmed_around_recovery(self) -> None:
        self.arm()
        self.make_stale()
        with self.assertRaisesRegex(MODULE.ControllerError, "stale-lease-requires-reconcile"):
            self.controller.arm(self.run_id, self.run_attempt, "preflight", 60)
        with self.assertRaisesRegex(MODULE.ControllerError, "stale-lease-requires-reconcile"):
            self.controller.disarm(self.run_id, self.run_attempt)
        self.assertTrue(self.config.lease_file.exists())

    def test_disarm_rejects_a_missing_lease(self) -> None:
        with self.assertRaisesRegex(MODULE.ControllerError, "no-active-lease"):
            self.controller.disarm(self.run_id, self.run_attempt)

    @unittest.skipIf(os.name == "nt", "POSIX ownership and mode contract")
    def test_disarm_requires_pointer_bound_root_sealed_activation(self) -> None:
        self.arm(mutation=True)
        with self.assertRaisesRegex(MODULE.ControllerError, "lease-run-not-activated"):
            self.controller.disarm(self.run_id, self.run_attempt)
        self.assertEqual(str(self.lkg_directory) + "\n", self.lkg_pointer.read_text())
        self.assertTrue(self.config.lease_file.exists())

        sealed = self._seal_active_run(bind_pointer=True)
        evidence, exit_code = sealed.disarm(self.run_id, self.run_attempt)
        self.assertEqual(0, exit_code)
        self.assertEqual("lease-disarmed", evidence["reason_code"])
        self.assertFalse(self.config.lease_file.exists())

    def test_supervisor_completes_and_renews_the_owned_lease(self) -> None:
        self.arm(mutation=True)
        evidence, exit_code = self.controller.supervise(
            self.run_id,
            self.run_attempt,
            "test-operation",
            True,
            2,
            [sys.executable, "-c", "raise SystemExit(0)"],
            heartbeat_seconds=0.05,
        )
        self.assertEqual(0, exit_code)
        self.assertEqual("supervised-operation-completed", evidence["reason_code"])

    def test_supervisor_terminates_child_when_heartbeat_fails(self) -> None:
        self.arm(mutation=True)
        marker = self.root / "orphaned-child"
        original = self.controller.heartbeat
        calls = 0

        def fail_after_start(*args, **kwargs):
            nonlocal calls
            calls += 1
            if calls > 1:
                raise MODULE.ControllerError("injected-heartbeat-failure")
            return original(*args, **kwargs)

        self.controller.heartbeat = fail_after_start  # type: ignore[method-assign]
        code = (
            "import pathlib,time; time.sleep(1); "
            f"pathlib.Path({str(marker)!r}).write_text('orphaned')"
        )
        with self.assertRaisesRegex(MODULE.ControllerError, "injected-heartbeat-failure"):
            self.controller.supervise(
                self.run_id,
                self.run_attempt,
                "test-operation",
                True,
                2,
                [sys.executable, "-c", code],
                heartbeat_seconds=0.05,
            )
        time.sleep(1.1)
        self.assertFalse(marker.exists())

    def test_supervisor_enforces_operation_timeout(self) -> None:
        self.arm(mutation=True)
        with self.assertRaisesRegex(MODULE.ControllerError, "supervised-operation-timeout"):
            self.controller.supervise(
                self.run_id,
                self.run_attempt,
                "test-operation",
                True,
                0.05,
                [sys.executable, "-c", "import time; time.sleep(5)"],
                heartbeat_seconds=0.01,
            )

    def test_supervisor_preserves_nonzero_leader_exit_status(self) -> None:
        self.arm(mutation=True)
        evidence, exit_code = self.controller.supervise(
            self.run_id,
            self.run_attempt,
            "test-operation",
            True,
            2,
            [sys.executable, "-c", "raise SystemExit(7)"],
            heartbeat_seconds=0.05,
        )
        self.assertEqual(7, exit_code)
        self.assertEqual("supervised-operation-failed", evidence["reason_code"])

    @unittest.skipUnless(os.name == "posix", "POSIX process-group contract")
    def test_supervisor_drains_forked_descendant_after_leader_success(self) -> None:
        self.arm(mutation=True)
        marker = self.root / "forked-supervisor-descendant"
        code = (
            "import os,pathlib,time; pid=os.fork(); "
            f"(time.sleep(1), pathlib.Path({str(marker)!r}).write_text('alive'), os._exit(0)) "
            "if pid == 0 else os._exit(0)"
        )
        evidence, exit_code = self.controller.supervise(
            self.run_id,
            self.run_attempt,
            "test-operation",
            True,
            3,
            [sys.executable, "-c", code],
            heartbeat_seconds=0.05,
        )
        self.assertEqual(0, exit_code)
        self.assertEqual("supervised-operation-completed", evidence["reason_code"])
        time.sleep(1.1)
        self.assertFalse(marker.exists())

    @unittest.skipUnless(os.name == "posix", "POSIX process-group contract")
    def test_recovery_invoke_drains_forked_descendant_after_leader_exit(self) -> None:
        marker = self.root / "forked-recovery-descendant"
        real_controller = MODULE.DeploymentLeaseController(
            self.config, runner=MODULE.default_runner, clock=lambda: self.current_time
        )
        code = (
            "import os,pathlib,time; pid=os.fork(); "
            f"(time.sleep(1), pathlib.Path({str(marker)!r}).write_text('alive'), os._exit(0)) "
            "if pid == 0 else os._exit(0)"
        )
        result = real_controller._invoke(
            [sys.executable, "-c", code], 3, "forked-test"
        )
        self.assertEqual(0, result.returncode)
        time.sleep(1.1)
        self.assertFalse(marker.exists())

    @unittest.skipUnless(sys.platform.startswith("linux"), "Linux subreaper contract")
    def test_launcher_reaps_zombie_adopted_after_leader_exit(self) -> None:
        child_pid_file = self.root / "zombie-child.pid"
        real_controller = MODULE.DeploymentLeaseController(
            self.config, runner=MODULE.default_runner, clock=lambda: self.current_time
        )
        code = (
            "import os,pathlib,time; pid=os.fork(); "
            f"pathlib.Path({str(child_pid_file)!r}).write_text(str(pid)) if pid else None; "
            "os._exit(0) if pid == 0 else time.sleep(0.2)"
        )
        result = real_controller._invoke([sys.executable, "-c", code], 3, "zombie-test")
        self.assertEqual(0, result.returncode)
        child_pid = int(child_pid_file.read_text(encoding="utf-8"))
        self.assertFalse(Path(f"/proc/{child_pid}").exists())

    @unittest.skipUnless(sys.platform.startswith("linux"), "Linux parent-death contract")
    def test_parent_death_cleans_launcher_and_target_descendants(self) -> None:
        marker = self.root / "parent-death-descendant"
        launcher_pid_file = self.root / "launcher.pid"
        helper = (
            "import importlib.util,os,pathlib,sys; "
            "spec=importlib.util.spec_from_file_location('lease_parent_death',sys.argv[1]); "
            "module=importlib.util.module_from_spec(spec); sys.modules[spec.name]=module; spec.loader.exec_module(module); "
            "target=[sys.executable,'-c',"
            f"\"import pathlib,time;time.sleep(1);pathlib.Path({str(marker)!r}).write_text('alive')\"]; "
            "launcher=module._LauncherHandle(target,capture=False,inherit_output=False); "
            "launcher.wait_ready(); pathlib.Path(sys.argv[2]).write_text(str(launcher.process.pid)); os._exit(0)"
        )
        parent = subprocess.run(
            [sys.executable, "-c", helper, str(SCRIPT), str(launcher_pid_file)],
            check=False,
            timeout=5,
        )
        self.assertEqual(0, parent.returncode)
        launcher_pid = int(launcher_pid_file.read_text(encoding="utf-8"))
        deadline = time.monotonic() + 3
        launcher_proc = Path(f"/proc/{launcher_pid}")
        while launcher_proc.exists() and time.monotonic() < deadline:
            stat_fields = (launcher_proc / "stat").read_text(encoding="ascii").split()
            if len(stat_fields) > 2 and stat_fields[2] == "Z":
                break
            time.sleep(0.05)
        time.sleep(1.1)
        if launcher_proc.exists():
            stat_fields = (launcher_proc / "stat").read_text(encoding="ascii").split()
            self.assertGreater(len(stat_fields), 2)
            self.assertEqual("Z", stat_fields[2], "launcher remained live after parent death")
        self.assertFalse(marker.exists())


class RecoveryTests(LeaseFixture):
    def test_no_lkg_without_lease_stops_unowned_hub(self) -> None:
        self.lkg_pointer.unlink()
        self.runner.stop_running = True

        evidence, exit_code = self.controller.reconcile_no_lkg(
            force=False,
            env_file=self.env_file,
            rotation_state_file=self.rotation_state,
            python_executable=self.python,
            docker_executable=self.docker,
            hub_container_name="xframework-bolt-hub",
            stop_timeout_seconds=10,
        )

        self.assertEqual(0, exit_code)
        self.assertEqual("no-active-lease-no-lkg-hub-stopped", evidence["reason_code"])
        self.assertTrue(evidence["gates"]["hub_stopped"])
        self.assertEqual(1, self.runner.count("stop"))

    def test_no_lkg_force_recovery_aborts_prepared_state_before_disarm(self) -> None:
        self.arm()
        self.make_stale()
        self.lkg_pointer.unlink()
        self.rotation_manager = secure_file(
            self.run_directory / "manage-bolt-phase0-rotation.py",
            executable=True,
        )

        evidence, exit_code = self.controller.reconcile_no_lkg(
            force=True,
            env_file=self.env_file,
            rotation_state_file=self.rotation_state,
            python_executable=self.python,
            docker_executable=self.docker,
            hub_container_name="xframework-bolt-hub",
            stop_timeout_seconds=10,
        )

        self.assertEqual(0, exit_code)
        self.assertEqual("no-lkg-pre-mutation-aborted", evidence["reason_code"])
        self.assertTrue(evidence["gates"]["hub_stopped"])
        self.assertTrue(evidence["gates"]["rotation_aborted"])
        self.assertEqual(1, self.runner.count("rotation"))
        self.assertFalse(self.config.lease_file.exists())
    def test_absent_leased_run_crash_window_restores_prior_lkg(self) -> None:
        self.arm(mutation=True)
        self.run_directory.rmdir()
        self.make_stale()

        evidence, exit_code = self.controller.reconcile(self.recovery)

        self.assertEqual(0, exit_code)
        self.assertEqual("security-qualified-lkg-restored", evidence["reason_code"])
        self.assertEqual(1, self.runner.count("restore"))
        self.assertFalse(self.config.lease_file.exists())

    @unittest.skipIf(os.name == "nt", "POSIX ownership and mode contract")
    def test_sealed_unbound_run_crash_window_restores_prior_lkg(self) -> None:
        self.arm(mutation=True)
        sealed = self._seal_active_run(bind_pointer=False)
        self.make_stale()

        evidence, exit_code = sealed.reconcile(self.recovery)

        self.assertEqual(0, exit_code)
        self.assertEqual("security-qualified-lkg-restored", evidence["reason_code"])
        self.assertEqual(str(self.lkg_directory) + "\n", self.lkg_pointer.read_text())
        self.assertEqual(1, self.runner.count("restore"))
        self.assertFalse(self.config.lease_file.exists())

    def test_force_recovery_bypasses_fresh_lease_noop(self) -> None:
        self.arm(mutation=True)

        evidence, exit_code = self.controller.force_recovery(self.recovery)

        self.assertEqual(0, exit_code)
        self.assertEqual("force-restored", evidence["action"])
        self.assertEqual(1, self.runner.count("restore"))
        self.assertEqual(1, self.runner.count("runtime"))
        self.assertEqual(1, self.runner.count("recovery"))
        self.assertFalse(self.config.lease_file.exists())

    def test_force_recovery_without_lease_still_restores_lkg(self) -> None:
        evidence, exit_code = self.controller.force_recovery(self.recovery)

        self.assertEqual(0, exit_code)
        self.assertEqual("force-restored", evidence["action"])
        self.assertIsNone(evidence["lease"])
        self.assertEqual(1, self.runner.count("restore"))

    def test_force_recovery_failure_stops_hub_immediately(self) -> None:
        self.arm(mutation=True)
        self.runner.fail_step = "restore"

        evidence, exit_code = self.controller.force_recovery(self.recovery)

        self.assertEqual(1, exit_code)
        self.assertEqual("hub-stopped", evidence["action"])
        self.assert_hub_stop_invoked()

    def test_successful_restore_requires_both_gates_and_disarms(self) -> None:
        self.arm(mutation=True)
        self.make_stale()
        evidence, exit_code = self.controller.reconcile(self.recovery)

        self.assertEqual(0, exit_code)
        self.assertEqual("restored", evidence["action"])
        self.assertEqual(
            {
                "rotation_aborted": False,
                "restore_applied": True,
                "runtime_verified": True,
                "recovery_gate_verified": True,
                "hub_stopped": False,
            },
            evidence["gates"],
        )
        self.assertFalse(self.config.lease_file.exists())
        self.assertEqual(1, self.runner.count("restore"))
        self.assertEqual(1, self.runner.count("runtime"))
        self.assertEqual(1, self.runner.count("recovery"))
        restore = next(command for command in self.runner.commands if self.runner._step(command) == "restore")
        self.assertIn(str(self.lkg_directory / "docker-compose.yml"), restore)
        self.assertIn(str(self.lkg_directory / "pinned-compose.override.json"), restore)
        self.assertNotIn("migrate", restore)
        for service in MODULE.RESTORE_SERVICES:
            self.assertIn(service, restore)

    def test_missing_or_unqualified_lkg_stops_hub(self) -> None:
        cases = ("missing", "outside", "marker")
        for case in cases:
            with self.subTest(case=case):
                self.setUp()
                self.arm(mutation=True)
                self.make_stale()
                if case == "missing":
                    self.lkg_pointer.unlink()
                elif case == "outside":
                    self.lkg_pointer.write_text(str(self.root) + "\n", encoding="utf-8")
                else:
                    (self.lkg_directory / "security-qualified").unlink()

                evidence, exit_code = self.controller.reconcile(self.recovery)

                self.assertEqual(1, exit_code)
                self.assertEqual("hub-stopped", evidence["action"])
                self.assert_hub_stop_invoked()
                self.assertFalse(self.config.lease_file.exists())

    def test_restore_failure_stops_hub(self) -> None:
        self.arm(mutation=True)
        self.make_stale()
        self.runner.fail_step = "restore"
        evidence, exit_code = self.controller.reconcile(self.recovery)

        self.assertEqual(1, exit_code)
        self.assertEqual("restore-failed", evidence["reason_code"])
        self.assert_hub_stop_invoked()
        self.assertEqual(0, self.runner.count("runtime"))

    def test_runtime_and_recovery_gate_failures_stop_hub(self) -> None:
        for failed_step in ("runtime", "runtime-output", "recovery", "recovery-output"):
            with self.subTest(failed_step=failed_step):
                self.setUp()
                self.arm(mutation=True)
                self.make_stale()
                self.runner.fail_step = failed_step

                evidence, exit_code = self.controller.reconcile(self.recovery)

                self.assertEqual(1, exit_code)
                self.assertEqual("hub-stopped", evidence["action"])
                self.assert_hub_stop_invoked()

    def test_timeout_stops_hub_and_child_output_is_not_evidence(self) -> None:
        self.arm(mutation=True)
        self.make_stale()
        self.runner.timeout_step = "restore"
        evidence, exit_code = self.controller.reconcile(self.recovery)

        self.assertEqual(1, exit_code)
        self.assertEqual("restore-timeout", evidence["reason_code"])
        self.assert_hub_stop_invoked()
        serialized = json.dumps(self.read_evidence())
        self.assertNotIn("SECRET=not-emitted", serialized)
        self.assertNotIn("suppressed-secret", serialized)
        self.assertNotIn(str(self.env_file), serialized)

    def test_stop_timeout_escalates_to_kill_and_verifies_shutdown(self) -> None:
        self.arm(mutation=True)
        self.make_stale()
        self.runner.fail_step = "restore"
        self.runner.timeout_step = "stop"
        self.runner.stop_running = True

        evidence, exit_code = self.controller.reconcile(self.recovery)

        self.assertEqual(1, exit_code)
        self.assertEqual("hub-stopped", evidence["action"])
        self.assertEqual(1, self.runner.count("kill"))
        self.assertFalse(self.config.lease_file.exists())

    def test_abort_failure_also_fails_closed(self) -> None:
        self.arm()
        self.make_stale()
        self.runner.fail_step = "rotation"
        evidence, exit_code = self.controller.reconcile(self.recovery)

        self.assertEqual(1, exit_code)
        self.assertEqual("rotation-abort-failed", evidence["reason_code"])
        self.assert_hub_stop_invoked()

    def test_unverified_stop_keeps_stale_lease_for_retry(self) -> None:
        self.arm(mutation=True)
        self.make_stale()
        self.runner.fail_step = "restore"
        self.runner.stop_failure = True
        self.runner.stop_running = True
        self.runner.fail_step = "restore"

        # Force stop and kill verification to remain running.
        original = self.runner

        def never_stops(command: list[str], timeout: int, capture: bool):
            result = original(command, timeout, capture)
            if original._step(command) == "inspect":
                return subprocess.CompletedProcess(command, 0, "true\n", "")
            if original._step(command) == "kill":
                return subprocess.CompletedProcess(command, 1, "", "")
            return result

        self.controller.runner = never_stops
        evidence, exit_code = self.controller.reconcile(self.recovery)

        self.assertEqual(1, exit_code)
        self.assertEqual("hub-stop-unverified", evidence["action"])
        self.assertTrue(self.config.lease_file.exists())

    def test_concurrent_reconcile_executes_restore_only_once(self) -> None:
        self.arm(mutation=True)
        self.make_stale()
        self.runner.delay_restore = 0.05
        results: list[tuple[dict[str, Any], int]] = []
        errors: list[BaseException] = []

        def invoke() -> None:
            try:
                results.append(self.controller.reconcile(self.recovery))
            except BaseException as error:
                errors.append(error)

        threads = [threading.Thread(target=invoke), threading.Thread(target=invoke)]
        for thread in threads:
            thread.start()
        for thread in threads:
            thread.join(timeout=5)

        self.assertEqual([], errors)
        self.assertEqual(2, len(results))
        self.assertEqual(1, self.runner.count("restore"))
        self.assertEqual({"restored", "noop"}, {result[0]["action"] for result in results})
        self.assertEqual("restored", self.read_evidence()["action"])


class InputHardeningTests(LeaseFixture):
    @unittest.skipIf(os.name == "nt", "POSIX root ownership enforcement")
    def test_deployment_owned_file_is_not_root_sealed(self) -> None:
        helper = secure_file(self.root / "deployment-owned-helper", executable=True)
        with self.assertRaisesRegex(MODULE.ControllerError, "unsealed-lkg-file"):
            MODULE.validate_root_sealed_file(helper, expected_mode=0o550)

    def test_duplicate_nan_and_control_json_are_rejected(self) -> None:
        inputs = (
            b'{"schema":"a","schema":"b"}',
            b'{"value":NaN}',
            b'{"value":"escaped\\u000aattack"}',
        )
        expected = ("duplicate-json-key", "invalid-json-number", "invalid-control-character")
        for raw, code in zip(inputs, expected, strict=True):
            with self.subTest(code=code):
                with self.assertRaisesRegex(MODULE.ControllerError, code):
                    MODULE.decode_json(raw)

    def test_tampered_lease_run_directory_traversal_is_rejected(self) -> None:
        self.arm(mutation=True)
        document = json.loads(self.config.lease_file.read_text(encoding="utf-8"))
        document["run_directory"] = str(self.run_root / ".." / "attacker")
        MODULE.atomic_write_json(
            self.config.lease_file,
            document,
            self.config.deployment_uid,
        )
        self.make_stale()

        with self.assertRaisesRegex(MODULE.ControllerError, "lease-run-directory-mismatch"):
            self.controller.reconcile(self.recovery)
        self.assertEqual([], self.runner.commands)

    def test_symlink_pointer_and_helper_are_rejected_without_execution(self) -> None:
        if os.name == "nt":
            self.skipTest("symlink creation requires platform privileges")
        self.arm(mutation=True)
        self.make_stale()
        alias = self.lkg_parent / "alias"
        alias.symlink_to(self.lkg_pointer)
        attacked = dataclasses_replace(self.recovery, lkg_pointer=alias)
        evidence, exit_code = self.controller.reconcile(attacked)
        self.assertEqual(1, exit_code)
        self.assertEqual("hub-stopped", evidence["action"])

        self.setUp()
        helper_alias = self.root / "runtime-alias.py"
        helper_alias.symlink_to(self.runtime_verifier)
        attacked = dataclasses_replace(self.recovery, runtime_verifier=helper_alias)
        with self.assertRaisesRegex(MODULE.ControllerError, "symlink-rejected"):
            self.controller.reconcile(attacked)
        self.assertEqual([], self.runner.commands)

    def test_service_inventory_cannot_be_reduced_or_reordered(self) -> None:
        for services in (MODULE.PHASE0_SERVICES[:-1], tuple(reversed(MODULE.PHASE0_SERVICES))):
            with self.subTest(services=services):
                attacked = dataclasses_replace(self.recovery, services=services)
                with self.assertRaisesRegex(MODULE.ControllerError, "invalid-service-inventory"):
                    self.controller.reconcile(attacked)

    def test_lkg_artifact_change_between_resolution_and_apply_is_rejected(self) -> None:
        artifacts = self.controller._resolve_lkg(self.recovery)
        (self.lkg_directory / "docker-compose.yml").write_text(
            "services: {attacker: {}}\n", encoding="utf-8"
        )
        with self.assertRaisesRegex(MODULE.ControllerError, "lkg-artifact-changed"):
            self.controller._restore(self.recovery, artifacts)
        self.assertEqual(0, self.runner.count("restore"))


def dataclasses_replace(instance: Any, **changes: Any) -> Any:
    return MODULE.dataclasses.replace(instance, **changes)


if __name__ == "__main__":
    unittest.main()
