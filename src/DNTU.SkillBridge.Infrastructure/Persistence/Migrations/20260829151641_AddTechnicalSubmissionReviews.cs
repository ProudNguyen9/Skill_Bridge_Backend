using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DNTU.SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTechnicalSubmissionReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_TechnicalSubmissionReviews",
                table: "TechnicalSubmissionReviews");

            migrationBuilder.RenameTable(
                name: "TechnicalSubmissionReviews",
                newName: "technical_submission_reviews");

            migrationBuilder.AlterColumn<string>(
                name: "Feedback",
                table: "technical_submission_reviews",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Decision",
                table: "technical_submission_reviews",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddPrimaryKey(
                name: "PK_technical_submission_reviews",
                table: "technical_submission_reviews",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_technical_submission_reviews_ReviewerUserId_CreatedAt",
                table: "technical_submission_reviews",
                columns: new[] { "ReviewerUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_technical_submission_reviews_SubmissionId_CreatedAt",
                table: "technical_submission_reviews",
                columns: new[] { "SubmissionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_technical_submission_reviews_SubmissionVersionId",
                table: "technical_submission_reviews",
                column: "SubmissionVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_technical_submission_reviews_project_submissions_Submission~",
                table: "technical_submission_reviews",
                column: "SubmissionId",
                principalTable: "project_submissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_technical_submission_reviews_submission_versions_Submission~",
                table: "technical_submission_reviews",
                column: "SubmissionVersionId",
                principalTable: "submission_versions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_technical_submission_reviews_project_submissions_Submission~",
                table: "technical_submission_reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_technical_submission_reviews_submission_versions_Submission~",
                table: "technical_submission_reviews");

            migrationBuilder.DropPrimaryKey(
                name: "PK_technical_submission_reviews",
                table: "technical_submission_reviews");

            migrationBuilder.DropIndex(
                name: "IX_technical_submission_reviews_ReviewerUserId_CreatedAt",
                table: "technical_submission_reviews");

            migrationBuilder.DropIndex(
                name: "IX_technical_submission_reviews_SubmissionId_CreatedAt",
                table: "technical_submission_reviews");

            migrationBuilder.DropIndex(
                name: "IX_technical_submission_reviews_SubmissionVersionId",
                table: "technical_submission_reviews");

            migrationBuilder.RenameTable(
                name: "technical_submission_reviews",
                newName: "TechnicalSubmissionReviews");

            migrationBuilder.AlterColumn<string>(
                name: "Feedback",
                table: "TechnicalSubmissionReviews",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<int>(
                name: "Decision",
                table: "TechnicalSubmissionReviews",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AddPrimaryKey(
                name: "PK_TechnicalSubmissionReviews",
                table: "TechnicalSubmissionReviews",
                column: "Id");
        }
    }
}
