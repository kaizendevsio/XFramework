#!/usr/bin/env python3
from __future__ import annotations

import contextlib
import hashlib
import importlib.util
import io
import json
import os
import select
import stat
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

try:
    import pwd
except ImportError:  # pragma: no cover - Windows
    pwd = None  # type: ignore[assignment]


SCRIPT = Path(__file__).with_name("manage-bolt-phase0-root.py")
SPEC = importlib.util.spec_from_file_location("phase0_root_boundary", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)

LEASE_SCRIPT = Path(__file__).with_name("manage-bolt-phase0-deployment-lease.py")
LEASE_SPEC = importlib.util.spec_from_file_location("phase0_lease_for_root_test", LEASE_SCRIPT)
assert LEASE_SPEC and LEASE_SPEC.loader
LEASE_MODULE = importlib.util.module_from_spec(LEASE_SPEC)
sys.modules[LEASE_SPEC.name] = LEASE_MODULE
LEASE_SPEC.loader.exec_module(LEASE_MODULE)


def directory(path: Path, mode: int) -> Path:
    path.mkdir(parents=True, exist_ok=True)
    if os.name == "posix":
        path.chmod(mode)
    return path


def file(path: Path, mode: int, content: bytes = b"fixed\n") -> Path:
    path.write_bytes(content)
    if os.name == "posix":
        path.chmod(mode)
    return path


class FakeRunner:
    def __init__(self) -> None:
        self.hub_running = False
        self.hub_absent = False
        self.inspect_failure_with_present_container = False
        self.inspect_output: str | None = None
        self.container_list_output: str | None = None
        self.docker_unavailable = False
        self.timer_active = True
        self.commands: list[list[str]] = []
        self.overrides: dict[tuple[str, str], str] = {}

    def __call__(self, command: list[str], capture: bool) -> subprocess.CompletedProcess[str]:
        self.commands.append(command.copy())
        if len(command) > 1 and command[1] == "show":
            unit = command[2]
            prop = command[3].removeprefix("--property=")
            service = "xframework-bolt-phase0-watchdog.service"
            timer = "xframework-bolt-phase0-watchdog.timer"
            values = {
                (service, "DropInPaths"): "",
                (timer, "DropInPaths"): "",
                (service, "FragmentPath"): self.service_fragment,
                (timer, "FragmentPath"): self.timer_fragment,
                (timer, "Unit"): service,
                (service, "ExecCondition"): "",
                (service, "ExecStartPre"): "",
                (service, "ExecStartPost"): "",
                (service, "User"): "github-runner",
                (service, "Group"): "github-runner",
                (service, "SupplementaryGroups"): "docker",
                (service, "NoNewPrivileges"): "yes",
                (service, "PrivateTmp"): "yes",
                (service, "ProtectSystem"): "strict",
                (service, "ProtectHome"): "read-only",
                (service, "LockPersonality"): "yes",
                (service, "MemoryDenyWriteExecute"): "yes",
                (service, "ProtectKernelTunables"): "yes",
                (service, "ProtectKernelModules"): "yes",
                (service, "ProtectControlGroups"): "yes",
                (service, "RestrictSUIDSGID"): "yes",
                (service, "UMask"): "0077",
                (service, "ReadWritePaths"): f"{self.deploy_root} {Path(self.protected_env).parent}",
                (service, "RestrictAddressFamilies"): "AF_UNIX AF_INET AF_INET6",
                (service, "TimeoutStartUSec"): "1h 10min",
                (timer, "AccuracyUSec"): "1s",
                (timer, "Persistent"): "yes",
                (timer, "UnitFileState"): "enabled",
                (timer, "ActiveState"): "active" if self.timer_active else "inactive",
                (timer, "TimersMonotonic"): "OnBootUSec=30s OnUnitActiveUSec=30s",
                (
                    service,
                    "ExecStart",
                ): f"{{ path={self.watchdog} ; argv[]={self.watchdog} ; ignore_errors=no ; }}",
            }
            value = self.overrides.get((unit, prop), values.get((unit, prop), ""))
            return subprocess.CompletedProcess(command, 0, value + "\n", "")
        if len(command) > 1 and command[1] == "inspect":
            if (
                self.docker_unavailable
                or self.hub_absent
                or self.inspect_failure_with_present_container
            ):
                return subprocess.CompletedProcess(command, 1, "", "unavailable")
            if self.inspect_output is not None:
                return subprocess.CompletedProcess(command, 0, self.inspect_output + "\n", "")
            return subprocess.CompletedProcess(
                command, 0, ("true\n" if self.hub_running else "false\n"), ""
            )
        if len(command) > 2 and command[1:3] == ["container", "ls"]:
            if self.docker_unavailable:
                return subprocess.CompletedProcess(command, 1, "", "unavailable")
            if self.container_list_output is not None:
                return subprocess.CompletedProcess(command, 0, self.container_list_output, "")
            present = self.inspect_failure_with_present_container or not self.hub_absent
            return subprocess.CompletedProcess(
                command, 0, "xframework-bolt-hub\n" if present else "", ""
            )
        if len(command) > 1 and command[1] == "info":
            return subprocess.CompletedProcess(
                command,
                1 if self.docker_unavailable else 0,
                "" if self.docker_unavailable else "27.5.1\n",
                "",
            )
        if len(command) > 1 and command[1] in {"stop", "kill"}:
            if command[-1] == "xframework-bolt-hub":
                self.hub_running = False
            elif command[-1] == "xframework-bolt-phase0-watchdog.timer":
                self.timer_active = False
        if len(command) > 1 and command[1] in {"start", "enable"} and command[-1] == "xframework-bolt-phase0-watchdog.timer":
            self.timer_active = True
        return subprocess.CompletedProcess(command, 0, "" if capture else "", "")


class RootBoundaryTests(unittest.TestCase):
    def setUp(self) -> None:
        temporary_parent = None
        if os.name == "posix":
            temporary_parent = Path("/var/lib") if os.geteuid() == 0 else Path.home()
        self.temporary = tempfile.TemporaryDirectory(
            dir=temporary_parent
        )
        self.addCleanup(self.temporary.cleanup)
        root = Path(self.temporary.name).resolve()
        if os.name == "posix":
            root.chmod(0o755)
        deploy = directory(root / "deploy", 0o755)
        runs = directory(deploy / "runs", 0o755)
        quarantine = directory(deploy / "quarantine", 0o700)
        lkg = directory(deploy / "lkg", 0o755)
        state = directory(deploy / "state", 0o700)
        hooks = directory(deploy / "hooks", 0o700)
        protected = directory(root / "protected", 0o1770)
        protected_env = file(
            protected / "xeon-dev.env",
            0o600,
            (
                b"BOLT_TEST=1\n"
                b"BOLT_SYNTHETIC_PROXY_MODE=direct-kestrel\n"
            ),
        )
        fixed = directory(root / "fixed", 0o755)
        trust = directory(fixed / "xframework-bolt-phase0", 0o755)
        lease_lock = file(trust / "deployment-lease.lock", 0o440, b"0")
        helper = file(fixed / "root-helper", 0o555)
        watchdog = file(fixed / "watchdog", 0o555)
        lease_manager = file(fixed / "lease-manager.py", 0o555)
        qualifier = file(fixed / "qualifier.py", 0o444)
        service = file(fixed / "watchdog.service", 0o644)
        timer = file(fixed / "watchdog.timer", 0o644)
        python = file(fixed / "python.exe", 0o755, b"test executable\n")
        docker = file(fixed / "docker", 0o755)
        systemctl = file(fixed / "systemctl", 0o755)
        self.paths = MODULE.RootPaths(
            deploy_root=deploy,
            run_root=runs,
            quarantine_root=quarantine,
            lkg_root=lkg,
            state_root=state,
            hooks_root=hooks,
            pointer=lkg / "current",
            root_helper=helper,
            watchdog=watchdog,
            lease_manager=lease_manager,
            lease_lock=lease_lock,
            qualifier=qualifier,
            service_fragment=service,
            timer_fragment=timer,
            python_link=python,
            docker=docker,
            systemctl=systemctl,
            protected_env=protected_env,
        )
        self.runner = FakeRunner()
        self.runner.service_fragment = str(service)
        self.runner.timer_fragment = str(timer)
        self.runner.deploy_root = str(deploy)
        self.runner.protected_env = str(self.paths.protected_env)
        self.runner.watchdog = str(watchdog)
        self.boundary = MODULE.RootBoundary(
            self.paths, runner=self.runner, enforce_root=False
        )

    def lease_config(self, **changes: object) -> LEASE_MODULE.ControllerConfig:
        source_components = tuple(
            (field, getattr(self.paths, path_attribute), mode)
            for field, (path_attribute, mode) in MODULE.SOURCE_BINDING_FIELDS.items()
        )
        values = {
            "state_root": self.paths.state_root,
            "run_root": self.paths.run_root,
            "project_name": "xframework",
            "deployment_uid": self.boundary.deployment_uid,
            "lock_file": self.paths.lease_lock,
            "lock_owner_uid": self.boundary.deployment_uid,
            "lock_owner_gid": self.boundary.deployment_gid,
            "lock_parent_uid": self.boundary.deployment_uid,
            "source_binding_owner_uid": self.boundary.deployment_uid,
            "source_binding_owner_gid": self.boundary.deployment_gid,
            "source_binding_component_owner_uid": self.boundary.deployment_uid,
            "source_binding_component_owner_gid": self.boundary.deployment_gid,
            "source_binding_components": source_components,
            "require_source_binding_on_arm": True,
        }
        values.update(changes)
        return LEASE_MODULE.ControllerConfig(**values)  # type: ignore[arg-type]

    def source_binding_request(self) -> dict[str, str]:
        return {
            field: "sha256:"
            + hashlib.sha256(
                getattr(self.paths, path_attribute).read_bytes()
            ).hexdigest()
            for field, (path_attribute, _) in MODULE.SOURCE_BINDING_FIELDS.items()
        }

    def arm_activation_lease(
        self,
        candidate: Path,
    ) -> LEASE_MODULE.DeploymentLeaseController:
        marker = {
            "schema": LEASE_MODULE.SOURCE_BINDING_SCHEMA,
            "run_id": "123",
            "run_attempt": 1,
            "source_binding": self.source_binding_request(),
        }
        self.boundary._write_marker(
            candidate / LEASE_MODULE.SOURCE_BINDING_MARKER,
            (
                json.dumps(marker, sort_keys=True, separators=(",", ":")) + "\n"
            ).encode("ascii"),
            owner_uid=self.boundary.deployment_uid,
            owner_gid=self.boundary.deployment_gid,
            mode=0o600 if os.name == "nt" else 0o440,
        )
        lease = LEASE_MODULE.DeploymentLeaseController(self.lease_config())
        lease.arm("123", 1, "preflight", 600, True)
        lease.heartbeat("123", 1, "activation", True)
        return lease

    @unittest.skipUnless(sys.platform.startswith("linux"), "Linux flock identity contract")
    def test_lease_lock_replacement_after_flock_fails_closed(self) -> None:
        import fcntl

        lock_path = self.paths.lease_lock
        replacement = file(
            lock_path.parent / ".deployment-lease.replacement",
            0o440,
            b"replacement",
        )
        original_flock = fcntl.flock
        replaced = False
        entered = False

        def replace_after_flock(descriptor: int, operation: int) -> None:
            nonlocal replaced
            original_flock(descriptor, operation)
            if operation == fcntl.LOCK_EX and not replaced:
                os.replace(replacement, lock_path)
                replaced = True

        with mock.patch.object(fcntl, "flock", side_effect=replace_after_flock):
            with self.assertRaisesRegex(MODULE.RootBoundaryError, "lease-lock-replaced"):
                with self.boundary._lease_lock():
                    entered = True

        self.assertTrue(replaced)
        self.assertFalse(entered)

    @unittest.skipUnless(sys.platform.startswith("linux"), "Linux exit identity contract")
    def test_lease_lock_replacement_before_unlock_fails_closed(self) -> None:
        replacement = file(
            self.paths.lease_lock.parent / ".deployment-lease.exit-replacement",
            0o440,
            b"replacement",
        )
        with self.assertRaisesRegex(MODULE.RootBoundaryError, "lease-lock-replaced"):
            with self.boundary._lease_lock():
                os.replace(replacement, self.paths.lease_lock)

    def test_missing_lease_lock_fails_without_creation(self) -> None:
        self.paths.lease_lock.unlink()
        with self.assertRaisesRegex(MODULE.RootBoundaryError, "insecure-lease-lock"):
            with self.boundary._lease_lock():
                self.fail("missing lock was acquired")
        self.assertFalse(self.paths.lease_lock.exists())

    @unittest.skipUnless(os.name == "posix", "POSIX lock metadata contract")
    def test_lease_lock_rejects_symlink_hardlink_and_wrong_mode(self) -> None:
        lock_path = self.paths.lease_lock
        target = file(lock_path.parent / "lock-target", 0o440, b"target")

        lock_path.unlink()
        lock_path.symlink_to(target.name)
        with self.assertRaisesRegex(MODULE.RootBoundaryError, "insecure-lease-lock"):
            with self.boundary._lease_lock():
                self.fail("symlink lock was acquired")

        lock_path.unlink()
        os.link(target, lock_path)
        with self.assertRaisesRegex(MODULE.RootBoundaryError, "insecure-lease-lock"):
            with self.boundary._lease_lock():
                self.fail("hard-linked lock was acquired")

        lock_path.unlink()
        file(lock_path, 0o400, b"wrong-mode")
        with self.assertRaisesRegex(MODULE.RootBoundaryError, "insecure-lease-lock"):
            with self.boundary._lease_lock():
                self.fail("wrong-mode lock was acquired")

    @unittest.skipUnless(
        os.name == "posix" and hasattr(os, "geteuid") and os.geteuid() == 0,
        "requires root to create a wrong-owner lock",
    )
    def test_lease_lock_rejects_wrong_owner(self) -> None:
        os.chown(self.paths.lease_lock, 65534, -1)
        with self.assertRaisesRegex(MODULE.RootBoundaryError, "insecure-lease-lock"):
            with self.boundary._lease_lock():
                self.fail("wrong-owner lock was acquired")

    @unittest.skipUnless(
        os.name == "posix" and hasattr(os, "geteuid") and os.geteuid() == 0,
        "requires root to create a wrong-group lock",
    )
    def test_lease_lock_rejects_wrong_group(self) -> None:
        wrong_gid = 65534 if self.boundary.deployment_gid != 65534 else 65533
        os.chown(self.paths.lease_lock, -1, wrong_gid)
        with self.assertRaisesRegex(MODULE.RootBoundaryError, "insecure-lease-lock"):
            with self.boundary._lease_lock():
                self.fail("wrong-group lock was acquired")

    @unittest.skipUnless(os.name == "posix", "POSIX lock parent contract")
    def test_lease_lock_requires_exact_parent_mode(self) -> None:
        self.paths.lease_lock.parent.chmod(0o775)
        with self.assertRaisesRegex(
            MODULE.RootBoundaryError, "insecure-lease-lock-parent"
        ):
            with self.boundary._lease_lock():
                self.fail("lock under a wrong-mode parent was acquired")

    @unittest.skipUnless(
        sys.platform.startswith("linux")
        and hasattr(os, "geteuid")
        and os.geteuid() == 0,
        "requires Linux root and a deployment identity",
    )
    def test_root_and_deployment_managers_share_root_owned_readonly_inode(self) -> None:
        assert pwd is not None
        deployment_uid = int(os.environ.get("SUDO_UID", pwd.getpwnam("nobody").pw_uid))
        deployment_gid = int(os.environ.get("SUDO_GID", pwd.getpwuid(deployment_uid).pw_gid))
        with tempfile.TemporaryDirectory(prefix="phase0-lock-", dir="/var/lib") as temporary:
            root = Path(temporary)
            root.chmod(0o755)
            trust = directory(root / "trust", 0o755)
            attacker = directory(root / "attacker", 0o700)
            lock_path = file(trust / "deployment-lease.lock", 0o440, b"0")
            replacement = file(attacker / "replacement", 0o440, b"0")
            os.chown(lock_path, 0, deployment_gid)
            os.chown(attacker, deployment_uid, deployment_gid)
            os.chown(replacement, deployment_uid, deployment_gid)
            expected = lock_path.stat()
            read_fd, write_fd = os.pipe()
            reader = os.fdopen(read_fd, "r", encoding="ascii")
            try:
                with MODULE.exclusive_lease_lock(
                    lock_path,
                    owner_uid=0,
                    owner_gid=deployment_gid,
                    trusted_parent_uid=0,
                ):
                    child = os.fork()
                    if child == 0:
                        reader.close()
                        try:
                            os.setgroups([deployment_gid])
                            os.setgid(deployment_gid)
                            os.setuid(deployment_uid)
                            try:
                                os.replace(replacement, lock_path)
                            except PermissionError:
                                os.write(write_fd, b"replacement-denied\n")
                            else:
                                os.write(write_fd, b"replacement-succeeded\n")
                                os._exit(2)
                            with LEASE_MODULE.exclusive_lock(
                                lock_path,
                                owner_uid=0,
                                owner_gid=deployment_gid,
                                trusted_parent_uid=0,
                            ):
                                acquired = lock_path.stat()
                                os.write(
                                    write_fd,
                                    f"acquired:{acquired.st_dev}:{acquired.st_ino}\n".encode(),
                                )
                            os._exit(0)
                        except BaseException:
                            os._exit(3)

                    os.close(write_fd)
                    self.assertEqual("replacement-denied\n", reader.readline())
                    self.assertEqual([], select.select([reader], [], [], 0.2)[0])
                self.assertNotEqual([], select.select([reader], [], [], 3.0)[0])
                acquired_line = reader.readline()
            finally:
                reader.close()

            status_pid, status = os.waitpid(child, 0)
            self.assertEqual(child, status_pid)
            self.assertEqual(0, os.waitstatus_to_exitcode(status))
            current = lock_path.stat()
            self.assertEqual((expected.st_dev, expected.st_ino), (current.st_dev, current.st_ino))
            self.assertEqual(
                f"acquired:{expected.st_dev}:{expected.st_ino}\n", acquired_line
            )

    def test_no_lkg_bootstrap_requires_verified_stopped_hub(self) -> None:
        evidence = self.boundary.verify_bootstrap()
        self.assertEqual("bootstrap-no-lkg-hub-stopped", evidence["state"])
        self.runner.hub_running = True
        with self.assertRaisesRegex(MODULE.RootBoundaryError, "bootstrap-hub-running"):
            self.boundary.verify_bootstrap()

    def test_source_binding_requires_every_exact_installed_bootstrap_component(self) -> None:
        request = self.source_binding_request()
        evidence = self.boundary.verify_source_binding(request)
        self.assertEqual("passed", evidence["source_binding"])

        for field, (path_attribute, mode) in MODULE.SOURCE_BINDING_FIELDS.items():
            with self.subTest(field=field):
                path = getattr(self.paths, path_attribute)
                original = path.read_bytes()
                if os.name == "posix":
                    path.chmod(mode | 0o200)
                path.write_bytes(original + b"stale\n")
                if os.name == "posix":
                    path.chmod(mode)
                with self.assertRaisesRegex(
                    MODULE.RootBoundaryError, "source-binding-mismatch"
                ):
                    self.boundary.verify_source_binding(request)
                if os.name == "posix":
                    path.chmod(mode | 0o200)
                path.write_bytes(original)
                if os.name == "posix":
                    path.chmod(mode)

    def test_source_binding_rejects_malformed_digest(self) -> None:
        request = self.source_binding_request()
        request["lease_manager_sha256"] = "sha256:not-a-digest"
        with self.assertRaisesRegex(MODULE.RootBoundaryError, "invalid-source-binding"):
            self.boundary.verify_source_binding(request)

    @unittest.skipUnless(os.name == "posix", "requires POSIX replace-open semantics")
    def test_file_read_rejects_path_replacement_after_open(self) -> None:
        path = self.paths.lease_manager
        replacement = file(
            path.parent / "replacement-manager.py",
            0o555,
            b"replacement\n",
        )
        original_read = MODULE.os.read
        replaced = False

        def replace_after_first_read(descriptor: int, amount: int) -> bytes:
            nonlocal replaced
            chunk = original_read(descriptor, amount)
            if chunk and not replaced:
                os.replace(replacement, path)
                replaced = True
            return chunk

        with (
            mock.patch.object(MODULE.os, "read", side_effect=replace_after_first_read),
            self.assertRaisesRegex(MODULE.RootBoundaryError, "file-changed"),
        ):
            MODULE._file(path, self.boundary.deployment_uid, 0o555)
        self.assertTrue(replaced)

    def test_source_binding_request_requires_exact_fields(self) -> None:
        request = {
            **self.source_binding_request(),
            "run_attempt": "1",
            "run_id": "29244847846",
        }
        fields = {"run_id", "run_attempt", *MODULE.SOURCE_BINDING_FIELDS}
        raw = json.dumps(
            request,
            ensure_ascii=True,
            separators=(",", ":"),
            sort_keys=True,
        ).encode("ascii") + b"\n"
        self.assertEqual(
            request,
            MODULE.decode_root_request(raw, fields),
        )

        missing = dict(request)
        missing.pop("lease_manager_sha256")
        extra = {**request, "unexpected_sha256": "sha256:" + "0" * 64}
        for candidate in (missing, extra):
            encoded = json.dumps(
                candidate,
                ensure_ascii=True,
                separators=(",", ":"),
                sort_keys=True,
            ).encode("ascii") + b"\n"
            with self.assertRaisesRegex(MODULE.RootBoundaryError, "invalid-root-request"):
                MODULE.decode_root_request(
                    encoded, fields
                )

    def test_no_lkg_bootstrap_rejects_unqualified_proxy_mode(self) -> None:
        self.paths.protected_env.write_bytes(b"BOLT_SYNTHETIC_PROXY_MODE=logs\n")
        with self.assertRaisesRegex(
            MODULE.RootBoundaryError, "invalid-proxy-mode"
        ):
            self.boundary.verify_bootstrap()

    @unittest.skipIf(os.name == "nt", "POSIX mode contract")
    def test_protected_env_parent_and_file_modes_are_exact(self) -> None:
        self.boundary._validate_roots()
        self.paths.protected_env.parent.chmod(0o0770)
        with self.assertRaisesRegex(MODULE.RootBoundaryError, "insecure-directory"):
            self.boundary._validate_roots()
        self.paths.protected_env.parent.chmod(0o1770)
        self.paths.protected_env.chmod(0o0640)
        with self.assertRaisesRegex(MODULE.RootBoundaryError, "insecure-file"):
            self.boundary._validate_roots()

    def test_protected_env_maximum_is_exactly_one_mibibyte(self) -> None:
        self.assertEqual(1024 * 1024, MODULE.MAX_PROTECTED_ENV_BYTES)
        self.paths.protected_env.write_bytes(
            b"A" * (MODULE.MAX_PROTECTED_ENV_BYTES + 1)
        )

        with self.assertRaisesRegex(MODULE.RootBoundaryError, "insecure-file"):
            self.boundary._validate_roots()

    def test_protected_proxy_mode_is_exact_and_fail_closed(self) -> None:
        self.assertEqual("direct-kestrel", self.boundary._protected_proxy_mode())

        invalid = {
            "direct-with-path": (
                b"BOLT_SYNTHETIC_PROXY_MODE=direct-kestrel\n"
                b"BOLT_SYNTHETIC_PROXY_LOG_PATHS=/var/log/proxy/access.log\n"
            ),
            "direct-with-empty-path-key": (
                b"BOLT_SYNTHETIC_PROXY_MODE=direct-kestrel\n"
                b"BOLT_SYNTHETIC_PROXY_LOG_PATHS=\n"
            ),
        }
        for name, payload in invalid.items():
            with self.subTest(name=name):
                self.paths.protected_env.write_bytes(payload)
                with self.assertRaisesRegex(
                    MODULE.RootBoundaryError, "invalid-proxy-configuration"
                ):
                    self.boundary._protected_proxy_mode()

        for value in ("logs", "LOGS", "direct_kestrel", ""):
            with self.subTest(value=value):
                self.paths.protected_env.write_bytes(
                    f"BOLT_SYNTHETIC_PROXY_MODE={value}\n".encode("utf-8")
                )
                with self.assertRaisesRegex(MODULE.RootBoundaryError, "invalid-proxy-mode"):
                    self.boundary._protected_proxy_mode()

        for name, payload in {
            "bom": b"\xef\xbb\xbfBOLT_SYNTHETIC_PROXY_MODE=direct-kestrel\n",
            "nul": b"BOLT_SYNTHETIC_PROXY_MODE=direct-kestrel\x00\n",
        }.items():
            with self.subTest(name=name):
                self.paths.protected_env.write_bytes(payload)
                with self.assertRaisesRegex(MODULE.RootBoundaryError, "invalid-protected-env"):
                    self.boundary._protected_proxy_mode()

    @unittest.skipUnless(
        os.name == "posix" and hasattr(os, "geteuid") and os.geteuid() == 0,
        "requires root to exercise sticky-directory ownership",
    )
    def test_sticky_protected_parent_allows_env_replace_but_blocks_root_sibling_rename(self) -> None:
        assert pwd is not None
        identity = pwd.getpwnam("nobody")
        protected = self.paths.protected_env.parent
        env_file = self.paths.protected_env
        sibling = file(protected / "root-evidence", 0o600, b"sealed\n")
        os.chown(protected, 0, identity.pw_gid)
        os.chmod(protected, 0o1770)
        os.chown(env_file, identity.pw_uid, identity.pw_gid)
        os.chown(sibling, 0, 0)
        code = (
            "import os,pathlib,sys; root=pathlib.Path(sys.argv[1]); "
            "env=root/'xeon-dev.env'; temp=root/'.env.tmp'; temp.write_text('NEW=1\\n'); "
            "os.replace(temp,env); blocked=False; "
            "\ntry: os.replace(root/'root-evidence',root/'stolen')\n"
            "except PermissionError: blocked=True\n"
            "raise SystemExit(0 if blocked else 1)"
        )
        result = subprocess.run(
            [sys.executable, "-c", code, str(protected)],
            check=False,
            preexec_fn=lambda: (os.setgid(identity.pw_gid), os.setuid(identity.pw_uid)),
        )
        self.assertEqual(0, result.returncode)
        self.assertEqual("NEW=1\n", env_file.read_text(encoding="utf-8"))
        self.assertTrue(sibling.exists())

    def test_no_lkg_bootstrap_requires_exact_absence_after_inspect_failure(self) -> None:
        self.runner.inspect_failure_with_present_container = True
        with self.assertRaisesRegex(MODULE.RootBoundaryError, "docker-inspection-failed"):
            self.boundary.verify_bootstrap()

        self.runner.inspect_failure_with_present_container = False
        self.runner.hub_absent = True
        self.assertEqual(
            "bootstrap-no-lkg-hub-stopped", self.boundary.verify_bootstrap()["state"]
        )
        self.runner.hub_absent = False
        self.runner.docker_unavailable = True
        with self.assertRaisesRegex(MODULE.RootBoundaryError, "docker-inspection-failed"):
            self.boundary.verify_bootstrap()

        absence_probes = [
            command
            for command in self.runner.commands
            if len(command) > 2 and command[1:3] == ["container", "ls"]
        ]
        self.assertTrue(absence_probes)
        self.assertEqual(
            [
                str(self.paths.docker),
                "container",
                "ls",
                "-a",
                "--no-trunc",
                "--filter",
                "name=^/xframework-bolt-hub$",
                "--format",
                "{{.Names}}",
            ],
            absence_probes[0],
        )

    def test_malformed_inspect_output_fails_closed(self) -> None:
        self.runner.inspect_output = "unexpected"
        with self.assertRaisesRegex(MODULE.RootBoundaryError, "docker-inspection-failed"):
            self.boundary.verify_bootstrap()

    def test_absence_probe_requires_exactly_empty_output(self) -> None:
        self.runner.hub_absent = True
        self.runner.container_list_output = "\n"
        with self.assertRaisesRegex(MODULE.RootBoundaryError, "docker-inspection-failed"):
            self.boundary.verify_bootstrap()

    def test_prepare_bound_run_rejects_invalid_identity_and_existing_path(self) -> None:
        binding = self.source_binding_request()
        for run_id, attempt in (("0", "1"), ("123/escape", "1"), ("123", "0"), ("123", "2")):
            with self.subTest(run_id=run_id, attempt=attempt), self.assertRaisesRegex(
                MODULE.RootBoundaryError, "invalid-run-identity"
            ):
                self.boundary.prepare_bound_run(run_id, attempt, binding)
        evidence = self.boundary.prepare_bound_run("123", "1", binding)
        self.assertEqual("candidate-run-prepared", evidence["state"])
        self.assertEqual("passed", evidence["source_binding"])
        marker = self.paths.run_root / "123-1" / MODULE.SOURCE_BINDING_MARKER
        document = json.loads(marker.read_text(encoding="ascii"))
        self.assertEqual(MODULE.SOURCE_BINDING_SCHEMA, document["schema"])
        self.assertEqual("123", document["run_id"])
        self.assertEqual(1, document["run_attempt"])
        self.assertEqual(binding, document["source_binding"])
        if os.name == "posix":
            metadata = marker.stat()
            self.assertEqual(0o440, stat.S_IMODE(metadata.st_mode))
            self.assertEqual(self.boundary.deployment_uid, metadata.st_uid)
            self.assertEqual(self.boundary.deployment_gid, metadata.st_gid)
        with self.assertRaisesRegex(MODULE.RootBoundaryError, "run-exists"):
            self.boundary.prepare_bound_run("123", "1", binding)

    def test_prepare_bound_run_holds_installation_lock_through_creation(self) -> None:
        events: list[str] = []
        target = self.paths.run_root / "123-1"
        original_verify = self.boundary.verify_source_binding

        @contextlib.contextmanager
        def tracked_lock(*_: object, **__: object):
            events.append("lock-acquired")
            yield
            self.assertTrue(target.is_dir())
            self.assertTrue((target / MODULE.SOURCE_BINDING_MARKER).is_file())
            events.append("lock-released")

        def tracked_verify(
            expected: dict[str, str], *, lease_lock_held: bool = False
        ) -> dict[str, object]:
            self.assertTrue(lease_lock_held)
            self.assertEqual(["lock-acquired"], events)
            events.append("source-bound")
            return original_verify(expected, lease_lock_held=True)

        with (
            mock.patch.object(MODULE, "exclusive_lease_lock", tracked_lock),
            mock.patch.object(
                self.boundary,
                "verify_source_binding",
                side_effect=tracked_verify,
            ),
        ):
            evidence = self.boundary.prepare_bound_run(
                "123", "1", self.source_binding_request()
            )

        self.assertEqual("candidate-run-prepared", evidence["state"])
        self.assertEqual(
            ["lock-acquired", "source-bound", "lock-released"], events
        )

    @unittest.skipUnless(os.name == "posix", "POSIX operator recovery contract")
    def test_abandon_bound_run_validates_and_removes_only_the_bound_marker(self) -> None:
        binding = self.source_binding_request()
        self.boundary.prepare_bound_run("123", "1", binding)
        marker = self.paths.run_root / "123-1" / MODULE.SOURCE_BINDING_MARKER

        evidence = self.boundary.abandon_bound_run("123", "1", binding)

        self.assertEqual("candidate-source-binding-abandoned", evidence["state"])
        self.assertEqual("passed", evidence["source_binding"])
        self.assertFalse(marker.exists())
        self.assertTrue(marker.parent.is_dir())

    @unittest.skipUnless(os.name == "posix", "POSIX operator recovery contract")
    def test_abandon_bound_run_rejects_active_state_without_removing_marker(self) -> None:
        binding = self.source_binding_request()
        self.boundary.prepare_bound_run("123", "1", binding)
        marker = self.paths.run_root / "123-1" / MODULE.SOURCE_BINDING_MARKER
        lease = file(
            self.paths.state_root / "deployment-lease.json",
            0o600,
            b"{}\n",
        )

        with self.assertRaisesRegex(MODULE.RootBoundaryError, "active-deployment-state"):
            self.boundary.abandon_bound_run("123", "1", binding)

        self.assertTrue(marker.is_file())
        lease.unlink()

    def test_abandon_bound_run_rejects_non_posix_before_state_access(self) -> None:
        with (
            mock.patch.object(MODULE.os, "name", "nt"),
            mock.patch.object(
                MODULE,
                "exclusive_lease_lock",
                side_effect=AssertionError("lock must not be accessed"),
            ),
            self.assertRaisesRegex(MODULE.RootBoundaryError, "unsupported-platform"),
        ):
            self.boundary.abandon_bound_run("123", "1", {})

    @unittest.skipUnless(
        sys.platform.startswith("linux")
        and hasattr(os, "geteuid")
        and os.geteuid() == 0,
        "requires Linux root to exercise deployment-user marker replacement",
    )
    def test_abandon_bound_run_freezes_directory_before_marker_unlink(self) -> None:
        assert pwd is not None
        identity = pwd.getpwnam("daemon")
        for path in (self.paths.state_root, self.paths.hooks_root, self.paths.protected_env):
            os.chown(path, identity.pw_uid, identity.pw_gid)
        os.chown(self.paths.protected_env.parent, 0, identity.pw_gid)
        os.chown(self.paths.lease_lock, 0, identity.pw_gid)
        replacement_attempts: list[subprocess.CompletedProcess[str]] = []

        def try_replacement(marker: Path) -> None:
            result = subprocess.run(
                [
                    "/usr/sbin/runuser",
                    "-u",
                    identity.pw_name,
                    "--",
                    "/usr/bin/mv",
                    str(marker),
                    str(marker.with_name("hidden-source-binding.json")),
                ],
                capture_output=True,
                text=True,
                timeout=10,
            )
            replacement_attempts.append(result)

        boundary = MODULE.RootBoundary(
            self.paths,
            runner=self.runner,
            deployment_user=identity.pw_name,
            after_abandon_marker_validation=try_replacement,
        )
        binding = self.source_binding_request()
        boundary.prepare_bound_run("123", "1", binding)

        evidence = boundary.abandon_bound_run("123", "1", binding)

        self.assertEqual("candidate-source-binding-abandoned", evidence["state"])
        self.assertEqual(1, len(replacement_attempts))
        self.assertNotEqual(0, replacement_attempts[0].returncode)
        target = self.paths.run_root / "123-1"
        metadata = target.stat()
        self.assertEqual(identity.pw_uid, metadata.st_uid)
        self.assertEqual(identity.pw_gid, metadata.st_gid)
        self.assertEqual(0o700, stat.S_IMODE(metadata.st_mode))
        self.assertFalse((target / MODULE.SOURCE_BINDING_MARKER).exists())

    @unittest.skipUnless(
        sys.platform.startswith("linux")
        and hasattr(os, "geteuid")
        and os.geteuid() == 0,
        "requires Linux root to exercise partial directory freezes",
    )
    def test_abandon_bound_run_restores_directory_after_freeze_mode_failure(self) -> None:
        self._assert_abandon_freeze_failure_restores_directory("fchmod")

    @unittest.skipUnless(
        sys.platform.startswith("linux")
        and hasattr(os, "geteuid")
        and os.geteuid() == 0,
        "requires Linux root to exercise partial directory freezes",
    )
    def test_abandon_bound_run_restores_directory_after_freeze_sync_failure(self) -> None:
        self._assert_abandon_freeze_failure_restores_directory("fsync")

    def _assert_abandon_freeze_failure_restores_directory(self, operation: str) -> None:
        assert pwd is not None
        identity = pwd.getpwnam("daemon")
        for path in (self.paths.state_root, self.paths.hooks_root, self.paths.protected_env):
            os.chown(path, identity.pw_uid, identity.pw_gid)
        os.chown(self.paths.protected_env.parent, 0, identity.pw_gid)
        os.chown(self.paths.lease_lock, 0, identity.pw_gid)
        boundary = MODULE.RootBoundary(
            self.paths,
            runner=self.runner,
            deployment_user=identity.pw_name,
        )
        binding = self.source_binding_request()
        boundary.prepare_bound_run("123", "1", binding)
        target = self.paths.run_root / "123-1"
        marker = target / MODULE.SOURCE_BINDING_MARKER
        target_inode = target.stat().st_ino
        original = getattr(MODULE.os, operation)
        injected = False

        def fail_once(descriptor: int, *args: object) -> object:
            nonlocal injected
            metadata = os.fstat(descriptor)
            if not injected and metadata.st_ino == target_inode:
                injected = True
                raise OSError(f"injected-{operation}-failure")
            return original(descriptor, *args)

        with (
            mock.patch.object(MODULE.os, operation, side_effect=fail_once),
            self.assertRaisesRegex(OSError, f"injected-{operation}-failure"),
        ):
            boundary.abandon_bound_run("123", "1", binding)

        self.assertTrue(injected)
        metadata = target.stat()
        self.assertEqual(identity.pw_uid, metadata.st_uid)
        self.assertEqual(identity.pw_gid, metadata.st_gid)
        self.assertEqual(0o700, stat.S_IMODE(metadata.st_mode))
        self.assertTrue(marker.is_file())

    def test_effective_dropins_and_redirected_exec_are_rejected(self) -> None:
        service = "xframework-bolt-phase0-watchdog.service"
        timer = "xframework-bolt-phase0-watchdog.timer"
        for unit, prop, value in (
            (service, "DropInPaths", "/etc/systemd/system/watchdog.service.d/override.conf"),
            (timer, "DropInPaths", "/etc/systemd/system/watchdog.timer.d/override.conf"),
            (service, "ExecStartPre", "{ path=/bin/true ; argv[]=/bin/true ; }"),
            (service, "ExecStart", "{ path=/bin/true ; argv[]=/bin/true ; }"),
            (timer, "Unit", "attacker.service"),
        ):
            with self.subTest(unit=unit, prop=prop):
                self.runner.overrides[(unit, prop)] = value
                with self.assertRaisesRegex(MODULE.RootBoundaryError, "systemd-contract"):
                    self.boundary._validate_systemd()
                self.runner.overrides.clear()

    def test_effective_write_paths_allow_only_deploy_root_and_env_parent(self) -> None:
        service = "xframework-bolt-phase0-watchdog.service"
        expected = f"{self.paths.deploy_root} {self.paths.protected_env.parent}"
        self.boundary._validate_systemd()
        for value in (
            str(self.paths.deploy_root),
            f"{self.paths.deploy_root} {self.paths.protected_env}",
            f"{self.paths.deploy_root} /opt",
            f"{expected} /tmp",
        ):
            with self.subTest(value=value):
                self.runner.overrides[(service, "ReadWritePaths")] = value
                with self.assertRaisesRegex(MODULE.RootBoundaryError, "systemd-contract"):
                    self.boundary._validate_systemd()
                self.runner.overrides.clear()

    def test_restrict_address_families_accepts_systemd_canonical_order(self) -> None:
        service = "xframework-bolt-phase0-watchdog.service"
        self.runner.overrides[(service, "RestrictAddressFamilies")] = (
            "AF_INET AF_INET6 AF_UNIX"
        )

        self.boundary._validate_systemd()

    def test_restrict_address_families_rejects_membership_drift(self) -> None:
        service = "xframework-bolt-phase0-watchdog.service"
        for value in (
            "AF_UNIX AF_INET",
            "AF_UNIX AF_INET AF_INET6 AF_NETLINK",
            "AF_UNIX AF_INET AF_INET",
        ):
            with self.subTest(value=value):
                self.runner.overrides[(service, "RestrictAddressFamilies")] = value
                with self.assertRaisesRegex(MODULE.RootBoundaryError, "systemd-contract"):
                    self.boundary._validate_systemd()
                self.runner.overrides.clear()

    def test_quarantine_replacement_attempt_never_becomes_lkg(self) -> None:
        candidate = directory(self.paths.run_root / "123-1", 0o700)
        file(candidate / "docker-compose.yml", 0o600, b"services: {}\n")
        lease = self.arm_activation_lease(candidate)

        def replace_original(source: Path, _: Path) -> None:
            self.assertTrue(source.is_dir())
            metadata = source.lstat()
            if os.name == "posix":
                self.assertEqual(self.boundary.deployment_uid, metadata.st_uid)
                self.assertEqual(self.boundary.deployment_gid, metadata.st_gid)
                self.assertEqual(0o700, stat.S_IMODE(metadata.st_mode))
            evidence, exit_code = lease.require_fresh()
            self.assertEqual(0, exit_code)
            self.assertEqual("lease-active", evidence["reason_code"])
            source.rmdir()
            directory(source, 0o700)
            file(source / "docker-compose.yml", 0o600, b"services: {attacker: {}}\n")

        boundary = MODULE.RootBoundary(
            self.paths,
            runner=self.runner,
            enforce_root=False,
            after_quarantine=replace_original,
        )
        boundary._qualify = lambda *_: None  # type: ignore[method-assign]
        boundary._seal = lambda *_: None  # type: ignore[method-assign]
        with self.assertRaisesRegex(MODULE.RootBoundaryError, "candidate-path-recreated"):
            boundary.activate("123", "1", "a" * 40, "xframework")
        self.assertFalse(self.paths.pointer.exists())
        self.assertFalse(self.runner.hub_running)
        timer_stops = [
            command
            for command in self.runner.commands
            if len(command) > 2
            and command[1] == "stop"
            and command[-1] == "xframework-bolt-phase0-watchdog.timer"
        ]
        self.assertEqual([], timer_stops)
        self.assertTrue(self.runner.timer_active)

    def test_sealing_and_pointer_publication_are_fsync_ordered(self) -> None:
        events: list[tuple[str, Path]] = []
        boundary = MODULE.RootBoundary(
            self.paths,
            runner=self.runner,
            enforce_root=False,
            sync_path=lambda path, is_directory: None,
            operation_trace=lambda operation, path: events.append((operation, path)),
        )
        run = directory(self.paths.quarantine_root / "staging" / "123-1", 0o700)
        artifact = file(run / "docker-compose.yml", 0o600, b"services: {}\n")
        boundary._write_marker(run / "qualified-commit", b"a" * 40 + b"\n")
        boundary._write_marker(run / "security-qualified", b"")
        boundary._seal(run)
        installed = self.paths.run_root / "123-1"
        os.rename(run, installed)
        boundary._sync(installed, True)
        boundary._sync(run.parent, True)
        boundary._sync(self.paths.run_root, True)
        boundary._publish_pointer(installed)

        sealed_index = events.index(("run-sealed", run))
        pointer_index = events.index(("pointer-replaced", self.paths.pointer))
        self.assertLess(events.index(("fsync-file", artifact)), sealed_index)
        self.assertLess(events.index(("fsync-dir", run)), sealed_index)
        self.assertLess(sealed_index, pointer_index)
        self.assertLess(
            events.index(("fsync-file", self.paths.lkg_root / next(
                path.name for operation, path in events
                if operation == "fsync-file" and path.parent == self.paths.lkg_root
            ))),
            pointer_index,
        )
        self.assertGreater(events.index(("fsync-dir", self.paths.lkg_root)), pointer_index)

    def test_final_sealed_rename_and_pointer_publish_share_the_lease_lock(self) -> None:
        events: list[tuple[str, Path]] = []
        boundary = MODULE.RootBoundary(
            self.paths,
            runner=self.runner,
            enforce_root=False,
            operation_trace=lambda operation, path: events.append((operation, path)),
        )
        candidate = directory(self.paths.run_root / "123-1", 0o700)
        file(candidate / "docker-compose.yml", 0o600, b"services: {}\n")

        def qualify(run: Path, run_id: str, attempt: str, commit: str, project: str) -> None:
            file(
                run / "qualification-evidence.json",
                0o600,
                json.dumps(
                    {
                        "schema": MODULE.QUALIFICATION_SCHEMA,
                        "status": "passed",
                        "run_id": run_id,
                        "run_attempt": int(attempt),
                        "source_commit": commit,
                        "project_name": project,
                        "artifacts": {"docker-compose.yml": {"path": "docker-compose.yml"}},
                        "errors": [],
                    }
                ).encode(),
            )
            boundary._write_marker(run / "qualified-commit", (commit + "\n").encode())
            boundary._write_marker(run / "security-qualified", b"")

        boundary._qualify = qualify  # type: ignore[method-assign]
        boundary._validate_sealed_run = lambda _: None  # type: ignore[method-assign]
        lease = self.arm_activation_lease(candidate)
        evidence = boundary.activate("123", "1", "a" * 40, "xframework")
        self.assertEqual("qualified-lkg-activated", evidence["state"])

        acquired = [index for index, event in enumerate(events) if event[0] == "lease-lock-acquired"]
        releasing = [index for index, event in enumerate(events) if event[0] == "lease-lock-releasing"]
        installed = events.index(("sealed-run-installed", self.paths.run_root / "123-1"))
        published = events.index(("pointer-replaced", self.paths.pointer))
        self.assertEqual(2, len(acquired))
        self.assertEqual(2, len(releasing))
        self.assertLess(acquired[1], installed)
        self.assertLess(installed, published)
        self.assertLess(published, releasing[1])

    def test_final_activation_rejects_a_lease_removed_during_qualification(self) -> None:
        candidate = directory(self.paths.run_root / "123-1", 0o700)
        file(candidate / "docker-compose.yml", 0o600, b"services: {}\n")
        lease = self.arm_activation_lease(candidate)

        def remove_lease(_: Path, __: Path) -> None:
            self.paths.state_root.joinpath("deployment-lease.json").unlink()

        boundary = MODULE.RootBoundary(
            self.paths,
            runner=self.runner,
            enforce_root=False,
            after_quarantine=remove_lease,
        )
        boundary._qualify = lambda *_: None  # type: ignore[method-assign]
        boundary._seal = lambda *_: None  # type: ignore[method-assign]
        with self.assertRaisesRegex(MODULE.RootBoundaryError, "missing-path"):
            boundary.activate("123", "1", "a" * 40, "xframework")
        self.assertFalse(self.paths.pointer.exists())

    def test_activation_lease_parser_rejects_duplicate_keys(self) -> None:
        candidate = directory(self.paths.run_root / "123-1", 0o700)
        lease = self.arm_activation_lease(candidate)
        lease_path = self.paths.state_root / "deployment-lease.json"
        raw = lease_path.read_text(encoding="utf-8").rstrip()
        duplicate = raw[:-1] + ',"run_attempt":1}\n'
        replacement = file(
            lease_path.with_name("deployment-lease.replacement"),
            0o600,
            duplicate.encode("utf-8"),
        )
        os.replace(replacement, lease_path)

        with self.assertRaisesRegex(MODULE.RootBoundaryError, "invalid-activation-lease"):
            self.boundary._validate_activation_lease(
                "123", 1, candidate, "xframework"
            )

    def test_activation_lease_parser_rejects_legacy_or_unbound_lease(self) -> None:
        candidate = directory(self.paths.run_root / "123-1", 0o700)
        lease = self.arm_activation_lease(candidate)
        lease_path = self.paths.state_root / "deployment-lease.json"
        current = json.loads(lease_path.read_text(encoding="utf-8"))

        invalid_documents = []
        legacy = dict(current)
        legacy["schema"] = "xframework.bolt.phase0.deployment-lease.v1"
        legacy.pop("bootstrap_source_bound")
        invalid_documents.append(legacy)
        unbound = dict(current)
        unbound["bootstrap_source_bound"] = False
        invalid_documents.append(unbound)

        for document in invalid_documents:
            with self.subTest(schema=document["schema"]):
                LEASE_MODULE.atomic_write_json(
                    lease_path,
                    document,
                    self.boundary.deployment_uid,
                )
                with self.assertRaisesRegex(
                    MODULE.RootBoundaryError, "invalid-activation-lease"
                ):
                    self.boundary._validate_activation_lease(
                        "123", 1, candidate, "xframework"
                    )

    def test_final_activation_rejects_a_lease_that_stales_during_qualification(self) -> None:
        candidate = directory(self.paths.run_root / "123-1", 0o700)
        file(candidate / "docker-compose.yml", 0o600, b"services: {}\n")
        lease = self.arm_activation_lease(candidate)
        lease_path = self.paths.state_root / "deployment-lease.json"

        def stale_lease(_: Path, __: Path) -> None:
            document = json.loads(lease_path.read_text(encoding="utf-8"))
            document["heartbeat_utc"] = "2000-01-01T00:00:00Z"
            LEASE_MODULE.atomic_write_json(
                lease_path, document, self.boundary.deployment_uid
            )

        boundary = MODULE.RootBoundary(
            self.paths,
            runner=self.runner,
            enforce_root=False,
            after_quarantine=stale_lease,
        )
        boundary._qualify = lambda *_: None  # type: ignore[method-assign]
        boundary._seal = lambda *_: None  # type: ignore[method-assign]
        with self.assertRaisesRegex(MODULE.RootBoundaryError, "stale-activation-lease"):
            boundary.activate("123", "1", "a" * 40, "xframework")
        self.assertFalse(self.paths.pointer.exists())

    def test_ensure_watchdog_enables_and_validates_the_effective_timer(self) -> None:
        self.runner.timer_active = False
        evidence = self.boundary.ensure_watchdog()
        self.assertEqual("watchdog-active", evidence["state"])
        self.assertTrue(self.runner.timer_active)

    @unittest.skipIf(os.name == "nt", "symlink creation may require privileges")
    def test_candidate_symlink_is_rejected_after_quarantine(self) -> None:
        source = directory(self.paths.quarantine_root / "candidate", 0o700)
        target = file(self.paths.quarantine_root / "target", 0o600)
        (source / "docker-compose.yml").symlink_to(target)
        destination = self.paths.quarantine_root / "copy"
        with self.assertRaisesRegex(MODULE.RootBoundaryError, "invalid-candidate-artifact"):
            self.boundary._copy_quarantined(source, destination)

    def test_artifact_replaced_after_lstat_is_never_copied(self) -> None:
        source = directory(self.paths.quarantine_root / "candidate-replacement", 0o700)
        artifact = file(source / "docker-compose.yml", 0o600, b"trusted\n")
        destination = self.paths.quarantine_root / "replacement-copy"

        def replace_after_validation(path: Path) -> None:
            self.assertEqual(artifact, path)
            path.unlink()
            file(path, 0o600, b"attacker\n")

        boundary = MODULE.RootBoundary(
            self.paths,
            runner=self.runner,
            enforce_root=False,
            after_artifact_lstat=replace_after_validation,
        )
        with self.assertRaisesRegex(MODULE.RootBoundaryError, "candidate-artifact-replaced"):
            boundary._copy_quarantined(source, destination)
        self.assertFalse((destination / "docker-compose.yml").exists())

    @unittest.skipIf(os.name == "nt", "POSIX executable mode contract")
    def test_python_symlink_resolves_to_root_safe_executable(self) -> None:
        real = file(Path(self.temporary.name) / "python-real", 0o755)
        link = Path(self.temporary.name) / "python-link"
        link.symlink_to(real)
        self.assertEqual(real, MODULE.resolve_system_python(link, require_root=False))
        real.chmod(0o777)
        with self.assertRaisesRegex(MODULE.RootBoundaryError, "invalid-python"):
            MODULE.resolve_system_python(link, require_root=False)

    def test_cli_has_only_fixed_root_boundary_commands(self) -> None:
        for command in (
            "verify-bootstrap",
            "ensure-watchdog",
            "prepare-bound-run",
            "abandon-bound-run",
            "activate",
        ):
            self.assertEqual(command, MODULE.parse_args([command]).command)
        with self.assertRaises(SystemExit):
            MODULE.parse_args(["prepare-bound-run", "123", "1"])
        with self.assertRaises(SystemExit):
            MODULE.parse_args(["activate", "123", "1", "a" * 40, "xframework"])
        with self.assertRaises(SystemExit):
            MODULE.parse_args(["future-command"])

    def test_root_request_requires_exact_canonical_schema(self) -> None:
        prepare = {"run_attempt": "1", "run_id": "29244847846"}
        raw = json.dumps(
            prepare,
            ensure_ascii=True,
            separators=(",", ":"),
            sort_keys=True,
        ).encode("ascii") + b"\n"
        self.assertEqual(
            prepare,
            MODULE.decode_root_request(raw, {"run_id", "run_attempt"}),
        )

        invalid = (
            b"",
            raw[:-1],
            raw + b"\n",
            raw.replace(b"\n", b"\r\n"),
            b'{"run_attempt":"1","run_id":"1"} \n',
            b'{"run_attempt":"1","run_id":"1","run_id":"2"}\n',
            b'{"run_attempt":1,"run_id":"1"}\n',
            b'{"run_attempt":"1"}\n',
            b'{"extra":"x","run_attempt":"1","run_id":"1"}\n',
            b'{"run_attempt":NaN,"run_id":"1"}\n',
            b"\xff\n",
            b"x" * (MODULE.ROOT_REQUEST_MAX_BYTES + 1),
        )
        for candidate in invalid:
            with self.subTest(candidate=candidate[:80]):
                with self.assertRaisesRegex(MODULE.RootBoundaryError, "invalid-root-request"):
                    MODULE.decode_root_request(candidate, {"run_id", "run_attempt"})

    def test_invalid_privileged_request_stops_hub_fail_closed(self) -> None:
        boundary = mock.Mock()
        with (
            mock.patch.object(MODULE, "RootBoundary", return_value=boundary),
            mock.patch.object(
                MODULE,
                "read_root_request",
                side_effect=MODULE.RootBoundaryError("invalid-root-request"),
            ),
            mock.patch("builtins.print"),
        ):
            self.assertEqual(1, MODULE.main(["activate"]))
        boundary.stop_hub.assert_called_once_with()

    @unittest.skipIf(os.name == "nt", "select does not support Windows pipe descriptors")
    def test_root_request_reader_accepts_exact_pipe_and_rejects_oversize(self) -> None:
        fields = {"run_id", "run_attempt"}
        raw = b'{"run_attempt":"1","run_id":"29244847846"}\n'
        for payload, expected_error in (
            (raw, None),
            (b"x" * (MODULE.ROOT_REQUEST_MAX_BYTES + 1), "root-request-too-large"),
        ):
            read_descriptor, write_descriptor = os.pipe()
            try:
                os.write(write_descriptor, payload)
                os.close(write_descriptor)
                write_descriptor = -1
                binary = os.fdopen(read_descriptor, "rb", buffering=0)
                read_descriptor = -1
                stream = io.TextIOWrapper(binary, encoding="utf-8")
                try:
                    with mock.patch.object(MODULE.sys, "stdin", stream):
                        if expected_error is None:
                            self.assertEqual(
                                {"run_attempt": "1", "run_id": "29244847846"},
                                MODULE.read_root_request(fields),
                            )
                        else:
                            with self.assertRaisesRegex(
                                MODULE.RootBoundaryError, expected_error
                            ):
                                MODULE.read_root_request(fields)
                finally:
                    stream.close()
            finally:
                if read_descriptor >= 0:
                    os.close(read_descriptor)
                if write_descriptor >= 0:
                    os.close(write_descriptor)

    @unittest.skipIf(os.name == "nt", "select does not support Windows pipe descriptors")
    def test_root_request_reader_has_a_hard_deadline(self) -> None:
        read_descriptor, write_descriptor = os.pipe()
        try:
            binary = os.fdopen(read_descriptor, "rb", buffering=0)
            read_descriptor = -1
            stream = io.TextIOWrapper(binary, encoding="utf-8")
            try:
                with (
                    mock.patch.object(MODULE.sys, "stdin", stream),
                    mock.patch.object(MODULE, "ROOT_REQUEST_TIMEOUT_SECONDS", 0.01),
                ):
                    with self.assertRaisesRegex(
                        MODULE.RootBoundaryError, "root-request-timeout"
                    ):
                        MODULE.read_root_request({"run_id", "run_attempt"})
            finally:
                stream.close()
        finally:
            if read_descriptor >= 0:
                os.close(read_descriptor)
            os.close(write_descriptor)


if __name__ == "__main__":
    unittest.main()
