using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DNTU.SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SqlServerInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "academic_evaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RubricId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluatorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TotalScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IsPassed = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    FinalizedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academic_evaluations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "admin_policy_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SettingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_policy_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "allowance_allocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MilestoneAllowanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_allowance_allocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IpHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "banks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Bin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_banks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_courses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "disbursements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    BankReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PaidAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_disbursements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "faculties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_faculties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "funding_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ProviderTransactionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_funding_orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ResponseJson = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "industries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_industries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "milestone_allowances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MilestoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_milestone_allowances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    AvailableAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payment_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FundingOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ProviderTransactionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_transactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "portfolio_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_portfolio_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "project_fundings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequiredAmount = table.Column<long>(type: "bigint", nullable: false),
                    FundedAmount = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_fundings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reconciliation_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    CheckedCount = table.Column<int>(type: "int", nullable: false),
                    RepairedCount = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportExports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Format = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FilterJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportExports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "rubrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LecturerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rubrics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sepay_ipn_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderEventId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    InvoiceCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProviderTransactionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Applied = table.Column<bool>(type: "bit", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sepay_ipn_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "student_payout_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AccountHolderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AccountNumberCiphertext = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AccountNumberLast4 = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_payout_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "teams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedByStudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    EmailVerified = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "verified_skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VerifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Level = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EvidenceSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EvidenceEvaluationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_verified_skills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "academic_criterion_scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RubricCriterionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academic_criterion_scores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_academic_criterion_scores_academic_evaluations_EvaluationId",
                        column: x => x.EvaluationId,
                        principalTable: "academic_evaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "majors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FacultyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_majors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_majors_faculties_FacultyId",
                        column: x => x.FacultyId,
                        principalTable: "faculties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: false),
                    TaxCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IndustryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Website = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LogoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    VerificationStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VerificationNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_companies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_companies_industries_IndustryId",
                        column: x => x.IndustryId,
                        principalTable: "industries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_role_permissions_permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "course_projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RubricId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_course_projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_course_projects_courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_course_projects_rubrics_RubricId",
                        column: x => x.RubricId,
                        principalTable: "rubrics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rubric_criteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RubricId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rubric_criteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rubric_criteria_rubrics_RubricId",
                        column: x => x.RubricId,
                        principalTable: "rubrics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "account_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_account_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "company_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DocumentUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_company_documents_users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "company_invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_company_invitations_users_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lecturer_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LecturerCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Department = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AcademicTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Bio = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    WebsiteUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OfficeLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsProfilePublic = table.Column<bool>(type: "bit", nullable: false),
                    ShowContactInfo = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lecturer_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lecturer_profiles_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_preferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmailEnabled = table.Column<bool>(type: "bit", nullable: false),
                    RealtimeEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_preferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notification_preferences_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReadAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notifications_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_user_roles_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_roles_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_sessions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    MajorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FacultyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AcademicYear = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Bio = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    GithubUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LinkedinUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PortfolioUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CvUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
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
                name: "company_members",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_members", x => new { x.CompanyId, x.UserId });
                    table.ForeignKey(
                        name: "FK_company_members_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_company_members_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NormalizedTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProblemStatement = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    BusinessRequirements = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TechnicalConstraints = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IndustryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Difficulty = table.Column<int>(type: "int", nullable: false),
                    WorkType = table.Column<int>(type: "int", nullable: false),
                    DurationWeeks = table.Column<int>(type: "int", nullable: false),
                    ApplicationDeadline = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExpectedStudentCount = table.Column<int>(type: "int", nullable: false),
                    MinTeamSize = table.Column<int>(type: "int", nullable: false),
                    MaxTeamSize = table.Column<int>(type: "int", nullable: false),
                    AllowanceAmount = table.Column<long>(type: "bigint", nullable: true),
                    AllowanceCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
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
                name: "lecturer_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LecturerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lecturer_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lecturer_assignments_lecturer_profiles_LecturerId",
                        column: x => x.LecturerId,
                        principalTable: "lecturer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReplacedByTokenId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_user_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "user_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_certificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Issuer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CertificateUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsProfilePublic = table.Column<bool>(type: "bit", nullable: false),
                    ShowContactInfo = table.Column<bool>(type: "bit", nullable: false),
                    ShowDeclaredSkills = table.Column<bool>(type: "bit", nullable: false),
                    ShowCertificates = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Level = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsSelfDeclared = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "team_invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvitedStudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvitedByStudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_team_invitations_student_profiles_InvitedStudentId",
                        column: x => x.InvitedStudentId,
                        principalTable: "student_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_team_invitations_teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "team_members",
                columns: table => new
                {
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_members", x => new { x.TeamId, x.StudentId });
                    table.ForeignKey(
                        name: "FK_team_members_student_profiles_StudentId",
                        column: x => x.StudentId,
                        principalTable: "student_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_team_members_teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CoverLetter = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    WithdrawnAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    WithdrawReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_applications_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_applications_student_profiles_StudentId",
                        column: x => x.StudentId,
                        principalTable: "student_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_applications_teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "file_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpectedSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ChecksumSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_file_records_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "project_activities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_activities_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_approvals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_approvals_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_approvals_users_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_commitments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PolicyVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ConfirmationDeadline = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_commitments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_commitments_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_commitments_student_profiles_StudentId",
                        column: x => x.StudentId,
                        principalTable: "student_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_completions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_completions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_completions_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_deliverables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
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
                name: "project_meetings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    StartAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExternalUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ExternalLinkType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ReminderState = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_meetings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_meetings_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_members_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_members_student_profiles_StudentId",
                        column: x => x.StudentId,
                        principalTable: "student_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_milestones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_milestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_milestones_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_risk_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Level = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ReasonsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CalculatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_risk_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_risk_history_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_risk_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Level = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ReasonsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CalculatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_risk_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_risk_snapshots_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_skills",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequirementLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "project_tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AssigneeStudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_tasks_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "saved_projects",
                columns: table => new
                {
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_projects", x => new { x.StudentId, x.ProjectId });
                    table.ForeignKey(
                        name: "FK_saved_projects_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_saved_projects_student_profiles_StudentId",
                        column: x => x.StudentId,
                        principalTable: "student_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "withdrawal_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RecommendedByLecturerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecommendedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LecturerNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_withdrawal_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_withdrawal_requests_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_withdrawal_requests_student_profiles_StudentId",
                        column: x => x.StudentId,
                        principalTable: "student_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workspace_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MembersCanCreateTasks = table.Column<bool>(type: "bit", nullable: false),
                    MembersCanScheduleMeetings = table.Column<bool>(type: "bit", nullable: false),
                    WorkingAgreement = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workspace_settings_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meeting_minutes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meeting_minutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_meeting_minutes_project_meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "project_meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meeting_participants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttendanceStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meeting_participants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_meeting_participants_project_meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "project_meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_meeting_participants_student_profiles_StudentId",
                        column: x => x.StudentId,
                        principalTable: "student_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "milestone_approval_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MilestoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ToStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "milestone_deliverables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MilestoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Criteria = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_milestone_deliverables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_milestone_deliverables_file_records_FileId",
                        column: x => x.FileId,
                        principalTable: "file_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_milestone_deliverables_project_milestones_MilestoneId",
                        column: x => x.MilestoneId,
                        principalTable: "project_milestones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_submissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MilestoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedByStudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_submissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_submissions_project_milestones_MilestoneId",
                        column: x => x.MilestoneId,
                        principalTable: "project_milestones",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_project_submissions_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meeting_action_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ResponsibleStudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    ProjectTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meeting_action_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_meeting_action_items_project_meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "project_meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_meeting_action_items_project_tasks_ProjectTaskId",
                        column: x => x.ProjectTaskId,
                        principalTable: "project_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_checklist_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_checklist_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_checklist_items_project_tasks_ProjectTaskId",
                        column: x => x.ProjectTaskId,
                        principalTable: "project_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_comments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_comments_project_tasks_ProjectTaskId",
                        column: x => x.ProjectTaskId,
                        principalTable: "project_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meeting_minute_revisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingMinuteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    EditedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "submission_status_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ToStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_status_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_submission_status_history_project_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "project_submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    GithubUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DemoUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    VideoUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_submission_versions_file_records_FileId",
                        column: x => x.FileId,
                        principalTable: "file_records",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_submission_versions_project_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "project_submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "business_submission_reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequirementsFeedback = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CollaborationFeedback = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_submission_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_business_submission_reviews_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_business_submission_reviews_project_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "project_submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_business_submission_reviews_submission_versions_SubmissionVersionId",
                        column: x => x.SubmissionVersionId,
                        principalTable: "submission_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_business_submission_reviews_users_ReviewerUserId",
                        column: x => x.ReviewerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "technical_submission_reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Feedback = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CriteriaNotes = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_technical_submission_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_technical_submission_reviews_project_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "project_submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_technical_submission_reviews_submission_versions_SubmissionVersionId",
                        column: x => x.SubmissionVersionId,
                        principalTable: "submission_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_academic_criterion_scores_EvaluationId_RubricCriterionId",
                table: "academic_criterion_scores",
                columns: new[] { "EvaluationId", "RubricCriterionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_academic_evaluations_EvaluatorUserId_Status",
                table: "academic_evaluations",
                columns: new[] { "EvaluatorUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_academic_evaluations_ProjectId_StudentId_RubricId",
                table: "academic_evaluations",
                columns: new[] { "ProjectId", "StudentId", "RubricId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_tokens_TokenHash",
                table: "account_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_tokens_UserId_Purpose_ExpiresAt",
                table: "account_tokens",
                columns: new[] { "UserId", "Purpose", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_admin_policy_settings_Category",
                table: "admin_policy_settings",
                column: "Category",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_allowance_allocations_MilestoneAllowanceId_StudentId",
                table: "allowance_allocations",
                columns: new[] { "MilestoneAllowanceId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_applications_ProjectId_Status",
                table: "applications",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_applications_ProjectId_StudentId",
                table: "applications",
                columns: new[] { "ProjectId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_applications_StudentId_Status",
                table: "applications",
                columns: new[] { "StudentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_applications_TeamId",
                table: "applications",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "ux_applications_student_accepted",
                table: "applications",
                column: "StudentId",
                unique: true,
                filter: "[Status] = 'ACCEPTED'");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_Action_CreatedAt",
                table: "audit_logs",
                columns: new[] { "Action", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_ActorUserId_CreatedAt",
                table: "audit_logs",
                columns: new[] { "ActorUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_EntityType_EntityId",
                table: "audit_logs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_ProjectId",
                table: "audit_logs",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_banks_Bin",
                table: "banks",
                column: "Bin",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_banks_Code",
                table: "banks",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_banks_IsActive_Name",
                table: "banks",
                columns: new[] { "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_banks_NormalizedName",
                table: "banks",
                column: "NormalizedName",
                unique: true);

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
                name: "IX_business_submission_reviews_SubmissionId_SubmissionVersionId_Decision",
                table: "business_submission_reviews",
                columns: new[] { "SubmissionId", "SubmissionVersionId", "Decision" });

            migrationBuilder.CreateIndex(
                name: "IX_business_submission_reviews_SubmissionVersionId",
                table: "business_submission_reviews",
                column: "SubmissionVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_companies_IndustryId",
                table: "companies",
                column: "IndustryId");

            migrationBuilder.CreateIndex(
                name: "IX_companies_NormalizedName",
                table: "companies",
                column: "NormalizedName");

            migrationBuilder.CreateIndex(
                name: "IX_companies_Slug",
                table: "companies",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_companies_TaxCode",
                table: "companies",
                column: "TaxCode",
                unique: true,
                filter: "[TaxCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_companies_VerificationStatus_IsActive",
                table: "companies",
                columns: new[] { "VerificationStatus", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_company_documents_CompanyId",
                table: "company_documents",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_company_documents_UploadedByUserId",
                table: "company_documents",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_company_invitations_CompanyId_NormalizedEmail_Status",
                table: "company_invitations",
                columns: new[] { "CompanyId", "NormalizedEmail", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_company_invitations_InvitedByUserId",
                table: "company_invitations",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_company_members_UserId",
                table: "company_members",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_course_projects_CourseId_ProjectId_Status",
                table: "course_projects",
                columns: new[] { "CourseId", "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_course_projects_ProjectId_Status",
                table: "course_projects",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_course_projects_RubricId",
                table: "course_projects",
                column: "RubricId");

            migrationBuilder.CreateIndex(
                name: "IX_courses_Code",
                table: "courses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_disbursements_IdempotencyKey",
                table: "disbursements",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_disbursements_ProjectId_StudentId",
                table: "disbursements",
                columns: new[] { "ProjectId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_faculties_Code",
                table: "faculties",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_faculties_IsActive_Name",
                table: "faculties",
                columns: new[] { "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_faculties_NormalizedName",
                table: "faculties",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_file_records_ProjectId_Status",
                table: "file_records",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_file_records_StorageKey",
                table: "file_records",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_file_records_UploadedByUserId_Status_ExpiresAt",
                table: "file_records",
                columns: new[] { "UploadedByUserId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_funding_orders_InvoiceCode",
                table: "funding_orders",
                column: "InvoiceCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_funding_orders_ProjectId_Status",
                table: "funding_orders",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_funding_orders_Provider_ProviderTransactionId",
                table: "funding_orders",
                columns: new[] { "Provider", "ProviderTransactionId" },
                unique: true,
                filter: "[Provider] IS NOT NULL AND [ProviderTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_ExpiresAt",
                table: "idempotency_records",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_UserId_Key_Endpoint",
                table: "idempotency_records",
                columns: new[] { "UserId", "Key", "Endpoint" },
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_industries_IsActive_Name",
                table: "industries",
                columns: new[] { "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_industries_NormalizedName",
                table: "industries",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_industries_Slug",
                table: "industries",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lecturer_assignments_LecturerId",
                table: "lecturer_assignments",
                column: "LecturerId");

            migrationBuilder.CreateIndex(
                name: "IX_lecturer_assignments_ProjectId",
                table: "lecturer_assignments",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lecturer_profiles_LecturerCode",
                table: "lecturer_profiles",
                column: "LecturerCode",
                unique: true,
                filter: "[LecturerCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_lecturer_profiles_UserId",
                table: "lecturer_profiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_majors_Code",
                table: "majors",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_majors_FacultyId_NormalizedName",
                table: "majors",
                columns: new[] { "FacultyId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_majors_IsActive_Name",
                table: "majors",
                columns: new[] { "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_meeting_action_items_MeetingId_IsCompleted",
                table: "meeting_action_items",
                columns: new[] { "MeetingId", "IsCompleted" });

            migrationBuilder.CreateIndex(
                name: "IX_meeting_action_items_ProjectTaskId",
                table: "meeting_action_items",
                column: "ProjectTaskId",
                unique: true,
                filter: "[ProjectTaskId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_meeting_minute_revisions_MeetingMinuteId_CreatedAt",
                table: "meeting_minute_revisions",
                columns: new[] { "MeetingMinuteId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_meeting_minutes_MeetingId",
                table: "meeting_minutes",
                column: "MeetingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_meeting_participants_MeetingId_StudentId",
                table: "meeting_participants",
                columns: new[] { "MeetingId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_meeting_participants_StudentId",
                table: "meeting_participants",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_milestone_allowances_MilestoneId",
                table: "milestone_allowances",
                column: "MilestoneId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_milestone_approval_history_MilestoneId_CreatedAt",
                table: "milestone_approval_history",
                columns: new[] { "MilestoneId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_milestone_deliverables_FileId",
                table: "milestone_deliverables",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_milestone_deliverables_MilestoneId",
                table: "milestone_deliverables",
                column: "MilestoneId");

            migrationBuilder.CreateIndex(
                name: "IX_notification_preferences_UserId",
                table: "notification_preferences",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_UserId_ReadAt_CreatedAt",
                table: "notifications",
                columns: new[] { "UserId", "ReadAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_ProcessedAt_AvailableAt",
                table: "outbox_messages",
                columns: new[] { "ProcessedAt", "AvailableAt" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_FundingOrderId",
                table: "payment_transactions",
                column: "FundingOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_Provider_ProviderTransactionId",
                table: "payment_transactions",
                columns: new[] { "Provider", "ProviderTransactionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_permissions_Name",
                table: "permissions",
                column: "Name",
                unique: true);

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
                name: "IX_project_activities_ProjectId_CreatedAt",
                table: "project_activities",
                columns: new[] { "ProjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_project_activities_ProjectId_EventType_CreatedAt",
                table: "project_activities",
                columns: new[] { "ProjectId", "EventType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_project_approvals_DecidedByUserId",
                table: "project_approvals",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_project_approvals_ProjectId_DecidedAt",
                table: "project_approvals",
                columns: new[] { "ProjectId", "DecidedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_project_commitments_ProjectId_Status",
                table: "project_commitments",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_project_commitments_ProjectId_StudentId",
                table: "project_commitments",
                columns: new[] { "ProjectId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_commitments_StudentId_Status",
                table: "project_commitments",
                columns: new[] { "StudentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_project_completions_ProjectId",
                table: "project_completions",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_deliverables_ProjectId_SortOrder",
                table: "project_deliverables",
                columns: new[] { "ProjectId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_project_fundings_ProjectId",
                table: "project_fundings",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_meetings_ProjectId_StartAt",
                table: "project_meetings",
                columns: new[] { "ProjectId", "StartAt" });

            migrationBuilder.CreateIndex(
                name: "IX_project_members_ProjectId_StudentId",
                table: "project_members",
                columns: new[] { "ProjectId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_members_StudentId_IsActive",
                table: "project_members",
                columns: new[] { "StudentId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_project_milestones_ProjectId_Sequence",
                table: "project_milestones",
                columns: new[] { "ProjectId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_milestones_ProjectId_Status_DueAt",
                table: "project_milestones",
                columns: new[] { "ProjectId", "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_project_risk_history_ProjectId_CalculatedAt",
                table: "project_risk_history",
                columns: new[] { "ProjectId", "CalculatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_project_risk_snapshots_Level_CalculatedAt",
                table: "project_risk_snapshots",
                columns: new[] { "Level", "CalculatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_project_risk_snapshots_ProjectId",
                table: "project_risk_snapshots",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_skills_SkillId",
                table: "project_skills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_project_submissions_MilestoneId_Status",
                table: "project_submissions",
                columns: new[] { "MilestoneId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_project_submissions_ProjectId_Status",
                table: "project_submissions",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_project_tasks_AssigneeStudentId_DueAt",
                table: "project_tasks",
                columns: new[] { "AssigneeStudentId", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_project_tasks_ProjectId_Status_SortOrder",
                table: "project_tasks",
                columns: new[] { "ProjectId", "Status", "SortOrder" });

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

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_runs_StartedAt",
                table: "reconciliation_runs",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_SessionId_Sequence",
                table: "refresh_tokens",
                columns: new[] { "SessionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_TokenHash",
                table: "refresh_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_PermissionId",
                table: "role_permissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_roles_NormalizedName",
                table: "roles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rubric_criteria_RubricId_SortOrder",
                table: "rubric_criteria",
                columns: new[] { "RubricId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rubrics_LecturerId_IsLocked",
                table: "rubrics",
                columns: new[] { "LecturerId", "IsLocked" });

            migrationBuilder.CreateIndex(
                name: "IX_saved_projects_ProjectId",
                table: "saved_projects",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_saved_projects_StudentId_CreatedAt",
                table: "saved_projects",
                columns: new[] { "StudentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sepay_ipn_events_InvoiceCode_ReceivedAt",
                table: "sepay_ipn_events",
                columns: new[] { "InvoiceCode", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sepay_ipn_events_ProviderEventId",
                table: "sepay_ipn_events",
                column: "ProviderEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sepay_ipn_events_ProviderTransactionId",
                table: "sepay_ipn_events",
                column: "ProviderTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_skills_Code",
                table: "skills",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_skills_IsActive_Name",
                table: "skills",
                columns: new[] { "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_skills_NormalizedName",
                table: "skills",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_skills_Slug",
                table: "skills",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_student_certificates_StudentId_Name",
                table: "student_certificates",
                columns: new[] { "StudentId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_student_payout_accounts_StudentId_IsDefault",
                table: "student_payout_accounts",
                columns: new[] { "StudentId", "IsDefault" });

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
                unique: true,
                filter: "[StudentCode] IS NOT NULL");

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

            migrationBuilder.CreateIndex(
                name: "IX_submission_status_history_SubmissionId_CreatedAt",
                table: "submission_status_history",
                columns: new[] { "SubmissionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_submission_versions_FileId",
                table: "submission_versions",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_submission_versions_SubmissionId_VersionNumber",
                table: "submission_versions",
                columns: new[] { "SubmissionId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_checklist_items_ProjectTaskId_SortOrder",
                table: "task_checklist_items",
                columns: new[] { "ProjectTaskId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_task_comments_ProjectTaskId_CreatedAt",
                table: "task_comments",
                columns: new[] { "ProjectTaskId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_team_invitations_InvitedStudentId_Status_ExpiresAt",
                table: "team_invitations",
                columns: new[] { "InvitedStudentId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_team_invitations_TeamId_InvitedStudentId_Status",
                table: "team_invitations",
                columns: new[] { "TeamId", "InvitedStudentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_team_members_StudentId",
                table: "team_members",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_teams_CreatedByStudentId",
                table: "teams",
                column: "CreatedByStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_technical_submission_reviews_ReviewerUserId_CreatedAt",
                table: "technical_submission_reviews",
                columns: new[] { "ReviewerUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_technical_submission_reviews_SubmissionId_CreatedAt",
                table: "technical_submission_reviews",
                columns: new[] { "SubmissionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_technical_submission_reviews_SubmissionId_SubmissionVersionId_Decision",
                table: "technical_submission_reviews",
                columns: new[] { "SubmissionId", "SubmissionVersionId", "Decision" });

            migrationBuilder.CreateIndex(
                name: "IX_technical_submission_reviews_SubmissionVersionId",
                table: "technical_submission_reviews",
                column: "SubmissionVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_RoleId",
                table: "user_roles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_UserId_RevokedAt",
                table: "user_sessions",
                columns: new[] { "UserId", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_users_NormalizedEmail",
                table: "users",
                column: "NormalizedEmail",
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

            migrationBuilder.CreateIndex(
                name: "IX_withdrawal_requests_ProjectId_Status_CreatedAt",
                table: "withdrawal_requests",
                columns: new[] { "ProjectId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_withdrawal_requests_ProjectId_StudentId",
                table: "withdrawal_requests",
                columns: new[] { "ProjectId", "StudentId" },
                unique: true,
                filter: "[Status] IN ('REQUESTED', 'RECOMMENDED')");

            migrationBuilder.CreateIndex(
                name: "IX_withdrawal_requests_StudentId_Status_CreatedAt",
                table: "withdrawal_requests",
                columns: new[] { "StudentId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_workspace_settings_ProjectId",
                table: "workspace_settings",
                column: "ProjectId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "academic_criterion_scores");

            migrationBuilder.DropTable(
                name: "account_tokens");

            migrationBuilder.DropTable(
                name: "admin_policy_settings");

            migrationBuilder.DropTable(
                name: "allowance_allocations");

            migrationBuilder.DropTable(
                name: "applications");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "banks");

            migrationBuilder.DropTable(
                name: "business_submission_reviews");

            migrationBuilder.DropTable(
                name: "company_documents");

            migrationBuilder.DropTable(
                name: "company_invitations");

            migrationBuilder.DropTable(
                name: "company_members");

            migrationBuilder.DropTable(
                name: "course_projects");

            migrationBuilder.DropTable(
                name: "disbursements");

            migrationBuilder.DropTable(
                name: "funding_orders");

            migrationBuilder.DropTable(
                name: "idempotency_records");

            migrationBuilder.DropTable(
                name: "lecturer_assignments");

            migrationBuilder.DropTable(
                name: "meeting_action_items");

            migrationBuilder.DropTable(
                name: "meeting_minute_revisions");

            migrationBuilder.DropTable(
                name: "meeting_participants");

            migrationBuilder.DropTable(
                name: "milestone_allowances");

            migrationBuilder.DropTable(
                name: "milestone_approval_history");

            migrationBuilder.DropTable(
                name: "milestone_deliverables");

            migrationBuilder.DropTable(
                name: "notification_preferences");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "payment_transactions");

            migrationBuilder.DropTable(
                name: "portfolio_entries");

            migrationBuilder.DropTable(
                name: "project_activities");

            migrationBuilder.DropTable(
                name: "project_approvals");

            migrationBuilder.DropTable(
                name: "project_commitments");

            migrationBuilder.DropTable(
                name: "project_completions");

            migrationBuilder.DropTable(
                name: "project_deliverables");

            migrationBuilder.DropTable(
                name: "project_fundings");

            migrationBuilder.DropTable(
                name: "project_members");

            migrationBuilder.DropTable(
                name: "project_risk_history");

            migrationBuilder.DropTable(
                name: "project_risk_snapshots");

            migrationBuilder.DropTable(
                name: "project_skills");

            migrationBuilder.DropTable(
                name: "reconciliation_runs");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "ReportExports");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "rubric_criteria");

            migrationBuilder.DropTable(
                name: "saved_projects");

            migrationBuilder.DropTable(
                name: "sepay_ipn_events");

            migrationBuilder.DropTable(
                name: "student_certificates");

            migrationBuilder.DropTable(
                name: "student_payout_accounts");

            migrationBuilder.DropTable(
                name: "student_privacy_settings");

            migrationBuilder.DropTable(
                name: "student_skills");

            migrationBuilder.DropTable(
                name: "submission_status_history");

            migrationBuilder.DropTable(
                name: "task_checklist_items");

            migrationBuilder.DropTable(
                name: "task_comments");

            migrationBuilder.DropTable(
                name: "team_invitations");

            migrationBuilder.DropTable(
                name: "team_members");

            migrationBuilder.DropTable(
                name: "technical_submission_reviews");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "verified_skills");

            migrationBuilder.DropTable(
                name: "withdrawal_requests");

            migrationBuilder.DropTable(
                name: "workspace_settings");

            migrationBuilder.DropTable(
                name: "academic_evaluations");

            migrationBuilder.DropTable(
                name: "courses");

            migrationBuilder.DropTable(
                name: "lecturer_profiles");

            migrationBuilder.DropTable(
                name: "meeting_minutes");

            migrationBuilder.DropTable(
                name: "user_sessions");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "rubrics");

            migrationBuilder.DropTable(
                name: "skills");

            migrationBuilder.DropTable(
                name: "project_tasks");

            migrationBuilder.DropTable(
                name: "teams");

            migrationBuilder.DropTable(
                name: "submission_versions");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "student_profiles");

            migrationBuilder.DropTable(
                name: "project_meetings");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "file_records");

            migrationBuilder.DropTable(
                name: "project_submissions");

            migrationBuilder.DropTable(
                name: "majors");

            migrationBuilder.DropTable(
                name: "project_milestones");

            migrationBuilder.DropTable(
                name: "faculties");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "companies");

            migrationBuilder.DropTable(
                name: "industries");
        }
    }
}
