---
name: xframework-audit-module
description: Audit an XFramework module for VSA structure, endpoint quality, services, EF Core, caching, validation, packages, tests, and modernization opportunities. Use for whole-module audits.
---

# XFramework Module Audit

Audit an XFramework module against the project standards.

## When To Use

Use this skill when:
- The user asks to audit a module.
- A module needs readiness review before broader refactor or release.
- You need a structured health report for an `.Api` project.

## References

- `docs/solutions/conventions/xframework-best-practices.md`
- Related files in `docs/solutions/`, found by module name, tags, and component.

## Scope

Review the module's `.Api` project, including:
- `Program.cs`
- `Features/`
- `Services/`
- `Installers/`
- `.csproj`
- `GlobalUsings.cs`
- `Entities/` when present

## Audit Categories

- Structure and VSA architecture.
- C# 14 / .NET 10 modernization.
- Endpoint quality.
- Service quality.
- EF Core data access.
- Caching and invalidation.
- Validation and security.
- Package alignment and project configuration.

## Output

```markdown
# Module Audit: [ModuleName]
**Date:** [today]
**Files Reviewed:** [count]
**Overall Score:** [A/B/C/D/F]

## Summary
[2-3 sentence executive summary]

## Critical Issues
1. [issue + file + line + fix]

## Warnings
1. [issue + file + line + fix]

## Modernization Opportunities
1. [improvement + file + suggested change]

## What's Working Well
1. [positive finding]

## Recommended Action Plan
1. [ordered list of changes]
```
