+++
# --- Step Metadata ---
step_id = "WF-BUILD-PIPELINE-V1-02-VALIDATE_PARAMETERS"
title = "Step 02: Validate Build Parameters"
description = """
Ensure all required parameters for the build are present and valid.
This includes checking the build version format and other specific parameters.
"""

# --- Flow Control ---
depends_on = ["WF-BUILD-PIPELINE-V1-01-SETUP_ENVIRONMENT"]
next_step = "03_generate_manifests.md"
error_step = "EE_handle_error.md" # Placeholder

# --- Execution ---
delegate_to = "lead-devops" # Or a more specific validation agent if available

# --- Interface ---
inputs = [
    "build_version: (from 01_setup_environment.md output)",
    "other_build_parameters: (e.g., target platforms, specific configurations, from 00_start.md or environment)"
]
outputs = [
    "validated_build_parameters: Confirmation that all parameters are valid.",
    "build_version: The validated build version."
]

# --- Housekeeping ---
last_updated = "2025-10-05"
template_schema_doc = ".ruru/templates/toml-md/25_workflow_step_standard.md"
+++

# Step 02: Validate Build Parameters

## Actions

1.  **Validate Build Version:**
    *   `- [✅]` Check if the `build_version` (received from the previous step) is provided.
    *   `- [✅]` Verify that the `build_version` string adheres to semantic versioning (e.g., `vX.Y.Z`, `X.Y.Z`).
2.  **Validate Other Parameters:**
    *   `- [✅]` Identify any other build-specific parameters required by this pipeline (e.g., target platforms, feature flags if applicable).
    *   `- [✅]` For each required parameter, check if it's provided and if its value is within acceptable limits or formats.
3.  **Confirm Validation:**
    *   `- [✅]` If all parameters are valid, set `validated_build_parameters` output to "success".
    *   `- [✅]` Pass through the validated `build_version`.
    *   `- [✅]` Log successful parameter validation.

## Acceptance Criteria

*   The `build_version` is present and correctly formatted (semantic versioning).
*   All other pipeline-specific parameters are present and valid.
*   The `validated_build_parameters` output is "success".
*   The `build_version` is correctly passed as an output.

## Error Handling

*   If `build_version` is missing or malformed, log the error, set `validated_build_parameters` to "failed", and proceed to `EE_handle_error.md`.
*   If any other required parameter is missing or invalid, log the specific error, set `validated_build_parameters` to "failed", and proceed to `EE_handle_error.md`.