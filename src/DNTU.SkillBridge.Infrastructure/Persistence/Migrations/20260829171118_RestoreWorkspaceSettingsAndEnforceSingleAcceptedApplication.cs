using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DNTU.SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RestoreWorkspaceSettingsAndEnforceSingleAcceptedApplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workspace_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembersCanCreateTasks = table.Column<bool>(type: "boolean", nullable: false),
                    MembersCanScheduleMeetings = table.Column<bool>(type: "boolean", nullable: false),
                    WorkingAgreement = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workspace_settings_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_applications_student_accepted",
                table: "applications",
                column: "StudentId",
                unique: true,
                filter: "\"Status\" = 'ACCEPTED'");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_settings_ProjectId",
                table: "workspace_settings",
                column: "ProjectId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workspace_settings");

            migrationBuilder.DropIndex(
                name: "ux_applications_student_accepted",
                table: "applications");
        }
    }
}
