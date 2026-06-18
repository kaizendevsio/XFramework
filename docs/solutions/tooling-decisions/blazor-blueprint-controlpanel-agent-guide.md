---
title: "BlazorBlueprint ControlPanel Agent Guide"
date: 2026-06-18
category: tooling-decisions
module: ControlPanel
problem_type: tooling
component: blazorblueprint
severity: high
applies_when:
  - "Building, fixing, or reviewing ControlPanel UI that uses BlazorBlueprint"
  - "Using Popover, Dropdown Menu, Dialog, Combobox, Select, Tabs, DataGrid, or form controls"
  - "Debugging ControlPanel overlay, trigger, focus, styling, or render-mode behavior"
tags: [blazor, blazorblueprint, controlpanel, ui, agents]
status: current
---

# BlazorBlueprint ControlPanel Agent Guide

This guide is the local XFramework playbook for using BlazorBlueprint in the ControlPanel. It captures the official setup, the current package version, and the component pitfalls that caused recent ControlPanel regressions.

## Source Of Truth

- Current XFramework source wins when this document and code disagree.
- Official docs are the next source of truth:
  - Website docs: https://blazorblueprintui.com/docs/installation
  - Component examples: https://blazorblueprintui.com/docs/components
  - GitHub source: https://github.com/blazorblueprintui/ui
  - AI docs: https://blazorblueprintui.com/llms/index.txt
- The installed NuGet package is the final API check. Inspect XML/source before adding less-common parameters:

```powershell
$xml = "$env:USERPROFILE\.nuget\packages\blazorblueprint.components\3.12.0\lib\net8.0\BlazorBlueprint.Components.xml"
Select-String -Path $xml -Pattern 'BbDropdownMenuContent|BbPopoverContent|BbCombobox'
```

If the docs are ambiguous, open the official docs in the Codex built-in browser and visually inspect the example before implementing. BlazorBlueprint docs include live visual examples, and overlay behavior is easier to verify in the browser than from text alone.

## ControlPanel Setup

The current ControlPanel uses BlazorBlueprint `3.12.0`:

- Package versions are pinned in `Directory.Packages.props`.
- `ControlPanel.Server.csproj` references `BlazorBlueprint.Components` and `BlazorBlueprint.Icons.Lucide`.
- `Program.cs` calls `AddInteractiveServerComponents()` and `AddBlazorBlueprintComponents(...)`.
- `Components/_Imports.razor` imports `BlazorBlueprint.Components`, `BlazorBlueprint.Primitives`, `BlazorBlueprint.Primitives.Services`, `BlazorBlueprint.Icons.Lucide.Components`, and `BlazorBlueprint.Icons.Lucide.Data`.
- `Components/App.razor` loads `_content/BlazorBlueprint.Components/css/themes.css` and `_content/BlazorBlueprint.Components/blazorblueprint.css`.
- `Components/App.razor` renders routes with `@rendermode="InteractiveServer"`.
- `Components/Layout/MainLayout.razor` includes `BbToastProvider`, `BbDialogProvider`, and `BbPortalHost`.

Do not remove `BbPortalHost`. The official installation docs state it is required for portal-based controls such as Popover, Dialog, Sheet, Dropdown Menu, Combobox, and Select.

## Component Layering

Prefer the styled `BlazorBlueprint.Components` layer for ControlPanel pages. Reach for `BlazorBlueprint.Primitives` only when a page needs custom behavior that the styled component does not expose.

Use these common styled components first:

- `BbButton` for commands.
- `BbCombobox` for searchable tenant, user, credential, and lookup selection.
- `BbSwitch` for boolean form values.
- `BbDatePicker`, `BbTimePicker`, or matching form field wrappers for date/time values.
- `BbDropdownMenu` for command/profile menus.
- `BbDialog` for modal forms.
- `BbTabs` only when the tab count is small and does not cause horizontal scrolling.
- `BbDataGrid` for data-heavy tables that need paging, search, row click, or column templates.

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

ControlPanel forms should use framework controls that match the data type:

- Boolean properties use `BbSwitch`, not a dropdown with `true` and `false`.
- Long option lists use `BbCombobox`, not native `<select>`.
- Short fixed options may use BlazorBlueprint `Select` or radio controls.
- Optional date/time values should use date/time components that allow clearing the value.
- GUIDs should usually be internal values behind a user-facing combobox label.

For `BbCombobox`, prefer typed `SelectOption<TValue>` options when the selection is simple. Use compositional `BbComboboxItem` only when the option rows need rich custom markup.

## Visual Verification Workflow

Use the Codex built-in browser for BlazorBlueprint work that touches interactive UI:

1. Open the relevant official docs page and inspect the live example.
2. Implement using the documented component shape.
3. Build the ControlPanel.
4. Open the changed ControlPanel page in the built-in browser.
5. Click the trigger or control directly.
6. Verify open/close behavior, outside click, Escape handling, keyboard focus, z-index, width, and mobile/narrow layout.

For overlays inside dialogs, verify the nested overlay in both light and dark themes. For data grids, verify row hover/click styling and keyboard focus do not produce the old strong blue row border unless the design specifically calls for it.

## Known Local Pitfalls

- The profile switcher once used `CloseOnClickOutside` and `CloseOnEscape` on `BbPopover`; that caused runtime failures because those options belong on content components, not the `BbPopover` root.
- A profile trigger using `AsChild=true` with plain HTML markup did not open because the raw child did not consume `TriggerContext`. Prefer documented trigger examples and verify the rendered DOM.
- `LoginLayout` is intentionally plain. If a login page starts using Blueprint overlays, toasts, dialogs, or services, add the required providers or an interactive island.
- Native OS dropdowns look out of place in ControlPanel. Use BlazorBlueprint Combobox/Select components for tenant and entity selection.

## Useful Local Checks

```powershell
rg -n "BlazorBlueprint|BbPortalHost|BbToastProvider|BbDialogProvider|AddBlazorBlueprint|@rendermode|Interactive" `
  Directory.Packages.props src/Presentation/ControlPanel.Server

dotnet build src/Presentation/ControlPanel.Server/ControlPanel.Server.csproj -m:1 /nr:false
```

When changing component parameters, inspect both official source and NuGet XML. The website docs show visual behavior, while the local package determines what the project can compile.
