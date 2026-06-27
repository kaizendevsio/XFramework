using Communications.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Communications.Domain.Shared.Configurations;

public sealed class MessageDirectThreadConfiguration : IEntityTypeConfiguration<MessageDirectThread>
{
    public void Configure(EntityTypeBuilder<MessageDirectThread> entity)
    {
        entity.HasKey(e => e.Id).HasName("messagedirectthread_pk");

        entity.ToTable("MessageDirectThread", "Communications");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");

        entity.HasIndex(e => new { e.TenantId, e.FirstCredentialId, e.SecondCredentialId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("UX_MessageDirectThread_Tenant_Pair_Active");

        entity.HasIndex(e => e.MessageThreadId)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("UX_MessageDirectThread_Thread_Active");

        entity.HasOne(e => e.MessageThread).WithMany()
            .HasForeignKey(e => e.MessageThreadId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("messagedirectthread_messagethread_id_fk");
    }
}
