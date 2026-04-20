using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrvixFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentIdToKnowledgeBaseDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "KnowledgeBaseDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseDocuments_DepartmentId",
                table: "KnowledgeBaseDocuments",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseDocuments_TenantId_DepartmentId",
                table: "KnowledgeBaseDocuments",
                columns: new[] { "TenantId", "DepartmentId" });

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeBaseDocuments_Departments_DepartmentId",
                table: "KnowledgeBaseDocuments",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeBaseDocuments_Departments_DepartmentId",
                table: "KnowledgeBaseDocuments");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeBaseDocuments_DepartmentId",
                table: "KnowledgeBaseDocuments");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeBaseDocuments_TenantId_DepartmentId",
                table: "KnowledgeBaseDocuments");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "KnowledgeBaseDocuments");
        }
    }
}
