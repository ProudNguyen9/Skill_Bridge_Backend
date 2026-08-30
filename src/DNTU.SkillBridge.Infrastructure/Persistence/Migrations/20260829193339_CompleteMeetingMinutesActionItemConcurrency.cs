using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DNTU.SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteMeetingMinutesActionItemConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_meeting_action_items_project_tasks_ProjectTaskId",
                table: "meeting_action_items");

            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "meeting_action_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Existing rows need a non-empty optimistic-concurrency token before API mutations.
            migrationBuilder.Sql("UPDATE meeting_action_items SET \"Version\" = \"Id\" WHERE \"Version\" = '00000000-0000-0000-0000-000000000000'");

            migrationBuilder.CreateTable(
                name: "meeting_minute_revisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingMinuteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    EditedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meeting_minute_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_meeting_minute_revisions_meeting_minutes_MeetingMinuteId",
                        column: x => x.MeetingMinuteId,
                        principalTable: "meeting_minutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_meeting_minute_revisions_MeetingMinuteId_CreatedAt",
                table: "meeting_minute_revisions",
                columns: new[] { "MeetingMinuteId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_meeting_action_items_project_tasks_ProjectTaskId",
                table: "meeting_action_items",
                column: "ProjectTaskId",
                principalTable: "project_tasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_meeting_action_items_project_tasks_ProjectTaskId",
                table: "meeting_action_items");

            migrationBuilder.DropTable(
                name: "meeting_minute_revisions");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "meeting_action_items");

            migrationBuilder.AddForeignKey(
                name: "FK_meeting_action_items_project_tasks_ProjectTaskId",
                table: "meeting_action_items",
                column: "ProjectTaskId",
                principalTable: "project_tasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
