"""Read-only fast-path eligibility check, run on the deployment host over SSH.

Print only a verified commit; any missing evidence means a full deployment.
No configuration values or credentials are printed.
"""
import hashlib
import json
import pathlib
import re
import subprocess
import sys

RUNTIME_SERVICES = (
    "postgres", "redis", "minio", "seq", "identityserver", "bolt-hub",
    "communications", "audit", "notifications", "storage", "attendance",
    "smsgateway", "wallets", "inventario", "pos", "portal",
    "operations-dashboard", "yap",
)


def baseline(releases, protected_env):
    release = pathlib.Path((releases / "current").read_text().strip()).resolve()
    if not release.is_relative_to(releases.resolve()) or release == releases.resolve():
        return None
    if not (release / "complete").is_file():
        return None
    commit = (release / "commit").read_text().strip()
    if not re.fullmatch(r"[0-9a-f]{40}", commit):
        return None
    digest = hashlib.sha256(protected_env.read_bytes()).hexdigest()
    if (release / "protected-env.sha256").read_text().strip() != digest:
        return None
    # Also detect manual runtime drift before carrying forward old image pins.
    manifest = json.loads((release / "images.override.json").read_text())["services"]
    for service in RUNTIME_SERVICES:
        expected = manifest[service]["image"]
        if not re.fullmatch(r"sha256:[0-9a-f]{64}", expected):
            return None
        result = subprocess.run(
            ["docker", "inspect", f"xframework-{service}"],
            check=True, capture_output=True, text=True, timeout=15,
        )
        container = json.loads(result.stdout)[0]
        if container["Image"] != expected or not container["State"]["Running"]:
            return None
        if container["State"].get("Health", {}).get("Status", "healthy") != "healthy":
            return None
    return commit


if __name__ == "__main__":
    try:
        result = baseline(pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2]))
    except (OSError, ValueError, KeyError, IndexError, subprocess.SubprocessError):
        result = None
    print(result or "full")
