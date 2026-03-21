# Modernize Code to C# 14 / .NET 10

You are modernizing XFramework code to use the latest C# 14 and .NET 10 idioms.

## Context
Read `docs/standards/xframework-best-practices.md` section 3 (C# 14 & .NET 10 Idioms) and section 16 (Code Style).

## Arguments
$ARGUMENTS should specify the file(s) or module to modernize.

## Steps

1. **Read the specified files**
2. **Identify outdated patterns** that can be modernized
3. **Apply changes** with explanations

## Modernization Checklist

### Primary Constructors (C# 12+)
```csharp
// ❌ Old pattern
public class ProductService
{
    private readonly AppDbContext _db;
    private readonly ICacheService _cache;
    private readonly ILogger<ProductService> _logger;

    public ProductService(AppDbContext db, ICacheService cache, ILogger<ProductService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }
}

// ✅ Modern
public class ProductService(AppDbContext db, ICacheService cache, ILogger<ProductService> logger)
{
}
```

### File-Scoped Namespaces
```csharp
// ❌ Old
namespace XFramework.Core.Patterns
{
    public record Result { }
}

// ✅ Modern
namespace XFramework.Core.Patterns;

public record Result { }
```

### Collection Expressions (C# 12+)
```csharp
// ❌ Old
var list = new List<string> { "a", "b", "c" };
var array = new int[] { 1, 2, 3 };
var empty = Array.Empty<string>();

// ✅ Modern
List<string> list = ["a", "b", "c"];
int[] array = [1, 2, 3];
string[] empty = [];
```

### Required Members
```csharp
// ❌ Old — no compile-time guarantee
public record CreateProductRequest
{
    public string Name { get; init; }
    public decimal Price { get; init; }
}

// ✅ Modern — compiler enforces required
public record CreateProductRequest
{
    public required string Name { get; init; }
    public required decimal Price { get; init; }
}
```

### Pattern Matching
```csharp
// ❌ Old
if (result.IsSuccess)
    return TypedResults.Ok(result.Data);
else if (result.StatusCode == 404)
    return TypedResults.NotFound();
else
    return TypedResults.Problem(result.Message);

// ✅ Modern
return result switch
{
    { IsSuccess: true } => TypedResults.Ok(result.Data),
    { StatusCode: 404 } => TypedResults.NotFound(),
    _ => TypedResults.Problem(detail: result.Message, statusCode: result.StatusCode)
};
```

### Null-Coalescing Assignment
```csharp
// ❌ Old
if (options == null)
    options = new JsonSerializerOptions();

// ✅ Modern
options ??= new JsonSerializerOptions();
```

### Target-Typed New
```csharp
// ❌ Old
Dictionary<string, List<string>> errors = new Dictionary<string, List<string>>();

// ✅ Modern
Dictionary<string, List<string>> errors = new();
```

### Raw String Literals (C# 11+)
```csharp
// ❌ Old
var json = "{\n  \"name\": \"test\"\n}";

// ✅ Modern
var json = """
    {
      "name": "test"
    }
    """;
```

### Sealed Classes
```csharp
// ❌ Not sealed (allows unintended inheritance, misses JIT optimizations)
public class ProductService { }

// ✅ Sealed by default
public sealed class ProductService { }
```

## Rules
- **Do not change behavior** — only syntax and patterns
- **Preserve all existing functionality**
- **Run build after changes** to verify nothing breaks
- **One category of change at a time** if the file is large
- **Prioritize:** primary constructors > file-scoped namespaces > collection expressions > pattern matching > sealed
