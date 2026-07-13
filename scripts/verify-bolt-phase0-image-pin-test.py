#!/usr/bin/env python3
from __future__ import annotations

import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


SCRIPT = Path(__file__).with_name("verify-bolt-phase0-image-pin.py")
SPEC = importlib.util.spec_from_file_location("phase0_image_pin", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


SHA = "a" * 40
DIGEST = "sha256:" + "b" * 64
PREFIX = "registry.example/xframework"


class Phase0ImagePinTests(unittest.TestCase):
    @staticmethod
    def manifest(image: str, digest: str = DIGEST, platform: dict | None = None) -> dict:
        descriptor = {"digest": digest}
        if platform is not None:
            descriptor["platform"] = platform
        return {"Ref": f"{image}@{digest}", "Descriptor": descriptor}

    def test_registry_confirmation_produces_exact_approved_pin(self) -> None:
        image = f"{PREFIX}/bolt-hub:{SHA}"
        pins = MODULE.build_pins(
            SHA,
            ["bolt-hub"],
            [("bolt-hub", image, self.manifest(image))],
            PREFIX,
        )
        self.assertEqual({"bolt-hub": f"{PREFIX}/bolt-hub@{DIGEST}"}, pins)

    def test_tagged_manifest_ref_is_validated_then_normalized_to_tagless_pin(self) -> None:
        image = f"{PREFIX}/bolt-hub:{SHA}"
        pins = MODULE.build_pins(SHA, ["bolt-hub"], [("bolt-hub", image, self.manifest(image))], PREFIX)
        self.assertNotIn(f":{SHA}", pins["bolt-hub"])
        self.assertEqual(f"{PREFIX}/bolt-hub@{DIGEST}", pins["bolt-hub"])

    def test_inactive_synthetics_image_has_an_explicit_approved_repository(self) -> None:
        service = "bolt-phase0-synthetics"
        image = f"{PREFIX}/{service}:{SHA}"
        pins = MODULE.build_pins(
            SHA,
            [service],
            [(service, image, self.manifest(image))],
            PREFIX,
        )
        self.assertEqual({service: f"{PREFIX}/{service}@{DIGEST}"}, pins)

    def test_mutable_tag_is_rejected(self) -> None:
        with self.assertRaisesRegex(ValueError, "source commit"):
            MODULE.build_pins(
                SHA,
                ["bolt-hub"],
                [
                    (
                        "bolt-hub",
                        f"{PREFIX}/bolt-hub:develop",
                        self.manifest(f"{PREFIX}/bolt-hub:develop"),
                    )
                ],
                PREFIX,
            )

    def test_unapproved_or_swapped_repository_is_rejected(self) -> None:
        records = (
            (
                "bolt-hub",
                f"registry.example/other/bolt-hub:{SHA}",
                self.manifest(f"registry.example/other/bolt-hub:{SHA}"),
            ),
            (
                "bolt-hub",
                f"{PREFIX}/portal:{SHA}",
                self.manifest(f"{PREFIX}/portal:{SHA}"),
            ),
        )
        for record in records:
            with self.subTest(record=record), self.assertRaisesRegex(ValueError, "approved repository"):
                MODULE.build_pins(SHA, ["bolt-hub"], [record], PREFIX)

    def test_ref_or_descriptor_digest_mismatch_is_rejected(self) -> None:
        image = f"{PREFIX}/bolt-hub:{SHA}"
        manifests = (
            {"Ref": f"{PREFIX}/bolt-hub:develop@{DIGEST}", "Descriptor": {"digest": DIGEST}},
            {
                "Ref": f"{image}@{DIGEST}",
                "Descriptor": {"digest": "sha256:" + "c" * 64},
            },
            {"Ref": f"{image}@SHA256:{'B' * 64}", "Descriptor": {"digest": "SHA256:" + "B" * 64}},
        )
        for manifest in manifests:
            record = ("bolt-hub", image, manifest)
            with self.subTest(manifest=manifest), self.assertRaisesRegex(ValueError, "manifest"):
                MODULE.build_pins(SHA, ["bolt-hub"], [record], PREFIX)

    def test_duplicate_or_ambiguous_linux_amd64_manifests_are_rejected(self) -> None:
        image = f"{PREFIX}/bolt-hub:{SHA}"
        linux_amd64 = {"os": "linux", "architecture": "amd64"}
        single = self.manifest(image, platform=linux_amd64)
        ambiguous = [single, self.manifest(image, "sha256:" + "c" * 64, linux_amd64)]
        for manifest, message in (([single, single], "duplicate"), (ambiguous, "exactly one")):
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                MODULE.build_pins(SHA, ["bolt-hub"], [("bolt-hub", image, manifest)], PREFIX)

    def test_missing_pin_and_unknown_service_are_rejected(self) -> None:
        with self.assertRaisesRegex(ValueError, "missing=.*portal"):
            MODULE.build_pins(SHA, ["bolt-hub", "portal"], [], PREFIX)
        with self.assertRaisesRegex(ValueError, "no approved repository"):
            MODULE.build_pins(SHA, ["rogue"], [], PREFIX)

    def test_cli_reads_raw_manifest_and_emits_safe_pin_evidence(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            image = f"{PREFIX}/bolt-hub:{SHA}"
            manifest = root / "manifest.json"
            override = root / "override.json"
            evidence = root / "evidence.json"
            manifest.write_text(json.dumps(self.manifest(image)), encoding="utf-8")
            argv = [
                str(SCRIPT),
                "--expected-image-tag",
                SHA,
                "--expected-service",
                "bolt-hub",
                "--approved-repository-prefix",
                PREFIX,
                "--registry-manifest",
                "bolt-hub",
                image,
                str(manifest),
                "--output-override",
                str(override),
                "--output-evidence",
                str(evidence),
            ]
            with mock.patch.object(sys, "argv", argv):
                self.assertEqual(0, MODULE.main())
            document = json.loads(evidence.read_text(encoding="utf-8"))
            self.assertEqual("passed", document["status"])
            self.assertEqual(f"{PREFIX}/bolt-hub@{DIGEST}", document["pins"]["bolt-hub"])
            self.assertRegex(document["registry_manifests"]["bolt-hub"]["manifest_sha256"], r"^sha256:")


if __name__ == "__main__":
    unittest.main()
