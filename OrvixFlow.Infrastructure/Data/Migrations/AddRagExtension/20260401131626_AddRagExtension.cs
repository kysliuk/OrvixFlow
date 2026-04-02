using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;
using Pgvector;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OrvixFlow.Infrastructure.Data.Migrations.AddRagExtension
{
    /// <inheritdoc />
    public partial class AddRagExtension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Vector>(
                name: "EmbeddingVector",
                table: "KnowledgeBases",
                type: "vector(1536)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChunkIndex",
                table: "KnowledgeBases",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ChunkType",
                table: "KnowledgeBases",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "DocumentId",
                table: "KnowledgeBases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "KnowledgeBases",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "KnowledgeBases",
                type: "tsvector",
                nullable: true)
                .Annotation("Npgsql:TsVectorConfig", "english")
                .Annotation("Npgsql:TsVectorProperties", new[] { "Title", "RawContent" });

            migrationBuilder.CreateTable(
                name: "KnowledgeBaseDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    SourceType = table.Column<string>(type: "text", nullable: false),
                    StoragePath = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IndexedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeBaseDocuments", x => x.Id);
                });



            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBases_DocumentId",
                table: "KnowledgeBases",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBases_EmbeddingVector",
                table: "KnowledgeBases",
                column: "EmbeddingVector")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBases_SearchVector",
                table: "KnowledgeBases",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeBases_KnowledgeBaseDocuments_DocumentId",
                table: "KnowledgeBases",
                column: "DocumentId",
                principalTable: "KnowledgeBaseDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeBases_KnowledgeBaseDocuments_DocumentId",
                table: "KnowledgeBases");

            migrationBuilder.DropTable(
                name: "KnowledgeBaseDocuments");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeBases_DocumentId",
                table: "KnowledgeBases");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeBases_EmbeddingVector",
                table: "KnowledgeBases");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeBases_SearchVector",
                table: "KnowledgeBases");

            migrationBuilder.DropColumn(
                name: "ChunkIndex",
                table: "KnowledgeBases");

            migrationBuilder.DropColumn(
                name: "ChunkType",
                table: "KnowledgeBases");

            migrationBuilder.DropColumn(
                name: "DocumentId",
                table: "KnowledgeBases");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "KnowledgeBases");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "KnowledgeBases");

            migrationBuilder.AlterColumn<Vector>(
                name: "EmbeddingVector",
                table: "KnowledgeBases",
                type: "vector",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(1536)",
                oldNullable: true);
        }
    }
}
