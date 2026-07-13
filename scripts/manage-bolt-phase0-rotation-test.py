#!/usr/bin/env python3
from __future__ import annotations

import importlib.util
import json
import os
import stat
import subprocess
import sys
import tempfile
import unittest
from datetime import datetime, timedelta, timezone
from pathlib import Path
from unittest import mock


SCRIPT = Path(__file__).with_name("manage-bolt-phase0-rotation.py")
SPEC = importlib.util.spec_from_file_location("phase0_rotation", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)

NOW = datetime(2026, 7, 13, 12, 0, 0, tzinfo=timezone.utc)
VALID_FOR = 3_600
MINIMUM_REMAINING = 900


def primary_value(index: int) -> str:
    return f"credential-{index:02d}-" + chr(ord("a") + index) * 52


def make_env(path: Path) -> dict[str, str]:
    values = {
        "CREDENTIAL_GENERATION_ID": "generation-g",
        **{
            name: primary_value(index)
            for index, name in enumerate(MODULE.PRIMARY_SECRET_NAMES)
        },
    }
    lines = [
        "# deployment settings retained verbatim\n",
        "UNRELATED=keep-this-value\n",
        "\n",
        *[f"{name}={value}\n" for name, value in values.items()],
        "# trailing comment\n",
    ]
    path.write_text("".join(lines), encoding="utf-8")
    return values


def write_inventory(path: Path, generation: str, **overrides: str) -> None:
    services = {service: generation for service in MODULE.REQUIRED_SERVICES}
    services.update(overrides)
    path.write_text(
        json.dumps(
            {
                "schema": MODULE.INVENTORY_SCHEMA,
                "generated_at_utc": MODULE.format_utc(NOW),
                "services": services,
            }
        ),
        encoding="utf-8",
    )


class RotationFixture(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary_directory.name)
        self.env_path = self.root / "compose.env"
        self.state_path = self.root / "rotation-state.json"
        self.inventory_path = self.root / "inventory.json"
        self.original = make_env(self.env_path)

    def tearDown(self) -> None:
        self.temporary_directory.cleanup()

    def prepare(self) -> dict:
        return MODULE.prepare(self.env_path, self.state_path, VALID_FOR, NOW)

    def activate(self) -> dict:
        return MODULE.activate(
            self.env_path,
            self.state_path,
            MINIMUM_REMAINING,
            NOW + timedelta(minutes=1),
        )

    def converge(self) -> dict:
        state = json.loads(self.state_path.read_text(encoding="utf-8"))
        write_inventory(self.inventory_path, state["target_generation_id"])
        return MODULE.verify_convergence_input(
            self.env_path,
            self.state_path,
            self.inventory_path,
            NOW + timedelta(minutes=2),
        )


class BootstrapTests(RotationFixture):
    def remove_generation(self) -> None:
        content = self.env_path.read_text(encoding="utf-8")
        content = "\n".join(
            line for line in content.splitlines()
            if not line.startswith("CREDENTIAL_GENERATION_ID=")
        ) + "\n"
        self.env_path.write_text(content, encoding="utf-8")

    def test_validate_bootstrap_is_read_only_when_generation_is_missing(self) -> None:
        self.remove_generation()
        before = self.env_path.read_bytes()

        result = MODULE.validate_bootstrap_inputs(self.env_path, self.state_path)

        self.assertEqual(MODULE.BOOTSTRAP_VALIDATION_SCHEMA, result["schema"])
        self.assertFalse(result["generation_marker_present"])
        self.assertTrue(result["mutation_required"])
        self.assertEqual(before, self.env_path.read_bytes())
        self.assertFalse(self.state_path.exists())
        self.assertFalse(
            self.env_path.with_name(f".{self.env_path.name}.phase0-rotation.lock").exists()
        )

    def test_validate_bootstrap_rejects_invalid_inputs_without_mutation(self) -> None:
        self.remove_generation()
        document = MODULE.parse_env(self.env_path)
        self.env_path.write_bytes(document.render({}, {"JWT_SECRET"}))
        before = self.env_path.read_bytes()

        with self.assertRaisesRegex(MODULE.RotationError, "JWT_SECRET"):
            MODULE.validate_bootstrap_inputs(self.env_path, self.state_path)

        self.assertEqual(before, self.env_path.read_bytes())
        self.assertFalse(self.state_path.exists())

    def test_validate_bootstrap_rejects_existing_state_without_mutation(self) -> None:
        self.state_path.write_text("{}", encoding="utf-8")
        before_env = self.env_path.read_bytes()
        before_state = self.state_path.read_bytes()

        with self.assertRaisesRegex(MODULE.RotationError, "state to be absent"):
            MODULE.validate_bootstrap_inputs(self.env_path, self.state_path)

        self.assertEqual(before_env, self.env_path.read_bytes())
        self.assertEqual(before_state, self.state_path.read_bytes())

    def test_bootstrap_adds_only_missing_generation_without_rotation_state(self) -> None:
        self.remove_generation()
        before = MODULE.parse_env(self.env_path).values

        result = MODULE.bootstrap(self.env_path, self.state_path, NOW)
        document = MODULE.parse_env(self.env_path)

        self.assertEqual(MODULE.BOOTSTRAP_SCHEMA, result["schema"])
        self.assertRegex(document.values["CREDENTIAL_GENERATION_ID"], r"^legacy-")
        self.assertEqual(document.values["CREDENTIAL_GENERATION_ID"], result["current_generation_id"])
        self.assertEqual(before, {
            name: value
            for name, value in document.values.items()
            if name != "CREDENTIAL_GENERATION_ID"
        })
        self.assertFalse(self.state_path.exists())
        self.assertTrue(set(MODULE.SECONDARY_STATE_NAMES).isdisjoint(document.values))
        if os.name != "nt":
            self.assertEqual(0o600, stat.S_IMODE(self.env_path.stat().st_mode))

    def test_bootstrap_is_idempotent_for_existing_and_new_generation(self) -> None:
        existing_before = self.env_path.read_bytes()
        existing_first = MODULE.bootstrap(self.env_path, self.state_path, NOW)
        existing_second = MODULE.bootstrap(self.env_path, self.state_path, NOW + timedelta(hours=1))
        self.assertEqual(existing_first, existing_second)
        self.assertEqual(existing_before, self.env_path.read_bytes())

        self.remove_generation()
        generated_first = MODULE.bootstrap(self.env_path, self.state_path, NOW)
        generated_bytes = self.env_path.read_bytes()
        generated_second = MODULE.bootstrap(
            self.env_path,
            self.state_path,
            NOW + timedelta(hours=1),
        )
        self.assertEqual(generated_first, generated_second)
        self.assertEqual(generated_bytes, self.env_path.read_bytes())
        self.assertFalse(self.state_path.exists())

    def test_bootstrap_validates_all_primary_secrets_before_mutation(self) -> None:
        self.remove_generation()
        document = MODULE.parse_env(self.env_path)
        self.env_path.write_bytes(document.render({}, {"JWT_SECRET"}))
        before = self.env_path.read_bytes()

        with self.assertRaisesRegex(MODULE.RotationError, "JWT_SECRET"):
            MODULE.bootstrap(self.env_path, self.state_path, NOW)

        self.assertEqual(before, self.env_path.read_bytes())
        self.assertFalse(self.state_path.exists())

    def test_bootstrap_rejects_partial_secondary_state_without_mutation(self) -> None:
        self.remove_generation()
        with self.env_path.open("a", encoding="utf-8") as stream:
            stream.write("JWT_SECONDARY_SECRET=" + "z" * 64 + "\n")
        before = self.env_path.read_bytes()

        with self.assertRaisesRegex(MODULE.RotationError, "untracked or partial"):
            MODULE.bootstrap(self.env_path, self.state_path, NOW)

        self.assertEqual(before, self.env_path.read_bytes())
        self.assertFalse(self.state_path.exists())

    def test_bootstrap_rejects_existing_rotation_state_without_mutation(self) -> None:
        self.remove_generation()
        self.state_path.write_text("{}", encoding="utf-8")
        before = self.env_path.read_bytes()

        with self.assertRaisesRegex(MODULE.RotationError, "state to be absent"):
            MODULE.bootstrap(self.env_path, self.state_path, NOW)

        self.assertEqual(before, self.env_path.read_bytes())
        self.assertEqual(b"{}", self.state_path.read_bytes())

    def test_bootstrap_cli_output_contains_no_credentials(self) -> None:
        self.remove_generation()

        result = subprocess.run(
            [
                sys.executable,
                str(SCRIPT),
                "bootstrap",
                "--env-file",
                str(self.env_path),
                "--state-file",
                str(self.state_path),
            ],
            check=False,
            capture_output=True,
            text=True,
        )

        self.assertEqual(0, result.returncode, result.stderr)
        public_material = result.stdout + result.stderr
        self.assertTrue(
            all(secret not in public_material for secret in self.original.values())
        )
        self.assertEqual(MODULE.BOOTSTRAP_SCHEMA, json.loads(result.stdout)["schema"])
        self.assertFalse(self.state_path.exists())

    @unittest.skipIf(os.name == "nt", "Windows symlink creation may require elevated privileges")
    def test_bootstrap_rejects_env_and_state_symlinks(self) -> None:
        target = self.root / "actual.env"
        self.env_path.replace(target)
        self.env_path.symlink_to(target)
        with self.assertRaisesRegex(MODULE.RotationError, "non-symlink"):
            MODULE.bootstrap(self.env_path, self.state_path, NOW)

        self.env_path.unlink()
        target.replace(self.env_path)
        state_target = self.root / "actual-state.json"
        state_target.write_text("{}", encoding="utf-8")
        self.state_path.symlink_to(state_target)
        with self.assertRaisesRegex(MODULE.RotationError, "state to be absent"):
            MODULE.bootstrap(self.env_path, self.state_path, NOW)


class PrepareTests(RotationFixture):
    def test_prepare_requires_bootstrapped_generation_without_mutation(self) -> None:
        content = self.env_path.read_text(encoding="utf-8")
        content = "\n".join(
            line for line in content.splitlines()
            if not line.startswith("CREDENTIAL_GENERATION_ID=")
        ) + "\n"
        self.env_path.write_text(content, encoding="utf-8")
        before = self.env_path.read_bytes()

        with self.assertRaisesRegex(MODULE.RotationError, "CREDENTIAL_GENERATION_ID"):
            self.prepare()

        self.assertEqual(before, self.env_path.read_bytes())
        self.assertFalse(self.state_path.exists())

    def test_prepare_keeps_g_active_and_stages_distinct_high_entropy_values(self) -> None:
        result = self.prepare()
        document = MODULE.parse_env(self.env_path)

        self.assertEqual("prepared", result["phase"])
        self.assertEqual("generation-g", document.values["CREDENTIAL_GENERATION_ID"])
        self.assertEqual(result["target_generation_id"], document.values["CREDENTIAL_SECONDARY_GENERATION_ID"])
        self.assertEqual(
            "2026-07-13T13:00:00Z",
            document.values["CREDENTIAL_SECONDARY_VALID_UNTIL_UTC"],
        )
        secondaries = [document.values[name] for name in MODULE.SECONDARY_SECRET_NAMES]
        self.assertEqual(len(secondaries), len(set(secondaries)))
        self.assertTrue(all(len(value) >= 64 for value in secondaries))
        self.assertTrue(set(secondaries).isdisjoint(self.original.values()))
        content = self.env_path.read_text(encoding="utf-8")
        self.assertIn("# deployment settings retained verbatim\n", content)
        self.assertIn("UNRELATED=keep-this-value\n", content)
        self.assertIn("# trailing comment\n", content)
        if os.name != "nt":
            self.assertEqual(0o600, stat.S_IMODE(self.env_path.stat().st_mode))
            self.assertEqual(0o600, stat.S_IMODE(self.state_path.stat().st_mode))

    def test_prepare_is_idempotent_without_regenerating_credentials(self) -> None:
        first = self.prepare()
        env_after_first = self.env_path.read_bytes()
        state_after_first = self.state_path.read_bytes()

        second = MODULE.prepare(self.env_path, self.state_path, 900, NOW + timedelta(minutes=1))

        self.assertEqual(first, second)
        self.assertEqual(env_after_first, self.env_path.read_bytes())
        self.assertEqual(state_after_first, self.state_path.read_bytes())

    def test_prepare_rejects_out_of_bounds_window_without_writing_state(self) -> None:
        for validity in (MODULE.MIN_VALID_FOR_SECONDS - 1, MODULE.MAX_VALID_FOR_SECONDS + 1):
            with self.subTest(validity=validity):
                with self.assertRaises(MODULE.RotationError):
                    MODULE.prepare(self.env_path, self.state_path, validity, NOW)
                self.assertFalse(self.state_path.exists())

    def test_duplicate_or_unsafe_env_syntax_is_rejected_without_modification(self) -> None:
        bad_documents = (
            "CREDENTIAL_GENERATION_ID=g\nCREDENTIAL_GENERATION_ID=g2\n",
            "export CREDENTIAL_GENERATION_ID=g\n",
            " CREDENTIAL_GENERATION_ID=g\n",
            "CREDENTIAL_GENERATION_ID=g\x00ignored\n",
            "CREDENTIAL_GENERATION_ID=g\u2028JWT_SECRET=value\n",
        )
        for content in bad_documents:
            with self.subTest(content=repr(content)):
                self.env_path.write_text(content, encoding="utf-8")
                before = self.env_path.read_bytes()
                with self.assertRaises(MODULE.RotationError):
                    self.prepare()
                self.assertEqual(before, self.env_path.read_bytes())
                self.assertFalse(self.state_path.exists())

    def test_untracked_partial_secondary_state_fails_closed(self) -> None:
        with self.env_path.open("a", encoding="utf-8") as stream:
            stream.write("JWT_SECONDARY_SECRET=" + "z" * 64 + "\n")

        with self.assertRaisesRegex(MODULE.RotationError, "untracked or partial"):
            self.prepare()

        self.assertFalse(self.state_path.exists())

    def test_secret_values_never_enter_state_or_cli_output(self) -> None:
        result = subprocess.run(
            [
                sys.executable,
                str(SCRIPT),
                "prepare",
                "--env-file",
                str(self.env_path),
                "--state-file",
                str(self.state_path),
                "--valid-for-seconds",
                str(VALID_FOR),
            ],
            check=False,
            capture_output=True,
            text=True,
        )
        self.assertEqual(0, result.returncode, result.stderr)
        document = MODULE.parse_env(self.env_path)
        all_secrets = [document.values[name] for name in MODULE.PRIMARY_SECRET_NAMES]
        all_secrets.extend(document.values[name] for name in MODULE.SECONDARY_SECRET_NAMES)
        public_material = result.stdout + result.stderr + self.state_path.read_text(encoding="utf-8")
        self.assertTrue(all(secret not in public_material for secret in all_secrets))
        self.assertEqual("prepared", json.loads(result.stdout)["phase"])

    def test_interrupted_prepare_recovers_without_regenerating_staged_values(self) -> None:
        real_write_json = MODULE._write_json
        writes = 0

        def fail_second_state_write(path: Path, document: dict) -> None:
            nonlocal writes
            writes += 1
            if writes == 2:
                raise OSError("simulated journal interruption")
            real_write_json(path, document)

        with mock.patch.object(MODULE, "_write_json", side_effect=fail_second_state_write):
            with self.assertRaises(OSError):
                self.prepare()

        staged = self.env_path.read_bytes()
        state = json.loads(self.state_path.read_text(encoding="utf-8"))
        self.assertEqual("preparing", state["phase"])

        recovered = MODULE.prepare(self.env_path, self.state_path, VALID_FOR, NOW + timedelta(seconds=1))
        self.assertEqual("prepared", recovered["phase"])
        self.assertEqual(staged, self.env_path.read_bytes())

    @unittest.skipIf(os.name == "nt", "Windows symlink creation may require elevated privileges")
    def test_symlink_env_is_rejected(self) -> None:
        target = self.root / "actual.env"
        self.env_path.replace(target)
        self.env_path.symlink_to(target)

        with self.assertRaisesRegex(MODULE.RotationError, "non-symlink"):
            self.prepare()


class ActivationTests(RotationFixture):
    def test_validate_current_only_rejects_secondary_state(self) -> None:
        clean = MODULE.validate_current_only(self.env_path)
        self.assertEqual(MODULE.CURRENT_ONLY_SCHEMA, clean["schema"])
        self.assertEqual("passed", clean["status"])

        self.prepare()
        before = self.env_path.read_bytes()
        with self.assertRaisesRegex(MODULE.RotationError, "secondary state"):
            MODULE.validate_current_only(self.env_path)
        self.assertEqual(before, self.env_path.read_bytes())

    def test_abort_prepared_removes_only_secondary_credentials(self) -> None:
        self.prepare()

        result = MODULE.abort_prepared(self.env_path, self.state_path)

        document = MODULE.parse_env(self.env_path)
        self.assertEqual("aborted", result["phase"])
        self.assertFalse(self.state_path.exists())
        self.assertEqual("generation-g", document.values["CREDENTIAL_GENERATION_ID"])
        self.assertTrue(set(MODULE.SECONDARY_STATE_NAMES).isdisjoint(document.values))
        validation = MODULE.validate_bootstrap_inputs(
            self.env_path,
            self.root / "fresh-run-rotation-state.json",
        )
        self.assertTrue(validation["generation_marker_present"])
        self.assertFalse(validation["mutation_required"])
        repeated = MODULE.abort_prepared(self.env_path, self.state_path)
        self.assertEqual("unprepared", repeated["phase"])

    def test_abort_prepared_rejects_orphaned_secondary_state(self) -> None:
        self.prepare()
        self.state_path.unlink()
        before = self.env_path.read_bytes()

        with self.assertRaisesRegex(MODULE.RotationError, "secondary state"):
            MODULE.abort_prepared(self.env_path, self.state_path)

        self.assertEqual(before, self.env_path.read_bytes())
        self.assertFalse(self.state_path.exists())

    def test_abort_prepared_rejects_activated_rotation(self) -> None:
        self.prepare()
        self.activate()

        with self.assertRaises(MODULE.RotationError):
            MODULE.abort_prepared(self.env_path, self.state_path)

    def test_activate_swaps_every_pair_and_is_idempotent(self) -> None:
        prepared = self.prepare()
        before = MODULE.parse_env(self.env_path).values
        first = self.activate()
        after = MODULE.parse_env(self.env_path).values

        self.assertEqual("activated", first["phase"])
        self.assertEqual(prepared["target_generation_id"], after["CREDENTIAL_GENERATION_ID"])
        self.assertEqual("generation-g", after["CREDENTIAL_SECONDARY_GENERATION_ID"])
        self.assertEqual(
            before["CREDENTIAL_SECONDARY_VALID_UNTIL_UTC"],
            after["CREDENTIAL_SECONDARY_VALID_UNTIL_UTC"],
        )
        for primary, secondary in MODULE.SECRET_PAIRS:
            self.assertEqual(before[secondary], after[primary])
            self.assertEqual(before[primary], after[secondary])

        env_after_first = self.env_path.read_bytes()
        state_after_first = self.state_path.read_bytes()
        second = MODULE.activate(
            self.env_path,
            self.state_path,
            MINIMUM_REMAINING,
            NOW + timedelta(minutes=2),
        )
        self.assertEqual(first, second)
        self.assertEqual(env_after_first, self.env_path.read_bytes())
        self.assertEqual(state_after_first, self.state_path.read_bytes())

    def test_activate_recovers_when_env_swap_precedes_journal_update(self) -> None:
        self.prepare()
        with mock.patch.object(MODULE, "_write_json", side_effect=OSError("interrupted")):
            with self.assertRaises(OSError):
                self.activate()
        active_env = self.env_path.read_bytes()
        self.assertEqual(
            "generation-g",
            json.loads(self.state_path.read_text(encoding="utf-8"))["previous_generation_id"],
        )

        recovered = MODULE.activate(
            self.env_path,
            self.state_path,
            MINIMUM_REMAINING,
            NOW + timedelta(minutes=2),
        )
        self.assertEqual("activated", recovered["phase"])
        self.assertEqual(active_env, self.env_path.read_bytes())

    def test_activation_after_bounded_expiry_is_blocked_without_mutation(self) -> None:
        self.prepare()
        before = self.env_path.read_bytes()

        with self.assertRaisesRegex(MODULE.RotationError, "expired"):
            MODULE.activate(
                self.env_path,
                self.state_path,
                MINIMUM_REMAINING,
                NOW + timedelta(hours=1),
            )

        self.assertEqual(before, self.env_path.read_bytes())
        self.assertEqual(
            "prepared",
            json.loads(self.state_path.read_text(encoding="utf-8"))["phase"],
        )

    def test_activated_journal_never_swaps_a_prepared_env_forward_again(self) -> None:
        self.prepare()
        prepared_env = self.env_path.read_bytes()
        self.activate()
        self.env_path.write_bytes(prepared_env)

        with self.assertRaisesRegex(MODULE.RotationError, "does not match"):
            MODULE.activate(
                self.env_path,
                self.state_path,
                MINIMUM_REMAINING,
                NOW + timedelta(minutes=2),
            )

        self.assertEqual(prepared_env, self.env_path.read_bytes())

    def test_activate_accepts_exact_minimum_remaining_boundary(self) -> None:
        self.prepare()

        result = MODULE.activate(
            self.env_path,
            self.state_path,
            3_000,
            NOW + timedelta(minutes=10),
        )

        self.assertEqual("activated", result["phase"])

    def test_activate_rejects_insufficient_window_before_any_mutation(self) -> None:
        self.prepare()
        env_before = self.env_path.read_bytes()
        state_before = self.state_path.read_bytes()

        with self.assertRaisesRegex(MODULE.RotationError, "insufficient remaining validity"):
            MODULE.activate(
                self.env_path,
                self.state_path,
                3_000,
                NOW + timedelta(minutes=10, seconds=1),
            )

        self.assertEqual(env_before, self.env_path.read_bytes())
        self.assertEqual(state_before, self.state_path.read_bytes())

    def test_activate_rejects_invalid_requested_window_without_mutation(self) -> None:
        self.prepare()
        env_before = self.env_path.read_bytes()
        state_before = self.state_path.read_bytes()

        for requested in (
            MODULE.MIN_ACTIVATION_REMAINING_SECONDS - 1,
            MODULE.MAX_VALID_FOR_SECONDS + 1,
        ):
            with self.subTest(requested=requested):
                with self.assertRaisesRegex(MODULE.RotationError, "minimum remaining validity"):
                    MODULE.activate(self.env_path, self.state_path, requested, NOW)
                self.assertEqual(env_before, self.env_path.read_bytes())
                self.assertEqual(state_before, self.state_path.read_bytes())

    def test_activate_recovers_swapped_env_even_if_budget_later_becomes_insufficient(self) -> None:
        self.prepare()
        with mock.patch.object(MODULE, "_write_json", side_effect=OSError("interrupted")):
            with self.assertRaises(OSError):
                MODULE.activate(
                    self.env_path,
                    self.state_path,
                    3_000,
                    NOW + timedelta(minutes=10),
                )

        recovered = MODULE.activate(
            self.env_path,
            self.state_path,
            3_000,
            NOW + timedelta(minutes=10, seconds=1),
        )

        self.assertEqual("activated", recovered["phase"])


class ConvergenceTests(RotationFixture):
    def setUp(self) -> None:
        super().setUp()
        self.prepare()
        self.activated = self.activate()

    def test_exact_target_inventory_records_only_nonsecret_evidence(self) -> None:
        write_inventory(self.inventory_path, self.activated["target_generation_id"])
        result = MODULE.verify_convergence_input(
            self.env_path,
            self.state_path,
            self.inventory_path,
            NOW + timedelta(minutes=2),
        )

        self.assertEqual("converged", result["phase"])
        state_text = self.state_path.read_text(encoding="utf-8")
        state = json.loads(state_text)
        self.assertEqual(set(MODULE.REQUIRED_SERVICES), set(state["convergence"]["services"]))
        document = MODULE.parse_env(self.env_path)
        for name in (*MODULE.PRIMARY_SECRET_NAMES, *MODULE.SECONDARY_SECRET_NAMES):
            self.assertNotIn(document.values[name], state_text)

        before = self.state_path.read_bytes()
        repeated = MODULE.verify_convergence_input(
            self.env_path,
            self.state_path,
            self.inventory_path,
            NOW + timedelta(minutes=3),
        )
        self.assertEqual(result, repeated)
        self.assertEqual(before, self.state_path.read_bytes())

    def test_exact_generation_objects_are_normalized_before_recording(self) -> None:
        target = self.activated["target_generation_id"]
        services = {
            service: {"credential_generation_id": target}
            for service in MODULE.REQUIRED_SERVICES
        }
        self.inventory_path.write_text(json.dumps({"services": services}), encoding="utf-8")

        result = MODULE.verify_convergence_input(
            self.env_path,
            self.state_path,
            self.inventory_path,
            NOW + timedelta(minutes=2),
        )

        self.assertEqual("converged", result["phase"])
        state = json.loads(self.state_path.read_text(encoding="utf-8"))
        self.assertEqual(
            {service: target for service in MODULE.REQUIRED_SERVICES},
            state["convergence"]["services"],
        )

    def test_missing_extra_or_wrong_generation_inventory_fails_closed(self) -> None:
        target = self.activated["target_generation_id"]
        cases = []
        missing = {service: target for service in MODULE.REQUIRED_SERVICES[1:]}
        cases.append(missing)
        extra = {service: target for service in MODULE.REQUIRED_SERVICES}
        extra["migrate"] = target
        cases.append(extra)
        wrong = {service: target for service in MODULE.REQUIRED_SERVICES}
        wrong["portal"] = "generation-g"
        cases.append(wrong)

        for index, services in enumerate(cases):
            with self.subTest(index=index):
                self.inventory_path.write_text(json.dumps({"services": services}), encoding="utf-8")
                with self.assertRaises(MODULE.RotationError):
                    MODULE.verify_convergence_input(
                        self.env_path,
                        self.state_path,
                        self.inventory_path,
                        NOW + timedelta(minutes=2),
                    )
                state = json.loads(self.state_path.read_text(encoding="utf-8"))
                self.assertEqual("activated", state["phase"])

    def test_duplicate_json_service_key_is_rejected(self) -> None:
        target = self.activated["target_generation_id"]
        pairs = ",".join(f'"{service}":"{target}"' for service in MODULE.REQUIRED_SERVICES)
        duplicate = f'{{"services":{{{pairs},"portal":"{target}"}}}}'
        self.inventory_path.write_text(duplicate, encoding="utf-8")

        with self.assertRaisesRegex(MODULE.RotationError, "duplicate JSON key"):
            MODULE.verify_convergence_input(
                self.env_path,
                self.state_path,
                self.inventory_path,
                NOW + timedelta(minutes=2),
            )


class FinalizeTests(RotationFixture):
    def setUp(self) -> None:
        super().setUp()
        self.prepare()
        self.activate()

    def test_finalize_requires_both_expiry_and_convergence(self) -> None:
        before = self.env_path.read_bytes()
        with self.assertRaisesRegex(MODULE.RotationError, "convergence"):
            MODULE.finalize(self.env_path, self.state_path, NOW + timedelta(hours=2))
        self.assertEqual(before, self.env_path.read_bytes())

        self.converge()
        with self.assertRaisesRegex(MODULE.RotationError, "not expired"):
            MODULE.finalize(self.env_path, self.state_path, NOW + timedelta(minutes=59))
        self.assertEqual(before, self.env_path.read_bytes())

    def test_finalize_removes_only_retiring_state_and_is_idempotent(self) -> None:
        self.converge()
        result = MODULE.finalize(self.env_path, self.state_path, NOW + timedelta(hours=1))
        document = MODULE.parse_env(self.env_path)

        self.assertEqual("finalized", result["phase"])
        self.assertFalse(set(MODULE.SECONDARY_STATE_NAMES).intersection(document.values))
        self.assertEqual("keep-this-value", document.values["UNRELATED"])
        self.assertEqual(result["target_generation_id"], document.values["CREDENTIAL_GENERATION_ID"])

        env_after_first = self.env_path.read_bytes()
        state_after_first = self.state_path.read_bytes()
        repeated = MODULE.finalize(self.env_path, self.state_path, NOW + timedelta(hours=2))
        self.assertEqual(result, repeated)
        self.assertEqual(env_after_first, self.env_path.read_bytes())
        self.assertEqual(state_after_first, self.state_path.read_bytes())

    def test_tampered_convergence_digest_blocks_finalize(self) -> None:
        self.converge()
        state = json.loads(self.state_path.read_text(encoding="utf-8"))
        state["convergence"]["inventory_sha256"] = "0" * 64
        self.state_path.write_text(json.dumps(state), encoding="utf-8")
        before = self.env_path.read_bytes()

        with self.assertRaisesRegex(MODULE.RotationError, "digest"):
            MODULE.finalize(self.env_path, self.state_path, NOW + timedelta(hours=1))
        self.assertEqual(before, self.env_path.read_bytes())

    def test_tampered_validity_window_blocks_status_and_finalize(self) -> None:
        self.converge()
        state = json.loads(self.state_path.read_text(encoding="utf-8"))
        tampered_expiry = "2026-07-15T13:00:00Z"
        state["secondary_valid_until_utc"] = tampered_expiry
        self.state_path.write_text(json.dumps(state), encoding="utf-8")
        document = MODULE.parse_env(self.env_path)
        self.env_path.write_bytes(
            document.render({"CREDENTIAL_SECONDARY_VALID_UNTIL_UTC": tampered_expiry})
        )
        before = self.env_path.read_bytes()

        with self.assertRaisesRegex(MODULE.RotationError, "out-of-bounds"):
            MODULE.rotation_status(self.env_path, self.state_path)
        with self.assertRaisesRegex(MODULE.RotationError, "out-of-bounds"):
            MODULE.finalize(self.env_path, self.state_path, NOW + timedelta(days=3))
        self.assertEqual(before, self.env_path.read_bytes())

    def test_interrupted_finalize_recovers_without_restoring_old_credentials(self) -> None:
        self.converge()
        with mock.patch.object(MODULE, "_write_json", side_effect=OSError("interrupted")):
            with self.assertRaises(OSError):
                MODULE.finalize(self.env_path, self.state_path, NOW + timedelta(hours=1))
        finalized_env = self.env_path.read_bytes()
        self.assertFalse(
            set(MODULE.SECONDARY_STATE_NAMES).intersection(MODULE.parse_env(self.env_path).values)
        )

        recovered = MODULE.finalize(self.env_path, self.state_path, NOW + timedelta(hours=1, seconds=1))
        self.assertEqual("finalized", recovered["phase"])
        self.assertEqual(finalized_env, self.env_path.read_bytes())


if __name__ == "__main__":
    unittest.main()
