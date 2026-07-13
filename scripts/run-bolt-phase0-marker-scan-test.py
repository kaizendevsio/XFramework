#!/usr/bin/env python3
from __future__ import annotations

import base64
import contextlib
import datetime as dt
import hashlib
import importlib.util
import io
import json
import os
import stat
import subprocess
import sys
import tempfile
import unittest
import uuid
from pathlib import Path
from typing import Any
from unittest import mock


SCRIPT = Path(__file__).with_name("run-bolt-phase0-marker-scan.py")
WRAPPER = Path(__file__).with_name("run-bolt-phase0-synthetics.sh")
SPEC = importlib.util.spec_from_file_location("bolt_phase0_marker_scan", SCRIPT)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError("marker scan module unavailable")
scan = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = scan
SPEC.loader.exec_module(scan)

NOW = dt.datetime(2026, 7, 13, 8, 30, tzinfo=dt.timezone.utc)
API_KEY = "seq-read-only-api-key-123456789"


def private_directory(path: Path) -> Path:
    path.mkdir(mode=0o700)
    os.chmod(path, 0o700)
    return path


def private_write(path: Path, data: str | bytes) -> None:
    payload = data.encode("utf-8") if isinstance(data, str) else data
    path.write_bytes(payload)
    os.chmod(path, 0o600)


def jwt(marker: str) -> bytes:
    def encode(value: dict[str, Any]) -> bytes:
        raw = json.dumps(value, separators=(",", ":")).encode("utf-8")
        return base64.urlsafe_b64encode(raw).rstrip(b"=")

    return b".".join(
        (
            encode({"alg": "HS256", "typ": "JWT"}),
            encode({"iss": "https://identity.test", "exp": 2_000_000_000, "jti": marker}),
            b"signature-material-that-is-long-enough",
        )
    )


class Workspace:
    def __init__(self, root: Path, token_count: int = 2) -> None:
        self.private = private_directory(root / "private")
        self.env = self.private / "deployment.env"
        self.manifest = self.private / "manifest.json"
        self.receipt = self.private / "receipt.json"
        self.proxy_one = self.private / "proxy-access.log"
        self.proxy_two = self.private / "proxy-error.log"
        private_write(self.proxy_one, "retained request one\n")
        private_write(self.proxy_two, "retained request two\n")
        self.markers = [str(uuid.UUID(int=index + 1)) for index in range(token_count)]
        self.tokens = [jwt(marker) for marker in self.markers]
        self.token_paths: list[Path] = []
        entries: list[dict[str, Any]] = []
        for index, (marker, token) in enumerate(zip(self.markers, self.tokens, strict=True)):
            path = self.private / f"token-{index}.jwt"
            private_write(path, token + b"\n")
            metadata = path.stat(follow_symlinks=False)
            digest = hashlib.sha256(token).hexdigest()
            entries.append(
                {
                    "purpose": f"purpose_{index}",
                    "path": path.as_posix(),
                    "sha256Prefix": digest[:12],
                    "expiresAtUtc": "2033-05-18T03:33:20Z",
                    "issuerUri": "https://identity.test",
                    "marker": marker,
                    "markerSha256Prefix": hashlib.sha256(marker.encode("ascii")).hexdigest()[:12],
                    "identity": [
                        metadata.st_dev,
                        metadata.st_ino,
                        metadata.st_size,
                        metadata.st_mtime_ns,
                        digest,
                    ],
                }
            )
            self.token_paths.append(path)
        self.manifest_document = {
            "schemaVersion": "bolt-phase0-token-manifest/v1",
            "issuerUri": "https://identity.test",
            "principalReference": "bolt-phase0-test",
            "refreshedAtUtc": "2026-07-13T08:29:00Z",
            "minimumRemainingLifetimeSeconds": 480,
            "expiryEnabled": False,
            "tokens": entries,
        }
        self.write_manifest()
        self.write_env()

    def write_manifest(self) -> None:
        private_write(self.manifest, json.dumps(self.manifest_document, separators=(",", ":")))

    def write_env(
        self,
        extra: str = "",
        *,
        proxy_mode: str | None = "logs",
        include_proxy_paths: bool = True,
        proxy_paths_value: str | None = None,
    ) -> None:
        values = {
            "BOLT_SYNTHETIC_SEQ_API_URL": "http://127.0.0.1:5342/api/events",
            "BOLT_SYNTHETIC_SEQ_API_KEY": API_KEY,
            "BOLT_SYNTHETIC_JAEGER_QUERY_API_URL": "http://127.0.0.1:16686/api",
        }
        if proxy_mode is not None:
            values["BOLT_SYNTHETIC_PROXY_MODE"] = proxy_mode
        if include_proxy_paths:
            values["BOLT_SYNTHETIC_PROXY_LOG_PATHS"] = (
                f"{self.proxy_one.as_posix()},{self.proxy_two.as_posix()}"
                if proxy_paths_value is None
                else proxy_paths_value
            )
        content = "".join(f"{key}={value}\n" for key, value in values.items()) + extra
        private_write(self.env, content)

    def environ(self, kind: str) -> dict[str, str]:
        return {
            "XFRAMEWORK_ENV_FILE": self.env.as_posix(),
            "BOLT_SYNTHETIC_TOKEN_MANIFEST": self.manifest.as_posix(),
            "BOLT_SYNTHETIC_PROBE_KIND": kind,
            "BOLT_SYNTHETIC_PROBE_RECEIPT": self.receipt.as_posix(),
            "BOLT_SYNTHETIC_STAGE": "canary",
        }


class FakeResponse:
    def __init__(
        self,
        body: bytes | str,
        *,
        status: int = 200,
        headers: dict[str, str] | None = None,
    ) -> None:
        self.body = body.encode("utf-8") if isinstance(body, str) else body
        self.status = status
        self.offset = 0
        self.headers = {
            "Content-Type": "application/json",
            "Content-Length": str(len(self.body)),
            **(headers or {}),
        }

    def getheader(self, name: str, default: str | None = None) -> str | None:
        for key, value in self.headers.items():
            if key.lower() == name.lower():
                return value
        return default

    def getheaders(self) -> list[tuple[str, str]]:
        return list(self.headers.items())

    def read(self, amount: int) -> bytes:
        chunk = self.body[self.offset : self.offset + amount]
        self.offset += len(chunk)
        return chunk


class FakeConnection:
    def __init__(self, factory: "FakeConnectionFactory", response: FakeResponse) -> None:
        self.factory = factory
        self.response = response

    def request(self, method: str, target: str, body: Any = None, headers: dict[str, str] | None = None) -> None:
        self.factory.requests.append((method, target, body, dict(headers or {})))

    def getresponse(self) -> FakeResponse:
        return self.response

    def close(self) -> None:
        pass


class FakeConnectionFactory:
    def __init__(self, responses: list[FakeResponse]) -> None:
        self.responses = list(responses)
        self.requests: list[tuple[str, str, Any, dict[str, str]]] = []
        self.endpoints: list[tuple[str, float]] = []

    def __call__(self, endpoint: Any, timeout: float) -> FakeConnection:
        self.endpoints.append((endpoint.geturl(), timeout))
        if not self.responses:
            raise AssertionError("unexpected HTTP request")
        return FakeConnection(self, self.responses.pop(0))


class MarkerScanTests(unittest.TestCase):
    def fixed_now(self) -> dt.datetime:
        return NOW

    def wrapper_validator_source(self) -> str:
        wrapper = WRAPPER.read_text(encoding="utf-8")
        start_marker = (
            '  python3 - "$receipt" "$kind" "$probe_started_epoch" '
            '"$token_manifest" "$proxy_mode" <<\'PY\'\n'
        )
        end_marker = "\nPY\n  last_probe_receipt=\"$receipt\""
        start = wrapper.find(start_marker)
        if start < 0:
            self.fail("wrapper receipt validator start marker is unavailable")
        start += len(start_marker)
        end = wrapper.find(end_marker, start)
        if end < 0:
            self.fail("wrapper receipt validator end marker is unavailable")
        source = wrapper[start:end]
        if not hasattr(os, "geteuid"):
            source = source.replace(
                "import sys\n",
                (
                    "import sys\n"
                    "os.geteuid = lambda: os.stat(sys.argv[1]).st_uid\n"
                    "stat.S_IRWXG = stat.S_IRWXO = 0\n"
                ),
                1,
            )
        return source

    def run_wrapper_validator(
        self,
        workspace: Workspace,
        proxy_mode: str,
        assertions: dict[str, Any],
    ) -> subprocess.CompletedProcess[str]:
        now = dt.datetime.now(dt.timezone.utc)
        receipt = {
            "schemaVersion": "bolt-phase0-probe-receipt/v1",
            "probe": "proxy-marker-scan",
            "status": "passed",
            "startedAtUtc": now.isoformat().replace("+00:00", "Z"),
            "completedAtUtc": now.isoformat().replace("+00:00", "Z"),
            "assertions": assertions,
        }
        private_write(workspace.receipt, json.dumps(receipt, separators=(",", ":")))
        return subprocess.run(
            [
                sys.executable,
                "-c",
                self.wrapper_validator_source(),
                str(workspace.receipt),
                "proxy-marker-scan",
                str(int(now.timestamp()) - 1),
                str(workspace.manifest),
                proxy_mode,
            ],
            check=False,
            capture_output=True,
            text=True,
        )

    def assert_private_receipt(
        self,
        workspace: Workspace,
        kind: str,
        expected_assertions: dict[str, Any] | None = None,
    ) -> dict[str, Any]:
        receipt = json.loads(workspace.receipt.read_text(encoding="utf-8"))
        self.assertEqual(
            {"schemaVersion", "probe", "status", "startedAtUtc", "completedAtUtc", "assertions"},
            set(receipt),
        )
        self.assertEqual("bolt-phase0-probe-receipt/v1", receipt["schemaVersion"])
        self.assertEqual(kind, receipt["probe"])
        self.assertEqual("passed", receipt["status"])
        self.assertEqual("2026-07-13T08:30:00Z", receipt["startedAtUtc"])
        self.assertEqual("2026-07-13T08:30:00Z", receipt["completedAtUtc"])
        expected = expected_assertions or {
            "retainedStoreQueried": True,
            "matches": 0,
            "tokensSearched": len(workspace.tokens),
            "markersSearched": len(workspace.markers),
        }
        self.assertEqual(expected, receipt["assertions"])
        if scan.ENFORCE_POSIX_PERMISSIONS:
            self.assertEqual(0, stat.S_IMODE(workspace.receipt.stat().st_mode) & 0o077)
        return receipt

    def test_all_hook_kinds_write_exact_private_receipts_without_secrets(self) -> None:
        cases = {
            "proxy-marker-scan": [],
            "seq-marker-scan": [FakeResponse("[]")],
            "trace-marker-scan": [
                FakeResponse('{"data":["XFramework.Bolt.Hub"],"errors":null}'),
                FakeResponse('{"data":[],"errors":null}'),
            ],
        }
        for kind, responses in cases.items():
            with self.subTest(kind=kind), tempfile.TemporaryDirectory() as temporary:
                workspace = Workspace(Path(temporary))
                factory = FakeConnectionFactory(responses)
                result = scan.run_hook(
                    workspace.environ(kind),
                    connection_factory=factory,
                    now_factory=self.fixed_now,
                )
                self.assertEqual(0, result)
                receipt = self.assert_private_receipt(workspace, kind)
                receipt_text = json.dumps(receipt)
                for secret in [API_KEY, *workspace.markers, *(token.decode("ascii") for token in workspace.tokens)]:
                    self.assertNotIn(secret, receipt_text)

    def test_direct_kestrel_mode_skips_log_scanning_and_writes_exact_receipt(self) -> None:
        expected_assertions = {
            "retainedStoreQueried": False,
            "notApplicableReason": "direct-kestrel-publication",
            "matches": 0,
            "tokensSearched": 2,
            "markersSearched": 2,
        }
        for path_configuration in ("absent", "empty"):
            with self.subTest(path_configuration=path_configuration), tempfile.TemporaryDirectory() as temporary:
                workspace = Workspace(Path(temporary))
                workspace.write_env(
                    proxy_mode="direct-kestrel",
                    include_proxy_paths=path_configuration == "empty",
                    proxy_paths_value="",
                )
                with mock.patch.object(scan, "scan_proxy_logs") as proxy_scan:
                    result = scan.run_hook(
                        workspace.environ("proxy-marker-scan"),
                        now_factory=self.fixed_now,
                    )
                self.assertEqual(0, result)
                proxy_scan.assert_not_called()
                receipt = self.assert_private_receipt(
                    workspace,
                    "proxy-marker-scan",
                    expected_assertions,
                )
                receipt_text = json.dumps(receipt)
                secrets = [
                    API_KEY,
                    workspace.proxy_one.as_posix(),
                    workspace.proxy_two.as_posix(),
                    *workspace.markers,
                    *(token.decode("ascii") for token in workspace.tokens),
                ]
                for secret in secrets:
                    self.assertNotIn(secret, receipt_text)

    def test_proxy_mode_and_log_path_combinations_fail_closed(self) -> None:
        cases = {
            "unknown-mode": {"proxy_mode": "unknown"},
            "case-variant": {"proxy_mode": "LOGS"},
            "missing-mode": {"proxy_mode": None},
            "paths-present-in-direct-mode": {"proxy_mode": "direct-kestrel"},
            "paths-absent-in-logs-mode": {"proxy_mode": "logs", "include_proxy_paths": False},
            "paths-empty-in-logs-mode": {
                "proxy_mode": "logs",
                "include_proxy_paths": True,
                "proxy_paths_value": "",
            },
        }
        for name, configuration in cases.items():
            with self.subTest(name=name), tempfile.TemporaryDirectory() as temporary:
                workspace = Workspace(Path(temporary))
                workspace.write_env(**configuration)
                self.assertEqual(
                    1,
                    scan.run_hook(
                        workspace.environ("proxy-marker-scan"),
                        now_factory=self.fixed_now,
                    ),
                )
                self.assertFalse(workspace.receipt.exists())

    def test_wrapper_validator_accepts_only_the_mode_specific_proxy_assertion_union(self) -> None:
        logs_assertions = {
            "retainedStoreQueried": True,
            "matches": 0,
            "tokensSearched": 2,
            "markersSearched": 2,
        }
        direct_assertions = {
            "retainedStoreQueried": False,
            "notApplicableReason": "direct-kestrel-publication",
            "matches": 0,
            "tokensSearched": 2,
            "markersSearched": 2,
        }
        with tempfile.TemporaryDirectory() as temporary:
            workspace = Workspace(Path(temporary))
            logs_success = self.run_wrapper_validator(workspace, "logs", logs_assertions)
            logs_rejects_direct = self.run_wrapper_validator(workspace, "logs", direct_assertions)
            direct_success = self.run_wrapper_validator(workspace, "direct-kestrel", direct_assertions)
            direct_rejects_logs = self.run_wrapper_validator(workspace, "direct-kestrel", logs_assertions)
            unknown_rejected = self.run_wrapper_validator(workspace, "unknown", logs_assertions)

            self.assertEqual(0, logs_success.returncode)
            self.assertEqual(1, logs_rejects_direct.returncode)
            self.assertEqual(0, direct_success.returncode)
            self.assertEqual(1, direct_rejects_logs.returncode)
            self.assertEqual(1, unknown_rejected.returncode)
            for result in (
                logs_success,
                logs_rejects_direct,
                direct_success,
                direct_rejects_logs,
                unknown_rejected,
            ):
                self.assertEqual("", result.stdout)
                output = result.stdout + result.stderr
                for secret in [*workspace.markers, *(token.decode("ascii") for token in workspace.tokens)]:
                    self.assertNotIn(secret, output)

    def test_proxy_detects_exact_token_and_boundary_spanning_jti_but_allows_near_matches(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            workspace = Workspace(Path(temporary))
            private_write(workspace.proxy_one, workspace.tokens[0])
            self.assertEqual(1, scan.run_hook(workspace.environ("proxy-marker-scan"), now_factory=self.fixed_now))
            self.assertFalse(workspace.receipt.exists())

            near_token = workspace.tokens[0][:-1] + b"X"
            marker = workspace.markers[0].encode("ascii")
            boundary = scan.READ_CHUNK_BYTES
            private_write(workspace.proxy_one, near_token + b"\n" + b"a" * (boundary - 5) + marker)
            self.assertEqual(1, scan.run_hook(workspace.environ("proxy-marker-scan"), now_factory=self.fixed_now))

            private_write(workspace.proxy_one, near_token + b"\n" + marker[:-1] + b"f")
            self.assertEqual(0, scan.run_hook(workspace.environ("proxy-marker-scan"), now_factory=self.fixed_now))

    def test_seq_and_jaeger_detect_exact_token_or_jti_without_putting_needles_in_requests(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            workspace = Workspace(Path(temporary))
            seq_factory = FakeConnectionFactory(
                [FakeResponse(json.dumps({"Events": [{"message": workspace.tokens[0].decode("ascii")}]}))]
            )
            self.assertEqual(
                1,
                scan.run_hook(
                    workspace.environ("seq-marker-scan"),
                    connection_factory=seq_factory,
                    now_factory=self.fixed_now,
                ),
            )
            self.assertFalse(workspace.receipt.exists())

            trace_factory = FakeConnectionFactory(
                [
                    FakeResponse('{"data":["XFramework.Bolt.Hub"],"errors":null}'),
                    FakeResponse(json.dumps({"data": [{"tags": [{"value": workspace.markers[1]}]}], "errors": None})),
                ]
            )
            self.assertEqual(
                1,
                scan.run_hook(
                    workspace.environ("trace-marker-scan"),
                    connection_factory=trace_factory,
                    now_factory=self.fixed_now,
                ),
            )
            all_targets = "\n".join(request[1] for request in seq_factory.requests + trace_factory.requests)
            for needle in [*workspace.markers, *(token.decode("ascii") for token in workspace.tokens)]:
                self.assertNotIn(needle, all_targets)
            self.assertEqual(API_KEY, seq_factory.requests[0][3]["X-Seq-ApiKey"])

    def test_malformed_env_and_manifest_fail_closed_without_receipts(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            workspace = Workspace(Path(temporary))
            workspace.write_env("DUPLICATE=first\nDUPLICATE=second\n")
            self.assertEqual(1, scan.run_hook(workspace.environ("proxy-marker-scan"), now_factory=self.fixed_now))
            self.assertFalse(workspace.receipt.exists())

    def test_noncanonical_proxy_source_path_fails_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            workspace = Workspace(Path(temporary))
            noncanonical = f"{workspace.private.as_posix()}/../private/{workspace.proxy_one.name}"
            content = workspace.env.read_text(encoding="utf-8").replace(
                workspace.proxy_one.as_posix(),
                noncanonical,
            )
            private_write(workspace.env, content)
            self.assertEqual(1, scan.run_hook(workspace.environ("proxy-marker-scan"), now_factory=self.fixed_now))
            self.assertFalse(workspace.receipt.exists())

            workspace.write_env()
            workspace.manifest_document["tokens"][0]["marker"] = "not-a-jti"
            workspace.write_manifest()
            self.assertEqual(1, scan.run_hook(workspace.environ("proxy-marker-scan"), now_factory=self.fixed_now))
            self.assertFalse(workspace.receipt.exists())

    def test_redirect_proxy_environment_and_unprotected_urls_are_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            workspace = Workspace(Path(temporary))
            redirect = FakeConnectionFactory(
                [FakeResponse("{}", status=302, headers={"Location": "https://redirect.test/api/events"})]
            )
            self.assertEqual(
                1,
                scan.run_hook(
                    workspace.environ("seq-marker-scan"),
                    connection_factory=redirect,
                    now_factory=self.fixed_now,
                ),
            )
            proxy_environment = workspace.environ("proxy-marker-scan") | {"HTTPS_PROXY": "http://proxy.invalid"}
            self.assertEqual(1, scan.run_hook(proxy_environment, now_factory=self.fixed_now))

            unsafe = workspace.env.read_text(encoding="utf-8").replace(
                "http://127.0.0.1:5342/api/events",
                "http://seq.test:5342/api/events",
            )
            private_write(workspace.env, unsafe)
            self.assertEqual(
                1,
                scan.run_hook(
                    workspace.environ("seq-marker-scan"),
                    connection_factory=FakeConnectionFactory([FakeResponse("[]")]),
                    now_factory=self.fixed_now,
                ),
            )
            endpoint = scan.parse_http_endpoint("https://seq.test/api/events", expected_path="/api/events")
            self.assertEqual(("https", 443), (endpoint.parsed.scheme, endpoint.port))

    def test_http_response_event_trace_and_proxy_size_limits_fail_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            workspace = Workspace(Path(temporary))
            with mock.patch.object(scan, "MAX_HTTP_RESPONSE_BYTES", 8):
                factory = FakeConnectionFactory([FakeResponse(b"[12345678]")])
                self.assertEqual(
                    1,
                    scan.run_hook(
                        workspace.environ("seq-marker-scan"),
                        connection_factory=factory,
                        now_factory=self.fixed_now,
                    ),
                )

            with mock.patch.object(scan, "MAX_SEQ_EVENTS", 2):
                factory = FakeConnectionFactory([FakeResponse('[{"id":1},{"id":2}]')])
                self.assertEqual(
                    1,
                    scan.run_hook(
                        workspace.environ("seq-marker-scan"),
                        connection_factory=factory,
                        now_factory=self.fixed_now,
                    ),
                )

            with mock.patch.object(scan, "MAX_JAEGER_TRACES", 1):
                factory = FakeConnectionFactory(
                    [
                        FakeResponse('{"data":["svc"],"errors":null}'),
                        FakeResponse('{"data":[{"traceID":"1"}],"errors":null}'),
                    ]
                )
                self.assertEqual(
                    1,
                    scan.run_hook(
                        workspace.environ("trace-marker-scan"),
                        connection_factory=factory,
                        now_factory=self.fixed_now,
                    ),
                )

            with mock.patch.object(scan, "MAX_PROXY_FILE_BYTES", 4):
                self.assertEqual(1, scan.run_hook(workspace.environ("proxy-marker-scan"), now_factory=self.fixed_now))
            self.assertFalse(workspace.receipt.exists())

    def test_hook_subprocess_emits_no_stdout_stderr_on_success_or_failure(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            workspace = Workspace(Path(temporary))
            refreshed_at = dt.datetime.now(dt.timezone.utc) - dt.timedelta(minutes=1)
            workspace.manifest_document["refreshedAtUtc"] = refreshed_at.isoformat().replace("+00:00", "Z")
            workspace.write_manifest()
            environment = {
                key: value
                for key, value in os.environ.items()
                if key.lower() not in scan.PROXY_VARIABLES
            }
            environment.update(workspace.environ("proxy-marker-scan"))
            success = subprocess.run(
                [sys.executable, str(SCRIPT)],
                check=False,
                capture_output=True,
                text=True,
                env=environment,
            )
            self.assertEqual(0, success.returncode)
            self.assertEqual("", success.stdout)
            self.assertEqual("", success.stderr)
            self.assertTrue(workspace.receipt.exists())

            secret = "secret-value-that-must-never-appear"
            private_write(workspace.env, f"BROKEN={secret}\nBROKEN=duplicate\n")
            failure = subprocess.run(
                [sys.executable, str(SCRIPT)],
                check=False,
                capture_output=True,
                text=True,
                env=environment,
            )
            self.assertEqual(1, failure.returncode)
            self.assertEqual("", failure.stdout)
            self.assertEqual("", failure.stderr)
            self.assertNotIn(secret, failure.stdout + failure.stderr)
            self.assertFalse(workspace.receipt.exists())

    def test_run_hook_itself_does_not_write_output_on_success_or_failure(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            workspace = Workspace(Path(temporary))
            stdout = io.StringIO()
            stderr = io.StringIO()
            with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
                success = scan.run_hook(workspace.environ("proxy-marker-scan"), now_factory=self.fixed_now)
                failure = scan.run_hook(
                    workspace.environ("proxy-marker-scan") | {"NO_PROXY": "127.0.0.1"},
                    now_factory=self.fixed_now,
                )
            self.assertEqual((0, 1), (success, failure))
            self.assertEqual("", stdout.getvalue())
            self.assertEqual("", stderr.getvalue())
            self.assertFalse(workspace.receipt.exists())


if __name__ == "__main__":
    unittest.main()
