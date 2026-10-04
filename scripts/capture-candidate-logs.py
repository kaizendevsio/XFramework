#!/usr/bin/env python3
"""Keep a failed candidate's container logs before the rollback recreates its containers, and print a redacted excerpt.

The full logs stay on the deploy host (owner-only files in the release directory). Only the problem lines (warnings,
errors, exceptions and their stack frames) are printed, after removing every secret value from the candidate env file,
every synthetic token, and anything shaped like a JWT, bearer token, key=value credential or long opaque string.
Diagnostics only: always exits 0.
"""

from __future__ import annotations

import argparse
import os
import re
import subprocess
import sys
from pathlib import Path
from typing import Iterable, Sequence

SERVICES = ("identityserver", "bolt-hub", "communications", "notifications", "storage", "attendance", "smsgateway",
            "wallets", "inventario", "pos", "portal", "operations-dashboard", "audit", "yap")
MAX_LOG_BYTES = 8 * 1024 * 1024
MAX_EXCERPT_LINES = 120
MAX_LINE_CHARS = 1000
CONTEXT_LINES = 25
SECRET_KEY = re.compile(r"SECRET|PASSWORD|PASSWD|TOKEN|KEY|SIGNATURE|CONNECTION|CREDENTIAL|SALT|SEED", re.I)
PROBLEM = re.compile(r"\b(?:fail|crit|error|fatal|warn|warning|exception|unhandled)\b|Exception\b|\[(?:ERR|FTL|WRN)\]", re.I)
CONTINUATION = re.compile(r"^(?:\S+Z )?(?:\s+\S|\s*at |\s*---|\s*--- End of)")
PATTERNS = (
    (re.compile(r"eyJ[A-Za-z0-9_-]{4,}\.[A-Za-z0-9_-]{4,}\.[A-Za-z0-9_-]*"), "<jwt>"),
    (re.compile(r"(?i)\b(bearer|basic)\s+[A-Za-z0-9._~+/=-]+"), r"\1 <redacted>"),
    (re.compile(r"(?i)\b([\w.-]*(?:password|passwd|pwd|secret|token|api[_-]?key|apikey|authorization|signature|"
                r"credential)[\w.-]*)(\s*[=:]\s*)(\"?)[^\s;,\"&]+"), r"\1\2\3<redacted>"),
    (re.compile(r"-----BEGIN [A-Z ]+-----.*?-----END [A-Z ]+-----", re.S), "<pem>"),
    # Long runs that mix letters and digits (keys, hashes); identifiers such as constraint names stay readable.
    (re.compile(r"(?<![A-Za-z0-9+/_=-])(?=[A-Za-z0-9+/_=-]*\d)(?=[A-Za-z0-9+/_=-]*[A-Za-z])[A-Za-z0-9+/_=-]{40,}"), "<opaque>"),
)


def env_secrets(env_file: str) -> list[str]:
    """Values of the env file's secret-looking keys (and of any value that embeds a password)."""
    try:
        text = Path(env_file).read_text(encoding="utf-8", errors="replace")
    except OSError:
        return []
    values = []
    for line in text.splitlines():
        if not line or line.lstrip().startswith("#") or "=" not in line:
            continue
        key, value = line.split("=", 1)
        value = value.strip().strip('"').strip("'")
        if len(value) >= 6 and (SECRET_KEY.search(key) or re.search(r"(?i)password=", value)):
            values.append(value)
    return values


def file_secrets(directory: str) -> list[str]:
    values = []
    try:
        entries = list(Path(directory).iterdir())
    except OSError:
        return []
    for entry in entries:
        try:
            if entry.is_file() and not entry.is_symlink():
                value = entry.read_text(encoding="utf-8", errors="replace").strip()
                if len(value) >= 6:
                    values.append(value)
        except OSError:
            continue
    return values


def redact(text: str, secrets: Iterable[str]) -> str:
    for secret in sorted(set(secrets), key=len, reverse=True):
        text = text.replace(secret, "<secret>")
    for pattern, replacement in PATTERNS:
        text = pattern.sub(replacement, text)
    return text


def excerpt(log: str, secrets: Sequence[str]) -> list[str]:
    """The problem lines and the stack frames that follow them, redacted, newest kept when over the limit."""
    lines = log.splitlines()
    keep: list[int] = []
    context = 0
    for index, line in enumerate(lines):
        if PROBLEM.search(line):
            keep.append(index)
            context = CONTEXT_LINES
        elif context and CONTINUATION.match(line):
            keep.append(index)
            context -= 1
        else:
            context = 0
    selected = [redact(lines[i], secrets)[:MAX_LINE_CHARS] for i in keep[-MAX_EXCERPT_LINES:]]
    if len(keep) > MAX_EXCERPT_LINES:
        selected.insert(0, f"... {len(keep) - MAX_EXCERPT_LINES} earlier problem lines omitted ...")
    return selected


def container_logs(container: str, since: str | None) -> str | None:
    if subprocess.run(["docker", "inspect", container], capture_output=True, timeout=30).returncode != 0:
        return None
    command = ["docker", "logs", "--timestamps"] + (["--since", since] if since else ["--tail", "5000"]) + [container]
    result = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=60)
    return result.stdout[-MAX_LOG_BYTES:].decode("utf-8", errors="replace")


def capture(run_dir: str, env_file: str, services: Sequence[str], since: str | None) -> None:
    secrets = env_secrets(env_file) + file_secrets(os.path.join(run_dir, "synthetic-tokens"))
    log_dir = Path(run_dir) / "candidate-logs"
    log_dir.mkdir(mode=0o700, exist_ok=True)
    for service in services:
        container = f"xframework-{service}"
        try:
            log = container_logs(container, since)
        except (OSError, subprocess.SubprocessError) as error:
            print(f"== {container}: logs unavailable ({type(error).__name__})")
            continue
        if log is None:
            continue
        path = log_dir / f"{service}.log"
        descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
        with os.fdopen(descriptor, "w", encoding="utf-8") as stream:
            stream.write(log)
        lines = excerpt(log, secrets)
        print(f"== {container}: {len(lines)} problem line(s); full log kept on the host at {path}")
        for line in lines:
            print(f"   {line}")


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run-dir", required=True)
    parser.add_argument("--env-file", required=True)
    parser.add_argument("--since", help="docker logs --since value (for example a Unix time)")
    parser.add_argument("--service", action="append", dest="services")
    args = parser.parse_args(argv)
    try:
        os.umask(0o077)
        capture(args.run_dir, args.env_file, args.services or SERVICES, args.since)
    except Exception as error:  # Diagnostics must never change the deployment's outcome.
        print(f"Candidate log capture failed: {type(error).__name__}", file=sys.stderr)
    return 0


if __name__ == "__main__":
    sys.exit(main())
