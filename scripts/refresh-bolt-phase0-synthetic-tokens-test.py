#!/usr/bin/env python3

from __future__ import annotations

import base64
import contextlib
import importlib.util
import io
import json
import os
import stat
import subprocess
import sys
import tempfile
import time
import unittest
from unittest import mock
import uuid
from pathlib import Path
from typing import Any


SCRIPT = Path(__file__).with_name("refresh-bolt-phase0-synthetic-tokens.py")
SPEC = importlib.util.spec_from_file_location("bolt_phase0_refresh", SCRIPT)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError("refresh module unavailable")
refresh = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = refresh
SPEC.loader.exec_module(refresh)


TENANT_ID = "11111111-1111-4111-8111-111111111111"
CREDENTIAL_ID = "22222222-2222-4222-8222-222222222222"
ROLE_ID = "33333333-3333-4333-8333-333333333333"
GENERATION = "phase0-g2"
BASE_URL = "https://identity.test:8443"
ISSUER = "xframework"
AUDIENCE = "xframework-phase0"
USERNAME = "bolt-phase0-user"
PASSWORD = "synthetic-user-password-that-must-not-leak"
CLIENT_SECRET = "communications-client-secret-that-must-never-leak-123456789"


def private_directory(path: Path) -> Path:
    path.mkdir(mode=0o700)
    os.chmod(path, 0o700)
    return path


def private_write(path: Path, data: str | bytes) -> None:
    payload = data.encode("utf-8") if isinstance(data, str) else data
    path.write_bytes(payload)
    os.chmod(path, 0o600)


def b64url(value: bytes) -> str:
    return base64.urlsafe_b64encode(value).rstrip(b"=").decode("ascii")


def jwt(claims: dict[str, Any], *, generation: str = GENERATION) -> str:
    header = {"alg": "HS512", "kid": generation, "typ": "JWT"}
    encoded_header = b64url(json.dumps(header, separators=(",", ":"), sort_keys=True).encode())
    encoded_claims = b64url(json.dumps(claims, separators=(",", ":"), sort_keys=True).encode())
    signature = b64url(("signature-" + str(claims.get("jti", "missing"))).encode())
    return f"{encoded_header}.{encoded_claims}.{signature}"


def service_claims(now: int, jti: str, **overrides: Any) -> dict[str, Any]:
    claims = {
        "iss": ISSUER,
        "aud": AUDIENCE,
        "exp": now + 120,
        "nbf": now - 1,
        "jti": jti,
        "credential_generation": GENERATION,
        "client_credential_generation": GENERATION,
        "client_id": "XFramework.Communications",
        "service": "XFramework.Communications",
        "sub": "XFramework.Communications",
        "scope": "bolt.service",
    }
    claims.update(overrides)
    return claims


def user_claims(now: int, jti: str, **overrides: Any) -> dict[str, Any]:
    claims = {
        "iss": ISSUER,
        "aud": AUDIENCE,
        "exp": now + 900,
        "nbf": now - 1,
        "jti": jti,
        "credential_generation": GENERATION,
        "credential_id": CREDENTIAL_ID,
        "tenant_id": TENANT_ID,
        "tenantId": TENANT_ID,
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name": CREDENTIAL_ID,
        "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": json.dumps([ROLE_ID]),
    }
    claims.update(overrides)
    return claims


def service_response(claims: dict[str, Any]) -> dict[str, Any]:
    expiration = refresh._format_expiration(claims["exp"])
    return {"accessToken": jwt(claims), "tokenType": "Bearer", "expiresAtUtc": expiration}


def user_response(claims: dict[str, Any]) -> dict[str, Any]:
    return {
        "identity": {"id": "44444444-4444-4444-8444-444444444444", "tenantId": TENANT_ID},
        "credential": {"id": CREDENTIAL_ID, "tenantId": TENANT_ID, "userName": USERNAME},
        "accessToken": jwt(claims),
        "tokenType": "Bearer",
        "expiresIn": 900,
        "refreshToken": "refresh-secret-that-is-never-retained",
        "sessionId": "55555555-5555-4555-8555-555555555555",
    }


class FakeResponse:
    def __init__(self, document: dict[str, Any] | None = None, *, status: int = 200, headers: dict[str, str] | None = None):
        self.status = status
        self.body = json.dumps(document or {}).encode("utf-8")
        self.headers = {
            "Content-Type": "application/json; charset=utf-8",
            "Content-Length": str(len(self.body)),
            **(headers or {}),
        }

    def getheader(self, name: str, default: str | None = None) -> str | None:
        return self.headers.get(name, default)

    def read(self, amount: int) -> bytes:
        return self.body[:amount]


class FakeContext:
    check_hostname = True
    verify_mode = refresh.ssl.CERT_REQUIRED


class FakeConnection:
    def __init__(self, owner: "ConnectionFactory", host: str, port: int, **kwargs: Any):
        self.owner = owner
        self.host = host
        self.port = port
        self.kwargs = kwargs

    def request(self, method: str, path: str, *, body: bytes, headers: dict[str, str]) -> None:
        self.owner.requests.append((self.host, self.port, self.kwargs, method, path, body, headers))

    def getresponse(self) -> FakeResponse:
        if not self.owner.responses:
            raise AssertionError("unexpected request")
        return self.owner.responses.pop(0)

    def close(self) -> None:
        pass


class ConnectionFactory:
    def __init__(self, responses: list[FakeResponse]):
        self.responses = list(responses)
        self.requests: list[tuple[Any, ...]] = []

    def __call__(self, host: str, port: int, **kwargs: Any) -> FakeConnection:
        return FakeConnection(self, host, port, **kwargs)


class Workspace:
    def __init__(self, root: Path):
        self.root = root
        self.private = private_directory(root / "private")
        self.ca = self.private / "identity-ca.crt"
        self.env = self.private / "protected.env"
        self.communications = self.private / "communications.jwt"
        self.user = self.private / "user.jwt"
        self.expiry = self.private / "expiry.jwt"
        self.receipt = self.private / "receipt.json"
        private_write(self.ca, "test-ca-material")

    def values(self) -> dict[str, str]:
        return {
            "BOLT_SYNTHETIC_IDENTITYSERVER_BASE_URL": BASE_URL,
            "BOLT_SYNTHETIC_IDENTITYSERVER_CA_PATH": str(self.ca),
            "JWT_ISSUER": ISSUER,
            "JWT_AUDIENCE": AUDIENCE,
            "CREDENTIAL_GENERATION_ID": GENERATION,
            "COMMUNICATIONS_SERVICE_IDENTITY_SECRET": CLIENT_SECRET,
            "BOLT_SYNTHETIC_TENANT_ID": TENANT_ID,
            "BOLT_SYNTHETIC_CREDENTIAL_ID": CREDENTIAL_ID,
            "BOLT_SYNTHETIC_USER_USERNAME": USERNAME,
            "BOLT_SYNTHETIC_USER_PASSWORD": PASSWORD,
            "BOLT_SYNTHETIC_USER_ROLE_ID": ROLE_ID,
            "BOLT_SYNTHETIC_USER_AUTHORIZATION_TYPE": "Username",
            "BOLT_SYNTHETIC_MIN_TOKEN_LIFETIME_SECONDS": "60",
            "BOLT_SYNTHETIC_COMMUNICATIONS_TOKEN_PATH": str(self.communications),
            "BOLT_SYNTHETIC_USER_TOKEN_PATH": str(self.user),
            "BOLT_SYNTHETIC_EXPIRY_TOKEN_PATH": str(self.expiry),
        }

    def write_env(self, values: dict[str, str] | None = None, *, crlf: bool = False) -> None:
        lines = ["# protected Phase 0 settings"] + [f"{key}={value}" for key, value in (values or self.values()).items()]
        separator = "\r\n" if crlf else "\n"
        private_write(self.env, separator.join(lines) + separator)

    def config(self) -> refresh.Config:
        self.write_env()
        return refresh.load_config(refresh.parse_protected_env(str(self.env)))


class RefreshTokenHookTests(unittest.TestCase):
    def test_env_parser_accepts_crlf_and_full_line_comments_without_evaluation(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            workspace = Workspace(Path(temporary))
            values = workspace.values()
            values["LITERAL_VALUE"] = "$(must-not-run)"
            workspace.write_env(values, crlf=True)

            parsed = refresh.parse_protected_env(str(workspace.env))

            self.assertEqual(parsed["LITERAL_VALUE"], "$(must-not-run)")

    def test_env_parser_accepts_compose_and_dotnet_mixed_case_keys(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            workspace = Workspace(Path(temporary))
            values = workspace.values()
            values["ControlPanel__BootstrapAdmin__Password"] = "opaque-secret"
            values["_COMPOSE_PRIVATE_SETTING"] = "literal"
            workspace.write_env(values)

            parsed = refresh.parse_protected_env(str(workspace.env))

            self.assertEqual(
                parsed["ControlPanel__BootstrapAdmin__Password"], "opaque-secret"
            )
            self.assertEqual(parsed["_COMPOSE_PRIVATE_SETTING"], "literal")
            refresh.load_config(parsed)

    def test_env_parser_rejects_duplicate_ambiguous_and_control_syntax(self) -> None:
        cases = {
            "duplicate": "KEY=value\nKEY=second\n",
            "inline-comment": "KEY=value # ambiguous\n",
            "leading-space": " KEY=value\n",
            "bare-cr": "KEY=value\rNEXT=value\n",
            "control": "KEY=bad\x01value\n",
            "comment-control": "# bad\u0085comment\nKEY=value\n",
            "unicode-line": "KEY=first\u2028second\n",
            "export": "export KEY=value\n",
        }
        for name, content in cases.items():
            with self.subTest(name=name), tempfile.TemporaryDirectory() as temporary:
                root = private_directory(Path(temporary) / "private")
                path = root / "env"
                private_write(path, content.encode("utf-8"))
                with self.assertRaises(refresh.RefreshError):
                    refresh.parse_protected_env(str(path))

    def test_http_base_url_is_rejected_before_network_access(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            workspace = Workspace(Path(temporary))
            values = workspace.values()
            values["BOLT_SYNTHETIC_IDENTITYSERVER_BASE_URL"] = "http://identity.test:8080"
            workspace.write_env(values)
            with self.assertRaisesRegex(refresh.RefreshError, "CONFIGURATION"):
                refresh.load_config(refresh.parse_protected_env(str(workspace.env)))

    def test_non_url_jwt_issuer_is_accepted_as_exact_configured_identifier(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            workspace = Workspace(Path(temporary))
            config = workspace.config()

            self.assertEqual(config.base_url, BASE_URL)
            self.assertEqual(config.issuer, "xframework")

    def test_direct_https_uses_explicit_ca_hostname_validation_and_ignores_proxy_environment(self) -> None:
        now = int(time.time())
        with tempfile.TemporaryDirectory() as temporary:
            workspace = Workspace(Path(temporary))
            config = workspace.config()
            factory = ConnectionFactory([FakeResponse(service_response(service_claims(now, uuid.uuid4().hex)))])
            observed_ca: list[str] = []

            def context_factory(ca_path: str) -> FakeContext:
                observed_ca.append(ca_path)
                return FakeContext()

            with mock.patch.dict(os.environ, {"HTTPS_PROXY": "http://attacker.invalid:3128"}):
                refresh._post_json(
                    config,
                    "/api/service-identity/bolt-transport-token",
                    {"clientId": "XFramework.Communications", "clientSecret": CLIENT_SECRET},
                    connection_factory=factory,
                    context_factory=context_factory,
                )

            self.assertEqual(observed_ca, [str(workspace.ca)])
            self.assertEqual(factory.requests[0][0:2], ("identity.test", 8443))
            self.assertIs(factory.requests[0][2]["context"].check_hostname, True)
            self.assertEqual(factory.requests[0][2]["context"].verify_mode, refresh.ssl.CERT_REQUIRED)
            self.assertNotIn("attacker.invalid", repr(factory.requests))

    def test_redirect_and_non_json_responses_fail_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            config = Workspace(Path(temporary)).config()
            for name, response in {
                "redirect": FakeResponse(status=302, headers={"Location": "http://attacker.invalid"}),
                "content-type": FakeResponse(headers={"Content-Type": "text/plain"}),
                "encoding": FakeResponse(headers={"Content-Encoding": "gzip"}),
            }.items():
                with self.subTest(name=name):
                    factory = ConnectionFactory([response])
                    with self.assertRaises(refresh.RefreshError):
                        refresh._post_json(
                            config,
                            "/api/auth/authenticate",
                            {},
                            connection_factory=factory,
                            context_factory=lambda _: FakeContext(),
                        )

    def test_bare_response_schemas_require_exact_key_sets(self) -> None:
        now = int(time.time())
        with tempfile.TemporaryDirectory() as temporary:
            config = Workspace(Path(temporary)).config()
            service = service_response(service_claims(now, uuid.uuid4().hex))
            user = user_response(user_claims(now, str(uuid.uuid4())))

            for name, document in {
                "service-extra": {**service, "unexpected": True},
                "service-missing": {key: value for key, value in service.items() if key != "tokenType"},
            }.items():
                with self.subTest(name=name):
                    with self.assertRaises(refresh.RefreshError):
                        refresh._parse_service_token(document, config, now=now, expiry=False)

            for name, document in {
                "user-extra": {**user, "unexpected": True},
                "user-missing": {key: value for key, value in user.items() if key != "sessionId"},
            }.items():
                with self.subTest(name=name):
                    with self.assertRaises(refresh.RefreshError):
                        refresh._parse_user_token(document, config, now=now)

    def test_user_response_fields_are_semantically_validated(self) -> None:
        now = int(time.time())
        with tempfile.TemporaryDirectory() as temporary:
            config = Workspace(Path(temporary)).config()
            valid = user_response(user_claims(now, str(uuid.uuid4())))
            cases = {
                "token-type": {**valid, "tokenType": "bearer"},
                "expires-bool": {**valid, "expiresIn": True},
                "expires-zero": {**valid, "expiresIn": 0},
                "expires-mismatch": {**valid, "expiresIn": 1},
                "refresh-empty": {**valid, "refreshToken": ""},
                "refresh-whitespace": {**valid, "refreshToken": "contains whitespace"},
                "session-invalid": {**valid, "sessionId": "not-a-guid"},
                "session-empty": {**valid, "sessionId": "00000000-0000-0000-0000-000000000000"},
            }
            for name, document in cases.items():
                with self.subTest(name=name), self.assertRaisesRegex(
                    refresh.RefreshError, "RESPONSE_SCHEMA"
                ):
                    refresh._parse_user_token(document, config, now=now)

    def test_legacy_result_envelope_is_rejected(self) -> None:
        now = int(time.time())
        with tempfile.TemporaryDirectory() as temporary:
            config = Workspace(Path(temporary)).config()
            legacy_envelope = {
                "data": service_response(service_claims(now, uuid.uuid4().hex)),
                "isSuccess": True,
                "message": None,
                "statusCode": 200,
                "errors": None,
            }

            with self.assertRaisesRegex(refresh.RefreshError, "RESPONSE_SCHEMA"):
                refresh._parse_service_token(legacy_envelope, config, now=now, expiry=False)

    def test_malformed_and_mismatched_tokens_are_rejected(self) -> None:
        now = int(time.time())
        with tempfile.TemporaryDirectory() as temporary:
            config = Workspace(Path(temporary)).config()
            cases = {
                "shape": {"accessToken": "not-a-jwt", "tokenType": "Bearer", "expiresAtUtc": refresh._format_expiration(now + 120)},
                "generation": service_response(service_claims(now, uuid.uuid4().hex)),
                "identity": service_response(service_claims(now, uuid.uuid4().hex, service="XFramework.Portal")),
                "issuer": service_response(service_claims(now, uuid.uuid4().hex, iss="https://wrong.test")),
                "audience": service_response(service_claims(now, uuid.uuid4().hex, aud="wrong")),
                "expired": service_response(service_claims(now, uuid.uuid4().hex, exp=now - 1)),
                "future-nbf": service_response(service_claims(now, uuid.uuid4().hex, nbf=now + 120)),
            }
            generation_claims = service_claims(now, uuid.uuid4().hex, credential_generation="wrong-generation")
            cases["generation"] = {
                "accessToken": jwt(generation_claims, generation="wrong-generation"),
                "tokenType": "Bearer",
                "expiresAtUtc": refresh._format_expiration(generation_claims["exp"]),
            }
            for name, document in cases.items():
                with self.subTest(name=name):
                    with self.assertRaises(refresh.RefreshError):
                        refresh._parse_service_token(document, config, now=now, expiry=False)

    def test_user_token_must_bind_expected_tenant_and_credential(self) -> None:
        now = int(time.time())
        with tempfile.TemporaryDirectory() as temporary:
            config = Workspace(Path(temporary)).config()
            wrong_claim = user_response(user_claims(now, uuid.uuid4().hex, credential_id=ROLE_ID))
            wrong_response = user_response(user_claims(now, uuid.uuid4().hex))
            wrong_response["credential"]["tenantId"] = ROLE_ID
            for document in (wrong_claim, wrong_response):
                with self.assertRaises(refresh.RefreshError):
                    refresh._parse_user_token(document, config, now=now)

    def test_destinations_reject_duplicate_paths_broad_parents_links_and_hardlinks(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            private = private_directory(root / "private")
            first = private / "first"
            second = private / "second"
            private_write(first, "token")
            with self.assertRaisesRegex(refresh.RefreshError, "DESTINATION_ALIAS"):
                refresh.validate_destinations([str(first), str(first)])

            if refresh.ENFORCE_POSIX_PERMISSIONS:
                broad = root / "broad"
                broad.mkdir()
                os.chmod(broad, 0o755)
                with self.assertRaisesRegex(refresh.RefreshError, "DESTINATION"):
                    refresh.validate_destinations([str(broad / "token")])

            try:
                os.link(first, second)
            except OSError:
                pass
            else:
                with self.assertRaises(refresh.RefreshError):
                    refresh.validate_destinations([str(first), str(second)])

            link = private / "link"
            try:
                os.symlink(first, link)
            except (OSError, NotImplementedError):
                pass
            else:
                with self.assertRaises(refresh.RefreshError):
                    refresh.validate_destinations([str(link)])

    def test_atomic_replace_creates_owner_only_regular_file_and_replaces_inode(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            private = private_directory(Path(temporary) / "private")
            target = private / "token"
            private_write(target, "old")
            old = os.stat(target, follow_symlinks=False)

            refresh.atomic_replace(str(target), b"new-value\n")

            current = os.stat(target, follow_symlinks=False)
            self.assertTrue(stat.S_ISREG(current.st_mode))
            if refresh.ENFORCE_POSIX_PERMISSIONS:
                self.assertEqual(stat.S_IMODE(current.st_mode) & 0o077, 0)
            self.assertEqual(target.read_bytes(), b"new-value\n")
            self.assertNotEqual((old.st_dev, old.st_ino), (current.st_dev, current.st_ino))

    def test_success_refreshes_distinct_tokens_and_writes_only_nonsecret_receipt(self) -> None:
        now = int(time.time())
        communications_claims = service_claims(now, uuid.uuid4().hex)
        expiry_claims = service_claims(now, uuid.uuid4().hex, exp=now + 90)
        current_user_claims = user_claims(now, str(uuid.uuid4()))
        with tempfile.TemporaryDirectory() as temporary:
            workspace = Workspace(Path(temporary))
            workspace.write_env()
            factory = ConnectionFactory(
                [
                    FakeResponse(service_response(communications_claims)),
                    FakeResponse(service_response(expiry_claims)),
                    FakeResponse(user_response(current_user_claims)),
                ]
            )
            stdout = io.StringIO()
            stderr = io.StringIO()
            with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
                refresh.execute(
                    str(workspace.env),
                    str(workspace.receipt),
                    True,
                    connection_factory=factory,
                    context_factory=lambda _: FakeContext(),
                    now_provider=lambda: now,
                )

            self.assertEqual(stdout.getvalue(), "")
            self.assertEqual(stderr.getvalue(), "")
            self.assertEqual(workspace.communications.read_text().strip(), jwt(communications_claims))
            self.assertEqual(workspace.expiry.read_text().strip(), jwt(expiry_claims))
            self.assertEqual(workspace.user.read_text().strip(), jwt(current_user_claims))
            receipt = json.loads(workspace.receipt.read_text())
            self.assertEqual(
                set(receipt),
                {"schemaVersion", "status", "issuerUri", "principalReference", "refreshedAtUtc", "tokenExpirationsUtc"},
            )
            receipt_text = workspace.receipt.read_text()
            for secret in (CLIENT_SECRET, PASSWORD, jwt(communications_claims), jwt(expiry_claims), jwt(current_user_claims)):
                self.assertNotIn(secret, receipt_text)
            for path in (workspace.communications, workspace.user, workspace.expiry, workspace.ca, workspace.env):
                self.assertNotIn(str(path), receipt_text)
            self.assertEqual([request[4] for request in factory.requests], [
                "/api/service-identity/bolt-transport-token",
                "/api/service-identity/bolt-transport-token",
                "/api/auth/authenticate",
            ])
            first_body = json.loads(factory.requests[0][5])
            user_body = json.loads(factory.requests[2][5])
            self.assertEqual(first_body, {"clientId": "XFramework.Communications", "clientSecret": CLIENT_SECRET})
            self.assertEqual(user_body["password"], PASSWORD)
            self.assertEqual(user_body["metadata"]["tenantId"], TENANT_ID)
            self.assertEqual(user_body["metadata"]["credentialId"], CREDENTIAL_ID)
            self.assertEqual(user_body["metadata"]["name"], "bolt-phase0-synthetic")
            self.assertEqual(user_body["metadata"]["deviceName"], "bolt-phase0-synthetic")
            self.assertEqual(user_body["metadata"]["deviceAgent"], "bolt-phase0-synthetic")

    def test_disabled_expiry_writes_private_empty_placeholder_and_omits_receipt_entry(self) -> None:
        now = int(time.time())
        with tempfile.TemporaryDirectory() as temporary:
            workspace = Workspace(Path(temporary))
            workspace.write_env()
            factory = ConnectionFactory(
                [
                    FakeResponse(service_response(service_claims(now, uuid.uuid4().hex))),
                    FakeResponse(service_response(service_claims(now, uuid.uuid4().hex, exp=now + 90))),
                    FakeResponse(user_response(user_claims(now, str(uuid.uuid4())))),
                ]
            )
            refresh.execute(
                str(workspace.env),
                str(workspace.receipt),
                False,
                connection_factory=factory,
                context_factory=lambda _: FakeContext(),
                now_provider=lambda: now,
            )
            self.assertEqual(workspace.expiry.read_bytes(), b"")
            self.assertEqual(set(json.loads(workspace.receipt.read_text())["tokenExpirationsUtc"]), {"communications", "user"})
            if refresh.ENFORCE_POSIX_PERMISSIONS:
                self.assertEqual(stat.S_IMODE(workspace.expiry.stat().st_mode) & 0o077, 0)

    def test_subprocess_failure_emits_only_fixed_secret_free_code(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            workspace = Workspace(Path(temporary))
            private_write(
                workspace.env,
                f"SECRET={CLIENT_SECRET}\nDUPLICATE={PASSWORD}\nDUPLICATE=second\n",
            )
            environment = {
                "PATH": os.environ.get("PATH", ""),
                "XFRAMEWORK_ENV_FILE": str(workspace.env),
                "BOLT_SYNTHETIC_REFRESH_RECEIPT": str(workspace.receipt),
                "BOLT_SYNTHETIC_EXPIRY_ENABLED": "true",
            }
            process = subprocess.run(
                [sys.executable, str(SCRIPT)],
                env=environment,
                stdin=subprocess.DEVNULL,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
                check=False,
            )
            self.assertEqual(process.returncode, 1)
            self.assertEqual(process.stdout, "")
            self.assertEqual(process.stderr, "BOLT_PHASE0_REFRESH_ENV_SYNTAX\n")
            self.assertNotIn(CLIENT_SECRET, process.stderr)
            self.assertNotIn(PASSWORD, process.stderr)
            self.assertNotIn(str(workspace.env), process.stderr)
            self.assertNotIn(str(workspace.receipt), process.stderr)


if __name__ == "__main__":
    unittest.main(verbosity=2)
