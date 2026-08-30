using DNTU.SkillBridge.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class ProjectFundingConfiguration : IEntityTypeConfiguration<ProjectFunding>
{
    public void Configure(EntityTypeBuilder<ProjectFunding> builder)
    {
        builder.ToTable("project_fundings");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Currency).HasMaxLength(3).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.HasIndex(entity => entity.ProjectId).IsUnique();
    }
}

public sealed class FundingOrderConfiguration : IEntityTypeConfiguration<FundingOrder>
{
    public void Configure(EntityTypeBuilder<FundingOrder> builder)
    {
        builder.ToTable("funding_orders");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.InvoiceCode).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Currency).HasMaxLength(3).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(entity => entity.Provider).HasMaxLength(40);
        builder.Property(entity => entity.ProviderTransactionId).HasMaxLength(128);
        builder.HasIndex(entity => entity.InvoiceCode).IsUnique();
        builder.HasIndex(entity => new { entity.ProjectId, entity.Status });
        builder.HasIndex(entity => new { entity.Provider, entity.ProviderTransactionId }).IsUnique().HasFilter("\"Provider\" IS NOT NULL AND \"ProviderTransactionId\" IS NOT NULL");
    }
}

public sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("payment_transactions");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Provider).HasMaxLength(40).IsRequired();
        builder.Property(entity => entity.ProviderTransactionId).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.Currency).HasMaxLength(3).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.HasIndex(entity => entity.FundingOrderId);
        builder.HasIndex(entity => new { entity.Provider, entity.ProviderTransactionId }).IsUnique();
    }
}

public sealed class MilestoneAllowanceConfiguration : IEntityTypeConfiguration<MilestoneAllowance>
{
    public void Configure(EntityTypeBuilder<MilestoneAllowance> builder)
    {
        builder.ToTable("milestone_allowances");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Currency).HasMaxLength(3).IsRequired();
        builder.HasIndex(entity => entity.MilestoneId).IsUnique();
    }
}

public sealed class AllowanceAllocationConfiguration : IEntityTypeConfiguration<AllowanceAllocation>
{
    public void Configure(EntityTypeBuilder<AllowanceAllocation> builder)
    {
        builder.ToTable("allowance_allocations");
        builder.HasKey(entity => entity.Id);
        builder.HasIndex(entity => new { entity.MilestoneAllowanceId, entity.StudentId }).IsUnique();
    }
}

public sealed class StudentPayoutAccountConfiguration : IEntityTypeConfiguration<StudentPayoutAccount>
{
    public void Configure(EntityTypeBuilder<StudentPayoutAccount> builder)
    {
        builder.ToTable("student_payout_accounts");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.BankCode).HasMaxLength(30).IsRequired();
        builder.Property(entity => entity.AccountHolderName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.AccountNumberCiphertext).HasMaxLength(2000).IsRequired();
        builder.Property(entity => entity.AccountNumberLast4).HasMaxLength(4).IsRequired();
        builder.Ignore(entity => entity.MaskedAccountNumber);
        builder.HasIndex(entity => new { entity.StudentId, entity.IsDefault });
    }
}

public sealed class DisbursementConfiguration : IEntityTypeConfiguration<Disbursement>
{
    public void Configure(EntityTypeBuilder<Disbursement> builder)
    {
        builder.ToTable("disbursements");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Currency).HasMaxLength(3).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(entity => entity.IdempotencyKey).HasMaxLength(128);
        builder.Property(entity => entity.BankReference).HasMaxLength(128);
        builder.Property(entity => entity.Note).HasMaxLength(1000);
        builder.HasIndex(entity => new { entity.ProjectId, entity.StudentId });
        builder.HasIndex(entity => entity.IdempotencyKey).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");
    }
}

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Key).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.Endpoint).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.RequestHash).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.ResponseJson).HasMaxLength(10000).IsRequired();
        builder.HasIndex(entity => new { entity.UserId, entity.Key, entity.Endpoint }).IsUnique();
        builder.HasIndex(entity => entity.ExpiresAt);
    }
}

public sealed class SePayIpnEventConfiguration : IEntityTypeConfiguration<SePayIpnEvent>
{
    public void Configure(EntityTypeBuilder<SePayIpnEvent> builder)
    {
        builder.ToTable("sepay_ipn_events");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ProviderEventId).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.InvoiceCode).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.ProviderTransactionId).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.Currency).HasMaxLength(3).IsRequired();
        builder.Property(entity => entity.Status).HasMaxLength(40).IsRequired();
        builder.Property(entity => entity.PayloadHash).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.FailureReason).HasMaxLength(300);
        builder.HasIndex(entity => entity.ProviderEventId).IsUnique();
        builder.HasIndex(entity => entity.ProviderTransactionId).IsUnique();
        builder.HasIndex(entity => new { entity.InvoiceCode, entity.ReceivedAt });
    }
}

public sealed class ReconciliationRunConfiguration : IEntityTypeConfiguration<ReconciliationRun>
{
    public void Configure(EntityTypeBuilder<ReconciliationRun> builder)
    {
        builder.ToTable("reconciliation_runs");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(entity => entity.Error).HasMaxLength(300);
        builder.HasIndex(entity => entity.StartedAt);
    }
}
