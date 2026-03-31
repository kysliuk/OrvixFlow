using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OrvixFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRagExtensionPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("2125b0fa-2206-465d-bd7b-450d64f19200"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("273e38cf-ced7-44cf-93cb-b1d0925c13c1"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("2751ceab-eaa3-4e49-bb89-081f9d83c90b"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("27f3060a-0406-4bc7-8819-e3e9ec422e2d"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("3f1d55b2-6c8f-455e-8671-662b1d72e315"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("4e963452-cf09-4417-9582-a77d348fcc2e"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("6f76c46b-0938-48c7-93e3-6b785555d3a7"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("71b6d4ab-dc3b-4ed2-9f93-f45846dc3a58"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("72ad86b8-4dd3-42b8-ad88-9543240bbea5"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("7e485387-c346-4880-b73e-3ced17230424"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("83f60807-56e1-4fd7-9986-352dc1b610e8"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("8a8d7f5a-04e5-4b47-a6b0-f52307f0c045"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("8ca42d84-46f5-4be6-a8f9-413b73ee340c"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("9b10a27d-c6ba-47e0-bff3-6c8e146298a1"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("9e6e69e5-c5ba-40df-9640-6d7a2cfc46d3"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a4342db1-ac94-43fc-b058-c0eb5c655360"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a86d400c-5310-43d3-aa8d-333ad7a9b67d"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("b134423e-afe3-4718-b65d-0622061066d6"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("bac94c22-69e8-46f9-bcf7-2503e8ab3f94"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("c4207a74-9f6f-4af6-8e8c-9232814c06fe"));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 31, 13, 43, 9, 194, DateTimeKind.Utc).AddTicks(2403));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 31, 13, 43, 9, 196, DateTimeKind.Utc).AddTicks(470));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 31, 13, 43, 9, 196, DateTimeKind.Utc).AddTicks(580));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 31, 13, 43, 9, 196, DateTimeKind.Utc).AddTicks(653));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 31, 13, 43, 9, 196, DateTimeKind.Utc).AddTicks(689));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 31, 13, 43, 9, 196, DateTimeKind.Utc).AddTicks(726));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 31, 13, 43, 9, 196, DateTimeKind.Utc).AddTicks(750));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 31, 13, 43, 9, 196, DateTimeKind.Utc).AddTicks(786));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 31, 13, 43, 9, 196, DateTimeKind.Utc).AddTicks(815));

            migrationBuilder.InsertData(
                table: "PlanModuleInclusions",
                columns: new[] { "Id", "CreatedAt", "ModuleDefinitionId", "PlanTemplateId" },
                values: new object[,]
                {
                    { new Guid("0e243aa2-3c4f-4805-a82b-0ff44df7fca1"), new DateTime(2026, 3, 31, 13, 43, 9, 213, DateTimeKind.Utc).AddTicks(2710), new Guid("44444444-4444-4444-4444-444444444444"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("1ed1b64a-8104-496d-9ca0-5b44de61b3ef"), new DateTime(2026, 3, 31, 13, 43, 9, 212, DateTimeKind.Utc).AddTicks(8066), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa") },
                    { new Guid("243507a4-fe6a-431a-82d3-a93eb1747b44"), new DateTime(2026, 3, 31, 13, 43, 9, 213, DateTimeKind.Utc).AddTicks(2637), new Guid("77777777-7777-7777-7777-777777777777"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("3699a661-3bfa-4889-84ce-55a4275a0931"), new DateTime(2026, 3, 31, 13, 43, 9, 213, DateTimeKind.Utc).AddTicks(2613), new Guid("55555555-5555-5555-5555-555555555555"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("4d19b213-135c-4e41-89b1-aaeddcf46f6b"), new DateTime(2026, 3, 31, 13, 43, 9, 213, DateTimeKind.Utc).AddTicks(2545), new Guid("11111111-1111-1111-1111-111111111111"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("68820318-ce72-4859-b384-8fe63ad7d79f"), new DateTime(2026, 3, 31, 13, 43, 9, 213, DateTimeKind.Utc).AddTicks(2519), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("6a1cd56c-e372-4f2d-b4cf-02e1bd6dfd58"), new DateTime(2026, 3, 31, 13, 43, 9, 213, DateTimeKind.Utc).AddTicks(2468), new Guid("44444444-4444-4444-4444-444444444444"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") },
                    { new Guid("83b2689a-03dc-4107-83e5-a5a478e2047d"), new DateTime(2026, 3, 31, 13, 43, 9, 213, DateTimeKind.Utc).AddTicks(2591), new Guid("22222222-2222-2222-2222-222222222222"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("8a111ae9-b773-426e-a4fe-c66977fedb7d"), new DateTime(2026, 3, 31, 13, 43, 9, 213, DateTimeKind.Utc).AddTicks(2443), new Guid("11111111-1111-1111-1111-111111111111"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") },
                    { new Guid("933df29a-e0f4-4ca0-b474-c31317e022d6"), new DateTime(2026, 3, 31, 13, 43, 9, 213, DateTimeKind.Utc).AddTicks(2687), new Guid("11111111-1111-1111-1111-111111111111"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("9767c79c-54e8-40ce-8064-02a2a4d0880a"), new DateTime(2026, 3, 31, 13, 43, 9, 213, DateTimeKind.Utc).AddTicks(2662), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("9dd3dbfd-030a-4954-b832-332218517aa2"), new DateTime(2026, 3, 31, 13, 43, 9, 213, DateTimeKind.Utc).AddTicks(2360), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") },
                    { new Guid("bbe3c268-11da-450c-8dda-b7e69203e116"), new DateTime(2026, 3, 31, 13, 43, 9, 213, DateTimeKind.Utc).AddTicks(2421), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") },
                    { new Guid("c02f71bf-cc21-420d-a25a-6572c4465317"), new DateTime(2026, 3, 31, 13, 43, 9, 213, DateTimeKind.Utc).AddTicks(2568), new Guid("44444444-4444-4444-4444-444444444444"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("d7fe4282-629b-480c-9dc6-27b993b3c332"), new DateTime(2026, 3, 31, 13, 43, 9, 213, DateTimeKind.Utc).AddTicks(2737), new Guid("22222222-2222-2222-2222-222222222222"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("df4f0284-832b-4679-9b2b-4424ae04d0b9"), new DateTime(2026, 3, 31, 13, 43, 9, 213, DateTimeKind.Utc).AddTicks(2820), new Guid("66666666-6666-6666-6666-666666666666"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("e85fdae2-3525-442f-8651-f2aeb47c2dcf"), new DateTime(2026, 3, 31, 13, 43, 9, 213, DateTimeKind.Utc).AddTicks(2396), new Guid("11111111-1111-1111-1111-111111111111"), new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") },
                    { new Guid("edba4bdc-b37a-47b6-91e0-c71f2d4d0697"), new DateTime(2026, 3, 31, 13, 43, 9, 213, DateTimeKind.Utc).AddTicks(2761), new Guid("55555555-5555-5555-5555-555555555555"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("f6b39d2c-c679-45b3-82c0-52cad19680c2"), new DateTime(2026, 3, 31, 13, 43, 9, 213, DateTimeKind.Utc).AddTicks(2492), new Guid("22222222-2222-2222-2222-222222222222"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") },
                    { new Guid("fdb021be-f4fa-44a2-9c58-085859d5a344"), new DateTime(2026, 3, 31, 13, 43, 9, 213, DateTimeKind.Utc).AddTicks(2789), new Guid("77777777-7777-7777-7777-777777777777"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("0e243aa2-3c4f-4805-a82b-0ff44df7fca1"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("1ed1b64a-8104-496d-9ca0-5b44de61b3ef"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("243507a4-fe6a-431a-82d3-a93eb1747b44"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("3699a661-3bfa-4889-84ce-55a4275a0931"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("4d19b213-135c-4e41-89b1-aaeddcf46f6b"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("68820318-ce72-4859-b384-8fe63ad7d79f"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("6a1cd56c-e372-4f2d-b4cf-02e1bd6dfd58"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("83b2689a-03dc-4107-83e5-a5a478e2047d"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("8a111ae9-b773-426e-a4fe-c66977fedb7d"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("933df29a-e0f4-4ca0-b474-c31317e022d6"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("9767c79c-54e8-40ce-8064-02a2a4d0880a"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("9dd3dbfd-030a-4954-b832-332218517aa2"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("bbe3c268-11da-450c-8dda-b7e69203e116"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("c02f71bf-cc21-420d-a25a-6572c4465317"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("d7fe4282-629b-480c-9dc6-27b993b3c332"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("df4f0284-832b-4679-9b2b-4424ae04d0b9"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("e85fdae2-3525-442f-8651-f2aeb47c2dcf"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("edba4bdc-b37a-47b6-91e0-c71f2d4d0697"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("f6b39d2c-c679-45b3-82c0-52cad19680c2"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("fdb021be-f4fa-44a2-9c58-085859d5a344"));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 31, 13, 41, 6, 586, DateTimeKind.Utc).AddTicks(7722));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 31, 13, 41, 6, 587, DateTimeKind.Utc).AddTicks(5286));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 31, 13, 41, 6, 587, DateTimeKind.Utc).AddTicks(5319));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 31, 13, 41, 6, 587, DateTimeKind.Utc).AddTicks(5342));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 31, 13, 41, 6, 587, DateTimeKind.Utc).AddTicks(5363));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 31, 13, 41, 6, 587, DateTimeKind.Utc).AddTicks(5384));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 31, 13, 41, 6, 587, DateTimeKind.Utc).AddTicks(5405));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 31, 13, 41, 6, 587, DateTimeKind.Utc).AddTicks(5426));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 31, 13, 41, 6, 587, DateTimeKind.Utc).AddTicks(5449));

            migrationBuilder.InsertData(
                table: "PlanModuleInclusions",
                columns: new[] { "Id", "CreatedAt", "ModuleDefinitionId", "PlanTemplateId" },
                values: new object[,]
                {
                    { new Guid("2125b0fa-2206-465d-bd7b-450d64f19200"), new DateTime(2026, 3, 31, 13, 41, 6, 597, DateTimeKind.Utc).AddTicks(5480), new Guid("22222222-2222-2222-2222-222222222222"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") },
                    { new Guid("273e38cf-ced7-44cf-93cb-b1d0925c13c1"), new DateTime(2026, 3, 31, 13, 41, 6, 597, DateTimeKind.Utc).AddTicks(5731), new Guid("55555555-5555-5555-5555-555555555555"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("2751ceab-eaa3-4e49-bb89-081f9d83c90b"), new DateTime(2026, 3, 31, 13, 41, 6, 597, DateTimeKind.Utc).AddTicks(5443), new Guid("11111111-1111-1111-1111-111111111111"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") },
                    { new Guid("27f3060a-0406-4bc7-8819-e3e9ec422e2d"), new DateTime(2026, 3, 31, 13, 41, 6, 597, DateTimeKind.Utc).AddTicks(5697), new Guid("44444444-4444-4444-4444-444444444444"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("3f1d55b2-6c8f-455e-8671-662b1d72e315"), new DateTime(2026, 3, 31, 13, 41, 6, 597, DateTimeKind.Utc).AddTicks(5408), new Guid("11111111-1111-1111-1111-111111111111"), new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") },
                    { new Guid("4e963452-cf09-4417-9582-a77d348fcc2e"), new DateTime(2026, 3, 31, 13, 41, 6, 597, DateTimeKind.Utc).AddTicks(3749), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa") },
                    { new Guid("6f76c46b-0938-48c7-93e3-6b785555d3a7"), new DateTime(2026, 3, 31, 13, 41, 6, 597, DateTimeKind.Utc).AddTicks(5515), new Guid("11111111-1111-1111-1111-111111111111"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("71b6d4ab-dc3b-4ed2-9f93-f45846dc3a58"), new DateTime(2026, 3, 31, 13, 41, 6, 597, DateTimeKind.Utc).AddTicks(5425), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") },
                    { new Guid("72ad86b8-4dd3-42b8-ad88-9543240bbea5"), new DateTime(2026, 3, 31, 13, 41, 6, 597, DateTimeKind.Utc).AddTicks(5662), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("7e485387-c346-4880-b73e-3ced17230424"), new DateTime(2026, 3, 31, 13, 41, 6, 597, DateTimeKind.Utc).AddTicks(5644), new Guid("77777777-7777-7777-7777-777777777777"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("83f60807-56e1-4fd7-9986-352dc1b610e8"), new DateTime(2026, 3, 31, 13, 41, 6, 597, DateTimeKind.Utc).AddTicks(5609), new Guid("22222222-2222-2222-2222-222222222222"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("8a8d7f5a-04e5-4b47-a6b0-f52307f0c045"), new DateTime(2026, 3, 31, 13, 41, 6, 597, DateTimeKind.Utc).AddTicks(5749), new Guid("77777777-7777-7777-7777-777777777777"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("8ca42d84-46f5-4be6-a8f9-413b73ee340c"), new DateTime(2026, 3, 31, 13, 41, 6, 597, DateTimeKind.Utc).AddTicks(5626), new Guid("55555555-5555-5555-5555-555555555555"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("9b10a27d-c6ba-47e0-bff3-6c8e146298a1"), new DateTime(2026, 3, 31, 13, 41, 6, 597, DateTimeKind.Utc).AddTicks(5533), new Guid("44444444-4444-4444-4444-444444444444"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("9e6e69e5-c5ba-40df-9640-6d7a2cfc46d3"), new DateTime(2026, 3, 31, 13, 41, 6, 597, DateTimeKind.Utc).AddTicks(5766), new Guid("66666666-6666-6666-6666-666666666666"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("a4342db1-ac94-43fc-b058-c0eb5c655360"), new DateTime(2026, 3, 31, 13, 41, 6, 597, DateTimeKind.Utc).AddTicks(5462), new Guid("44444444-4444-4444-4444-444444444444"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") },
                    { new Guid("a86d400c-5310-43d3-aa8d-333ad7a9b67d"), new DateTime(2026, 3, 31, 13, 41, 6, 597, DateTimeKind.Utc).AddTicks(5387), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") },
                    { new Guid("b134423e-afe3-4718-b65d-0622061066d6"), new DateTime(2026, 3, 31, 13, 41, 6, 597, DateTimeKind.Utc).AddTicks(5679), new Guid("11111111-1111-1111-1111-111111111111"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("bac94c22-69e8-46f9-bcf7-2503e8ab3f94"), new DateTime(2026, 3, 31, 13, 41, 6, 597, DateTimeKind.Utc).AddTicks(5714), new Guid("22222222-2222-2222-2222-222222222222"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("c4207a74-9f6f-4af6-8e8c-9232814c06fe"), new DateTime(2026, 3, 31, 13, 41, 6, 597, DateTimeKind.Utc).AddTicks(5497), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") }
                });
        }
    }
}
