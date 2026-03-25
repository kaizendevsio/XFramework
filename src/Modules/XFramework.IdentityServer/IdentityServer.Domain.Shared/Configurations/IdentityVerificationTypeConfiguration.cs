using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public class IdentityVerificationTypeConfiguration : IEntityTypeConfiguration<IdentityVerificationType>
{
    public void Configure(EntityTypeBuilder<IdentityVerificationType> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK_tbl_VerificationType");

        entity.ToTable("IdentityVerificationType", "Identity");


        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert

        entity.Property(e => e.Name).HasMaxLength(100);

        entity.HasData(
            new IdentityVerificationType{ Id = IdentityConstants.VerificationType.Sms, IsEnabled = false, Name = "SMS", DefaultExpiry = 10 },
            new IdentityVerificationType{ Id = IdentityConstants.VerificationType.Email, IsEnabled = false, Name = "Email", DefaultExpiry = 120 },
            new IdentityVerificationType{ Id = IdentityConstants.VerificationType.Kyc, IsEnabled = false, Name = "KYC", DefaultExpiry = 1051200 }
        );
    }
}
