using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrvixFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HashRefreshTokensAtRest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LookupKey",
                table: "RefreshTokens",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 771, DateTimeKind.Utc).AddTicks(5357));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 771, DateTimeKind.Utc).AddTicks(5361));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 771, DateTimeKind.Utc).AddTicks(5364));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 771, DateTimeKind.Utc).AddTicks(5366));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 771, DateTimeKind.Utc).AddTicks(5367));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 771, DateTimeKind.Utc).AddTicks(5369));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 771, DateTimeKind.Utc).AddTicks(5372));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 771, DateTimeKind.Utc).AddTicks(5373));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 771, DateTimeKind.Utc).AddTicks(5378));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 771, DateTimeKind.Utc).AddTicks(3938));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 773, DateTimeKind.Utc).AddTicks(3428));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 773, DateTimeKind.Utc).AddTicks(3840));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 773, DateTimeKind.Utc).AddTicks(3843));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 773, DateTimeKind.Utc).AddTicks(3845));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 773, DateTimeKind.Utc).AddTicks(3847));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 773, DateTimeKind.Utc).AddTicks(3855));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 773, DateTimeKind.Utc).AddTicks(3858));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 773, DateTimeKind.Utc).AddTicks(3860));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 773, DateTimeKind.Utc).AddTicks(3862));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000a"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 773, DateTimeKind.Utc).AddTicks(3864));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000b"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 773, DateTimeKind.Utc).AddTicks(3866));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000c"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 773, DateTimeKind.Utc).AddTicks(3868));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000d"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 773, DateTimeKind.Utc).AddTicks(3870));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000e"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 773, DateTimeKind.Utc).AddTicks(3874));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000f"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 773, DateTimeKind.Utc).AddTicks(3876));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 773, DateTimeKind.Utc).AddTicks(3878));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 773, DateTimeKind.Utc).AddTicks(3880));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 773, DateTimeKind.Utc).AddTicks(3881));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 773, DateTimeKind.Utc).AddTicks(3883));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 773, DateTimeKind.Utc).AddTicks(3884));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 773, DateTimeKind.Utc).AddTicks(3885));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 773, DateTimeKind.Utc).AddTicks(3889));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000017"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 773, DateTimeKind.Utc).AddTicks(3892));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000018"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 773, DateTimeKind.Utc).AddTicks(3894));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000019"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 56, 50, 773, DateTimeKind.Utc).AddTicks(3905));

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_LookupKey",
                table: "RefreshTokens",
                column: "LookupKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_LookupKey",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "LookupKey",
                table: "RefreshTokens");

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 258, DateTimeKind.Utc).AddTicks(7908));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 258, DateTimeKind.Utc).AddTicks(7912));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 258, DateTimeKind.Utc).AddTicks(7915));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 258, DateTimeKind.Utc).AddTicks(7917));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 258, DateTimeKind.Utc).AddTicks(7926));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 258, DateTimeKind.Utc).AddTicks(7928));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 258, DateTimeKind.Utc).AddTicks(7931));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 258, DateTimeKind.Utc).AddTicks(7933));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 258, DateTimeKind.Utc).AddTicks(7935));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 258, DateTimeKind.Utc).AddTicks(6488));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 260, DateTimeKind.Utc).AddTicks(5694));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 260, DateTimeKind.Utc).AddTicks(6029));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 260, DateTimeKind.Utc).AddTicks(6032));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 260, DateTimeKind.Utc).AddTicks(6033));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 260, DateTimeKind.Utc).AddTicks(6035));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 260, DateTimeKind.Utc).AddTicks(6037));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 260, DateTimeKind.Utc).AddTicks(6039));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 260, DateTimeKind.Utc).AddTicks(6041));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 260, DateTimeKind.Utc).AddTicks(6043));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000a"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 260, DateTimeKind.Utc).AddTicks(6047));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000b"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 260, DateTimeKind.Utc).AddTicks(6049));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000c"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 260, DateTimeKind.Utc).AddTicks(6051));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000d"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 260, DateTimeKind.Utc).AddTicks(6053));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000e"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 260, DateTimeKind.Utc).AddTicks(6068));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000f"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 260, DateTimeKind.Utc).AddTicks(6070));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 260, DateTimeKind.Utc).AddTicks(6071));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 260, DateTimeKind.Utc).AddTicks(6073));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 260, DateTimeKind.Utc).AddTicks(6076));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 260, DateTimeKind.Utc).AddTicks(6077));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 260, DateTimeKind.Utc).AddTicks(6079));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 260, DateTimeKind.Utc).AddTicks(6080));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 260, DateTimeKind.Utc).AddTicks(6082));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000017"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 260, DateTimeKind.Utc).AddTicks(6083));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000018"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 260, DateTimeKind.Utc).AddTicks(6085));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000019"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 16, 38, 56, 260, DateTimeKind.Utc).AddTicks(6086));
        }
    }
}
