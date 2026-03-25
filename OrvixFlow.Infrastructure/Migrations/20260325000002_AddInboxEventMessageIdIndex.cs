using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrvixFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInboxEventMessageIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_InboxEvents_TenantId_MessageId",
                table: "InboxEvents",
                columns: new[] { "TenantId", "MessageId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InboxEvents_TenantId_MessageId",
                table: "InboxEvents");
        }
    }
}
