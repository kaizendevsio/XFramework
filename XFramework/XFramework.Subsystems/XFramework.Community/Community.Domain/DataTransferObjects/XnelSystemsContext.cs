using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Community.Domain.DataTransferObjects
{
    public partial class XnelSystemsContext : DbContext
    {
        public XnelSystemsContext()
        {
        }

        public XnelSystemsContext(DbContextOptions<XnelSystemsContext> options)
            : base(options)
        {
        }

        public virtual DbSet<AddressBarangay> AddressBarangays { get; set; } = null!;
        public virtual DbSet<AddressCity> AddressCities { get; set; } = null!;
        public virtual DbSet<AddressCountry> AddressCountries { get; set; } = null!;
        public virtual DbSet<AddressProvince> AddressProvinces { get; set; } = null!;
        public virtual DbSet<AddressRegion> AddressRegions { get; set; } = null!;
        public virtual DbSet<Application> Applications { get; set; } = null!;
        public virtual DbSet<AuditField> AuditFields { get; set; } = null!;
        public virtual DbSet<AuditHistory> AuditHistories { get; set; } = null!;
        public virtual DbSet<AuthorizationLog> AuthorizationLogs { get; set; } = null!;
        public virtual DbSet<CommunityConnection> CommunityConnections { get; set; } = null!;
        public virtual DbSet<CommunityConnectionEntity> CommunityConnectionEntities { get; set; } = null!;
        public virtual DbSet<CommunityContent> CommunityContents { get; set; } = null!;
        public virtual DbSet<CommunityContentEntity> CommunityContentEntities { get; set; } = null!;
        public virtual DbSet<CommunityContentFile> CommunityContentFiles { get; set; } = null!;
        public virtual DbSet<CommunityContentReaction> CommunityContentReactions { get; set; } = null!;
        public virtual DbSet<CommunityContentReactionEntity> CommunityContentReactionEntities { get; set; } = null!;
        public virtual DbSet<CommunityIdentity> CommunityIdentities { get; set; } = null!;
        public virtual DbSet<CommunityIdentityEntity> CommunityIdentityEntities { get; set; } = null!;
        public virtual DbSet<CurrencyEntity> CurrencyEntities { get; set; } = null!;
        public virtual DbSet<Enterprise> Enterprises { get; set; } = null!;
        public virtual DbSet<ExchangeRate> ExchangeRates { get; set; } = null!;
        public virtual DbSet<IdentityAddress> IdentityAddresses { get; set; } = null!;
        public virtual DbSet<IdentityAddressEntity> IdentityAddressEntities { get; set; } = null!;
        public virtual DbSet<IdentityContact> IdentityContacts { get; set; } = null!;
        public virtual DbSet<IdentityContactEntity> IdentityContactEntities { get; set; } = null!;
        public virtual DbSet<IdentityCredential> IdentityCredentials { get; set; } = null!;
        public virtual DbSet<IdentityFavorite> IdentityFavorites { get; set; } = null!;
        public virtual DbSet<IdentityInformation> IdentityInformations { get; set; } = null!;
        public virtual DbSet<IdentityRole> IdentityRoles { get; set; } = null!;
        public virtual DbSet<IdentityRoleEntity> IdentityRoleEntities { get; set; } = null!;
        public virtual DbSet<IdentityRoleEntityGroup> IdentityRoleEntityGroups { get; set; } = null!;
        public virtual DbSet<IdentityVerification> IdentityVerifications { get; set; } = null!;
        public virtual DbSet<IdentityVerificationEntity> IdentityVerificationEntities { get; set; } = null!;
        public virtual DbSet<Log> Logs { get; set; } = null!;
        public virtual DbSet<RegistryConfiguration> RegistryConfigurations { get; set; } = null!;
        public virtual DbSet<RegistryConfigurationGroup> RegistryConfigurationGroups { get; set; } = null!;
        public virtual DbSet<RegistryFavoriteEntity> RegistryFavoriteEntities { get; set; } = null!;
        public virtual DbSet<SessionDatum> SessionData { get; set; } = null!;
        public virtual DbSet<SessionEntity> SessionEntities { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see http://go.microsoft.com/fwlink/?LinkId=723263.
                optionsBuilder.UseNpgsql("Host=localhost;Database=XnelSystems;Username=dbAdmin;Password=4*5WD-K8%f*NqmPY");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("uuid-ossp");

            modelBuilder.Entity<AddressBarangay>(entity =>
            {
                entity.ToTable("AddressBarangay", "GeoLocation");

                entity.HasIndex(e => e.Code, "addresses_refbrgy_code_uindex")
                    .IsUnique();

                entity.HasIndex(e => e.Guid, "tbl_addressbarangay_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .ValueGeneratedNever()
                    .HasColumnName("id");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.HasOne(d => d.CityCodeNavigation)
                    .WithMany(p => p.AddressBarangays)
                    .HasPrincipalKey(p => p.Code)
                    .HasForeignKey(d => d.CityCode)
                    .HasConstraintName("tbl_addressbarangay_tbl_addresscity_code_fk");
            });

            modelBuilder.Entity<AddressCity>(entity =>
            {
                entity.ToTable("AddressCity", "GeoLocation");

                entity.HasIndex(e => e.Code, "tbl_addresscity_code_uindex")
                    .IsUnique();

                entity.HasIndex(e => e.Guid, "tbl_addresscity_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .ValueGeneratedNever()
                    .HasColumnName("id");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.HasOne(d => d.ProvCodeNavigation)
                    .WithMany(p => p.AddressCities)
                    .HasPrincipalKey(p => p.Code)
                    .HasForeignKey(d => d.ProvCode)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("tbl_addresscity_tbl_addressprovince_code_fk");
            });

            modelBuilder.Entity<AddressCountry>(entity =>
            {
                entity.ToTable("AddressCountry", "GeoLocation");

                entity.HasIndex(e => e.Guid, "tbl_addresscountry_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.CurrencyId).HasColumnName("CurrencyID");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsoCode2).HasMaxLength(2);

                entity.Property(e => e.IsoCode3).HasMaxLength(3);

                entity.Property(e => e.Language).HasMaxLength(50);

                entity.Property(e => e.Name).HasMaxLength(50);

                entity.Property(e => e.PhoneCountryCode).HasMaxLength(9);

                entity.HasOne(d => d.Currency)
                    .WithMany(p => p.AddressCountries)
                    .HasForeignKey(d => d.CurrencyId)
                    .HasConstraintName("CurrencyID");
            });

            modelBuilder.Entity<AddressProvince>(entity =>
            {
                entity.ToTable("AddressProvince", "GeoLocation");

                entity.HasIndex(e => e.Code, "tbl_addressprovince_code_uindex")
                    .IsUnique();

                entity.HasIndex(e => e.Guid, "tbl_addressprovince_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .ValueGeneratedNever()
                    .HasColumnName("id");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.HasOne(d => d.RegCodeNavigation)
                    .WithMany(p => p.AddressProvinces)
                    .HasPrincipalKey(p => p.Code)
                    .HasForeignKey(d => d.RegCode)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("tbl_addressprovince_tbl_addressregions_code_fk");
            });

            modelBuilder.Entity<AddressRegion>(entity =>
            {
                entity.ToTable("AddressRegion", "GeoLocation");

                entity.HasIndex(e => e.Code, "tbl_addressregions_code_uindex")
                    .IsUnique();

                entity.HasIndex(e => e.Guid, "tbl_addressregions_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .ValueGeneratedNever()
                    .HasColumnName("id");

                entity.Property(e => e.CountryId).HasColumnName("CountryID");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.HasOne(d => d.Country)
                    .WithMany(p => p.AddressRegions)
                    .HasForeignKey(d => d.CountryId)
                    .HasConstraintName("tbl_addressregions_tbl_addresscountry_id_fk");
            });

            modelBuilder.Entity<Application>(entity =>
            {
                entity.ToTable("Application", "Application");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.AppName).HasColumnType("character varying");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.EnterpriseId).HasColumnName("EnterpriseID");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.ParentAppId).HasColumnName("ParentAppID");

                entity.Property(e => e.Version).HasPrecision(6, 3);

                entity.HasOne(d => d.Enterprise)
                    .WithMany(p => p.Applications)
                    .HasForeignKey(d => d.EnterpriseId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("tbl_applications_tbl_enterprises_id_fk");
            });

            modelBuilder.Entity<AuditField>(entity =>
            {
                entity.ToTable("AuditField", "Audit");

                entity.HasIndex(e => e.Guid, "tbl_auditfields_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");
            });

            modelBuilder.Entity<AuditHistory>(entity =>
            {
                entity.ToTable("AuditHistory", "Audit");

                entity.HasIndex(e => e.Guid, "tbl_audithistory_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.KeyValues).HasColumnType("character varying");

                entity.Property(e => e.NewValues).HasColumnType("character varying");

                entity.Property(e => e.OldValues).HasColumnType("character varying");

                entity.Property(e => e.QueryAction).HasColumnType("character varying");

                entity.Property(e => e.TableName).HasColumnType("character varying");
            });

            modelBuilder.Entity<AuthorizationLog>(entity =>
            {
                entity.ToTable("AuthorizationLog", "Audit");

                entity.HasIndex(e => e.IdentityCredentialsId, "IX_tbl_IdentityAuthorizationLogs_IdentityCredentialsID");

                entity.HasIndex(e => e.Guid, "tbl_authorizationlogs_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.DeviceName).HasMaxLength(50);

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.Ipaddress)
                    .HasMaxLength(18)
                    .HasColumnName("IPAddress");

                entity.Property(e => e.LoginSource).HasMaxLength(50);

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.IdentityCredentials)
                    .WithMany(p => p.AuthorizationLogs)
                    .HasForeignKey(d => d.IdentityCredentialsId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("tbl_userauthhistory_fk");
            });

            modelBuilder.Entity<CommunityConnection>(entity =>
            {
                entity.ToTable("CommunityConnection", "Community");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .HasDefaultValueSql("nextval('\"Community\".\"SocialMediaConnection_ID_seq\"'::regclass)");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Entity)
                    .WithMany(p => p.CommunityConnections)
                    .HasForeignKey(d => d.EntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("metadata_metadataentity_id_fk");

                entity.HasOne(d => d.SourceSocialMediaIdentity)
                    .WithMany(p => p.CommunityConnectionSourceSocialMediaIdentities)
                    .HasForeignKey(d => d.SourceSocialMediaIdentityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("socialmedia_sourcesocialmediaidentityid_id_fk");

                entity.HasOne(d => d.TargetSocialMediaIdentity)
                    .WithMany(p => p.CommunityConnectionTargetSocialMediaIdentities)
                    .HasForeignKey(d => d.TargetSocialMediaIdentityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("socialmedia_targetsocialmediaidentityid_id_fk");
            });

            modelBuilder.Entity<CommunityConnectionEntity>(entity =>
            {
                entity.ToTable("CommunityConnectionEntity", "Community");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .HasDefaultValueSql("nextval('\"Community\".\"SocialMediaConnectionEntity_ID_seq\"'::regclass)");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<CommunityContent>(entity =>
            {
                entity.ToTable("CommunityContent", "Community");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .HasDefaultValueSql("nextval('\"Community\".\"SocialMediaContent_ID_seq\"'::regclass)");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Text).HasColumnType("character varying");

                entity.Property(e => e.Title).HasColumnType("character varying");

                entity.HasOne(d => d.CommunityGroup)
                    .WithMany(p => p.CommunityContentCommunityGroups)
                    .HasForeignKey(d => d.CommunityGroupId)
                    .HasConstraintName("communitycontent_communityidentity_id_fk");

                entity.HasOne(d => d.Entity)
                    .WithMany(p => p.CommunityContents)
                    .HasForeignKey(d => d.EntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("socialmediacontent_socialmediacontententity_id_fk");

                entity.HasOne(d => d.ParentContent)
                    .WithMany(p => p.InverseParentContent)
                    .HasForeignKey(d => d.ParentContentId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("socialmediacontent_socialmediacontent_id_fk");

                entity.HasOne(d => d.SocialMediaIdentity)
                    .WithMany(p => p.CommunityContentSocialMediaIdentities)
                    .HasForeignKey(d => d.SocialMediaIdentityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("socialmediacontent_socialmediaidentity_id_fk");
            });

            modelBuilder.Entity<CommunityContentEntity>(entity =>
            {
                entity.ToTable("CommunityContentEntity", "Community");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .HasDefaultValueSql("nextval('\"Community\".\"SocialMediaContentEntity_ID_seq\"'::regclass)");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<CommunityContentFile>(entity =>
            {
                entity.ToTable("CommunityContentFiles", "Community");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .HasDefaultValueSql("nextval('\"Community\".\"SocialMediaContentFiles_ID_seq\"'::regclass)");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Content)
                    .WithMany(p => p.CommunityContentFiles)
                    .HasForeignKey(d => d.ContentId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("socialmediacontentfiles_socialmediacontent_id_fk");
            });

            modelBuilder.Entity<CommunityContentReaction>(entity =>
            {
                entity.ToTable("CommunityContentReaction", "Community");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .HasDefaultValueSql("nextval('\"Community\".\"SocialMediaContentReaction_ID_seq\"'::regclass)");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Content)
                    .WithMany(p => p.CommunityContentReactions)
                    .HasForeignKey(d => d.ContentId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("socialmediacontentreaction_socialmediacontent_id_fk");

                entity.HasOne(d => d.Entity)
                    .WithMany(p => p.CommunityContentReactions)
                    .HasForeignKey(d => d.EntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("socialmediacontentreaction_contentreactionentity_id_fk");

                entity.HasOne(d => d.SocialMediaIdentity)
                    .WithMany(p => p.CommunityContentReactions)
                    .HasForeignKey(d => d.SocialMediaIdentityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("socialmediacontentreaction_socialmediaidentity_id_fk");
            });

            modelBuilder.Entity<CommunityContentReactionEntity>(entity =>
            {
                entity.ToTable("CommunityContentReactionEntity", "Community");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .HasDefaultValueSql("nextval('\"Community\".\"SocialMediaContentReactionEntity_ID_seq\"'::regclass)");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Emoji).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<CommunityIdentity>(entity =>
            {
                entity.ToTable("CommunityIdentity", "Community");

                entity.HasIndex(e => e.Id, "socialidentity_\"id\"_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .HasDefaultValueSql("nextval('\"Community\".\"SocialIdentity_ID_seq\"'::regclass)");

                entity.Property(e => e.Alias).HasColumnType("character varying");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.HandleName).HasColumnType("character varying");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.LastActive).HasDefaultValueSql("now()");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Tagline).HasColumnType("character varying");

                entity.HasOne(d => d.Entity)
                    .WithMany(p => p.CommunityIdentities)
                    .HasForeignKey(d => d.EntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("communityidentity_communityidentityentity_id_fk");

                entity.HasOne(d => d.IdentityCredential)
                    .WithMany(p => p.CommunityIdentities)
                    .HasForeignKey(d => d.IdentityCredentialId)
                    .HasConstraintName("socialidentity_identitycredential_id_fk");
            });

            modelBuilder.Entity<CommunityIdentityEntity>(entity =>
            {
                entity.ToTable("CommunityIdentityEntity", "Community");

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<CurrencyEntity>(entity =>
            {
                entity.ToTable("CurrencyEntity", "Finance");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasDefaultValueSql("nextval('\"Finance\".\"tbl_Currency_id_seq\"'::regclass)");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.CurrencyIsoCode3).HasMaxLength(4);

                entity.Property(e => e.Description).HasMaxLength(500);

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasMaxLength(256);
            });

            modelBuilder.Entity<Enterprise>(entity =>
            {
                entity.ToTable("Enterprise", "Application");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.Name).HasMaxLength(500);
            });

            modelBuilder.Entity<ExchangeRate>(entity =>
            {
                entity.ToTable("ExchangeRate", "Finance");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Fee).HasPrecision(18, 10);

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.SourceCurrencyEntityId).HasColumnName("SourceCurrencyEntityID");

                entity.Property(e => e.TargetCurrencyEntityId).HasColumnName("TargetCurrencyEntityID");

                entity.Property(e => e.Value).HasPrecision(18, 10);

                entity.HasOne(d => d.SourceCurrencyEntity)
                    .WithMany(p => p.ExchangeRateSourceCurrencyEntities)
                    .HasForeignKey(d => d.SourceCurrencyEntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("SourceCurrencyID");

                entity.HasOne(d => d.TargetCurrencyEntity)
                    .WithMany(p => p.ExchangeRateTargetCurrencyEntities)
                    .HasForeignKey(d => d.TargetCurrencyEntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("TargetCurrencyID");
            });

            modelBuilder.Entity<IdentityAddress>(entity =>
            {
                entity.ToTable("IdentityAddress", "Identity");

                entity.HasIndex(e => e.AddressEntitiesId, "IX_tbl_IdentityAddresses_AddressEntitiesID");

                entity.HasIndex(e => e.IdentityInfoId, "IX_tbl_IdentityAddresses_UserInfoID");

                entity.HasIndex(e => e.Guid, "tbl_identityaddresses_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.AddressEntitiesId).HasColumnName("AddressEntitiesID");

                entity.Property(e => e.Building).HasMaxLength(500);

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IdentityInfoId).HasColumnName("IdentityInfoID");

                entity.Property(e => e.Street).HasMaxLength(500);

                entity.Property(e => e.Subdivision).HasMaxLength(500);

                entity.Property(e => e.UnitNumber).HasMaxLength(500);

                entity.HasOne(d => d.AddressEntities)
                    .WithMany(p => p.IdentityAddresses)
                    .HasForeignKey(d => d.AddressEntitiesId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("AddressEntitiesID");

                entity.HasOne(d => d.BarangayNavigation)
                    .WithMany(p => p.IdentityAddresses)
                    .HasForeignKey(d => d.Barangay)
                    .HasConstraintName("tbl_identityaddresses__id_fk_brgy");

                entity.HasOne(d => d.CityNavigation)
                    .WithMany(p => p.IdentityAddresses)
                    .HasForeignKey(d => d.City)
                    .HasConstraintName("tbl_identityaddresses__id_fk_city");

                entity.HasOne(d => d.CountryNavigation)
                    .WithMany(p => p.IdentityAddresses)
                    .HasForeignKey(d => d.Country)
                    .HasConstraintName("tbl_identityaddresses_tbl_addresscountry__fk");

                entity.HasOne(d => d.IdentityInfo)
                    .WithMany(p => p.IdentityAddresses)
                    .HasForeignKey(d => d.IdentityInfoId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("UserInfoID");

                entity.HasOne(d => d.ProvinceNavigation)
                    .WithMany(p => p.IdentityAddresses)
                    .HasForeignKey(d => d.Province)
                    .HasConstraintName("tbl_identityaddresses__id_fk_province");

                entity.HasOne(d => d.RegionNavigation)
                    .WithMany(p => p.IdentityAddresses)
                    .HasForeignKey(d => d.Region)
                    .HasConstraintName("tbl_identityaddresses__id_fk");
            });

            modelBuilder.Entity<IdentityAddressEntity>(entity =>
            {
                entity.ToTable("IdentityAddressEntity", "Identity");

                entity.HasIndex(e => e.Guid, "tbl_identityaddressentities_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.Name).HasMaxLength(500);
            });

            modelBuilder.Entity<IdentityContact>(entity =>
            {
                entity.ToTable("IdentityContact", "Identity");

                entity.HasIndex(e => e.UcentitiesId, "IX_tbl_IdentityContacts_UCEntitiesID");

                entity.HasIndex(e => e.UserCredentialId, "tbl_identitycontacts_usercredentialid_index");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.UcentitiesId).HasColumnName("UCEntitiesID");

                entity.Property(e => e.UserCredentialId).HasColumnName("UserCredentialID");

                entity.Property(e => e.Value).HasColumnType("character varying");

                entity.HasOne(d => d.Ucentities)
                    .WithMany(p => p.IdentityContacts)
                    .HasForeignKey(d => d.UcentitiesId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("UCEntitiesID");

                entity.HasOne(d => d.UserCredential)
                    .WithMany(p => p.IdentityContacts)
                    .HasForeignKey(d => d.UserCredentialId)
                    .HasConstraintName("tbl_identitycontacts___fk");
            });

            modelBuilder.Entity<IdentityContactEntity>(entity =>
            {
                entity.ToTable("IdentityContactEntity", "Identity");

                entity.HasIndex(e => e.Guid, "tbl_identitycontactentities_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<IdentityCredential>(entity =>
            {
                entity.ToTable("IdentityCredential", "Identity");

                entity.HasIndex(e => e.IdentityInfoId, "IX_tbl_IdentityCredentials_IdentityInfoID");

                entity.HasIndex(e => e.Guid, "tbl_identitycredentials_cuid_uindex")
                    .IsUnique();

                entity.HasIndex(e => e.UserName, "tbl_identitycredentials_un")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.ApplicationId).HasColumnName("ApplicationID");

                entity.Property(e => e.Guid)
                    .HasMaxLength(500)
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IdentityInfoId).HasColumnName("IdentityInfoID");

                entity.Property(e => e.Token).HasMaxLength(512);

                entity.Property(e => e.UserAlias).HasMaxLength(100);

                entity.Property(e => e.UserName).HasMaxLength(100);

                entity.HasOne(d => d.Application)
                    .WithMany(p => p.IdentityCredentials)
                    .HasForeignKey(d => d.ApplicationId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("tbl_identitycredentials___fk");

                entity.HasOne(d => d.IdentityInfo)
                    .WithMany(p => p.IdentityCredentials)
                    .HasForeignKey(d => d.IdentityInfoId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("tbl_identitycredentials_fk");
            });

            modelBuilder.Entity<IdentityFavorite>(entity =>
            {
                entity.ToTable("IdentityFavorite", "Identity");

                entity.HasIndex(e => e.Id, "tbl_userfavorites_\"id\"_uindex")
                    .IsUnique();

                entity.HasIndex(e => e.Guid, "tbl_userfavorites_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .HasDefaultValueSql("nextval('\"Identity\".\"tbl_IdentityFavorites_ID_seq\"'::regclass)");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Data).HasMaxLength(5000);

                entity.Property(e => e.FavoriteEntityId).HasColumnName("FavoriteEntityID");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");

                entity.Property(e => e.IsEnabled).HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.FavoriteEntity)
                    .WithMany(p => p.IdentityFavorites)
                    .HasForeignKey(d => d.FavoriteEntityId)
                    .HasConstraintName("tbl_userfavorites_tbl_favoriteentities_id_fk");

                entity.HasOne(d => d.IdentityCredential)
                    .WithMany(p => p.IdentityFavorites)
                    .HasForeignKey(d => d.IdentityCredentialId)
                    .HasConstraintName("tbl_userfavorites_tbl_identitycredentials_id_fk");
            });

            modelBuilder.Entity<IdentityInformation>(entity =>
            {
                entity.ToTable("IdentityInformation", "Identity");

                entity.HasIndex(e => e.Guid, "tbl_userinfo_un")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.FirstName).HasMaxLength(100);

                entity.Property(e => e.Guid)
                    .HasMaxLength(500)
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IdentityDescription).HasMaxLength(100);

                entity.Property(e => e.IdentityName).HasMaxLength(100);

                entity.Property(e => e.LastName).HasMaxLength(100);

                entity.Property(e => e.MiddleName).HasMaxLength(100);
            });

            modelBuilder.Entity<IdentityRole>(entity =>
            {
                entity.ToTable("IdentityRole", "Identity");

                entity.HasIndex(e => e.RoleEntityId, "IX_tbl_IdentityRoles_RoleEntityID");

                entity.HasIndex(e => e.UserCredId, "IX_tbl_IdentityRoles_UserCredID");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.RoleEntityId).HasColumnName("RoleEntityID");

                entity.Property(e => e.UserCredId).HasColumnName("UserCredID");

                entity.HasOne(d => d.RoleEntity)
                    .WithMany(p => p.IdentityRoles)
                    .HasForeignKey(d => d.RoleEntityId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("tbl_identityroles_fk_1");

                entity.HasOne(d => d.UserCred)
                    .WithMany(p => p.IdentityRoles)
                    .HasForeignKey(d => d.UserCredId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("tbl_identityroles_fk");
            });

            modelBuilder.Entity<IdentityRoleEntity>(entity =>
            {
                entity.ToTable("IdentityRoleEntity", "Identity");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.ApplicationId).HasDefaultValueSql("1");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.Name).HasMaxLength(100);

                entity.HasOne(d => d.Application)
                    .WithMany(p => p.IdentityRoleEntities)
                    .HasForeignKey(d => d.ApplicationId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("tbl_identityroleentities_tbl_applications_id_fk");

                entity.HasOne(d => d.Group)
                    .WithMany(p => p.IdentityRoleEntities)
                    .HasForeignKey(d => d.GroupId)
                    .HasConstraintName("identityroleentity_identityroleentitygroup_id_fk");
            });

            modelBuilder.Entity<IdentityRoleEntityGroup>(entity =>
            {
                entity.ToTable("IdentityRoleEntityGroup", "Identity");

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<IdentityVerification>(entity =>
            {
                entity.ToTable("IdentityVerification", "Identity");

                entity.HasIndex(e => e.IdentityCredId, "IX_tbl_IdentityVerifications_IdentityCredID");

                entity.HasIndex(e => e.VerificationTypeId, "IX_tbl_IdentityVerifications_VerificationTypeID");

                entity.HasIndex(e => e.Guid, "tbl_identityverifications_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IdentityCredId).HasColumnName("IdentityCredID");

                entity.Property(e => e.StatusUpdatedOn).HasColumnType("time with time zone");

                entity.Property(e => e.Token).HasColumnType("character varying");

                entity.Property(e => e.VerificationTypeId).HasColumnName("VerificationTypeID");

                entity.HasOne(d => d.IdentityCred)
                    .WithMany(p => p.IdentityVerifications)
                    .HasForeignKey(d => d.IdentityCredId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("tbl_UserVerifications_AuthID");

                entity.HasOne(d => d.VerificationType)
                    .WithMany(p => p.IdentityVerifications)
                    .HasForeignKey(d => d.VerificationTypeId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("tbl_UserVerifications_VerificationTypeID");
            });

            modelBuilder.Entity<IdentityVerificationEntity>(entity =>
            {
                entity.ToTable("IdentityVerificationEntity", "Identity");

                entity.HasIndex(e => e.Guid, "tbl_identityverificationentities_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.Name).HasMaxLength(100);
            });

            modelBuilder.Entity<Log>(entity =>
            {
                entity.ToTable("Log", "Audit");

                entity.HasIndex(e => e.ApplicationId, "IX_tbl_ApplicationLogs_AppID");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.ApplicationId).HasColumnName("ApplicationID");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.Initiator).HasColumnType("character varying");

                entity.Property(e => e.Message).HasColumnType("character varying");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.Property(e => e.Seen).HasDefaultValueSql("false");

                entity.HasOne(d => d.Application)
                    .WithMany(p => p.Logs)
                    .HasForeignKey(d => d.ApplicationId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("tbl_applogs_appid_fk");
            });

            modelBuilder.Entity<RegistryConfiguration>(entity =>
            {
                entity.ToTable("RegistryConfiguration", "Registry");

                entity.HasIndex(e => e.Guid, "tbl_configurations_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .HasDefaultValueSql("nextval('\"Registry\".\"tbl_Configuration_ID_seq\"'::regclass)");

                entity.Property(e => e.ApplicationId).HasColumnName("ApplicationID");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.Key).HasColumnType("character varying");

                entity.Property(e => e.Unit).HasMaxLength(100);

                entity.Property(e => e.Value).HasColumnType("character varying");

                entity.HasOne(d => d.Application)
                    .WithMany(p => p.RegistryConfigurations)
                    .HasForeignKey(d => d.ApplicationId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("tbl_applicationconfiguration_tbl_application_id_fk");

                entity.HasOne(d => d.Group)
                    .WithMany(p => p.RegistryConfigurations)
                    .HasForeignKey(d => d.GroupId)
                    .HasConstraintName("tbl_configurations_tbl_configurationgroup_id_fk");
            });

            modelBuilder.Entity<RegistryConfigurationGroup>(entity =>
            {
                entity.ToTable("RegistryConfigurationGroup", "Registry");

                entity.HasIndex(e => e.Guid, "tbl_configurationgroup_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .HasDefaultValueSql("nextval('\"Registry\".\"tbl_ConfigurationGroup_ID_seq\"'::regclass)");

                entity.Property(e => e.Description).HasMaxLength(1000);

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.Name).HasMaxLength(100);
            });

            modelBuilder.Entity<RegistryFavoriteEntity>(entity =>
            {
                entity.ToTable("RegistryFavoriteEntity", "Registry");

                entity.HasIndex(e => e.Id, "tbl_favoriteentities_\"id\"_uindex")
                    .IsUnique();

                entity.HasIndex(e => e.Guid, "tbl_favoriteentities_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .HasDefaultValueSql("nextval('\"Registry\".\"tbl_FavoriteEntities_ID_seq\"'::regclass)");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.Description).HasMaxLength(500);

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");

                entity.Property(e => e.IsEnabled).HasDefaultValueSql("true");

                entity.Property(e => e.Name).HasMaxLength(100);
            });

            modelBuilder.Entity<SessionDatum>(entity =>
            {
                entity.ToTable("SessionData", "Identity");

                entity.HasIndex(e => e.SessionEntityId, "IX_tbl_SessionData_SessionEntityID");

                entity.HasIndex(e => e.UserCredentialId, "IX_tbl_SessionData_UserCredentialID");

                entity.HasIndex(e => e.Guid, "tbl_sessiondata_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.SessionData).HasMaxLength(2000);

                entity.Property(e => e.SessionEntityId).HasColumnName("SessionEntityID");

                entity.Property(e => e.UserCredentialId).HasColumnName("UserCredentialID");

                entity.HasOne(d => d.SessionEntity)
                    .WithMany(p => p.SessionData)
                    .HasForeignKey(d => d.SessionEntityId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("tbl_sessiondata_fk");

                entity.HasOne(d => d.UserCredential)
                    .WithMany(p => p.SessionData)
                    .HasForeignKey(d => d.UserCredentialId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("tbl_sessiondata_fk_1");
            });

            modelBuilder.Entity<SessionEntity>(entity =>
            {
                entity.ToTable("SessionEntity", "Identity");

                entity.HasIndex(e => e.Guid, "tbl_sessionentities_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
