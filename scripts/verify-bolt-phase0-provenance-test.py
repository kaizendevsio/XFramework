#!/usr/bin/env python3
from __future__ import annotations

import base64
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).with_name("verify-bolt-phase0-provenance.py")
SPEC = importlib.util.spec_from_file_location("phase0_provenance", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


SHA = "a" * 40
REPOSITORY = "registry.example/xframework/bolt-hub"
PIN = REPOSITORY + "@sha256:" + "b" * 64
SOURCE_REPOSITORY = "https://github.com/kaizendevsio/XFramework"
BUILDER = "https://github.com/Attestations/GitHubHostedActions@v1"
WORKFLOW = SOURCE_REPOSITORY + "/.github/workflows/deploy-xeon-dev.yml@refs/heads/develop"
INVOCATION = SOURCE_REPOSITORY + "/actions/runs/123/attempts/1"
BASE_IMAGES = [
    "mcr.microsoft.com/dotnet/sdk@sha256:" + "c" * 64,
    "mcr.microsoft.com/dotnet/aspnet@sha256:" + "d" * 64,
]
BUILD_INPUT = {
    "context": ".",
    "dockerfile": "Dockerfile",
    "args": {"PROJECT_PATH": "src/Modules/XFramework.Bolt/Bolt.Hub/Bolt.Hub.csproj"},
    "target": None,
}


class Phase0ProvenanceTests(unittest.TestCase):
    @staticmethod
    def statement(dockerfile_digest: str) -> dict:
        dependencies = []
        for image in BASE_IMAGES:
            repository, digest = image.split("@sha256:", 1)
            dependencies.append({"uri": repository, "digest": {"sha256": digest}})
        return {
            "_type": MODULE.STATEMENT_TYPE,
            "subject": [{"name": REPOSITORY, "digest": {"sha256": "b" * 64}}],
            "predicateType": MODULE.PREDICATE_TYPE,
            "predicate": {
                "buildDefinition": {
                    "buildType": MODULE.BUILD_TYPE,
                    "externalParameters": {
                        "service": "bolt-hub",
                        "source_repository": SOURCE_REPOSITORY,
                        "source_commit": SHA,
                        "workflow_ref": WORKFLOW,
                        "dockerfile": {"path": "Dockerfile", "digest": dockerfile_digest},
                        "build": BUILD_INPUT,
                    },
                    "resolvedDependencies": dependencies,
                },
                "runDetails": {
                    "builder": {"id": BUILDER},
                    "metadata": {"invocationId": INVOCATION},
                },
            },
        }

    @staticmethod
    def envelope(statement: dict) -> dict:
        payload = json.dumps(statement, separators=(",", ":"), sort_keys=True).encode("utf-8")
        return {
            "payload": base64.b64encode(payload).decode("ascii"),
            "payloadType": MODULE.DSSE_PAYLOAD_TYPE,
            "signatures": [{"sig": base64.b64encode(b"test-signature").decode("ascii")}],
        }

    def verify(
        self,
        mutate_statement=None,
        mutate_bundle=None,
        mutate_build_inputs=None,
        trusted_builders=None,
        trusted_workflows=None,
    ):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        root = Path(temporary.name)
        dockerfile = root / "Dockerfile"
        dockerfile.write_text(
            f"FROM {BASE_IMAGES[0].replace('@', ':10.0@')} AS build\n"
            f"FROM {BASE_IMAGES[1].replace('@', ':10.0@')} AS runtime\n",
            encoding="ascii",
        )

        build_inputs = {
            "schema": MODULE.BUILD_INPUT_SCHEMA,
            "source_commit": SHA,
            "services": {"bolt-hub": json.loads(json.dumps(BUILD_INPUT))},
        }
        if mutate_build_inputs:
            mutate_build_inputs(build_inputs)
        build_inputs_path = root / "build-inputs.json"
        build_inputs_path.write_text(json.dumps(build_inputs), encoding="utf-8")

        statement = self.statement(MODULE.sha256_file(dockerfile))
        if mutate_statement:
            mutate_statement(statement)
        envelope = self.envelope(statement)
        verification_path = root / "verification.json"
        verification_path.write_text(json.dumps([envelope]), encoding="utf-8")

        bundle_envelope = json.loads(json.dumps(envelope))
        if mutate_bundle:
            mutate_bundle(bundle_envelope)
        bundle_path = root / "bundle.json"
        bundle_path.write_text(
            json.dumps(
                {
                    "mediaType": "application/vnd.dev.sigstore.bundle.v0.3+json",
                    "verificationMaterial": {},
                    "dsseEnvelope": bundle_envelope,
                }
            ),
            encoding="utf-8",
        )

        return MODULE.verify_inputs(
            {"bolt-hub": PIN},
            SHA,
            SOURCE_REPOSITORY,
            dockerfile,
            build_inputs_path,
            BASE_IMAGES,
            [BUILDER] if trusted_builders is None else trusted_builders,
            [WORKFLOW] if trusted_workflows is None else trusted_workflows,
            INVOCATION,
            [("bolt-hub", str(verification_path), str(bundle_path))],
        )

    def test_exact_verified_dsse_binding_passes_and_retains_safe_evidence_hashes(self) -> None:
        self.assertEqual("https://in-toto.io/Statement/v0.1", MODULE.STATEMENT_TYPE)
        bindings, digest = self.verify()
        binding = bindings["bolt-hub"]
        self.assertEqual(PIN, binding["pin"])
        self.assertEqual(digest, binding["dockerfile"]["digest"])
        self.assertEqual(sorted(BASE_IMAGES), binding["base_images"])
        self.assertRegex(binding["cosign_bundle_sha256"], r"^sha256:[0-9a-f]{64}$")
        self.assertEqual(MODULE.DSSE_PAYLOAD_TYPE, binding["verified_dsse_envelope"]["payloadType"])

    def test_same_identity_can_be_trusted_as_builder_and_workflow(self) -> None:
        bindings, _ = self.verify(
            mutate_statement=lambda statement: statement["predicate"]["runDetails"]["builder"].update(
                id=WORKFLOW
            ),
            trusted_builders=[WORKFLOW],
            trusted_workflows=[WORKFLOW],
        )
        self.assertEqual(WORKFLOW, bindings["bolt-hub"]["builder_id"])
        self.assertEqual(WORKFLOW, bindings["bolt-hub"]["workflow_ref"])

    def test_unsupported_statement_version_fails_closed(self) -> None:
        with self.assertRaisesRegex(ValueError, "pinned Cosign provenance contract"):
            self.verify(
                mutate_statement=lambda statement: statement.update(
                    _type="https://in-toto.io/Statement/v1"
                )
            )

    def test_duplicate_values_within_each_trust_namespace_fail_closed(self) -> None:
        cases = (
            ("builder", [BUILDER, BUILDER], [WORKFLOW]),
            ("workflow", [BUILDER], [WORKFLOW, WORKFLOW]),
        )
        for description, builders, workflows in cases:
            with self.subTest(description=description), self.assertRaisesRegex(ValueError, description):
                self.verify(trusted_builders=builders, trusted_workflows=workflows)

    def test_unsafe_values_within_each_trust_namespace_fail_closed(self) -> None:
        cases = (
            ("builder", ["https://github.com/unsafe builder"], [WORKFLOW]),
            ("workflow", [BUILDER], ["https://github.com/unsafe workflow"]),
        )
        for description, builders, workflows in cases:
            with self.subTest(description=description), self.assertRaisesRegex(ValueError, description):
                self.verify(trusted_builders=builders, trusted_workflows=workflows)

    def test_subject_mismatch_fails_closed(self) -> None:
        with self.assertRaisesRegex(ValueError, "subject"):
            self.verify(lambda statement: statement["subject"][0]["digest"].update(sha256="f" * 64))

    def test_bundle_payload_mismatch_fails_closed(self) -> None:
        with self.assertRaisesRegex(ValueError, "bundle does not match"):
            self.verify(mutate_bundle=lambda envelope: envelope.update(payload=base64.b64encode(b"{}").decode()))

    def test_wrong_commit_base_dockerfile_workflow_or_builder_fails_closed(self) -> None:
        mutations = (
            lambda statement: statement["predicate"]["buildDefinition"]["externalParameters"].update(
                source_commit="f" * 40
            ),
            lambda statement: statement["predicate"]["buildDefinition"]["resolvedDependencies"][0][
                "digest"
            ].update(sha256="f" * 64),
            lambda statement: statement["predicate"]["buildDefinition"]["externalParameters"][
                "dockerfile"
            ].update(digest="sha256:" + "f" * 64),
            lambda statement: statement["predicate"]["buildDefinition"]["externalParameters"].update(
                workflow_ref="https://attacker.invalid/workflow"
            ),
            lambda statement: statement["predicate"]["runDetails"]["builder"].update(
                id="https://attacker.invalid/builder"
            ),
        )
        for mutate in mutations:
            with self.subTest(mutate=mutate), self.assertRaises(ValueError):
                self.verify(mutate)

    def test_signed_service_or_build_args_mismatch_fails_closed(self) -> None:
        mutations = (
            lambda statement: statement["predicate"]["buildDefinition"]["externalParameters"].update(
                service="portal"
            ),
            lambda statement: statement["predicate"]["buildDefinition"]["externalParameters"]["build"][
                "args"
            ].update(PROJECT_PATH="src/attacker.csproj"),
        )
        for mutate in mutations:
            with self.subTest(mutate=mutate), self.assertRaises(ValueError):
                self.verify(mutate)

    def test_unapproved_build_arg_cannot_enter_retained_evidence(self) -> None:
        with self.assertRaisesRegex(ValueError, "nonsecret PROJECT_PATH"):
            self.verify(
                mutate_build_inputs=lambda document: document["services"]["bolt-hub"]["args"].update(
                    REGISTRY_PASSWORD="must-not-be-retained"
                )
            )

    def test_missing_or_duplicate_service_records_fail_closed(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            dockerfile = root / "Dockerfile"
            dockerfile.write_text(
                f"FROM {BASE_IMAGES[0].replace('@', ':10.0@')} AS build\n"
                f"FROM {BASE_IMAGES[1].replace('@', ':10.0@')} AS runtime\n",
                encoding="ascii",
            )
            build_inputs = root / "build-inputs.json"
            build_inputs.write_text(
                json.dumps(
                    {
                        "schema": MODULE.BUILD_INPUT_SCHEMA,
                        "source_commit": SHA,
                        "services": {"bolt-hub": BUILD_INPUT},
                    }
                ),
                encoding="utf-8",
            )
            with self.assertRaisesRegex(ValueError, "coverage mismatch"):
                MODULE.verify_inputs(
                    {"bolt-hub": PIN},
                    SHA,
                    SOURCE_REPOSITORY,
                    dockerfile,
                    build_inputs,
                    BASE_IMAGES,
                    [BUILDER],
                    [WORKFLOW],
                    INVOCATION,
                    [],
                )


if __name__ == "__main__":
    unittest.main()
