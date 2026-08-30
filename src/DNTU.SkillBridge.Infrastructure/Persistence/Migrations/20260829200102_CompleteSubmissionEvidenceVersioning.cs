using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DNTU.SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteSubmissionEvidenceVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VideoUrl",
                table: "submission_versions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "project_submissions",
                type: "uuid",
                nullable: false,
                // Existing submissions predate the concurrency token. A non-empty sentinel keeps
                // them transitionable; every successful transition replaces it with a v7 token.
                defaultValue: new Guid("00000000-0000-4000-8000-000000000001"));

            migrationBuilder.CreateTable(
                name: "submission_status_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_status_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_submission_status_history_project_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "project_submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_submission_status_history_SubmissionId_CreatedAt",
                table: "submission_status_history",
                columns: new[] { "SubmissionId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "submission_status_history");

            migrationBuilder.DropColumn(
                name: "VideoUrl",
                table: "submission_versions");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "project_submissions");
        }
    }
}
