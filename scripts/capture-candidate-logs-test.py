#!/usr/bin/env python3
"""Tests for capture-candidate-logs.py: secrets never reach the printed excerpt, and the failure does."""

from __future__ import annotations

import base64
import contextlib
import importlib.util
import io
import json
import os
import tempfile
import uuid
import unittest
from pathlib import Path
from unittest import mock

SCRIPT = Path(__file__).with_name("capture-candidate-logs.py")
spec = importlib.util.spec_from_file_location("capture_candidate_logs", SCRIPT)
capture = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(capture)


# Generated at run time: no credential-shaped literal lives in the repository.
def _segment(value: object) -> str:
    return base64.urlsafe_b64encode(json.dumps(value).encode()).rstrip(b"=").decode()


JWT = ".".join([_segment({"alg": "RS256"}), _segment({"sub": uuid.uuid4().hex}), _segment(uuid.uuid4().hex)])
CLIENT_SECRET = "client-" + uuid.uuid4().hex
DB_PASSWORD = "db-" + uuid.uuid4().hex
TOKEN_FILE_VALUE = "marker-" + uuid.uuid4().hex
BASIC = base64.b64encode(f"user:{uuid.uuid4().hex}".encode()).decode()
API_KEY = uuid.uuid4().hex[:12]
PEM_BODY = uuid.uuid4().hex
PEM = "\n".join(["-----BEGIN " + "TEST KEY-----", PEM_BODY, "-----END " + "TEST KEY-----"])


def write_env(directory: Path) -> Path:
    env = directory / "candidate.env"
    env.write_text(
        "\n".join([
            "# comment",
            f"PORTAL_SERVICE_IDENTITY_SECRET={CLIENT_SECRET}",
            f"DefaultDatabaseConnection=Host=postgres;Username=app;Password={DB_PASSWORD}",
            "BOLT_SYNTHETIC_TENANT_ID=0ec87d7b-50ee-4936-8528-1e953add4eee",
            "ASPNETCORE_ENVIRONMENT=Docker",
        ]) + "\n",
        encoding="utf-8",
    )
    tokens = directory / "synthetic-tokens"
    tokens.mkdir()
    (tokens / "user-actor-token").write_text(TOKEN_FILE_VALUE + "\n", encoding="utf-8")
    return env


LOG = "\n".join([
    "2026-10-03T14:57:20Z info: Microsoft.Hosting.Lifetime[0] Application started.",
    f"2026-10-03T14:57:21Z info: request with Authorization: Bearer {JWT}",
    "2026-10-03T14:57:26Z fail: IdentityServer.Api.Services.AuthService[5001]",
    "2026-10-03T14:57:26Z       Operation Authenticate failed for IdentityCredential 00000000-0000-0000-0000-000000000000: "
    "Tenant '0ec87d7b-50ee-4936-8528-1e953add4eee' could not be found.",
    "2026-10-03T14:57:26Z System.InvalidOperationException: Tenant '0ec87d7b-50ee-4936-8528-1e953add4eee' could not be found.",
    "2026-10-03T14:57:26Z    at XFramework.Core.Services.TenantResolver.GetTenant(Nullable`1 id, CancellationToken ct)",
    f"2026-10-03T14:57:27Z warn: token refresh failed clientSecret={CLIENT_SECRET} password: {DB_PASSWORD} jwt {JWT}",
    f"2026-10-03T14:57:27Z error: marker leaked {TOKEN_FILE_VALUE} and {'Ab3' * 22}",
    "2026-10-03T14:57:28Z info: unrelated healthy line",
])


class CaptureCandidateLogsTests(unittest.TestCase):
    def test_excerpt_keeps_the_failure_and_its_stack_but_no_secret(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            env = write_env(directory)
            secrets = capture.env_secrets(str(env)) + capture.file_secrets(str(directory / "synthetic-tokens"))

            text = "\n".join(capture.excerpt(LOG, secrets))

        self.assertIn("Tenant '0ec87d7b-50ee-4936-8528-1e953add4eee' could not be found.", text)
        self.assertIn("at XFramework.Core.Services.TenantResolver.GetTenant", text)
        self.assertNotIn("unrelated healthy line", text)
        self.assertNotIn("Application started", text)
        for secret in (JWT, CLIENT_SECRET, DB_PASSWORD, TOKEN_FILE_VALUE, "Ab3" * 22):
            self.assertNotIn(secret, text)

    def test_redact_removes_credential_shapes_without_known_values(self) -> None:
        text = capture.redact(
            f"Authorization: Basic {BASIC} api_key={API_KEY} token={JWT} {PEM}",
            [],
        )
        self.assertNotIn(BASIC, text)
        self.assertNotIn(API_KEY, text)
        self.assertNotIn("eyJ", text)
        self.assertNotIn(PEM_BODY, text)

    def test_capture_keeps_full_log_owner_only_and_prints_redacted_excerpt(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            env = write_env(directory)
            stdout = io.StringIO()
            with mock.patch.object(capture, "container_logs", side_effect=lambda c, s: LOG if c == "xframework-identityserver" else None), \
                    contextlib.redirect_stdout(stdout):
                status = capture.main(["--run-dir", str(directory), "--env-file", str(env), "--since", "1759503000"])

            saved = directory / "candidate-logs" / "identityserver.log"
            self.assertEqual(status, 0)
            self.assertEqual(saved.read_text(encoding="utf-8"), LOG)
            if os.name == "posix":
                self.assertEqual(saved.stat().st_mode & 0o077, 0)
            self.assertFalse((directory / "candidate-logs" / "bolt-hub.log").exists())
            printed = stdout.getvalue()
            self.assertIn("xframework-identityserver", printed)
            self.assertIn("could not be found", printed)
            for secret in (JWT, CLIENT_SECRET, DB_PASSWORD, TOKEN_FILE_VALUE):
                self.assertNotIn(secret, printed)

    def test_failures_never_fail_the_step(self) -> None:
        with mock.patch.object(capture, "capture", side_effect=RuntimeError("boom")), \
                contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(capture.main(["--run-dir", "/nonexistent", "--env-file", "/nonexistent"]), 0)


if __name__ == "__main__":
    unittest.main()
