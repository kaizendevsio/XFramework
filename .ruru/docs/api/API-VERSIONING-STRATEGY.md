# XFramework API Versioning Strategy

This document outlines the API versioning approach, breaking change policy, and migration guidelines for XFramework.

## 📋 Table of Contents

- [Versioning Approach](#versioning-approach)
- [Version Format](#version-format)
- [Breaking vs Non-Breaking Changes](#breaking-vs-non-breaking-changes)
- [Version Header](#version-header)
- [Deprecation Policy](#deprecation-policy)
- [Migration Process](#migration-process)
- [Current Version](#current-version)

---

## Versioning Approach

XFramework uses **header-based API versioning** with the following principles:

1. **Major Version in Header** - Version is specified via `api-version` header
2. **Backward Compatibility** - Minor versions maintain backward compatibility
3. **Gradual Migration** - Old versions supported for deprecation period
4. **Clear Communication** - Breaking changes documented in advance

### Why Header-Based Versioning?

✅ **Clean URLs** - No version numbers in URL paths  
✅ **Flexible** - Easy to add/remove versions without URL changes  
✅ **Standard** - Follows REST API best practices  
✅ **Client Control** - Clients explicitly specify version they support  

---

## Version Format

### Semantic Versioning

XFramework follows **Semantic Versioning 2.0.0**:

```
MAJOR.MINOR.PATCH
```

- **MAJOR** - Incompatible API changes (breaking changes)
- **MINOR** - Backward-compatible functionality additions
- **PATCH** - Backward-compatible bug fixes

### API Version Examples

- `3.0` - Major version 3, minor version 0 (current)
- `3.1` - Added new endpoints, no breaking changes
- `4.0` - Breaking changes introduced

---

## Breaking vs Non-Breaking Changes

### ✅ Non-Breaking Changes (Minor/Patch)

**Safe to deploy without version bump:**

- Adding new optional parameters
- Adding new endpoints
- Adding new response fields
- Adding new enum values (if clients handle unknowns)
- Bug fixes that don't change behavior
- Performance improvements
- Internal refactoring (VSA migration)
- Adding new error codes

**Example - Adding Optional Parameter:**
```diff
  GET /api/products?pageSize=20&pageNumber=1&tenantId={id}
+ GET /api/products?pageSize=20&pageNumber=1&tenantId={id}&sortBy=name
```

### ⚠️ Breaking Changes (Major)

**Require major version bump:**

- Removing endpoints
- Removing or renaming request parameters
- Removing or renaming response fields
- Changing parameter types
- Changing authentication method
- Changing response status codes for existing scenarios
- Making optional parameters required
- Changing URL structure

**Example - Removing Field (Breaking):**
```diff
  {
    "id": "123",
    "name": "Product",
-   "legacyField": "value"
  }
```

**Example - Renaming Parameter (Breaking):**
```diff
- GET /api/products?userId={id}
+ GET /api/products?tenantId={id}
```

---

## Version Header

### Request Header

Clients **MUST** include the `api-version` header:

```http
GET /api/products
api-version: 3.0
Authorization: Bearer {token}
```

### Default Behavior

If no `api-version` header is provided:
- **Development**: Latest version (3.0)
- **Production**: Returns `400 Bad Request` with error:
  ```json
  {
    "isSuccess": false,
    "error": {
      "code": "MISSING_API_VERSION",
      "message": "api-version header is required"
    }
  }
  ```

### Version Detection in Code

```csharp
var versionSet = app.NewApiVersionSet()
    .HasApiVersion(3.0)
    .ReportApiVersions()
    .Build();
```

---

## Deprecation Policy

### Timeline

1. **Announcement** (T+0) - Deprecation announced in release notes
2. **Deprecation Period** (T+6 months) - Old version still supported with warnings
3. **End of Life** (T+12 months) - Old version removed

### Deprecation Response Headers

Deprecated versions include response headers:

```http
HTTP/1.1 200 OK
api-deprecated-versions: 2.0
api-supported-versions: 3.0, 3.1
Sunset: Sat, 31 Dec 2025 23:59:59 GMT
```

### Migration Notice

Responses include deprecation warnings:

```json
{
  "isSuccess": true,
  "data": { /* ... */ },
  "warnings": [
    {
      "code": "API_VERSION_DEPRECATED",
      "message": "API version 2.0 will be removed on 2025-12-31",
      "recommendation": "Migrate to version 3.0",
      "migrationGuide": "https://docs.xframework.com/migration/v2-to-v3"
    }
  ]
}
```

---

## Migration Process

### For API Consumers

1. **Review Migration Guide** - Check breaking changes for new version
2. **Update Client Code** - Modify code to handle changes
3. **Update Version Header** - Change `api-version` header value
4. **Test Thoroughly** - Verify all endpoints work correctly
5. **Deploy** - Roll out updated client application

### Migration Guide Template

Each major version includes a migration guide:

```markdown
# Migration from v2.0 to v3.0

## Breaking Changes

1. **Authentication** - Changed from API Key to JWT Bearer tokens
2. **Pagination** - Changed from offset to cursor-based pagination
3. **Error Codes** - Standardized error code format

## Step-by-Step Migration

### 1. Update Authentication
- Remove: `x-api-key` header
- Add: `Authorization: Bearer {token}` header
- Implement: Token refresh flow

### 2. Update Pagination
- Replace `offset` parameter with `cursor`
- Handle `nextCursor` from response

### 3. Update Error Handling
- Update error code parsing logic
- Handle new Result<T> format
```

---

## Current Version

### Version 3.0 (Current)

**Release Date**: 2025-01-20  
**Status**: ✅ Active  

**Major Changes from v2.0:**
- ✅ Migrated to Vertical Slice Architecture (VSA)
- ✅ Removed MediatR dependency
- ✅ Introduced `Result<T>` pattern for all operations
- ✅ Added hybrid caching (Memory + Redis)
- ✅ Implemented OpenTelemetry tracing
- ✅ Added structured logging with LoggerMessage
- ✅ Enhanced health checks for Kubernetes
- ✅ Improved performance with EF Core optimizations

**Breaking Changes:**
- Removed CQRS command/query objects
- Changed response format to `Result<T>` pattern
- Updated error response structure

**New Features:**
- Cache control via `noCache` query parameter
- Distributed tracing with correlation IDs
- Kubernetes-ready health endpoints (`/health/live`, `/health/ready`)
- XML documentation for all endpoints

---

## Version Roadmap

### Version 3.1 (Planned - Q2 2025)

**New Features** (Non-Breaking):
- GraphQL endpoint support
- Real-time subscriptions via WebSockets
- Batch operation endpoints
- Advanced filtering with OData query syntax

**No Breaking Changes**

### Version 4.0 (Planned - Q4 2025)

**Breaking Changes**:
- Remove deprecated v2.0 endpoints
- Change authentication to OAuth 2.0 + OIDC
- Require HTTPS for all endpoints
- Update pagination to cursor-based globally

---

## Version Configuration

### Swagger Configuration

```csharp
public static IServiceCollection InstallSwagger(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.AddEndpointsApiExplorer();
    
    services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v3", new OpenApiInfo
        {
            Version = "v3.0",
            Title = "XFramework API",
            Description = "VSA-based microservices architecture"
        });
        
        // Add version header parameter to all operations
        options.OperationFilter<ApiVersionOperationFilter>();
    });
    
    return services;
}
```

### Version Detection Middleware

```csharp
app.Use(async (context, next) =>
{
    if (!context.Request.Headers.TryGetValue("api-version", out var version))
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new
        {
            isSuccess = false,
            error = new
            {
                code = "MISSING_API_VERSION",
                message = "api-version header is required"
            }
        });
        return;
    }
    
    await next();
});
```

---

## Client Implementation Examples

### C# / .NET Client

```csharp
public class XFrameworkApiClient
{
    private readonly HttpClient _httpClient;
    private const string ApiVersion = "3.0";
    
    public XFrameworkApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("api-version", ApiVersion);
    }
    
    public async Task<Result<Product>> GetProductAsync(Guid id)
    {
        var response = await _httpClient.GetAsync($"/api/products/{id}");
        return await response.Content.ReadFromJsonAsync<Result<Product>>();
    }
}
```

### JavaScript / TypeScript Client

```typescript
class XFrameworkApiClient {
  private readonly baseUrl: string;
  private readonly apiVersion: string = '3.0';
  private token: string | null = null;

  constructor(baseUrl: string) {
    this.baseUrl = baseUrl;
  }

  private async fetch<T>(
    endpoint: string,
    options?: RequestInit
  ): Promise<Result<T>> {
    const response = await fetch(`${this.baseUrl}${endpoint}`, {
      ...options,
      headers: {
        'api-version': this.apiVersion,
        'Content-Type': 'application/json',
        'Authorization': this.token ? `Bearer ${this.token}` : '',
        ...options?.headers,
      },
    });

    return response.json();
  }

  async getProduct(id: string): Promise<Result<Product>> {
    return this.fetch<Product>(`/api/products/${id}`);
  }
}
```

### Python Client

```python
import requests
from typing import Optional, Dict, Any

class XFrameworkApiClient:
    def __init__(self, base_url: str, api_version: str = "3.0"):
        self.base_url = base_url
        self.api_version = api_version
        self.token: Optional[str] = None
    
    def _headers(self) -> Dict[str, str]:
        headers = {
            'api-version': self.api_version,
            'Content-Type': 'application/json'
        }
        if self.token:
            headers['Authorization'] = f'Bearer {self.token}'
        return headers
    
    def get_product(self, product_id: str) -> Dict[str, Any]:
        response = requests.get(
            f'{self.base_url}/api/products/{product_id}',
            headers=self._headers()
        )
        return response.json()
```

---

## Testing Version Support

### Integration Tests

```csharp
[Fact]
public async Task GetProduct_WithValidVersion_ReturnsSuccess()
{
    // Arrange
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Add("api-version", "3.0");
    
    // Act
    var response = await client.GetAsync("/api/products/123");
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}

[Fact]
public async Task GetProduct_WithoutVersion_ReturnsBadRequest()
{
    // Arrange
    var client = _factory.CreateClient();
    
    // Act
    var response = await client.GetAsync("/api/products/123");
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}

[Fact]
public async Task GetProduct_WithUnsupportedVersion_ReturnsNotFound()
{
    // Arrange
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Add("api-version", "99.0");
    
    // Act
    var response = await client.GetAsync("/api/products/123");
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
}
```

---

## FAQ

### Q: Why not use URL-based versioning (e.g., `/v3/products`)?

**A:** Header-based versioning provides:
- Cleaner URLs
- Easier to manage in code
- Better separation of concerns
- Flexibility to change versions without URL changes

### Q: What happens if a client doesn't specify a version?

**A:** In production, requests without `api-version` header are rejected with `400 Bad Request`. In development, it defaults to the latest version.

### Q: How long are deprecated versions supported?

**A:** Deprecated versions are supported for 12 months from deprecation announcement.

### Q: Can we support multiple versions simultaneously?

**A:** Yes! The API can support multiple versions (e.g., 2.0, 3.0, 3.1) concurrently during migration periods.

### Q: How do we handle version-specific business logic?

**A:** Use version-specific service implementations or conditional logic:

```csharp
public class ProductService : IProductService
{
    public async Task<Result<ProductDto>> GetAsync(
        Guid id, 
        string apiVersion)
    {
        return apiVersion switch
        {
            "2.0" => await GetV2Async(id),
            "3.0" => await GetV3Async(id),
            _ => Result<ProductDto>.Failure("UNSUPPORTED_VERSION")
        };
    }
}
```

---

## Related Documentation

- [API Documentation Guide](./api-documentation.md)
- [Postman Collection Guide](./POSTMAN-COLLECTION-GUIDE.md)
- [Developer Onboarding Guide](../guides/developer-onboarding.md)
- [VSA Migration Guide](../guides/vsa-migration-guide.md)

---

**Last Updated**: 2025-01-20  
**Document Version**: 1.0  
**Current API Version**: 3.0