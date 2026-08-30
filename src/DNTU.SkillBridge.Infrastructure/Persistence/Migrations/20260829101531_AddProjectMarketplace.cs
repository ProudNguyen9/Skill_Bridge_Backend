using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DNTU.SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectMarketplace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProblemStatement = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    BusinessRequirements = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    TechnicalConstraints = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IndustryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Difficulty = table.Column<int>(type: "integer", nullable: false),
                    WorkType = table.Column<int>(type: "integer", nullable: false),
                    DurationWeeks = table.Column<int>(type: "integer", nullable: false),
                    ApplicationDeadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpectedStudentCount = table.Column<int>(type: "integer", nullable: false),
                    MinTeamSize = table.Column<int>(type: "integer", nullable: false),
                    MaxTeamSize = table.Column<int>(type: "integer", nullable: false),
                    AllowanceAmount = table.Column<long>(type: "bigint", nullable: true),
                    AllowanceCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_projects_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_projects_industries_IndustryId",
                        column: x => x.IndustryId,
                        principalTable: "industries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_deliverables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_deliverables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_deliverables_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_skills",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_skills", x => new { x.ProjectId, x.SkillId });
                    table.ForeignKey(
                        name: "FK_project_skills_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_skills_skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_deliverables_ProjectId_SortOrder",
                table: "project_deliverables",
                columns: new[] { "ProjectId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_project_skills_SkillId",
                table: "project_skills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_projects_ApplicationDeadline",
                table: "projects",
                column: "ApplicationDeadline");

            migrationBuilder.CreateIndex(
                name: "IX_projects_Code",
                table: "projects",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_projects_CompanyId_Status",
                table: "projects",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_projects_CreatedAt",
                table: "projects",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_projects_IndustryId",
                table: "projects",
                column: "IndustryId");

            migrationBuilder.CreateIndex(
                name: "IX_projects_Slug",
                table: "projects",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_projects_Status_IsActive",
                table: "projects",
                columns: new[] { "Status", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_deliverables");

            migrationBuilder.DropTable(
                name: "project_skills");

            migrationBuilder.DropTable(
                name: "projects");
        }
    }
}
