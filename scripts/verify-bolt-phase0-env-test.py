#!/usr/bin/env python3
from __future__ import annotations

import importlib.util
import re
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).with_name("verify-bolt-phase0-env.py")
WORKFLOW = SCRIPT.parents[1] / ".github" / "workflows" / "deploy-xeon-dev.yml"
READ_ENV_CALL = re.compile(r"\$\(\s*read_env\b[^)]*\)")
IMPLICIT_READ_ENV = re.compile(
    r"\$\(\s*read_env[ \t]+(?P<quote>['\"]?)(?P<key>[A-Z][A-Z0-9_]*)(?P=quote)\s*\)"
)

SPEC = importlib.util.spec_from_file_location("verify_bolt_phase0_env", SCRIPT)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"unable to load {SCRIPT}")
ENV_PARSER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(ENV_PARSER)


class Phase0EnvTests(unittest.TestCase):
    def run_reader(self, content: bytes, key: str) -> subprocess.CompletedProcess[str]:
        with tempfile.TemporaryDirectory() as directory:
            env_file = Path(directory) / "deployment.env"
            env_file.write_bytes(content)
            return subprocess.run(
                [sys.executable, str(SCRIPT), "--file", str(env_file), "--key", key],
                check=False,
                capture_output=True,
                text=True,
            )

    def test_typed_values_are_returned_canonically(self) -> None:
        content = (
            b"BOLT_HUB_TLS_CA_PATH=/opt/xframework/tls/ca.crt\n"
            b"BOLT_HUB_PUBLIC_HOSTNAME=Bolt.Example.Internal\n"
            b"BOLT_HUB_EXPOSE_PORT=7000\n"
        )
        path = self.run_reader(content, "BOLT_HUB_TLS_CA_PATH")
        hostname = self.run_reader(content, "BOLT_HUB_PUBLIC_HOSTNAME")
        port = self.run_reader(content, "BOLT_HUB_EXPOSE_PORT")

        self.assertEqual((0, "/opt/xframework/tls/ca.crt"), (path.returncode, path.stdout))
        self.assertEqual((0, "bolt.example.internal"), (hostname.returncode, hostname.stdout))
        self.assertEqual((0, "7000"), (port.returncode, port.stdout))

    def test_identityserver_values_use_explicit_safe_types(self) -> None:
        content = (
            b"IDENTITYSERVER_TLS_CA_PATH=/opt/xframework/tls/identityserver-ca.crt\n"
            b"IDENTITYSERVER_TLS_FULLCHAIN_PATH=/opt/xframework/tls/identityserver-fullchain.pem\n"
            b"IDENTITYSERVER_TLS_PRIVATE_KEY_PATH=/opt/xframework/tls/identityserver-private-key.pem\n"
            b"IDENTITYSERVER_PUBLIC_HOSTNAME=Identity.Example.Internal\n"
            b"IDENTITYSERVER_PUBLIC_HTTPS_PORT=8261\n"
            b"IDENTITYSERVER_BOLT_TRANSPORT_TOKEN_PATH=/api/service-identity/bolt-transport-token\n"
        )
        expected_values = {
            "IDENTITYSERVER_TLS_CA_PATH": "/opt/xframework/tls/identityserver-ca.crt",
            "IDENTITYSERVER_TLS_FULLCHAIN_PATH": "/opt/xframework/tls/identityserver-fullchain.pem",
            "IDENTITYSERVER_TLS_PRIVATE_KEY_PATH": "/opt/xframework/tls/identityserver-private-key.pem",
            "IDENTITYSERVER_PUBLIC_HOSTNAME": "identity.example.internal",
            "IDENTITYSERVER_PUBLIC_HTTPS_PORT": "8261",
            "IDENTITYSERVER_BOLT_TRANSPORT_TOKEN_PATH": (
                "/api/service-identity/bolt-transport-token"
            ),
        }

        for key, expected in expected_values.items():
            with self.subTest(key=key):
                result = self.run_reader(content, key)
                self.assertEqual((0, expected), (result.returncode, result.stdout))

    def test_identityserver_token_path_rejects_adversarial_values(self) -> None:
        invalid_values = (
            "api/service-identity/bolt-transport-token",
            "/",
            "//api/service-identity/bolt-transport-token",
            "/api//service-identity/bolt-transport-token",
            "/api/./service-identity/bolt-transport-token",
            "/api/../service-identity/bolt-transport-token",
            "/api/service-identity/bolt-transport-token/",
            "/api\\service-identity\\bolt-transport-token",
            "/api/service-identity/bolt-transport-token?audience=bolt",
            "/api/service-identity/bolt-transport-token#fragment",
            "/api/service-identity/%62olt-transport-token",
            "https://identity.example.internal/api/service-identity/bolt-transport-token",
        )

        for value in invalid_values:
            with self.subTest(value=value):
                with self.assertRaises(ValueError):
                    ENV_PARSER.validate_absolute_http_path(value)

    def test_workflow_implicit_read_env_keys_have_registered_types(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        read_env_calls = READ_ENV_CALL.findall(workflow)
        implicit_matches = list(IMPLICIT_READ_ENV.finditer(workflow))
        implicit_keys = {match.group("key") for match in implicit_matches}

        self.assertTrue(read_env_calls, "expected implicit read_env calls in deployment workflow")
        self.assertEqual(len(read_env_calls), len(implicit_matches))
        self.assertEqual(set(), implicit_keys - ENV_PARSER.KEY_TYPES.keys())

    def test_quotes_substitutions_backticks_comments_and_shell_values_are_rejected(self) -> None:
        bad_values = (
            b"BOLT_HUB_PUBLIC_HOSTNAME='bolt.example.internal'\n",
            b"BOLT_HUB_PUBLIC_HOSTNAME=$(touch-marker)\n",
            b"BOLT_HUB_PUBLIC_HOSTNAME=`touch-marker`\n",
            b"BOLT_HUB_PUBLIC_HOSTNAME=bolt.example.internal # trusted\n",
            b"BOLT_HUB_PUBLIC_HOSTNAME=bolt.example.internal;touch-marker\n",
        )
        for content in bad_values:
            with self.subTest(content=content):
                result = self.run_reader(content, "BOLT_HUB_PUBLIC_HOSTNAME")
                self.assertNotEqual(0, result.returncode)
                self.assertEqual("", result.stdout)

    def test_opaque_unrequested_secrets_do_not_block_typed_reads_or_leak(self) -> None:
        opaque_secret = "opaque-$value!with#shell;characters[]{}"
        content = (
            f"DB_PASSWORD={opaque_secret}\n"
            "JWT_SECRET='quoted-secret-value'\n"
            "BOLT_HUB_TLS_CA_PATH=/opt/xframework/tls/ca.crt\n"
        ).encode("utf-8")

        result = self.run_reader(content, "BOLT_HUB_TLS_CA_PATH")

        self.assertEqual((0, "/opt/xframework/tls/ca.crt"), (result.returncode, result.stdout))
        self.assertNotIn(opaque_secret, result.stdout)
        self.assertNotIn(opaque_secret, result.stderr)

        with tempfile.TemporaryDirectory() as directory:
            env_file = Path(directory) / "deployment.env"
            env_file.write_bytes(content)
            self.assertEqual(
                {"BOLT_HUB_TLS_CA_PATH": "/opt/xframework/tls/ca.crt"},
                ENV_PARSER.parse_env(env_file, {"BOLT_HUB_TLS_CA_PATH"}),
            )

    def test_duplicate_opaque_values_still_fail_without_leaking(self) -> None:
        content = (
            b"DB_PASSWORD=first-opaque-secret!\n"
            b"DB_PASSWORD=second-opaque-secret$\n"
            b"BOLT_HUB_TLS_CA_PATH=/opt/xframework/tls/ca.crt\n"
        )

        result = self.run_reader(content, "BOLT_HUB_TLS_CA_PATH")

        self.assertNotEqual(0, result.returncode)
        self.assertEqual("", result.stdout)
        self.assertNotIn("first-opaque-secret", result.stderr)
        self.assertNotIn("second-opaque-secret", result.stderr)

    def test_crlf_and_full_line_comments_are_accepted_without_evaluation(self) -> None:
        content = (
            b"# deployment metadata that is never evaluated\r\n"
            b"  # indented full-line comment\r\n"
            b"\r\n"
            b"BOLT_HUB_EXPOSE_PORT=7000\r\n"
        )

        result = self.run_reader(content, "BOLT_HUB_EXPOSE_PORT")

        self.assertEqual((0, "7000"), (result.returncode, result.stdout))

    def test_bare_carriage_returns_duplicates_and_malformed_records_are_rejected(self) -> None:
        records = (
            b"BOLT_HUB_EXPOSE_PORT=7000\r",
            b"BOLT_HUB_EXPOSE_PORT=7000\nBOLT_HUB_EXPOSE_PORT=7001\n",
            b"export BOLT_HUB_EXPOSE_PORT=7000\n",
        )
        for content in records:
            with self.subTest(content=content):
                self.assertNotEqual(0, self.run_reader(content, "BOLT_HUB_EXPOSE_PORT").returncode)

    def test_relative_parent_paths_and_noncanonical_ports_fail_closed(self) -> None:
        invalid = (
            (b"BOLT_HUB_TLS_PRIVATE_KEY_PATH=relative/key.pem\n", "BOLT_HUB_TLS_PRIVATE_KEY_PATH"),
            (b"BOLT_HUB_TLS_PRIVATE_KEY_PATH=/opt/tls/../key.pem\n", "BOLT_HUB_TLS_PRIVATE_KEY_PATH"),
            (b"BOLT_HUB_EXPOSE_PORT=07000\n", "BOLT_HUB_EXPOSE_PORT"),
            (b"BOLT_HUB_EXPOSE_PORT=65536\n", "BOLT_HUB_EXPOSE_PORT"),
            (b"BOLT_HUB_PUBLIC_HOSTNAME=127.0.0.1\n", "BOLT_HUB_PUBLIC_HOSTNAME"),
        )
        for content, key in invalid:
            with self.subTest(content=content):
                self.assertNotEqual(0, self.run_reader(content, key).returncode)


if __name__ == "__main__":
    unittest.main()
