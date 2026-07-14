#!/usr/bin/env python3
from __future__ import annotations

import json
import os
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).with_name("verify-identityserver-phase0-tls.sh")


def working_bash() -> str | None:
    candidates = [
        shutil.which("bash"),
        r"C:\Program Files\Git\bin\bash.exe",
        r"C:\Program Files\Git\usr\bin\bash.exe",
    ]
    for executable in candidates:
        if not executable or not Path(executable).is_file():
            continue
        probe = subprocess.run([executable, "-c", "exit 0"], check=False, capture_output=True)
        if probe.returncode == 0:
            return executable
    return None


BASH = working_bash()
OPENSSL = shutil.which("openssl")


@unittest.skipUnless(BASH, "a working bash executable is required")
class IdentityServerPhase0TlsInputTests(unittest.TestCase):
    def invoke(
        self,
        hostname: str = "identity.example.test",
        port: str = "8261",
        token_path: str = "/api/service-identity/bolt-transport-token",
        key_path: str = "/tmp/identityserver-private-key.pem",
        fullchain_path: str = "/tmp/identityserver-fullchain.pem",
        ca_path: str = "/tmp/identityserver-ca.crt",
        evidence_path: str = "/tmp/identityserver-tls-evidence.json",
    ) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [
                str(BASH),
                str(SCRIPT),
                fullchain_path,
                key_path,
                ca_path,
                hostname,
                port,
                token_path,
                evidence_path,
            ],
            check=False,
            capture_output=True,
            text=True,
        )

    def test_shell_metacharacter_hostname_is_rejected_before_tls_reads(self) -> None:
        result = self.invoke(hostname="identity.example.test;touch-marker")
        self.assertNotEqual(0, result.returncode)
        self.assertIn("hostname", result.stderr.lower())

    def test_ip_address_is_rejected(self) -> None:
        result = self.invoke(hostname="127.0.0.1")
        self.assertNotEqual(0, result.returncode)
        self.assertIn("hostname", result.stderr.lower())

    def test_noncanonical_port_is_rejected(self) -> None:
        result = self.invoke(port="08261")
        self.assertNotEqual(0, result.returncode)
        self.assertIn("port", result.stderr.lower())

    def test_unapproved_token_path_is_rejected(self) -> None:
        result = self.invoke(token_path="/api/service-identity/token")
        self.assertNotEqual(0, result.returncode)
        self.assertIn("approved endpoint", result.stderr.lower())

    def test_relative_private_key_path_is_rejected(self) -> None:
        result = self.invoke(key_path="relative/private-key.pem")
        self.assertNotEqual(0, result.returncode)
        self.assertIn("absolute", result.stderr.lower())

    def test_chain_and_hostname_verification_require_x509_strict(self) -> None:
        source = SCRIPT.read_text(encoding="utf-8")

        self.assertEqual(
            2,
            source.count("openssl verify -x509_strict -purpose sslserver"),
        )

    @unittest.skipUnless(
        os.name == "posix" and OPENSSL,
        "POSIX OpenSSL is required for strict X.509 certificate fixtures",
    )
    def test_strict_verification_requires_ca_key_usage_and_accepts_compliant_ca(self) -> None:
        for include_key_usage in (False, True):
            with self.subTest(include_key_usage=include_key_usage), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                ca_key = root / "ca.key"
                ca_cert = root / "ca.crt"
                server_key = root / "server.key"
                server_request = root / "server.csr"
                server_cert = root / "server.crt"
                fullchain = root / "fullchain.pem"
                extensions = root / "server-extensions.cnf"
                evidence = root / "evidence.json"

                ca_command = [
                    str(OPENSSL),
                    "req",
                    "-x509",
                    "-newkey",
                    "rsa:2048",
                    "-nodes",
                    "-keyout",
                    str(ca_key),
                    "-out",
                    str(ca_cert),
                    "-subj",
                    "/CN=Phase 0 TLS Test CA",
                    "-days",
                    "2",
                    "-sha256",
                    "-addext",
                    "basicConstraints=critical,CA:TRUE,pathlen:0",
                ]
                if include_key_usage:
                    ca_command.extend(
                        ["-addext", "keyUsage=critical,keyCertSign,cRLSign"]
                    )
                subprocess.run(ca_command, check=True, capture_output=True)
                subprocess.run(
                    [
                        str(OPENSSL),
                        "req",
                        "-newkey",
                        "rsa:2048",
                        "-nodes",
                        "-keyout",
                        str(server_key),
                        "-out",
                        str(server_request),
                        "-subj",
                        "/CN=identityserver",
                    ],
                    check=True,
                    capture_output=True,
                )
                extensions.write_text(
                    "\n".join(
                        (
                            "authorityKeyIdentifier=keyid,issuer",
                            "basicConstraints=critical,CA:FALSE",
                            "keyUsage=critical,digitalSignature,keyEncipherment",
                            "extendedKeyUsage=serverAuth",
                            "subjectAltName=DNS:identityserver,DNS:identity.example.test",
                        )
                    )
                    + "\n",
                    encoding="ascii",
                )
                subprocess.run(
                    [
                        str(OPENSSL),
                        "x509",
                        "-req",
                        "-in",
                        str(server_request),
                        "-CA",
                        str(ca_cert),
                        "-CAkey",
                        str(ca_key),
                        "-CAcreateserial",
                        "-out",
                        str(server_cert),
                        "-days",
                        "2",
                        "-sha256",
                        "-extfile",
                        str(extensions),
                    ],
                    check=True,
                    capture_output=True,
                )
                fullchain.write_bytes(server_cert.read_bytes() + ca_cert.read_bytes())
                server_key.chmod(0o600)

                result = self.invoke(
                    key_path=str(server_key),
                    fullchain_path=str(fullchain),
                    ca_path=str(ca_cert),
                    evidence_path=str(evidence),
                )

                if include_key_usage:
                    self.assertEqual(0, result.returncode, result.stderr)
                    self.assertEqual("passed", json.loads(evidence.read_text())["status"])
                else:
                    self.assertNotEqual(0, result.returncode)
                    self.assertIn(
                        "key usage",
                        (result.stdout + result.stderr).lower(),
                    )


if __name__ == "__main__":
    unittest.main()
