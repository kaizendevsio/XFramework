using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddStorageModuleV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Storage",
                table: "StorageFileType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Storage",
                table: "StorageFileType",
                type: "boolean",
                nullable: false,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Storage",
                table: "StorageFileIdentifierGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Storage",
                table: "StorageFileIdentifierGroup",
                type: "boolean",
                nullable: false,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Storage",
                table: "StorageFileIdentifier",
                type: "boolean",
                nullable: false,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Storage",
                table: "StorageFile",
                type: "boolean",
                nullable: false,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<string>(
                name: "BucketName",
                schema: "Storage",
                table: "StorageFile",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CdnBaseUrl",
                schema: "Storage",
                table: "StorageFile",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                schema: "Storage",
                table: "StorageFile",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ContentLengthBytes",
                schema: "Storage",
                table: "StorageFile",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DownloadUrlExpiresAt",
                schema: "Storage",
                table: "StorageFile",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ETag",
                schema: "Storage",
                table: "StorageFile",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObjectKey",
                schema: "Storage",
                table: "StorageFile",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ObjectDeletedAt",
                schema: "Storage",
                table: "StorageFile",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProviderProfileId",
                schema: "Storage",
                table: "StorageFile",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderProfileName",
                schema: "Storage",
                table: "StorageFile",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicUrl",
                schema: "Storage",
                table: "StorageFile",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionUntil",
                schema: "Storage",
                table: "StorageFile",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sha256Hash",
                schema: "Storage",
                table: "StorageFile",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "Storage",
                table: "StorageFile",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantBucketId",
                schema: "Storage",
                table: "StorageFile",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UploadStartedAt",
                schema: "Storage",
                table: "StorageFile",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UploadedAt",
                schema: "Storage",
                table: "StorageFile",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Visibility",
                schema: "Storage",
                table: "StorageFile",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "StorageProviderProfile",
                schema: "Storage",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Endpoint = table.Column<string>(type: "text", nullable: true),
                    Region = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AccessKeyId = table.Column<string>(type: "text", nullable: true),
                    SecretAccessKey = table.Column<string>(type: "text", nullable: true),
                    ConnectionString = table.Column<string>(type: "text", nullable: true),
                    AccessKeyIdSecretName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SecretAccessKeySecretName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConnectionStringSecretName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    BucketPrefix = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PublicBaseUrl = table.Column<string>(type: "text", nullable: true),
                    CdnBaseUrl = table.Column<string>(type: "text", nullable: true),
                    UsePathStyle = table.Column<bool>(type: "boolean", nullable: false),
                    AutoCreateBuckets = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("storageproviderprofile_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "StorageUploadSession",
                schema: "Storage",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    StorageFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderUploadId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ChunkSizeBytes = table.Column<int>(type: "integer", nullable: false),
                    TotalSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    TotalParts = table.Column<int>(type: "integer", nullable: false),
                    ExpectedSha256Hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AbortedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("storageuploadsession_pk", x => x.ID);
                    table.ForeignKey(
                        name: "storageuploadsession_storagefile_id_fk",
                        column: x => x.StorageFileId,
                        principalSchema: "Storage",
                        principalTable: "StorageFile",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StorageTenantBucket",
                schema: "Storage",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ProviderProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    BucketName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PublicBaseUrl = table.Column<string>(type: "text", nullable: true),
                    CdnBaseUrl = table.Column<string>(type: "text", nullable: true),
                    LastEnsuredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("storagetenantbucket_pk", x => x.ID);
                    table.ForeignKey(
                        name: "storagetenantbucket_storageproviderprofile_id_fk",
                        column: x => x.ProviderProfileId,
                        principalSchema: "Storage",
                        principalTable: "StorageProviderProfile",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StorageUploadPart",
                schema: "Storage",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    UploadSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartNumber = table.Column<int>(type: "integer", nullable: false),
                    OffsetBytes = table.Column<long>(type: "bigint", nullable: false),
                    SizeBytes = table.Column<int>(type: "integer", nullable: false),
                    Sha256Hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderPartId = table.Column<string>(type: "text", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("storageuploadpart_pk", x => x.ID);
                    table.ForeignKey(
                        name: "storageuploadpart_storageuploadsession_id_fk",
                        column: x => x.UploadSessionId,
                        principalSchema: "Storage",
                        principalTable: "StorageUploadSession",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StorageFile_ProviderProfileId",
                schema: "Storage",
                table: "StorageFile",
                column: "ProviderProfileId");

            migrationBuilder.CreateIndex(
                name: "ix_storagefile_tenant_bucket_object",
                schema: "Storage",
                table: "StorageFile",
                columns: new[] { "TenantId", "BucketName", "ObjectKey" });

            migrationBuilder.CreateIndex(
                name: "ix_storagefile_tenant_identifier",
                schema: "Storage",
                table: "StorageFile",
                columns: new[] { "TenantId", "Identifier" });

            migrationBuilder.CreateIndex(
                name: "ix_storagefile_tenant_retention_objectdeleted",
                schema: "Storage",
                table: "StorageFile",
                columns: new[] { "TenantId", "RetentionUntil", "ObjectDeletedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_storagefile_tenant_status_visibility",
                schema: "Storage",
                table: "StorageFile",
                columns: new[] { "TenantId", "Status", "Visibility" });

            migrationBuilder.CreateIndex(
                name: "IX_StorageFile_TenantBucketId",
                schema: "Storage",
                table: "StorageFile",
                column: "TenantBucketId");

            migrationBuilder.CreateIndex(
                name: "ix_storageproviderprofile_tenant_default",
                schema: "Storage",
                table: "StorageProviderProfile",
                columns: new[] { "TenantId", "IsDefault" },
                unique: true,
                filter: "\"IsDefault\" = true AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "ix_storageproviderprofile_tenant_name",
                schema: "Storage",
                table: "StorageProviderProfile",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "ix_storagetenantbucket_bucket",
                schema: "Storage",
                table: "StorageTenantBucket",
                column: "BucketName",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_StorageTenantBucket_ProviderProfileId",
                schema: "Storage",
                table: "StorageTenantBucket",
                column: "ProviderProfileId");

            migrationBuilder.CreateIndex(
                name: "ix_storagetenantbucket_tenant_provider",
                schema: "Storage",
                table: "StorageTenantBucket",
                columns: new[] { "TenantId", "ProviderProfileId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "ix_storageuploadpart_session_part",
                schema: "Storage",
                table: "StorageUploadPart",
                columns: new[] { "UploadSessionId", "PartNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StorageUploadSession_StorageFileId",
                schema: "Storage",
                table: "StorageUploadSession",
                column: "StorageFileId");

            migrationBuilder.CreateIndex(
                name: "ix_storageuploadsession_tenant_status_expires",
                schema: "Storage",
                table: "StorageUploadSession",
                columns: new[] { "TenantId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "ix_storageuploadsession_uploadid",
                schema: "Storage",
                table: "StorageUploadSession",
                column: "UploadId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "storagefile_storageproviderprofile_id_fk",
                schema: "Storage",
                table: "StorageFile",
                column: "ProviderProfileId",
                principalSchema: "Storage",
                principalTable: "StorageProviderProfile",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "storagefile_storagetenantbucket_id_fk",
                schema: "Storage",
                table: "StorageFile",
                column: "TenantBucketId",
                principalSchema: "Storage",
                principalTable: "StorageTenantBucket",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "storagefile_storageproviderprofile_id_fk",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropForeignKey(
                name: "storagefile_storagetenantbucket_id_fk",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropTable(
                name: "StorageTenantBucket",
                schema: "Storage");

            migrationBuilder.DropTable(
                name: "StorageUploadPart",
                schema: "Storage");

            migrationBuilder.DropTable(
                name: "StorageProviderProfile",
                schema: "Storage");

            migrationBuilder.DropTable(
                name: "StorageUploadSession",
                schema: "Storage");

            migrationBuilder.DropIndex(
                name: "IX_StorageFile_ProviderProfileId",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropIndex(
                name: "ix_storagefile_tenant_bucket_object",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropIndex(
                name: "ix_storagefile_tenant_identifier",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropIndex(
                name: "ix_storagefile_tenant_retention_objectdeleted",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropIndex(
                name: "ix_storagefile_tenant_status_visibility",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropIndex(
                name: "IX_StorageFile_TenantBucketId",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropColumn(
                name: "BucketName",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropColumn(
                name: "CdnBaseUrl",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropColumn(
                name: "ContentLengthBytes",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropColumn(
                name: "DownloadUrlExpiresAt",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropColumn(
                name: "ETag",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropColumn(
                name: "ObjectKey",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropColumn(
                name: "ObjectDeletedAt",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropColumn(
                name: "ProviderProfileId",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropColumn(
                name: "ProviderProfileName",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropColumn(
                name: "PublicUrl",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropColumn(
                name: "RetentionUntil",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropColumn(
                name: "Sha256Hash",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropColumn(
                name: "TenantBucketId",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropColumn(
                name: "UploadStartedAt",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropColumn(
                name: "UploadedAt",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropColumn(
                name: "Visibility",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.AlterColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Storage",
                table: "StorageFileType",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Storage",
                table: "StorageFileType",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "false");

            migrationBuilder.AlterColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Storage",
                table: "StorageFileIdentifierGroup",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Storage",
                table: "StorageFileIdentifierGroup",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "false");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Storage",
                table: "StorageFileIdentifier",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "false");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Storage",
                table: "StorageFile",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "false");
        }
    }
}
