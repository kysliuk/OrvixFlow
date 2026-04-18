using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrvixFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthEmailQueueAndTokenHashing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "VerificationTokenExpiresAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Body",
                table: "NotificationQueues",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "NotificationQueues",
                type: "text",
                nullable: true);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VerificationTokenExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Body",
                table: "NotificationQueues");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "NotificationQueues");

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 721, DateTimeKind.Utc).AddTicks(6979));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 721, DateTimeKind.Utc).AddTicks(6984));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 721, DateTimeKind.Utc).AddTicks(6987));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 721, DateTimeKind.Utc).AddTicks(6997));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 721, DateTimeKind.Utc).AddTicks(6999));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 721, DateTimeKind.Utc).AddTicks(7001));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 721, DateTimeKind.Utc).AddTicks(7003));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 721, DateTimeKind.Utc).AddTicks(7005));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 721, DateTimeKind.Utc).AddTicks(7007));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 721, DateTimeKind.Utc).AddTicks(5576));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 723, DateTimeKind.Utc).AddTicks(5639));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 723, DateTimeKind.Utc).AddTicks(5997));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 723, DateTimeKind.Utc).AddTicks(6000));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 723, DateTimeKind.Utc).AddTicks(6001));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 723, DateTimeKind.Utc).AddTicks(6003));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 723, DateTimeKind.Utc).AddTicks(6004));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 723, DateTimeKind.Utc).AddTicks(6006));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 723, DateTimeKind.Utc).AddTicks(6007));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 723, DateTimeKind.Utc).AddTicks(6012));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000a"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 723, DateTimeKind.Utc).AddTicks(6014));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000b"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 723, DateTimeKind.Utc).AddTicks(6015));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000c"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 723, DateTimeKind.Utc).AddTicks(6017));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000d"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 723, DateTimeKind.Utc).AddTicks(6019));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000e"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 723, DateTimeKind.Utc).AddTicks(6021));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000f"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 723, DateTimeKind.Utc).AddTicks(6023));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 723, DateTimeKind.Utc).AddTicks(6025));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 723, DateTimeKind.Utc).AddTicks(6028));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 723, DateTimeKind.Utc).AddTicks(6030));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 723, DateTimeKind.Utc).AddTicks(6032));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 723, DateTimeKind.Utc).AddTicks(6034));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 723, DateTimeKind.Utc).AddTicks(6035));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 723, DateTimeKind.Utc).AddTicks(6037));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000017"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 723, DateTimeKind.Utc).AddTicks(6038));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000018"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 723, DateTimeKind.Utc).AddTicks(6040));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000019"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 15, 14, 59, 723, DateTimeKind.Utc).AddTicks(6042));
        }
    }
}
