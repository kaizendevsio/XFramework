using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

#nullable disable

namespace IdentityServer.Domain.DTO
{
    public partial class XFrameworkContext : DbContext
    {
        public XFrameworkContext()
        {
        }

        public XFrameworkContext(DbContextOptions<XFrameworkContext> options)
            : base(options)
        {
        }

        public virtual DbSet<TblAddressBarangay> TblAddressBarangays { get; set; }
        public virtual DbSet<TblAddressCity> TblAddressCities { get; set; }
        public virtual DbSet<TblAddressCountry> TblAddressCountries { get; set; }
        public virtual DbSet<TblAddressProvince> TblAddressProvinces { get; set; }
        public virtual DbSet<TblAddressRegion> TblAddressRegions { get; set; }
        public virtual DbSet<TblApplication> TblApplications { get; set; }
        public virtual DbSet<TblCurrency> TblCurrencies { get; set; }
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
        public virtual DbSet<TblSessionDatum> TblSessionData { get; set; }
        public virtual DbSet<TblSessionEntity> TblSessionEntities { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see http://go.microsoft.com/fwlink/?LinkId=723263.
                optionsBuilder.UseNpgsql("Host=localhost;Database=XFramework;Username=dbAdmin;Password=4*5WD-K8%f*NqmPY");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasAnnotation("Relational:Collation", "English_United States.1252");

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
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.CurrencyId).HasColumnName("CurrencyID");

                entity.Property(e => e.IsoCode2).HasMaxLength(2);

                entity.Property(e => e.IsoCode3).HasMaxLength(3);

                entity.Property(e => e.Language).HasMaxLength(50);

                entity.Property(e => e.LastChanged).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

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
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.AppName).HasColumnType("character varying");

                entity.Property(e => e.AvailabilityDate).HasColumnType("timestamp with time zone");

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.EnterpriseId).HasColumnName("EnterpriseID");

                entity.Property(e => e.Expiration).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ParentAppId).HasColumnName("ParentAppID");

                entity.Property(e => e.Uid)
                    .HasColumnType("character varying")
                    .HasColumnName("UID");

                entity.HasOne(d => d.Enterprise)
                    .WithMany(p => p.TblApplications)
                    .HasForeignKey(d => d.EnterpriseId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("tbl_applications_tbl_enterprises_id_fk");
            });

            modelBuilder.Entity<TblCurrency>(entity =>
            {
                entity.ToTable("tbl_Currency", "Finance");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.CurrencyIsoCode3).HasMaxLength(4);

                entity.Property(e => e.Description).HasMaxLength(500);

                entity.Property(e => e.LastChanged).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Name).HasMaxLength(256);
            });

            modelBuilder.Entity<TblEnterprise>(entity =>
            {
                entity.ToTable("tbl_Enterprises", "Application");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Name).HasMaxLength(500);
            });

            modelBuilder.Entity<TblExchangeRate>(entity =>
            {
                entity.ToTable("tbl_ExchangeRate", "Finance");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.EffectivityDate).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ExpiryDate).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Fee).HasPrecision(18, 10);

                entity.Property(e => e.LastChanged).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.SourceCurrencyId).HasColumnName("SourceCurrencyID");

                entity.Property(e => e.TargetCurrencyId).HasColumnName("TargetCurrencyID");

                entity.Property(e => e.Value).HasPrecision(18, 10);

                entity.HasOne(d => d.SourceCurrency)
                    .WithMany(p => p.TblExchangeRateSourceCurrencies)
                    .HasForeignKey(d => d.SourceCurrencyId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("SourceCurrencyID");

                entity.HasOne(d => d.TargetCurrency)
                    .WithMany(p => p.TblExchangeRateTargetCurrencies)
                    .HasForeignKey(d => d.TargetCurrencyId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("TargetCurrencyID");
            });

            modelBuilder.Entity<TblIdentityAddress>(entity =>
            {
                entity.ToTable("tbl_IdentityAddresses", "Identity");

                entity.HasIndex(e => e.AddressEntitiesId, "IX_tbl_IdentityAddresses_AddressEntitiesID");

                entity.HasIndex(e => e.UserInfoId, "IX_tbl_IdentityAddresses_UserInfoID");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.AddressEntitiesId).HasColumnName("AddressEntitiesID");

                entity.Property(e => e.Building).HasMaxLength(500);

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Street).HasMaxLength(500);

                entity.Property(e => e.Subdivision).HasMaxLength(500);

                entity.Property(e => e.UnitNumber).HasMaxLength(500);

                entity.Property(e => e.UserInfoId).HasColumnName("UserInfoID");

                entity.HasOne(d => d.AddressEntities)
                    .WithMany(p => p.TblIdentityAddresses)
                    .HasForeignKey(d => d.AddressEntitiesId)
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

                entity.HasOne(d => d.ProvinceNavigation)
                    .WithMany(p => p.TblIdentityAddresses)
                    .HasForeignKey(d => d.Province)
                    .HasConstraintName("tbl_identityaddresses__id_fk_province");

                entity.HasOne(d => d.RegionNavigation)
                    .WithMany(p => p.TblIdentityAddresses)
                    .HasForeignKey(d => d.Region)
                    .HasConstraintName("tbl_identityaddresses__id_fk");

                entity.HasOne(d => d.UserInfo)
                    .WithMany(p => p.TblIdentityAddresses)
                    .HasForeignKey(d => d.UserInfoId)
                    .HasConstraintName("UserInfoID");
            });

            modelBuilder.Entity<TblIdentityAddressEntity>(entity =>
            {
                entity.ToTable("tbl_IdentityAddressEntities", "Identity");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Name).HasMaxLength(500);
            });

            modelBuilder.Entity<TblIdentityContact>(entity =>
            {
                entity.ToTable("tbl_IdentityContacts", "Identity");

                entity.HasIndex(e => e.UcentitiesId, "IX_tbl_IdentityContacts_UCEntitiesID");

                entity.HasIndex(e => e.Value, "tbl_identitycontacts_un")
                    .IsUnique();

                entity.HasIndex(e => e.UserCredentialId, "tbl_identitycontacts_usercredentialid_index");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.UcentitiesId).HasColumnName("UCEntitiesID");

                entity.Property(e => e.UserCredentialId).HasColumnName("UserCredentialID");

                entity.Property(e => e.Value)
                    .IsRequired()
                    .HasColumnType("character varying");

                entity.HasOne(d => d.Ucentities)
                    .WithMany(p => p.TblIdentityContacts)
                    .HasForeignKey(d => d.UcentitiesId)
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
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<TblIdentityCredential>(entity =>
            {
                entity.ToTable("tbl_IdentityCredentials", "Identity");

                entity.HasIndex(e => e.AppId, "IX_tbl_IdentityCredentials_AppID");

                entity.HasIndex(e => e.IdentityInfoId, "IX_tbl_IdentityCredentials_IdentityInfoID");

                entity.HasIndex(e => e.UserName, "tbl_identitycredentials_un")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.AppId).HasColumnName("AppID");

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.IdentityInfoId).HasColumnName("IdentityInfoID");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.UserAlias).HasMaxLength(100);

                entity.Property(e => e.UserName).HasMaxLength(100);

                entity.HasOne(d => d.App)
                    .WithMany(p => p.TblIdentityCredentials)
                    .HasForeignKey(d => d.AppId)
                    .HasConstraintName("tbl_identitycredentials1___fk");

                entity.HasOne(d => d.IdentityInfo)
                    .WithMany(p => p.TblIdentityCredentials)
                    .HasForeignKey(d => d.IdentityInfoId)
                    .HasConstraintName("tbl_identitycredentials_fk");
            });

            modelBuilder.Entity<TblIdentityInfo>(entity =>
            {
                entity.ToTable("tbl_IdentityInfo", "Identity");

                entity.HasIndex(e => e.Uid, "tbl_userinfo_un")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Dob)
                    .HasColumnType("date")
                    .HasColumnName("DOB");

                entity.Property(e => e.FirstName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.IdentityDescription)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.IdentityName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.LastName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.MiddleName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Uid)
                    .IsRequired()
                    .HasMaxLength(500)
                    .HasColumnName("UID");
            });

            modelBuilder.Entity<TblIdentityRole>(entity =>
            {
                entity.ToTable("tbl_IdentityRoles", "Identity");

                entity.HasIndex(e => e.RoleEntityId, "IX_tbl_IdentityRoles_RoleEntityID");

                entity.HasIndex(e => e.UserCredId, "IX_tbl_IdentityRoles_UserCredID");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.RoleEntityId).HasColumnName("RoleEntityID");

                entity.Property(e => e.RoleExpiration).HasColumnType("timestamp with time zone");

                entity.Property(e => e.UserCredId).HasColumnName("UserCredID");

                entity.HasOne(d => d.RoleEntity)
                    .WithMany(p => p.TblIdentityRoles)
                    .HasForeignKey(d => d.RoleEntityId)
                    .HasConstraintName("tbl_identityroles_fk_1");

                entity.HasOne(d => d.UserCred)
                    .WithMany(p => p.TblIdentityRoles)
                    .HasForeignKey(d => d.UserCredId)
                    .HasConstraintName("tbl_identityroles_fk");
            });

            modelBuilder.Entity<TblIdentityRoleEntity>(entity =>
            {
                entity.ToTable("tbl_IdentityRoleEntities", "Identity");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

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
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Expiry).HasColumnType("timestamp with time zone");

                entity.Property(e => e.IdentityCredId).HasColumnName("IdentityCredID");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.StatusUpdatedOn).HasColumnType("time with time zone");

                entity.Property(e => e.Token).HasColumnType("character varying");

                entity.Property(e => e.VerificationTypeId).HasColumnName("VerificationTypeID");

                entity.HasOne(d => d.IdentityCred)
                    .WithMany(p => p.TblIdentityVerifications)
                    .HasForeignKey(d => d.IdentityCredId)
                    .HasConstraintName("tbl_UserVerifications_AuthID");

                entity.HasOne(d => d.VerificationType)
                    .WithMany(p => p.TblIdentityVerifications)
                    .HasForeignKey(d => d.VerificationTypeId)
                    .HasConstraintName("tbl_UserVerifications_VerificationTypeID");
            });

            modelBuilder.Entity<TblIdentityVerificationEntity>(entity =>
            {
                entity.ToTable("tbl_IdentityVerificationEntities", "Identity");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Name).HasMaxLength(100);
            });

            modelBuilder.Entity<TblSessionDatum>(entity =>
            {
                entity.ToTable("tbl_SessionData", "Identity");

                entity.HasIndex(e => e.SessionEntityId, "IX_tbl_SessionData_SessionEntityID");

                entity.HasIndex(e => e.UserCredentialId, "IX_tbl_SessionData_UserCredentialID");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.SessionData).HasMaxLength(2000);

                entity.Property(e => e.SessionEntityId).HasColumnName("SessionEntityID");

                entity.Property(e => e.UserCredentialId).HasColumnName("UserCredentialID");

                entity.HasOne(d => d.SessionEntity)
                    .WithMany(p => p.TblSessionData)
                    .HasForeignKey(d => d.SessionEntityId)
                    .HasConstraintName("tbl_sessiondata_fk");

                entity.HasOne(d => d.UserCredential)
                    .WithMany(p => p.TblSessionData)
                    .HasForeignKey(d => d.UserCredentialId)
                    .HasConstraintName("tbl_sessiondata_fk_1");
            });

            modelBuilder.Entity<TblSessionEntity>(entity =>
            {
                entity.ToTable("tbl_SessionEntities", "Identity");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
