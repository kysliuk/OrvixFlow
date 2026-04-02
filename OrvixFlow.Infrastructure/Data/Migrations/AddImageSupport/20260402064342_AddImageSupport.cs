using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OrvixFlow.Infrastructure.Data.Migrations.AddImageSupport
{
    /// <inheritdoc />
    public partial class AddImageSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KnowledgeBaseImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChunkId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoragePath = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    AltText = table.Column<string>(type: "text", nullable: false),
                    Caption = table.Column<string>(type: "text", nullable: true),
                    CaptionEmbedding = table.Column<Vector>(type: "vector(1536)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeBaseImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeBaseImages_KnowledgeBaseDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "KnowledgeBaseDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KnowledgeBaseImages_KnowledgeBases_ChunkId",
                        column: x => x.ChunkId,
                        principalTable: "KnowledgeBases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseImages_CaptionEmbedding",
                table: "KnowledgeBaseImages",
                column: "CaptionEmbedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseImages_ChunkId",
                table: "KnowledgeBaseImages",
                column: "ChunkId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseImages_DocumentId",
                table: "KnowledgeBaseImages",
                column: "DocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnowledgeBaseImages");
        }
    }
}
