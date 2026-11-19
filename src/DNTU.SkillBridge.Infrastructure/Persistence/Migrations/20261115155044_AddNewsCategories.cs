using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DNTU.SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "news_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_news_categories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_news_categories_Name",
                table: "news_categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_news_categories_SortOrder_Name",
                table: "news_categories",
                columns: new[] { "SortOrder", "Name" });

            migrationBuilder.InsertData(
                table: "news_categories",
                columns: new[] { "Id", "Name", "SortOrder", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { Guid.Parse("22222222-2222-4222-8222-000000000001"), "Hợp tác Doanh nghiệp", 10, new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero), null },
                    { Guid.Parse("22222222-2222-4222-8222-000000000002"), "Dự án Sinh viên", 20, new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero), null },
                    { Guid.Parse("22222222-2222-4222-8222-000000000003"), "Hội thảo Công nghệ", 30, new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero), null },
                    { Guid.Parse("22222222-2222-4222-8222-000000000004"), "Tuyển dụng & Thực tập", 40, new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero), null },
                    { Guid.Parse("22222222-2222-4222-8222-000000000005"), "Thông báo Khoa", 50, new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero), null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "news_categories");
        }
    }
}
