using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DNTU.SkillBridge.Domain.Companies;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DNTU.SkillBridge.IntegrationTests;

[Collection(CatalogApiCollection.Name)]
public sealed class CompanyEndpointTests(CatalogApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = CatalogApiJson.Options;
    private const string Password = "Password#123456";

    [Fact]
    public async Task Get_Me_Returns404_BeforeCompanyCreated()
    {
        var client = await RegisterCompanyAsync();

        var response = await client.GetAsync("/api/v1/companies/me");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_Me_CreatesCompany_WithOwnerMembership_AndUniqueSlug()
    {
        var client = await RegisterCompanyAsync();
        var name = $"Công ty Kiểm thử {Guid.NewGuid():N}";

        var created = await client.PutAsJsonAsync("/api/v1/companies/me", new { name }, Json);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var payload = (await created.Content.ReadFromJsonAsync<Envelope<CompanyProfileDto>>(Json))!.Data;
        Assert.Equal("PENDING", payload.VerificationStatus);
        Assert.False(payload.CanPublishProjects);

        var members = (await client.GetFromJsonAsync<ListEnvelope<MemberDto>>("/api/v1/companies/me/members", Json))!.Data;
        Assert.Single(members);
        Assert.Equal("OWNER", members.First().Role);

        var slug = payload.Slug;
        var secondClient = await RegisterCompanyAsync();
        var secondName = name;
        var secondCreated = await secondClient.PutAsJsonAsync("/api/v1/companies/me", new { name = secondName }, Json);
        var secondPayload = (await secondCreated.Content.ReadFromJsonAsync<Envelope<CompanyProfileDto>>(Json))!.Data;
        Assert.NotEqual(slug, secondPayload.Slug);
        Assert.StartsWith(slug, secondPayload.Slug);
    }

    [Fact]
    public async Task Put_Me_RejectsDuplicateTaxCode_With409()
    {
        var first = await RegisterCompanyAsync();
        await first.PutAsJsonAsync("/api/v1/companies/me", new { name = $"Tax Co {Guid.NewGuid():N}", taxCode = "0312345678" }, Json);

        var second = await RegisterCompanyAsync();
        var conflict = await second.PutAsJsonAsync("/api/v1/companies/me", new { name = $"Other Co {Guid.NewGuid():N}", taxCode = "0312345678" }, Json);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Public_Directory_ExcludesPendingCompanies()
    {
        var client = await RegisterCompanyAsync();
        await client.PutAsJsonAsync("/api/v1/companies/me", new { name = $"Pending Co {Guid.NewGuid():N}" }, Json);

        var anonymous = factory.CreateClient();
        var list = (await anonymous.GetFromJsonAsync<ListEnvelope<PublicCompanyDto>>("/api/v1/companies", Json))!.Data;
        // Other tests in this collection legitimately verify companies; assert the pending one is absent.
        Assert.DoesNotContain(list, company => company.Name.StartsWith("Pending Co"));
    }

    [Fact]
    public async Task Public_Slug_Profile_Returns404_WhenNotVerified()
    {
        var client = await RegisterCompanyAsync();
        await client.PutAsJsonAsync("/api/v1/companies/me", new { name = $"Hidden Co {Guid.NewGuid():N}" }, Json);
        var payload = (await (await client.GetAsync("/api/v1/companies/me")).Content.ReadFromJsonAsync<Envelope<CompanyProfileDto>>(Json))!.Data;

        var anonymous = factory.CreateClient();
        var response = await anonymous.GetAsync($"/api/v1/companies/{payload.Slug}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Documents_AreCompanyScoped_OtherCompanyGets404()
    {
        var first = await RegisterCompanyAsync();
        await first.PutAsJsonAsync("/api/v1/companies/me", new { name = $"Doc Co {Guid.NewGuid():N}" }, Json);
        var created = await first.PostAsJsonAsync("/api/v1/companies/me/documents", new
        {
            name = "Giấy phép kinh doanh",
            documentType = "BUSINESS_REGISTRATION",
            documentUrl = "https://example.com/docs/gpkd.pdf"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var documentId = (await created.Content.ReadFromJsonAsync<Envelope<DocumentDto>>(Json))!.Data.Id;

        var second = await RegisterCompanyAsync();
        await second.PutAsJsonAsync("/api/v1/companies/me", new { name = $"Rival Co {Guid.NewGuid():N}" }, Json);
        var foreignList = (await second.GetFromJsonAsync<ListEnvelope<DocumentDto>>("/api/v1/companies/me/documents", Json))!.Data;
        Assert.Empty(foreignList);
        var foreignDelete = await second.DeleteAsync($"/api/v1/companies/me/documents/{documentId}");
        Assert.Equal(HttpStatusCode.NotFound, foreignDelete.StatusCode);

        var ownList = (await first.GetFromJsonAsync<ListEnvelope<DocumentDto>>("/api/v1/companies/me/documents", Json))!.Data;
        Assert.Single(ownList);
        var ownDelete = await first.DeleteAsync($"/api/v1/companies/me/documents/{documentId}");
        Assert.Equal(HttpStatusCode.NoContent, ownDelete.StatusCode);
    }

    [Fact]
    public async Task Member_Removal_EnforcesMinimumOneOwner()
    {
        var client = await RegisterCompanyAsync();
        await client.PutAsJsonAsync("/api/v1/companies/me", new { name = $"Owner Co {Guid.NewGuid():N}" }, Json);

        // The creator is the only owner: removing them must fail.
        var me = (await client.GetFromJsonAsync<Envelope<CurrentUserDto>>("/api/v1/auth/me", Json))!.Data;
        var removeSelf = await client.DeleteAsync($"/api/v1/companies/me/members/{me.UserId}");
        Assert.Equal(HttpStatusCode.BadRequest, removeSelf.StatusCode);

        // Add a second member directly to exercise successful removal.
        var secondUser = await CreateCompanyUserDirectlyAsync();
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var companyId = await dbContext.CompanyMembers
                .Where(member => member.UserId == me.UserId)
                .Select(member => member.CompanyId)
                .SingleAsync();
            dbContext.CompanyMembers.Add(new CompanyMember(companyId, secondUser, CompanyMemberRole.MEMBER, "Nhân viên"));
            await dbContext.SaveChangesAsync();
        });

        var members = (await client.GetFromJsonAsync<ListEnvelope<MemberDto>>("/api/v1/companies/me/members", Json))!.Data;
        Assert.Equal(2, members.Count);

        var removed = await client.DeleteAsync($"/api/v1/companies/me/members/{secondUser}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

        var after = (await client.GetFromJsonAsync<ListEnvelope<MemberDto>>("/api/v1/companies/me/members", Json))!.Data;
        Assert.Single(after);
    }

    [Fact]
    public async Task Invitation_CreatesPendingRecord_AndRejectsExistingMember()
    {
        var client = await RegisterCompanyAsync();
        await client.PutAsJsonAsync("/api/v1/companies/me", new { name = $"Invite Co {Guid.NewGuid():N}" }, Json);

        var invited = await client.PostAsJsonAsync("/api/v1/companies/me/members/invitations", new
        {
            email = "candidate@skillbridge.local",
            role = "MANAGER",
            title = "Trưởng phòng dự án"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, invited.StatusCode);
        var invitation = (await invited.Content.ReadFromJsonAsync<Envelope<InvitationDto>>(Json))!.Data;
        Assert.Equal("PENDING", invitation.Status);
        Assert.Equal("MANAGER", invitation.Role);

        // Inviting an existing member (the owner themself) must be rejected.
        var ownerEmail = await GetOwnEmailAsync(client);
        var existingMember = await client.PostAsJsonAsync("/api/v1/companies/me/members/invitations", new
        {
            email = ownerEmail,
            role = "MEMBER"
        }, Json);
        Assert.Equal(HttpStatusCode.NotFound, existingMember.StatusCode);
    }

    [Fact]
    public async Task Auth_Me_ReportsCompanyProfileType_AfterCreation()
    {
        var client = await RegisterCompanyAsync();
        await client.PutAsJsonAsync("/api/v1/companies/me", new { name = $"Profile Co {Guid.NewGuid():N}" }, Json);

        var me = (await client.GetFromJsonAsync<Envelope<CurrentUserDto>>("/api/v1/auth/me", Json))!.Data;
        Assert.Equal("COMPANY", me.ProfileType);
        Assert.NotEqual(Guid.Empty, me.CompanyId!.Value);
    }

    private async Task<HttpClient> RegisterCompanyAsync()
    {
        var email = $"company-{Guid.NewGuid():N}@skillbridge.local";
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            displayName = "Công ty Kiểm thử",
            password = Password,
            accountType = "Company"
        }, Json);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<Envelope<AuthDto>>(Json))!.Data;
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        return client;
    }

    private async Task<Guid> CreateCompanyUserDirectlyAsync()
    {
        var userId = Guid.Empty;
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var user = new Domain.Identity.User($"extra-{Guid.NewGuid():N}@skillbridge.local", "Nhân viên thêm", "not-a-real-hash");
            var role = await dbContext.Roles.SingleAsync(role => role.NormalizedName == RoleNames.Company);
            user.AssignRole(role.Id);
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
            userId = user.Id;
        });
        return userId;
    }

    private async Task<string> GetOwnEmailAsync(HttpClient client)
    {
        var me = (await client.GetFromJsonAsync<Envelope<CurrentUserDto>>("/api/v1/auth/me", Json))!.Data;
        return me.Email;
    }

    private sealed record Envelope<T>(T Data);

    private sealed record ListEnvelope<T>(IReadOnlyCollection<T> Data);

    private sealed record AuthDto(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt, Guid SessionId);

    private sealed record CurrentUserDto(Guid UserId, string Email, string DisplayName, bool EmailVerified, bool IsActive, IReadOnlyCollection<string> Roles, IReadOnlyCollection<string> Permissions, string? ProfileType, Guid? StudentId, Guid? CompanyId, Guid? LecturerId, bool ProfileCompleted);

    private sealed record CompanyProfileDto(Guid Id, string Name, string Slug, string? TaxCode, Guid? IndustryId, string? IndustryName, string? Website, string? Description, string? LogoUrl, string? Address, string? ContactEmail, string? ContactPhone, string VerificationStatus, bool CanPublishProjects);

    private sealed record MemberDto(Guid UserId, string DisplayName, string Email, string Role, string? Title);

    private sealed record InvitationDto(Guid Id, string Email, string Role, string Status);

    private sealed record DocumentDto(Guid Id, string Name, string DocumentType, string DocumentUrl, DateTimeOffset CreatedAt);

    private sealed record PublicCompanyDto(Guid Id, string Name, string Slug, Guid? IndustryId, string? IndustryName, string? Website, string? Description, string? LogoUrl, string? Address);
}
