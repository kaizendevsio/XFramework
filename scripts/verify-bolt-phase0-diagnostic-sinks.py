#!/usr/bin/env python3
"""Fail closed when Phase 0 credentials appear in bounded diagnostic evidence."""

from __future__ import annotations

import argparse
import base64
import datetime as dt
import json
import os
import re
import stat
import sys
import time
import urllib.parse
import urllib.request
from pathlib import Path
from typing import Any, Sequence

TOKEN_PATH_KEYS = ("BOLT_SYNTHETIC_COMMUNICATIONS_TRANSPORT_TOKEN_PATH",
                   "BOLT_SYNTHETIC_PORTAL_TRANSPORT_TOKEN_PATH",
                   "BOLT_SYNTHETIC_USER_ACTOR_TOKEN_PATH",
                   "BOLT_SYNTHETIC_EXPIRY_TRANSPORT_TOKEN_PATH")
ENV_KEY = re.compile(r"[A-Za-z_][A-Za-z0-9_]*")
JWT_PART = re.compile(r"[A-Za-z0-9_-]+")
JTI = re.compile(r"(?:[0-9a-fA-F]{32}|[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-"
                 r"[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12})")
UTC = re.compile(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,6})?Z")
MAX_FILE_BYTES, MAX_TOTAL_FILE_BYTES = 16 * 1024 * 1024, 64 * 1024 * 1024
MAX_RESPONSE_BYTES, MAX_WINDOW_SECONDS = 8 * 1024 * 1024, 6 * 60 * 60
MAX_EVENTS, MAX_SERVICES, MAX_TRACES, MAX_REQUESTS = 1000, 128, 200, 130
REQUEST_TIMEOUT_SECONDS, OVERALL_TIMEOUT_SECONDS = 10, 120

class CheckError(Exception): pass

def fail(code: str) -> None:
    raise CheckError(code)

def read_bounded(path: str, maximum: int, *, empty: bool = False) -> bytes:
    try:
        candidate = Path(path)
        size = candidate.stat().st_size
        if not candidate.is_file() or size > maximum or (size == 0 and not empty):
            fail("FILE")
        data = candidate.read_bytes()
    except CheckError:
        raise
    except OSError:
        fail("FILE")
    if len(data) != size:
        fail("FILE_CHANGED")
    return data

def parse_env(path: str) -> dict[str, str]:
    try:
        text = read_bounded(path, 1024 * 1024).decode("utf-8")
    except UnicodeError:
        fail("ENV")
    values: dict[str, str] = {}
    for line in text.splitlines():
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        if line[0].isspace() or "=" not in line:
            fail("ENV")
        key, value = line.split("=", 1)
        if not ENV_KEY.fullmatch(key) or key in values or value != value.strip():
            fail("ENV")
        values[key] = value
    return values

def load_needles(values: dict[str, str]) -> tuple[bytes, ...]:
    needles: list[bytes] = []
    paths: set[str] = set()
    for key in TOKEN_PATH_KEYS:
        path = values.get(key)
        if not path or path in paths:
            fail("TOKEN_CONFIG")
        paths.add(path)
        lines = read_bounded(path, 16 * 1024).splitlines()
        if len(lines) != 1:
            fail("JWT")
        token = lines[0]
        try:
            parts = token.decode("ascii").split(".")
            if len(parts) != 3 or any(not JWT_PART.fullmatch(part) for part in parts):
                fail("JWT")
            payload = json.loads(
                base64.urlsafe_b64decode(parts[1] + "=" * (-len(parts[1]) % 4)).decode("utf-8")
            )
        except CheckError:
            raise
        except (UnicodeError, ValueError, json.JSONDecodeError):
            fail("JWT")
        jti = payload.get("jti") if isinstance(payload, dict) else None
        if not isinstance(jti, str) or not JTI.fullmatch(jti):
            fail("JTI")
        needles.extend((token, jti.encode("ascii")))
    if len(set(needles[::2])) != 4 or len(set(needles[1::2])) != 4:
        fail("TOKEN_DUPLICATE")
    return tuple(needles)

def parse_utc(value: str, code: str) -> dt.datetime:
    if not UTC.fullmatch(value):
        fail(code)
    try:
        return dt.datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError:
        fail(code)

def format_utc(value: dt.datetime) -> str:
    return value.isoformat(timespec="microseconds").replace("+00:00", "Z")

def origin(value: str, code: str) -> str:
    try:
        parsed = urllib.parse.urlsplit(value)
        port = parsed.port
    except ValueError:
        fail(code)
    if (
        parsed.scheme not in {"http", "https"}
        or not parsed.hostname
        or parsed.username is not None
        or parsed.password is not None
        or parsed.path not in {"", "/"}
        or parsed.query
        or parsed.fragment
        or (port is not None and not 1 <= port <= 65535)
    ):
        fail(code)
    return value.rstrip("/")

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request: Any, *args: Any, **kwargs: Any) -> None: return None

def request_json(url: str, headers: dict[str, str], needles: Sequence[bytes],
                 budget: dict[str, float | int]) -> Any:
    budget["requests"] = int(budget["requests"]) + 1
    elapsed = time.monotonic() - float(budget["started"])
    if int(budget["requests"]) > MAX_REQUESTS or elapsed >= OVERALL_TIMEOUT_SECONDS:
        fail("REQUEST_LIMIT")
    request = urllib.request.Request(url, headers={
        "Accept": "application/json", "Accept-Encoding": "identity", **headers})
    try:
        with urllib.request.build_opener(NoRedirect).open(
            request, timeout=min(REQUEST_TIMEOUT_SECONDS, OVERALL_TIMEOUT_SECONDS - elapsed)
        ) as response:
            if response.status != 200 or response.headers.get_content_type() != "application/json":
                fail("SINK_UNAVAILABLE")
            if response.headers.get("Content-Encoding", "identity").lower() != "identity":
                fail("SINK_RESPONSE")
            declared = response.headers.get("Content-Length")
            if declared is not None and (not declared.isdigit() or int(declared) > MAX_RESPONSE_BYTES):
                fail("SINK_LIMIT")
            body = response.read(MAX_RESPONSE_BYTES + 1)
            if not body or len(body) > MAX_RESPONSE_BYTES:
                fail("SINK_LIMIT")
            if declared is not None and len(body) != int(declared):
                fail("SINK_RESPONSE")
    except CheckError:
        raise
    except Exception:
        fail("SINK_UNAVAILABLE")
    if any(needle in body for needle in needles):
        fail("CREDENTIAL_LEAK")
    try:
        return json.loads(body.decode("utf-8"))
    except (UnicodeError, ValueError, json.JSONDecodeError, RecursionError):
        fail("SINK_RESPONSE")

def result_list(document: Any, keys: tuple[str, ...], code: str) -> list[Any]:
    if not isinstance(document, dict):
        fail(code)
    present = [document[key] for key in keys if key in document]
    if len(present) != 1 or not isinstance(present[0], list) or document.get("errors") not in (None, []):
        fail(code)
    values = present[0]
    total = document.get("total")
    if total is not None and (
        not isinstance(total, int) or isinstance(total, bool) or total < 0 or total > len(values)
    ):
        fail(code)
    return values

def scan_sinks(seq_url: str, jaeger_url: str, start: dt.datetime, end: dt.datetime,
               needles: Sequence[bytes], api_key: str) -> tuple[int, int, int, int]:
    budget: dict[str, float | int] = {"started": time.monotonic(), "requests": 0}
    query = urllib.parse.urlencode({"count": MAX_EVENTS + 1, "render": "true",
                                    "fromDateUtc": format_utc(start),
                                    "toDateUtc": format_utc(end)})
    if api_key and (
        not 16 <= len(api_key) <= 512 or not api_key.isascii() or any(c.isspace() for c in api_key)
    ):
        fail("SEQ_API_KEY")
    seq_document = request_json(f"{seq_url}/api/events?{query}",
                                {"X-Seq-ApiKey": api_key} if api_key else {}, needles, budget)
    events = seq_document if isinstance(seq_document, list) else result_list(
        seq_document, ("Events", "events"), "SEQ_RESPONSE"
    )
    if len(events) > MAX_EVENTS:
        fail("SEQ_LIMIT")

    services = result_list(request_json(f"{jaeger_url}/api/services", {}, needles, budget),
                           ("data",), "JAEGER_RESPONSE")
    if (
        len(services) > MAX_SERVICES
        or any(not isinstance(item, str) or not item or len(item) > 256 for item in services)
        or len(set(services)) != len(services)
    ):
        fail("JAEGER_LIMIT")
    traces = 0
    for service in services:
        query = urllib.parse.urlencode({"service": service,
                                        "start": int(start.timestamp() * 1_000_000),
                                        "end": int(end.timestamp() * 1_000_000),
                                        "limit": MAX_TRACES + 1})
        found = result_list(request_json(f"{jaeger_url}/api/traces?{query}", {}, needles, budget),
                            ("data",), "JAEGER_RESPONSE")
        if len(found) > MAX_TRACES:
            fail("JAEGER_LIMIT")
        traces += len(found)
    return len(events), len(services), traces, int(budget["requests"])

def write_evidence(path: str, counts: dict[str, int]) -> None:
    payload = json.dumps({"status": "passed", "counts": counts}, sort_keys=True,
                         separators=(",", ":")).encode("ascii") + b"\n"
    descriptor = -1
    created = False
    try:
        descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
        created = True
        os.fchmod(descriptor, 0o600)
        if os.write(descriptor, payload) != len(payload):
            fail("EVIDENCE_WRITE")
        os.fsync(descriptor)
        if os.name == "posix" and stat.S_IMODE(os.fstat(descriptor).st_mode) != 0o600:
            fail("EVIDENCE_MODE")
    except FileExistsError:
        fail("EVIDENCE_EXISTS")
    except CheckError:
        raise
    except OSError:
        fail("EVIDENCE_WRITE")
    finally:
        if descriptor >= 0:
            os.close(descriptor)
        if created and sys.exc_info()[0] is not None:
            try:
                os.unlink(path)
            except OSError:
                pass

def verify(args: argparse.Namespace) -> None:
    values = parse_env(args.candidate_env)
    needles = load_needles(values)
    files = args.bounded_file
    if not files or len(files) > 32 or len(set(files)) != len(files):
        fail("FILES")
    file_bytes = 0
    for path in files:
        data = read_bounded(path, MAX_FILE_BYTES, empty=True)
        file_bytes += len(data)
        if any(needle in data for needle in needles):
            fail("CREDENTIAL_LEAK")
    if file_bytes > MAX_TOTAL_FILE_BYTES:
        fail("FILE_LIMIT")

    now = dt.datetime.now(dt.timezone.utc)
    start = parse_utc(args.window_start, "WINDOW")
    end = parse_utc(args.window_end, "WINDOW") if args.window_end else now
    if end <= start or (end - start).total_seconds() > MAX_WINDOW_SECONDS:
        fail("WINDOW")
    seq_events, services, traces, requests = scan_sinks(
        origin(args.seq_base_url, "SEQ_URL"), origin(args.jaeger_base_url, "JAEGER_URL"),
        start, end, needles, values.get("SEQ_API_KEY", ""))
    write_evidence(args.evidence, {
        "credentials": 4, "files": len(files), "fileBytes": file_bytes,
        "seqEvents": seq_events, "jaegerServices": services,
        "jaegerTraces": traces, "httpRequests": requests})

def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--candidate-env", required=True)
    parser.add_argument("--bounded-file", action="append", required=True)
    parser.add_argument("--window-start", required=True)
    parser.add_argument("--window-end")
    parser.add_argument("--seq-base-url", required=True)
    parser.add_argument("--jaeger-base-url", required=True)
    parser.add_argument("--evidence", required=True)
    try:
        verify(parser.parse_args(argv))
        print("BOLT_PHASE0_DIAGNOSTIC_SINKS_OK")
        return 0
    except CheckError as error:
        print(f"BOLT_PHASE0_DIAGNOSTIC_SINKS_{error}", file=sys.stderr)
        return 1
    except BaseException:
        print("BOLT_PHASE0_DIAGNOSTIC_SINKS_INTERNAL", file=sys.stderr)
        return 1
if __name__ == "__main__":
    raise SystemExit(main())
