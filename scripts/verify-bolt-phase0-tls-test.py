#!/usr/bin/env python3
from __future__ import annotations

import shutil
import subprocess
import unittest
from pathlib import Path


SCRIPT = Path(__file__).with_name("verify-bolt-phase0-tls.sh")


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
class Phase0TlsInputTests(unittest.TestCase):
    def invoke(self, hostname: str, port: str, key_path: str | None = None) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [
                str(BASH),
                str(SCRIPT),
                "/tmp/fullchain.pem",
                key_path or "/tmp/private-key.pem",
                "/tmp/ca.crt",
                "wss://bolt-hub:8443/bolt/ws",
                hostname,
                port,
                "/tmp/evidence.json",
            ],
            check=False,
            capture_output=True,
            text=True,
        )

    def test_shell_metacharacter_hostname_is_rejected_before_tls_reads(self) -> None:
        result = self.invoke("bolt.example.internal;touch-marker", "7000")
        self.assertNotEqual(0, result.returncode)
        self.assertIn("hostname", result.stderr.lower())

    def test_noncanonical_port_is_rejected(self) -> None:
        result = self.invoke("bolt.example.internal", "07000")
        self.assertNotEqual(0, result.returncode)
        self.assertIn("port", result.stderr.lower())

    def test_ip_address_is_rejected_as_published_hostname(self) -> None:
        result = self.invoke("127.0.0.1", "7000")
        self.assertNotEqual(0, result.returncode)
        self.assertIn("hostname", result.stderr.lower())

    def test_relative_private_key_path_is_rejected(self) -> None:
        result = self.invoke("bolt.example.internal", "7000", "relative/private-key.pem")
        self.assertNotEqual(0, result.returncode)
        self.assertIn("absolute", result.stderr.lower())


if __name__ == "__main__":
    unittest.main()
