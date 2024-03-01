using IdentityServer.Domain.Generic;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Generic.Contracts;

namespace XFramework.Domain.Contexts;

public partial class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public virtual DbSet<AddressBarangay> AddressBarangays { get; set; }

    public virtual DbSet<AddressCity> AddressCities { get; set; }

    public virtual DbSet<AddressCountry> AddressCountries { get; set; }

    public virtual DbSet<AddressProvince> AddressProvinces { get; set; }

    public virtual DbSet<AddressRegion> AddressRegions { get; set; }

    public virtual DbSet<Tenant> Tenants { get; set; }

    public virtual DbSet<AuthorizationLog> AuthorizationLogs { get; set; }

    public virtual DbSet<BusinessPackage> BusinessPackages { get; set; }

    public virtual DbSet<BusinessPackageInclusion> BusinessPackageInclusions { get; set; }

    public virtual DbSet<BusinessPackageInclusionType> BusinessPackageInclusionTypes { get; set; }

    public virtual DbSet<BusinessPackageType> BusinessPackageTypes { get; set; }

    public virtual DbSet<BusinessPackageUpgradeTransaction> BusinessPackageUpgradeTransactions { get; set; }


    public virtual DbSet<CommunityConnection> CommunityConnections { get; set; }

    public virtual DbSet<CommunityConnectionType> CommunityConnectionTypes { get; set; }

    public virtual DbSet<CommunityContent> CommunityContents { get; set; }

    public virtual DbSet<CommunityContentType> CommunityContentTypes { get; set; }

    public virtual DbSet<CommunityContentFile> CommunityContentFiles { get; set; }

    public virtual DbSet<CommunityContentReaction> CommunityContentReactions { get; set; }

    public virtual DbSet<CommunityContentReactionType> CommunityContentReactionTypes { get; set; }

    public virtual DbSet<CommunityIdentity> CommunityIdentities { get; set; }

    public virtual DbSet<CommunityIdentityType> CommunityIdentityTypes { get; set; }

    public virtual DbSet<CommunityIdentityFile> CommunityIdentityFiles { get; set; }

    public virtual DbSet<CommunityIdentityFileType> CommunityIdentityFileTypes { get; set; }

    public virtual DbSet<CurrencyType> CurrencyTypes { get; set; }

    public virtual DbSet<DepositRequest> DepositRequests { get; set; }
    
    public virtual DbSet<ExchangeRate> ExchangeRates { get; set; }

    public virtual DbSet<PaymentGateway> PaymentGateways { get; set; }

    public virtual DbSet<PaymentGatewayCategory> PaymentGatewayCategories { get; set; }

    public virtual DbSet<PaymentGatewayEndpoint> PaymentGatewayEndpoints { get; set; }

    public virtual DbSet<PaymentGatewayType> PaymentGatewayTypes { get; set; }

    public virtual DbSet<PaymentGatewayInstruction> PaymentGatewayInstructions { get; set; }
    
    public virtual DbSet<PaymentGatewayResponse> PaymentGatewayResponses { get; set; }

    public virtual DbSet<PaymentGatewayResponseStatusType> PaymentGatewayResponseStatusTypes { get; set; }

    public virtual DbSet<PaymentGatewayResponseType> PaymentGatewayResponseTypes { get; set; }

    public virtual DbSet<IdentityAddress> IdentityAddresses { get; set; }

    public virtual DbSet<IdentityAddressType> IdentityAddressTypes { get; set; }

    public virtual DbSet<IdentityContact> IdentityContacts { get; set; }

    public virtual DbSet<IdentityContactType> IdentityContactTypes { get; set; }

    public virtual DbSet<IdentityContactGroup> IdentityContactGroups { get; set; }

    public virtual DbSet<IdentityCredential> IdentityCredentials { get; set; }

    public virtual DbSet<IdentityFavorite> IdentityFavorites { get; set; }

    public virtual DbSet<IdentityInformation> IdentityInformations { get; set; }

    public virtual DbSet<IdentityRole> IdentityRoles { get; set; }

    public virtual DbSet<IdentityRoleType> IdentityRoleTypes { get; set; }

    public virtual DbSet<IdentityRoleTypeGroup> IdentityRoleTypeGroups { get; set; }

    public virtual DbSet<IdentityVerification> IdentityVerifications { get; set; }

    public virtual DbSet<IdentityVerificationType> IdentityVerificationTypes { get; set; }
    
    public virtual DbSet<IncomeType> IncomeTypes { get; set; }
    
    public virtual DbSet<IncomeTransaction> IncomeTransactions { get; set; }
    
    public virtual DbSet<Message> Messages { get; set; }

    public virtual DbSet<MessageDelivery> MessageDeliveries { get; set; }

    public virtual DbSet<MessageDeliveryType> MessageDeliveryTypes { get; set; }

    public virtual DbSet<MessageDirect> MessageDirects { get; set; }

    public virtual DbSet<MessageFile> MessageFiles { get; set; }

    public virtual DbSet<MessageReaction> MessageReactions { get; set; }

    public virtual DbSet<MessageReactionType> MessageReactionTypes { get; set; }

    public virtual DbSet<MessageThread> MessageThreads { get; set; }

    public virtual DbSet<MessageThreadType> MessageThreadTypes { get; set; }

    public virtual DbSet<MessageThreadMember> MessageThreadMembers { get; set; }

    public virtual DbSet<MessageThreadMemberGroup> MessageThreadMemberGroups { get; set; }

    public virtual DbSet<MessageThreadMemberRole> MessageThreadMemberRoles { get; set; }

    public virtual DbSet<MessageType> MessageTypes { get; set; }

    public virtual DbSet<MetaData> MetaData { get; set; }

    public virtual DbSet<MetaDataType> MetaDataTypes { get; set; }

    public virtual DbSet<MetaDataTypeGroup> MetaDataTypeGroups { get; set; }
    
    public virtual DbSet<RegistryConfiguration> RegistryConfigurations { get; set; }

    public virtual DbSet<RegistryConfigurationGroup> RegistryConfigurationGroups { get; set; }

    public virtual DbSet<RegistryFavoriteType> RegistryFavoriteTypes { get; set; }

    public virtual DbSet<Session> Session { get; set; }

    public virtual DbSet<SessionType> SessionTypes { get; set; }

    public virtual DbSet<StorageFile> StorageFiles { get; set; }

    public virtual DbSet<StorageFileType> StorageFileTypes { get; set; }

    public virtual DbSet<StorageFileIdentifier> StorageFileIdentifiers { get; set; }

    public virtual DbSet<StorageFileIdentifierGroup> StorageFileIdentifierGroups { get; set; }

    public virtual DbSet<Subscription> Subscriptions { get; set; }

    public virtual DbSet<SubscriptionType> SubscriptionTypes { get; set; }
    
    public virtual DbSet<Wallet> Wallets { get; set; }

    public virtual DbSet<WalletAddress> WalletAddresses { get; set; }

    public virtual DbSet<WalletType> WalletTypes { get; set; }

    public virtual DbSet<WalletTransaction> WalletTransactions { get; set; }

    public virtual DbSet<WithdrawalRequest> WithdrawalRequests { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("uuid-ossp");

        modelBuilder.Entity<AddressBarangay>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("addresses_refbrgy_pk");

            entity.ToTable("AddressBarangay", "GeoLocation");

            entity.HasIndex(e => e.Code, "addresses_refbrgy_code_uindex").IsUnique();


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert


            entity.HasOne(d => d.CityCode).WithMany(p => p.AddressBarangays)
                .HasPrincipalKey(p => p.Code)
                .HasForeignKey(d => d.CityCodeId)
                .HasConstraintName("tbl_addressbarangay_tbl_addresscity_code_fk");
        });

        modelBuilder.Entity<AddressCity>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_addresscity_pk");

            entity.ToTable("AddressCity", "GeoLocation");

            entity.HasIndex(e => e.Code, "tbl_addresscity_code_uindex").IsUnique();


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert


            entity.HasOne(d => d.ProvCode).WithMany(p => p.AddressCities)
                .HasPrincipalKey(p => p.Code)
                .HasForeignKey(d => d.ProvCodeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("tbl_addresscity_tbl_addressprovince_code_fk");
        });

        modelBuilder.Entity<AddressCountry>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_AddressCountry_pkey");

            entity.ToTable("AddressCountry", "GeoLocation");


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CurrencyId).HasColumnName("CurrencyID");

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


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert


            entity.HasOne(d => d.RegCode).WithMany(p => p.AddressProvinces)
                .HasPrincipalKey(p => p.Code)
                .HasForeignKey(d => d.RegCodeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("tbl_addressprovince_tbl_addressregions_code_fk");
        });

        modelBuilder.Entity<AddressRegion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_addressregions_pk");

            entity.ToTable("AddressRegion", "GeoLocation");

            entity.HasIndex(e => e.Code, "tbl_addressregions_code_uindex").IsUnique();


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CountryId).HasColumnName("CountryID");


            entity.HasOne(d => d.Country).WithMany(p => p.AddressRegions)
                .HasForeignKey(d => d.CountryId)
                .HasConstraintName("tbl_addressregions_tbl_addresscountry_id_fk");
        });

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_Application");

            entity.ToTable("Application", "Application");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.Name).HasColumnType("character varying");
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.ParentTenantId).HasColumnName("ParentAppID");
            entity.Property(e => e.Version).HasPrecision(6, 3);
        });

        modelBuilder.Entity<AuthorizationLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityAuthorizationLogs");

            entity.ToTable("AuthorizationLog", "Audit");

            entity.HasIndex(e => e.CredentialId, "IX_tbl_IdentityAuthorizationLogs_CredentialID");


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.DeviceName).HasMaxLength(50);

            entity.Property(e => e.Ipaddress)
                .HasMaxLength(18)
                .HasColumnName("IPAddress");
            entity.Property(e => e.LoginSource).HasMaxLength(50);
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.IdentityCredentials).WithMany(p => p.AuthorizationLogs)
                .HasForeignKey(d => d.CredentialId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("tbl_userauthhistory_fk");
        });

        
        modelBuilder.Entity<BusinessPackage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_BusinessPackages_pkey");

            entity.ToTable("BusinessPackage", "BusinessPackage");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert

            entity.Property(e => e.CancellationDate).HasColumnType("timestamp without time zone");
            entity.Property(e => e.CodeHash).HasMaxLength(300);
            entity.Property(e => e.CodeString).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Remarks).HasMaxLength(1000);
            entity.Property(e => e.UserDepositRequestId).HasColumnName("UserDepositRequestID");

            entity.HasOne(d => d.ConsumedBy).WithMany(p => p.BusinessPackageConsumedByNavigations)
                .HasForeignKey(d => d.ConsumedById)
                .HasConstraintName("tbl_userbusinesspackage_fk");

            entity.HasOne(d => d.Credential).WithMany(p => p.BusinessPackageIdentityCredentials)
                .HasForeignKey(d => d.CredentialId)
                .HasConstraintName("BusinessPackage_CredentialId");
            
            entity.HasOne(d => d.Type).WithMany(p => p.BusinessPackages)
                .HasForeignKey(d => d.TypeId)
                .HasConstraintName("BusinessPackage_TypeId");

            entity.HasOne(d => d.RecipientIdentityCredential)
                .WithMany(p => p.BusinessPackageRecipientIdentityCredentials)
                .HasForeignKey(d => d.RecipientCredentialId)
                .HasConstraintName("tbl_userbusinesspackage_tbl_userauth_id_fk");

            entity.HasOne(d => d.UserDepositRequest).WithMany(p => p.BusinessPackages)
                .HasForeignKey(d => d.UserDepositRequestId)
                .HasConstraintName("DepositRequestId");
        });

        modelBuilder.Entity<BusinessPackageInclusion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_BusinessPackageInclusions_pkey");

            entity.ToTable("BusinessPackageInclusion", "BusinessPackage");
            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.BusinessPackageId).HasColumnName("BusinessPackageID");

            entity.Property(e => e.TypeId).HasColumnName("InclusionTypeID");
            entity.Property(e => e.StringValue).HasMaxLength(100);
            entity.Property(e => e.Value).HasPrecision(16, 5);

            entity.HasOne(d => d.Type).WithMany(p => p.BusinessPackageInclusions)
                .HasForeignKey(d => d.TypeId)
                .HasConstraintName("BusinessPackageInclusions_TypeID");
            
            entity.HasOne(d => d.BusinessPackage).WithMany(p => p.BusinessPackageInclusions)
                .HasForeignKey(d => d.BusinessPackageId)
                .HasConstraintName("BusinessPackageInclusions_BusinessPackageId");
        });

        modelBuilder.Entity<BusinessPackageInclusionType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_BusinessPackageInclusionsType_pkey");

            entity.ToTable("BusinessPackageInclusionsType", "BusinessPackage");
            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.Code).HasMaxLength(5);
            entity.Property(e => e.Description).HasMaxLength(1000);

            entity.Property(e => e.IconImage).HasColumnType("character varying");
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Unit).HasColumnType("character varying");
        });

        modelBuilder.Entity<BusinessPackageType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_BusinessPackageType_pkey");

            entity.ToTable("BusinessPackageType", "BusinessPackage");
            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.Description).HasMaxLength(500);

            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<BusinessPackageUpgradeTransaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("businesspackageupgradetransactions_pk");

            entity.ToTable("BusinessPackageUpgradeTransaction", "BusinessPackage");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.CurrentBusinessPackage)
                .WithMany(p => p.BusinessPackageUpgradeTransactions)
                .HasForeignKey(d => d.CurrentBusinessPackageId)
                .HasConstraintName("ubput_tbl_businesspackage_id_fk");

            entity.HasOne(d => d.Credential).WithMany(p => p.BusinessPackageUpgradeTransactions)
                .HasForeignKey(d => d.CredentialId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ubput_tbl_identitycredentials_id_fk");
        });

        modelBuilder.Entity<CommissionDeductionRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_usercommissiondeductionrequest_pk");

            entity.ToTable("CommissionDeductionRequest", "Affiliate");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.Balance).HasPrecision(18, 10);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.DeductionCharge).HasPrecision(18, 10);

            entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.PrincipalAmount).HasPrecision(18, 10);

            entity.HasOne(d => d.BusinessPackage).WithMany(p => p.CommissionDeductionRequests)
                .HasForeignKey(d => d.BusinessPackageId)
                .HasConstraintName("tbl_ucdr_tbl_userbusinesspackage_id_fk");
        });

        modelBuilder.Entity<CommunityConnection>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("socialmediaconnection_pk");

            entity.ToTable("CommunityConnection", "Community");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Type).WithMany(p => p.CommunityConnections)
                .HasForeignKey(d => d.TypeId)
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

        modelBuilder.Entity<CommunityConnectionType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("socialmediaconnectionentity_pk");

            entity.ToTable("CommunityConnectionType", "Community");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<CommunityContent>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("socialmediacontent_pk");

            entity.ToTable("CommunityContent", "Community");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Text).HasColumnType("character varying");
            entity.Property(e => e.Title).HasColumnType("character varying");

            entity.HasOne(d => d.CommunityGroup).WithMany(p => p.CommunityContentCommunityGroups)
                .HasForeignKey(d => d.CommunityGroupId)
                .HasConstraintName("communitycontent_communityidentity_id_fk");

            entity.HasOne(d => d.Type).WithMany(p => p.CommunityContents)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("socialmediacontent_socialmediacontententity_id_fk");

            entity.HasOne(d => d.ParentContent).WithMany(p => p.InverseParentContent)
                .HasForeignKey(d => d.ParentContentId)
                .HasConstraintName("socialmediacontent_socialmediacontent_id_fk");

            entity.HasOne(d => d.SocialMediaIdentity).WithMany(p => p.CommunityContentSocialMediaIdentities)
                .HasForeignKey(d => d.SocialMediaIdentityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("socialmediacontent_socialmediaidentity_id_fk");
        });

        modelBuilder.Entity<CommunityContentType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("socialmediacontententity_pk");

            entity.ToTable("CommunityContentType", "Community");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<CommunityContentFile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("socialmediacontentfiles_pk");

            entity.ToTable("CommunityContentFiles", "Community");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Content).WithMany(p => p.CommunityContentFiles)
                .HasForeignKey(d => d.ContentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("socialmediacontentfiles_socialmediacontent_id_fk");

            entity.HasOne(d => d.Storage).WithMany(p => p.CommunityContentFiles)
                .HasForeignKey(d => d.StorageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("socialmediacontentfiles_storagefile_id_fk");
        });

        modelBuilder.Entity<CommunityContentReaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("socialmediacontentreaction_pk");

            entity.ToTable("CommunityContentReaction", "Community");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Content).WithMany(p => p.CommunityContentReactions)
                .HasForeignKey(d => d.ContentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("socialmediacontentreaction_socialmediacontent_id_fk");

            entity.HasOne(d => d.Type).WithMany(p => p.CommunityContentReactions)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("socialmediacontentreaction_contentreactionentity_id_fk");

            entity.HasOne(d => d.SocialMediaIdentity).WithMany(p => p.CommunityContentReactions)
                .HasForeignKey(d => d.SocialMediaIdentityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("socialmediacontentreaction_socialmediaidentity_id_fk");
        });

        modelBuilder.Entity<CommunityContentReactionType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("socialmediacontentreactionentity_pk");

            entity.ToTable("CommunityContentReactionType", "Community");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Emoji).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<CommunityIdentity>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("socialidentity_pk");

            entity.ToTable("CommunityIdentity", "Community");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.Alias).HasColumnType("character varying");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.HandleName).HasColumnType("character varying");
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.LastActive).HasDefaultValueSql("now()");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Tagline).HasColumnType("character varying");

            entity.HasOne(d => d.Type).WithMany(p => p.CommunityIdentities)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("communityidentity_communityidentityentity_id_fk");

            entity.HasOne(d => d.Credential).WithMany(p => p.CommunityIdentities)
                .HasForeignKey(d => d.CredentialId)
                .HasConstraintName("socialidentity_identitycredential_id_fk");
        });

        modelBuilder.Entity<CommunityIdentityType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("communityidentityentity_pk");

            entity.ToTable("CommunityIdentityType", "Community");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<CommunityIdentityFile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("communityidentityfiles_pk");

            entity.ToTable("CommunityIdentityFile", "Community");


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Type).WithMany(p => p.CommunityIdentityFiles)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("communityidentityfile_communityidentityfileentity_id_fk");

            entity.HasOne(d => d.Identity).WithMany(p => p.CommunityIdentityFiles)
                .HasForeignKey(d => d.IdentityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("communityidentityfiles_communityidentity_id_fk");

            entity.HasOne(d => d.Storage).WithMany(p => p.CommunityIdentityFiles)
                .HasForeignKey(d => d.StorageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("communityidentityfiles_storagefile_id_fk");
        });

        modelBuilder.Entity<CommunityIdentityFileType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("communityidentityfileentity_pk");

            entity.ToTable("CommunityIdentityFileType", "Community");


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<CurrencyType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_currency_pk");

            entity.ToTable("CurrencyType", "Finance");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CurrencyIsoCode3).HasMaxLength(4);
            entity.Property(e => e.Description).HasMaxLength(500);

            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasMaxLength(256);
        });

        modelBuilder.Entity<DepositRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_DepositRequests_pkey");

            entity.ToTable("DepositRequest", "Wallet");


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.Address).HasMaxLength(10000);
            entity.Property(e => e.Amount).HasPrecision(18, 10);
            entity.Property(e => e.ConvenienceFee).HasPrecision(18, 10);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Discount).HasPrecision(18, 10);

            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.RawRequestData).HasMaxLength(10000);
            entity.Property(e => e.RawResponseData).HasMaxLength(5000);
            entity.Property(e => e.ReferenceNo).HasMaxLength(35);
            entity.Property(e => e.Remarks).HasMaxLength(10000);
            entity.Property(e => e.SystemFee).HasPrecision(18, 10);

            entity.HasOne(d => d.PaymentGateway).WithMany(p => p.DepositRequests)
                .HasForeignKey(d => d.GatewayId)
                .HasConstraintName("DepositRequest_Gateway_ID_fk");

            entity.HasOne(d => d.Credential).WithMany(p => p.DepositRequests)
                .HasForeignKey(d => d.CredentialId)
                .HasConstraintName("DepositRequest_CredentialId");

            entity.HasOne(d => d.SourceCurrency).WithMany(p => p.DepositRequests)
                .HasForeignKey(d => d.SourceCurrencyId)
                .HasConstraintName("SourceCurrencyId");

            entity.HasOne(d => d.WalletType).WithMany(p => p.DepositRequests)
                .HasForeignKey(d => d.WalletTypeId)
                .HasConstraintName("DepositRequest_WalletTypeId");
        });
        
        modelBuilder.Entity<ExchangeRate>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_ExchangeRate_pkey");

            entity.ToTable("ExchangeRate", "Finance");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Fee).HasPrecision(18, 10);

            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.SourceCurrencyTypeId).HasColumnName("SourceCurrencyTypeID");
            entity.Property(e => e.TargetCurrencyTypeId).HasColumnName("TargetCurrencyTypeID");
            entity.Property(e => e.Value).HasPrecision(18, 10);

            entity.HasOne(d => d.SourceCurrencyType).WithMany(p => p.ExchangeRateSourceCurrencyTypes)
                .HasForeignKey(d => d.SourceCurrencyTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("SourceCurrencyID");

            entity.HasOne(d => d.TargetCurrencyType).WithMany(p => p.ExchangeRateTargetCurrencyTypes)
                .HasForeignKey(d => d.TargetCurrencyTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("TargetCurrencyID");
        });

        modelBuilder.Entity<PaymentGateway>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_gateways_pk");

            entity.ToTable("Gateway", "Integration.PaymentGateway");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.ConvenienceFee).HasPrecision(10, 2);
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.Discount)
                .HasPrecision(10, 2)
                .HasDefaultValueSql("0");
            entity.Property(e => e.GatewayCategoryId).HasColumnName("GatewayCategoryID");

            entity.Property(e => e.Image).HasColumnType("character varying");
            entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");
            entity.Property(e => e.IsEnabled).HasDefaultValueSql("true");
            entity.Property(e => e.Name).HasColumnType("character varying");
            entity.Property(e => e.ServiceCharge).HasPrecision(3, 2);

            entity.HasOne(d => d.PaymentGatewayCategory).WithMany(p => p.Gateways)
                .HasForeignKey(d => d.GatewayCategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("tbl_gateways_tbl_gatewaycategories_id_fk");

            entity.HasOne(d => d.ProviderEndpoint).WithMany(p => p.Gateways)
                .HasForeignKey(d => d.ProviderEndpointId)
                .HasConstraintName("tbl_gateways_tbl_providerendpoints_id_fk");
        });

        modelBuilder.Entity<PaymentGatewayCategory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_gatewaycategories_pk");

            entity.ToTable("GatewayCategory", "Integration.PaymentGateway");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsDeleted)
                .HasDefaultValueSql("false")
                .HasColumnName("isDeleted");
            entity.Property(e => e.IsEnabled)
                .HasDefaultValueSql("true")
                .HasColumnName("isEnabled");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<PaymentGatewayEndpoint>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_gatewayendpoints_pk");

            entity.ToTable("GatewayEndpoint", "Integration.PaymentGateway");


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.BaseUrlEndpoint).HasMaxLength(100);
            entity.Property(e => e.GatewayId).HasColumnName("GatewayID");

            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.UrlEndpoint).HasMaxLength(100);

            entity.HasOne(d => d.Gateway).WithMany(p => p.GatewayEndpoints)
                .HasForeignKey(d => d.GatewayId)
                .HasConstraintName("tbl_gatewayendpoints_tbl_gatewayType_id_fk");
        });

        modelBuilder.Entity<PaymentGatewayType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_gatewayType_pk");

            entity.ToTable("GatewayType", "Integration.PaymentGateway");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsDeleted)
                .HasDefaultValueSql("false")
                .HasColumnName("isDeleted");
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true")
                .HasColumnName("isEnabled");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<PaymentGatewayInstruction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("GatewayInstructions_pk");

            entity.ToTable("GatewayInstructions", "Integration.PaymentGateway");

            entity.Property(e => e.ExampleText).HasColumnType("character varying");
            entity.Property(e => e.InstructionText).HasColumnType("character varying");
            entity.Property(e => e.Note).HasColumnType("character varying");

            entity.HasOne(d => d.Gateway).WithMany(p => p.GatewayInstructions)
                .HasForeignKey(d => d.GatewayId)
                .HasConstraintName("GatewayInstructions_Gateways_ID_fk");
        });

        modelBuilder.Entity<PaymentGatewayResponse>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("gatewayresponse_pk");

            entity.ToTable("GatewayResponse", "Integration.PaymentGateway");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.Code).HasColumnType("character varying");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.Message).HasColumnType("character varying");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.PaymentGatewayResponseType).WithMany(p => p.GatewayResponses)
                .HasForeignKey(d => d.GatewayResponseTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("gatewayresponse_gatewayresponsetype_id_fk");

            entity.HasOne(d => d.ResponseStatusType).WithMany(p => p.GatewayResponses)
                .HasForeignKey(d => d.ResponseStatusTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("gatewayresponse_gatewayresponsestatustype_id_fk");
        });

        modelBuilder.Entity<PaymentGatewayResponseStatusType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("gatewayresponsestatustype_pk");

            entity.ToTable("GatewayResponseStatusType", "Integration.PaymentGateway");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.Code).HasColumnType("character varying");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<PaymentGatewayResponseType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("gatewayresponsetype_pk");

            entity.ToTable("GatewayResponseType", "Integration.PaymentGateway");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.Code).HasColumnType("character varying");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.PaymentGatewayType).WithMany(p => p.GatewayResponseTypes)
                .HasForeignKey(d => d.GatewayTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("gatewayresponsetype_gatewayTypes_id_fk");
        });

        modelBuilder.Entity<IdentityAddress>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityAddresses");

            entity.ToTable("IdentityAddress", "Identity");

            entity.HasIndex(e => e.AddressTypeId, "IX_tbl_IdentityAddresses_AddressTypeID");

            entity.HasIndex(e => e.IdentityInfoId, "IX_tbl_IdentityAddresses_UserInfoID");
            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.AddressTypeId).HasColumnName("AddressTypeID");
            entity.Property(e => e.Building).HasMaxLength(500);

            entity.Property(e => e.IdentityInfoId).HasColumnName("IdentityInfoID");
            entity.Property(e => e.Street).HasMaxLength(500);
            entity.Property(e => e.Subdivision).HasMaxLength(500);
            entity.Property(e => e.UnitNumber).HasMaxLength(500);

            entity.HasOne(d => d.AddressType).WithMany(p => p.IdentityAddresses)
                .HasForeignKey(d => d.AddressTypeId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("AddressTypeID");

            entity.HasOne(d => d.Barangay).WithMany(p => p.IdentityAddresses)
                .HasForeignKey(d => d.BarangayId)
                .HasConstraintName("tbl_identityaddresses__id_fk_brgy");

            entity.HasOne(d => d.City).WithMany(p => p.IdentityAddresses)
                .HasForeignKey(d => d.CityId)
                .HasConstraintName("tbl_identityaddresses__id_fk_city");

            entity.HasOne(d => d.Country).WithMany(p => p.IdentityAddresses)
                .HasForeignKey(d => d.CountryId)
                .HasConstraintName("tbl_identityaddresses_tbl_addresscountry__fk");

            entity.HasOne(d => d.IdentityInfo).WithMany(p => p.IdentityAddresses)
                .HasForeignKey(d => d.IdentityInfoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("UserInfoID");

            entity.HasOne(d => d.Province).WithMany(p => p.IdentityAddresses)
                .HasForeignKey(d => d.ProvinceId)
                .HasConstraintName("tbl_identityaddresses__id_fk_province");

            entity.HasOne(d => d.Region).WithMany(p => p.IdentityAddresses)
                .HasForeignKey(d => d.RegionId)
                .HasConstraintName("tbl_identityaddresses__id_fk");
        });

        modelBuilder.Entity<IdentityAddressType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityAddressType");

            entity.ToTable("IdentityAddressType", "Identity");


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert

            entity.Property(e => e.Name).HasMaxLength(500);
        });

        modelBuilder.Entity<IdentityContact>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityContacts");

            entity.ToTable("IdentityContact", "Identity");

            entity.HasIndex(e => e.TypeId, "IX_tbl_IdentityContacts_TypeID");

            entity.HasIndex(e => e.CredentialId, "tbl_identitycontacts_CredentialID_index");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert

            entity.Property(e => e.CredentialId).HasColumnName("CredentialID");
            entity.Property(e => e.Value).HasColumnType("character varying");

            entity.HasOne(d => d.Type).WithMany(p => p.IdentityContacts)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("IdentityContact_TypeID");

            entity.HasOne(d => d.Group).WithMany(p => p.IdentityContacts)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("identitycontact_identitycontactgroup__fk");

            entity.HasOne(d => d.Credential).WithMany(p => p.IdentityContacts)
                .HasForeignKey(d => d.CredentialId)
                .HasConstraintName("tbl_identitycontacts___fk");
        });

        modelBuilder.Entity<IdentityContactType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityContactType");

            entity.ToTable("IdentityContactType", "Identity");


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert

            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<IdentityContactGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("identitycontactgroup_pk");

            entity.ToTable("IdentityContactGroup", "Identity");


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

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


            entity.HasIndex(e => e.UserName, "tbl_identitycredentials_un").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.TenantId).HasColumnName("TenantId");

            entity.Property(e => e.IdentityInfoId).HasColumnName("IdentityInfoID");
            entity.Property(e => e.Token).HasMaxLength(512);
            entity.Property(e => e.UserAlias).HasMaxLength(100);
            entity.Property(e => e.UserName).HasMaxLength(100);

            entity.HasOne(d => d.Tenant).WithMany(p => p.IdentityCredentials)
                .HasForeignKey(d => d.TenantId)
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

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Data).HasMaxLength(5000);
            entity.Property(e => e.FavoriteTypeId).HasColumnName("FavoriteTypeID");

            entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");
            entity.Property(e => e.IsEnabled).HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.FavoriteType).WithMany(p => p.IdentityFavorites)
                .HasForeignKey(d => d.FavoriteTypeId)
                .HasConstraintName("tbl_userfavorites_tbl_favoriteType_id_fk");

            entity.HasOne(d => d.Credential).WithMany(p => p.IdentityFavorites)
                .HasForeignKey(d => d.CredentialId)
                .HasConstraintName("tbl_userfavorites_tbl_identitycredentials_id_fk");
        });

        modelBuilder.Entity<IdentityInformation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityInfo");

            entity.ToTable("IdentityInformation", "Identity");


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.TenantId).HasColumnName("TenantId");
            entity.Property(e => e.FirstName).HasMaxLength(100);

            entity.Property(e => e.IdentityDescription).HasMaxLength(100);
            entity.Property(e => e.IdentityName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.MiddleName).HasMaxLength(100);

            entity.HasOne(d => d.Tenant).WithMany(p => p.IdentityInformations)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("identityinformation_application_id_fk");
        });

        modelBuilder.Entity<IdentityRole>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityRoles");

            entity.ToTable("IdentityRole", "Identity");

            entity.HasIndex(e => e.TypeId, "IX_tbl_IdentityRoles_RoleTypeID");

            entity.HasIndex(e => e.CredentialId, "IX_tbl_IdentityRoles_UserCredID");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert

            entity.Property(e => e.TypeId).HasColumnName("RoleTypeID");
            entity.Property(e => e.CredentialId).HasColumnName("UserCredID");

            entity.HasOne(d => d.Type).WithMany(p => p.IdentityRoles)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("tbl_identityroles_fk_1");

            entity.HasOne(d => d.Credential).WithMany(p => p.IdentityRoles)
                .HasForeignKey(d => d.CredentialId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("tbl_identityroles_fk");
        });

        modelBuilder.Entity<IdentityRoleType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityRoleType");

            entity.ToTable("IdentityRoleType", "Identity");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.TenantId);

            entity.Property(e => e.Name).HasMaxLength(100);

            entity.HasOne(d => d.Tenant).WithMany(p => p.IdentityRoleTypes)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("tbl_identityroleTypes_tbl_applications_id_fk");

            entity.HasOne(d => d.Group).WithMany(p => p.IdentityRoleTypes)
                .HasForeignKey(d => d.GroupId)
                .HasConstraintName("identityroleentity_identityroleentitygroup_id_fk");
        });

        modelBuilder.Entity<IdentityRoleTypeGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("identityroleentitygroup_pk");

            entity.ToTable("IdentityRoleEntityGroup", "Identity");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");

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

            entity.HasIndex(e => e.CredentialId, "IX_tbl_IdentityVerifications_CredentialID");

            entity.HasIndex(e => e.VerificationTypeId, "IX_tbl_IdentityVerifications_VerificationTypeID");


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert

            entity.Property(e => e.CredentialId).HasColumnName("CredentialID");
            entity.Property(e => e.StatusUpdatedOn).HasColumnType("time with time zone");
            entity.Property(e => e.Token).HasColumnType("character varying");
            entity.Property(e => e.VerificationTypeId).HasColumnName("VerificationTypeID");

            entity.HasOne(d => d.Credential).WithMany(p => p.IdentityVerifications)
                .HasForeignKey(d => d.CredentialId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("tbl_UserVerifications_AuthID");

            entity.HasOne(d => d.VerificationType).WithMany(p => p.IdentityVerifications)
                .HasForeignKey(d => d.VerificationTypeId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("tbl_UserVerifications_VerificationTypeID");
        });

        modelBuilder.Entity<IdentityVerificationType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_VerificationType");

            entity.ToTable("IdentityVerificationType", "Identity");


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert

            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<IncomeType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_IncomeType_pkey");

            entity.ToTable("IncomeType", "Income");


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IncomeTypeDescription).HasMaxLength(500);
            entity.Property(e => e.IncomeTypeName).HasMaxLength(100);
            entity.Property(e => e.IncomeTypeShortName).HasMaxLength(50);
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<IncomeTransaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_IncomeTransactions_pkey");

            entity.ToTable("IncomeTransaction", "Income");


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IncomeValue).HasPrecision(18, 10);
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Remarks).HasMaxLength(10000);

            entity.HasOne(d => d.Credential).WithMany(p => p.IncomeTransactions)
                .HasForeignKey(d => d.CredentialId)
                .HasConstraintName("IncomeTransaction_CredentialId");

            entity.HasOne(d => d.IncomeType).WithMany(p => p.IncomeTransactions)
                .HasForeignKey(d => d.IncomeTypeId)
                .HasConstraintName("tbl_userincometransaction_tbl_incometype_id_fk");
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("message_pk");

            entity.ToTable("Message", "Messaging");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

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

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Type).WithMany(p => p.MessageDeliveries)
                .HasForeignKey(d => d.TypeId)
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

        modelBuilder.Entity<MessageDeliveryType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("messagedeliveryentity_pk");

            entity.ToTable("MessageDeliveryType", "Messaging");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

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


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

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


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

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

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Type).WithMany(p => p.MessageReactions)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("messagereaction_messagereactionentity_id_fk");

            entity.HasOne(d => d.Message).WithMany(p => p.MessageReactions)
                .HasForeignKey(d => d.MessageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("messagereaction_message_id_fk");
        });

        modelBuilder.Entity<MessageReactionType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("messagereactionentity_pk");

            entity.ToTable("MessageReactionType", "Messaging");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
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

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.Type).WithMany(p => p.MessageThreads)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("messagethread_messagethreadentity_id_fk");
        });

        modelBuilder.Entity<MessageThreadType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("messagethreadentity_pk");

            entity.ToTable("MessageThreadType", "Messaging");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.MessageType).WithMany(p => p.MessageThreadTypes)
                .HasForeignKey(d => d.MessageTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("messagethreadentity_messagetype_id_fk");
        });

        modelBuilder.Entity<MessageThreadMember>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("messagethreadmember_pk");

            entity.ToTable("MessageThreadMember", "Messaging");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.Alias).HasColumnType("character varying");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.Emoji).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Group).WithMany(p => p.MessageThreadMembers)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("messagethreadmember_messagethreadmembergroup_id_fk");

            entity.HasOne(d => d.Credential).WithMany(p => p.MessageThreadMembers)
                .HasForeignKey(d => d.CredentialId)
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

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.Alias).HasColumnType("character varying");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.Emoji).HasColumnType("character varying");

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

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

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

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<MetaData>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("metadata_pk");

            entity.ToTable("MetaData", "MetaData");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Value).HasColumnType("character varying");

            entity.HasOne(d => d.Type).WithMany(p => p.MetaData)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("metadata_metadataentity_id_fk");
        });

        modelBuilder.Entity<MetaDataType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("metadataentity_pk");

            entity.ToTable("MetaDataType", "MetaData");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.Group).WithMany(p => p.MetaDataTypes)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("metadataentity_metadataentitygroup_id_fk");
        });

        modelBuilder.Entity<MetaDataTypeGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("metadataentitygroup_pk");

            entity.ToTable("MetaDataEntityGroup", "MetaData");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
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


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.TenantId).HasColumnName("ApplicationId");

            entity.Property(e => e.Key).HasColumnType("character varying");
            entity.Property(e => e.Unit).HasMaxLength(100);
            entity.Property(e => e.Value).HasColumnType("character varying");

            entity.HasOne(d => d.Tenant).WithMany(p => p.RegistryConfigurations)
                .HasForeignKey(d => d.TenantId)
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

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert

            entity.Property(e => e.Description).HasMaxLength(1000);

            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<RegistryFavoriteType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_favoriteType_pk");

            entity.ToTable("RegistryFavoriteType", "Registry");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Description).HasMaxLength(500);

            entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");
            entity.Property(e => e.IsEnabled).HasDefaultValueSql("true");
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_SessionData");

            entity.ToTable("Session", "Identity");

            entity.HasIndex(e => e.SessionTypeId, "IX_tbl_SessionData_SessionTypeID");

            entity.HasIndex(e => e.CredentialId, "IX_tbl_SessionData_CredentialID");


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert

            entity.Property(e => e.SessionData).HasMaxLength(2000);
            entity.Property(e => e.SessionTypeId).HasColumnName("SessionTypeID");
            entity.Property(e => e.CredentialId).HasColumnName("CredentialID");

            entity.HasOne(d => d.SessionType).WithMany(p => p.SessionData)
                .HasForeignKey(d => d.SessionTypeId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("tbl_sessiondata_fk");

            entity.HasOne(d => d.Credential).WithMany(p => p.SessionData)
                .HasForeignKey(d => d.CredentialId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("tbl_sessiondata_fk_1");
        });

        modelBuilder.Entity<SessionType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tbl_SessionType");

            entity.ToTable("SessionType", "Identity");


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert

            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<StorageFile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("storagefile_pk");

            entity.ToTable("StorageFile", "Storage");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.ContentPath).HasColumnType("character varying");
            entity.Property(e => e.ContentType).HasColumnType("character varying");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.Hash).HasColumnType("character varying");
            entity.Property(e => e.Identifier);
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.Type).WithMany(p => p.StorageFiles)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("storagefile_storagefileentity_id_fk");

            entity.HasOne(d => d.StorageFileIdentifier).WithMany(p => p.StorageFiles)
                .HasForeignKey(d => d.StorageFileIdentifierId)
                .HasConstraintName("storagefile_storagefileidentifier_id_fk");
        });

        modelBuilder.Entity<StorageFileType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("storagefileentity_pk");

            entity.ToTable("StorageFileType", "Storage");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

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


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");

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


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("subscription_pk");

            entity.ToTable("Subscription", "Affiliate");


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CredentialId).HasColumnName("CredentialID");
            entity.Property(e => e.TypeId).HasColumnName("TypeID");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Value).HasColumnType("character varying");

            entity.HasOne(d => d.Credential).WithMany(p => p.Subscriptions)
                .HasForeignKey(d => d.CredentialId)
                .HasConstraintName("subscription_identitycredential_id_fk");

            entity.HasOne(d => d.Type).WithMany(p => p.Subscriptions)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("subscription_subscriptionentity_id_fk");
        });

        modelBuilder.Entity<SubscriptionType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("subscriptionentity_pk");

            entity.ToTable("SubscriptionType", "Affiliate");


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_Wallets_pkey");

            entity.ToTable("Wallet", "Wallet");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.Balance).HasPrecision(24, 8);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Credential).WithMany(p => p.Wallets)
                .HasForeignKey(d => d.CredentialId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("tbl_Wallets_CredentialId_fkey");

            entity.HasOne(d => d.WalletType).WithMany(p => p.Wallets)
                .HasForeignKey(d => d.WalletTypeId)
                .HasConstraintName("tbl_Wallets_WalletTypeId_fkey");
        });

        modelBuilder.Entity<WalletAddress>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_WalletAddresses_pkey");

            entity.ToTable("WalletAddress", "Wallet");


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.Address).HasMaxLength(512);
            entity.Property(e => e.Balance).HasPrecision(18, 10);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Remarks).HasMaxLength(100);

            entity.HasOne(d => d.Wallet).WithMany(p => p.WalletAddresses)
                .HasForeignKey(d => d.WalletId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("walletaddress_wallet_id_fk");
        });

        modelBuilder.Entity<WalletType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_WalletType_pkey");

            entity.ToTable("WalletType", "Wallet");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.TenantId);
            entity.Property(e => e.Code).HasMaxLength(9);
            entity.Property(e => e.CurrencyTypeId).HasColumnName("CurrencyTypeID");
            entity.Property(e => e.Desc).HasMaxLength(500);

            entity.Property(e => e.Name).HasMaxLength(20);

            entity.HasOne(d => d.Tenant).WithMany(p => p.WalletTypes)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("tbl_walletTypes_tbl_applications_id_fk");

            entity.HasOne(d => d.CurrencyType).WithMany(p => p.WalletTypes)
                .HasForeignKey(d => d.CurrencyTypeId)
                .HasConstraintName("CurrencyID");
        });

        modelBuilder.Entity<WalletTransaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_WalletTransactions_pkey");

            entity.ToTable("WalletTransaction", "Wallet");


            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.Amount).HasPrecision(24, 8);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasMaxLength(10000);

            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.PreviousTotalBalance).HasPrecision(24, 8);
            entity.Property(e => e.Remarks).HasMaxLength(10000);
            entity.Property(e => e.RunningTotalBalance).HasPrecision(24, 8);

            entity.HasOne(d => d.Credential).WithMany(p => p.WalletTransactions)
                .HasForeignKey(d => d.CredentialId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("UserAuthID");

            entity.HasOne(d => d.Wallet).WithMany(p => p.WalletTransactions)
                .HasForeignKey(d => d.WalletId)
                .HasConstraintName("SourceUserWalletId");
        });

        modelBuilder.Entity<WithdrawalRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tbl_WithdrawalRequest_pkey");

            entity.ToTable("WithdrawalRequest", "Wallet");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
            entity.Property(e => e.Address).HasMaxLength(10000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Remarks).HasColumnType("character varying");
            entity.Property(e => e.TotalAmount).HasPrecision(18, 10);

            entity.HasOne(d => d.Credential).WithMany(p => p.WithdrawalRequests)
                .HasForeignKey(d => d.CredentialId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("WithdrawalRequest_CredentialId");

            entity.HasOne(d => d.Wallet).WithMany(p => p.WithdrawalRequests)
                .HasForeignKey(d => d.WalletId)
                .HasConstraintName("WithdrawalRequest_WalletId");
            
        });

        OnModelCreatingPartial(modelBuilder);
        SeedDatabase(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);

    /*protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseLazyLoadingProxies();
    }*/
    
    protected void SeedDatabase(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdentityContactType>().HasData(
            new IdentityContactType() { Id = IdentityConstants.ContactType.Phone, IsEnabled = true, IsDeleted = false, Name = "Phone" },
            new IdentityContactType() { Id = IdentityConstants.ContactType.Email, IsEnabled = true, IsDeleted = false, Name = "Email" },
            new IdentityContactType() { Id = IdentityConstants.ContactType.Facebook, IsEnabled = true, IsDeleted = false, Name = "Facebook" },
            new IdentityContactType() { Id = IdentityConstants.ContactType.Instagram, IsEnabled = true, IsDeleted = false, Name = "Instagram" },
            new IdentityContactType() { Id = IdentityConstants.ContactType.Twitter, IsEnabled = true, IsDeleted = false, Name = "Twitter" },
            new IdentityContactType() { Id = IdentityConstants.ContactType.LinkedIn, IsEnabled = true, IsDeleted = false, Name = "LinkedIn" }
        );
        
        modelBuilder.Entity<IdentityContactGroup>().HasData(
            new IdentityContactGroup { Id = IdentityConstants.ContactGroup.Home, IsEnabled = true, Name = "HOME" },
            new IdentityContactGroup { Id = IdentityConstants.ContactGroup.Personal, IsEnabled = true, Name = "PERSONAL" },
            new IdentityContactGroup { Id = IdentityConstants.ContactGroup.Business, IsEnabled = true, Name = "BUSINESS" },
            new IdentityContactGroup { Id = IdentityConstants.ContactGroup.Work, IsEnabled = true, Name = "WORK" }
        );
        
        modelBuilder.Entity<IdentityAddressType>().HasData(
            new IdentityAddressType { Id = IdentityConstants.AddressType.Home, IsEnabled = true, Name = "HOME" },
            new IdentityAddressType { Id = IdentityConstants.AddressType.Personal, IsEnabled = true, Name = "PERSONAL" },
            new IdentityAddressType { Id = IdentityConstants.AddressType.Business, IsEnabled = true, Name = "BUSINESS" },
            new IdentityAddressType { Id = IdentityConstants.AddressType.Work, IsEnabled = true, Name = "WORK" },
            new IdentityAddressType { Id = IdentityConstants.AddressType.Billing, IsEnabled = true, Name = "BILLING" },
            new IdentityAddressType { Id = IdentityConstants.AddressType.Shipping, IsEnabled = true, Name = "SHIPPING" }
        );
        
        modelBuilder.Entity<IdentityVerificationType>().HasData(
            new IdentityVerificationType{ Id = IdentityConstants.VerificationType.Sms, IsEnabled = false, Name = "SMS", DefaultExpiry = 10 },
            new IdentityVerificationType{ Id = IdentityConstants.VerificationType.Email, IsEnabled = false, Name = "Email", DefaultExpiry = 120 },
            new IdentityVerificationType{ Id = IdentityConstants.VerificationType.Kyc, IsEnabled = false, Name = "KYC", DefaultExpiry = 1051200 }
        );
    }
}