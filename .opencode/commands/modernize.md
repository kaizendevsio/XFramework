---
description: Modernize code to C# 14 and .NET 10
agent: build
---

# Modernize Code to C# 14 / .NET 10

Modernize the specified XFramework files or module without changing behavior.

Arguments: `$ARGUMENTS` should specify files, folders, or a module.

Use `docs/solutions/conventions/xframework-best-practices.md` sections 3 and 16.

Prioritize:
- Primary constructors for DI-heavy classes.
- File-scoped namespaces.
- Collection expressions.
- Required members on mandatory DTO/request properties.
- Pattern matching for clear branching.
- Null-coalescing assignment and target-typed `new` where they improve clarity.
- Raw string literals for embedded JSON or multiline text.
- `sealed` classes where inheritance is not intended.

Rules:
- Preserve behavior.
- Prefer one category of modernization at a time in large files.
- Avoid churn that only changes style without improving readability.
- Run a build or targeted test if feasible.
