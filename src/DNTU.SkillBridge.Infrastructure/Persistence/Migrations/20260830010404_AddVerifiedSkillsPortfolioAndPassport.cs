using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DNTU.SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVerifiedSkillsPortfolioAndPassport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "portfolio_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_portfolio_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "verified_skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    VerifiedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_verified_skills", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_portfolio_entries_StudentId_IsPublished",
                table: "portfolio_entries",
                columns: new[] { "StudentId", "IsPublished" });

            migrationBuilder.CreateIndex(
                name: "IX_portfolio_entries_StudentId_ProjectId",
                table: "portfolio_entries",
                columns: new[] { "StudentId", "ProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_verified_skills_StudentId_IsRevoked",
                table: "verified_skills",
                columns: new[] { "StudentId", "IsRevoked" });

            migrationBuilder.CreateIndex(
                name: "IX_verified_skills_StudentId_SkillId_ProjectId",
                table: "verified_skills",
                columns: new[] { "StudentId", "SkillId", "ProjectId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "portfolio_entries");

            migrationBuilder.DropTable(
                name: "verified_skills");
        }
    }
}
