# Create Tests for XFramework Code

You are writing tests for XFramework code following the testing best practices.

## Context
Read `docs/standards/xframework-best-practices.md` section 12 (Testing). Reference existing tests at `src/Tests/XFramework.Core.Tests/`.

## Arguments
$ARGUMENTS should specify what to test: a service, endpoint, or component (e.g., "ProductService", "CreateProductEndpoint", "Result pattern").

## Steps

1. **Read the code to be tested** — understand all public methods and their Result<T> returns
2. **Read existing test examples:**
   - `src/Tests/XFramework.Core.Tests/Patterns/ResultTests.cs`
   - `src/Tests/XFramework.Core.Tests/Services/Caching/HybridCacheServiceTests.cs`
3. **Create the test file** in the appropriate location under `src/Tests/`

## Test File Template

```csharp
namespace [Module].Tests.Services;

[TestFixture]
public class [Entity]ServiceTests
{
    private Mock<AppDbContext> _dbMock;
    private Mock<ICacheService> _cacheMock;
    private Mock<ILogger<[Entity]Service>> _loggerMock;
    private [Entity]Service _sut;

    [SetUp]
    public void Setup()
    {
        _dbMock = new Mock<AppDbContext>();
        _cacheMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<[Entity]Service>>();
        _sut = new [Entity]Service(_dbMock.Object, _cacheMock.Object, _loggerMock.Object);
    }

    [Test]
    public async Task GetByIdAsync_EntityExists_ReturnsSuccess()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity = new [Entity] { Id = id, Name = "Test" };
        // ... setup mocks

        // Act
        var result = await _sut.GetByIdAsync(id, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Id.Should().Be(id);
    }

    [Test]
    public async Task GetByIdAsync_EntityNotFound_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        // ... setup mocks to return null

        // Act
        var result = await _sut.GetByIdAsync(id, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
        result.Message.Should().Contain("not found");
    }
}
```

## Rules to Enforce
- **Framework:** NUnit + FluentAssertions + Moq
- **Test naming:** `MethodName_Scenario_ExpectedResult`
- **AAA pattern:** Arrange, Act, Assert — clearly separated
- **Assert on Result properties** — IsSuccess, StatusCode, Message, Data
- **One assertion concept per test** (multiple `.Should()` on same result is fine)
- **Test both success and failure paths** for every service method
- **Test edge cases:** null inputs, empty GUIDs, empty strings, max pagination, zero amounts
- **For endpoints:** use `WebApplicationFactory<Program>` for integration tests
- **Mock only external dependencies** — prefer real objects for value types, DTOs, records
- **No test should depend on another test's state** — each test is independent
- **Minimum coverage:** 80% on services, 100% on core patterns
- **File location:** mirror the source structure under `src/Tests/`
  - Service at `[Module].Api/Services/Foo.cs` → Test at `[Module].Tests/Services/FooTests.cs`
