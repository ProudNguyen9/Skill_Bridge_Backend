using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DNTU.SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncAdminModelDrift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "student_profiles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "student_profiles");
        }
    }
}
