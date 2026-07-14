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


def proc_tcp(
    health_scope: str = "loopback",
    extra_port: int | None = None,
    extra_address: str = "0100007F",
    include_docker_dns: bool = True,
) -> str:
    health_address = "0100007F" if health_scope == "loopback" else "00000000"
    lines = [
        TCP_HEADER.rstrip("\n"),
        f"   0: {health_address}:1F90 00000000:0000 0A 00000000:00000000 00:00000000 00000000 0 0 1",
        "   1: 00000000:20FB 00000000:0000 0A 00000000:00000000 00:00000000 00000000 0 0 2",
    ]
    if include_docker_dns:
        lines.append(
            "   2: 0B00007F:AF95 00000000:0000 0A 00000000:00000000 00:00000000 00000000 0 0 3"
        )
    if extra_port is not None:
        lines.append(
            f"   3: {extra_address}:{extra_port:04X} 00000000:0000 0A 00000000:00000000 00:00000000 00000000 0 0 4"
        )
    return "\n".join(lines) + "\n"


def listener_runner(
    tcp: str,
    resolv_conf: str = "nameserver 127.0.0.11\n",
    process_sockets: str = "socket:[1]\nsocket:[2]\n",
):
    def run(command: list[str]) -> subprocess.CompletedProcess[str]:
        if command[-1] == "/proc/net/tcp":
            content = tcp
        elif command[-1] == "/proc/net/tcp6":
            content = TCP_HEADER
        elif command[-1] == "/etc/resolv.conf":
            content = resolv_conf
        else:
            content = process_sockets
        return subprocess.CompletedProcess(command, 0, content, "")

    return run


def inspector(
    private_key: Path,
    configured_image: str = EXPECTED,
    repo_digests: list[str] | None = None,
    health: str | None = "healthy",
    running: bool = True,
    ports: dict | None = None,
    port_bindings: dict | None = None,
    mounts: list[dict] | None = None,
):
    network_bindings = {
        "8080/tcp": None,
        "8443/tcp": [
            {"HostIp": "0.0.0.0", "HostPort": "7443"},
            {"HostIp": "::", "HostPort": "7443"},
        ],
    }
    configured_bindings = {"8443/tcp": [{"HostIp": "", "HostPort": "7443"}]}
    actual_ports = ports if ports is not None else network_bindings
    if port_bindings is not None:
        actual_port_bindings = port_bindings
    elif ports is not None:
        actual_port_bindings = ports
    else:
        actual_port_bindings = configured_bindings

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
            "ports": actual_ports,
            "port_bindings": actual_port_bindings,
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


def identity_keydata_mount(**overrides: object) -> dict:
    return {
        "Type": "volume",
        "Name": f"{PROJECT}_{MODULE.IDENTITY_KEYDATA_VOLUME}",
        "Source": "/var/lib/docker/volumes/xframework_identity-keydata/_data",
        "Destination": MODULE.IDENTITY_KEYDATA_DIRECTORY,
        "RW": True,
        **overrides,
    }


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
        resolv_conf = kwargs.pop("resolv_conf", "nameserver 127.0.0.11\n")
        process_sockets = kwargs.pop("process_sockets", "socket:[1]\nsocket:[2]\n")
        return MODULE.collect_service(
            "bolt-hub",
            CONTAINER_ID,
            EXPECTED,
            PROJECT,
            self.private_key,
            7443,
            inspector(self.private_key, **kwargs),
            listener_runner(tcp, resolv_conf, process_sockets),
        )

    def collect_identity(
        self,
        *,
        mounts: list[dict] | None = None,
        tcp: str | None = None,
        ports: dict | None = None,
        port_bindings: dict | None = None,
    ):
        identity_runtime_ports = ports or {
            "8080/tcp": None,
            "8443/tcp": [
                {"HostIp": "0.0.0.0", "HostPort": "8261"},
                {"HostIp": "::", "HostPort": "8261"},
            ],
        }
        identity_configured_ports = port_bindings or {
            "8443/tcp": [{"HostIp": "", "HostPort": "8261"}]
        }
        identity_mounts = mounts if mounts is not None else [
            {
                "Type": "bind",
                "Source": str(self.identity_private_key),
                "Destination": "/run/secrets/identityserver-tls-private-key.pem",
                "RW": False,
            },
            identity_keydata_mount(),
        ]
        base = inspector(
            self.private_key,
            mounts=identity_mounts,
            ports=identity_runtime_ports,
            port_bindings=identity_configured_ports,
        )

        def identity_inspector(command: list[str]) -> dict:
            result = base(command)
            if command[1:2] == ["inspect"]:
                result["container_name"] = "/xframework-identityserver"
                result["labels"]["com.docker.compose.service"] = "identityserver"
            return result

        return MODULE.collect_service(
            "identityserver",
            CONTAINER_ID,
            EXPECTED,
            PROJECT,
            self.private_key,
            7443,
            identity_inspector,
            listener_runner(tcp or proc_tcp(health_scope="wildcard")),
            identity_private_key=self.identity_private_key,
            identity_expected_published_port=8261,
        )

    def test_inspect_template_handles_a_missing_health_object_without_dot_lookup(self) -> None:
        self.assertIn(
            '"health":{{with index .State "Health"}}'
            '{{json (index . "Status")}}{{else}}null{{end}}',
            MODULE.CONTAINER_FORMAT,
        )
        self.assertNotIn(".State.Health", MODULE.CONTAINER_FORMAT)

    def test_successful_migration_accepts_absent_health_without_listener_inspection(self) -> None:
        base = inspector(self.private_key, health=None, running=False, mounts=[])

        def migrate_inspector(command: list[str]) -> dict:
            result = base(command)
            if command[1:2] == ["inspect"]:
                result["container_name"] = "/xframework-migrate"
                result["labels"]["com.docker.compose.service"] = "migrate"
            return result

        command_runner = mock.Mock(side_effect=AssertionError("listener inspection is unexpected"))
        evidence, errors = MODULE.collect_service(
            "migrate",
            CONTAINER_ID,
            EXPECTED,
            PROJECT,
            self.private_key,
            7443,
            migrate_inspector,
            command_runner,
        )

        self.assertEqual([], errors)
        self.assertIsNone(evidence["health"])
        self.assertFalse(evidence["running"])
        self.assertEqual("exited", evidence["status"])
        self.assertEqual(0, evidence["exit_code"])
        command_runner.assert_not_called()

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
        _, duplicate_errors = self.collect(tcp=proc_tcp(extra_port=8080))
        self.assertTrue(any("8080" in error for error in public_errors))
        self.assertTrue(any("unexpected actual" in error for error in extra_errors))
        self.assertTrue(any("unexpected actual" in error for error in duplicate_errors))

    def test_embedded_dns_listener_is_ignored_only_for_the_configured_docker_resolver(self) -> None:
        evidence, errors = self.collect()
        self.assertEqual([], errors)
        self.assertEqual({8080, 8443}, {item["port"] for item in evidence["listeners"]})

        _, unconfigured = self.collect(resolv_conf="nameserver 1.1.1.1\n")
        self.assertTrue(any("unexpected actual" in error for error in unconfigured))

        with self.assertRaisesRegex(RuntimeError, "embedded DNS listener topology"):
            self.collect(tcp=proc_tcp(extra_port=9000, extra_address="0B00007F"))

        with self.assertRaisesRegex(RuntimeError, "embedded DNS listener topology"):
            self.collect(
                tcp=proc_tcp(
                    extra_port=9000,
                    extra_address="0B00007F",
                    include_docker_dns=False,
                ),
                process_sockets="socket:[1]\nsocket:[2]\nsocket:[4]\n",
            )

        with self.assertRaisesRegex(RuntimeError, "embedded DNS listener topology"):
            self.collect(tcp=proc_tcp(include_docker_dns=False))

    def test_wrong_or_plaintext_published_port_fails_closed(self) -> None:
        wrong = {"8443/tcp": [{"HostIp": "0.0.0.0", "HostPort": "7000"}]}
        plaintext = {
            "8443/tcp": [{"HostIp": "0.0.0.0", "HostPort": "7443"}],
            "8080/tcp": [{"HostIp": "0.0.0.0", "HostPort": "8080"}],
        }
        for ports in (wrong, plaintext):
            with self.subTest(ports=ports):
                evidence, errors = self.collect(ports=ports)
                self.assertTrue(any("port" in error for error in errors))
                self.assertIsNone(evidence["published_port"])

    def test_bound_plaintext_port_fails_even_when_configured_binding_is_tls_only(self) -> None:
        runtime_ports = {
            "8080/tcp": [{"HostIp": "0.0.0.0", "HostPort": "8080"}],
            "8443/tcp": [
                {"HostIp": "0.0.0.0", "HostPort": "7443"},
                {"HostIp": "::", "HostPort": "7443"},
            ],
        }
        configured = {"8443/tcp": [{"HostIp": "", "HostPort": "7443"}]}
        _, errors = self.collect(ports=runtime_ports, port_bindings=configured)
        self.assertTrue(any("runtime network publication" in error for error in errors))

    def test_loopback_or_duplicate_tls_host_bindings_fail_closed(self) -> None:
        configured = {"8443/tcp": [{"HostIp": "", "HostPort": "7443"}]}
        invalid_runtime_bindings = (
            [
                {"HostIp": "127.0.0.1", "HostPort": "7443"},
                {"HostIp": "::", "HostPort": "7443"},
            ],
            [
                {"HostIp": "0.0.0.0", "HostPort": "7443"},
                {"HostIp": "0.0.0.0", "HostPort": "7443"},
            ],
        )
        for bindings in invalid_runtime_bindings:
            with self.subTest(bindings=bindings):
                ports = {"8080/tcp": None, "8443/tcp": bindings}
                evidence, errors = self.collect(ports=ports, port_bindings=configured)
                self.assertTrue(any("runtime network publication" in error for error in errors))
                self.assertIsNone(evidence["published_port"])

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
        evidence, errors = self.collect_identity()
        self.assertEqual([], errors)
        self.assertEqual(8261, evidence["published_port"]["published_port"])
        self.assertEqual("wildcard", evidence["listeners"][0]["scope"])
        self.assertEqual(2, len(evidence["private_key_mounts"]))
        self.assertEqual(
            "<identity-signing-key-volume>",
            evidence["private_key_mounts"][1]["resolved_source"],
        )

        _, cross_errors = self.collect_identity(
            mounts=[
                {
                    "Type": "bind",
                    "Source": str(self.private_key),
                    "Destination": "/run/secrets/identityserver-tls-private-key.pem",
                    "RW": False,
                },
                identity_keydata_mount(),
            ]
        )
        self.assertTrue(any("private-key mount" in error for error in cross_errors))

    def test_identityserver_loopback_http_or_public_8080_fails_closed(self) -> None:
        _, listener_errors = self.collect_identity(tcp=proc_tcp(health_scope="loopback"))
        self.assertTrue(any("0.0.0.0" in error for error in listener_errors))

        public_ports = {
            "8080/tcp": [
                {"HostIp": "0.0.0.0", "HostPort": "8080"},
                {"HostIp": "::", "HostPort": "8080"},
            ],
            "8443/tcp": [
                {"HostIp": "0.0.0.0", "HostPort": "8261"},
                {"HostIp": "::", "HostPort": "8261"},
            ],
        }
        evidence, publication_errors = self.collect_identity(ports=public_ports)
        self.assertTrue(any("runtime network publication" in error for error in publication_errors))
        self.assertIsNone(evidence["published_port"])

    def test_identityserver_signing_key_volume_must_be_persistent_and_writable(self) -> None:
        tls_mount = {
            "Type": "bind",
            "Source": str(self.identity_private_key),
            "Destination": "/run/secrets/identityserver-tls-private-key.pem",
            "RW": False,
        }
        invalid_mounts = (
            [tls_mount],
            [tls_mount, identity_keydata_mount(RW=False)],
            [tls_mount, identity_keydata_mount(Name="xframework_ephemeral-keydata")],
            [tls_mount, identity_keydata_mount(Type="bind", Name="")],
        )
        for mounts in invalid_mounts:
            with self.subTest(mounts=mounts):
                _, errors = self.collect_identity(mounts=mounts)
                self.assertTrue(any("signing-key volume" in error for error in errors))

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

    def test_proc_listener_parser_rejects_malformed_records(self) -> None:
        with self.assertRaisesRegex(ValueError, "malformed /proc TCP record"):
            MODULE.parse_proc_net(TCP_HEADER + "   0: 0100007F:1F90 00000000:0000 0A\n", "ipv4")


if __name__ == "__main__":
    unittest.main()
