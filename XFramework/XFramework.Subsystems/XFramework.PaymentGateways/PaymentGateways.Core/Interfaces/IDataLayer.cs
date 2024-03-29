﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using PaymentGateways.Domain.BusinessObjects;
using PaymentGateways.Domain.DataTransferObjects;

namespace PaymentGateways.Core.Interfaces;

public interface IDataLayer
{
    public DatabaseFacade Database { get; }
    public EntityEntry<TEntity> Entry<TEntity>([NotNull] TEntity entity) where TEntity : class;
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

    public void RollBack();
    public List<AuditEntryBO> OnBeforeSaveChanges();

      
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
        public DbSet<Gateway> Gateways { get; set; }
        public DbSet<GatewayCategory> GatewayCategories { get; set; }
        public DbSet<GatewayEndpoint> GatewayEndpoints { get; set; }
        public DbSet<GatewayEntity> GatewayEntities { get; set; }
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