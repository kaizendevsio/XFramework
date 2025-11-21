# Performance Testing Guide - XFramework VSA Migration

## Overview

This guide provides comprehensive performance testing procedures for XFramework's Vertical Slice Architecture (VSA) migration. It covers load testing scenarios, benchmarking procedures, k6 scripts, metrics, and bottleneck identification.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Testing Approach](#testing-approach)
- [Load Testing Scenarios](#load-testing-scenarios)
- [k6 Load Test Scripts](#k6-load-test-scripts)
- [Performance Benchmarking](#performance-benchmarking)
- [Metrics and Targets](#metrics-and-targets)
- [Bottleneck Identification](#bottleneck-identification)
- [Results Documentation](#results-documentation)

## Prerequisites

### Required Tools

```bash
# Install k6 (Windows)
winget install k6

# Install k6 (Linux/macOS)
brew install k6

# Verify installation
k6 version
```

### Additional Tools

- **OpenTelemetry Collector**: For distributed tracing analysis
- **Prometheus**: For metrics collection
- **Grafana**: For visualization
- **Redis**: Ensure Redis is running for cache testing
- **SQL Server**: Database must be accessible

### Environment Setup

```json
// appsettings.Performance.json
{
  "OpenTelemetry": {
    "Sampling": {
      "Probability": 1.0  // 100% sampling for performance testing
    },
    "Exporters": {
      "OTLP": {
        "Enabled": true,
        "Endpoint": "http://localhost:4317"
      }
    }
  },
  "CacheOptions": {
    "Enabled": true,
    "EnableL1Cache": true,
    "EnableL2Cache": true,
    "EnableStatistics": true
  }
}
```

## Testing Approach

### Test Levels

1. **Smoke Tests**: Verify basic functionality (5 VUs, 1 minute)
2. **Load Tests**: Normal expected load (50-100 VUs, 10 minutes)
3. **Stress Tests**: Above normal load (200-500 VUs, 15 minutes)
4. **Spike Tests**: Sudden traffic increases (10 → 500 VUs, 5 minutes)
5. **Soak Tests**: Extended duration (50 VUs, 2-4 hours)

### Module-Specific Testing

Test each module independently before integration:
- Authentication & Authorization
- Product/Catalog Management
- User Management
- Wallet/Transaction Processing
- Real-time (StreamFlow) Operations

## Load Testing Scenarios

### Scenario 1: Authentication Module

**Objective**: Test JWT authentication, token refresh, and user session management.

**Test Profile**:
- **Target Load**: 100 concurrent users
- **Duration**: 10 minutes
- **Expected Throughput**: 500-1000 req/sec
- **Success Criteria**: 
  - P95 latency < 200ms
  - Error rate < 0.1%
  - Token generation < 100ms

**Critical Endpoints**:
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `GET /api/auth/validate`
- `POST /api/auth/logout`

### Scenario 2: Product Catalog Module

**Objective**: Test read-heavy operations with caching.

**Test Profile**:
- **Target Load**: 200 concurrent users
- **Duration**: 15 minutes
- **Expected Throughput**: 1000-2000 req/sec
- **Success Criteria**:
  - P95 latency < 150ms (cached)
  - P95 latency < 500ms (uncached)
  - Cache hit ratio > 80%
  - Error rate < 0.1%

**Critical Endpoints**:
- `GET /api/products` (list with pagination)
- `GET /api/products/{id}` (detail view)
- `GET /api/products/search` (with filters)
- `GET /api/categories`

### Scenario 3: Wallet/Transaction Module

**Objective**: Test write-heavy operations with database transactions.

**Test Profile**:
- **Target Load**: 50 concurrent users
- **Duration**: 10 minutes
- **Expected Throughput**: 200-400 req/sec
- **Success Criteria**:
  - P95 latency < 500ms
  - Transaction consistency: 100%
  - Error rate < 0.01%
  - No deadlocks

**Critical Endpoints**:
- `POST /api/wallet/transfer`
- `POST /api/wallet/deposit`
- `POST /api/wallet/withdraw`
- `GET /api/wallet/{id}/balance`
- `GET /api/wallet/{id}/transactions`

### Scenario 4: Multi-Tenant Operations

**Objective**: Test tenant isolation and data segregation.

**Test Profile**:
- **Target Load**: 100 concurrent users (across 10 tenants)
- **Duration**: 15 minutes
- **Success Criteria**:
  - No data leakage between tenants
  - Consistent latency per tenant
  - Proper tenant context isolation

**Test Approach**:
- Use different tenant IDs in headers
- Verify response data belongs to correct tenant
- Monitor tenant-specific metrics

### Scenario 5: Real-time (StreamFlow) Module

**Objective**: Test WebSocket connections and message throughput.

**Test Profile**:
- **Target Load**: 500 concurrent WebSocket connections
- **Duration**: 20 minutes
- **Message Rate**: 10 messages/second per connection
- **Success Criteria**:
  - Message delivery < 50ms
  - Connection stability > 99%
  - No message loss
  - Memory stability

## k6 Load Test Scripts

### Base Configuration Script

```javascript
// config.js
export const BASE_URL = __ENV.BASE_URL || 'http://localhost:5106';
export const TENANT_ID = __ENV.TENANT_ID || 'tenant-001';

export const thresholds = {
  http_req_duration: ['p(95)<500', 'p(99)<1000'],
  http_req_failed: ['rate<0.01'],
  http_reqs: ['rate>100'],
};

export function getHeaders(token = null) {
  const headers = {
    'Content-Type': 'application/json',
    'X-Tenant-ID': TENANT_ID,
  };
  
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }
  
  return headers;
}

export function handleResponse(response, checkName) {
  check(response, {
    [`${checkName}: status is 200`]: (r) => r.status === 200,
    [`${checkName}: response time < 500ms`]: (r) => r.timings.duration < 500,
    [`${checkName}: has body`]: (r) => r.body.length > 0,
  });
}
```

### Script 1: Authentication Load Test

```javascript
// auth-load-test.js
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Trend } from 'k6/metrics';
import { BASE_URL, TENANT_ID, getHeaders } from './config.js';

// Custom metrics
const loginDuration = new Trend('login_duration');
const loginSuccesses = new Counter('login_successes');
const loginFailures = new Counter('login_failures');

export const options = {
  stages: [
    { duration: '2m', target: 50 },  // Ramp up
    { duration: '5m', target: 100 }, // Stay at 100 users
    { duration: '2m', target: 150 }, // Spike
    { duration: '3m', target: 100 }, // Back down
    { duration: '2m', target: 0 },   // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<200', 'p(99)<500'],
    http_req_failed: ['rate<0.001'],
    login_duration: ['p(95)<150'],
  },
};

const credentials = [
  { username: 'user1@example.com', password: 'Test@123' },
  { username: 'user2@example.com', password: 'Test@123' },
  { username: 'user3@example.com', password: 'Test@123' },
  // Add more test users
];

export default function () {
  const user = credentials[Math.floor(Math.random() * credentials.length)];
  
  // Login
  const loginPayload = JSON.stringify({
    email: user.username,
    password: user.password,
  });
  
  const loginStart = Date.now();
  const loginRes = http.post(
    `${BASE_URL}/api/auth/login`,
    loginPayload,
    { headers: getHeaders() }
  );
  
  const loginTime = Date.now() - loginStart;
  loginDuration.add(loginTime);
  
  const loginSuccess = check(loginRes, {
    'login: status is 200': (r) => r.status === 200,
    'login: has token': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body.data && body.data.token;
      } catch {
        return false;
      }
    },
  });
  
  if (loginSuccess) {
    loginSuccesses.add(1);
    const token = JSON.parse(loginRes.body).data.token;
    
    // Validate token
    const validateRes = http.get(
      `${BASE_URL}/api/auth/validate`,
      { headers: getHeaders(token) }
    );
    
    check(validateRes, {
      'validate: status is 200': (r) => r.status === 200,
    });
    
    sleep(1);
    
    // Refresh token (simulating long-lived session)
    const refreshRes = http.post(
      `${BASE_URL}/api/auth/refresh`,
      JSON.stringify({ token }),
      { headers: getHeaders(token) }
    );
    
    check(refreshRes, {
      'refresh: status is 200': (r) => r.status === 200,
    });
    
    sleep(1);
    
  } else {
    loginFailures.add(1);
  }
  
  sleep(Math.random() * 3 + 1); // Random think time 1-4 seconds
}
```

### Script 2: Product Catalog Load Test

```javascript
// product-load-test.js
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Trend, Rate } from 'k6/metrics';
import { BASE_URL, getHeaders } from './config.js';

// Custom metrics
const cacheHits = new Counter('cache_hits');
const cacheMisses = new Counter('cache_misses');
const cacheHitRate = new Rate('cache_hit_rate');
const listDuration = new Trend('product_list_duration');
const detailDuration = new Trend('product_detail_duration');

export const options = {
  stages: [
    { duration: '3m', target: 100 },  // Ramp up
    { duration: '10m', target: 200 }, // Sustained load
    { duration: '2m', target: 0 },    // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<150', 'p(99)<500'],
    http_req_failed: ['rate<0.001'],
    cache_hit_rate: ['rate>0.8'], // 80% cache hit rate
    product_list_duration: ['p(95)<200'],
    product_detail_duration: ['p(95)<100'],
  },
};

let authToken = null;

export function setup() {
  // Get auth token for all VUs
  const loginRes = http.post(
    `${BASE_URL}/api/auth/login`,
    JSON.stringify({
      email: 'testuser@example.com',
      password: 'Test@123',
    }),
    { headers: getHeaders() }
  );
  
  return { token: JSON.parse(loginRes.body).data.token };
}

export default function (data) {
  const token = data.token;
  const headers = getHeaders(token);
  
  // Test 1: List products (cached)
  const listStart = Date.now();
  const listRes = http.get(
    `${BASE_URL}/api/products?page=1&pageSize=20`,
    { headers }
  );
  listDuration.add(Date.now() - listStart);
  
  const listCached = check(listRes, {
    'list: status is 200': (r) => r.status === 200,
    'list: has products': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body.data && body.data.length > 0;
      } catch {
        return false;
      }
    },
    'list: from cache': (r) => r.headers['X-Cache'] === 'HIT',
  });
  
  if (listRes.headers['X-Cache'] === 'HIT') {
    cacheHits.add(1);
    cacheHitRate.add(true);
  } else {
    cacheMisses.add(1);
    cacheHitRate.add(false);
  }
  
  sleep(0.5);
  
  // Test 2: Get product detail (cached)
  const productId = Math.floor(Math.random() * 100) + 1;
  const detailStart = Date.now();
  const detailRes = http.get(
    `${BASE_URL}/api/products/${productId}`,
    { headers }
  );
  detailDuration.add(Date.now() - detailStart);
  
  check(detailRes, {
    'detail: status is 200 or 404': (r) => r.status === 200 || r.status === 404,
  });
  
  if (detailRes.headers['X-Cache'] === 'HIT') {
    cacheHits.add(1);
    cacheHitRate.add(true);
  } else {
    cacheMisses.add(1);
    cacheHitRate.add(false);
  }
  
  sleep(1);
  
  // Test 3: Search products
  const searchTerms = ['laptop', 'phone', 'tablet', 'headphones'];
  const term = searchTerms[Math.floor(Math.random() * searchTerms.length)];
  
  const searchRes = http.get(
    `${BASE_URL}/api/products/search?q=${term}`,
    { headers }
  );
  
  check(searchRes, {
    'search: status is 200': (r) => r.status === 200,
  });
  
  sleep(Math.random() * 2 + 1); // Think time 1-3 seconds
}
```

### Script 3: Wallet Transaction Load Test

```javascript
// wallet-load-test.js
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Trend } from 'k6/metrics';
import { BASE_URL, getHeaders } from './config.js';

// Custom metrics
const transferSuccesses = new Counter('transfer_successes');
const transferFailures = new Counter('transfer_failures');
const transferDuration = new Trend('transfer_duration');
const balanceCheckDuration = new Trend('balance_check_duration');

export const options = {
  stages: [
    { duration: '2m', target: 20 },  // Ramp up slowly
    { duration: '10m', target: 50 }, // Sustained transactional load
    { duration: '2m', target: 0 },   // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<500', 'p(99)<1000'],
    http_req_failed: ['rate<0.0001'], // Very low error tolerance for transactions
    transfer_duration: ['p(95)<800'],
  },
};

export function setup() {
  const loginRes = http.post(
    `${BASE_URL}/api/auth/login`,
    JSON.stringify({
      email: 'testuser@example.com',
      password: 'Test@123',
    }),
    { headers: getHeaders() }
  );
  
  return { token: JSON.parse(loginRes.body).data.token };
}

export default function (data) {
  const token = data.token;
  const headers = getHeaders(token);
  
  // Test 1: Check balance
  const walletId = `wallet-${Math.floor(Math.random() * 10) + 1}`;
  const balanceStart = Date.now();
  const balanceRes = http.get(
    `${BASE_URL}/api/wallet/${walletId}/balance`,
    { headers }
  );
  balanceCheckDuration.add(Date.now() - balanceStart);
  
  check(balanceRes, {
    'balance: status is 200': (r) => r.status === 200,
    'balance: has amount': (r) => {
      try {
        const body = JSON.parse(r.body);
        return typeof body.data.balance === 'number';
      } catch {
        return false;
      }
    },
  });
  
  sleep(1);
  
  // Test 2: Transfer funds (small random amount)
  const amount = Math.floor(Math.random() * 100) + 10;
  const targetWallet = `wallet-${Math.floor(Math.random() * 10) + 1}`;
  
  const transferPayload = JSON.stringify({
    fromWalletId: walletId,
    toWalletId: targetWallet,
    amount: amount,
    currency: 'USD',
    description: 'Load test transfer',
  });
  
  const transferStart = Date.now();
  const transferRes = http.post(
    `${BASE_URL}/api/wallet/transfer`,
    transferPayload,
    { headers }
  );
  const transferTime = Date.now() - transferStart;
  transferDuration.add(transferTime);
  
  const transferSuccess = check(transferRes, {
    'transfer: status is 200 or 400': (r) => r.status === 200 || r.status === 400,
    'transfer: has transaction id': (r) => {
      if (r.status === 200) {
        try {
          const body = JSON.parse(r.body);
          return body.data && body.data.transactionId;
        } catch {
          return false;
        }
      }
      return true; // 400 is acceptable (insufficient funds, etc.)
    },
  });
  
  if (transferRes.status === 200) {
    transferSuccesses.add(1);
  } else {
    transferFailures.add(1);
  }
  
  sleep(2);
  
  // Test 3: Get transaction history
  const historyRes = http.get(
    `${BASE_URL}/api/wallet/${walletId}/transactions?page=1&pageSize=10`,
    { headers }
  );
  
  check(historyRes, {
    'history: status is 200': (r) => r.status === 200,
  });
  
  sleep(Math.random() * 3 + 2); // Think time 2-5 seconds
}
```

### Script 4: Spike Test (Traffic Burst)

```javascript
// spike-test.js
import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_URL, getHeaders } from './config.js';

export const options = {
  stages: [
    { duration: '2m', target: 10 },   // Normal load
    { duration: '30s', target: 500 }, // Sudden spike!
    { duration: '3m', target: 500 },  // Maintain spike
    { duration: '2m', target: 10 },   // Back to normal
    { duration: '1m', target: 0 },    // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<1000'], // More relaxed during spike
    http_req_failed: ['rate<0.05'],     // Allow some failures during spike
  },
};

export function setup() {
  const loginRes = http.post(
    `${BASE_URL}/api/auth/login`,
    JSON.stringify({
      email: 'testuser@example.com',
      password: 'Test@123',
    }),
    { headers: getHeaders() }
  );
  
  return { token: JSON.parse(loginRes.body).data.token };
}

export default function (data) {
  const token = data.token;
  const headers = getHeaders(token);
  
  // Mix of operations during spike
  const operations = [
    () => http.get(`${BASE_URL}/api/products?page=1&pageSize=20`, { headers }),
    () => http.get(`${BASE_URL}/api/products/${Math.floor(Math.random() * 100)}`, { headers }),
    () => http.get(`${BASE_URL}/api/categories`, { headers }),
    () => http.get(`${BASE_URL}/health`, { headers }),
  ];
  
  const op = operations[Math.floor(Math.random() * operations.length)];
  const res = op();
  
  check(res, {
    'spike: request completed': (r) => r.status < 500,
  });
  
  sleep(0.1); // Minimal think time during spike
}
```

### Script 5: Soak Test (Endurance)

```javascript
// soak-test.js
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend } from 'k6/metrics';
import { BASE_URL, getHeaders } from './config.js';

const memoryTrend = new Trend('memory_usage');

export const options = {
  stages: [
    { duration: '5m', target: 50 },    // Ramp up
    { duration: '2h', target: 50 },    // Sustained load for 2 hours
    { duration: '5m', target: 0 },     // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'],
    http_req_failed: ['rate<0.001'],
  },
};

export function setup() {
  const loginRes = http.post(
    `${BASE_URL}/api/auth/login`,
    JSON.stringify({
      email: 'testuser@example.com',
      password: 'Test@123',
    }),
    { headers: getHeaders() }
  );
  
  return { token: JSON.parse(loginRes.body).data.token };
}

export default function (data) {
  const token = data.token;
  const headers = getHeaders(token);
  
  // Realistic user journey
  // 1. Browse products
  const listRes = http.get(
    `${BASE_URL}/api/products?page=${Math.floor(Math.random() * 5) + 1}&pageSize=20`,
    { headers }
  );
  check(listRes, { 'list: status 200': (r) => r.status === 200 });
  sleep(2);
  
  // 2. View product details
  const productId = Math.floor(Math.random() * 100) + 1;
  const detailRes = http.get(
    `${BASE_URL}/api/products/${productId}`,
    { headers }
  );
  check(detailRes, { 'detail: status 200 or 404': (r) => r.status === 200 || r.status === 404 });
  sleep(3);
  
  // 3. Check health periodically (monitor memory leaks)
  if (Math.random() < 0.1) { // 10% of requests
    const healthRes = http.get(`${BASE_URL}/health`, { headers });
    check(healthRes, { 'health: status 200': (r) => r.status === 200 });
    
    // Extract memory usage if available
    try {
      const healthData = JSON.parse(healthRes.body);
      if (healthData.entries && healthData.entries.memory) {
        const memoryMB = healthData.entries.memory.data.allocated / 1024 / 1024;
        memoryTrend.add(memoryMB);
      }
    } catch {}
  }
  
  sleep(Math.random() * 5 + 3); // Think time 3-8 seconds
}
```

## Performance Benchmarking

### Benchmark Execution

```bash
# Run individual test
k6 run auth-load-test.js

# Run with custom thresholds
k6 run --vus 100 --duration 10m product-load-test.js

# Run with environment variables
k6 run -e BASE_URL=https://staging.api.com -e TENANT_ID=tenant-002 auth-load-test.js

# Output to JSON for analysis
k6 run --out json=results.json product-load-test.js

# Output to InfluxDB for Grafana
k6 run --out influxdb=http://localhost:8086/k6 wallet-load-test.js
```

### Database Benchmarking

Test EF Core query performance:

```csharp
// PerformanceBenchmark.cs
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

[MemoryDiagnoser]
[RankColumn]
public class QueryBenchmarks
{
    private AppDbContext _context;
    
    [GlobalSetup]
    public void Setup()
    {
        // Initialize DbContext
        _context = new AppDbContext(options);
    }
    
    [Benchmark(Baseline = true)]
    public async Task<List<Product>> GetProducts_WithTracking()
    {
        return await _context.Products
            .Include(p => p.Category)
            .ToListAsync();
    }
    
    [Benchmark]
    public async Task<List<Product>> GetProducts_NoTracking()
    {
        return await _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .ToListAsync();
    }
    
    [Benchmark]
    public async Task<List<Product>> GetProducts_SplitQuery()
    {
        return await _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .AsSplitQuery()
            .ToListAsync();
    }
    
    [Benchmark]
    public async Task<List<Product>> GetProducts_Cached()
    {
        var cacheKey = "products:all";
        return await _cacheService.GetOrSetAsync(
            cacheKey,
            async ct => await _context.Products.AsNoTracking().ToListAsync(),
            TimeSpan.FromMinutes(10)
        );
    }
}
```

Run benchmark:

```bash
dotnet run -c Release --project PerformanceBenchmarks
```

## Metrics and Targets

### Response Time Targets

| Endpoint Type | P50 | P95 | P99 | Max |
|--------------|-----|-----|-----|-----|
| Health Check | <10ms | <20ms | <50ms | <100ms |
| Cached Reads | <50ms | <100ms | <150ms | <300ms |
| Uncached Reads | <200ms | <400ms | <800ms | <1500ms |
| Simple Writes | <300ms | <500ms | <800ms | <1500ms |
| Complex Transactions | <500ms | <1000ms | <2000ms | <3000ms |

### Throughput Targets

| Module | Expected RPS | Max RPS | Success Rate |
|--------|-------------|---------|--------------|
| Authentication | 500-1000 | 2000 | >99.9% |
| Product Catalog (read) | 1000-2000 | 5000 | >99.5% |
| Product Catalog (write) | 100-200 | 500 | >99.9% |
| Wallet Transactions | 200-400 | 800 | >99.99% |
| Real-time (StreamFlow) | 5000 msgs/sec | 10000 | >99.95% |

### Resource Utilization Targets

| Resource | Normal Load | Peak Load | Critical Threshold |
|----------|------------|-----------|-------------------|
| CPU | <40% | <70% | >85% |
| Memory | <60% | <80% | >90% |
| Database Connections | <50% pool | <80% pool | >90% pool |
| Redis Memory | <1GB | <2GB | >4GB |
| Disk I/O | <50% | <75% | >90% |

### Cache Performance Targets

| Metric | Target | Measurement |
|--------|--------|-------------|
| Cache Hit Ratio | >80% | Monitor via `ICacheService.GetStatistics()` |
| L1 Cache Hit Ratio | >60% | Memory cache hits / total requests |
| L2 Cache Hit Ratio | >90% | Redis cache hits / L1 misses |
| Cache Lookup Time (L1) | <1ms | Average memory lookup |
| Cache Lookup Time (L2) | <5ms | Average Redis lookup |

### Database Performance Targets

| Metric | Target | Measurement |
|--------|--------|-------------|
| Query Execution Time (simple) | <20ms | EF Core query timing |
| Query Execution Time (complex) | <100ms | With joins and filtering |
| Connection Pool Saturation | <80% | Active connections / max pool size |
| Deadlock Rate | 0 | SQL Server profiling |
| Index Usage | >95% | All queries should use indexes |

## Bottleneck Identification

### Step 1: Analyze k6 Results

```bash
# Generate HTML report
k6 run --out json=results.json test.js
# Use k6-reporter to generate HTML
k6-reporter results.json
```

Look for:
- High P95/P99 latencies
- Increased error rates
- Declining throughput over time
- Memory growth trends

### Step 2: OpenTelemetry Tracing Analysis

Access Jaeger UI at `http://localhost:16686`:

1. **Find Slow Traces**:
   - Filter by `duration > 500ms`
   - Look for operations taking longest time
   - Identify database queries, external calls

2. **Analyze Span Distribution**:
   - Database operations should be <50% of total time
   - Cache lookups should be <5ms
   - Business logic should be <100ms

3. **Check for N+1 Queries**:
   - Multiple sequential database calls
   - Missing `Include()` statements
   - Should use `AsSplitQuery()` for collections

### Step 3: Database Performance Analysis

```sql
-- Find slow queries
SELECT TOP 20
    qs.execution_count,
    SUBSTRING(qt.text, (qs.statement_start_offset/2)+1,
        ((CASE qs.statement_end_offset
            WHEN -1 THEN DATALENGTH(qt.text)
            ELSE qs.statement_end_offset
        END - qs.statement_start_offset)/2) + 1) AS query_text,
    qs.total_elapsed_time / qs.execution_count AS avg_time_microseconds,
    qs.total_logical_reads / qs.execution_count AS avg_logical_reads,
    qs.creation_time,
    qs.last_execution_time
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) qt
WHERE qt.text LIKE '%Products%' -- Filter by table
ORDER BY qs.total_elapsed_time / qs.execution_count DESC;

-- Check index usage
SELECT 
    OBJECT_NAME(s.object_id) AS TableName,
    i.name AS IndexName,
    s.user_seeks,
    s.user_scans,
    s.user_lookups,
    s.user_updates
FROM sys.dm_db_index_usage_stats s
INNER JOIN sys.indexes i ON s.object_id = i.object_id 
    AND s.index_id = i.index_id
WHERE s.database_id = DB_ID()
ORDER BY s.user_seeks + s.user_scans + s.user_lookups DESC;

-- Check for missing indexes
SELECT TOP 20
    migs.avg_total_user_cost * (migs.avg_user_impact / 100.0) * (migs.user_seeks + migs.user_scans) AS improvement_measure,
    'CREATE INDEX idx_' + CONVERT(varchar, mig.index_group_handle) + '_' + 
        CONVERT(varchar, mid.index_handle) +
        ' ON ' + mid.statement + ' (' + ISNULL(mid.equality_columns, '') +
        CASE WHEN mid.equality_columns IS NOT NULL AND mid.inequality_columns IS NOT NULL THEN ',' ELSE '' END +
        ISNULL(mid.inequality_columns, '') + ')' +
        ISNULL(' INCLUDE (' + mid.included_columns + ')', '') AS create_index_statement
FROM sys.dm_db_missing_index_groups mig
INNER JOIN sys.dm_db_missing_index_group_stats migs ON migs.group_handle = mig.index_group_handle
INNER JOIN sys.dm_db_missing_index_details mid ON mig.index_handle = mid.index_handle
ORDER BY improvement_measure DESC;
```

### Step 4: Redis Performance Analysis

```bash
# Connect to Redis CLI
redis-cli

# Check memory usage
INFO memory

# Check hit rate
INFO stats

# Monitor commands in real-time
MONITOR

# Check slow log
SLOWLOG GET 10

# Check key distribution
DBSIZE
KEYS *:*  # Use with caution in production

# Memory analysis by key pattern
MEMORY USAGE product:*
```

### Step 5: Application Metrics Analysis

Monitor via Prometheus/Grafana:

```promql
# Request rate by endpoint
rate(http_requests_total[5m])

# P95 latency by endpoint
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))

# Error rate
rate(http_requests_total{status=~"5.."}[5m]) / rate(http_requests_total[5m])

# Cache hit ratio
rate(cache_hits_total[5m]) / (rate(cache_hits_total[5m]) + rate(cache_misses_total[5m]))

# Database connection pool usage
db_connections_active / db_connections_max

# Memory usage
process_resident_memory_bytes
```

### Common Bottlenecks and Solutions

| Bottleneck | Symptom | Solution |
|------------|---------|----------|
| **Missing Indexes** | Slow queries, high CPU | Add indexes to filtered/joined columns |
| **N+1 Queries** | Many sequential DB calls | Use `Include()`, `ThenInclude()`, `AsSplitQuery()` |
| **Entity Tracking** | High memory, slow queries | Use `AsNoTracking()` for read-only queries |
| **No Caching** | High DB load, slow responses | Implement output caching or application caching |
| **Cache Misses** | Inconsistent latency | Optimize cache keys, increase TTL, warm cache |
| **Connection Pool Exhaustion** | Timeout errors | Increase pool size, fix connection leaks |
| **Large Result Sets** | High memory, slow responses | Implement pagination, use projections |
| **Blocking Operations** | Thread pool starvation | Use async/await everywhere |
| **No Compression** | High bandwidth usage | Enable response compression (already configured) |
| **Inefficient Serialization** | High CPU, slow responses | Use System.Text.Json (default), avoid reflection |

## Results Documentation

### Test Execution Template

```markdown
# Performance Test Results - [Date]

## Test Configuration
- **Environment**: [Staging/Production-like]
- **Test Tool**: k6 v[version]
- **Test Duration**: [duration]
- **Target Load**: [VUs/RPS]
- **Test Script**: [script name]

## System Configuration
- **Server**: [specs]
- **Database**: SQL Server [version], [size]
- **Redis**: [version], [memory]
- **Application**: .NET 9, [other details]

## Results Summary

### Response Times
| Metric | Value | Target | Status |
|--------|-------|--------|--------|
| P50 | Xms | <Yms | ✅/❌ |
| P95 | Xms | <Yms | ✅/❌ |
| P99 | Xms | <Yms | ✅/❌ |
| Max | Xms | <Yms | ✅/❌ |

### Throughput
- **Average RPS**: X req/sec
- **Peak RPS**: Y req/sec
- **Success Rate**: Z%

### Resource Utilization
- **CPU (avg)**: X%
- **CPU (peak)**: Y%
- **Memory (avg)**: XGB
- **Memory (peak)**: YGB

### Cache Performance
- **Hit Ratio**: X%
- **L1 Hits**: Y%
- **L2 Hits**: Z%

## Identified Issues
1. [Issue description]
   - **Impact**: [severity]
   - **Root Cause**: [analysis]
   - **Recommended Fix**: [solution]

## Recommendations
1. [Recommendation]
2. [Recommendation]

## Appendix
- k6 output: [file path]
- Database query analysis: [file path]
- Trace samples: [Jaeger links]
```

### Grafana Dashboard Template

Create a dashboard with these panels:

1. **Request Rate** (Graph)
   - Query: `rate(http_requests_total[1m])`
   - Split by endpoint

2. **Response Time Percentiles** (Graph)
   - P50, P95, P99 latencies
   - Split by endpoint

3. **Error Rate** (Graph)
   - 4xx and 5xx rates
   - Alert if > 1%

4. **Cache Hit Ratio** (Gauge)
   - Current hit ratio
   - Alert if < 80%

5. **Resource Utilization** (Graph)
   - CPU, Memory, Disk I/O

6. **Active Connections** (Graph)
   - Database pool usage
   - Redis connections

7. **Top Slow Endpoints** (Table)
   - Endpoint, P95 latency
   - Sorted by latency

## Continuous Performance Testing

### CI/CD Integration

```yaml
# .github/workflows/performance-test.yml
name: Performance Tests

on:
  pull_request:
    paths:
      - 'src/**'
  schedule:
    - cron: '0 2 * * *'  # Daily at 2 AM

jobs:
  performance-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup k6
        run: |
          curl https://github.com/grafana/k6/releases/download/v0.47.0/k6-v0.47.0-linux-amd64.tar.gz -L | tar xvz
          sudo cp k6-v0.47.0-linux-amd64/k6 /usr/bin/
      
      - name: Run smoke test
        run: k6 run --vus 5 --duration 1m tests/k6/smoke-test.js
      
      - name: Run load test
        run: k6 run --vus 50 --duration 5m tests/k6/load-test.js
      
      - name: Check thresholds
        if: failure()
        run: echo "Performance tests failed - thresholds not met"
      
      - name: Upload results
        uses: actions/upload-artifact@v3
        with:
          name: performance-results
          path: results/
```

### Performance Regression Detection

Compare baseline vs current:

```javascript
// regression-check.js
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.1/index.js';

export function handleSummary(data) {
  const baseline = JSON.parse(open('./baseline.json'));
  
  const regressions = [];
  
  // Check P95 latency regression
  const currentP95 = data.metrics.http_req_duration.values['p(95)'];
  const baselineP95 = baseline.metrics.http_req_duration.values['p(95)'];
  
  if (currentP95 > baselineP95 * 1.2) { // 20% regression threshold
    regressions.push(`P95 latency regression: ${currentP95}ms vs ${baselineP95}ms baseline`);
  }
  
  // Check throughput regression
  const currentRPS = data.metrics.http_reqs.values.rate;
  const baselineRPS = baseline.metrics.http_reqs.values.rate;
  
  if (currentRPS < baselineRPS * 0.8) { // 20% degradation
    regressions.push(`Throughput regression: ${currentRPS} RPS vs ${baselineRPS} RPS baseline`);
  }
  
  if (regressions.length > 0) {
    console.error('Performance Regressions Detected:');
    regressions.forEach(r => console.error(`  - ${r}`));
    return { 'stdout': textSummary(data, { indent: ' ', enableColors: true }) };
  }
  
  return { 'stdout': textSummary(data, { indent: ' ', enableColors: true }) };
}
```

## Related Documentation

- [Monitoring and Alerting Guide](./monitoring-alerting-guide.md)
- [OpenTelemetry Guide](../observability/opentelemetry-guide.md)
- [Caching Strategy](../caching-strategy.md)
- [Deployment Runbook](./deployment-runbook.md)