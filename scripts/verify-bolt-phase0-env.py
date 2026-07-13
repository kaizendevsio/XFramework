#!/usr/bin/env python3
"""Read typed Phase 0 values from a Compose env file without evaluating it."""

from __future__ import annotations

import argparse
import ipaddress
import json
import re
import sys
from pathlib import Path, PurePosixPath
from typing import Collection


NAME = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*$")
SAFE_TYPED_VALUE = re.compile(r"^[A-Za-z0-9_./,:@%+=+-]*$")
HOST_LABEL = re.compile(r"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?$")
HTTP_PATH_SEGMENT = re.compile(r"^[A-Za-z0-9._~-]+$")
FORBIDDEN = frozenset("`'\"#$\\;&|<>(){}[]*?!")
KEY_TYPES = {
    "BOLT_HUB_TLS_CA_PATH": "absolute-path",
    "BOLT_HUB_TLS_FULLCHAIN_PATH": "absolute-path",
    "BOLT_HUB_TLS_PRIVATE_KEY_PATH": "absolute-path",
    "BOLT_HUB_PUBLIC_HOSTNAME": "hostname",
    "BOLT_HUB_EXPOSE_PORT": "port",
    "IDENTITYSERVER_TLS_CA_PATH": "absolute-path",
    "IDENTITYSERVER_TLS_FULLCHAIN_PATH": "absolute-path",
    "IDENTITYSERVER_TLS_PRIVATE_KEY_PATH": "absolute-path",
    "IDENTITYSERVER_PUBLIC_HOSTNAME": "hostname",
    "IDENTITYSERVER_PUBLIC_HTTPS_PORT": "port",
    "IDENTITYSERVER_BOLT_TRANSPORT_TOKEN_PATH": "absolute-http-path",
}


def parse_env(
    path: Path, requested_keys: Collection[str] | None = None
) -> dict[str, str]:
    values: dict[str, str] = {}
    seen: set[str] = set()
    requested = set(requested_keys) if requested_keys is not None else None
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        for line_number, raw in enumerate(stream, start=1):
            if raw.endswith("\r\n"):
                line = raw[:-2]
            elif raw.endswith("\n"):
                line = raw[:-1]
            elif "\r" in raw:
                raise ValueError(f"line {line_number}: bare carriage returns are not allowed")
            else:
                line = raw
            if "\x00" in line or "\n" in line:
                raise ValueError(f"line {line_number}: embedded newline or NUL is not allowed")
            if not line.strip() or line.lstrip().startswith("#"):
                continue
            name, separator, value = line.partition("=")
            if not separator or not NAME.fullmatch(name):
                raise ValueError(f"line {line_number}: expected NAME=value")
            if name in seen:
                raise ValueError(f"line {line_number}: duplicate variable {name}")
            seen.add(name)
            if requested is None or name in requested:
                values[name] = value
    return values


def validate_absolute_path(value: str) -> str:
    path = PurePosixPath(value)
    if not value.startswith("/") or value == "/" or "//" in value:
        raise ValueError("must be a canonical absolute POSIX file path")
    if any(part in {"", ".", ".."} for part in path.parts[1:]) or str(path) != value:
        raise ValueError("must not contain empty, dot, or parent path segments")
    return value


def validate_absolute_http_path(value: str) -> str:
    if not value.isascii() or not value.startswith("/") or value == "/":
        raise ValueError("must be a non-root absolute HTTP path")
    if any(delimiter in value for delimiter in ("?", "#", "\\")):
        raise ValueError("must not contain a query, fragment, or backslash")
    segments = value[1:].split("/")
    if any(segment in {"", ".", ".."} for segment in segments):
        raise ValueError("must not contain empty, dot, or parent path segments")
    if not all(HTTP_PATH_SEGMENT.fullmatch(segment) for segment in segments):
        raise ValueError("must contain only canonical unreserved URI path characters")
    return value


def validate_hostname(value: str) -> str:
    if not value or len(value) > 253 or value.endswith("."):
        raise ValueError("must be a non-empty canonical hostname")
    labels = value.split(".")
    if not all(HOST_LABEL.fullmatch(label) for label in labels):
        raise ValueError("must contain only valid DNS hostname labels")
    try:
        ipaddress.ip_address(value)
    except ValueError:
        pass
    else:
        raise ValueError("must be a DNS hostname, not an IP address")
    return value.lower()


def validate_port(value: str) -> str:
    if not value.isascii() or not value.isdigit() or value.startswith("0"):
        raise ValueError("must be a canonical decimal TCP port")
    port = int(value)
    if not 1 <= port <= 65535 or str(port) != value:
        raise ValueError("must be a TCP port between 1 and 65535")
    return value


VALIDATORS = {
    "absolute-path": validate_absolute_path,
    "absolute-http-path": validate_absolute_http_path,
    "hostname": validate_hostname,
    "port": validate_port,
}


def typed_value(key: str, value: str, explicit_type: str | None = None) -> str:
    value_type = explicit_type or KEY_TYPES.get(key)
    if value_type not in VALIDATORS:
        raise ValueError(f"no approved type is defined for {key}; pass --type for a single key")
    if any(character in FORBIDDEN for character in value) or not SAFE_TYPED_VALUE.fullmatch(
        value
    ):
        raise ValueError(f"{key}: contains characters outside its approved typed syntax")
    try:
        return VALIDATORS[value_type](value)
    except ValueError as error:
        raise ValueError(f"{key}: {error}") from error


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--file", required=True)
    parser.add_argument("--key", action="append", required=True)
    parser.add_argument("--type", choices=sorted(VALIDATORS))
    args = parser.parse_args()

    try:
        if len(args.key) != len(set(args.key)):
            raise ValueError("requested keys must be unique")
        if args.type and len(args.key) != 1:
            raise ValueError("--type can only be used with one --key")
        values = parse_env(Path(args.file), args.key)
        missing = [key for key in args.key if key not in values]
        if missing:
            raise ValueError(f"missing required variables: {', '.join(missing)}")
        selected = {
            key: typed_value(key, values[key], args.type if len(args.key) == 1 else None)
            for key in args.key
        }
    except (OSError, ValueError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 1

    if len(args.key) == 1:
        sys.stdout.write(selected[args.key[0]])
    else:
        json.dump(selected, sys.stdout, sort_keys=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
