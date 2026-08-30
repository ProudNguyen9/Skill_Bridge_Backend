using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DNTU.SkillBridge.Domain.Catalog;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Infrastructure.Persistence;
using DNTU.SkillBridge.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace DNTU.SkillBridge.IntegrationTests;

[Collection(CatalogApiCollection.Name)]
public sealed class CatalogEndpointTests(CatalogApiFactory factory)
{
    private const string SigningKey = "integration-tests-signing-key-with-at-least-sixty-four-characters-0001";
    private static readonly JsonSerializerOptions Json = CatalogApiJson.Options;

    [Fact]
    public async Task Get_Skills_ReturnsSeededActiveSkills_OrderedByName()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/catalog/skills");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<Envelope<SkillDto>>(Json);
        Assert.NotNull(payload);
        Assert.NotEmpty(payload!.Data);
        Assert.Contains(payload.Data, skill => skill.Code == "ASPNET_CORE" && skill.Category == "TECHNICAL");
        Assert.All(payload.Data, skill => Assert.True(skill.IsActive));

        var names = payload.Data.Select(skill => skill.Name).ToList();
        Assert.Equal(names.OrderBy(name => name, StringComparer.Ordinal).ToList(), names);
    }

    [Fact]
    public async Task Get_SkillsAlias_ReturnsSamePayloadShape()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/skills");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<Envelope<SkillDto>>(Json);
        Assert.NotNull(payload);
        Assert.NotEmpty(payload!.Data);
    }

    [Fact]
    public async Task Get_Skills_IncludeInactive_RequiresAdministrator()
    {
        var client = factory.CreateClient();

        var anonymous = await client.GetAsync("/api/v1/catalog/skills?includeInactive=true");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(RoleNames.Student));
        var student = await client.GetAsync("/api/v1/catalog/skills?includeInactive=true");
        Assert.Equal(HttpStatusCode.Forbidden, student.StatusCode);

        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(RoleNames.Admin));
        var admin = await client.GetAsync("/api/v1/catalog/skills?includeInactive=true");
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
    }

    [Fact]
    public async Task Get_Skills_DefaultList_OmitsInactive_While_AdminIncludeInactive_Lists_Them()
    {
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var probe = new Skill("RET_SKILL", "Retired Skill", SkillCategory.TECHNICAL, "inactive probe");
            dbContext.Skills.Add(probe);
            await dbContext.SaveChangesAsync();

            probe.Deactivate();
            await dbContext.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        var defaultPayload = await client.GetFromJsonAsync<Envelope<SkillDto>>("/api/v1/catalog/skills", Json);
        Assert.NotNull(defaultPayload);
        Assert.DoesNotContain(defaultPayload!.Data, skill => skill.Code == "RET_SKILL");

        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(RoleNames.Admin));
        var adminPayload = await client.GetFromJsonAsync<Envelope<SkillDto>>("/api/v1/catalog/skills?includeInactive=true", Json);
        Assert.NotNull(adminPayload);
        Assert.Contains(adminPayload!.Data, skill => skill.Code == "RET_SKILL" && !skill.IsActive);
    }

    [Fact]
    public async Task Get_Majors_FiltersByFaculty_AndOrdersDeterministically()
    {
        Guid fitId = default;
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            fitId = await dbContext.Faculties
                .Where(faculty => faculty.Code == "FIT")
                .Select(faculty => faculty.Id)
                .SingleAsync();
        });

        var client = factory.CreateClient();
        var payload = await client.GetFromJsonAsync<Envelope<MajorDto>>($"/api/v1/catalog/majors?facultyId={fitId}", Json);

        Assert.NotNull(payload);
        Assert.NotEmpty(payload!.Data);
        Assert.All(payload.Data, major => Assert.Equal(fitId, major.FacultyId));
        Assert.All(payload.Data, major => Assert.Equal("Khoa Công nghệ thông tin", major.FacultyName));

        var names = payload.Data.Select(major => major.Name).ToList();
        Assert.Equal(names.OrderBy(name => name, StringComparer.Ordinal).ToList(), names);
    }

    [Fact]
    public async Task Get_Banks_ReturnsSeededBanks_WithBinCodes()
    {
        var client = factory.CreateClient();

        var payload = await client.GetFromJsonAsync<Envelope<BankDto>>("/api/v1/catalog/banks", Json);

        Assert.NotNull(payload);
        Assert.Contains(payload!.Data, bank => bank.Code == "VCB" && bank.Bin == "970436" && bank.Name == "Vietcombank");
        Assert.All(payload.Data, bank => Assert.Matches(@"^\d{6}$", bank.Bin));
    }

    [Fact]
    public async Task Get_Industries_ReturnsSeededIndustries_WithSlugs()
    {
        var client = factory.CreateClient();

        var payload = await client.GetFromJsonAsync<Envelope<IndustryDto>>("/api/v1/catalog/industries", Json);

        Assert.NotNull(payload);
        Assert.Contains(payload!.Data, industry => industry.Name == "Công nghệ thông tin" && industry.Slug == "cong-nghe-thong-tin");
    }

    [Fact]
    public async Task Get_ProjectMetadata_ReturnsConfiguredExplicitValues()
    {
        var client = factory.CreateClient();

        var payload = await client.GetFromJsonAsync<SingleEnvelope<ProjectMetadataDto>>("/api/v1/catalog/project-metadata", Json);

        Assert.NotNull(payload);
        Assert.Equal(["BEGINNER", "INTERMEDIATE", "ADVANCED"], payload!.Data.Difficulties);
        Assert.Equal(["ONSITE", "HYBRID", "REMOTE"], payload.Data.WorkTypes);
        Assert.Equal("VND", payload.Data.Allowance.Currency);
        Assert.True(payload.Data.DurationWeeks.Min <= payload.Data.DurationWeeks.Max);
        Assert.True(payload.Data.TeamSize.Min <= payload.Data.TeamSize.Max);
    }

    [Fact]
    public async Task Get_TaskMetadata_ReturnsStatusesAndPriorities()
    {
        var client = factory.CreateClient();

        var payload = await client.GetFromJsonAsync<SingleEnvelope<TaskMetadataDto>>("/api/v1/catalog/task-metadata", Json);

        Assert.NotNull(payload);
        Assert.Equal(["BACKLOG", "TODO", "IN_PROGRESS", "REVIEW", "DONE"], payload!.Data.Statuses);
        Assert.Equal(["LOW", "MEDIUM", "HIGH", "CRITICAL"], payload.Data.Priorities);
    }

    [Fact]
    public async Task Get_Faculties_ReturnsSeededFaculties()
    {
        var client = factory.CreateClient();

        var payload = await client.GetFromJsonAsync<Envelope<FacultyDto>>("/api/v1/catalog/faculties", Json);

        Assert.NotNull(payload);
        Assert.Contains(payload!.Data, faculty => faculty.Code == "FIT");
    }

    [Fact]
    public async Task CatalogSeed_IsIdempotent()
    {
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var skillsBefore = await dbContext.Skills.CountAsync();
            var majorsBefore = await dbContext.Majors.CountAsync();
            var banksBefore = await dbContext.Banks.CountAsync();

            await CatalogSeed.SeedAsync(dbContext, CancellationToken.None);

            Assert.Equal(skillsBefore, await dbContext.Skills.CountAsync());
            Assert.Equal(majorsBefore, await dbContext.Majors.CountAsync());
            Assert.Equal(banksBefore, await dbContext.Banks.CountAsync());
        });
    }

    [Fact]
    public async Task Duplicate_Skill_Name_IsRejected_ByUniqueIndex()
    {
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync();
            dbContext.Skills.Add(new Skill("DUPLICATE_CODE", "React", SkillCategory.TECHNICAL));
            await Assert.ThrowsAnyAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        });
    }

    [Fact]
    public async Task Duplicate_Skill_Code_IsRejected_ByUniqueIndex()
    {
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync();
            dbContext.Skills.Add(new Skill("REACT", "Another React Name", SkillCategory.TECHNICAL));
            await Assert.ThrowsAnyAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        });
    }

    private static string CreateToken(string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new("session_id", Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role)
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            "DNTU.SkillBridge.Api",
            "DNTU.SkillBridge.Web",
            claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record Envelope<T>(IReadOnlyCollection<T> Data);

    private sealed record SingleEnvelope<T>(T Data);

    private sealed record SkillDto(Guid Id, string Code, string Name, string Category, string? Description, bool IsActive);

    private sealed record IndustryDto(Guid Id, string Name, string Slug, bool IsActive);

    private sealed record FacultyDto(Guid Id, string Code, string Name, bool IsActive);

    private sealed record MajorDto(Guid Id, string Code, string Name, Guid FacultyId, string FacultyName, bool IsActive);

    private sealed record BankDto(Guid Id, string Code, string Name, string Bin, bool IsActive);

    private sealed record RangeDto(int Min, int Max);

    private sealed record AllowanceDto(string Currency, RangeDto Amount);

    private sealed record ProjectMetadataDto(
        IReadOnlyCollection<string> Difficulties,
        IReadOnlyCollection<string> WorkTypes,
        RangeDto DurationWeeks,
        RangeDto TeamSize,
        AllowanceDto Allowance);

    private sealed record TaskMetadataDto(IReadOnlyCollection<string> Statuses, IReadOnlyCollection<string> Priorities);
}
