using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Domain.BusinessObject;
using IdentityServer.Domain.DataTableObject;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace IdentityServer.Core.Interfaces
{
    public interface IDataLayer
    {
        public DatabaseFacade Database { get; }
        public  EntityEntry<TEntity> Entry<TEntity>([NotNull] TEntity entity) where TEntity : class;
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
        
        public virtual DbSet<TblAddressBarangay> TblAddressBarangays { get; set; }
        public virtual DbSet<TblAddressCity> TblAddressCities { get; set; }
        public virtual DbSet<TblAddressCountry> TblAddressCountries { get; set; }
        public virtual DbSet<TblAddressProvince> TblAddressProvinces { get; set; }
        public virtual DbSet<TblAddressRegion> TblAddressRegions { get; set; }
        public virtual DbSet<TblApplication> TblApplications { get; set; }
        public virtual DbSet<TblAuditField> TblAuditFields { get; set; }
        public virtual DbSet<TblAuditHistory> TblAuditHistories { get; set; }
        public virtual DbSet<TblAuthorizationLog> TblAuthorizationLogs { get; set; }
        public virtual DbSet<TblConfiguration> TblConfigurations { get; set; }
        public virtual DbSet<TblCurrencyEntity> TblCurrencyEntities { get; set; }
        public virtual DbSet<TblEnterprise> TblEnterprises { get; set; }
        public virtual DbSet<TblExchangeRate> TblExchangeRates { get; set; }
        public virtual DbSet<TblIdentityAddress> TblIdentityAddresses { get; set; }
        public virtual DbSet<TblIdentityAddressEntity> TblIdentityAddressEntities { get; set; }
        public virtual DbSet<TblIdentityContact> TblIdentityContacts { get; set; }
        public virtual DbSet<TblIdentityContactEntity> TblIdentityContactEntities { get; set; }
        public virtual DbSet<TblIdentityCredential> TblIdentityCredentials { get; set; }
        public virtual DbSet<TblIdentityInfo> TblIdentityInfos { get; set; }
        public virtual DbSet<TblIdentityRole> TblIdentityRoles { get; set; }
        public virtual DbSet<TblIdentityRoleEntity> TblIdentityRoleEntities { get; set; }
        public virtual DbSet<TblIdentityVerification> TblIdentityVerifications { get; set; }
        public virtual DbSet<TblIdentityVerificationEntity> TblIdentityVerificationEntities { get; set; }
        public virtual DbSet<TblLog> TblLogs { get; set; }
        public virtual DbSet<TblSessionDatum> TblSessionData { get; set; }
        public virtual DbSet<TblSessionEntity> TblSessionEntities { get; set; }
    }
}