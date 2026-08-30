using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.UnitTests;

public class CatalogSlugTests
{
    [Theory]
    [InlineData("ASP.NET Core", "aspnet-core")]
    [InlineData("Kỹ thuật phần mềm", "ky-thuat-phan-mem")]
    [InlineData("Tài chính - Ngân hàng", "tai-chinh-ngan-hang")]
    [InlineData("Công nghệ thông tin", "cong-nghe-thong-tin")]
    [InlineData("  Điện tử  Viễn thông ", "dien-tu-vien-thong")]
    [InlineData("Đà Nẵng", "da-nang")]
    public void Create_NormalizesVietnameseNamesToStableSlugs(string input, string expected)
    {
        Assert.Equal(expected, SlugBuilder.Create(input));
    }

    [Fact]
    public void Create_IsIdempotent_AndLowercase()
    {
        var slug = SlugBuilder.Create("Công Nghệ Thông Tin");
        Assert.Equal(SlugBuilder.Create(slug), slug);
        Assert.Equal(slug.ToLowerInvariant(), slug);
        Assert.DoesNotContain(" ", slug);
    }
}
