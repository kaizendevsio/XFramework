using Messaging.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Messaging.Domain.Shared.Configurations;

public sealed class MessageTemplateConfiguration : IEntityTypeConfiguration<MessageTemplate>
{
    public void Configure(EntityTypeBuilder<MessageTemplate> entity)
    {
        entity.HasKey(e => e.Id).HasName("messagetemplate_pk");

        entity.ToTable("MessageTemplate", "Messaging");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");

        entity.Property(e => e.Key).HasColumnType("character varying").IsRequired();
        entity.Property(e => e.Name).HasColumnType("character varying").IsRequired();
        entity.Property(e => e.Description).HasColumnType("character varying");
        entity.Property(e => e.TemplateType).HasColumnType("character varying").IsRequired();
        entity.Property(e => e.Subject).HasColumnType("character varying");
        entity.Property(e => e.Body).HasColumnType("character varying").IsRequired();
        entity.Property(e => e.RequiredVariablesJson)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb");

        entity.HasIndex(e => new { e.TenantId, e.TemplateType, e.Key })
            .HasDatabaseName("IX_MessageTemplate_Tenant_Type_Key");

        entity.HasIndex(e => new { e.TenantId, e.TemplateType, e.Key })
            .IsUnique()
            .HasDatabaseName("UX_MessageTemplate_Tenant_Type_Key_Active")
            .HasFilter("\"IsDeleted\" = false AND \"OwnerCredentialId\" IS NULL");

        entity.HasIndex(e => new { e.TenantId, e.OwnerCredentialId, e.Key })
            .IsUnique()
            .HasDatabaseName("UX_MessageTemplate_User_Key_Active")
            .HasFilter("\"IsDeleted\" = false AND \"OwnerCredentialId\" IS NOT NULL");

        entity.HasIndex(e => new { e.TenantId, e.TemplateType, e.ModifiedAt })
            .HasDatabaseName("IX_MessageTemplate_Tenant_Type_ModifiedAt");

        entity.HasOne(e => e.OwnerCredential)
            .WithMany()
            .HasForeignKey(e => e.OwnerCredentialId)
            .HasConstraintName("messagetemplate_ownercredential_id_fk");
    }
}
