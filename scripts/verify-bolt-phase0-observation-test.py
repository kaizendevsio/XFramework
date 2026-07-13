#!/usr/bin/env python3
from __future__ import annotations

import copy
import importlib.util
import json
import os
import tempfile
import unittest
from datetime import datetime, timedelta, timezone
from pathlib import Path
from types import SimpleNamespace


SCRIPT = Path(__file__).with_name("verify-bolt-phase0-observation.py")
SPEC = importlib.util.spec_from_file_location("phase0_observation", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


BASE = datetime(2026, 7, 13, 1, 0, 0, tzinfo=timezone.utc)
SECRET_TOKEN_HASH = "deadcafedead"
SECRET_TARGET = "wss://secret.internal.example/bolt/ws"
SECRET_IDENTITY = "principal:secret@example.invalid"


def timestamp(seconds: int) -> str:
    return (BASE + timedelta(seconds=seconds)).strftime("%Y-%m-%dT%H:%M:%S.%fZ")


def bounds() -> dict:
    return {
        "maximumFrameBytes": 8 * 1024 * 1024,
        "sendQueueCapacityPerConnection": 1024,
        "sendEnqueueTimeoutMilliseconds": 30_000,
        "sendBackpressureDropThresholdBytes": 1024 * 1024,
        "sendBackpressureFeedbackThresholdBytes": 2 * 1024 * 1024,
        "maximumPendingRpcCalls": 4096,
        "maximumPendingRpcCallsPerPrincipal": 128,
        "maximumConnectionsPerPrincipal": 8,
        "maximumLogicalStreamsPerPrincipal": 128,
        "maximumMediaStreamsPerPrincipal": 0 + 8,
        "maximumSubscriptionsPerPrincipal": 128,
        "maximumDurableSubscribersPerTopic": 1024,
        "mediaEnabled": False,
    }


def transport() -> dict:
    return {
        "acceptedConnections": 4,
        "registeredConnections": 4,
        "unregisteredConnections": 0,
        "liveConnections": 4,
        "closingConnections": 0,
        "unregisteredTrackedConnections": 0,
        "pendingRpcCalls": 0,
        "activeLogicalStreams": 0,
        "activeMediaStreams": 0,
        "activeCalls": 0,
        "activeSubscriptionReservations": 0,
        "liveTransientSubscriptions": 0,
        "liveDurableSubscriptions": 0,
        "aggregateQueuedSendBytes": 0,
        "maximumQueuedSendBytes": 0,
        "connectionsUnderSendPressure": 0,
        "runningSendLoops": 4,
        "completedSendLoops": 0,
        "faultedSendLoops": 0,
        "liveConnectionsWithInactiveSendLoops": 0,
        "negativeRuntimeCounters": 0,
        "maximumConnectionsForOnePrincipal": 1,
        "maximumPendingRpcCallsForOnePrincipal": 0,
        "maximumLogicalStreamsForOnePrincipal": 0,
        "maximumMediaStreamsForOnePrincipal": 0,
        "maximumSubscriptionsForOnePrincipal": 0,
        "isDisposed": False,
        "configuredBounds": bounds(),
    }


def serialized_bolt_server_health_snapshot() -> dict:
    """Mirror the camel-cased JSON emitted for BoltServerHealthSnapshot."""
    return json.loads(json.dumps(transport()))


def client_transport() -> dict:
    return {
        "isRegistered": True,
        "isHealthy": True,
        "connectionCount": 1,
        "connectedTransports": 1,
        "pendingSends": 0,
        "activeSends": 0,
        "maxActiveSendElapsedMs": 0,
        "runningSendLoops": 1,
        "runningReceiveLoops": 1,
        "faultedSendLoops": 0,
        "faultedReceiveLoops": 0,
        "pendingSendsUnhealthyThreshold": 819,
        "activeSendUnhealthyThresholdMs": 30_000,
        "totalSendFailures": 0,
        "totalSendTimeouts": 0,
        "totalReceiveLoopFaults": 0,
        "totalUnexpectedDisconnects": 0,
        "totalSuccessfulReconnects": 0,
    }


def check(name: str, data: dict | None = None) -> dict:
    return {
        "name": name,
        "status": "Healthy",
        "description": None,
        "duration": 1.25,
        "tags": ["ready"],
        "data": data or {},
        "exception": None,
    }


def health(service: str, sampled_at: str) -> dict:
    checks = [check(f"{service}-database")]
    if service == "bolt-hub":
        checks.append(check(MODULE.TRANSPORT_CHECK, {"transport": transport()}))
    elif service in {"identityserver", "communications"}:
        checks.append(check(MODULE.CLIENT_TRANSPORT_CHECK, {"transport": client_transport()}))
    return {
        "status": "Healthy",
        "duration": 2.5,
        "timestamp": sampled_at,
        "checks": checks,
    }


def sample(seconds: int) -> dict:
    sampled_at = timestamp(seconds)
    return {
        "sampled_at_utc": sampled_at,
        "services": {
            service: {"http_status": 200, "health": health(service, sampled_at)}
            for service in MODULE.SERVICES
        },
    }


def synthetic_report(timing_ms: int = 50, operation_name: str = "rpc_roundtrip") -> dict:
    run_id = "123e4567-e89b-42d3-a456-426614174000"
    operation = {
        "name": operation_name,
        "startedAtUtc": timestamp(5),
        "completedAtUtc": timestamp(6),
        "status": "passed",
        "timingMs": timing_ms,
        "results": {"identity": SECRET_IDENTITY},
    }
    core = {
        "schemaVersion": MODULE.SYNTHETIC_CORE_SCHEMA,
        "runId": run_id,
        "tokenSha256Prefixes": {"user": SECRET_TOKEN_HASH},
        "startedAtUtc": timestamp(4),
        "completedAtUtc": timestamp(7),
        "target": SECRET_TARGET,
        "status": "passed",
        "timings": {"totalMs": timing_ms},
        "operations": [operation],
    }
    return {
        "schemaVersion": MODULE.SYNTHETIC_SCHEMA,
        "runId": run_id,
        "stage": "canary",
        "status": "passed",
        "coreReportSha256": "a" * 64,
        "synthetic": core,
        "postRunEvidence": {
            "schemaVersion": "bolt-phase0-post-run-evidence/v1",
            "principalReference": SECRET_IDENTITY,
        },
    }


def document() -> dict:
    return {
        "schema": MODULE.INPUT_SCHEMA,
        "observation_started_at_utc": timestamp(0),
        "observation_completed_at_utc": timestamp(40),
        "health_samples": [sample(value) for value in (0, 10, 20, 30, 40)],
        "synthetic_reports": [synthetic_report()],
    }


def args(**overrides) -> SimpleNamespace:
    values = {
        "minimum_duration_seconds": 40,
        "minimum_samples": 5,
        "maximum_sample_gap_seconds": 10,
        "maximum_health_age_seconds": 2,
        "pressure_persistence_samples": 3,
        "growth_window_samples": 3,
        "maximum_connection_count_churn": 4,
        "maximum_estimated_reconnects": 2,
        "latency_budget": ["*=100,200"],
    }
    values.update(overrides)
    return SimpleNamespace(**values)


def policy(**overrides) -> dict:
    return MODULE.validate_policy(args(**overrides))


def hub_transport(observation: dict, index: int) -> dict:
    checks = observation["health_samples"][index]["services"]["bolt-hub"]["health"]["checks"]
    return next(item for item in checks if item["name"] == MODULE.TRANSPORT_CHECK)["data"]["transport"]


def service_client_transport(observation: dict, service: str, index: int) -> dict:
    checks = observation["health_samples"][index]["services"][service]["health"]["checks"]
    return next(item for item in checks if item["name"] == MODULE.CLIENT_TRANSPORT_CHECK)["data"]["transport"]


class Phase0ObservationVerifierTests(unittest.TestCase):
    def test_real_serialized_bolt_server_health_snapshot_shape_passes(self) -> None:
        candidate = document()
        snapshot = serialized_bolt_server_health_snapshot()
        hub_transport(candidate, 2).clear()
        hub_transport(candidate, 2).update(snapshot)

        evidence = MODULE.evaluate(candidate, policy())

        self.assertEqual("passed", evidence["status"])
        self.assertEqual(
            snapshot["configuredBounds"],
            evidence["thresholds"]["observed_transport_bounds"],
        )

    def test_valid_observation_passes_with_deterministic_latency_and_redacted_output(self) -> None:
        evidence = MODULE.evaluate(document(), policy())

        self.assertEqual("passed", evidence["status"])
        self.assertEqual(5, evidence["health_aggregates"]["sample_count"])
        self.assertEqual(0, evidence["health_aggregates"]["registered_connection_count_churn"])
        latency = evidence["synthetic_aggregates"]["operation_latency"]["rpc_roundtrip"]
        self.assertEqual(50, latency["p95_ms"])
        self.assertEqual(50, latency["p99_ms"])
        serialized = json.dumps(evidence, sort_keys=True)
        self.assertNotIn(SECRET_TOKEN_HASH, serialized)
        self.assertNotIn(SECRET_TARGET, serialized)
        self.assertNotIn(SECRET_IDENTITY, serialized)
        self.assertNotIn("results", serialized)

    def test_missing_canary_client_transport_check_fails_closed(self) -> None:
        candidate = document()
        checks = candidate["health_samples"][2]["services"]["communications"]["health"]["checks"]
        checks[:] = [item for item in checks if item["name"] != MODULE.CLIENT_TRANSPORT_CHECK]

        evidence = MODULE.evaluate(candidate, policy())

        self.assertEqual("failed", evidence["status"])
        self.assertTrue(any("missing the Bolt-client-transport check" in error for error in evidence["errors"]))

    def test_canary_client_send_loop_failure_fails_closed(self) -> None:
        candidate = document()
        snapshot = service_client_transport(candidate, "identityserver", 2)
        snapshot["runningSendLoops"] = 0
        snapshot["faultedSendLoops"] = 1

        evidence = MODULE.evaluate(candidate, policy())

        self.assertEqual("failed", evidence["status"])
        self.assertTrue(any("inactive send loop" in error for error in evidence["errors"]))
        self.assertTrue(any("faulted send loop" in error for error in evidence["errors"]))

    def test_canary_client_computed_unhealthy_state_fails_closed(self) -> None:
        candidate = document()
        service_client_transport(candidate, "identityserver", 2)["isHealthy"] = False

        evidence = MODULE.evaluate(candidate, policy())

        self.assertEqual("failed", evidence["status"])
        self.assertTrue(any("snapshot is not healthy" in error for error in evidence["errors"]))

    def test_transient_canary_client_failures_leave_failing_watermarks(self) -> None:
        fields = {
            "totalSendFailures": "recorded a send failure",
            "totalSendTimeouts": "recorded a send timeout",
            "totalReceiveLoopFaults": "recorded a receive-loop fault",
            "totalUnexpectedDisconnects": "recorded an unexpected disconnect",
            "totalSuccessfulReconnects": "reconnected during observation",
        }
        for field, expected_error in fields.items():
            with self.subTest(field=field):
                candidate = document()
                service_client_transport(candidate, "communications", 3)[field] = 1

                evidence = MODULE.evaluate(candidate, policy())

                self.assertEqual("failed", evidence["status"])
                self.assertTrue(any(expected_error in error for error in evidence["errors"]))

    def test_canary_client_transport_schema_and_thresholds_are_strict(self) -> None:
        mutations = (
            lambda value: value.pop("runningReceiveLoops"),
            lambda value: value.__setitem__("isRegistered", 1),
            lambda value: value.__setitem__("isHealthy", 1),
            lambda value: value.__setitem__("pendingSendsUnhealthyThreshold", 0),
            lambda value: value.__setitem__("activeSendUnhealthyThresholdMs", True),
        )
        for mutation in mutations:
            with self.subTest(mutation=mutation):
                candidate = document()
                mutation(service_client_transport(candidate, "communications", 1))
                with self.assertRaises(ValueError):
                    MODULE.evaluate(candidate, policy())

    def test_valid_cli_run_writes_atomic_versioned_evidence(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            input_path = root / "input.json"
            output_path = root / "evidence.json"
            input_path.write_text(json.dumps(document()), encoding="utf-8")

            exit_code = MODULE.main(self.cli(input_path, output_path))

            self.assertEqual(0, exit_code)
            evidence = json.loads(output_path.read_text(encoding="utf-8"))
            self.assertEqual(MODULE.OUTPUT_SCHEMA, evidence["schema"])
            self.assertEqual("passed", evidence["status"])
            self.assertEqual(
                {
                    "schema",
                    "generated_at_utc",
                    "status",
                    "observation",
                    "thresholds",
                    "health_aggregates",
                    "synthetic_aggregates",
                    "errors",
                },
                set(evidence),
            )
            if os.name != "nt":
                self.assertEqual(0o600, output_path.stat().st_mode & 0o777)

    def test_malformed_duplicate_and_nan_json_fail_closed_and_write_evidence(self) -> None:
        cases = (
            "{",
            '{"schema":"one","schema":"two"}',
            '{"schema":NaN}',
        )
        for raw in cases:
            with self.subTest(raw=raw), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                input_path = root / "input.json"
                output_path = root / "evidence.json"
                input_path.write_text(raw, encoding="utf-8")
                exit_code = MODULE.main(self.cli(input_path, output_path))
                self.assertEqual(1, exit_code)
                evidence = json.loads(output_path.read_text(encoding="utf-8"))
                self.assertEqual("failed", evidence["status"])
                self.assertEqual(
                    ["observation input or policy failed strict validation"], evidence["errors"]
                )
                if os.name != "nt":
                    self.assertEqual(0o600, output_path.stat().st_mode & 0o777)

    def test_stale_out_of_order_and_noncanonical_samples_fail(self) -> None:
        scenarios = []
        stale = document()
        stale["health_samples"][2]["services"]["identityserver"]["health"]["timestamp"] = timestamp(0)
        scenarios.append((stale, "stale"))
        out_of_order = document()
        out_of_order["health_samples"][2]["sampled_at_utc"] = timestamp(5)
        scenarios.append((out_of_order, "strictly increasing"))
        for candidate, expected in scenarios:
            with self.subTest(expected=expected):
                evidence = MODULE.evaluate(candidate, policy())
                self.assertEqual("failed", evidence["status"])
                self.assertTrue(any(expected in error for error in evidence["errors"]))
        invalid = document()
        invalid["health_samples"][0]["sampled_at_utc"] = "2026-07-13T01:00:00+00:00"
        with self.assertRaisesRegex(ValueError, "Z suffix"):
            MODULE.evaluate(invalid, policy())

    def test_missing_transport_check_fails(self) -> None:
        candidate = document()
        candidate["health_samples"][1]["services"]["bolt-hub"]["health"]["checks"] = [
            check("bolt-hub-database")
        ]
        evidence = MODULE.evaluate(candidate, policy())
        self.assertTrue(any("missing the Bolt-transport" in error for error in evidence["errors"]))

    def test_persistent_send_pressure_fails(self) -> None:
        candidate = document()
        for index in (1, 2, 3):
            snapshot = hub_transport(candidate, index)
            snapshot["aggregateQueuedSendBytes"] = 2 * 1024 * 1024
            snapshot["maximumQueuedSendBytes"] = 2 * 1024 * 1024
            snapshot["connectionsUnderSendPressure"] = 1
        evidence = MODULE.evaluate(candidate, policy())
        self.assertTrue(any("send pressure persisted" in error for error in evidence["errors"]))

    def test_monotonic_nonzero_queue_and_rpc_growth_fail(self) -> None:
        candidate = document()
        for index, value in enumerate((0, 10, 20, 30, 0)):
            snapshot = hub_transport(candidate, index)
            snapshot["aggregateQueuedSendBytes"] = value
            snapshot["maximumQueuedSendBytes"] = value
        for index, value in enumerate((0, 1, 2, 3, 0)):
            hub_transport(candidate, index)["pendingRpcCalls"] = value
        evidence = MODULE.evaluate(candidate, policy())
        self.assertTrue(any("queued send bytes grew monotonically" in error for error in evidence["errors"]))
        self.assertTrue(any("pending RPC calls grew monotonically" in error for error in evidence["errors"]))

    def test_connection_churn_and_reconnect_thresholds_fail(self) -> None:
        candidate = document()
        for index, value in enumerate((4, 1, 4, 1, 4)):
            hub_transport(candidate, index)["registeredConnections"] = value
        evidence = MODULE.evaluate(
            candidate,
            policy(maximum_connection_count_churn=4, maximum_estimated_reconnects=2),
        )
        self.assertGreater(evidence["health_aggregates"]["registered_connection_count_churn"], 4)
        self.assertGreater(evidence["health_aggregates"]["estimated_reconnects"], 2)
        self.assertTrue(any("churn exceeds" in error for error in evidence["errors"]))
        self.assertTrue(any("reconnect count exceeds" in error for error in evidence["errors"]))

    def test_over_limit_disposed_and_faulted_transport_data_fail(self) -> None:
        candidate = document()
        snapshot = hub_transport(candidate, 2)
        snapshot["pendingRpcCalls"] = snapshot["configuredBounds"]["maximumPendingRpcCalls"] + 1
        snapshot["maximumConnectionsForOnePrincipal"] = (
            snapshot["configuredBounds"]["maximumConnectionsPerPrincipal"] + 1
        )
        snapshot["faultedSendLoops"] = 1
        snapshot["liveConnectionsWithInactiveSendLoops"] = 1
        snapshot["isDisposed"] = True
        evidence = MODULE.evaluate(candidate, policy())
        joined = "\n".join(evidence["errors"])
        self.assertIn("pending RPC limit is exceeded", joined)
        self.assertIn("principal connection limit is exceeded", joined)
        self.assertIn("faulted send loop", joined)
        self.assertIn("inactive send loop", joined)
        self.assertIn("transport is disposed", joined)

    def test_principal_pending_rpc_limit_uses_principal_bound(self) -> None:
        candidate = document()
        snapshot = hub_transport(candidate, 2)
        snapshot["maximumPendingRpcCallsForOnePrincipal"] = (
            snapshot["configuredBounds"]["maximumPendingRpcCallsPerPrincipal"] + 1
        )
        self.assertLess(
            snapshot["maximumPendingRpcCallsForOnePrincipal"],
            snapshot["configuredBounds"]["maximumPendingRpcCalls"],
        )

        evidence = MODULE.evaluate(candidate, policy())

        self.assertTrue(any("principal pending RPC limit is exceeded" in error for error in evidence["errors"]))

    def test_missing_or_invalid_principal_pending_rpc_bound_fails_closed(self) -> None:
        cases = {
            "missing": None,
            "zero": 0,
            "boolean": True,
            "string": "128",
        }
        for name, invalid_value in cases.items():
            with self.subTest(name=name):
                candidate = document()
                configured_bounds = hub_transport(candidate, 2)["configuredBounds"]
                if invalid_value is None:
                    del configured_bounds["maximumPendingRpcCallsPerPrincipal"]
                    expected = "does not match its required schema"
                else:
                    configured_bounds["maximumPendingRpcCallsPerPrincipal"] = invalid_value
                    expected = "must be"

                with self.assertRaisesRegex(ValueError, expected):
                    MODULE.evaluate(candidate, policy())

    def test_negative_counter_and_transport_bound_drift_fail(self) -> None:
        negative = document()
        hub_transport(negative, 2)["activeCalls"] = -1
        with self.assertRaisesRegex(ValueError, "nonnegative"):
            MODULE.evaluate(negative, policy())

        drift = document()
        hub_transport(drift, 3)["configuredBounds"]["maximumPendingRpcCalls"] += 1
        evidence = MODULE.evaluate(drift, policy())
        self.assertTrue(any("bounds changed" in error for error in evidence["errors"]))

    def test_failed_http_health_and_check_status_fail(self) -> None:
        candidate = document()
        response = candidate["health_samples"][3]["services"]["communications"]
        response["http_status"] = 503
        response["health"]["status"] = "Unhealthy"
        response["health"]["checks"][0]["status"] = "Unhealthy"
        evidence = MODULE.evaluate(candidate, policy())
        joined = "\n".join(evidence["errors"])
        self.assertIn("HTTP status is not 200", joined)
        self.assertIn("readiness status is not Healthy", joined)
        self.assertIn("non-Healthy readiness check", joined)

    def test_latency_budget_breach_and_missing_budget_fail(self) -> None:
        candidate = document()
        candidate["synthetic_reports"][0]["synthetic"]["operations"][0]["timingMs"] = 250
        evidence = MODULE.evaluate(candidate, policy(latency_budget=["rpc_roundtrip=100,200"]))
        self.assertTrue(any("latency budget" in error for error in evidence["errors"]))

        missing = MODULE.evaluate(candidate, policy(latency_budget=["different_operation=100,200"]))
        self.assertTrue(any("explicitly budgeted" in error for error in missing["errors"]))
        self.assertTrue(any("lack an explicit" in error for error in missing["errors"]))

    def test_nearest_rank_percentiles_are_deterministic(self) -> None:
        candidate = document()
        reports = []
        for index, timing in enumerate(range(1, 101), start=1):
            report = synthetic_report(timing)
            report["runId"] = f"123e4567-e89b-42d3-a456-{index:012x}"
            report["synthetic"]["runId"] = report["runId"]
            reports.append(report)
        candidate["synthetic_reports"] = reports
        evidence = MODULE.evaluate(candidate, policy(latency_budget=["rpc_roundtrip=95,99"]))
        latency = evidence["synthetic_aggregates"]["operation_latency"]["rpc_roundtrip"]
        self.assertEqual(95, latency["p95_ms"])
        self.assertEqual(99, latency["p99_ms"])
        self.assertEqual("passed", evidence["status"])

    @staticmethod
    def cli(input_path: Path, output_path: Path) -> list[str]:
        return [
            "--input",
            str(input_path),
            "--output",
            str(output_path),
            "--minimum-duration-seconds",
            "40",
            "--minimum-samples",
            "5",
            "--maximum-sample-gap-seconds",
            "10",
            "--maximum-health-age-seconds",
            "2",
            "--pressure-persistence-samples",
            "3",
            "--growth-window-samples",
            "3",
            "--maximum-connection-count-churn",
            "4",
            "--maximum-estimated-reconnects",
            "2",
            "--latency-budget",
            "*=100,200",
        ]


if __name__ == "__main__":
    unittest.main()
