using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrvixFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMailboxCredential : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CredentialId",
                table: "MailboxConnections",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MailboxCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MailboxConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    ProviderAccountId = table.Column<string>(type: "text", nullable: false),
                    EncryptedAccessToken = table.Column<string>(type: "text", nullable: false),
                    EncryptedRefreshToken = table.Column<string>(type: "text", nullable: false),
                    Scopes = table.Column<string>(type: "text", nullable: false),
                    TokenExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailboxCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MailboxCredentials_MailboxConnections_MailboxConnectionId",
                        column: x => x.MailboxConnectionId,
                        principalTable: "MailboxConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 723, DateTimeKind.Utc).AddTicks(7532));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 723, DateTimeKind.Utc).AddTicks(7588));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 723, DateTimeKind.Utc).AddTicks(7610));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 723, DateTimeKind.Utc).AddTicks(7632));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 723, DateTimeKind.Utc).AddTicks(7654));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 723, DateTimeKind.Utc).AddTicks(7676));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 723, DateTimeKind.Utc).AddTicks(7698));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 723, DateTimeKind.Utc).AddTicks(7718));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 723, DateTimeKind.Utc).AddTicks(7741));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 722, DateTimeKind.Utc).AddTicks(6244));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 733, DateTimeKind.Utc).AddTicks(9706));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 734, DateTimeKind.Utc).AddTicks(1919));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 734, DateTimeKind.Utc).AddTicks(1949));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 734, DateTimeKind.Utc).AddTicks(1972));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 734, DateTimeKind.Utc).AddTicks(2001));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 734, DateTimeKind.Utc).AddTicks(2027));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 734, DateTimeKind.Utc).AddTicks(2053));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 734, DateTimeKind.Utc).AddTicks(2077));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 734, DateTimeKind.Utc).AddTicks(2103));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000a"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 734, DateTimeKind.Utc).AddTicks(2162));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000b"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 734, DateTimeKind.Utc).AddTicks(2189));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000c"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 734, DateTimeKind.Utc).AddTicks(2216));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000d"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 734, DateTimeKind.Utc).AddTicks(2244));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000e"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 734, DateTimeKind.Utc).AddTicks(2272));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000f"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 734, DateTimeKind.Utc).AddTicks(2299));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 734, DateTimeKind.Utc).AddTicks(2324));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 734, DateTimeKind.Utc).AddTicks(2350));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 734, DateTimeKind.Utc).AddTicks(2376));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 734, DateTimeKind.Utc).AddTicks(2402));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 734, DateTimeKind.Utc).AddTicks(2429));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 734, DateTimeKind.Utc).AddTicks(2454));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 734, DateTimeKind.Utc).AddTicks(2480));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000017"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 734, DateTimeKind.Utc).AddTicks(2506));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000018"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 734, DateTimeKind.Utc).AddTicks(2533));

            migrationBuilder.UpdateData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000019"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 11, 48, 25, 734, DateTimeKind.Utc).AddTicks(2562));

            migrationBuilder.CreateIndex(
                name: "IX_MailboxConnections_CredentialId",
                table: "MailboxConnections",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_MailboxCredentials_MailboxConnectionId",
                table: "MailboxCredentials",
                column: "MailboxConnectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_MailboxConnections_MailboxCredentials_CredentialId",
                table: "MailboxConnections",
                column: "CredentialId",
                principalTable: "MailboxCredentials",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MailboxConnections_MailboxCredentials_CredentialId",
                table: "MailboxConnections");

            migrationBuilder.DropTable(
                name: "MailboxCredentials");

            migrationBuilder.DropIndex(
                name: "IX_MailboxConnections_CredentialId",
                table: "MailboxConnections");

            migrationBuilder.DropColumn(
                name: "CredentialId",
                table: "MailboxConnections");

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
    }
}
