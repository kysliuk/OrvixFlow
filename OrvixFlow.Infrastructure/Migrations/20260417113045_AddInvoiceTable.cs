using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrvixFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxInboxMessagesPerMonth",
                table: "PlanEntitlements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxMailboxConnections",
                table: "PlanEntitlements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ExternalCustomerId",
                table: "CompanySubscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalInvoiceId = table.Column<string>(type: "text", nullable: false),
                    AmountCents = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    InvoicePdfUrl = table.Column<string>(type: "text", nullable: true),
                    InvoiceUrl = table.Column<string>(type: "text", nullable: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinalizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_Tenants_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantWebhookLimits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CallbackCount = table.Column<int>(type: "integer", nullable: false),
                    WindowStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Limit = table.Column<int>(type: "integer", nullable: false),
                    LastResetUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantWebhookLimits", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 673, DateTimeKind.Utc).AddTicks(7979));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 673, DateTimeKind.Utc).AddTicks(8012));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 673, DateTimeKind.Utc).AddTicks(8033));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 673, DateTimeKind.Utc).AddTicks(8054));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 673, DateTimeKind.Utc).AddTicks(8074));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 673, DateTimeKind.Utc).AddTicks(8095));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 673, DateTimeKind.Utc).AddTicks(8116));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 673, DateTimeKind.Utc).AddTicks(8137));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 673, DateTimeKind.Utc).AddTicks(8157));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 673, DateTimeKind.Utc).AddTicks(691));

            migrationBuilder.UpdateData(
                table: "PlanEntitlements",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "MaxInboxMessagesPerMonth", "MaxMailboxConnections" },
                values: new object[] { 50, 1 });

            migrationBuilder.UpdateData(
                table: "PlanEntitlements",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                columns: new[] { "MaxInboxMessagesPerMonth", "MaxMailboxConnections" },
                values: new object[] { 500, 3 });

            migrationBuilder.UpdateData(
                table: "PlanEntitlements",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                columns: new[] { "MaxInboxMessagesPerMonth", "MaxMailboxConnections" },
                values: new object[] { 2000, 10 });

            migrationBuilder.UpdateData(
                table: "PlanEntitlements",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                columns: new[] { "MaxInboxMessagesPerMonth", "MaxMailboxConnections" },
                values: new object[] { 0, 50 });

            migrationBuilder.UpdateData(
                table: "PlanEntitlements",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                columns: new[] { "MaxInboxMessagesPerMonth", "MaxMailboxConnections" },
                values: new object[] { 0, 0 });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 686, DateTimeKind.Utc).AddTicks(5768));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 686, DateTimeKind.Utc).AddTicks(9564));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 686, DateTimeKind.Utc).AddTicks(9604));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 686, DateTimeKind.Utc).AddTicks(9636));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 686, DateTimeKind.Utc).AddTicks(9667));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 686, DateTimeKind.Utc).AddTicks(9696));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 686, DateTimeKind.Utc).AddTicks(9725));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 686, DateTimeKind.Utc).AddTicks(9752));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 686, DateTimeKind.Utc).AddTicks(9777));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000a"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 686, DateTimeKind.Utc).AddTicks(9804));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000b"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 686, DateTimeKind.Utc).AddTicks(9834));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000c"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 686, DateTimeKind.Utc).AddTicks(9861));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000d"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 686, DateTimeKind.Utc).AddTicks(9893));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000e"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 686, DateTimeKind.Utc).AddTicks(9922));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000f"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 686, DateTimeKind.Utc).AddTicks(9950));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 686, DateTimeKind.Utc).AddTicks(9979));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 687, DateTimeKind.Utc).AddTicks(6));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 687, DateTimeKind.Utc).AddTicks(34));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 687, DateTimeKind.Utc).AddTicks(62));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 11, 30, 41, 687, DateTimeKind.Utc).AddTicks(90));

            migrationBuilder.UpdateData(
                table: "PlanTemplates",
                keyColumn: "Id",
                keyValue: new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                column: "SortOrder",
                value: 1);

            migrationBuilder.UpdateData(
                table: "PlanTemplates",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                column: "SortOrder",
                value: 2);

            migrationBuilder.UpdateData(
                table: "PlanTemplates",
                keyColumn: "Id",
                keyValue: new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                column: "SortOrder",
                value: 3);

            migrationBuilder.UpdateData(
                table: "PlanTemplates",
                keyColumn: "Id",
                keyValue: new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                columns: new[] { "IsPubliclyVisible", "SortOrder" },
                values: new object[] { false, 4 });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CompanyId",
                table: "Invoices",
                column: "CompanyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "TenantWebhookLimits");

            migrationBuilder.DropColumn(
                name: "MaxInboxMessagesPerMonth",
                table: "PlanEntitlements");

            migrationBuilder.DropColumn(
                name: "MaxMailboxConnections",
                table: "PlanEntitlements");

            migrationBuilder.DropColumn(
                name: "ExternalCustomerId",
                table: "CompanySubscriptions");

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 944, DateTimeKind.Utc).AddTicks(417));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 944, DateTimeKind.Utc).AddTicks(531));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 944, DateTimeKind.Utc).AddTicks(554));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 944, DateTimeKind.Utc).AddTicks(575));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 944, DateTimeKind.Utc).AddTicks(596));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 944, DateTimeKind.Utc).AddTicks(617));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 944, DateTimeKind.Utc).AddTicks(638));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 944, DateTimeKind.Utc).AddTicks(658));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 944, DateTimeKind.Utc).AddTicks(679));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 943, DateTimeKind.Utc).AddTicks(3109));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 954, DateTimeKind.Utc).AddTicks(9041));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 955, DateTimeKind.Utc).AddTicks(3352));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 955, DateTimeKind.Utc).AddTicks(3405));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 955, DateTimeKind.Utc).AddTicks(3434));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 955, DateTimeKind.Utc).AddTicks(3464));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 955, DateTimeKind.Utc).AddTicks(3490));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 955, DateTimeKind.Utc).AddTicks(3517));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 955, DateTimeKind.Utc).AddTicks(3546));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 955, DateTimeKind.Utc).AddTicks(3576));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000a"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 955, DateTimeKind.Utc).AddTicks(3643));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000b"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 955, DateTimeKind.Utc).AddTicks(3669));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000c"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 955, DateTimeKind.Utc).AddTicks(3695));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000d"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 955, DateTimeKind.Utc).AddTicks(3722));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000e"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 955, DateTimeKind.Utc).AddTicks(3749));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000f"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 955, DateTimeKind.Utc).AddTicks(3775));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 955, DateTimeKind.Utc).AddTicks(3805));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 955, DateTimeKind.Utc).AddTicks(3838));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 955, DateTimeKind.Utc).AddTicks(3867));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 955, DateTimeKind.Utc).AddTicks(3898));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 11, 37, 955, DateTimeKind.Utc).AddTicks(3936));

            migrationBuilder.UpdateData(
                table: "PlanTemplates",
                keyColumn: "Id",
                keyValue: new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                column: "SortOrder",
                value: 0);

            migrationBuilder.UpdateData(
                table: "PlanTemplates",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                column: "SortOrder",
                value: 0);

            migrationBuilder.UpdateData(
                table: "PlanTemplates",
                keyColumn: "Id",
                keyValue: new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                column: "SortOrder",
                value: 0);

            migrationBuilder.UpdateData(
                table: "PlanTemplates",
                keyColumn: "Id",
                keyValue: new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                columns: new[] { "IsPubliclyVisible", "SortOrder" },
                values: new object[] { true, 0 });
        }
    }
}
