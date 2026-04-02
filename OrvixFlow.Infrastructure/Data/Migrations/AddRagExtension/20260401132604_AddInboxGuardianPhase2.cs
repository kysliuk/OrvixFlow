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
        }
    }
}
