using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using XFramework.Domain.BusinessObjects;
using XFramework.Domain.DataTransferObjects;

namespace XFramework.Core.Interfaces
{
    public interface IDataLayer
    {
        public  ChangeTracker ChangeTracker { get; }
        public  DatabaseFacade Database { get; }
        public  IModel Model { get; }
        public  DbContextId ContextId { get; }
        public  EntityEntry Add([NotNullAttribute] object entity);
        public  EntityEntry<TEntity> Add<TEntity>([NotNullAttribute] TEntity entity) where TEntity : class;
       
        public  void AddRange([NotNullAttribute] params object[] entities);
        public  void AddRange([NotNullAttribute] IEnumerable<object> entities);
        
        public  Task AddRangeAsync([NotNullAttribute] params object[] entities);
        
        public  EntityEntry Attach([NotNullAttribute] object entity);
        
        public  EntityEntry<TEntity> Attach<TEntity>([NotNullAttribute] TEntity entity) where TEntity : class;
        
        public  void AttachRange([NotNullAttribute] IEnumerable<object> entities);
        public  void AttachRange([NotNullAttribute] params object[] entities);
        public  void Dispose();
        public  ValueTask DisposeAsync();
        public  EntityEntry Entry([NotNullAttribute] object entity);
        public  EntityEntry<TEntity> Entry<TEntity>([NotNullAttribute] TEntity entity) where TEntity : class;
        public  EntityEntry Remove([NotNullAttribute] object entity);
        public  EntityEntry<TEntity> Remove<TEntity>([NotNullAttribute] TEntity entity) where TEntity : class;
        public  void RemoveRange([NotNullAttribute] IEnumerable<object> entities);
        public  void RemoveRange([NotNullAttribute] params object[] entities);
        public  int SaveChanges(bool acceptAllChangesOnSuccess);
        public  int SaveChanges();
        public  Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        public  DbSet<TEntity> Set<TEntity>() where TEntity : class;
        public  EntityEntry Update([NotNullAttribute] object entity);
        public  EntityEntry<TEntity> Update<TEntity>([NotNullAttribute] TEntity entity) where TEntity : class;
        public  void UpdateRange([NotNullAttribute] IEnumerable<object> entities);
        public  void UpdateRange([NotNullAttribute] params object[] entities);

        public void RollBack();
        public List<AuditEntry> OnBeforeSaveChanges();

        public DbSet<TblAddressEntities> TblAddressEntities { get; set; }
        public DbSet<TblAddresses> TblAddresses { get; set; }
        public DbSet<TblApplication> TblApplication { get; set; }
        public DbSet<TblApplicationLogs> TblApplicationLogs { get; set; }
        public DbSet<TblAuditFields> TblAuditFields { get; set; }
        public DbSet<TblAuditHistory> TblAuditHistory { get; set; }
        public DbSet<TblCompanies> TblCompanies { get; set; }
        public DbSet<TblCompanyEntities> TblCompanyEntities { get; set; }
        public DbSet<TblSessionData> TblSessionData { get; set; }
        public DbSet<TblSessionEntities> TblSessionEntities { get; set; }
        public DbSet<TblUserAuthHistory> TblUserAuthHistory { get; set; }
        public DbSet<TblUserContactEntities> TblUserContactEntities { get; set; }
        public DbSet<TblUserContacts> TblUserContacts { get; set; }
        public DbSet<TblUserCredentials> TblUserCredentials { get; set; }
        public DbSet<TblUserInfo> TblUserInfo { get; set; }
        public DbSet<TblUserRoleEntities> TblUserRoleEntities { get; set; }
        public DbSet<TblUserRoles> TblUserRoles { get; set; }
    } 
}
