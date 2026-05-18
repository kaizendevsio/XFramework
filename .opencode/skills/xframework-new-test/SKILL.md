---
name: xframework-new-test
description: Create NUnit/FluentAssertions/Moq tests for XFramework services, endpoints, and core patterns. Use when adding coverage, fixing missing tests, or validating new behavior.
---

# XFramework Test Creation

Create tests for XFramework services, endpoints, components, or core patterns.

## When To Use

Use this skill when:
- The user asks to add tests.
- A code change introduces new behavior or fixes a bug.
- Review finds missing success, failure, or edge-case coverage.

## References

- `docs/solutions/conventions/xframework-best-practices.md`, testing section.
- Existing tests under `src/Tests/`.

## Rules

- Use NUnit, FluentAssertions, and Moq unless a local test project uses different conventions.
- Name tests `MethodName_Scenario_ExpectedResult`.
- Use Arrange, Act, Assert structure.
- Assert on `Result` properties: `IsSuccess`, `StatusCode`, `Message`, and `Data`.
- Cover success, expected failure, and edge cases.
- Keep tests independent.
- Mirror source structure under `src/Tests/`.
- Prefer integration tests with `WebApplicationFactory<Program>` for endpoints when a suitable test host exists.

## Workflow

1. Read the code under test and nearby test examples.
2. Choose the narrowest test project that already references the target code.
3. Add tests for success, failure, and meaningful edge cases.
4. Run the targeted test command when feasible.

Report the test command and result.
