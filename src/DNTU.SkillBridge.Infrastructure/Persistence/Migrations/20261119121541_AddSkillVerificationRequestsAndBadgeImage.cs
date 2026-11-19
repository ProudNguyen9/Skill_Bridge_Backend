using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DNTU.SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillVerificationRequestsAndBadgeImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BadgeImageUrl",
                table: "skills",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "skill_verification_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedLecturerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Level = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EvidenceUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EvidenceFileUrlsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    StudentNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReviewedByLecturerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LecturerFeedback = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skill_verification_requests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_skill_verification_requests_AssignedLecturerId_Status",
                table: "skill_verification_requests",
                columns: new[] { "AssignedLecturerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_skill_verification_requests_SkillId",
                table: "skill_verification_requests",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_skill_verification_requests_StudentId_Status",
                table: "skill_verification_requests",
                columns: new[] { "StudentId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "skill_verification_requests");

            migrationBuilder.DropColumn(
                name: "BadgeImageUrl",
                table: "skills");
        }
    }
}
