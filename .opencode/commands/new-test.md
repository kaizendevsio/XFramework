---
description: Create tests for XFramework code
agent: build
---

# Create Tests for XFramework Code

Create tests for the target XFramework service, endpoint, component, or pattern.

Arguments: `$ARGUMENTS` should specify what to test, such as `ProductService`, `CreateProductEndpoint`, or `Result pattern`.

Use `docs/solutions/conventions/xframework-best-practices.md` section 12 and existing tests under `src/Tests/` as references.

Rules:
- Use NUnit, FluentAssertions, and Moq unless the existing test project uses a different local convention.
- Name tests `MethodName_Scenario_ExpectedResult`.
- Use Arrange, Act, Assert structure.
- Assert on `Result` properties: `IsSuccess`, `StatusCode`, `Message`, and `Data`.
- Cover success, expected failure, and edge cases.
- Keep tests independent.
- Mirror source structure under `src/Tests/`.
- Prefer integration tests with `WebApplicationFactory<Program>` for endpoints.

After changes, run the narrowest relevant test command if feasible and report the result.
