---
name: xframework-modernize
description: Modernize XFramework C# code to C# 14 and .NET 10 idioms without behavior changes. Use when asked to modernize, clean up syntax, or update old C# patterns.
---

# XFramework Modernization

Modernize specified XFramework files or modules without changing behavior.

## When To Use

Use this skill when:
- The user asks to modernize code.
- Review finds outdated C# syntax.
- A file is being touched anyway and local modernization improves clarity.

## References

- `docs/solutions/conventions/xframework-best-practices.md`, sections 3 and 16, as the current .NET 10 / C# 14 convention source.
- `docs/solutions/architecture-patterns/xframework-agent-architecture-surface-map.md` and `docs/solutions/conventions/xframework-feature-surface-map.md` when module structure is unclear.

## Priorities

- Primary constructors for DI-heavy classes.
- File-scoped namespaces.
- Collection expressions.
- Required members on mandatory DTO/request properties.
- Pattern matching for clear branching.
- Null-coalescing assignment and target-typed `new` where they improve clarity.
- Raw string literals for embedded JSON or multiline text.
- `sealed` classes where inheritance is not intended.

## Rules

- Preserve behavior.
- Prefer one category of modernization at a time in large files.
- Avoid churn that only changes style without improving readability.
- Do not modernize unrelated files unless the user asks for a broad pass.
- Run a build or targeted test if feasible.

Report modernization categories applied and verification.
