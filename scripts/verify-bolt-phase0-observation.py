#!/usr/bin/env python3
"""Verify a bounded Bolt Phase 0 canary observation without retaining sensitive payloads."""

from __future__ import annotations

import argparse
import json
import math
import os
import re
import stat
import sys
import tempfile
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable


INPUT_SCHEMA = "xframework.bolt.phase0.observation-input.v1"
OUTPUT_SCHEMA = "xframework.bolt.phase0.observation.v1"
SYNTHETIC_SCHEMA = "bolt-phase0-synthetic-evidence/v1"
SYNTHETIC_CORE_SCHEMA = "bolt-phase0-synthetic-report/v1"
SERVICES = ("bolt-hub", "identityserver", "communications")
TRANSPORT_CHECK = "Bolt-transport"
CLIENT_TRANSPORT_CHECK = "Bolt-client-transport"
SAFE_OPERATION = re.compile(r"^[a-z][a-z0-9_]{0,63}$")
UTC_TIMESTAMP = re.compile(
    r"^(?P<date>\d{4}-\d{2}-\d{2})T(?P<time>\d{2}:\d{2}:\d{2})"
    r"(?P<fraction>\.\d{1,7})?Z$"
)
MAX_INPUT_BYTES = 32 * 1024 * 1024
MAX_COUNTER = (1 << 63) - 1
MAX_HEALTH_SAMPLES = 100_000
MAX_SYNTHETIC_REPORTS = 100_000
MAX_CHECKS_PER_SERVICE = 1_000
MAX_OPERATIONS_PER_REPORT = 1_000
MAX_RETAINED_ERRORS = 256
EPOCH = datetime(1970, 1, 1, tzinfo=timezone.utc)
PUBLIC_POLICY_FIELDS = (
    "minimum_duration_seconds",
    "minimum_samples",
    "maximum_sample_gap_seconds",
    "maximum_health_age_seconds",
    "pressure_persistence_samples",
    "growth_window_samples",
    "maximum_connection_count_churn",
    "maximum_estimated_reconnects",
    "latency_budgets_ms",
)

INPUT_FIELDS = {
    "schema",
    "observation_started_at_utc",
    "observation_completed_at_utc",
    "health_samples",
    "synthetic_reports",
}
SAMPLE_FIELDS = {"sampled_at_utc", "services"}
SERVICE_FIELDS = {"http_status", "health"}
HEALTH_FIELDS = {"status", "duration", "timestamp", "checks"}
CHECK_FIELDS = {"name", "status", "description", "duration", "tags", "data", "exception"}
SYNTHETIC_FIELDS = {
    "schemaVersion",
    "runId",
    "stage",
    "status",
    "coreReportSha256",
    "synthetic",
    "postRunEvidence",
}
CORE_FIELDS = {
    "schemaVersion",
    "runId",
    "tokenSha256Prefixes",
    "startedAtUtc",
    "completedAtUtc",
    "target",
    "status",
    "timings",
    "operations",
}
OPERATION_FIELDS = {"name", "startedAtUtc", "completedAtUtc", "status", "timingMs", "results"}
TRANSPORT_COUNTER_FIELDS = {
    "acceptedConnections",
    "registeredConnections",
    "unregisteredConnections",
    "liveConnections",
    "closingConnections",
    "unregisteredTrackedConnections",
    "pendingRpcCalls",
    "activeLogicalStreams",
    "activeMediaStreams",
    "activeCalls",
    "activeSubscriptionReservations",
    "liveTransientSubscriptions",
    "liveDurableSubscriptions",
    "aggregateQueuedSendBytes",
    "maximumQueuedSendBytes",
    "connectionsUnderSendPressure",
    "runningSendLoops",
    "completedSendLoops",
    "faultedSendLoops",
    "liveConnectionsWithInactiveSendLoops",
    "negativeRuntimeCounters",
    "maximumConnectionsForOnePrincipal",
    "maximumPendingRpcCallsForOnePrincipal",
    "maximumLogicalStreamsForOnePrincipal",
    "maximumMediaStreamsForOnePrincipal",
    "maximumSubscriptionsForOnePrincipal",
}
TRANSPORT_FIELDS = TRANSPORT_COUNTER_FIELDS | {"isDisposed", "configuredBounds"}
BOUND_INTEGER_FIELDS = {
    "maximumFrameBytes",
    "sendQueueCapacityPerConnection",
    "sendEnqueueTimeoutMilliseconds",
    "sendBackpressureDropThresholdBytes",
    "sendBackpressureFeedbackThresholdBytes",
    "maximumPendingRpcCalls",
    "maximumPendingRpcCallsPerPrincipal",
    "maximumConnectionsPerPrincipal",
    "maximumLogicalStreamsPerPrincipal",
    "maximumMediaStreamsPerPrincipal",
    "maximumSubscriptionsPerPrincipal",
    "maximumDurableSubscribersPerTopic",
}
BOUND_FIELDS = BOUND_INTEGER_FIELDS | {"mediaEnabled"}
CLIENT_TRANSPORT_INTEGER_FIELDS = {
    "connectionCount",
    "connectedTransports",
    "pendingSends",
    "activeSends",
    "maxActiveSendElapsedMs",
    "runningSendLoops",
    "runningReceiveLoops",
    "faultedSendLoops",
    "faultedReceiveLoops",
    "pendingSendsUnhealthyThreshold",
    "activeSendUnhealthyThresholdMs",
    "totalSendFailures",
    "totalSendTimeouts",
    "totalReceiveLoopFaults",
    "totalUnexpectedDisconnects",
    "totalSuccessfulReconnects",
}
CLIENT_TRANSPORT_FIELDS = CLIENT_TRANSPORT_INTEGER_FIELDS | {"isRegistered", "isHealthy"}


class DuplicateKeyError(ValueError):
    pass


class Timestamp:
    __slots__ = ("text", "ticks")

    def __init__(self, text: str, ticks: int):
        self.text = text
        self.ticks = ticks


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--minimum-duration-seconds", type=int, required=True)
    parser.add_argument("--minimum-samples", type=int, required=True)
    parser.add_argument("--maximum-sample-gap-seconds", type=int, required=True)
    parser.add_argument("--maximum-health-age-seconds", type=int, required=True)
    parser.add_argument("--pressure-persistence-samples", type=int, required=True)
    parser.add_argument("--growth-window-samples", type=int, required=True)
    parser.add_argument("--maximum-connection-count-churn", type=int, required=True)
    parser.add_argument("--maximum-estimated-reconnects", type=int, required=True)
    parser.add_argument(
        "--latency-budget",
        action="append",
        required=True,
        metavar="OPERATION=P95_MS,P99_MS",
        help="repeat for each operation, or use '*=P95_MS,P99_MS' as an explicit default",
    )
    return parser.parse_args(argv)


def reject_constant(value: str) -> None:
    raise ValueError(f"non-finite JSON number {value} is forbidden")


def unique_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise DuplicateKeyError("JSON contains a duplicate object key")
        result[key] = value
    return result


def load_json(path: Path) -> Any:
    size = path.stat().st_size
    if size <= 0 or size > MAX_INPUT_BYTES:
        raise ValueError("observation input size is outside the accepted range")
    raw = path.read_bytes()
    if raw.startswith(b"\xef\xbb\xbf"):
        raise ValueError("observation input must be canonical UTF-8 without a BOM")
    try:
        text = raw.decode("utf-8", errors="strict")
    except UnicodeDecodeError as error:
        raise ValueError("observation input is not valid UTF-8") from error
    return json.loads(text, object_pairs_hook=unique_object, parse_constant=reject_constant)


def require_object(value: Any, fields: set[str], description: str) -> dict[str, Any]:
    if not isinstance(value, dict) or set(value) != fields:
        raise ValueError(f"{description} does not match its required schema")
    return value


def require_list(value: Any, description: str) -> list[Any]:
    if not isinstance(value, list):
        raise ValueError(f"{description} must be an array")
    return value


def require_nonnegative_int(value: Any, description: str) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or not 0 <= value <= MAX_COUNTER:
        raise ValueError(f"{description} must be a nonnegative 64-bit integer")
    return value


def require_positive_int(value: Any, description: str) -> int:
    result = require_nonnegative_int(value, description)
    if result == 0:
        raise ValueError(f"{description} must be positive")
    return result


def require_nonnegative_number(value: Any, description: str) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"{description} must be a nonnegative finite number")
    result = float(value)
    if not math.isfinite(result) or result < 0:
        raise ValueError(f"{description} must be a nonnegative finite number")
    return result


def parse_utc_timestamp(value: Any, description: str) -> Timestamp:
    if not isinstance(value, str):
        raise ValueError(f"{description} must be a canonical UTC timestamp")
    match = UTC_TIMESTAMP.fullmatch(value)
    if not match:
        raise ValueError(f"{description} must use RFC 3339 UTC form with a Z suffix")
    try:
        second = datetime.fromisoformat(f"{match.group('date')}T{match.group('time')}").replace(
            tzinfo=timezone.utc
        )
    except ValueError as error:
        raise ValueError(f"{description} is not a valid UTC timestamp") from error
    fraction = (match.group("fraction") or "").removeprefix(".")
    fractional_ticks = int(fraction.ljust(7, "0")) if fraction else 0
    delta = second - EPOCH
    epoch_seconds = delta.days * 86_400 + delta.seconds
    return Timestamp(value, epoch_seconds * 10_000_000 + fractional_ticks)


def seconds_between(start: Timestamp, end: Timestamp) -> float:
    return (end.ticks - start.ticks) / 10_000_000


def parse_latency_budgets(values: Iterable[str]) -> dict[str, tuple[int, int]]:
    budgets: dict[str, tuple[int, int]] = {}
    for value in values:
        operation, separator, raw_limits = value.partition("=")
        limits = raw_limits.split(",") if separator else []
        if operation != "*" and not SAFE_OPERATION.fullmatch(operation):
            raise ValueError("latency budget operation name is invalid")
        if operation in budgets:
            raise ValueError("latency budget operation names must be unique")
        if len(limits) != 2 or not all(item.isascii() and item.isdecimal() for item in limits):
            raise ValueError("latency budget must use OPERATION=P95_MS,P99_MS")
        p95, p99 = (int(item) for item in limits)
        if not 1 <= p95 <= p99 <= MAX_COUNTER:
            raise ValueError("latency budget must satisfy 1 <= p95 <= p99")
        budgets[operation] = (p95, p99)
    if not budgets:
        raise ValueError("at least one explicit latency budget is required")
    return budgets


def validate_policy(args: argparse.Namespace) -> dict[str, Any]:
    integer_rules = {
        "minimum_duration_seconds": (args.minimum_duration_seconds, 1, 86_400),
        "minimum_samples": (args.minimum_samples, 3, 100_000),
        "maximum_sample_gap_seconds": (args.maximum_sample_gap_seconds, 1, 86_400),
        "maximum_health_age_seconds": (args.maximum_health_age_seconds, 0, 3_600),
        "maximum_connection_count_churn": (args.maximum_connection_count_churn, 0, MAX_COUNTER),
        "maximum_estimated_reconnects": (args.maximum_estimated_reconnects, 0, MAX_COUNTER),
    }
    for name, (value, minimum, maximum) in integer_rules.items():
        if isinstance(value, bool) or not isinstance(value, int) or not minimum <= value <= maximum:
            raise ValueError(f"{name} is outside its accepted range")
    if not 2 <= args.pressure_persistence_samples <= args.minimum_samples:
        raise ValueError("pressure_persistence_samples must be between 2 and minimum_samples")
    if not 3 <= args.growth_window_samples <= args.minimum_samples:
        raise ValueError("growth_window_samples must be between 3 and minimum_samples")
    budgets = parse_latency_budgets(args.latency_budget)
    return {
        "minimum_duration_seconds": args.minimum_duration_seconds,
        "minimum_samples": args.minimum_samples,
        "maximum_sample_gap_seconds": args.maximum_sample_gap_seconds,
        "maximum_health_age_seconds": args.maximum_health_age_seconds,
        "pressure_persistence_samples": args.pressure_persistence_samples,
        "growth_window_samples": args.growth_window_samples,
        "maximum_connection_count_churn": args.maximum_connection_count_churn,
        "maximum_estimated_reconnects": args.maximum_estimated_reconnects,
        "latency_budgets_ms": {
            name: {"p95": limits[0], "p99": limits[1]} for name, limits in sorted(budgets.items())
        },
        "_parsed_latency_budgets": budgets,
    }


def public_thresholds(
    policy: dict[str, Any] | None, observed_transport_bounds: dict[str, Any] | None
) -> dict[str, Any]:
    values = policy or {}
    result = {field: values.get(field) for field in PUBLIC_POLICY_FIELDS}
    if result["latency_budgets_ms"] is None:
        result["latency_budgets_ms"] = {}
    result["observed_transport_bounds"] = observed_transport_bounds
    return result


def empty_health_aggregates() -> dict[str, Any]:
    return {
        "sample_count": 0,
        "transport_snapshot_count": 0,
        "registered_connection_count_min": None,
        "registered_connection_count_max": None,
        "registered_connection_count_churn": 0,
        "estimated_reconnects": 0,
        "maximum_aggregate_queued_send_bytes": None,
        "maximum_pending_rpc_calls": None,
        "longest_send_pressure_run_samples": 0,
        "monotonic_nonzero_queue_growth_detected": False,
        "monotonic_nonzero_pending_rpc_growth_detected": False,
    }


def validate_bounds(value: Any, sample_index: int) -> dict[str, Any]:
    bounds = require_object(value, BOUND_FIELDS, f"Hub sample {sample_index} configured bounds")
    parsed: dict[str, Any] = {}
    for field in BOUND_INTEGER_FIELDS:
        parsed[field] = require_positive_int(bounds[field], f"Hub sample {sample_index} bound {field}")
    if not isinstance(bounds["mediaEnabled"], bool):
        raise ValueError(f"Hub sample {sample_index} mediaEnabled bound must be boolean")
    parsed["mediaEnabled"] = bounds["mediaEnabled"]
    return parsed


def validate_transport(
    value: Any, sample_index: int, errors: list[str]
) -> tuple[dict[str, int], dict[str, Any]]:
    transport = require_object(value, TRANSPORT_FIELDS, f"Hub sample {sample_index} transport snapshot")
    counters = {
        field: require_nonnegative_int(
            transport[field], f"Hub sample {sample_index} transport counter {field}"
        )
        for field in TRANSPORT_COUNTER_FIELDS
    }
    if not isinstance(transport["isDisposed"], bool):
        raise ValueError(f"Hub sample {sample_index} disposed state must be boolean")
    bounds = validate_bounds(transport["configuredBounds"], sample_index)

    def fail(condition: bool, message: str) -> None:
        if condition:
            errors.append(f"sample {sample_index}: {message}")

    fail(transport["isDisposed"], "Hub transport is disposed")
    fail(counters["faultedSendLoops"] != 0, "Hub has a faulted send loop")
    fail(
        counters["liveConnectionsWithInactiveSendLoops"] != 0,
        "Hub has a live connection with an inactive send loop",
    )
    fail(counters["negativeRuntimeCounters"] != 0, "Hub reported a negative runtime counter")
    fail(
        counters["unregisteredTrackedConnections"] != 0,
        "Hub registered index contains an unregistered connection",
    )
    fail(
        counters["pendingRpcCalls"] > bounds["maximumPendingRpcCalls"],
        "Hub global pending RPC limit is exceeded",
    )
    fail(
        counters["maximumConnectionsForOnePrincipal"] > bounds["maximumConnectionsPerPrincipal"],
        "Hub principal connection limit is exceeded",
    )
    fail(
        counters["maximumPendingRpcCallsForOnePrincipal"]
        > bounds["maximumPendingRpcCallsPerPrincipal"],
        "Hub principal pending RPC limit is exceeded",
    )
    fail(
        counters["maximumLogicalStreamsForOnePrincipal"] > bounds["maximumLogicalStreamsPerPrincipal"],
        "Hub principal logical-stream limit is exceeded",
    )
    fail(
        counters["maximumMediaStreamsForOnePrincipal"] > bounds["maximumMediaStreamsPerPrincipal"],
        "Hub principal media-stream limit is exceeded",
    )
    fail(
        counters["maximumSubscriptionsForOnePrincipal"] > bounds["maximumSubscriptionsPerPrincipal"],
        "Hub principal subscription limit is exceeded",
    )
    fail(
        not bounds["mediaEnabled"] and counters["activeMediaStreams"] != 0,
        "Hub has active media streams while media is disabled",
    )
    fail(
        counters["connectionsUnderSendPressure"] > counters["acceptedConnections"],
        "Hub send-pressure count exceeds accepted connections",
    )
    fail(
        counters["liveConnections"] > counters["acceptedConnections"],
        "Hub live connection count exceeds accepted connections",
    )
    fail(
        counters["closingConnections"] > counters["acceptedConnections"],
        "Hub closing connection count exceeds accepted connections",
    )
    fail(
        counters["maximumQueuedSendBytes"] > counters["aggregateQueuedSendBytes"],
        "Hub maximum queued bytes exceed aggregate queued bytes",
    )
    fail(
        counters["connectionsUnderSendPressure"] > 0
        and counters["maximumQueuedSendBytes"] <= bounds["sendBackpressureDropThresholdBytes"],
        "Hub send-pressure count is inconsistent with queued-byte bounds",
    )
    fail(
        counters["maximumQueuedSendBytes"] > bounds["sendBackpressureDropThresholdBytes"]
        and counters["connectionsUnderSendPressure"] == 0,
        "Hub queued-byte pressure is not reflected in the pressure count",
    )
    return counters, bounds


def validate_client_transport(
    value: Any, service: str, sample_index: int, errors: list[str]
) -> None:
    transport = require_object(
        value,
        CLIENT_TRANSPORT_FIELDS,
        f"{service} sample {sample_index} Bolt client transport snapshot",
    )
    if not isinstance(transport["isRegistered"], bool):
        raise ValueError(f"{service} sample {sample_index} Bolt client registration state must be boolean")
    if not isinstance(transport["isHealthy"], bool):
        raise ValueError(f"{service} sample {sample_index} Bolt client health state must be boolean")
    counters = {
        field: require_nonnegative_int(
            transport[field], f"{service} sample {sample_index} Bolt client transport counter {field}"
        )
        for field in CLIENT_TRANSPORT_INTEGER_FIELDS
    }
    if counters["pendingSendsUnhealthyThreshold"] == 0:
        raise ValueError(f"{service} sample {sample_index} Bolt client pending-send threshold must be positive")
    if counters["activeSendUnhealthyThresholdMs"] == 0:
        raise ValueError(f"{service} sample {sample_index} Bolt client active-send threshold must be positive")

    def fail(condition: bool, message: str) -> None:
        if condition:
            errors.append(f"sample {sample_index}: {service} {message}")

    connection_count = counters["connectionCount"]
    fail(not transport["isRegistered"], "Bolt client is not registered")
    fail(not transport["isHealthy"], "Bolt client snapshot is not healthy")
    fail(connection_count == 0, "Bolt client has no connections")
    fail(counters["connectedTransports"] != connection_count, "Bolt client has a disconnected transport")
    fail(counters["runningSendLoops"] != connection_count, "Bolt client has an inactive send loop")
    fail(counters["runningReceiveLoops"] != connection_count, "Bolt client has an inactive receive loop")
    fail(counters["faultedSendLoops"] != 0, "Bolt client has a faulted send loop")
    fail(counters["faultedReceiveLoops"] != 0, "Bolt client has a faulted receive loop")
    fail(counters["totalSendFailures"] != 0, "Bolt client recorded a send failure")
    fail(counters["totalSendTimeouts"] != 0, "Bolt client recorded a send timeout")
    fail(counters["totalReceiveLoopFaults"] != 0, "Bolt client recorded a receive-loop fault")
    fail(counters["totalUnexpectedDisconnects"] != 0, "Bolt client recorded an unexpected disconnect")
    fail(counters["totalSuccessfulReconnects"] != 0, "Bolt client reconnected during observation")
    fail(
        counters["maxActiveSendElapsedMs"] > counters["activeSendUnhealthyThresholdMs"],
        "Bolt client has a stalled active send",
    )
    fail(
        counters["pendingSends"] > counters["pendingSendsUnhealthyThreshold"],
        "Bolt client pending sends exceed the unhealthy threshold",
    )


def validate_health_response(
    value: Any,
    service: str,
    sample_index: int,
    sampled_at: Timestamp,
    maximum_health_age_seconds: int,
    errors: list[str],
) -> tuple[dict[str, int], dict[str, Any]] | None:
    response = require_object(value, HEALTH_FIELDS, f"{service} sample {sample_index} health response")
    if response["status"] != "Healthy":
        errors.append(f"sample {sample_index}: {service} readiness status is not Healthy")
    require_nonnegative_number(response["duration"], f"{service} sample {sample_index} health duration")
    health_at = parse_utc_timestamp(response["timestamp"], f"{service} sample {sample_index} health timestamp")
    if abs(seconds_between(health_at, sampled_at)) > maximum_health_age_seconds:
        errors.append(f"sample {sample_index}: {service} health response is stale")

    checks = require_list(response["checks"], f"{service} sample {sample_index} health checks")
    if not checks:
        raise ValueError(f"{service} sample {sample_index} has no health checks")
    if len(checks) > MAX_CHECKS_PER_SERVICE:
        raise ValueError(f"{service} sample {sample_index} has too many health checks")
    seen: set[str] = set()
    transport: dict[str, int] | None = None
    client_transport_seen = False
    for check_index, raw_check in enumerate(checks):
        check = require_object(
            raw_check,
            CHECK_FIELDS,
            f"{service} sample {sample_index} check {check_index}",
        )
        name = check["name"]
        if not isinstance(name, str) or not name or len(name) > 128 or name in seen:
            raise ValueError(f"{service} sample {sample_index} health check name is invalid or duplicated")
        seen.add(name)
        if check["status"] != "Healthy":
            errors.append(f"sample {sample_index}: {service} contains a non-Healthy readiness check")
        if check["exception"] is not None:
            errors.append(f"sample {sample_index}: {service} readiness check contains an exception")
        require_nonnegative_number(
            check["duration"], f"{service} sample {sample_index} check {check_index} duration"
        )
        if check["description"] is not None and not isinstance(check["description"], str):
            raise ValueError(f"{service} sample {sample_index} check description is invalid")
        tags = require_list(check["tags"], f"{service} sample {sample_index} check tags")
        if any(not isinstance(tag, str) or not tag or len(tag) > 64 for tag in tags) or len(set(tags)) != len(tags):
            raise ValueError(f"{service} sample {sample_index} check tags are invalid")
        if not isinstance(check["data"], dict):
            raise ValueError(f"{service} sample {sample_index} check data must be an object")
        if service == "bolt-hub" and name == TRANSPORT_CHECK:
            if set(check["data"]) != {"transport"}:
                raise ValueError(f"Hub sample {sample_index} transport check data schema is invalid")
            transport = validate_transport(check["data"]["transport"], sample_index, errors)
        if service in {"identityserver", "communications"} and name == CLIENT_TRANSPORT_CHECK:
            if set(check["data"]) != {"transport"}:
                raise ValueError(
                    f"{service} sample {sample_index} Bolt client transport check data schema is invalid"
                )
            validate_client_transport(check["data"]["transport"], service, sample_index, errors)
            client_transport_seen = True

    if service == "bolt-hub" and transport is None:
        errors.append(f"sample {sample_index}: Hub readiness is missing the Bolt-transport check")
    if service in {"identityserver", "communications"} and not client_transport_seen:
        errors.append(
            f"sample {sample_index}: {service} readiness is missing the Bolt-client-transport check"
        )
    return transport


def percentile_nearest_rank(values: list[int], percentile: int) -> int:
    if not values:
        raise ValueError("cannot calculate a percentile from an empty sample")
    ordered = sorted(values)
    rank = (percentile * len(ordered) + 99) // 100
    return ordered[max(0, rank - 1)]


def validate_synthetic_reports(
    reports: Any,
    observation_start: Timestamp,
    observation_end: Timestamp,
) -> dict[str, list[int]]:
    items = require_list(reports, "synthetic reports")
    if not items:
        raise ValueError("at least one already validated synthetic report is required")
    if len(items) > MAX_SYNTHETIC_REPORTS:
        raise ValueError("synthetic report count exceeds the accepted maximum")
    timings: dict[str, list[int]] = defaultdict(list)
    seen_run_ids: set[str] = set()
    for report_index, raw_report in enumerate(items):
        report = require_object(raw_report, SYNTHETIC_FIELDS, f"synthetic report {report_index}")
        if report["schemaVersion"] != SYNTHETIC_SCHEMA or report["status"] != "passed":
            raise ValueError(f"synthetic report {report_index} is not passed v1 evidence")
        run_id = report["runId"]
        if not isinstance(run_id, str) or not re.fullmatch(
            r"[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}", run_id
        ) or run_id in seen_run_ids:
            raise ValueError(f"synthetic report {report_index} run identifier is invalid or duplicated")
        seen_run_ids.add(run_id)
        if not isinstance(report["stage"], str) or not re.fullmatch(r"[a-z][a-z0-9-]{0,31}", report["stage"]):
            raise ValueError(f"synthetic report {report_index} stage is invalid")
        if not isinstance(report["coreReportSha256"], str) or not re.fullmatch(
            r"[0-9a-f]{64}", report["coreReportSha256"]
        ):
            raise ValueError(f"synthetic report {report_index} digest is invalid")
        if (
            not isinstance(report["postRunEvidence"], dict)
            or report["postRunEvidence"].get("schemaVersion")
            != "bolt-phase0-post-run-evidence/v1"
        ):
            raise ValueError(f"synthetic report {report_index} post-run evidence is invalid")

        core = require_object(report["synthetic"], CORE_FIELDS, f"synthetic report {report_index} core")
        if core["schemaVersion"] != SYNTHETIC_CORE_SCHEMA or core["status"] != "passed":
            raise ValueError(f"synthetic report {report_index} core is not passed v1 evidence")
        if core["runId"] != run_id:
            raise ValueError(f"synthetic report {report_index} run identifier does not bind its core")
        started = parse_utc_timestamp(core["startedAtUtc"], f"synthetic report {report_index} start")
        completed = parse_utc_timestamp(core["completedAtUtc"], f"synthetic report {report_index} completion")
        if started.ticks < observation_start.ticks or completed.ticks > observation_end.ticks or completed.ticks < started.ticks:
            raise ValueError(f"synthetic report {report_index} is outside the observation interval")
        require_object(core["timings"], {"totalMs"}, f"synthetic report {report_index} timings")
        require_nonnegative_int(core["timings"]["totalMs"], f"synthetic report {report_index} total timing")
        if not isinstance(core["tokenSha256Prefixes"], dict) or not isinstance(core["target"], str):
            raise ValueError(f"synthetic report {report_index} core evidence shape is invalid")

        operations = require_list(core["operations"], f"synthetic report {report_index} operations")
        if not operations:
            raise ValueError(f"synthetic report {report_index} has no operations")
        if len(operations) > MAX_OPERATIONS_PER_REPORT:
            raise ValueError(f"synthetic report {report_index} has too many operations")
        seen_operations: set[str] = set()
        for operation_index, raw_operation in enumerate(operations):
            operation = require_object(
                raw_operation,
                OPERATION_FIELDS,
                f"synthetic report {report_index} operation {operation_index}",
            )
            name = operation["name"]
            if not isinstance(name, str) or not SAFE_OPERATION.fullmatch(name) or name in seen_operations:
                raise ValueError(f"synthetic report {report_index} operation name is invalid or duplicated")
            seen_operations.add(name)
            if operation["status"] != "passed" or not isinstance(operation["results"], dict):
                raise ValueError(f"synthetic report {report_index} operation is not passed evidence")
            timing = require_nonnegative_int(
                operation["timingMs"], f"synthetic report {report_index} operation timing"
            )
            operation_start = parse_utc_timestamp(
                operation["startedAtUtc"], f"synthetic report {report_index} operation start"
            )
            operation_end = parse_utc_timestamp(
                operation["completedAtUtc"], f"synthetic report {report_index} operation completion"
            )
            if operation_start.ticks < started.ticks or operation_end.ticks > completed.ticks or operation_end.ticks < operation_start.ticks:
                raise ValueError(f"synthetic report {report_index} operation interval is invalid")
            timings[name].append(timing)
    return dict(timings)


def longest_true_run(values: Iterable[bool]) -> int:
    longest = current = 0
    for value in values:
        current = current + 1 if value else 0
        longest = max(longest, current)
    return longest


def has_monotonic_nonzero_growth(values: list[int], window: int) -> bool:
    run_start = 0
    for index, value in enumerate(values):
        if value <= 0 or (index > 0 and value < values[index - 1]):
            run_start = index + 1 if value <= 0 else index
            continue
        if index - run_start + 1 >= window and value > values[index - window + 1]:
            return True
    return False


def evaluate(document: Any, policy: dict[str, Any]) -> dict[str, Any]:
    errors: list[str] = []
    root = require_object(document, INPUT_FIELDS, "observation input")
    if root["schema"] != INPUT_SCHEMA:
        raise ValueError("observation input has an unsupported schema")
    observation_start = parse_utc_timestamp(root["observation_started_at_utc"], "observation start")
    observation_end = parse_utc_timestamp(root["observation_completed_at_utc"], "observation completion")
    duration = seconds_between(observation_start, observation_end)
    if duration < policy["minimum_duration_seconds"]:
        errors.append("observation duration is shorter than the configured minimum")
    if duration < 0:
        errors.append("observation completion precedes its start")

    samples = require_list(root["health_samples"], "health samples")
    if len(samples) > MAX_HEALTH_SAMPLES:
        raise ValueError("health sample count exceeds the accepted maximum")
    if len(samples) < policy["minimum_samples"]:
        errors.append("health sample count is below the configured minimum")
    sample_times: list[Timestamp] = []
    snapshots: list[dict[str, int]] = []
    observed_bounds: list[dict[str, Any]] = []
    previous: Timestamp | None = None
    for sample_index, raw_sample in enumerate(samples):
        sample = require_object(raw_sample, SAMPLE_FIELDS, f"health sample {sample_index}")
        sampled_at = parse_utc_timestamp(sample["sampled_at_utc"], f"health sample {sample_index} timestamp")
        sample_times.append(sampled_at)
        if previous is not None:
            gap = seconds_between(previous, sampled_at)
            if gap <= 0:
                errors.append(f"sample {sample_index}: health samples are not in strictly increasing order")
            elif gap > policy["maximum_sample_gap_seconds"]:
                errors.append(f"sample {sample_index}: health sample gap exceeds the configured maximum")
        previous = sampled_at
        if sampled_at.ticks < observation_start.ticks or sampled_at.ticks > observation_end.ticks:
            errors.append(f"sample {sample_index}: health sample is outside the observation interval")

        services = require_object(sample["services"], set(SERVICES), f"health sample {sample_index} services")
        snapshot: tuple[dict[str, int], dict[str, Any]] | None = None
        for service in SERVICES:
            service_result = require_object(
                services[service], SERVICE_FIELDS, f"{service} sample {sample_index} response"
            )
            if (
                isinstance(service_result["http_status"], bool)
                or not isinstance(service_result["http_status"], int)
                or service_result["http_status"] != 200
            ):
                errors.append(f"sample {sample_index}: {service} readiness HTTP status is not 200")
            candidate = validate_health_response(
                service_result["health"],
                service,
                sample_index,
                sampled_at,
                policy["maximum_health_age_seconds"],
                errors,
            )
            if service == "bolt-hub":
                snapshot = candidate
        if snapshot is not None:
            counters, bounds = snapshot
            snapshots.append(counters)
            observed_bounds.append(bounds)

    if observed_bounds and any(item != observed_bounds[0] for item in observed_bounds[1:]):
        errors.append("Hub configured transport bounds changed during the observation")

    if sample_times:
        if seconds_between(observation_start, sample_times[0]) > policy["maximum_sample_gap_seconds"]:
            errors.append("first health sample is stale relative to observation start")
        if seconds_between(sample_times[-1], observation_end) > policy["maximum_sample_gap_seconds"]:
            errors.append("last health sample is stale relative to observation completion")

    registered = [snapshot["registeredConnections"] for snapshot in snapshots]
    queued = [snapshot["aggregateQueuedSendBytes"] for snapshot in snapshots]
    pending_rpc = [snapshot["pendingRpcCalls"] for snapshot in snapshots]
    pressure = [snapshot["connectionsUnderSendPressure"] > 0 for snapshot in snapshots]
    positive_deltas = sum(max(0, right - left) for left, right in zip(registered, registered[1:]))
    negative_deltas = sum(max(0, left - right) for left, right in zip(registered, registered[1:]))
    churn = positive_deltas + negative_deltas
    estimated_reconnects = min(positive_deltas, negative_deltas)
    pressure_run = longest_true_run(pressure)
    queue_growth = has_monotonic_nonzero_growth(queued, policy["growth_window_samples"])
    rpc_growth = has_monotonic_nonzero_growth(pending_rpc, policy["growth_window_samples"])
    if pressure_run >= policy["pressure_persistence_samples"]:
        errors.append("Hub send pressure persisted for the configured failure window")
    if queue_growth:
        errors.append("Hub queued send bytes grew monotonically while nonzero")
    if rpc_growth:
        errors.append("Hub pending RPC calls grew monotonically while nonzero")
    if churn > policy["maximum_connection_count_churn"]:
        errors.append("Hub registered connection-count churn exceeds the configured maximum")
    if estimated_reconnects > policy["maximum_estimated_reconnects"]:
        errors.append("Hub estimated reconnect count exceeds the configured maximum")

    timings = validate_synthetic_reports(
        root["synthetic_reports"], observation_start, observation_end
    )
    budgets: dict[str, tuple[int, int]] = policy["_parsed_latency_budgets"]
    exact_budget_names = set(budgets) - {"*"}
    missing_operations = exact_budget_names - set(timings)
    if missing_operations:
        errors.append("one or more explicitly budgeted synthetic operations are missing")
    latency_aggregates: dict[str, Any] = {}
    for name, values in sorted(timings.items()):
        limits = budgets.get(name, budgets.get("*"))
        if limits is None:
            errors.append("one or more synthetic operations lack an explicit latency budget")
            continue
        p95 = percentile_nearest_rank(values, 95)
        p99 = percentile_nearest_rank(values, 99)
        if p95 > limits[0] or p99 > limits[1]:
            errors.append(f"synthetic operation {name} exceeds its caller-provided latency budget")
        latency_aggregates[name] = {
            "sample_count": len(values),
            "p95_ms": p95,
            "p99_ms": p99,
            "p95_budget_ms": limits[0],
            "p99_budget_ms": limits[1],
        }

    safe_thresholds = public_thresholds(policy, observed_bounds[0] if observed_bounds else None)
    health_aggregates = {
        "sample_count": len(samples),
        "transport_snapshot_count": len(snapshots),
        "registered_connection_count_min": min(registered) if registered else None,
        "registered_connection_count_max": max(registered) if registered else None,
        "registered_connection_count_churn": churn,
        "estimated_reconnects": estimated_reconnects,
        "maximum_aggregate_queued_send_bytes": max(queued) if queued else None,
        "maximum_pending_rpc_calls": max(pending_rpc) if pending_rpc else None,
        "longest_send_pressure_run_samples": pressure_run,
        "monotonic_nonzero_queue_growth_detected": queue_growth,
        "monotonic_nonzero_pending_rpc_growth_detected": rpc_growth,
    }
    if len(errors) > MAX_RETAINED_ERRORS:
        omitted = len(errors) - MAX_RETAINED_ERRORS
        errors = errors[:MAX_RETAINED_ERRORS] + [
            f"{omitted} additional validation errors were omitted from evidence"
        ]
    return {
        "schema": OUTPUT_SCHEMA,
        "generated_at_utc": canonical_now(),
        "status": "passed" if not errors else "failed",
        "observation": {
            "started_at_utc": observation_start.text,
            "completed_at_utc": observation_end.text,
            "duration_seconds": duration,
            "sample_count": len(samples),
        },
        "thresholds": safe_thresholds,
        "health_aggregates": health_aggregates,
        "synthetic_aggregates": {
            "report_count": len(root["synthetic_reports"]),
            "operation_latency": latency_aggregates,
        },
        "errors": errors,
    }


def canonical_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%fZ")


def failure_evidence(policy: dict[str, Any] | None, message: str) -> dict[str, Any]:
    return {
        "schema": OUTPUT_SCHEMA,
        "generated_at_utc": canonical_now(),
        "status": "failed",
        "observation": {
            "started_at_utc": None,
            "completed_at_utc": None,
            "duration_seconds": None,
            "sample_count": 0,
        },
        "thresholds": public_thresholds(policy, None),
        "health_aggregates": empty_health_aggregates(),
        "synthetic_aggregates": {"report_count": 0, "operation_latency": {}},
        "errors": [message],
    }


def atomic_write_json(path: Path, document: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    payload = (json.dumps(document, indent=2, sort_keys=True, allow_nan=False) + "\n").encode("utf-8")
    descriptor, temporary_name = tempfile.mkstemp(prefix=f".{path.name}.", suffix=".tmp", dir=path.parent)
    temporary = Path(temporary_name)
    try:
        os.chmod(temporary, stat.S_IRUSR | stat.S_IWUSR)
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
        try:
            directory_descriptor = os.open(path.parent, os.O_RDONLY)
        except OSError:
            directory_descriptor = None
        if directory_descriptor is not None:
            try:
                os.fsync(directory_descriptor)
            except OSError:
                pass
            finally:
                os.close(directory_descriptor)
    except Exception:
        try:
            os.close(descriptor)
        except OSError:
            pass
        temporary.unlink(missing_ok=True)
        raise


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    output = Path(args.output)
    policy: dict[str, Any] | None = None
    try:
        policy = validate_policy(args)
        document = load_json(Path(args.input))
        evidence = evaluate(document, policy)
    except (OSError, ValueError, json.JSONDecodeError, DuplicateKeyError, RecursionError):
        evidence = failure_evidence(policy, "observation input or policy failed strict validation")

    try:
        atomic_write_json(output, evidence)
    except OSError as error:
        print(f"ERROR: could not write observation evidence atomically: {error}", file=sys.stderr)
        return 1

    if evidence["status"] != "passed":
        for error in evidence["errors"]:
            print(f"ERROR: {error}", file=sys.stderr)
        print(f"Bolt Phase 0 observation evidence failed; evidence: {output}", file=sys.stderr)
        return 1
    print(f"Bolt Phase 0 observation evidence passed; evidence: {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
