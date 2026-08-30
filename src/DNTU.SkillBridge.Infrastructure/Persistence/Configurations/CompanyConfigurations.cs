using DNTU.SkillBridge.Domain;
using DNTU.SkillBridge.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("companies");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.NormalizedName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Slug).HasMaxLength(220).IsRequired();
        builder.Property(entity => entity.TaxCode).HasMaxLength(20);
        builder.Property(entity => entity.Website).HasMaxLength(500);
        builder.Property(entity => entity.Description).HasMaxLength(2000);
        builder.Property(entity => entity.LogoUrl).HasMaxLength(500);
        builder.Property(entity => entity.Address).HasMaxLength(500);
        builder.Property(entity => entity.ContactEmail).HasMaxLength(320);
        builder.Property(entity => entity.ContactPhone).HasMaxLength(20);
        builder.Property(entity => entity.VerificationStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.VerificationNote).HasMaxLength(1000);
        builder.HasIndex(entity => entity.Slug).IsUnique();
        builder.HasIndex(entity => entity.NormalizedName);
        builder.HasIndex(entity => entity.TaxCode).IsUnique();
        builder.HasIndex(entity => new { entity.VerificationStatus, entity.IsActive });
        builder.HasOne<DNTU.SkillBridge.Domain.Catalog.Industry>()
            .WithMany()
            .HasForeignKey(entity => entity.IndustryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(entity => entity.Members)
            .WithOne(member => member.Company)
            .HasForeignKey(member => member.CompanyId);
    }
}

public sealed class CompanyMemberConfiguration : IEntityTypeConfiguration<CompanyMember>
{
    public void Configure(EntityTypeBuilder<CompanyMember> builder)
    {
        builder.ToTable("company_members");
        builder.HasKey(entity => new { entity.CompanyId, entity.UserId });
        builder.Property(entity => entity.Title).HasMaxLength(100);
        builder.Property(entity => entity.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(entity => entity.UserId).IsUnique();
        builder.HasOne<DNTU.SkillBridge.Domain.Identity.User>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CompanyInvitationConfiguration : IEntityTypeConfiguration<CompanyInvitation>
{
    public void Configure(EntityTypeBuilder<CompanyInvitation> builder)
    {
        builder.ToTable("company_invitations");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Email).HasMaxLength(320).IsRequired();
        builder.Property(entity => entity.NormalizedEmail).HasMaxLength(320).IsRequired();
        builder.Property(entity => entity.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(entity => new { entity.CompanyId, entity.NormalizedEmail, entity.Status });
        builder.HasOne<DNTU.SkillBridge.Domain.Identity.User>()
            .WithMany()
            .HasForeignKey(entity => entity.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CompanyDocumentConfiguration : IEntityTypeConfiguration<CompanyDocument>
{
    public void Configure(EntityTypeBuilder<CompanyDocument> builder)
    {
        builder.ToTable("company_documents");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.DocumentType).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.DocumentUrl).HasMaxLength(500).IsRequired();
        builder.HasIndex(entity => entity.CompanyId);
        builder.HasOne<DNTU.SkillBridge.Domain.Identity.User>()
            .WithMany()
            .HasForeignKey(entity => entity.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
