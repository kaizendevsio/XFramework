﻿using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects
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
        public virtual DbSet<DepositRequest> DepositRequests { get; set; }
        public virtual DbSet<Enterprise> Enterprises { get; set; }
        public virtual DbSet<ExchangeRate> ExchangeRates { get; set; }
        public virtual DbSet<Gateway> Gateways { get; set; }
        public virtual DbSet<GatewayCategory> GatewayCategories { get; set; }
        public virtual DbSet<GatewayEndpoint> GatewayEndpoints { get; set; }
        public virtual DbSet<GatewayEntity> GatewayEntities { get; set; }
        public virtual DbSet<GatewayInstruction> GatewayInstructions { get; set; }
        public virtual DbSet<GatewayResponse> GatewayResponses { get; set; }
        public virtual DbSet<GatewayResponseStatusType> GatewayResponseStatusTypes { get; set; }
        public virtual DbSet<GatewayResponseType> GatewayResponseTypes { get; set; }
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
        public virtual DbSet<RegistryConfiguration> RegistryConfigurations { get; set; }
        public virtual DbSet<RegistryConfigurationGroup> RegistryConfigurationGroups { get; set; }
        public virtual DbSet<RegistryFavoriteEntity> RegistryFavoriteEntities { get; set; }
        public virtual DbSet<SessionDatum> SessionData { get; set; }
        public virtual DbSet<SessionEntity> SessionEntities { get; set; }
        public virtual DbSet<Wallet> Wallets { get; set; }
        public virtual DbSet<WalletAddress> WalletAddresses { get; set; }
        public virtual DbSet<WalletEntity> WalletEntities { get; set; }
        public virtual DbSet<WalletTransaction> WalletTransactions { get; set; }
        public virtual DbSet<WithdrawalRequest> WithdrawalRequests { get; set; }

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
            modelBuilder.HasPostgresExtension("uuid-ossp")
                .HasAnnotation("Relational:Collation", "English_Philippines.1252");

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

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.UpdatedAt).HasColumnType("timestamp with time zone");

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

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.UpdatedAt).HasColumnType("timestamp with time zone");

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
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.CurrencyId).HasColumnName("CurrencyID");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsoCode2).HasMaxLength(2);

                entity.Property(e => e.IsoCode3).HasMaxLength(3);

                entity.Property(e => e.Language).HasMaxLength(50);

                entity.Property(e => e.LastChanged).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

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

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.UpdatedAt).HasColumnType("timestamp with time zone");

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

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.UpdatedAt).HasColumnType("timestamp with time zone");

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
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.AppName).HasColumnType("character varying");

                entity.Property(e => e.AvailabilityDate).HasColumnType("timestamp with time zone");

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.EnterpriseId).HasColumnName("EnterpriseID");

                entity.Property(e => e.Expiration).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Guid)
                    .IsRequired()
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
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.ModifiedAt).HasColumnType("timestamp with time zone");
            });

            modelBuilder.Entity<AuditHistory>(entity =>
            {
                entity.ToTable("AuditHistory", "Audit");

                entity.HasIndex(e => e.Guid, "tbl_audithistory_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Guid)
                    .IsRequired()
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
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.DeviceName).HasMaxLength(50);

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.Ipaddress)
                    .HasMaxLength(18)
                    .HasColumnName("IPAddress");

                entity.Property(e => e.LoginSource).HasMaxLength(50);

                entity.Property(e => e.ModifiedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                entity.HasOne(d => d.IdentityCredentials)
                    .WithMany(p => p.AuthorizationLogs)
                    .HasForeignKey(d => d.IdentityCredentialsId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("tbl_userauthhistory_fk");
            });

            modelBuilder.Entity<CurrencyEntity>(entity =>
            {
                entity.ToTable("CurrencyEntity", "Finance");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasDefaultValueSql("nextval('\"Finance\".\"tbl_Currency_id_seq\"'::regclass)");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.CurrencyIsoCode3).HasMaxLength(4);

                entity.Property(e => e.Description).HasMaxLength(500);

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.ModifiedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasMaxLength(256);
            });

            modelBuilder.Entity<DepositRequest>(entity =>
            {
                entity.ToTable("DepositRequest", "Wallet");

                entity.HasIndex(e => e.Guid, "tbl_userdepositrequest_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.Address).HasMaxLength(10000);

                entity.Property(e => e.Amount).HasPrecision(18, 10);

                entity.Property(e => e.ConvenienceFee).HasPrecision(18, 10);

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.Discount).HasPrecision(18, 10);

                entity.Property(e => e.ExpiryDate).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.ModifiedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.RawRequestData).HasMaxLength(10000);

                entity.Property(e => e.RawResponseData).HasMaxLength(5000);

                entity.Property(e => e.ReferenceNo).HasMaxLength(35);

                entity.Property(e => e.Remarks).HasMaxLength(10000);

                entity.Property(e => e.SystemFee).HasPrecision(18, 10);

                entity.HasOne(d => d.Gateway)
                    .WithMany(p => p.DepositRequests)
                    .HasForeignKey(d => d.GatewayId)
                    .HasConstraintName("DepositRequest_Gateway_ID_fk");

                entity.HasOne(d => d.IdentityCredential)
                    .WithMany(p => p.DepositRequests)
                    .HasForeignKey(d => d.IdentityCredentialId)
                    .HasConstraintName("IdentityCredentialId");

                entity.HasOne(d => d.SourceCurrency)
                    .WithMany(p => p.DepositRequests)
                    .HasForeignKey(d => d.SourceCurrencyId)
                    .HasConstraintName("SourceCurrencyId");

                entity.HasOne(d => d.TargetWalletType)
                    .WithMany(p => p.DepositRequests)
                    .HasForeignKey(d => d.TargetWalletTypeId)
                    .HasConstraintName("TargetWalletTypeId");
            });

            modelBuilder.Entity<Enterprise>(entity =>
            {
                entity.ToTable("Enterprise", "Application");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.ModifiedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Name).HasMaxLength(500);
            });

            modelBuilder.Entity<ExchangeRate>(entity =>
            {
                entity.ToTable("ExchangeRate", "Finance");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.EffectivityDate).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ExpiryDate).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Fee).HasPrecision(18, 10);

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.ModifiedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

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

            modelBuilder.Entity<Gateway>(entity =>
            {
                entity.ToTable("Gateway", "Integration.PaymentGateway");

                entity.HasIndex(e => e.Id, "tbl_gateways_\"id\"_uindex")
                    .IsUnique();

                entity.HasIndex(e => e.Guid, "tbl_gateways_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .HasDefaultValueSql("nextval('\"Integration.PaymentGateway\".\"tbl_Gateways_ID_seq\"'::regclass)");

                entity.Property(e => e.ConvenienceFee).HasPrecision(10, 2);

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Description)
                    .IsRequired()
                    .HasColumnType("character varying");

                entity.Property(e => e.Discount)
                    .HasPrecision(10, 2)
                    .HasDefaultValueSql("0");

                entity.Property(e => e.GatewayCategoryId).HasColumnName("GatewayCategoryID");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.Image).HasColumnType("character varying");

                entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");

                entity.Property(e => e.IsEnabled).HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasColumnType("character varying");

                entity.Property(e => e.ServiceCharge).HasPrecision(3, 2);

                entity.HasOne(d => d.GatewayCategory)
                    .WithMany(p => p.Gateways)
                    .HasForeignKey(d => d.GatewayCategoryId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("tbl_gateways_tbl_gatewaycategories_id_fk");

                entity.HasOne(d => d.ProviderEndpoint)
                    .WithMany(p => p.Gateways)
                    .HasForeignKey(d => d.ProviderEndpointId)
                    .HasConstraintName("tbl_gateways_tbl_providerendpoints_id_fk");
            });

            modelBuilder.Entity<GatewayCategory>(entity =>
            {
                entity.ToTable("GatewayCategory", "Integration.PaymentGateway");

                entity.HasIndex(e => e.Id, "tbl_gatewaycategories_\"id\"_uindex")
                    .IsUnique();

                entity.HasIndex(e => e.Guid, "tbl_gatewaycategories_uuid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .HasDefaultValueSql("nextval('\"Integration.PaymentGateway\".\"tbl_GatewayCategories_ID_seq\"'::regclass)");

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsDeleted)
                    .HasColumnName("isDeleted")
                    .HasDefaultValueSql("false");

                entity.Property(e => e.IsEnabled)
                    .HasColumnName("isEnabled")
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasColumnType("character varying");
            });

            modelBuilder.Entity<GatewayEndpoint>(entity =>
            {
                entity.ToTable("GatewayEndpoint", "Integration.PaymentGateway");

                entity.HasIndex(e => e.Guid, "tbl_gatewayendpoints_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .HasDefaultValueSql("nextval('\"Integration.PaymentGateway\".\"tbl_GatewayEndpoints_ID_seq\"'::regclass)");

                entity.Property(e => e.BaseUrlEndpoint).HasMaxLength(100);

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.GatewayId).HasColumnName("GatewayID");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.ModifiedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Name).HasMaxLength(100);

                entity.Property(e => e.UrlEndpoint).HasMaxLength(100);

                entity.HasOne(d => d.Gateway)
                    .WithMany(p => p.GatewayEndpoints)
                    .HasForeignKey(d => d.GatewayId)
                    .HasConstraintName("tbl_gatewayendpoints_tbl_gatewayentities_id_fk");
            });

            modelBuilder.Entity<GatewayEntity>(entity =>
            {
                entity.ToTable("GatewayEntity", "Integration.PaymentGateway");

                entity.HasIndex(e => e.Id, "tbl_gatewayentities_id_uindex")
                    .IsUnique();

                entity.HasIndex(e => e.Guid, "tbl_gatewayentities_uuid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .HasDefaultValueSql("nextval('\"Integration.PaymentGateway\".\"tbl_GatewayEntities_ID_seq\"'::regclass)");

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsDeleted)
                    .HasColumnName("isDeleted")
                    .HasDefaultValueSql("false");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasColumnName("isEnabled")
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasColumnType("character varying");
            });

            modelBuilder.Entity<GatewayInstruction>(entity =>
            {
                entity.ToTable("GatewayInstructions", "Integration.PaymentGateway");

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ExampleText).HasColumnType("character varying");

                entity.Property(e => e.InstructionText).HasColumnType("character varying");

                entity.Property(e => e.ModifiedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Note).HasColumnType("character varying");

                entity.HasOne(d => d.Gateway)
                    .WithMany(p => p.GatewayInstructions)
                    .HasForeignKey(d => d.GatewayId)
                    .HasConstraintName("GatewayInstructions_Gateways_ID_fk");
            });

            modelBuilder.Entity<GatewayResponse>(entity =>
            {
                entity.ToTable("GatewayResponse", "Integration.PaymentGateway");

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.Code)
                    .IsRequired()
                    .HasColumnType("character varying");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.Description)
                    .IsRequired()
                    .HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.Message)
                    .IsRequired()
                    .HasColumnType("character varying");

                entity.Property(e => e.ModifiedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                entity.HasOne(d => d.GatewayResponseType)
                    .WithMany(p => p.GatewayResponses)
                    .HasForeignKey(d => d.GatewayResponseTypeId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("gatewayresponse_gatewayresponsetype_id_fk");

                entity.HasOne(d => d.ResponseStatusType)
                    .WithMany(p => p.GatewayResponses)
                    .HasForeignKey(d => d.ResponseStatusTypeId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("gatewayresponse_gatewayresponsestatustype_id_fk");
            });

            modelBuilder.Entity<GatewayResponseStatusType>(entity =>
            {
                entity.ToTable("GatewayResponseStatusType", "Integration.PaymentGateway");

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.Code)
                    .IsRequired()
                    .HasColumnType("character varying");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasColumnType("character varying");
            });

            modelBuilder.Entity<GatewayResponseType>(entity =>
            {
                entity.ToTable("GatewayResponseType", "Integration.PaymentGateway");

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.Code)
                    .IsRequired()
                    .HasColumnType("character varying");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasColumnType("character varying");

                entity.HasOne(d => d.GatewayEntity)
                    .WithMany(p => p.GatewayResponseTypes)
                    .HasForeignKey(d => d.GatewayEntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("gatewayresponsetype_gatewayentities_id_fk");
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
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.AddressEntitiesId).HasColumnName("AddressEntitiesID");

                entity.Property(e => e.Building).HasMaxLength(500);

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IdentityInfoId).HasColumnName("IdentityInfoID");

                entity.Property(e => e.ModifiedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Street).HasMaxLength(500);

                entity.Property(e => e.Subdivision).HasMaxLength(500);

                entity.Property(e => e.UnitNumber).HasMaxLength(500);

                entity.HasOne(d => d.AddressEntities)
                    .WithMany(p => p.IdentityAddresses)
                    .HasForeignKey(d => d.AddressEntitiesId)
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
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.ModifiedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Name).HasMaxLength(500);
            });

            modelBuilder.Entity<IdentityContact>(entity =>
            {
                entity.ToTable("IdentityContact", "Identity");

                entity.HasIndex(e => e.EntityId, "IX_tbl_IdentityContacts_UCEntitiesID");

                entity.HasIndex(e => e.UserCredentialId, "tbl_identitycontacts_usercredentialid_index");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.ModifiedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.UserCredentialId).HasColumnName("UserCredentialID");

                entity.Property(e => e.Value)
                    .IsRequired()
                    .HasColumnType("character varying");

                entity.HasOne(d => d.Entity)
                    .WithMany(p => p.IdentityContacts)
                    .HasForeignKey(d => d.EntityId)
                    .HasConstraintName("UCEntitiesID");

                entity.HasOne(d => d.Group)
                    .WithMany(p => p.IdentityContacts)
                    .HasForeignKey(d => d.GroupId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("identitycontact_identitycontactgroup__fk");

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
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.ModifiedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<IdentityContactGroup>(entity =>
            {
                entity.ToTable("IdentityContactGroup", "Identity");

                entity.HasIndex(e => e.Guid, "identitycontactgroup_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasColumnType("character varying");
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
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.ApplicationId).HasColumnName("ApplicationID");

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasMaxLength(500)
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IdentityInfoId).HasColumnName("IdentityInfoID");

                entity.Property(e => e.ModifiedAt).HasColumnType("timestamp with time zone");

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

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.Data).HasMaxLength(5000);

                entity.Property(e => e.FavoriteEntityId).HasColumnName("FavoriteEntityID");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");

                entity.Property(e => e.IsEnabled).HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

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
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.ApplicationId).HasColumnName("ApplicationID");

                entity.Property(e => e.BirthDate).HasColumnType("date");

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.FirstName).HasMaxLength(100);

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasMaxLength(500)
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IdentityDescription).HasMaxLength(100);

                entity.Property(e => e.IdentityName).HasMaxLength(100);

                entity.Property(e => e.LastName).HasMaxLength(100);

                entity.Property(e => e.MiddleName).HasMaxLength(100);

                entity.Property(e => e.ModifiedAt).HasColumnType("timestamp with time zone");

                entity.HasOne(d => d.Application)
                    .WithMany(p => p.IdentityInformations)
                    .HasForeignKey(d => d.ApplicationId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("identityinformation_application_id_fk");
            });

            modelBuilder.Entity<IdentityRole>(entity =>
            {
                entity.ToTable("IdentityRole", "Identity");

                entity.HasIndex(e => e.RoleEntityId, "IX_tbl_IdentityRoles_RoleEntityID");

                entity.HasIndex(e => e.UserCredId, "IX_tbl_IdentityRoles_UserCredID");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.ModifiedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.RoleEntityId).HasColumnName("RoleEntityID");

                entity.Property(e => e.RoleExpiration).HasColumnType("timestamp with time zone");

                entity.Property(e => e.UserCredId).HasColumnName("UserCredID");

                entity.HasOne(d => d.RoleEntity)
                    .WithMany(p => p.IdentityRoles)
                    .HasForeignKey(d => d.RoleEntityId)
                    .HasConstraintName("tbl_identityroles_fk_1");

                entity.HasOne(d => d.UserCred)
                    .WithMany(p => p.IdentityRoles)
                    .HasForeignKey(d => d.UserCredId)
                    .HasConstraintName("tbl_identityroles_fk");
            });

            modelBuilder.Entity<IdentityRoleEntity>(entity =>
            {
                entity.ToTable("IdentityRoleEntity", "Identity");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.ApplicationId).HasDefaultValueSql("1");

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.ModifiedAt).HasColumnType("timestamp with time zone");

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

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.Description)
                    .IsRequired()
                    .HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasColumnType("character varying");
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
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Expiry).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IdentityCredId).HasColumnName("IdentityCredID");

                entity.Property(e => e.ModifiedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.StatusUpdatedOn).HasColumnType("time with time zone");

                entity.Property(e => e.Token).HasColumnType("character varying");

                entity.Property(e => e.VerificationTypeId).HasColumnName("VerificationTypeID");

                entity.HasOne(d => d.IdentityCred)
                    .WithMany(p => p.IdentityVerifications)
                    .HasForeignKey(d => d.IdentityCredId)
                    .HasConstraintName("tbl_UserVerifications_AuthID");

                entity.HasOne(d => d.VerificationType)
                    .WithMany(p => p.IdentityVerifications)
                    .HasForeignKey(d => d.VerificationTypeId)
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
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.ModifiedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Name).HasMaxLength(100);
            });

            modelBuilder.Entity<Log>(entity =>
            {
                entity.ToTable("Log", "Audit");

                entity.HasIndex(e => e.ApplicationId, "IX_tbl_ApplicationLogs_AppID");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.ApplicationId).HasColumnName("ApplicationID");

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

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

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.Key)
                    .IsRequired()
                    .HasColumnType("character varying");

                entity.Property(e => e.ModifiedAt).HasColumnType("timestamp with time zone");

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

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Description).HasMaxLength(1000);

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.ModifiedAt).HasColumnType("timestamp with time zone");

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

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.Description).HasMaxLength(500);

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");

                entity.Property(e => e.IsEnabled).HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasColumnType("timestamp with time zone");

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
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.ModifiedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.SessionData).HasMaxLength(2000);

                entity.Property(e => e.SessionEntityId).HasColumnName("SessionEntityID");

                entity.Property(e => e.UserCredentialId).HasColumnName("UserCredentialID");

                entity.HasOne(d => d.SessionEntity)
                    .WithMany(p => p.SessionData)
                    .HasForeignKey(d => d.SessionEntityId)
                    .HasConstraintName("tbl_sessiondata_fk");

                entity.HasOne(d => d.UserCredential)
                    .WithMany(p => p.SessionData)
                    .HasForeignKey(d => d.UserCredentialId)
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
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.ModifiedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<Wallet>(entity =>
            {
                entity.ToTable("Wallet", "Wallet");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.Balance).HasPrecision(24, 8);

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");

                entity.Property(e => e.ModifiedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                entity.HasOne(d => d.IdentityCredential)
                    .WithMany(p => p.Wallets)
                    .HasForeignKey(d => d.IdentityCredentialId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("tbl_Wallets_IdentityCredentialId_fkey");

                entity.HasOne(d => d.WalletEntity)
                    .WithMany(p => p.Wallets)
                    .HasForeignKey(d => d.WalletEntityId)
                    .HasConstraintName("tbl_Wallets_WalletEntityId_fkey");
            });

            modelBuilder.Entity<WalletAddress>(entity =>
            {
                entity.ToTable("WalletAddress", "Wallet");

                entity.HasIndex(e => e.Guid, "tbl_userwalletaddress_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.Address).HasMaxLength(512);

                entity.Property(e => e.Balance).HasPrecision(18, 10);

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.ModifiedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.Remarks).HasMaxLength(100);

                entity.HasOne(d => d.Wallet)
                    .WithMany(p => p.WalletAddresses)
                    .HasForeignKey(d => d.WalletId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("walletaddress_wallet_id_fk");
            });

            modelBuilder.Entity<WalletEntity>(entity =>
            {
                entity.ToTable("WalletEntity", "Wallet");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.ApplicationId).HasDefaultValueSql("1");

                entity.Property(e => e.Code)
                    .IsRequired()
                    .HasMaxLength(9);

                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.CurrencyEntityId).HasColumnName("CurrencyEntityID");

                entity.Property(e => e.Desc).HasMaxLength(500);

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.LastChanged).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedAt).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.HasOne(d => d.Application)
                    .WithMany(p => p.WalletEntities)
                    .HasForeignKey(d => d.ApplicationId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("tbl_walletentities_tbl_applications_id_fk");

                entity.HasOne(d => d.CurrencyEntity)
                    .WithMany(p => p.WalletEntities)
                    .HasForeignKey(d => d.CurrencyEntityId)
                    .HasConstraintName("CurrencyID");
            });

            modelBuilder.Entity<WalletTransaction>(entity =>
            {
                entity.ToTable("WalletTransaction", "Wallet");

                entity.HasIndex(e => e.Guid, "tbl_userwallettransaction_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.Amount).HasPrecision(24, 8);

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasMaxLength(10000);

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.ModifiedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.PreviousBalance).HasPrecision(24, 8);

                entity.Property(e => e.Remarks).HasMaxLength(10000);

                entity.Property(e => e.RunningBalance).HasPrecision(24, 8);

                entity.HasOne(d => d.IdentityCredential)
                    .WithMany(p => p.WalletTransactions)
                    .HasForeignKey(d => d.IdentityCredentialId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("UserAuthID");

                entity.HasOne(d => d.SourceUserWallet)
                    .WithMany(p => p.WalletTransactionSourceUserWallets)
                    .HasForeignKey(d => d.SourceUserWalletId)
                    .HasConstraintName("SourceUserWalletId");

                entity.HasOne(d => d.TargetUserWallet)
                    .WithMany(p => p.WalletTransactionTargetUserWallets)
                    .HasForeignKey(d => d.TargetUserWalletId)
                    .HasConstraintName("tbl_userwallettransaction_tbl_userwallet_id_fk");
            });

            modelBuilder.Entity<WithdrawalRequest>(entity =>
            {
                entity.ToTable("WithdrawalRequest", "Wallet");

                entity.HasIndex(e => e.Guid, "tbl_userwithdrawalrequest_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn()
                    .HasIdentityOptions(null, null, null, 2147483647L, null, null);

                entity.Property(e => e.Address).HasMaxLength(10000);

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .IsRequired()
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.ModifiedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.Remarks).HasColumnType("character varying");

                entity.Property(e => e.TotalAmount).HasPrecision(18, 10);

                entity.HasOne(d => d.IdentityCredential)
                    .WithMany(p => p.WithdrawalRequests)
                    .HasForeignKey(d => d.IdentityCredentialId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("IdentityCredentialId");

                entity.HasOne(d => d.SourceWalletType)
                    .WithMany(p => p.WithdrawalRequestSourceWalletTypes)
                    .HasForeignKey(d => d.SourceWalletTypeId)
                    .HasConstraintName("SourceWalletTypeId");

                entity.HasOne(d => d.TargetCurrency)
                    .WithMany(p => p.WithdrawalRequestTargetCurrencies)
                    .HasForeignKey(d => d.TargetCurrencyId)
                    .HasConstraintName("TargetCurrencyId");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
