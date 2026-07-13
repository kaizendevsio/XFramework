#!/usr/bin/env python3
from __future__ import annotations

import base64
import contextlib
import copy
import importlib.util
import io
import json
import os
import tempfile
import unittest
from datetime import datetime, timedelta, timezone
from pathlib import Path
from unittest import mock


SCRIPT = Path(__file__).with_name("verify-bolt-phase0-credential-convergence.py")
SPEC = importlib.util.spec_from_file_location("phase0_credential_convergence", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


BASE = datetime(2026, 7, 13, 2, 0, 0, tzinfo=timezone.utc)
TARGET = "credential-g2"
RETIRING = "credential-g1"
SERVICES = ["bolt-hub", "identityserver", "communications"]
CLIENTS = ["XFramework.Communications", "XFramework.Portal"]
SECRET = "never-retain-this-secret"


def timestamp(seconds: int) -> str:
    return (BASE + timedelta(seconds=seconds)).strftime("%Y-%m-%dT%H:%M:%S.%fZ")


def diagnostic(phase: str, *, expiry: str | None = None) -> dict:
    result = {
        "configured": True,
        "currentGenerationId": TARGET,
        "validationFallbackConfigured": phase == "dual-validation",
    }
    if phase == "dual-validation":
        result.update(
            {
                "validationFallbackGenerationId": RETIRING,
                "validationFallbackValidUntilUtc": expiry or timestamp(600),
                "validationFallbackActive": True,
            }
        )
    return result


def credential_data(service: str, phase: str, *, expiry: str | None = None) -> dict:
    return {
        "jwt": diagnostic(phase, expiry=expiry),
        "serviceCredential": diagnostic(phase, expiry=expiry),
        "identityServerClients": {
            client: diagnostic(phase, expiry=expiry) for client in CLIENTS
        }
        if service == "identityserver"
        else {},
    }


def check(name: str, data: dict) -> dict:
    return {
        "name": name,
        "status": "Healthy",
        "description": f"safe description {SECRET}",
        "duration": 1.5,
        "tags": ["ready", "security", "credentials"],
        "data": data,
        "exception": None,
    }


def service(name: str, phase: str, *, expiry: str | None = None) -> dict:
    return {
        "name": name,
        "http_status": 200,
        "health": {
            "status": "Healthy",
            "duration": 2.5,
            "timestamp": timestamp(0),
            "checks": [check("credential-generations", credential_data(name, phase, expiry=expiry))],
        },
    }


def document(phase: str = "dual-validation") -> dict:
    return {
        "schema": MODULE.INPUT_SCHEMA,
        "collected_at_utc": timestamp(1),
        "target_generation_id": TARGET,
        "retiring_generation_id": RETIRING,
        "phase": phase,
        "identityserver_service": "identityserver",
        "expected_services": list(SERVICES),
        "expected_identityserver_clients": list(CLIENTS),
        "services": [service(name, phase) for name in SERVICES],
    }


def b64url(value: bytes) -> str:
    return base64.urlsafe_b64encode(value).rstrip(b"=").decode("ascii")


def jwt(
    generation: str,
    *,
    kind: str = "jwt",
    kid: str | None = None,
    overrides: dict | None = None,
) -> str:
    header = {"alg": "HS512" if kind == "jwt" else "RS256", "kid": kid or generation, "typ": "JWT"}
    claims = (
        {"credential_generation": generation}
        if kind == "jwt"
        else {"client_credential_generation": generation}
    )
    claims.update(overrides or {})
    return ".".join(
        (
            b64url(json.dumps(header, separators=(",", ":"), sort_keys=True).encode()),
            b64url(json.dumps(claims, separators=(",", ":"), sort_keys=True).encode()),
            b64url(b"synthetic-signature"),
        )
    )


def private_write(path: Path, value: str) -> None:
    path.write_text(value, encoding="ascii")
    os.chmod(path, 0o600)


class CredentialConvergenceVerifierTests(unittest.TestCase):
    def evaluate(self, value: dict) -> dict:
        metadata, errors = MODULE.validate_document(value, 30)
        self.assertEqual([], errors)
        return metadata

    def run_cli(
        self,
        root: Path,
        value: dict | str,
        *,
        current: list[tuple[str, Path]] | None = None,
        retired: list[tuple[str, Path]] | None = None,
    ) -> tuple[int, dict, str, str]:
        input_path = root / "input.json"
        output_path = root / "evidence.json"
        input_path.write_text(value if isinstance(value, str) else json.dumps(value), encoding="utf-8")
        arguments = [
            "--input",
            str(input_path),
            "--output",
            str(output_path),
            "--maximum-health-age-seconds",
            "30",
        ]
        for kind, path in current or []:
            arguments.extend(("--current-jwt", kind, str(path)))
        for kind, path in retired or []:
            arguments.extend(("--retired-jwt", kind, str(path)))
        stdout = io.StringIO()
        stderr = io.StringIO()
        with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
            exit_code = MODULE.main(arguments)
        evidence = json.loads(output_path.read_text(encoding="utf-8"))
        return exit_code, evidence, stdout.getvalue(), stderr.getvalue()

    def test_valid_dual_validation_state_and_both_token_kinds_pass(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            current_jwt = root / "current-jwt"
            current_service = root / "current-service"
            retired_jwt = root / "retired-jwt"
            retired_service = root / "retired-service"
            private_write(current_jwt, jwt(TARGET))
            private_write(current_service, jwt(TARGET, kind="service", kid="rsa-signing-key-42"))
            private_write(retired_jwt, jwt(RETIRING))
            private_write(retired_service, jwt(RETIRING, kind="service", kid="rsa-signing-key-41"))

            exit_code, evidence, _, stderr = self.run_cli(
                root,
                document(),
                current=[("jwt", current_jwt), ("service", current_service)],
                retired=[("jwt", retired_jwt), ("service", retired_service)],
            )

            self.assertEqual(0, exit_code, stderr)
            self.assertEqual("passed", evidence["status"])
            self.assertEqual("dual-validation", evidence["phase"])
            self.assertEqual(timestamp(600), evidence["fallback_valid_until_utc"])
            self.assertEqual(len(SERVICES), evidence["service_count"])
            self.assertEqual(len(CLIENTS), evidence["identityserver_client_count"])
            self.assertEqual(2, evidence["current_token_count"])
            self.assertEqual(2, evidence["retired_token_count"])
            self.assertEqual([], evidence["errors"])
            self.assertEqual(
                {
                    "schema",
                    "generated_at_utc",
                    "observed_at_utc",
                    "fallback_valid_until_utc",
                    "phase",
                    "target_generation_id",
                    "retiring_generation_id",
                    "service_count",
                    "identityserver_client_count",
                    "current_token_count",
                    "retired_token_count",
                    "status",
                    "errors",
                },
                set(evidence),
            )
            if os.name != "nt":
                self.assertEqual(0o600, (root / "evidence.json").stat().st_mode & 0o777)

    def test_token_coverage_requires_both_current_and_retired_kinds_during_dual_validation(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            current_jwt = root / "current-jwt"
            retired_jwt = root / "retired-jwt"
            private_write(current_jwt, jwt(TARGET))
            private_write(retired_jwt, jwt(RETIRING))

            exit_code, evidence, _, _ = self.run_cli(
                root,
                document(),
                current=[("jwt", current_jwt)],
                retired=[("jwt", retired_jwt)],
            )

            self.assertEqual(1, exit_code)
            self.assertEqual(["TOKEN_COVERAGE"], evidence["errors"])

    def test_finalized_token_coverage_requires_current_kinds_and_forbids_retired_inputs(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            current_jwt = root / "current-jwt"
            current_service = root / "current-service"
            retired_jwt = root / "retired-jwt"
            private_write(current_jwt, jwt(TARGET))
            private_write(current_service, jwt(TARGET, kind="service", kid="rsa-signing-key-42"))
            private_write(retired_jwt, jwt(RETIRING))

            finalized = document("finalized")
            exit_code, evidence, _, _ = self.run_cli(
                root,
                finalized,
                current=[("jwt", current_jwt), ("service", current_service)],
                retired=[("jwt", retired_jwt)],
            )

            self.assertEqual(1, exit_code)
            self.assertEqual(["TOKEN_COVERAGE"], evidence["errors"])

    def test_valid_finalized_state_passes_without_fallback_or_tokens(self) -> None:
        metadata = self.evaluate(document("finalized"))

        self.assertEqual("finalized", metadata["phase"])
        self.assertIsNone(metadata["fallback_valid_until_utc"])
        self.assertEqual(len(SERVICES), metadata["service_count"])

    def test_env_string_only_spoof_is_not_accepted(self) -> None:
        value = document()
        value["services"][0]["health"]["checks"][0]["data"] = {
            "credential_generation_id": TARGET
        }

        with self.assertRaises(MODULE.SafeValidationError) as raised:
            self.evaluate(value)
        self.assertEqual("CREDENTIAL_DATA_SCHEMA", raised.exception.code)

    def test_missing_extra_and_duplicate_service_coverage_fail(self) -> None:
        scenarios: list[dict] = []
        missing = document()
        missing["services"].pop()
        scenarios.append(missing)
        extra = document()
        extra["services"].append(service("unexpected", "dual-validation"))
        scenarios.append(extra)
        duplicate = document()
        duplicate["services"][2] = copy.deepcopy(duplicate["services"][0])
        scenarios.append(duplicate)
        expected_extra = document()
        expected_extra["expected_services"].append("unexpected")
        scenarios.append(expected_extra)

        for value in scenarios:
            with self.subTest(value=value), self.assertRaises(MODULE.SafeValidationError) as raised:
                self.evaluate(value)
            self.assertEqual("SERVICE_COVERAGE", raised.exception.code)

    def test_missing_extra_and_duplicate_client_coverage_fail(self) -> None:
        scenarios: list[dict] = []
        missing = document()
        del missing["services"][1]["health"]["checks"][0]["data"]["identityServerClients"][CLIENTS[0]]
        scenarios.append(missing)
        extra = document()
        extra["services"][1]["health"]["checks"][0]["data"]["identityServerClients"]["extra-client"] = diagnostic(
            "dual-validation"
        )
        scenarios.append(extra)
        duplicate_expected = document()
        duplicate_expected["expected_identityserver_clients"].append(CLIENTS[0])
        scenarios.append(duplicate_expected)
        non_identity_clients = document()
        non_identity_clients["services"][0]["health"]["checks"][0]["data"]["identityServerClients"] = {
            CLIENTS[0]: diagnostic("dual-validation")
        }
        scenarios.append(non_identity_clients)

        for value in scenarios:
            with self.subTest(value=value), self.assertRaises(MODULE.SafeValidationError) as raised:
                self.evaluate(value)
            self.assertEqual("CLIENT_COVERAGE", raised.exception.code)

    def test_inconsistent_or_expired_fallback_fails(self) -> None:
        inconsistent = document()
        inconsistent["services"][2]["health"]["checks"][0]["data"]["jwt"][
            "validationFallbackValidUntilUtc"
        ] = timestamp(601)
        expired = document()
        for item in expired["services"]:
            data = item["health"]["checks"][0]["data"]
            for candidate in [data["jwt"], data["serviceCredential"], *data["identityServerClients"].values()]:
                candidate["validationFallbackValidUntilUtc"] = timestamp(0)

        for value in (inconsistent, expired):
            with self.subTest(value=value), self.assertRaises(MODULE.SafeValidationError) as raised:
                self.evaluate(value)
            self.assertEqual("FALLBACK_EXPIRY", raised.exception.code)

    def test_finalized_fallback_residue_fails(self) -> None:
        value = document("finalized")
        value["services"][0]["health"]["checks"][0]["data"]["jwt"][
            "validationFallbackConfigured"
        ] = True

        with self.assertRaises(MODULE.SafeValidationError) as raised:
            self.evaluate(value)
        self.assertEqual("FALLBACK_RESIDUE", raised.exception.code)

    def test_current_and_retired_claim_or_kid_mismatch_fail(self) -> None:
        cases = [
            ("current", "jwt", jwt(RETIRING), "CURRENT_TOKEN_GENERATION"),
            ("current", "jwt", jwt(TARGET, kid=RETIRING), "CURRENT_TOKEN_GENERATION"),
            (
                "current",
                "service",
                jwt(RETIRING, kind="service", kid="rsa-key"),
                "CURRENT_TOKEN_GENERATION",
            ),
            ("retired", "jwt", jwt(TARGET), "RETIRED_TOKEN_GENERATION"),
            (
                "retired",
                "service",
                jwt(TARGET, kind="service", kid="rsa-key"),
                "RETIRED_TOKEN_GENERATION",
            ),
            (
                "current",
                "service",
                jwt(
                    TARGET,
                    kind="service",
                    kid="rsa-key",
                    overrides={"credential_generation": RETIRING},
                ),
                "CURRENT_TOKEN_GENERATION",
            ),
        ]
        for group, kind, token, expected_error in cases:
            with self.subTest(group=group, kind=kind), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                path = root / "token"
                private_write(path, token)
                current = [(kind, path)] if group == "current" else []
                retired = [(kind, path)] if group == "retired" else []
                exit_code, evidence, _, _ = self.run_cli(
                    root, document(), current=current, retired=retired
                )
                self.assertEqual(1, exit_code)
                self.assertEqual([expected_error], evidence["errors"])

    def test_malformed_jwt_and_duplicate_claims_fail_closed(self) -> None:
        duplicate_claims = (
            b64url(json.dumps({"alg": "HS512", "kid": TARGET}).encode())
            + "."
            + b64url(
                (
                    '{"credential_generation":"credential-g2",'
                    '"credential_generation":"credential-g1"}'
                ).encode()
            )
            + "."
            + b64url(b"signature")
        )
        for token in ("not-a-jwt", "e30.e30.bad=padding", duplicate_claims, "e30.e30.c2ln"):
            with self.subTest(token=token), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                path = root / "token"
                private_write(path, token)
                exit_code, evidence, _, _ = self.run_cli(
                    root, document(), current=[("jwt", path)]
                )
                self.assertEqual(1, exit_code)
                self.assertEqual(["CURRENT_TOKEN_INVALID"], evidence["errors"])

    def test_duplicate_nan_oversize_timestamp_and_schema_inputs_fail(self) -> None:
        cases: list[tuple[str, str]] = [
            ('{"schema":"one","schema":"two"}', "normal"),
            ('{"schema":NaN}', "normal"),
            (json.dumps({**document(), "unexpected": True}), "normal"),
        ]
        future = document()
        future["services"][0]["health"]["timestamp"] = timestamp(2)
        cases.append((json.dumps(future), "normal"))
        noncanonical = document()
        noncanonical["collected_at_utc"] = BASE.isoformat()
        cases.append((json.dumps(noncanonical), "normal"))

        for raw, _ in cases:
            with self.subTest(raw=raw[:40]), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                exit_code, evidence, _, _ = self.run_cli(root, raw)
                self.assertEqual(1, exit_code)
                self.assertEqual("failed", evidence["status"])

        with tempfile.TemporaryDirectory() as temporary, mock.patch.object(
            MODULE, "MAX_INPUT_BYTES", 64
        ):
            root = Path(temporary)
            exit_code, evidence, _, _ = self.run_cli(root, document())
            self.assertEqual(1, exit_code)
            self.assertEqual(["INPUT_INVALID"], evidence["errors"])

    def test_secret_token_path_claims_health_and_exceptions_are_never_disclosed(self) -> None:
        with tempfile.TemporaryDirectory(prefix=f"{SECRET}-") as temporary:
            root = Path(temporary)
            token_path = root / f"{SECRET}.jwt"
            private_write(
                token_path,
                jwt(
                    TARGET,
                    overrides={
                        "sub": SECRET,
                        "private_claim": SECRET,
                        "credential_generation": RETIRING,
                    },
                ),
            )
            value = document()
            value["services"][0]["health"]["checks"][0]["exception"] = SECRET

            exit_code, evidence, stdout, stderr = self.run_cli(
                root, value, current=[("jwt", token_path)]
            )

            self.assertEqual(1, exit_code)
            serialized = json.dumps(evidence, sort_keys=True)
            for output in (serialized, stdout, stderr):
                self.assertNotIn(SECRET, output)
                self.assertNotIn(str(token_path), output)
                self.assertNotIn("private_claim", output)
            self.assertEqual(["CREDENTIAL_CHECK_STATUS"], evidence["errors"])

    def test_duplicate_or_cross_generation_token_file_references_fail(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            path = root / "token"
            private_write(path, jwt(TARGET))

            exit_code, evidence, _, _ = self.run_cli(
                root,
                document(),
                current=[("jwt", path)],
                retired=[("jwt", path)],
            )

            self.assertEqual(1, exit_code)
            self.assertEqual(["TOKEN_COVERAGE"], evidence["errors"])


if __name__ == "__main__":
    unittest.main()
