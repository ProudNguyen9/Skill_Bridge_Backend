using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DNTU.SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteTechnicalAndCompanyBusinessReviewWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_BusinessSubmissionReviews",
                table: "BusinessSubmissionReviews");

            migrationBuilder.DropColumn(
                name: "Feedback",
                table: "BusinessSubmissionReviews");

            migrationBuilder.RenameTable(
                name: "BusinessSubmissionReviews",
                newName: "business_submission_reviews");

            migrationBuilder.AddColumn<string>(
                name: "CriteriaNotes",
                table: "technical_submission_reviews",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Decision",
                table: "business_submission_reviews",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "CollaborationFeedback",
                table: "business_submission_reviews",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RequirementsFeedback",
                table: "business_submission_reviews",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_business_submission_reviews",
                table: "business_submission_reviews",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_technical_submission_reviews_SubmissionId_SubmissionVersion~",
                table: "technical_submission_reviews",
                columns: new[] { "SubmissionId", "SubmissionVersionId", "Decision" });

            migrationBuilder.CreateIndex(
                name: "IX_business_submission_reviews_CompanyId_CreatedAt",
                table: "business_submission_reviews",
                columns: new[] { "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_business_submission_reviews_ReviewerUserId",
                table: "business_submission_reviews",
                column: "ReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_business_submission_reviews_SubmissionId_CreatedAt",
                table: "business_submission_reviews",
                columns: new[] { "SubmissionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_business_submission_reviews_SubmissionId_SubmissionVersionI~",
                table: "business_submission_reviews",
                columns: new[] { "SubmissionId", "SubmissionVersionId", "Decision" });

            migrationBuilder.CreateIndex(
                name: "IX_business_submission_reviews_SubmissionVersionId",
                table: "business_submission_reviews",
                column: "SubmissionVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_business_submission_reviews_companies_CompanyId",
                table: "business_submission_reviews",
                column: "CompanyId",
                principalTable: "companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_business_submission_reviews_project_submissions_SubmissionId",
                table: "business_submission_reviews",
                column: "SubmissionId",
                principalTable: "project_submissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_business_submission_reviews_submission_versions_SubmissionV~",
                table: "business_submission_reviews",
                column: "SubmissionVersionId",
                principalTable: "submission_versions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_business_submission_reviews_users_ReviewerUserId",
                table: "business_submission_reviews",
                column: "ReviewerUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_business_submission_reviews_companies_CompanyId",
                table: "business_submission_reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_business_submission_reviews_project_submissions_SubmissionId",
                table: "business_submission_reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_business_submission_reviews_submission_versions_SubmissionV~",
                table: "business_submission_reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_business_submission_reviews_users_ReviewerUserId",
                table: "business_submission_reviews");

            migrationBuilder.DropIndex(
                name: "IX_technical_submission_reviews_SubmissionId_SubmissionVersion~",
                table: "technical_submission_reviews");

            migrationBuilder.DropPrimaryKey(
                name: "PK_business_submission_reviews",
                table: "business_submission_reviews");

            migrationBuilder.DropIndex(
                name: "IX_business_submission_reviews_CompanyId_CreatedAt",
                table: "business_submission_reviews");

            migrationBuilder.DropIndex(
                name: "IX_business_submission_reviews_ReviewerUserId",
                table: "business_submission_reviews");

            migrationBuilder.DropIndex(
                name: "IX_business_submission_reviews_SubmissionId_CreatedAt",
                table: "business_submission_reviews");

            migrationBuilder.DropIndex(
                name: "IX_business_submission_reviews_SubmissionId_SubmissionVersionI~",
                table: "business_submission_reviews");

            migrationBuilder.DropIndex(
                name: "IX_business_submission_reviews_SubmissionVersionId",
                table: "business_submission_reviews");

            migrationBuilder.DropColumn(
                name: "CriteriaNotes",
                table: "technical_submission_reviews");

            migrationBuilder.DropColumn(
                name: "CollaborationFeedback",
                table: "business_submission_reviews");

            migrationBuilder.DropColumn(
                name: "RequirementsFeedback",
                table: "business_submission_reviews");

            migrationBuilder.RenameTable(
                name: "business_submission_reviews",
                newName: "BusinessSubmissionReviews");

            migrationBuilder.AlterColumn<int>(
                name: "Decision",
                table: "BusinessSubmissionReviews",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AddColumn<string>(
                name: "Feedback",
                table: "BusinessSubmissionReviews",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BusinessSubmissionReviews",
                table: "BusinessSubmissionReviews",
                column: "Id");
        }
    }
}
