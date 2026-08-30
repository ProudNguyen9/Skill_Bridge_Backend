using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DNTU.SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectMemberStudentNavigation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StudentId1",
                table: "project_members",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_project_members_StudentId1",
                table: "project_members",
                column: "StudentId1");

            migrationBuilder.AddForeignKey(
                name: "FK_project_members_student_profiles_StudentId1",
                table: "project_members",
                column: "StudentId1",
                principalTable: "student_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_project_members_student_profiles_StudentId1",
                table: "project_members");

            migrationBuilder.DropIndex(
                name: "IX_project_members_StudentId1",
                table: "project_members");

            migrationBuilder.DropColumn(
                name: "StudentId1",
                table: "project_members");
        }
    }
}
