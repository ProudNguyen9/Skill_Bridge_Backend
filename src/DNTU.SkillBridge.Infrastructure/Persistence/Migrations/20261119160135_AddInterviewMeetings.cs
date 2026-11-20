using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DNTU.SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewMeetings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ApplicationId",
                table: "project_meetings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeetingType",
                table: "project_meetings",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "PROJECT");

            migrationBuilder.CreateIndex(
                name: "IX_project_meetings_ApplicationId",
                table: "project_meetings",
                column: "ApplicationId",
                unique: true,
                filter: "[ApplicationId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_project_meetings_applications_ApplicationId",
                table: "project_meetings",
                column: "ApplicationId",
                principalTable: "applications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_project_meetings_applications_ApplicationId",
                table: "project_meetings");

            migrationBuilder.DropIndex(
                name: "IX_project_meetings_ApplicationId",
                table: "project_meetings");

            migrationBuilder.DropColumn(
                name: "ApplicationId",
                table: "project_meetings");

            migrationBuilder.DropColumn(
                name: "MeetingType",
                table: "project_meetings");
        }
    }
}
