# Fix EF Core Data Access Patterns

You are reviewing and fixing Entity Framework Core usage in XFramework code.

## Context
Read `docs/standards/xframework-best-practices.md` sections 8 (Data Access) and 15 (Performance).

## Arguments
$ARGUMENTS should specify the file or module to review EF patterns in.

## Steps

1. **Read the specified files** containing EF Core queries
2. **Identify violations** of EF Core best practices
3. **Fix each issue** with an explanation

## Common Issues to Find and Fix

### 1. Missing AsNoTracking on reads
```csharp
// ❌ Bad — tracked by default (even though global NoTracking is set, be explicit for clarity)
var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);

// ✅ Good
var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
```

### 2. Loading full entities when only DTOs needed
```csharp
// ❌ Bad — loads all columns
var product = await db.Products.FindAsync(id, ct);
return new ProductResponse(product.Id, product.Name, product.Price);

// ✅ Good — projection at database level
var response = await db.Products
    .Where(p => p.Id == id)
    .Select(p => new ProductResponse(p.Id, p.Name, p.Price))
    .FirstOrDefaultAsync(ct);
```

### 3. N+1 queries
```csharp
// ❌ Bad — executes N+1 queries
var orders = await db.Orders.ToListAsync(ct);
foreach (var order in orders)
    order.Items = await db.OrderItems.Where(i => i.OrderId == order.Id).ToListAsync(ct);

// ✅ Good — single query with include
var orders = await db.Orders
    .Include(o => o.Items)
    .AsSplitQuery()
    .ToListAsync(ct);
```

### 4. SaveChanges in loop
```csharp
// ❌ Bad
foreach (var item in items)
{
    db.Items.Add(item);
    await db.SaveChangesAsync(ct);
}

// ✅ Good
db.Items.AddRange(items);
await db.SaveChangesAsync(ct);
```

### 5. Manual soft-delete / tenant filtering
```csharp
// ❌ Bad — global query filter already handles this
var products = await db.Products.Where(p => !p.IsDeleted && p.TenantId == tenantId).ToListAsync(ct);

// ✅ Good — filters applied automatically by XDbContext
var products = await db.Products.ToListAsync(ct);
```

### 6. Missing AsSplitQuery with multiple Includes
```csharp
// ❌ Bad — cartesian explosion
var order = await db.Orders.Include(o => o.Items).Include(o => o.Payments).FirstOrDefaultAsync(ct);

// ✅ Good
var order = await db.Orders
    .Include(o => o.Items)
    .Include(o => o.Payments)
    .AsSplitQuery()
    .FirstOrDefaultAsync(ct);
```

### 7. Use ExecuteUpdate/ExecuteDelete for bulk ops
```csharp
// ❌ Bad — loads all entities into memory
var products = await db.Products.Where(p => p.CategoryId == catId).ToListAsync(ct);
foreach (var p in products) p.IsAvailable = false;
await db.SaveChangesAsync(ct);

// ✅ Good — single SQL UPDATE statement
await db.Products
    .Where(p => p.CategoryId == catId)
    .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsAvailable, false), ct);
```

## Output
For each issue found, report: file, line, issue description, and the fix applied.
