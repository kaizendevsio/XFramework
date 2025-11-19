+++
# --- Step Metadata ---
step_id = "WF-BUILD-PIPELINE-V1-04-RUN_LINTERS"
title = "Step 04: (Optional) Run Linters & Static Analysis"
description = """
Ensure code quality by running linters (e.g., ESLint) and any configured
static analysis tools before proceeding with the build.
"""

# --- Flow Control ---
depends_on = ["WF-BUILD-PIPELINE-V1-03-GENERATE_MANIFESTS"]
next_step = "05_run_tests.md"
error_step = "EE_handle_error.md" # Placeholder
is_optional = true # This step can be skipped
optional = true # As per TASK-ROOCMD-20251005-230200

# --- Execution ---
delegate_to = "lead-qa" # Or a code quality specialist

# --- Interface ---
inputs = [
    "source_code_root: Path to the project's source code.",
    "linter_config_file: Path to the linter configuration (e.g., .eslintrc.js)."
]
outputs = [
    "linter_status: Confirmation of successful linting (e.g., \"passed\", \"passed_with_warnings\", \"failed\").",
    "linter_report_path: Path to the generated linter report, if any."
]

# --- Housekeeping ---
last_updated = "2025-10-05"
template_schema_doc = ".ruru/templates/toml-md/25_workflow_step_standard.md"
+++

# Step 04: (Optional) Run Linters & Static Analysis

## Actions

1.  **Check if Step is Skipped:**
    *   `- [ ]` Determine if this optional step should be executed based on workflow configuration or input parameters. If skipped, proceed directly to `next_step` and set `linter_status` to "skipped".
2.  **Execute Linters:**
    *   `- [ ]` Run the primary linter command (e.g., `npx eslint . --format json --output-file .ruru/.builds/[build_version]/lint_report.json`).
    *   `- [ ]` Capture the exit code and any output.
3.  **Execute Static Analysis (if configured):**
    *   `- [ ]` Run any additional static analysis tools.
    *   `- [ ]` Capture exit codes and outputs.
4.  **Process Results:**
    *   `- [ ]` Analyze linter/static analysis results.
    *   `- [ ]` If critical errors are found, set `linter_status` to "failed".
    *   `- [ ]` If only warnings, set `linter_status` to "passed_with_warnings".
    *   `- [ ]` If no issues, set `linter_status` to "passed".
    *   `- [ ]` Set `linter_report_path` if a report file was generated.
5.  **Confirm Status:**
    *   `- [ ]` Log the outcome of the linting and static analysis.

## Acceptance Criteria

*   If executed, linters and static analysis tools run without fatal errors.
*   The `linter_status` output reflects the outcome ("passed", "passed_with_warnings", "failed", or "skipped").
*   If a report is generated, `linter_report_path` points to it.
*   Build halts or proceeds to `error_step` if `linter_status` is "failed" and not configured to ignore.

## Error Handling

*   If linter/static analysis execution itself fails (e.g., command not found, config error), log the error, set `linter_status` to "error", and proceed to `EE_handle_error.md`.
*   If critical linting/analysis errors are reported by the tools, and the build is configured to halt on such errors, proceed to `EE_handle_error.md`.