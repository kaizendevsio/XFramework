+++
# --- Step Metadata ---
step_id = "WF-BUILD-PIPELINE-V1-06-CREATE_ARTIFACTS"
title = "Step 06: Create Build Artifacts"
description = """
Package the build outputs (manifests and other necessary files)
into distributable archives (e.g., .zip or .tar.gz) for each build configuration.
"""

# --- Flow Control ---
depends_on = ["WF-BUILD-PIPELINE-V1-05-RUN_TESTS"] # Or 04 if tests are skipped
next_step = "07_generate_release_notes.md"
error_step = "EE_handle_error.md" # Placeholder

# --- Execution ---
delegate_to = "lead-devops"

# --- Interface ---
inputs = [
    "build_version: (from previous steps)",
    "manifest_directory: Path to the directory containing generated manifests (from 03_generate_manifests.md output)",
    "test_status: (from 05_run_tests.md output, to ensure tests passed or were skipped appropriately before packaging)"
]
outputs = [
    "artifact_creation_status: Confirmation of successful artifact packaging.",
    "artifact_paths: List of paths to the generated build archives."
]

# --- Housekeeping ---
last_updated = "2025-10-05"
template_schema_doc = ".ruru/templates/toml-md/25_workflow_step_standard.md"
+++

# Step 06: Create Build Artifacts

## Actions

1.  **Define Artifact Configurations:**
    *   `- [ ]` Identify the different build configurations/targets (e.g., 'core', 'full', 'ai_ml_python'). This might come from a configuration file or be predefined.
2.  **Determine Release Directory:**
    *   `- [ ]` Establish the base directory for storing final release artifacts (e.g., `[.ruru/.builds/[build_version]/dist/](.ruru/.builds/[build_version]/dist/)`).
    *   `- [ ]` Ensure this directory is created.
3.  **Package Artifacts per Configuration:**
    *   For each defined build configuration:
        *   `- [ ]` Identify the specific manifest files (e.g., `.roomodes`, `.kilocodemodes`) from the `manifest_directory` relevant to this configuration.
        *   `- [ ]` Identify any other project files or assets that need to be included in this specific artifact.
        *   `- [ ]` Create an archive (e.g., `[configuration_name].zip` or `[configuration_name].tar.gz`) containing these files.
        *   `- [ ]` Place the created archive into the release directory.
4.  **Verify Archives:**
    *   `- [ ]` (Optional but recommended) Perform a quick verification of each archive (e.g., check if it's non-empty, list contents to ensure key files are present).
5.  **Confirm Creation:**
    *   `- [ ]` If all artifacts are packaged successfully, set `artifact_creation_status` to "success".
    *   `- [ ]` Populate `artifact_paths` with the list of full paths to the generated archives.
    *   `- [ ]` Log successful artifact creation.

## Acceptance Criteria

*   Distributable archives are created for each specified build configuration.
*   Archives are placed in the correct release directory.
*   The `artifact_creation_status` output is "success".
*   The `artifact_paths` output contains a list of paths to the created archives.

## Error Handling

*   If identifying files for a configuration fails, log the error, set `artifact_creation_status` to "failed", and proceed to `EE_handle_error.md`.
*   If archiving fails for any configuration, log the error, set `artifact_creation_status` to "failed", and proceed to `EE_handle_error.md`.
*   If archive verification fails (if implemented), log the error and potentially proceed to `EE_handle_error.md` depending on severity.