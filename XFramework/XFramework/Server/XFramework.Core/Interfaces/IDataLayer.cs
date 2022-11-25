﻿using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using XFramework.Domain.DataTransferObjects;

namespace XFramework.Core.Interfaces
{
    public interface IDataLayer
    {
        public ChangeTracker ChangeTracker { get; }
        public DatabaseFacade Database { get; }
        public IModel Model { get; }
        public DbContextId ContextId { get; }
        public EntityEntry Add([NotNullAttribute] object entity);
        public EntityEntry<TEntity> Add<TEntity>([NotNullAttribute] TEntity entity) where TEntity : class;

        public void AddRange([NotNullAttribute] params object[] entities);
        public void AddRange([NotNullAttribute] IEnumerable<object> entities);

        public Task AddRangeAsync([NotNullAttribute] params object[] entities);

        public EntityEntry Attach([NotNullAttribute] object entity);

        public EntityEntry<TEntity> Attach<TEntity>([NotNullAttribute] TEntity entity) where TEntity : class;

        public void AttachRange([NotNullAttribute] IEnumerable<object> entities);
        public void AttachRange([NotNullAttribute] params object[] entities);
        public void Dispose();
        public ValueTask DisposeAsync();
        public EntityEntry Entry([NotNullAttribute] object entity);
        public EntityEntry<TEntity> Entry<TEntity>([NotNullAttribute] TEntity entity) where TEntity : class;
        public EntityEntry Remove([NotNullAttribute] object entity);
        public EntityEntry<TEntity> Remove<TEntity>([NotNullAttribute] TEntity entity) where TEntity : class;
        public void RemoveRange([NotNullAttribute] IEnumerable<object> entities);
        public void RemoveRange([NotNullAttribute] params object[] entities);
        public int SaveChanges(bool acceptAllChangesOnSuccess);
        public int SaveChanges();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        public DbSet<TEntity> Set<TEntity>() where TEntity : class;
        public EntityEntry Update([NotNullAttribute] object entity);
        public EntityEntry<TEntity> Update<TEntity>([NotNullAttribute] TEntity entity) where TEntity : class;
        public void UpdateRange([NotNullAttribute] IEnumerable<object> entities);
        public void UpdateRange([NotNullAttribute] params object[] entities);

        public DbSet<AddressBarangay> AddressBarangays { get; set; }

        public DbSet<AddressCity> AddressCities { get; set; }

        public DbSet<AddressCountry> AddressCountries { get; set; }

        public DbSet<AddressProvince> AddressProvinces { get; set; }

        public DbSet<AddressRegion> AddressRegions { get; set; }

        public DbSet<Application> Applications { get; set; }

        public DbSet<AuditField> AuditFields { get; set; }

        public DbSet<AuditHistory> AuditHistories { get; set; }

        public DbSet<AuthorizationLog> AuthorizationLogs { get; set; }

        public DbSet<CurrencyEntity> CurrencyEntities { get; set; }

        public DbSet<DepositRequest> DepositRequests { get; set; }

        public DbSet<Enterprise> Enterprises { get; set; }

        public DbSet<ExchangeRate> ExchangeRates { get; set; }

        public DbSet<IdentityAddress> IdentityAddresses { get; set; }

        public DbSet<IdentityAddressEntity> IdentityAddressEntities { get; set; }

        public DbSet<IdentityContact> IdentityContacts { get; set; }

        public DbSet<IdentityContactEntity> IdentityContactEntities { get; set; }

        public DbSet<IdentityContactGroup> IdentityContactGroups { get; set; }

        public DbSet<IdentityCredential> IdentityCredentials { get; set; }

        public DbSet<IdentityFavorite> IdentityFavorites { get; set; }

        public DbSet<IdentityInformation> IdentityInformations { get; set; }

        public DbSet<IdentityRole> IdentityRoles { get; set; }

        public DbSet<IdentityRoleEntity> IdentityRoleEntities { get; set; }

        public DbSet<IdentityRoleEntityGroup> IdentityRoleEntityGroups { get; set; }

        public DbSet<IdentityVerification> IdentityVerifications { get; set; }

        public DbSet<IdentityVerificationEntity> IdentityVerificationEntities { get; set; }

        public DbSet<Log> Logs { get; set; }

        public DbSet<RegistryConfiguration> RegistryConfigurations { get; set; }

        public DbSet<RegistryConfigurationGroup> RegistryConfigurationGroups { get; set; }

        public DbSet<RegistryFavoriteEntity> RegistryFavoriteEntities { get; set; }

        public DbSet<SessionDatum> SessionData { get; set; }

        public DbSet<SessionEntity> SessionEntities { get; set; }

        public DbSet<Wallet> Wallets { get; set; }

        public DbSet<WalletAddress> WalletAddresses { get; set; }

        public DbSet<WalletEntity> WalletEntities { get; set; }

        public DbSet<WalletTransaction> WalletTransactions { get; set; }

        public DbSet<WithdrawalRequest> WithdrawalRequests { get; set; }
    }
}