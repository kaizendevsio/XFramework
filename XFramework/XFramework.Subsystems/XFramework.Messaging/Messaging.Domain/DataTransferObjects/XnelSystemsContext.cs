using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Messaging.Domain.DataTransferObjects;

public partial class XnelSystemsContext : DbContext
{
    public XnelSystemsContext()
    {
    }

    public XnelSystemsContext(DbContextOptions<XnelSystemsContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AddressBarangay> AddressBarangays { get; set; }

    public virtual DbSet<AddressCity> AddressCities { get; set; }

    public virtual DbSet<AddressCountry> AddressCountries { get; set; }

    public virtual DbSet<AddressProvince> AddressProvinces { get; set; }

    public virtual DbSet<AddressRegion> AddressRegions { get; set; }

    public virtual DbSet<Application> Applications { get; set; }

    public virtual DbSet<AuditField> AuditFields { get; set; }

    public virtual DbSet<AuditHistory> AuditHistories { get; set; }

    public virtual DbSet<AuthorizationLog> AuthorizationLogs { get; set; }

    public virtual DbSet<CurrencyEntity> CurrencyEntities { get; set; }

    public virtual DbSet<Enterprise> Enterprises { get; set; }

    public virtual DbSet<ExchangeRate> ExchangeRates { get; set; }

    public virtual DbSet<IdentityAddress> IdentityAddresses { get; set; }

    public virtual DbSet<IdentityAddressEntity> IdentityAddressEntities { get; set; }

    public virtual DbSet<IdentityContact> IdentityContacts { get; set; }

    public virtual DbSet<IdentityContactEntity> IdentityContactEntities { get; set; }

    public virtual DbSet<IdentityContactGroup> IdentityContactGroups { get; set; }

    public virtual DbSet<IdentityCredential> IdentityCredentials { get; set; }

    public virtual DbSet<IdentityFavorite> IdentityFavorites { get; set; }

    public virtual DbSet<IdentityInformation> IdentityInformations { get; set; }

    public virtual DbSet<IdentityRole> IdentityRoles { get; set; }

    public virtual DbSet<IdentityRoleEntity> IdentityRoleEntities { get; set; }

    public virtual DbSet<IdentityRoleEntityGroup> IdentityRoleEntityGroups { get; set; }

    public virtual DbSet<IdentityVerification> IdentityVerifications { get; set; }

    public virtual DbSet<IdentityVerificationEntity> IdentityVerificationEntities { get; set; }

    public virtual DbSet<Log> Logs { get; set; }

    public virtual DbSet<Message> Messages { get; set; }

    public virtual DbSet<MessageDelivery> MessageDeliveries { get; set; }

    public virtual DbSet<MessageDeliveryEntity> MessageDeliveryEntities { get; set; }

    public virtual DbSet<MessageDirect> MessageDirects { get; set; }

    public virtual DbSet<MessageFile> MessageFiles { get; set; }

    public virtual DbSet<MessageReaction> MessageReactions { get; set; }

    public virtual DbSet<MessageReactionEntity> MessageReactionEntities { get; set; }

    public virtual DbSet<MessageThread> MessageThreads { get; set; }

    public virtual DbSet<MessageThreadEntity> MessageThreadEntities { get; set; }

    public virtual DbSet<MessageThreadMember> MessageThreadMembers { get; set; }

    public virtual DbSet<MessageThreadMemberGroup> MessageThreadMemberGroups { get; set; }

    public virtual DbSet<MessageThreadMemberRole> MessageThreadMemberRoles { get; set; }

    public virtual DbSet<MessageType> MessageTypes { get; set; }

    public virtual DbSet<RegistryConfiguration> RegistryConfigurations { get; set; }

    public virtual DbSet<RegistryConfigurationGroup> RegistryConfigurationGroups { get; set; }

    public virtual DbSet<RegistryFavoriteEntity> RegistryFavoriteEntities { get; set; }

    public virtual DbSet<SessionDatum> SessionData { get; set; }

    public virtual DbSet<SessionEntity> SessionEntities { get; set; }

    public virtual DbSet<StorageFile> StorageFiles { get; set; }

    public virtual DbSet<StorageFileEntity> StorageFileEntities { get; set; }

    public virtual DbSet<StorageFileIdentifier> StorageFileIdentifiers { get; set; }

    public virtual DbSet<StorageFileIdentifierGroup> StorageFileIdentifierGroups { get; set; }

    /*protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see http://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseNpgsql("Host=localhost;Database=XnelSystems;Username=dbAdmin;Password=4*5WD-K8%f*NqmPY");*/

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("uuid-ossp");

        modelBuilder.Entity<AddressBarangay>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("addresses_refbrgy_pk");

            entity.ToTable("AddressBarangay", "GeoLocation");

            entity.HasIndex(e => e.Code, "addresses_refbrgy_code_uindex").IsUnique();

            entity.HasIndex(e => e.Guid, "tbl_addressbarangay_guid_uindex").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");

            entity.HasOne(d => d.CityCodeNavigation).WithMany(p => p.AddressBarangays)
                .HasPrincipalKey(p => p.Code)
                .HasForeignKey(d => d.CityCode)
                .HasConstraintName("tbl_addressbarangay_tbl_addresscity_code_fk");
        });

        modelBuilder.Entity<AddressCity>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_addresscity_pk");

            entity.ToTable("AddressCity", "GeoLocation");

            entity.HasIndex(e => e.Code, "tbl_addresscity_code_uindex").IsUnique();

            entity.HasIndex(e => e.Guid, "tbl_addresscity_guid_uindex").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");

            entity.HasOne(d => d.ProvCodeNavigation).WithMany(p => p.AddressCities)
                .HasPrincipalKey(p => p.Code)
                .HasForeignKey(d => d.ProvCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("tbl_addresscity_tbl_addressprovince_code_fk");
        });

        modelBuilder.Entity<AddressCountry>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_AddressCountry_pkey");

            entity.ToTable("AddressCountry", "GeoLocation");

            entity.HasIndex(e => e.Guid, "tbl_addresscountry_guid_uindex").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasIdentityOptions(null, null, null, 2147483647L, null, null)
                .HasColumnName("ID");
            entity.Property(e => e.CurrencyId).HasColumnName("CurrencyID");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.IsoCode2).HasMaxLength(2);
            entity.Property(e => e.IsoCode3).HasMaxLength(3);
            entity.Property(e => e.Language).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(50);
            entity.Property(e => e.PhoneCountryCode).HasMaxLength(9);

            entity.HasOne(d => d.Currency).WithMany(p => p.AddressCountries)
                .HasForeignKey(d => d.CurrencyId)
                .HasConstraintName("CurrencyID");
        });

        modelBuilder.Entity<AddressProvince>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_addressprovince_pk");

            entity.ToTable("AddressProvince", "GeoLocation");

            entity.HasIndex(e => e.Code, "tbl_addressprovince_code_uindex").IsUnique();

            entity.HasIndex(e => e.Guid, "tbl_addressprovince_guid_uindex").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");

            entity.HasOne(d => d.RegCodeNavigation).WithMany(p => p.AddressProvinces)
                .HasPrincipalKey(p => p.Code)
                .HasForeignKey(d => d.RegCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("tbl_addressprovince_tbl_addressregions_code_fk");
        });

        modelBuilder.Entity<AddressRegion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_addressregions_pk");

            entity.ToTable("AddressRegion", "GeoLocation");

            entity.HasIndex(e => e.Code, "tbl_addressregions_code_uindex").IsUnique();

            entity.HasIndex(e => e.Guid, "tbl_addressregions_guid_uindex").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CountryId).HasColumnName("CountryID");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");

            entity.HasOne(d => d.Country).WithMany(p => p.AddressRegions)
                .HasForeignKey(d => d.CountryId)
                .HasConstraintName("tbl_addressregions_tbl_addresscountry_id_fk");
        });

        modelBuilder.Entity<Application>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_Application");

            entity.ToTable("Application", "Application");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasIdentityOptions(null, null, null, 2147483647L, null, null)
                .HasColumnName("ID");
            entity.Property(e => e.AppName).HasColumnType("character varying");
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.EnterpriseId).HasColumnName("EnterpriseID");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.ParentAppId).HasColumnName("ParentAppID");
            entity.Property(e => e.Version).HasPrecision(6, 3);

            entity.HasOne(d => d.Enterprise).WithMany(p => p.Applications)
                .HasForeignKey(d => d.EnterpriseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("tbl_applications_tbl_enterprises_id_fk");
        });

        modelBuilder.Entity<AuditField>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_AuditFields");

            entity.ToTable("AuditField", "Audit");

            entity.HasIndex(e => e.Guid, "tbl_auditfields_guid_uindex").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasIdentityOptions(null, null, null, 2147483647L, null, null)
                .HasColumnName("ID");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
        });

        modelBuilder.Entity<AuditHistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_AuditHistory");

            entity.ToTable("AuditHistory", "Audit");

            entity.HasIndex(e => e.Guid, "tbl_audithistory_guid_uindex").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasIdentityOptions(null, null, null, 2147483647L, null, null)
                .HasColumnName("ID");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.KeyValues).HasColumnType("character varying");
            entity.Property(e => e.NewValues).HasColumnType("character varying");
            entity.Property(e => e.OldValues).HasColumnType("character varying");
            entity.Property(e => e.QueryAction).HasColumnType("character varying");
            entity.Property(e => e.TableName).HasColumnType("character varying");
        });

        modelBuilder.Entity<AuthorizationLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityAuthorizationLogs");

            entity.ToTable("AuthorizationLog", "Audit");

            entity.HasIndex(e => e.IdentityCredentialsId, "IX_tbl_IdentityAuthorizationLogs_IdentityCredentialsID");

            entity.HasIndex(e => e.Guid, "tbl_authorizationlogs_guid_uindex").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasIdentityOptions(null, null, null, 2147483647L, null, null)
                .HasColumnName("ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.DeviceName).HasMaxLength(50);
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.Ipaddress)
                .HasMaxLength(18)
                .HasColumnName("IPAddress");
            entity.Property(e => e.LoginSource).HasMaxLength(50);
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.IdentityCredentials).WithMany(p => p.AuthorizationLogs)
                .HasForeignKey(d => d.IdentityCredentialsId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("tbl_userauthhistory_fk");
        });

        modelBuilder.Entity<CurrencyEntity>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_currency_pk");

            entity.ToTable("CurrencyEntity", "Finance");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("nextval('\"Finance\".\"tbl_Currency_id_seq\"'::regclass)")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CurrencyIsoCode3).HasMaxLength(4);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasMaxLength(256);
        });

        modelBuilder.Entity<Enterprise>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_tbl_Enterprises");

            entity.ToTable("Enterprise", "Application");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasIdentityOptions(null, null, null, 2147483647L, null, null)
                .HasColumnName("ID");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.Name).HasMaxLength(500);
        });

        modelBuilder.Entity<ExchangeRate>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_ExchangeRate_pkey");

            entity.ToTable("ExchangeRate", "Finance");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasIdentityOptions(null, null, null, 2147483647L, null, null)
                .HasColumnName("ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Fee).HasPrecision(18, 10);
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.SourceCurrencyEntityId).HasColumnName("SourceCurrencyEntityID");
            entity.Property(e => e.TargetCurrencyEntityId).HasColumnName("TargetCurrencyEntityID");
            entity.Property(e => e.Value).HasPrecision(18, 10);

            entity.HasOne(d => d.SourceCurrencyEntity).WithMany(p => p.ExchangeRateSourceCurrencyEntities)
                .HasForeignKey(d => d.SourceCurrencyEntityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("SourceCurrencyID");

            entity.HasOne(d => d.TargetCurrencyEntity).WithMany(p => p.ExchangeRateTargetCurrencyEntities)
                .HasForeignKey(d => d.TargetCurrencyEntityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("TargetCurrencyID");
        });

        modelBuilder.Entity<IdentityAddress>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityAddresses");

            entity.ToTable("IdentityAddress", "Identity");

            entity.HasIndex(e => e.AddressEntitiesId, "IX_tbl_IdentityAddresses_AddressEntitiesID");

            entity.HasIndex(e => e.IdentityInfoId, "IX_tbl_IdentityAddresses_UserInfoID");

            entity.HasIndex(e => e.Guid, "tbl_identityaddresses_guid_uindex").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasIdentityOptions(null, null, null, 2147483647L, null, null)
                .HasColumnName("ID");
            entity.Property(e => e.AddressEntitiesId).HasColumnName("AddressEntitiesID");
            entity.Property(e => e.Building).HasMaxLength(500);
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.IdentityInfoId).HasColumnName("IdentityInfoID");
            entity.Property(e => e.Street).HasMaxLength(500);
            entity.Property(e => e.Subdivision).HasMaxLength(500);
            entity.Property(e => e.UnitNumber).HasMaxLength(500);

            entity.HasOne(d => d.AddressEntities).WithMany(p => p.IdentityAddresses)
                .HasForeignKey(d => d.AddressEntitiesId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("AddressEntitiesID");

            entity.HasOne(d => d.BarangayNavigation).WithMany(p => p.IdentityAddresses)
                .HasForeignKey(d => d.Barangay)
                .HasConstraintName("tbl_identityaddresses__id_fk_brgy");

            entity.HasOne(d => d.CityNavigation).WithMany(p => p.IdentityAddresses)
                .HasForeignKey(d => d.City)
                .HasConstraintName("tbl_identityaddresses__id_fk_city");

            entity.HasOne(d => d.CountryNavigation).WithMany(p => p.IdentityAddresses)
                .HasForeignKey(d => d.Country)
                .HasConstraintName("tbl_identityaddresses_tbl_addresscountry__fk");

            entity.HasOne(d => d.IdentityInfo).WithMany(p => p.IdentityAddresses)
                .HasForeignKey(d => d.IdentityInfoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("UserInfoID");

            entity.HasOne(d => d.ProvinceNavigation).WithMany(p => p.IdentityAddresses)
                .HasForeignKey(d => d.Province)
                .HasConstraintName("tbl_identityaddresses__id_fk_province");

            entity.HasOne(d => d.RegionNavigation).WithMany(p => p.IdentityAddresses)
                .HasForeignKey(d => d.Region)
                .HasConstraintName("tbl_identityaddresses__id_fk");
        });

        modelBuilder.Entity<IdentityAddressEntity>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityAddressEntities");

            entity.ToTable("IdentityAddressEntity", "Identity");

            entity.HasIndex(e => e.Guid, "tbl_identityaddressentities_guid_uindex").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasIdentityOptions(null, null, null, 2147483647L, null, null)
                .HasColumnName("ID");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.Name).HasMaxLength(500);
        });

        modelBuilder.Entity<IdentityContact>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityContacts");

            entity.ToTable("IdentityContact", "Identity");

            entity.HasIndex(e => e.EntityId, "IX_tbl_IdentityContacts_UCEntitiesID");

            entity.HasIndex(e => e.UserCredentialId, "tbl_identitycontacts_usercredentialid_index");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasIdentityOptions(null, null, null, 2147483647L, null, null)
                .HasColumnName("ID");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.UserCredentialId).HasColumnName("UserCredentialID");
            entity.Property(e => e.Value).HasColumnType("character varying");

            entity.HasOne(d => d.Entity).WithMany(p => p.IdentityContacts)
                .HasForeignKey(d => d.EntityId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("UCEntitiesID");

            entity.HasOne(d => d.Group).WithMany(p => p.IdentityContacts)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("identitycontact_identitycontactgroup__fk");

            entity.HasOne(d => d.UserCredential).WithMany(p => p.IdentityContacts)
                .HasForeignKey(d => d.UserCredentialId)
                .HasConstraintName("tbl_identitycontacts___fk");
        });

        modelBuilder.Entity<IdentityContactEntity>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityContactEntities");

            entity.ToTable("IdentityContactEntity", "Identity");

            entity.HasIndex(e => e.Guid, "tbl_identitycontactentities_guid_uindex").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasIdentityOptions(null, null, null, 2147483647L, null, null)
                .HasColumnName("ID");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<IdentityContactGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("identitycontactgroup_pk");

            entity.ToTable("IdentityContactGroup", "Identity");

            entity.HasIndex(e => e.Guid, "identitycontactgroup_guid_uindex").IsUnique();

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<IdentityCredential>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityCredentials");

            entity.ToTable("IdentityCredential", "Identity");

            entity.HasIndex(e => e.IdentityInfoId, "IX_tbl_IdentityCredentials_IdentityInfoID");

            entity.HasIndex(e => e.Guid, "tbl_identitycredentials_cuid_uindex").IsUnique();

            entity.HasIndex(e => e.UserName, "tbl_identitycredentials_un").IsUnique();

            entity.Property(e => e.Id)
                .HasIdentityOptions(null, null, null, 2147483647L, null, null)
                .HasColumnName("ID");
            entity.Property(e => e.ApplicationId).HasColumnName("ApplicationID");
            entity.Property(e => e.Guid)
                .HasMaxLength(500)
                .HasDefaultValueSql("(uuid_generate_v4())::text");
            entity.Property(e => e.IdentityInfoId).HasColumnName("IdentityInfoID");
            entity.Property(e => e.Token).HasMaxLength(512);
            entity.Property(e => e.UserAlias).HasMaxLength(100);
            entity.Property(e => e.UserName).HasMaxLength(100);

            entity.HasOne(d => d.Application).WithMany(p => p.IdentityCredentials)
                .HasForeignKey(d => d.ApplicationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("tbl_identitycredentials___fk");

            entity.HasOne(d => d.IdentityInfo).WithMany(p => p.IdentityCredentials)
                .HasForeignKey(d => d.IdentityInfoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("tbl_identitycredentials_fk");
        });

        modelBuilder.Entity<IdentityFavorite>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_userfavorites_pk");

            entity.ToTable("IdentityFavorite", "Identity");

            entity.HasIndex(e => e.Id, "tbl_userfavorites_\"id\"_uindex").IsUnique();

            entity.HasIndex(e => e.Guid, "tbl_userfavorites_guid_uindex").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("nextval('\"Identity\".\"tbl_IdentityFavorites_ID_seq\"'::regclass)")
                .HasColumnName("ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Data).HasMaxLength(5000);
            entity.Property(e => e.FavoriteEntityId).HasColumnName("FavoriteEntityID");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");
            entity.Property(e => e.IsEnabled).HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.FavoriteEntity).WithMany(p => p.IdentityFavorites)
                .HasForeignKey(d => d.FavoriteEntityId)
                .HasConstraintName("tbl_userfavorites_tbl_favoriteentities_id_fk");

            entity.HasOne(d => d.IdentityCredential).WithMany(p => p.IdentityFavorites)
                .HasForeignKey(d => d.IdentityCredentialId)
                .HasConstraintName("tbl_userfavorites_tbl_identitycredentials_id_fk");
        });

        modelBuilder.Entity<IdentityInformation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityInfo");

            entity.ToTable("IdentityInformation", "Identity");

            entity.HasIndex(e => e.Guid, "tbl_userinfo_un").IsUnique();

            entity.Property(e => e.Id)
                .HasIdentityOptions(null, null, null, 2147483647L, null, null)
                .HasColumnName("ID");
            entity.Property(e => e.ApplicationId).HasColumnName("ApplicationID");
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.Guid)
                .HasMaxLength(500)
                .HasDefaultValueSql("(uuid_generate_v4())::text");
            entity.Property(e => e.IdentityDescription).HasMaxLength(100);
            entity.Property(e => e.IdentityName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.MiddleName).HasMaxLength(100);

            entity.HasOne(d => d.Application).WithMany(p => p.IdentityInformations)
                .HasForeignKey(d => d.ApplicationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("identityinformation_application_id_fk");
        });

        modelBuilder.Entity<IdentityRole>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityRoles");

            entity.ToTable("IdentityRole", "Identity");

            entity.HasIndex(e => e.RoleEntityId, "IX_tbl_IdentityRoles_RoleEntityID");

            entity.HasIndex(e => e.UserCredId, "IX_tbl_IdentityRoles_UserCredID");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasIdentityOptions(null, null, null, 2147483647L, null, null)
                .HasColumnName("ID");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.RoleEntityId).HasColumnName("RoleEntityID");
            entity.Property(e => e.UserCredId).HasColumnName("UserCredID");

            entity.HasOne(d => d.RoleEntity).WithMany(p => p.IdentityRoles)
                .HasForeignKey(d => d.RoleEntityId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("tbl_identityroles_fk_1");

            entity.HasOne(d => d.UserCred).WithMany(p => p.IdentityRoles)
                .HasForeignKey(d => d.UserCredId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("tbl_identityroles_fk");
        });

        modelBuilder.Entity<IdentityRoleEntity>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityRoleEntities");

            entity.ToTable("IdentityRoleEntity", "Identity");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasIdentityOptions(null, null, null, 2147483647L, null, null)
                .HasColumnName("ID");
            entity.Property(e => e.ApplicationId).HasDefaultValueSql("1");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.Name).HasMaxLength(100);

            entity.HasOne(d => d.Application).WithMany(p => p.IdentityRoleEntities)
                .HasForeignKey(d => d.ApplicationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("tbl_identityroleentities_tbl_applications_id_fk");

            entity.HasOne(d => d.Group).WithMany(p => p.IdentityRoleEntities)
                .HasForeignKey(d => d.GroupId)
                .HasConstraintName("identityroleentity_identityroleentitygroup_id_fk");
        });

        modelBuilder.Entity<IdentityRoleEntityGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("identityroleentitygroup_pk");

            entity.ToTable("IdentityRoleEntityGroup", "Identity");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<IdentityVerification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityVerifications");

            entity.ToTable("IdentityVerification", "Identity");

            entity.HasIndex(e => e.IdentityCredId, "IX_tbl_IdentityVerifications_IdentityCredID");

            entity.HasIndex(e => e.VerificationTypeId, "IX_tbl_IdentityVerifications_VerificationTypeID");

            entity.HasIndex(e => e.Guid, "tbl_identityverifications_guid_uindex").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasIdentityOptions(null, null, null, 2147483647L, null, null)
                .HasColumnName("ID");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.IdentityCredId).HasColumnName("IdentityCredID");
            entity.Property(e => e.StatusUpdatedOn).HasColumnType("time with time zone");
            entity.Property(e => e.Token).HasColumnType("character varying");
            entity.Property(e => e.VerificationTypeId).HasColumnName("VerificationTypeID");

            entity.HasOne(d => d.IdentityCred).WithMany(p => p.IdentityVerifications)
                .HasForeignKey(d => d.IdentityCredId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("tbl_UserVerifications_AuthID");

            entity.HasOne(d => d.VerificationType).WithMany(p => p.IdentityVerifications)
                .HasForeignKey(d => d.VerificationTypeId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("tbl_UserVerifications_VerificationTypeID");
        });

        modelBuilder.Entity<IdentityVerificationEntity>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_VerificationType");

            entity.ToTable("IdentityVerificationEntity", "Identity");

            entity.HasIndex(e => e.Guid, "tbl_identityverificationentities_guid_uindex").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasIdentityOptions(null, null, null, 2147483647L, null, null)
                .HasColumnName("ID");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<Log>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_ApplicationLogs");

            entity.ToTable("Log", "Audit");

            entity.HasIndex(e => e.ApplicationId, "IX_tbl_ApplicationLogs_AppID");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasIdentityOptions(null, null, null, 2147483647L, null, null)
                .HasColumnName("ID");
            entity.Property(e => e.ApplicationId).HasColumnName("ApplicationID");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.Initiator).HasColumnType("character varying");
            entity.Property(e => e.Message).HasColumnType("character varying");
            entity.Property(e => e.Name).HasColumnType("character varying");
            entity.Property(e => e.Seen).HasDefaultValueSql("false");

            entity.HasOne(d => d.Application).WithMany(p => p.Logs)
                .HasForeignKey(d => d.ApplicationId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("tbl_applogs_appid_fk");
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("message_pk");

            entity.ToTable("Message", "Messaging");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Text).HasColumnType("character varying");

            entity.HasOne(d => d.MessageThread).WithMany(p => p.Messages)
                .HasForeignKey(d => d.MessageThreadId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("message_messagethread_id_fk");

            entity.HasOne(d => d.MessageThreadMember).WithMany(p => p.Messages)
                .HasForeignKey(d => d.MessageThreadMemberId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("message_messagethreadmember_id_fk");
        });

        modelBuilder.Entity<MessageDelivery>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("messagedelivery_pk");

            entity.ToTable("MessageDelivery", "Messaging");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Entity).WithMany(p => p.MessageDeliveries)
                .HasForeignKey(d => d.EntityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("messagedelivery_messagedeliveryentity_id_fk");

            entity.HasOne(d => d.Message).WithMany(p => p.MessageDeliveries)
                .HasForeignKey(d => d.MessageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("messagedelivery_message_id_fk");

            entity.HasOne(d => d.MessageThreadMember).WithMany(p => p.MessageDeliveries)
                .HasForeignKey(d => d.MessageThreadMemberId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("messagedelivery_messagethreadmember_id_fk");
        });

        modelBuilder.Entity<MessageDeliveryEntity>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("messagedeliveryentity_pk");

            entity.ToTable("MessageDeliveryEntity", "Messaging");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<MessageDirect>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("messagedirect_pk");

            entity.ToTable("MessageDirect", "Messaging");

            entity.HasIndex(e => e.Guid, "messagedirect_guid_uindex").IsUnique();

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.Intent).HasColumnType("character varying");
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.Message).HasColumnType("character varying");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Recipient).HasColumnType("character varying");
            entity.Property(e => e.Sender).HasColumnType("character varying");
            entity.Property(e => e.Subject).HasColumnType("character varying");

            entity.HasOne(d => d.ParentMessage).WithMany(p => p.InverseParentMessage)
                .HasForeignKey(d => d.ParentMessageId)
                .HasConstraintName("messagedirect_messagedirect_id_fk");

            entity.HasOne(d => d.RecipientNavigation).WithMany(p => p.MessageDirectRecipientNavigations)
                .HasForeignKey(d => d.RecipientId)
                .HasConstraintName("messagedirect_identitycredential_2_id_fk");

            entity.HasOne(d => d.SenderNavigation).WithMany(p => p.MessageDirectSenderNavigations)
                .HasForeignKey(d => d.SenderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("messagedirect_identitycredential_id_fk");

            entity.HasOne(d => d.Type).WithMany(p => p.MessageDirects)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("messagedirect_messagetype_id_fk");
        });

        modelBuilder.Entity<MessageFile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("messagefiles_pk");

            entity.ToTable("MessageFiles", "Messaging");

            entity.HasIndex(e => e.Guid, "messagefiles_guid_uindex").IsUnique();

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Message).WithMany(p => p.MessageFiles)
                .HasForeignKey(d => d.MessageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("messagefiles_message_id_fk");

            entity.HasOne(d => d.Storage).WithMany(p => p.MessageFiles)
                .HasForeignKey(d => d.StorageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("messagefiles_storagefile_id_fk");
        });

        modelBuilder.Entity<MessageReaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("messagereaction_pk");

            entity.ToTable("MessageReaction", "Messaging");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Entity).WithMany(p => p.MessageReactions)
                .HasForeignKey(d => d.EntityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("messagereaction_messagereactionentity_id_fk");

            entity.HasOne(d => d.Message).WithMany(p => p.MessageReactions)
                .HasForeignKey(d => d.MessageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("messagereaction_message_id_fk");
        });

        modelBuilder.Entity<MessageReactionEntity>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("messagereactionentity_pk");

            entity.ToTable("MessageReactionEntity", "Messaging");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Emoji).HasColumnType("character varying");
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<MessageThread>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("messagethread_pk");

            entity.ToTable("MessageThread", "Messaging");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.Entity).WithMany(p => p.MessageThreads)
                .HasForeignKey(d => d.EntityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("messagethread_messagethreadentity_id_fk");
        });

        modelBuilder.Entity<MessageThreadEntity>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("messagethreadentity_pk");

            entity.ToTable("MessageThreadEntity", "Messaging");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.MessageType).WithMany(p => p.MessageThreadEntities)
                .HasForeignKey(d => d.MessageTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("messagethreadentity_messagetype_id_fk");
        });

        modelBuilder.Entity<MessageThreadMember>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("messagethreadmember_pk");

            entity.ToTable("MessageThreadMember", "Messaging");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Alias).HasColumnType("character varying");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.Emoji).HasColumnType("character varying");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Group).WithMany(p => p.MessageThreadMembers)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("messagethreadmember_messagethreadmembergroup_id_fk");

            entity.HasOne(d => d.IdentityCredential).WithMany(p => p.MessageThreadMembers)
                .HasForeignKey(d => d.IdentityCredentialId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("messagethreadmember_identitycredential_id_fk");

            entity.HasOne(d => d.MessageThread).WithMany(p => p.MessageThreadMembers)
                .HasForeignKey(d => d.MessageThreadId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("messagethreadmember_messagethread_id_fk");
        });

        modelBuilder.Entity<MessageThreadMemberGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("messagethreadmembergroup_pk");

            entity.ToTable("MessageThreadMemberGroup", "Messaging");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Alias).HasColumnType("character varying");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.Emoji).HasColumnType("character varying");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.MessageThread).WithMany(p => p.MessageThreadMemberGroups)
                .HasForeignKey(d => d.MessageThreadId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("messagethreadmembergroup_messagethread_id_fk");
        });

        modelBuilder.Entity<MessageThreadMemberRole>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("messagethreadmemberrole_pk");

            entity.ToTable("MessageThreadMemberRole", "Messaging");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.MessageThreadMember).WithMany(p => p.MessageThreadMemberRoles)
                .HasForeignKey(d => d.MessageThreadMemberId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("messagethreadmemberrole_messagethreadmember_id_fk");

            entity.HasOne(d => d.Role).WithMany(p => p.MessageThreadMemberRoles)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("messagethreadmemberrole_identityrole_id_fk");
        });

        modelBuilder.Entity<MessageType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("messagetype_pk");

            entity.ToTable("MessageType", "Messaging");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<RegistryConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_applicationconfiguration_pk");

            entity.ToTable("RegistryConfiguration", "Registry");

            entity.HasIndex(e => e.Guid, "tbl_configurations_guid_uindex").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("nextval('\"Registry\".\"tbl_Configuration_ID_seq\"'::regclass)")
                .HasColumnName("ID");
            entity.Property(e => e.ApplicationId).HasColumnName("ApplicationID");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.Key).HasColumnType("character varying");
            entity.Property(e => e.Unit).HasMaxLength(100);
            entity.Property(e => e.Value).HasColumnType("character varying");

            entity.HasOne(d => d.Application).WithMany(p => p.RegistryConfigurations)
                .HasForeignKey(d => d.ApplicationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("tbl_applicationconfiguration_tbl_application_id_fk");

            entity.HasOne(d => d.Group).WithMany(p => p.RegistryConfigurations)
                .HasForeignKey(d => d.GroupId)
                .HasConstraintName("tbl_configurations_tbl_configurationgroup_id_fk");
        });

        modelBuilder.Entity<RegistryConfigurationGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_configurationgroup_pk");

            entity.ToTable("RegistryConfigurationGroup", "Registry");

            entity.HasIndex(e => e.Guid, "tbl_configurationgroup_guid_uindex").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("nextval('\"Registry\".\"tbl_ConfigurationGroup_ID_seq\"'::regclass)")
                .HasColumnName("ID");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<RegistryFavoriteEntity>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_favoriteentities_pk");

            entity.ToTable("RegistryFavoriteEntity", "Registry");

            entity.HasIndex(e => e.Id, "tbl_favoriteentities_\"id\"_uindex").IsUnique();

            entity.HasIndex(e => e.Guid, "tbl_favoriteentities_guid_uindex").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("nextval('\"Registry\".\"tbl_FavoriteEntities_ID_seq\"'::regclass)")
                .HasColumnName("ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");
            entity.Property(e => e.IsEnabled).HasDefaultValueSql("true");
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<SessionDatum>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_SessionData");

            entity.ToTable("SessionData", "Identity");

            entity.HasIndex(e => e.SessionEntityId, "IX_tbl_SessionData_SessionEntityID");

            entity.HasIndex(e => e.UserCredentialId, "IX_tbl_SessionData_UserCredentialID");

            entity.HasIndex(e => e.Guid, "tbl_sessiondata_guid_uindex").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasIdentityOptions(null, null, null, 2147483647L, null, null)
                .HasColumnName("ID");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.SessionData).HasMaxLength(2000);
            entity.Property(e => e.SessionEntityId).HasColumnName("SessionEntityID");
            entity.Property(e => e.UserCredentialId).HasColumnName("UserCredentialID");

            entity.HasOne(d => d.SessionEntity).WithMany(p => p.SessionData)
                .HasForeignKey(d => d.SessionEntityId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("tbl_sessiondata_fk");

            entity.HasOne(d => d.UserCredential).WithMany(p => p.SessionData)
                .HasForeignKey(d => d.UserCredentialId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("tbl_sessiondata_fk_1");
        });

        modelBuilder.Entity<SessionEntity>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_SessionEntities");

            entity.ToTable("SessionEntity", "Identity");

            entity.HasIndex(e => e.Guid, "tbl_sessionentities_guid_uindex").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasIdentityOptions(null, null, null, 2147483647L, null, null)
                .HasColumnName("ID");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<StorageFile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("storagefile_pk");

            entity.ToTable("StorageFile", "Storage");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.ContentPath).HasColumnType("character varying");
            entity.Property(e => e.ContentType).HasColumnType("character varying");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.Hash).HasColumnType("character varying");
            entity.Property(e => e.IdentifierGuid).HasColumnType("character varying");
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.Entity).WithMany(p => p.StorageFiles)
                .HasForeignKey(d => d.EntityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("storagefile_storagefileentity_id_fk");

            entity.HasOne(d => d.StorageFileIdentifier).WithMany(p => p.StorageFiles)
                .HasForeignKey(d => d.StorageFileIdentifierId)
                .HasConstraintName("storagefile_storagefileidentifier_id_fk");
        });

        modelBuilder.Entity<StorageFileEntity>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("storagefileentity_pk");

            entity.ToTable("StorageFileEntity", "Storage");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<StorageFileIdentifier>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("storagefileidentifier_pk");

            entity.ToTable("StorageFileIdentifier", "Storage");

            entity.HasIndex(e => e.Guid, "storagefileidentifier_guid_uindex").IsUnique();

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.Group).WithMany(p => p.StorageFileIdentifiers)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("storagefileidentifier_storagefileidentifiergroup_id_fk");
        });

        modelBuilder.Entity<StorageFileIdentifierGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("scheduleentitygroup_pk");

            entity.ToTable("StorageFileIdentifierGroup", "Storage");

            entity.HasIndex(e => e.Guid, "storagefileidentifiergroup_guid_uindex").IsUnique();

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Guid)
                .HasDefaultValueSql("(uuid_generate_v4())::text")
                .HasColumnType("character varying");
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
