# Deployment Runbook - XFramework VSA Migration

## Overview

This runbook provides step-by-step procedures for deploying XFramework's Vertical Slice Architecture (VSA) migration to production. It includes pre-deployment checklists, deployment steps, verification procedures, and rollback instructions.

## Table of Contents

- [Deployment Overview](#deployment-overview)
- [Pre-Deployment Checklist](#pre-deployment-checklist)
- [Environment Preparation](#environment-preparation)
- [Database Migration](#database-migration)
- [Application Deployment](#application-deployment)
- [Configuration Verification](#configuration-verification)
- [Smoke Tests](#smoke-tests)
- [Post-Deployment Monitoring](#post-deployment-monitoring)
- [Rollback Procedures](#rollback-procedures)
- [Troubleshooting](#troubleshooting)

## Deployment Overview

### Deployment Architecture

```
┌─────────────────┐
│  Load Balancer  │
│   (nginx/ALB)   │
└────────┬────────┘
         │
    ┌────┴────┐
    │         │
┌───▼───┐ ┌──▼────┐
│ API 1 │ │ API 2 │  (N instances)
└───┬───┘ └──┬────┘
    │        │
    └────┬───┘
         │
    ┌────▼──────┐
    │  Database │
    │ SQL Server│
    └───────────┘
    ┌───────────┐
    │   Redis   │
    │  (Cache)  │
    └───────────┘
```

### Deployment Strategy

- **Blue-Green Deployment**: Recommended for zero-downtime
- **Rolling Deployment**: Update instances one at a time
- **Canary Deployment**: Gradual rollout to subset of users

### Deployment Windows

| Environment | Window | Approval Required |
|------------|--------|-------------------|
| Development | Anytime | No |
| Staging | Business hours | Lead approval |
| Production | Maintenance window | Change board approval |

**Production Maintenance Windows**:
- Primary: Saturday 02:00 - 06:00 SGT (low traffic)
- Emergency: Any time with approval

## Pre-Deployment Checklist

### 1. Code Readiness

- [ ] **All code merged to main branch**
  ```bash
  git checkout main
  git pull origin main
  git log --oneline -10  # Verify latest commits
  ```

- [ ] **All tests passing**
  ```bash
  dotnet test --configuration Release --no-build
  # Expected: Test Run Successful, 0 Failed
  ```

- [ ] **Code review completed**
  - [ ] PR approved by at least 2 reviewers
  - [ ] No unresolved comments
  - [ ] Security review completed (for sensitive changes)

- [ ] **Version number updated**
  ```xml
  <!-- Check Directory.Build.props or .csproj -->
  <Version>1.2.0</Version>
  ```

### 2. Documentation

- [ ] **Release notes prepared**
  - [ ] Features added
  - [ ] Bugs fixed
  - [ ] Breaking changes (if any)
  - [ ] Known issues

- [ ] **Deployment documentation updated**
  - [ ] This runbook is current
  - [ ] Configuration changes documented
  - [ ] Migration scripts reviewed

- [ ] **API documentation updated**
  - [ ] Swagger/OpenAPI spec updated
  - [ ] Postman collections updated

### 3. Database Changes

- [ ] **Migration scripts tested in staging**
  ```bash
  # Verify migration count
  dotnet ef migrations list --project src/Kernel/XFramework.Domain
  ```

- [ ] **Rollback scripts prepared**
  - [ ] For each forward migration
  - [ ] Tested in staging environment

- [ ] **Database backup scheduled**
  - [ ] Full backup before migration
  - [ ] Backup retention verified

- [ ] **Migration execution time estimated**
  - [ ] Large migrations (>5 min) scheduled separately
  - [ ] Downtime window communicated

### 4. Infrastructure

- [ ] **Server capacity verified**
  - [ ] CPU: <60% utilization
  - [ ] Memory: <70% utilization
  - [ ] Disk: >30% free space

- [ ] **Dependencies available**
  - [ ] SQL Server accessible
  - [ ] Redis accessible
  - [ ] External APIs reachable

- [ ] **SSL certificates valid**
  ```bash
  # Check certificate expiration
  echo | openssl s_client -connect api.example.com:443 2>/dev/null | \
    openssl x509 -noout -dates
  ```

- [ ] **Load balancer configured**
  - [ ] Health check endpoints verified
  - [ ] SSL termination configured
  - [ ] Routing rules updated

### 5. Monitoring & Alerts

- [ ] **Monitoring systems operational**
  - [ ] Prometheus scraping endpoints
  - [ ] Grafana dashboards accessible
  - [ ] OpenTelemetry collector running

- [ ] **Alerts configured**
  - [ ] High error rate alert
  - [ ] High latency alert
  - [ ] Service down alert
  - [ ] Database connection alert

- [ ] **On-call team notified**
  - [ ] Primary contact available
  - [ ] Backup contact available
  - [ ] Escalation path confirmed

### 6. Communication

- [ ] **Stakeholders notified**
  - [ ] Product team
  - [ ] Customer support
  - [ ] Operations team

- [ ] **Status page updated** (if applicable)
  - [ ] Scheduled maintenance notice posted
  - [ ] 24-hour advance notice given

- [ ] **Deployment team assembled**
  - [ ] Lead developer
  - [ ] DevOps engineer
  - [ ] DBA (if database changes)

## Environment Preparation

### 1. Backup Current State

```bash
# Backup database
sqlcmd -S your-server -U your-user -P your-password \
  -Q "BACKUP DATABASE XFramework TO DISK='C:\Backups\XFramework_$(date +%Y%m%d_%H%M%S).bak' WITH COMPRESSION"

# Backup configuration files
mkdir -p backups/$(date +%Y%m%d)
cp /etc/xframework/appsettings.json backups/$(date +%Y%m%d)/
cp /etc/xframework/appsettings.Production.json backups/$(date +%Y%m%d)/

# Tag current version in git
git tag -a v1.1.0-pre-deployment -m "Pre-deployment snapshot"
git push origin v1.1.0-pre-deployment
```

### 2. Prepare Deployment Package

```bash
# Build release package
dotnet publish src/Presentation/YourApi/YourApi.csproj \
  -c Release \
  -o ./publish \
  --self-contained false \
  --runtime linux-x64

# Verify build output
ls -lah ./publish
# Should contain: YourApi.dll, appsettings.json, wwwroot/, etc.

# Create deployment archive
cd publish
tar -czf ../xframework-api-v1.2.0.tar.gz .
cd ..

# Calculate checksum
sha256sum xframework-api-v1.2.0.tar.gz > xframework-api-v1.2.0.tar.gz.sha256

# Upload to artifact repository
# Example: Azure Blob Storage, S3, Nexus, etc.
az storage blob upload \
  --account-name yourstorageaccount \
  --container-name deployments \
  --name xframework-api-v1.2.0.tar.gz \
  --file xframework-api-v1.2.0.tar.gz
```

### 3. Prepare Configuration

```bash
# Create environment-specific configuration
# appsettings.Production.json (sanitized, no secrets)
cat > appsettings.Production.json <<EOF
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Server=prod-sql-server;Database=XFramework;",
    "Redis": "prod-redis-server:6379"
  },
  "OpenTelemetry": {
    "Sampling": {
      "Probability": 0.1
    },
    "Exporters": {
      "OTLP": {
        "Enabled": true,
        "Endpoint": "http://otel-collector:4317"
      }
    }
  },
  "CacheOptions": {
    "Enabled": true,
    "EnableL1Cache": true,
    "EnableL2Cache": true,
    "DefaultAbsoluteExpirationSeconds": 600
  }
}
EOF

# Secrets go in Azure Key Vault or environment variables
# NOT in configuration files
```

## Database Migration

### Pre-Migration Checklist

- [ ] **Database backup completed**
- [ ] **Migration scripts reviewed**
- [ ] **Estimated execution time known**
- [ ] **Rollback plan ready**
- [ ] **Database maintenance mode enabled** (if required)

### Migration Execution

#### Option 1: Using EF Core Migrations (Recommended)

```bash
# 1. Set connection string (use environment variable for security)
export ConnectionStrings__DefaultConnection="Server=prod-server;Database=XFramework;User Id=sa;Password=***;TrustServerCertificate=true"

# 2. List pending migrations
dotnet ef database update --list \
  --project src/Kernel/XFramework.Domain \
  --startup-project src/Presentation/YourApi

# Output shows pending migrations:
# 20251120_InitialCreate (Pending)
# 20251120_AddAuditFields (Pending)

# 3. Apply migrations with verbose logging
dotnet ef database update \
  --project src/Kernel/XFramework.Domain \
  --startup-project src/Presentation/YourApi \
  --verbose

# 4. Verify migration success
dotnet ef migrations list \
  --project src/Kernel/XFramework.Domain \
  --startup-project src/Presentation/YourApi

# All migrations should show (Applied)
```

#### Option 2: Using SQL Scripts (For Critical Migrations)

```bash
# 1. Generate SQL script from EF migrations
dotnet ef migrations script \
  --project src/Kernel/XFramework.Domain \
  --startup-project src/Presentation/YourApi \
  --output migration.sql

# 2. Review the SQL script
less migration.sql

# 3. Execute using sqlcmd (with transaction)
sqlcmd -S prod-server -U sa -P *** -i migration.sql -o migration.log

# 4. Check for errors
cat migration.log | grep -i error

# 5. Verify migration
sqlcmd -S prod-server -U sa -P *** \
  -Q "SELECT TOP 1 * FROM __EFMigrationsHistory ORDER BY MigrationId DESC"
```

### Post-Migration Verification

```sql
-- 1. Verify table structure
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('Products', 'Users', 'Wallets')  -- Key tables
ORDER BY TABLE_NAME, ORDINAL_POSITION;

-- 2. Verify data integrity
SELECT COUNT(*) FROM Products;
SELECT COUNT(*) FROM Users;
SELECT COUNT(*) FROM Wallets;

-- Compare with pre-migration counts (should match or be close)

-- 3. Verify indexes
SELECT 
    t.name AS TableName,
    i.name AS IndexName,
    i.type_desc AS IndexType
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
WHERE t.name IN ('Products', 'Users', 'Wallets')
ORDER BY t.name, i.name;

-- 4. Check for any orphaned records (if foreign keys added)
-- Example:
SELECT p.* FROM Products p
LEFT JOIN Categories c ON p.CategoryId = c.Id
WHERE c.Id IS NULL;
-- Should return 0 rows
```

## Application Deployment

### Deployment Strategy Selection

#### Blue-Green Deployment (Zero Downtime)

```bash
# Current production: Green environment
# Deploy to: Blue environment

# Step 1: Deploy to Blue environment
scp xframework-api-v1.2.0.tar.gz user@blue-server:/opt/xframework/

# Step 2: Extract and configure
ssh user@blue-server
cd /opt/xframework
tar -xzf xframework-api-v1.2.0.tar.gz -C blue/
cp production-config/appsettings.Production.json blue/
# Copy secrets from Key Vault
az keyvault secret show --vault-name prod-vault --name db-password --query value -o tsv > /tmp/dbpass

# Step 3: Start Blue instance
sudo systemctl start xframework-blue

# Step 4: Verify Blue is healthy
curl http://localhost:5001/health
# Expected: {"status":"Healthy"}

# Step 5: Run smoke tests on Blue
./scripts/smoke-test.sh http://localhost:5001

# Step 6: Switch load balancer to Blue
# Update nginx/ALB to route traffic to Blue
sudo nano /etc/nginx/sites-available/xframework
# Change upstream to point to blue instance
sudo systemctl reload nginx

# Step 7: Monitor for 15 minutes
# Watch error rates, latency, logs

# Step 8: If successful, stop Green instance
sudo systemctl stop xframework-green

# Step 9: Green becomes the new standby (for next deployment or rollback)
```

#### Rolling Deployment (Kubernetes)

```yaml
# deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: xframework-api
spec:
  replicas: 3
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 1
      maxUnavailable: 0
  selector:
    matchLabels:
      app: xframework-api
  template:
    metadata:
      labels:
        app: xframework-api
        version: v1.2.0
    spec:
      containers:
      - name: api
        image: your-registry/xframework-api:v1.2.0
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: db-secret
              key: connection-string
        livenessProbe:
          httpGet:
            path: /health/live
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 5
```

```bash
# Deploy using kubectl
kubectl apply -f deployment.yaml

# Watch rollout status
kubectl rollout status deployment/xframework-api

# Monitor pods
kubectl get pods -l app=xframework-api -w

# Check logs of new pods
kubectl logs -l app=xframework-api,version=v1.2.0 -f
```

#### Traditional Server Deployment

```bash
# Step 1: Stop current application
sudo systemctl stop xframework-api

# Step 2: Backup current version
sudo cp -r /opt/xframework/current /opt/xframework/backup-$(date +%Y%m%d-%H%M%S)

# Step 3: Deploy new version
sudo rm -rf /opt/xframework/current/*
sudo tar -xzf xframework-api-v1.2.0.tar.gz -C /opt/xframework/current/

# Step 4: Set permissions
sudo chown -R xframework:xframework /opt/xframework/current
sudo chmod +x /opt/xframework/current/YourApi

# Step 5: Update configuration
sudo cp /opt/xframework/config/appsettings.Production.json /opt/xframework/current/

# Step 6: Start application
sudo systemctl start xframework-api

# Step 7: Verify startup
sudo systemctl status xframework-api
sudo journalctl -u xframework-api -f --since "2 minutes ago"
```

### Systemd Service File (Linux)

```ini
# /etc/systemd/system/xframework-api.service
[Unit]
Description=XFramework API Service
After=network.target

[Service]
Type=notify
User=xframework
Group=xframework
WorkingDirectory=/opt/xframework/current
ExecStart=/opt/xframework/current/YourApi
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=xframework-api
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

# Health check
SuccessExitStatus=143
TimeoutStopSec=30

[Install]
WantedBy=multi-user.target
```

```bash
# Enable and start service
sudo systemctl daemon-reload
sudo systemctl enable xframework-api
sudo systemctl start xframework-api
```

## Configuration Verification

### 1. Application Configuration

```bash
# Verify appsettings.json is correct
cat /opt/xframework/current/appsettings.json | jq '.'

# Check environment variables
env | grep ASPNETCORE
env | grep ConnectionStrings

# Verify secrets are NOT in config files
grep -r "password\|secret\|key" /opt/xframework/current/appsettings*.json
# Should only show placeholder values
```

### 2. Database Connectivity

```bash
# Test database connection
curl -s http://localhost:5000/health | jq '.entries.database'
# Expected: {"status":"Healthy"}

# Or manually test
sqlcmd -S your-server -U your-user -P your-password \
  -Q "SELECT @@VERSION"
```

### 3. Redis Connectivity

```bash
# Test Redis connection
curl -s http://localhost:5000/health | jq '.entries.redis'
# Expected: {"status":"Healthy"}

# Or manually test
redis-cli -h redis-server ping
# Expected: PONG
```

### 4. OpenTelemetry Configuration

```bash
# Check if traces are being exported
curl -s http://localhost:5000/health | jq '.entries'

# Verify Jaeger UI shows traces
curl http://jaeger-ui:16686/api/traces?service=xframework-api
```

### 5. Security Configuration

```bash
# Verify HTTPS is enforced
curl -I http://localhost:5000/api/products
# Expected: 301 or 307 redirect to https://

# Verify security headers
curl -I https://api.example.com/api/products
# Check for:
# - Strict-Transport-Security
# - X-Content-Type-Options
# - X-Frame-Options
```

## Smoke Tests

### Automated Smoke Test Script

```bash
#!/bin/bash
# smoke-test.sh

API_URL="${1:-http://localhost:5000}"
ADMIN_TOKEN="<your-admin-token>"

echo "=== XFramework API Smoke Tests ==="
echo "Testing: $API_URL"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
NC='\033[0m' # No Color

# Function to test endpoint
test_endpoint() {
    local name=$1
    local method=$2
    local endpoint=$3
    local expected_status=$4
    local headers=$5
    
    echo -n "Testing $name... "
    
    if [ -z "$headers" ]; then
        response=$(curl -s -o /dev/null -w "%{http_code}" -X $method "$API_URL$endpoint")
    else
        response=$(curl -s -o /dev/null -w "%{http_code}" -X $method "$API_URL$endpoint" -H "$headers")
    fi
    
    if [ "$response" == "$expected_status" ]; then
        echo -e "${GREEN}PASS${NC} ($response)"
        return 0
    else
        echo -e "${RED}FAIL${NC} (Expected: $expected_status, Got: $response)"
        return 1
    fi
}

# Track results
PASSED=0
FAILED=0

# Test 1: Health Check (Liveness)
if test_endpoint "Health Check (Live)" "GET" "/health/live" "200"; then
    ((PASSED++))
else
    ((FAILED++))
fi

# Test 2: Health Check (Readiness)
if test_endpoint "Health Check (Ready)" "GET" "/health/ready" "200"; then
    ((PASSED++))
else
    ((FAILED++))
fi

# Test 3: Health Check (Full)
if test_endpoint "Health Check (Full)" "GET" "/health" "200"; then
    ((PASSED++))
else
    ((FAILED++))
fi

# Test 4: API Documentation
if test_endpoint "Swagger UI" "GET" "/swagger/index.html" "200"; then
    ((PASSED++))
else
    ((FAILED++))
fi

# Test 5: Unauthenticated endpoint should fail
if test_endpoint "Protected Endpoint (No Auth)" "GET" "/api/products" "401"; then
    ((PASSED++))
else
    ((FAILED++))
fi

# Test 6: With valid token
if test_endpoint "Products List (Authenticated)" "GET" "/api/products?page=1&pageSize=10" "200" "Authorization: Bearer $ADMIN_TOKEN"; then
    ((PASSED++))
else
    ((FAILED++))
fi

# Test 7: Create operation (if safe to test)
# Uncomment if you have a test endpoint
# if test_endpoint "Create Test Item" "POST" "/api/test" "201" "Authorization: Bearer $ADMIN_TOKEN"; then
#     ((PASSED++))
# else
#     ((FAILED++))
# fi

echo ""
echo "=== Results ==="
echo "Passed: $PASSED"
echo "Failed: $FAILED"

if [ $FAILED -eq 0 ]; then
    echo -e "${GREEN}All tests passed!${NC}"
    exit 0
else
    echo -e "${RED}Some tests failed!${NC}"
    exit 1
fi
```

```bash
# Run smoke tests
chmod +x smoke-test.sh
./smoke-test.sh https://api.example.com
```

### Manual Smoke Tests

```bash
# 1. Test basic authentication
curl -X POST https://api.example.com/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "testuser@example.com",
    "password": "TestPassword123!"
  }'
# Expected: 200 OK with token

# 2. Test product listing
curl -H "Authorization: Bearer <token>" \
  https://api.example.com/api/products?page=1&pageSize=5
# Expected: 200 OK with product list

# 3. Test product detail
curl -H "Authorization: Bearer <token>" \
  https://api.example.com/api/products/1
# Expected: 200 OK with product details

# 4. Test tenant isolation
curl -H "Authorization: Bearer <tenant-a-token>" \
     -H "X-Tenant-ID: tenant-a" \
  https://api.example.com/api/products
# Expected: Only Tenant A's products

# 5. Test caching (should be faster on second request)
time curl -H "Authorization: Bearer <token>" \
  https://api.example.com/api/products?page=1
# Note first request time

time curl -H "Authorization: Bearer <token>" \
  https://api.example.com/api/products?page=1
# Second request should be faster (cache hit)

# 6. Test error handling
curl https://api.example.com/api/products/99999
# Expected: 404 Not Found with proper error message

# 7. Test rate limiting (if enabled)
for i in {1..20}; do
  curl -s -o /dev/null -w "%{http_code}\n" \
    -X POST https://api.example.com/api/auth/login \
    -H "Content-Type: application/json" \
    -d '{"email":"test@test.com","password":"wrong"}'
done
# Expected: After threshold, 429 Too Many Requests
```

## Post-Deployment Monitoring

### First 15 Minutes (Critical)

```bash
# 1. Watch real-time logs
tail -f /var/log/xframework/app.log
# Or with Docker/K8s:
docker logs -f xframework-api
kubectl logs -f deployment/xframework-api

# 2. Monitor error rate in Grafana
# Open: https://grafana.example.com/d/xframework-errors
# Check: Error rate should be < 0.1%

# 3. Monitor response times
# Check: P95 latency should be < 500ms

# 4. Monitor health endpoint
watch -n 5 'curl -s http://localhost:5000/health | jq .'

# 5. Check database connections
# Query: Active connections should be < 80% of pool size
```

### First Hour (Important)

```bash
# 1. Review error logs
grep -i error /var/log/xframework/app.log | tail -50

# 2. Check memory usage
free -h
# Or with container:
docker stats xframework-api

# 3. Check CPU usage
top -b -n 1 | grep xframework

# 4. Verify cache hit ratio
curl -s http://localhost:5000/metrics | grep cache_hit_ratio
# Expected: > 0.8 (80%)

# 5. Check database query performance
# Run slow query log analysis
```

### First 24 Hours (Monitoring)

- [ ] **Check error rates hourly**
- [ ] **Monitor performance metrics**
- [ ] **Review user feedback/support tickets**
- [ ] **Verify batch jobs/scheduled tasks run**
- [ ] **Check disk space usage**
- [ ] **Verify backups completed**

### Metrics to Monitor

| Metric | Normal Range | Alert Threshold | Action |
|--------|-------------|-----------------|--------|
| Error Rate | <0.1% | >1% | Investigate logs |
| P95 Latency | <500ms | >1000ms | Check slow queries |
| CPU Usage | <40% | >80% | Scale up/optimize |
| Memory Usage | <60% | >85% | Investigate memory leaks |
| Database Connections | <50 | >80 | Check connection leaks |
| Cache Hit Ratio | >80% | <50% | Review caching strategy |
| Disk Space | >30% free | <10% free | Clean logs/temp files |

## Rollback Procedures

### When to Rollback

Rollback immediately if:
- [ ] Critical functionality broken
- [ ] Data corruption detected
- [ ] Error rate >5%
- [ ] P95 latency >3000ms sustained
- [ ] Security vulnerability introduced
- [ ] Cannot resolve issue within 30 minutes

### Rollback Checklist

- [ ] **Decision to rollback approved** (by deployment lead)
- [ ] **Stakeholders notified**
- [ ] **Rollback reason documented**
- [ ] **Database state assessed**

### Application Rollback

#### Blue-Green Rollback (Fastest)

```bash
# Simply switch load balancer back to Green environment
sudo nano /etc/nginx/sites-available/xframework
# Change upstream back to green instance
sudo systemctl reload nginx

# Verify Green is serving traffic
curl https://api.example.com/health
```

#### Kubernetes Rollback

```bash
# Rollback to previous revision
kubectl rollout undo deployment/xframework-api

# Or rollback to specific revision
kubectl rollout history deployment/xframework-api
kubectl rollout undo deployment/xframework-api --to-revision=3

# Monitor rollback
kubectl rollout status deployment/xframework-api
```

#### Traditional Server Rollback

```bash
# Stop current version
sudo systemctl stop xframework-api

# Restore previous version
sudo rm -rf /opt/xframework/current/*
sudo cp -r /opt/xframework/backup-20251120-020000/* /opt/xframework/current/

# Restore configuration
sudo cp /opt/xframework/config-backup/appsettings.Production.json /opt/xframework/current/

# Start previous version
sudo systemctl start xframework-api

# Verify
sudo systemctl status xframework-api
```

### Database Rollback

⚠️ **WARNING**: Database rollback is risky and may cause data loss.

#### If No Data Changes (Safe)

```bash
# Simply rollback EF Core migrations
dotnet ef database update PreviousMigrationName \
  --project src/Kernel/XFramework.Domain \
  --startup-project src/Presentation/YourApi
```

#### If Data Changed (Complex)

```sql
-- Option 1: Restore from backup (causes downtime and data loss)
USE master;
ALTER DATABASE XFramework SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE XFramework FROM DISK = 'C:\Backups\XFramework_20251120_020000.bak' WITH REPLACE;
ALTER DATABASE XFramework SET MULTI_USER;

-- Option 2: Apply reverse migration script (if prepared)
-- Execute the rollback SQL script you prepared earlier
```

#### Data Reconciliation

If rollback causes data inconsistency:

```sql
-- 1. Identify affected records
SELECT * FROM Products WHERE UpdatedDate > '2025-11-20 02:00:00';

-- 2. Manually reconcile or delete
-- Based on business rules

-- 3. Run data integrity checks
DBCC CHECKDB (XFramework);
```

### Post-Rollback Steps

```bash
# 1. Verify application is functioning
./smoke-test.sh https://api.example.com

# 2. Notify stakeholders
# Send notification that rollback completed

# 3. Update status page
# Mark incident as resolved (with rollback note)

# 4. Schedule post-mortem
# Document what went wrong and how to prevent

# 5. Create fix plan
# Plan for addressing the issue that caused rollback
```

## Troubleshooting

### Common Issues

#### Issue: Application Won't Start

**Symptoms**:
```bash
sudo systemctl status xframework-api
● xframework-api.service - XFramework API Service
   Loaded: loaded
   Active: failed
```

**Diagnosis**:
```bash
# Check logs
sudo journalctl -u xframework-api -n 50 --no-pager

# Common causes:
# 1. Configuration error
# 2. Database connection failure
# 3. Port already in use
# 4. Missing dependencies
```

**Solutions**:
```bash
# 1. Verify configuration
cat /opt/xframework/current/appsettings.Production.json | jq '.'

# 2. Test database connection
sqlcmd -S server -U user -P password -Q "SELECT 1"

# 3. Check port availability
sudo lsof -i :5000
# Kill process if needed:
sudo kill -9 <PID>

# 4. Check file permissions
ls -lah /opt/xframework/current/YourApi
sudo chown xframework:xframework /opt/xframework/current/YourApi
sudo chmod +x /opt/xframework/current/YourApi
```

#### Issue: High Error Rate

**Symptoms**:
- Error rate >1%
- Multiple 500 responses in logs

**Diagnosis**:
```bash
# Check application logs
tail -100 /var/log/xframework/app.log | grep -i error

# Check specific error patterns
grep "Exception" /var/log/xframework/app.log | tail -20

# Review OpenTelemetry traces
# Look for failed spans in Jaeger
```

**Solutions**:
```bash
# 1. If database-related:
# - Check connection pool exhaustion
# - Verify query performance
# - Check for deadlocks

# 2. If external API-related:
# - Check API availability
# - Verify timeouts configured
# - Check circuit breaker status

# 3. If memory-related:
# - Check for memory leaks
# - Restart application
# - Scale up resources
```

#### Issue: Slow Performance

**Symptoms**:
- P95 latency >1000ms
- Requests timing out

**Diagnosis**:
```bash
# 1. Check database performance
# Run this query on SQL Server:
SELECT TOP 20
    total_elapsed_time / execution_count AS avg_time,
    execution_count,
    SUBSTRING(st.text, (qs.statement_start_offset/2)+1,
        ((CASE qs.statement_end_offset
            WHEN -1 THEN DATALENGTH(st.text)
            ELSE qs.statement_end_offset
        END - qs.statement_start_offset)/2) + 1) AS query_text
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
ORDER BY avg_time DESC;

# 2. Check cache hit ratio
curl http://localhost:5000/metrics | grep cache

# 3. Check CPU/Memory
top -b -n 1
```

**Solutions**:
```bash
# 1. Add missing indexes
# Based on slow query analysis

# 2. Optimize cache settings
# Increase TTL for frequently accessed data

# 3. Scale horizontally
# Add more application instances

# 4. Enable query result caching
# Use AsNoTracking() for read-only queries
```

#### Issue: Database Connection Failures

**Symptoms**:
```
SqlException: A network-related or instance-specific error occurred
```

**Diagnosis**:
```bash
# Test connection from application server
sqlcmd -S db-server -U user -P password -Q "SELECT @@VERSION"

# Check firewall
telnet db-server 1433

# Check connection pool
# Look in application logs for pool exhaustion warnings
```

**Solutions**:
```bash
# 1. Verify connection string
# Check appsettings.json

# 2. Increase connection pool size
# Update connection string: Max Pool Size=200

# 3. Fix connection leaks
# Ensure all DbContext instances are disposed

# 4. Restart database connection pool
# Restart application
sudo systemctl restart xframework-api
```

### Emergency Contacts

| Role | Name | Phone | Email |
|------|------|-------|-------|
| Deployment Lead | [Name] | [Phone] | [Email] |
| DevOps Engineer | [Name] | [Phone] | [Email] |
| Database Admin | [Name] | [Phone] | [Email] |
| On-Call Engineer | [Rotation] | [On-Call #] | [Email] |

### Escalation Path

1. **Deployment Team** (0-15 min): Try to resolve
2. **Team Lead** (15-30 min): Decide rollback/continue
3. **Engineering Manager** (30-60 min): Coordinate resources
4. **VP Engineering** (>60 min): Critical incident management

## Post-Deployment Report

### Deployment Report Template

```markdown
# Deployment Report - v1.2.0

**Date**: 2025-11-20
**Deployment Window**: 02:00 - 04:30 SGT
**Deployment Lead**: [Name]
**Status**: ✅ Successful / ⚠️ Partial / ❌ Rolled Back

## Summary

[Brief overview of deployment]

## Timeline

| Time | Event |
|------|-------|
| 02:00 | Database backup started |
| 02:15 | Database migration completed |
| 02:30 | Application deployment started |
| 02:45 | Blue environment verified |
| 03:00 | Traffic switched to Blue |
| 03:15 | Monitoring confirmed stable |
| 04:00 | Green environment decommissioned |

## Changes Deployed

- [Feature 1]
- [Bug fix 2]
- [Database migration for X]

## Issues Encountered

1. **[Issue Description]**
   - Impact: [High/Medium/Low]
   - Resolution: [How it was fixed]
   - Time to resolve: [Duration]

## Performance Comparison

| Metric | Before | After | Change |
|--------|--------|-------|--------|
| P95 Latency | 450ms | 380ms | -15% ✅ |
| Error Rate | 0.05% | 0.03% | -40% ✅ |
| Throughput | 500 RPS | 520 RPS | +4% ✅ |

## Rollback

- [ ] Rollback required
- [ ] Rollback successful
- Reason: N/A

## Lessons Learned

1. [Lesson 1]
2. [Lesson 2]

## Action Items

- [ ] [Action item 1] - Assignee: [Name] - Due: [Date]
- [ ] [Action item 2] - Assignee: [Name] - Due: [Date]

## Sign-off

- Deployment Lead: [Signature/Name] - [Date]
- Team Lead: [Signature/Name] - [Date]
```

## Related Documentation

- [Performance Testing Guide](./performance-testing-guide.md)
- [Security Audit Checklist](./security-audit-checklist.md)
- [Migration Scripts Guide](./migration-scripts-guide.md)
- [Incident Response Plan](./incident-response-plan.md)
- [Monitoring and Alerting Guide](./monitoring-alerting-guide.md)