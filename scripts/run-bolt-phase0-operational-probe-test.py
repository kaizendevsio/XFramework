#!/usr/bin/env python3
from __future__ import annotations

import base64
import contextlib
import datetime as dt
import importlib.util
import io
import json
import os
import signal
import stat
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


SCRIPT = Path(__file__).with_name("run-bolt-phase0-operational-probe.py")
SPEC = importlib.util.spec_from_file_location("bolt_phase0_operational_probe", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


PROJECT = "xframework"
PEER_ID = "a" * 64
REDIS_ID = "b" * 64
OLD_SECRET = "old-" + "s" * 60
CURRENT_SECRET = "current-" + "s" * 60
NOW = dt.datetime(2026, 7, 13, 12, 0, tzinfo=dt.timezone.utc)


class FakeEnvParser:
    @staticmethod
    def typed_value(key: str, value: str, explicit_type: str | None = None) -> str:
        if explicit_type == "absolute-path" and not os.path.isabs(value):
            raise ValueError(key)
        if key.endswith("_PORT") and (not value.isdigit() or not 1 <= int(value) <= 65535):
            raise ValueError(key)
        return value.lower() if key.endswith("HOSTNAME") else value


def private_file(path: Path, data: str | bytes, *, executable: bool = False) -> Path:
    path.write_bytes(data.encode("utf-8") if isinstance(data, str) else data)
    path.chmod(0o700 if executable else 0o600)
    return path.resolve()


def jwt(generation: str, subject: str) -> str:
    def segment(value: dict) -> str:
        raw = json.dumps(value, separators=(",", ":")).encode("utf-8")
        return base64.urlsafe_b64encode(raw).rstrip(b"=").decode("ascii")

    return f"{segment({'alg': 'HS512'})}.{segment({'credential_generation': generation, 'sub': subject})}.c2ln"


class DockerHarness:
    def __init__(self, root: Path) -> None:
        self.root = root
        self.commands: list[tuple[list[str], float, dict[str, str] | None]] = []
        self.peer_status = 401
        self.redis_running = True
        self.redis_health = "healthy"
        self.redis_paused = False
        self.redis_labels = True
        self.duplicate_service: str | None = None
        self.post_command: str | None = None
        self.post_returncode = 0
        self.post_stdout = b""
        self.post_receipt = True
        self.post_assertions = {"durableStateVerified": True, "dataLossObserved": False}
        self.never_healthy = False
        self.stop_seen = False

    @staticmethod
    def _service(command: list[str]) -> str:
        prefix = "label=com.docker.compose.service="
        return next(item[len(prefix) :] for item in command if item.startswith(prefix))

    def _inspect(self, container_id: str) -> MODULE.ProcessResult:
        service = "redis" if container_id == REDIS_ID else "communications"
        labels = {
            "com.docker.compose.project": PROJECT,
            "com.docker.compose.service": service,
        }
        if service == "redis" and not self.redis_labels:
            labels["com.docker.compose.project"] = "attacker"
        document = {
            "id": container_id,
            "running": self.redis_running if service == "redis" else True,
            "paused": self.redis_paused if service == "redis" else False,
            "status": "running" if (self.redis_running or service != "redis") else "exited",
            "health": (
                "starting" if service == "redis" and self.never_healthy and self.stop_seen else
                self.redis_health if service == "redis" else "healthy"
            ),
            "labels": labels,
        }
        return MODULE.ProcessResult(0, json.dumps(document).encode("utf-8"), b"")

    def __call__(
        self, command: list[str], timeout: float, environment: dict[str, str] | None
    ) -> MODULE.ProcessResult:
        self.commands.append((list(command), timeout, dict(environment) if environment else None))
        if command[:4] == ["docker", "container", "ls", "-aq"]:
            service = self._service(command)
            if self.duplicate_service == service:
                output = f"{PEER_ID}\n{'c' * 64}\n"
            else:
                output = f"{REDIS_ID if service == 'redis' else PEER_ID}\n"
            return MODULE.ProcessResult(0, output.encode("ascii"), b"")
        if command[:2] == ["docker", "inspect"]:
            return self._inspect(command[-1])
        if command[:2] == ["docker", "exec"]:
            return MODULE.ProcessResult(0, str(self.peer_status).encode("ascii"), b"")
        if command[:2] == ["docker", "stop"]:
            self.stop_seen = True
            self.redis_running = False
            self.redis_health = "unhealthy"
            return MODULE.ProcessResult(0, f"{REDIS_ID}\n".encode("ascii"), b"")
        if command[:2] == ["docker", "start"]:
            self.redis_running = True
            self.redis_health = "healthy"
            return MODULE.ProcessResult(0, f"{REDIS_ID}\n".encode("ascii"), b"")
        if self.post_command and command == [self.post_command]:
            if self.post_receipt and environment:
                receipt = Path(environment["BOLT_SYNTHETIC_POST_RECOVERY_RECEIPT"])
                private_file(
                    receipt,
                    json.dumps(
                        {
                            "schemaVersion": MODULE.DURABLE_RECEIPT_SCHEMA,
                            "status": "passed",
                            "assertions": self.post_assertions,
                        }
                    ),
                )
            return MODULE.ProcessResult(self.post_returncode, self.post_stdout, b"")
        raise AssertionError(f"unexpected command: {command}")


class DockerInspectTemplateTests(unittest.TestCase):
    def test_optional_health_lookup_is_missing_key_safe(self) -> None:
        self.assertIn(
            '"health":{{with index .State "Health"}}'
            '{{json (index . "Status")}}{{else}}null{{end}}',
            MODULE.DOCKER_INSPECT_FORMAT,
        )
        self.assertNotIn(".State.Health", MODULE.DOCKER_INSPECT_FORMAT)


class RequestHarness:
    def __init__(self, current_user: str, old_user: str, old_service: str) -> None:
        self.current_user = current_user
        self.old_user = old_user
        self.old_service = old_service
        self.calls: list[tuple[MODULE.TlsTarget, str, str, bytes | None, dict[str, str]]] = []
        self.old_user_status = 401
        self.old_service_status = 403
        self.secret_status = 401
        self.health_status = 200
        self.current_status = 101
        self.current_headers = {"upgrade": "websocket", "connection": "Upgrade"}

    def __call__(
        self,
        target: MODULE.TlsTarget,
        method: str,
        path: str,
        body: bytes | None,
        headers: dict[str, str],
    ) -> MODULE.HttpResult:
        copied = dict(headers)
        self.calls.append((target, method, path, body, copied))
        authorization = copied.get("Authorization")
        if authorization == f"Bearer {self.old_user}":
            return MODULE.HttpResult(self.old_user_status, {}, b"")
        if authorization == f"Bearer {self.old_service}":
            return MODULE.HttpResult(self.old_service_status, {}, b"")
        if authorization == f"Bearer {self.current_user}":
            return MODULE.HttpResult(self.current_status, self.current_headers, b"")
        if method == "POST":
            return MODULE.HttpResult(self.secret_status, {}, b"rejected")
        if path in {"/health/live", "/health/ready"}:
            return MODULE.HttpResult(self.health_status, {}, b"healthy")
        raise AssertionError((target, method, path))


class OperationalProbeTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name).resolve()
        self.root.chmod(0o700)
        self.env_file = private_file(self.root / "deployment.env", "SAFE_VALUE=ok\n")
        self.ca = private_file(self.root / "ca.crt", "test-ca")
        self.identity_ca = private_file(self.root / "identity-ca.crt", "identity-ca")
        self.current_user = jwt("g2", "user")
        self.current_service = jwt("g2", "service")
        self.old_user = jwt("g1", "old-user")
        self.old_service = jwt("g1", "old-service")
        self.token_paths = {
            "user": private_file(self.root / "user.jwt", self.current_user),
            "communications": private_file(self.root / "service.jwt", self.current_service),
            "rejected_user": private_file(self.root / "old-user.jwt", self.old_user),
            "rejected_communications": private_file(
                self.root / "old-service.jwt", self.old_service
            ),
        }
        self.manifest = private_file(
            self.root / "manifest.json",
            json.dumps(
                {
                    "schemaVersion": "bolt-phase0-token-manifest/v1",
                    "tokens": [
                        {"purpose": purpose, "path": str(path)}
                        for purpose, path in self.token_paths.items()
                    ],
                }
            ),
        )
        self.retired_secret = private_file(self.root / "retired-secret", OLD_SECRET)
        self.post_command = private_file(self.root / "durable-probe", "#!/bin/sh\nexit 0\n", executable=True)
        self.receipt = self.root / "receipt.json"

    def plaintext_values(self) -> dict[str, str]:
        return {
            "BOLT_SYNTHETIC_COMPOSE_PROJECT_NAME": PROJECT,
            "BOLT_SYNTHETIC_PLAINTEXT_PEER_SERVICE": "communications",
        }

    def redis_values(self) -> dict[str, str]:
        return {
            "BOLT_SYNTHETIC_COMPOSE_PROJECT_NAME": PROJECT,
            "BOLT_SYNTHETIC_REDIS_POST_RECOVERY_COMMAND_PATH": str(self.post_command),
        }

    def old_values(self) -> dict[str, str]:
        return {
            "BOLT_HUB_TLS_CA_PATH": str(self.ca),
            "BOLT_HUB_PUBLIC_HOSTNAME": "bolt.example.test",
            "BOLT_HUB_EXPOSE_PORT": "7443",
            "BOLT_SYNTHETIC_IDENTITYSERVER_CA_PATH": str(self.identity_ca),
            "BOLT_SYNTHETIC_IDENTITYSERVER_BASE_URL": "https://identity.example.test:8261",
            "IDENTITYSERVER_BOLT_TRANSPORT_TOKEN_PATH": "/api/service-identity/bolt-transport-token",
            "BOLT_SYNTHETIC_REJECTED_CLIENT_SECRET_PATH": str(self.retired_secret),
            "COMMUNICATIONS_SERVICE_IDENTITY_SECRET": CURRENT_SECRET,
        }

    def run_with_values(self, kind: str, stage: str, values: dict, **kwargs) -> None:
        with mock.patch.object(
            MODULE, "load_protected_env", return_value=(values, FakeEnvParser)
        ):
            MODULE.run_probe(
                kind,
                stage,
                str(self.env_file),
                str(self.manifest),
                self.receipt,
                now=lambda: NOW,
                **kwargs,
            )

    def test_protected_env_keeps_opaque_values_inert_and_typed_values_strict(self) -> None:
        values, parser = MODULE.load_protected_env(
            str(self.env_file), Path(__file__).with_name("verify-bolt-phase0-env.py").resolve()
        )
        self.assertEqual({"SAFE_VALUE": "ok"}, values)
        self.assertTrue(callable(parser.typed_value))

        private_file(self.env_file, "UNSAFE=$(id)\n")
        values, parser = MODULE.load_protected_env(
            str(self.env_file), Path(__file__).with_name("verify-bolt-phase0-env.py").resolve()
        )
        self.assertEqual({"UNSAFE": "$(id)"}, values)
        with self.assertRaises(ValueError):
            parser.typed_value("UNSAFE", values["UNSAFE"], "absolute-path")

    def test_plaintext_probe_uses_peer_container_without_bearer_and_writes_exact_receipt(self) -> None:
        docker = DockerHarness(self.root)
        self.run_with_values(
            "plaintext-rejection", "batch-1", self.plaintext_values(), runner=docker
        )
        receipt = json.loads(self.receipt.read_text(encoding="utf-8"))
        self.assertEqual(MODULE.ASSERTIONS["plaintext-rejection"], receipt["assertions"])
        self.assertEqual("plaintext-rejection", receipt["probe"])
        exec_command = next(command for command, _, _ in docker.commands if command[:2] == ["docker", "exec"])
        self.assertEqual(PEER_ID, exec_command[2])
        self.assertIn("http://bolt-hub:8080/bolt/ws", exec_command)
        self.assertFalse(any("authorization" in item.lower() or "bearer" in item.lower() for item in exec_command))

    def test_plaintext_upgrade_acceptance_or_ambiguous_status_fails_closed(self) -> None:
        for status in (0, 200, 301, 404, 101, 500):
            with self.subTest(status=status):
                docker = DockerHarness(self.root)
                docker.peer_status = status
                with self.assertRaises(MODULE.ProbeError):
                    MODULE.run_plaintext_rejection(self.plaintext_values(), docker)

    def test_plaintext_connection_refusal_is_an_explicit_rejection(self) -> None:
        docker = DockerHarness(self.root)

        def refused(command, timeout, environment):
            result = docker(command, timeout, environment)
            if command[:2] == ["docker", "exec"]:
                return MODULE.ProcessResult(7, b"", b"curl: connection refused")
            return result

        assertions = MODULE.run_plaintext_rejection(self.plaintext_values(), refused)

        self.assertEqual(MODULE.ASSERTIONS["plaintext-rejection"], assertions)

    def test_plaintext_probe_rejects_reserved_duplicate_or_unhealthy_peer(self) -> None:
        for peer in ("bolt-hub", "redis", "bolt-phase0-synthetics"):
            values = self.plaintext_values()
            values["BOLT_SYNTHETIC_PLAINTEXT_PEER_SERVICE"] = peer
            with self.subTest(peer=peer), self.assertRaises(MODULE.ProbeError):
                MODULE.run_plaintext_rejection(values, DockerHarness(self.root))

        duplicate = DockerHarness(self.root)
        duplicate.duplicate_service = "communications"
        with self.assertRaises(MODULE.ProbeError):
            MODULE.run_plaintext_rejection(self.plaintext_values(), duplicate)

    def test_plaintext_probe_rejects_child_stderr_and_oversized_mock_output(self) -> None:
        def stderr_runner(command, timeout, environment):
            if command[:4] == ["docker", "container", "ls", "-aq"]:
                return MODULE.ProcessResult(0, f"{PEER_ID}\n".encode(), b"warning")
            raise AssertionError(command)

        with self.assertRaises(MODULE.ProbeError):
            MODULE.run_plaintext_rejection(self.plaintext_values(), stderr_runner)

        result = MODULE.ProcessResult(0, b"x" * (MODULE.MAX_PROCESS_OUTPUT_BYTES + 1), b"")
        with self.assertRaises(MODULE.ProbeError):
            MODULE._run_checked(lambda *_: result, ["bounded"], allow_stdout=True)

    def test_default_subprocess_runner_terminates_at_the_bound(self) -> None:
        class TimedOutProcess:
            def __init__(self) -> None:
                self.waits: list[float] = []
                self.terminated = False

            def wait(self, timeout: float) -> int:
                self.waits.append(timeout)
                if len(self.waits) == 1:
                    raise subprocess.TimeoutExpired(["bounded"], timeout)
                return 143

            def terminate(self) -> None:
                self.terminated = True

            def kill(self) -> None:
                raise AssertionError("terminate should have completed")

        process = TimedOutProcess()
        with mock.patch.object(subprocess, "Popen", return_value=process):
            with self.assertRaisesRegex(MODULE.ProbeError, "SUBPROCESS_TIMEOUT"):
                MODULE._default_process_runner(["bounded"], 3, {})
        self.assertEqual([3, 2], process.waits)
        self.assertTrue(process.terminated)

    def test_redis_probe_stops_and_recovers_only_resolved_compose_redis(self) -> None:
        docker = DockerHarness(self.root)
        docker.post_command = str(self.post_command)
        self.run_with_values(
            "redis-interruption",
            "canary",
            self.redis_values(),
            runner=docker,
            sleeper=lambda _: None,
        )
        receipt = json.loads(self.receipt.read_text(encoding="utf-8"))
        self.assertEqual(MODULE.ASSERTIONS["redis-interruption"], receipt["assertions"])
        mutations = [command for command, _, _ in docker.commands if command[:2] in (["docker", "stop"], ["docker", "start"])]
        self.assertEqual(["docker", "stop", "--time", "10", REDIS_ID], mutations[0])
        self.assertEqual(["docker", "start", REDIS_ID], mutations[1])
        self.assertTrue(docker.redis_running)
        post_call = next(item for item in docker.commands if item[0] == [str(self.post_command)])
        self.assertEqual("post-recovery", post_call[2]["BOLT_SYNTHETIC_DURABLE_PROBE_MODE"])
        self.assertNotIn(OLD_SECRET, " ".join(post_call[0]))

    def test_redis_probe_recovers_before_failing_post_recovery_attestation(self) -> None:
        for failure in ("exit", "output", "receipt", "loss"):
            with self.subTest(failure=failure):
                docker = DockerHarness(self.root)
                docker.post_command = str(self.post_command)
                if failure == "exit":
                    docker.post_returncode = 1
                elif failure == "output":
                    docker.post_stdout = b"not silent"
                elif failure == "receipt":
                    docker.post_receipt = False
                else:
                    docker.post_assertions = {"durableStateVerified": True, "dataLossObserved": True}
                with self.assertRaises(MODULE.ProbeError):
                    MODULE.run_redis_interruption(
                        self.redis_values(),
                        FakeEnvParser,
                        str(self.env_file),
                        str(self.manifest),
                        self.root,
                        docker,
                        lambda _: None,
                    )
                self.assertTrue(docker.redis_running)

    def test_sigterm_during_redis_stop_restores_redis_before_main_fails(self) -> None:
        docker = DockerHarness(self.root)
        docker.post_command = str(self.post_command)

        def interrupted_probe(*_args, **_kwargs) -> None:
            def interrupting_runner(command, timeout, environment):
                result = docker(command, timeout, environment)
                if command[:2] == ["docker", "stop"]:
                    signal.raise_signal(signal.SIGTERM)
                return result

            MODULE.run_redis_interruption(
                self.redis_values(),
                FakeEnvParser,
                str(self.env_file),
                str(self.manifest),
                self.root,
                interrupting_runner,
                lambda _: None,
            )

        environment = {
            "BOLT_SYNTHETIC_PROBE_KIND": "redis-interruption",
            "BOLT_SYNTHETIC_STAGE": "canary",
            "XFRAMEWORK_ENV_FILE": str(self.env_file),
            "BOLT_SYNTHETIC_TOKEN_MANIFEST": str(self.manifest),
            "BOLT_SYNTHETIC_PROBE_RECEIPT": str(self.receipt),
        }
        with mock.patch.dict(os.environ, environment, clear=True), mock.patch.object(
            MODULE, "run_probe", side_effect=interrupted_probe
        ):
            self.assertEqual(1, MODULE.main())
        self.assertTrue(docker.redis_running)
        self.assertTrue(any(command[:2] == ["docker", "start"] for command, _, _ in docker.commands))

    def test_redis_probe_fails_before_mutation_for_duplicate_or_wrong_identity(self) -> None:
        for mode in ("duplicate", "labels"):
            with self.subTest(mode=mode):
                docker = DockerHarness(self.root)
                docker.post_command = str(self.post_command)
                if mode == "duplicate":
                    docker.duplicate_service = "redis"
                else:
                    docker.redis_labels = False
                with self.assertRaises(MODULE.ProbeError):
                    MODULE.run_redis_interruption(
                        self.redis_values(), FakeEnvParser, str(self.env_file), str(self.manifest),
                        self.root, docker, lambda _: None
                    )
                self.assertFalse(any(command[:2] == ["docker", "stop"] for command, _, _ in docker.commands))

    def test_redis_probe_is_bounded_and_fails_when_health_never_recovers(self) -> None:
        docker = DockerHarness(self.root)
        docker.post_command = str(self.post_command)
        docker.never_healthy = True
        sleeps: list[float] = []
        with self.assertRaises(MODULE.ProbeError):
            MODULE.run_redis_interruption(
                self.redis_values(), FakeEnvParser, str(self.env_file), str(self.manifest),
                self.root, docker, sleeps.append
            )
        self.assertEqual(MODULE.RECOVERY_ATTEMPTS - 1, len(sleeps))
        self.assertFalse(any(command == [str(self.post_command)] for command, _, _ in docker.commands))

    def test_redis_stage_is_mandatory_and_no_mutation_occurs(self) -> None:
        docker = DockerHarness(self.root)
        with self.assertRaises(MODULE.ProbeError):
            self.run_with_values(
                "redis-interruption", "batch-1", self.redis_values(), runner=docker
            )
        self.assertEqual([], docker.commands)

    def test_old_generation_probe_proves_all_rejections_and_current_health(self) -> None:
        requests = RequestHarness(self.current_user, self.old_user, self.old_service)
        self.run_with_values(
            "old-generation-rejection",
            "finalized",
            self.old_values(),
            requester=requests,
        )
        receipt_bytes = self.receipt.read_bytes()
        receipt = json.loads(receipt_bytes)
        self.assertEqual(MODULE.ASSERTIONS["old-generation-rejection"], receipt["assertions"])
        self.assertNotIn(OLD_SECRET.encode(), receipt_bytes)
        self.assertEqual(6, len(requests.calls))
        post = next(call for call in requests.calls if call[1] == "POST")
        self.assertEqual("identity.example.test", post[0].host)
        self.assertEqual(8261, post[0].port)
        self.assertEqual(str(self.identity_ca), post[0].ca_path)
        self.assertIn(OLD_SECRET.encode(), post[3])
        self.assertNotIn("Authorization", post[4])
        self.assertTrue(all(call[0].ca_path in {str(self.ca), str(self.identity_ca)} for call in requests.calls))

    def test_default_https_request_is_direct_timed_and_response_bounded(self) -> None:
        class Response:
            status = 200

            @staticmethod
            def getheader(name: str, default=None):
                return "2" if name == "Content-Length" else default

            @staticmethod
            def getheaders():
                return [("Content-Type", "application/json")]

            @staticmethod
            def read(limit: int) -> bytes:
                self.assertEqual(MODULE.MAX_HTTP_RESPONSE_BYTES + 1, limit)
                return b"{}"

        class Connection:
            def __init__(self) -> None:
                self.requests = []
                self.closed = False

            def request(self, method, path, body=None, headers=None):
                self.requests.append((method, path, body, headers))

            @staticmethod
            def getresponse():
                return Response()

            def close(self):
                self.closed = True

        connection = Connection()
        context = object()
        with mock.patch.object(MODULE, "_strict_tls_context", return_value=context), mock.patch.object(
            MODULE.http.client, "HTTPSConnection", return_value=connection
        ) as factory, mock.patch.dict(os.environ, {"HTTPS_PROXY": "http://attacker.invalid"}):
            result = MODULE._default_http_request(
                MODULE.TlsTarget("identity.example.test", 8261, str(self.identity_ca)),
                "POST",
                "/probe",
                b"{}",
                {"Content-Type": "application/json"},
            )
        factory.assert_called_once_with(
            "identity.example.test", 8261, context=context, timeout=MODULE.HTTP_TIMEOUT_SECONDS
        )
        self.assertEqual(200, result.status)
        self.assertEqual("application/json", result.headers["content-type"])
        self.assertTrue(connection.closed)

        class OversizedResponse(Response):
            @staticmethod
            def getheader(name: str, default=None):
                return str(MODULE.MAX_HTTP_RESPONSE_BYTES + 1) if name == "Content-Length" else default

        connection = Connection()
        connection.getresponse = lambda: OversizedResponse()
        with mock.patch.object(MODULE, "_strict_tls_context", return_value=context), mock.patch.object(
            MODULE.http.client, "HTTPSConnection", return_value=connection
        ):
            with self.assertRaisesRegex(MODULE.ProbeError, "HTTP_RESPONSE"):
                MODULE._default_http_request(
                    MODULE.TlsTarget("identity.example.test", 8261, str(self.identity_ca)),
                    "GET",
                    "/health/live",
                    None,
                    {},
                )

    def test_old_token_or_client_secret_acceptance_fails_closed(self) -> None:
        scenarios = ("user", "service", "secret")
        for scenario in scenarios:
            with self.subTest(scenario=scenario):
                requests = RequestHarness(self.current_user, self.old_user, self.old_service)
                if scenario == "user":
                    requests.old_user_status = 101
                elif scenario == "service":
                    requests.old_service_status = 200
                else:
                    requests.secret_status = 200
                with self.assertRaises(MODULE.ProbeError):
                    MODULE.run_old_generation_rejection(
                        self.old_values(), FakeEnvParser, str(self.manifest), requests
                    )

    def test_current_http_and_bolt_health_fail_closed_independently(self) -> None:
        requests = RequestHarness(self.current_user, self.old_user, self.old_service)
        requests.health_status = 503
        with self.assertRaises(MODULE.ProbeError):
            MODULE.run_old_generation_rejection(
                self.old_values(), FakeEnvParser, str(self.manifest), requests
            )

        requests = RequestHarness(self.current_user, self.old_user, self.old_service)
        requests.current_headers = {}
        with self.assertRaises(MODULE.ProbeError):
            MODULE.run_old_generation_rejection(
                self.old_values(), FakeEnvParser, str(self.manifest), requests
            )

    def test_old_generation_requires_https_and_distinct_private_retired_secret(self) -> None:
        values = self.old_values()
        values["BOLT_SYNTHETIC_IDENTITYSERVER_BASE_URL"] = "http://identity.example.test:8261"
        with self.assertRaises(MODULE.ProbeError):
            MODULE.run_old_generation_rejection(
                values, FakeEnvParser, str(self.manifest),
                RequestHarness(self.current_user, self.old_user, self.old_service)
            )

        private_file(self.retired_secret, CURRENT_SECRET)
        with self.assertRaises(MODULE.ProbeError):
            MODULE.run_old_generation_rejection(
                self.old_values(), FakeEnvParser, str(self.manifest),
                RequestHarness(self.current_user, self.old_user, self.old_service)
            )

    def test_old_generation_requires_retired_manifest_entries_and_distinct_generation(self) -> None:
        incomplete = private_file(
            self.root / "incomplete.json",
            json.dumps(
                {
                    "schemaVersion": "bolt-phase0-token-manifest/v1",
                    "tokens": [
                        {"purpose": "user", "path": str(self.token_paths["user"])},
                        {"purpose": "communications", "path": str(self.token_paths["communications"])},
                    ],
                }
            ),
        )
        with self.assertRaises(MODULE.ProbeError):
            MODULE.run_old_generation_rejection(
                self.old_values(), FakeEnvParser, str(incomplete),
                RequestHarness(self.current_user, self.old_user, self.old_service)
            )

        private_file(self.token_paths["rejected_user"], jwt("g2", "old-user"))
        with self.assertRaises(MODULE.ProbeError):
            MODULE.run_old_generation_rejection(
                self.old_values(), FakeEnvParser, str(self.manifest),
                RequestHarness(self.current_user, self.old_user, self.old_service)
            )

    def test_old_generation_stage_is_mandatory_before_network_access(self) -> None:
        requests = RequestHarness(self.current_user, self.old_user, self.old_service)
        with self.assertRaises(MODULE.ProbeError):
            self.run_with_values(
                "old-generation-rejection", "canary", self.old_values(), requester=requests
            )
        self.assertEqual([], requests.calls)

    def test_receipt_is_atomic_owner_only_and_has_exact_schema(self) -> None:
        docker = DockerHarness(self.root)
        self.run_with_values(
            "plaintext-rejection", "batch-2", self.plaintext_values(), runner=docker
        )
        metadata = self.receipt.stat()
        if MODULE.ENFORCE_POSIX_PERMISSIONS:
            self.assertEqual(0o600, stat.S_IMODE(metadata.st_mode))
            self.assertEqual(os.geteuid(), metadata.st_uid)
        document = json.loads(self.receipt.read_text(encoding="utf-8"))
        self.assertEqual(
            {"schemaVersion", "probe", "status", "startedAtUtc", "completedAtUtc", "assertions"},
            set(document),
        )
        self.assertEqual("2026-07-13T12:00:00.000Z", document["startedAtUtc"])
        self.assertEqual([], list(self.root.glob(".receipt.json.*")))

    def test_existing_receipt_or_non_private_parent_fails_closed(self) -> None:
        private_file(self.receipt, "existing")
        with self.assertRaises(MODULE.ProbeError):
            MODULE.write_atomic_receipt(self.receipt, {"status": "passed"})

        if MODULE.ENFORCE_POSIX_PERMISSIONS:
            public = self.root / "public"
            public.mkdir(mode=0o755)
            with self.assertRaises(MODULE.ProbeError):
                MODULE.write_atomic_receipt(public / "receipt.json", {"status": "passed"})

    def test_main_is_silent_on_failure_and_does_not_create_receipt(self) -> None:
        output = io.StringIO()
        environment = {
            "BOLT_SYNTHETIC_PROBE_KIND": "unknown",
            "BOLT_SYNTHETIC_STAGE": "canary",
            "XFRAMEWORK_ENV_FILE": str(self.env_file),
            "BOLT_SYNTHETIC_TOKEN_MANIFEST": str(self.manifest),
            "BOLT_SYNTHETIC_PROBE_RECEIPT": str(self.receipt),
        }
        with mock.patch.dict(os.environ, environment, clear=True), contextlib.redirect_stdout(output), contextlib.redirect_stderr(output):
            self.assertEqual(1, MODULE.main())
        self.assertEqual("", output.getvalue())
        self.assertFalse(self.receipt.exists())

    def test_unknown_probe_fails_before_loading_protected_configuration(self) -> None:
        with mock.patch.object(MODULE, "load_protected_env") as loader:
            with self.assertRaises(MODULE.ProbeError):
                MODULE.run_probe(
                    "unknown", "canary", str(self.env_file), str(self.manifest), self.receipt
                )
        loader.assert_not_called()


if __name__ == "__main__":
    unittest.main()
