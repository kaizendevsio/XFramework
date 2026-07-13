#!/usr/bin/env python3
from __future__ import annotations

import importlib.util
import json
import os
import subprocess
import tempfile
import unittest
from unittest import mock
from pathlib import Path


SCRIPT = Path(__file__).with_name("verify-bolt-phase0-runtime.py")
SPEC = importlib.util.spec_from_file_location("phase0_runtime", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


CONTAINER_ID = "b" * 64
IMAGE_ID = "sha256:" + "c" * 64
EXPECTED = "registry.example/xframework/bolt-hub@sha256:" + "d" * 64
PROJECT = "xframework"
TCP_HEADER = "  sl  local_address rem_address   st tx_queue rx_queue tr tm->when retrnsmt   uid  timeout inode\n"


class SubprocessBoundTests(unittest.TestCase):
    def test_default_runner_applies_internal_timeout_and_closed_stdin(self) -> None:
        completed = subprocess.CompletedProcess(["docker", "ps"], 0, "", "")
        with mock.patch.object(MODULE.subprocess, "run", return_value=completed) as run:
            self.assertIs(completed, MODULE.run(["docker", "ps"]))
        _, kwargs = run.call_args
        self.assertEqual(MODULE.SUBPROCESS_TIMEOUT_SECONDS, kwargs["timeout"])
        self.assertIs(subprocess.DEVNULL, kwargs["stdin"])
        self.assertTrue(kwargs["close_fds"])

    def test_default_runner_converts_timeout_to_bounded_failure(self) -> None:
        with mock.patch.object(
            MODULE.subprocess,
            "run",
            side_effect=subprocess.TimeoutExpired(["docker", "ps"], 30),
        ):
            result = MODULE.run(["docker", "ps"])
        self.assertEqual(124, result.returncode)
        self.assertIn("timed out", result.stderr)


def proc_tcp(health_scope: str = "loopback", extra_port: int | None = None) -> str:
    health_address = "0100007F" if health_scope == "loopback" else "00000000"
    lines = [
        TCP_HEADER.rstrip("\n"),
        f"   0: {health_address}:1F90 00000000:0000 0A 00000000:00000000 00:00000000 00000000 0 0 1",
        "   1: 00000000:20FB 00000000:0000 0A 00000000:00000000 00:00000000 00000000 0 0 2",
    ]
    if extra_port is not None:
        lines.append(
            f"   2: 0100007F:{extra_port:04X} 00000000:0000 0A 00000000:00000000 00:00000000 00000000 0 0 3"
        )
    return "\n".join(lines) + "\n"


def listener_runner(tcp: str):
    def run(command: list[str]) -> subprocess.CompletedProcess[str]:
        content = tcp if command[-1] == "/proc/net/tcp" else TCP_HEADER
        return subprocess.CompletedProcess(command, 0, content, "")

    return run


def inspector(
    private_key: Path,
    configured_image: str = EXPECTED,
    repo_digests: list[str] | None = None,
    health: str | None = "healthy",
    running: bool = True,
    ports: dict | None = None,
    mounts: list[dict] | None = None,
):
    bindings = {"8443/tcp": [{"HostIp": "0.0.0.0", "HostPort": "7443"}]}

    def inspect(command: list[str]) -> dict:
        if command[1:3] == ["image", "inspect"]:
            return {"local_image_id": IMAGE_ID, "repo_digests": repo_digests or [EXPECTED]}
        return {
            "container_name": "/xframework-bolt-hub",
            "container_id": CONTAINER_ID,
            "configured_image": configured_image,
            "local_image_id": IMAGE_ID,
            "started_at": "2026-07-13T10:11:12.123456789Z",
            "running": running,
            "status": "running" if running else "exited",
            "exit_code": 0,
            "health": health,
            "labels": {"com.docker.compose.project": PROJECT, "com.docker.compose.service": "bolt-hub"},
            "ports": ports if ports is not None else bindings,
            "port_bindings": ports if ports is not None else bindings,
            "mounts": mounts
            if mounts is not None
            else [
                {
                    "Type": "bind",
                    "Source": str(private_key),
                    "Destination": "/run/secrets/bolt-hub-tls-private-key.pem",
                    "RW": False,
                }
            ],
        }

    return inspect


class Phase0RuntimeTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name).resolve()
        self.private_key = self.root / "private-key.pem"
        self.private_key.write_text("private", encoding="ascii")
        self.identity_private_key = self.root / "identity-private-key.pem"
        self.identity_private_key.write_text("identity-private", encoding="ascii")

    def collect(self, **kwargs):
        tcp = kwargs.pop("tcp", proc_tcp())
        return MODULE.collect_service(
            "bolt-hub",
            CONTAINER_ID,
            EXPECTED,
            PROJECT,
            self.private_key,
            7443,
            inspector(self.private_key, **kwargs),
            listener_runner(tcp),
        )

    def test_valid_runtime_boundary_evidence_passes_without_environment_inspection(self) -> None:
        evidence, errors = self.collect()
        self.assertEqual([], errors)
        self.assertTrue(evidence["running"])
        self.assertEqual({8080, 8443}, {item["port"] for item in evidence["listeners"]})
        self.assertEqual(7443, evidence["published_port"]["published_port"])
        self.assertEqual("<expected-private-key>", evidence["private_key_mounts"][0]["resolved_source"])
        self.assertNotIn(".Config.Env", MODULE.CONTAINER_FORMAT)

    def test_stopped_unhealthy_or_wrongly_labeled_hub_fails_closed(self) -> None:
        _, stopped = self.collect(running=False, health="unhealthy")
        self.assertTrue(any("not running and healthy" in error for error in stopped))

        bad = inspector(self.private_key)

        def wrong_label(command: list[str]) -> dict:
            result = bad(command)
            if command[1:2] == ["inspect"]:
                result["labels"]["com.docker.compose.project"] = "attacker"
            return result

        _, errors = MODULE.collect_service(
            "bolt-hub",
            CONTAINER_ID,
            EXPECTED,
            PROJECT,
            self.private_key,
            7443,
            wrong_label,
            listener_runner(proc_tcp()),
        )
        self.assertTrue(any("identity labels" in error for error in errors))

    def test_public_health_listener_and_extra_listener_fail_closed(self) -> None:
        _, public_errors = self.collect(tcp=proc_tcp(health_scope="wildcard"))
        _, extra_errors = self.collect(tcp=proc_tcp(extra_port=9000))
        self.assertTrue(any("8080" in error for error in public_errors))
        self.assertTrue(any("unexpected actual" in error for error in extra_errors))

    def test_wrong_or_plaintext_published_port_fails_closed(self) -> None:
        wrong = {"8443/tcp": [{"HostIp": "0.0.0.0", "HostPort": "7000"}]}
        plaintext = {
            "8443/tcp": [{"HostIp": "0.0.0.0", "HostPort": "7443"}],
            "8080/tcp": [{"HostIp": "0.0.0.0", "HostPort": "8080"}],
        }
        for ports in (wrong, plaintext):
            with self.subTest(ports=ports):
                _, errors = self.collect(ports=ports)
                self.assertTrue(any("port" in error for error in errors))

    def test_parent_directory_and_symlink_key_mount_bypasses_fail_closed(self) -> None:
        parent_mount = [
            {"Type": "bind", "Source": str(self.root), "Destination": "/mnt/config", "RW": False}
        ]
        _, parent_errors = self.collect(mounts=parent_mount)
        self.assertTrue(any("private-key mount" in error for error in parent_errors))

        alias = self.root / "alias.pem"
        try:
            alias.symlink_to(self.private_key)
        except OSError:
            os.link(self.private_key, alias)
        alias_mount = [
            {
                "Type": "bind",
                "Source": str(alias),
                "Destination": "/run/secrets/bolt-hub-tls-private-key.pem",
                "RW": False,
            }
        ]
        evidence, errors = self.collect(mounts=alias_mount)
        self.assertEqual([], errors)
        self.assertEqual("exact", evidence["private_key_mounts"][0]["relation"])

    def test_identityserver_requires_its_distinct_key_and_tls_only_publication(self) -> None:
        base = inspector(
            self.private_key,
            mounts=[
                {
                    "Type": "bind",
                    "Source": str(self.identity_private_key),
                    "Destination": "/run/secrets/identityserver-tls-private-key.pem",
                    "RW": False,
                }
            ],
            ports={"8443/tcp": [{"HostIp": "0.0.0.0", "HostPort": "8261"}]},
        )

        def identity_inspector(command: list[str]) -> dict:
            result = base(command)
            if command[1:2] == ["inspect"]:
                result["container_name"] = "/xframework-identityserver"
                result["labels"]["com.docker.compose.service"] = "identityserver"
            return result

        evidence, errors = MODULE.collect_service(
            "identityserver",
            CONTAINER_ID,
            EXPECTED,
            PROJECT,
            self.private_key,
            7443,
            identity_inspector,
            listener_runner(proc_tcp()),
            identity_private_key=self.identity_private_key,
            identity_expected_published_port=8261,
        )
        self.assertEqual([], errors)
        self.assertEqual(8261, evidence["published_port"]["published_port"])

        cross_mounted = inspector(
            self.private_key,
            mounts=[
                {
                    "Type": "bind",
                    "Source": str(self.private_key),
                    "Destination": "/run/secrets/identityserver-tls-private-key.pem",
                    "RW": False,
                }
            ],
            ports={"8443/tcp": [{"HostIp": "0.0.0.0", "HostPort": "8261"}]},
        )

        def cross_inspector(command: list[str]) -> dict:
            result = cross_mounted(command)
            if command[1:2] == ["inspect"]:
                result["labels"]["com.docker.compose.service"] = "identityserver"
            return result

        _, cross_errors = MODULE.collect_service(
            "identityserver",
            CONTAINER_ID,
            EXPECTED,
            PROJECT,
            self.private_key,
            7443,
            cross_inspector,
            listener_runner(proc_tcp()),
            identity_private_key=self.identity_private_key,
            identity_expected_published_port=8261,
        )
        self.assertTrue(any("private-key mount" in error for error in cross_errors))

    def test_mutable_image_and_wrong_repository_digest_fail_closed(self) -> None:
        _, mutable = self.collect(configured_image="registry.example/xframework/bolt-hub:develop")
        unrelated = "registry.example/other/bolt-hub@sha256:" + "d" * 64
        _, unrelated_errors = self.collect(repo_digests=[unrelated])
        self.assertTrue(any("configured image" in error for error in mutable))
        self.assertTrue(any("exact authorized" in error for error in unrelated_errors))

    def test_duplicate_project_service_containers_are_observable(self) -> None:
        other = "e" * 64

        def runner(command: list[str]) -> subprocess.CompletedProcess[str]:
            return subprocess.CompletedProcess(command, 0, f"{CONTAINER_ID}\n{other}\n", "")

        ids = MODULE.project_container_ids(PROJECT, "bolt-hub", runner)
        self.assertEqual([CONTAINER_ID, other], ids)

    def test_inactive_synthetics_pin_is_required_but_not_in_runtime_inventory(self) -> None:
        repositories = {
            service: f"registry.example/xframework/{service}" for service in MODULE.PHASE0_IMAGE_SERVICES
        }
        pins = {
            service: f"{repository}@sha256:{index:064x}"
            for index, (service, repository) in enumerate(repositories.items(), start=1)
        }
        pin_file = self.root / "pins.json"
        pin_file.write_text(
            json.dumps(
                {
                    "schema": "xframework.bolt.phase0.image-pins.v2",
                    "status": "passed",
                    "registry_confirmed": True,
                    "approved_repositories": repositories,
                    "pins": pins,
                }
            ),
            encoding="utf-8",
        )
        loaded = MODULE.load_pins(pin_file)
        self.assertIn("bolt-phase0-synthetics", loaded)
        self.assertNotIn("bolt-phase0-synthetics", MODULE.PHASE0_SERVICES)

    def test_staged_inventory_is_explicit_and_cannot_omit_hub_boundary(self) -> None:
        MODULE.validate_runtime_services(
            ["migrate", "bolt-hub", "identityserver", "communications"],
            allow_staged_inventory=True,
        )

        invalid = (
            (["identityserver", "communications"], True),
            (["migrate", "bolt-hub", "not-a-service"], True),
            (["migrate", "bolt-hub", "bolt-hub"], True),
            (["migrate", "bolt-hub"], False),
        )
        for services, staged in invalid:
            with self.subTest(services=services, staged=staged):
                with self.assertRaises(ValueError):
                    MODULE.validate_runtime_services(services, staged)

    def test_runtime_can_read_typed_mount_and_port_values_without_shell_evaluation(self) -> None:
        env_file = self.root / "deployment.env"
        deployment_key = "/opt/xframework/tls/private-key.pem"
        env_file.write_bytes(
            f"BOLT_HUB_TLS_PRIVATE_KEY_PATH={deployment_key}\nBOLT_HUB_EXPOSE_PORT=7443\n".encode("ascii")
        )
        self.assertEqual(deployment_key, MODULE.typed_env_value(str(env_file), "BOLT_HUB_TLS_PRIVATE_KEY_PATH"))
        self.assertEqual("7443", MODULE.typed_env_value(str(env_file), "BOLT_HUB_EXPOSE_PORT"))

    def test_proc_listener_parser_ignores_non_listening_sockets(self) -> None:
        content = TCP_HEADER + "   0: 0100007F:1F90 00000000:0000 01 00000000:00000000 00:00000000 00000000 0 0 1\n"
        self.assertEqual([], MODULE.parse_proc_net(content, "ipv4"))


if __name__ == "__main__":
    unittest.main()
