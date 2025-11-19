+++
# --- Step Metadata ---
step_id = "WF-CREATE-ROO-CMD-BUILD-V1-01-VALIDATE-PARAMS" # (String, Required) Unique ID for this step.
title = "Step 01: Validate Build Parameters" # (String, Required) Title of this specific step.
description = """
(String, Required) Performs detailed validation on the build parameters received from the start step,
the version confirmed by the user, and GitHub repository details read from project.toml.
Checks for valid version format, recognized target platforms, and presence of repository info.
"""

# --- Flow Control ---
depends_on = ["WF-CREATE-ROO-CMD-BUILD-V1-00-START", "WF-CREATE-ROO-CMD-BUILD-V1-00B-SUGGEST-VERSION"] # (Array of Strings, Required) step_ids this step needs completed first.
next_step = "02_setup_environment.md" # (String, Optional) Filename of the next step on successful completion.
error_step = "EE_handle_validation_error.md" # (String, Optional) Filename to jump to if this step fails.

# --- Execution ---
delegate_to = "lead-devops" # (String, Optional) Mode responsible for executing the core logic of this step.

# --- Interface ---
inputs = [ # (Array of Strings, Optional) Data/artifacts needed. Can reference outputs from 'depends_on' steps.
    "Output from step WF-CREATE-ROO-CMD-BUILD-V1-00-START: build_params", # Contains platform, flags etc.
    "Output from step WF-CREATE-ROO-CMD-BUILD-V1-00B-SUGGEST-VERSION: confirmed_version", # The user-confirmed version string
    "File: .ruru/config/project.toml" # For GitHub owner and repo
]
outputs = [ # (Array of Strings, Optional) Data/artifacts produced by this step.
    "validated_build_params: The validated build parameters object, including confirmed version, github_owner, and github_repo_name.",
]

# --- Housekeeping ---
last_updated = "2025-08-05" # (String, Required) Date of last modification. Use placeholder.
template_schema_doc = ".ruru/templates/toml-md/25_workflow_step_standard.md" # (String, Required) Link to this template definition.
+++

# Step 01: Validate Build Parameters

## Actions

1.  **Receive Parameters:** Get the `build_params` object (containing platform, flags etc.) from step `00` and the `confirmed_version` string from step `00b`.
2.  **Read Project Configuration:** Read [`.ruru/config/project.toml`](.ruru/config/project.toml:1) to extract `github.owner` and `github.repo`.
3.  **Validate Version:** Check if the `confirmed_version` string follows semantic versioning rules (e.g., `vX.Y.Z`).
4.  **Validate Repository Info:**
    *   Check if `github.owner` (from `project.toml`) is a non-empty string.
    *   Check if `github.repo` (from `project.toml`) is a non-empty string.
5.  **Validate Platform:** Ensure the target platform(s) in `build_params` are supported/recognized.
6.  **Validate Flags:** Check any additional build flags in `build_params` for validity.
7.  **Prepare Output:** Create the `validated_build_params` object, merging the `confirmed_version` (as the `version` field), the extracted `github_owner` and `github_repo_name` (from `project.toml`), and other validated parameters from `build_params`.

## Acceptance Criteria

*   `build_params` and `confirmed_version` are received successfully.
*   GitHub owner and repo are successfully read from [`.ruru/config/project.toml`](.ruru/config/project.toml:1).
*   The `confirmed_version`, `github_owner` (from `project.toml`), `github_repo_name` (from `project.toml`), platform, and flags are confirmed as valid.
*   `validated_build_params` object (including `version`, `github_owner`, `github_repo_name` fields) is created for the next step.

## Error Handling

*   If any validation check fails (invalid `confirmed_version` format, failure to read or parse [`.ruru/config/project.toml`](.ruru/config/project.toml:1), missing/invalid `github_owner` or `github_repo_name` from `project.toml`, unsupported platform, etc.), proceed to `EE_handle_validation_error.md` (if defined).