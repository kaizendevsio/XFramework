# XFramework Postman Collection Guide

This guide explains how to use the XFramework API Postman collection for testing and development.

## 📦 Files

- **`XFramework-API.postman_collection.json`** - Complete API collection with all endpoints
- **`XFramework-Local.postman_environment.json`** - Local development environment variables

## 🚀 Quick Start

### 1. Import Collection and Environment

1. Open Postman
2. Click **Import** button
3. Select both JSON files:
   - `XFramework-API.postman_collection.json`
   - `XFramework-Local.postman_environment.json`
4. Click **Import**

### 2. Select Environment

1. Click the environment dropdown (top-right)
2. Select **"XFramework - Local Development"**

### 3. Authenticate

1. Navigate to **Authentication → Login**
2. Update the request body with valid credentials:
   ```json
   {
     "username": "your-email@example.com",
     "password": "YourPassword123!",
     "tenantId": "{{tenant_id}}"
   }
   ```
3. Click **Send**
4. The JWT token will be automatically saved to `{{jwt_token}}` variable

## 📋 Environment Variables

The collection uses the following environment variables:

| Variable | Default Value | Description |
|----------|--------------|-------------|
| `base_url` | `http://localhost:5000` | Base API URL |
| `api_version` | `3.0` | API version header value |
| `tenant_id` | `00000000-0000-0000-0000-000000000001` | Default tenant ID |
| `jwt_token` | (auto-set) | JWT authentication token |
| `refresh_token` | (auto-set) | Refresh token for token renewal |

### Customizing Environment Variables

1. Click on **Environments** (left sidebar)
2. Select **XFramework - Local Development**
3. Modify values as needed:
   - Change `base_url` for different deployment environments
   - Update `tenant_id` for multi-tenant testing

## 📁 Collection Structure

### 1. Health Checks

Test application health and readiness:

- **Health Check (Detailed)** - `/health` - Full health status with all checks
- **Liveness Probe** - `/health/live` - Kubernetes liveness probe
- **Readiness Probe** - `/health/ready` - Kubernetes readiness probe

**Usage**: Run these first to ensure the API is running.

### 2. Authentication

Obtain and refresh JWT tokens:

- **Login** - Authenticate and get JWT token (auto-saves to `{{jwt_token}}`)
- **Refresh Token** - Renew JWT using refresh token

**Login Response Example**:
```json
{
  "isSuccess": true,
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIs...",
    "refreshToken": "refresh_token_here",
    "expiresAt": "2024-01-20T10:30:00Z"
  },
  "error": null
}
```

### 3. Products (Inventario Module)

CRUD operations for products:

- **Get Products List** - Paginated list with caching
- **Get Product by ID** - Single product retrieval
- **Create Product** - Create new product
- **Update Product** - Update existing product (PUT)
- **Delete Product** - Soft delete product

**Query Parameters**:
- `pageSize` - Number of items per page (default: 20)
- `pageNumber` - Page number (1-based)
- `tenantId` - Tenant identifier (required)
- `noCache` - Bypass cache (true/false)

**Create Product Example**:
```json
{
  "name": "Sample Product",
  "description": "This is a sample product",
  "sku": "PROD-001",
  "price": 29.99,
  "stockQuantity": 100,
  "isActive": true,
  "tenantId": "{{tenant_id}}"
}
```

### 4. Wallets

Financial wallet operations:

- **Get Wallets List** - List all wallets for a tenant
- **Increment Wallet Balance** - Add funds to wallet
- **Transfer Between Wallets** - Transfer funds between two wallets

**Increment Balance Example**:
```json
{
  "walletId": "00000000-0000-0000-0000-000000000001",
  "amount": 100.00,
  "description": "Deposit",
  "referenceNumber": "REF-001",
  "tenantId": "{{tenant_id}}"
}
```

**Transfer Example**:
```json
{
  "fromWalletId": "00000000-0000-0000-0000-000000000001",
  "toWalletId": "00000000-0000-0000-0000-000000000002",
  "amount": 50.00,
  "description": "Transfer",
  "referenceNumber": "TRF-001",
  "tenantId": "{{tenant_id}}"
}
```

### 5. Messaging

Message operations:

- **Get Messages** - Retrieve message list
- **Send Message** - Send direct message

**Send Message Example**:
```json
{
  "recipientId": "00000000-0000-0000-0000-000000000002",
  "subject": "Test Message",
  "body": "This is a test message",
  "messageType": "Direct",
  "tenantId": "{{tenant_id}}"
}
```

### 6. StreamFlow

Real-time messaging hub:

- **Connect to Hub** - WebSocket endpoint for SignalR connection

**Note**: StreamFlow requires SignalR client. Use this endpoint to establish WebSocket connection.

## 🔐 Authentication

### Bearer Token Authentication

All authenticated endpoints use Bearer token authentication. The token is automatically included in requests using the `{{jwt_token}}` variable.

**Manual Token Setup** (if auto-save fails):

1. Copy the JWT token from login response
2. Go to **Environments → XFramework - Local Development**
3. Set `jwt_token` variable to the copied token

### Token Expiration

Tokens expire after a configured duration (default: 1 hour). When expired:

1. Use **Authentication → Refresh Token** to get a new token
2. Or use **Authentication → Login** to re-authenticate

## 📊 Response Format

All API responses follow the Result<T> pattern:

### Success Response
```json
{
  "isSuccess": true,
  "data": {
    // Response data here
  },
  "error": null
}
```

### Error Response
```json
{
  "isSuccess": false,
  "data": null,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid input parameters",
    "details": ["Field 'name' is required"]
  }
}
```

### Paginated Response
```json
{
  "isSuccess": true,
  "data": {
    "items": [/* array of items */],
    "pageNumber": 1,
    "pageSize": 20,
    "totalCount": 150,
    "totalPages": 8,
    "hasPreviousPage": false,
    "hasNextPage": true
  },
  "error": null
}
```

## 🧪 Testing Workflows

### Complete Product CRUD Test

1. **Create Product** - Save product ID from response
2. **Get Product by ID** - Verify creation using saved ID
3. **Update Product** - Modify product data
4. **Get Products List** - Verify product appears in list
5. **Delete Product** - Soft delete
6. **Get Product by ID** - Verify 404 or isDeleted flag

### Wallet Transfer Test

1. **Get Wallets List** - Note two wallet IDs
2. **Increment Wallet Balance** - Add funds to first wallet
3. **Transfer Between Wallets** - Transfer from first to second
4. **Get Wallets List** - Verify balances updated

## 🔍 Debugging Tips

### Enable Postman Console

1. View → Show Postman Console (Alt+Ctrl+C)
2. Monitor request/response details
3. View auto-script execution (login token capture)

### Check Request Headers

Verify the following headers are set:
- `Authorization: Bearer {{jwt_token}}`
- `api-version: {{api_version}}`
- `Content-Type: application/json`

### Common Issues

**Issue**: 401 Unauthorized
- **Solution**: Ensure you've run the Login request and token was saved

**Issue**: 400 Bad Request
- **Solution**: Check request body matches expected schema in documentation

**Issue**: 404 Not Found
- **Solution**: Verify the endpoint URL and ensure the API is running

**Issue**: 500 Internal Server Error
- **Solution**: Check API logs in console or Serilog output

## 🌐 Multiple Environments

Create additional environments for different deployment stages:

### Development Environment
```json
{
  "base_url": "http://localhost:5000",
  "tenant_id": "dev-tenant-id"
}
```

### Staging Environment
```json
{
  "base_url": "https://staging-api.xframework.com",
  "tenant_id": "staging-tenant-id"
}
```

### Production Environment
```json
{
  "base_url": "https://api.xframework.com",
  "tenant_id": "prod-tenant-id"
}
```

## 📈 Performance Testing

Use Postman's Collection Runner for basic load testing:

1. Click **Runner** button
2. Select **XFramework API** collection
3. Configure:
   - Iterations: 100
   - Delay: 100ms
   - Data file: Optional CSV with test data
4. Click **Run XFramework API**

For proper load testing, use tools like:
- **k6** - Modern load testing tool
- **Apache JMeter** - Enterprise-grade load testing
- **Gatling** - Scala-based performance testing

## 🔗 Additional Resources

- [XFramework Developer Onboarding Guide](../guides/developer-onboarding.md)
- [API Documentation Guide](../guides/api-documentation.md)
- [VSA Migration Guide](../guides/vsa-migration-guide.md)
- [Result Pattern Guide](../patterns/result-pattern-guide.md)

## 📝 Collection Maintenance

### Adding New Requests

1. Create request in appropriate folder
2. Use environment variables (`{{base_url}}`, etc.)
3. Add example responses
4. Document in this guide

### Updating for New API Versions

1. Duplicate collection
2. Rename to include version (e.g., "XFramework API v4")
3. Update `api_version` variable
4. Test all endpoints for breaking changes

## 🤝 Contributing

When adding endpoints to the collection:

1. ✅ Use environment variables for all dynamic values
2. ✅ Add request descriptions
3. ✅ Include example request/response bodies
4. ✅ Use proper HTTP methods (GET, POST, PUT, DELETE)
5. ✅ Group related endpoints in folders
6. ✅ Add pre-request scripts for complex auth flows
7. ✅ Add tests for automatic validation (optional)

## 📞 Support

For issues or questions:
- Review the [Troubleshooting Guide](../guides/developer-onboarding.md#troubleshooting)
- Check API logs at `ServiceLogs/` directory
- Review Swagger documentation at `http://localhost:5000/swagger`

---

**Last Updated**: 2025-01-20  
**Collection Version**: 3.0.0  
**Compatible API Version**: 3.0+