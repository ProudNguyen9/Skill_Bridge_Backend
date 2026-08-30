using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DNTU.SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "student_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    MajorId = table.Column<Guid>(type: "uuid", nullable: true),
                    FacultyId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYear = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Bio = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    GithubUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LinkedinUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PortfolioUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CvUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_student_profiles_faculties_FacultyId",
                        column: x => x.FacultyId,
                        principalTable: "faculties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_profiles_majors_MajorId",
                        column: x => x.MajorId,
                        principalTable: "majors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "student_certificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Issuer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CertificateUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_certificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_student_certificates_student_profiles_StudentId",
                        column: x => x.StudentId,
                        principalTable: "student_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_privacy_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsProfilePublic = table.Column<bool>(type: "boolean", nullable: false),
                    ShowContactInfo = table.Column<bool>(type: "boolean", nullable: false),
                    ShowDeclaredSkills = table.Column<bool>(type: "boolean", nullable: false),
                    ShowCertificates = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_privacy_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_student_privacy_settings_student_profiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "student_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsSelfDeclared = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_skills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_student_skills_skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_skills_student_profiles_StudentId",
                        column: x => x.StudentId,
                        principalTable: "student_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_student_certificates_StudentId_Name",
                table: "student_certificates",
                columns: new[] { "StudentId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_student_privacy_settings_StudentProfileId",
                table: "student_privacy_settings",
                column: "StudentProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_student_profiles_FacultyId",
                table: "student_profiles",
                column: "FacultyId");

            migrationBuilder.CreateIndex(
                name: "IX_student_profiles_MajorId",
                table: "student_profiles",
                column: "MajorId");

            migrationBuilder.CreateIndex(
                name: "IX_student_profiles_StudentCode",
                table: "student_profiles",
                column: "StudentCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_student_profiles_UserId",
                table: "student_profiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_student_skills_SkillId",
                table: "student_skills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_student_skills_StudentId",
                table: "student_skills",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_student_skills_StudentId_SkillId",
                table: "student_skills",
                columns: new[] { "StudentId", "SkillId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "student_certificates");

            migrationBuilder.DropTable(
                name: "student_privacy_settings");

            migrationBuilder.DropTable(
                name: "student_skills");

            migrationBuilder.DropTable(
                name: "student_profiles");
        }
    }
}
