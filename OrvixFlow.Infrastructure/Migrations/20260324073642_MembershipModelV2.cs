using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OrvixFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MembershipModelV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BillingSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    StripeCustomerId = table.Column<string>(type: "text", nullable: false),
                    StripeSubscriptionId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CurrentPlan = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Departments_Tenants_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModuleDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    Tier = table.Column<string>(type: "text", nullable: false),
                    Visibility = table.Column<string>(type: "text", nullable: false),
                    IsOperational = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UsageEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModuleKey = table.Column<string>(type: "text", nullable: false),
                    MetricType = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserCompanyMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyRole = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    InvitedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InvitedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCompanyMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCompanyMemberships_Tenants_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserCompanyMemberships_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModuleAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Scope = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModuleAssignments_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModuleAssignments_ModuleDefinitions_ModuleDefinitionId",
                        column: x => x.ModuleDefinitionId,
                        principalTable: "ModuleDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModuleAssignments_Tenants_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModuleAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserDepartmentMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentRole = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserCompanyMembershipId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDepartmentMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDepartmentMemberships_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserDepartmentMemberships_Tenants_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserDepartmentMemberships_UserCompanyMemberships_UserCompan~",
                        column: x => x.UserCompanyMembershipId,
                        principalTable: "UserCompanyMemberships",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserDepartmentMemberships_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModulePermissionGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanView = table.Column<bool>(type: "boolean", nullable: false),
                    CanUse = table.Column<bool>(type: "boolean", nullable: false),
                    CanTest = table.Column<bool>(type: "boolean", nullable: false),
                    CanConfigure = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageIntegrations = table.Column<bool>(type: "boolean", nullable: false),
                    CanManagePrompts = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewLogs = table.Column<bool>(type: "boolean", nullable: false),
                    IsAdmin = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModulePermissionGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModulePermissionGrants_ModuleAssignments_ModuleAssignmentId",
                        column: x => x.ModuleAssignmentId,
                        principalTable: "ModuleAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ModuleDefinitions",
                columns: new[] { "Id", "CreatedAt", "DisplayName", "IsActive", "IsOperational", "Key", "Tier", "Visibility" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), new DateTime(2026, 3, 24, 7, 36, 34, 574, DateTimeKind.Utc).AddTicks(135), "Doc-Intel", true, false, "doc-intel", "Utility", "UserFacing" },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new DateTime(2026, 3, 24, 7, 36, 34, 574, DateTimeKind.Utc).AddTicks(6617), "Finance-Flow", true, false, "finance-flow", "Utility", "UserFacing" },
                    { new Guid("33333333-3333-3333-3333-333333333333"), new DateTime(2026, 3, 24, 7, 36, 34, 574, DateTimeKind.Utc).AddTicks(6632), "Inbox-Guardian", true, false, "inbox-guardian", "Utility", "UserFacing" },
                    { new Guid("44444444-4444-4444-4444-444444444444"), new DateTime(2026, 3, 24, 7, 36, 34, 574, DateTimeKind.Utc).AddTicks(6662), "Lead-Qualifier", true, false, "lead-qualifier", "Industry", "UserFacing" },
                    { new Guid("55555555-5555-5555-5555-555555555555"), new DateTime(2026, 3, 24, 7, 36, 34, 574, DateTimeKind.Utc).AddTicks(6670), "Legal-Scribe", true, false, "legal-scribe", "Industry", "UserFacing" },
                    { new Guid("66666666-6666-6666-6666-666666666666"), new DateTime(2026, 3, 24, 7, 36, 34, 574, DateTimeKind.Utc).AddTicks(6790), "Data-Guardian", true, false, "data-guardian", "Industry", "UserFacing" },
                    { new Guid("77777777-7777-7777-7777-777777777777"), new DateTime(2026, 3, 24, 7, 36, 34, 574, DateTimeKind.Utc).AddTicks(6797), "SOP-Generator", true, false, "sop-generator", "Industry", "UserFacing" },
                    { new Guid("88888888-8888-8888-8888-888888888888"), new DateTime(2026, 3, 24, 7, 36, 34, 574, DateTimeKind.Utc).AddTicks(6803), "Metered-Billing", true, true, "metered-billing", "Shadow", "Restricted" },
                    { new Guid("99999999-9999-9999-9999-999999999999"), new DateTime(2026, 3, 24, 7, 36, 34, 574, DateTimeKind.Utc).AddTicks(6808), "Audit-Log", true, true, "audit-log", "Shadow", "Restricted" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillingSubscriptions_CompanyId",
                table: "BillingSubscriptions",
                column: "CompanyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_CompanyId_Code",
                table: "Departments",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModuleAssignments_CompanyId_ModuleDefinitionId_DepartmentId~",
                table: "ModuleAssignments",
                columns: new[] { "CompanyId", "ModuleDefinitionId", "DepartmentId", "UserId", "Scope" });

            migrationBuilder.CreateIndex(
                name: "IX_ModuleAssignments_DepartmentId",
                table: "ModuleAssignments",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleAssignments_ModuleDefinitionId",
                table: "ModuleAssignments",
                column: "ModuleDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleAssignments_UserId",
                table: "ModuleAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleDefinitions_Key",
                table: "ModuleDefinitions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModulePermissionGrants_ModuleAssignmentId",
                table: "ModulePermissionGrants",
                column: "ModuleAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageEvents_CompanyId_MetricType_OccurredAt",
                table: "UsageEvents",
                columns: new[] { "CompanyId", "MetricType", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanyMemberships_CompanyId",
                table: "UserCompanyMemberships",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanyMemberships_UserId_CompanyId",
                table: "UserCompanyMemberships",
                columns: new[] { "UserId", "CompanyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserDepartmentMemberships_CompanyId",
                table: "UserDepartmentMemberships",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDepartmentMemberships_DepartmentId",
                table: "UserDepartmentMemberships",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDepartmentMemberships_UserCompanyMembershipId",
                table: "UserDepartmentMemberships",
                column: "UserCompanyMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDepartmentMemberships_UserId_CompanyId_DepartmentId",
                table: "UserDepartmentMemberships",
                columns: new[] { "UserId", "CompanyId", "DepartmentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillingSubscriptions");

            migrationBuilder.DropTable(
                name: "ModulePermissionGrants");

            migrationBuilder.DropTable(
                name: "UsageEvents");

            migrationBuilder.DropTable(
                name: "UserDepartmentMemberships");

            migrationBuilder.DropTable(
                name: "ModuleAssignments");

            migrationBuilder.DropTable(
                name: "UserCompanyMemberships");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "ModuleDefinitions");
        }
    }
}
