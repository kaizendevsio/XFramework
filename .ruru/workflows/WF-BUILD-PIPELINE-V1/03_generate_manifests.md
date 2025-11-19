+++
# --- Step Metadata ---
step_id = "WF-BUILD-PIPELINE-V1-03-GENERATE_MANIFESTS"
title = "Step 03: Generate Mode Manifests"
description = """
Create .roomodes and .kilocodemodes files for all defined build configurations
by executing the generate-manifest.js script.
"""

# --- Flow Control ---
depends_on = ["WF-BUILD-PIPELINE-V1-02-VALIDATE_PARAMETERS"]
next_step = "06_create_artifacts.md"
error_step = "EE_handle_error.md" # Placeholder

# --- Execution ---
delegate_to = "lead-devops" # Or a script-runner specialist
command = """
mkdir -p "{{env.BUILD_DIR}}/manifests/" && \\
node .ruru/scripts/generate-manifest.js --inputFile .roomodes --outputPath "{{env.BUILD_DIR}}/manifests/.roomodes" && \\
node .ruru/scripts/generate-manifest.js --inputFile .kilocodemodes --outputPath "{{env.BUILD_DIR}}/manifests/.kilocodemodes" && \\
echo "Verifying generated manifests in {{env.BUILD_DIR}}/manifests/:" && \\
ls -la "{{env.BUILD_DIR}}/manifests/"
"""

# --- Interface ---
inputs = [
    "build_version: (from 02_validate_parameters.md output)",
    "validated_build_parameters: (from 02_validate_parameters.md output)"
]
outputs = [
    "manifest_generation_status: Confirmation of successful manifest generation.",
    "roomodes_manifest_path: \"{{env.BUILD_DIR}}/manifests/.roomodes\"",
    "kilocodemodes_manifest_path: \"{{env.BUILD_DIR}}/manifests/.kilocodemodes\""
]

# --- Housekeeping ---
last_updated = "2025-05-12"
template_schema_doc = ".ruru/templates/toml-md/25_workflow_step_standard.md"
+++

# Step 03: Generate Mode Manifests

## Actions

1.  **Execute Manifest Generation Command:**
    *   `- [ ]` The `command` defined in the TOML frontmatter will be executed. This command:
        *   Creates the `{{env.BUILD_DIR}}/manifests/` directory.
        *   Executes `[.ruru/scripts/generate-manifest.js](.ruru/scripts/generate-manifest.js)` for `.roomodes`, outputting to `{{env.BUILD_DIR}}/manifests/.roomodes`.
        *   Executes `[.ruru/scripts/generate-manifest.js](.ruru/scripts/generate-manifest.js)` for `.kilocodemodes`, outputting to `{{env.BUILD_DIR}}/manifests/.kilocodemodes`.
2.  **Verify Command Execution:**
    *   `- [ ]` Check the exit code of the overall `command`. A non-zero exit code indicates failure.
    *   `- [ ]` Verify that `{{env.BUILD_DIR}}/manifests/.roomodes` and `{{env.BUILD_DIR}}/manifests/.kilocodemodes` are present.
3.  **Confirm Generation:**
    *   `- [ ]` If command execution is successful and files are verified, set `manifest_generation_status` to "success".
    *   `- [ ]` The `roomodes_manifest_path` and `kilocodemodes_manifest_path` outputs will be populated based on the TOML `outputs` definition.
    *   `- [ ]` Log successful manifest generation.

## Acceptance Criteria

*   The `command` in the TOML frontmatter executes successfully.
*   Manifest files `.roomodes` and `.kilocodemodes` are generated in `{{env.BUILD_DIR}}/manifests/`.
*   The `manifest_generation_status` output is "success".
*   The `roomodes_manifest_path` output is `\"{{env.BUILD_DIR}}/manifests/.roomodes\"`.
*   The `kilocodemodes_manifest_path` output is `\"{{env.BUILD_DIR}}/manifests/.kilocodemodes\"`.

## Error Handling

*   If the `command` execution fails (non-zero exit code), log the error, set `manifest_generation_status` to "failed", and proceed to `EE_handle_error.md`.
*   If expected output files (`{{env.BUILD_DIR}}/manifests/.roomodes`, `{{env.BUILD_DIR}}/manifests/.kilocodemodes`) are not found after command execution, log the error, set `manifest_generation_status` to "failed", and proceed to `EE_handle_error.md`.