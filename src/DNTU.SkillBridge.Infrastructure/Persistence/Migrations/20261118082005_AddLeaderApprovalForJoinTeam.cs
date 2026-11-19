using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DNTU.SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaderApprovalForJoinTeam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JoinProposedRole",
                table: "applications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "JoinRequestedAt",
                table: "applications",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "JoinRequestedByUserId",
                table: "applications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "JoinTargetTeamId",
                table: "applications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_applications_JoinTargetTeamId_Status",
                table: "applications",
                columns: new[] { "JoinTargetTeamId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_applications_JoinTargetTeamId_Status",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "JoinProposedRole",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "JoinRequestedAt",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "JoinRequestedByUserId",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "JoinTargetTeamId",
                table: "applications");
        }
    }
}
