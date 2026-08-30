using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DNTU.SkillBridge.IntegrationTests;

[Collection(CatalogApiCollection.Name)]
public sealed class StudentEndpointTests(CatalogApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = CatalogApiJson.Options;
    private const string Password = "Password#123456";

    [Fact]
    public async Task Get_Me_AutoCreatesProfile_WithDefaultPrivatePrivacy()
    {
        var client = await RegisterStudentAsync();

        var response = await client.GetAsync("/api/v1/students/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<Envelope<StudentProfileDto>>(Json);
        Assert.NotNull(payload);
        Assert.False(payload!.Data.Privacy.IsProfilePublic);
        Assert.Equal(0, payload.Data.ProfileCompletionPercent);
    }

    [Fact]
    public async Task Put_Me_UpdatesProfile_AndRejectsMismatchedMajorFaculty()
    {
        var client = await RegisterStudentAsync();

        var majors = (await client.GetFromJsonAsync<ListEnvelope<MajorDto>>("/api/v1/catalog/majors", Json))!.Data;
        var major = majors.First();
        var otherFaculty = (await client.GetFromJsonAsync<ListEnvelope<FacultyDto>>("/api/v1/catalog/faculties", Json))!.Data
            .First(faculty => faculty.Id != major.FacultyId);

        var update = new
        {
            studentCode = "SE001",
            majorId = major.Id,
            facultyId = major.FacultyId,
            academicYear = "K2023",
            phoneNumber = "0901234567",
            bio = "Sinh viên yêu công nghệ.",
            githubUrl = "https://github.com/skillbridge-student"
        };

        var updated = await client.PutAsJsonAsync("/api/v1/students/me", update, Json);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var payload = await updated.Content.ReadFromJsonAsync<Envelope<StudentProfileDto>>(Json);
        Assert.NotNull(payload);
        Assert.Equal("SE001", payload!.Data.StudentCode);
        Assert.Equal("K2023", payload.Data.AcademicYear);
        Assert.True(payload.Data.ProfileCompletionPercent >= 60);

        var mismatch = await client.PutAsJsonAsync("/api/v1/students/me", new
        {
            studentCode = "SE001",
            majorId = major.Id,
            facultyId = otherFaculty.Id
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
    }

    [Fact]
    public async Task Put_Skills_ReplacesSet_AndRejectsUnknownSkill()
    {
        var client = await RegisterStudentAsync();
        var skills = (await client.GetFromJsonAsync<ListEnvelope<SkillDto>>("/api/v1/catalog/skills", Json))!.Data;
        var first = skills.OrderBy(skill => skill.Name).First();
        var second = skills.OrderBy(skill => skill.Name).Skip(1).First();

        var replace = await client.PutAsJsonAsync("/api/v1/students/me/skills", new
        {
            skills = new object[]
            {
                new { skillId = first.Id, level = "ADVANCED" },
                new { skillId = second.Id, level = "BEGINNER" }
            }
        }, Json);
        Assert.Equal(HttpStatusCode.OK, replace.StatusCode);

        var payload = await replace.Content.ReadFromJsonAsync<ListEnvelope<DeclaredSkillDto>>(Json);
        Assert.NotNull(payload);
        Assert.Equal(2, payload!.Data.Count);
        Assert.Contains(payload.Data, skill => skill.SkillId == first.Id && skill.Level == "ADVANCED");

        var unknown = await client.PutAsJsonAsync("/api/v1/students/me/skills", new
        {
            skills = new object[] { new { skillId = Guid.NewGuid(), level = "ADVANCED" } }
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task Certificates_SupportCrud_AndDenyOtherStudents()
    {
        var client = await RegisterStudentAsync();

        var created = await client.PostAsJsonAsync("/api/v1/students/me/certificates", new
        {
            name = "AWS Cloud Practitioner",
            issuer = "Amazon Web Services",
            issueDate = "2025-06-01",
            certificateUrl = "https://example.com/cert/123"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var createdPayload = await created.Content.ReadFromJsonAsync<Envelope<CertificateDto>>(Json);
        Assert.NotNull(createdPayload);
        var certificateId = createdPayload!.Data.Id;

        var listed = await client.GetFromJsonAsync<ListEnvelope<CertificateDto>>("/api/v1/students/me/certificates", Json);
        Assert.NotNull(listed);
        Assert.Single(listed!.Data);

        var updated = await client.PutAsJsonAsync($"/api/v1/students/me/certificates/{certificateId}", new
        {
            name = "AWS Solutions Architect",
            issuer = "Amazon Web Services",
            issueDate = "2025-06-01",
            certificateUrl = "https://example.com/cert/123"
        }, Json);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var updatedPayload = await updated.Content.ReadFromJsonAsync<Envelope<CertificateDto>>(Json);
        Assert.Equal("AWS Solutions Architect", updatedPayload!.Data.Name);

        var otherClient = await RegisterStudentAsync();
        var foreignUpdate = await otherClient.PutAsJsonAsync($"/api/v1/students/me/certificates/{certificateId}", new
        {
            name = "Hijack",
            issuer = "Attacker",
            issueDate = "2025-06-01"
        }, Json);
        Assert.Equal(HttpStatusCode.NotFound, foreignUpdate.StatusCode);

        var foreignDelete = await otherClient.DeleteAsync($"/api/v1/students/me/certificates/{certificateId}");
        Assert.Equal(HttpStatusCode.NotFound, foreignDelete.StatusCode);

        var deleted = await client.DeleteAsync($"/api/v1/students/me/certificates/{certificateId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var empty = await client.GetFromJsonAsync<ListEnvelope<CertificateDto>>("/api/v1/students/me/certificates", Json);
        Assert.Empty(empty!.Data);
    }

    [Fact]
    public async Task Public_Profile_HonorsPrivacy_Flags_And404WhenPrivate()
    {
        var client = await RegisterStudentAsync();

        var profile = (await client.GetFromJsonAsync<Envelope<StudentProfileDto>>("/api/v1/students/me", Json))!.Data;
        var skills = (await client.GetFromJsonAsync<ListEnvelope<SkillDto>>("/api/v1/catalog/skills", Json))!.Data;
        await client.PutAsJsonAsync("/api/v1/students/me", new
        {
            bio = "Public bio",
            cvUrl = "https://example.com/private-cv.pdf",
            phoneNumber = "0901234567"
        }, Json);
        await client.PutAsJsonAsync("/api/v1/students/me/skills", new
        {
            skills = new object[] { new { skillId = skills.First().Id, level = "INTERMEDIATE" } }
        }, Json);
        await client.PostAsJsonAsync("/api/v1/students/me/certificates", new
        {
            name = "TOEIC 800",
            issuer = "ETS",
            issueDate = "2025-01-15"
        }, Json);

        var anonymous = factory.CreateClient();
        var privateResponse = await anonymous.GetAsync($"/api/v1/students/{profile.Id}");
        Assert.Equal(HttpStatusCode.NotFound, privateResponse.StatusCode);

        await client.PutAsJsonAsync("/api/v1/students/me/privacy", new
        {
            isProfilePublic = true,
            showContactInfo = false,
            showDeclaredSkills = true,
            showCertificates = true
        }, Json);

        var publicResponse = await anonymous.GetAsync($"/api/v1/students/{profile.Id}");
        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
        Assert.DoesNotContain("private-cv.pdf", await publicResponse.Content.ReadAsStringAsync());

        var publicPayload = (await publicResponse.Content.ReadFromJsonAsync<Envelope<PublicStudentDto>>(Json))!.Data;
        Assert.Equal("Public bio", publicPayload.Bio);
        Assert.Null(publicPayload.PhoneNumber);
        Assert.Single(publicPayload.Skills);
        Assert.Single(publicPayload.Certificates);

        await client.PutAsJsonAsync("/api/v1/students/me/privacy", new
        {
            isProfilePublic = true,
            showContactInfo = true,
            showDeclaredSkills = false,
            showCertificates = false
        }, Json);

        var gated = (await anonymous.GetFromJsonAsync<Envelope<PublicStudentDto>>($"/api/v1/students/{profile.Id}", Json))!.Data;
        Assert.Equal("0901234567", gated.PhoneNumber);
        Assert.Empty(gated.Skills);
        Assert.Empty(gated.Certificates);
    }

    [Fact]
    public async Task Auth_Me_ReturnsStudentProfileContext()
    {
        var client = await RegisterStudentAsync();

        var profile = (await client.GetFromJsonAsync<Envelope<StudentProfileDto>>("/api/v1/students/me", Json))!.Data;
        var me = await client.GetFromJsonAsync<Envelope<CurrentUserDto>>("/api/v1/auth/me", Json);

        Assert.NotNull(me);
        Assert.Equal("STUDENT", me!.Data.ProfileType);
        Assert.Equal(profile.Id, me.Data.StudentId);
    }

    [Fact]
    public async Task Non_Student_Role_IsForbidden_FromStudentEndpoints()
    {
        var email = $"company-{Guid.NewGuid():N}@skillbridge.local";
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            displayName = "Công ty TNHH Kiểm thử",
            password = Password,
            accountType = "Company"
        }, Json);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, Json);
        var auth = (await login.Content.ReadFromJsonAsync<Envelope<AuthDto>>(Json))!.Data;
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);

        var response = await client.GetAsync("/api/v1/students/me");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<HttpClient> RegisterStudentAsync()
    {
        var email = $"student-{Guid.NewGuid():N}@skillbridge.local";
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            displayName = "Sinh viên Kiểm thử",
            password = Password,
            accountType = "Student"
        }, Json);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<Envelope<AuthDto>>(Json))!.Data;
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        return client;
    }

    private sealed record Envelope<T>(T Data);

    private sealed record ListEnvelope<T>(IReadOnlyCollection<T> Data);

    private sealed record AuthDto(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt, Guid SessionId);

    private sealed record CurrentUserDto(Guid UserId, string Email, string DisplayName, bool EmailVerified, bool IsActive, IReadOnlyCollection<string> Roles, IReadOnlyCollection<string> Permissions, string? ProfileType, Guid? StudentId, Guid? CompanyId, Guid? LecturerId, bool ProfileCompleted);

    private sealed record StudentPrivacyDto(bool IsProfilePublic, bool ShowContactInfo, bool ShowDeclaredSkills, bool ShowCertificates);

    private sealed record StudentProfileDto(Guid Id, string? StudentCode, Guid? MajorId, string? MajorName, Guid? FacultyId, string? FacultyName, string? AcademicYear, string? PhoneNumber, string? Bio, string? GithubUrl, string? LinkedinUrl, string? PortfolioUrl, string? CvUrl, StudentPrivacyDto Privacy, int ProfileCompletionPercent);

    private sealed record SkillDto(Guid Id, string Code, string Name, string Category, string? Description, bool IsActive);

    private sealed record FacultyDto(Guid Id, string Code, string Name, bool IsActive);

    private sealed record MajorDto(Guid Id, string Code, string Name, Guid FacultyId, string FacultyName, bool IsActive);

    private sealed record DeclaredSkillDto(Guid SkillId, string SkillCode, string SkillName, string Category, string Level);

    private sealed record CertificateDto(Guid Id, string Name, string Issuer, DateOnly IssueDate, string? CertificateUrl);

    private sealed record PublicSkillDto(Guid SkillId, string SkillName, string Level);

    private sealed record PublicCertificateDto(string Name, string Issuer, DateOnly IssueDate);

    private sealed record PublicStudentDto(Guid Id, string DisplayName, Guid? FacultyId, string? FacultyName, Guid? MajorId, string? MajorName, string? AcademicYear, string? Bio, string? GithubUrl, string? LinkedinUrl, string? PortfolioUrl, string? PhoneNumber, IReadOnlyCollection<PublicSkillDto> Skills, IReadOnlyCollection<PublicCertificateDto> Certificates);
}
