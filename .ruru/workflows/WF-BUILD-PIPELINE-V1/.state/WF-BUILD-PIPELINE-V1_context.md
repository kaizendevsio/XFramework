+++
# --- Workflow Context Metadata ---
workflow_id = "WF-BUILD-PIPELINE-V1"
current_step_id = "" # Current step
status = "failed" # "not_started", "in_progress", "completed", "error", "paused"
last_updated = "2025-10-04T23:01:55Z" # Timestamp of the last update
error_count = 1
template_schema_doc = ".ruru/templates/toml-md/25_workflow_context.md"

# --- Workflow Context Data ---
# This section stores dynamic data passed between workflow steps.
# All data here should be simple key-value pairs.
[context_data]
build_version = "v7.3.0"
linter_status = "skipped"
linter_report_path = ""
test_status = "passed"
test_report_path = ""
artifact_creation_status = "failed"
artifact_paths = []
workflow_result = """
Build Version: v7.3.0
Overall Status: FAILED
Key Artifacts: []
Release Notes: Not generated due to build failure
"""
final_build_status = "FAILED"
# Example: source_branch = "feature/new-login"
# Example: deployment_target = "staging"
# Example: previous_step_output = "some_value"
# Example: manifest_generation_status = "success" # From previous successful step
error_handling_status = "logged_manual_intervention_required"

# --- Error Details (if status is 'error') ---
[[error_details]]
step_id = "WF-BUILD-PIPELINE-V1-06-CREATE_ARTIFACTS" # ID of the step that failed
error_message = "Manifest files not found in expected directory '.ruru/.builds/v7.3.0/manifests/'."
timestamp = "2025-10-04T22:56:36Z"
recovery_suggestion = "Ensure the '03_generate_manifests.md' step completed successfully and created the manifest files in the correct location."
+++

# Workflow Context: WF-BUILD-PIPELINE-V1

This file stores the dynamic context and state for the `WF-BUILD-PIPELINE-V1` workflow.
It is automatically managed by the Workflow Manager. Do not edit manually unless you are recovering from an error.

## Current State:
*   **Workflow ID:** WF-BUILD-PIPELINE-V1
*   **Current Step:** 99_finish.md
*   **Status:** error_handled
*   **Last Updated:** 2025-10-04T22:58:53Z

## Context Data:
*   `build_version`: "v7.3.0"
*   `linter_status`: "skipped"
*   `linter_report_path`: ""
*   `test_status`: "passed"
*   `test_report_path`: ""
*   `artifact_creation_status`: "failed"
*   `artifact_paths`: []
*   `error_handling_status`: "logged_manual_intervention_required"

## Error Log:
[[error_details]]
step_id = "WF-BUILD-PIPELINE-V1-06-CREATE_ARTIFACTS" # ID of the step that failed
error_message = "Manifest files not found in expected directory '.ruru/.builds/v7.3.0/manifests/'."
timestamp = "2025-10-04T22:56:36Z"
recovery_suggestion = "Ensure the '03_generate_manifests.md' step completed successfully and created the manifest files in the correct location."