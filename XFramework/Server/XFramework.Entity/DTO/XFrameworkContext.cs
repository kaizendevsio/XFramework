using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace XFramework.Data.DTO
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

        public virtual DbSet<TblAddressEntities> TblAddressEntities { get; set; }
        public virtual DbSet<TblAddresses> TblAddresses { get; set; }
        public virtual DbSet<TblApplication> TblApplication { get; set; }
        public virtual DbSet<TblApplicationLogs> TblApplicationLogs { get; set; }
        public virtual DbSet<TblAuditFields> TblAuditFields { get; set; }
        public virtual DbSet<TblAuditHistory> TblAuditHistory { get; set; }
        public virtual DbSet<TblCompanies> TblCompanies { get; set; }
        public virtual DbSet<TblCompanyEntities> TblCompanyEntities { get; set; }
        public virtual DbSet<TblSessionData> TblSessionData { get; set; }
        public virtual DbSet<TblSessionEntities> TblSessionEntities { get; set; }
        public virtual DbSet<TblUserAuthHistory> TblUserAuthHistory { get; set; }
        public virtual DbSet<TblUserContactEntities> TblUserContactEntities { get; set; }
        public virtual DbSet<TblUserContacts> TblUserContacts { get; set; }
        public virtual DbSet<TblUserCredentials> TblUserCredentials { get; set; }
        public virtual DbSet<TblUserInfo> TblUserInfo { get; set; }
        public virtual DbSet<TblUserRoleEntities> TblUserRoleEntities { get; set; }
        public virtual DbSet<TblUserRoles> TblUserRoles { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. See http://go.microsoft.com/fwlink/?LinkId=723263 for guidance on storing connection strings.
                //optionsBuilder.UseNpgsql("Host=localhost;Database=XFramework;Username=dbAdmin;Password=4*5WD-K8%f*NqmPY");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TblAddressEntities>(entity =>
            {
                entity.ToTable("tbl_AddressEntities", "dbo");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Name).HasMaxLength(500);
            });

            modelBuilder.Entity<TblAddresses>(entity =>
            {
                entity.ToTable("tbl_Addresses", "dbo");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.AddressEntitiesId).HasColumnName("AddressEntitiesID");

                entity.Property(e => e.Barangay).HasMaxLength(500);

                entity.Property(e => e.Building).HasMaxLength(500);

                entity.Property(e => e.City).HasMaxLength(500);

                entity.Property(e => e.CompanyId).HasColumnName("CompanyID");

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Region).HasMaxLength(500);

                entity.Property(e => e.Street).HasMaxLength(500);

                entity.Property(e => e.Subdivision).HasMaxLength(500);

                entity.Property(e => e.UnitNumber).HasMaxLength(500);

                entity.Property(e => e.UserInfoId).HasColumnName("UserInfoID");

                entity.HasOne(d => d.AddressEntities)
                    .WithMany(p => p.TblAddresses)
                    .HasForeignKey(d => d.AddressEntitiesId)
                    .HasConstraintName("AddressEntitiesID");

                entity.HasOne(d => d.Company)
                    .WithMany(p => p.TblAddresses)
                    .HasForeignKey(d => d.CompanyId)
                    .HasConstraintName("CompanyID");

                entity.HasOne(d => d.UserInfo)
                    .WithMany(p => p.TblAddresses)
                    .HasForeignKey(d => d.UserInfoId)
                    .HasConstraintName("UserInfoID");
            });

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
                    .HasColumnName("UID")
                    .HasColumnType("character varying");
            });

            modelBuilder.Entity<TblApplicationLogs>(entity =>
            {
                entity.ToTable("tbl_ApplicationLogs", "dbo");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.AppId).HasColumnName("AppID");

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Initiator).HasColumnType("character varying");

                entity.Property(e => e.Message).HasColumnType("character varying");
            });

            modelBuilder.Entity<TblAuditFields>(entity =>
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

            modelBuilder.Entity<TblCompanies>(entity =>
            {
                entity.ToTable("tbl_Companies", "dbo");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .ValueGeneratedOnAdd()
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.CompanyEntitiesId).HasColumnName("CompanyEntitiesID");

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.HasOne(d => d.IdNavigation)
                    .WithOne(p => p.TblCompanies)
                    .HasForeignKey<TblCompanies>(d => d.Id)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("tbl_companies_fk");
            });

            modelBuilder.Entity<TblCompanyEntities>(entity =>
            {
                entity.ToTable("tbl_CompanyEntities", "dbo");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Name).HasMaxLength(500);
            });

            modelBuilder.Entity<TblSessionData>(entity =>
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

            modelBuilder.Entity<TblSessionEntities>(entity =>
            {
                entity.ToTable("tbl_SessionEntities", "dbo");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<TblUserAuthHistory>(entity =>
            {
                entity.ToTable("tbl_UserAuthHistory", "dbo");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.DeviceName).HasMaxLength(50);

                entity.Property(e => e.Ipaddress)
                    .HasColumnName("IPAddress")
                    .HasMaxLength(18);

                entity.Property(e => e.LastChanged).HasColumnType("timestamp with time zone");

                entity.Property(e => e.LoginSource).HasMaxLength(50);

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.UserCredentialsId).HasColumnName("UserCredentialsID");

                entity.HasOne(d => d.UserCredentials)
                    .WithMany(p => p.TblUserAuthHistory)
                    .HasForeignKey(d => d.UserCredentialsId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("tbl_userauthhistory_fk");
            });

            modelBuilder.Entity<TblUserContactEntities>(entity =>
            {
                entity.ToTable("tbl_UserContactEntities", "dbo");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<TblUserContacts>(entity =>
            {
                entity.ToTable("tbl_UserContacts", "dbo");

                entity.HasIndex(e => e.Value)
                    .HasName("tbl_usercontacts_un")
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
                    .WithMany(p => p.TblUserContacts)
                    .HasForeignKey(d => d.UcentitiesId)
                    .HasConstraintName("UCEntitiesID");

                entity.HasOne(d => d.UserInfo)
                    .WithMany(p => p.TblUserContacts)
                    .HasForeignKey(d => d.UserInfoId)
                    .HasConstraintName("UserContactInfoID");
            });

            modelBuilder.Entity<TblUserCredentials>(entity =>
            {
                entity.ToTable("tbl_UserCredentials", "dbo");

                entity.HasIndex(e => e.UserName)
                    .HasName("tbl_usercredentials_un")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.AppId).HasColumnName("AppID");

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ResetPasswordCodeExpiration).HasColumnType("timestamp with time zone");

                entity.Property(e => e.UserAlias).HasMaxLength(100);

                entity.Property(e => e.UserInfoId).HasColumnName("UserInfoID");

                entity.Property(e => e.UserName).HasMaxLength(100);

                entity.HasOne(d => d.App)
                    .WithMany(p => p.TblUserCredentials)
                    .HasForeignKey(d => d.AppId)
                    .HasConstraintName("tbl_usercredentials_appid_fk");

                entity.HasOne(d => d.UserInfo)
                    .WithMany(p => p.TblUserCredentials)
                    .HasForeignKey(d => d.UserInfoId)
                    .HasConstraintName("tbl_usercredentials_fk");
            });

            modelBuilder.Entity<TblUserInfo>(entity =>
            {
                entity.ToTable("tbl_UserInfo", "dbo");

                entity.HasIndex(e => e.Uid)
                    .HasName("tbl_userinfo_un")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Dob)
                    .HasColumnName("DOB")
                    .HasColumnType("date");

                entity.Property(e => e.FirstName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.LastName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Uid)
                    .IsRequired()
                    .HasColumnName("UID")
                    .HasMaxLength(500);
            });

            modelBuilder.Entity<TblUserRoleEntities>(entity =>
            {
                entity.ToTable("tbl_UserRoleEntities", "dbo");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.Name).HasMaxLength(100);
            });

            modelBuilder.Entity<TblUserRoles>(entity =>
            {
                entity.ToTable("tbl_UserRoles", "dbo");

                entity.Property(e => e.Id)
                    .HasColumnName("ID")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.CreatedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.ModifiedOn).HasColumnType("timestamp with time zone");

                entity.Property(e => e.RoleEntityId).HasColumnName("RoleEntityID");

                entity.Property(e => e.RoleExpiration).HasColumnType("timestamp with time zone");

                entity.Property(e => e.UserCredId).HasColumnName("UserCredID");

                entity.HasOne(d => d.RoleEntity)
                    .WithMany(p => p.TblUserRoles)
                    .HasForeignKey(d => d.RoleEntityId)
                    .HasConstraintName("tbl_userroles_fk_1");

                entity.HasOne(d => d.UserCred)
                    .WithMany(p => p.TblUserRoles)
                    .HasForeignKey(d => d.UserCredId)
                    .HasConstraintName("tbl_userroles_fk");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
