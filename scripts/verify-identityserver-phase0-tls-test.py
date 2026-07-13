#!/usr/bin/env python3
from __future__ import annotations

import shutil
import subprocess
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


@unittest.skipUnless(BASH, "a working bash executable is required")
class IdentityServerPhase0TlsInputTests(unittest.TestCase):
    def invoke(
        self,
        hostname: str = "identity.example.test",
        port: str = "8261",
        token_path: str = "/api/service-identity/bolt-transport-token",
        key_path: str = "/tmp/identityserver-private-key.pem",
    ) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [
                str(BASH),
                str(SCRIPT),
                "/tmp/identityserver-fullchain.pem",
                key_path,
                "/tmp/identityserver-ca.crt",
                hostname,
                port,
                token_path,
                "/tmp/identityserver-tls-evidence.json",
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


if __name__ == "__main__":
    unittest.main()
