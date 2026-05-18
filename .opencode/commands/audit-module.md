---
description: Audit an XFramework module
agent: build
---

# Audit an Entire Module Against Best Practices

Audit an XFramework module against the project standards.

Arguments: `$ARGUMENTS` should specify the module name, such as `Inventario`, `Wallets`, or `IdentityServer`.

Use `docs/solutions/conventions/xframework-best-practices.md` as the governing standard. Search `docs/solutions/` for related module, architecture, tooling, and workflow learnings before finalizing the audit.

Review the module's `.Api` project, including `Program.cs`, `Features/`, `Services/`, `Installers/`, `.csproj`, `GlobalUsings.cs`, and `Entities/` when present.

Audit categories:
- Structure and VSA architecture.
- C# 14 / .NET 10 modernization.
- Endpoint quality.
- Service quality.
- EF Core data access.
- Caching and invalidation.
- Validation and security.
- Package alignment and project configuration.

Output a markdown audit report with summary, critical issues, warnings, modernization opportunities, what works well, and a prioritized action plan.
