using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DNTU.SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncAdminModelDrift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Some deployed databases already contain this column even though
            // the migration history does not. Reconcile both cases safely.
            migrationBuilder.Sql(
                "IF COL_LENGTH(N'dbo.student_profiles', N'AvatarUrl') IS NULL " +
                "ALTER TABLE [dbo].[student_profiles] ADD [AvatarUrl] nvarchar(500) NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This migration reconciles schema drift and cannot safely infer
            // whether AvatarUrl existed before it ran, so preserve the column.
        }
    }
}
