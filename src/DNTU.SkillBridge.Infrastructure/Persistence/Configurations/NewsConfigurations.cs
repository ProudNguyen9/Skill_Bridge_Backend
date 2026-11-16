using DNTU.SkillBridge.Domain.News;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Configurations;

public sealed class NewsCategoryConfiguration : IEntityTypeConfiguration<NewsCategory>
{
    public void Configure(EntityTypeBuilder<NewsCategory> builder)
    {
        builder.ToTable("news_categories");
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Name).HasMaxLength(100).IsRequired();
        builder.Property(category => category.SortOrder).IsRequired();
        builder.HasIndex(category => category.Name).IsUnique();
        builder.HasIndex(category => new { category.SortOrder, category.Name });
    }
}

public sealed class NewsArticleConfiguration : IEntityTypeConfiguration<NewsArticle>
{
    public void Configure(EntityTypeBuilder<NewsArticle> builder)
    {
        builder.ToTable("news_articles");
        builder.HasKey(article => article.Id);
        builder.Property(article => article.Slug).HasMaxLength(240).IsRequired();
        builder.Property(article => article.Title).HasMaxLength(300).IsRequired();
        builder.Property(article => article.Category).HasMaxLength(100).IsRequired();
        builder.Property(article => article.Excerpt).HasMaxLength(600).IsRequired();
        builder.Property(article => article.ContentJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(article => article.CoverImageUrl).HasMaxLength(1000);
        builder.Property(article => article.CoverImageAlt).HasMaxLength(300);
        builder.Property(article => article.Source).HasMaxLength(200);
        builder.Property(article => article.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(article => article.Slug).IsUnique();
        builder.HasIndex(article => new { article.Status, article.PublishAtUtc });
        builder.HasIndex(article => new { article.Category, article.PublishAtUtc });
    }
}

public sealed class NewsPinSettingsConfiguration : IEntityTypeConfiguration<NewsPinSettings>
{
    public void Configure(EntityTypeBuilder<NewsPinSettings> builder)
    {
        builder.ToTable("news_pin_settings");
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.PinnedUntilUtc);
        builder.HasOne<NewsArticle>()
            .WithMany()
            .HasForeignKey(settings => settings.PinnedArticleId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
