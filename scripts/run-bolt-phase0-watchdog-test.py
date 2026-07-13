#!/usr/bin/env python3
from __future__ import annotations

import ast
import re
import hashlib
import json
import os
import shlex
import shutil
import stat
import subprocess
import sys
import tempfile
import textwrap
import time
import unittest
from pathlib import Path

if os.name == "posix":
    import grp
    import pwd
else:
    grp = None
    pwd = None


ROOT = Path(__file__).resolve().parent.parent
SCRIPT = ROOT / "scripts" / "run-bolt-phase0-watchdog.sh"
SERVICE = ROOT / "deploy" / "systemd" / "xframework-bolt-phase0-watchdog.service"
TIMER = ROOT / "deploy" / "systemd" / "xframework-bolt-phase0-watchdog.timer"
WORKFLOW = ROOT / ".github" / "workflows" / "deploy-xeon-dev.yml"
PHASE0_CI_WORKFLOW = ROOT / ".github" / "workflows" / "bolt-phase0-ci.yml"
LEGACY_WORKFLOW = ROOT / ".github" / "workflows" / "deploy-xeon-dev-service.yml"
PINNED_KNOWN_HOSTS = ROOT / ".github" / "known_hosts" / "xeon-dev"
ROOT_HELPER = ROOT / "scripts" / "manage-bolt-phase0-root.py"
BOOTSTRAP = ROOT / "deploy" / "bootstrap-xframework-bolt-phase0-root.sh"
SYNTHETICS = ROOT / "scripts" / "run-bolt-phase0-synthetics.sh"
LEASE_MANAGER = ROOT / "scripts" / "manage-bolt-phase0-deployment-lease.py"
SERVICES = (
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
PROTECTED_DEPLOYMENT_VARS = (
    "JWT_SECRET",
    "JWT_ISSUER",
    "JWT_AUDIENCE",
    "BOLT_SIGNATURE",
    "IDENTITYSERVER_SERVICE_IDENTITY_SECRET",
    "BOLT_HUB_SERVICE_IDENTITY_SECRET",
    "COMMUNICATIONS_SERVICE_IDENTITY_SECRET",
    "NOTIFICATIONS_SERVICE_IDENTITY_SECRET",
    "STORAGE_SERVICE_IDENTITY_SECRET",
    "ATTENDANCE_SERVICE_IDENTITY_SECRET",
    "SMSGATEWAY_SERVICE_IDENTITY_SECRET",
    "WALLETS_SERVICE_IDENTITY_SECRET",
    "INVENTARIO_SERVICE_IDENTITY_SECRET",
    "POS_SERVICE_IDENTITY_SECRET",
    "PORTAL_SERVICE_IDENTITY_SECRET",
    "OPERATIONS_DASHBOARD_SERVICE_IDENTITY_SECRET",
    "BOLT_HUB_TLS_FULLCHAIN_PATH",
    "BOLT_HUB_TLS_PRIVATE_KEY_PATH",
    "BOLT_HUB_TLS_CA_PATH",
    "BOLT_HUB_EXPOSE_PORT",
    "BOLT_HUB_PUBLIC_HOSTNAME",
    "IDENTITYSERVER_TLS_FULLCHAIN_PATH",
    "IDENTITYSERVER_TLS_PRIVATE_KEY_PATH",
    "IDENTITYSERVER_TLS_CA_PATH",
    "IDENTITYSERVER_PUBLIC_HOSTNAME",
    "IDENTITYSERVER_PUBLIC_HTTPS_PORT",
    "IDENTITYSERVER_BOLT_TRANSPORT_TOKEN_PATH",
    "BOLT_SYNTHETIC_IDENTITYSERVER_BASE_URL",
    "BOLT_SYNTHETIC_IDENTITYSERVER_CA_PATH",
    "BOLT_SYNTHETIC_TENANT_ID",
    "BOLT_SYNTHETIC_CREDENTIAL_ID",
    "BOLT_SYNTHETIC_DEVICE_ID",
    "BOLT_SYNTHETIC_USER_USERNAME",
    "BOLT_SYNTHETIC_USER_PASSWORD",
    "BOLT_SYNTHETIC_USER_ROLE_ID",
    "BOLT_SYNTHETIC_USER_AUTHORIZATION_TYPE",
    "BOLT_SYNTHETIC_MIN_TOKEN_LIFETIME_SECONDS",
    "BOLT_SYNTHETIC_TOKEN_REFRESH_COMMAND_PATH",
    "BOLT_SYNTHETIC_PROXY_MARKER_SCAN_COMMAND_PATH",
    "BOLT_SYNTHETIC_SEQ_MARKER_SCAN_COMMAND_PATH",
    "BOLT_SYNTHETIC_TRACE_MARKER_SCAN_COMMAND_PATH",
    "BOLT_SYNTHETIC_PLAINTEXT_REJECTION_COMMAND_PATH",
    "BOLT_SYNTHETIC_REDIS_INTERRUPTION_COMMAND_PATH",
    "BOLT_SYNTHETIC_OLD_GENERATION_REJECTION_COMMAND_PATH",
    "BOLT_SYNTHETIC_COMPOSE_PROJECT_NAME",
    "BOLT_SYNTHETIC_PLAINTEXT_PEER_SERVICE",
    "BOLT_SYNTHETIC_PROXY_MODE",
    "BOLT_SYNTHETIC_SEQ_API_URL",
    "BOLT_SYNTHETIC_SEQ_API_KEY",
    "BOLT_SYNTHETIC_JAEGER_QUERY_API_URL",
    "BOLT_SYNTHETIC_REDIS_POST_RECOVERY_COMMAND_PATH",
    "BOLT_PHASE0_RECOVERY_SYNTHETIC_COMMAND_PATH",
    "BOLT_SYNTHETIC_REJECTED_CLIENT_SECRET_PATH",
    "BOLT_SYNTHETIC_COMMUNICATIONS_TOKEN_PATH",
    "BOLT_SYNTHETIC_USER_TOKEN_PATH",
    "BOLT_SYNTHETIC_EXPIRY_TOKEN_PATH",
    "BOLT_SYNTHETIC_REJECTED_COMMUNICATIONS_TOKEN_PATH",
    "BOLT_SYNTHETIC_REJECTED_USER_TOKEN_PATH",
)


class WatchdogContractTests(unittest.TestCase):
    @staticmethod
    def _protected_deployment_preflight() -> tuple[tuple[str, ...], str]:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        start = workflow.index("- name: Validate protected deployment inputs without mutation")
        end = workflow.index("- name: Verify sealed watchdog bootstrap prerequisite", start)
        block = workflow[start:end]
        inventory_match = re.search(
            r"required_vars=\(\n(?P<body>.*?)\n\s+\)",
            block,
            re.DOTALL,
        )
        if inventory_match is None:
            raise AssertionError("protected deployment variable inventory was not found")
        inventory = tuple(
            line.strip()
            for line in inventory_match.group("body").splitlines()
            if line.strip()
        )
        script_match = re.search(
            r"python3 - \"\$XFRAMEWORK_ENV_FILE\" \"\$@\" <<'PY'\n"
            r"(?P<body>.*?)\n\s+PY\n",
            block,
            re.DOTALL,
        )
        if script_match is None:
            raise AssertionError("protected deployment Python preflight was not found")
        return inventory, textwrap.dedent(script_match.group("body"))

    @staticmethod
    def _protected_values(proxy_mode: str) -> dict[str, str]:
        values = {
            name: f"phase0-value-{index}"
            for index, name in enumerate(PROTECTED_DEPLOYMENT_VARS, start=1)
        }
        values["BOLT_SYNTHETIC_PROXY_MODE"] = proxy_mode
        if proxy_mode == "logs":
            values["BOLT_SYNTHETIC_PROXY_LOG_PATHS"] = "/var/log/xframework/proxy.log"
        return values

    @staticmethod
    def _write_protected_values(path: Path, values: dict[str, str], *, bom: bool = False) -> None:
        ordered_names = [
            *PROTECTED_DEPLOYMENT_VARS,
            *(
                ("BOLT_SYNTHETIC_PROXY_LOG_PATHS",)
                if "BOLT_SYNTHETIC_PROXY_LOG_PATHS" in values
                else ()
            ),
        ]
        content = "".join(
            f"{name}={values[name]}\n"
            for name in ordered_names
            if name in values
        ).encode("utf-8")
        path.write_bytes((b"\xef\xbb\xbf" if bom else b"") + content)
        path.chmod(0o600)

    def test_managers_share_fixed_existing_readonly_lock_contract(self) -> None:
        root_source = ROOT_HELPER.read_text(encoding="utf-8")
        lease_source = LEASE_MANAGER.read_text(encoding="utf-8")
        expected = "/usr/local/libexec/xframework-bolt-phase0/deployment-lease.lock"
        self.assertIn(expected, root_source)
        self.assertIn(expected, lease_source)
        self.assertNotIn(".deployment-lease.lock", root_source + lease_source)

        root_lock = root_source[
            root_source.index("def exclusive_lease_lock(") : root_source.index("def _file(")
        ]
        lease_lock = lease_source[
            lease_source.index("def exclusive_lock(") : lease_source.index("def _linux_prctl(")
        ]
        for implementation in (root_lock, lease_lock):
            self.assertIn("os.O_RDONLY", implementation)
            self.assertIn('getattr(os, "O_NOFOLLOW", 0)', implementation)
            self.assertIn("fcntl.LOCK_EX", implementation)
            self.assertGreaterEqual(implementation.count("validate_identity()"), 3)
            self.assertNotIn("os.O_RDWR", implementation)
            self.assertNotIn("os.O_CREAT", implementation)
            self.assertNotIn("os.O_EXCL", implementation)
            self.assertNotIn("os.unlink", implementation)
            self.assertNotIn("os.replace", implementation)

    def test_launcher_uses_qualified_lkg_helpers_and_exact_service_inventory(self) -> None:
        content = SCRIPT.read_text(encoding="utf-8")
        service_block = re.search(r"services=\(\n(?P<body>.*?)\n\)", content, re.DOTALL)
        self.assertIsNotNone(service_block)
        actual = tuple(line.strip() for line in service_block.group("body").splitlines())
        self.assertEqual(SERVICES, actual)
        for required in (
            "--project-name xframework",
            "--env-file /opt/xframework/xeon-dev.env",
            "manager=\"$lkg_run/manage-bolt-phase0-deployment-lease.py\"",
            "--rotation-manager \"$lkg_run/manage-bolt-phase0-rotation.py\"",
            "--runtime-verifier \"$lkg_run/verify-bolt-phase0-runtime.py\"",
            "--recovery-gate-hook \"$lkg_run/verify-bolt-phase0-qualification.py\"",
            "--python-executable \"$python\"",
            "--docker-executable \"$docker\"",
            'fixed_lease_manager=/usr/local/libexec/xframework-bolt-phase0/manage-bolt-phase0-deployment-lease.py',
            'controller_command=watch-no-lkg',
        ):
            self.assertIn(required, content)
        self.assertIn('trap on_exit EXIT', content)
        self.assertLess(content.index('trap on_exit EXIT'), content.index('test "$(id -un)"'))
        self.assertIn('summary.get("sha256") != "sha256:" + hashlib.sha256(raw).hexdigest()', content)
        self.assertNotIn("eval", content)
        self.assertNotIn("bash -c", content)

    def test_systemd_service_is_fail_closed_and_runs_as_deployment_identity(self) -> None:
        content = SERVICE.read_text(encoding="utf-8")
        for required in (
            "User=github-runner",
            "Group=github-runner",
            "SupplementaryGroups=docker",
            "ExecStart=/usr/local/sbin/xframework-bolt-phase0-watchdog",
            "NoNewPrivileges=true",
            "ProtectSystem=strict",
            "ProtectHome=read-only",
            "ReadWritePaths=/home/github-runner/xframework-deploy /opt/xframework",
            "UMask=0077",
        ):
            self.assertIn(required, content)
        write_paths = next(
            line for line in content.splitlines() if line.startswith("ReadWritePaths=")
        )
        self.assertEqual(
            "ReadWritePaths=/home/github-runner/xframework-deploy /opt/xframework",
            write_paths,
        )
        self.assertNotIn(" /opt ", f" {write_paths} ")
        self.assertNotIn(" /opt/xframework/xeon-dev.env ", f" {write_paths} ")
        self.assertNotIn(" /usr/local ", f" {write_paths} ")
        self.assertNotIn("/usr/local/libexec", write_paths)
        self.assertNotIn("Environment=", content)
        self.assertIn("TimeoutStartSec=4200", content)
        self.assertIn("4 x 900s", content)

    def test_timer_runs_independently_every_thirty_seconds(self) -> None:
        content = TIMER.read_text(encoding="utf-8")
        self.assertIn("OnBootSec=30s", content)
        self.assertIn("OnUnitActiveSec=30s", content)
        self.assertIn("Persistent=true", content)
        self.assertIn("Unit=xframework-bolt-phase0-watchdog.service", content)

    def test_sudoers_delegates_only_the_fixed_root_helper_boundary(self) -> None:
        bootstrap = BOOTSTRAP.read_text(encoding="utf-8")
        helper_source = ROOT_HELPER.read_text(encoding="utf-8")
        expected_alias = (
            "Cmnd_Alias XFRAMEWORK_BOLT_PHASE0_ROOT = "
            "/usr/local/sbin/xframework-bolt-phase0-root verify-bootstrap, "
            "/usr/local/sbin/xframework-bolt-phase0-root ensure-watchdog, "
            "/usr/local/sbin/xframework-bolt-phase0-root prepare-bound-run, "
            "/usr/local/sbin/xframework-bolt-phase0-root activate"
        )

        aliases = [
            line
            for line in bootstrap.splitlines()
            if line.startswith("Cmnd_Alias XFRAMEWORK_BOLT_PHASE0_ROOT = ")
        ]
        self.assertEqual([expected_alias], aliases)
        authorizations = [
            line
            for line in bootstrap.splitlines()
            if line.startswith("github-runner ALL=(root) NOPASSWD:")
        ]
        self.assertEqual(
            ["github-runner ALL=(root) NOPASSWD: XFRAMEWORK_BOLT_PHASE0_ROOT"],
            authorizations,
        )
        self.assertNotIn("prepare-bound-run *", bootstrap)
        self.assertNotIn("activate *", bootstrap)
        self.assertNotIn("abandon-bound-run", bootstrap)

        parser_commands = {
            node.args[0].value
            for node in ast.walk(ast.parse(helper_source))
            if isinstance(node, ast.Call)
            and isinstance(node.func, ast.Attribute)
            and node.func.attr == "add_parser"
            and node.args
            and isinstance(node.args[0], ast.Constant)
            and isinstance(node.args[0].value, str)
        }
        self.assertEqual(
            parser_commands,
            {
                "verify-bootstrap",
                "ensure-watchdog",
                "prepare-bound-run",
                "abandon-bound-run",
                "activate",
            },
        )

    @unittest.skipUnless(
        os.name == "posix" and hasattr(os, "geteuid") and os.geteuid() == 0,
        "requires a disposable Linux root sudo policy",
    )
    def test_sudo_policy_preserves_stdin_and_denies_extra_arguments(self) -> None:
        assert pwd is not None
        bootstrap = BOOTSTRAP.read_text(encoding="utf-8")
        sudo = Path(os.environ.get("BOLT_PHASE0_SUDO_BINARY", shutil.which("sudo") or ""))
        visudo = Path(
            os.environ.get("BOLT_PHASE0_VISUDO_BINARY", shutil.which("visudo") or "")
        )
        runuser = Path(shutil.which("runuser") or "")
        for executable in (sudo, visudo, runuser):
            self.assertTrue(executable.is_file(), executable)

        test_user = "nobody"
        pwd.getpwnam(test_user)
        helper_root = Path(
            tempfile.mkdtemp(prefix="xframework-bolt-phase0-policy-", dir="/opt")
        )
        policy_name = (
            f"xframework_bolt_phase0_policy_test_{os.getpid()}_"
            f"{helper_root.name.rsplit('-', 1)[-1]}"
        )
        policy_path = Path("/etc/sudoers.d") / policy_name
        helper = helper_root / "root-helper"
        policy_created = False
        try:
            os.chmod(helper_root, 0o755)
            helper.write_text(
                "#!/bin/sh\nset -eu\ntest ! -t 0\ncat\n",
                encoding="utf-8",
                newline="\n",
            )
            os.chown(helper, 0, 0)
            os.chmod(helper, 0o555)

            alias_line = next(
                line
                for line in bootstrap.splitlines()
                if line.startswith("Cmnd_Alias XFRAMEWORK_BOLT_PHASE0_ROOT = ")
            )
            authorization_line = next(
                line
                for line in bootstrap.splitlines()
                if line.startswith("github-runner ALL=(root) NOPASSWD:")
            )
            test_alias = f"XFRAMEWORK_BOLT_PHASE0_POLICY_TEST_{os.getpid()}"
            policy = (
                alias_line.replace("XFRAMEWORK_BOLT_PHASE0_ROOT", test_alias)
                .replace("/usr/local/sbin/xframework-bolt-phase0-root", str(helper))
                + "\n"
                + authorization_line.replace("github-runner", test_user).replace(
                    "XFRAMEWORK_BOLT_PHASE0_ROOT", test_alias
                )
                + "\n"
            ).encode("utf-8")
            descriptor = os.open(
                policy_path,
                os.O_WRONLY
                | os.O_CREAT
                | os.O_EXCL
                | getattr(os, "O_CLOEXEC", 0)
                | getattr(os, "O_NOFOLLOW", 0),
                0o440,
            )
            policy_created = True
            try:
                view = memoryview(policy)
                while view:
                    written = os.write(descriptor, view)
                    self.assertGreater(written, 0)
                    view = view[written:]
                os.fchown(descriptor, 0, 0)
                os.fchmod(descriptor, 0o440)
                os.fsync(descriptor)
            finally:
                os.close(descriptor)
            subprocess.run(
                [str(visudo), "-cf", str(policy_path)],
                check=True,
                capture_output=True,
                timeout=10,
            )

            base = [str(runuser), "-u", test_user, "--", str(sudo), "-n", str(helper)]
            for command, payload in (
                ("verify-bootstrap", b""),
                ("ensure-watchdog", b""),
                ("prepare-bound-run", b'{"run_attempt":"1","run_id":"1"}\n'),
                ("activate", b'{"expected_commit":"a","project_name":"x"}\n'),
            ):
                result = subprocess.run(
                    [*base, command],
                    input=payload,
                    capture_output=True,
                    timeout=10,
                )
                self.assertEqual(0, result.returncode, result.stderr.decode(errors="replace"))
                self.assertEqual(payload, result.stdout)
            for arguments in (
                (),
                ("prepare-bound-run", "unexpected"),
                ("abandon-bound-run",),
                ("activate", "unexpected"),
                ("future-command",),
            ):
                result = subprocess.run(
                    [*base, *arguments],
                    input=b"",
                    capture_output=True,
                    timeout=10,
                )
                self.assertNotEqual(0, result.returncode, arguments)
                self.assertEqual(b"", result.stdout)
        finally:
            if policy_created:
                policy_path.unlink()
            shutil.rmtree(helper_root)

    def test_ci_pins_sudo_rs_compatibility_execution(self) -> None:
        workflow = PHASE0_CI_WORKFLOW.read_text(encoding="utf-8")
        for required in (
            "Exercise root-helper policy under pinned sudo-rs 0.2.8",
            '"Sudo version "*)',
            "sudo-0.2.8.tar.gz",
            "71633a32d1b6d12428e95112920570a87bf6bbd2a005d81e9bd87371aa23e2d5",
            'test "$($sudo_root/bin/sudo --version 2>&1 | sed -n \'1p\')" = "sudo-rs 0.2.8"',
            'BOLT_PHASE0_SUDO_BINARY="$sudo_root/bin/sudo"',
            'BOLT_PHASE0_VISUDO_BINARY="$sudo_root/bin/visudo"',
            "WatchdogContractTests.test_sudo_policy_preserves_stdin_and_denies_extra_arguments",
        ):
            self.assertIn(required, workflow)

    def test_bootstrap_installs_and_root_helper_checks_effective_systemd_units(self) -> None:
        bootstrap = BOOTSTRAP.read_text(encoding="utf-8")
        content = ROOT_HELPER.read_text(encoding="utf-8")
        for required in (
            '"DropInPaths"',
            '"FragmentPath"',
            '"Unit"',
            '"ExecCondition"',
            '"ExecStartPre"',
            '"ExecStartPost"',
            '"User"',
            '"Group"',
            '"SupplementaryGroups"',
            '"ProtectKernelTunables"',
            '"ProtectKernelModules"',
            '"ProtectControlGroups"',
            '"RestrictSUIDSGID"',
            '"OnBootUSec=30s"',
            '"OnUnitActiveUSec=30s"',
        ):
            self.assertIn(required, content)
        for required in (
            "#!/usr/bin/bash",
            "PATH=/usr/sbin:/usr/bin:/sbin:/bin",
            "systemctl daemon-reload",
            "systemctl enable --now xframework-bolt-phase0-watchdog.timer",
            'source_root="${1:-}"',
            'bootstrap_source="${BASH_SOURCE[0]}"',
            'expected_bootstrap = source_root + "/deploy/bootstrap-xframework-bolt-phase0-root.sh"',
            'os.O_RDONLY | getattr(os, "O_CLOEXEC", 0) | getattr(os, "O_NOFOLLOW", 0)',
            "metadata.st_nlink != 1",
            "metadata.st_mode & 0o222",
            "def read_complete(descriptor: int, expected_size: int) -> bytes:",
            "except InterruptedError:",
            "request_size = min(64 * 1024, expected_size - total + 1)",
            "if total != expected_size:",
            "first != second",
            "signature(before) != signature(after)",
            "os.replace(temporary, name, src_dir_fd=parent_fd, dst_dir_fd=parent_fd)",
            "docker info --format '{{.ServerVersion}}'",
            "docker container ls -a --no-trunc --filter 'name=^/xframework-bolt-hub$' --format '{{.Names}}'",
            'install -d -o root -g "$deployment_group" -m 1770 "$protected_root"',
            'parent_fd = os.open(root, directory_flags)',
            'env_fd = os.open("xeon-dev.env", flags, dir_fd=parent_fd)',
            'getattr(os, "O_NOFOLLOW", 0)',
            "opened = os.fstat(env_fd)",
            "opened.st_nlink != 1",
            "os.fchown(env_fd, uid, gid)",
            "os.fchmod(env_fd, 0o600)",
            'os.stat("xeon-dev.env", dir_fd=parent_fd, follow_symlinks=False)',
            'lease_lock="$libexec_root/deployment-lease.lock"',
            'deployment_lease="$deploy_root/phase0-watchdog/deployment-lease.json"',
            'lkg_pointer="$deploy_root/phase0-last-known-good/current"',
            'source_binding_marker=bootstrap-source-binding.json',
            'exec {bootstrap_lock_fd}< "$lease_lock"',
            '/usr/bin/flock --exclusive --timeout 30 "$bootstrap_lock_fd"',
            'for active_state in "$deployment_lease" "$lkg_pointer"',
            '/usr/bin/python3 - "$deploy_root/runs" "$source_binding_marker" "$deployment_user"',
            'fail("Phase 0 bootstrap requires no prepared source-binding marker")',
            "/usr/bin/flock --unlock \"$bootstrap_lock_fd\"",
            '/usr/bin/python3 - "$lease_lock" "$deployment_group"',
            "os.O_WRONLY",
            "os.O_CREAT",
            "os.O_EXCL",
            "os.fchown(created, 0, lock_gid)",
            "os.fchmod(created, 0o440)",
            "stat.S_IMODE(opened.st_mode) != 0o440",
        ):
            self.assertIn(required, bootstrap)
        self.assertNotIn('chown "$deployment_user:$deployment_group" "$protected_env"', bootstrap)
        self.assertNotIn('chmod 0600 "$protected_env"', bootstrap)
        self.assertNotRegex(bootstrap, r'install .*\$source_root')
        self.assertNotIn("repository_root", bootstrap)
        self.assertIn("0o1770", content)
        self.assertIn("protected_parent.st_gid != self.deployment_gid", content)
        self.assertNotIn("systemctl cat", content + bootstrap)
        lock_acquired = bootstrap.index(
            '/usr/bin/flock --exclusive --timeout 30 "$bootstrap_lock_fd"'
        )
        state_checked = bootstrap.index(
            'for active_state in "$deployment_lease" "$lkg_pointer"'
        )
        marker_checked = bootstrap.index(
            '/usr/bin/python3 - "$deploy_root/runs" "$source_binding_marker" "$deployment_user"'
        )
        fixed_copy = bootstrap.index('/usr/bin/python3 - "$source_root" <<\'PY\'')
        units_enabled = bootstrap.index(
            "systemctl enable --now xframework-bolt-phase0-watchdog.timer"
        )
        lock_released = bootstrap.index(
            '/usr/bin/flock --unlock "$bootstrap_lock_fd"'
        )
        self_validation = bootstrap.index('"$root_helper" verify-bootstrap')
        self.assertLess(lock_acquired, state_checked)
        self.assertLess(state_checked, marker_checked)
        self.assertLess(marker_checked, fixed_copy)
        self.assertLess(fixed_copy, units_enabled)
        self.assertLess(units_enabled, lock_released)
        self.assertLess(lock_released, self_validation)

    @unittest.skipUnless(os.name == "posix", "POSIX bootstrap marker scan")
    def test_bootstrap_rejects_prepared_source_binding_marker(self) -> None:
        assert pwd is not None
        scanner = self._bootstrap_python_body(
            r'/usr/bin/python3 - "\$deploy_root/runs" "\$source_binding_marker" "\$deployment_user" <<\'PY\''
        )
        temporary_parent = Path("/root") if os.geteuid() == 0 else Path.home()
        with tempfile.TemporaryDirectory(
            prefix="phase0-bootstrap-marker-", dir=temporary_parent
        ) as temporary:
            run_root = Path(temporary) / "runs"
            run_root.mkdir(mode=0o700)
            run = run_root / "123-1"
            run.mkdir(mode=0o700)
            user = pwd.getpwuid(os.getuid()).pw_name
            command = [
                sys.executable,
                "-c",
                scanner,
                str(run_root),
                "bootstrap-source-binding.json",
                user,
            ]
            accepted = subprocess.run(command, capture_output=True, text=True)
            self.assertEqual(0, accepted.returncode, accepted.stderr)

            (run / "bootstrap-source-binding.json").write_text(
                "{}\n", encoding="ascii"
            )
            rejected = subprocess.run(command, capture_output=True, text=True)
            self.assertNotEqual(0, rejected.returncode)
            self.assertIn(
                "Phase 0 bootstrap requires no prepared source-binding marker",
                rejected.stderr,
            )

    @unittest.skipUnless(
        sys.platform.startswith("linux") and hasattr(os, "geteuid") and os.geteuid() == 0,
        "requires Linux root-owned installed-lock semantics",
    )
    def test_bootstrap_lock_creation_is_idempotent_and_preserves_inode(self) -> None:
        assert grp is not None
        creator = self._bootstrap_python_body(
            r'/usr/bin/python3 - "\$lease_lock" "\$deployment_group" <<\'PY\''
        )
        deployment_gid = int(os.environ.get("SUDO_GID", os.getgid()))
        deployment_group = grp.getgrgid(deployment_gid).gr_name
        with tempfile.TemporaryDirectory(prefix="phase0-bootstrap-lock-", dir="/root") as temporary:
            trust = Path(temporary) / "trust"
            Path(temporary).chmod(0o755)
            trust.mkdir(mode=0o755)
            lock_path = trust / "deployment-lease.lock"
            command = [sys.executable, "-c", creator, str(lock_path), deployment_group]

            first = subprocess.run(command, capture_output=True, text=True)
            self.assertEqual(0, first.returncode, first.stdout + first.stderr)
            before = lock_path.stat()
            second = subprocess.run(command, capture_output=True, text=True)
            self.assertEqual(0, second.returncode, second.stdout + second.stderr)
            after = lock_path.stat()

            self.assertEqual((before.st_dev, before.st_ino), (after.st_dev, after.st_ino))
            self.assertEqual(0, after.st_uid)
            self.assertEqual(deployment_gid, after.st_gid)
            self.assertEqual(0o440, stat.S_IMODE(after.st_mode))
            self.assertEqual(1, after.st_nlink)
            self.assertEqual(b"0", lock_path.read_bytes())

    @unittest.skipUnless(
        sys.platform.startswith("linux") and hasattr(os, "geteuid") and os.geteuid() == 0,
        "requires Linux root-owned installed-lock semantics",
    )
    def test_bootstrap_rejects_insecure_existing_lock_without_replacing_it(self) -> None:
        assert grp is not None
        creator = self._bootstrap_python_body(
            r'/usr/bin/python3 - "\$lease_lock" "\$deployment_group" <<\'PY\''
        )
        deployment_gid = int(os.environ.get("SUDO_GID", os.getgid()))
        deployment_group = grp.getgrgid(deployment_gid).gr_name
        for attack in ("symlink", "hardlink", "owner", "group", "mode"):
            with self.subTest(attack=attack), tempfile.TemporaryDirectory(
                prefix="phase0-bootstrap-lock-", dir="/root"
            ) as temporary:
                root = Path(temporary)
                root.chmod(0o755)
                trust = root / "trust"
                trust.mkdir(mode=0o755)
                lock_path = trust / "deployment-lease.lock"
                target = trust / "target"
                target.write_bytes(b"0")
                target.chmod(0o440)
                os.chown(target, 0, deployment_gid)
                if attack == "symlink":
                    lock_path.symlink_to(target.name)
                elif attack == "hardlink":
                    os.link(target, lock_path)
                else:
                    lock_path.write_bytes(b"0")
                    lock_path.chmod(0o440)
                    os.chown(lock_path, 0, deployment_gid)
                    if attack == "owner":
                        os.chown(lock_path, 65534, deployment_gid)
                    elif attack == "group":
                        wrong_gid = 65534 if deployment_gid != 65534 else 65533
                        os.chown(lock_path, 0, wrong_gid)
                    else:
                        lock_path.chmod(0o400)
                before = lock_path.lstat()
                result = subprocess.run(
                    [sys.executable, "-c", creator, str(lock_path), deployment_group],
                    capture_output=True,
                    text=True,
                )
                self.assertNotEqual(0, result.returncode)
                after = lock_path.lstat()
                self.assertEqual((before.st_dev, before.st_ino), (after.st_dev, after.st_ino))

    @staticmethod
    def _bootstrap_python_body(pattern: str) -> str:
        match = re.search(pattern + r"\n(?P<body>.*?)\nPY\n", BOOTSTRAP.read_text(encoding="utf-8"), re.DOTALL)
        if match is None:
            raise AssertionError(f"bootstrap Python block not found: {pattern}")
        return match.group("body")

    @staticmethod
    def _populate_root_staging(stage: Path) -> None:
        components = (
            "deploy/bootstrap-xframework-bolt-phase0-root.sh",
            "scripts/manage-bolt-phase0-root.py",
            "scripts/run-bolt-phase0-watchdog.sh",
            "scripts/manage-bolt-phase0-deployment-lease.py",
            "scripts/verify-bolt-phase0-qualification.py",
            "deploy/systemd/xframework-bolt-phase0-watchdog.service",
            "deploy/systemd/xframework-bolt-phase0-watchdog.timer",
        )
        stage.mkdir(mode=0o700)
        for relative in components:
            path = stage / relative
            path.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
            path.write_text(f"reviewed component: {relative}\n", encoding="utf-8")
            path.chmod(0o500 if relative.endswith("bootstrap-xframework-bolt-phase0-root.sh") else 0o400)
        for directory in sorted((path for path in stage.rglob("*") if path.is_dir()), reverse=True):
            directory.chmod(0o700)

    @unittest.skipUnless(
        sys.platform.startswith("linux") and hasattr(os, "geteuid") and os.geteuid() == 0,
        "requires Linux root-owned staging semantics",
    )
    def test_bootstrap_staging_accepts_reviewed_root_owned_nonwritable_bundle(self) -> None:
        validator = self._bootstrap_python_body(
            r'/usr/bin/python3 - "\$source_root" "\$bootstrap_source" <<\'PY\''
        )
        with tempfile.TemporaryDirectory(prefix="phase0-stage-", dir="/root") as temporary:
            stage = Path(temporary) / "bundle"
            self._populate_root_staging(stage)
            bootstrap_path = stage / "deploy/bootstrap-xframework-bolt-phase0-root.sh"
            result = subprocess.run(
                [sys.executable, "-c", validator, str(stage), str(bootstrap_path)],
                capture_output=True,
                text=True,
            )
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)

    @unittest.skipUnless(
        sys.platform.startswith("linux") and hasattr(os, "geteuid") and os.geteuid() == 0,
        "requires Linux root-owned staging semantics",
    )
    def test_bootstrap_staging_rejects_writable_parent_and_symlink_component(self) -> None:
        validator = self._bootstrap_python_body(
            r'/usr/bin/python3 - "\$source_root" "\$bootstrap_source" <<\'PY\''
        )
        for attack in ("writable-parent", "symlink-component"):
            with self.subTest(attack=attack), tempfile.TemporaryDirectory(
                prefix="phase0-stage-", dir="/root"
            ) as temporary:
                stage = Path(temporary) / "bundle"
                self._populate_root_staging(stage)
                bootstrap_path = stage / "deploy/bootstrap-xframework-bolt-phase0-root.sh"
                if attack == "writable-parent":
                    stage.chmod(0o770)
                else:
                    component = stage / "scripts/run-bolt-phase0-watchdog.sh"
                    component.unlink()
                    component.symlink_to(stage / "scripts/manage-bolt-phase0-root.py")
                result = subprocess.run(
                    [sys.executable, "-c", validator, str(stage), str(bootstrap_path)],
                    capture_output=True,
                    text=True,
                )
                self.assertNotEqual(0, result.returncode)
                self.assertTrue(result.stdout or result.stderr)

    @unittest.skipUnless(
        sys.platform.startswith("linux") and hasattr(os, "geteuid") and os.geteuid() == 0,
        "requires Linux root-owned staging semantics",
    )
    def test_bootstrap_component_copy_accepts_reviewed_stable_sources(self) -> None:
        copier = self._bootstrap_python_body(r'/usr/bin/python3 - "\$source_root" <<\'PY\'')
        with tempfile.TemporaryDirectory(prefix="phase0-stage-", dir="/root") as temporary:
            root = Path(temporary)
            stage = root / "bundle"
            destination = root / "installed"
            self._populate_root_staging(stage)
            destination.mkdir(mode=0o700)
            destination_paths = (
                "/usr/local/sbin/xframework-bolt-phase0-root",
                "/usr/local/sbin/xframework-bolt-phase0-watchdog",
                "/usr/local/libexec/xframework-bolt-phase0/manage-bolt-phase0-deployment-lease.py",
                "/usr/local/libexec/xframework-bolt-phase0/verify-bolt-phase0-qualification.py",
                "/etc/systemd/system/xframework-bolt-phase0-watchdog.service",
                "/etc/systemd/system/xframework-bolt-phase0-watchdog.timer",
            )
            source_paths = (
                "scripts/manage-bolt-phase0-root.py",
                "scripts/run-bolt-phase0-watchdog.sh",
                "scripts/manage-bolt-phase0-deployment-lease.py",
                "scripts/verify-bolt-phase0-qualification.py",
                "deploy/systemd/xframework-bolt-phase0-watchdog.service",
                "deploy/systemd/xframework-bolt-phase0-watchdog.timer",
            )
            short_read_hook = (
                "\n_real_os_read = os.read\n"
                "_forced_read_calls = 0\n"
                "def _forced_short_read(descriptor, size):\n"
                "    global _forced_read_calls\n"
                "    _forced_read_calls += 1\n"
                "    if _forced_read_calls % 5 == 0:\n"
                "        raise InterruptedError()\n"
                "    return _real_os_read(descriptor, min(size, 3))\n"
                "os.read = _forced_short_read\n"
            )
            rewritten = copier.replace(
                "\nroot_fd = open_directory(source_root)",
                short_read_hook + "\nroot_fd = open_directory(source_root)",
                1,
            )
            expected: list[Path] = []
            for index, original in enumerate(destination_paths):
                target = destination / f"component-{index}"
                expected.append(target)
                rewritten = rewritten.replace(original, str(target))
            result = subprocess.run(
                [sys.executable, "-c", rewritten, str(stage)],
                capture_output=True,
                text=True,
            )
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertTrue(all(path.is_file() and path.stat().st_uid == 0 for path in expected))
            for source, installed in zip(source_paths, expected, strict=True):
                self.assertEqual((stage / source).read_bytes(), installed.read_bytes())

    @unittest.skipUnless(
        sys.platform.startswith("linux") and hasattr(os, "geteuid") and os.geteuid() == 0,
        "requires Linux root-owned staging semantics",
    )
    def test_bootstrap_component_copy_rejects_premature_short_read_eof(self) -> None:
        copier = self._bootstrap_python_body(r'/usr/bin/python3 - "\$source_root" <<\'PY\'')
        definitions = copier.rsplit("\nroot_fd = open_directory(source_root)", 1)[0]
        truncated = definitions + (
            "\n_real_os_read = os.read\n"
            "_forced_read_calls = 0\n"
            "def _premature_eof(descriptor, size):\n"
            "    global _forced_read_calls\n"
            "    _forced_read_calls += 1\n"
            "    if _forced_read_calls == 2:\n"
            "        return b''\n"
            "    return _real_os_read(descriptor, min(size, 3))\n"
            "os.read = _premature_eof\n"
            "root_fd = open_directory(source_root)\n"
            "try:\n"
            "    read_component(root_fd, 'scripts/manage-bolt-phase0-root.py')\n"
            "finally:\n"
            "    os.close(root_fd)\n"
        )
        with tempfile.TemporaryDirectory(prefix="phase0-stage-", dir="/root") as temporary:
            stage = Path(temporary) / "bundle"
            self._populate_root_staging(stage)
            result = subprocess.run(
                [sys.executable, "-c", truncated, str(stage)],
                capture_output=True,
                text=True,
            )
            self.assertNotEqual(0, result.returncode)
            self.assertIn("bootstrap component byte count changed during read", result.stderr)

    @unittest.skipUnless(
        sys.platform.startswith("linux") and hasattr(os, "geteuid") and os.geteuid() == 0,
        "requires Linux root-owned staging semantics",
    )
    def test_bootstrap_component_copy_rejects_replacement_and_content_race(self) -> None:
        copier = self._bootstrap_python_body(r'/usr/bin/python3 - "\$source_root" <<\'PY\'')
        definitions = copier.rsplit("\nroot_fd = open_directory(source_root)", 1)[0]
        marker = "# TEST_COMPONENT_REPLACEMENT_WINDOW: the path and opened inode must remain identical."
        self.assertIn(marker, definitions)
        for attack, injected in (
            (
                "replacement",
                "os.rename(parts[-1], parts[-1] + '.opened', src_dir_fd=parent_fd, dst_dir_fd=parent_fd)\n"
                "            os.symlink(parts[-1] + '.opened', parts[-1], dir_fd=parent_fd)",
            ),
            (
                "content-race",
                "race_fd = os.open(parts[-1], os.O_WRONLY, dir_fd=parent_fd)\n"
                "            os.pwrite(race_fd, b'X', 0)\n"
                "            os.close(race_fd)",
            ),
        ):
            with self.subTest(attack=attack), tempfile.TemporaryDirectory(
                prefix="phase0-stage-", dir="/root"
            ) as temporary:
                stage = Path(temporary) / "bundle"
                self._populate_root_staging(stage)
                attacked = definitions.replace(marker, marker + "\n            " + injected)
                attacked += (
                    "\nroot_fd = open_directory(source_root)\n"
                    "try:\n"
                    "    read_component(root_fd, 'scripts/manage-bolt-phase0-root.py')\n"
                    "finally:\n"
                    "    os.close(root_fd)\n"
                )
                result = subprocess.run(
                    [sys.executable, "-c", attacked, str(stage)],
                    capture_output=True,
                    text=True,
                )
                self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
                self.assertIn("bootstrap component changed during copy", result.stderr)

    @unittest.skipUnless(
        os.name == "posix" and hasattr(os, "geteuid") and os.geteuid() == 0,
        "requires Linux root ownership semantics",
    )
    def test_bootstrap_env_replacement_cannot_redirect_root_metadata_changes(self) -> None:
        assert pwd is not None and grp is not None
        bootstrap = BOOTSTRAP.read_text(encoding="utf-8")
        match = re.search(
            r"python3 - \"\$protected_root\" .*? <<'PY'\n(?P<body>.*?)\nPY\n",
            bootstrap,
            re.DOTALL,
        )
        self.assertIsNotNone(match)
        body = match.group("body")
        marker = "# TEST_REPLACEMENT_WINDOW: descriptor operations below must remain path-independent."
        self.assertIn(marker, body)
        with tempfile.TemporaryDirectory() as temporary:
            parent = Path(temporary) / "protected"
            parent.mkdir(mode=0o770)
            os.chown(parent, 0, os.getgid())
            parent.chmod(0o1770)
            env_file = parent / "xeon-dev.env"
            env_file.write_text("SAFE=1\n", encoding="utf-8")
            victim = parent / "root-evidence"
            victim.write_text("SEALED\n", encoding="utf-8")
            victim.chmod(0o644)
            attacked = body.replace(
                marker,
                marker
                + "\nos.rename('xeon-dev.env', '.opened-env', src_dir_fd=parent_fd, dst_dir_fd=parent_fd)"
                + f"\nos.symlink({str(victim)!r}, 'xeon-dev.env', dir_fd=parent_fd)",
            )
            identity = pwd.getpwuid(os.getuid())
            group = grp.getgrgid(os.getgid())
            result = subprocess.run(
                [sys.executable, "-c", attacked, str(parent), identity.pw_name, group.gr_name],
                capture_output=True,
                text=True,
            )
            self.assertNotEqual(0, result.returncode)
            self.assertEqual("SEALED\n", victim.read_text(encoding="utf-8"))
            self.assertEqual(0o644, stat.S_IMODE(victim.stat().st_mode))

    def test_prequalification_preserves_installed_launcher_and_units(self) -> None:
        content = WORKFLOW.read_text(encoding="utf-8")
        helper = ROOT_HELPER.read_text(encoding="utf-8")
        verify = content.index("- name: Verify sealed watchdog bootstrap prerequisite")
        prepare_bound_run = content.index("prepare-bound-run", verify)
        arm = content.index("- name: Arm external deployment watchdog")
        activate = content.index("- name: Quarantine, root-qualify, seal, and activate recovery bundle")
        disarm = content.index("- name: Disarm external deployment watchdog after activation")
        self.assertLess(verify, arm)
        self.assertLess(prepare_bound_run, arm)
        self.assertLess(arm, activate)
        self.assertLess(activate, disarm)
        self.assertNotIn("install -o root", content)
        self.assertNotIn("systemctl daemon-reload", content)
        self.assertIn(
            "REMOTE_LEASE_MANAGER: /usr/local/libexec/xframework-bolt-phase0/manage-bolt-phase0-deployment-lease.py",
            content,
        )
        for required in (
            '"lease_manager_sha256": Path("scripts/manage-bolt-phase0-deployment-lease.py")',
            '"qualifier_sha256": Path("scripts/verify-bolt-phase0-qualification.py")',
            '"root_helper_sha256": Path("scripts/manage-bolt-phase0-root.py")',
            '"service_fragment_sha256": Path("deploy/systemd/xframework-bolt-phase0-watchdog.service")',
            '"timer_fragment_sha256": Path("deploy/systemd/xframework-bolt-phase0-watchdog.timer")',
            '"watchdog_sha256": Path("scripts/run-bolt-phase0-watchdog.sh")',
            'evidence.get("source_binding") != "passed"',
        ):
            self.assertIn(required, content)
        prepare_method = helper[
            helper.index("    def prepare_bound_run(") : helper.index(
                "    def _copy_quarantined("
            )
        ]
        lock = prepare_method.index("with exclusive_lease_lock(")
        binding = prepare_method.index(
            "self.verify_source_binding(expected, lease_lock_held=True)"
        )
        creation = prepare_method.index("os.mkdir(target, 0o700)")
        self.assertLess(lock, binding)
        self.assertLess(binding, creation)
        marker = prepare_method.index("target / SOURCE_BINDING_MARKER")
        self.assertLess(creation, marker)
        arm_step = content[arm:activate]
        self.assertIn("--require-bootstrap-source-binding", arm_step)

    def test_proxy_mode_is_preflighted_after_direct_publication_and_root_bound(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        root_helper = ROOT_HELPER.read_text(encoding="utf-8")
        compose_gate = workflow.index("python3 scripts/verify-bolt-phase0-compose.py")
        protected_gate = workflow.index("- name: Validate protected deployment inputs without mutation")
        self.assertLess(compose_gate, protected_gate)
        inventory, preflight = self._protected_deployment_preflight()
        self.assertEqual(PROTECTED_DEPLOYMENT_VARS, inventory)
        self.assertEqual(58, len(inventory))
        for required in (
            'flags = os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC | os.O_NONBLOCK',
            'descriptor = os.open(env_path, flags)',
            'opened = os.fstat(descriptor)',
            'before_path = path_metadata()',
            'after_path = path_metadata()',
            'first_content = read_opened_file(descriptor)',
            'second_content = read_opened_file(descriptor)',
            'first_content != second_content',
            'first_content.startswith(b"\\xef\\xbb\\xbf")',
            'maximum_protected_env_bytes = 1024 * 1024',
            'metadata.st_size > maximum_protected_env_bytes',
            'total > maximum_protected_env_bytes',
            'proxy_mode != "direct-kestrel"',
            '"BOLT_SYNTHETIC_PROXY_LOG_PATHS" in values',
        ):
            self.assertIn(required, preflight)
        self.assertEqual(1, preflight.count("os.open(env_path, flags)"))
        self.assertNotIn("read_text", preflight)
        self.assertNotIn('encoding="utf-8-sig"', preflight)
        self.assertIn('values.get("BOLT_SYNTHETIC_PROXY_MODE")', root_helper)
        self.assertIn('"--proxy-mode",', root_helper)
        self.assertIn("self._protected_proxy_mode()", root_helper)

    def test_controller_validation_precedes_remote_publication_authorization(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        self.assertIn("publication_topology_confirmation:", workflow)
        self.assertIn(
            "PUBLICATION_TOPOLOGY_ATTESTATION: ${{ inputs.publication_topology_confirmation }}",
            workflow,
        )
        self.assertIn("PROMOTION_ACTOR_ID: ${{ github.actor_id }}", workflow)
        self.assertIn("PROMOTION_TRIGGERING_ACTOR: ${{ github.triggering_actor }}", workflow)
        self.assertIn('test "$PROMOTION_RUN_ATTEMPT" = 1', workflow)
        self.assertIn('test "$PROMOTION_TRIGGERING_ACTOR" = "$PROMOTION_ACTOR"', workflow)
        step_start = workflow.index("- name: Authorize digest-pinned deployment manifest")
        step_end = workflow.index("- name: Pull candidate images without mutating services", step_start)
        step = workflow[step_start:step_end]
        local_start = step.index("python3 scripts/verify-bolt-phase0-compose.py")
        local_end = step.index('ssh -i "$DEPLOY_SSH_KEY"', local_start)
        local_invocation = step[local_start:local_end]
        remote_env_start = step.index('remote_env="')
        remote_environment = step[remote_env_start:step.index("\n", remote_env_start)]
        remote_start = step.index('python3 "$COMPOSE_VERIFIER"')
        remote_invocation = step[remote_start:step.index("REMOTE_SCRIPT", remote_start)]

        local_fields = (
            '--pins-file "$pins_file"',
            '--provenance-file "$provenance_file"',
            "--publication-host-context build-controller",
            "--authorize-deployment",
        )
        for field in local_fields:
            self.assertIn(field, local_invocation)
        self.assertNotIn("--publication-host-context deployment-host", local_invocation)
        self.assertNotIn("--publication-topology-", local_invocation)
        self.assertNotIn("--expected-public-hostname", local_invocation)

        remote_environment_fields = (
            "PUBLICATION_TOPOLOGY_ATTESTATION='$PUBLICATION_TOPOLOGY_ATTESTATION'",
            "PUBLICATION_TOPOLOGY_ATTESTED_BY='$PUBLICATION_TOPOLOGY_ATTESTED_BY'",
            "PUBLICATION_TOPOLOGY_ATTESTED_BY_ID='$PUBLICATION_TOPOLOGY_ATTESTED_BY_ID'",
            "PUBLICATION_TOPOLOGY_TRIGGERING_ACTOR='$PUBLICATION_TOPOLOGY_TRIGGERING_ACTOR'",
            "PUBLICATION_TOPOLOGY_RUN_ID='$GITHUB_RUN_ID'",
            "PUBLICATION_TOPOLOGY_RUN_ATTEMPT='$GITHUB_RUN_ATTEMPT'",
        )
        for field in remote_environment_fields:
            self.assertIn(field, remote_environment)

        remote_fields = (
            '--publication-topology-attestation "$PUBLICATION_TOPOLOGY_ATTESTATION"',
            '--publication-topology-attested-by "$PUBLICATION_TOPOLOGY_ATTESTED_BY"',
            '--publication-topology-attested-by-id "$PUBLICATION_TOPOLOGY_ATTESTED_BY_ID"',
            '--publication-topology-triggering-actor "$PUBLICATION_TOPOLOGY_TRIGGERING_ACTOR"',
            '--publication-topology-run-id "$PUBLICATION_TOPOLOGY_RUN_ID"',
            '--publication-topology-run-attempt "$PUBLICATION_TOPOLOGY_RUN_ATTEMPT"',
            "--publication-host-context deployment-host",
        )
        for field in remote_fields:
            self.assertIn(field, remote_invocation)
        self.assertNotIn("--publication-host-context build-controller", remote_invocation)

        self.assertIn('remote_evidence="$REMOTE_RUN_DIR/pinned-manifest-evidence.json"', step)
        self.assertIn('document.get("deployment_authorized") is not False', step)
        self.assertIn('context.get("detail") != {"context": "build-controller"}', step)
        self.assertIn('"direct-publication-host-interface" in checks', step)
        self.assertIn('document.get("deployment_authorized") is not True', step)
        self.assertIn('context.get("detail") != {"context": "deployment-host"}', step)
        self.assertIn('"direct-publication-host-interface",', step)
        self.assertIn('"operator-attested-direct-publication-topology",', step)
        self.assertIn('if [ "$status" -eq 0 ]; then', step)
        self.assertIn(
            '"$DEPLOY_HOST:$remote_evidence" "$evidence_dir/pinned-manifest-remote.json"\n'
            '            python3 - "$evidence_dir/pinned-manifest-remote.json"',
            step,
        )
        self.assertLess(
            step.index('python3 - "$evidence_dir/pinned-manifest-local.json"'),
            step.index('python3 "$COMPOSE_VERIFIER"'),
        )
        self.assertLess(
            step.index('python3 "$COMPOSE_VERIFIER"'),
            step.index('python3 - "$evidence_dir/pinned-manifest-remote.json"'),
        )

        pull = workflow.index("- name: Pull candidate images without mutating services")
        watchdog = workflow.index("- name: Arm external deployment watchdog")
        first_mutation = workflow.index("--mutation-began")
        self.assertLess(step_start, pull)
        self.assertLess(pull, watchdog)
        self.assertLess(watchdog, first_mutation)

    @unittest.skipUnless(
        sys.platform.startswith("linux")
        and hasattr(os, "O_NOFOLLOW")
        and hasattr(os, "O_CLOEXEC")
        and hasattr(os, "O_NONBLOCK"),
        "requires Linux descriptor and no-follow semantics",
    )
    def test_protected_deployment_preflight_executes_inventory_and_mode_matrix(self) -> None:
        inventory, preflight = self._protected_deployment_preflight()
        self.assertEqual(PROTECTED_DEPLOYMENT_VARS, inventory)

        direct_with_paths = self._protected_values("direct-kestrel")
        direct_with_paths["BOLT_SYNTHETIC_PROXY_LOG_PATHS"] = "/var/log/proxy.log"
        direct_with_empty_path_key = self._protected_values("direct-kestrel")
        direct_with_empty_path_key["BOLT_SYNTHETIC_PROXY_LOG_PATHS"] = ""
        missing_required = self._protected_values("direct-kestrel")
        missing_required.pop("JWT_SECRET")
        invalid_mode = self._protected_values("direct-kestrel")
        invalid_mode["BOLT_SYNTHETIC_PROXY_MODE"] = "auto"
        placeholder_required = self._protected_values("direct-kestrel")
        placeholder_required["JWT_SECRET"] = "change-me-in-production"

        cases = (
            ("direct", self._protected_values("direct-kestrel"), True, None, False, 0o600),
            (
                "logs",
                self._protected_values("logs"),
                False,
                "BOLT_SYNTHETIC_PROXY_MODE must be exactly direct-kestrel",
                False,
                0o600,
            ),
            (
                "direct-with-paths",
                direct_with_paths,
                False,
                "BOLT_SYNTHETIC_PROXY_LOG_PATHS must be absent",
                False,
                0o600,
            ),
            (
                "direct-with-empty-path-key",
                direct_with_empty_path_key,
                False,
                "BOLT_SYNTHETIC_PROXY_LOG_PATHS must be absent",
                False,
                0o600,
            ),
            (
                "missing-required",
                missing_required,
                False,
                "missing protected deployment variables: JWT_SECRET",
                False,
                0o600,
            ),
            (
                "invalid-mode",
                invalid_mode,
                False,
                "BOLT_SYNTHETIC_PROXY_MODE must be exactly",
                False,
                0o600,
            ),
            (
                "placeholder-required",
                placeholder_required,
                False,
                "JWT_SECRET contains a placeholder",
                False,
                0o600,
            ),
            (
                "utf8-bom",
                self._protected_values("direct-kestrel"),
                False,
                "must not contain a UTF-8 BOM or NUL",
                True,
                0o600,
            ),
            (
                "group-readable",
                self._protected_values("direct-kestrel"),
                False,
                "must not be group/world accessible",
                False,
                0o640,
            ),
        )
        for name, values, succeeds, message, bom, mode in cases:
            with self.subTest(name=name), tempfile.TemporaryDirectory() as temporary:
                env_file = Path(temporary) / "xeon-dev.env"
                self._write_protected_values(env_file, values, bom=bom)
                env_file.chmod(mode)
                result = subprocess.run(
                    [sys.executable, "-c", preflight, str(env_file), *inventory],
                    capture_output=True,
                    text=True,
                )
                self.assertEqual(succeeds, result.returncode == 0, result.stdout + result.stderr)
                if message is not None:
                    self.assertIn(message, result.stderr)

        with tempfile.TemporaryDirectory() as temporary:
            env_file = Path(temporary) / "xeon-dev.env"
            env_file.write_bytes(b"X" * (1024 * 1024 + 1))
            env_file.chmod(0o600)
            result = subprocess.run(
                [sys.executable, "-c", preflight, str(env_file), *inventory],
                capture_output=True,
                text=True,
            )
            self.assertNotEqual(0, result.returncode)
            self.assertIn("exceeds the size limit", result.stderr)

        for attack in ("symlink", "hardlink", "fifo"):
            with self.subTest(attack=attack), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                target = root / "target.env"
                self._write_protected_values(target, self._protected_values("direct-kestrel"))
                candidate = root / "xeon-dev.env"
                if attack == "symlink":
                    candidate.symlink_to(target.name)
                    expected = "must not be a symlink"
                else:
                    if attack == "hardlink":
                        os.link(target, candidate)
                        expected = "must have exactly one link"
                    else:
                        os.mkfifo(candidate, mode=0o600)
                        expected = "must be a regular file"
                result = subprocess.run(
                    [sys.executable, "-c", preflight, str(candidate), *inventory],
                    capture_output=True,
                    text=True,
                    timeout=5,
                )
                self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
                self.assertIn(expected, result.stderr)

    def test_workflows_use_runner_home_ssh_key_and_legacy_path_stays_disabled(self) -> None:
        active = WORKFLOW.read_text(encoding="utf-8")
        legacy = LEGACY_WORKFLOW.read_text(encoding="utf-8")
        expected = "DEPLOY_SSH_KEY: /home/xeon/.ssh/xframework_xeon_dev_ed25519"
        self.assertEqual(1, active.count(expected))
        self.assertEqual(1, legacy.count(expected))
        self.assertNotIn("/home/github-runner/.ssh", active)
        self.assertNotIn("/home/github-runner/.ssh", legacy)
        self.assertNotIn("ssh-keyscan", active)
        self.assertIn("if: ${{ false }}", legacy)

    def test_active_workflow_fail_closed_ssh_key_metadata_preflight(self) -> None:
        content = WORKFLOW.read_text(encoding="utf-8")
        setup = content[
            content.index("- name: Configure pinned SSH trust and operation bounds") :
            content.index("- name: Verify build runner, Docker, registry, and deploy host")
        ]
        for required in (
            'runner_home="/home/xeon"',
            'runner_ssh_dir="$runner_home/.ssh"',
            'expected_deploy_ssh_key="$runner_ssh_dir/xframework_xeon_dev_ed25519"',
            'test "$DEPLOY_SSH_KEY" = "$expected_deploy_ssh_key"',
            'test ! -L "$runner_home"',
            'test "$(readlink -e -- "$runner_home")" = "$runner_home"',
            'test ! -L "$runner_ssh_dir"',
            'test "$(readlink -e -- "$runner_ssh_dir")" = "$runner_ssh_dir"',
            "stat -Lc '%u:%g:%a' -- \"$runner_ssh_dir\"",
            'test "$ssh_dir_uid" = "$runner_uid"',
            'test "$ssh_dir_gid" = "$runner_gid"',
            'test "$ssh_dir_mode" = 700',
            'test ! -L "$DEPLOY_SSH_KEY"',
            'test "$(readlink -e -- "$DEPLOY_SSH_KEY")" = "$DEPLOY_SSH_KEY"',
            'exec {deploy_key_fd}< "$DEPLOY_SSH_KEY"',
            'deploy_key_fd_path="/proc/$$/fd/$deploy_key_fd"',
            'test -f "$deploy_key_fd_path"',
            "stat -Lc '%d:%i' -- \"$DEPLOY_SSH_KEY\"",
            "stat -Lc '%d:%i' -- \"$deploy_key_fd_path\"",
            "stat -Lc '%u:%g:%a:%h:%s' -- \"$deploy_key_fd_path\"",
            'test "$key_uid" = "$runner_uid"',
            'test "$key_gid" = "$runner_gid"',
            'test "$key_mode" = 600',
            'test "$key_nlink" = 1',
            'test "$key_size" -gt 0',
            'exec {deploy_key_fd}<&-',
        ):
            self.assertIn(required, setup)
        self.assertNotIn('test -r "$DEPLOY_SSH_KEY"', setup)

    def test_active_workflow_uses_run_scoped_distinct_local_tls_fixtures(self) -> None:
        content = WORKFLOW.read_text(encoding="utf-8")
        setup = content[
            content.index("- name: Validate Phase 0 Compose semantics") :
            content.index("- name: Validate protected deployment inputs without mutation")
        ]
        for required in (
            'local_tls_root="$RUNNER_TEMP/bolt-phase0-local-tls"',
            'test ! -e "$local_tls_root"',
            'test ! -L "$local_tls_root"',
            'install -d -m 700 "$local_tls_root"',
            "os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW | os.O_CLOEXEC",
            'stat.S_IMODE(opened.st_mode) != 0o600',
            'opened.st_nlink != 1',
            'opened.st_size != 0',
            'if len(identities) != len(names):',
            'export BOLT_HUB_TLS_CA_PATH BOLT_HUB_TLS_FULLCHAIN_PATH BOLT_HUB_TLS_PRIVATE_KEY_PATH',
            'export IDENTITYSERVER_TLS_CA_PATH IDENTITYSERVER_TLS_FULLCHAIN_PATH IDENTITYSERVER_TLS_PRIVATE_KEY_PATH',
            '} >> "$GITHUB_ENV"',
        ):
            self.assertIn(required, setup)
        for name in (
            "bolt-hub-ca.crt",
            "bolt-hub-fullchain.pem",
            "bolt-hub-private-key.pem",
            "identityserver-ca.crt",
            "identityserver-fullchain.pem",
            "identityserver-private-key.pem",
        ):
            self.assertEqual(2, setup.count(name))

    def test_active_workflow_writes_only_to_prepared_deployment_children(self) -> None:
        content = WORKFLOW.read_text(encoding="utf-8")
        direct_children = set(
            re.findall(r"\$REMOTE_DEPLOY_DIR/([A-Za-z0-9_.${}-]+)", content)
        )

        self.assertEqual({"hooks"}, direct_children)
        self.assertNotIn("mkdir -p '$REMOTE_DEPLOY_DIR'", content)
        self.assertNotIn("install -d -m 700 '$REMOTE_RUN_DIR'", content)
        self.assertNotIn('install -d -m 700 "$REMOTE_RUN_DIR"', content)
        self.assertGreaterEqual(
            content.count("stat -Lc '%u:%g:%a:%F' -- \"$REMOTE_RUN_DIR\""), 3
        )
        self.assertIn(
            "REMOTE_ROTATION_STATE: /home/github-runner/xframework-deploy/runs/${{ github.run_id }}-${{ github.run_attempt }}/phase0-rotation-state.json",
            content,
        )
        self.assertNotIn(
            "REMOTE_ROTATION_STATE: /home/github-runner/xframework-deploy/phase0-rotation-state.json",
            content,
        )
        for required in (
            'remote_candidate="$REMOTE_RUN_DIR/docker-compose.${IMAGE_TAG}.candidate.yml"',
            'remote_compose_verifier="$REMOTE_RUN_DIR/verify-bolt-phase0-compose.py"',
            'remote_tls_verifier="$REMOTE_RUN_DIR/verify-bolt-phase0-tls.sh"',
            'remote_identity_tls_verifier="$REMOTE_RUN_DIR/verify-identityserver-phase0-tls.sh"',
            'remote_verifier="$REMOTE_RUN_DIR/verify-bolt-phase0-observation.py"',
            'remote_verifier="$REMOTE_RUN_DIR/verify-bolt-phase0-credential-convergence.py"',
            '"rm -f \'$remote_compose_verifier\' \'$remote_tls_verifier\' \'$remote_identity_tls_verifier\' \'$remote_compose_evidence\'"',
            '"rm -f \'$remote_verifier\' \'$remote_input\' \'$remote_samples\' \'$remote_started\'"',
            '"rm -f \'$remote_verifier\'"',
            'rm -f -- "$REMOTE_ROTATION_STATE"',
        ):
            self.assertIn(required, content)
        self.assertNotIn("mutation-started", content)
        self.assertNotIn(
            "$REMOTE_DEPLOY_DIR/phase0-watchdog/deployment-lease-disarm.json", content
        )
        self.assertNotIn(
            "$REMOTE_DEPLOY_DIR/phase0-watchdog/workflow-recovery-evidence.json",
            content,
        )

    def test_build_and_local_receipts_use_profile_and_open_fd_contracts(self) -> None:
        content = WORKFLOW.read_text(encoding="utf-8")

        self.assertIn(
            'docker compose -f "$COMPOSE_FILE_PATH" --profile "*" config --format json',
            content,
        )
        self.assertIn(
            'docker compose -f "$COMPOSE_FILE_PATH" --profile phase0-verification build "${services[@]}"',
            content,
        )
        self.assertNotIn(
            'docker compose -f "$COMPOSE_FILE_PATH" config --format json',
            content,
        )
        self.assertNotIn(
            "stat -c '%u:%a:%h:%F'",
            content,
        )
        self.assertNotIn("local_temporary", content)
        self.assertEqual(3, content.count("set -o noclobber"))
        self.assertEqual(3, content.count("set +o noclobber"))
        self.assertEqual(3, content.count('exec {receipt_fd}> "$local_evidence"'))
        self.assertGreaterEqual(content.count('>&"$receipt_fd"'), 4)
        self.assertGreaterEqual(
            content.count("stat -Lc '%d:%i' -- \"$receipt_fd_path\""),
            6,
        )
        self.assertGreaterEqual(
            content.count("stat -c '%d:%i' -- \"$local_evidence\""),
            6,
        )
        self.assertGreaterEqual(content.count("exec {receipt_fd}>&-"), 6)
        for name in ("activation", "disarm", "recovery"):
            self.assertIn(f"validate_{name}_receipt()", content)
            self.assertIn(f"cleanup_{name}_receipt()", content)
            self.assertIn(
                f"{name} evidence path changed while its descriptor was open",
                content,
            )
        self.assertNotIn('rm -f -- "$local_evidence"', content)
        self.assertIn("recovery-evidence-capture-failed", content)
        self.assertIn("}' >&\"$receipt_fd\"", content)

    def test_workflow_pins_compatible_cosign_contract_before_build(self) -> None:
        content = WORKFLOW.read_text(encoding="utf-8")
        install_start = content.index("- name: Install Cosign")
        contract_start = content.index("- name: Verify Cosign CLI contract")
        build_start = content.index("- name: Build images on buildserver")
        provenance_start = content.index("- name: Sign and verify build provenance")
        provenance_end = content.index(
            "- name: Authorize digest-pinned deployment manifest",
            provenance_start,
        )

        self.assertLess(install_start, contract_start)
        self.assertLess(contract_start, build_start)
        install_block = content[install_start:contract_start]
        self.assertIn(
            "sigstore/cosign-installer@6f9f17788090df1f26f669e9d70d6ae9567deba6",
            install_block,
        )
        self.assertIn("# v4.1.2", install_block)
        self.assertIn("cosign-release: v3.1.1", install_block)

        contract_block = content[contract_start:build_start]
        for required in (
            'expected_version = "v3.1.1"',
            '["cosign", "version", "--json"]',
            '"attest": ("--bundle", "--predicate", "--type", "--yes")',
            '"verify-attestation": (',
            '"--certificate-identity"',
            '"--certificate-oidc-issuer"',
            '"--certificate-github-workflow-repository"',
            '"--certificate-github-workflow-ref"',
            '"--certificate-github-workflow-sha"',
            '"--certificate-github-workflow-trigger"',
            'for command in ("attest", "verify-attestation"):',
            '["cosign", command, "--new-bundle-format=true", "--help"]',
        ):
            self.assertIn(required, contract_block)

        provenance_block = content[provenance_start:provenance_end]
        self.assertIn(
            'cosign attest --yes --new-bundle-format=true --type slsaprovenance1',
            provenance_block,
        )
        self.assertIn('cosign verify-attestation \\', provenance_block)
        self.assertEqual(2, provenance_block.count("--new-bundle-format=true"))

    def test_active_workflow_uses_checked_in_pinned_known_hosts_and_exact_ssh_config(self) -> None:
        content = WORKFLOW.read_text(encoding="utf-8")
        pinned_lines = [
            line
            for line in PINNED_KNOWN_HOSTS.read_text(encoding="utf-8").splitlines()
            if line.strip()
        ]
        self.assertEqual(1, len(pinned_lines))
        self.assertRegex(pinned_lines[0], r"^xeon-dev ssh-ed25519 [A-Za-z0-9+/]+={0,2}$")
        self.assertIn('source_known_hosts=".github/known_hosts/xeon-dev"', content)
        expected_config = """          Host xeon-dev
            HostName xeon-dev
            User github-runner
            IdentityFile $DEPLOY_SSH_KEY
            IdentitiesOnly yes
            BatchMode yes
            PreferredAuthentications publickey
            PasswordAuthentication no
            KbdInteractiveAuthentication no
            StrictHostKeyChecking yes
            UserKnownHostsFile $known_hosts
            GlobalKnownHostsFile /dev/null
            HostKeyAlgorithms ssh-ed25519
            ConnectTimeout 10
            ConnectionAttempts 2
            ServerAliveInterval 15
            ServerAliveCountMax 3
            ControlMaster no
            ClearAllForwardings yes
            RequestTTY no
          EOF"""
        self.assertIn(expected_config, content)

    def test_rotation_bootstrap_mutation_occurs_only_after_prepare_and_lease_arm(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        prepare_run = workflow.index(
            '"sudo -n \'$REMOTE_ROOT_HELPER\' prepare-bound-run"'
        )
        validation = workflow.index("- name: Validate credential bootstrap inputs without mutation")
        arm = workflow.index("- name: Arm external deployment watchdog")
        bootstrap = workflow.index("- name: Bootstrap current credential generation under lease")
        prepare_generation = workflow.index("- name: Prepare bounded validation-only credential generation G+1")
        self.assertLess(prepare_run, validation)
        self.assertLess(validation, arm)
        self.assertLess(arm, bootstrap)
        self.assertLess(bootstrap, prepare_generation)

        prelease = workflow[:arm]
        self.assertIn('python3 "$REMOTE_MANAGER" validate-bootstrap', prelease)
        self.assertNotRegex(prelease, r'python3 "\$REMOTE_MANAGER" bootstrap\b')
        self.assertNotIn("phase0-rotation.lock", prelease)

        block = workflow[bootstrap:prepare_generation]
        for required in (
            "supervise --run-id",
            "--phase credential-bootstrap --mutation-began",
            "--timeout-seconds 540 --quiet --",
            "/usr/bin/timeout --foreground --kill-after=30s 510s",
            'python3 "$REMOTE_MANAGER" bootstrap',
        ):
            self.assertIn(required, block)

    def test_dynamic_root_requests_use_canonical_stdin_not_sudo_arguments(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        helper = ROOT_HELPER.read_text(encoding="utf-8")

        self.assertNotIn("prepare-bound-run '$GITHUB_RUN_ID'", workflow)
        self.assertNotIn("activate '$GITHUB_RUN_ID'", workflow)
        self.assertIn(
            '"sudo -n \'$REMOTE_ROOT_HELPER\' prepare-bound-run"',
            workflow,
        )
        self.assertIn(
            '"$DEPLOY_HOST" "sudo -n \'$REMOTE_ROOT_HELPER\' activate" '
            '>&"$receipt_fd"',
            workflow,
        )
        for required in (
            'exec {receipt_fd}> "$local_evidence"',
            "set -o noclobber",
            '"$(id -u):600:1"',
            'test ! -L "$local_evidence"',
            "validate_activation_receipt",
            "stat -Lc '%d:%i' -- \"$receipt_fd_path\"",
            "stat -c '%d:%i' -- \"$local_evidence\"",
            "exec {receipt_fd}>&-",
        ):
            self.assertIn(required, workflow)
        self.assertNotIn("root-activation-evidence.json.tmp", workflow)
        self.assertEqual(
            1,
            workflow.count("printf '%s\\n' \"$source_binding_request\" | ssh"),
        )
        self.assertEqual(1, workflow.count("printf '%s\\n' \"$request\" | ssh"))
        for required in (
            "ROOT_REQUEST_MAX_BYTES = 2048",
            "ROOT_REQUEST_TIMEOUT_SECONDS = 5.0",
            "raw.count(b\"\\n\") != 1",
            "object_pairs_hook=exact_object",
            'separators=(",", ":")',
            "sort_keys=True",
            '{"run_id", "run_attempt", *SOURCE_BINDING_FIELDS}',
        ):
            self.assertIn(required, helper)

    def test_dropins_and_redirecting_exec_hooks_are_explicitly_rejected(self) -> None:
        content = ROOT_HELPER.read_text(encoding="utf-8")
        self.assertIn('(service, "DropInPaths"): ""', content)
        self.assertIn('(timer, "DropInPaths"): ""', content)
        for property_name in ("ExecCondition", "ExecStartPre", "ExecStartPost"):
            self.assertIn(property_name, content)

    def test_workflow_sudo_is_limited_to_fixed_root_helper_and_failure_ssh_is_pinned(self) -> None:
        content = WORKFLOW.read_text(encoding="utf-8")
        sudo_lines = [line.strip() for line in content.splitlines() if "sudo -n" in line]
        self.assertEqual(4, len(sudo_lines))
        self.assertTrue(
            all(
                "$REMOTE_ROOT_HELPER" in line
                or "/usr/local/sbin/xframework-bolt-phase0-root ensure-watchdog" in line
                for line in sudo_lines
            )
        )
        for forbidden in ("sudo -n python", "sudo -n bash", "sudo -n sh", "sudo -n install", "sudo -n systemctl"):
            self.assertNotIn(forbidden, content)
        failure = content[content.index("- name: Restore security-qualified deployment or stop Bolt"):]
        self.assertIn("steps.ssh_setup.outcome == 'success'", failure)
        self.assertIn('-o StrictHostKeyChecking=yes -o UserKnownHostsFile="$DEPLOY_KNOWN_HOSTS"', failure)
        self.assertEqual(2, failure.count("xframework-bolt-phase0-root ensure-watchdog"))

    def test_every_synthetic_invocation_has_a_supervised_lease_heartbeat_contract(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        synthetic = SYNTHETICS.read_text(encoding="utf-8")
        self.assertEqual(6, workflow.count("bash scripts/run-bolt-phase0-synthetics.sh"))
        for required in (
            "BOLT_PHASE0_LEASE_RUN_ID: ${{ github.run_id }}",
            "BOLT_PHASE0_LEASE_RUN_ATTEMPT: ${{ github.run_attempt }}",
            "BOLT_PHASE0_LEASE_HEARTBEAT_SECONDS: 30",
            "REMOTE_LEASE_MANAGER: /usr/local/libexec/xframework-bolt-phase0/manage-bolt-phase0-deployment-lease.py",
        ):
            self.assertIn(required, workflow)
        for required in (
            "lease_heartbeat_loop &",
            "REMOTE_ENV_PARSER",
            '"$XFRAMEWORK_ENV_FILE" "$REMOTE_ENV_PARSER" "$REMOTE_RUN_DIR"',
            'kill -TERM "$synthetic_parent_pid"',
            'if [ -e "$heartbeat_failed" ] || ! kill -0 "$heartbeat_pid"',
            '"synthetic-$STAGE"',
            "stopped unexpectedly",
            "0:555",
            '/usr/bin/timeout --foreground --kill-after=10s 60s docker logs',
            '/usr/bin/timeout --foreground --kill-after=10s 30s "${compose[@]}" ps -q',
            'supervise --run-id "$LEASE_RUN_ID" --run-attempt "$LEASE_RUN_ATTEMPT" --phase "redis-$STAGE" --mutation-began',
            "--timeout-seconds 360 --quiet --",
            "/usr/bin/timeout --signal=TERM --kill-after=5s 300s",
        ):
            self.assertIn(required, synthetic)
        self.assertNotIn("REMOTE_DEPLOY_DIR", synthetic)
        self.assertLess(synthetic.index("lease_heartbeat_loop &"), synthetic.index("lock_file="))

    def test_every_long_readiness_stage_has_a_fail_closed_heartbeat_supervisor(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        stages = (
            ("- name: Verify Hub TLS and plaintext boundary", "- name: Verify staged runtime after Hub deployment"),
            ("- name: Wait for canary readiness", "- name: Verify staged runtime after canary deployment"),
            ("- name: Wait for readiness", "- name: Capture retiring generation token evidence"),
        )
        for start, end in stages:
            with self.subTest(stage=start):
                block = workflow[workflow.index(start):workflow.index(end)]
                for required in (
                    "heartbeat_loop & heartbeat_pid=$!",
                    "trap cleanup_heartbeat EXIT",
                    "trap 'exit 1' TERM INT HUP",
                    'kill -TERM "$heartbeat_parent_pid"',
                    'kill -0 "$heartbeat_pid"',
                    'wait "$heartbeat_pid"',
                    'rm -f "$heartbeat_failed" "$heartbeat_ready"',
                    "lease heartbeat stopped unexpectedly",
                ):
                    self.assertIn(required, block)
                self.assertLess(
                    block.index("heartbeat_loop & heartbeat_pid=$!"),
                    block.index("for attempt"),
                )

        self.assertEqual(3, workflow.count("trap cleanup_heartbeat EXIT"))
        batch = workflow[
            workflow.index("- name: Promote remaining clients in bounded batches"):
            workflow.index("- name: Wait for readiness")
        ]
        self.assertIn("for attempt in $(seq 1 60); do\n              heartbeat", batch)

    def test_all_long_mutation_paths_use_fixed_supervision_and_bounded_docker(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        stages = (
            ("- name: Run migration before service mutation", "- name: Deploy Bolt Hub only"),
            ("- name: Deploy Bolt Hub only", "- name: Verify Hub TLS and plaintext boundary"),
            ("- name: Deploy IdentityServer and Communications canary", "- name: Wait for canary readiness"),
            ("- name: Promote remaining clients in bounded batches", "- name: Wait for readiness"),
            ("- name: Roll G+1 through Hub, canary, and client batches", "- name: Prove G+1 runtime convergence"),
            ("- name: Restart without fallback credentials and rerun synthetics", "- name: Prove finalized credential convergence"),
            ("- name: Exercise digest-pinned rollback under G+1", "- name: Quarantine, root-qualify, seal, and activate recovery bundle"),
        )
        for start, end in stages:
            with self.subTest(stage=start):
                block = workflow[workflow.index(start):workflow.index(end)]
                self.assertIn(" supervise --run-id ", block)
                self.assertIn("--timeout-seconds", block)
                self.assertIn("/usr/bin/timeout --foreground --kill-after=", block)
        compose_mutations = list(re.finditer(r'^\s+"\$\{compose\[@\]\}" up ', workflow, re.MULTILINE))
        self.assertGreaterEqual(len(compose_mutations), 7)
        for mutation in compose_mutations:
            prefix = workflow[max(0, mutation.start() - 600):mutation.start()]
            self.assertIn("supervise --run-id", prefix)
            self.assertIn("/usr/bin/timeout", prefix)
        self.assertGreaterEqual(workflow.count("supervise --run-id"), 8)
        self.assertGreaterEqual(workflow.count("heartbeat\n              health="), 4)
        leased = workflow[
            workflow.index("- name: Arm external deployment watchdog"):
            workflow.index("- name: Quarantine, root-qualify, seal, and activate recovery bundle")
        ]
        for line in leased.splitlines():
            if re.search(r"docker (inspect|exec|cp|run)", line):
                self.assertIn("/usr/bin/timeout", line, f"unbounded Docker command: {line}")

    def test_runtime_and_rotation_operations_are_supervised_below_lease_expiry(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        leased = workflow[
            workflow.index("- name: Arm external deployment watchdog"):
            workflow.index("- name: Quarantine, root-qualify, seal, and activate recovery bundle")
        ]
        runtime_invocations = [
            line for line in leased.splitlines()
            if "verify-bolt-phase0-runtime.py" in line or "'$remote_verifier' --compose-file" in line
        ]
        self.assertGreaterEqual(len(runtime_invocations), 5)
        for line in runtime_invocations:
            if "py_compile" in line or "scp " in line or line.strip().startswith("scripts/"):
                continue
            self.assertIn(" supervise ", line)
            self.assertIn("--timeout-seconds 540", line)
            self.assertIn("/usr/bin/timeout", line)
        for operation in ("prepare", "activate", "finalize"):
            match = re.search(
                rf"supervise --run-id .*?--timeout-seconds 540 --quiet -- /usr/bin/timeout .*?python3 \"\$REMOTE_MANAGER\" {operation}",
                leased,
                re.DOTALL,
            )
            self.assertIsNotNone(match, operation)

    def test_every_mutating_rotation_command_is_supervised_or_recovery_bounded(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        leased = workflow[
            workflow.index("- name: Arm external deployment watchdog"):
            workflow.index("- name: Quarantine, root-qualify, seal, and activate recovery bundle")
        ]
        for operation in ("bootstrap", "prepare", "activate", "verify-convergence-input", "finalize"):
            matches = list(
                re.finditer(rf'python3 "\$REMOTE_MANAGER" {re.escape(operation)}\b', leased)
            )
            self.assertEqual(1, len(matches), operation)
            prefix = leased[max(0, matches[0].start() - 500):matches[0].start()]
            self.assertIn("supervise --run-id", prefix, operation)
            self.assertIn("/usr/bin/timeout", prefix, operation)
        recovery = LEASE_MANAGER.read_text(encoding="utf-8")
        abort = recovery[recovery.index("def _abort_prepared"):recovery.index("def _fingerprint")]
        self.assertIn('"abort-prepared"', abort)
        self.assertIn("self._invoke(", abort)
        self.assertIn("runner: Runner = default_runner", recovery)

    def test_bootstrap_does_not_treat_docker_daemon_failure_as_stopped(self) -> None:
        content = BOOTSTRAP.read_text(encoding="utf-8")
        self.assertIn('inspect_status=0', content)
        self.assertIn("docker info --format '{{.ServerVersion}}'", content)
        self.assertIn(
            "docker container ls -a --no-trunc --filter 'name=^/xframework-bolt-hub$' --format '{{.Names}}'",
            content,
        )
        self.assertIn('[ -n "$container_names" ]', content)
        self.assertNotIn("docker inspect --format '{{.State.Running}}' xframework-bolt-hub 2>/dev/null || true", content)

    def test_bootstrap_inspect_failure_requires_exact_absence_and_healthy_daemon(self) -> None:
        bash = self.bash_path()
        if bash is None:
            self.skipTest("bash is unavailable")
        for name, environment, expected_success in (
            (
                "present-container",
                {"INSPECT_CONTAINER_PRESENT": "true"},
                False,
            ),
            ("truly-absent", {}, True),
            ("daemon-failure", {"DOCKER_DAEMON_FAILURE": "true"}, False),
            ("empty-daemon-version", {"EMPTY_DAEMON_VERSION": "true"}, False),
        ):
            with self.subTest(name=name), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                docker_log = root / "docker.log"
                docker_log.write_text("", encoding="utf-8")
                docker = self.make_inspect_failure_docker(root)
                timeout_wrapper = self.make_timeout_wrapper(root)
                content = BOOTSTRAP.read_text(encoding="utf-8")
                content = content[: content.index("\ninstall -d")]
                content = content.replace('test "$(id -u)" -eq 0', ":")
                content = content.replace(
                    'case "$source_root:$bootstrap_source" in\n'
                    '  /*:/*) ;;\n'
                    '  *) echo "bootstrap and staging root must use absolute paths" >&2; exit 1 ;;\n'
                    'esac',
                    ":",
                )
                content = re.sub(
                    r'/usr/bin/python3 - "\$source_root" "\$bootstrap_source" <<\'PY\'\n.*?\nPY\n',
                    ":\n",
                    content,
                    count=1,
                    flags=re.DOTALL,
                )
                content = content.replace('id "$deployment_user" >/dev/null', ":")
                content = content.replace("/usr/bin/timeout", shlex.quote(timeout_wrapper.as_posix()))
                content = content.replace("/usr/bin/docker", shlex.quote(docker.as_posix()))
                preflight = root / "bootstrap-preflight.sh"
                preflight.write_text(content + "\n", encoding="utf-8", newline="\n")
                result = subprocess.run(
                    [bash, str(preflight), str(root)],
                    capture_output=True,
                    text=True,
                    env={**os.environ, "DOCKER_LOG": str(docker_log), **environment},
                )
                self.assertEqual(expected_success, result.returncode == 0, result.stderr)
                log = docker_log.read_text(encoding="utf-8")
                self.assertIn(
                    "container ls -a --no-trunc --filter name=^/xframework-bolt-hub$ --format {{.Names}}",
                    log,
                )

    def test_launcher_has_valid_bash_syntax_when_bash_is_available(self) -> None:
        bash = self.bash_path()
        if bash is None:
            self.skipTest("bash is unavailable")
        result = subprocess.run([bash, "-n", str(SCRIPT)], capture_output=True, text=True)
        self.assertEqual(0, result.returncode, result.stderr)

    @staticmethod
    def bash_path() -> str | None:
        bash = shutil.which("bash")
        if os.name == "nt":
            git_bash = (
                Path(os.environ.get("ProgramFiles", r"C:\Program Files"))
                / "Git"
                / "bin"
                / "bash.exe"
            )
            bash = str(git_bash) if git_bash.is_file() else None
        return bash

    def test_missing_or_tampered_lkg_helper_stops_bolt_traffic(self) -> None:
        bash = self.bash_path()
        if bash is None:
            self.skipTest("bash is unavailable")
        for mutation in ("missing", "tampered", "installed-launcher"):
            with self.subTest(mutation=mutation), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                launcher, run, docker_log = self.make_sandbox(root)
                runtime = run / "verify-bolt-phase0-runtime.py"
                if mutation == "missing":
                    runtime.unlink()
                elif mutation == "tampered":
                    runtime.write_text("#!/usr/bin/env python3\nraise SystemExit(99)\n", encoding="utf-8")
                    if os.name == "posix":
                        runtime.chmod(0o700)
                else:
                    launcher.write_text(
                        launcher.read_text(encoding="utf-8") + "\n# replaced after activation\n",
                        encoding="utf-8",
                    )
                result = subprocess.run(
                    [bash, str(launcher)],
                    capture_output=True,
                    text=True,
                    env={**os.environ, "DOCKER_LOG": str(docker_log)},
                )
                self.assertNotEqual(0, result.returncode)
                self.assertIn(
                    "stop --time 30 xframework-bolt-hub",
                    docker_log.read_text(encoding="utf-8"),
                )

    def test_force_recovery_dispatches_explicit_controller_command(self) -> None:
        bash = self.bash_path()
        if bash is None:
            self.skipTest("bash is unavailable")
        with tempfile.TemporaryDirectory() as temporary:
            launcher, _, docker_log = self.make_sandbox(Path(temporary))
            result = subprocess.run(
                [bash, str(launcher), "force-recovery"],
                capture_output=True,
                text=True,
                env={**os.environ, "DOCKER_LOG": str(docker_log)},
            )
            self.assertEqual(0, result.returncode, result.stderr)
            self.assertIn("force-recovery", self.manager_log.read_text(encoding="utf-8"))
            self.assertEqual("", docker_log.read_text(encoding="utf-8"))

    def test_no_lkg_running_hub_requires_fresh_validated_lease(self) -> None:
        bash = self.bash_path()
        if bash is None:
            self.skipTest("bash is unavailable")
        for lease_state, expected_success, expected_stop in (
            ("fresh", True, False),
            ("absent", True, True),
            ("stale", False, True),
            ("invalid", False, True),
        ):
            with self.subTest(lease_state=lease_state), tempfile.TemporaryDirectory() as temporary:
                launcher, docker_log = self.make_no_lkg_sandbox(Path(temporary))
                result = subprocess.run(
                    [bash, str(launcher)],
                    capture_output=True,
                    text=True,
                    env={
                        **os.environ,
                        "DOCKER_LOG": str(docker_log),
                        "HUB_RUNNING": "true",
                        "LEASE_STATE": lease_state,
                    },
                )
                self.assertEqual(expected_success, result.returncode == 0, result.stderr)
                log = docker_log.read_text(encoding="utf-8")
                if expected_stop:
                    self.assertIn("stop --time 30 xframework-bolt-hub", log)
                else:
                    self.assertNotIn("stop --time 30 xframework-bolt-hub", log)

        with tempfile.TemporaryDirectory() as temporary:
            launcher, docker_log = self.make_no_lkg_sandbox(Path(temporary))
            result = subprocess.run(
                [bash, str(launcher), "force-recovery"],
                capture_output=True,
                text=True,
                env={
                    **os.environ,
                    "DOCKER_LOG": str(docker_log),
                    "HUB_RUNNING": "true",
                    "LEASE_STATE": "fresh",
                },
            )
            self.assertEqual(0, result.returncode, result.stderr)
            self.assertIn(
                "stop --time 30 xframework-bolt-hub",
                docker_log.read_text(encoding="utf-8"),
            )

    def test_fail_closed_bounds_hanging_stop_and_advances_to_kill_and_verification(self) -> None:
        bash = self.bash_path()
        if bash is None:
            self.skipTest("bash is unavailable")
        with tempfile.TemporaryDirectory() as temporary:
            launcher, docker_log = self.make_no_lkg_sandbox(Path(temporary))
            started = time.monotonic()
            result = subprocess.run(
                [bash, str(launcher)],
                capture_output=True,
                text=True,
                timeout=5,
                env={
                    **os.environ,
                    "DOCKER_LOG": str(docker_log),
                    "HUB_RUNNING": "true",
                    "LEASE_STATE": "stale",
                    "HANG_STOP": "true",
                },
            )
            self.assertLess(time.monotonic() - started, 5)
            self.assertNotEqual(0, result.returncode)
            log = docker_log.read_text(encoding="utf-8")
            self.assertIn("timeout-stop", log)
            self.assertIn("kill xframework-bolt-hub", log)
            self.assertGreaterEqual(log.count("inspect --format"), 3)

    def test_fail_closed_bounds_hanging_kill_and_still_performs_final_verification(self) -> None:
        bash = self.bash_path()
        if bash is None:
            self.skipTest("bash is unavailable")
        with tempfile.TemporaryDirectory() as temporary:
            launcher, docker_log = self.make_no_lkg_sandbox(Path(temporary))
            started = time.monotonic()
            result = subprocess.run(
                [bash, str(launcher)],
                capture_output=True,
                text=True,
                timeout=5,
                env={
                    **os.environ,
                    "DOCKER_LOG": str(docker_log),
                    "HUB_RUNNING": "true",
                    "LEASE_STATE": "stale",
                    "HANG_STOP": "true",
                    "HANG_KILL": "true",
                },
            )
            self.assertLess(time.monotonic() - started, 5)
            self.assertNotEqual(0, result.returncode)
            log = docker_log.read_text(encoding="utf-8")
            self.assertIn("timeout-stop", log)
            self.assertIn("timeout-kill", log)
            self.assertGreaterEqual(log.count("inspect --format"), 3)

    def test_no_lkg_branch_dispatches_fixed_recovery_controller(self) -> None:
        content = SCRIPT.read_text(encoding="utf-8")
        for required in (
            "controller_command=watch-no-lkg",
            "controller_command=force-no-lkg",
            '--python-executable "$python"',
            '--docker-executable "$docker"',
        ):
            self.assertIn(required, content)
        self.assertNotIn("--rotation-state-file", content)
        lease_manager = LEASE_MANAGER.read_text(encoding="utf-8")
        self.assertIn('ROTATION_STATE_NAME = "phase0-rotation-state.json"', lease_manager)
        self.assertIn(
            "state_file = lease.run_directory / ROTATION_STATE_NAME", lease_manager
        )

    def test_launcher_requires_root_sealed_modes_and_global_digest_binding(self) -> None:
        content = SCRIPT.read_text(encoding="utf-8")
        for required in (
            "root_directory(deploy_root, 0o755)",
            "root_directory(run_root, 0o755)",
            "root_directory(run, 0o550)",
            'root_file(run / "qualification-evidence.json", 0o440',
            "root_file(installed_launcher, 0o555",
            "root_file(fixed_lease_manager, 0o555",
            'raw_by_name["run-bolt-phase0-watchdog.sh"]',
            'raw_by_name["manage-bolt-phase0-deployment-lease.py"]',
            "metadata.st_uid != 0",
            'evidence.get("proxy_mode") != "direct-kestrel"',
        ):
            self.assertIn(required, content)

    def test_launcher_requires_exact_qualification_evidence_proxy_mode(self) -> None:
        bash = self.bash_path()
        if bash is None:
            self.skipTest("bash is unavailable")
        cases = (
            ("logs", "logs", True, False),
            ("direct-kestrel", "direct-kestrel", True, True),
            ("missing", None, False, False),
            ("empty", "", True, False),
            ("case-variant", "Logs", True, False),
            ("wrong-separator", "direct_kestrel", True, False),
            ("null", None, True, False),
            ("boolean", True, True, False),
            ("collection", ["logs"], True, False),
        )
        for name, proxy_mode, present, expected_success in cases:
            with self.subTest(name=name), tempfile.TemporaryDirectory() as temporary:
                launcher, run, docker_log = self.make_sandbox(Path(temporary))
                evidence_path = run / "qualification-evidence.json"
                evidence = json.loads(evidence_path.read_text(encoding="utf-8"))
                if present:
                    evidence["proxy_mode"] = proxy_mode
                else:
                    evidence.pop("proxy_mode", None)
                evidence_path.write_text(json.dumps(evidence) + "\n", encoding="utf-8")
                result = subprocess.run(
                    [bash, str(launcher)],
                    capture_output=True,
                    text=True,
                    env={**os.environ, "DOCKER_LOG": str(docker_log)},
                )
                self.assertEqual(expected_success, result.returncode == 0, result.stderr)
                docker_output = docker_log.read_text(encoding="utf-8")
                if expected_success:
                    self.assertEqual("", docker_output)
                else:
                    self.assertIn("stop --time 30 xframework-bolt-hub", docker_output)

    def test_no_lkg_inspect_failure_requires_exact_absence_and_healthy_daemon(self) -> None:
        bash = self.bash_path()
        if bash is None:
            self.skipTest("bash is unavailable")
        for name, environment, expected_success in (
            (
                "present-container",
                {"INSPECT_FAILURE": "true", "INSPECT_CONTAINER_PRESENT": "true"},
                False,
            ),
            ("truly-absent", {"INSPECT_FAILURE": "true"}, True),
            (
                "daemon-failure",
                {"INSPECT_FAILURE": "true", "DOCKER_DAEMON_FAILURE": "true"},
                False,
            ),
        ):
            with self.subTest(name=name), tempfile.TemporaryDirectory() as temporary:
                launcher, docker_log = self.make_no_lkg_sandbox(Path(temporary))
                result = subprocess.run(
                    [bash, str(launcher)],
                    capture_output=True,
                    text=True,
                    env={**os.environ, "DOCKER_LOG": str(docker_log), **environment},
                )
                self.assertEqual(expected_success, result.returncode == 0, result.stderr)
                log = docker_log.read_text(encoding="utf-8")
                self.assertIn(
                    "container ls -a --no-trunc --filter name=^/xframework-bolt-hub$ --format {{.Names}}",
                    log,
                )
                if name == "truly-absent":
                    self.assertIn("info --format {{.ServerVersion}}", log)

    def make_sandbox(self, root: Path) -> tuple[Path, Path, Path]:
        deploy_root = root / "deploy"
        run = deploy_root / "runs" / "123456789-2"
        lkg = deploy_root / "phase0-last-known-good"
        run.mkdir(parents=True)
        lkg.mkdir(parents=True)
        if os.name == "posix":
            run.chmod(0o700)
            lkg.chmod(0o700)
        (lkg / "current").write_bytes((str(run) + "\n").encode("utf-8"))
        self.manager_log = root / "manager.log"
        docker_log = root / "docker.log"
        docker_log.write_text("", encoding="utf-8")
        manager = (
            "#!/usr/bin/env python3\n"
            "import os, pathlib, sys\n"
            "pathlib.Path(os.environ['MANAGER_LOG']).write_text(' '.join(sys.argv[1:]) + '\\n', encoding='utf-8')\n"
        ).encode()
        launcher = deploy_root / "run-bolt-phase0-watchdog.sh"
        fixed_manager = root / "fixed-lease-manager.py"
        fixed_manager.write_bytes(manager)
        python_wrapper = self.make_python_wrapper(root)
        timeout_wrapper = self.make_timeout_wrapper(root)
        content = SCRIPT.read_text(encoding="utf-8")
        content = content.replace(
            "deploy_root=/home/github-runner/xframework-deploy",
            f"deploy_root={shlex.quote(deploy_root.as_posix())}",
        )
        content = content.replace("python_link=/usr/bin/python3", f"python_link={shlex.quote(python_wrapper.as_posix())}")
        content = content.replace("docker=/usr/bin/docker", f"docker={shlex.quote((root / 'docker').as_posix())}")
        content = content.replace(
            "timeout=/usr/bin/timeout",
            f"timeout={shlex.quote(timeout_wrapper.as_posix())}",
        )
        content = content.replace("/usr/bin/timeout", shlex.quote(timeout_wrapper.as_posix()))
        content = content.replace("/usr/bin/docker", shlex.quote((root / "docker").as_posix()))
        content = content.replace(
            "installed_launcher=/usr/local/sbin/xframework-bolt-phase0-watchdog",
            f"installed_launcher={shlex.quote(launcher.as_posix())}",
        )
        content = content.replace(
            "fixed_lease_manager=/usr/local/libexec/xframework-bolt-phase0/manage-bolt-phase0-deployment-lease.py",
            f"fixed_lease_manager={shlex.quote(fixed_manager.as_posix())}",
        )
        content = content.replace("expected_user=github-runner", "expected_user=\"$(id -un)\"")
        content = self.relax_sandbox_root_checks(content)
        content = content.replace('os.name == "posix"', "False")
        launcher_raw = content.encode()
        executable_names = (
            "manage-bolt-phase0-deployment-lease.py",
            "manage-bolt-phase0-rotation.py",
            "verify-bolt-phase0-runtime.py",
            "verify-bolt-phase0-env.py",
            "verify-bolt-phase0-qualification.py",
            "run-bolt-phase0-recovery-synthetic.py",
            "refresh-bolt-phase0-synthetic-tokens.py",
            "run-bolt-phase0-marker-scan.py",
            "run-bolt-phase0-operational-probe.py",
            "run-bolt-phase0-post-recovery-durable.py",
            "run-bolt-phase0-watchdog.sh",
        )
        configuration_names = (
            "xframework-bolt-phase0-watchdog.service",
            "xframework-bolt-phase0-watchdog.timer",
        )
        artifacts = {}
        for name in executable_names:
            raw = (
                manager
                if name == "manage-bolt-phase0-deployment-lease.py"
                else launcher_raw
                if name == "run-bolt-phase0-watchdog.sh"
                else b"#!/usr/bin/env python3\n"
            )
            path = run / name
            path.write_bytes(raw)
            if os.name == "posix":
                path.chmod(0o700)
            artifacts[name] = {
                "path": name,
                "sha256": "sha256:" + hashlib.sha256(raw).hexdigest(),
                "schema": None,
                "generated_at_utc": None,
            }
        for name in configuration_names:
            raw = f"# {name}\n".encode()
            (run / name).write_bytes(raw)
            artifacts[name] = {
                "path": name,
                "sha256": "sha256:" + hashlib.sha256(raw).hexdigest(),
                "schema": None,
                "generated_at_utc": None,
            }
        evidence = {
            "schema": "xframework.bolt.phase0.qualification.v1",
            "status": "passed",
            "run_id": "123456789",
            "run_attempt": 2,
            "source_commit": "a" * 40,
            "proxy_mode": "direct-kestrel",
            "artifacts": artifacts,
            "errors": [],
        }
        (run / "qualification-evidence.json").write_text(
            json.dumps(evidence) + "\n", encoding="utf-8"
        )
        (run / "qualified-commit").write_bytes(("a" * 40 + "\n").encode("ascii"))
        (run / "security-qualified").write_bytes(b"")
        for path in (
            lkg / "current",
            run / "qualification-evidence.json",
            run / "qualified-commit",
            run / "security-qualified",
        ):
            if os.name == "posix":
                path.chmod(0o600)

        docker = root / "docker"
        docker.write_text(
            "#!/usr/bin/env bash\n"
            "printf '%s\\n' \"$*\" >> \"$DOCKER_LOG\"\n"
            "if [ \"$1\" = inspect ]; then echo false; fi\n"
            "exit 0\n",
            encoding="utf-8",
        )
        if os.name == "posix":
            docker.chmod(0o700)
        launcher.write_text(content, encoding="utf-8", newline="\n")
        if os.name == "posix":
            launcher.chmod(0o700)
        os.environ["MANAGER_LOG"] = str(self.manager_log)
        return launcher, run, docker_log

    def make_no_lkg_sandbox(self, root: Path) -> tuple[Path, Path]:
        deploy_root = root / "deploy"
        deploy_root.mkdir()
        launcher = root / "watchdog.sh"
        fixed_manager = root / "fixed-lease-manager.py"
        fixed_manager.write_text(
            "import os,sys\n"
            "args=sys.argv[1:]\n"
            "command=next((x for x in args if x in {'watch-no-lkg','force-no-lkg'}), '')\n"
            "if command == 'force-no-lkg':\n"
            " log=os.environ['DOCKER_LOG']\n"
            " open(log,'a',encoding='utf-8').write('stop --time 30 xframework-bolt-hub\\n')\n"
            " open(log+'.stopped','w',encoding='utf-8').close()\n"
            " raise SystemExit(0)\n"
            "state=os.environ.get('LEASE_STATE','absent')\n"
            "hub=os.environ.get('HUB_RUNNING','false') == 'true'\n"
            "if command == 'watch-no-lkg' and state == 'absent':\n"
            " log=os.environ['DOCKER_LOG']\n"
            " open(log,'a',encoding='utf-8').write('stop --time 30 xframework-bolt-hub\\n')\n"
            " open(log+'.stopped','w',encoding='utf-8').close()\n"
            " raise SystemExit(0)\n"
            "raise SystemExit(0 if command == 'watch-no-lkg' and state == 'fresh' else 1)\n",
            encoding="utf-8",
        )
        python_wrapper = self.make_python_wrapper(root)
        timeout_wrapper = self.make_timeout_wrapper(root)
        docker = root / "docker"
        docker.write_text(
            "#!/usr/bin/env bash\n"
            "printf '%s\\n' \"$*\" >> \"$DOCKER_LOG\"\n"
            "if [ \"${DOCKER_DAEMON_FAILURE:-false}\" = true ]; then exit 1; fi\n"
            "if [ \"$1\" = inspect ]; then\n"
            "  if [ \"${INSPECT_FAILURE:-false}\" = true ]; then exit 1; fi\n"
            "  if [ -e \"$DOCKER_LOG.stopped\" ]; then echo false; else echo \"${HUB_RUNNING:-false}\"; fi\n"
            "elif [ \"$1 $2\" = \"container ls\" ]; then\n"
            "  if [ \"${INSPECT_CONTAINER_PRESENT:-false}\" = true ] && [ ! -e \"$DOCKER_LOG.stopped\" ]; then echo xframework-bolt-hub; fi\n"
            "elif [ \"$1\" = info ]; then\n"
            "  if [ \"${EMPTY_DAEMON_VERSION:-false}\" != true ]; then echo 27.5.1; fi\n"
            "elif [ \"$1\" = stop ] || [ \"$1\" = kill ]; then\n"
            "  : > \"$DOCKER_LOG.stopped\"\n"
            "fi\n"
            "exit 0\n",
            encoding="utf-8",
        )
        content = SCRIPT.read_text(encoding="utf-8")
        replacements = {
            "deploy_root=/home/github-runner/xframework-deploy": f"deploy_root={shlex.quote(deploy_root.as_posix())}",
            "python_link=/usr/bin/python3": f"python_link={shlex.quote(python_wrapper.as_posix())}",
            "docker=/usr/bin/docker": f"docker={shlex.quote(docker.as_posix())}",
            "timeout=/usr/bin/timeout": f"timeout={shlex.quote(timeout_wrapper.as_posix())}",
            "expected_user=github-runner": 'expected_user="$(id -un)"',
            "installed_launcher=/usr/local/sbin/xframework-bolt-phase0-watchdog": f"installed_launcher={shlex.quote(launcher.as_posix())}",
            "fixed_lease_manager=/usr/local/libexec/xframework-bolt-phase0/manage-bolt-phase0-deployment-lease.py": f"fixed_lease_manager={shlex.quote(fixed_manager.as_posix())}",
        }
        for old, new in replacements.items():
            content = content.replace(old, new)
        content = content.replace("/usr/bin/timeout", shlex.quote(timeout_wrapper.as_posix()))
        content = content.replace("/usr/bin/docker", shlex.quote(docker.as_posix()))
        content = self.relax_sandbox_root_checks(content)
        launcher.write_text(content, encoding="utf-8", newline="\n")
        if os.name == "posix":
            launcher.chmod(0o700)
            docker.chmod(0o700)
            fixed_manager.chmod(0o555)
        docker_log = root / "docker.log"
        docker_log.write_text("", encoding="utf-8")
        return launcher, docker_log

    @staticmethod
    def relax_sandbox_root_checks(content: str) -> str:
        for check in (
            'test "$(stat -c \'%u\' "$python")" = 0',
            'test -x "$fixed_lease_manager"',
            'test "$(stat -c \'%u\' "$fixed_lease_manager")" = 0',
            'test "$(stat -c \'%a\' "$fixed_lease_manager")" = 555',
            'test "$(stat -c \'%u:%a\' "$(dirname "$fixed_lease_manager")")" = 0:755',
        ):
            content = content.replace(check, ":")
        return content

    @staticmethod
    def make_python_wrapper(root: Path) -> Path:
        wrapper = root / "python"
        wrapper.write_text(
            "#!/usr/bin/env bash\n"
            f"exec {shlex.quote(Path(sys.executable).as_posix())} \"$@\"\n",
            encoding="utf-8",
        )
        if os.name == "posix":
            wrapper.chmod(0o700)
        return wrapper

    @staticmethod
    def make_timeout_wrapper(root: Path) -> Path:
        wrapper = root / "timeout"
        wrapper.write_text(
            "#!/usr/bin/env bash\n"
            "while [[ \"$1\" == --* ]]; do shift; done\n"
            "shift\n"
            "operation=\"${2:-}\"\n"
            "if [ \"$operation\" = stop ] && [ \"${HANG_STOP:-false}\" = true ]; then\n"
            "  printf 'timeout-stop\\n' >> \"${DOCKER_LOG:-/dev/null}\"\n"
            "  exit 124\n"
            "fi\n"
            "if [ \"$operation\" = kill ] && [ \"${HANG_KILL:-false}\" = true ]; then\n"
            "  printf 'timeout-kill\\n' >> \"${DOCKER_LOG:-/dev/null}\"\n"
            "  exit 124\n"
            "fi\n"
            "exec \"$@\"\n",
            encoding="utf-8",
        )
        if os.name == "posix":
            wrapper.chmod(0o700)
        return wrapper

    @staticmethod
    def make_inspect_failure_docker(root: Path) -> Path:
        docker = root / "inspect-failure-docker"
        docker.write_text(
            "#!/usr/bin/env bash\n"
            "printf '%s\\n' \"$*\" >> \"$DOCKER_LOG\"\n"
            "if [ \"$1\" = inspect ]; then exit 1; fi\n"
            "if [ \"${DOCKER_DAEMON_FAILURE:-false}\" = true ]; then exit 1; fi\n"
            "if [ \"$1 $2\" = \"container ls\" ]; then\n"
            "  if [ \"${INSPECT_CONTAINER_PRESENT:-false}\" = true ]; then echo xframework-bolt-hub; fi\n"
            "elif [ \"$1\" = info ]; then\n"
            "  if [ \"${EMPTY_DAEMON_VERSION:-false}\" != true ]; then echo 27.5.1; fi\n"
            "fi\n",
            encoding="utf-8",
        )
        if os.name == "posix":
            docker.chmod(0o700)
        return docker


if __name__ == "__main__":
    unittest.main()
