# Monitoring and Alerting Guide - XFramework VSA Migration

## Overview

This guide provides comprehensive monitoring and alerting setup for XFramework's Vertical Slice Architecture (VSA). It covers OpenTelemetry configuration, metrics collection, alert rules, dashboard design, log aggregation, and SLI/SLO definitions.

## Table of Contents

- [Monitoring Architecture](#monitoring-architecture)
- [OpenTelemetry Setup](#opentelemetry-setup)
- [Key Metrics](#key-metrics)
- [Alert Configuration](#alert-configuration)
- [Dashboard Recommendations](#dashboard-recommendations)
- [Log Aggregation](#log-aggregation)
- [SLI and SLO Definitions](#sli-and-slo-definitions)
- [Troubleshooting](#troubleshooting)

## Monitoring Architecture

### Stack Components

```
┌─────────────────────────────────────────────┐
│           XFramework Applications           │
│  (ASP.NET Core APIs with OpenTelemetry)    │
└──────────────┬──────────────────────────────┘
               │
               │ OTLP (gRPC/HTTP)
               ▼
┌──────────────────────────────────────────────┐
│      OpenTelemetry Collector                 │
│  - Receives: Traces, Metrics, Logs          │
│  - Processes: Batching, Sampling            │
│  - Exports: To multiple backends            │
└──────┬───────────────────────┬───────────────┘
       │                       │
       │ Traces                │ Metrics
       ▼                       ▼
┌─────────────┐         ┌─────────────┐
│   Jaeger    │         │ Prometheus  │
│  (Tracing)  │         │  (Metrics)  │
└─────────────┘         └──────┬──────┘
                               │
                               │ Query
                               ▼
                        ┌─────────────┐
                        │   Grafana   │
                        │ (Dashboards │
                        │  & Alerts)  │
                        └─────────────┘
                               ▲
                               │ Logs
                        ┌──────┴──────┐
                        │     Loki    │
                        │(Log Aggr.)  │
                        └─────────────┘
```

### Data Flow

1. **Application → OTel Collector**
   - Metrics: Pushed every 10 seconds
   - Traces: Sent as they occur (with sampling)
   - Logs: Streamed continuously

2. **OTel Collector → Backends**
   - Batches data for efficiency
   - Routes to appropriate backend
   - Handles retries and buffering

3. **Visualization & Alerting**
   - Grafana queries Prometheus for metrics
   - Grafana queries Loki for logs
   - Grafana queries Jaeger for traces
   - Alert rules evaluated in Prometheus

## OpenTelemetry Setup

### Installation (Already Configured)

XFramework includes OpenTelemetry via [`XFrameworkCore`](../../../src/Kernel/XFramework.Core/Extensions/OpenTelemetryExtensions.cs). Reference the [OpenTelemetry Guide](../../observability/opentelemetry-guide.md) for detailed setup.

### Production Configuration

```json
// appsettings.Production.json
{
  "OpenTelemetry": {
    "ServiceName": "XFramework.YourModule.Api",
    "ServiceVersion": "1.2.0",
    "Sampling": {
      "Probability": 0.1  // 10% sampling in production
    },
    "Exporters": {
      "Console": {
        "Enabled": false  // Disable in production
      },
      "OTLP": {
        "Enabled": true,
        "Endpoint": "http://otel-collector:4317",
        "Protocol": "grpc"
      }
    },
    "Resources": {
      "Environment": "production",
      "Region": "ap-southeast-1",
      "Cluster": "prod-cluster-1"
    }
  }
}
```

### OpenTelemetry Collector Configuration

```yaml
# otel-collector-config.yaml
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317
      http:
        endpoint: 0.0.0.0:4318

processors:
  batch:
    timeout: 10s
    send_batch_size: 1024
  
  memory_limiter:
    check_interval: 1s
    limit_mib: 512
  
  resourcedetection:
    detectors: [env, system]
  
  attributes:
    actions:
      - key: environment
        value: production
        action: insert

exporters:
  # Prometheus for metrics
  prometheus:
    endpoint: "0.0.0.0:8889"
  
  # Jaeger for traces
  jaeger:
    endpoint: jaeger:14250
    tls:
      insecure: true
  
  # Loki for logs
  loki:
    endpoint: http://loki:3100/loki/api/v1/push
  
  # Logging exporter for debugging
  logging:
    loglevel: info

service:
  pipelines:
    traces:
      receivers: [otlp]
      processors: [memory_limiter, batch, resourcedetection]
      exporters: [jaeger, logging]
    
    metrics:
      receivers: [otlp]
      processors: [memory_limiter, batch, resourcedetection, attributes]
      exporters: [prometheus]
    
    logs:
      receivers: [otlp]
      processors: [memory_limiter, batch]
      exporters: [loki]
```

### Deployment (Docker Compose)

```yaml
# docker-compose.monitoring.yml
version: '3.8'

services:
  otel-collector:
    image: otel/opentelemetry-collector-contrib:0.89.0
    command: ["--config=/etc/otel-collector-config.yaml"]
    volumes:
      - ./otel-collector-config.yaml:/etc/otel-collector-config.yaml
    ports:
      - "4317:4317"  # OTLP gRPC
      - "4318:4318"  # OTLP HTTP
      - "8889:8889"  # Prometheus metrics
    networks:
      - monitoring

  prometheus:
    image: prom/prometheus:v2.47.0
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus-data:/prometheus
    ports:
      - "9090:9090"
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
      - '--storage.tsdb.retention.time=30d'
    networks:
      - monitoring

  grafana:
    image: grafana/grafana:10.2.0
    volumes:
      - grafana-data:/var/lib/grafana
      - ./grafana/dashboards:/etc/grafana/provisioning/dashboards
      - ./grafana/datasources:/etc/grafana/provisioning/datasources
    ports:
      - "3000:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
      - GF_INSTALL_PLUGINS=grafana-piechart-panel
    networks:
      - monitoring

  jaeger:
    image: jaegertracing/all-in-one:1.51
    ports:
      - "16686:16686"  # Jaeger UI
      - "14250:14250"  # Model proto
    environment:
      - COLLECTOR_OTLP_ENABLED=true
    networks:
      - monitoring

  loki:
    image: grafana/loki:2.9.0
    ports:
      - "3100:3100"
    command: -config.file=/etc/loki/local-config.yaml
    networks:
      - monitoring

volumes:
  prometheus-data:
  grafana-data:

networks:
  monitoring:
    driver: bridge
```

```bash
# Start monitoring stack
docker-compose -f docker-compose.monitoring.yml up -d

# Verify services
curl http://localhost:9090/-/healthy  # Prometheus
curl http://localhost:3000/api/health  # Grafana
curl http://localhost:16686/          # Jaeger
```

## Key Metrics

### Application Metrics

#### HTTP Request Metrics

```csharp
// Automatically collected by OpenTelemetry ASP.NET Core instrumentation

// Available metrics:
// - http_server_request_duration (histogram) - Request duration in seconds
// - http_server_active_requests (gauge) - Number of active requests
// - http_server_request_body_size (histogram) - Request body size
// - http_server_response_body_size (histogram) - Response body size
```

**PromQL Queries**:

```promql
# Request rate (requests per second)
rate(http_server_request_duration_count[5m])

# P95 latency
histogram_quantile(0.95, rate(http_server_request_duration_bucket[5m]))

# Error rate (5xx responses)
sum(rate(http_server_request_duration_count{http_status_code=~"5.."}[5m])) 
/ 
sum(rate(http_server_request_duration_count[5m]))

# Requests by endpoint
sum by (http_route) (rate(http_server_request_duration_count[5m]))
```

#### Custom Business Metrics

```csharp
// From XFrameworkMetrics.cs

// Products
XFrameworkMetrics.ProductsCreated.Add(1, 
    new KeyValuePair<string, object?>("category_id", categoryId));

XFrameworkMetrics.ProductCreationDuration.Record(stopwatch.ElapsedMilliseconds,
    new KeyValuePair<string, object?>("result", "success"));

// Wallets
XFrameworkMetrics.WalletIncrements.Add(1);
XFrameworkMetrics.WalletOperationDuration.Record(duration);
XFrameworkMetrics.WalletTransactionAmount.Record(amount);

// Authentication
XFrameworkMetrics.AuthenticationAttempts.Add(1,
    new KeyValuePair<string, object?>("result", "success"));
```

**PromQL Queries**:

```promql
# Product creation rate
rate(xframework_products_created_total[5m])

# Average wallet operation duration
rate(xframework_wallet_operation_duration_sum[5m]) 
/ 
rate(xframework_wallet_operation_duration_count[5m])

# Authentication success rate
sum(rate(xframework_authentication_attempts_total{result="success"}[5m])) 
/ 
sum(rate(xframework_authentication_attempts_total[5m]))
```

### Infrastructure Metrics

#### Database Metrics

```promql
# Connection pool usage
db_connection_pool_active / db_connection_pool_max

# Query duration P95
histogram_quantile(0.95, rate(db_query_duration_bucket[5m]))

# Query error rate
rate(db_query_errors_total[5m])

# Deadlocks
rate(db_deadlocks_total[5m])
```

#### Cache Metrics

```csharp
// L1 Cache (Memory)
process_memory_working_set_bytes  // Memory usage

// L2 Cache (Redis)
redis_connected_clients
redis_used_memory_bytes
redis_keyspace_hits_total
redis_keyspace_misses_total
```

**PromQL Queries**:

```promql
# Cache hit ratio
sum(rate(redis_keyspace_hits_total[5m])) 
/ 
(sum(rate(redis_keyspace_hits_total[5m])) + sum(rate(redis_keyspace_misses_total[5m])))

# Memory cache usage
process_memory_working_set_bytes / 1024 / 1024  # Convert to MB

# Redis memory usage
redis_used_memory_bytes / 1024 / 1024  # Convert to MB
```

#### Resource Metrics

```promql
# CPU usage
rate(process_cpu_seconds_total[5m]) * 100

# Memory usage
process_working_set_bytes / 1024 / 1024 / 1024  # GB

# Disk usage
node_filesystem_avail_bytes{mountpoint="/"} 
/ 
node_filesystem_size_bytes{mountpoint="/"} * 100

# Network I/O
rate(node_network_receive_bytes_total[5m])
rate(node_network_transmit_bytes_total[5m])
```

### Health Check Metrics

```promql
# Health check status (1 = healthy, 0 = unhealthy)
health_check_status

# Health check duration
health_check_duration_seconds

# Example: Database health
health_check_status{check_name="database"}
```

## Alert Configuration

### Alert Rules (Prometheus)

```yaml
# prometheus-alerts.yml
groups:
  - name: xframework_critical
    interval: 30s
    rules:
      # P0 Alerts
      - alert: APICompletelyDown
        expr: up{job="xframework-api"} == 0
        for: 2m
        labels:
          severity: critical
          priority: P0
        annotations:
          summary: "API service is completely down"
          description: "{{ $labels.instance }} has been down for more than 2 minutes"
          runbook_url: "https://wiki.xframework.com/runbooks/api-down"

      - alert: HighErrorRate
        expr: |
          sum(rate(http_server_request_duration_count{http_status_code=~"5.."}[5m])) 
          / 
          sum(rate(http_server_request_duration_count[5m])) > 0.05
        for: 5m
        labels:
          severity: critical
          priority: P0
        annotations:
          summary: "High error rate detected (>5%)"
          description: "Error rate is {{ $value | humanizePercentage }}"
          runbook_url: "https://wiki.xframework.com/runbooks/high-error-rate"

      - alert: DatabaseConnectionPoolExhausted
        expr: db_connection_pool_active / db_connection_pool_max > 0.95
        for: 2m
        labels:
          severity: critical
          priority: P0
        annotations:
          summary: "Database connection pool near exhaustion"
          description: "Connection pool at {{ $value | humanizePercentage }} capacity"
          runbook_url: "https://wiki.xframework.com/runbooks/db-connections"

      - alert: MemoryCritical
        expr: process_working_set_bytes / 1024 / 1024 / 1024 > 7
        for: 5m
        labels:
          severity: critical
          priority: P0
        annotations:
          summary: "Memory usage critical (>7GB)"
          description: "Memory usage is {{ $value }}GB"
          runbook_url: "https://wiki.xframework.com/runbooks/high-memory"

  - name: xframework_high
    interval: 1m
    rules:
      # P1 Alerts
      - alert: HighLatency
        expr: |
          histogram_quantile(0.95, 
            rate(http_server_request_duration_bucket[5m])
          ) > 3
        for: 10m
        labels:
          severity: high
          priority: P1
        annotations:
          summary: "High latency detected (P95 >3s)"
          description: "P95 latency is {{ $value }}s"
          runbook_url: "https://wiki.xframework.com/runbooks/high-latency"

      - alert: CacheDown
        expr: redis_up == 0
        for: 5m
        labels:
          severity: high
          priority: P1
        annotations:
          summary: "Redis cache is down"
          description: "Redis has been down for 5 minutes"
          runbook_url: "https://wiki.xframework.com/runbooks/cache-down"

      - alert: LowCacheHitRatio
        expr: |
          sum(rate(redis_keyspace_hits_total[10m])) 
          / 
          (sum(rate(redis_keyspace_hits_total[10m])) + sum(rate(redis_keyspace_misses_total[10m]))) 
          < 0.5
        for: 15m
        labels:
          severity: high
          priority: P1
        annotations:
          summary: "Cache hit ratio below 50%"
          description: "Hit ratio is {{ $value | humanizePercentage }}"
          runbook_url: "https://wiki.xframework.com/runbooks/low-cache-hit"

      - alert: DatabaseSlowQueries
        expr: |
          histogram_quantile(0.95, 
            rate(db_query_duration_bucket[5m])
          ) > 1
        for: 10m
        labels:
          severity: high
          priority: P1
        annotations:
          summary: "Database queries are slow (P95 >1s)"
          description: "P95 query time is {{ $value }}s"
          runbook_url: "https://wiki.xframework.com/runbooks/slow-queries"

  - name: xframework_medium
    interval: 5m
    rules:
      # P2 Alerts
      - alert: ElevatedErrorRate
        expr: |
          sum(rate(http_server_request_duration_count{http_status_code=~"5.."}[10m])) 
          / 
          sum(rate(http_server_request_duration_count[10m])) > 0.01
        for: 15m
        labels:
          severity: medium
          priority: P2
        annotations:
          summary: "Elevated error rate (>1%)"
          description: "Error rate is {{ $value | humanizePercentage }}"

      - alert: HighMemoryUsage
        expr: process_working_set_bytes / 1024 / 1024 / 1024 > 5
        for: 15m
        labels:
          severity: medium
          priority: P2
        annotations:
          summary: "High memory usage (>5GB)"
          description: "Memory usage is {{ $value }}GB"

      - alert: DiskSpaceLow
        expr: |
          (node_filesystem_avail_bytes{mountpoint="/"} 
          / 
          node_filesystem_size_bytes{mountpoint="/"}) * 100 < 15
        for: 10m
        labels:
          severity: medium
          priority: P2
        annotations:
          summary: "Disk space low (<15%)"
          description: "{{ $value }}% disk space remaining"

      - alert: HighCPUUsage
        expr: rate(process_cpu_seconds_total[5m]) * 100 > 80
        for: 15m
        labels:
          severity: medium
          priority: P2
        annotations:
          summary: "High CPU usage (>80%)"
          description: "CPU usage is {{ $value }}%"
```

### Alert Routing (Alertmanager)

```yaml
# alertmanager.yml
global:
  resolve_timeout: 5m
  slack_api_url: 'https://hooks.slack.com/services/YOUR/SLACK/WEBHOOK'

route:
  group_by: ['alertname', 'cluster', 'service']
  group_wait: 10s
  group_interval: 10s
  repeat_interval: 12h
  receiver: 'default'
  
  routes:
    # P0 - Critical (immediate page)
    - match:
        priority: P0
      receiver: 'pagerduty-critical'
      group_wait: 0s
      repeat_interval: 5m
    
    # P1 - High (page during business hours)
    - match:
        priority: P1
      receiver: 'pagerduty-high'
      group_wait: 30s
      repeat_interval: 30m
    
    # P2 - Medium (Slack only)
    - match:
        priority: P2
      receiver: 'slack-medium'
      repeat_interval: 4h
    
    # P3 - Low (Email only)
    - match:
        priority: P3
      receiver: 'email-low'
      repeat_interval: 24h

receivers:
  - name: 'default'
    slack_configs:
      - channel: '#alerts'
        title: '{{ .GroupLabels.alertname }}'
        text: '{{ range .Alerts }}{{ .Annotations.description }}{{ end }}'

  - name: 'pagerduty-critical'
    pagerduty_configs:
      - service_key: 'YOUR_PAGERDUTY_SERVICE_KEY'
        description: '{{ .GroupLabels.alertname }}: {{ .CommonAnnotations.summary }}'
    slack_configs:
      - channel: '#incidents'
        title: '🚨 P0 ALERT: {{ .GroupLabels.alertname }}'
        text: '{{ .CommonAnnotations.description }}'
        color: 'danger'

  - name: 'pagerduty-high'
    pagerduty_configs:
      - service_key: 'YOUR_PAGERDUTY_SERVICE_KEY'
        description: '{{ .GroupLabels.alertname }}: {{ .CommonAnnotations.summary }}'
    slack_configs:
      - channel: '#alerts'
        title: '⚠️ P1 ALERT: {{ .GroupLabels.alertname }}'
        text: '{{ .CommonAnnotations.description }}'
        color: 'warning'

  - name: 'slack-medium'
    slack_configs:
      - channel: '#alerts'
        title: 'ℹ️ P2 ALERT: {{ .GroupLabels.alertname }}'
        text: '{{ .CommonAnnotations.description }}'

  - name: 'email-low'
    email_configs:
      - to: 'oncall@xframework.com'
        from: 'alerts@xframework.com'
        smarthost: 'smtp.gmail.com:587'
        auth_username: 'alerts@xframework.com'
        auth_password: 'YOUR_EMAIL_PASSWORD'
```

## Dashboard Recommendations

### Main Application Dashboard

```json
// Grafana Dashboard JSON (excerpt)
{
  "dashboard": {
    "title": "XFramework - Application Overview",
    "panels": [
      {
        "title": "Request Rate",
        "type": "graph",
        "targets": [
          {
            "expr": "sum(rate(http_server_request_duration_count[5m])) by (http_route)",
            "legendFormat": "{{ http_route }}"
          }
        ]
      },
      {
        "title": "Response Time (P50, P95, P99)",
        "type": "graph",
        "targets": [
          {
            "expr": "histogram_quantile(0.50, rate(http_server_request_duration_bucket[5m]))",
            "legendFormat": "P50"
          },
          {
            "expr": "histogram_quantile(0.95, rate(http_server_request_duration_bucket[5m]))",
            "legendFormat": "P95"
          },
          {
            "expr": "histogram_quantile(0.99, rate(http_server_request_duration_bucket[5m]))",
            "legendFormat": "P99"
          }
        ]
      },
      {
        "title": "Error Rate",
        "type": "graph",
        "targets": [
          {
            "expr": "sum(rate(http_server_request_duration_count{http_status_code=~\"4..\"}[5m])) / sum(rate(http_server_request_duration_count[5m]))",
            "legendFormat": "4xx"
          },
          {
            "expr": "sum(rate(http_server_request_duration_count{http_status_code=~\"5..\"}[5m])) / sum(rate(http_server_request_duration_count[5m]))",
            "legendFormat": "5xx"
          }
        ],
        "alert": {
          "conditions": [
            {
              "evaluator": { "params": [0.01], "type": "gt" },
              "query": { "params": ["5xx", "5m", "now"] }
            }
          ]
        }
      },
      {
        "title": "Active Requests",
        "type": "stat",
        "targets": [
          {
            "expr": "http_server_active_requests"
          }
        ]
      }
    ]
  }
}
```

### Database Performance Dashboard

**Panels**:
- Connection pool usage (gauge)
- Query duration P95 (graph)
- Query rate by operation (graph)
- Slow queries (table)
- Deadlocks (counter)
- Transaction rate (graph)

### Cache Performance Dashboard

**Panels**:
- Cache hit ratio (gauge)
- L1 vs L2 hits (pie chart)
- Cache operation latency (graph)
- Memory usage (graph)
- Eviction rate (graph)
- Top cache keys (table)

### Infrastructure Dashboard

**Panels**:
- CPU usage by instance (graph)
- Memory usage by instance (graph)
- Disk I/O (graph)
- Network I/O (graph)
- Instance health status (stat)

### Business Metrics Dashboard

**Panels**:
- Products created (counter)
- Wallet transactions (counter)
- Transaction volume (graph)
- Authentication success rate (gauge)
- Active users (gauge)
- Revenue metrics (graph)

## Log Aggregation

### Structured Logging Setup

```csharp
// Already configured in XFramework via Serilog
// Logs include TraceId/SpanId for correlation

// Example log entry
{
  "@timestamp": "2025-11-20T16:45:32.123Z",
  "level": "Information",
  "message": "Product created successfully",
  "properties": {
    "ProductId": "123e4567-e89b-12d3-a456-426614174000",
    "TenantId": "tenant-001",
    "UserId": "user-456",
    "TraceId": "4bf92f3577b34da6a3ce929d0e0e4736",
    "SpanId": "00f067aa0ba902b7",
    "SourceContext": "XFramework.ProductService"
  }
}
```

### Loki Configuration

```yaml
# loki-config.yml
auth_enabled: false

server:
  http_listen_port: 3100

ingester:
  lifecycler:
    address: 127.0.0.1
    ring:
      kvstore:
        store: inmemory
      replication_factor: 1
  chunk_idle_period: 5m
  chunk_retain_period: 30s

schema_config:
  configs:
    - from: 2024-01-01
      store: boltdb
      object_store: filesystem
      schema: v11
      index:
        prefix: index_
        period: 168h

storage_config:
  boltdb:
    directory: /loki/index
  filesystem:
    directory: /loki/chunks

limits_config:
  enforce_metric_name: false
  reject_old_samples: true
  reject_old_samples_max_age: 168h
  ingestion_rate_mb: 10
  ingestion_burst_size_mb: 20

chunk_store_config:
  max_look_back_period: 0s

table_manager:
  retention_deletes_enabled: true
  retention_period: 720h  # 30 days
```

### Log Queries (LogQL)

```logql
# All errors in last hour
{app="xframework-api"} |= "error" | json

# Errors for specific tenant
{app="xframework-api"} | json | TenantId="tenant-001" | level="Error"

# Slow requests (>1s)
{app="xframework-api"} | json | duration > 1000

# Failed authentication attempts
{app="xframework-api"} | json | SourceContext="AuthService" | level="Warning"

# Trace all requests for specific user
{app="xframework-api"} | json | UserId="user-123"

# Count errors per minute
sum(count_over_time({app="xframework-api"} |= "error" [1m]))

# Top error messages
topk(10, 
  sum by (message) (
    count_over_time({app="xframework-api"} | level="Error" [24h])
  )
)
```

### Log Retention Strategy

| Log Level | Retention Period | Storage Location |
|-----------|-----------------|------------------|
| Error | 90 days | Loki + Archive |
| Warning | 30 days | Loki |
| Information | 7 days | Loki |
| Debug | 1 day | Loki (dev only) |
| Trace | N/A | Disabled in production |

## SLI and SLO Definitions

### Service Level Indicators (SLIs)

#### Availability SLI

```promql
# Percentage of successful requests (non-5xx) in last 30 days
sum(rate(http_server_request_duration_count{http_status_code!~"5.."}[30d])) 
/ 
sum(rate(http_server_request_duration_count[30d])) * 100
```

**Target**: ≥99.9% (allows ~43 minutes downtime/month)

#### Latency SLI

```promql
# Percentage of requests completing within 500ms (P95) in last 7 days
histogram_quantile(0.95, 
  rate(http_server_request_duration_bucket[7d])
) < 0.5
```

**Target**: ≥95% of requests < 500ms

#### Error Rate SLI

```promql
# Error budget: Percentage of failed requests allowed
sum(rate(http_server_request_duration_count{http_status_code=~"5.."}[30d])) 
/ 
sum(rate(http_server_request_duration_count[30d])) * 100
```

**Target**: ≤0.1% error rate

### Service Level Objectives (SLOs)

| Service | Availability SLO | Latency SLO (P95) | Error Budget |
|---------|-----------------|-------------------|--------------|
| **API Gateway** | 99.95% | <200ms | 0.05% |
| **Authentication API** | 99.99% | <150ms | 0.01% |
| **Product API** | 99.9% | <500ms | 0.1% |
| **Wallet API** | 99.99% | <800ms | 0.01% |
| **Search API** | 99.5% | <1000ms | 0.5% |

### Error Budget Calculation

```
Error Budget = (1 - SLO) × Total Requests

Example for 99.9% SLO over 30 days:
- Total requests: 100M
- Allowed failures: 100M × 0.001 = 100,000 requests
- ~3,333 failed requests per day allowed
- ~139 failed requests per hour allowed
```

### SLO Dashboard

```promql
# Current availability (30-day rolling)
100 - (
  sum(rate(http_server_request_duration_count{http_status_code=~"5.."}[30d])) 
  / 
  sum(rate(http_server_request_duration_count[30d])) * 100
)

# Error budget remaining (%)
(
  (1 - 0.999) - 
  (sum(rate(http_server_request_duration_count{http_status_code=~"5.."}[30d])) 
   / 
   sum(rate(http_server_request_duration_count[30d])))
) 
/ 
(1 - 0.999) * 100

# Days until error budget exhausted (at current burn rate)
30 * (error_budget_remaining / error_budget_total)
```

## Troubleshooting

### Common Monitoring Issues

#### Issue: No Metrics Appearing in Prometheus

**Diagnosis**:
```bash
# Check if app is exposing metrics
curl http://localhost:5000/metrics

# Check Prometheus targets
curl http://localhost:9090/api/v1/targets

# Check OTel Collector
docker logs otel-collector
```

**Solutions**:
- Verify OpenTelemetry is configured in app
- Check Prometheus scrape configuration
- Ensure network connectivity
- Verify metric endpoint is accessible

#### Issue: Traces Not Showing in Jaeger

**Diagnosis**:
```bash
# Check OTel Collector logs
docker logs otel-collector | grep -i jaeger

# Check Jaeger
curl http://localhost:16686/api/services
```

**Solutions**:
- Verify sampling rate (might be too low)
- Check OTel Collector → Jaeger connectivity
- Ensure trace context propagation is working
- Review firewall rules

#### Issue: Alerts Not Firing

**Diagnosis**:
```bash
# Check alert rules in Prometheus
curl http://localhost:9090/api/v1/rules

# Check Alertmanager
curl http://localhost:9093/api/v1/alerts
```

**Solutions**:
- Verify alert rule syntax
- Check alert evaluation interval
- Confirm Prometheus → Alertmanager connectivity
- Review Alertmanager routing configuration

#### Issue: High Cardinality Metrics

**Symptoms**: Prometheus using excessive memory/disk

**Solutions**:
```yaml
# Reduce label cardinality
# Bad: User-specific labels
user_requests_total{user_id="123"}  # ❌ Too many unique values

# Good: Aggregate by user type
user_requests_total{user_type="premium"}  # ✅ Limited values

# Use recording rules for expensive queries
groups:
  - name: precomputed
    interval: 1m
    rules:
      - record: api:request_rate:5m
        expr: rate(http_server_request_duration_count[5m])
```

### Performance Tuning

#### Prometheus Optimization

```yaml
# prometheus.yml
global:
  scrape_interval: 15s  # Default 1m might be too sparse
  scrape_timeout: 10s
  evaluation_interval: 15s

# Increase retention for production
storage:
  tsdb:
    retention_time: 30d  # Increase if needed
    retention_size: 50GB
```

#### OTel Collector Tuning

```yaml
processors:
  batch:
    timeout: 10s
    send_batch_size: 10000  # Increase for high volume
    send_batch_max_size: 11000
  
  memory_limiter:
    check_interval: 1s
    limit_mib: 2048  # Increase for high volume
    spike_limit_mib: 512
```

## Related Documentation

- [OpenTelemetry Guide](../../observability/opentelemetry-guide.md)
- [Performance Testing Guide](./performance-testing-guide.md)
- [Incident Response Plan](./incident-response-plan.md)
- [Deployment Runbook](./deployment-runbook.md)
- [Security Audit Checklist](./security-audit-checklist.md)