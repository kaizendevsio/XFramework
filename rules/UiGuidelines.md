# UI Guidelines

This is the primary UI rules entrypoint for XFramework agents. Read this file before changing ControlPanel or Blazor UI.

## Authority

- Current source code wins when docs and implementation disagree.
- This file owns agent-facing UI rules. Do not create parallel UI guidance elsewhere unless explicitly requested.
- For exact BlazorBlueprint component APIs, setup details, overlay pitfalls, and visual verification workflow, use [BlazorBlueprint ControlPanel Agent Guide](../docs/solutions/tooling-decisions/blazor-blueprint-controlpanel-agent-guide.md).
- For ControlPanel business operations, wrapper vs `IDataContext` decisions, and UI write-path tests, use [ControlPanel Service Wrapper And Integration Test Contract](../docs/solutions/developer-experience/controlpanel-service-wrapper-and-integration-test-contract.md).
- Before using newer or less-common BlazorBlueprint parameters/components, inspect the installed NuGet XML because website docs can lead the pinned package version.

```powershell
$xml = "$env:USERPROFILE\.nuget\packages\blazorblueprint.components\3.12.0\lib\net8.0\BlazorBlueprint.Components.xml"
Select-String -Path $xml -Pattern 'BbDataGrid|BbDynamicForm|BbFormWizard|BbFilterBuilder|DialogOpenOptions|ToastData'
```

## ControlPanel Baseline

- Preserve BlazorBlueprint setup: `AddInteractiveServerComponents`, `AddBlazorBlueprintComponents`, `_content/BlazorBlueprint.Components` CSS, `InteractiveServer` render mode, and shared imports.
- Preserve `BbToastProvider`, `BbDialogProvider`, and `BbPortalHost` in layouts that use portal-based controls.
- Prefer styled `BlazorBlueprint.Components` controls. Use `BlazorBlueprint.Primitives` only when the styled component cannot support the workflow.
- Use Lucide icon components from `BlazorBlueprint.Icons.Lucide.Components`; do not hand-draw common UI icons.

## BlazorBlueprint Component Matrix

Before creating custom UI, check the installed BlazorBlueprint component surface and use the closest existing component.

| Need | Prefer |
|---|---|
| Commands and action groups | `BbButton`, `BbButtonGroup`, `BbSplitButton` |
| Binary toggle action | `BbToggle`, `BbToggleGroup` |
| Boolean form value | `BbSwitch`, `BbCheckbox`, `BbFormFieldSwitch`, `BbFormFieldCheckbox` |
| Text entry | `BbInput`, `BbInputField`, `BbFormFieldInput`, `BbTextarea`, `BbFormFieldTextarea` |
| Numeric, money, formatted entry | `BbNumericInput<TValue>`, `BbCurrencyInput`, `BbMaskedInput` and matching form-field wrappers |
| Single choice | `BbSelect`, `BbNativeSelect`, `BbRadioGroup`, `BbCombobox` |
| Multiple choice | `BbMultiSelect<TValue>`, `BbCheckboxGroup`, `BbTagInput` and matching form-field wrappers |
| Date and time | `BbDatePicker`, `BbDateRangePicker`, `BbTimePicker`, `BbCalendar` and matching form-field wrappers |
| Files, colors, ratings, ranges | `BbFileUpload`, `BbColorPicker`, `BbRating`, `BbSlider`, `BbRangeSlider` |
| Entity dependency selection | Shared `XfEntityPicker<TItem>` |
| Tabular operations | `BbDataGrid<TItem>` |
| List/grid toggle views | `BbDataView<TItem>` |
| Complex filtering | `BbFilterBuilder` with `FilterDefinition` and `FilterField` |
| Empty/loading/progress states | `BbEmpty`, `BbSkeleton`, `BbSpinner`, `BbProgress` |
| Status and metadata display | `BbBadge`, `BbAlert`, `BbAvatar`, `BbTypography`, `BbTimeline`, `BbItem`, `BbKbd` |
| Navigation | `BbBreadcrumb`, `BbPagination`, `BbSidebar`, `BbNavigationMenu`, `BbResponsiveNav`, `BbTabs`, `BbTreeView` |
| Layout | `BbCard`, `BbAccordion`, `BbCollapsible`, `BbResizable`, `BbScrollArea`, `BbSeparator`, `BbAspectRatio`, `BbDashboardGrid` |
| Overlays | `BbDialog`, `BbAlertDialog`, `BbSheet`, `BbDrawer`, `BbPopover`, `BbDropdownMenu`, `BbContextMenu`, `BbHoverCard`, `BbTooltip`, `BbCommand` |
| Notifications and confirms | `ToastService`, `DialogService`, `BbToastProvider`, `BbDialogProvider` |
| Rich content editing | `BbRichTextEditor`; use only where rich text is an actual domain requirement |
| Theme controls | `BbThemeSwitcher`, `BbDarkModeToggle`, `ThemeService` |

## Models And API Conventions

- Use BlazorBlueprint model types instead of ad hoc DTOs when configuring Blueprint controls.
- Use `SelectOption<TValue>` for simple `Combobox`, `Select`, `MultiSelect`, checkbox-group, and radio options.
- Use `FormSchema`, `FormSectionDefinition`, `FormFieldDefinition`, `FieldType`, and `FieldValidation` only for schema-driven forms.
- Use `FormFieldChangedEventArgs` for dynamic-form field change handling.
- Use `BbFormWizard` with `BbWizardStep` for multi-step workflows.
- Use `DataGridState`, `DataGridRequest`, and `DataGridResult` when controlling or server-loading a `BbDataGrid`.
- Use `FilterDefinition` and `FilterField` when building visual advanced filters with `BbFilterBuilder`.
- Use `ToastData` for structured toast behavior such as actions, duration, position, countdown, or semantic variant.
- Use `DialogOpenOptions`, `ConfirmDialogOptions`, `PromptDialogOptions`, and `AlertDialogOptions` for programmatic dialogs.
- Use `ThemeOptions`, `ThemeService`, `BaseColor`, and `PrimaryColor` only for app-level theme configuration or theme controls.

## Data Grids

- Use `BbDataGrid` for list, report, finder, and data-heavy tabular UI.
- Do not create raw HTML tables or custom table components unless `BbDataGrid` cannot support the workflow. Document the exception in the change.
- Enable native column filtering on useful user-facing data columns.
- Use `Filterable="true"` on property columns.
- Use both `Filterable="true"` and `FilterBy` on template columns, with `FilterBy` matching the rendered business value.
- Do not filter command/action columns.
- Use `ShowPagination` and a reasonable `InitialPageSize` for list and report surfaces.
- Add `OnRowClick` for grids whose rows have detail pages. Keep action buttons responsible for their own click behavior.
- Use `ItemsProvider` for large or server-loaded grids instead of materializing full datasets on the page.
- Use `IsLoading`, `LoadingTemplate`, and `EmptyTemplate` instead of external ad hoc loading/empty markup when the state belongs to the grid.
- Use `DetailTemplate` or `BbDataGridExpandColumn` for expandable row detail instead of nested cards below the grid.
- Use `BbDataGridGroupColumn` for grouping, `BbDataGridColumnVisibility` for user-controlled column visibility, and `BbDataGridHierarchyColumn` for hierarchical data.
- Use `Virtualize`, `ItemSize`, and `VirtualScrollHeight` for large client-side lists where virtualization is appropriate.
- Use `BbDataView<TItem>` instead of `BbDataGrid<TItem>` only when the UX is truly a list/card grid with a layout toggle rather than an operational table.
- Use `BbFilterBuilder` for advanced multi-condition report/search filters; use native grid column filters for ordinary column filtering.

## Forms And Controls

- Match controls to data type: `BbSwitch` for booleans, `BbCombobox` for long lookup lists, Blueprint `Select` or radio controls for short fixed option sets, and date/time components for date/time values.
- Use `BbFormField*` wrappers when a field needs label, description, validation, or consistent form layout. Use bare inputs only for custom composition.
- Use `BbNumericInput<TValue>` for numeric quantities, thresholds, page sizes, counts, and percentages.
- Use `BbCurrencyInput` for money amounts; do not use plain text inputs plus manual formatting.
- Use `BbMaskedInput` for structured identifiers, phone numbers, postal codes, or other fixed-format strings when the mask is known.
- Use `BbDateRangePicker` for reports and filters that naturally work over a date range.
- Use `BbMultiSelect<TValue>` or `BbCheckboxGroup` for multiple selections; do not overload a single-select combobox.
- Use `BbTagInput` for user-managed labels, keywords, or tags.
- Use `BbInputOTP` only for OTP/verification-code entry.
- Use `BbFileUpload` for uploads so drag/drop, preview, and progress behavior stay consistent.
- GUIDs should usually be hidden behind user-facing labels in comboboxes or entity pickers.
- Avoid native OS dropdowns in ControlPanel unless the Blueprint control cannot support the workflow.
- Use typed `SelectOption<TValue>` options for simple `BbCombobox` selections. Use compositional combobox items only for rich option rows.
- For dialogs launched from menus or other overlays, use controlled state with `@bind-Open`.
- For overlays inside dialogs, set `TrapFocus="false"` on `BbDialogContent` when required by the nested control behavior.
- For modal/dialog forms with many fields or tall content, use a responsive two-column field layout on desktop instead of stacking everything in one long column. Keep one column on narrow/mobile widths.

## Schema Forms And Wizards

- Use strongly typed request/view models for normal business workflows.
- Use `BbDynamicForm` only for schema-driven admin/configuration experiences where fields are genuinely data-defined.
- When using `BbDynamicForm`, define fields with `FormSchema`, `FormSectionDefinition`, `FormFieldDefinition`, `FieldType`, `FieldValidation`, and `SelectOption<TValue>` instead of custom schema objects.
- Use `FieldType.Custom` plus `FieldRenderers` only when the built-in field types cannot represent the control.
- Use `BbFormWizard` with `BbWizardStep` for multi-step flows. Prefer step-level validation and explicit `FieldNames` over one large final validation surprise.
- Do not use dynamic forms or wizards to bypass endpoint validators, request contracts, or wrapper-first workflow rules.

## Entity Pickers

- Use the shared `XfEntityPicker<TItem>` pattern for dependency entities such as warehouse, location, lot, supplier, purchase order, wallet, credential, and payment gateway.
- Dependency creation belongs in picker-owned `Create New` dialogs, not inside parent workflow forms.
- Picker `Advanced Search` must be materially advanced. Configure domain-specific `AdvancedColumns`, `AdvancedSearchScope`, sorting, and grid column filters.
- Do not implement advanced search as a larger copy of a quick text dropdown.

## Layout And Workflow

- List pages are for scanning, filtering, creating small records, and navigating to detail pages.
- List pages must not embed create or update forms as cards/sections above, below, or beside the grid. Provide a visible create action button that opens a focused dialog/sheet or navigates to a dedicated create page.
- Existing-record edit workflows belong on detail pages with a clear edit mode, not list-page edit modals.
- Detail pages should show the operational context needed for the entity.
- If a detail page grows beyond a few scannable sections, add a local detail sidebar/navigation and split major sections into dedicated detail subpages or routes. Do not force many unrelated sections into one long scrolling page.
- Product-specific inventory workflows should keep operators on `/inventario/products/{id}` or a detail-sidebar route when practical.
- Use shared layout classes instead of ad hoc flex/responsive utility combinations:
  - `xf-page-header`
  - `xf-page-actions`
  - `xf-filter-actions`
  - `xf-summary-grid` and its numbered variants
  - `xf-detail-field-grid` and `xf-detail-field-grid-wide`
- Avoid card-in-card summary layouts unless the surrounding component is a true detail surface.

## Standard Page Recipes

- List page: `xf-page-header` with title and `xf-page-actions`, optional `xf-filter-actions`, `BbDataGrid` with useful `Filterable` columns, pagination, row click when detail exists, and visible create/action buttons. Do not render create/update forms inline on the list page; launch create from a button and route updates to detail/edit surfaces.
- Detail page: stable header, summary/metrics in `xf-summary-grid`, inline edit through `EditableForm` when suitable, operational sections in grids/tabs, and wrapper-backed commands for business actions. When sections become too many or too tall, use a detail sidebar/nav and move sections to subpages/routes.
- Report page: date/range and entity filters using Blueprint controls or `XfEntityPicker`, optional `BbFilterBuilder` for complex conditions, `BbDataGrid` or `BbDataView`, export/actions in the header, and explicit empty/loading states.
- Picker dialog: quick search in the trigger popover, advanced search in a `BbDialog` with `BbDataGrid`, domain-specific columns, `Filterable`/`FilterBy`, and optional picker-owned create dialog.
- Destructive action: use `DialogService.Confirm` or `BbAlertDialog`, use destructive button styling, describe the exact consequence, then call the wrapper/service and show a semantic toast.
- Side-panel workflow: use `BbSheet` or `BbDrawer` when users need to keep page context visible; use `BbDialog` for focused modal forms.
- Modal form: use one column for short forms, two desktop columns for longer forms, clear section grouping when fields span different concerns, and full-width rows only for textarea, rich text, upload, grid, or complex picker content.

## Anti-Patterns

- Do not create raw `<table>` markup for data-heavy UI.
- Do not build custom table, modal, popover, toast, tooltip, dropdown, or confirmation components when BlazorBlueprint has a component or service for it.
- Do not use native `<select>` for tenant, user, credential, entity, or long domain lookup selection.
- Do not show GUIDs as primary user-facing labels.
- Do not invent schema/configuration models when BlazorBlueprint already provides `FormSchema`, `FormFieldDefinition`, `FilterDefinition`, `FilterField`, `ToastData`, or dialog option models.
- Do not place create or update forms in cards above list grids. Lists should expose actions, not permanently embedded mutation forms.
- Do not put existing-record edits in list-page modals.
- Do not put required information behind hover-only UI.
- Do not hard-code third-party component chrome text when `IBbLocalizer` can override it.
- Do not introduce arbitrary responsive Tailwind utility classes without verifying they exist in the compiled ControlPanel CSS.
- Do not expose raw exception details, SQL, stack traces, tokens, or PII in UI toasts, alerts, or validation messages.

## Loading And Editable Detail UI

- Do not leave bare `"Loading..."` text in new or touched ControlPanel UI.
- Use the shared `CenteredSpinner` or a BlazorBlueprint spinner/skeleton pattern that matches the surrounding page.
- Use `BbEmpty` for no-data states that need a clear message and optional action.
- Use `BbAlert` for persistent warnings or contextual messages; use `ToastService` for temporary result feedback.
- Use `BbProgress` for visible long-running operations with measurable progress.
- Use existing `EditableForm`, `EditableField`, and `EditableFieldType` helpers for simple inline detail editing where they fit.
- Avoid duplicate edit dialogs that repeat the same fields already shown in the detail summary.

## Data Access From UI

- Use module service wrapper methods for business operations and custom endpoints.
- Do not expect generated per-entity CRUD members on service wrappers. Generic entity query UI should use `IDataContext`; business wrapper calls should stay wrapper calls.
- Use `IDataContext.Query<T>()` for read/query UI when the entity supports remote data-context querying.
- Use `IDataContext.Add`, `Update`, and `Remove` only for simple generated CRUD on entities explicitly allowlisted for remote mutation and only when no richer wrapper/request contract exists.
- Do not use direct `IDataContext.SaveChangesAsync()` to bypass validators, feature gates, tenant derivation, idempotency, ledger/allocation logic, or status transitions.
- Add or update tests for the exact UI write path used.

## Overlay Rules

- Before debugging Popover, Dropdown Menu, Dialog, Combobox, or Select behavior, confirm the page is interactive, `BbPortalHost` exists, content is not clipped by `overflow: hidden`, and the trigger renders a clickable DOM element.
- Use `BbAlertDialog` or `DialogService.Confirm` for destructive confirmation. Do not build one-off confirmation modals.
- Use `BbSheet` or `BbDrawer` for side-panel workflows that supplement the current page without losing context.
- Use `BbTooltip` for short affordance hints and `BbHoverCard` for richer previews. Do not put required information behind hover-only UI.
- Use `BbContextMenu` only for secondary contextual actions; keep primary actions visible.
- Use `BbCommand` for command-palette or quick-pick experiences.
- Use `AsChild=true` only with child components that consume BlazorBlueprint trigger context. For plain markup, set `AsChild="false"`.
- Put dismissal and positioning options on content components when BlazorBlueprint expects them there.
- Inspect the rendered DOM in the browser before changing CSS for broken trigger, focus, or z-index behavior.

## Toasts, Dialogs, Theme, And Localization

- Use `ToastService.Success`, `Info`, `Warning`, or `Error` for semantic temporary feedback.
- Use `ToastData` when a toast needs action text, custom duration, countdown, compact size, custom position, or icon behavior.
- Keep toast text concise and user-facing; do not expose exception details directly.
- Use `DialogService.AlertAsync`, `Confirm`, `PromptAsync`, or `OpenAsync` for programmatic dialogs that do not need local component state.
- Use `DialogOpenOptions` for custom dialog title, description, size, close behavior, and custom component dialogs.
- Preserve existing `BbThemeSwitcher`, `BbDarkModeToggle`, and `ThemeService` setup. Do not add parallel theme state.
- Use `IBbLocalizer`/BlazorBlueprint localization overrides for component chrome instead of hard-coded replacements inside third-party component internals.

## Verification

- Build the relevant ControlPanel project after UI changes:

```powershell
dotnet build src/Presentation/ControlPanel.Server/ControlPanel.Server.csproj -m:1 /nr:false
```

- Browser-smoke user-facing UI changes.
- Verify accessibility basics: every form control has a label or accessible name, validation messages are visible and associated with the field, focus returns after overlay close, keyboard navigation works, and destructive actions are confirmable.
- For overlays, verify open/close behavior, outside click, Escape handling, focus, z-index, trigger width, and narrow layout.
- For dialogs with nested overlays, verify the nested overlay in both light and dark themes.
- For modal forms, verify long forms do not become unnecessarily tall on desktop and still collapse cleanly to one column on mobile/narrow widths.
- For grids, verify filtering, sorting where relevant, row hover/click styling, keyboard focus, and mobile/narrow layout.
- For forms, verify required fields, invalid values, disabled/read-only states, submit loading state, save/discard behavior, and error feedback.
- For entity pickers, verify quick search, advanced search columns, column filtering, row selection, clear behavior, create flow if enabled, and keyboard focus.
- For loading and empty states, verify the loading indicator does not shift layout badly and the empty state gives a useful next action where appropriate.
- For theme-sensitive UI, verify light and dark themes when adding custom classes or colors.

## Useful Searches

```powershell
rg -n "BlazorBlueprint|BbPortalHost|BbToastProvider|BbDialogProvider|AddBlazorBlueprint|@rendermode|Interactive" Directory.Packages.props src/Presentation/ControlPanel.Server
rg -n "<table\b|<BbDataGrid\b|Filterable=|FilterBy=|OnRowClick=" src/Presentation/ControlPanel.Server
rg -n "DataContext\.(Add|Update|Remove)|SaveChangesAsync\(" src/Presentation/ControlPanel.Server/Components/Pages
rg -n "BbDynamicForm|FormSchema|FormFieldDefinition|FilterDefinition|ToastData|DialogOpenOptions|<select\b|<table\b" src/Presentation/ControlPanel.Server
```
