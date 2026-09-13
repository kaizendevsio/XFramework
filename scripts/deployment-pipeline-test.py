"""Regression checks for conservative Yap deployment and build context selection."""
import hashlib
import importlib.util
import json
import pathlib
import re
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

import yaml

ROOT = pathlib.Path(__file__).resolve().parent.parent


def load(name):
    spec = importlib.util.spec_from_file_location(name, ROOT / "scripts" / f"{name}.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


scope = load("deployment-scope")
contexts = load("prepare-dotnet-build-context")
baseline = load("deployed-yap-baseline")


class ScopeTests(unittest.TestCase):
    def test_only_yap_source_and_tests_qualify(self):
        self.assertEqual("yap", scope.classify([
            "src/Presentation/XFramework.Yap.Client/wwwroot/app.css",
            "src/Presentation/XFramework.Yap/Services/YapApi.cs",
            "src/Tests/Yap.Tests/SessionTests.cs",
        ]))

    def test_shared_unknown_and_metadata_changes_require_full(self):
        for path in ["", "docs/note.md", "Dockerfile", "Version.props",
                     "src/Modules/XFramework.Communications/Chat.cs",
                     "src/Presentation/XFramework.YapExtra/Program.cs",
                     "src/Presentation/XFramework.Yap/XFramework.Yap.csproj",
                     "src/Presentation/XFramework.Yap/appsettings.Docker.json",
                     "src/Presentation/XFramework.Yap/packages.lock.json"]:
            with self.subTest(path=path):
                self.assertEqual("full", scope.classify([
                    "src/Presentation/XFramework.Yap/Program.cs", path]))
        self.assertEqual("full", scope.classify([]))

    def test_missing_or_non_ancestor_base_fails_closed(self):
        with patch.object(scope.subprocess, "run", side_effect=subprocess.CalledProcessError(1, "git")):
            self.assertEqual("full", scope.git_scope("missing", "HEAD"))

    def test_rename_source_path_is_not_hidden(self):
        with patch.object(scope.subprocess, "run") as run:
            run.return_value.stdout = (
                b"src/Modules/Old.cs\0src/Presentation/XFramework.Yap/New.cs\0")
            self.assertEqual("full", scope.git_scope("base", "head"))
            self.assertIn("--no-renames", run.call_args.args[0])


class BaselineTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = pathlib.Path(self.temp.name).resolve()
        self.release = self.root / "123-1"
        self.release.mkdir()
        (self.root / "current").write_text(str(self.release))
        (self.release / "complete").touch()
        self.commit = "a" * 40
        (self.release / "commit").write_text(self.commit)
        self.env = self.root / "protected.env"
        self.env.write_text("FIXTURE=value\n")
        (self.release / "protected-env.sha256").write_text(hashlib.sha256(self.env.read_bytes()).hexdigest())
        self.image = "sha256:" + "b" * 64
        manifest = {"services": {s: {"image": self.image} for s in baseline.RUNTIME_SERVICES}}
        (self.release / "images.override.json").write_text(json.dumps(manifest))
        self.container = {"Image": self.image, "State": {"Running": True, "Health": {"Status": "healthy"}}}
        self.mock = patch.object(baseline.subprocess, "run")
        self.run = self.mock.start()
        self.addCleanup(self.mock.stop)
        self.run.side_effect = lambda *a, **k: subprocess.CompletedProcess(a, 0, json.dumps([self.container]))

    def test_complete_healthy_unchanged_release_qualifies(self):
        self.assertEqual(self.commit, baseline.baseline(self.root, self.env))
        self.assertEqual(len(baseline.RUNTIME_SERVICES), self.run.call_count)

    def test_configuration_change_requires_full(self):
        self.env.write_text("FIXTURE=rotated\n")
        self.assertIsNone(baseline.baseline(self.root, self.env))

    def test_runtime_image_drift_requires_full(self):
        self.container["Image"] = "sha256:" + "c" * 64
        self.assertIsNone(baseline.baseline(self.root, self.env))

    def test_unhealthy_runtime_requires_full(self):
        self.container["State"]["Health"]["Status"] = "unhealthy"
        self.assertIsNone(baseline.baseline(self.root, self.env))

    def test_incomplete_release_requires_full(self):
        (self.release / "complete").unlink()
        self.assertIsNone(baseline.baseline(self.root, self.env))

    def test_outside_pointer_requires_full(self):
        (self.root / "current").write_text(str(self.root.parent))
        self.assertIsNone(baseline.baseline(self.root, self.env))


class ContextTests(unittest.TestCase):
    def test_cli_stages_all_compose_builds(self):
        compose = yaml.safe_load((ROOT / "docker-compose.yml").read_text(encoding="utf-8"))
        services = [name for name, service in compose["services"].items() if "build" in service]
        with tempfile.TemporaryDirectory() as temp:
            output = pathlib.Path(temp) / "build.json"
            result = subprocess.run([
                sys.executable, "-B", str(ROOT / "scripts/prepare-dotnet-build-context.py"),
                "--output", str(output), "--services", *services,
            ], input=json.dumps(compose), text=True, capture_output=True, cwd=ROOT)
            self.assertEqual(0, result.returncode, result.stderr)
            overrides = json.loads(output.read_text())["services"]
            self.assertEqual(set(services), set(overrides))
            self.assertNotEqual(str(ROOT), overrides["yap"]["build"]["context"], result.stderr)
            for service in services:
                context = pathlib.Path(overrides[service]["build"]["context"])
                project = compose["services"][service]["build"]["args"]["PROJECT_PATH"]
                self.assertTrue((context / project).is_file(), service)
                self.assertTrue((context / "Dockerfile").is_file(), service)

    def test_yap_includes_transitive_service_and_generator_dependencies(self):
        directories = contexts.project_directories(ROOT, "src/Presentation/XFramework.Yap/XFramework.Yap.csproj")
        names = {path.name for path in directories}
        self.assertTrue({"XFramework.Yap", "XFramework.Yap.Client", "XFramework.Yap.Contracts",
                         "Communications.Integration", "IdentityServer.Integration", "Storage.Integration"} <= names)
        self.assertTrue(any("SourceGenerators" in str(path) for path in directories))
        self.assertNotIn("XFramework.Portal", names)
        self.assertNotIn("Communications.Api", names)

    def test_external_linked_inputs_fall_back(self):
        with self.assertRaises(ValueError):
            contexts.project_directories(ROOT, "src/Tests/Yap.Client.Tests/Yap.Client.Tests.csproj")

    def test_scoped_context_ignores_unrelated_source_changes(self):
        with tempfile.TemporaryDirectory() as temp:
            root = pathlib.Path(temp).resolve()
            for relative, text in {
                "Dockerfile": "FROM scratch", "src/App/App.csproj":
                '<Project><ItemGroup><ProjectReference Include="../Lib/Lib.csproj" Condition="false" /></ItemGroup></Project>',
                "src/App/App.cs": "app", "src/Lib/Lib.csproj": "<Project />",
                "src/Lib/Lib.cs": "lib", "src/Other/Other.cs": "unrelated",
            }.items():
                path = root / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(text)
            tracked = [p.relative_to(root) for p in root.rglob("*") if p.is_file()]
            destination = root / "output"
            contexts.stage(root, "src/App/App.csproj", destination, tracked)
            self.assertTrue((destination / "src/Lib/Lib.cs").exists())
            self.assertFalse((destination / "src/Other/Other.cs").exists())
            self.assertTrue((destination / "Dockerfile").exists())


class WorkflowTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.workflow = yaml.load((ROOT / ".github/workflows/deploy-xeon-dev.yml").read_text(), Loader=yaml.BaseLoader)
        cls.steps = {step["name"]: step for step in cls.workflow["jobs"]["deploy"]["steps"]}

    def test_yap_skips_all_core_mutations(self):
        for name in ["Stop legacy Bolt Hub for bounded transition", "Run migration",
                     "Configure and verify Tailscale Serve boundary", "Deploy IdentityServer",
                     "Deploy Bolt Hub", "Deploy Communications", "Configure and verify Yap HTTPS ingress"]:
            self.assertEqual("env.DEPLOY_SCOPE != 'yap'", self.steps[name]["if"])

    def test_build_push_pull_are_scoped(self):
        for name in ["Build images", "Push images", "Pull candidate images"]:
            self.assertIn('if [ "$DEPLOY_SCOPE" = yap ]; then services=(yap); fi', self.steps[name]["run"])

    def test_full_expiry_and_observation_both_gate_success(self):
        run = self.steps["Run authenticated Bolt smoke and concurrent core observation"]["run"]
        self.assertIn("expiry_enabled=true", run)
        self.assertIn('wait "$observation_pid" || observation_status=$?', run)
        self.assertIn('test "$observation_status" -eq 0', run)
        self.assertIn("trap cleanup_observation EXIT", run)
        self.assertIn("smoke_args=(-e BOLT_SYNTHETIC_EXPIRY_TRANSPORT_TOKEN_FILE=)", run)
        self.assertNotIn("expiry_enabled=false", run)

    def test_yap_rollback_does_not_recreate_backends(self):
        rollback = next(step["run"] for name, step in self.steps.items() if name.startswith("Restore previous release"))
        block = rollback.split('if [ -f "$REMOTE_RUN_DIR/yap-only" ]; then\n', 1)[1].split("\nservices=", 1)[0]
        self.assertIn("--wait-timeout 120 yap", block)
        self.assertNotIn("up -d --no-build postgres", block)
        self.assertIn("exit 0", block)

    def test_workflow_bash_and_python_heredocs_parse(self):
        bash = shutil.which("bash")
        git_bash = pathlib.Path("C:/Program Files/Git/bin/bash.exe")
        if git_bash.exists():
            bash = str(git_bash)
        if not bash:
            self.skipTest("Bash syntax is checked by the workflow lint gate")
        for path in (ROOT / ".github/workflows").glob("*.yml"):
            document = yaml.load(path.read_text(encoding="utf-8"), Loader=yaml.BaseLoader)
            for job in document.get("jobs", {}).values():
                for step in job.get("steps", []):
                    run = step.get("run", "")
                    if not run or step.get("shell", "bash") != "bash":
                        continue
                    with self.subTest(workflow=path.name, step=step.get("name")):
                        parsed = subprocess.run([bash, "-n"], input=run, capture_output=True, text=True)
                        self.assertEqual(0, parsed.returncode, parsed.stderr)
                        for body in re.findall(r"<<\s*'PY'[^\n]*\n(.*?)^PY\s*$", run, re.S | re.M):
                            compile(body, str(path), "exec")


if __name__ == "__main__":
    unittest.main()
