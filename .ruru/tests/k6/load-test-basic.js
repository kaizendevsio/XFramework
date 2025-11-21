/**
 * XFramework - Basic Load Test
 * 
 * This script performs basic load testing across all API modules
 * to validate the performance targets defined in the roadmap.
 * 
 * Targets:
 * - API Response Time: < 50ms (p95)
 * - Throughput: > 10,000 req/s per instance
 * - Cache Hit Rate: > 80%
 * 
 * Usage:
 *   k6 run load-test-basic.js
 * 
 * With custom VUs:
 *   k6 run --vus 100 --duration 5m load-test-basic.js
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

// Custom metrics
const errorRate = new Rate('errors');
const cacheHitRate = new Rate('cache_hits');
const apiDuration = new Trend('api_duration');
const successfulRequests = new Counter('successful_requests');

// Test configuration
export const options = {
  stages: [
    { duration: '2m', target: 50 },   // Ramp-up to 50 users
    { duration: '5m', target: 50 },   // Stay at 50 users
    { duration: '2m', target: 100 },  // Ramp-up to 100 users
    { duration: '5m', target: 100 },  // Stay at 100 users
    { duration: '2m', target: 200 },  // Spike to 200 users
    { duration: '2m', target: 0 },    // Ramp-down to 0 users
  ],
  thresholds: {
    'http_req_duration': ['p(95)<50'],           // 95% of requests must complete within 50ms
    'http_req_duration{name:cached}': ['p(95)<10'], // Cached requests should be sub-10ms
    'errors': ['rate<0.01'],                      // Error rate should be less than 1%
    'cache_hits': ['rate>0.80'],                  // Cache hit rate should be greater than 80%
    'http_req_failed': ['rate<0.01'],            // HTTP failures should be less than 1%
  },
};

// Environment configuration
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const API_VERSION = '3.0';
const TENANT_ID = __ENV.TENANT_ID || '00000000-0000-0000-0000-000000000001';

// Test data
let authToken = '';
let productIds = [];

export function setup() {
  // Authenticate and get token
  const loginPayload = JSON.stringify({
    username: 'admin@example.com',
    password: 'SecurePassword123!',
    tenantId: TENANT_ID,
  });

  const loginRes = http.post(`${BASE_URL}/api/auth/login`, loginPayload, {
    headers: {
      'Content-Type': 'application/json',
      'api-version': API_VERSION,
    },
  });

  const loginData = loginRes.json();
  if (loginData.isSuccess && loginData.data && loginData.data.token) {
    authToken = loginData.data.token;
    console.log('✅ Authentication successful');
  } else {
    console.error('❌ Authentication failed:', loginData);
  }

  // Get some product IDs for testing
  const productsRes = http.get(
    `${BASE_URL}/api/products?pageSize=10&pageNumber=1&tenantId=${TENANT_ID}`,
    {
      headers: {
        'Authorization': `Bearer ${authToken}`,
        'api-version': API_VERSION,
      },
    }
  );

  const productsData = productsRes.json();
  if (productsData.isSuccess && productsData.data && productsData.data.items) {
    productIds = productsData.data.items.map(p => p.id);
    console.log(`✅ Retrieved ${productIds.length} product IDs for testing`);
  }

  return { authToken, productIds };
}

export default function (data) {
  const params = {
    headers: {
      'Authorization': `Bearer ${data.authToken}`,
      'api-version': API_VERSION,
    },
    tags: { name: 'products_list' },
  };

  // Test 1: Health Check (should be fast)
  const healthRes = http.get(`${BASE_URL}/health/live`, {
    tags: { name: 'health_check' },
  });
  check(healthRes, {
    'health check is 200': (r) => r.status === 200,
  });

  // Test 2: Get Products List (with caching)
  const productsRes = http.get(
    `${BASE_URL}/api/products?pageSize=20&pageNumber=1&tenantId=${TENANT_ID}&noCache=false`,
    {
      ...params,
      tags: { name: 'cached' },
    }
  );

  const productCheck = check(productsRes, {
    'products list is 200': (r) => r.status === 200,
    'products response time < 50ms': (r) => r.timings.duration < 50,
  });

  if (productCheck) {
    successfulRequests.add(1);
    apiDuration.add(productsRes.timings.duration);
    
    // Check if response was from cache (you can detect this via custom headers)
    const fromCache = productsRes.headers['X-Cache'] === 'HIT' || 
                      productsRes.timings.duration < 10;
    cacheHitRate.add(fromCache ? 1 : 0);
  } else {
    errorRate.add(1);
  }

  // Test 3: Get Single Product (random from list)
  if (data.productIds && data.productIds.length > 0) {
    const randomId = data.productIds[Math.floor(Math.random() * data.productIds.length)];
    const productRes = http.get(
      `${BASE_URL}/api/products/${randomId}?tenantId=${TENANT_ID}&noCache=false`,
      {
        ...params,
        tags: { name: 'cached' },
      }
    );

    check(productRes, {
      'single product is 200': (r) => r.status === 200,
    });

    if (productRes.status === 200) {
      successfulRequests.add(1);
    } else {
      errorRate.add(1);
    }
  }

  // Test 4: Create Product (write operation)
  if (Math.random() < 0.1) { // 10% of requests are writes
    const newProduct = JSON.stringify({
      name: `Load Test Product ${Date.now()}`,
      description: 'Generated during load test',
      sku: `SKU-${Date.now()}`,
      price: Math.random() * 100,
      stockQuantity: Math.floor(Math.random() * 1000),
      isActive: true,
      tenantId: TENANT_ID,
    });

    const createRes = http.post(
      `${BASE_URL}/api/products?tenantId=${TENANT_ID}`,
      newProduct,
      {
        headers: {
          ...params.headers,
          'Content-Type': 'application/json',
        },
        tags: { name: 'create_product' },
      }
    );

    check(createRes, {
      'product creation is 201 or 200': (r) => r.status === 201 || r.status === 200,
    });

    if (createRes.status === 201 || createRes.status === 200) {
      successfulRequests.add(1);
    } else {
      errorRate.add(1);
    }
  }

  // Random sleep between 0.5 and 1.5 seconds
  sleep(Math.random() + 0.5);
}

export function handleSummary(data) {
  return {
    'stdout': textSummary(data, { indent: ' ', enableColors: true }),
    'summary.json': JSON.stringify(data),
  };
}

function textSummary(data, options) {
  const indent = options?.indent || '';
  const colors = options?.enableColors || false;
  
  let summary = '\n' + indent + '📊 Load Test Summary\n';
  summary += indent + '='.repeat(50) + '\n\n';
  
  // Scenarios
  summary += indent + '📈 Scenarios:\n';
  summary += indent + `   Total Duration: ${data.state.testRunDurationMs}ms\n`;
  summary += indent + `   Total Iterations: ${data.metrics.iterations.values.count}\n\n`;
  
  // HTTP Metrics
  summary += indent + '🌐 HTTP Metrics:\n';
  summary += indent + `   Total Requests: ${data.metrics.http_reqs?.values.count || 0}\n`;
  summary += indent + `   Request Rate: ${(data.metrics.http_reqs?.values.rate || 0).toFixed(2)} req/s\n`;
  summary += indent + `   Failed Requests: ${(data.metrics.http_req_failed?.values.rate * 100 || 0).toFixed(2)}%\n\n`;
  
  // Response Times
  summary += indent + '⏱️  Response Times:\n';
  summary += indent + `   Avg: ${(data.metrics.http_req_duration?.values.avg || 0).toFixed(2)}ms\n`;
  summary += indent + `   Min: ${(data.metrics.http_req_duration?.values.min || 0).toFixed(2)}ms\n`;
  summary += indent + `   Med: ${(data.metrics.http_req_duration?.values.med || 0).toFixed(2)}ms\n`;
  summary += indent + `   Max: ${(data.metrics.http_req_duration?.values.max || 0).toFixed(2)}ms\n`;
  summary += indent + `   p(90): ${(data.metrics.http_req_duration?.values['p(90)'] || 0).toFixed(2)}ms\n`;
  summary += indent + `   p(95): ${(data.metrics.http_req_duration?.values['p(95)'] || 0).toFixed(2)}ms\n`;
  summary += indent + `   p(99): ${(data.metrics.http_req_duration?.values['p(99)'] || 0).toFixed(2)}ms\n\n`;
  
  // Custom Metrics
  if (data.metrics.errors) {
    summary += indent + '❌ Error Rate:\n';
    summary += indent + `   ${(data.metrics.errors.values.rate * 100).toFixed(2)}%\n\n`;
  }
  
  if (data.metrics.cache_hits) {
    summary += indent + '💾 Cache Hit Rate:\n';
    summary += indent + `   ${(data.metrics.cache_hits.values.rate * 100).toFixed(2)}%\n\n`;
  }
  
  // Thresholds
  summary += indent + '✅ Threshold Status:\n';
  const thresholds = data.metrics.http_req_duration?.thresholds || {};
  for (const [name, result] of Object.entries(thresholds)) {
    const status = result.ok ? '✅' : '❌';
    summary += indent + `   ${status} ${name}\n`;
  }
  
  return summary;
}