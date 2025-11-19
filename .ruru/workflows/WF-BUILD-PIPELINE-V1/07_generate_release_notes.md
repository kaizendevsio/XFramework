+++
# --- Step Metadata ---
step_id = "WF-BUILD-PIPELINE-V1-07-GENERATE_RELEASE_NOTES"
title = "Step 07: Generate Release Notes"
description = """
Create release notes for the current build version, gathering information
from commit history, MDTM tasks, and potentially a CHANGELOG.md.
"""

# --- Flow Control ---
depends_on = ["WF-BUILD-PIPELINE-V1-06-CREATE_ARTIFACTS"]
next_step = "08_final_validation.md"
error_step = "EE_handle_error.md" # Placeholder

# --- Execution ---
delegate_to = "util-writer" # Or a release manager specialist

# --- Interface ---
inputs = [
    "build_version: (from previous steps)",
    "artifact_paths: List of paths to the generated build archives (from 06_create_artifacts.md output)",
    "commit_history_range: (e.g., last tag to HEAD)",
    "relevant_mdtm_task_ids: List of MDTM task IDs completed for this release."
]
outputs = [
    "release_notes_status: Confirmation of successful release notes generation.",
    "release_notes_path: Path to the generated RELEASE_NOTES_v[version].md file."
]

# --- Housekeeping ---
last_updated = "2025-10-05"
template_schema_doc = ".ruru/templates/toml-md/25_workflow_step_standard.md"
+++

# Step 07: Generate Release Notes

## Actions

1.  **Gather Information:**
    *   `- [ ]` Retrieve commit messages within the `commit_history_range`.
    *   `- [ ]` Fetch details (titles, descriptions) for `relevant_mdtm_task_ids`.
    *   `- [ ]` Read content from `CHANGELOG.md` if it exists and is relevant.
2.  **Select Template:**
    *   `- [ ]` Choose an appropriate release notes template (e.g., `[.ruru/templates/toml-md/13_release_notes.md](.ruru/templates/toml-md/13_release_notes.md)` or `[.ruru/templates/toml-md/18_release_notes.md](.ruru/templates/toml-md/18_release_notes.md)`).
3.  **Draft Release Notes:**
    *   `- [ ]` Populate the chosen template with the gathered information, categorizing changes (features, fixes, chores).
    *   `- [ ]` Include the `build_version` prominently.
    *   `- [ ]` (Optional) Include links or references to the `artifact_paths`.
4.  **Determine Output Path:**
    *   `- [ ]` Construct the output path for the release notes file (e.g., `[.ruru/docs/releases/RELEASE_NOTES_v[build_version].md](.ruru/docs/releases/RELEASE_NOTES_v[build_version].md)`).
5.  **Save Release Notes:**
    *   `- [ ]` Write the drafted content to the determined output path.
6.  **Confirm Generation:**
    *   `- [ ]` If release notes are saved successfully, set `release_notes_status` to "success".
    *   `- [ ]` Set `release_notes_path` to the path of the generated file.
    *   `- [ ]` Log successful release notes generation.

## Acceptance Criteria

*   Information from commits, tasks, and changelog (if used) is incorporated into the release notes.
*   Release notes are formatted according to the chosen template.
*   The `RELEASE_NOTES_v[version].md` file is created in the standard location.
*   The `release_notes_status` output is "success".
*   The `release_notes_path` output correctly points to the generated file.

## Error Handling

*   If information gathering fails (e.g., cannot access commit history), log the error, set `release_notes_status` to "failed", and proceed to `EE_handle_error.md`.
*   If writing the release notes file fails, log the error, set `release_notes_status` to "failed", and proceed to `EE_handle_error.md`.