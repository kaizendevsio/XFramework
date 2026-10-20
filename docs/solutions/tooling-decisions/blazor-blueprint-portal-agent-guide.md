---
title: "BlazorBlueprint Portal Agent Guide"
date: 2026-06-18
category: tooling-decisions
module: Portal
problem_type: tooling
component: blazorblueprint
severity: high
applies_when:
  - "Building, fixing, or reviewing Portal UI that uses BlazorBlueprint"
  - "Using Popover, Dropdown Menu, Dialog, Combobox, Select, Tabs, DataGrid, or form controls"
  - "Debugging Portal overlay, trigger, focus, styling, or render-mode behavior"
tags: [blazor, blazorblueprint, portal, ui, agents]
status: current
---

# BlazorBlueprint Portal Agent Guide

This guide is the local XFramework playbook for using BlazorBlueprint in the Portal. It captures the official setup, the current package version, and the component pitfalls that caused recent Portal regressions.

## Source Of Truth

- Current XFramework source wins when this document and code disagree.
- Official docs are the next source of truth:
  - Website docs: https://blazorblueprintui.com/docs/installation
  - Component examples: https://blazorblueprintui.com/docs/components
  - GitHub source: https://github.com/blazorblueprintui/ui
  - AI docs: https://blazorblueprintui.com/llms/index.txt
- The installed NuGet package is the final API check. Inspect XML/source before adding less-common parameters:

```powershell
$xml = "$env:USERPROFILE\.nuget\packages\blazorblueprint.components\4.1.0\lib\net10.0\BlazorBlueprint.Components.xml"
Select-String -Path $xml -Pattern 'BbDropdownMenuContent|BbPopoverContent|BbCombobox'
```

If the docs are ambiguous, open the official docs in the Codex built-in browser and visually inspect the example before implementing. BlazorBlueprint docs include live visual examples, and overlay behavior is easier to verify in the browser than from text alone.

## Portal Setup

The current Portal uses BlazorBlueprint Components/Primitives `4.1.0` and Lucide `2.0.2` (upgraded from Components/Primitives `3.16.0`):

- Package versions are pinned in `Directory.Packages.props`.
- `XFramework.Portal.csproj` references `BlazorBlueprint.Components` and `BlazorBlueprint.Icons.Lucide`.
- `Program.cs` calls `AddInteractiveServerComponents()` and `AddBlazorBlueprintComponents(...)`.
- `Components/_Imports.razor` imports `BlazorBlueprint.Components`, `BlazorBlueprint.Primitives`, `BlazorBlueprint.Primitives.Services`, `BlazorBlueprint.Icons.Lucide.Components`, and `BlazorBlueprint.Icons.Lucide.Data`.
- `Components/App.razor` loads `_content/BlazorBlueprint.Components/css/themes.css` and `_content/BlazorBlueprint.Components/blazorblueprint.css`.
- Portal's `css/app.css` is followed by generated `css/utilities.css`, then scoped styles. The utility build scans the host, shared UI, and feature RCL Razor/C# sources, without scanning Blueprint packages. Existing custom rules in `app.css` remain unchanged.
- `Components/App.razor` renders routes with `@rendermode="InteractiveServer"`.
- `Components/Layout/MainLayout.razor` includes `BbToastProvider`, `BbDialogProvider`, `XfContainerPortalHost`, and `BbOverlayPortalHost`. `XfPortalService` preserves BlazorBlueprint's public portal contract while giving the custom container host the changed portal ID, allowing it to queue only dialog/sheet refreshes whose content already rendered in the active cycle.

Do not remove the portal-host composition. Portal-based controls such as Popover, Dialog, Sheet, Dropdown Menu, Combobox, and Select require it. In `MainLayout`, retain `XfPortalService`, `XfContainerPortalHost`, and `BbOverlayPortalHost` until repeated dynamic-dialog browser tests verify the stock replacement. Version `4.1.0` contains upstream deferred refresh recovery, but that alone does not establish equivalence with the local workaround.

## v4 Upgrade Notes

Stable versions were checked against official NuGet on 2026-10-19: [Components 4.1.0](https://www.nuget.org/packages/BlazorBlueprint.Components/4.1.0), [Primitives 4.1.0](https://www.nuget.org/packages/BlazorBlueprint.Primitives/4.1.0), and [Lucide 2.0.2](https://www.nuget.org/packages/BlazorBlueprint.Icons.Lucide/2.0.2). The Components package records upstream commit `484e630073ff2861aebafc2093dd67bf0c8105f5`. Its bundled release notes still say "unreleased"; the published NuGet version is stable, not a prerelease.

Read the [v4 migration guide](https://github.com/blazorblueprintui/ui/blob/484e630073ff2861aebafc2093dd67bf0c8105f5/V4-MIGRATION-GUIDE.md) and [changelog](https://github.com/blazorblueprintui/ui/blob/484e630073ff2861aebafc2093dd67bf0c8105f5/CHANGELOG.md) before further migration:

- .NET 10 is required. Keep Components and Primitives versions aligned.
- Library utilities now use `bb:` and a separate cascade layer. Keep app utilities unprefixed, with explicit theme border color and class-based dark mode. Do not select Blueprint internals by their former utility names; prefer `data-*` attributes.
- Portal hosts now live in `BlazorBlueprint.Primitives`. Razor imports already include it; C# test fixtures that import only `.Services` need the root namespace too.
- Drawer trigger/close now render buttons; use `AsChild="true"` around Blueprint buttons. Tooltip triggers default to wrappers; explicitly set `AsChild` when the child consumes trigger context.
- Removed no-op calendar/command/chart parameters and changed primitive navigation registration are listed in the upstream guide. No matching Portal usages were found during the upgrade scan.
- Version 4.1 adds stricter date-time minute-step and dock-ID validation, changes modified-arrow tab handling, and renames the grid resize module to `table-columns.js`. Dialog/Popover `Modal` dismissal behavior also changed; repeat nested-overlay tests.
- Retain `XfPopoverClickOutside` and the controlled-tree reconciliation workaround pending focused browser verification. Upstream host registration now counts hosts, whereas the retained custom service uses a boolean; include layout disposal/remount in eventual replacement tests.
- XML documentation also includes internal APIs. `BbFormWizard.GoToNextAsync`, `GoToBackAsync`, `CompleteAsync`, `CanGoBack`, and `IsLastStep` are internal in 4.1.0. Use public controlled-step parameters/events or built-in navigation, not an `@ref` call to these members.

Regenerate only the separate utility bundle from `src/Presentation/XFramework.Portal`:

```powershell
npm ci --ignore-scripts
npm run build:css
```

Commit the generated `wwwroot/css/utilities.css` with its source and lockfile. Node is not required for ordinary .NET builds or deployment of the checked-in CSS. Standalone browser fixtures must serve and load `utilities.css` after `app.css`, just as `App.razor` does. The version assertions in `IdentityServerPortalContractTests.PortalContainerHost_QueuesAlreadyRenderedDialogRefreshes` deliberately require review when the package changes; advance them only with the corresponding compatibility verification.

Standalone interactive fixtures without the layout's theme controls must call `ThemeService.InitializeAsync()` after their first interactive render. Registration alone does not apply the configured theme. Assert the actual root theme attributes, radius, and rendered light/dark surface after initialization; static HTML attributes alone can hide incorrect fixture styling.

## Component Layering

Prefer the styled `BlazorBlueprint.Components` layer for Portal pages. Reach for `BlazorBlueprint.Primitives` only when a page needs custom behavior that the styled component does not expose.

Use these common styled components first:

- `BbButton` for commands.
- `BbCombobox` for searchable tenant, user, credential, and lookup selection.
- `BbSwitch` for boolean form values.
- `BbDatePicker`, `BbTimePicker`, or matching form field wrappers for date/time values.
- `BbDropdownMenu` for command/profile menus.
- `BbDialog` for modal forms.
- `BbTabs` only when the tab count is small and does not cause horizontal scrolling.
- `BbDataGrid` for list, report, and data-heavy tabular UI that needs paging, search, row click, sorting, filtering, or column templates. Prefer it over raw `<table>` markup and custom table components.

For `BbDataGrid`, enable native column filtering on useful user-facing data columns. Set `Filterable="true"` on property columns. For template columns, set both `Filterable="true"` and a `FilterBy` expression that matches the rendered business value. Do not make action/command columns filterable.

For icons, use Lucide components from `BlazorBlueprint.Icons.Lucide.Components`. Do not hand-draw common UI icons.

## Overlay And Trigger Rules

`Popover`, `DropdownMenu`, `Dialog`, `Combobox`, and `Select` are interactive, portal-based components. Before debugging their behavior, confirm all of these are true:

- The page or layout is interactive, not statically rendered.
- `BbPortalHost` exists in the rendered layout.
- The component content is not hidden by a local stacking context or `overflow: hidden`.
- The trigger renders an actual clickable element in the DOM.

`AsChild` is the most common source of broken trigger behavior.

- `BbDropdownMenuTrigger` defaults to `AsChild=true`.
- With `AsChild=true`, trigger behavior is passed through `TriggerContext` to a child component.
- Use `AsChild=true` only with child components that consume that context, such as BlazorBlueprint button components.
- Do not wrap a plain HTML `<button>` or bare markup with `AsChild=true` unless browser testing proves it receives the trigger behavior.
- If using plain markup, set `AsChild="false"` and avoid putting another `<button>` inside the trigger, because that creates nested buttons.

`BbPopover` root owns open state. Do not put content-dismissal parameters on the root. In v3.12.0, examples and source put these on `BbPopoverContent`:

- `CloseOnEscape`
- `CloseOnClickOutside`
- `Side`
- `Align`
- `Offset`
- `MatchTriggerWidth`
- `Strategy`
- `ZIndex`

`BbDropdownMenuContent` also owns dismissal and positioning options such as `CloseOnEscape`, `CloseOnClickOutside`, `Side`, `Align`, `Offset`, `Strategy`, `MatchTriggerWidth`, and `ZIndex`.

If a profile menu, tenant selector, or action menu fails to open or close, inspect the rendered DOM in the built-in browser before changing CSS. A missing trigger element, missing portal host, invalid parameter, or static render mode is more likely than a styling issue.

## Dialogs With Portaled Controls

The Dialog docs recommend `TrapFocus="false"` on `BbDialogContent` when putting overlay controls inside a dialog, including `DatePicker`, `Select`, and `Combobox`. Without that, the dialog focus trap can prevent the nested popover from opening correctly.

Use controlled dialog state (`@bind-Open`) for workflows where menu items open dialogs, because menu close, dialog open, and focus restoration can otherwise fight each other.

## Form Controls

Portal forms should use framework controls that match the data type:

- Boolean properties use `BbSwitch`, not a dropdown with `true` and `false`.
- Long option lists use `BbCombobox`, not native `<select>`.
- Short fixed options may use BlazorBlueprint `Select` or radio controls.
- Optional date/time values should use date/time components that allow clearing the value.
- GUIDs should usually be internal values behind a user-facing combobox label.

For `BbCombobox`, prefer typed `SelectOption<TValue>` options when the selection is simple. Use compositional `BbComboboxItem` only when the option rows need rich custom markup.

For parent dependency entities, use the shared `XfEntityPicker<TItem>` pattern instead of embedding prerequisite forms in the parent workflow. The picker trigger should provide quick search/select, an `Advanced Search` dialog, and a `Create New` dialog. For example, a product stock dialog should pick a warehouse/location/lot through entity pickers; warehouse and location creation belongs to the picker-owned create dialogs, not directly inside the stock form.

`Advanced Search` must be meaningfully advanced. Do not implement it as a larger copy of the quick dropdown. Configure entity-specific `AdvancedColumns` and `AdvancedSearchScope` so the dialog shows a sortable multi-column `BbDataGrid` finder. Use the grid's native column filters (`Filterable` and `FilterBy`) instead of redundant top-of-dialog filter bands. The operator should be able to tell what is being searched and filtered, for example warehouse code/name/location/default status, lot number/status/expiry/on-hand quantity, or supplier code/name/contact/active status.

## Operational Page Layout

For module admin surfaces such as Inventario, keep list and detail workflows distinct:

- List pages are for scanning, filtering, creating small records, and navigating to detail pages.
- List and report pages should use `BbDataGrid` for tabular records and should enable native filters on the business columns users naturally search by. Keep command/action columns unfiltered.
- Viewing and editing existing records should happen on the detail page with a clear edit mode, not in list-page edit modals.
- Detail pages should show the operational context users need for that entity. For example, product detail should include catalog fields, replenishment rules, stock balances by warehouse/location/lot, and traceability records when those features are enabled.
- Header actions belong on the far right of the page header. Use the shared `xf-page-header` and `xf-page-actions` classes instead of hand-assembling inconsistent flex utility combinations.
- Card headers that combine a title with search/filter controls should use `xf-page-header` plus `xf-filter-actions`; do not assume arbitrary responsive width utilities such as `md:w-[28rem]` are available in the compiled app CSS.
- Summary metrics should use the shared `xf-summary-grid` classes. Do not rely on new responsive Tailwind utility class names until the compiled `wwwroot/css/app.css` is verified to include them on xeon-dev.
- Avoid card-in-card summary layouts unless the surrounding component is a true detail surface. Dense operational pages should use stable grids and tables that do not collapse to one full-width row on desktop.

## Visual Verification Workflow

Use the Codex built-in browser for BlazorBlueprint work that touches interactive UI:

1. Open the relevant official docs page and inspect the live example.
2. Implement using the documented component shape.
3. Build the Portal.
4. Open the changed Portal page in the built-in browser.
5. Click the trigger or control directly.
6. Verify open/close behavior, outside click, Escape handling, keyboard focus, z-index, width, and mobile/narrow layout.

For overlays inside dialogs, verify the nested overlay in both light and dark themes. For data grids, verify row hover/click styling and keyboard focus do not produce the old strong blue row border unless the design specifically calls for it.

## Known Local Pitfalls

- The profile switcher once used `CloseOnClickOutside` and `CloseOnEscape` on `BbPopover`; that caused runtime failures because those options belong on content components, not the `BbPopover` root.
- BlazorBlueprint `3.12.0`'s category portal host ignores refresh notifications received while it is rendering. `XfPortalService` and `XfContainerPortalHost` queue a follow-up render only when the changed dialog/sheet portal already rendered in the current cycle; `MainLayout` retains the stock overlay host for popovers and selects. The Portal contract intentionally fails when the package version changes so this compatibility layer is reviewed instead of becoming permanent infrastructure.
- A profile trigger using `AsChild=true` with plain HTML markup did not open because the raw child did not consume `TriggerContext`. Prefer documented trigger examples and verify the rendered DOM.
- `LoginLayout` is intentionally plain. If a login page starts using Blueprint overlays, toasts, dialogs, or services, add the required providers or an interactive island.
- Native OS dropdowns look out of place in Portal. Use BlazorBlueprint Combobox/Select components for tenant and entity selection.

## Useful Local Checks

For controlled `BbTreeView` checks in Blueprint 3.16.0, rejecting or cancelling an optimistic change can leave the displayed check different from the saved value: the primitive skips syncing values equal to its previously supplied parameter. Tenant Modules remounts the tree only when the attempted checked set differs from the authoritative result. Keep search, expanded groups, and selection controlled so this reconciliation preserves the user's context. Browser regression tests must cover cancel and blocked-disable flows, not just successful saves.

The built-in tree search indexes data-driven `Items`, not declarative `BbTreeItem` content. For declarative trees, use an external Blueprint input, filter rendered items, and expand ancestors of matching children while preserving the pre-search expanded set. Otherwise restoring a nonempty search on remount can collapse every group and hide the corrected checkbox.

```powershell
rg -n "BlazorBlueprint|BbPortalHost|BbToastProvider|BbDialogProvider|AddBlazorBlueprint|@rendermode|Interactive" `
  Directory.Packages.props src/Presentation/XFramework.Portal

dotnet build src/Presentation/XFramework.Portal/XFramework.Portal.csproj -m:1 /nr:false
```

When changing component parameters, inspect both official source and NuGet XML. The website docs show visual behavior, while the local package determines what the project can compile.
