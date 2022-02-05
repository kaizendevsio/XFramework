using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Wallets.Domain.DataTransferObjects
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
        public virtual DbSet<TblConfigurationGroup> TblConfigurationGroups { get; set; }
        public virtual DbSet<TblCurrencyEntity> TblCurrencyEntities { get; set; }
        public virtual DbSet<TblEnterprise> TblEnterprises { get; set; }
        public virtual DbSet<TblExchangeRate> TblExchangeRates { get; set; }
        public virtual DbSet<TblFavoriteEntity> TblFavoriteEntities { get; set; }
        public virtual DbSet<TblIdentityAddress> TblIdentityAddresses { get; set; }
        public virtual DbSet<TblIdentityAddressEntity> TblIdentityAddressEntities { get; set; }
        public virtual DbSet<TblIdentityContact> TblIdentityContacts { get; set; }
        public virtual DbSet<TblIdentityContactEntity> TblIdentityContactEntities { get; set; }
        public virtual DbSet<TblIdentityCredential> TblIdentityCredentials { get; set; }
        public virtual DbSet<TblIdentityInformation> TblIdentityInformations { get; set; }
        public virtual DbSet<TblIdentityRole> TblIdentityRoles { get; set; }
        public virtual DbSet<TblIdentityRoleEntity> TblIdentityRoleEntities { get; set; }
        public virtual DbSet<TblIdentityVerification> TblIdentityVerifications { get; set; }
        public virtual DbSet<TblIdentityVerificationEntity> TblIdentityVerificationEntities { get; set; }
        public virtual DbSet<TblLog> TblLogs { get; set; }
        public virtual DbSet<TblSessionDatum> TblSessionData { get; set; }
        public virtual DbSet<TblSessionEntity> TblSessionEntities { get; set; }
        public virtual DbSet<TblUserBillsPaymentTransaction> TblUserBillsPaymentTransactions { get; set; }
        public virtual DbSet<TblUserBinaryList> TblUserBinaryLists { get; set; }
        public virtual DbSet<TblUserBinaryListMultiplex> TblUserBinaryListMultiplices { get; set; }
        public virtual DbSet<TblUserBusinessPackage> TblUserBusinessPackages { get; set; }
        public virtual DbSet<TblUserBusinessPackageUpgradeTransaction> TblUserBusinessPackageUpgradeTransactions { get; set; }
        public virtual DbSet<TblUserBusinessType> TblUserBusinessTypes { get; set; }
        public virtual DbSet<TblUserCommissionDeductionRequest> TblUserCommissionDeductionRequests { get; set; }
        public virtual DbSet<TblUserDepositRequest> TblUserDepositRequests { get; set; }
        public virtual DbSet<TblUserEloadTransaction> TblUserEloadTransactions { get; set; }
        public virtual DbSet<TblUserFavorite> TblUserFavorites { get; set; }
        public virtual DbSet<TblUserIncomePartition> TblUserIncomePartitions { get; set; }
        public virtual DbSet<TblUserIncomeTransaction> TblUserIncomeTransactions { get; set; }
        public virtual DbSet<TblUserMap> TblUserMaps { get; set; }
        public virtual DbSet<TblUserWallet> TblUserWallets { get; set; }
        public virtual DbSet<TblUserWalletAddress> TblUserWalletAddresses { get; set; }
        public virtual DbSet<TblUserWalletTransaction> TblUserWalletTransactions { get; set; }
        public virtual DbSet<TblUserWithdrawalRequest> TblUserWithdrawalRequests { get; set; }
        public virtual DbSet<TblWalletEntity> TblWalletEntities { get; set; }

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

            modelBuilder.Entity<TblAddressBarangay>(entity =>
            {
                entity.ToTable("tbl_AddressBarangay", "GeoLocation");

                entity.HasIndex(e => e.Code, "addresses_refbrgy_code_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .ValueGeneratedNever()
                    .HasColumnName("id");

                entity.HasOne(d => d.CityCodeNavigation)
                    .WithMany(p => p.TblAddressBarangays)
                    .HasPrincipalKey(p => p.Code)
                    .HasForeignKey(d => d.CityCode)
                    .HasConstraintName("tbl_addressbarangay_tbl_addresscity_code_fk");
            });

            modelBuilder.Entity<TblAddressCity>(entity =>
            {
                entity.ToTable("tbl_AddressCity", "GeoLocation");

                entity.HasIndex(e => e.Code, "tbl_addresscity_code_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .ValueGeneratedNever()
                    .HasColumnName("id");

                entity.HasOne(d => d.ProvCodeNavigation)
                    .WithMany(p => p.TblAddressCities)
                    .HasPrincipalKey(p => p.Code)
                    .HasForeignKey(d => d.ProvCode)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("tbl_addresscity_tbl_addressprovince_code_fk");
            });

            modelBuilder.Entity<TblAddressCountry>(entity =>
            {
                entity.ToTable("tbl_AddressCountry", "GeoLocation");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.CurrencyId).HasColumnName("CurrencyID");

                entity.Property(e => e.IsoCode2).HasMaxLength(2);

                entity.Property(e => e.IsoCode3).HasMaxLength(3);

                entity.Property(e => e.Language).HasMaxLength(50);

                entity.Property(e => e.Name).HasMaxLength(50);

                entity.Property(e => e.PhoneCountryCode).HasMaxLength(9);

                entity.HasOne(d => d.Currency)
                    .WithMany(p => p.TblAddressCountries)
                    .HasForeignKey(d => d.CurrencyId)
                    .HasConstraintName("CurrencyID");
            });

            modelBuilder.Entity<TblAddressProvince>(entity =>
            {
                entity.ToTable("tbl_AddressProvince", "GeoLocation");

                entity.HasIndex(e => e.Code, "tbl_addressprovince_code_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .ValueGeneratedNever()
                    .HasColumnName("id");

                entity.HasOne(d => d.RegCodeNavigation)
                    .WithMany(p => p.TblAddressProvinces)
                    .HasPrincipalKey(p => p.Code)
                    .HasForeignKey(d => d.RegCode)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("tbl_addressprovince_tbl_addressregions_code_fk");
            });

            modelBuilder.Entity<TblAddressRegion>(entity =>
            {
                entity.ToTable("tbl_AddressRegions", "GeoLocation");

                entity.HasIndex(e => e.Code, "tbl_addressregions_code_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .ValueGeneratedNever()
                    .HasColumnName("id");
            });

            modelBuilder.Entity<TblApplication>(entity =>
            {
                entity.ToTable("tbl_Applications", "Application");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.AppName).HasColumnType("character varying");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.EnterpriseId).HasColumnName("EnterpriseID");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.ParentAppId).HasColumnName("ParentAppID");

                entity.Property(e => e.Version).HasPrecision(6, 3);

                entity.HasOne(d => d.Enterprise)
                    .WithMany(p => p.TblApplications)
                    .HasForeignKey(d => d.EnterpriseId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("tbl_applications_tbl_enterprises_id_fk");
            });

            modelBuilder.Entity<TblAuditField>(entity =>
            {
                entity.ToTable("tbl_AuditFields", "Audit");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);
            });

            modelBuilder.Entity<TblAuditHistory>(entity =>
            {
                entity.ToTable("tbl_AuditHistory", "Audit");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.KeyValues).HasColumnType("character varying");

                entity.Property(e => e.NewValues).HasColumnType("character varying");

                entity.Property(e => e.OldValues).HasColumnType("character varying");

                entity.Property(e => e.QueryAction).HasColumnType("character varying");

                entity.Property(e => e.TableName).HasColumnType("character varying");
            });

            modelBuilder.Entity<TblAuthorizationLog>(entity =>
            {
                entity.ToTable("tbl_AuthorizationLogs", "Audit");

                entity.HasIndex(e => e.IdentityCredentialsId, "IX_tbl_IdentityAuthorizationLogs_IdentityCredentialsID");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.DeviceName).HasMaxLength(50);

                entity.Property(e => e.IdentityCredentialsId).HasColumnName("IdentityCredentialsID");

                entity.Property(e => e.Ipaddress)
                    .HasMaxLength(18)
                    .HasColumnName("IPAddress");

                entity.Property(e => e.LoginSource).HasMaxLength(50);

                entity.HasOne(d => d.IdentityCredentials)
                    .WithMany(p => p.TblAuthorizationLogs)
                    .HasForeignKey(d => d.IdentityCredentialsId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("tbl_userauthhistory_fk");
            });

            modelBuilder.Entity<TblConfiguration>(entity =>
            {
                entity.ToTable("tbl_Configurations", "Registry");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .HasDefaultValueSql("nextval('\"Registry\".\"tbl_Configuration_ID_seq\"'::regclass)");

                entity.Property(e => e.ApplicationId).HasColumnName("ApplicationID");

                entity.Property(e => e.Key)
                    .IsRequired()
                    .HasColumnType("character varying");

                entity.Property(e => e.Unit).HasMaxLength(100);

                entity.Property(e => e.Value).HasColumnType("character varying");

                entity.HasOne(d => d.Application)
                    .WithMany(p => p.TblConfigurations)
                    .HasForeignKey(d => d.ApplicationId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("tbl_applicationconfiguration_tbl_application_id_fk");

                entity.HasOne(d => d.Group)
                    .WithMany(p => p.TblConfigurations)
                    .HasForeignKey(d => d.GroupId)
                    .HasConstraintName("tbl_configurations_tbl_configurationgroup_id_fk");
            });

            modelBuilder.Entity<TblConfigurationGroup>(entity =>
            {
                entity.ToTable("tbl_ConfigurationGroup", "Registry");

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.Description).HasMaxLength(1000);

                entity.Property(e => e.Name).HasMaxLength(100);
            });

            modelBuilder.Entity<TblCurrencyEntity>(entity =>
            {
                entity.ToTable("tbl_CurrencyEntities", "Finance");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasDefaultValueSql("nextval('\"Finance\".\"tbl_Currency_id_seq\"'::regclass)");

                entity.Property(e => e.CurrencyIsoCode3).HasMaxLength(4);

                entity.Property(e => e.Description).HasMaxLength(500);

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.Name).HasMaxLength(256);
            });

            modelBuilder.Entity<TblEnterprise>(entity =>
            {
                entity.ToTable("tbl_Enterprises", "Application");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.Name).HasMaxLength(500);
            });

            modelBuilder.Entity<TblExchangeRate>(entity =>
            {
                entity.ToTable("tbl_ExchangeRate", "Finance");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.Fee).HasPrecision(18, 10);

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.SourceCurrencyEntityId).HasColumnName("SourceCurrencyEntityID");

                entity.Property(e => e.TargetCurrencyEntityId).HasColumnName("TargetCurrencyEntityID");

                entity.Property(e => e.Value).HasPrecision(18, 10);

                entity.HasOne(d => d.SourceCurrencyEntity)
                    .WithMany(p => p.TblExchangeRateSourceCurrencyEntities)
                    .HasForeignKey(d => d.SourceCurrencyEntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("SourceCurrencyID");

                entity.HasOne(d => d.TargetCurrencyEntity)
                    .WithMany(p => p.TblExchangeRateTargetCurrencyEntities)
                    .HasForeignKey(d => d.TargetCurrencyEntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("TargetCurrencyID");
            });

            modelBuilder.Entity<TblFavoriteEntity>(entity =>
            {
                entity.ToTable("tbl_FavoriteEntities", "Registry");

                entity.HasIndex(e => e.Id, "tbl_favoriteentities_\"id\"_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.Description).HasMaxLength(500);

                entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");

                entity.Property(e => e.IsEnabled).HasDefaultValueSql("true");

                entity.Property(e => e.Name).HasMaxLength(100);
            });

            modelBuilder.Entity<TblIdentityAddress>(entity =>
            {
                entity.ToTable("tbl_IdentityAddresses", "Identity");

                entity.HasIndex(e => e.AddressEntitiesId, "IX_tbl_IdentityAddresses_AddressEntitiesID");

                entity.HasIndex(e => e.IdentityInfoId, "IX_tbl_IdentityAddresses_UserInfoID");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.AddressEntitiesId).HasColumnName("AddressEntitiesID");

                entity.Property(e => e.Building).HasMaxLength(500);

                entity.Property(e => e.IdentityInfoId).HasColumnName("IdentityInfoID");

                entity.Property(e => e.Street).HasMaxLength(500);

                entity.Property(e => e.Subdivision).HasMaxLength(500);

                entity.Property(e => e.UnitNumber).HasMaxLength(500);

                entity.HasOne(d => d.AddressEntities)
                    .WithMany(p => p.TblIdentityAddresses)
                    .HasForeignKey(d => d.AddressEntitiesId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("AddressEntitiesID");

                entity.HasOne(d => d.BarangayNavigation)
                    .WithMany(p => p.TblIdentityAddresses)
                    .HasForeignKey(d => d.Barangay)
                    .HasConstraintName("tbl_identityaddresses__id_fk_brgy");

                entity.HasOne(d => d.CityNavigation)
                    .WithMany(p => p.TblIdentityAddresses)
                    .HasForeignKey(d => d.City)
                    .HasConstraintName("tbl_identityaddresses__id_fk_city");

                entity.HasOne(d => d.CountryNavigation)
                    .WithMany(p => p.TblIdentityAddresses)
                    .HasForeignKey(d => d.Country)
                    .HasConstraintName("tbl_identityaddresses_tbl_addresscountry__fk");

                entity.HasOne(d => d.IdentityInfo)
                    .WithMany(p => p.TblIdentityAddresses)
                    .HasForeignKey(d => d.IdentityInfoId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("UserInfoID");

                entity.HasOne(d => d.ProvinceNavigation)
                    .WithMany(p => p.TblIdentityAddresses)
                    .HasForeignKey(d => d.Province)
                    .HasConstraintName("tbl_identityaddresses__id_fk_province");

                entity.HasOne(d => d.RegionNavigation)
                    .WithMany(p => p.TblIdentityAddresses)
                    .HasForeignKey(d => d.Region)
                    .HasConstraintName("tbl_identityaddresses__id_fk");
            });

            modelBuilder.Entity<TblIdentityAddressEntity>(entity =>
            {
                entity.ToTable("tbl_IdentityAddressEntities", "Identity");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.Name).HasMaxLength(500);
            });

            modelBuilder.Entity<TblIdentityContact>(entity =>
            {
                entity.ToTable("tbl_IdentityContacts", "Identity");

                entity.HasIndex(e => e.UcentitiesId, "IX_tbl_IdentityContacts_UCEntitiesID");

                entity.HasIndex(e => e.UserCredentialId, "tbl_identitycontacts_usercredentialid_index");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.UcentitiesId).HasColumnName("UCEntitiesID");

                entity.Property(e => e.UserCredentialId).HasColumnName("UserCredentialID");

                entity.Property(e => e.Value)
                    .IsRequired()
                    .HasColumnType("character varying");

                entity.HasOne(d => d.Ucentities)
                    .WithMany(p => p.TblIdentityContacts)
                    .HasForeignKey(d => d.UcentitiesId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("UCEntitiesID");

                entity.HasOne(d => d.UserCredential)
                    .WithMany(p => p.TblIdentityContacts)
                    .HasForeignKey(d => d.UserCredentialId)
                    .HasConstraintName("tbl_identitycontacts___fk");
            });

            modelBuilder.Entity<TblIdentityContactEntity>(entity =>
            {
                entity.ToTable("tbl_IdentityContactEntities", "Identity");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<TblIdentityCredential>(entity =>
            {
                entity.ToTable("tbl_IdentityCredentials", "Identity");

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
                    .IsRequired()
                    .HasMaxLength(500)
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IdentityInfoId).HasColumnName("IdentityInfoID");

                entity.Property(e => e.Token).HasMaxLength(512);

                entity.Property(e => e.UserAlias).HasMaxLength(100);

                entity.Property(e => e.UserName).HasMaxLength(100);

                entity.HasOne(d => d.Application)
                    .WithMany(p => p.TblIdentityCredentials)
                    .HasForeignKey(d => d.ApplicationId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("tbl_identitycredentials___fk");

                entity.HasOne(d => d.IdentityInfo)
                    .WithMany(p => p.TblIdentityCredentials)
                    .HasForeignKey(d => d.IdentityInfoId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("tbl_identitycredentials_fk");
            });

            modelBuilder.Entity<TblIdentityInformation>(entity =>
            {
                entity.ToTable("tbl_IdentityInformation", "Identity");

                entity.HasIndex(e => e.Guid, "tbl_userinfo_un")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.FirstName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasMaxLength(500)
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IdentityDescription).HasMaxLength(100);

                entity.Property(e => e.IdentityName).HasMaxLength(100);

                entity.Property(e => e.LastName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.MiddleName).HasMaxLength(100);
            });

            modelBuilder.Entity<TblIdentityRole>(entity =>
            {
                entity.ToTable("tbl_IdentityRoles", "Identity");

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
                    .WithMany(p => p.TblIdentityRoles)
                    .HasForeignKey(d => d.RoleEntityId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("tbl_identityroles_fk_1");

                entity.HasOne(d => d.UserCred)
                    .WithMany(p => p.TblIdentityRoles)
                    .HasForeignKey(d => d.UserCredId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("tbl_identityroles_fk");
            });

            modelBuilder.Entity<TblIdentityRoleEntity>(entity =>
            {
                entity.ToTable("tbl_IdentityRoleEntities", "Identity");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.Name).HasMaxLength(100);
            });

            modelBuilder.Entity<TblIdentityVerification>(entity =>
            {
                entity.ToTable("tbl_IdentityVerifications", "Identity");

                entity.HasIndex(e => e.IdentityCredId, "IX_tbl_IdentityVerifications_IdentityCredID");

                entity.HasIndex(e => e.VerificationTypeId, "IX_tbl_IdentityVerifications_VerificationTypeID");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.IdentityCredId).HasColumnName("IdentityCredID");

                entity.Property(e => e.StatusUpdatedOn).HasColumnType("time with time zone");

                entity.Property(e => e.Token).HasColumnType("character varying");

                entity.Property(e => e.VerificationTypeId).HasColumnName("VerificationTypeID");

                entity.HasOne(d => d.IdentityCred)
                    .WithMany(p => p.TblIdentityVerifications)
                    .HasForeignKey(d => d.IdentityCredId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("tbl_UserVerifications_AuthID");

                entity.HasOne(d => d.VerificationType)
                    .WithMany(p => p.TblIdentityVerifications)
                    .HasForeignKey(d => d.VerificationTypeId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("tbl_UserVerifications_VerificationTypeID");
            });

            modelBuilder.Entity<TblIdentityVerificationEntity>(entity =>
            {
                entity.ToTable("tbl_IdentityVerificationEntities", "Identity");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.Name).HasMaxLength(100);
            });

            modelBuilder.Entity<TblLog>(entity =>
            {
                entity.ToTable("tbl_Logs", "Audit");

                entity.HasIndex(e => e.ApplicationId, "IX_tbl_ApplicationLogs_AppID");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.ApplicationId).HasColumnName("ApplicationID");

                entity.Property(e => e.Initiator).HasColumnType("character varying");

                entity.Property(e => e.Message).HasColumnType("character varying");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.Property(e => e.Seen).HasDefaultValueSql("false");

                entity.Property(e => e.Uuid)
                    .HasColumnType("character varying")
                    .HasColumnName("UUID");

                entity.HasOne(d => d.Application)
                    .WithMany(p => p.TblLogs)
                    .HasForeignKey(d => d.ApplicationId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("tbl_applogs_appid_fk");
            });

            modelBuilder.Entity<TblSessionDatum>(entity =>
            {
                entity.ToTable("tbl_SessionData", "Identity");

                entity.HasIndex(e => e.SessionEntityId, "IX_tbl_SessionData_SessionEntityID");

                entity.HasIndex(e => e.UserCredentialId, "IX_tbl_SessionData_UserCredentialID");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.SessionData).HasMaxLength(2000);

                entity.Property(e => e.SessionEntityId).HasColumnName("SessionEntityID");

                entity.Property(e => e.UserCredentialId).HasColumnName("UserCredentialID");

                entity.HasOne(d => d.SessionEntity)
                    .WithMany(p => p.TblSessionData)
                    .HasForeignKey(d => d.SessionEntityId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("tbl_sessiondata_fk");

                entity.HasOne(d => d.UserCredential)
                    .WithMany(p => p.TblSessionData)
                    .HasForeignKey(d => d.UserCredentialId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("tbl_sessiondata_fk_1");
            });

            modelBuilder.Entity<TblSessionEntity>(entity =>
            {
                entity.ToTable("tbl_SessionEntities", "Identity");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<TblUserBillsPaymentTransaction>(entity =>
            {
                entity.ToTable("tbl_UserBillsPaymentTransaction", "UserData");

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.Amount).HasPrecision(10, 2);

                entity.Property(e => e.ConvenienceFee)
                    .HasPrecision(10, 2)
                    .HasDefaultValueSql("0");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Discount).HasPrecision(10, 2);

                entity.Property(e => e.RawRequest).HasMaxLength(3000);

                entity.Property(e => e.RawResponse).HasMaxLength(3000);

                entity.Property(e => e.ReferenceNo).HasMaxLength(100);

                entity.Property(e => e.ResponseReasonPhrase).HasMaxLength(3000);

                entity.Property(e => e.ServiceCharge).HasPrecision(10, 2);

                entity.Property(e => e.TempCredentialUid).HasColumnType("character varying");

                entity.Property(e => e.TotalAmount).HasPrecision(10, 2);

                entity.Property(e => e.Uuid)
                    .HasMaxLength(50)
                    .HasColumnName("UUID")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.HasOne(d => d.Credential)
                    .WithMany(p => p.TblUserBillsPaymentTransactions)
                    .HasForeignKey(d => d.CredentialId)
                    .HasConstraintName("tbl_userbillspaymenttransaction_tbl_identitycredentials_id_fk");

                entity.HasOne(d => d.IncomeTransaction)
                    .WithMany(p => p.TblUserBillsPaymentTransactions)
                    .HasForeignKey(d => d.IncomeTransactionId)
                    .HasConstraintName("tbl_userbillspaymenttransaction_tbl_userincometransaction_id_fk");
            });

            modelBuilder.Entity<TblUserBinaryList>(entity =>
            {
                entity.ToTable("tbl_UserBinaryList", "UserData");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.SourceUserMapId).HasColumnName("SourceUserMapID");

                entity.Property(e => e.TargetUserMapId).HasColumnName("TargetUserMapID");

                entity.HasOne(d => d.SourceUserMap)
                    .WithMany(p => p.TblUserBinaryListSourceUserMaps)
                    .HasForeignKey(d => d.SourceUserMapId)
                    .HasConstraintName("tbl_userbinarylist_tbl_usermap_id_fk_2");

                entity.HasOne(d => d.TargetUserMap)
                    .WithMany(p => p.TblUserBinaryListTargetUserMaps)
                    .HasForeignKey(d => d.TargetUserMapId)
                    .HasConstraintName("tbl_userbinarylist_tbl_usermap_id_fk");
            });

            modelBuilder.Entity<TblUserBinaryListMultiplex>(entity =>
            {
                entity.ToTable("tbl_UserBinaryListMultiplex", "UserData");

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.UserMap)
                    .WithMany(p => p.TblUserBinaryListMultiplices)
                    .HasForeignKey(d => d.UserMapId)
                    .HasConstraintName("tbl_userbinarylistmultiplex_tbl_usermap_id_fk");
            });

            modelBuilder.Entity<TblUserBusinessPackage>(entity =>
            {
                entity.ToTable("tbl_UserBusinessPackage", "UserData");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.BusinessPackageId).HasColumnName("BusinessPackageID");

                entity.Property(e => e.CancellationDate).HasColumnType("timestamp without time zone");

                entity.Property(e => e.CodeHash).HasMaxLength(300);

                entity.Property(e => e.CodeString).HasMaxLength(100);

                entity.Property(e => e.Remarks).HasMaxLength(1000);

                entity.Property(e => e.UserAuthId).HasColumnName("UserAuthID");

                entity.Property(e => e.UserDepositRequestId).HasColumnName("UserDepositRequestID");

                entity.HasOne(d => d.ConsumedByNavigation)
                    .WithMany(p => p.TblUserBusinessPackageConsumedByNavigations)
                    .HasForeignKey(d => d.ConsumedBy)
                    .HasConstraintName("tbl_userbusinesspackage_fk");

                entity.HasOne(d => d.RecipientAuth)
                    .WithMany(p => p.TblUserBusinessPackageRecipientAuths)
                    .HasForeignKey(d => d.RecipientAuthId)
                    .HasConstraintName("tbl_userbusinesspackage_tbl_userauth_id_fk");

                entity.HasOne(d => d.UserAuth)
                    .WithMany(p => p.TblUserBusinessPackageUserAuths)
                    .HasForeignKey(d => d.UserAuthId)
                    .HasConstraintName("UserAuthID");

                entity.HasOne(d => d.UserDepositRequest)
                    .WithMany(p => p.TblUserBusinessPackages)
                    .HasForeignKey(d => d.UserDepositRequestId)
                    .HasConstraintName("UserDepositRequestID");
            });

            modelBuilder.Entity<TblUserBusinessPackageUpgradeTransaction>(entity =>
            {
                entity.ToTable("tbl_UserBusinessPackageUpgradeTransactions", "UserData");

                entity.HasIndex(e => e.Id, "userbusinesspackageupgradetransactions_\"id\"_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .HasDefaultValueSql("nextval('\"UserData\".\"UserBusinessPackageUpgradeTransactions_ID_seq\"'::regclass)");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.Uuid)
                    .HasMaxLength(256)
                    .HasColumnName("uuid")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.HasOne(d => d.Credential)
                    .WithMany(p => p.TblUserBusinessPackageUpgradeTransactions)
                    .HasForeignKey(d => d.CredentialId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("ubput_tbl_identitycredentials_id_fk");

                entity.HasOne(d => d.UserBusinessPackage)
                    .WithMany(p => p.TblUserBusinessPackageUpgradeTransactions)
                    .HasForeignKey(d => d.UserBusinessPackageId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("ubput_tbl_userbusinesspackage_id_fk");
            });

            modelBuilder.Entity<TblUserBusinessType>(entity =>
            {
                entity.ToTable("tbl_UserBusinessType", "UserData");

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");

                entity.Property(e => e.IsEnabled).HasDefaultValueSql("true");

                entity.HasOne(d => d.IdentityCredential)
                    .WithMany(p => p.TblUserBusinessTypes)
                    .HasForeignKey(d => d.IdentityCredentialId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("tbl_userbusinesstype_tbl_identitycredentials_id_fk");
            });

            modelBuilder.Entity<TblUserCommissionDeductionRequest>(entity =>
            {
                entity.ToTable("tbl_UserCommissionDeductionRequest", "UserData");

                entity.HasIndex(e => e.Id, "tbl_usercommissiondeductionrequest_\"id\"_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.Balance).HasPrecision(18, 10);

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.DeductionCharge).HasPrecision(18, 10);

                entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.PrincipalAmount).HasPrecision(18, 10);

                entity.HasOne(d => d.UserBusinessPackage)
                    .WithMany(p => p.TblUserCommissionDeductionRequests)
                    .HasForeignKey(d => d.UserBusinessPackageId)
                    .HasConstraintName("tbl_ucdr_tbl_userbusinesspackage_id_fk");
            });

            modelBuilder.Entity<TblUserDepositRequest>(entity =>
            {
                entity.ToTable("tbl_UserDepositRequest", "UserData");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.Address).HasMaxLength(10000);

                entity.Property(e => e.Amount).HasPrecision(18, 10);

                entity.Property(e => e.ConvenienceFee).HasPrecision(18, 10);

                entity.Property(e => e.Discount).HasPrecision(18, 10);

                entity.Property(e => e.RawRequestData).HasMaxLength(10000);

                entity.Property(e => e.RawResponseData).HasMaxLength(5000);

                entity.Property(e => e.ReferenceNo).HasMaxLength(35);

                entity.Property(e => e.Remarks).HasMaxLength(10000);

                entity.Property(e => e.SystemFee).HasPrecision(18, 10);

                entity.Property(e => e.TempCredentialUid).HasColumnType("character varying");

                entity.Property(e => e.UserAuthId).HasColumnName("UserAuthID");

                entity.HasOne(d => d.SourceCurrency)
                    .WithMany(p => p.TblUserDepositRequests)
                    .HasForeignKey(d => d.SourceCurrencyId)
                    .HasConstraintName("SourceCurrencyId");

                entity.HasOne(d => d.TargetWalletType)
                    .WithMany(p => p.TblUserDepositRequests)
                    .HasForeignKey(d => d.TargetWalletTypeId)
                    .HasConstraintName("TargetWalletTypeId");

                entity.HasOne(d => d.UserAuth)
                    .WithMany(p => p.TblUserDepositRequests)
                    .HasForeignKey(d => d.UserAuthId)
                    .HasConstraintName("UserAuthID");
            });

            modelBuilder.Entity<TblUserEloadTransaction>(entity =>
            {
                entity.ToTable("tbl_UserEloadTransaction", "UserData");

                entity.HasIndex(e => e.Id, "tbl_usereloadtransaction_\"id\"_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.Amount).HasColumnType("character varying");

                entity.Property(e => e.CustomerNumber).HasMaxLength(13);

                entity.Property(e => e.IsDeleted)
                    .HasColumnName("isDeleted")
                    .HasDefaultValueSql("false");

                entity.Property(e => e.IsEnabled)
                    .HasColumnName("isEnabled")
                    .HasDefaultValueSql("true");

                entity.Property(e => e.RawRequest).HasMaxLength(5000);

                entity.Property(e => e.RawResponse).HasMaxLength(5000);

                entity.Property(e => e.SenderNumber).HasMaxLength(13);

                entity.Property(e => e.TelcoProductCodeId).HasColumnName("TelcoProductCodeID");

                entity.Property(e => e.TransactionId)
                    .HasColumnType("character varying")
                    .HasColumnName("TransactionID");

                entity.Property(e => e.WalletTypeId).HasColumnName("WalletTypeID");
            });

            modelBuilder.Entity<TblUserFavorite>(entity =>
            {
                entity.ToTable("tbl_UserFavorites", "UserData");

                entity.HasIndex(e => e.Id, "tbl_userfavorites_\"id\"_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.CredentialId).HasColumnName("CredentialID");

                entity.Property(e => e.Data).HasMaxLength(5000);

                entity.Property(e => e.FavoriteEntityId).HasColumnName("FavoriteEntityID");

                entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");

                entity.Property(e => e.IsEnabled).HasDefaultValueSql("true");

                entity.HasOne(d => d.Credential)
                    .WithMany(p => p.TblUserFavorites)
                    .HasForeignKey(d => d.CredentialId)
                    .HasConstraintName("tbl_userfavorites_tbl_identitycredentials_id_fk");

                entity.HasOne(d => d.FavoriteEntity)
                    .WithMany(p => p.TblUserFavorites)
                    .HasForeignKey(d => d.FavoriteEntityId)
                    .HasConstraintName("tbl_userfavorites_tbl_favoriteentities_id_fk");
            });

            modelBuilder.Entity<TblUserIncomePartition>(entity =>
            {
                entity.ToTable("tbl_UserIncomePartition", "UserData");

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.Percentage).HasPrecision(18, 8);

                entity.HasOne(d => d.UserRole)
                    .WithMany(p => p.TblUserIncomePartitions)
                    .HasForeignKey(d => d.UserRoleId)
                    .HasConstraintName("UserRoleId");
            });

            modelBuilder.Entity<TblUserIncomeTransaction>(entity =>
            {
                entity.ToTable("tbl_UserIncomeTransaction", "UserData");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.IncomeValue).HasPrecision(18, 10);

                entity.Property(e => e.Remarks).HasMaxLength(10000);

                entity.Property(e => e.TempCredentialUid).HasColumnType("character varying");

                entity.HasOne(d => d.PairMap)
                    .WithMany(p => p.TblUserIncomeTransactionPairMaps)
                    .HasForeignKey(d => d.PairMapId)
                    .HasConstraintName("tbl_userincometransaction_tbl_usermap_id_fk_3");

                entity.HasOne(d => d.SourceMap)
                    .WithMany(p => p.TblUserIncomeTransactionSourceMaps)
                    .HasForeignKey(d => d.SourceMapId)
                    .HasConstraintName("tbl_userincometransaction_tbl_usermap_id_fk_2");

                entity.HasOne(d => d.TargetMap)
                    .WithMany(p => p.TblUserIncomeTransactionTargetMaps)
                    .HasForeignKey(d => d.TargetMapId)
                    .HasConstraintName("tbl_userincometransaction_tbl_usermap_id_fk");

                entity.HasOne(d => d.UserAuth)
                    .WithMany(p => p.TblUserIncomeTransactions)
                    .HasForeignKey(d => d.UserAuthId)
                    .HasConstraintName("UserAuthID");
            });

            modelBuilder.Entity<TblUserMap>(entity =>
            {
                entity.ToTable("tbl_UserMap", "UserData");

                entity.HasIndex(e => e.Alias, "tbl_usermap_alias_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .ValueGeneratedNever()
                    .HasColumnName("ID");

                entity.Property(e => e.Alias).HasColumnType("character varying");

                entity.Property(e => e.Uid)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.HasOne(d => d.IdNavigation)
                    .WithOne(p => p.TblUserMap)
                    .HasForeignKey<TblUserMap>(d => d.Id)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("tbl_usermap_fk");

                entity.HasOne(d => d.SponsorUser)
                    .WithMany(p => p.InverseSponsorUser)
                    .HasForeignKey(d => d.SponsorUserId)
                    .HasConstraintName("SponsorUserId");

                entity.HasOne(d => d.UplineUser)
                    .WithMany(p => p.InverseUplineUser)
                    .HasForeignKey(d => d.UplineUserId)
                    .HasConstraintName("uplineuserbpid");
            });

            modelBuilder.Entity<TblUserWallet>(entity =>
            {
                entity.ToTable("tbl_UserWallet", "UserData");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.Balance).HasPrecision(24, 8);

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");

                entity.HasOne(d => d.UserAuth)
                    .WithMany(p => p.TblUserWallets)
                    .HasForeignKey(d => d.UserAuthId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("tbl_UserWallet_UserAuthId_fkey");

                entity.HasOne(d => d.WalletType)
                    .WithMany(p => p.TblUserWallets)
                    .HasForeignKey(d => d.WalletTypeId)
                    .HasConstraintName("tbl_UserWallet_WalletTypeId_fkey");
            });

            modelBuilder.Entity<TblUserWalletAddress>(entity =>
            {
                entity.ToTable("tbl_UserWalletAddress", "UserData");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.Address).HasMaxLength(512);

                entity.Property(e => e.Balance).HasPrecision(18, 10);

                entity.Property(e => e.Remarks).HasMaxLength(100);

                entity.HasOne(d => d.UserAuth)
                    .WithMany(p => p.TblUserWalletAddresses)
                    .HasForeignKey(d => d.UserAuthId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("UserAuthID");

                entity.HasOne(d => d.WalletType)
                    .WithMany(p => p.TblUserWalletAddresses)
                    .HasForeignKey(d => d.WalletTypeId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("tbl_userwalletaddress_fk");
            });

            modelBuilder.Entity<TblUserWalletTransaction>(entity =>
            {
                entity.ToTable("tbl_UserWalletTransaction", "UserData");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.Amount).HasPrecision(24, 8);

                entity.Property(e => e.Description).HasMaxLength(10000);

                entity.Property(e => e.PreviousBalance).HasPrecision(24, 8);

                entity.Property(e => e.Remarks).HasMaxLength(10000);

                entity.Property(e => e.RunningBalance).HasPrecision(24, 8);

                entity.HasOne(d => d.SourceUserWallet)
                    .WithMany(p => p.TblUserWalletTransactionSourceUserWallets)
                    .HasForeignKey(d => d.SourceUserWalletId)
                    .HasConstraintName("SourceUserWalletId");

                entity.HasOne(d => d.TargetUserWallet)
                    .WithMany(p => p.TblUserWalletTransactionTargetUserWallets)
                    .HasForeignKey(d => d.TargetUserWalletId)
                    .HasConstraintName("tbl_userwallettransaction_tbl_userwallet_id_fk");

                entity.HasOne(d => d.UserAuth)
                    .WithMany(p => p.TblUserWalletTransactions)
                    .HasForeignKey(d => d.UserAuthId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("UserAuthID");
            });

            modelBuilder.Entity<TblUserWithdrawalRequest>(entity =>
            {
                entity.ToTable("tbl_UserWithdrawalRequest", "UserData");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.Address).HasMaxLength(10000);

                entity.Property(e => e.Remarks).HasColumnType("character varying");

                entity.Property(e => e.TotalAmount).HasPrecision(18, 10);

                entity.HasOne(d => d.SourceWalletType)
                    .WithMany(p => p.TblUserWithdrawalRequestSourceWalletTypes)
                    .HasForeignKey(d => d.SourceWalletTypeId)
                    .HasConstraintName("SourceWalletTypeId");

                entity.HasOne(d => d.TargetCurrency)
                    .WithMany(p => p.TblUserWithdrawalRequestTargetCurrencies)
                    .HasForeignKey(d => d.TargetCurrencyId)
                    .HasConstraintName("TargetCurrencyId");

                entity.HasOne(d => d.UserAuth)
                    .WithMany(p => p.TblUserWithdrawalRequests)
                    .HasForeignKey(d => d.UserAuthId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("UserAuthID");
            });

            modelBuilder.Entity<TblWalletEntity>(entity =>
            {
                entity.ToTable("tbl_WalletEntities", "Wallet");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L);

                entity.Property(e => e.Code)
                    .IsRequired()
                    .HasMaxLength(9);

                entity.Property(e => e.CurrencyEntityId).HasColumnName("CurrencyEntityID");

                entity.Property(e => e.Desc).HasMaxLength(500);

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.HasOne(d => d.Application)
                    .WithMany(p => p.TblWalletEntities)
                    .HasForeignKey(d => d.ApplicationId)
                    .HasConstraintName("tbl_walletentities_tbl_applications_id_fk");

                entity.HasOne(d => d.CurrencyEntity)
                    .WithMany(p => p.TblWalletEntities)
                    .HasForeignKey(d => d.CurrencyEntityId)
                    .HasConstraintName("CurrencyID");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
