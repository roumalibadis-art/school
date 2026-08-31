using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace USTHBStudy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Documents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Title = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Slug = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ModuleId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AcademicYearId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    SessionId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    FileStorageKey = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PreviewStorageKey = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ThumbnailStorageKey = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FileName = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    PageCount = table.Column<int>(type: "int", nullable: true),
                    MimeType = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FileHashSha256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsPremium = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Source = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RightsStatus = table.Column<int>(type: "int", nullable: false),
                    PermissionNotes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UploadedById = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ReviewNote = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PublishedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ViewCount = table.Column<long>(type: "bigint", nullable: false),
                    DownloadCount = table.Column<long>(type: "bigint", nullable: false),
                    SolutionForDocumentId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Documents_Documents_SolutionForDocumentId",
                        column: x => x.SolutionForDocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_AcademicYearId_Type",
                table: "Documents",
                columns: new[] { "AcademicYearId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_FileHashSha256",
                table: "Documents",
                column: "FileHashSha256");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ModuleId_Type_Status",
                table: "Documents",
                columns: new[] { "ModuleId", "Type", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_SessionId",
                table: "Documents",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Slug",
                table: "Documents",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_SolutionForDocumentId",
                table: "Documents",
                column: "SolutionForDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Status_IsPremium_CreatedAt",
                table: "Documents",
                columns: new[] { "Status", "IsPremium", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Documents");
        }
    }
}
