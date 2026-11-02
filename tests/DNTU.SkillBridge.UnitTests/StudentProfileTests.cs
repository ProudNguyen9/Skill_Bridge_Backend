using DNTU.SkillBridge.Domain.Students;

namespace DNTU.SkillBridge.UnitTests;

public sealed class StudentProfileTests
{
    [Fact]
    public void Update_allows_missing_fields_and_converts_empty_strings_to_null()
    {
        var profile = new StudentProfile(Guid.NewGuid());

        // Save incomplete profile with nulls and whitespace
        profile.Update(
            studentCode: "  ",
            majorId: null,
            facultyId: null,
            academicYear: "",
            phoneNumber: "   ",
            bio: null,
            githubUrl: "   ",
            linkedinUrl: "",
            portfolioUrl: null,
            cvUrl: "",
            avatarUrl: "   "
        );

        Assert.Null(profile.StudentCode);
        Assert.Null(profile.MajorId);
        Assert.Null(profile.FacultyId);
        Assert.Null(profile.AcademicYear);
        Assert.Null(profile.PhoneNumber);
        Assert.Null(profile.Bio);
        Assert.Null(profile.GithubUrl);
        Assert.Null(profile.LinkedinUrl);
        Assert.Null(profile.PortfolioUrl);
        Assert.Null(profile.CvUrl);
        Assert.Null(profile.AvatarUrl);
    }

    [Fact]
    public void Update_normalizes_urls_without_protocol_and_saves_avatar_url()
    {
        var profile = new StudentProfile(Guid.NewGuid());

        profile.Update(
            studentCode: "2280601234",
            majorId: null,
            facultyId: null,
            academicYear: "2022-2026",
            phoneNumber: "0901234567",
            bio: "Student bio",
            githubUrl: "github.com/student123",
            linkedinUrl: "linkedin.com/in/student123",
            portfolioUrl: "https://myportfolio.dev",
            cvUrl: "drive.google.com/cv.pdf",
            avatarUrl: "http://localhost:5017/uploads/avatars/me.jpg"
        );

        Assert.Equal("2280601234", profile.StudentCode);
        Assert.Equal("https://github.com/student123", profile.GithubUrl);
        Assert.Equal("https://linkedin.com/in/student123", profile.LinkedinUrl);
        Assert.Equal("https://myportfolio.dev", profile.PortfolioUrl);
        Assert.Equal("https://drive.google.com/cv.pdf", profile.CvUrl);
        Assert.Equal("http://localhost:5017/uploads/avatars/me.jpg", profile.AvatarUrl);
    }
}
