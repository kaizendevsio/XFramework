using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddCentralAuditTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "event",
                schema: "audit",
                columns: table => new
                {
                    event_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    event_uuid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    event_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    operation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    transaction_id = table.Column<long>(type: "bigint", nullable: false),
                    transaction_ordinal = table.Column<int>(type: "integer", nullable: false),
                    change_set_id = table.Column<Guid>(type: "uuid", nullable: true),
                    database_user = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    service_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    environment = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    instance_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    actor_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    actor_credential_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_identity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    effective_tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_tenant_id_before = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_tenant_id_after = table.Column<Guid>(type: "uuid", nullable: true),
                    session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    trace_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    span_id = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    operation_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    client_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    schema_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    table_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    entity_key = table.Column<string>(type: "jsonb", nullable: false),
                    changed_fields = table.Column<string[]>(type: "text[]", nullable: false),
                    old_values = table.Column<string>(type: "jsonb", nullable: true),
                    new_values = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event", x => x.event_id);
                },
                comment: "Append-only, database-triggered audit trail.");

            migrationBuilder.CreateIndex(
                name: "IX_event_actor_credential_id_event_id",
                schema: "audit",
                table: "event",
                columns: new[] { "actor_credential_id", "event_id" });

            migrationBuilder.CreateIndex(
                name: "IX_event_change_set_id",
                schema: "audit",
                table: "event",
                column: "change_set_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_correlation_id",
                schema: "audit",
                table: "event",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_event_uuid",
                schema: "audit",
                table: "event",
                column: "event_uuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_entity_key",
                schema: "audit",
                table: "event",
                column: "entity_key")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_event_recorded_at",
                schema: "audit",
                table: "event",
                column: "recorded_at")
                .Annotation("Npgsql:IndexMethod", "brin");

            migrationBuilder.CreateIndex(
                name: "IX_event_schema_name_table_name_event_id",
                schema: "audit",
                table: "event",
                columns: new[] { "schema_name", "table_name", "event_id" });

            migrationBuilder.CreateIndex(
                name: "IX_event_subject_tenant_id_after_event_id",
                schema: "audit",
                table: "event",
                columns: new[] { "subject_tenant_id_after", "event_id" });

            migrationBuilder.CreateIndex(
                name: "IX_event_subject_tenant_id_before_event_id",
                schema: "audit",
                table: "event",
                columns: new[] { "subject_tenant_id_before", "event_id" });

            migrationBuilder.Sql("""
                CREATE TABLE audit.table_policy
                (
                    schema_name text NOT NULL,
                    table_name text NOT NULL,
                    enabled boolean NOT NULL DEFAULT true,
                    excluded_columns text[] NOT NULL DEFAULT ARRAY[]::text[],
                    redacted_columns text[] NOT NULL DEFAULT ARRAY[]::text[],
                    CONSTRAINT pk_audit_table_policy PRIMARY KEY (schema_name, table_name)
                );

                COMMENT ON TABLE audit.table_policy IS
                    'Owner-managed exceptions to the audit-by-default table and column policy.';

                CREATE OR REPLACE FUNCTION audit.try_uuid(value text)
                RETURNS uuid
                LANGUAGE plpgsql
                IMMUTABLE
                PARALLEL SAFE
                AS $function$
                BEGIN
                    IF value IS NULL OR btrim(value) = '' THEN
                        RETURN NULL;
                    END IF;

                    RETURN value::uuid;
                EXCEPTION WHEN invalid_text_representation THEN
                    RETURN NULL;
                END;
                $function$;

                CREATE OR REPLACE FUNCTION audit.redact_json(
                    payload jsonb,
                    excluded_columns text[],
                    explicitly_redacted_columns text[])
                RETURNS jsonb
                LANGUAGE sql
                IMMUTABLE
                PARALLEL SAFE
                STRICT
                AS $function$
                    SELECT COALESCE(
                        jsonb_object_agg(
                            item.key,
                            CASE
                                WHEN lower(item.key) ~ '(password|secret|token|api.?key|private.?key)'
                                    OR lower(item.key) IN ('otp', 'pin')
                                    OR EXISTS (
                                        SELECT 1
                                        FROM unnest(explicitly_redacted_columns) AS redacted(column_name)
                                        WHERE lower(redacted.column_name) = lower(item.key))
                                THEN to_jsonb('[REDACTED]'::text)
                                ELSE item.value
                            END)
                        FILTER (WHERE NOT EXISTS (
                            SELECT 1
                            FROM unnest(excluded_columns) AS excluded(column_name)
                            WHERE lower(excluded.column_name) = lower(item.key))),
                        '{}'::jsonb)
                    FROM jsonb_each(payload) AS item;
                $function$;

                CREATE OR REPLACE FUNCTION audit.capture_row_change()
                RETURNS trigger
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path = pg_catalog, audit
                AS $function$
                DECLARE
                    old_row jsonb := CASE WHEN TG_OP IN ('UPDATE', 'DELETE') THEN to_jsonb(OLD) ELSE NULL END;
                    new_row jsonb := CASE WHEN TG_OP IN ('INSERT', 'UPDATE') THEN to_jsonb(NEW) ELSE NULL END;
                    old_audit_row jsonb;
                    new_audit_row jsonb;
                    entity_key_value jsonb;
                    changed_field_names text[];
                    excluded_column_names text[] := ARRAY[]::text[];
                    redacted_column_names text[] := ARRAY[]::text[];
                    table_audit_enabled boolean := true;
                    event_kind_value text := lower(TG_OP);
                    transaction_ordinal_value integer;
                BEGIN
                    IF TG_TABLE_SCHEMA <> 'audit' THEN
                        SELECT policy.enabled, policy.excluded_columns, policy.redacted_columns
                        INTO table_audit_enabled, excluded_column_names, redacted_column_names
                        FROM audit.table_policy AS policy
                        WHERE policy.schema_name = TG_TABLE_SCHEMA
                          AND policy.table_name = TG_TABLE_NAME;

                        IF NOT FOUND THEN
                            table_audit_enabled := true;
                            excluded_column_names := ARRAY[]::text[];
                            redacted_column_names := ARRAY[]::text[];
                        END IF;
                    END IF;

                    IF NOT table_audit_enabled THEN
                        RETURN COALESCE(NEW, OLD);
                    END IF;

                    SELECT excluded_column_names || COALESCE(
                        array_agg(attribute.attname ORDER BY attribute.attnum),
                        ARRAY[]::text[])
                    INTO excluded_column_names
                    FROM pg_attribute AS attribute
                    WHERE attribute.attrelid = TG_RELID
                      AND attribute.attnum > 0
                      AND NOT attribute.attisdropped
                      AND attribute.atttypid = 'bytea'::regtype;

                    IF TG_OP = 'UPDATE'
                       AND COALESCE(old_row ->> 'IsDeleted', old_row ->> 'is_deleted', 'false') <> 'true'
                       AND COALESCE(new_row ->> 'IsDeleted', new_row ->> 'is_deleted', 'false') = 'true' THEN
                        event_kind_value := 'soft_delete';
                    END IF;

                    SELECT COALESCE(array_agg(fields.key ORDER BY fields.key), ARRAY[]::text[])
                    INTO changed_field_names
                    FROM (
                        SELECT key FROM jsonb_object_keys(COALESCE(old_row, '{}'::jsonb)) AS old_fields(key)
                        UNION
                        SELECT key FROM jsonb_object_keys(COALESCE(new_row, '{}'::jsonb)) AS new_fields(key)
                    ) AS fields
                    WHERE old_row -> fields.key IS DISTINCT FROM new_row -> fields.key;

                    SELECT COALESCE(
                        jsonb_object_agg(
                            attribute.attname,
                            COALESCE(new_row, old_row) -> attribute.attname
                            ORDER BY key_column.ordinality),
                        '{}'::jsonb)
                    INTO entity_key_value
                    FROM pg_index AS index_definition
                    CROSS JOIN LATERAL unnest(index_definition.indkey)
                        WITH ORDINALITY AS key_column(attribute_number, ordinality)
                    JOIN pg_attribute AS attribute
                      ON attribute.attrelid = index_definition.indrelid
                     AND attribute.attnum = key_column.attribute_number
                    WHERE index_definition.indrelid = TG_RELID
                      AND index_definition.indisprimary;

                    old_audit_row := CASE
                        WHEN old_row IS NULL THEN NULL
                        ELSE audit.redact_json(old_row, excluded_column_names, redacted_column_names)
                    END;
                    new_audit_row := CASE
                        WHEN new_row IS NULL THEN NULL
                        ELSE audit.redact_json(new_row, excluded_column_names, redacted_column_names)
                    END;

                    transaction_ordinal_value := COALESCE(
                        NULLIF(current_setting('xframework.audit.transaction_ordinal', true), '')::integer,
                        0) + 1;
                    PERFORM set_config(
                        'xframework.audit.transaction_ordinal',
                        transaction_ordinal_value::text,
                        true);

                    INSERT INTO audit.event
                    (
                        event_kind,
                        operation,
                        transaction_id,
                        transaction_ordinal,
                        change_set_id,
                        database_user,
                        service_name,
                        environment,
                        instance_id,
                        actor_kind,
                        actor_credential_id,
                        actor_identity_id,
                        actor_tenant_id,
                        effective_tenant_id,
                        subject_tenant_id_before,
                        subject_tenant_id_after,
                        session_id,
                        correlation_id,
                        trace_id,
                        span_id,
                        operation_name,
                        client_ip,
                        user_agent,
                        schema_name,
                        table_name,
                        entity_key,
                        changed_fields,
                        old_values,
                        new_values
                    )
                    VALUES
                    (
                        event_kind_value,
                        TG_OP,
                        txid_current(),
                        transaction_ordinal_value,
                        audit.try_uuid(NULLIF(current_setting('xframework.audit.change_set_id', true), '')),
                        session_user,
                        NULLIF(current_setting('xframework.audit.service_name', true), ''),
                        NULLIF(current_setting('xframework.audit.environment', true), ''),
                        NULLIF(current_setting('xframework.audit.instance_id', true), ''),
                        COALESCE(NULLIF(current_setting('xframework.audit.actor_kind', true), ''), 'Unknown'),
                        audit.try_uuid(NULLIF(current_setting('xframework.audit.actor_credential_id', true), '')),
                        audit.try_uuid(NULLIF(current_setting('xframework.audit.actor_identity_id', true), '')),
                        audit.try_uuid(NULLIF(current_setting('xframework.audit.actor_tenant_id', true), '')),
                        audit.try_uuid(NULLIF(current_setting('xframework.audit.effective_tenant_id', true), '')),
                        audit.try_uuid(COALESCE(old_row ->> 'TenantId', old_row ->> 'tenant_id')),
                        audit.try_uuid(COALESCE(new_row ->> 'TenantId', new_row ->> 'tenant_id')),
                        audit.try_uuid(NULLIF(current_setting('xframework.audit.session_id', true), '')),
                        audit.try_uuid(NULLIF(current_setting('xframework.audit.correlation_id', true), '')),
                        NULLIF(current_setting('xframework.audit.trace_id', true), ''),
                        NULLIF(current_setting('xframework.audit.span_id', true), ''),
                        NULLIF(current_setting('xframework.audit.operation_name', true), ''),
                        NULLIF(current_setting('xframework.audit.client_ip', true), ''),
                        NULLIF(current_setting('xframework.audit.user_agent', true), ''),
                        TG_TABLE_SCHEMA,
                        TG_TABLE_NAME,
                        entity_key_value,
                        changed_field_names,
                        old_audit_row,
                        new_audit_row
                    );

                    RETURN COALESCE(NEW, OLD);
                END;
                $function$;

                CREATE OR REPLACE FUNCTION audit.reject_event_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path = pg_catalog, audit
                AS $function$
                BEGIN
                    IF TG_OP = 'INSERT' AND pg_trigger_depth() > 1 THEN
                        RETURN NEW;
                    END IF;

                    RAISE EXCEPTION 'audit.event is append-only and may only be written by the audit trigger'
                        USING ERRCODE = '42501';
                END;
                $function$;

                CREATE TRIGGER xframework_audit_event_append_only_row
                BEFORE INSERT OR UPDATE OR DELETE ON audit.event
                FOR EACH ROW EXECUTE FUNCTION audit.reject_event_mutation();

                CREATE TRIGGER xframework_audit_event_append_only_truncate
                BEFORE TRUNCATE ON audit.event
                FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_event_mutation();

                CREATE OR REPLACE FUNCTION audit.ensure_table_triggers()
                RETURNS integer
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path = pg_catalog, audit
                AS $function$
                DECLARE
                    target record;
                    installed_count integer := 0;
                BEGIN
                    FOR target IN
                        SELECT namespace.nspname AS schema_name, relation.relname AS table_name, relation.oid AS relation_id
                        FROM pg_class AS relation
                        JOIN pg_namespace AS namespace ON namespace.oid = relation.relnamespace
                        LEFT JOIN audit.table_policy AS policy
                          ON policy.schema_name = namespace.nspname
                         AND policy.table_name = relation.relname
                        WHERE relation.relkind IN ('r', 'p')
                          AND NOT relation.relispartition
                          AND namespace.nspname NOT IN ('audit', 'pg_catalog', 'information_schema')
                          AND namespace.nspname NOT LIKE 'pg_toast%'
                          AND namespace.nspname NOT LIKE 'pg_temp_%'
                          AND relation.relname <> '__EFMigrationsHistory'
                          AND COALESCE(policy.enabled, true)
                          AND NOT EXISTS (
                              SELECT 1
                              FROM pg_trigger AS existing_trigger
                              WHERE existing_trigger.tgrelid = relation.oid
                                AND existing_trigger.tgname = 'xframework_audit_row'
                                AND NOT existing_trigger.tgisinternal)
                    LOOP
                        EXECUTE format(
                            'CREATE TRIGGER xframework_audit_row AFTER INSERT OR UPDATE OR DELETE ON %I.%I FOR EACH ROW EXECUTE FUNCTION audit.capture_row_change()',
                            target.schema_name,
                            target.table_name);
                        installed_count := installed_count + 1;
                    END LOOP;

                    RETURN installed_count;
                END;
                $function$;

                CREATE OR REPLACE FUNCTION audit.missing_table_triggers()
                RETURNS TABLE(schema_name text, table_name text)
                LANGUAGE sql
                STABLE
                SECURITY DEFINER
                SET search_path = pg_catalog, audit
                AS $function$
                    SELECT namespace.nspname::text, relation.relname::text
                    FROM pg_class AS relation
                    JOIN pg_namespace AS namespace ON namespace.oid = relation.relnamespace
                    LEFT JOIN audit.table_policy AS policy
                      ON policy.schema_name = namespace.nspname
                     AND policy.table_name = relation.relname
                    WHERE relation.relkind IN ('r', 'p')
                      AND NOT relation.relispartition
                      AND namespace.nspname NOT IN ('audit', 'pg_catalog', 'information_schema')
                      AND namespace.nspname NOT LIKE 'pg_toast%'
                      AND namespace.nspname NOT LIKE 'pg_temp_%'
                      AND relation.relname <> '__EFMigrationsHistory'
                      AND COALESCE(policy.enabled, true)
                      AND NOT EXISTS (
                          SELECT 1
                          FROM pg_trigger AS existing_trigger
                          WHERE existing_trigger.tgrelid = relation.oid
                            AND existing_trigger.tgname = 'xframework_audit_row'
                            AND NOT existing_trigger.tgisinternal)
                    ORDER BY namespace.nspname, relation.relname;
                $function$;

                CREATE TRIGGER xframework_audit_policy_row
                AFTER INSERT OR UPDATE OR DELETE ON audit.table_policy
                FOR EACH ROW EXECUTE FUNCTION audit.capture_row_change();

                SELECT audit.ensure_table_triggers();

                REVOKE UPDATE, DELETE, TRUNCATE ON audit.event FROM PUBLIC;
                REVOKE INSERT ON audit.event FROM PUBLIC;
                REVOKE ALL ON audit.table_policy FROM PUBLIC;
                REVOKE EXECUTE ON FUNCTION audit.capture_row_change() FROM PUBLIC;
                REVOKE EXECUTE ON FUNCTION audit.reject_event_mutation() FROM PUBLIC;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SCHEMA IF EXISTS audit CASCADE;");
        }
    }
}
