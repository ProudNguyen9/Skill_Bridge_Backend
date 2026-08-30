using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DNTU.SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationTeamSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                table: "applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_applications_TeamId",
                table: "applications",
                column: "TeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_applications_teams_TeamId",
                table: "applications",
                column: "TeamId",
                principalTable: "teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_applications_teams_TeamId",
                table: "applications");

            migrationBuilder.DropIndex(
                name: "IX_applications_TeamId",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "applications");
        }
    }
}
