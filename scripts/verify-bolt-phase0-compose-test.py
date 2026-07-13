#!/usr/bin/env python3
from __future__ import annotations

import importlib.util
import json
import os
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest import mock


SCRIPT = Path(__file__).with_name("verify-bolt-phase0-compose.py")
SPEC = importlib.util.spec_from_file_location("phase0_compose", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


SHA = "a" * 40


class Phase0ComposeTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name).resolve()
        self.tls = self.root / "tls"
        self.tls.mkdir()
        self.ca = self.tls / "ca.crt"
        self.fullchain = self.tls / "fullchain.pem"
        self.private_key = self.tls / "bolt-hub.key"
        self.identity_tls = self.root / "identityserver-tls"
        self.identity_tls.mkdir()
        self.identity_ca = self.identity_tls / "ca.crt"
        self.identity_fullchain = self.identity_tls / "fullchain.pem"
        self.identity_private_key = self.identity_tls / "identityserver.key"
        for path in (
            self.ca,
            self.fullchain,
            self.private_key,
            self.identity_ca,
            self.identity_fullchain,
            self.identity_private_key,
        ):
            path.write_text(path.name, encoding="ascii")

    def args(self, authorize: bool = False) -> SimpleNamespace:
        return SimpleNamespace(
            expected_internal_url=MODULE.SECURE_URL,
            expected_ca_path=str(self.ca),
            expected_fullchain_path=str(self.fullchain),
            expected_private_key_path=str(self.private_key),
            expected_published_port=7443,
            expected_public_hostname="bolt.example.test",
            expected_identityserver_ca_path=str(self.identity_ca),
            expected_identityserver_fullchain_path=str(self.identity_fullchain),
            expected_identityserver_private_key_path=str(self.identity_private_key),
            expected_identityserver_published_port=8444,
            expected_identityserver_public_hostname="identity.example.test",
            expected_identityserver_token_path=MODULE.IDENTITY_TOKEN_PATH,
            env_file=None,
            authorized_service=[],
            expected_image_pins={},
            expected_source_commit=None,
            provenance_bindings={},
            provenance_source_commit=None,
            provenance_verified=False,
            authorize_deployment=authorize,
            publication_topology_attestation=MODULE.DIRECT_PUBLICATION_ATTESTATION,
            publication_topology_attested_by="phase0-operator",
            publication_topology_attested_by_id="1234567",
            publication_topology_triggering_actor="phase0-operator",
            publication_topology_run_id="123456789",
            publication_topology_run_attempt=1,
            publication_host_context=(
                MODULE.DEPLOYMENT_HOST_CONTEXT if authorize else None
            ),
        )

    def manifest(self) -> dict:
        common_environment = {
            "BoltConfiguration__RequireSecureTransport": "true",
            "BoltConfiguration__ServerUrls__0": MODULE.SECURE_URL,
        }
        services = {
            name: {
                "image": f"registry.example/xframework/{name}:{SHA}",
                "environment": {
                    **common_environment,
                    **{
                        f"ServiceIdentity__DefaultScopes__{index}": scope
                        for index, scope in enumerate(MODULE.SERVICE_IDENTITY_RUNTIME_DEFAULT_SCOPES[name])
                    },
                },
                "secrets": [
                    {
                        "source": MODULE.CA_SECRET,
                        "target": "/usr/local/share/ca-certificates/xframework-bolt-hub-ca.crt",
                        "mode": "0444",
                    }
                ],
            }
            for name in MODULE.CLIENT_SERVICES
        }
        services["bolt-hub"] = {
            "environment": {
                "ServiceIdentity__DefaultScopes__0": "bolt.service",
            }
        }
        identity_client_environment = {
            key: value
            for index, (client_id, scopes) in enumerate(MODULE.SERVICE_IDENTITY_CLIENT_SCOPE_MATRIX.items())
            for key, value in (
                (f"ServiceIdentity__Clients__{index}__ClientId", client_id),
                (f"ServiceIdentity__Clients__{index}__AllowedScopes", ",".join(scopes)),
            )
        }
        services["bolt-phase0-synthetics"] = {
            "image": f"registry.example/xframework/bolt-phase0-synthetics:{SHA}",
            "profiles": ["phase0-verification"],
            "restart": "no",
            "environment": {"BOLT_SYNTHETIC_TARGET": MODULE.SECURE_URL},
            "secrets": [
                {
                    "source": MODULE.CA_SECRET,
                    "target": "/usr/local/share/ca-certificates/xframework-bolt-hub-ca.crt",
                    "mode": "0444",
                }
            ],
        }
        services["identityserver"].update(
            {
                "environment": {
                    **common_environment,
                    "ServiceIdentity__DefaultScopes__0": "bolt.service",
                    "ASPNETCORE_URLS": "http://127.0.0.1:8080;https://+:8443",
                    **MODULE.IDENTITY_EXPECTED_ENDPOINT_ENV,
                    **MODULE.IDENTITY_ISSUER_ENV,
                    **identity_client_environment,
                },
                "ports": [{"target": 8443, "published": "8444", "protocol": "tcp"}],
                "secrets": [
                    {
                        "source": MODULE.CA_SECRET,
                        "target": "/usr/local/share/ca-certificates/xframework-bolt-hub-ca.crt",
                        "mode": "0444",
                    },
                    {
                        "source": MODULE.IDENTITY_CA_SECRET,
                        "target": "/run/secrets/identityserver-ca.crt",
                        "mode": "0444",
                    },
                    {
                        "source": MODULE.IDENTITY_FULLCHAIN_SECRET,
                        "target": "/run/secrets/identityserver-tls-fullchain.pem",
                        "mode": "0444",
                    },
                    {
                        "source": MODULE.IDENTITY_PRIVATE_KEY_SECRET,
                        "target": "/run/secrets/identityserver-tls-private-key.pem",
                        "mode": "0400",
                    },
                ],
                "healthcheck": {
                    "test": [
                        "CMD-SHELL",
                        "curl -fsS http://127.0.0.1:8080/health/live >/dev/null",
                    ]
                },
            }
        )
        hub_environment = {
            "ASPNETCORE_URLS": "http://127.0.0.1:8080;https://+:8443",
            **MODULE.EXPECTED_ENDPOINT_ENV,
            "BoltConfiguration__RequireSecureTransport": "true",
            "BoltConfiguration__MediaEnabled": "false",
            "BoltConfiguration__RegistrationIdentityBindingMode": "Enforce",
            **MODULE.PHASE0_QUOTAS,
        }
        services["bolt-hub"].update({
            "image": f"registry.example/xframework/bolt-hub:{SHA}",
            "deploy": {"replicas": 1},
            "environment": {
                **hub_environment,
                "ServiceIdentity__DefaultScopes__0": "bolt.service",
            },
            "ports": [{"target": 8443, "published": "7443", "protocol": "tcp"}],
            "secrets": [
                {
                    "source": MODULE.CA_SECRET,
                    "target": "/usr/local/share/ca-certificates/xframework-bolt-hub-ca.crt",
                    "mode": "0444",
                },
                {
                    "source": MODULE.FULLCHAIN_SECRET,
                    "target": "/run/secrets/bolt-hub-tls-fullchain.pem",
                    "mode": "0444",
                },
                {
                    "source": MODULE.PRIVATE_KEY_SECRET,
                    "target": "/run/secrets/bolt-hub-tls-private-key.pem",
                    "mode": "0400",
                },
            ],
            "healthcheck": {
                "test": [
                    "CMD-SHELL",
                    "curl -fsS http://127.0.0.1:8080/health/live && "
                    "curl -fsS http://127.0.0.1:8080/health/ready",
                ]
            },
        })
        return {
            "services": services,
            "secrets": {
                MODULE.CA_SECRET: {"file": str(self.ca)},
                MODULE.FULLCHAIN_SECRET: {"file": str(self.fullchain)},
                MODULE.PRIVATE_KEY_SECRET: {"file": str(self.private_key)},
                MODULE.IDENTITY_CA_SECRET: {"file": str(self.identity_ca)},
                MODULE.IDENTITY_FULLCHAIN_SECRET: {"file": str(self.identity_fullchain)},
                MODULE.IDENTITY_PRIVATE_KEY_SECRET: {"file": str(self.identity_private_key)},
            },
        }

    def errors(self, manifest: dict, args: SimpleNamespace | None = None) -> list[str]:
        gate = MODULE.Gate()
        MODULE.verify(manifest, args or self.args(), gate)
        return gate.errors

    def test_valid_manifest_passes_and_records_every_quota(self) -> None:
        gate = MODULE.Gate()
        MODULE.verify(self.manifest(), self.args(), gate)
        self.assertEqual([], gate.errors)
        quotas = gate.checks["hub-exact-phase0-quotas"]["detail"]
        self.assertEqual(set(MODULE.PHASE0_QUOTAS), set(quotas["configuration"]))
        self.assertEqual(MODULE.PHASE0_EFFECTIVE_QUOTAS, quotas["effective_server_options"])
        self.assertEqual(
            {
                client_id: tuple(sorted(scopes))
                for client_id, scopes in MODULE.SERVICE_IDENTITY_CLIENT_SCOPE_MATRIX.items()
            },
            gate.checks["identityserver-exact-client-scope-matrix"]["detail"]["observed"],
        )

    def test_identityserver_client_scope_matrix_rejects_cross_module_privilege(self) -> None:
        cases = (
            (3, "bolt.service,identity.admin"),
            (10, "bolt.service,communications.admin"),
            (1, "bolt.service,datacontext.query,datacontext.mutate,identity.admin,wallets.admin"),
        )
        for client_index, scopes in cases:
            with self.subTest(client_index=client_index, scopes=scopes):
                manifest = self.manifest()
                manifest["services"]["identityserver"]["environment"][
                    f"ServiceIdentity__Clients__{client_index}__AllowedScopes"
                ] = scopes
                self.assertTrue(any(
                    error.startswith("identityserver-exact-client-scope-matrix:")
                    for error in self.errors(manifest)
                ))

    def test_identityserver_client_scope_matrix_rejects_indexed_escalation(self) -> None:
        for keep_scalar, expected_error in (
            (True, "mixes scalar and indexed AllowedScopes"),
            (False, "'identity.admin'"),
        ):
            with self.subTest(keep_scalar=keep_scalar):
                manifest = self.manifest()
                identity_environment = manifest["services"]["identityserver"]["environment"]
                if not keep_scalar:
                    del identity_environment["ServiceIdentity__Clients__3__AllowedScopes"]
                identity_environment[
                    "ServiceIdentity__Clients__3__AllowedScopes__1"
                ] = "identity.admin"

                self.assertTrue(any(
                    expected_error in error
                    for error in self.errors(manifest)
                ))

    def test_identityserver_client_scope_matrix_accepts_exact_indexed_shape(self) -> None:
        manifest = self.manifest()
        identity_environment = manifest["services"]["identityserver"]["environment"]
        for index, scopes in enumerate(MODULE.SERVICE_IDENTITY_CLIENT_SCOPE_MATRIX.values()):
            del identity_environment[f"ServiceIdentity__Clients__{index}__AllowedScopes"]
            for scope_index, scope in enumerate(scopes):
                identity_environment[
                    f"ServiceIdentity__Clients__{index}__AllowedScopes__{scope_index}"
                ] = scope

        self.assertEqual([], self.errors(manifest))

    def test_identityserver_client_scope_matrix_rejects_mixed_forms_even_when_equivalent(self) -> None:
        manifest = self.manifest()
        manifest["services"]["identityserver"]["environment"][
            "ServiceIdentity__Clients__3__AllowedScopes__0"
        ] = "bolt.service"

        errors = self.errors(manifest)

        self.assertTrue(any(
            "mixes scalar and indexed AllowedScopes" in error
            for error in errors
        ))

    def test_identityserver_client_scope_matrix_rejects_unapproved_scope_descendants(self) -> None:
        cases = (
            "ServiceIdentity__Clients__3__AllowedScopes__admin",
            "ServiceIdentity__Clients__3__AllowedScopes__0__Value",
            "ServiceIdentity__Clients__3__ClientId__0",
        )
        for key in cases:
            with self.subTest(key=key):
                manifest = self.manifest()
                manifest["services"]["identityserver"]["environment"][key] = "identity.admin"
                self.assertTrue(any(
                    "unapproved" in error
                    for error in self.errors(manifest)
                ))

    def test_identityserver_client_scope_matrix_rejects_case_variant_configuration_path(self) -> None:
        manifest = self.manifest()
        manifest["services"]["identityserver"]["environment"][
            "serviceidentity__clients__3__allowedscopes"
        ] = "bolt.service"

        self.assertTrue(any(
            "multiple environment keys" in error
            for error in self.errors(manifest)
        ))

    def test_identityserver_client_scope_matrix_rejects_duplicate_indexed_scope_case_variants(self) -> None:
        manifest = self.manifest()
        identity_environment = manifest["services"]["identityserver"]["environment"]
        del identity_environment["ServiceIdentity__Clients__3__AllowedScopes"]
        identity_environment["ServiceIdentity__Clients__3__AllowedScopes__0"] = "bolt.service"
        identity_environment["ServiceIdentity__Clients__3__AllowedScopes__1"] = "BOLT.SERVICE"

        errors = self.errors(manifest)

        self.assertTrue(any(
            "contains duplicate scopes" in error
            for error in errors
        ))

    def test_identityserver_client_scope_matrix_rejects_duplicate_client_id_case_variant(self) -> None:
        manifest = self.manifest()
        identity_environment = manifest["services"]["identityserver"]["environment"]
        identity_environment["ServiceIdentity__Clients__12__ClientId"] = "xframework.communications"
        identity_environment["ServiceIdentity__Clients__12__AllowedScopes"] = "bolt.service"

        self.assertTrue(any(
            "duplicates another ClientId by case" in error
            for error in self.errors(manifest)
        ))

    def test_runtime_default_scopes_cannot_fall_back_to_unrestricted_defaults(self) -> None:
        manifest = self.manifest()
        del manifest["services"]["communications"]["environment"]["ServiceIdentity__DefaultScopes__0"]
        manifest["services"]["portal"]["environment"]["ServiceIdentity__DefaultScopes__2"] = "wallets.admin"

        errors = self.errors(manifest)

        self.assertTrue(any(
            error.startswith("service-identity-exact-runtime-default-scopes:")
            for error in errors
        ))

    def test_every_phase0_quota_override_fails_closed(self) -> None:
        for key in MODULE.PHASE0_QUOTAS:
            with self.subTest(key=key):
                manifest = self.manifest()
                manifest["services"]["bolt-hub"]["environment"][key] = "999999999"
                self.assertTrue(any(error.startswith("hub-exact-phase0-quotas:") for error in self.errors(manifest)))

    def test_plaintext_client_media_and_replica_drift_fail_closed(self) -> None:
        manifest = self.manifest()
        manifest["services"]["portal"]["environment"]["BoltConfiguration__ServerUrls__0"] = "ws://bolt-hub:8080/bolt/ws"
        manifest["services"]["bolt-hub"]["environment"]["BoltConfiguration__MediaEnabled"] = "true"
        manifest["services"]["bolt-hub"]["scale"] = 2
        errors = self.errors(manifest)
        self.assertTrue(any(error.startswith("all-clients-use-wss:") for error in errors))
        self.assertTrue(any(error.startswith("hub-media-disabled:") for error in errors))
        self.assertTrue(any(error.startswith("hub-single-replica:") for error in errors))

    def test_synthetics_service_must_remain_inactive_and_use_wss(self) -> None:
        manifest = self.manifest()
        synthetics = manifest["services"]["bolt-phase0-synthetics"]
        synthetics["profiles"] = []
        synthetics["environment"]["BOLT_SYNTHETIC_TARGET"] = "ws://bolt-hub:8080/bolt/ws"
        self.assertTrue(
            any(
                error.startswith("synthetics-profile-is-inactive-and-secure:")
                for error in self.errors(manifest)
            )
        )

    def test_extra_kestrel_endpoint_and_url_override_fail_closed(self) -> None:
        manifest = self.manifest()
        hub_env = manifest["services"]["bolt-hub"]["environment"]
        hub_env["Kestrel__Endpoints__Backdoor__Url"] = "http://0.0.0.0:9000"
        hub_env["ASPNETCORE_HTTP_PORTS"] = "9001"
        self.assertTrue(
            any(error.startswith("hub-effective-kestrel-endpoints:") for error in self.errors(manifest))
        )

    def test_case_colon_and_entrypoint_configuration_overrides_fail_closed(self) -> None:
        manifest = self.manifest()
        hub = manifest["services"]["bolt-hub"]
        hub["environment"]["boltconfiguration:maxframebytes"] = "999999999"
        hub["entrypoint"] = ["dotnet", "Bolt.Hub.dll", "--urls", "http://0.0.0.0:9000"]
        errors = self.errors(manifest)
        self.assertTrue(any(error.startswith("canonical-protected-environment-keys:") for error in errors))
        self.assertTrue(any(error.startswith("hub-effective-kestrel-endpoints:") for error in errors))

    def test_parent_directory_bind_exposing_private_key_fails_closed(self) -> None:
        manifest = self.manifest()
        manifest["services"]["portal"]["volumes"] = [
            {"type": "bind", "source": str(self.tls), "target": "/mnt/tls", "read_only": True}
        ]
        errors = self.errors(manifest)
        self.assertTrue(any(error.startswith("resolved-private-key-mounted-only-by-hub:") for error in errors))

    def test_symlink_alias_exposing_private_key_fails_closed(self) -> None:
        alias = self.root / "innocent.pem"
        try:
            alias.symlink_to(self.private_key)
        except OSError:
            os.link(self.private_key, alias)
        manifest = self.manifest()
        manifest["secrets"]["innocent-name"] = {"file": str(alias)}
        manifest["services"]["portal"]["secrets"].append(
            {"source": "innocent-name", "target": "/tmp/innocent.pem", "mode": "0400"}
        )
        errors = self.errors(manifest)
        self.assertTrue(any(error.startswith("resolved-private-key-mounted-only-by-hub:") for error in errors))

    def test_identityserver_private_key_alias_fails_closed(self) -> None:
        manifest = self.manifest()
        alias = self.root / "identity-innocent.pem"
        try:
            alias.symlink_to(self.identity_private_key)
        except OSError:
            os.link(self.identity_private_key, alias)
        manifest["secrets"]["identity-alias"] = {"file": str(alias)}
        manifest["services"]["portal"]["secrets"].append(
            {"source": "identity-alias", "target": "/tmp/innocent.pem", "mode": "0400"}
        )
        errors = self.errors(manifest)
        self.assertTrue(
            any(error.startswith("resolved-private-key-mounted-only-by-identityserver:") for error in errors)
        )

    def test_shared_hub_and_identityserver_key_inode_fails_closed(self) -> None:
        manifest = self.manifest()
        manifest["secrets"][MODULE.IDENTITY_PRIVATE_KEY_SECRET]["file"] = str(self.private_key)
        args = self.args()
        args.expected_identityserver_private_key_path = str(self.private_key)
        errors = self.errors(manifest, args)
        self.assertTrue(
            any(error.startswith("dual-tls-secret-files-resolved-and-distinct:") for error in errors)
        )
        self.assertTrue(any(error.startswith("resolved-private-key-mounted-only-by-hub:") for error in errors))

    def test_identityserver_parent_directory_mount_fails_closed(self) -> None:
        manifest = self.manifest()
        manifest["services"]["communications"]["volumes"] = [
            {"type": "bind", "source": str(self.identity_tls), "target": "/mnt/tls", "read_only": True}
        ]
        errors = self.errors(manifest)
        self.assertTrue(
            any(error.startswith("resolved-private-key-mounted-only-by-identityserver:") for error in errors)
        )

    def test_identityserver_plaintext_publication_fails_closed(self) -> None:
        manifest = self.manifest()
        manifest["services"]["identityserver"]["ports"] = [
            {"target": 8080, "published": "8444", "protocol": "tcp"}
        ]
        self.assertTrue(
            any(error.startswith("identityserver-only-tls-publication:") for error in self.errors(manifest))
        )

    def test_identityserver_transport_token_issuer_must_be_enabled_for_120_seconds(self) -> None:
        for key in MODULE.IDENTITY_ISSUER_ENV:
            with self.subTest(key=key):
                manifest = self.manifest()
                del manifest["services"]["identityserver"]["environment"][key]
                self.assertTrue(
                    any(
                        error.startswith("identityserver-phase0-transport-token-issuer:")
                        for error in self.errors(manifest)
                    )
                )

    @unittest.skipIf(os.name == "nt", "protected deployment env paths use canonical POSIX paths")
    def test_protected_env_binds_identityserver_hook_configuration(self) -> None:
        env_file = self.root / "deployment.env"
        env_file.write_text(
            "\n".join(
                (
                    f"IDENTITYSERVER_TLS_CA_PATH={self.identity_ca}",
                    f"IDENTITYSERVER_TLS_FULLCHAIN_PATH={self.identity_fullchain}",
                    f"IDENTITYSERVER_TLS_PRIVATE_KEY_PATH={self.identity_private_key}",
                    "IDENTITYSERVER_PUBLIC_HOSTNAME=identity.example.test",
                    "IDENTITYSERVER_PUBLIC_HTTPS_PORT=8444",
                    f"IDENTITYSERVER_BOLT_TRANSPORT_TOKEN_PATH={MODULE.IDENTITY_TOKEN_PATH}",
                )
            )
            + "\n",
            encoding="utf-8",
        )
        args = self.args()
        args.env_file = str(env_file)
        self.assertEqual([], self.errors(self.manifest(), args))

        args.expected_identityserver_public_hostname = "other.example.test"
        self.assertTrue(
            any(
                error.startswith("identityserver-public-token-refresh-configuration:")
                for error in self.errors(self.manifest(), args)
            )
        )

    @mock.patch.object(
        MODULE,
        "inspect_publication_topology",
        return_value={
            "resolved_addresses": ["100.64.0.10"],
            "host_interface_addresses": ["100.64.0.10"],
            "matched_addresses": ["100.64.0.10"],
        },
    )
    def test_digest_pin_requires_matching_verified_provenance(self, _: mock.Mock) -> None:
        manifest = self.manifest()
        pin = "registry.example/xframework/bolt-hub@sha256:" + "d" * 64
        manifest["services"]["bolt-hub"]["image"] = pin
        args = self.args(authorize=True)
        args.authorized_service = ["bolt-hub"]
        args.expected_image_pins = {"bolt-hub": pin}
        args.expected_source_commit = SHA
        args.provenance_bindings = {
            "bolt-hub": {"pin": pin, "source_commit": SHA, "signature_verified": True}
        }
        args.provenance_source_commit = SHA
        args.provenance_verified = True
        self.assertEqual([], self.errors(manifest, args))

        args.publication_topology_attestation = ""
        self.assertTrue(
            any(
                error.startswith("operator-attested-direct-publication-topology:")
                for error in self.errors(manifest, args)
            )
        )
        args.publication_topology_attestation = MODULE.DIRECT_PUBLICATION_ATTESTATION

        args.publication_topology_run_attempt = 2
        self.assertTrue(
            any(
                error.startswith("operator-attested-direct-publication-topology:")
                for error in self.errors(manifest, args)
            )
        )
        args.publication_topology_run_attempt = 1

        args.publication_topology_triggering_actor = "rerun-operator"
        self.assertTrue(
            any(
                error.startswith("operator-attested-direct-publication-topology:")
                for error in self.errors(manifest, args)
            )
        )
        args.publication_topology_triggering_actor = args.publication_topology_attested_by

        manifest["services"]["bolt-hub"]["ports"][0]["host_ip"] = "127.0.0.1"
        self.assertTrue(
            any(
                error.startswith("direct-publication-host-interface:")
                for error in self.errors(manifest, args)
            )
        )
        del manifest["services"]["bolt-hub"]["ports"][0]["host_ip"]

        with mock.patch.object(
            MODULE,
            "inspect_publication_topology",
            return_value={
                "resolved_addresses": ["100.64.0.10"],
                "host_interface_addresses": ["100.64.0.11"],
                "matched_addresses": [],
            },
        ):
            self.assertTrue(
                any(
                    error.startswith("direct-publication-host-interface:")
                    for error in self.errors(manifest, args)
                )
            )

        with mock.patch.object(
            MODULE,
            "inspect_publication_topology",
            return_value={
                "resolved_addresses": ["100.64.0.10", "100.64.0.12"],
                "host_interface_addresses": ["100.64.0.10"],
                "matched_addresses": ["100.64.0.10"],
            },
        ):
            self.assertTrue(
                any(
                    error.startswith("direct-publication-host-interface:")
                    for error in self.errors(manifest, args)
                )
            )

        args.provenance_bindings = {}
        args.provenance_verified = False
        self.assertTrue(
            any(
                error.startswith("digest-pinned-provenance-authorized-images:")
                for error in self.errors(manifest, args)
            )
        )

    def test_build_controller_validates_artifacts_without_topology_claims(self) -> None:
        manifest = self.manifest()
        pin = "registry.example/xframework/bolt-hub@sha256:" + "d" * 64
        manifest["services"]["bolt-hub"]["image"] = pin
        args = self.args(authorize=True)
        args.authorized_service = ["bolt-hub"]
        args.expected_image_pins = {"bolt-hub": pin}
        args.expected_source_commit = SHA
        args.provenance_bindings = {
            "bolt-hub": {"pin": pin, "source_commit": SHA, "signature_verified": True}
        }
        args.provenance_source_commit = SHA
        args.provenance_verified = True
        args.publication_host_context = MODULE.BUILD_CONTROLLER_CONTEXT

        gate = MODULE.Gate()
        with (
            mock.patch.object(
                MODULE,
                "resolve_publication_addresses",
                side_effect=AssertionError("controller must not resolve the deployment host"),
            ),
            mock.patch.object(
                MODULE,
                "inspect_publication_topology",
                side_effect=AssertionError("controller must not inspect its own interfaces"),
            ),
        ):
            MODULE.verify(manifest, args, gate)

        self.assertEqual([], gate.errors)
        self.assertTrue(gate.checks["publication-host-context"]["passed"])
        self.assertNotIn("direct-publication-host-interface", gate.checks)
        self.assertNotIn("operator-attested-direct-publication-topology", gate.checks)
        self.assertFalse(MODULE.deployment_is_authorized(args, gate))

        evidence = MODULE.build_preflight_evidence(args, gate, {})
        self.assertEqual("passed", evidence["status"])
        self.assertIs(False, evidence["deployment_authorized"])

        original_binding = args.provenance_bindings["bolt-hub"]
        invalid_cases = {
            "missing-pin": ({}, args.provenance_bindings, args.expected_source_commit),
            "source-mismatch": (
                args.expected_image_pins,
                args.provenance_bindings,
                "b" * 40,
            ),
            "signature-unverified": (
                args.expected_image_pins,
                {
                    "bolt-hub": {
                        **original_binding,
                        "signature_verified": False,
                    }
                },
                args.expected_source_commit,
            ),
        }
        for name, (pins, bindings, source_commit) in invalid_cases.items():
            with self.subTest(name=name):
                args.expected_image_pins = pins
                args.provenance_bindings = bindings
                args.expected_source_commit = source_commit
                self.assertTrue(
                    any(
                        error.startswith("digest-pinned-provenance-authorized-images:")
                        for error in self.errors(manifest, args)
                    )
                )

    def test_only_deployment_host_context_can_authorize_deployment(self) -> None:
        args = self.args(authorize=True)
        gate = MODULE.Gate()
        for name in MODULE.DEPLOYMENT_AUTHORIZATION_CHECKS:
            detail = (
                {"context": MODULE.DEPLOYMENT_HOST_CONTEXT}
                if name == "publication-host-context"
                else "verified"
            )
            gate.check(name, True, detail)

        self.assertTrue(MODULE.deployment_is_authorized(args, gate))
        evidence = MODULE.build_preflight_evidence(args, gate, {})
        self.assertEqual("passed", evidence["status"])
        self.assertIs(True, evidence["deployment_authorized"])
        gate.check(
            "publication-host-context",
            True,
            {"context": MODULE.BUILD_CONTROLLER_CONTEXT},
        )
        self.assertFalse(MODULE.deployment_is_authorized(args, gate))
        gate.check(
            "publication-host-context",
            True,
            {"context": MODULE.DEPLOYMENT_HOST_CONTEXT},
        )
        args.publication_host_context = MODULE.BUILD_CONTROLLER_CONTEXT
        self.assertFalse(MODULE.deployment_is_authorized(args, gate))
        args.publication_host_context = MODULE.DEPLOYMENT_HOST_CONTEXT
        del gate.checks["direct-publication-host-interface"]
        self.assertFalse(MODULE.deployment_is_authorized(args, gate))
        gate.check("direct-publication-host-interface", True, "verified")
        gate.check("failed", False, "failure")
        self.assertFalse(MODULE.deployment_is_authorized(args, gate))

    def test_deployment_authorization_requires_explicit_publication_context(self) -> None:
        manifest = self.manifest()
        args = self.args(authorize=True)
        args.publication_host_context = None
        gate = MODULE.Gate()

        with mock.patch.object(
            MODULE,
            "inspect_publication_topology",
            side_effect=AssertionError("missing context must not inspect interfaces"),
        ):
            MODULE.verify(manifest, args, gate)

        self.assertFalse(gate.checks["publication-host-context"]["passed"])
        self.assertTrue(
            any(error.startswith("publication-host-context:") for error in gate.errors)
        )
        evidence = MODULE.build_preflight_evidence(args, gate, {})
        self.assertEqual("failed", evidence["status"])
        self.assertIs(False, evidence["deployment_authorized"])

    def test_publication_topology_inspector_normalizes_dns_and_host_inventory(self) -> None:
        getaddrinfo = mock.patch.object(
            MODULE.socket,
            "getaddrinfo",
            return_value=[
                (2, 1, 6, "", ("100.64.0.10", 0)),
                (2, 1, 6, "", ("100.64.0.10", 0)),
            ],
        )
        is_file = mock.patch.object(MODULE.Path, "is_file", return_value=True)
        run = mock.patch.object(
            MODULE.subprocess,
            "run",
            return_value=SimpleNamespace(
                stdout=json.dumps(
                    [
                        {
                            "addr_info": [
                                {"family": "inet", "local": "100.64.0.10"},
                                {"family": "inet6", "local": "fe80::1"},
                            ]
                        }
                    ]
                )
            ),
        )
        with getaddrinfo, is_file, run as invoked:
            observed = MODULE.inspect_publication_topology("bolt.example.test")

        self.assertEqual(
            {
                "resolved_addresses": ["100.64.0.10"],
                "host_interface_addresses": ["100.64.0.10"],
                "matched_addresses": ["100.64.0.10"],
            },
            observed,
        )
        invoked.assert_called_once_with(
            [str(MODULE.Path("/usr/sbin/ip")), "-json", "address", "show", "up", "scope", "global"],
            check=True,
            capture_output=True,
            text=True,
            timeout=10,
        )

    def test_publication_topology_inspector_rejects_malformed_inventory(self) -> None:
        with (
            mock.patch.object(
                MODULE.socket,
                "getaddrinfo",
                return_value=[(2, 1, 6, "", ("100.64.0.10", 0))],
            ),
            mock.patch.object(MODULE.Path, "is_file", return_value=True),
            mock.patch.object(
                MODULE.subprocess,
                "run",
                return_value=SimpleNamespace(stdout="{}"),
            ),
            self.assertRaisesRegex(ValueError, "invalid-host-interface-inventory"),
        ):
            MODULE.inspect_publication_topology("bolt.example.test")

    def test_publication_topology_inspector_requires_trusted_ip_command(self) -> None:
        with (
            mock.patch.object(
                MODULE.socket,
                "getaddrinfo",
                return_value=[(2, 1, 6, "", ("100.64.0.10", 0))],
            ),
            mock.patch.object(MODULE.Path, "is_file", return_value=False),
            self.assertRaisesRegex(RuntimeError, "trusted-ip-command-unavailable"),
        ):
            MODULE.inspect_publication_topology("bolt.example.test")

    def test_publication_topology_inspector_reports_no_intersection(self) -> None:
        with (
            mock.patch.object(
                MODULE.socket,
                "getaddrinfo",
                return_value=[(2, 1, 6, "", ("100.64.0.10", 0))],
            ),
            mock.patch.object(MODULE.Path, "is_file", return_value=True),
            mock.patch.object(
                MODULE.subprocess,
                "run",
                return_value=SimpleNamespace(
                    stdout=json.dumps([{"addr_info": [{"local": "100.64.0.11"}]}])
                ),
            ),
        ):
            observed = MODULE.inspect_publication_topology("bolt.example.test")

        self.assertEqual([], observed["matched_addresses"])


if __name__ == "__main__":
    unittest.main()
