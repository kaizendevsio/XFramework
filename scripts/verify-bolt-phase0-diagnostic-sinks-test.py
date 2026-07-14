#!/usr/bin/env python3
from __future__ import annotations

import base64
import datetime as dt
import json
import os
import stat
import subprocess
import sys
import tempfile
import threading
import unittest
import urllib.parse
from dataclasses import dataclass
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any


SCRIPT = Path(__file__).with_name("verify-bolt-phase0-diagnostic-sinks.py")
TOKEN_KEYS = (
    "BOLT_SYNTHETIC_COMMUNICATIONS_TRANSPORT_TOKEN_PATH",
    "BOLT_SYNTHETIC_PORTAL_TRANSPORT_TOKEN_PATH",
    "BOLT_SYNTHETIC_USER_ACTOR_TOKEN_PATH",
    "BOLT_SYNTHETIC_EXPIRY_TRANSPORT_TOKEN_PATH",
)


def _b64url(value: object) -> str:
    raw = json.dumps(value, separators=(",", ":")).encode("utf-8")
    return base64.urlsafe_b64encode(raw).rstrip(b"=").decode("ascii")


def _jwt(jti: str, serial: int) -> str:
    return (
        f"{_b64url({'alg': 'RS256', 'typ': 'bolt+jwt'})}."
        f"{_b64url({'jti': jti, 'serial': serial})}.signature{serial}"
    )


@dataclass
class Response:
    document: Any = None
    status: int = 200
    raw: bytes | None = None
    declared_length: int | None = None
    content_type: str = "application/json"

    def body(self) -> bytes:
        if self.raw is not None:
            return self.raw
        return json.dumps(self.document, separators=(",", ":")).encode("utf-8")


class SinkState:
    def __init__(self, role: str):
        self.role = role
        self.requests: list[tuple[str, dict[str, str]]] = []
        self.seq = Response([])
        self.services = Response({"data": ["bolt-hub", "identityserver"], "total": 2})
        self.traces: dict[str, Response] = {
            "bolt-hub": Response({"data": [], "total": 0}),
            "identityserver": Response({"data": [], "total": 0}),
        }

    def reset(self) -> None:
        self.requests.clear()
        self.seq = Response([])
        self.services = Response({"data": ["bolt-hub", "identityserver"], "total": 2})
        self.traces = {
            "bolt-hub": Response({"data": [], "total": 0}),
            "identityserver": Response({"data": [], "total": 0}),
        }


class SinkHandler(BaseHTTPRequestHandler):
    server: Any

    def do_GET(self) -> None:
        parsed = urllib.parse.urlsplit(self.path)
        headers = {key.lower(): value for key, value in self.headers.items()}
        self.server.state.requests.append((self.path, headers))
        state: SinkState = self.server.state
        if state.role == "seq" and parsed.path == "/api/events":
            response = state.seq
        elif state.role == "jaeger" and parsed.path == "/api/services":
            response = state.services
        elif state.role == "jaeger" and parsed.path == "/api/traces":
            service = urllib.parse.parse_qs(parsed.query).get("service", [""])[0]
            response = state.traces.get(service, Response(status=404, document={}))
        else:
            response = Response(status=404, document={})

        body = response.body()
        self.send_response(response.status)
        self.send_header("Content-Type", response.content_type)
        self.send_header(
            "Content-Length",
            str(response.declared_length if response.declared_length is not None else len(body)),
        )
        self.end_headers()
        self.wfile.write(body)
        self.wfile.flush()
        if response.declared_length is not None and response.declared_length != len(body):
            self.close_connection = True

    def log_message(self, format: str, *args: object) -> None:
        return


class FakeSink:
    def __init__(self, role: str):
        self.state = SinkState(role)
        self.server = ThreadingHTTPServer(("127.0.0.1", 0), SinkHandler)
        self.server.state = self.state
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)

    @property
    def url(self) -> str:
        host, port = self.server.server_address
        return f"http://{host}:{port}"

    def start(self) -> None:
        self.thread.start()

    def close(self) -> None:
        self.server.shutdown()
        self.server.server_close()
        self.thread.join(timeout=5)


class DiagnosticSinkVerifierTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.seq_server = FakeSink("seq")
        cls.jaeger_server = FakeSink("jaeger")
        cls.seq_server.start()
        cls.jaeger_server.start()

    @classmethod
    def tearDownClass(cls) -> None:
        cls.seq_server.close()
        cls.jaeger_server.close()

    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name).resolve()
        self.seq_server.state.reset()
        self.jaeger_server.state.reset()
        self.jtis = [f"00000000-0000-4000-8000-{serial:012d}" for serial in range(1, 5)]
        self.tokens = [_jwt(jti, serial) for serial, jti in enumerate(self.jtis, 1)]
        self.token_paths: list[Path] = []
        for index, token in enumerate(self.tokens):
            path = self.root / f"token-{index}.jwt"
            path.write_bytes(token.encode("ascii") + b"\n")
            path.chmod(0o600)
            self.token_paths.append(path)
        self.env_path = self.root / "candidate.env"
        self._write_env()
        self.log_path = self.root / "bounded.log"
        self.log_path.write_text("bounded diagnostic output\n", encoding="utf-8")
        now = dt.datetime.now(dt.timezone.utc).replace(microsecond=0)
        self.window_start = (now - dt.timedelta(minutes=2)).isoformat().replace("+00:00", "Z")
        self.window_end = (now - dt.timedelta(minutes=1)).isoformat().replace("+00:00", "Z")
        self.sequence = 0

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def _write_env(self, override: str | None = None) -> None:
        if override is None:
            override = "\n".join(
                f"{key}={path}" for key, path in zip(TOKEN_KEYS, self.token_paths)
            ) + "\nSEQ_API_KEY=0123456789abcdef\n"
        self.env_path.write_text(override, encoding="utf-8")
        self.env_path.chmod(0o600)

    def _run(
        self,
        *,
        evidence: Path | None = None,
        seq_url: str | None = None,
        jaeger_url: str | None = None,
    ) -> tuple[subprocess.CompletedProcess[str], Path]:
        self.sequence += 1
        output = evidence or self.root / f"evidence-{self.sequence}.json"
        command = [
            sys.executable,
            str(SCRIPT),
            "--candidate-env",
            str(self.env_path),
            "--bounded-file",
            str(self.log_path),
            "--window-start",
            self.window_start,
            "--window-end",
            self.window_end,
            "--seq-base-url",
            seq_url or self.seq_server.url,
            "--jaeger-base-url",
            jaeger_url or self.jaeger_server.url,
            "--evidence",
            str(output),
        ]
        return subprocess.run(
            command,
            capture_output=True,
            text=True,
            encoding="utf-8",
            timeout=15,
            check=False,
        ), output

    def _assert_failed_without_secrets(
        self, result: subprocess.CompletedProcess[str], output: Path
    ) -> None:
        self.assertNotEqual(0, result.returncode)
        self.assertFalse(output.exists())
        combined = result.stdout + result.stderr
        for secret in self.tokens + self.jtis:
            self.assertNotIn(secret, combined)

    def test_success_queries_bounded_windows_and_writes_private_counts_only(self) -> None:
        result, output = self._run()

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("BOLT_PHASE0_DIAGNOSTIC_SINKS_OK\n", result.stdout)
        evidence = json.loads(output.read_text(encoding="ascii"))
        self.assertEqual({"status", "counts"}, set(evidence))
        self.assertEqual("passed", evidence["status"])
        self.assertEqual(
            {
                "credentials": 4,
                "files": 1,
                "fileBytes": len(self.log_path.read_bytes()),
                "seqEvents": 0,
                "jaegerServices": 2,
                "jaegerTraces": 0,
                "httpRequests": 4,
            },
            evidence["counts"],
        )
        if os.name == "posix":
            self.assertEqual(0o600, stat.S_IMODE(output.stat().st_mode))
        serialized = output.read_text(encoding="ascii")
        for secret in self.tokens + self.jtis:
            self.assertNotIn(secret, serialized)

        seq_query = urllib.parse.parse_qs(
            urllib.parse.urlsplit(self.seq_server.state.requests[0][0]).query
        )
        self.assertEqual(["1001"], seq_query["count"])
        self.assertEqual(["true"], seq_query["render"])
        self.assertEqual([self.window_start.replace("Z", ".000000Z")], seq_query["fromDateUtc"])
        self.assertEqual([self.window_end.replace("Z", ".000000Z")], seq_query["toDateUtc"])
        self.assertEqual(
            "0123456789abcdef",
            self.seq_server.state.requests[0][1]["x-seq-apikey"],
        )

        trace_requests = [
            urllib.parse.parse_qs(urllib.parse.urlsplit(path).query)
            for path, _ in self.jaeger_server.state.requests
            if urllib.parse.urlsplit(path).path == "/api/traces"
        ]
        self.assertEqual({"bolt-hub", "identityserver"}, {query["service"][0] for query in trace_requests})
        self.assertTrue(all(query["limit"] == ["201"] for query in trace_requests))
        self.assertTrue(all("start" in query and "end" in query for query in trace_requests))

    def test_full_token_and_jti_leaks_in_local_files_fail(self) -> None:
        for leak in (self.tokens[0], self.jtis[1]):
            with self.subTest(kind="token" if leak.startswith("ey") else "jti"):
                self.log_path.write_text(f"prefix {leak} suffix", encoding="ascii")
                result, output = self._run()
                self._assert_failed_without_secrets(result, output)
                self.log_path.write_text("clean\n", encoding="ascii")

    def test_seq_event_jti_leak_fails(self) -> None:
        self.seq_server.state.seq = Response([{"message": self.jtis[2]}])

        result, output = self._run()

        self._assert_failed_without_secrets(result, output)

    def test_jaeger_trace_token_leak_fails(self) -> None:
        self.jaeger_server.state.traces["bolt-hub"] = Response(
            {"data": [{"spans": [{"tag": self.tokens[3]}]}], "total": 1}
        )

        result, output = self._run()

        self._assert_failed_without_secrets(result, output)

    def test_malformed_sink_responses_fail_closed(self) -> None:
        cases = (
            ("seq-json", lambda: setattr(self.seq_server.state, "seq", Response(raw=b"{"))),
            ("seq-shape", lambda: setattr(self.seq_server.state, "seq", Response({"events": {}}))),
            ("services-shape", lambda: setattr(self.jaeger_server.state, "services", Response({"data": {}}))),
            (
                "traces-errors",
                lambda: self.jaeger_server.state.traces.__setitem__(
                    "bolt-hub", Response({"data": [], "errors": ["backend failure"]})
                ),
            ),
        )
        for name, configure in cases:
            with self.subTest(name=name):
                self.seq_server.state.reset()
                self.jaeger_server.state.reset()
                configure()
                result, output = self._run()
                self._assert_failed_without_secrets(result, output)

    def test_capped_or_truncated_results_fail_closed(self) -> None:
        cases = (
            (
                "seq-result-cap",
                lambda: setattr(self.seq_server.state, "seq", Response([{}] * 1001)),
            ),
            (
                "service-cap",
                lambda: setattr(
                    self.jaeger_server.state,
                    "services",
                    Response({"data": [f"service-{index}" for index in range(129)]}),
                ),
            ),
            (
                "trace-cap",
                lambda: self.jaeger_server.state.traces.__setitem__(
                    "bolt-hub", Response({"data": [{}] * 201})
                ),
            ),
            (
                "transport-truncated",
                lambda: setattr(
                    self.seq_server.state,
                    "seq",
                    Response(raw=b"[]", declared_length=20),
                ),
            ),
        )
        for name, configure in cases:
            with self.subTest(name=name):
                self.seq_server.state.reset()
                self.jaeger_server.state.reset()
                configure()
                result, output = self._run()
                self._assert_failed_without_secrets(result, output)

    def test_unavailable_seq_or_jaeger_fails_closed(self) -> None:
        for role in ("seq", "jaeger"):
            with self.subTest(role=role):
                self.seq_server.state.reset()
                self.jaeger_server.state.reset()
                if role == "seq":
                    self.seq_server.state.seq = Response(status=503, document={})
                else:
                    self.jaeger_server.state.services = Response(status=503, document={})
                result, output = self._run()
                self._assert_failed_without_secrets(result, output)

    def test_invalid_env_syntax_and_missing_token_path_fail(self) -> None:
        valid_lines = [f"{key}={path}" for key, path in zip(TOKEN_KEYS, self.token_paths)]
        cases = (
            "BROKEN\n",
            "\n".join(valid_lines + [valid_lines[0]]) + "\n",
            "\n".join(valid_lines[:-1]) + "\n",
        )
        for content in cases:
            with self.subTest(content_lines=len(content.splitlines())):
                self._write_env(content)
                result, output = self._run()
                self._assert_failed_without_secrets(result, output)
        self._write_env()

    def test_invalid_jwt_and_jti_fail(self) -> None:
        invalid_tokens = (
            "not-a-jwt",
            f"{_b64url({'alg': 'RS256'})}.{_b64url({'sub': 'missing-jti'})}.signature",
            f"{_b64url({'alg': 'RS256'})}.{_b64url({'jti': 'too-short'})}.signature",
        )
        original = self.token_paths[0].read_bytes()
        for token in invalid_tokens:
            with self.subTest(token_parts=token.count(".") + 1):
                self.token_paths[0].write_bytes(token.encode("ascii") + b"\n")
                self.token_paths[0].chmod(0o600)
                result, output = self._run()
                self._assert_failed_without_secrets(result, output)
        self.token_paths[0].write_bytes(original)
        self.token_paths[0].chmod(0o600)

    def test_existing_evidence_is_never_overwritten(self) -> None:
        output = self.root / "existing.json"
        output.write_text("operator-owned\n", encoding="ascii")
        output.chmod(0o600)

        result, returned_output = self._run(evidence=output)

        self.assertEqual(output, returned_output)
        self.assertNotEqual(0, result.returncode)
        self.assertEqual("operator-owned\n", output.read_text(encoding="ascii"))


if __name__ == "__main__":
    unittest.main(verbosity=2)
