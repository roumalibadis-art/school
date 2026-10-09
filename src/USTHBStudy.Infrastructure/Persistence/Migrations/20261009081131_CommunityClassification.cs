using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace USTHBStudy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommunityClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ModuleId",
                table: "Documents",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)")
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "ClassificationReviewReason",
                table: "Documents",
                type: "varchar(40)",
                maxLength: 40,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "ClassificationStatus",
                table: "Documents",
                type: "int",
                nullable: false,
                // 1 = Classified. Every document that exists today already carries staff-set metadata, so it
                // must NOT fall into the community queue (0 = Unclassified).
                defaultValue: 1);

            migrationBuilder.AddColumn<long>(
                name: "ClassificationVersion",
                table: "Documents",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClassifiedAt",
                table: "Documents",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "Documents",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "SpecialtyId",
                table: "Documents",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<int>(
                name: "VerificationStatus",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                table: "Documents",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VerifiedById",
                table: "Documents",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<int>(
                name: "VotingRound",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // Existing documents were classified by staff when they were created.
            migrationBuilder.Sql("UPDATE `Documents` SET `ClassifiedAt` = `CreatedAt` WHERE `ClassifiedAt` IS NULL;");

            migrationBuilder.CreateTable(
                name: "ClassificationSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DocumentsPerTask = table.Column<int>(type: "int", nullable: false),
                    AssignmentExpiryHours = table.Column<int>(type: "int", nullable: false),
                    MinSecondsBeforeVote = table.Column<int>(type: "int", nullable: false),
                    RequiredVoters = table.Column<int>(type: "int", nullable: false),
                    AgreementPercent = table.Column<int>(type: "int", nullable: false),
                    RequiredFields = table.Column<int>(type: "int", nullable: false),
                    NonEducationalPercent = table.Column<int>(type: "int", nullable: false),
                    NonEducationalPolicy = table.Column<int>(type: "int", nullable: false),
                    LoginTriggerEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DownloadTriggerEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DownloadsPerPrompt = table.Column<int>(type: "int", nullable: false),
                    PromptSnoozeMinutes = table.Column<int>(type: "int", nullable: false),
                    QuotaEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FreeDownloadsPerWindow = table.Column<int>(type: "int", nullable: false),
                    QuotaWindowDays = table.Column<int>(type: "int", nullable: false),
                    BonusDownloadsPerContribution = table.Column<int>(type: "int", nullable: false),
                    MaxBonusPerWindow = table.Column<int>(type: "int", nullable: false),
                    MaxRewardedContributionsPerDay = table.Column<int>(type: "int", nullable: false),
                    MaxPendingProposalsPerUser = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassificationSettings", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ClassificationTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Trigger = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassificationTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassificationTasks_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ClassificationVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DocumentId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Round = table.Column<int>(type: "int", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    SpecialtyId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    DepartmentId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    DocumentType = table.Column<int>(type: "int", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    SessionId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    SpecialtyProposalId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    DepartmentProposalId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    DocumentTypeProposalId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    AcademicYearProposalId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    SessionProposalId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Rewarded = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AgreedWithOutcome = table.Column<bool>(type: "tinyint(1)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassificationVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassificationVotes_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassificationVotes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassificationVotes_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassificationVotes_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassificationVotes_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassificationVotes_Specialties_SpecialtyId",
                        column: x => x.SpecialtyId,
                        principalTable: "Specialties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ContributionStats",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TasksAssigned = table.Column<int>(type: "int", nullable: false),
                    TasksCompleted = table.Column<int>(type: "int", nullable: false),
                    DocumentsAssigned = table.Column<int>(type: "int", nullable: false),
                    ValidContributions = table.Column<int>(type: "int", nullable: false),
                    SkippedCount = table.Column<int>(type: "int", nullable: false),
                    ResolvedVotes = table.Column<int>(type: "int", nullable: false),
                    AgreedVotes = table.Column<int>(type: "int", nullable: false),
                    TotalDownloads = table.Column<long>(type: "bigint", nullable: false),
                    DownloadsAtLastPrompt = table.Column<long>(type: "bigint", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastPromptAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    SnoozedUntil = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    QuotaWindowStart = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    QuotaDownloadsUsed = table.Column<int>(type: "int", nullable: false),
                    QuotaBonusEarned = table.Column<int>(type: "int", nullable: false),
                    RewardDay = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RewardsToday = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContributionStats", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_ContributionStats_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ExternalLoginTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TokenHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalLoginTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalLoginTickets_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TaxonomyProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DedupeKey = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ParentId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    DocumentId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    SubmittedById = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SubmittedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ReviewedById = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ReviewedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AdminNote = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResolvedEntityId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ResolvedDocumentType = table.Column<int>(type: "int", nullable: true),
                    ApprovedName = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxonomyProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxonomyProposals_AspNetUsers_SubmittedById",
                        column: x => x.SubmittedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ClassificationAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TaskId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DocumentId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Round = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassificationAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassificationAssignments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassificationAssignments_ClassificationTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "ClassificationTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassificationAssignments_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ClassificationStatus_VerificationStatus_CreatedAt",
                table: "Documents",
                columns: new[] { "ClassificationStatus", "VerificationStatus", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DepartmentId",
                table: "Documents",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_SpecialtyId",
                table: "Documents",
                column: "SpecialtyId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationAssignments_DocumentId_Round_Status",
                table: "ClassificationAssignments",
                columns: new[] { "DocumentId", "Round", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationAssignments_DocumentId_UserId_Round",
                table: "ClassificationAssignments",
                columns: new[] { "DocumentId", "UserId", "Round" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationAssignments_TaskId",
                table: "ClassificationAssignments",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationAssignments_UserId_Status",
                table: "ClassificationAssignments",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationTasks_CreatedAt",
                table: "ClassificationTasks",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationTasks_UserId_Status",
                table: "ClassificationTasks",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationVotes_AcademicYearId",
                table: "ClassificationVotes",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationVotes_AcademicYearProposalId",
                table: "ClassificationVotes",
                column: "AcademicYearProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationVotes_DepartmentId",
                table: "ClassificationVotes",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationVotes_DepartmentProposalId",
                table: "ClassificationVotes",
                column: "DepartmentProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationVotes_DocumentId_Round",
                table: "ClassificationVotes",
                columns: new[] { "DocumentId", "Round" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationVotes_DocumentId_UserId_Round",
                table: "ClassificationVotes",
                columns: new[] { "DocumentId", "UserId", "Round" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationVotes_DocumentTypeProposalId",
                table: "ClassificationVotes",
                column: "DocumentTypeProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationVotes_SessionId",
                table: "ClassificationVotes",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationVotes_SessionProposalId",
                table: "ClassificationVotes",
                column: "SessionProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationVotes_SpecialtyId",
                table: "ClassificationVotes",
                column: "SpecialtyId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationVotes_SpecialtyProposalId",
                table: "ClassificationVotes",
                column: "SpecialtyProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationVotes_UserId",
                table: "ClassificationVotes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLoginTickets_ExpiresAt",
                table: "ExternalLoginTickets",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLoginTickets_TokenHash",
                table: "ExternalLoginTickets",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLoginTickets_UserId",
                table: "ExternalLoginTickets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxonomyProposals_DedupeKey",
                table: "TaxonomyProposals",
                column: "DedupeKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxonomyProposals_Status_Category_SubmittedAt",
                table: "TaxonomyProposals",
                columns: new[] { "Status", "Category", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxonomyProposals_SubmittedById_Status",
                table: "TaxonomyProposals",
                columns: new[] { "SubmittedById", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Departments_DepartmentId",
                table: "Documents",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Specialties_SpecialtyId",
                table: "Documents",
                column: "SpecialtyId",
                principalTable: "Specialties",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // NOTE: restoring the NOT NULL on Documents.ModuleId fails (by design — nothing is deleted) while any
            // unclassified document without a module still exists. Classify or remove those first.
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Departments_DepartmentId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Specialties_SpecialtyId",
                table: "Documents");

            migrationBuilder.DropTable(
                name: "ClassificationAssignments");

            migrationBuilder.DropTable(
                name: "ClassificationSettings");

            migrationBuilder.DropTable(
                name: "ClassificationVotes");

            migrationBuilder.DropTable(
                name: "ContributionStats");

            migrationBuilder.DropTable(
                name: "ExternalLoginTickets");

            migrationBuilder.DropTable(
                name: "TaxonomyProposals");

            migrationBuilder.DropTable(
                name: "ClassificationTasks");

            migrationBuilder.DropIndex(
                name: "IX_Documents_ClassificationStatus_VerificationStatus_CreatedAt",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_DepartmentId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_SpecialtyId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ClassificationReviewReason",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ClassificationStatus",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ClassificationVersion",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ClassifiedAt",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SpecialtyId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "VerificationStatus",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "VerifiedById",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "VotingRound",
                table: "Documents");

            migrationBuilder.AlterColumn<Guid>(
                name: "ModuleId",
                table: "Documents",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)",
                oldNullable: true)
                .OldAnnotation("Relational:Collation", "ascii_general_ci");
        }
    }
}
