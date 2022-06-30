﻿using IdentityServer.Domain.BusinessObjects;

namespace IdentityServer.Core.DataAccess;

public class DataLayer : XnelSystemsContext, IDataLayer
{
    public DataLayer(DbContextOptions<XnelSystemsContext> options)
        : base(options)
    {
    }

    public void RollBack()
    {
        var context = this;
        var changedEntries = context.ChangeTracker.Entries()
            .Where(x => x.State != EntityState.Unchanged).ToList();

        foreach (var entry in changedEntries)
        {
            switch (entry.State)
            {
                case EntityState.Modified:
                    entry.CurrentValues.SetValues(entry.OriginalValues);
                    entry.State = EntityState.Unchanged;
                    break;
                case EntityState.Added:
                    entry.State = EntityState.Detached;
                    break;
                case EntityState.Deleted:
                    entry.State = EntityState.Unchanged;
                    break;
            }
        }
    }

    public override int SaveChanges()
    {
        Database.EnsureCreated();
        var auditEntries = OnBeforeSaveChanges();
        var result = base.SaveChanges();
        OnAfterSaveChanges(auditEntries);
        return result;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await Database.EnsureCreatedAsync(cancellationToken);
        var auditEntries = OnBeforeSaveChanges();
        var result = await base.SaveChangesAsync(cancellationToken);
        OnAfterSaveChanges(auditEntries);
        return result;
    }

    public List<AuditEntryBO> OnBeforeSaveChanges()
    {
        ChangeTracker.DetectChanges();
        var auditEntries = new List<AuditEntryBO>();
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is AuditHistory || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                continue;

            var auditEntry = new AuditEntryBO(entry);
            auditEntry.TableName = entry.Metadata.GetTableName();
            auditEntries.Add(auditEntry);

            foreach (var property in entry.Properties)
            {
                switch (property.Metadata.Name)
                {
                    case "IsEnabled":
                        property.CurrentValue ??= true;
                        break;
                    case "CreatedAt":
                        if (entry.State == EntityState.Added)
                        {
                            property.CurrentValue = DateTime.SpecifyKind(DateTime.Now.ToUniversalTime(), DateTimeKind.Utc);
                        }
                        break;
                    case "ModifiedAt":
                        property.CurrentValue = DateTime.SpecifyKind(DateTime.Now.ToUniversalTime(), DateTimeKind.Utc);
                        break;
                    case "IsDeleted":
                        property.CurrentValue ??= false;
                        break;
                    case "CreatedBy":
                        property.CurrentValue ??= (long?) 0m;
                        break;
                    case "Guid":
                        if (string.IsNullOrEmpty($"{property.CurrentValue}"))
                        {
                            property.CurrentValue = $"{Guid.NewGuid()}";
                        }
                        break;
                    default:
                        break;
                }

                if (property.IsTemporary)
                {
                    // value will be generated by the database, get the value after saving
                    auditEntry.TemporaryProperties.Add(property);
                    continue;
                }

                var propertyName = property.Metadata.Name;
                if (property.Metadata.IsPrimaryKey())
                {
                    auditEntry.KeyValues[propertyName] = property.CurrentValue;
                    continue;
                }

                switch (entry.State)
                {
                    case EntityState.Added:
                        auditEntry.NewValues[propertyName] = property.CurrentValue;
                        auditEntry.QueryAction = entry.State.ToString();
                        break;

                    case EntityState.Deleted:
                        auditEntry.OldValues[propertyName] = property.OriginalValue;
                        auditEntry.QueryAction = entry.State.ToString();
                        break;

                    case EntityState.Modified:
                        if (property.IsModified)
                        {
                            auditEntry.OldValues[propertyName] = property.OriginalValue;
                            auditEntry.NewValues[propertyName] = property.CurrentValue;
                            auditEntry.QueryAction = entry.State.ToString();
                        }
                        break;
                }
            }
        }

        // Save audit entities that have all the modifications
        foreach (var auditEntry in auditEntries.Where(_ => !_.HasTemporaryProperties))
        {
            AuditHistories.Add(auditEntry.ToAudit());
        }

        // keep a list of entries where the value of some properties are unknown at this step
        return auditEntries.Where(_ => _.HasTemporaryProperties).ToList();
    }

    private int OnAfterSaveChanges(List<AuditEntryBO> auditEntries)
    {
        if (auditEntries == null || auditEntries.Count == 0)
            return 1;

        foreach (var auditEntry in auditEntries)
        {
            // Get the final value of the temporary properties
            foreach (var prop in auditEntry.TemporaryProperties)
            {
                                        
                if (prop.Metadata.IsPrimaryKey())
                {
                    auditEntry.KeyValues[prop.Metadata.Name] = prop.CurrentValue;
                }
                else
                {
                    auditEntry.NewValues[prop.Metadata.Name] = prop.CurrentValue;
                }
            }

            // Save the Audit entry
            AuditHistories.Add(auditEntry.ToAudit());
        }

        return SaveChanges();
    }
}