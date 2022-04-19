using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Community.Core.Interfaces;

public interface IDataLayer
{
    public DatabaseFacade Database { get; }
    public  EntityEntry<TEntity> Entry<TEntity>([NotNull] TEntity entity) where TEntity : class;
    public  EntityEntry Remove([NotNull] object entity);
    public  EntityEntry<TEntity> Remove<TEntity>([NotNull] TEntity entity) where TEntity : class;
    public  void RemoveRange([NotNull] IEnumerable<object> entities);
    public  void RemoveRange([NotNull] params object[] entities);
    public  int SaveChanges(bool acceptAllChangesOnSuccess);
    public  int SaveChanges();
    public  Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    public  DbSet<TEntity> Set<TEntity>() where TEntity : class;
    public  EntityEntry Update([NotNull] object entity);
    public  EntityEntry<TEntity> Update<TEntity>([NotNull] TEntity entity) where TEntity : class;
    public  void UpdateRange([NotNull] IEnumerable<object> entities);
    public  void UpdateRange([NotNull] params object[] entities);

    public void RollBack();

    public DbSet<AddressBarangay> AddressBarangays { get; set; }
    public DbSet<AddressCity> AddressCities { get; set; }
    public DbSet<AddressCountry> AddressCountries { get; set; }
    public DbSet<AddressProvince> AddressProvinces { get; set; }
    public DbSet<AddressRegion> AddressRegions { get; set; }
    public DbSet<Application> Applications { get; set; }
    public DbSet<AuditField> AuditFields { get; set; }
    public DbSet<AuditHistory> AuditHistories { get; set; }
    public DbSet<AuthorizationLog> AuthorizationLogs { get; set; }
    public DbSet<CommunityConnection> CommunityConnections { get; set; }
    public DbSet<CommunityConnectionEntity> CommunityConnectionEntities { get; set; }
    public DbSet<CommunityContent> CommunityContents { get; set; }
    public DbSet<CommunityContentEntity> CommunityContentEntities { get; set; }
    public DbSet<CommunityContentFile> CommunityContentFiles { get; set; }
    public DbSet<CommunityContentReaction> CommunityContentReactions { get; set; }
    public DbSet<CommunityContentReactionEntity> CommunityContentReactionEntities { get; set; }
    public DbSet<CommunityIdentity> CommunityIdentities { get; set; }
    public DbSet<CommunityIdentityEntity> CommunityIdentityEntities { get; set; }
    public DbSet<CurrencyEntity> CurrencyEntities { get; set; }
    public DbSet<Enterprise> Enterprises { get; set; }
    public DbSet<ExchangeRate> ExchangeRates { get; set; }
    public DbSet<IdentityAddress> IdentityAddresses { get; set; }
    public DbSet<IdentityAddressEntity> IdentityAddressEntities { get; set; }
    public DbSet<IdentityContact> IdentityContacts { get; set; }
    public DbSet<IdentityContactEntity> IdentityContactEntities { get; set; }
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
}