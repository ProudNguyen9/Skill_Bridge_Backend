using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DNTU.SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyProfileExtras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Lưu ý: snapshot cũ thiếu student_profiles.AvatarUrl nên tool tự sinh
            // AddColumn thừa — DB đã có cột này, bỏ operation để tránh lỗi trùng cột.
            migrationBuilder.AddColumn<string>(
                name: "BannerUrl",
                table: "companies",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyType",
                table: "companies",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FoundedYear",
                table: "companies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tagline",
                table: "companies",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BannerUrl",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "CompanyType",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "FoundedYear",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "Tagline",
                table: "companies");
        }
    }
}
