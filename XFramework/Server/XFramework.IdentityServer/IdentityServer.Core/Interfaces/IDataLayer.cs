using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Domain.BusinessObjects;
using IdentityServer.Domain.DataTableObjects;
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
        
        public DbSet<TblAddressBarangay> TblAddressBarangays { get; set; }
        public DbSet<TblAddressCity> TblAddressCities { get; set; }
        public DbSet<TblAddressCountry> TblAddressCountries { get; set; }
        public DbSet<TblAddressProvince> TblAddressProvinces { get; set; }
        public DbSet<TblAddressRegion> TblAddressRegions { get; set; }
        public DbSet<TblApplication> TblApplications { get; set; }
        public DbSet<TblAuditField> TblAuditFields { get; set; }
        public DbSet<TblAuditHistory> TblAuditHistories { get; set; }
        public DbSet<TblAuthorizationLog> TblAuthorizationLogs { get; set; }
        public DbSet<TblConfiguration> TblConfigurations { get; set; }
        public DbSet<TblCurrencyEntity> TblCurrencyEntities { get; set; }
        public DbSet<TblEnterprise> TblEnterprises { get; set; }
        public DbSet<TblExchangeRate> TblExchangeRates { get; set; }
        public DbSet<TblIdentityAddress> TblIdentityAddresses { get; set; }
        public DbSet<TblIdentityAddressEntity> TblIdentityAddressEntities { get; set; }
        public DbSet<TblIdentityContact> TblIdentityContacts { get; set; }
        public DbSet<TblIdentityContactEntity> TblIdentityContactEntities { get; set; }
        public DbSet<TblIdentityCredential> TblIdentityCredentials { get; set; }
        public DbSet<TblIdentityInformation> TblIdentityInformations { get; set; }
        public DbSet<TblIdentityRole> TblIdentityRoles { get; set; }
        public DbSet<TblIdentityRoleEntity> TblIdentityRoleEntities { get; set; }
        public DbSet<TblIdentityVerification> TblIdentityVerifications { get; set; }
        public DbSet<TblIdentityVerificationEntity> TblIdentityVerificationEntities { get; set; }
        public DbSet<TblLog> TblLogs { get; set; }
        public DbSet<TblSessionDatum> TblSessionData { get; set; }
        public DbSet<TblSessionEntity> TblSessionEntities { get; set; }
    }
}