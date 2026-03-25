using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrvixFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInboxEventWebhookCallbackPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WebhookCallbackPath",
                table: "InboxEvents",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WebhookCallbackPath",
                table: "InboxEvents");
        }
    }
}
