using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrvixFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentRbacPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvitedDepartmentRole",
                table: "Invitations",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""
UPDATE "UserDepartmentMemberships"
SET "DepartmentRole" = 'DepartmentManager'
WHERE "DepartmentRole" = 'Manager';

UPDATE "UserDepartmentMemberships"
SET "DepartmentRole" = 'DepartmentOperator'
WHERE "DepartmentRole" = 'Member';

INSERT INTO "UserDepartmentMemberships" ("Id", "UserId", "CompanyId", "DepartmentId", "DepartmentRole", "Status", "CreatedAt")
SELECT
    md5(random()::text || clock_timestamp()::text)::uuid,
    ucm."UserId",
    ucm."CompanyId",
    d."Id",
    CASE WHEN ucm."CompanyRole" = 'DepartmentManager' THEN 'DepartmentManager' ELSE 'DepartmentOperator' END,
    COALESCE(ucm."Status", 'Active'),
    NOW()
FROM "UserCompanyMemberships" ucm
INNER JOIN "Departments" d ON d."CompanyId" = ucm."CompanyId" AND d."Code" = 'general'
WHERE ucm."CompanyRole" IN ('DepartmentManager', 'Operator', 'Viewer')
  AND NOT EXISTS (
      SELECT 1
      FROM "UserDepartmentMemberships" udm
      WHERE udm."UserId" = ucm."UserId"
        AND udm."CompanyId" = ucm."CompanyId"
  );

UPDATE "UserCompanyMemberships"
SET "CompanyRole" = 'CompanyMember'
WHERE "CompanyRole" IN ('DepartmentManager', 'Operator', 'Viewer');

UPDATE "Invitations"
SET "InvitedDepartmentRole" = 'DepartmentManager', "AssignedRole" = 'CompanyMember'
WHERE "AssignedRole" = 'DepartmentManager' AND "Status" = 'Pending';

UPDATE "Invitations"
SET "InvitedDepartmentRole" = 'DepartmentOperator', "AssignedRole" = 'CompanyMember'
WHERE "AssignedRole" IN ('Operator', 'Viewer') AND "Status" = 'Pending';
""");

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 769, DateTimeKind.Utc).AddTicks(6032));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 769, DateTimeKind.Utc).AddTicks(6067));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 769, DateTimeKind.Utc).AddTicks(6090));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 769, DateTimeKind.Utc).AddTicks(6111));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 769, DateTimeKind.Utc).AddTicks(6135));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 769, DateTimeKind.Utc).AddTicks(6158));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 769, DateTimeKind.Utc).AddTicks(6180));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 769, DateTimeKind.Utc).AddTicks(6201));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 769, DateTimeKind.Utc).AddTicks(6223));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 768, DateTimeKind.Utc).AddTicks(7748));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 784, DateTimeKind.Utc).AddTicks(6218));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 785, DateTimeKind.Utc).AddTicks(542));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 785, DateTimeKind.Utc).AddTicks(585));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 785, DateTimeKind.Utc).AddTicks(617));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 785, DateTimeKind.Utc).AddTicks(643));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 785, DateTimeKind.Utc).AddTicks(671));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 785, DateTimeKind.Utc).AddTicks(701));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 785, DateTimeKind.Utc).AddTicks(729));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 785, DateTimeKind.Utc).AddTicks(757));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000a"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 785, DateTimeKind.Utc).AddTicks(842));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000b"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 785, DateTimeKind.Utc).AddTicks(875));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000c"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 785, DateTimeKind.Utc).AddTicks(902));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000d"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 785, DateTimeKind.Utc).AddTicks(930));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000e"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 785, DateTimeKind.Utc).AddTicks(960));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000f"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 785, DateTimeKind.Utc).AddTicks(990));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 785, DateTimeKind.Utc).AddTicks(1016));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 785, DateTimeKind.Utc).AddTicks(1041));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 785, DateTimeKind.Utc).AddTicks(1069));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 785, DateTimeKind.Utc).AddTicks(1094));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 785, DateTimeKind.Utc).AddTicks(1121));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 785, DateTimeKind.Utc).AddTicks(1148));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 785, DateTimeKind.Utc).AddTicks(1176));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000017"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 785, DateTimeKind.Utc).AddTicks(1202));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000018"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 785, DateTimeKind.Utc).AddTicks(1229));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000019"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 24, 12, 35, 35, 785, DateTimeKind.Utc).AddTicks(1256));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvitedDepartmentRole",
                table: "Invitations");

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 938, DateTimeKind.Utc).AddTicks(6445));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 938, DateTimeKind.Utc).AddTicks(6479));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 938, DateTimeKind.Utc).AddTicks(6501));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 938, DateTimeKind.Utc).AddTicks(6523));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 938, DateTimeKind.Utc).AddTicks(6544));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 938, DateTimeKind.Utc).AddTicks(6565));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 938, DateTimeKind.Utc).AddTicks(6586));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 938, DateTimeKind.Utc).AddTicks(6608));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 938, DateTimeKind.Utc).AddTicks(6825));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 937, DateTimeKind.Utc).AddTicks(9031));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 950, DateTimeKind.Utc).AddTicks(4757));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 950, DateTimeKind.Utc).AddTicks(6944));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 950, DateTimeKind.Utc).AddTicks(6974));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 950, DateTimeKind.Utc).AddTicks(6993));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 950, DateTimeKind.Utc).AddTicks(7013));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 950, DateTimeKind.Utc).AddTicks(7033));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 950, DateTimeKind.Utc).AddTicks(7052));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 950, DateTimeKind.Utc).AddTicks(7072));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 950, DateTimeKind.Utc).AddTicks(7091));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000a"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 950, DateTimeKind.Utc).AddTicks(7138));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000b"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 950, DateTimeKind.Utc).AddTicks(7157));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000c"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 950, DateTimeKind.Utc).AddTicks(7177));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000d"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 950, DateTimeKind.Utc).AddTicks(7196));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000e"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 950, DateTimeKind.Utc).AddTicks(7215));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000f"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 950, DateTimeKind.Utc).AddTicks(7235));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 950, DateTimeKind.Utc).AddTicks(7255));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 950, DateTimeKind.Utc).AddTicks(7274));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 950, DateTimeKind.Utc).AddTicks(7293));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 950, DateTimeKind.Utc).AddTicks(7315));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 950, DateTimeKind.Utc).AddTicks(7333));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 950, DateTimeKind.Utc).AddTicks(7359));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 950, DateTimeKind.Utc).AddTicks(7385));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000017"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 950, DateTimeKind.Utc).AddTicks(7411));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000018"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 950, DateTimeKind.Utc).AddTicks(7438));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000019"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 12, 44, 26, 950, DateTimeKind.Utc).AddTicks(7465));
        }
    }
}
