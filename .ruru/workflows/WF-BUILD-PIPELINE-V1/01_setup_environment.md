+++
# --- Step Metadata ---
step_id = "WF-BUILD-PIPELINE-V1-01-SETUP_ENVIRONMENT"
title = "Step 01: Setup Build Environment"
description = """
Prepare the environment for the build. This includes cleaning previous artifacts,
verifying tools, and setting up environment variables.
"""

# --- Flow Control ---
depends_on = ["WF-BUILD-PIPELINE-V1-00-START"] # Depends on the workflow start
next_step = "02_validate_parameters.md"
error_step = "EE_handle_error.md" # Placeholder, can be defined later

# --- Execution ---
delegate_to = "lead-devops"

# --- Interface ---
inputs = [
    "Build version (from 00_start.md output, e.g., \"v7.3.0\")",
]
outputs = [
    "build_environment_status: Confirmation that the build environment is cleaned and prepared.",
    "build_version: The validated build version to be used by subsequent steps."
]

# --- Housekeeping ---
last_updated = "2025-10-05"
template_schema_doc = ".ruru/templates/toml-md/25_workflow_step_standard.md"
+++

# Step 01: Setup Build Environment

## Actions

1.  **Clean Build Artifacts:**
    *   `- [✅]` Remove any existing content from the primary build directory (e.g., `[.ruru/.builds/](.ruru/.builds/)` or a designated temporary build directory for this run).
    *   `- [✅]` Ensure the primary build directory exists and is empty.
2.  **Verify Tools & Dependencies:**
    *   `- [✅]` Check for the presence and correct version of Node.js.
    *   `- [✅]` Check for the presence and correct version of Bun.
    *   `- [✅]` Verify any other critical command-line tools are available.
3.  **Set Up Environment Variables:**
    *   `- [✅]` Define/export any environment variables required for the build process (e.g., `NODE_ENV=production`).
    *   `- [✅]` Confirm that the input `build_version` is available as an environment variable or parameter for script access.
4.  **Confirm Setup:**
    *   `- [✅]` Log successful environment setup.
    *   `- [✅]` Output the `build_environment_status` as "prepared".
    *   `- [✅]` Pass through the `build_version` for subsequent steps.

## Acceptance Criteria

*   The build directory is clean and ready.
*   All required tools (Node.js, Bun) are verified.
*   Necessary environment variables are set.
*   The `build_environment_status` output is "prepared".
*   The `build_version` is correctly passed as an output.

## Error Handling

*   If cleaning the build directory fails, log the error and proceed to `EE_handle_error.md`.
*   If required tools are missing or versions are incorrect, log the error and proceed to `EE_handle_error.md`.
*   If environment variable setup fails, log the error and proceed to `EE_handle_error.md`.