#!/usr/bin/env python3
from __future__ import annotations

import importlib.util
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
        values = {
            "state_root": self.paths.state_root,
            "run_root": self.paths.run_root,
            "project_name": "xframework",
            "deployment_uid": self.boundary.deployment_uid,
            "lock_file": self.paths.lease_lock,
            "lock_owner_uid": self.boundary.deployment_uid,
            "lock_owner_gid": self.boundary.deployment_gid,
            "lock_parent_uid": self.boundary.deployment_uid,
        }
        values.update(changes)
        return LEASE_MODULE.ControllerConfig(**values)  # type: ignore[arg-type]

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

    def test_prepare_run_rejects_invalid_identity_and_existing_path(self) -> None:
        for run_id, attempt in (("0", "1"), ("123/escape", "1"), ("123", "0"), ("123", "2")):
            with self.subTest(run_id=run_id, attempt=attempt), self.assertRaisesRegex(
                MODULE.RootBoundaryError, "invalid-run-identity"
            ):
                self.boundary.prepare_run(run_id, attempt)
        evidence = self.boundary.prepare_run("123", "1")
        self.assertEqual("candidate-run-prepared", evidence["state"])
        with self.assertRaisesRegex(MODULE.RootBoundaryError, "run-exists"):
            self.boundary.prepare_run("123", "1")

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
        lease = LEASE_MODULE.DeploymentLeaseController(
            self.lease_config()
        )
        lease.arm("123", 1, "preflight", 600)
        lease.heartbeat("123", 1, "activation", True)

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
        lease = LEASE_MODULE.DeploymentLeaseController(
            self.lease_config()
        )
        lease.arm("123", 1, "preflight", 600)
        lease.heartbeat("123", 1, "activation", True)
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
        lease = LEASE_MODULE.DeploymentLeaseController(
            self.lease_config()
        )
        lease.arm("123", 1, "preflight", 600)
        lease.heartbeat("123", 1, "activation", True)

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
        lease = LEASE_MODULE.DeploymentLeaseController(
            self.lease_config()
        )
        lease.arm("123", 1, "preflight", 600)
        lease.heartbeat("123", 1, "activation", True)
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

    def test_final_activation_rejects_a_lease_that_stales_during_qualification(self) -> None:
        candidate = directory(self.paths.run_root / "123-1", 0o700)
        file(candidate / "docker-compose.yml", 0o600, b"services: {}\n")
        lease = LEASE_MODULE.DeploymentLeaseController(
            self.lease_config()
        )
        lease.arm("123", 1, "preflight", 600)
        lease.heartbeat("123", 1, "activation", True)
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
        self.assertEqual("verify-bootstrap", MODULE.parse_args(["verify-bootstrap"]).command)
        self.assertEqual("ensure-watchdog", MODULE.parse_args(["ensure-watchdog"]).command)
        with self.assertRaises(SystemExit):
            MODULE.parse_args(["activate", "123", "1", "a" * 40, "xframework", "extra"])


if __name__ == "__main__":
    unittest.main()
