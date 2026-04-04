using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OrvixFlow.Infrastructure.Data.Migrations.AddImageSupport
{
    /// <inheritdoc />
    public partial class AddOverrideSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AddColumn<bool>(
                name: "IsPubliclyVisible",
                table: "PlanTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "PlanTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LimitDescription",
                table: "PlanModuleInclusions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxItemsTotal",
                table: "PlanModuleInclusions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxUsagePerMonth",
                table: "PlanModuleInclusions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IconKey",
                table: "ModuleDefinitions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "ModuleDefinitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UpgradePromptText",
                table: "ModuleDefinitions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TraceId",
                table: "InboxEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActorUserId",
                table: "AuditTrails",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AfterJson",
                table: "AuditTrails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BeforeJson",
                table: "AuditTrails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityType",
                table: "AuditTrails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OverrideEntityId",
                table: "AuditTrails",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CompanyEntitlementOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaxSeats = table.Column<int>(type: "integer", nullable: true),
                    MaxMonthlyTokens = table.Column<int>(type: "integer", nullable: true),
                    MaxApiRequestsPerDay = table.Column<int>(type: "integer", nullable: true),
                    MaxStorageMb = table.Column<int>(type: "integer", nullable: true),
                    MaxKnowledgeBases = table.Column<int>(type: "integer", nullable: true),
                    MaxInboxMessages = table.Column<int>(type: "integer", nullable: true),
                    MaxMailboxConnections = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "text", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyEntitlementOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyEntitlementOverrides_Tenants_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompanyEntitlementOverrides_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompanyModuleOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyModuleOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyModuleOverrides_ModuleDefinitions_ModuleDefinitionId",
                        column: x => x.ModuleDefinitionId,
                        principalTable: "ModuleDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompanyModuleOverrides_Tenants_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompanyModuleOverrides_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedAt", "IconKey", "SortOrder", "UpgradePromptText" },
                values: new object[] { new DateTime(2026, 4, 2, 11, 16, 6, 409, DateTimeKind.Utc).AddTicks(8569), null, 0, null });

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                columns: new[] { "CreatedAt", "IconKey", "SortOrder", "UpgradePromptText" },
                values: new object[] { new DateTime(2026, 4, 2, 11, 16, 6, 411, DateTimeKind.Utc).AddTicks(2418), null, 0, null });

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                columns: new[] { "CreatedAt", "IconKey", "SortOrder", "UpgradePromptText" },
                values: new object[] { new DateTime(2026, 4, 2, 11, 16, 6, 411, DateTimeKind.Utc).AddTicks(2491), null, 0, null });

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                columns: new[] { "CreatedAt", "IconKey", "SortOrder", "UpgradePromptText" },
                values: new object[] { new DateTime(2026, 4, 2, 11, 16, 6, 411, DateTimeKind.Utc).AddTicks(2522), null, 0, null });

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                columns: new[] { "CreatedAt", "IconKey", "SortOrder", "UpgradePromptText" },
                values: new object[] { new DateTime(2026, 4, 2, 11, 16, 6, 411, DateTimeKind.Utc).AddTicks(2553), null, 0, null });

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"),
                columns: new[] { "CreatedAt", "IconKey", "SortOrder", "UpgradePromptText" },
                values: new object[] { new DateTime(2026, 4, 2, 11, 16, 6, 411, DateTimeKind.Utc).AddTicks(2585), null, 0, null });

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"),
                columns: new[] { "CreatedAt", "IconKey", "SortOrder", "UpgradePromptText" },
                values: new object[] { new DateTime(2026, 4, 2, 11, 16, 6, 411, DateTimeKind.Utc).AddTicks(2615), null, 0, null });

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"),
                columns: new[] { "CreatedAt", "IconKey", "SortOrder", "UpgradePromptText" },
                values: new object[] { new DateTime(2026, 4, 2, 11, 16, 6, 411, DateTimeKind.Utc).AddTicks(2643), null, 0, null });

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"),
                columns: new[] { "CreatedAt", "IconKey", "SortOrder", "UpgradePromptText" },
                values: new object[] { new DateTime(2026, 4, 2, 11, 16, 6, 411, DateTimeKind.Utc).AddTicks(2671), null, 0, null });

            migrationBuilder.UpdateData(
                table: "PlanTemplates",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                columns: new[] { "IsPubliclyVisible", "SortOrder" },
                values: new object[] { true, 0 });

            migrationBuilder.UpdateData(
                table: "PlanTemplates",
                keyColumn: "Id",
                keyValue: new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                columns: new[] { "IsPubliclyVisible", "SortOrder" },
                values: new object[] { true, 0 });

            migrationBuilder.UpdateData(
                table: "PlanTemplates",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                columns: new[] { "IsPubliclyVisible", "SortOrder" },
                values: new object[] { true, 0 });

            migrationBuilder.UpdateData(
                table: "PlanTemplates",
                keyColumn: "Id",
                keyValue: new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                columns: new[] { "IsPubliclyVisible", "SortOrder" },
                values: new object[] { true, 0 });

            migrationBuilder.UpdateData(
                table: "PlanTemplates",
                keyColumn: "Id",
                keyValue: new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                columns: new[] { "IsPubliclyVisible", "SortOrder" },
                values: new object[] { true, 0 });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyEntitlementOverrides_CompanyId",
                table: "CompanyEntitlementOverrides",
                column: "CompanyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyEntitlementOverrides_CreatedByUserId",
                table: "CompanyEntitlementOverrides",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyModuleOverrides_CompanyId_ModuleDefinitionId",
                table: "CompanyModuleOverrides",
                columns: new[] { "CompanyId", "ModuleDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyModuleOverrides_CreatedByUserId",
                table: "CompanyModuleOverrides",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyModuleOverrides_ModuleDefinitionId",
                table: "CompanyModuleOverrides",
                column: "ModuleDefinitionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyEntitlementOverrides");

            migrationBuilder.DropTable(
                name: "CompanyModuleOverrides");

            migrationBuilder.DropColumn(
                name: "IsPubliclyVisible",
                table: "PlanTemplates");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "PlanTemplates");

            migrationBuilder.DropColumn(
                name: "LimitDescription",
                table: "PlanModuleInclusions");

            migrationBuilder.DropColumn(
                name: "MaxItemsTotal",
                table: "PlanModuleInclusions");

            migrationBuilder.DropColumn(
                name: "MaxUsagePerMonth",
                table: "PlanModuleInclusions");

            migrationBuilder.DropColumn(
                name: "IconKey",
                table: "ModuleDefinitions");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "ModuleDefinitions");

            migrationBuilder.DropColumn(
                name: "UpgradePromptText",
                table: "ModuleDefinitions");

            migrationBuilder.DropColumn(
                name: "TraceId",
                table: "InboxEvents");

            migrationBuilder.DropColumn(
                name: "ActorUserId",
                table: "AuditTrails");

            migrationBuilder.DropColumn(
                name: "AfterJson",
                table: "AuditTrails");

            migrationBuilder.DropColumn(
                name: "BeforeJson",
                table: "AuditTrails");

            migrationBuilder.DropColumn(
                name: "EntityType",
                table: "AuditTrails");

            migrationBuilder.DropColumn(
                name: "OverrideEntityId",
                table: "AuditTrails");

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 2, 6, 43, 31, 597, DateTimeKind.Utc).AddTicks(2065));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 2, 6, 43, 31, 612, DateTimeKind.Utc).AddTicks(3108));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 2, 6, 43, 31, 612, DateTimeKind.Utc).AddTicks(3157));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 2, 6, 43, 31, 612, DateTimeKind.Utc).AddTicks(3187));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 2, 6, 43, 31, 612, DateTimeKind.Utc).AddTicks(3216));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 2, 6, 43, 31, 612, DateTimeKind.Utc).AddTicks(3244));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 2, 6, 43, 31, 612, DateTimeKind.Utc).AddTicks(3273));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 2, 6, 43, 31, 612, DateTimeKind.Utc).AddTicks(3304));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 2, 6, 43, 31, 612, DateTimeKind.Utc).AddTicks(3334));
        }
    }
}
