using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DNTU.SkillBridge.IntegrationTests;

[Collection(CatalogApiCollection.Name)]
public sealed class TeamEndpointTests(CatalogApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = CatalogApiJson.Options;
    private const string Password = "Password#123456";

    [Fact]
    public async Task Leader_CanInvite_AndStudentCanAcceptInvitation()
    {
        var leader = await RegisterStudentAsync("leader");
        var member = await RegisterStudentAsync("member");

        var memberTeam = await member.PostAsJsonAsync("/api/v1/teams", new { name = "Nhóm thành viên" }, Json);
        Assert.Equal(HttpStatusCode.Created, memberTeam.StatusCode);

        var leaderTeamResponse = await leader.PostAsJsonAsync("/api/v1/teams", new { name = "Nhóm dự án" }, Json);
        Assert.Equal(HttpStatusCode.Created, leaderTeamResponse.StatusCode);
        var leaderTeam = (await leaderTeamResponse.Content.ReadFromJsonAsync<Envelope<TeamDto>>(Json))!.Data;

        var memberUserId = await GetUserIdAsync(member);
        Guid memberProfileId = default;
        await factory.RunWithDbContextAsync(async db =>
        {
            memberProfileId = await db.StudentProfiles.AsNoTracking()
                .Where(profile => profile.UserId == memberUserId)
                .Select(profile => profile.Id)
                .SingleAsync();
        });

        var invite = await leader.PostAsJsonAsync($"/api/v1/teams/{leaderTeam.Id}/invitations", new
        {
            studentId = memberProfileId,
            role = "MEMBER",
            expiresInDays = 7
        }, Json);
        Assert.Equal(HttpStatusCode.OK, invite.StatusCode);
        var invitation = (await invite.Content.ReadFromJsonAsync<Envelope<InvitationDto>>(Json))!.Data;

        var invitations = (await (await member.GetAsync("/api/v1/students/me/team-invitations"))
            .Content.ReadFromJsonAsync<Envelope<IReadOnlyCollection<InvitationDto>>>(Json))!.Data;
        Assert.Contains(invitations, item => item.Id == invitation.Id && item.Status == "PENDING");

        Assert.Equal(HttpStatusCode.NoContent, (await member.PostAsync($"/api/v1/team-invitations/{invitation.Id}/accept", null)).StatusCode);
        var members = (await (await leader.GetAsync($"/api/v1/teams/{leaderTeam.Id}/members"))
            .Content.ReadFromJsonAsync<Envelope<IReadOnlyCollection<MemberDto>>>(Json))!.Data;
        Assert.Contains(members, item => item.StudentId == memberProfileId && item.Role == "MEMBER");
    }

    [Fact]
    public async Task NonLeader_CannotManage_Team()
    {
        var leader = await RegisterStudentAsync("owner");
        var other = await RegisterStudentAsync("other");
        var created = await leader.PostAsJsonAsync("/api/v1/teams", new { name = "Nhóm riêng" }, Json);
        var team = (await created.Content.ReadFromJsonAsync<Envelope<TeamDto>>(Json))!.Data;

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/teams/{team.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PutAsJsonAsync($"/api/v1/teams/{team.Id}", new { name = "Chiếm quyền" }, Json)).StatusCode);
    }

    private async Task<HttpClient> RegisterStudentAsync(string label)
    {
        var email = $"team-{label}-{Guid.NewGuid():N}@skillbridge.local";
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/auth/register", new { email, displayName = $"Student {label}", password = Password, accountType = "Student" }, Json)).StatusCode);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, Json);
        var token = (await login.Content.ReadFromJsonAsync<Envelope<AuthDto>>(Json))!.Data.AccessToken;
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Test-User-Email", email);
        return client;
    }

    private async Task<Guid> GetUserIdAsync(HttpClient client)
    {
        var email = client.DefaultRequestHeaders.GetValues("X-Test-User-Email").Single();
        Guid id = default;
        await factory.RunWithDbContextAsync(async db => id = await db.Users.Where(user => user.Email == email).Select(user => user.Id).SingleAsync());
        return id;
    }

    private sealed record Envelope<T>(T Data);
    private sealed record AuthDto(string AccessToken);
    private sealed record TeamDto(Guid Id, string Name);
    private sealed record InvitationDto(Guid Id, string Status);
    private sealed record MemberDto(Guid StudentId, string Role);
}
