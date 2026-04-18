using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OrvixFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedKnowledgeBaseInPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 14, 34, 35, 702, DateTimeKind.Utc).AddTicks(155));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 14, 34, 35, 702, DateTimeKind.Utc).AddTicks(158));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 14, 34, 35, 702, DateTimeKind.Utc).AddTicks(161));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 14, 34, 35, 702, DateTimeKind.Utc).AddTicks(163));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 14, 34, 35, 702, DateTimeKind.Utc).AddTicks(174));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 14, 34, 35, 702, DateTimeKind.Utc).AddTicks(176));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 14, 34, 35, 702, DateTimeKind.Utc).AddTicks(178));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 14, 34, 35, 702, DateTimeKind.Utc).AddTicks(180));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 14, 34, 35, 702, DateTimeKind.Utc).AddTicks(182));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 14, 34, 35, 701, DateTimeKind.Utc).AddTicks(8793));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000001"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId" },
                values: new object[] { new DateTime(2026, 4, 18, 14, 34, 35, 704, DateTimeKind.Utc).AddTicks(271), new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000002"),
                columns: new[] { "CreatedAt", "PlanTemplateId" },
                values: new object[] { new DateTime(2026, 4, 18, 14, 34, 35, 704, DateTimeKind.Utc).AddTicks(866), new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000003"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId" },
                values: new object[] { new DateTime(2026, 4, 18, 14, 34, 35, 704, DateTimeKind.Utc).AddTicks(876), new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000004"),
                columns: new[] { "CreatedAt", "PlanTemplateId" },
                values: new object[] { new DateTime(2026, 4, 18, 14, 34, 35, 704, DateTimeKind.Utc).AddTicks(878), new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000005"),
                columns: new[] { "CreatedAt", "PlanTemplateId" },
                values: new object[] { new DateTime(2026, 4, 18, 14, 34, 35, 704, DateTimeKind.Utc).AddTicks(880), new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000006"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId" },
                values: new object[] { new DateTime(2026, 4, 18, 14, 34, 35, 704, DateTimeKind.Utc).AddTicks(882), new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000007"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId" },
                values: new object[] { new DateTime(2026, 4, 18, 14, 34, 35, 704, DateTimeKind.Utc).AddTicks(883), new Guid("33333333-3333-3333-3333-333333333333") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000008"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId", "PlanTemplateId" },
                values: new object[] { new DateTime(2026, 4, 18, 14, 34, 35, 704, DateTimeKind.Utc).AddTicks(885), new Guid("11111111-1111-1111-1111-111111111111"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000009"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId", "PlanTemplateId" },
                values: new object[] { new DateTime(2026, 4, 18, 14, 34, 35, 704, DateTimeKind.Utc).AddTicks(887), new Guid("44444444-4444-4444-4444-444444444444"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000a"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId", "PlanTemplateId" },
                values: new object[] { new DateTime(2026, 4, 18, 14, 34, 35, 704, DateTimeKind.Utc).AddTicks(889), new Guid("22222222-2222-2222-2222-222222222222"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000b"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId" },
                values: new object[] { new DateTime(2026, 4, 18, 14, 34, 35, 704, DateTimeKind.Utc).AddTicks(893), new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000c"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId" },
                values: new object[] { new DateTime(2026, 4, 18, 14, 34, 35, 704, DateTimeKind.Utc).AddTicks(895), new Guid("33333333-3333-3333-3333-333333333333") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000d"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId" },
                values: new object[] { new DateTime(2026, 4, 18, 14, 34, 35, 704, DateTimeKind.Utc).AddTicks(897), new Guid("11111111-1111-1111-1111-111111111111") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000e"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId", "PlanTemplateId" },
                values: new object[] { new DateTime(2026, 4, 18, 14, 34, 35, 704, DateTimeKind.Utc).AddTicks(899), new Guid("44444444-4444-4444-4444-444444444444"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000f"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId", "PlanTemplateId" },
                values: new object[] { new DateTime(2026, 4, 18, 14, 34, 35, 704, DateTimeKind.Utc).AddTicks(900), new Guid("22222222-2222-2222-2222-222222222222"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000010"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId", "PlanTemplateId" },
                values: new object[] { new DateTime(2026, 4, 18, 14, 34, 35, 704, DateTimeKind.Utc).AddTicks(902), new Guid("55555555-5555-5555-5555-555555555555"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000011"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId", "PlanTemplateId" },
                values: new object[] { new DateTime(2026, 4, 18, 14, 34, 35, 704, DateTimeKind.Utc).AddTicks(903), new Guid("77777777-7777-7777-7777-777777777777"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000012"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId" },
                values: new object[] { new DateTime(2026, 4, 18, 14, 34, 35, 704, DateTimeKind.Utc).AddTicks(905), new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000013"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId" },
                values: new object[] { new DateTime(2026, 4, 18, 14, 34, 35, 704, DateTimeKind.Utc).AddTicks(909), new Guid("33333333-3333-3333-3333-333333333333") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000014"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId" },
                values: new object[] { new DateTime(2026, 4, 18, 14, 34, 35, 704, DateTimeKind.Utc).AddTicks(910), new Guid("11111111-1111-1111-1111-111111111111") });

            migrationBuilder.InsertData(
                table: "PlanModuleInclusions",
                columns: new[] { "Id", "CreatedAt", "LimitDescription", "MaxItemsTotal", "MaxUsagePerMonth", "ModuleDefinitionId", "PlanTemplateId" },
                values: new object[,]
                {
                    { new Guid("a0000000-0000-0000-0000-000000000015"), new DateTime(2026, 4, 18, 14, 34, 35, 704, DateTimeKind.Utc).AddTicks(912), null, null, null, new Guid("44444444-4444-4444-4444-444444444444"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("a0000000-0000-0000-0000-000000000016"), new DateTime(2026, 4, 18, 14, 34, 35, 704, DateTimeKind.Utc).AddTicks(913), null, null, null, new Guid("22222222-2222-2222-2222-222222222222"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("a0000000-0000-0000-0000-000000000017"), new DateTime(2026, 4, 18, 14, 34, 35, 704, DateTimeKind.Utc).AddTicks(915), null, null, null, new Guid("55555555-5555-5555-5555-555555555555"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("a0000000-0000-0000-0000-000000000018"), new DateTime(2026, 4, 18, 14, 34, 35, 704, DateTimeKind.Utc).AddTicks(916), null, null, null, new Guid("77777777-7777-7777-7777-777777777777"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("a0000000-0000-0000-0000-000000000019"), new DateTime(2026, 4, 18, 14, 34, 35, 704, DateTimeKind.Utc).AddTicks(917), null, null, null, new Guid("66666666-6666-6666-6666-666666666666"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000015"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000016"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000017"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000018"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000019"));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 12, 10, 28, 257, DateTimeKind.Utc).AddTicks(9310));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 12, 10, 28, 257, DateTimeKind.Utc).AddTicks(9351));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 12, 10, 28, 257, DateTimeKind.Utc).AddTicks(9373));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 12, 10, 28, 257, DateTimeKind.Utc).AddTicks(9395));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 12, 10, 28, 257, DateTimeKind.Utc).AddTicks(9416));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 12, 10, 28, 257, DateTimeKind.Utc).AddTicks(9437));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 12, 10, 28, 257, DateTimeKind.Utc).AddTicks(9528));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 12, 10, 28, 257, DateTimeKind.Utc).AddTicks(9548));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 12, 10, 28, 257, DateTimeKind.Utc).AddTicks(9568));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 17, 12, 10, 28, 256, DateTimeKind.Utc).AddTicks(9299));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000001"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId" },
                values: new object[] { new DateTime(2026, 4, 17, 12, 10, 28, 269, DateTimeKind.Utc).AddTicks(7258), new Guid("33333333-3333-3333-3333-333333333333") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000002"),
                columns: new[] { "CreatedAt", "PlanTemplateId" },
                values: new object[] { new DateTime(2026, 4, 17, 12, 10, 28, 270, DateTimeKind.Utc).AddTicks(1710), new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000003"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId" },
                values: new object[] { new DateTime(2026, 4, 17, 12, 10, 28, 270, DateTimeKind.Utc).AddTicks(1758), new Guid("11111111-1111-1111-1111-111111111111") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000004"),
                columns: new[] { "CreatedAt", "PlanTemplateId" },
                values: new object[] { new DateTime(2026, 4, 17, 12, 10, 28, 270, DateTimeKind.Utc).AddTicks(1788), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000005"),
                columns: new[] { "CreatedAt", "PlanTemplateId" },
                values: new object[] { new DateTime(2026, 4, 17, 12, 10, 28, 270, DateTimeKind.Utc).AddTicks(1817), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000006"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId" },
                values: new object[] { new DateTime(2026, 4, 17, 12, 10, 28, 270, DateTimeKind.Utc).AddTicks(1848), new Guid("44444444-4444-4444-4444-444444444444") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000007"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId" },
                values: new object[] { new DateTime(2026, 4, 17, 12, 10, 28, 270, DateTimeKind.Utc).AddTicks(1876), new Guid("22222222-2222-2222-2222-222222222222") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000008"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId", "PlanTemplateId" },
                values: new object[] { new DateTime(2026, 4, 17, 12, 10, 28, 270, DateTimeKind.Utc).AddTicks(1906), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000009"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId", "PlanTemplateId" },
                values: new object[] { new DateTime(2026, 4, 17, 12, 10, 28, 270, DateTimeKind.Utc).AddTicks(1936), new Guid("11111111-1111-1111-1111-111111111111"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000a"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId", "PlanTemplateId" },
                values: new object[] { new DateTime(2026, 4, 17, 12, 10, 28, 270, DateTimeKind.Utc).AddTicks(1968), new Guid("44444444-4444-4444-4444-444444444444"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000b"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId" },
                values: new object[] { new DateTime(2026, 4, 17, 12, 10, 28, 270, DateTimeKind.Utc).AddTicks(1997), new Guid("22222222-2222-2222-2222-222222222222") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000c"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId" },
                values: new object[] { new DateTime(2026, 4, 17, 12, 10, 28, 270, DateTimeKind.Utc).AddTicks(2024), new Guid("55555555-5555-5555-5555-555555555555") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000d"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId" },
                values: new object[] { new DateTime(2026, 4, 17, 12, 10, 28, 270, DateTimeKind.Utc).AddTicks(2055), new Guid("77777777-7777-7777-7777-777777777777") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000e"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId", "PlanTemplateId" },
                values: new object[] { new DateTime(2026, 4, 17, 12, 10, 28, 270, DateTimeKind.Utc).AddTicks(2085), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000f"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId", "PlanTemplateId" },
                values: new object[] { new DateTime(2026, 4, 17, 12, 10, 28, 270, DateTimeKind.Utc).AddTicks(2113), new Guid("11111111-1111-1111-1111-111111111111"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000010"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId", "PlanTemplateId" },
                values: new object[] { new DateTime(2026, 4, 17, 12, 10, 28, 270, DateTimeKind.Utc).AddTicks(2141), new Guid("44444444-4444-4444-4444-444444444444"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000011"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId", "PlanTemplateId" },
                values: new object[] { new DateTime(2026, 4, 17, 12, 10, 28, 270, DateTimeKind.Utc).AddTicks(2171), new Guid("22222222-2222-2222-2222-222222222222"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000012"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId" },
                values: new object[] { new DateTime(2026, 4, 17, 12, 10, 28, 270, DateTimeKind.Utc).AddTicks(2199), new Guid("55555555-5555-5555-5555-555555555555") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000013"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId" },
                values: new object[] { new DateTime(2026, 4, 17, 12, 10, 28, 270, DateTimeKind.Utc).AddTicks(2227), new Guid("77777777-7777-7777-7777-777777777777") });

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000014"),
                columns: new[] { "CreatedAt", "ModuleDefinitionId" },
                values: new object[] { new DateTime(2026, 4, 17, 12, 10, 28, 270, DateTimeKind.Utc).AddTicks(2256), new Guid("66666666-6666-6666-6666-666666666666") });
        }
    }
}
