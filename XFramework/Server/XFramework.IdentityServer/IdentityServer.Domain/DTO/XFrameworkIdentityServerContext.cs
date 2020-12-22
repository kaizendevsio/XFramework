using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

#nullable disable

namespace IdentityServer.Domain.DTO
{
    public partial class XFrameworkIdentityServerContext : DbContext
    {
        public XFrameworkIdentityServerContext()
        {
        }

        public XFrameworkIdentityServerContext(DbContextOptions<XFrameworkIdentityServerContext> options)
            : base(options)
        {
        }

        public virtual DbSet<TblApplication> TblApplications { get; set; }
        public virtual DbSet<TblApplicationLog> TblApplicationLogs { get; set; }
        public virtual DbSet<TblAuditField> TblAuditFields { get; set; }
        public virtual DbSet<TblAuditHistory> TblAuditHistories { get; set; }
        public virtual DbSet<TblIdentityAddress> TblIdentityAddresses { get; set; }
        public virtual DbSet<TblIdentityAddressEntity> TblIdentityAddressEntities { get; set; }
        public virtual DbSet<TblIdentityContact> TblIdentityContacts { get; set; }
        public virtual DbSet<TblIdentityContactEntity> TblIdentityContactEntities { get; set; }
        public virtual DbSet<TblIdentityCredential> TblIdentityCredentials { get; set; }
        public virtual DbSet<TblIdentityInfo> TblIdentityInfos { get; set; }
        public virtual DbSet<TblIdentityRole> TblIdentityRoles { get; set; }
        public virtual DbSet<TblIdentityRoleEntity> TblIdentityRoleEntities { get; set; }
        public virtual DbSet<TblIdentityVerification> TblIdentityVerifications { get; set; }
        public virtual DbSet<TblSessionDatum> TblSessionData { get; set; }
        public virtual DbSet<TblSessionEntity> TblSessionEntities { get; set; }
        public virtual DbSet<TblVerificationType> TblVerificationTypes { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                //optionsBuilder.UseNpgsql("Host=localhost;Database=XFramework.IdentityServer;Username=dbAdmin;Password=4*5WD-K8%f*NqmPY");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasAnnotation("Relational:Collation", "English_United States.1252");

            modelBuilder.Entity<TblApplication>(entity =>
            {
                entity.ToTable("tbl_Application", "dbo");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.AppName).HasColumnType("character varying");

                entity.Property(e => e.AvailabilityDate).HasColumnType("timestamp with time zone");

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Expiration).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ParentAppId).HasColumnName("ParentAppID");

                entity.Property(e => e.Uid)
                    .HasColumnType("character varying")
                    .HasColumnName("UID");
            });

            modelBuilder.Entity<TblApplicationLog>(entity =>
            {
                entity.ToTable("tbl_ApplicationLogs", "dbo");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.AppId).HasColumnName("AppID");

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Initiator).HasColumnType("character varying");

                entity.Property(e => e.Message).HasColumnType("character varying");

                entity.HasOne(d => d.App)
                    .WithMany(p => p.TblApplicationLogs)
                    .HasForeignKey(d => d.AppId)
                    .HasConstraintName("tbl_applogs_appid_fk");
            });

            modelBuilder.Entity<TblAuditField>(entity =>
            {
                entity.ToTable("tbl_AuditFields", "dbo");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");
            });

            modelBuilder.Entity<TblAuditHistory>(entity =>
            {
                entity.ToTable("tbl_AuditHistory", "dbo");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.KeyValues).HasColumnType("character varying");

                entity.Property(e => e.NewValues).HasColumnType("character varying");

                entity.Property(e => e.OldValues).HasColumnType("character varying");

                entity.Property(e => e.QueryAction).HasColumnType("character varying");

                entity.Property(e => e.TableName).HasColumnType("character varying");
            });

            modelBuilder.Entity<TblIdentityAddress>(entity =>
            {
                entity.ToTable("tbl_IdentityAddresses", "dbo");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.AddressEntitiesId).HasColumnName("AddressEntitiesID");

                entity.Property(e => e.Barangay).HasMaxLength(500);

                entity.Property(e => e.Building).HasMaxLength(500);

                entity.Property(e => e.City).HasMaxLength(500);

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Region).HasMaxLength(500);

                entity.Property(e => e.Street).HasMaxLength(500);

                entity.Property(e => e.Subdivision).HasMaxLength(500);

                entity.Property(e => e.UnitNumber).HasMaxLength(500);

                entity.Property(e => e.UserInfoId).HasColumnName("UserInfoID");

                entity.HasOne(d => d.AddressEntities)
                    .WithMany(p => p.TblIdentityAddresses)
                    .HasForeignKey(d => d.AddressEntitiesId)
                    .HasConstraintName("AddressEntitiesID");

                entity.HasOne(d => d.UserInfo)
                    .WithMany(p => p.TblIdentityAddresses)
                    .HasForeignKey(d => d.UserInfoId)
                    .HasConstraintName("UserInfoID");
            });

            modelBuilder.Entity<TblIdentityAddressEntity>(entity =>
            {
                entity.ToTable("tbl_IdentityAddressEntities", "dbo");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Name).HasMaxLength(500);
            });

            modelBuilder.Entity<TblIdentityContact>(entity =>
            {
                entity.ToTable("tbl_IdentityContacts", "dbo");

                entity.HasIndex(e => e.Value, "tbl_identitycontacts_un")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.UcentitiesId).HasColumnName("UCEntitiesID");

                entity.Property(e => e.UserInfoId).HasColumnName("UserInfoID");

                entity.Property(e => e.Value)
                    .IsRequired()
                    .HasColumnType("character varying");

                entity.HasOne(d => d.Ucentities)
                    .WithMany(p => p.TblIdentityContacts)
                    .HasForeignKey(d => d.UcentitiesId)
                    .HasConstraintName("UCEntitiesID");

                entity.HasOne(d => d.UserInfo)
                    .WithMany(p => p.TblIdentityContacts)
                    .HasForeignKey(d => d.UserInfoId)
                    .HasConstraintName("IdentityContactInfoID");
            });

            modelBuilder.Entity<TblIdentityContactEntity>(entity =>
            {
                entity.ToTable("tbl_IdentityContactEntities", "dbo");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<TblIdentityCredential>(entity =>
            {
                entity.ToTable("tbl_IdentityCredentials", "dbo");

                entity.HasIndex(e => e.UserName, "tbl_identitycredentials_un")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.AppId).HasColumnName("AppID");

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.IdentityInfoId).HasColumnName("IdentityInfoID");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.UserAlias).HasMaxLength(100);

                entity.Property(e => e.UserName).HasMaxLength(100);

                entity.HasOne(d => d.App)
                    .WithMany(p => p.TblIdentityCredentials)
                    .HasForeignKey(d => d.AppId)
                    .HasConstraintName("tbl_usercredentials_appid_fk");

                entity.HasOne(d => d.IdentityInfo)
                    .WithMany(p => p.TblIdentityCredentials)
                    .HasForeignKey(d => d.IdentityInfoId)
                    .HasConstraintName("tbl_identitycredentials_fk");
            });

            modelBuilder.Entity<TblIdentityInfo>(entity =>
            {
                entity.ToTable("tbl_IdentityInfo", "dbo");

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
                entity.ToTable("tbl_IdentityRoles", "dbo");

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
                entity.ToTable("tbl_IdentityRoleEntities", "dbo");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Name).HasMaxLength(100);
            });

            modelBuilder.Entity<TblIdentityVerification>(entity =>
            {
                entity.ToTable("tbl_IdentityVerifications", "dbo");

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

            modelBuilder.Entity<TblSessionDatum>(entity =>
            {
                entity.ToTable("tbl_SessionData", "dbo");

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
                entity.ToTable("tbl_SessionEntities", "dbo");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<TblVerificationType>(entity =>
            {
                entity.ToTable("tbl_VerificationType", "dbo");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.LastChanged).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Name).HasMaxLength(100);
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
