---
title: "Control Panel Editable UI Patterns"
date: 2026-03-30
category: design-patterns
module: ControlPanel.Server
problem_type: design_pattern
component: documentation
severity: low
applies_when:
  - "Designing Blazor control-panel list loading states, row navigation, and inline editable detail forms"
tags: [control-panel, blazor, ui, forms, loading]
---

# Control Panel UI Improvements — Design Spec

**Date:** 2026-03-30
**Scope:** Loading spinners, row-click navigation, inline edit mode with form state tracking

---

## 1. Loading Spinners

### Current State
Only 4 pages have `_loading` state — they show plain text `"Loading..."`. Other pages show empty grids while data loads.

### Design
- Add `_loading` bool to every page that loads data in `OnInitializedAsync`
- Replace all loading text with a centered `BbSpinner`:

```razor
@if (_loading)
{
    <div class="flex items-center justify-center py-12">
        <BbSpinner Size="SpinnerSize.Large" />
    </div>
}
else
{
    @* page content *@
}
```

- The spinner sits centered within the content area where the component would render
- Pages affected: all list pages + detail pages (~20 pages)

---

## 2. Row-Click Navigation

### Current State
Users must click the "eye" icon button in the Actions column to navigate to detail pages.

### Design
- Add `OnRowClick` to every `BbDataGrid` that has a detail page
- Row click navigates to the entity's detail page
- Action buttons (delete, freeze, etc.) naturally handle their own click events — Blazor's event handling means the button's `OnClick` fires and the row click doesn't interfere

```razor
<BbDataGrid Items="@_tenants"
            OnRowClick="@(item => Navigation.NavigateTo($"/identity/tenants/{item.Id}"))"
            Class="cursor-pointer">
```

### Affected Grids
| List Page | Detail Route |
|-----------|-------------|
| Tenants | `/identity/tenants/{Id}` |
| Users | `/identity/users/{Id}` |
| Wallets | `/finance/wallets/{Id}` |

Other list pages (Roles, Sessions, AuthLogs, Contacts, Addresses) don't have detail pages — no row click for those.

---

## 3. Inline Edit Form

### Problem
Detail pages currently have an "Edit" button that opens a **modal dialog** with a duplicate form. This means:
- Field definitions are duplicated (once in summary card, once in dialog)
- No dirty state tracking
- Modal obscures the context the user is editing

### Design: `EditableForm<TModel>` + `EditableField`

Two new shared components that make detail page summary cards toggle between read-only display and inline editing.

### `EditableForm<TModel>`

A wrapper component that manages edit lifecycle.

**Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| `Model` | `TModel` | The model being viewed/edited |
| `OnSave` | `EventCallback` | Called when user clicks Save (parent does the actual DB write) |
| `Saving` | `bool` | Whether a save is in progress (disables buttons) |
| `ChildContent` | `RenderFragment` | Fields wrapped by this form |

**Cascaded State (via `EditableFormContext`):**
| Property | Type | Description |
|----------|------|-------------|
| `IsEditing` | `bool` | Whether the form is in edit mode |
| `IsDirty` | `bool` | Whether any field value differs from the snapshot |

**Behavior:**
1. **Read mode** (default): Shows "Edit" button in top-right. Fields render as plain text.
2. **User clicks Edit**: Takes a JSON snapshot of `Model`. Switches to edit mode. Button bar changes to "Save" (disabled until dirty) + "Discard Changes".
3. **User edits fields**: `IsDirty` recomputes by comparing current model to snapshot (via `System.Text.Json` serialization comparison).
4. **Save**: Calls `OnSave`. Parent does the DB write. On success, parent sets `IsEditing = false` (or the component does via a `SaveCompleted` callback).
5. **Discard**: Deserializes the snapshot back onto `Model`, exits edit mode.

**Rendered structure:**
```razor
<BbCard>
    <BbCardHeader>
        <div class="flex items-center justify-between">
            <BbCardTitle>@Title</BbCardTitle>
            @if (!_isEditing)
            {
                <BbButton Variant="ButtonVariant.Outline" OnClick="EnterEditMode">
                    <LucideIcon Name="pencil" class="mr-2 h-4 w-4" /> Edit
                </BbButton>
            }
            else
            {
                <div class="flex items-center gap-2">
                    <BbButton Variant="ButtonVariant.Ghost" OnClick="Discard">Discard Changes</BbButton>
                    <BbButton OnClick="Save" Disabled="@(!_isDirty || Saving)">
                        @if (Saving) { <BbSpinner Size="SpinnerSize.Small" Class="mr-2" /> }
                        Save
                    </BbButton>
                </div>
            }
        </div>
    </BbCardHeader>
    <BbCardContent>
        <CascadingValue Value="_context">
            @ChildContent
        </CascadingValue>
    </BbCardContent>
</BbCard>
```

### `EditableField`

A field component that renders differently based on the cascaded `EditableFormContext.IsEditing`.

**Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| `Label` | `string` | Field label |
| `Value` | `string?` | The current value (two-way bound) |
| `ValueChanged` | `EventCallback<string?>` | Binding callback |
| `DisplayValue` | `string?` | Optional custom display text (e.g., formatted date). Falls back to `Value`. |
| `Placeholder` | `string?` | Placeholder for edit mode |
| `Required` | `bool` | Whether field is required in edit mode |
| `ReadOnly` | `bool` | If true, always renders as text even in edit mode (for computed/system fields like Created, ID) |
| `FieldType` | `EditableFieldType` | `Text` (default), `Select`, `Switch` — determines which BbFormField renders in edit mode |

**`EditableFieldType` enum:** `Text`, `Select`, `Switch`, `Textarea`

For `Select` type, an additional `Options` parameter provides the choices:
| Parameter | Type | Description |
|-----------|------|-------------|
| `Options` | `IEnumerable<SelectOption<string>>?` | Options for select fields |

**Rendering:**

```razor
@if (_context.IsEditing && !ReadOnly)
{
    @switch (FieldType)
    {
        case EditableFieldType.Text:
            <BbFormFieldInput TValue="string" @bind-Value="Value" Label="@Label"
                              Placeholder="@Placeholder" Required="@Required" />
            break;
        case EditableFieldType.Select:
            <BbFormFieldSelect TValue="string" @bind-Value="Value" Label="@Label"
                               Options="@Options" Placeholder="@Placeholder" />
            break;
        // etc.
    }
}
else
{
    <div>
        <BbTypographyMuted>@Label</BbTypographyMuted>
        <BbTypographyP>@(DisplayValue ?? Value ?? "N/A")</BbTypographyP>
    </div>
}
```

### Usage in Detail Pages

**Before (TenantDetail.razor):**
```razor
@* Read-only summary card *@
<BbCard>
    <BbCardContent>
        <div>
            <BbTypographyMuted>Name</BbTypographyMuted>
            <BbTypographyP>@_tenant.Name</BbTypographyP>
        </div>
        ...
    </BbCardContent>
</BbCard>

@* Separate edit dialog *@
<BbDialog Open="@_editDialogOpen">
    <BbFormFieldInput @bind-Value="_tenantForm.Name" Label="Name" />
    ...
</BbDialog>
```

**After:**
```razor
<EditableForm TModel="Tenant" Model="_tenant" OnSave="SaveTenant" Saving="_saving" Title="Tenant Summary">
    <div class="grid grid-cols-2 gap-4 md:grid-cols-4">
        <EditableField Label="Name" @bind-Value="_tenant.Name" Required="true" />
        <EditableField Label="Description" @bind-Value="_tenant.Description" />
        <EditableField Label="Status" @bind-Value="_statusString"
                       FieldType="EditableFieldType.Select"
                       Options="_statusOptions"
                       DisplayValue="@GetStatusText(_tenant.Status)" />
        <EditableField Label="Version" @bind-Value="_versionString" />
        <EditableField Label="Expiration" @bind-Value="_expirationString"
                       DisplayValue="@(_tenant.Expiration?.ToString("yyyy-MM-dd") ?? "No expiration")" />
        <EditableField Label="Created" DisplayValue="@_tenant.CreatedAt.ToString("g")" ReadOnly="true" />
        <EditableField Label="Enabled" DisplayValue="@(_tenant.IsEnabled ? "Enabled" : "Disabled")" ReadOnly="true" />
    </div>
</EditableForm>
```

The edit dialog and its duplicate `TenantFormModel` class are **deleted entirely**.

### Dirty State Tracking

`EditableForm` tracks dirty state via JSON snapshot comparison:

```csharp
private string? _snapshot;

private void EnterEditMode()
{
    _snapshot = JsonSerializer.Serialize(Model);
    _isEditing = true;
}

private bool ComputeIsDirty()
{
    if (_snapshot is null) return false;
    return JsonSerializer.Serialize(Model) != _snapshot;
}

private void Discard()
{
    if (_snapshot is not null)
    {
        var original = JsonSerializer.Deserialize<TModel>(_snapshot);
        // Copy properties back — or parent handles via OnDiscard callback
    }
    _isEditing = false;
}
```

Note: Since the entity types (Tenant, IdentityInformation, etc.) are EF Core entities with complex navigation properties, the snapshot should only serialize the editable scalar properties. The `Discard` action will call an `OnDiscard` callback so the parent can reload from DB.

**Revised approach:** Instead of JSON snapshot, use a simpler `OnDiscard` callback pattern:
- `EnterEditMode`: Parent clones editable values into local fields (same as today's form model)
- `Discard`: Calls `OnDiscard` which reloads from DB
- `IsDirty`: Tracked by `EditableField` — each field reports whether its value changed since edit mode started

### File Structure

```
Components/Shared/
├── EditableForm.razor          # Form wrapper with edit/save/discard lifecycle
├── EditableFormContext.cs       # Cascaded context (IsEditing, MarkDirty, etc.)
├── EditableField.razor          # Read/edit toggle field
├── EditableFieldType.cs         # Enum: Text, Select, Switch, Textarea
├── StatusBadge.razor            # (existing)
├── ConfirmDeleteDialog.razor    # (existing)
└── CenteredSpinner.razor        # Simple centered spinner wrapper
```

---

## Pages Affected

| Page | Spinner | Row Click | Inline Edit |
|------|---------|-----------|-------------|
| Dashboard | yes | — | — |
| Tenants | yes | yes | — |
| TenantDetail | yes | — | yes (remove dialog) |
| Users | yes | yes | — |
| UserDetail | yes | — | yes (remove dialog) |
| Wallets | yes | yes | — |
| WalletDetail | yes | — | — (keep tabs for operations) |
| Roles | yes | — | — |
| Sessions | yes | — | — |
| AuthLogs | yes | — | — |
| Contacts | yes | — | — |
| Addresses | yes | — | — |
| Credentials | yes | — | — |
| Verifications | yes | — | — |
| Configurations | yes | — | — |
| All Lookups (12) | yes | — | — |
| Batch/Deposit/Withdraw/etc. | yes | — | — |

---

## Non-Goals

- Per-field inline editing (click individual fields) — full-form toggle only
- Undo/redo history — single-level discard only
- Auto-save / debounced save — explicit Save button only
- Form validation beyond `Required` — can add FluentValidation later
