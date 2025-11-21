# Security Audit Checklist - XFramework VSA Migration

## Overview

This comprehensive security audit checklist ensures XFramework's VSA migration meets enterprise security standards. It covers authentication, authorization, data protection, tenant isolation, and vulnerability prevention.

## Table of Contents

- [Pre-Audit Preparation](#pre-audit-preparation)
- [Authentication & Authorization](#authentication--authorization)
- [JWT Security](#jwt-security)
- [SQL Injection Prevention](#sql-injection-prevention)
- [XSS Vulnerability Checks](#xss-vulnerability-checks)
- [CORS Configuration](#cors-configuration)
- [Tenant Isolation](#tenant-isolation)
- [Rate Limiting](#rate-limiting)
- [HTTPS Enforcement](#https-enforcement)
- [Security Headers](#security-headers)
- [Data Protection](#data-protection)
- [Penetration Testing](#penetration-testing)
- [Audit Reporting](#audit-reporting)

## Pre-Audit Preparation

### Required Tools

```bash
# Security scanning tools
dotnet tool install --global security-scan
dotnet tool install --global dotnet-retire

# OWASP ZAP (for web application security testing)
# Download from: https://www.zaproxy.org/download/

# Dependency vulnerability scanning
npm install -g snyk
snyk auth

# SQL injection testing
# sqlmap (Python-based tool)
pip install sqlmap

# SSL/TLS testing
# testssl.sh
git clone --depth 1 https://github.com/drwetter/testssl.sh.git
```

### Environment Setup

```json
// appsettings.SecurityAudit.json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.AspNetCore.Authorization": "Debug",
      "Microsoft.AspNetCore.Authentication": "Debug"
    }
  },
  "EnableDetailedErrors": false,  // Must be false in production
  "EnableDeveloperExceptionPage": false  // Must be false in production
}
```

## Authentication & Authorization

### Checklist: Authentication Implementation

#### ✅ JWT Bearer Authentication

- [ ] **JWT implementation is secure**
  - [ ] Using industry-standard library (Microsoft.AspNetCore.Authentication.JwtBearer)
  - [ ] Token signing algorithm is HMAC-SHA512 or RS256
  - [ ] No weak algorithms (HS256 acceptable but SHA512 preferred)
  
  **Verification**:
  ```csharp
  // Check in JwtService.cs
  // Line 39: signingCredentials: new(securityKey, SecurityAlgorithms.HmacSha512)
  // ✅ Confirmed: Using HmacSha512
  ```

- [ ] **Token expiration is enforced**
  - [ ] Access tokens expire within reasonable time (≤15 minutes recommended)
  - [ ] Refresh tokens expire (≤7 days recommended)
  - [ ] Expired tokens are rejected
  
  **Verification**:
  ```csharp
  // Check token expiration settings in appsettings.json
  // Verify AccessTokenLifespan and RefreshTokenLifespan
  ```

- [ ] **Token validation is comprehensive**
  - [ ] Validates issuer
  - [ ] Validates audience
  - [ ] Validates expiration
  - [ ] Validates signature
  - [ ] Clock skew is minimal (≤5 minutes)
  
  **Test**:
  ```bash
  # Test with expired token
  curl -H "Authorization: Bearer <expired_token>" \
       http://localhost:5106/api/protected
  
  # Expected: 401 Unauthorized
  ```

#### ✅ Password Security

- [ ] **Password hashing is strong**
  - [ ] Using bcrypt, Argon2, or PBKDF2
  - [ ] No plain text password storage
  - [ ] No reversible encryption for passwords
  
  **Verification**:
  ```sql
  -- Check password storage in database
  SELECT TOP 5 
      Id, 
      Email, 
      PasswordHash, 
      LEN(PasswordHash) as HashLength
  FROM Users;
  
  -- Verify:
  -- ✅ PasswordHash should be non-null
  -- ✅ HashLength should be > 60 characters (bcrypt/PBKDF2)
  -- ❌ No plain text visible
  ```

- [ ] **Password policies enforced**
  - [ ] Minimum length ≥12 characters (or ≥8 with complexity)
  - [ ] Requires uppercase, lowercase, numbers, special characters
  - [ ] Password history prevents reuse (last 5 passwords)
  - [ ] Account lockout after failed attempts

#### ✅ Authorization Controls

- [ ] **Role-Based Access Control (RBAC) implemented**
  - [ ] Roles are defined and enforced
  - [ ] Users have appropriate roles
  - [ ] No over-privileged accounts
  
  **Verification**:
  ```csharp
  // Check endpoint authorization
  // Should see [Authorize(Roles = "Admin")] or similar
  ```

- [ ] **Principle of Least Privilege**
  - [ ] Default user has minimal permissions
  - [ ] Admin access is restricted
  - [ ] Service accounts have minimal required permissions

- [ ] **Authorization checks on all protected endpoints**
  - [ ] Every sensitive endpoint has `[Authorize]` attribute
  - [ ] No bypasses or missing checks
  
  **Test Script**:
  ```bash
  # Test unauthorized access
  curl -X GET http://localhost:5106/api/admin/users
  # Expected: 401 Unauthorized
  
  # Test with user token (non-admin)
  curl -H "Authorization: Bearer <user_token>" \
       -X GET http://localhost:5106/api/admin/users
  # Expected: 403 Forbidden
  ```

### Checklist: Session Management

- [ ] **Session tokens are secure**
  - [ ] Tokens are cryptographically random
  - [ ] Tokens are not predictable
  - [ ] Session IDs are regenerated after login
  
  **Verification**:
  ```csharp
  // Check GenerateRefreshToken in JwtService.cs
  // Line 86-92: Uses RandomNumberGenerator.Create()
  // ✅ Confirmed: Cryptographically secure random
  ```

- [ ] **Session invalidation works**
  - [ ] Logout invalidates tokens
  - [ ] Password change invalidates sessions
  - [ ] Session timeout is enforced

## JWT Security

### Detailed JWT Verification

#### ✅ Token Structure

- [ ] **Claims are appropriate**
  - [ ] No sensitive data in claims (PII, passwords)
  - [ ] Claims include necessary authorization info
  - [ ] Custom claims are properly namespaced
  
  **Review Claims**:
  ```csharp
  // Check JwtService.cs GenerateToken method
  // Verify claims don't include:
  // ❌ Passwords
  // ❌ Credit card numbers
  // ❌ Social security numbers
  // ✅ User ID, roles, tenant ID are OK
  ```

- [ ] **Token payload is not too large**
  - [ ] JWT size < 8KB (cookie limit)
  - [ ] Only essential claims included

#### ✅ Secret Management

- [ ] **JWT secret is strong**
  - [ ] Secret is ≥256 bits (32 characters)
  - [ ] Secret is randomly generated
  - [ ] Secret is not hardcoded
  
  **Verification**:
  ```bash
  # Check appsettings.json or environment variables
  # Secret length should be at least 32 characters
  ```

- [ ] **Secrets are stored securely**
  - [ ] Stored in Azure Key Vault, AWS Secrets Manager, or similar
  - [ ] Not in source control
  - [ ] Not in appsettings.json (use User Secrets or env vars)
  - [ ] Different secrets per environment
  
  **Check**:
  ```bash
  # Search for hardcoded secrets
  git log -p -S "Secret" | grep -i "secret.*="
  
  # Should find no actual secret values
  ```

#### ✅ Token Transmission

- [ ] **Tokens transmitted securely**
  - [ ] Only over HTTPS
  - [ ] Not in URL query parameters
  - [ ] In Authorization header (Bearer scheme)
  - [ ] Not logged in plain text
  
  **Test**:
  ```bash
  # Verify token is in header, not URL
  # Good: Authorization: Bearer <token>
  # Bad: /api/users?token=<token>
  ```

#### ✅ Token Validation

- [ ] **All validation checks enabled**
  ```csharp
  // Verify in Startup/Program.cs or JwtService.cs
  // Required settings:
  ValidateIssuerSigningKey = true,  // ✅
  ValidateIssuer = true,             // ✅
  ValidateAudience = true,           // ✅
  ValidateLifetime = true,           // ✅
  ClockSkew = TimeSpan.FromMinutes(5) // ≤5 minutes
  ```

- [ ] **Token revocation mechanism exists**
  - [ ] Logout invalidates refresh tokens
  - [ ] Can revoke tokens on security incident
  - [ ] Blacklist or token versioning implemented

## SQL Injection Prevention

### Checklist: Query Security

#### ✅ Parameterized Queries

- [ ] **All database queries are parameterized**
  - [ ] Using EF Core LINQ (safe by default)
  - [ ] No string concatenation for queries
  - [ ] No ExecuteSqlRaw with user input
  
  **Code Review**:
  ```csharp
  // ✅ SAFE - EF Core LINQ
  var products = await _context.Products
      .Where(p => p.Name.Contains(searchTerm))
      .ToListAsync();
  
  // ❌ UNSAFE - String concatenation
  var sql = $"SELECT * FROM Products WHERE Name LIKE '%{searchTerm}%'";
  
  // ✅ SAFE - Parameterized raw SQL
  var products = await _context.Products
      .FromSqlRaw("SELECT * FROM Products WHERE Name LIKE {0}", $"%{searchTerm}%")
      .ToListAsync();
  ```

- [ ] **Search codebase for SQL injection risks**
  ```bash
  # Search for dangerous patterns
  grep -r "FromSqlRaw\|ExecuteSqlRaw" src/
  grep -r "SqlCommand.*CommandText.*\+" src/
  grep -r "\.Query<.*string\.Format\|string\.Concat" src/
  
  # Review each occurrence for parameterization
  ```

#### ✅ Input Validation

- [ ] **All user inputs are validated**
  - [ ] Length limits enforced
  - [ ] Type validation (int, guid, email, etc.)
  - [ ] Whitelist validation where possible
  - [ ] Reject unexpected characters
  
  **Validation Examples**:
  ```csharp
  // Check for validation attributes in request DTOs
  [Required]
  [StringLength(100, MinimumLength = 3)]
  [RegularExpression(@"^[a-zA-Z0-9\s]+$")]
  public string ProductName { get; set; }
  
  [Range(0.01, 999999.99)]
  public decimal Price { get; set; }
  
  [EmailAddress]
  public string Email { get; set; }
  ```

#### ✅ ORM Safety

- [ ] **EF Core tracking disabled where appropriate**
  - [ ] Read-only queries use `AsNoTracking()`
  - [ ] Reduces attack surface
  
  **Verification**:
  ```csharp
  // Check query implementations
  // Should see: .AsNoTracking() for read operations
  ```

- [ ] **No dynamic LINQ with user input**
  - [ ] Avoid `System.Linq.Dynamic`
  - [ ] Use static expressions

### SQL Injection Testing

#### Manual Testing

```bash
# Test 1: Basic SQL injection in search
curl -X GET "http://localhost:5106/api/products/search?q='; DROP TABLE Products;--"
# Expected: No SQL error, query should be safely parameterized

# Test 2: Boolean-based blind SQLi
curl -X GET "http://localhost:5106/api/products/search?q=1' OR '1'='1"
# Expected: No extra results, parameterization prevents execution

# Test 3: Union-based SQLi
curl -X GET "http://localhost:5106/api/products/search?q=1' UNION SELECT NULL,NULL,NULL--"
# Expected: No error, no data leakage

# Test 4: Time-based blind SQLi
curl -X GET "http://localhost:5106/api/products/search?q=1'; WAITFOR DELAY '00:00:05'--"
# Expected: Response should be immediate, not delayed
```

#### Automated Testing

```bash
# Using sqlmap (use with caution, only on test environments)
sqlmap -u "http://localhost:5106/api/products/search?q=test" \
       --batch \
       --level=5 \
       --risk=3

# Expected: No vulnerabilities found
```

## XSS Vulnerability Checks

### Checklist: XSS Prevention

#### ✅ Output Encoding

- [ ] **All user input is encoded before output**
  - [ ] API returns JSON (auto-encoded by System.Text.Json)
  - [ ] HTML responses use Razor encoding (@Model.Name)
  - [ ] No raw HTML output from user data
  
  **Verification**:
  ```csharp
  // Check API responses
  // ✅ JSON responses are safe (auto-encoded)
  return Ok(new { name = userInput }); // Safe
  
  // ❌ Unsafe patterns to avoid:
  return Content($"<div>{userInput}</div>", "text/html"); // Dangerous
  ```

- [ ] **Content-Type headers are correct**
  - [ ] JSON responses have `Content-Type: application/json`
  - [ ] No user input in Content-Type header
  
  **Test**:
  ```bash
  curl -I http://localhost:5106/api/products/1
  # Verify: Content-Type: application/json; charset=utf-8
  ```

#### ✅ Input Sanitization

- [ ] **Rich text input is sanitized**
  - [ ] If allowing HTML (e.g., blog posts), use HTML sanitizer
  - [ ] Whitelist allowed tags and attributes
  - [ ] Remove JavaScript event handlers
  
  **Example** (if needed):
  ```csharp
  // Use HtmlSanitizer library
  var sanitizer = new HtmlSanitizer();
  var clean = sanitizer.Sanitize(userInput);
  ```

- [ ] **URL inputs are validated**
  - [ ] Check for `javascript:` protocol
  - [ ] Validate against whitelist of allowed domains
  
  **Validation**:
  ```csharp
  if (url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
  {
      throw new ValidationException("Invalid URL protocol");
  }
  ```

#### ✅ Client-Side Protection

- [ ] **CSP headers configured** (see Security Headers section)
- [ ] **No inline scripts in responses**
- [ ] **No eval() or similar dangerous functions**

### XSS Testing

```bash
# Test 1: Reflected XSS in search
curl -X GET "http://localhost:5106/api/products/search?q=<script>alert('XSS')</script>"
# Expected: JSON response with encoded output, no script execution

# Test 2: Stored XSS (if product names come from user input)
curl -X POST http://localhost:5106/api/products \
     -H "Content-Type: application/json" \
     -H "Authorization: Bearer <token>" \
     -d '{"name":"<img src=x onerror=alert(1)>","price":99.99}'
# Expected: Name stored as plain text, not interpreted as HTML

# Retrieve and verify encoding
curl http://localhost:5106/api/products/1
# Verify: Response contains escaped HTML entities
```

## CORS Configuration

### Checklist: CORS Security

#### ✅ CORS Policy Configuration

- [ ] **CORS is properly configured**
  - [ ] Not using wildcard origins (`*`) in production
  - [ ] Specific allowed origins are listed
  - [ ] Credentials are only allowed with specific origins
  
  **Check Configuration**:
  ```csharp
  // In Program.cs or Startup.cs
  // ❌ UNSAFE for production:
  builder.Services.AddCors(options =>
  {
      options.AddPolicy("AllowAll", policy =>
      {
          policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
      });
  });
  
  // ✅ SAFE for production:
  builder.Services.AddCors(options =>
  {
      options.AddPolicy("ProductionPolicy", policy =>
      {
          policy.WithOrigins("https://app.example.com", "https://admin.example.com")
                .WithMethods("GET", "POST", "PUT", "DELETE")
                .WithHeaders("Content-Type", "Authorization")
                .AllowCredentials();
      });
  });
  ```

- [ ] **Preflight requests handled correctly**
  - [ ] OPTIONS requests return proper headers
  - [ ] Access-Control-Allow-Methods is restrictive
  - [ ] Access-Control-Allow-Headers is restrictive

#### ✅ Origin Validation

- [ ] **Origin header is validated**
  - [ ] Not trusted blindly
  - [ ] Checked against whitelist
  - [ ] Case-sensitive comparison
  
  **Test**:
  ```bash
  # Test CORS with unauthorized origin
  curl -X OPTIONS http://localhost:5106/api/products \
       -H "Origin: https://evil.com" \
       -H "Access-Control-Request-Method: POST"
  
  # Expected: No Access-Control-Allow-Origin header in response
  
  # Test CORS with authorized origin
  curl -X OPTIONS http://localhost:5106/api/products \
       -H "Origin: https://app.example.com" \
       -H "Access-Control-Request-Method: POST"
  
  # Expected: Access-Control-Allow-Origin: https://app.example.com
  ```

#### ✅ Credentials Handling

- [ ] **AllowCredentials used carefully**
  - [ ] Only with specific origins (not wildcard)
  - [ ] Only when cookies/auth needed
  - [ ] Understand security implications
  
  **Security Note**:
  ```
  ⚠️ AllowCredentials with AllowAnyOrigin is BLOCKED by browsers
  ✅ Use specific origins when credentials are needed
  ```

### CORS Testing

```bash
# Test script for CORS validation
# test-cors.sh

#!/bin/bash
API_URL="http://localhost:5106"

echo "=== Testing CORS Configuration ==="

# Test 1: Unauthorized origin
echo -e "\n[Test 1] Unauthorized origin"
curl -i -X OPTIONS "$API_URL/api/products" \
     -H "Origin: https://evil.com" \
     -H "Access-Control-Request-Method: GET"

# Test 2: Authorized origin  
echo -e "\n[Test 2] Authorized origin"
curl -i -X OPTIONS "$API_URL/api/products" \
     -H "Origin: https://app.example.com" \
     -H "Access-Control-Request-Method: GET"

# Test 3: Wildcard check (should fail)
echo -e "\n[Test 3] Wildcard origin check"
curl -i -X GET "$API_URL/api/products" \
     -H "Origin: https://random-domain.com"
```

## Tenant Isolation

### Checklist: Multi-Tenant Security

#### ✅ Tenant Context Enforcement

- [ ] **Tenant ID is required for all operations**
  - [ ] Every request has tenant context (header, token, etc.)
  - [ ] No global/cross-tenant operations
  - [ ] Tenant ID validated on every request
  
  **Verification**:
  ```csharp
  // Check TenantService.cs and middleware
  // Should see tenant ID extraction from:
  // - HTTP headers (X-Tenant-ID)
  // - JWT claims
  // - Request context
  ```

- [ ] **Tenant isolation in database queries**
  - [ ] Global query filter applied
  - [ ] All queries filtered by tenant ID
  - [ ] No way to bypass tenant filter
  
  **Check DbContext**:
  ```csharp
  // Should see in OnModelCreating:
  modelBuilder.Entity<Product>()
      .HasQueryFilter(p => p.TenantId == _currentTenantId);
  
  // Verify for ALL entities
  ```

#### ✅ Data Segregation

- [ ] **No data leakage between tenants**
  - [ ] Test cross-tenant data access attempts
  - [ ] Verify tenant boundaries in responses
  - [ ] Check audit logs for proper tenant tagging

- [ ] **Tenant-specific caching**
  - [ ] Cache keys include tenant ID
  - [ ] Cache eviction is tenant-scoped
  
  **Verification**:
  ```csharp
  // Check cache key format in HybridCacheService
  // Should be: "tenant:{tenantId}:product:{productId}"
  // Not just: "product:{productId}"
  ```

#### ✅ Authorization Scoping

- [ ] **Permissions are tenant-scoped**
  - [ ] Admin in Tenant A cannot access Tenant B
  - [ ] Roles are tenant-specific
  - [ ] No super-admin across all tenants (or strictly controlled)

### Tenant Isolation Testing

```bash
# Test script for tenant isolation
# test-tenant-isolation.sh

#!/bin/bash

TENANT_A="tenant-001"
TENANT_B="tenant-002"
TOKEN_A="<token-for-tenant-a>"
TOKEN_B="<token-for-tenant-b>"

echo "=== Testing Tenant Isolation ==="

# Test 1: Create product in Tenant A
echo -e "\n[Test 1] Create product in Tenant A"
PRODUCT_ID=$(curl -X POST http://localhost:5106/api/products \
     -H "Authorization: Bearer $TOKEN_A" \
     -H "X-Tenant-ID: $TENANT_A" \
     -H "Content-Type: application/json" \
     -d '{"name":"Product A","price":99.99}' \
     | jq -r '.data.id')

echo "Created product ID: $PRODUCT_ID"

# Test 2: Try to access Tenant A's product from Tenant B (should fail)
echo -e "\n[Test 2] Try to access Tenant A's product from Tenant B"
curl -X GET "http://localhost:5106/api/products/$PRODUCT_ID" \
     -H "Authorization: Bearer $TOKEN_B" \
     -H "X-Tenant-ID: $TENANT_B"

# Expected: 404 Not Found or 403 Forbidden (NOT 200 with data)

# Test 3: Try to list products across tenants (should only see own)
echo -e "\n[Test 3] List products for Tenant B"
curl -X GET http://localhost:5106/api/products \
     -H "Authorization: Bearer $TOKEN_B" \
     -H "X-Tenant-ID: $TENANT_B"

# Expected: Should NOT include products from Tenant A

# Test 4: Attempt tenant ID tampering
echo -e "\n[Test 4] Token for Tenant A, but header says Tenant B"
curl -X GET http://localhost:5106/api/products \
     -H "Authorization: Bearer $TOKEN_A" \
     -H "X-Tenant-ID: $TENANT_B"

# Expected: 401 Unauthorized or 403 Forbidden (tenant mismatch detected)
```

## Rate Limiting

### Checklist: Rate Limiting Implementation

#### ✅ Rate Limiting Configuration

- [ ] **Rate limiting is enabled**
  - [ ] Global rate limits configured
  - [ ] Per-endpoint limits for sensitive operations
  - [ ] Per-user/tenant limits
  
  **Configuration Example**:
  ```csharp
  // Check for AspNetCoreRateLimit configuration
  services.AddMemoryCache();
  services.Configure<IpRateLimitOptions>(Configuration.GetSection("IpRateLimiting"));
  services.AddInMemoryRateLimiting();
  services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
  ```

- [ ] **Rate limit thresholds are appropriate**
  ```json
  // appsettings.json
  {
    "IpRateLimiting": {
      "EnableEndpointRateLimiting": true,
      "StackBlockedRequests": false,
      "GeneralRules": [
        {
          "Endpoint": "*",
          "Period": "1m",
          "Limit": 100  // 100 requests per minute per IP
        },
        {
          "Endpoint": "*/api/auth/login",
          "Period": "1m",
          "Limit": 5  // 5 login attempts per minute
        }
      ]
    }
  }
  ```

#### ✅ Rate Limit Headers

- [ ] **Rate limit info exposed in headers**
  - [ ] X-RateLimit-Limit
  - [ ] X-RateLimit-Remaining
  - [ ] X-RateLimit-Reset
  - [ ] Retry-After (when limited)

#### ✅ Bypass Prevention

- [ ] **Rate limits cannot be bypassed**
  - [ ] Applied before authentication
  - [ ] Cannot be circumvented by changing IPs (if using IP-based)
  - [ ] Distributed rate limiting for multi-server (Redis-backed)

### Rate Limiting Testing

```bash
# Test rate limiting
for i in {1..10}; do
  curl -w "\nRequest $i - Status: %{http_code}\n" \
       -X POST http://localhost:5106/api/auth/login \
       -H "Content-Type: application/json" \
       -d '{"email":"test@example.com","password":"wrong"}'
  sleep 1
done

# Expected: After 5 requests, should get 429 Too Many Requests
```

## HTTPS Enforcement

### Checklist: Transport Security

#### ✅ HTTPS Configuration

- [ ] **HTTPS is enforced**
  - [ ] HTTP redirects to HTTPS
  - [ ] No sensitive data over HTTP
  - [ ] HTTPS enforcement in middleware
  
  **Verification**:
  ```csharp
  // Check Program.cs or Startup.cs
  app.UseHttpsRedirection(); // ✅ Should be present
  app.UseHsts(); // ✅ Should be present (production only)
  ```

- [ ] **HSTS enabled**
  - [ ] Strict-Transport-Security header present
  - [ ] Max-age ≥ 1 year (31536000 seconds)
  - [ ] includeSubDomains if applicable
  - [ ] preload considered for production
  
  **Configuration**:
  ```csharp
  services.AddHsts(options =>
  {
      options.MaxAge = TimeSpan.FromDays(365);
      options.IncludeSubDomains = true;
      options.Preload = true;
  });
  ```

#### ✅ SSL/TLS Configuration

- [ ] **TLS version is modern**
  - [ ] TLS 1.2 minimum (TLS 1.3 preferred)
  - [ ] No SSL 2.0, SSL 3.0, TLS 1.0, TLS 1.1
  
  **Testing**:
  ```bash
  # Test SSL/TLS configuration
  testssl.sh/testssl.sh https://api.example.com
  
  # Or using OpenSSL
  openssl s_client -connect api.example.com:443 -tls1_2
  openssl s_client -connect api.example.com:443 -tls1_1  # Should fail
  ```

- [ ] **Certificate is valid**
  - [ ] Not self-signed (in production)
  - [ ] Not expired
  - [ ] Covers all required domains
  - [ ] Issued by trusted CA
  
  **Check**:
  ```bash
  # Check certificate details
  openssl s_client -connect api.example.com:443 -showcerts
  
  # Verify expiration
  echo | openssl s_client -connect api.example.com:443 2>/dev/null | \
        openssl x509 -noout -dates
  ```

#### ✅ Certificate Pinning (Optional)

- [ ] **Consider certificate pinning for mobile apps**
  - [ ] Pin to leaf cert or intermediate CA
  - [ ] Have backup pins
  - [ ] Monitor expiration

### HTTPS Testing

```bash
# Test 1: HTTP to HTTPS redirect
curl -I http://api.example.com
# Expected: 301/302 redirect to https://

# Test 2: HSTS header present
curl -I https://api.example.com
# Expected: Strict-Transport-Security: max-age=31536000; includeSubDomains

# Test 3: Mixed content (if serving web pages)
# Verify no HTTP resources on HTTPS pages
```

## Security Headers

### Checklist: HTTP Security Headers

#### ✅ Required Headers

- [ ] **X-Content-Type-Options: nosniff**
  - [ ] Prevents MIME-type sniffing
  
  **Configuration**:
  ```csharp
  app.Use(async (context, next) =>
  {
      context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
      await next();
  });
  ```

- [ ] **X-Frame-Options: DENY** (or SAMEORIGIN)
  - [ ] Prevents clickjacking
  
  ```csharp
  context.Response.Headers.Add("X-Frame-Options", "DENY");
  ```

- [ ] **Content-Security-Policy**
  - [ ] Restricts resource loading
  - [ ] Prevents inline scripts (if applicable)
  
  **Example**:
  ```csharp
  var csp = "default-src 'self'; " +
            "script-src 'self'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: https:; " +
            "font-src 'self'; " +
            "connect-src 'self'; " +
            "frame-ancestors 'none'";
  context.Response.Headers.Add("Content-Security-Policy", csp);
  ```

- [ ] **X-XSS-Protection: 1; mode=block** (deprecated but doesn't hurt)
  ```csharp
  context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
  ```

- [ ] **Referrer-Policy: strict-origin-when-cross-origin**
  ```csharp
  context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
  ```

- [ ] **Permissions-Policy** (formerly Feature-Policy)
  ```csharp
  context.Response.Headers.Add("Permissions-Policy", 
      "geolocation=(), microphone=(), camera=()");
  ```

#### ✅ Headers to Remove

- [ ] **Server header removed/obscured**
  ```csharp
  // In Program.cs
  builder.WebHost.ConfigureKestrel(options =>
  {
      options.AddServerHeader = false;
  });
  ```

- [ ] **X-Powered-By removed**
  ```csharp
  app.Use(async (context, next) =>
  {
      context.Response.Headers.Remove("X-Powered-By");
      await next();
  });
  ```

- [ ] **X-AspNet-Version removed**
  ```xml
  <!-- In web.config (if using IIS) -->
  <httpProtocol>
    <customHeaders>
      <remove name="X-AspNet-Version" />
    </customHeaders>
  </httpProtocol>
  ```

### Security Headers Testing

```bash
# Test all security headers
curl -I https://api.example.com/api/products

# Expected headers:
# ✅ Strict-Transport-Security: max-age=31536000
# ✅ X-Content-Type-Options: nosniff
# ✅ X-Frame-Options: DENY
# ✅ Content-Security-Policy: default-src 'self'
# ✅ Referrer-Policy: strict-origin-when-cross-origin
# ❌ Server: (should be absent or generic)
# ❌ X-Powered-By: (should be absent)
```

**Automated Scanner**:
```bash
# Use securityheaders.com scanner
curl https://securityheaders.com/?q=https://api.example.com
```

## Data Protection

### Checklist: Data Security

#### ✅ Data at Rest

- [ ] **Database encryption enabled**
  - [ ] SQL Server Transparent Data Encryption (TDE)
  - [ ] Encrypted backups
  
  **Verification**:
  ```sql
  -- Check if TDE is enabled
  SELECT 
      db.name,
      encryption_state,
      percent_complete,
      key_algorithm,
      key_length
  FROM sys.dm_database_encryption_keys dek
  INNER JOIN sys.databases db ON dek.database_id = db.database_id;
  
  -- encryption_state: 3 = Encrypted
  ```

- [ ] **Sensitive data encrypted in database**
  - [ ] Credit cards, SSN, etc. encrypted at column level
  - [ ] Encryption keys stored separately (Key Vault)
  
  **Example**:
  ```csharp
  // Using data protection API
  [ProtectedPersonalData]
  public string CreditCardNumber { get; set; }
  ```

#### ✅ Data in Transit

- [ ] **All external communications use HTTPS**
  - [ ] API calls to third parties
  - [ ] Database connections (if remote)
  - [ ] Redis connections encrypted

- [ ] **Connection strings secured**
  - [ ] Not in source control
  - [ ] Stored in Azure Key Vault or similar
  - [ ] Encrypted in configuration files
  
  **Check**:
  ```bash
  # Verify no connection strings in git history
  git log -p | grep -i "server=\|password="
  # Should find nothing
  ```

#### ✅ Logging Security

- [ ] **No sensitive data in logs**
  - [ ] No passwords, tokens, credit cards
  - [ ] PII is redacted or hashed
  - [ ] Audit logs for sensitive operations
  
  **Verification**:
  ```bash
  # Check log files for sensitive patterns
  grep -rni "password\|token\|ssn\|credit.*card" logs/
  # Should find no actual sensitive values
  ```

- [ ] **Log integrity maintained**
  - [ ] Logs are immutable
  - [ ] Centralized logging
  - [ ] Log retention policy enforced

### Sensitive Data Audit

```sql
-- Find potentially sensitive columns
SELECT 
    t.name AS TableName,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.max_length
FROM sys.tables t
INNER JOIN sys.columns c ON t.object_id = c.object_id
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE 
    c.name LIKE '%password%' OR
    c.name LIKE '%token%' OR
    c.name LIKE '%secret%' OR
    c.name LIKE '%credit%' OR
    c.name LIKE '%card%' OR
    c.name LIKE '%ssn%'
ORDER BY t.name, c.column_id;

-- Review each to ensure proper protection
```

## Penetration Testing

### Pre-Testing Checklist

- [ ] **Obtain written authorization**
- [ ] **Define scope and rules of engagement**
- [ ] **Notify relevant teams**
- [ ] **Have rollback plan**
- [ ] **Test in staging first**

### Automated Vulnerability Scanning

#### OWASP ZAP Scan

```bash
# Start ZAP in daemon mode
docker run -u zap -p 8080:8080 -i owasp/zap2docker-stable zap.sh \
  -daemon -host 0.0.0.0 -port 8080 -config api.disablekey=true

# Wait for ZAP to start (about 30 seconds)
sleep 30

# Spider the application
curl "http://localhost:8080/JSON/spider/action/scan/?url=http://api.example.com"

# Wait for spider to complete
# Check status
curl "http://localhost:8080/JSON/spider/view/status"

# Run active scan
curl "http://localhost:8080/JSON/ascan/action/scan/?url=http://api.example.com"

# Generate report
curl "http://localhost:8080/OTHER/core/other/htmlreport" > zap-report.html
```

#### Dependency Vulnerability Scan

```bash
# Scan .NET dependencies
dotnet list package --vulnerable --include-transitive

# Using Snyk
snyk test --file=src/YourProject/YourProject.csproj

# Using retire.js (for frontend dependencies)
retire --path ./wwwroot/lib --outputformat json
```

### Manual Penetration Testing

#### Authentication Testing

```bash
# 1. Brute force login
for i in {1..100}; do
  curl -X POST http://localhost:5106/api/auth/login \
       -H "Content-Type: application/json" \
       -d "{\"email\":\"admin@example.com\",\"password\":\"password$i\"}"
done
# Expected: Rate limiting kicks in, account lockout after X attempts

# 2. JWT tampering
# Take a valid token, modify the payload, and try to use it
# Expected: Signature validation fails, 401 Unauthorized

# 3. Token replay attack
# Use an old/revoked token
# Expected: 401 Unauthorized
```

#### Authorization Testing

```bash
# 1. Horizontal privilege escalation
# User A tries to access User B's resources
curl -H "Authorization: Bearer <user-a-token>" \
     http://localhost:5106/api/users/<user-b-id>
# Expected: 403 Forbidden

# 2. Vertical privilege escalation
# Regular user tries to access admin endpoint
curl -H "Authorization: Bearer <user-token>" \
     http://localhost:5106/api/admin/users
# Expected: 403 Forbidden

# 3. Missing authorization
# Try accessing protected endpoints without token
curl http://localhost:5106/api/users/me
# Expected: 401 Unauthorized
```

#### Input Validation Testing

```bash
# 1. Large payload DoS
# Generate 10MB JSON payload
python -c "print('{\"data\":\"' + 'A'*10485760 + '\"}')" | \
  curl -X POST http://localhost:5106/api/products \
       -H "Content-Type: application/json" \
       --data-binary @-
# Expected: 413 Payload Too Large or timeout

# 2. Malformed JSON
curl -X POST http://localhost:5106/api/products \
     -H "Content-Type: application/json" \
     -d '{invalid json}'
# Expected: 400 Bad Request

# 3. Type confusion
curl -X POST http://localhost:5106/api/products \
     -H "Content-Type: application/json" \
     -d '{"name":"Product","price":"not a number"}'
# Expected: 400 Bad Request with validation error
```

## Audit Reporting

### Security Audit Report Template

```markdown
# Security Audit Report - XFramework VSA

**Date**: [Date]
**Auditor**: [Name/Team]
**Environment**: [Staging/Production]
**Version**: [Application Version]

## Executive Summary

[Brief overview of findings]

## Scope

- Authentication & Authorization
- Data Protection
- Network Security
- Application Security
- Infrastructure Security

## Findings Summary

| Severity | Count | Status |
|----------|-------|--------|
| Critical | X | [Open/Closed] |
| High | X | [Open/Closed] |
| Medium | X | [Open/Closed] |
| Low | X | [Open/Closed] |
| Info | X | [Open/Closed] |

## Detailed Findings

### [CRITICAL-001] Title

**Severity**: Critical
**Category**: Authentication
**Status**: Open

**Description**:
[Detailed description of vulnerability]

**Impact**:
[What could happen if exploited]

**Evidence**:
```
[Proof of concept, screenshots, logs]
```

**Recommendation**:
[How to fix]

**Timeline**:
[When to fix - immediately for critical]

---

## Passed Checks

| Check | Result | Notes |
|-------|--------|-------|
| HTTPS Enforcement | ✅ Pass | HSTS enabled, proper redirects |
| SQL Injection | ✅ Pass | Parameterized queries throughout |
| XSS Prevention | ✅ Pass | Proper output encoding |
| ... | ... | ... |

## Compliance Status

- [ ] OWASP Top 10 (2021)
- [ ] PCI DSS (if applicable)
- [ ] GDPR (if applicable)
- [ ] HIPAA (if applicable)

## Recommendations

1. **Immediate Actions** (Critical/High)
   - [Action item 1]
   - [Action item 2]

2. **Short-term Actions** (Medium)
   - [Action item 3]
   - [Action item 4]

3. **Long-term Improvements** (Low)
   - [Action item 5]
   - [Action item 6]

## Follow-up Plan

- **Re-audit Date**: [Date]
- **Responsible Team**: [Team Name]
- **Tracking**: [Ticket System Link]

## Appendices

- Appendix A: Full vulnerability scan results
- Appendix B: Penetration test logs
- Appendix C: Configuration screenshots
```

## Security Maintenance

### Ongoing Security Tasks

```markdown
## Weekly
- [ ] Review security logs
- [ ] Check for failed login attempts
- [ ] Review rate limiting triggers
- [ ] Check SSL certificate expiration

## Monthly
- [ ] Run dependency vulnerability scan
- [ ] Review and rotate secrets if needed
- [ ] Review user permissions
- [ ] Audit tenant access patterns

## Quarterly
- [ ] Full security audit
- [ ] Penetration testing
- [ ] Review security policies
- [ ] Update security documentation

## Annually
- [ ] Third-party security audit
- [ ] Disaster recovery testing
- [ ] Security awareness training
- [ ] Review and update incident response plan
```

## Related Documentation

- [Deployment Runbook](./deployment-runbook.md)
- [Incident Response Plan](./incident-response-plan.md)
- [Monitoring and Alerting Guide](./monitoring-alerting-guide.md)
- [Performance Testing Guide](./performance-testing-guide.md)