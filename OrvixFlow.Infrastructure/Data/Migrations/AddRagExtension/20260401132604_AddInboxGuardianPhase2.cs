using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OrvixFlow.Infrastructure.Data.Migrations.AddRagExtension
{
    /// <inheritdoc />
    public partial class AddInboxGuardianPhase2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("01203a6a-31e9-4e43-9187-fdfba0802558"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("07f1e45d-b895-4837-b1cb-92befc225ac8"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("0b495272-73dd-40d9-a478-06961696a6a8"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("1d1a56c6-b03a-4080-8f55-ad4a404df87b"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("284dc61d-34ee-4d7a-ac89-b683259eea29"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("2fc33f06-ccee-4e0a-a396-fac17a6c176a"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("3329d0cf-961b-46c5-b4f1-90c7dbb28429"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("355a9dc0-4312-405d-8a56-b18d00d5b862"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("4aebc2ba-6735-40b8-afa0-0bd96c0c7007"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("4d725713-4cee-4383-a341-b99a046f5000"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("55dab1fe-3f3a-4265-8b95-be244d1ce632"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("5f8547ee-79be-4488-bec1-c66ebe711356"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("9425cbc2-63b9-46ec-ac52-9f420a06d247"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("98f5837a-c542-415c-8a77-f085d3e815d5"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("9aadec2d-fe92-4da7-a7f8-631db9aa7bf1"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a58131eb-a185-4cdc-ad7f-4e4d1b5f8f1c"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("b71782af-b9ae-4570-9178-d32f2f50d31a"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("b8e5501b-789e-4c12-b0fd-e66e53468b6b"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("bf234208-c210-4577-9d83-4afcbd6c1ac9"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("e7048034-b960-44a4-98eb-f60168d82fc1"));

            migrationBuilder.CreateTable(
                name: "AgentPersonas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tone = table.Column<string>(type: "text", nullable: false),
                    CustomInstructions = table.Column<string>(type: "text", nullable: false),
                    CustomSignOff = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentPersonas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DraftFeedbacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalDraft = table.Column<string>(type: "text", nullable: false),
                    FinalHumanDraft = table.Column<string>(type: "text", nullable: false),
                    EditDistance = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DraftFeedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DraftFeedbacks_ActionRequests_ActionRequestId",
                        column: x => x.ActionRequestId,
                        principalTable: "ActionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MailboxConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailAddress = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    N8nWorkflowId = table.Column<string>(type: "text", nullable: true),
                    N8nCredentialId = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConnectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailboxConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MailboxConnections_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 1, 13, 26, 0, 629, DateTimeKind.Utc).AddTicks(3889));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 1, 13, 26, 0, 631, DateTimeKind.Utc).AddTicks(1570));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 1, 13, 26, 0, 631, DateTimeKind.Utc).AddTicks(1626));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 1, 13, 26, 0, 631, DateTimeKind.Utc).AddTicks(1658));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 1, 13, 26, 0, 631, DateTimeKind.Utc).AddTicks(1689));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 1, 13, 26, 0, 631, DateTimeKind.Utc).AddTicks(1722));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 1, 13, 26, 0, 631, DateTimeKind.Utc).AddTicks(1752));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 1, 13, 26, 0, 631, DateTimeKind.Utc).AddTicks(1781));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 1, 13, 26, 0, 631, DateTimeKind.Utc).AddTicks(1811));

            migrationBuilder.InsertData(
                table: "PlanModuleInclusions",
                columns: new[] { "Id", "CreatedAt", "ModuleDefinitionId", "PlanTemplateId" },
                values: new object[,]
                {
                    { new Guid("08f7f4f4-0691-4007-9257-e4eebf746654"), new DateTime(2026, 4, 1, 13, 26, 0, 644, DateTimeKind.Utc).AddTicks(1182), new Guid("44444444-4444-4444-4444-444444444444"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") },
                    { new Guid("196a88fa-08da-4c8e-90bd-1a9c356ede43"), new DateTime(2026, 4, 1, 13, 26, 0, 643, DateTimeKind.Utc).AddTicks(8307), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa") },
                    { new Guid("2b9c1a2e-b902-40be-805b-a3eff211b05e"), new DateTime(2026, 4, 1, 13, 26, 0, 644, DateTimeKind.Utc).AddTicks(1493), new Guid("77777777-7777-7777-7777-777777777777"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("2e48dadb-6908-4377-82cd-0a0ca53d01c9"), new DateTime(2026, 4, 1, 13, 26, 0, 644, DateTimeKind.Utc).AddTicks(1465), new Guid("55555555-5555-5555-5555-555555555555"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("446c894f-d7c5-4714-87d5-975695836abc"), new DateTime(2026, 4, 1, 13, 26, 0, 644, DateTimeKind.Utc).AddTicks(1442), new Guid("22222222-2222-2222-2222-222222222222"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("6c80e6e6-2c76-439c-9077-685f38dc2056"), new DateTime(2026, 4, 1, 13, 26, 0, 644, DateTimeKind.Utc).AddTicks(1421), new Guid("44444444-4444-4444-4444-444444444444"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("849abdae-3f67-42ec-9c86-f60f5d812255"), new DateTime(2026, 4, 1, 13, 26, 0, 644, DateTimeKind.Utc).AddTicks(1157), new Guid("11111111-1111-1111-1111-111111111111"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") },
                    { new Guid("882ed7df-5285-49dc-ab3e-5b470ba9de6f"), new DateTime(2026, 4, 1, 13, 26, 0, 644, DateTimeKind.Utc).AddTicks(1398), new Guid("11111111-1111-1111-1111-111111111111"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("97721cb6-1854-4525-aef1-2899884ab5a4"), new DateTime(2026, 4, 1, 13, 26, 0, 644, DateTimeKind.Utc).AddTicks(1348), new Guid("77777777-7777-7777-7777-777777777777"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("a39f2612-2862-416f-a051-b36aee5e1489"), new DateTime(2026, 4, 1, 13, 26, 0, 644, DateTimeKind.Utc).AddTicks(1301), new Guid("22222222-2222-2222-2222-222222222222"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("a887af5f-5baa-4132-9a8a-3b75a8d77fc4"), new DateTime(2026, 4, 1, 13, 26, 0, 644, DateTimeKind.Utc).AddTicks(1108), new Guid("11111111-1111-1111-1111-111111111111"), new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") },
                    { new Guid("ac311e2f-3564-44ad-9714-b556efefd035"), new DateTime(2026, 4, 1, 13, 26, 0, 644, DateTimeKind.Utc).AddTicks(1277), new Guid("44444444-4444-4444-4444-444444444444"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("b27057d3-76b5-4c12-83f1-5fd441c04e21"), new DateTime(2026, 4, 1, 13, 26, 0, 644, DateTimeKind.Utc).AddTicks(1514), new Guid("66666666-6666-6666-6666-666666666666"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("b62d61f0-d631-4382-b9c4-07a7ca0117fe"), new DateTime(2026, 4, 1, 13, 26, 0, 644, DateTimeKind.Utc).AddTicks(1324), new Guid("55555555-5555-5555-5555-555555555555"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("bafe0c1c-edbb-420e-9171-0507f4bc6a4f"), new DateTime(2026, 4, 1, 13, 26, 0, 644, DateTimeKind.Utc).AddTicks(1230), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("d2c3ab76-4d33-442f-987d-dfdefa113a6e"), new DateTime(2026, 4, 1, 13, 26, 0, 644, DateTimeKind.Utc).AddTicks(1079), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") },
                    { new Guid("d9cfc5e9-bc40-4acb-9c42-ac7923637c9b"), new DateTime(2026, 4, 1, 13, 26, 0, 644, DateTimeKind.Utc).AddTicks(1208), new Guid("22222222-2222-2222-2222-222222222222"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") },
                    { new Guid("da19a03f-699a-4155-8fb0-77cd1e7e529e"), new DateTime(2026, 4, 1, 13, 26, 0, 644, DateTimeKind.Utc).AddTicks(1371), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("df2df6d0-32b1-47fc-9e03-7979c00eb609"), new DateTime(2026, 4, 1, 13, 26, 0, 644, DateTimeKind.Utc).AddTicks(1253), new Guid("11111111-1111-1111-1111-111111111111"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("fdf21d0f-9270-4d55-af89-dae97e40d1a6"), new DateTime(2026, 4, 1, 13, 26, 0, 644, DateTimeKind.Utc).AddTicks(1135), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentPersonas_TenantId",
                table: "AgentPersonas",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DraftFeedbacks_ActionRequestId",
                table: "DraftFeedbacks",
                column: "ActionRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_DraftFeedbacks_TenantId_ActionRequestId",
                table: "DraftFeedbacks",
                columns: new[] { "TenantId", "ActionRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_MailboxConnections_TenantId_EmailAddress",
                table: "MailboxConnections",
                columns: new[] { "TenantId", "EmailAddress" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MailboxConnections_UserId",
                table: "MailboxConnections",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentPersonas");

            migrationBuilder.DropTable(
                name: "DraftFeedbacks");

            migrationBuilder.DropTable(
                name: "MailboxConnections");

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("08f7f4f4-0691-4007-9257-e4eebf746654"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("196a88fa-08da-4c8e-90bd-1a9c356ede43"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("2b9c1a2e-b902-40be-805b-a3eff211b05e"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("2e48dadb-6908-4377-82cd-0a0ca53d01c9"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("446c894f-d7c5-4714-87d5-975695836abc"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("6c80e6e6-2c76-439c-9077-685f38dc2056"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("849abdae-3f67-42ec-9c86-f60f5d812255"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("882ed7df-5285-49dc-ab3e-5b470ba9de6f"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("97721cb6-1854-4525-aef1-2899884ab5a4"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a39f2612-2862-416f-a051-b36aee5e1489"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("a887af5f-5baa-4132-9a8a-3b75a8d77fc4"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("ac311e2f-3564-44ad-9714-b556efefd035"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("b27057d3-76b5-4c12-83f1-5fd441c04e21"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("b62d61f0-d631-4382-b9c4-07a7ca0117fe"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("bafe0c1c-edbb-420e-9171-0507f4bc6a4f"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("d2c3ab76-4d33-442f-987d-dfdefa113a6e"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("d9cfc5e9-bc40-4acb-9c42-ac7923637c9b"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("da19a03f-699a-4155-8fb0-77cd1e7e529e"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("df2df6d0-32b1-47fc-9e03-7979c00eb609"));

            migrationBuilder.DeleteData(
                table: "PlanModuleInclusions",
                keyColumn: "Id",
                keyValue: new Guid("fdf21d0f-9270-4d55-af89-dae97e40d1a6"));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 1, 13, 16, 24, 121, DateTimeKind.Utc).AddTicks(9445));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 1, 13, 16, 24, 122, DateTimeKind.Utc).AddTicks(6591));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 1, 13, 16, 24, 122, DateTimeKind.Utc).AddTicks(6623));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 1, 13, 16, 24, 122, DateTimeKind.Utc).AddTicks(6645));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 1, 13, 16, 24, 122, DateTimeKind.Utc).AddTicks(6665));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 1, 13, 16, 24, 122, DateTimeKind.Utc).AddTicks(6893));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 1, 13, 16, 24, 122, DateTimeKind.Utc).AddTicks(6920));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 1, 13, 16, 24, 122, DateTimeKind.Utc).AddTicks(6940));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"),
                column: "CreatedAt",
                value: new DateTime(2026, 4, 1, 13, 16, 24, 122, DateTimeKind.Utc).AddTicks(6961));

            migrationBuilder.InsertData(
                table: "PlanModuleInclusions",
                columns: new[] { "Id", "CreatedAt", "ModuleDefinitionId", "PlanTemplateId" },
                values: new object[,]
                {
                    { new Guid("01203a6a-31e9-4e43-9187-fdfba0802558"), new DateTime(2026, 4, 1, 13, 16, 24, 133, DateTimeKind.Utc).AddTicks(8354), new Guid("11111111-1111-1111-1111-111111111111"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("07f1e45d-b895-4837-b1cb-92befc225ac8"), new DateTime(2026, 4, 1, 13, 16, 24, 133, DateTimeKind.Utc).AddTicks(8550), new Guid("66666666-6666-6666-6666-666666666666"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("0b495272-73dd-40d9-a478-06961696a6a8"), new DateTime(2026, 4, 1, 13, 16, 24, 133, DateTimeKind.Utc).AddTicks(8459), new Guid("11111111-1111-1111-1111-111111111111"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("1d1a56c6-b03a-4080-8f55-ad4a404df87b"), new DateTime(2026, 4, 1, 13, 16, 24, 133, DateTimeKind.Utc).AddTicks(8515), new Guid("55555555-5555-5555-5555-555555555555"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("284dc61d-34ee-4d7a-ac89-b683259eea29"), new DateTime(2026, 4, 1, 13, 16, 24, 133, DateTimeKind.Utc).AddTicks(8319), new Guid("22222222-2222-2222-2222-222222222222"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") },
                    { new Guid("2fc33f06-ccee-4e0a-a396-fac17a6c176a"), new DateTime(2026, 4, 1, 13, 16, 24, 133, DateTimeKind.Utc).AddTicks(8227), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") },
                    { new Guid("3329d0cf-961b-46c5-b4f1-90c7dbb28429"), new DateTime(2026, 4, 1, 13, 16, 24, 133, DateTimeKind.Utc).AddTicks(6230), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa") },
                    { new Guid("355a9dc0-4312-405d-8a56-b18d00d5b862"), new DateTime(2026, 4, 1, 13, 16, 24, 133, DateTimeKind.Utc).AddTicks(8424), new Guid("77777777-7777-7777-7777-777777777777"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("4aebc2ba-6735-40b8-afa0-0bd96c0c7007"), new DateTime(2026, 4, 1, 13, 16, 24, 133, DateTimeKind.Utc).AddTicks(8477), new Guid("44444444-4444-4444-4444-444444444444"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("4d725713-4cee-4383-a341-b99a046f5000"), new DateTime(2026, 4, 1, 13, 16, 24, 133, DateTimeKind.Utc).AddTicks(8301), new Guid("44444444-4444-4444-4444-444444444444"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") },
                    { new Guid("55dab1fe-3f3a-4265-8b95-be244d1ce632"), new DateTime(2026, 4, 1, 13, 16, 24, 133, DateTimeKind.Utc).AddTicks(8249), new Guid("11111111-1111-1111-1111-111111111111"), new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") },
                    { new Guid("5f8547ee-79be-4488-bec1-c66ebe711356"), new DateTime(2026, 4, 1, 13, 16, 24, 133, DateTimeKind.Utc).AddTicks(8532), new Guid("77777777-7777-7777-7777-777777777777"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("9425cbc2-63b9-46ec-ac52-9f420a06d247"), new DateTime(2026, 4, 1, 13, 16, 24, 133, DateTimeKind.Utc).AddTicks(8496), new Guid("22222222-2222-2222-2222-222222222222"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("98f5837a-c542-415c-8a77-f085d3e815d5"), new DateTime(2026, 4, 1, 13, 16, 24, 133, DateTimeKind.Utc).AddTicks(8336), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("9aadec2d-fe92-4da7-a7f8-631db9aa7bf1"), new DateTime(2026, 4, 1, 13, 16, 24, 133, DateTimeKind.Utc).AddTicks(8407), new Guid("55555555-5555-5555-5555-555555555555"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("a58131eb-a185-4cdc-ad7f-4e4d1b5f8f1c"), new DateTime(2026, 4, 1, 13, 16, 24, 133, DateTimeKind.Utc).AddTicks(8442), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
                    { new Guid("b71782af-b9ae-4570-9178-d32f2f50d31a"), new DateTime(2026, 4, 1, 13, 16, 24, 133, DateTimeKind.Utc).AddTicks(8284), new Guid("11111111-1111-1111-1111-111111111111"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") },
                    { new Guid("b8e5501b-789e-4c12-b0fd-e66e53468b6b"), new DateTime(2026, 4, 1, 13, 16, 24, 133, DateTimeKind.Utc).AddTicks(8266), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") },
                    { new Guid("bf234208-c210-4577-9d83-4afcbd6c1ac9"), new DateTime(2026, 4, 1, 13, 16, 24, 133, DateTimeKind.Utc).AddTicks(8372), new Guid("44444444-4444-4444-4444-444444444444"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") },
                    { new Guid("e7048034-b960-44a4-98eb-f60168d82fc1"), new DateTime(2026, 4, 1, 13, 16, 24, 133, DateTimeKind.Utc).AddTicks(8390), new Guid("22222222-2222-2222-2222-222222222222"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") }
                });
        }
    }
}
