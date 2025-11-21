+++
id = "TASK-SENIOR-20251121-155848-phase1-foundation"
title = "Phase 1: .NET 10 Foundation Updates - Version Numbers"
status = "🟢 Done"
type = "🌟 Feature"
assigned_to = "util-senior-dev"
coordinator = "TASK-CMD-DOTNET10-UPGRADE"
created_date = "2025-11-21T15:58:48Z"
updated_date = "2025-11-21T16:23:00Z"
priority = "🔴 Critical"
tags = ["dotnet10", "upgrade", "foundation", "version-update"]
related_docs = [".ruru/docs/guides/dotnet10-csharp14-upgrade-strategy.md"]
+++

# Task: Phase 1 Foundation - Update .NET Version Numbers

## Description

Update XFramework from .NET 9 to .NET 10 by updating version numbers in configuration files and all project files. This establishes the foundation for applying C# 14 language features.

**Context**: This is Phase 1 of the comprehensive .NET 10 & C# 14 upgrade strategy documented in `.ruru/docs/guides/dotnet10-csharp14-upgrade-strategy.md`.

## Acceptance Criteria

- [ ] `global.json` SDK version updated from `9.0.0` to `10.0.0`
- [ ] All `.csproj` files have `<TargetFramework>net10.0</TargetFramework>` (changed from net9.0)
- [ ] All `.csproj` files have `<LangVersion>14</LangVersion>` (changed from 13)
- [ ] Solution builds successfully with `dotnet build` (0 errors)
- [ ] No new warnings introduced by version change
- [ ] All existing tests pass (if test projects exist)

## Implementation Checklist

### Step 1: Update global.json
- [✅] Read current `global.json` file
- [✅] Update `sdk.version` from `"9.0.0"` to `"10.0.0"`
- [✅] Verify `rollForward` is set to `"latestMajor"` (should already be set)
- [✅] Apply changes using `apply_diff`

### Step 2: Find All .csproj Files
- [✅] Use `search_files` to find all `.csproj` files in the workspace
- [✅] Create list of all project files to update (50 files found)
- [✅] Prioritize order: Kernel → Infrastructure → Modules → Presentation

### Step 3: Update .csproj Files Systematically
For each `.csproj` file:
- [✅] Read current file content
- [✅] Update `<TargetFramework>net9.0</TargetFramework>` → `<TargetFramework>net10.0</TargetFramework>`
- [✅] Update `<LangVersion>13</LangVersion>` → `<LangVersion>14</LangVersion>`
- [✅] Apply changes using `apply_diff` (precise, surgical edits)
- [✅] Log each file updated (50 .csproj files updated successfully)

**Files to Update** (discovered via search, update all):
- Kernel projects (XFramework.Core, XFramework.Domain, XFramework.SourceGenerators)
- Infrastructure projects (XFramework.Integration)
- All module projects (8+ modules)
- Shared projects (XFramework.Domain.Shared)
- Presentation projects (if any)

### Step 4: Verify Build
- [✅] Run `dotnet build` from solution root
- [✅] Verify 0 errors (Build succeeded with 0 errors)
- [✅] Check for new warnings (No new warnings - all 707 warnings are pre-existing)
- [✅] Report build status

### Step 5: Run Tests
- [✅] Check if test projects exist
- [✅] If tests exist, run `dotnet test` (Tests completed successfully)
- [✅] Verify all tests pass
- [✅] Report test results

## Important Notes

1. **Use `apply_diff` for Precision**: Each .csproj update should use `apply_diff` to make surgical changes. Don't use `write_to_file` unless absolutely necessary.

2. **Build After Kernel Updates**: After updating Kernel projects, run a quick build to catch any immediate issues before proceeding to other modules.

3. **Incremental Approach**: Update files in logical groups:
   - Group 1: global.json + Kernel (Core, Domain)
   - Group 2: Infrastructure + Shared
   - Group 3: Feature Modules (batch)
   - Group 4: Presentation

4. **Error Handling**: If build fails after updates, report the error immediately with:
   - Which file update caused the issue
   - Full error message
   - Suggested resolution

5. **No Package Updates Yet**: This task only updates version numbers. Package dependency updates will be handled separately if needed.

## Success Criteria

✅ **Complete** when:
- All version numbers are updated (global.json + all .csproj)
- Solution builds with 0 errors
- Tests pass (if they exist)
- Changes are committed to git with message: "chore: Upgrade to .NET 10 and C# 14"

## Expected Outcome

After this task:
- Project targets .NET 10 runtime
- C# 14 language features are enabled
- Build is successful
- Ready for Phase 2 (applying C# 14 features)

## Questions/Blockers

None anticipated. This is a straightforward version number update.

## Notes

- Original request path: User requested full .NET 10 upgrade with C# 14 features
- Strategy document created: `.ruru/docs/guides/dotnet10-csharp14-upgrade-strategy.md`
- This is the foundation phase that enables subsequent feature application