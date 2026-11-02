using System.Net;
using System.Security.Claims;
using DNTU.SkillBridge.Api.Realtime;
using DNTU.SkillBridge.Application.Workspaces;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;

namespace DNTU.SkillBridge.FunctionalTests;

public sealed class ProjectHubAndCorsSecurityTests : IAsyncLifetime
{
    private static readonly string ConnectionString = Environment.GetEnvironmentVariable("SKILLBRIDGE_FUNCTIONAL_SQLSERVER")
        ?? "Server=(localdb)\\SkillBridge2022;Database=skillbridge;Integrated Security=True;Encrypt=True;TrustServerCertificate=True";

    private WebApplicationFactory<Program>? factory;
    private HttpClient? client;

    public Task InitializeAsync()
    {
        factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Test");
                builder.UseSetting("ConnectionStrings:Default", ConnectionString);
                builder.UseSetting("Jwt:SigningKey", "functional-tests-signing-key-with-at-least-sixty-four-characters-0001");
            });

        client = factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        client?.Dispose();
        if (factory is not null)
        {
            await factory.DisposeAsync();
        }
    }

    #region 1. CORS Verification Tests

    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("http://localhost:3000")]
    [InlineData("http://localhost:5017")]
    public async Task Cors_PreflightRequest_FromAllowedOrigins_ReturnsAllowHeadersAndCredentials(string origin)
    {
        Assert.NotNull(client);

        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/projects");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        using var response = await client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode, $"Preflight request from {origin} should succeed with 2xx status code");
        
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"), "Response must contain Access-Control-Allow-Origin header");
        var allowOrigin = response.Headers.GetValues("Access-Control-Allow-Origin").FirstOrDefault();
        Assert.Equal(origin, allowOrigin);

        Assert.True(response.Headers.Contains("Access-Control-Allow-Credentials"), "Response must contain Access-Control-Allow-Credentials header");
        var allowCredentials = response.Headers.GetValues("Access-Control-Allow-Credentials").FirstOrDefault();
        Assert.Equal("true", allowCredentials);
    }

    [Theory]
    [InlineData("http://evil-attacker.com")]
    [InlineData("http://localhost:5173.attacker.com")]
    [InlineData("http://localhost:8080")]
    [InlineData("https://malicious-site.org")]
    public async Task Cors_PreflightRequest_FromUntrustedOrigins_DoesNotAllowOrigin(string untrustedOrigin)
    {
        Assert.NotNull(client);

        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/projects");
        request.Headers.Add("Origin", untrustedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        using var response = await client.SendAsync(request);

        if (response.Headers.Contains("Access-Control-Allow-Origin"))
        {
            var allowOrigin = response.Headers.GetValues("Access-Control-Allow-Origin").FirstOrDefault();
            Assert.NotEqual(untrustedOrigin, allowOrigin);
            Assert.NotEqual("*", allowOrigin);
        }
    }

    [Fact]
    public async Task Cors_SignalRNegotiatePreflight_AllowsFrontendOriginWithCredentials()
    {
        Assert.NotNull(client);

        using var request = new HttpRequestMessage(HttpMethod.Options, "/hubs/projects/negotiate?negotiateVersion=1");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,x-requested-with");

        using var response = await client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode, "SignalR preflight request must succeed");
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal("http://localhost:5173", response.Headers.GetValues("Access-Control-Allow-Origin").First());
        Assert.True(response.Headers.Contains("Access-Control-Allow-Credentials"));
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").First());
    }

    #endregion

    #region 2. ProjectHub Authorization Logic Unit / Empirical Tests

    [Fact]
    public async Task ProjectHub_JoinProject_WhenUserUnauthenticated_ThrowsHubException()
    {
        var fakeRepo = new FakeProjectAccessRepository();
        var hub = CreateHub(fakeRepo, user: null);

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.JoinProject(Guid.NewGuid()));
        Assert.Equal("Project access is denied.", ex.Message);
    }

    [Fact]
    public async Task ProjectHub_JoinProject_WhenNameIdentifierClaimMissing_ThrowsHubException()
    {
        var fakeRepo = new FakeProjectAccessRepository();
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.Role, RoleNames.Student),
            new Claim(ClaimTypes.Email, "student@test.com")
        ], "TestAuth"));

        var hub = CreateHub(fakeRepo, user);

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.JoinProject(Guid.NewGuid()));
        Assert.Equal("Project access is denied.", ex.Message);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345")]
    public async Task ProjectHub_JoinProject_WhenNameIdentifierClaimMalformed_ThrowsHubException(string malformedClaim)
    {
        var fakeRepo = new FakeProjectAccessRepository();
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, malformedClaim),
            new Claim(ClaimTypes.Role, RoleNames.Student)
        ], "TestAuth"));

        var hub = CreateHub(fakeRepo, user);

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.JoinProject(Guid.NewGuid()));
        Assert.Equal("Project access is denied.", ex.Message);
    }

    [Fact]
    public async Task ProjectHub_JoinProject_WhenArbitraryUserHasNoAccess_ThrowsHubException()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var fakeRepo = new FakeProjectAccessRepository
        {
            ActiveStudentMembershipResult = false,
            CompanyAccessResult = false,
            LecturerAccessResult = false
        };

        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, RoleNames.Student)
        ], "TestAuth"));

        var groupManager = new MockGroupManager();
        var hub = CreateHub(fakeRepo, user, groupManager);

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.JoinProject(projectId));
        Assert.Equal("Project access is denied.", ex.Message);
        Assert.Empty(groupManager.AddedGroups);
    }

    [Fact]
    public async Task ProjectHub_JoinProject_WhenActiveStudentMember_SuccessfullyAddsToGroup()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var fakeRepo = new FakeProjectAccessRepository
        {
            ActiveStudentMembershipResult = true
        };

        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, RoleNames.Student)
        ], "TestAuth"));

        var groupManager = new MockGroupManager();
        var hub = CreateHub(fakeRepo, user, groupManager);

        await hub.JoinProject(projectId);

        var expectedGroup = $"project:{projectId:N}";
        Assert.Contains((hub.Context.ConnectionId, expectedGroup), groupManager.AddedGroups);
    }

    [Fact]
    public async Task ProjectHub_JoinProject_WhenInactiveStudentMember_ThrowsHubException()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var fakeRepo = new FakeProjectAccessRepository
        {
            ActiveStudentMembershipResult = false // Inactive member
        };

        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, RoleNames.Student)
        ], "TestAuth"));

        var groupManager = new MockGroupManager();
        var hub = CreateHub(fakeRepo, user, groupManager);

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.JoinProject(projectId));
        Assert.Equal("Project access is denied.", ex.Message);
        Assert.Empty(groupManager.AddedGroups);
    }

    [Fact]
    public async Task ProjectHub_JoinProject_WhenCompanyMemberHasAccess_SuccessfullyAddsToGroup()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var fakeRepo = new FakeProjectAccessRepository
        {
            CompanyAccessResult = true
        };

        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, RoleNames.Company)
        ], "TestAuth"));

        var groupManager = new MockGroupManager();
        var hub = CreateHub(fakeRepo, user, groupManager);

        await hub.JoinProject(projectId);

        var expectedGroup = $"project:{projectId:N}";
        Assert.Contains((hub.Context.ConnectionId, expectedGroup), groupManager.AddedGroups);
    }

    [Fact]
    public async Task ProjectHub_JoinProject_WhenCrossCompanyUserHasNoAccess_ThrowsHubException()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var fakeRepo = new FakeProjectAccessRepository
        {
            CompanyAccessResult = false // Different company
        };

        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, RoleNames.Company)
        ], "TestAuth"));

        var groupManager = new MockGroupManager();
        var hub = CreateHub(fakeRepo, user, groupManager);

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.JoinProject(projectId));
        Assert.Equal("Project access is denied.", ex.Message);
        Assert.Empty(groupManager.AddedGroups);
    }

    [Fact]
    public async Task ProjectHub_JoinProject_WhenAssignedActiveLecturer_SuccessfullyAddsToGroup()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var fakeRepo = new FakeProjectAccessRepository
        {
            LecturerAccessResult = true
        };

        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, RoleNames.Lecturer)
        ], "TestAuth"));

        var groupManager = new MockGroupManager();
        var hub = CreateHub(fakeRepo, user, groupManager);

        await hub.JoinProject(projectId);

        var expectedGroup = $"project:{projectId:N}";
        Assert.Contains((hub.Context.ConnectionId, expectedGroup), groupManager.AddedGroups);
    }

    [Fact]
    public async Task ProjectHub_JoinProject_WhenUnassignedLecturer_ThrowsHubException()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var fakeRepo = new FakeProjectAccessRepository
        {
            LecturerAccessResult = false
        };

        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, RoleNames.Lecturer)
        ], "TestAuth"));

        var groupManager = new MockGroupManager();
        var hub = CreateHub(fakeRepo, user, groupManager);

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.JoinProject(projectId));
        Assert.Equal("Project access is denied.", ex.Message);
        Assert.Empty(groupManager.AddedGroups);
    }

    [Theory]
    [InlineData(RoleNames.Admin)]
    [InlineData(RoleNames.SuperAdmin)]
    public async Task ProjectHub_JoinProject_WhenAdminOrSuperAdmin_BypassesRepositoryAndAddsToGroup(string adminRole)
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        // All repo predicates return false; admin should still be allowed
        var fakeRepo = new FakeProjectAccessRepository
        {
            ActiveStudentMembershipResult = false,
            CompanyAccessResult = false,
            LecturerAccessResult = false
        };

        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, adminRole)
        ], "TestAuth"));

        var groupManager = new MockGroupManager();
        var hub = CreateHub(fakeRepo, user, groupManager);

        await hub.JoinProject(projectId);

        var expectedGroup = $"project:{projectId:N}";
        Assert.Contains((hub.Context.ConnectionId, expectedGroup), groupManager.AddedGroups);
    }

    [Fact]
    public async Task ProjectHub_LeaveProject_RemovesConnectionFromGroup()
    {
        var projectId = Guid.NewGuid();
        var fakeRepo = new FakeProjectAccessRepository();
        var groupManager = new MockGroupManager();
        var hub = CreateHub(fakeRepo, user: null, groupManager);

        await hub.LeaveProject(projectId);

        var expectedGroup = $"project:{projectId:N}";
        Assert.Contains((hub.Context.ConnectionId, expectedGroup), groupManager.RemovedGroups);
    }

    [Fact]
    public void ProjectHub_ProjectGroupHelper_FormatsGuidCorrectly()
    {
        var guid = Guid.Parse("11223344-5566-7788-99aa-bbccddeeff00");
        var group = ProjectHub.ProjectGroup(guid);

        Assert.Equal("project:112233445566778899aabbccddeeff00", group);
    }

    #endregion

    #region Test Doubles / Helpers

    private static ProjectHub CreateHub(
        IProjectAccessRepository repo,
        ClaimsPrincipal? user,
        MockGroupManager? groupManager = null)
    {
        var hub = new ProjectHub(repo);
        var mockContext = new MockHubCallerContext(user);
        hub.Context = mockContext;
        hub.Groups = groupManager ?? new MockGroupManager();
        return hub;
    }

    private sealed class FakeProjectAccessRepository : IProjectAccessRepository
    {
        public bool ActiveStudentMembershipResult { get; set; }
        public bool CompanyAccessResult { get; set; }
        public bool LecturerAccessResult { get; set; }

        public Task<bool> HasActiveStudentMembershipAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(ActiveStudentMembershipResult);

        public Task<bool> HasCompanyAccessAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(CompanyAccessResult);

        public Task<bool> HasLecturerAccessAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(LecturerAccessResult);
    }

    private sealed class MockHubCallerContext : HubCallerContext
    {
        private readonly ClaimsPrincipal? _user;

        public MockHubCallerContext(ClaimsPrincipal? user)
        {
            _user = user;
            ConnectionId = "test-conn-" + Guid.NewGuid().ToString("N")[..8];
        }

        public override string ConnectionId { get; }
        public override string? UserIdentifier => _user?.FindFirstValue(ClaimTypes.NameIdentifier);
        public override ClaimsPrincipal? User => _user;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;

        public override void Abort() { }
    }

    private sealed class MockGroupManager : IGroupManager
    {
        public List<(string connectionId, string groupName)> AddedGroups { get; } = [];
        public List<(string connectionId, string groupName)> RemovedGroups { get; } = [];

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            AddedGroups.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            RemovedGroups.Add((connectionId, groupName));
            return Task.CompletedTask;
        }
    }

    #endregion
}
