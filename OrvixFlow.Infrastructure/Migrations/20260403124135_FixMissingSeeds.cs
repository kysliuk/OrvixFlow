using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OrvixFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixMissingSeeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var plans = OrvixFlow.Core.Entities.PlanCatalog.BuildPlanSeed();
            foreach (var p in plans)
            {
                var maxSeats = p.MaxSeats.HasValue ? p.MaxSeats.Value.ToString() : "NULL";
                migrationBuilder.Sql($@"
                    INSERT INTO ""PlanTemplates"" 
                    (""Id"", ""Name"", ""Slug"", ""Description"", ""MonthlyPriceCents"", ""YearlyPriceCents"", ""Currency"", ""BillingInterval"", ""MaxSeats"", ""IsActive"", ""IsFree"", ""IsTrialAllowed"", ""TrialDays"", ""LegacyLocked"", ""CreatedAt"", ""IsPubliclyVisible"", ""SortOrder"")
                    VALUES 
                    ('{p.Id}', '{p.Name.Replace("'", "''")}', '{p.Slug}', '{p.Description.Replace("'", "''")}', {p.MonthlyPriceCents}, {p.YearlyPriceCents}, '{p.Currency}', '{p.BillingInterval}', {maxSeats}, {(p.IsActive?"true":"false")}, {(p.IsFree?"true":"false")}, {(p.IsTrialAllowed?"true":"false")}, {p.TrialDays}, {(p.LegacyLocked?"true":"false")}, '{p.CreatedAt:O}', {(p.IsPubliclyVisible?"true":"false")}, {p.SortOrder})
                    ON CONFLICT (""Id"") DO NOTHING;
                ");
            }

            var ents = OrvixFlow.Core.Entities.PlanCatalog.BuildEntitlementsSeed();
            foreach (var e in ents)
            {
                migrationBuilder.Sql($@"
                    INSERT INTO ""PlanEntitlements""
                    (""Id"", ""PlanTemplateId"", ""MaxMonthlyTokens"", ""MaxApiRequestsPerDay"", ""MaxStorageMb"", ""MaxKnowledgeBases"", ""CreatedAt"")
                    VALUES
                    ('{e.Id}', '{e.PlanTemplateId}', {e.MaxMonthlyTokens}, {e.MaxApiRequestsPerDay}, {e.MaxStorageMb}, {e.MaxKnowledgeBases}, '{e.CreatedAt:O}')
                    ON CONFLICT (""Id"") DO NOTHING;
                ");
            }

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

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 3, 12, 41, 30, 867, DateTimeKind.Utc).AddTicks(3104));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 3, 12, 41, 30, 868, DateTimeKind.Utc).AddTicks(4842));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 3, 12, 41, 30, 868, DateTimeKind.Utc).AddTicks(4895));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 3, 12, 41, 30, 868, DateTimeKind.Utc).AddTicks(4925));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 3, 12, 41, 30, 868, DateTimeKind.Utc).AddTicks(4957));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 3, 12, 41, 30, 868, DateTimeKind.Utc).AddTicks(4987));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 3, 12, 41, 30, 868, DateTimeKind.Utc).AddTicks(5016));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 3, 12, 41, 30, 868, DateTimeKind.Utc).AddTicks(5046));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 3, 12, 41, 30, 868, DateTimeKind.Utc).AddTicks(5075));

            migrationBuilder.InsertData(
                table: "PlanModuleInclusions",
                columns: new[] { "Id", "CreatedAt", "LimitDescription", "MaxItemsTotal", "MaxUsagePerMonth", "ModuleDefinitionId", "PlanTemplateId" },
                values: new object[,]
                {
                    { new Guid("a0000000-0000-0000-0000-000000000001"), new DateTime(2026, 4, 3, 12, 41, 30, 881, DateTimeKind.Utc).AddTicks(6264), null, null, null, new Guid("33333333-3333-3333-3333-333333333333"), new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa") },
                    { new Guid("a0000000-0000-0000-0000-000000000002"), new DateTime(2026, 4, 3, 12, 41, 30, 881, DateTimeKind.Utc).AddTicks(9610), null, null, null, new Guid("33333333-3333-3333-3333-333333333333"), new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") },
                    { new Guid("a0000000-0000-0000-0000-000000000003"), new DateTime(2026, 4, 3, 12, 41, 30, 881, DateTimeKind.Utc).AddTicks(9654), null, null, null, new Guid("11111111-1111-1111-1111-111111111111"), new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") },
                    { new Guid("a0000000-0000-0000-0000-000000000004"), new DateTime(2026, 4, 3, 12, 41, 30, 881, DateTimeKind.Utc).AddTicks(9684), null, null, null, new Guid("33333333-3333-3333-3333-333333333333"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") },
                    { new Guid("a0000000-0000-0000-0000-000000000005"), new DateTime(2026, 4, 3, 12, 41, 30, 881, DateTimeKind.Utc).AddTicks(9710), null, null, null, new Guid("11111111-1111-1111-1111-111111111111"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") },
                    { new Guid("a0000000-0000-0000-0000-000000000006"), new DateTime(2026, 4, 3, 12, 41, 30, 881, DateTimeKind.Utc).AddTicks(9740), null, null, null, new Guid("44444444-4444-4444-4444-444444444444"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") },
                    { new Guid("a0000000-0000-0000-0000-000000000007"), new DateTime(2026, 4, 3, 12, 41, 30, 881, DateTimeKind.Utc).AddTicks(9769), null, null, null, new Guid("22222222-2222-2222-2222-222222222222"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") },
                    { new Guid("a0000000-0000-0000-0000-000000000008"), new DateTime(2026, 4, 3, 12, 41, 30, 881, DateTimeKind.Utc).AddTicks(9875), null, null, null, new Guid("33333333-3333-3333-3333-333333333333"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("a0000000-0000-0000-0000-000000000009"), new DateTime(2026, 4, 3, 12, 41, 30, 881, DateTimeKind.Utc).AddTicks(9904), null, null, null, new Guid("11111111-1111-1111-1111-111111111111"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("a0000000-0000-0000-0000-00000000000a"), new DateTime(2026, 4, 3, 12, 41, 30, 882, DateTimeKind.Utc).AddTicks(122), null, null, null, new Guid("44444444-4444-4444-4444-444444444444"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("a0000000-0000-0000-0000-00000000000b"), new DateTime(2026, 4, 3, 12, 41, 30, 882, DateTimeKind.Utc).AddTicks(209), null, null, null, new Guid("22222222-2222-2222-2222-222222222222"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("a0000000-0000-0000-0000-00000000000c"), new DateTime(2026, 4, 3, 12, 41, 30, 882, DateTimeKind.Utc).AddTicks(238), null, null, null, new Guid("55555555-5555-5555-5555-555555555555"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("a0000000-0000-0000-0000-00000000000d"), new DateTime(2026, 4, 3, 12, 41, 30, 882, DateTimeKind.Utc).AddTicks(268), null, null, null, new Guid("77777777-7777-7777-7777-777777777777"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("a0000000-0000-0000-0000-00000000000e"), new DateTime(2026, 4, 3, 12, 41, 30, 882, DateTimeKind.Utc).AddTicks(297), null, null, null, new Guid("33333333-3333-3333-3333-333333333333"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("a0000000-0000-0000-0000-00000000000f"), new DateTime(2026, 4, 3, 12, 41, 30, 882, DateTimeKind.Utc).AddTicks(326), null, null, null, new Guid("11111111-1111-1111-1111-111111111111"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("a0000000-0000-0000-0000-000000000010"), new DateTime(2026, 4, 3, 12, 41, 30, 882, DateTimeKind.Utc).AddTicks(354), null, null, null, new Guid("44444444-4444-4444-4444-444444444444"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("a0000000-0000-0000-0000-000000000011"), new DateTime(2026, 4, 3, 12, 41, 30, 882, DateTimeKind.Utc).AddTicks(383), null, null, null, new Guid("22222222-2222-2222-2222-222222222222"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("a0000000-0000-0000-0000-000000000012"), new DateTime(2026, 4, 3, 12, 41, 30, 882, DateTimeKind.Utc).AddTicks(411), null, null, null, new Guid("55555555-5555-5555-5555-555555555555"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("a0000000-0000-0000-0000-000000000013"), new DateTime(2026, 4, 3, 12, 41, 30, 882, DateTimeKind.Utc).AddTicks(436), null, null, null, new Guid("77777777-7777-7777-7777-777777777777"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("a0000000-0000-0000-0000-000000000014"), new DateTime(2026, 4, 3, 12, 41, 30, 882, DateTimeKind.Utc).AddTicks(462), null, null, null, new Guid("66666666-6666-6666-6666-666666666666"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000a"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000b"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000c"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000d"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000e"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000f"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000012"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000013"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000014"));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 2, 11, 16, 6, 409, DateTimeKind.Utc).AddTicks(8569));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 2, 11, 16, 6, 411, DateTimeKind.Utc).AddTicks(2418));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 2, 11, 16, 6, 411, DateTimeKind.Utc).AddTicks(2491));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 2, 11, 16, 6, 411, DateTimeKind.Utc).AddTicks(2522));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 2, 11, 16, 6, 411, DateTimeKind.Utc).AddTicks(2553));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 2, 11, 16, 6, 411, DateTimeKind.Utc).AddTicks(2585));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 2, 11, 16, 6, 411, DateTimeKind.Utc).AddTicks(2615));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 2, 11, 16, 6, 411, DateTimeKind.Utc).AddTicks(2643));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 2, 11, 16, 6, 411, DateTimeKind.Utc).AddTicks(2671));

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
        }
    }
}
