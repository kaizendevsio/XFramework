+++
# --- Step Metadata ---
step_id = "WF-CREATE-ROO-CMD-BUILD-V1-02B-GENERATE-MANIFESTS"
title = "Step 02b: Generate Mode Manifests"
description = """
(String, Required) Generates the .roomodes manifest file for the Wallaby build
by executing the '.ruru/scripts/generate-manifest.js' script.
"""

# --- Flow Control ---
depends_on = ["WF-CREATE-ROO-CMD-BUILD-V1-02-SETUP-ENVIRONMENT"]
next_step = "03a_run_main_build.md"
error_step = "EE_handle_build_error.md" # Or a more specific manifest error handler if created

# --- Execution ---
delegate_to = "lead-devops"

# --- Interface ---
inputs = [
    "Output from step WF-CREATE-ROO-CMD-BUILD-V1-01-VALIDATE-PARAMS: validated_build_params", # To know it's a Wallaby build implicitly
]
outputs = [
    "roomodes_file_path: Path to the generated .roomodes file (e.g., '.roomodes').",
    "manifest_generation_status: Success or Failure.",
]

# --- Housekeeping ---
last_updated = "2025-05-12" # Current date
template_schema_doc = ".ruru/templates/toml-md/25_workflow_step_standard.md"
+++

# Step 02b: Generate Mode Manifests

## Actions

1.  **Receive Context:** Implicitly, the environment is set up from the previous step. The `validated_build_params` (from step 01) confirms the build type context (Wallaby).
2.  **Execute Manifest Generation Script:**
    *   Run the command: `node .ruru/scripts/generate-manifest.js --buildType wallaby --outputPath .roomodes`
    *   Capture output and check the exit code.
3.  **Prepare Output:**
    *   Set `manifest_generation_status` to "Success" or "Failure" based on the script's exit code.
    *   Set `roomodes_file_path` to `".roomodes"` if successful.

## Acceptance Criteria

*   The `generate-manifest.js` script is executed with `--buildType wallaby` and `--outputPath .roomodes`.
*   The script completes, and its exit code is checked.
*   `manifest_generation_status` is set correctly.
*   If successful, `roomodes_file_path` is `".roomodes"`.
*   The `.roomodes` file is present at the project root.

## Error Handling

*   If the `node` command fails (non-zero exit code), set `manifest_generation_status` to "Failure" and proceed to `EE_handle_build_error.md` (or a more specific error step if one exists for manifest generation).