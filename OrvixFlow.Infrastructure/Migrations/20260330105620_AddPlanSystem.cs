using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrvixFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "ModuleDefinitions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ModuleDefinitions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsPremium",
                table: "ModuleDefinitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PlanTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    MonthlyPriceCents = table.Column<int>(type: "integer", nullable: false),
                    YearlyPriceCents = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    BillingInterval = table.Column<string>(type: "text", nullable: false),
                    MaxSeats = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsFree = table.Column<bool>(type: "boolean", nullable: false),
                    IsTrialAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    TrialDays = table.Column<int>(type: "integer", nullable: false),
                    LegacyLocked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompanySubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    BillingInterval = table.Column<string>(type: "text", nullable: false),
                    CurrentPeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CurrentPeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TrialEndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PendingPlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    PendingChangeAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExternalSubscriptionId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanySubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanySubscriptions_PlanTemplates_PendingPlanId",
                        column: x => x.PendingPlanId,
                        principalTable: "PlanTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanySubscriptions_PlanTemplates_PlanTemplateId",
                        column: x => x.PlanTemplateId,
                        principalTable: "PlanTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanySubscriptions_Tenants_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanEntitlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaxMonthlyTokens = table.Column<int>(type: "integer", nullable: false),
                    MaxApiRequestsPerDay = table.Column<int>(type: "integer", nullable: false),
                    MaxStorageMb = table.Column<int>(type: "integer", nullable: false),
                    MaxKnowledgeBases = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanEntitlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanEntitlements_PlanTemplates_PlanTemplateId",
                        column: x => x.PlanTemplateId,
                        principalTable: "PlanTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanModuleInclusions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanModuleInclusions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanModuleInclusions_ModuleDefinitions_ModuleDefinitionId",
                        column: x => x.ModuleDefinitionId,
                        principalTable: "ModuleDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanModuleInclusions_PlanTemplates_PlanTemplateId",
                        column: x => x.PlanTemplateId,
                        principalTable: "PlanTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "Category", "CreatedAt", "Description", "IsPremium" },
                values: new object[] { "Utility", new DateTime(2026, 3, 30, 10, 56, 17, 638, DateTimeKind.Utc).AddTicks(1054), "", false });

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                columns: new[] { "Category", "CreatedAt", "Description", "IsPremium" },
                values: new object[] { "Utility", new DateTime(2026, 3, 30, 10, 56, 17, 638, DateTimeKind.Utc).AddTicks(8717), "", false });

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                columns: new[] { "Category", "CreatedAt", "Description", "IsPremium" },
                values: new object[] { "Utility", new DateTime(2026, 3, 30, 10, 56, 17, 638, DateTimeKind.Utc).AddTicks(8758), "", false });

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                columns: new[] { "Category", "CreatedAt", "Description", "IsPremium" },
                values: new object[] { "Utility", new DateTime(2026, 3, 30, 10, 56, 17, 638, DateTimeKind.Utc).AddTicks(8780), "", false });

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                columns: new[] { "Category", "CreatedAt", "Description", "IsPremium" },
                values: new object[] { "Utility", new DateTime(2026, 3, 30, 10, 56, 17, 638, DateTimeKind.Utc).AddTicks(8801), "", false });

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"),
                columns: new[] { "Category", "CreatedAt", "Description", "IsPremium" },
                values: new object[] { "Utility", new DateTime(2026, 3, 30, 10, 56, 17, 638, DateTimeKind.Utc).AddTicks(8890), "", false });

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"),
                columns: new[] { "Category", "CreatedAt", "Description", "IsPremium" },
                values: new object[] { "Utility", new DateTime(2026, 3, 30, 10, 56, 17, 638, DateTimeKind.Utc).AddTicks(8911), "", false });

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"),
                columns: new[] { "Category", "CreatedAt", "Description", "IsPremium" },
                values: new object[] { "Utility", new DateTime(2026, 3, 30, 10, 56, 17, 638, DateTimeKind.Utc).AddTicks(8931), "", false });

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"),
                columns: new[] { "Category", "CreatedAt", "Description", "IsPremium" },
                values: new object[] { "Utility", new DateTime(2026, 3, 30, 10, 56, 17, 638, DateTimeKind.Utc).AddTicks(8952), "", false });

            migrationBuilder.CreateIndex(
                name: "IX_CompanySubscriptions_CompanyId",
                table: "CompanySubscriptions",
                column: "CompanyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanySubscriptions_PendingPlanId",
                table: "CompanySubscriptions",
                column: "PendingPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanySubscriptions_PlanTemplateId",
                table: "CompanySubscriptions",
                column: "PlanTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanEntitlements_PlanTemplateId",
                table: "PlanEntitlements",
                column: "PlanTemplateId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanModuleInclusions_ModuleDefinitionId",
                table: "PlanModuleInclusions",
                column: "ModuleDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanModuleInclusions_PlanTemplateId_ModuleDefinitionId",
                table: "PlanModuleInclusions",
                columns: new[] { "PlanTemplateId", "ModuleDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanTemplates_Slug",
                table: "PlanTemplates",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanySubscriptions");

            migrationBuilder.DropTable(
                name: "PlanEntitlements");

            migrationBuilder.DropTable(
                name: "PlanModuleInclusions");

            migrationBuilder.DropTable(
                name: "PlanTemplates");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "ModuleDefinitions");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ModuleDefinitions");

            migrationBuilder.DropColumn(
                name: "IsPremium",
                table: "ModuleDefinitions");

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 26, 8, 7, 33, 363, DateTimeKind.Utc).AddTicks(7073));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 26, 8, 7, 33, 364, DateTimeKind.Utc).AddTicks(3963));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 26, 8, 7, 33, 364, DateTimeKind.Utc).AddTicks(3984));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 26, 8, 7, 33, 364, DateTimeKind.Utc).AddTicks(4118));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 26, 8, 7, 33, 364, DateTimeKind.Utc).AddTicks(4124));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 26, 8, 7, 33, 364, DateTimeKind.Utc).AddTicks(4129));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 26, 8, 7, 33, 364, DateTimeKind.Utc).AddTicks(4162));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 26, 8, 7, 33, 364, DateTimeKind.Utc).AddTicks(4167));

            migrationBuilder.UpdateData(
                table: "ModuleDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 26, 8, 7, 33, 364, DateTimeKind.Utc).AddTicks(4173));
        }
    }
}
