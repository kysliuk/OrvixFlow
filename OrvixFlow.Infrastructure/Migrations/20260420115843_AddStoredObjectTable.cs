using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrvixFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStoredObjectTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StoredObjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Module = table.Column<string>(type: "text", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageProvider = table.Column<string>(type: "text", nullable: false),
                    ContainerOrBucket = table.Column<string>(type: "text", nullable: false),
                    StorageKey = table.Column<string>(type: "text", nullable: false),
                    OriginalFileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "text", nullable: false),
                    VirusScanStatus = table.Column<string>(type: "text", nullable: false),
                    LifecycleStatus = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredObjects", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoredObjects_StorageKey",
                table: "StoredObjects",
                column: "StorageKey");

            migrationBuilder.CreateIndex(
                name: "IX_StoredObjects_TenantId_EntityType_EntityId",
                table: "StoredObjects",
                columns: new[] { "TenantId", "EntityType", "EntityId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoredObjects");
        }
    }
}
