using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrvixFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ApplyPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "KnowledgeBases",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Actor",
                table: "AuditTrails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EntityId",
                table: "AuditTrails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NewState",
                table: "AuditTrails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PreviousState",
                table: "AuditTrails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "InboxEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<string>(type: "text", nullable: false),
                    ThreadId = table.Column<string>(type: "text", nullable: true),
                    SenderEmail = table.Column<string>(type: "text", nullable: false),
                    SenderName = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    BodyText = table.Column<string>(type: "text", nullable: false),
                    WebhookCallbackPath = table.Column<string>(type: "text", nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    AutoExecute = table.Column<bool>(type: "boolean", nullable: false),
                    ConfidenceThreshold = table.Column<decimal>(type: "numeric", nullable: false),
                    ExcludedKeywords = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ActionRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    InboxEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluatedCategory = table.Column<string>(type: "text", nullable: false),
                    ConfidenceScore = table.Column<decimal>(type: "numeric", nullable: false),
                    DraftResponse = table.Column<string>(type: "text", nullable: false),
                    PolicyReason = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionRequests_InboxEvents_InboxEventId",
                        column: x => x.InboxEventId,
                        principalTable: "InboxEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActionRequests_InboxEventId",
                table: "ActionRequests",
                column: "InboxEventId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionRequests");

            migrationBuilder.DropTable(
                name: "WorkflowPolicies");

            migrationBuilder.DropTable(
                name: "InboxEvents");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "KnowledgeBases");

            migrationBuilder.DropColumn(
                name: "Actor",
                table: "AuditTrails");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "AuditTrails");

            migrationBuilder.DropColumn(
                name: "NewState",
                table: "AuditTrails");

            migrationBuilder.DropColumn(
                name: "PreviousState",
                table: "AuditTrails");
        }
    }
}
