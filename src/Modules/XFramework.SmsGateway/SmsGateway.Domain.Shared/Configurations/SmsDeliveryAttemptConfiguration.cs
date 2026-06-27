using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmsGateway.Domain.Shared.Contracts;

namespace SmsGateway.Domain.Shared.Configurations;

public sealed class SmsDeliveryAttemptConfiguration : IEntityTypeConfiguration<SmsDeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<SmsDeliveryAttempt> entity)
    {
        entity.HasKey(e => e.Id).HasName("smsdeliveryattempt_pk");
        entity.ToTable("SmsDeliveryAttempt", "SmsGateway");

        entity.Property(e => e.Id).HasColumnName("ID").HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled).IsRequired().HasDefaultValueSql("true");
        entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValueSql("false");
        entity.Property(e => e.Status).HasConversion<int>();
        entity.Property(e => e.LeaseOwner).HasMaxLength(128);
        entity.Property(e => e.ProviderMessageId).HasMaxLength(256);
        entity.Property(e => e.ErrorCode).HasMaxLength(128);
        entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
        entity.Property(e => e.StartedAt).HasDefaultValueSql("now()");

        entity.HasIndex(e => new { e.TenantId, e.SmsOutboundJobId, e.AttemptNumber })
            .IsUnique()
            .HasDatabaseName("ux_smsdeliveryattempt_tenant_job_attempt");

        entity.HasOne(e => e.SmsOutboundJob)
            .WithMany(e => e.Attempts)
            .HasForeignKey(e => e.SmsOutboundJobId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("smsdeliveryattempt_outboundjob_id_fk");
    }
}
