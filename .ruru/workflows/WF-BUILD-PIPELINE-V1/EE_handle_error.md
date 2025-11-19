+++
# --- Step Metadata ---
step_id = "WF-BUILD-PIPELINE-V1-EE-HANDLE_ERROR"
title = "Error Handling for WF-BUILD-PIPELINE-V1"
description = """
Handles errors encountered during the WF-BUILD-PIPELINE-V1 workflow execution.
This step is invoked when a preceding step sets its 'next_step' to this file
or when the workflow context 'status' is 'error' and 'current_step_id' points here.
"""

# --- Flow Control ---
depends_on = [] # Can be triggered from any step
next_step = "99_finish.md" # Typically, error handling leads to finishing the workflow
error_step = "99_finish.md" # If error handling itself fails, go to finish
is_error_handler = true

# --- Execution ---
delegate_to = "" # Error handling logic is often managed by the Workflow Manager or a coordinator

# --- Interface ---
inputs = [
    "workflow_context_file_path: Path to the main workflow context file.",
    "error_details: (from workflow_context.md, populated by the failing step)"
]
outputs = [
    "error_handling_status: Outcome of the error handling attempt (e.g., \"logged\", \"manual_intervention_required\", \"recovery_attempted\")."
]

# --- Housekeeping ---
last_updated = "2025-10-04T22:58:04Z" # Current UTC time
template_schema_doc = ".ruru/templates/toml-md/26_workflow_step_error_handler.md" # Assuming a template for error handlers
+++

# Error Handling for WF-BUILD-PIPELINE-V1

## Actions

1.  **Log Error Details:**
    *   `- [ ]` Read the `error_details` array from the `workflow_context_file_path`.
    *   `- [ ]` Ensure comprehensive logging of the error(s) to the main MDTM task log (`TASK-ROOCMD-20251005-073100`) and the session log.
2.  **Notify Coordinator:**
    *   `- [ ]` Prepare a summary of the error and the current workflow state.
    *   `- [ ]` This step (being executed by `util-workflow-manager`) will report back to the main coordinator (`roo-commander` or `prime-coordinator` as per `TASK-ROOCMD-20251005-073100`'s coordinator field).
3.  **Determine Next Steps (Simple Default):**
    *   `- [ ]` For this basic handler, set `error_handling_status` to "logged_manual_intervention_required".
    *   `- [ ]` The workflow will proceed to `99_finish.md` to formally conclude with an error status.
4.  **Update Workflow Context:**
    *   `- [ ]` Update the `workflow_context_file_path`'s `status` to "error_handled" (or a similar terminal error state).
    *   `- [ ]` Set `current_step_id` to `99_finish.md`.
    *   `- [ ]` Add `error_handling_status` to `context_data`.

## Acceptance Criteria

*   Error details from the workflow context are logged.
*   The coordinator is notified of the error state.
*   `error_handling_status` is set.
*   Workflow context is updated to proceed to the finish step with an error status.

## Error Handling
*   If logging or context updates fail, this step itself has failed. The workflow will proceed to `99_finish.md` regardless.