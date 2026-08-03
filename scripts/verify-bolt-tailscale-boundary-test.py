#!/usr/bin/env python3
from __future__ import annotations

import importlib.util
import json
import re
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).with_name("verify-bolt-tailscale-boundary.py")
CONFIGURATOR = Path(__file__).with_name("configure-bolt-tailscale-boundary.sh")
MAGICDNS_HOST = "xeon-dev.example-tail.ts.net"

SPEC = importlib.util.spec_from_file_location("verify_bolt_tailscale_boundary", SCRIPT)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"unable to load {SCRIPT}")
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def valid_serve_status() -> dict[str, object]:
    return {
        "TCP": {
            "7000": {"HTTPS": True},
            "8261": {"HTTPS": True},
            "9443": {"HTTPS": True},
        },
        "Web": {
            f"{MAGICDNS_HOST}:7000": {
                "Handlers": {"/": {"Proxy": "http://127.0.0.1:7000"}}
            },
            f"{MAGICDNS_HOST}:8261": {
                "Handlers": {"/": {"Proxy": "http://127.0.0.1:8261"}}
            },
            f"{MAGICDNS_HOST}:9443": {
                "Handlers": {"/unrelated": {"Proxy": "http://127.0.0.1:9000"}}
            },
        },
        "AllowFunnel": {
            f"{MAGICDNS_HOST}:7000": False,
            "other-node.example-tail.ts.net:443": True,
        },
    }


def valid_compose() -> dict[str, object]:
    services: dict[str, object] = {}
    for service, published in (("bolt-hub", 7000), ("identityserver", 8261)):
        services[service] = {
            "build": {"context": ".", "dockerfile": "Dockerfile"},
            "environment": {
                "ASPNETCORE_URLS": "http://+:8080",
                "Kestrel__Endpoints__Http__Url": "http://0.0.0.0:8080",
                "ServiceIdentity__ClientSecret": "fixture-secret-not-for-evidence",
            },
            "ports": [
                {
                    "host_ip": "127.0.0.1",
                    "mode": "ingress",
                    "protocol": "tcp",
                    "published": str(published),
                    "target": 8080,
                }
            ],
        }
    services["portal"] = {
        "build": {"context": ".", "dockerfile": "Dockerfile"},
        "environment": {"ASPNETCORE_URLS": "http://+:8080"},
    }
    services["identityserver"]["environment"][
        "ServiceIdentity__BoltTransportTokenIssuer__SigningKeyPath"
    ] = "/var/lib/xframework/identity/bolt-transport-signing-key.pem"
    services["identityserver"]["volumes"] = [
        {
            "source": "identity-keydata",
            "target": "/var/lib/xframework/identity",
            "type": "volume",
            "volume": {},
        }
    ]
    return {"services": services, "volumes": {"identity-keydata": {}}}


def previous_serve_status() -> dict[str, object]:
    status = valid_serve_status()
    status["TCP"].pop("7000")
    status["TCP"].pop("8261")
    status["Web"].pop(f"{MAGICDNS_HOST}:7000")
    status["Web"].pop(f"{MAGICDNS_HOST}:8261")
    status["AllowFunnel"][f"{MAGICDNS_HOST}:7000"] = True
    return status


class BoundaryVerifierTests(unittest.TestCase):
    def verify(
        self,
        serve_status: object | None = None,
        compose: object | None = None,
        **kwargs: object,
    ) -> list[str]:
        return MODULE.verify_boundary(
            valid_serve_status() if serve_status is None else serve_status,
            valid_compose() if compose is None else compose,
            MAGICDNS_HOST,
            **kwargs,
        )

    def test_valid_boundary_and_unrelated_handlers_are_accepted(self) -> None:
        errors = self.verify(
            funnel_config={
                "AllowFunnel": {
                    f"{MAGICDNS_HOST}:7000": False,
                    "other-node.example-tail.ts.net:443": True,
                }
            },
            previous_serve_status=previous_serve_status(),
        )

        self.assertEqual([], errors)

    def test_missing_and_wrong_proxy_fail_closed(self) -> None:
        missing = valid_serve_status()
        missing["Web"].pop(f"{MAGICDNS_HOST}:7000")
        wrong = valid_serve_status()
        wrong["Web"][f"{MAGICDNS_HOST}:8261"]["Handlers"]["/"][
            "Proxy"
        ] = "http://127.0.0.1:7000"

        self.assertIn("serve.root_proxy.missing:7000", self.verify(missing))
        self.assertIn("serve.root_proxy.wrong_target:8261", self.verify(wrong))

    def test_proxy_on_the_wrong_magicdns_host_is_missing(self) -> None:
        status = valid_serve_status()
        owned = status["Web"].pop(f"{MAGICDNS_HOST}:7000")
        status["Web"]["other-node.example-tail.ts.net:7000"] = owned

        self.assertIn("serve.root_proxy.missing:7000", self.verify(status))

    def test_extra_handler_on_owned_listener_fails_closed(self) -> None:
        status = valid_serve_status()
        status["Web"][f"{MAGICDNS_HOST}:7000"]["Handlers"]["/debug"] = {
            "Proxy": "http://127.0.0.1:9000"
        }

        self.assertIn("serve.extra_handler.forbidden:7000", self.verify(status))

    def test_non_https_listener_fails_closed(self) -> None:
        status = valid_serve_status()
        status["TCP"]["7000"] = {"HTTP": True}

        self.assertIn("serve.https_listener.invalid:7000", self.verify(status))

    def test_funnel_enabled_in_serve_or_captured_funnel_config_fails(self) -> None:
        status = valid_serve_status()
        status["AllowFunnel"][f"{MAGICDNS_HOST}:8261"] = True
        errors = self.verify(status)
        captured_errors = self.verify(
            funnel_config={
                "Foreground": {
                    "session": {
                        "AllowFunnel": {f"{MAGICDNS_HOST}:7000": True}
                    }
                }
            }
        )

        self.assertIn("serve.allow_funnel.enabled:8261", errors)
        self.assertIn("funnel.allow_funnel.enabled:7000", captured_errors)

    def test_wildcard_publication_fails_closed(self) -> None:
        compose = valid_compose()
        compose["services"]["bolt-hub"]["ports"][0]["host_ip"] = "0.0.0.0"

        self.assertIn(
            "compose.loopback_publication.invalid:bolt-hub", self.verify(compose=compose)
        )

    def test_ipv6_loopback_publication_fails_closed(self) -> None:
        compose = valid_compose()
        compose["services"]["identityserver"]["ports"][0]["host_ip"] = "::1"

        self.assertIn(
            "compose.loopback_publication.invalid:identityserver",
            self.verify(compose=compose),
        )

    def test_direct_container_8443_publication_fails_closed(self) -> None:
        compose = valid_compose()
        compose["services"]["bolt-hub"]["ports"][0]["target"] = 8443

        self.assertIn(
            "compose.loopback_publication.invalid:bolt-hub", self.verify(compose=compose)
        )

    def test_additional_host_publication_fails_closed(self) -> None:
        compose = valid_compose()
        compose["services"]["bolt-hub"]["ports"].append(
            {
                "host_ip": "127.0.0.1",
                "mode": "ingress",
                "protocol": "tcp",
                "published": "8443",
                "target": 8443,
            }
        )

        self.assertIn(
            "compose.loopback_publication.invalid:bolt-hub", self.verify(compose=compose)
        )

    def test_certificate_or_ca_secret_reference_fails_closed(self) -> None:
        compose = valid_compose()
        compose["services"]["bolt-hub"]["secrets"] = [
            {"source": "server-material", "target": "/run/secrets/server-material"}
        ]
        compose["secrets"] = {
            "server-material": {"file": "/opt/xframework/tls/bolt-hub-ca.crt"}
        }

        self.assertIn(
            "compose.tls_secret.forbidden:bolt-hub", self.verify(compose=compose)
        )

    def test_application_signing_key_secrets_are_not_treated_as_tls(self) -> None:
        compose = valid_compose()
        compose["services"]["bolt-hub"]["secrets"] = [
            {
                "source": "identity-user-jwt-public-key",
                "target": "/run/secrets/identity-user-jwt-public-key.pem",
            }
        ]
        compose["services"]["identityserver"]["secrets"] = [
            {
                "source": "identity-user-jwt-public-key",
                "target": "/run/secrets/identity-user-jwt-public-key.pem",
            },
            {
                "source": "identity-user-jwt-private-key",
                "target": "/run/secrets/identity-user-jwt-private-key.pem",
            },
        ]
        compose["secrets"] = {
            "identity-user-jwt-public-key": {"file": "/keys/user-jwt-public.pem"},
            "identity-user-jwt-private-key": {"file": "/keys/user-jwt-private.pem"},
        }

        self.assertEqual([], self.verify(compose=compose))

    def test_application_signing_secret_with_wrong_target_fails_closed(self) -> None:
        compose = valid_compose()
        compose["services"]["identityserver"]["secrets"] = [
            {
                "source": "identity-user-jwt-private-key",
                "target": "/run/secrets/identityserver-tls-private-key.pem",
            }
        ]

        self.assertIn(
            "compose.tls_secret.forbidden:identityserver", self.verify(compose=compose)
        )

    def test_kestrel_https_environment_fails_closed(self) -> None:
        compose = valid_compose()
        compose["services"]["identityserver"]["environment"][
            "Kestrel__Endpoints__Https__Certificate__Path"
        ] = "/run/secrets/identityserver.pem"

        self.assertIn(
            "compose.kestrel_https_env.forbidden:identityserver",
            self.verify(compose=compose),
        )

    def test_tls_or_ca_volume_mount_fails_closed(self) -> None:
        compose = valid_compose()
        compose["services"]["bolt-hub"]["volumes"] = [
            {
                "source": "/opt/xframework/tls/bolt-hub-ca.crt",
                "target": "/usr/local/share/ca-certificates/bolt-hub-ca.crt",
                "type": "bind",
            }
        ]

        self.assertIn(
            "compose.tls_volume.forbidden:bolt-hub", self.verify(compose=compose)
        )

    def test_portal_tls_material_and_kestrel_https_fail_closed(self) -> None:
        mutations = {
            "compose.tls_secret.forbidden:portal": lambda compose: compose[
                "services"
            ]["portal"].update(
                {
                    "secrets": [
                        {
                            "source": "portal-ca",
                            "target": "/run/secrets/portal-ca.crt",
                        }
                    ]
                }
            ),
            "compose.tls_volume.forbidden:portal": lambda compose: compose[
                "services"
            ]["portal"].update(
                {
                    "volumes": [
                        {
                            "source": "/opt/xframework/tls/portal-ca.crt",
                            "target": "/usr/local/share/ca-certificates/portal-ca.crt",
                            "type": "bind",
                        }
                    ]
                }
            ),
            "compose.kestrel_https_env.forbidden:portal": lambda compose: compose[
                "services"
            ]["portal"]["environment"].update({"ASPNETCORE_URLS": "https://+:8443"}),
        }

        for expected_error, mutate in mutations.items():
            with self.subTest(expected_error=expected_error):
                compose = valid_compose()
                mutate(compose)
                self.assertIn(expected_error, self.verify(compose=compose))

    def test_identity_signing_key_directory_volume_is_allowed(self) -> None:
        self.assertEqual([], self.verify())

    def test_https_in_aspnetcore_urls_fails_closed(self) -> None:
        compose = valid_compose()
        compose["services"]["bolt-hub"]["environment"][
            "ASPNETCORE_URLS"
        ] = "https://+:8443"

        self.assertIn(
            "compose.kestrel_https_env.forbidden:bolt-hub",
            self.verify(compose=compose),
        )

    def test_unrelated_serve_mutation_is_detected(self) -> None:
        before = previous_serve_status()
        after = valid_serve_status()
        after["Web"][f"{MAGICDNS_HOST}:9443"]["Handlers"]["/unrelated"][
            "Proxy"
        ] = "http://127.0.0.1:9001"

        self.assertIn(
            "serve.preservation.unrelated_changed",
            self.verify(after, previous_serve_status=before),
        )

    def test_evidence_is_versioned_compact_and_does_not_leak_values(self) -> None:
        serve_status = valid_serve_status()
        compose = valid_compose()
        evidence = MODULE.build_evidence(
            errors=[],
            serve_status=serve_status,
            compose_config=compose,
            magicdns_host=MAGICDNS_HOST,
            funnel_config=None,
            previous_serve_status=None,
        )
        encoded = json.dumps(evidence, separators=(",", ":"), sort_keys=True)

        self.assertEqual(MODULE.EVIDENCE_SCHEMA, evidence["schema"])
        self.assertEqual("passed", evidence["status"])
        self.assertNotIn(MAGICDNS_HOST, encoded)
        self.assertNotIn("fixture-secret-not-for-evidence", encoded)
        self.assertLess(len(encoded), 700)

    def test_cli_emits_failed_evidence_and_nonzero_status(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            serve_path = root / "serve.json"
            compose_path = root / "compose.json"
            serve = valid_serve_status()
            serve["AllowFunnel"][f"{MAGICDNS_HOST}:7000"] = True
            serve_path.write_text(json.dumps(serve), encoding="utf-8")
            compose_path.write_text(json.dumps(valid_compose()), encoding="utf-8")

            result = subprocess.run(
                [
                    sys.executable,
                    "-B",
                    str(SCRIPT),
                    "--serve-status-json",
                    str(serve_path),
                    "--compose-json",
                    str(compose_path),
                    "--magicdns-host",
                    MAGICDNS_HOST,
                ],
                check=False,
                capture_output=True,
                text=True,
            )

        evidence = json.loads(result.stdout)
        self.assertEqual(1, result.returncode)
        self.assertEqual("failed", evidence["status"])
        self.assertIn("serve.allow_funnel.enabled:7000", evidence["errors"])

    def test_configurator_only_reads_funnel_and_uses_serve_for_mutation(self) -> None:
        script = CONFIGURATOR.read_text(encoding="ascii")
        funnel_invocations = re.findall(
            r'"\$tailscale_bin"\s+funnel\s+([^\n]+)', script
        )

        self.assertEqual(['status --json >"$funnel_status_json"'], funnel_invocations)
        self.assertIn(
            'serve --bg --yes --https=7000 http://127.0.0.1:7000', script
        )
        self.assertIn(
            'serve --bg --yes --https=8261 http://127.0.0.1:8261', script
        )
        self.assertIn('minimum = (1, 98, 0)', script)
        self.assertNotIn('== 1.98.*', script)

    def test_configurator_version_gate_uses_semantic_minimum(self) -> None:
        script = CONFIGURATOR.read_text(encoding="ascii")
        marker = 'if ! "$python_bin" -B - "$version_json" <<\'PY\'\n'
        start = script.index(marker) + len(marker)
        version_check = script[start : script.index("\nPY\nthen", start)]

        for version, expected_status in (
            ("1.97.9", 1),
            ("1.98.0-beta.1", 1),
            ("1.98.0", 0),
            ("1.98.4", 0),
            ("1.99.0", 0),
            ("1.100.0", 0),
            ("2.0.0", 0),
        ):
            with self.subTest(version=version):
                with tempfile.TemporaryDirectory() as directory:
                    version_path = Path(directory) / "version.json"
                    version_path.write_text(
                        json.dumps({"majorMinorPatch": version}), encoding="utf-8"
                    )
                    result = subprocess.run(
                        [sys.executable, "-B", "-", str(version_path)],
                        input=version_check,
                        check=False,
                        capture_output=True,
                        text=True,
                    )
                self.assertEqual(expected_status, result.returncode)


if __name__ == "__main__":
    unittest.main()
