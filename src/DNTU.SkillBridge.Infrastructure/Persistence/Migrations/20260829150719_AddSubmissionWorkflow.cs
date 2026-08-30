using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DNTU.SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "meeting_action_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ResponsibleStudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    ProjectTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meeting_action_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_meeting_action_items_project_meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "project_meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_meeting_action_items_project_tasks_ProjectTaskId",
                        column: x => x.ProjectTaskId,
                        principalTable: "project_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "meeting_minutes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meeting_minutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_meeting_minutes_project_meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "project_meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_submissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    MilestoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmittedByStudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_submissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_submissions_project_milestones_MilestoneId",
                        column: x => x.MilestoneId,
                        principalTable: "project_milestones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_project_submissions_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    GithubUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DemoUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FileId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_submission_versions_file_records_FileId",
                        column: x => x.FileId,
                        principalTable: "file_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_submission_versions_project_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "project_submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_meeting_action_items_MeetingId_IsCompleted",
                table: "meeting_action_items",
                columns: new[] { "MeetingId", "IsCompleted" });

            migrationBuilder.CreateIndex(
                name: "IX_meeting_action_items_ProjectTaskId",
                table: "meeting_action_items",
                column: "ProjectTaskId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_meeting_minutes_MeetingId",
                table: "meeting_minutes",
                column: "MeetingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_submissions_MilestoneId_Status",
                table: "project_submissions",
                columns: new[] { "MilestoneId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_project_submissions_ProjectId_Status",
                table: "project_submissions",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_submission_versions_FileId",
                table: "submission_versions",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_submission_versions_SubmissionId_VersionNumber",
                table: "submission_versions",
                columns: new[] { "SubmissionId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meeting_action_items");

            migrationBuilder.DropTable(
                name: "meeting_minutes");

            migrationBuilder.DropTable(
                name: "submission_versions");

            migrationBuilder.DropTable(
                name: "project_submissions");
        }
    }
}
