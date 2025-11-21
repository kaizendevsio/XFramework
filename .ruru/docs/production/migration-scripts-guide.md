# Migration Scripts Guide - XFramework VSA Migration

## Overview

This guide provides comprehensive procedures for managing database migrations, data migrations, and configuration migrations in XFramework's Vertical Slice Architecture (VSA). It includes strategies, best practices, testing procedures, and rollback mechanisms.

## Table of Contents

- [Migration Strategy](#migration-strategy)
- [Database Schema Migrations](#database-schema-migrations)
- [Data Migration Procedures](#data-migration-procedures)
- [Configuration Migration](#configuration-migration)
- [Testing Migration Scripts](#testing-migration-scripts)
- [Rollback Procedures](#rollback-procedures)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)

## Migration Strategy

### Migration Types

| Type | Description | Risk | Downtime | Testing Required |
|------|-------------|------|----------|------------------|
| **Schema Migration** | Add/modify tables, columns, indexes | Medium | Usually no | Extensive |
| **Data Migration** | Move/transform existing data | High | Possible | Critical |
| **Configuration Migration** | Update app settings, feature flags | Low | No | Moderate |
| **Breaking Changes** | Remove columns, change types | Very High | Yes | Extensive |

### Migration Principles

1. **Backward Compatibility**: New code should work with old schema during transition
2. **Forward Compatibility**: Old code should tolerate new schema (where possible)
3. **Incremental Changes**: Break large migrations into small, reversible steps
4. **Zero-Downtime**: Use expand-contract pattern for breaking changes
5. **Idempotency**: Scripts should be safe to run multiple times
6. **Transactional**: Wrap migrations in transactions where possible

### Expand-Contract Pattern

For breaking changes, use a multi-phase approach:

```
Phase 1 (EXPAND): Add new column, keep old column
┌─────────────┬──────────────┬──────────────┐
│ Old Column  │ New Column   │ Application  │
│ (exists)    │ (added)      │ (unchanged)  │
└─────────────┴──────────────┴──────────────┘

Phase 2 (MIGRATE): Dual-write to both columns
┌─────────────┬──────────────┬──────────────┐
│ Old Column  │ New Column   │ Application  │
│ (populated) │ (populated)  │ (updated)    │
└─────────────┴──────────────┴──────────────┘

Phase 3 (CONTRACT): Remove old column
┌─────────────┬──────────────┐
│ New Column  │ Application  │
│ (only)      │ (final)      │
└─────────────┴──────────────┘
```

## Database Schema Migrations

### Using EF Core Migrations

#### Creating a Migration

```bash
# Navigate to domain project
cd src/Kernel/XFramework.Domain

# Create a new migration
dotnet ef migrations add AddProductDescriptionColumn \
  --startup-project ../../Presentation/YourApi \
  --context AppDbContext \
  --output-dir Migrations

# Review generated migration
ls -la Migrations/
```

Generated migration files:
```
Migrations/
├── 20251120160000_AddProductDescriptionColumn.cs
└── 20251120160000_AddProductDescriptionColumn.Designer.cs
```

#### Migration Code Structure

```csharp
// 20251120160000_AddProductDescriptionColumn.cs
using Microsoft.EntityFrameworkCore.Migrations;

public partial class AddProductDescriptionColumn : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Forward migration - add column
        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "Products",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true);
        
        // Create index if needed
        migrationBuilder.CreateIndex(
            name: "IX_Products_Description",
            table: "Products",
            column: "Description");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Rollback migration - remove index and column
        migrationBuilder.DropIndex(
            name: "IX_Products_Description",
            table: "Products");
        
        migrationBuilder.DropColumn(
            name: "Description",
            table: "Products");
    }
}
```

#### Complex Migration Example

```csharp
// Splitting a table or denormalizing data
public partial class SplitAddressFromUser : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Step 1: Create new Addresses table
        migrationBuilder.CreateTable(
            name: "Addresses",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                UserId = table.Column<Guid>(nullable: false),
                Street = table.Column<string>(maxLength: 200, nullable: false),
                City = table.Column<string>(maxLength: 100, nullable: false),
                State = table.Column<string>(maxLength: 50, nullable: false),
                ZipCode = table.Column<string>(maxLength: 10, nullable: false),
                Country = table.Column<string>(maxLength: 50, nullable: false),
                CreatedDate = table.Column<DateTime>(nullable: false),
                TenantId = table.Column<Guid>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Addresses", x => x.Id);
                table.ForeignKey(
                    name: "FK_Addresses_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });
        
        // Step 2: Create index on foreign key
        migrationBuilder.CreateIndex(
            name: "IX_Addresses_UserId",
            table: "Addresses",
            column: "UserId");
        
        // Step 3: Create index for tenant isolation
        migrationBuilder.CreateIndex(
            name: "IX_Addresses_TenantId",
            table: "Addresses",
            column: "TenantId");
        
        // Step 4: Migrate existing data (see Data Migration section)
        // Note: Large data migrations should be done separately
        // migrationBuilder.Sql(@"
        //     INSERT INTO Addresses (Id, UserId, Street, City, State, ZipCode, Country, CreatedDate, TenantId)
        //     SELECT NEWID(), Id, Street, City, State, ZipCode, Country, GETUTCDATE(), TenantId
        //     FROM Users
        //     WHERE Street IS NOT NULL
        // ");
        
        // Step 5: Keep old columns for now (Expand phase)
        // Don't drop User.Street, etc. yet
        // This allows rollback and gradual transition
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Rollback: Drop Addresses table
        migrationBuilder.DropTable(name: "Addresses");
        
        // Old columns in Users table remain intact
    }
}
```

#### Applying Migrations

```bash
# Development/Staging
dotnet ef database update \
  --project src/Kernel/XFramework.Domain \
  --startup-project src/Presentation/YourApi \
  --verbose

# Production (generate SQL script instead)
dotnet ef migrations script \
  --project src/Kernel/XFramework.Domain \
  --startup-project src/Presentation/YourApi \
  --idempotent \
  --output migration-script.sql

# Review the SQL script before applying
less migration-script.sql

# Apply via sqlcmd with transaction
sqlcmd -S prod-server -U sa -P *** -i migration-script.sql
```

#### Idempotent Migration Script

```sql
-- Generated by: dotnet ef migrations script --idempotent

-- Check if migration already applied
IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20251120160000_AddProductDescriptionColumn')
BEGIN
    -- Apply migration
    ALTER TABLE [Products] ADD [Description] nvarchar(1000) NULL;
    
    CREATE INDEX [IX_Products_Description] ON [Products] ([Description]);
    
    -- Record migration
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251120160000_AddProductDescriptionColumn', N'9.0.0');
END
GO
```

### Custom Migration Scripts

For complex scenarios, write custom SQL migrations:

```sql
-- Migration: 20251120_AddProductAuditTrigger.sql
-- Description: Add audit trigger for Product table
-- Author: [Your Name]
-- Date: 2025-11-20

BEGIN TRANSACTION;

-- Check if trigger exists
IF NOT EXISTS (SELECT * FROM sys.triggers WHERE name = 'TR_Products_Audit')
BEGIN
    -- Create audit table if not exists
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProductAuditLog')
    BEGIN
        CREATE TABLE ProductAuditLog (
            AuditId BIGINT IDENTITY(1,1) PRIMARY KEY,
            ProductId UNIQUEIDENTIFIER NOT NULL,
            Action VARCHAR(10) NOT NULL, -- INSERT, UPDATE, DELETE
            ChangedBy UNIQUEIDENTIFIER NULL,
            ChangedDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
            OldValue NVARCHAR(MAX) NULL,
            NewValue NVARCHAR(MAX) NULL,
            TenantId UNIQUEIDENTIFIER NOT NULL
        );
        
        CREATE INDEX IX_ProductAuditLog_ProductId ON ProductAuditLog(ProductId);
        CREATE INDEX IX_ProductAuditLog_TenantId ON ProductAuditLog(TenantId);
        CREATE INDEX IX_ProductAuditLog_ChangedDate ON ProductAuditLog(ChangedDate);
    END;
    
    -- Create trigger
    EXEC('
    CREATE TRIGGER TR_Products_Audit
    ON Products
    AFTER INSERT, UPDATE, DELETE
    AS
    BEGIN
        SET NOCOUNT ON;
        
        -- Handle INSERT
        IF EXISTS (SELECT * FROM inserted) AND NOT EXISTS (SELECT * FROM deleted)
        BEGIN
            INSERT INTO ProductAuditLog (ProductId, Action, ChangedDate, NewValue, TenantId)
            SELECT 
                i.Id,
                ''INSERT'',
                GETUTCDATE(),
                (SELECT i.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
                i.TenantId
            FROM inserted i;
        END
        
        -- Handle UPDATE
        IF EXISTS (SELECT * FROM inserted) AND EXISTS (SELECT * FROM deleted)
        BEGIN
            INSERT INTO ProductAuditLog (ProductId, Action, ChangedDate, OldValue, NewValue, TenantId)
            SELECT 
                i.Id,
                ''UPDATE'',
                GETUTCDATE(),
                (SELECT d.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
                (SELECT i.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
                i.TenantId
            FROM inserted i
            INNER JOIN deleted d ON i.Id = d.Id;
        END
        
        -- Handle DELETE
        IF NOT EXISTS (SELECT * FROM inserted) AND EXISTS (SELECT * FROM deleted)
        BEGIN
            INSERT INTO ProductAuditLog (ProductId, Action, ChangedDate, OldValue, TenantId)
            SELECT 
                d.Id,
                ''DELETE'',
                GETUTCDATE(),
                (SELECT d.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
                d.TenantId
            FROM deleted d;
        END
    END
    ');
    
    PRINT 'Trigger TR_Products_Audit created successfully';
END
ELSE
BEGIN
    PRINT 'Trigger TR_Products_Audit already exists';
END;

COMMIT TRANSACTION;
GO
```

## Data Migration Procedures

### Small Data Migrations (<10K rows)

Can be included in EF Core migration:

```csharp
public partial class MigrateProductCategories : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Simple data migration
        migrationBuilder.Sql(@"
            UPDATE Products 
            SET Category = 'Electronics'
            WHERE Category IS NULL 
              AND Name LIKE '%laptop%' OR Name LIKE '%phone%';
        ");
        
        migrationBuilder.Sql(@"
            UPDATE Products 
            SET Category = 'Books'
            WHERE Category IS NULL 
              AND Name LIKE '%book%' OR Name LIKE '%novel%';
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Rollback: Set back to NULL
        migrationBuilder.Sql(@"
            UPDATE Products 
            SET Category = NULL
            WHERE Category IN ('Electronics', 'Books');
        ");
    }
}
```

### Large Data Migrations (>10K rows)

Use batch processing:

```csharp
// DataMigrationService.cs
public class ProductDataMigrationService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ProductDataMigrationService> _logger;
    
    public async Task MigrateProductDescriptionsAsync(CancellationToken cancellationToken = default)
    {
        const int batchSize = 1000;
        var processed = 0;
        
        _logger.LogInformation("Starting product description migration");
        
        while (true)
        {
            // Fetch batch of products without descriptions
            var products = await _context.Products
                .Where(p => p.Description == null && p.OldDescription != null)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
            
            if (!products.Any())
                break;
            
            // Transform data
            foreach (var product in products)
            {
                product.Description = TransformDescription(product.OldDescription);
            }
            
            // Save batch
            await _context.SaveChangesAsync(cancellationToken);
            
            processed += products.Count;
            _logger.LogInformation("Migrated {Count} products (total: {Total})", 
                products.Count, processed);
            
            // Small delay to avoid overwhelming database
            await Task.Delay(100, cancellationToken);
        }
        
        _logger.LogInformation("Product description migration completed. Total: {Total}", processed);
    }
    
    private string TransformDescription(string oldDescription)
    {
        // Data transformation logic
        return oldDescription?.Trim().ToUpperInvariant() ?? string.Empty;
    }
}
```

Standalone migration script:

```bash
# run-data-migration.sh
#!/bin/bash

API_URL="http://localhost:5000"
ADMIN_TOKEN="<admin-token>"

echo "Starting data migration..."

# Trigger migration endpoint
curl -X POST "$API_URL/api/admin/migrations/product-descriptions" \
     -H "Authorization: Bearer $ADMIN_TOKEN" \
     -H "Content-Type: application/json" \
     -d '{
       "batchSize": 1000,
       "delayMs": 100
     }'

echo ""
echo "Migration triggered. Check logs for progress."
```

### SQL-Based Data Migration

For maximum performance:

```sql
-- Migration: 20251120_MigrateUserAddresses.sql
-- Migrates address data from Users table to Addresses table
-- Estimated rows: ~50,000
-- Estimated duration: 5 minutes

SET NOCOUNT ON;

DECLARE @BatchSize INT = 5000;
DECLARE @TotalMigrated INT = 0;
DECLARE @RowsAffected INT = 1;

PRINT 'Starting address migration...';
PRINT 'Batch size: ' + CAST(@BatchSize AS VARCHAR);

-- Create temp table to track progress
CREATE TABLE #MigrationProgress (
    UserId UNIQUEIDENTIFIER,
    Migrated BIT DEFAULT 0
);

-- Insert users that need migration
INSERT INTO #MigrationProgress (UserId)
SELECT Id
FROM Users
WHERE Street IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM Addresses 
      WHERE Addresses.UserId = Users.Id
  );

PRINT 'Total users to migrate: ' + CAST(@@ROWCOUNT AS VARCHAR);

-- Process in batches
WHILE @RowsAffected > 0
BEGIN
    BEGIN TRANSACTION;
    
    -- Get next batch
    ;WITH UserBatch AS (
        SELECT TOP (@BatchSize) UserId
        FROM #MigrationProgress
        WHERE Migrated = 0
    )
    INSERT INTO Addresses (Id, UserId, Street, City, State, ZipCode, Country, CreatedDate, TenantId)
    SELECT 
        NEWID() AS Id,
        u.Id AS UserId,
        u.Street,
        u.City,
        u.State,
        u.ZipCode,
        ISNULL(u.Country, 'USA') AS Country,
        GETUTCDATE() AS CreatedDate,
        u.TenantId
    FROM Users u
    INNER JOIN UserBatch ub ON u.Id = ub.UserId
    WHERE u.Street IS NOT NULL;
    
    SET @RowsAffected = @@ROWCOUNT;
    
    -- Mark as migrated
    UPDATE #MigrationProgress
    SET Migrated = 1
    WHERE UserId IN (
        SELECT TOP (@BatchSize) UserId
        FROM #MigrationProgress
        WHERE Migrated = 0
    );
    
    SET @TotalMigrated = @TotalMigrated + @RowsAffected;
    
    COMMIT TRANSACTION;
    
    PRINT 'Migrated batch: ' + CAST(@RowsAffected AS VARCHAR) + 
          ' (Total: ' + CAST(@TotalMigrated AS VARCHAR) + ')';
    
    -- Small delay between batches
    WAITFOR DELAY '00:00:00.100'; -- 100ms
END;

-- Cleanup
DROP TABLE #MigrationProgress;

PRINT 'Migration completed successfully!';
PRINT 'Total migrated: ' + CAST(@TotalMigrated AS VARCHAR);

-- Verification
PRINT '';
PRINT 'Verification:';
PRINT 'Users with addresses: ' + CAST((SELECT COUNT(*) FROM Users WHERE Street IS NOT NULL) AS VARCHAR);
PRINT 'Address records created: ' + CAST((SELECT COUNT(*) FROM Addresses) AS VARCHAR);
```

### Data Validation After Migration

```sql
-- Validate data migration
-- Run this after migration completes

PRINT 'Starting validation...';

-- Check 1: All users with addresses have Address records
DECLARE @MissingAddresses INT;
SELECT @MissingAddresses = COUNT(*)
FROM Users u
WHERE u.Street IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM Addresses a WHERE a.UserId = u.Id);

IF @MissingAddresses > 0
    PRINT 'WARNING: ' + CAST(@MissingAddresses AS VARCHAR) + ' users missing address records!';
ELSE
    PRINT 'PASS: All users have corresponding address records';

-- Check 2: No duplicate addresses
DECLARE @Duplicates INT;
SELECT @Duplicates = COUNT(*)
FROM (
    SELECT UserId, COUNT(*) as cnt
    FROM Addresses
    GROUP BY UserId
    HAVING COUNT(*) > 1
) dupes;

IF @Duplicates > 0
    PRINT 'WARNING: ' + CAST(@Duplicates AS VARCHAR) + ' users have duplicate addresses!';
ELSE
    PRINT 'PASS: No duplicate addresses found';

-- Check 3: Tenant isolation maintained
DECLARE @TenantIssues INT;
SELECT @TenantIssues = COUNT(*)
FROM Addresses a
INNER JOIN Users u ON a.UserId = u.Id
WHERE a.TenantId != u.TenantId;

IF @TenantIssues > 0
    PRINT 'ERROR: ' + CAST(@TenantIssues AS VARCHAR) + ' addresses have tenant mismatch!';
ELSE
    PRINT 'PASS: Tenant isolation verified';

-- Check 4: Data integrity
DECLARE @NullStreets INT;
SELECT @NullStreets = COUNT(*)
FROM Addresses
WHERE Street IS NULL OR Street = '';

IF @NullStreets > 0
    PRINT 'WARNING: ' + CAST(@NullStreets AS VARCHAR) + ' addresses have null/empty streets!';
ELSE
    PRINT 'PASS: All addresses have valid street data';

PRINT '';
PRINT 'Validation complete!';
```

## Configuration Migration

### Feature Flags Migration

```csharp
// FeatureFlagMigration.cs
public class FeatureFlagMigrationService
{
    private readonly IConfiguration _configuration;
    private readonly IFeatureFlagService _featureFlagService;
    
    public async Task MigrateToNewFormatAsync()
    {
        // Old format: Simple boolean flags in appsettings.json
        // New format: Percentage rollout with targeting
        
        var oldFlags = _configuration.GetSection("FeatureFlags").Get<Dictionary<string, bool>>();
        
        foreach (var (flagName, isEnabled) in oldFlags)
        {
            await _featureFlagService.CreateOrUpdateFlagAsync(new FeatureFlagConfig
            {
                Name = flagName,
                Enabled = isEnabled,
                RolloutPercentage = isEnabled ? 100 : 0,
                TargetingRules = new List<TargetingRule>(),
                CreatedDate = DateTime.UtcNow
            });
        }
    }
}
```

### Environment Configuration Migration

```bash
# migrate-config.sh
#!/bin/bash

# Migrate from file-based config to Azure Key Vault

SOURCE_FILE="appsettings.Production.json"
VAULT_NAME="xframework-prod-vault"

echo "Migrating secrets from $SOURCE_FILE to $VAULT_NAME..."

# Extract and migrate connection strings
DB_CONNECTION=$(jq -r '.ConnectionStrings.DefaultConnection' $SOURCE_FILE)
az keyvault secret set \
  --vault-name $VAULT_NAME \
  --name "ConnectionStrings--DefaultConnection" \
  --value "$DB_CONNECTION"

REDIS_CONNECTION=$(jq -r '.ConnectionStrings.Redis' $SOURCE_FILE)
az keyvault secret set \
  --vault-name $VAULT_NAME \
  --name "ConnectionStrings--Redis" \
  --value "$REDIS_CONNECTION"

# Extract and migrate JWT secret
JWT_SECRET=$(jq -r '.JwtOptions.Secret' $SOURCE_FILE)
az keyvault secret set \
  --vault-name $VAULT_NAME \
  --name "JwtOptions--Secret" \
  --value "$JWT_SECRET"

echo "Migration complete!"
echo "Remember to update application to use Key Vault configuration provider"
```

## Testing Migration Scripts

### Pre-Testing Checklist

- [ ] **Migration script reviewed by team**
- [ ] **Rollback script prepared**
- [ ] **Test database prepared** (copy of production)
- [ ] **Backup taken**
- [ ] **Estimated execution time known**
- [ ] **Validation queries prepared**

### Test Environment Setup

```bash
# Create test database from production backup
sqlcmd -S test-server -U sa -P *** -Q "
    RESTORE DATABASE XFramework_Test 
    FROM DISK = 'C:\Backups\XFramework_Prod_Latest.bak'
    WITH MOVE 'XFramework' TO 'C:\Data\XFramework_Test.mdf',
         MOVE 'XFramework_log' TO 'C:\Data\XFramework_Test_log.ldf',
         REPLACE;
"

# Update connection string to point to test DB
export ConnectionStrings__DefaultConnection="Server=test-server;Database=XFramework_Test;..."
```

### Migration Testing Procedure

```bash
#!/bin/bash
# test-migration.sh

set -e  # Exit on error

MIGRATION_SCRIPT="migration-script.sql"
ROLLBACK_SCRIPT="rollback-script.sql"
DB_SERVER="test-server"
DB_NAME="XFramework_Test"

echo "=== Migration Test Procedure ==="

# Step 1: Pre-migration snapshot
echo "[1/6] Taking pre-migration snapshot..."
sqlcmd -S $DB_SERVER -d $DB_NAME -Q "
    SELECT 
        COUNT(*) as TableCount,
        (SELECT COUNT(*) FROM Users) as UserCount,
        (SELECT COUNT(*) FROM Products) as ProductCount,
        (SELECT COUNT(*) FROM Addresses) as AddressCount
" -o pre-migration-snapshot.txt

# Step 2: Run migration
echo "[2/6] Running migration script..."
time sqlcmd -S $DB_SERVER -d $DB_NAME -i $MIGRATION_SCRIPT -o migration-output.log

# Check for errors
if grep -qi "error\|failed" migration-output.log; then
    echo "ERROR: Migration failed! Check migration-output.log"
    exit 1
fi

# Step 3: Post-migration snapshot
echo "[3/6] Taking post-migration snapshot..."
sqlcmd -S $DB_SERVER -d $DB_NAME -Q "
    SELECT 
        COUNT(*) as TableCount,
        (SELECT COUNT(*) FROM Users) as UserCount,
        (SELECT COUNT(*) FROM Products) as ProductCount,
        (SELECT COUNT(*) FROM Addresses) as AddressCount
" -o post-migration-snapshot.txt

# Step 4: Run validation queries
echo "[4/6] Running validation..."
sqlcmd -S $DB_SERVER -d $DB_NAME -i validation-queries.sql -o validation-output.log

# Step 5: Test rollback
echo "[5/6] Testing rollback..."
sqlcmd -S $DB_SERVER -d $DB_NAME -i $ROLLBACK_SCRIPT -o rollback-output.log

# Check rollback errors
if grep -qi "error\|failed" rollback-output.log; then
    echo "ERROR: Rollback failed! Check rollback-output.log"
    exit 1
fi

# Step 6: Compare snapshots
echo "[6/6] Comparing snapshots..."
diff pre-migration-snapshot.txt post-migration-snapshot.txt || true

echo ""
echo "=== Test Complete ==="
echo "Review outputs:"
echo "  - migration-output.log"
echo "  - validation-output.log"
echo "  - rollback-output.log"
```

### Load Testing Migrations

```bash
# Simulate production load during migration
# Use k6 to generate traffic

# load-during-migration.js
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '5m', target: 50 },   // Simulate moderate load
  ],
};

export default function () {
  const res = http.get('http://test-api/api/products');
  
  check(res, {
    'status is 200': (r) => r.status === 200,
    'response time < 1s': (r) => r.timings.duration < 1000,
  });
  
  sleep(1);
}
```

```bash
# Run migration and load test simultaneously
# Terminal 1: Start load test
k6 run load-during-migration.js &

# Terminal 2: Run migration
./test-migration.sh

# Monitor application logs for errors
tail -f /var/log/xframework/app.log
```

## Rollback Procedures

### Automatic Rollback Script

```sql
-- Rollback: 20251120_AddProductDescriptionColumn.sql
-- Reverses changes made in the forward migration

BEGIN TRANSACTION;

-- Check if migration was applied
IF EXISTS(SELECT * FROM [__EFMigrationsHistory] 
          WHERE [MigrationId] = N'20251120160000_AddProductDescriptionColumn')
BEGIN
    PRINT 'Rolling back migration: AddProductDescriptionColumn';
    
    -- Drop index
    IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Products_Description')
    BEGIN
        DROP INDEX [IX_Products_Description] ON [Products];
        PRINT 'Dropped index IX_Products_Description';
    END;
    
    -- Drop column
    IF EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID('Products') 
               AND name = 'Description')
    BEGIN
        ALTER TABLE [Products] DROP COLUMN [Description];
        PRINT 'Dropped column Description from Products';
    END;
    
    -- Remove migration record
    DELETE FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251120160000_AddProductDescriptionColumn';
    
    PRINT 'Rollback completed successfully';
    COMMIT TRANSACTION;
END
ELSE
BEGIN
    PRINT 'Migration not found in history. No rollback needed.';
    ROLLBACK TRANSACTION;
END;
GO
```

### Data Migration Rollback

```sql
-- Rollback data migration
-- Reverses address migration

BEGIN TRANSACTION;

PRINT 'Starting rollback of address migration...';

-- Delete migrated addresses
DELETE FROM Addresses
WHERE CreatedDate >= '2025-11-20 02:00:00'  -- Migration start time
  AND CreatedDate <= '2025-11-20 02:30:00'; -- Migration end time

PRINT 'Deleted ' + CAST(@@ROWCOUNT AS VARCHAR) + ' address records';

-- Verify rollback
DECLARE @RemainingCount INT;
SELECT @RemainingCount = COUNT(*) FROM Addresses;
PRINT 'Remaining addresses: ' + CAST(@RemainingCount AS VARCHAR);

COMMIT TRANSACTION;

PRINT 'Rollback completed';
```

### Point-in-Time Recovery

```sql
-- Restore database to point before migration
RESTORE DATABASE XFramework
FROM DISK = 'C:\Backups\XFramework_PreMigration.bak'
WITH REPLACE,
     RECOVERY;

-- Verify restoration
SELECT @@VERSION;
SELECT COUNT(*) FROM Products;
SELECT COUNT(*) FROM Addresses;  -- Should be 0 if migration added this table
```

## Best Practices

### 1. Migration File Organization

```
Migrations/
├── Schema/
│   ├── 20251120_001_AddProductDescriptionColumn.sql
│   ├── 20251120_001_Rollback.sql
│   ├── 20251120_002_CreateAddressesTable.sql
│   └── 20251120_002_Rollback.sql
├── Data/
│   ├── 20251120_MigrateProductCategories.sql
│   ├── 20251120_MigrateUserAddresses.sql
│   └── validation/
│       ├── ValidateProductCategories.sql
│       └── ValidateUserAddresses.sql
└── README.md
```

### 2. Migration Naming Convention

```
Format: YYYYMMDD_NNN_DescriptiveName.sql

Examples:
- 20251120_001_AddProductDescriptionColumn.sql
- 20251120_002_CreateIndexOnUserEmail.sql
- 20251120_MigrateUserAddressesToNewTable.sql (data migration)
```

### 3. Migration Documentation Template

```sql
-- Migration: 20251120_001_AddProductDescriptionColumn.sql
-- Author: [Your Name]
-- Date: 2025-11-20
-- Ticket: JIRA-1234
-- 
-- Description:
--   Adds Description column to Products table to support rich product information.
--   This is part of the Product Enhancement feature.
--
-- Impact:
--   - Tables affected: Products
--   - Estimated rows: 50,000
--   - Estimated duration: < 1 minute
--   - Downtime required: No
--   - Backward compatible: Yes
--
-- Dependencies:
--   - None
--
-- Rollback: 20251120_001_Rollback.sql
--
-- Testing:
--   - Tested on staging: 2025-11-19
--   - Tested with load: Yes
--   - Validation queries: validation/ValidateProductDescription.sql
--
```

### 4. Pre-Migration Checklist

- [ ] Migration script peer-reviewed
- [ ] Rollback script prepared and tested
- [ ] Backup scheduled
- [ ] Estimated execution time known
- [ ] Impact assessment completed
- [ ] Communication sent (if user-facing)
- [ ] Monitoring alerts configured
- [ ] On-call engineer notified

### 5. Safe Migration Patterns

```csharp
// ✅ GOOD: Add nullable column first
migrationBuilder.AddColumn<string>(
    name: "NewColumn",
    table: "Products",
    nullable: true);  // Nullable allows gradual rollout

// ❌ BAD: Add non-nullable column without default
migrationBuilder.AddColumn<string>(
    name: "NewColumn",
    table: "Products",
    nullable: false);  // Will fail if table has data

// ✅ GOOD: Add non-nullable with default
migrationBuilder.AddColumn<string>(
    name: "NewColumn",
    table: "Products",
    nullable: false,
    defaultValue: "default-value");
```

### 6. Performance Considerations

```sql
-- ✅ GOOD: Add index AFTER data population
ALTER TABLE Products ADD NewColumn VARCHAR(100) NULL;

-- Populate data (in batches)
-- ... data migration ...

-- Then create index
CREATE INDEX IX_Products_NewColumn ON Products(NewColumn);

-- ❌ BAD: Add index BEFORE data population
CREATE INDEX IX_Products_NewColumn ON Products(NewColumn);
-- Index will slow down data inserts
```

## Troubleshooting

### Issue: Migration Timeout

**Symptoms**:
```
Execution Timeout Expired. The timeout period elapsed prior to completion.
```

**Solutions**:
```sql
-- Increase command timeout in code
-- Or break into smaller batches
-- Or use WAITFOR to add delays

DECLARE @BatchSize INT = 1000;
WHILE EXISTS (SELECT 1 FROM SourceTable WHERE Migrated = 0)
BEGIN
    -- Process batch
    UPDATE TOP (@BatchSize) SourceTable
    SET Migrated = 1
    WHERE Migrated = 0;
    
    -- Small delay
    WAITFOR DELAY '00:00:00.500'; -- 500ms
END;
```

### Issue: Deadlock During Migration

**Symptoms**:
```
Transaction was deadlocked on lock resources with another process.
```

**Solutions**:
```sql
-- Use NOLOCK hint for reads (acceptable during migration)
SELECT * FROM Products WITH (NOLOCK)
WHERE ...

-- Or use smaller batches
-- Or run migration during low-traffic window
-- Or use SET DEADLOCK_PRIORITY LOW
```

### Issue: Data Validation Fails

**Symptoms**:
- Row counts don't match
- Data integrity check fails

**Solutions**:
```sql
-- Re-run migration for missing records
INSERT INTO Addresses (...)
SELECT ...
FROM Users u
WHERE NOT EXISTS (SELECT 1 FROM Addresses a WHERE a.UserId = u.Id);

-- Compare before/after snapshots
-- Identify discrepancies
-- Apply corrective measures
```

### Issue: Migration Corrupts Data

**Immediate Actions**:
1. Stop the migration
2. Assess damage scope
3. Restore from backup if critical
4. Fix migration script
5. Re-test thoroughly

**Prevention**:
- Always test in staging first
- Use transactions where possible
- Take backups before migration
- Validate data after migration

## Related Documentation

- [Deployment Runbook](./deployment-runbook.md)
- [Performance Testing Guide](./performance-testing-guide.md)
- [Incident Response Plan](./incident-response-plan.md)
- [Security Audit Checklist](./security-audit-checklist.md)