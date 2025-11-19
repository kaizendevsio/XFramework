+++
# --- Step Metadata ---
step_id = "WF-BUILD-PIPELINE-V1-05-RUN_TESTS"
title = "Step 05: (Optional) Run Tests"
description = """
Verify code correctness by executing unit tests, integration tests,
and E2E tests as applicable and configured.
"""

# --- Flow Control ---
depends_on = ["WF-BUILD-PIPELINE-V1-04-RUN_LINTERS"]
next_step = "06_create_artifacts.md"
error_step = "EE_handle_error.md" # Placeholder
is_optional = true # This step can be skipped

# --- Execution ---
delegate_to = "lead-qa" # Or a testing specialist

# --- Interface ---
inputs = [
    "source_code_root: Path to the project's source code.",
    "test_configuration: Information on which tests to run (unit, integration, E2E) and their respective commands/scripts.",
    "manifest_directory: (from 03_generate_manifests.md output, if tests depend on manifests)"
]
outputs = [
    "test_status: Confirmation of successful test execution (e.g., \"passed\", \"passed_with_failures\", \"failed\", \"skipped\").",
    "test_report_path: Path to the generated test report(s), if any."
]

# --- Housekeeping ---
last_updated = "2025-10-05"
template_schema_doc = ".ruru/templates/toml-md/25_workflow_step_standard.md"
+++

# Step 05: (Optional) Run Tests

## Actions

1.  **Check if Step is Skipped:**
    *   `- [ ]` Determine if this optional step should be executed based on workflow configuration or input parameters. If skipped, proceed directly to `next_step` and set `test_status` to "skipped".
2.  **Execute Unit Tests:**
    *   `- [ ]` Run unit test suites (e.g., `npm test -- --testPathPattern=generate-manifest.test.js` or similar Jest/Vitest commands).
    *   `- [ ]` Capture exit code and test results/reports.
3.  **Execute Integration Tests (if configured):**
    *   `- [ ]` Run integration test suites.
    *   `- [ ]` Capture exit code and test results/reports.
4.  **Execute E2E Tests (if configured and applicable):**
    *   `- [ ]` Run E2E test suites.
    *   `- [ ]` Capture exit code and test results/reports.
5.  **Process Results:**
    *   `- [ ]` Aggregate test results.
    *   `- [ ]` If critical test failures occur, set `test_status` to "failed".
    *   `- [ ]` If some tests fail but are non-critical (if applicable), set `test_status` to "passed_with_failures".
    *   `- [ ]` If all tests pass, set `test_status` to "passed".
    *   `- [ ]` Set `test_report_path` if report files were generated (could be multiple paths or a summary).
6.  **Confirm Status:**
    *   `- [ ]` Log the outcome of the test executions.

## Acceptance Criteria

*   If executed, all configured test suites (unit, integration, E2E) run.
*   The `test_status` output reflects the overall outcome ("passed", "passed_with_failures", "failed", or "skipped").
*   If reports are generated, `test_report_path` points to them.
*   Build halts or proceeds to `error_step` if `test_status` is "failed" (or "passed_with_failures" if configured to be strict) and not configured to ignore.

## Error Handling

*   If test execution itself fails (e.g., test runner error, config issue), log the error, set `test_status` to "error", and proceed to `EE_handle_error.md`.
*   If critical tests fail, and the build is configured to halt, proceed to `EE_handle_error.md`.