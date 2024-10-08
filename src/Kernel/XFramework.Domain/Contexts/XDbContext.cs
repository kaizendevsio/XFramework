using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Contexts;

public class XDbContext : DbContext
{
    public XDbContext()
    {
        
    }
    
    public XDbContext(DbContextOptions options)
        : base(options)
    {
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            // Check if the IsDeleted property exists
            var isDeletedProperty = clrType.GetProperty("IsDeleted");
            if (isDeletedProperty == null || isDeletedProperty.PropertyType != typeof(bool))
                continue;

            // Check if the IsEnabled property exists
            var isEnabledProperty = clrType.GetProperty("IsEnabled");
            if (isEnabledProperty == null || isEnabledProperty.PropertyType != typeof(bool))
                continue;

            var parameter = Expression.Parameter(clrType, "p");
            var propIsDeleted = Expression.Property(parameter, isDeletedProperty.Name);
            var falseValue = Expression.Constant(false);
            var trueValue = Expression.Constant(true);
            var isNotDeleted = Expression.Equal(propIsDeleted, falseValue);
            var finalExpression = isNotDeleted;
            var filter = Expression.Lambda(finalExpression, parameter);
            modelBuilder.Entity(clrType).HasQueryFilter((LambdaExpression)filter);
        }
        base.OnModelCreating(modelBuilder);
    }
    
    public override int SaveChanges()
    {
        OnBeforeSaveChanges();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        OnBeforeSaveChanges();
        return await base.SaveChangesAsync(cancellationToken);
    }
    
    public void OnBeforeSaveChanges()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
            {
                continue;
            }
            
            foreach (var property in entry.Properties)
            {
                switch (property.Metadata.Name)
                {
                    case nameof(BaseModel.IsEnabled):
                        property.CurrentValue ??= true;
                        break;
                    case nameof(BaseModel.CreatedAt):
                        if (entry.State == EntityState.Added)
                        {
                            property.CurrentValue = DateTime.Now.ToUniversalTime();
                        }
                        break;
                    case nameof(BaseModel.ModifiedAt):
                        property.CurrentValue = entry.State == EntityState.Modified 
                            ? DateTime.Now.ToUniversalTime() 
                            : property.CurrentValue;
                        break;
                    case nameof(BaseModel.IsDeleted):
                        if (entry.State == EntityState.Deleted)
                        {
                            entry.State = EntityState.Modified;
                            property.CurrentValue = true;
                            
                            entry.Properties.First(x => x.Metadata.Name == nameof(BaseModel.DeletedAt)).CurrentValue = DateTime.Now.ToUniversalTime();
                        }
                        else
                        {
                            property.CurrentValue ??= false;
                        }
                        break;
                    case nameof(BaseModel.TenantId):
                        if (property.CurrentValue is null || (Guid)property.CurrentValue == Guid.Empty)
                        {
                            throw new Exception("Cannot save entity without TenantId");
                        }
                        break;
                    default:
                        break;
                }
                
            }
        }
    }
}