+++
# --- Step Metadata ---
step_id = "WF-BUILD-PIPELINE-V1-08-FINAL_VALIDATION"
title = "Step 08: (Optional) Final Validation & Sanity Checks"
description = """
Perform final checks on the generated artifacts and release notes
to ensure integrity and completeness before publishing.
"""

# --- Flow Control ---
depends_on = ["WF-BUILD-PIPELINE-V1-07-GENERATE_RELEASE_NOTES"]
next_step = "09_tag_release.md"
error_step = "EE_handle_error.md" # Placeholder
is_optional = true # This step can be skipped

# --- Execution ---
delegate_to = "lead-qa" # Or release manager

# --- Interface ---
inputs = [
    "build_version: (from previous steps)",
    "artifact_paths: List of paths to the generated build archives (from 06_create_artifacts.md output)",
    "release_notes_path: Path to the generated RELEASE_NOTES_v[version].md file (from 07_generate_release_notes.md output)"
]
outputs = [
    "final_validation_status: Confirmation of successful validation (e.g., \"passed\", \"failed\", \"skipped\")."
]

# --- Housekeeping ---
last_updated = "2025-10-05"
template_schema_doc = ".ruru/templates/toml-md/25_workflow_step_standard.md"
+++

# Step 08: (Optional) Final Validation & Sanity Checks

## Actions

1.  **Check if Step is Skipped:**
    *   `- [ ]` Determine if this optional step should be executed. If skipped, proceed to `next_step` and set `final_validation_status` to "skipped".
2.  **Verify Integrity of Archives:**
    *   `- [ ]` For each path in `artifact_paths`:
        *   `- [ ]` Check if the archive file exists and is non-empty.
        *   `- [ ]` (Optional) Attempt to list contents or perform a checksum verification if feasible.
3.  **Check Release Notes:**
    *   `- [ ]` Verify the `release_notes_path` file exists and is non-empty.
    *   `- [ ]` (Manual or Automated) Briefly review release notes for:
        *   Correct version number.
        *   Presence of key sections (features, fixes).
        *   Absence of obvious formatting errors or placeholders.
4.  **Confirm Validation:**
    *   `- [ ]` If all checks pass, set `final_validation_status` to "passed".
    *   `- [ ]` Log the outcome of the final validation.

## Acceptance Criteria

*   If executed, all build artifacts listed in `artifact_paths` are verified (exist, non-empty).
*   If executed, the `release_notes_path` file is verified (exists, non-empty, basic content check).
*   The `final_validation_status` output is "passed" or "skipped".

## Error Handling

*   If any archive integrity check fails, log the specific error, set `final_validation_status` to "failed", and proceed to `EE_handle_error.md`.
*   If release notes checks fail, log the specific error, set `final_validation_status` to "failed", and proceed to `EE_handle_error.md`.