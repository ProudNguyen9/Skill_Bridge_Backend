using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DNTU.SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamAvatarUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "teams",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "lecturer_profiles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "lecturer_profiles");
        }
    }
}
