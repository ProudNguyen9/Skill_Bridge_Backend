using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DNTU.SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddThreePartyCommitmentSignatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyCommitmentSignatureData",
                table: "projects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompanyCommitmentSignedAt",
                table: "projects",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyCommitmentSignedByUserId",
                table: "projects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyCommitmentSignerName",
                table: "projects",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyCommitmentSignerTitle",
                table: "projects",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UniversityCommitmentSignatureData",
                table: "projects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UniversityCommitmentSignedAt",
                table: "projects",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UniversityCommitmentSignedByUserId",
                table: "projects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UniversityCommitmentSignerName",
                table: "projects",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UniversityCommitmentSignerTitle",
                table: "projects",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentSignatureData",
                table: "project_commitments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentSignerName",
                table: "project_commitments",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyCommitmentSignatureData",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "CompanyCommitmentSignedAt",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "CompanyCommitmentSignedByUserId",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "CompanyCommitmentSignerName",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "CompanyCommitmentSignerTitle",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "UniversityCommitmentSignatureData",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "UniversityCommitmentSignedAt",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "UniversityCommitmentSignedByUserId",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "UniversityCommitmentSignerName",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "UniversityCommitmentSignerTitle",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "StudentSignatureData",
                table: "project_commitments");

            migrationBuilder.DropColumn(
                name: "StudentSignerName",
                table: "project_commitments");
        }
    }
}
