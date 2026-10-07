"""Local scanner ingress contract checks; never invokes real Tailscale or curl."""
import copy
import json
import importlib.util
import os
import re
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parent.parent
SCRIPT = ROOT / "scripts/configure-portal-scanner-dev-ingress.sh"
ADDRESS = "xeon-dev.tailed40e.ts.net:5443"
PORTAL_SITE = {"Handlers": {"/": {"Proxy": "http://127.0.0.1:5000"}}}
spec = importlib.util.spec_from_file_location("portal_proxy", ROOT / "scripts/resolve-portal-trusted-proxy.py")
proxy = importlib.util.module_from_spec(spec)
spec.loader.exec_module(proxy)


class PortalProxyMetadataTests(unittest.TestCase):
    def test_candidate_derivation_replaces_only_proxy_setting_before_container_recreation(self):
        workflow = (ROOT / ".github/workflows/deploy-xeon-dev.yml").read_text(encoding="utf-8")
        blocks = re.findall(r"<<\s*'PY'[^\n]*\n(.*?)^\s*PY\s*$", workflow, re.S | re.M)
        body = next(block for block in blocks if 'lines.append(f"PORTAL_TRUSTED_PROXY_IP={proxy_ip}")' in block)
        body = "\n".join(line[10:] for line in body.splitlines())
        with tempfile.TemporaryDirectory() as temp:
            candidate = Path(temp) / "synthetic-candidate"
            candidate.write_text("PORTAL_EXPOSE_PORT=5000\nUNCHANGED=fixture\nPORTAL_TRUSTED_PROXY_IP=192.0.2.1\n")
            result = subprocess.run([sys.executable, "-B", "-c", body, str(candidate), "10.20.30.1"], capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stderr)
            self.assertEqual("PORTAL_EXPOSE_PORT=5000\nUNCHANGED=fixture\nPORTAL_TRUSTED_PROXY_IP=10.20.30.1\n", candidate.read_text())

    def network(self):
        return {
            "Name": "xframework_default", "Driver": "bridge",
            "Labels": {"com.docker.compose.project": "xframework", "com.docker.compose.network": "default"},
            "IPAM": {"Config": [{"Subnet": "172.16.1.0/24", "Gateway": "172.16.1.1"}]},
        }

    def test_exact_gateway_is_derived_from_metadata_not_a_hardcoded_range(self):
        network = self.network()
        self.assertEqual("172.16.1.1", proxy.resolve_gateway(network, "xframework"))
        network["IPAM"]["Config"] = [{"Subnet": "10.20.30.0/24", "Gateway": "10.20.30.1"}]
        self.assertEqual("10.20.30.1", proxy.resolve_gateway(network, "xframework"))

    def test_wrong_network_driver_project_and_missing_or_ambiguous_gateway_fail_closed(self):
        for mismatch in ("name", "driver", "project", "missing", "multiple", "outside", "unspecified"):
            with self.subTest(mismatch=mismatch):
                network = self.network()
                if mismatch == "name":
                    network["Name"] = "other_default"
                elif mismatch == "driver":
                    network["Driver"] = "overlay"
                elif mismatch == "project":
                    network["Labels"]["com.docker.compose.project"] = "other"
                elif mismatch == "missing":
                    network["IPAM"]["Config"] = []
                elif mismatch == "multiple":
                    network["IPAM"]["Config"] *= 2
                elif mismatch == "outside":
                    network["IPAM"]["Config"][0]["Gateway"] = "172.16.2.1"
                else:
                    network["IPAM"]["Config"] = [{"Subnet": "0.0.0.0/0", "Gateway": "0.0.0.0"}]
                with self.assertRaises(ValueError):
                    proxy.resolve_gateway(network, "xframework")


class ScannerIngressTests(unittest.TestCase):
    def setUp(self):
        git_bash = Path("C:/Program Files/Git/bin/bash.exe")
        self.bash = str(git_bash) if git_bash.exists() else shutil.which("bash")
        if not self.bash:
            self.skipTest("Bash is required for local ingress contract checks")
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.directory = Path(self.temp.name)
        self.marker = self.directory / "ownership.json"
        self.calls = self.directory / "calls.txt"
        binaries = self.directory / "bin"
        binaries.mkdir()
        mocks = {
            "tailscale": """#!/usr/bin/env bash
set -euo pipefail
if [ "$*" = 'serve status --json' ]; then
    printf '%s\\n' "$SCANNER_FIXTURE_STATE"
else
    printf 'tailscale %s\\n' "$*" >> "$SCANNER_FIXTURE_CALLS"
    exit "${SCANNER_FIXTURE_SERVE_EXIT:-0}"
fi
""",
            "curl": """#!/usr/bin/env bash
set -euo pipefail
printf 'curl %s\\n' "$*" >> "$SCANNER_FIXTURE_CALLS"
exit "${SCANNER_FIXTURE_CURL_EXIT:-0}"
""",
            "python3": f"#!/usr/bin/env bash\nexec '{Path(sys.executable).as_posix()}' \"$@\"\n",
        }
        for name, body in mocks.items():
            path = binaries / name
            path.write_text(body, encoding="utf-8", newline="\n")
            path.chmod(0o700)
        self.environment = dict(os.environ)
        self.environment["SCANNER_FIXTURE_BIN"] = binaries.as_posix()
        self.environment["SCANNER_FIXTURE_CALLS"] = self.calls.as_posix()
        self.state = {
            "TCP": {"443": {"HTTPS": True}, "8443": {"HTTPS": True}},
            "Web": {
                "xeon-dev.tailed40e.ts.net:443": {"Handlers": {"/": {"Proxy": "http://127.0.0.1:8188"}}},
                "xeon-dev.tailed40e.ts.net:8443": {"Handlers": {"/": {"Proxy": "http://127.0.0.1:5188"}}},
            },
            "AllowFunnel": {"xeon-dev.tailed40e.ts.net:8443": True},
        }

    def run_script(self, mode="configure"):
        self.environment["SCANNER_FIXTURE_STATE"] = json.dumps(self.state)
        return subprocess.run(
            [self.bash, "-c", """set -euo pipefail
tailscale() { bash "$SCANNER_FIXTURE_BIN/tailscale" "$@"; }
curl() { bash "$SCANNER_FIXTURE_BIN/curl" "$@"; }
python3() { bash "$SCANNER_FIXTURE_BIN/python3" "$@"; }
source "$1" "$2" "$3"
""", "scanner-ingress-test", str(SCRIPT), mode, self.marker.as_posix()],
            cwd=ROOT, env=self.environment, capture_output=True, text=True, timeout=15,
        )

    def called(self):
        return self.calls.read_text().splitlines() if self.calls.exists() else []

    def existing_portal(self):
        self.state["TCP"]["5443"] = {"HTTPS": True}
        self.state["Web"][ADDRESS] = copy.deepcopy(PORTAL_SITE)

    def test_new_listener_is_targeted_and_health_checks_validate_tls(self):
        result = self.run_script()
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual({"created": True}, json.loads(self.marker.read_text()))
        self.assertEqual("tailscale serve --bg --yes --https=5443 http://127.0.0.1:5000", self.called()[0])
        self.assertEqual(3, len(self.called()))
        self.assertIn("https://xeon-dev.tailed40e.ts.net:5443/health/ready", self.called()[1])
        self.assertIn("/_framework/blazor.web.js", self.called()[2])
        self.assertNotIn("--insecure", " ".join(self.called()))
        self.assertNotIn("reset", " ".join(self.called()))
        self.assertNotIn("funnel", " ".join(self.called()))

    def test_exact_existing_listener_is_reused_and_not_removed_on_rollback(self):
        self.existing_portal()
        result = self.run_script()
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual({"created": False}, json.loads(self.marker.read_text()))
        result = self.run_script("rollback")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertFalse(self.marker.exists())
        self.assertFalse(any(call.startswith("tailscale ") for call in self.called()))

    def test_new_listener_is_removed_with_other_routes_and_funnel_present(self):
        self.assertEqual(0, self.run_script().returncode)
        self.existing_portal()
        result = self.run_script("rollback")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("tailscale serve --bg --yes --https=5443 off", self.called()[-1])
        self.assertFalse(self.marker.exists())

    def test_readiness_failure_retains_ownership_for_rollback(self):
        self.environment["SCANNER_FIXTURE_CURL_EXIT"] = "22"
        self.assertNotEqual(0, self.run_script().returncode)
        self.assertEqual({"created": True}, json.loads(self.marker.read_text()))
        self.existing_portal()
        self.assertEqual(0, self.run_script("rollback").returncode)
        self.assertEqual("tailscale serve --bg --yes --https=5443 off", self.called()[-1])

    def test_serve_failure_before_listener_creation_is_safe_to_rollback(self):
        self.environment["SCANNER_FIXTURE_SERVE_EXIT"] = "1"
        self.assertNotEqual(0, self.run_script().returncode)
        self.assertTrue(self.marker.exists())
        self.assertEqual(0, self.run_script("rollback").returncode)
        self.assertEqual(1, len(self.called()), "absent listener needs no removal command")

    def test_conflicting_listener_route_funnel_and_foreground_fail_before_mutation(self):
        base = copy.deepcopy(self.state)
        for conflict in ("tcp", "proxy", "path", "host", "funnel", "foreground", "incomplete"):
            with self.subTest(conflict=conflict):
                self.state = copy.deepcopy(base)
                self.existing_portal()
                if conflict == "tcp":
                    self.state["TCP"]["5443"] = {"TCPForward": "127.0.0.1:9000"}
                elif conflict == "proxy":
                    self.state["Web"][ADDRESS]["Handlers"]["/"]["Proxy"] = "http://127.0.0.1:9000"
                elif conflict == "path":
                    self.state["Web"][ADDRESS]["Handlers"]["/other"] = {"Proxy": "http://127.0.0.1:9000"}
                elif conflict == "host":
                    self.state["Web"]["another.tailnet.ts.net:5443"] = self.state["Web"].pop(ADDRESS)
                elif conflict == "funnel":
                    self.state["AllowFunnel"][ADDRESS] = True
                elif conflict == "foreground":
                    self.state["Foreground"] = {"session": {"TCP": {"5443": {"HTTPS": True}}}}
                else:
                    self.state["Web"].pop(ADDRESS)
                result = self.run_script()
                self.assertNotEqual(0, result.returncode)
                self.assertFalse(self.marker.exists())
                self.assertEqual([], self.called())

    def test_rollback_refuses_listener_replaced_by_another_owner(self):
        self.assertEqual(0, self.run_script().returncode)
        self.existing_portal()
        self.state["Web"][ADDRESS]["Handlers"]["/"]["Proxy"] = "http://127.0.0.1:9000"
        count = len(self.called())
        result = self.run_script("rollback")
        self.assertNotEqual(0, result.returncode)
        self.assertTrue(self.marker.exists())
        self.assertEqual(count, len(self.called()))

    def test_rollback_without_marker_is_noop(self):
        self.assertEqual(0, self.run_script("rollback").returncode)
        self.assertEqual([], self.called())

    def test_marker_is_written_exclusively_before_any_mutation(self):
        self.marker.write_text('{"created":false}')
        self.assertNotEqual(0, self.run_script().returncode)
        self.assertEqual([], self.called())

    def test_compose_and_workflow_preserve_desktop_http_port(self):
        compose = (ROOT / "docker-compose.yml").read_text(encoding="utf-8")
        workflow = (ROOT / ".github/workflows/deploy-xeon-dev.yml").read_text(encoding="utf-8")
        self.assertIn('"${PORTAL_EXPOSE_PORT:-5000}:8080"', compose)
        self.assertIn("Portal__ScannerPublicBaseUrl: ${PORTAL_SCANNER_PUBLIC_BASE_URL:-https://xeon-dev.tailed40e.ts.net:5443}", compose)
        self.assertIn('"PORTAL_SCANNER_PUBLIC_BASE_URL": "https://xeon-dev.tailed40e.ts.net:5443"', workflow)
        self.assertNotIn('"PORTAL_EXPOSE_PORT":', workflow)
        self.assertIn("Portal__TrustedProxyIp: ${PORTAL_TRUSTED_PROXY_IP:-}", compose)
        self.assertIn('docker network inspect "${COMPOSE_PROJECT_NAME}_default"', workflow)
        self.assertIn('lines.append(f"PORTAL_TRUSTED_PROXY_IP={proxy_ip}")', workflow)
        self.assertLess(workflow.index('lines.append(f"PORTAL_TRUSTED_PROXY_IP={proxy_ip}")'), workflow.index('--force-recreate "${clients[@]}"'))
        self.assertIn("configure-portal-scanner-dev-ingress.sh' configure", workflow)
        self.assertIn('configure-portal-scanner-dev-ingress.sh" rollback', workflow)


if __name__ == "__main__":
    unittest.main()
