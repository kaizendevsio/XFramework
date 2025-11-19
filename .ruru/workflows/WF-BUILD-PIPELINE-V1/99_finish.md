+++
# --- Step Metadata ---
step_id = "WF-BUILD-PIPELINE-V1-99-FINISH"
step_number = 99
title = "Step 99: Finish Build Pipeline Workflow"
description = """
Finalization steps for the build pipeline. This includes aggregating results,
performing cleanup, generating a final report, and determining overall
workflow success or failure.
"""

# --- Flow Control ---
depends_on = ["WF-BUILD-PIPELINE-V1-10-PUBLISH_ARTIFACTS"] # Depends on the publish step
next_step = "" # End of the workflow
error_step = "" # Could point to a final_error_summary.md if complex error reporting is needed

# --- Execution ---
delegate_to = "lead-devops" # Or the main orchestrator of this workflow

# --- Interface ---
inputs = [
    "build_version: (from previous steps)",
    "publish_status: (from 10_publish_artifacts.md output)",
    "artifact_paths: (from 06_create_artifacts.md output)",
    "release_notes_path: (from 07_generate_release_notes.md output)",
    "final_validation_status: (from 08_final_validation.md output)",
    "tagging_status: (from 09_tag_release.md output)"
]
outputs = [
    "workflow_result: Summary of the build pipeline's success/failure and key outcomes.",
    "final_build_status: Overall status (e.g., \"SUCCESSFUL\", \"FAILED\", \"COMPLETED_WITH_WARNINGS\")."
]

# --- Housekeeping ---
last_updated = "2025-10-05"
template_schema_doc = ".ruru/templates/toml-md/26_workflow_step_finish.md"
+++

# Step 99: Finish Build Pipeline Workflow

## Actions

1.  **Aggregate Results:**
    *   `- [ ]` Collect all relevant status outputs from previous steps:
        *   `setup_environment_status`
        *   `validate_parameters_status`
        *   `manifest_generation_status`
        *   `linter_status` (if run)
        *   `test_status` (if run)
        *   `artifact_creation_status`
        *   `release_notes_status`
        *   `final_validation_status` (if run)
        *   `tagging_status` (if run)
        *   `publish_status` (if run)
2.  **Perform Cleanup:**
    *   `- [ ]` Remove temporary build directories/files from `[.ruru/.builds/[build_version]/](.ruru/.builds/[build_version]/)` if they are not the final distributable location (e.g., if `dist/` within it is the final, clean up other temp folders).
    *   `- [ ]` (Optional) Clean up any other temporary resources created during the workflow.
3.  **Determine Final Build Status:**
    *   `- [ ]` Based on the aggregated statuses, determine the `final_build_status`.
        *   If any critical step failed (e.g., manifest generation, artifact creation), status is "FAILED".
        *   If optional steps like linting/testing failed but the build is configured to proceed, status might be "COMPLETED_WITH_WARNINGS" or "SUCCESSFUL_WITH_ISSUES".
        *   If all critical steps passed, status is "SUCCESSFUL".
4.  **Generate Final Workflow Report/Summary:**
    *   `- [ ]` Create a concise summary:
        *   Build Version: `{{build_version}}`
        *   Overall Status: `{{final_build_status}}`
        *   Key Artifacts: `{{artifact_paths}}`
        *   Release Notes: `{{release_notes_path}}`
        *   (Optional) Links to detailed logs or reports from individual steps.
    *   `- [ ]` This summary will form the basis of the `workflow_result` output.
5.  **Report Outcome:**
    *   `- [ ]` Set the `workflow_result` output with the generated summary.
    *   `- [ ]` Log the completion of the workflow and its final status.

## Acceptance Criteria

*   All temporary files/directories (as configured) are cleaned up.
*   The `final_build_status` is correctly determined and reflects the outcomes of all preceding steps.
*   The `workflow_result` output contains a comprehensive summary of the build pipeline execution.

## Error Handling

*   If cleanup actions fail, log the error but it might not necessarily change a "SUCCESSFUL" build status unless critical.
*   Errors in generating the final report should be logged.