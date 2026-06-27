using Microsoft.EntityFrameworkCore.Migrations;
using XFramework.Domain.Contexts;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(AppDbContext))]
    [Migration("20260628090000_RenameMessagingToCommunications")]
    public partial class RenameMessagingToCommunications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'Messaging')
                       AND NOT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'Communications') THEN
                        EXECUTE 'ALTER SCHEMA "Messaging" RENAME TO "Communications"';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                UPDATE "Identity"."TenantModuleFeature" target
                SET
                    "IsEnabled" = target."IsEnabled" OR source."IsEnabled",
                    "DisplayName" = CASE
                        WHEN target."SubFeatureKey" = '' THEN 'Communications'
                        WHEN target."SubFeatureKey" = 'chat' THEN 'Communications Chat'
                        WHEN target."SubFeatureKey" = 'audio_video' THEN 'Communications Audio/Video'
                        ELSE target."DisplayName"
                    END,
                    "Description" = CASE
                        WHEN target."SubFeatureKey" = '' THEN 'Tenant communications settings, administration, moderation, and chat platform controls.'
                        WHEN target."SubFeatureKey" = 'chat' THEN 'Threads, direct messages, reactions, and attachments.'
                        WHEN target."SubFeatureKey" = 'audio_video' THEN 'Audio and video communication features.'
                        ELSE target."Description"
                    END,
                    "ModifiedAt" = now()
                FROM "Identity"."TenantModuleFeature" source
                WHERE lower(target."ModuleKey") = 'communications'
                  AND lower(source."ModuleKey") = 'messaging'
                  AND target."TenantId" = source."TenantId"
                  AND lower(target."SubFeatureKey") = lower(source."SubFeatureKey");
                """);

            migrationBuilder.Sql("""
                UPDATE "Identity"."TenantModuleFeature" target
                SET
                    "ModuleKey" = 'communications',
                    "DisplayName" = CASE
                        WHEN target."SubFeatureKey" = '' THEN 'Communications'
                        WHEN target."SubFeatureKey" = 'chat' THEN 'Communications Chat'
                        WHEN target."SubFeatureKey" = 'audio_video' THEN 'Communications Audio/Video'
                        ELSE replace(coalesce(target."DisplayName", 'Messaging'), 'Messaging', 'Communications')
                    END,
                    "Description" = CASE
                        WHEN target."SubFeatureKey" = '' THEN 'Tenant communications settings, administration, moderation, and chat platform controls.'
                        WHEN target."SubFeatureKey" = 'chat' THEN 'Threads, direct messages, reactions, and attachments.'
                        WHEN target."SubFeatureKey" = 'audio_video' THEN 'Audio and video communication features.'
                        ELSE replace(coalesce(target."Description", ''), 'Messaging', 'Communications')
                    END,
                    "ModifiedAt" = now()
                WHERE lower(target."ModuleKey") = 'messaging'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "Identity"."TenantModuleFeature" existing
                      WHERE existing."TenantId" = target."TenantId"
                        AND lower(existing."ModuleKey") = 'communications'
                        AND lower(existing."SubFeatureKey") = lower(target."SubFeatureKey")
                        AND existing."ID" <> target."ID"
                  );
                """);

            migrationBuilder.Sql("""
                UPDATE "Identity"."TenantModuleFeature"
                SET "IsEnabled" = false,
                    "IsDeleted" = true,
                    "ModifiedAt" = now()
                WHERE lower("ModuleKey") = 'messaging';
                """);

            migrationBuilder.Sql("""
                UPDATE "Registry"."RegistryConfigurationGroup"
                SET
                    "Name" = CASE "Name"
                        WHEN 'Messaging.Chat' THEN 'Communications.Chat'
                        WHEN 'Messaging.Policy' THEN 'Communications.Policy'
                        WHEN 'Messaging.Transport' THEN 'Communications.Transport'
                        WHEN 'MessagingService_Otp' THEN 'CommunicationsService_Otp'
                        WHEN 'MessagingService_PasswordReset' THEN 'CommunicationsService_PasswordReset'
                        ELSE "Name"
                    END,
                    "Description" = replace(coalesce("Description", ''), 'Messaging', 'Communications'),
                    "ModifiedAt" = now()
                WHERE "Name" IN (
                    'Messaging.Chat',
                    'Messaging.Policy',
                    'Messaging.Transport',
                    'MessagingService_Otp',
                    'MessagingService_PasswordReset'
                );
                """);

            migrationBuilder.Sql("""
                UPDATE "Registry"."RegistryConfiguration"
                SET "Key" = 'Settings:Communications:Sms:AgentClusterId',
                    "ModifiedAt" = now()
                WHERE "Key" = 'Settings:Messaging:Sms:AgentClusterId';
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('"Communications"."MessageTemplate"') IS NOT NULL THEN
                        UPDATE "Communications"."MessageTemplate"
                        SET "Key" = 'communications.generic',
                            "Name" = replace("Name", 'Messaging', 'Communications'),
                            "Description" = replace(coalesce("Description", ''), 'Messaging', 'Communications'),
                            "ModifiedAt" = now()
                        WHERE "Key" = 'messaging.generic';
                    END IF;

                    IF to_regclass('"Communications"."Message"') IS NOT NULL
                       AND EXISTS (
                           SELECT 1
                           FROM information_schema.columns
                           WHERE table_schema = 'Communications'
                             AND table_name = 'Message'
                             AND column_name = 'TemplateKey'
                       ) THEN
                        UPDATE "Communications"."Message"
                        SET "TemplateKey" = 'communications.generic',
                            "ModifiedAt" = now()
                        WHERE "TemplateKey" = 'messaging.generic';
                    END IF;

                    IF to_regclass('"Communications"."MessageDirect"') IS NOT NULL
                       AND EXISTS (
                           SELECT 1
                           FROM information_schema.columns
                           WHERE table_schema = 'Communications'
                             AND table_name = 'MessageDirect'
                             AND column_name = 'TemplateKey'
                       ) THEN
                        UPDATE "Communications"."MessageDirect"
                        SET "TemplateKey" = 'communications.generic',
                            "ModifiedAt" = now()
                        WHERE "TemplateKey" = 'messaging.generic';
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'Communications')
                       AND NOT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'Messaging') THEN
                        EXECUTE 'ALTER SCHEMA "Communications" RENAME TO "Messaging"';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                UPDATE "Identity"."TenantModuleFeature" target
                SET
                    "IsEnabled" = target."IsEnabled" OR source."IsEnabled",
                    "DisplayName" = CASE
                        WHEN target."SubFeatureKey" = '' THEN 'Messaging'
                        WHEN target."SubFeatureKey" = 'chat' THEN 'Messaging Chat'
                        WHEN target."SubFeatureKey" = 'audio_video' THEN 'Messaging Audio/Video'
                        ELSE target."DisplayName"
                    END,
                    "Description" = CASE
                        WHEN target."SubFeatureKey" = '' THEN 'Tenant messaging settings, administration, moderation, and chat platform controls.'
                        WHEN target."SubFeatureKey" = 'chat' THEN 'Threads, direct messages, reactions, and attachments.'
                        WHEN target."SubFeatureKey" = 'audio_video' THEN 'Audio and video messaging features.'
                        ELSE target."Description"
                    END,
                    "ModifiedAt" = now()
                FROM "Identity"."TenantModuleFeature" source
                WHERE lower(target."ModuleKey") = 'messaging'
                  AND lower(source."ModuleKey") = 'communications'
                  AND target."TenantId" = source."TenantId"
                  AND lower(target."SubFeatureKey") = lower(source."SubFeatureKey");
                """);

            migrationBuilder.Sql("""
                UPDATE "Identity"."TenantModuleFeature" target
                SET
                    "ModuleKey" = 'messaging',
                    "IsDeleted" = false,
                    "DisplayName" = replace(coalesce(target."DisplayName", 'Communications'), 'Communications', 'Messaging'),
                    "Description" = replace(coalesce(target."Description", ''), 'Communications', 'Messaging'),
                    "ModifiedAt" = now()
                WHERE lower(target."ModuleKey") = 'communications'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "Identity"."TenantModuleFeature" existing
                      WHERE existing."TenantId" = target."TenantId"
                        AND lower(existing."ModuleKey") = 'messaging'
                        AND lower(existing."SubFeatureKey") = lower(target."SubFeatureKey")
                        AND existing."ID" <> target."ID"
                  );
                """);

            migrationBuilder.Sql("""
                UPDATE "Identity"."TenantModuleFeature"
                SET "IsEnabled" = false,
                    "IsDeleted" = true,
                    "ModifiedAt" = now()
                WHERE lower("ModuleKey") = 'communications';
                """);

            migrationBuilder.Sql("""
                UPDATE "Registry"."RegistryConfigurationGroup"
                SET
                    "Name" = CASE "Name"
                        WHEN 'Communications.Chat' THEN 'Messaging.Chat'
                        WHEN 'Communications.Policy' THEN 'Messaging.Policy'
                        WHEN 'Communications.Transport' THEN 'Messaging.Transport'
                        WHEN 'CommunicationsService_Otp' THEN 'MessagingService_Otp'
                        WHEN 'CommunicationsService_PasswordReset' THEN 'MessagingService_PasswordReset'
                        ELSE "Name"
                    END,
                    "Description" = replace(coalesce("Description", ''), 'Communications', 'Messaging'),
                    "ModifiedAt" = now()
                WHERE "Name" IN (
                    'Communications.Chat',
                    'Communications.Policy',
                    'Communications.Transport',
                    'CommunicationsService_Otp',
                    'CommunicationsService_PasswordReset'
                );
                """);

            migrationBuilder.Sql("""
                UPDATE "Registry"."RegistryConfiguration"
                SET "Key" = 'Settings:Messaging:Sms:AgentClusterId',
                    "ModifiedAt" = now()
                WHERE "Key" = 'Settings:Communications:Sms:AgentClusterId';
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('"Messaging"."MessageTemplate"') IS NOT NULL THEN
                        UPDATE "Messaging"."MessageTemplate"
                        SET "Key" = 'messaging.generic',
                            "Name" = replace("Name", 'Communications', 'Messaging'),
                            "Description" = replace(coalesce("Description", ''), 'Communications', 'Messaging'),
                            "ModifiedAt" = now()
                        WHERE "Key" = 'communications.generic';
                    END IF;

                    IF to_regclass('"Messaging"."Message"') IS NOT NULL
                       AND EXISTS (
                           SELECT 1
                           FROM information_schema.columns
                           WHERE table_schema = 'Messaging'
                             AND table_name = 'Message'
                             AND column_name = 'TemplateKey'
                       ) THEN
                        UPDATE "Messaging"."Message"
                        SET "TemplateKey" = 'messaging.generic',
                            "ModifiedAt" = now()
                        WHERE "TemplateKey" = 'communications.generic';
                    END IF;

                    IF to_regclass('"Messaging"."MessageDirect"') IS NOT NULL
                       AND EXISTS (
                           SELECT 1
                           FROM information_schema.columns
                           WHERE table_schema = 'Messaging'
                             AND table_name = 'MessageDirect'
                             AND column_name = 'TemplateKey'
                       ) THEN
                        UPDATE "Messaging"."MessageDirect"
                        SET "TemplateKey" = 'messaging.generic',
                            "ModifiedAt" = now()
                        WHERE "TemplateKey" = 'communications.generic';
                    END IF;
                END $$;
                """);
        }
    }
}
