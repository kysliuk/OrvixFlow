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
            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("04cf6acd-fcc6-4418-842b-38e9e04122f8"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("0cab4a3f-180e-4672-b56c-73e1625cf4ac"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("1d5d43dc-c37a-4c8a-827c-ee6e0ccefd72"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("3f71c390-df32-4162-ad11-f5dd29d07a68"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("43719c9d-0422-452b-ab86-0f50c70d3803"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("4da1ef92-6f77-40fe-86e4-10e0c6449fc0"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("5e9044bf-a66e-4b12-a7da-8519bf61c992"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("69bae2de-8eb3-46f7-954a-bb834ee8cc82"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("6ed4ddcf-b763-4b6b-a1de-eb63e08e80a3"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("73eecd08-6438-42fb-be9d-ac94d80510ed"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("75923ec8-907d-49e2-adaa-8415060f9bd5"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("7d53516a-d145-41a6-8c8f-1cec5d7cd428"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("9a6ef5fd-df2d-4f08-915c-b9111c69adeb"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("9c1148e1-df79-4d38-a581-b2af85c42c46"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("b130c2a1-c7f1-4396-a95c-04549a99639f"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("bfd6a1a1-0a5f-4039-a4b7-ab262cff8063"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("c39de785-3818-48c5-bcb2-41e02c016b8f"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("eb3f0ab8-ebbc-40ef-91b1-c5765ab7e838"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("f11a7213-83cd-41e2-8393-cdb402fb8c0d"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("fa6896b6-8c7e-482d-8f01-4a46121701ec"));

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

            migrationBuilder.InsertData(
                table: "PlanModuleInclusions",
                columns: new[] { "Id", "CreatedAt", "LimitDescription", "MaxItemsTotal", "MaxUsagePerMonth", "ModuleDefinitionId", "PlanTemplateId" },
                values: new object[,]
                {
                    { new Guid("0b88fd03-6554-4709-84ee-904341db5638"), new DateTime(2026, 4, 2, 11, 16, 6, 441, DateTimeKind.Utc).AddTicks(9349), null, null, null, new Guid("11111111-1111-1111-1111-111111111111"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("0dccfe87-3bd7-48f1-9fe4-c611370add52"), new DateTime(2026, 4, 2, 11, 16, 6, 441, DateTimeKind.Utc).AddTicks(9374), null, null, null, new Guid("44444444-4444-4444-4444-444444444444"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("0ffa41e0-9d9f-44d6-b7dc-d64b9374ccc1"), new DateTime(2026, 4, 2, 11, 16, 6, 441, DateTimeKind.Utc).AddTicks(9172), null, null, null, new Guid("33333333-3333-3333-3333-333333333333"), new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") },
                    { new Guid("120e8544-f88b-4fac-966f-86dc4bc10980"), new DateTime(2026, 4, 2, 11, 16, 6, 441, DateTimeKind.Utc).AddTicks(9622), null, null, null, new Guid("66666666-6666-6666-6666-666666666666"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("290b3088-92ba-43fa-984c-9ffc9541f07f"), new DateTime(2026, 4, 2, 11, 16, 6, 441, DateTimeKind.Utc).AddTicks(9200), null, null, null, new Guid("11111111-1111-1111-1111-111111111111"), new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") },
                    { new Guid("318bc210-692e-4e50-a6bb-d4dd156fe3fd"), new DateTime(2026, 4, 2, 11, 16, 6, 441, DateTimeKind.Utc).AddTicks(9470), null, null, null, new Guid("33333333-3333-3333-3333-333333333333"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("3393a4b4-08c2-4133-a4c6-8585cf38b9a3"), new DateTime(2026, 4, 2, 11, 16, 6, 441, DateTimeKind.Utc).AddTicks(9400), null, null, null, new Guid("22222222-2222-2222-2222-222222222222"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("3753ab13-d2c6-483c-b9dc-9d4fd9a25d71"), new DateTime(2026, 4, 2, 11, 16, 6, 441, DateTimeKind.Utc).AddTicks(9597), null, null, null, new Guid("77777777-7777-7777-7777-777777777777"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("41c53aab-f026-4538-b655-a6040c737265"), new DateTime(2026, 4, 2, 11, 16, 6, 441, DateTimeKind.Utc).AddTicks(9495), null, null, null, new Guid("11111111-1111-1111-1111-111111111111"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("44391baf-85a1-4358-a0b6-56fcd0d5bb9c"), new DateTime(2026, 4, 2, 11, 16, 6, 441, DateTimeKind.Utc).AddTicks(9545), null, null, null, new Guid("22222222-2222-2222-2222-222222222222"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("4668e621-ab9b-42b2-bbeb-a09abfe53c02"), new DateTime(2026, 4, 2, 11, 16, 6, 441, DateTimeKind.Utc).AddTicks(9572), null, null, null, new Guid("55555555-5555-5555-5555-555555555555"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("532dda51-af84-4886-8b5d-9edc5c6b1635"), new DateTime(2026, 4, 2, 11, 16, 6, 441, DateTimeKind.Utc).AddTicks(9251), null, null, null, new Guid("11111111-1111-1111-1111-111111111111"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") },
                    { new Guid("6d8e30b7-ee7c-4c4f-8b20-91cd48508549"), new DateTime(2026, 4, 2, 11, 16, 6, 441, DateTimeKind.Utc).AddTicks(9520), null, null, null, new Guid("44444444-4444-4444-4444-444444444444"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("817389cc-bafb-4f26-8f20-a2137776a7e4"), new DateTime(2026, 4, 2, 11, 16, 6, 441, DateTimeKind.Utc).AddTicks(9324), null, null, null, new Guid("33333333-3333-3333-3333-333333333333"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("83594ab9-c8de-4394-96bf-11d7dbf2fab2"), new DateTime(2026, 4, 2, 11, 16, 6, 441, DateTimeKind.Utc).AddTicks(9226), null, null, null, new Guid("33333333-3333-3333-3333-333333333333"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") },
                    { new Guid("98d6f549-50a9-47a9-a000-5344be78a9ff"), new DateTime(2026, 4, 2, 11, 16, 6, 441, DateTimeKind.Utc).AddTicks(6303), null, null, null, new Guid("33333333-3333-3333-3333-333333333333"), new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa") },
                    { new Guid("a6c72f94-5a17-41a4-a080-b1a6a6c3ea48"), new DateTime(2026, 4, 2, 11, 16, 6, 441, DateTimeKind.Utc).AddTicks(9274), null, null, null, new Guid("44444444-4444-4444-4444-444444444444"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") },
                    { new Guid("b395775b-3db7-4950-a629-f25ae13d7d6a"), new DateTime(2026, 4, 2, 11, 16, 6, 441, DateTimeKind.Utc).AddTicks(9299), null, null, null, new Guid("22222222-2222-2222-2222-222222222222"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") },
                    { new Guid("bdcb3f88-2346-4121-aa5f-d1e43d9f2df0"), new DateTime(2026, 4, 2, 11, 16, 6, 441, DateTimeKind.Utc).AddTicks(9423), null, null, null, new Guid("55555555-5555-5555-5555-555555555555"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("de6b7bc5-e229-4852-9e71-b3f011a38e1f"), new DateTime(2026, 4, 2, 11, 16, 6, 441, DateTimeKind.Utc).AddTicks(9448), null, null, null, new Guid("77777777-7777-7777-7777-777777777777"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") }
                });

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

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("0b88fd03-6554-4709-84ee-904341db5638"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("0dccfe87-3bd7-48f1-9fe4-c611370add52"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("0ffa41e0-9d9f-44d6-b7dc-d64b9374ccc1"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("120e8544-f88b-4fac-966f-86dc4bc10980"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("290b3088-92ba-43fa-984c-9ffc9541f07f"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("318bc210-692e-4e50-a6bb-d4dd156fe3fd"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("3393a4b4-08c2-4133-a4c6-8585cf38b9a3"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("3753ab13-d2c6-483c-b9dc-9d4fd9a25d71"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("41c53aab-f026-4538-b655-a6040c737265"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("44391baf-85a1-4358-a0b6-56fcd0d5bb9c"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("4668e621-ab9b-42b2-bbeb-a09abfe53c02"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("532dda51-af84-4886-8b5d-9edc5c6b1635"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("6d8e30b7-ee7c-4c4f-8b20-91cd48508549"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("817389cc-bafb-4f26-8f20-a2137776a7e4"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("83594ab9-c8de-4394-96bf-11d7dbf2fab2"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("98d6f549-50a9-47a9-a000-5344be78a9ff"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a6c72f94-5a17-41a4-a080-b1a6a6c3ea48"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("b395775b-3db7-4950-a629-f25ae13d7d6a"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("bdcb3f88-2346-4121-aa5f-d1e43d9f2df0"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("de6b7bc5-e229-4852-9e71-b3f011a38e1f"));

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

            migrationBuilder.InsertData(
                table: "PlanModuleInclusions",
                columns: new[] { "Id", "CreatedAt", "ModuleDefinitionId", "PlanTemplateId" },
                values: new object[,]
                {
                    { new Guid("04cf6acd-fcc6-4418-842b-38e9e04122f8"), new DateTime(2026, 4, 2, 6, 43, 31, 666, DateTimeKind.Utc).AddTicks(8591), new Guid("66666666-6666-6666-6666-666666666666"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("0cab4a3f-180e-4672-b56c-73e1625cf4ac"), new DateTime(2026, 4, 2, 6, 43, 31, 666, DateTimeKind.Utc).AddTicks(8490), new Guid("44444444-4444-4444-4444-444444444444"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("1d5d43dc-c37a-4c8a-827c-ee6e0ccefd72"), new DateTime(2026, 4, 2, 6, 43, 31, 664, DateTimeKind.Utc).AddTicks(4579), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa") },
                    { new Guid("3f71c390-df32-4162-ad11-f5dd29d07a68"), new DateTime(2026, 4, 2, 6, 43, 31, 666, DateTimeKind.Utc).AddTicks(8306), new Guid("44444444-4444-4444-4444-444444444444"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("43719c9d-0422-452b-ab86-0f50c70d3803"), new DateTime(2026, 4, 2, 6, 43, 31, 666, DateTimeKind.Utc).AddTicks(8466), new Guid("11111111-1111-1111-1111-111111111111"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("4da1ef92-6f77-40fe-86e4-10e0c6449fc0"), new DateTime(2026, 4, 2, 6, 43, 31, 666, DateTimeKind.Utc).AddTicks(8365), new Guid("22222222-2222-2222-2222-222222222222"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("5e9044bf-a66e-4b12-a7da-8519bf61c992"), new DateTime(2026, 4, 2, 6, 43, 31, 666, DateTimeKind.Utc).AddTicks(8158), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") },
                    { new Guid("69bae2de-8eb3-46f7-954a-bb834ee8cc82"), new DateTime(2026, 4, 2, 6, 43, 31, 666, DateTimeKind.Utc).AddTicks(8106), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") },
                    { new Guid("6ed4ddcf-b763-4b6b-a1de-eb63e08e80a3"), new DateTime(2026, 4, 2, 6, 43, 31, 666, DateTimeKind.Utc).AddTicks(8541), new Guid("55555555-5555-5555-5555-555555555555"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("73eecd08-6438-42fb-be9d-ac94d80510ed"), new DateTime(2026, 4, 2, 6, 43, 31, 666, DateTimeKind.Utc).AddTicks(8135), new Guid("11111111-1111-1111-1111-111111111111"), new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") },
                    { new Guid("75923ec8-907d-49e2-adaa-8415060f9bd5"), new DateTime(2026, 4, 2, 6, 43, 31, 666, DateTimeKind.Utc).AddTicks(8256), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("7d53516a-d145-41a6-8c8f-1cec5d7cd428"), new DateTime(2026, 4, 2, 6, 43, 31, 666, DateTimeKind.Utc).AddTicks(8566), new Guid("77777777-7777-7777-7777-777777777777"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("9a6ef5fd-df2d-4f08-915c-b9111c69adeb"), new DateTime(2026, 4, 2, 6, 43, 31, 666, DateTimeKind.Utc).AddTicks(8207), new Guid("44444444-4444-4444-4444-444444444444"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") },
                    { new Guid("9c1148e1-df79-4d38-a581-b2af85c42c46"), new DateTime(2026, 4, 2, 6, 43, 31, 666, DateTimeKind.Utc).AddTicks(8415), new Guid("77777777-7777-7777-7777-777777777777"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("b130c2a1-c7f1-4396-a95c-04549a99639f"), new DateTime(2026, 4, 2, 6, 43, 31, 666, DateTimeKind.Utc).AddTicks(8232), new Guid("22222222-2222-2222-2222-222222222222"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") },
                    { new Guid("bfd6a1a1-0a5f-4039-a4b7-ab262cff8063"), new DateTime(2026, 4, 2, 6, 43, 31, 666, DateTimeKind.Utc).AddTicks(8390), new Guid("55555555-5555-5555-5555-555555555555"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("c39de785-3818-48c5-bcb2-41e02c016b8f"), new DateTime(2026, 4, 2, 6, 43, 31, 666, DateTimeKind.Utc).AddTicks(8516), new Guid("22222222-2222-2222-2222-222222222222"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("eb3f0ab8-ebbc-40ef-91b1-c5765ab7e838"), new DateTime(2026, 4, 2, 6, 43, 31, 666, DateTimeKind.Utc).AddTicks(8281), new Guid("11111111-1111-1111-1111-111111111111"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("f11a7213-83cd-41e2-8393-cdb402fb8c0d"), new DateTime(2026, 4, 2, 6, 43, 31, 666, DateTimeKind.Utc).AddTicks(8441), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("fa6896b6-8c7e-482d-8f01-4a46121701ec"), new DateTime(2026, 4, 2, 6, 43, 31, 666, DateTimeKind.Utc).AddTicks(8184), new Guid("11111111-1111-1111-1111-111111111111"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") }
                });
        }
    }
}
