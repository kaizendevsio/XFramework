using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Attendance");

            migrationBuilder.Sql(
                """
                INSERT INTO "Identity"."TenantModuleFeature"
                    ("ID", "ModuleKey", "SubFeatureKey", "DisplayName", "Description", "IsEnabled", "IsDeleted", "ConcurrencyStamp", "CreatedAt", "ModifiedAt", "TenantId")
                SELECT
                    uuid_generate_v4(),
                    'attendance',
                    '',
                    'Attendance',
                    'Attendance contexts, sessions, participants, time events, and reports.',
                    true,
                    false,
                    uuid_generate_v4(),
                    now(),
                    now(),
                    tenants."ID"
                FROM "Application"."Application" tenants
                WHERE tenants."IsDeleted" = false
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "Identity"."TenantModuleFeature" existing
                      WHERE existing."TenantId" = tenants."ID"
                        AND existing."ModuleKey" = 'attendance'
                        AND existing."SubFeatureKey" = ''
                  );
                """);

            migrationBuilder.CreateTable(
                name: "AttendancePolicy",
                schema: "Attendance",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    GracePeriodMinutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    EarlyCheckoutGraceMinutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CheckoutRequired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    TimeZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attendance_Policy", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceContext",
                schema: "Attendance",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ContextType = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DefaultPolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attendance_Context", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Attendance_Context_DefaultPolicy",
                        column: x => x.DefaultPolicyId,
                        principalSchema: "Attendance",
                        principalTable: "AttendancePolicy",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceParticipant",
                schema: "Attendance",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReferenceCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attendance_Participant", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Attendance_Participant_Context",
                        column: x => x.ContextId,
                        principalSchema: "Attendance",
                        principalTable: "AttendanceContext",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceSession",
                schema: "Attendance",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attendance_Session", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Attendance_Session_Context",
                        column: x => x.ContextId,
                        principalSchema: "Attendance",
                        principalTable: "AttendanceContext",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Attendance_Session_Policy",
                        column: x => x.PolicyId,
                        principalSchema: "Attendance",
                        principalTable: "AttendancePolicy",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceEvent",
                schema: "Attendance",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedByCredentialId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attendance_Event", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Attendance_Event_Participant",
                        column: x => x.ParticipantId,
                        principalSchema: "Attendance",
                        principalTable: "AttendanceParticipant",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Attendance_Event_Session",
                        column: x => x.SessionId,
                        principalSchema: "Attendance",
                        principalTable: "AttendanceSession",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceRecord",
                schema: "Attendance",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstCheckInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastCheckOutAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsManual = table.Column<bool>(type: "boolean", nullable: false),
                    SourceEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attendance_Record", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Attendance_Record_Participant",
                        column: x => x.ParticipantId,
                        principalSchema: "Attendance",
                        principalTable: "AttendanceParticipant",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Attendance_Record_Session",
                        column: x => x.SessionId,
                        principalSchema: "Attendance",
                        principalTable: "AttendanceSession",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceAdjustment",
                schema: "Attendance",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousStatus = table.Column<int>(type: "integer", nullable: false),
                    NewStatus = table.Column<int>(type: "integer", nullable: false),
                    AdjustedCheckInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AdjustedCheckOutAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActorCredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attendance_Adjustment", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Attendance_Adjustment_Record",
                        column: x => x.RecordId,
                        principalSchema: "Attendance",
                        principalTable: "AttendanceRecord",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAdjustment_RecordId",
                schema: "Attendance",
                table: "AttendanceAdjustment",
                column: "RecordId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAdjustment_Tenant_Actor_Created",
                schema: "Attendance",
                table: "AttendanceAdjustment",
                columns: new[] { "TenantId", "ActorCredentialId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAdjustment_Tenant_Session_Participant_Created",
                schema: "Attendance",
                table: "AttendanceAdjustment",
                columns: new[] { "TenantId", "SessionId", "ParticipantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAdjustment_TenantId",
                schema: "Attendance",
                table: "AttendanceAdjustment",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAdjustment_TenantId_IsDeleted",
                schema: "Attendance",
                table: "AttendanceAdjustment",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceContext_DefaultPolicyId",
                schema: "Attendance",
                table: "AttendanceContext",
                column: "DefaultPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceContext_Tenant_Type_Active",
                schema: "Attendance",
                table: "AttendanceContext",
                columns: new[] { "TenantId", "ContextType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceContext_TenantId",
                schema: "Attendance",
                table: "AttendanceContext",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceContext_TenantId_IsDeleted",
                schema: "Attendance",
                table: "AttendanceContext",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UX_AttendanceContext_Tenant_Code_Active",
                schema: "Attendance",
                table: "AttendanceContext",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Code\" IS NOT NULL AND \"Code\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceEvent_ParticipantId",
                schema: "Attendance",
                table: "AttendanceEvent",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceEvent_SessionId",
                schema: "Attendance",
                table: "AttendanceEvent",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceEvent_Tenant_Credential_Occurred",
                schema: "Attendance",
                table: "AttendanceEvent",
                columns: new[] { "TenantId", "CredentialId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceEvent_Tenant_Session_Participant_Occurred",
                schema: "Attendance",
                table: "AttendanceEvent",
                columns: new[] { "TenantId", "SessionId", "ParticipantId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceEvent_TenantId",
                schema: "Attendance",
                table: "AttendanceEvent",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceEvent_TenantId_IsDeleted",
                schema: "Attendance",
                table: "AttendanceEvent",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UX_AttendanceEvent_Tenant_IdempotencyKey",
                schema: "Attendance",
                table: "AttendanceEvent",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceParticipant_ContextId",
                schema: "Attendance",
                table: "AttendanceParticipant",
                column: "ContextId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceParticipant_Tenant_Context_Reference",
                schema: "Attendance",
                table: "AttendanceParticipant",
                columns: new[] { "TenantId", "ContextId", "ReferenceCode" },
                filter: "\"ReferenceCode\" IS NOT NULL AND \"ReferenceCode\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceParticipant_Tenant_Credential_Active",
                schema: "Attendance",
                table: "AttendanceParticipant",
                columns: new[] { "TenantId", "CredentialId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceParticipant_TenantId",
                schema: "Attendance",
                table: "AttendanceParticipant",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceParticipant_TenantId_IsDeleted",
                schema: "Attendance",
                table: "AttendanceParticipant",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UX_AttendanceParticipant_Tenant_Context_Credential_Active",
                schema: "Attendance",
                table: "AttendanceParticipant",
                columns: new[] { "TenantId", "ContextId", "CredentialId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePolicy_TenantId",
                schema: "Attendance",
                table: "AttendancePolicy",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePolicy_TenantId_IsDeleted",
                schema: "Attendance",
                table: "AttendancePolicy",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UX_AttendancePolicy_Tenant_Name_Active",
                schema: "Attendance",
                table: "AttendancePolicy",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecord_ParticipantId",
                schema: "Attendance",
                table: "AttendanceRecord",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecord_SessionId",
                schema: "Attendance",
                table: "AttendanceRecord",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecord_Tenant_Credential_Session",
                schema: "Attendance",
                table: "AttendanceRecord",
                columns: new[] { "TenantId", "CredentialId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecord_Tenant_Status_Session",
                schema: "Attendance",
                table: "AttendanceRecord",
                columns: new[] { "TenantId", "Status", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecord_TenantId",
                schema: "Attendance",
                table: "AttendanceRecord",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecord_TenantId_IsDeleted",
                schema: "Attendance",
                table: "AttendanceRecord",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UX_AttendanceRecord_Tenant_Session_Participant",
                schema: "Attendance",
                table: "AttendanceRecord",
                columns: new[] { "TenantId", "SessionId", "ParticipantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSession_ContextId",
                schema: "Attendance",
                table: "AttendanceSession",
                column: "ContextId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSession_PolicyId",
                schema: "Attendance",
                table: "AttendanceSession",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSession_Tenant_Context_Start",
                schema: "Attendance",
                table: "AttendanceSession",
                columns: new[] { "TenantId", "ContextId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSession_TenantId",
                schema: "Attendance",
                table: "AttendanceSession",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSession_TenantId_IsDeleted",
                schema: "Attendance",
                table: "AttendanceSession",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UX_AttendanceSession_Tenant_Context_Code_Active",
                schema: "Attendance",
                table: "AttendanceSession",
                columns: new[] { "TenantId", "ContextId", "Code" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Code\" IS NOT NULL AND \"Code\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceAdjustment",
                schema: "Attendance");

            migrationBuilder.DropTable(
                name: "AttendanceEvent",
                schema: "Attendance");

            migrationBuilder.DropTable(
                name: "AttendanceRecord",
                schema: "Attendance");

            migrationBuilder.DropTable(
                name: "AttendanceParticipant",
                schema: "Attendance");

            migrationBuilder.DropTable(
                name: "AttendanceSession",
                schema: "Attendance");

            migrationBuilder.DropTable(
                name: "AttendanceContext",
                schema: "Attendance");

            migrationBuilder.DropTable(
                name: "AttendancePolicy",
                schema: "Attendance");

            migrationBuilder.Sql(
                """
                DELETE FROM "Identity"."TenantModuleFeature"
                WHERE "ModuleKey" = 'attendance'
                  AND "SubFeatureKey" = '';
                """);
        }
    }
}
