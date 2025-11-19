+++
# --- Step Metadata ---
step_id = "WF-BUILD-PIPELINE-V1-09-TAG_RELEASE"
title = "Step 09: (Optional) Version Control Tagging"
description = """
Tag the release in the version control system (e.g., Git)
and push the tag to the remote repository.
"""

# --- Flow Control ---
depends_on = ["WF-BUILD-PIPELINE-V1-08-FINAL_VALIDATION"]
next_step = "10_publish_artifacts.md"
error_step = "EE_handle_error.md" # Placeholder
is_optional = true # This step can be skipped

# --- Execution ---
delegate_to = "lead-devops" # Or dev-git specialist

# --- Interface ---
inputs = [
    "build_version: (from previous steps, e.g., \"v7.3.0\")",
    "final_validation_status: (from 08_final_validation.md output, to ensure validation passed or was skipped)"
]
outputs = [
    "tagging_status: Confirmation of successful tagging (e.g., \"success\", \"skipped\", \"failed\")."
]

# --- Housekeeping ---
last_updated = "2025-10-05"
template_schema_doc = ".ruru/templates/toml-md/25_workflow_step_standard.md"
+++

# Step 09: (Optional) Version Control Tagging

## Actions

1.  **Check if Step is Skipped:**
    *   `- [ ]` Determine if this optional step should be executed. If skipped, proceed to `next_step` and set `tagging_status` to "skipped".
2.  **Ensure Clean Working Directory:**
    *   `- [ ]` Verify that the Git working directory is clean (no uncommitted changes) before tagging.
3.  **Create Git Tag:**
    *   `- [ ]` Execute the command to create a Git tag using the `build_version` (e.g., `git tag -a v[build_version] -m "Release v[build_version]"`).
4.  **Push Git Tag:**
    *   `- [ ]` Execute the command to push the newly created tag to the remote repository (e.g., `git push origin v[build_version]`).
5.  **Confirm Tagging:**
    *   `- [ ]` If tagging and pushing are successful, set `tagging_status` to "success".
    *   `- [ ]` Log successful Git tagging.

## Acceptance Criteria

*   If executed, the Git working directory is clean before tagging.
*   If executed, a Git tag corresponding to the `build_version` is created locally.
*   If executed, the Git tag is successfully pushed to the remote repository.
*   The `tagging_status` output is "success" or "skipped".

## Error Handling

*   If the Git working directory is not clean, log an error, set `tagging_status` to "failed", and proceed to `EE_handle_error.md`.
*   If `git tag` command fails, log the error, set `tagging_status` to "failed", and proceed to `EE_handle_error.md`.
*   If `git push` for the tag fails, log the error, set `tagging_status` to "failed", and proceed to `EE_handle_error.md`.