using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DNTU.SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRealApplicationTeamLecturerFlows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_lecturer_assignments_ProjectId",
                table: "lecturer_assignments");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "teams",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetProjectId",
                table: "teams",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeamType",
                table: "teams",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "PROJECT");

            migrationBuilder.AddColumn<string>(
                name: "FunctionalRole",
                table: "team_members",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "JoinedAt",
                table: "team_members",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "FunctionalRole",
                table: "team_invitations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "team_invitations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AllowanceShareAmount",
                table: "project_commitments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApplicationId",
                table: "project_commitments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Conditions",
                table: "project_commitments",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeclineReason",
                table: "project_commitments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeclinedAt",
                table: "project_commitments",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposedRole",
                table: "project_commitments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AllowanceShareAmount",
                table: "lecturer_assignments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CourseName",
                table: "lecturer_assignments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeclineReason",
                table: "lecturer_assignments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpectedDuration",
                table: "lecturer_assignments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpectedWork",
                table: "lecturer_assignments",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InvitedByUserId",
                table: "lecturer_assignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCourseLinked",
                table: "lecturer_assignments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RespondedAt",
                table: "lecturer_assignments",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                table: "lecturer_assignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.DropIndex(
                name: "ux_applications_student_accepted",
                table: "applications");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "applications",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.CreateIndex(
                name: "ux_applications_student_accepted",
                table: "applications",
                column: "StudentId",
                unique: true,
                filter: "[Status] = 'ACCEPTED'");

            migrationBuilder.AddColumn<string>(
                name: "AdjustmentProposalJson",
                table: "applications",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicationType",
                table: "applications",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "INDIVIDUAL");

            migrationBuilder.Sql("UPDATE [applications] SET [ApplicationType] = 'TEAM' WHERE [TeamId] IS NOT NULL;");

            migrationBuilder.AddColumn<string>(
                name: "Availability",
                table: "applications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyOfferConditions",
                table: "applications",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConfirmationDeadlineDays",
                table: "applications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConflictNote",
                table: "applications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CvFileName",
                table: "applications",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CvUrl",
                table: "applications",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GithubUrl",
                table: "applications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InterviewScheduleNote",
                table: "applications",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPreferredInConflict",
                table: "applications",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PortfolioUrl",
                table: "applications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PriorityOrder",
                table: "applications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecommendedByLecturerId",
                table: "applications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WithdrawnByUserId",
                table: "applications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "application_members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProposedRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConsentStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsSelectedByCompany = table.Column<bool>(type: "bit", nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_application_members_applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_application_members_student_profiles_StudentId",
                        column: x => x.StudentId,
                        principalTable: "student_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "team_project_recommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LecturerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RespondedByStudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RespondedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResponseNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_project_recommendations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_team_project_recommendations_lecturer_profiles_LecturerId",
                        column: x => x.LecturerId,
                        principalTable: "lecturer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_team_project_recommendations_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_team_project_recommendations_teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lecturer_assignments_ProjectId_ActiveOrInvited",
                table: "lecturer_assignments",
                column: "ProjectId",
                unique: true,
                filter: "[Status] IN ('INVITED', 'ACTIVE')");

            migrationBuilder.CreateIndex(
                name: "IX_lecturer_assignments_TeamId",
                table: "lecturer_assignments",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_application_members_ApplicationId_StudentId",
                table: "application_members",
                columns: new[] { "ApplicationId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_application_members_StudentId_ConsentStatus",
                table: "application_members",
                columns: new[] { "StudentId", "ConsentStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_team_project_recommendations_LecturerId_Status",
                table: "team_project_recommendations",
                columns: new[] { "LecturerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_team_project_recommendations_ProjectId",
                table: "team_project_recommendations",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_team_project_recommendations_TeamId_ProjectId",
                table: "team_project_recommendations",
                columns: new[] { "TeamId", "ProjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_members");

            migrationBuilder.DropTable(
                name: "team_project_recommendations");

            migrationBuilder.DropIndex(
                name: "IX_lecturer_assignments_ProjectId_ActiveOrInvited",
                table: "lecturer_assignments");

            migrationBuilder.DropIndex(
                name: "IX_lecturer_assignments_TeamId",
                table: "lecturer_assignments");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "TargetProjectId",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "TeamType",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "FunctionalRole",
                table: "team_members");

            migrationBuilder.DropColumn(
                name: "JoinedAt",
                table: "team_members");

            migrationBuilder.DropColumn(
                name: "FunctionalRole",
                table: "team_invitations");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "team_invitations");

            migrationBuilder.DropColumn(
                name: "AllowanceShareAmount",
                table: "project_commitments");

            migrationBuilder.DropColumn(
                name: "ApplicationId",
                table: "project_commitments");

            migrationBuilder.DropColumn(
                name: "Conditions",
                table: "project_commitments");

            migrationBuilder.DropColumn(
                name: "DeclineReason",
                table: "project_commitments");

            migrationBuilder.DropColumn(
                name: "DeclinedAt",
                table: "project_commitments");

            migrationBuilder.DropColumn(
                name: "ProposedRole",
                table: "project_commitments");

            migrationBuilder.DropColumn(
                name: "AllowanceShareAmount",
                table: "lecturer_assignments");

            migrationBuilder.DropColumn(
                name: "CourseName",
                table: "lecturer_assignments");

            migrationBuilder.DropColumn(
                name: "DeclineReason",
                table: "lecturer_assignments");

            migrationBuilder.DropColumn(
                name: "ExpectedDuration",
                table: "lecturer_assignments");

            migrationBuilder.DropColumn(
                name: "ExpectedWork",
                table: "lecturer_assignments");

            migrationBuilder.DropColumn(
                name: "InvitedByUserId",
                table: "lecturer_assignments");

            migrationBuilder.DropColumn(
                name: "IsCourseLinked",
                table: "lecturer_assignments");

            migrationBuilder.DropColumn(
                name: "RespondedAt",
                table: "lecturer_assignments");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "lecturer_assignments");

            migrationBuilder.DropColumn(
                name: "AdjustmentProposalJson",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "ApplicationType",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "Availability",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "CompanyOfferConditions",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "ConfirmationDeadlineDays",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "ConflictNote",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "CvFileName",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "CvUrl",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "GithubUrl",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "InterviewScheduleNote",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "IsPreferredInConflict",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "PortfolioUrl",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "PriorityOrder",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "RecommendedByLecturerId",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "WithdrawnByUserId",
                table: "applications");

            migrationBuilder.DropIndex(
                name: "ux_applications_student_accepted",
                table: "applications");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "applications",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.CreateIndex(
                name: "ux_applications_student_accepted",
                table: "applications",
                column: "StudentId",
                unique: true,
                filter: "[Status] = 'ACCEPTED'");

            migrationBuilder.CreateIndex(
                name: "IX_lecturer_assignments_ProjectId",
                table: "lecturer_assignments",
                column: "ProjectId",
                unique: true);
        }
    }
}
