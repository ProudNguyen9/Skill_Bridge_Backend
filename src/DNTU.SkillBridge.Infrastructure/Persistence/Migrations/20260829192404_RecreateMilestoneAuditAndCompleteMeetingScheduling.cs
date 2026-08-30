using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DNTU.SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecreateMilestoneAuditAndCompleteMeetingScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "milestone_approval_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MilestoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_milestone_approval_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_milestone_approval_history_project_milestones_MilestoneId",
                        column: x => x.MilestoneId,
                        principalTable: "project_milestones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_milestone_approval_history_MilestoneId_CreatedAt",
                table: "milestone_approval_history",
                columns: new[] { "MilestoneId", "CreatedAt" });

            migrationBuilder.AddColumn<string>(
                name: "ExternalLinkType",
                table: "project_meetings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "NONE");

            migrationBuilder.AddColumn<string>(
                name: "ReminderState",
                table: "project_meetings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "SCHEDULED");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "milestone_approval_history");

            migrationBuilder.DropColumn(
                name: "ExternalLinkType",
                table: "project_meetings");

            migrationBuilder.DropColumn(
                name: "ReminderState",
                table: "project_meetings");
        }
    }
}
