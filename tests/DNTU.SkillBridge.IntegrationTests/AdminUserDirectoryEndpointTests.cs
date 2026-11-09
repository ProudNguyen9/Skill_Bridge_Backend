using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DNTU.SkillBridge.Application.Administration;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace DNTU.SkillBridge.IntegrationTests;

[Collection(CatalogApiCollection.Name)]
public sealed class AdminUserDirectoryEndpointTests(CatalogApiFactory factory)
{
    private const string Endpoint = "/api/v1/admin/users";
    private const string SigningKey = "integration-tests-signing-key-with-at-least-sixty-four-characters-0001";
    private static readonly JsonSerializerOptions Json = CatalogApiJson.Options;

    [Theory]
    [InlineData(RoleNames.Admin)]
    [InlineData(RoleNames.SuperAdmin)]
    public async Task AdminRoles_GetPagedEnvelope_WithOnlySafeFields_AndRealMemberships(string role)
    {
        var fixture = await SeedAsync();
        using var client = CreateClient(role);
        using var response = await client.GetAsync($"{Endpoint}?Search={fixture.Marker}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal(["data", "meta"], root.EnumerateObject().Select(property => property.Name).Order().ToArray());
        var result = root.Deserialize<PagedResponse<AdminUserListItemResponse>>(Json)!;
        Assert.Equal(new PageMetadata(1, 20, 5, 1), result.Meta);
        Assert.Equal(5, result.Data.Count);
        foreach (var row in root.GetProperty("data").EnumerateArray())
        {
            Assert.Equal(["createdAt", "displayName", "email", "isActive", "roles", "userId"],
                row.EnumerateObject().Select(property => property.Name).Order().ToArray());
            Assert.Equal(JsonValueKind.Array, row.GetProperty("roles").ValueKind);
        }
        var multiRole = Assert.Single(result.Data, item => item.UserId == fixture.MultiRoleId);
        Assert.Equal([RoleNames.Admin, RoleNames.Lecturer, RoleNames.Student], multiRole.Roles);
        Assert.Equal(fixture.CreatedAt, multiRole.CreatedAt);
        Assert.Empty(Assert.Single(result.Data, item => item.UserId == fixture.NoRoleId).Roles);
        Assert.Contains(result.Data, item => !item.IsActive);
        var payload = document.RootElement.GetRawText();
        Assert.DoesNotContain("secret-password-hash", payload);
        Assert.DoesNotContain("private-google-subject", payload);
        // The strict six-field assertion also excludes lastLogin, passwordHash, normalizedEmail,
        // Google identity, verification state, sessions and every token/credential field.
    }

    [Fact]
    public async Task OmittedOrWhitespaceSearch_ReturnsDefaultPageAcrossAllUsers()
    {
        await SeedAsync();
        using var client = CreateClient(RoleNames.Admin);
        await using var db = factory.CreateDbContext();
        var total = await db.Users.CountAsync();
        foreach (var query in new[] { string.Empty, "Search=%20%20" })
        {
            var result = await ListAsync(client, query);
            Assert.Equal(PageMetadata.Create(1, 20, total), result.Meta);
            Assert.Equal(Math.Min(20, total), result.Data.Count);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Search_MatchesEmailOrDisplayName_CaseInsensitively_AndTrims(bool byDisplayName)
    {
        var fixture = await SeedAsync();
        using var client = CreateClient(RoleNames.Admin);
        var search = byDisplayName ? $"NameOnly-{fixture.Marker}" : $"EmailOnly-{fixture.Marker}";
        foreach (var term in new[] { search.ToUpperInvariant(), search.ToLowerInvariant(), $"  {search}  " })
        {
            var result = await ListAsync(client, $"Search={Uri.EscapeDataString(term)}");
            Assert.Equal(byDisplayName ? fixture.NoRoleId : fixture.CompanyId, Assert.Single(result.Data).UserId);
            Assert.Equal(new PageMetadata(1, 20, 1, 1), result.Meta);
        }
    }

    [Theory]
    [InlineData(RoleNames.Student, 2)]
    [InlineData(RoleNames.Lecturer, 1)]
    [InlineData(RoleNames.Company, 1)]
    [InlineData(RoleNames.Admin, 1)]
    [InlineData(RoleNames.SuperAdmin, 1)]
    public async Task RoleFilter_UsesExactNormalizedMembership_NotSingleRoleOrSubstring(string role, int count)
    {
        var fixture = await SeedAsync();
        using var client = CreateClient(RoleNames.Admin);
        var result = await ListAsync(client,
            $"Search={fixture.Marker}&Role={Uri.EscapeDataString($"  {role.ToLowerInvariant()}  ")}");
        Assert.Equal(count, result.Meta.TotalItems);
        Assert.Equal(count, result.Data.Count);
        Assert.All(result.Data, item => Assert.Contains(role, item.Roles));
        if (role is RoleNames.Student or RoleNames.Lecturer or RoleNames.Admin)
        {
            Assert.Contains(result.Data, item => item.UserId == fixture.MultiRoleId);
            Assert.Equal(3, Assert.Single(result.Data, item => item.UserId == fixture.MultiRoleId).Roles.Length);
        }
    }

    [Theory]
    [InlineData(null, 5)]
    [InlineData(true, 4)]
    [InlineData(false, 1)]
    public async Task IsActiveFilter_IsOptional_AndIncludesInactiveAccounts(bool? isActive, int count)
    {
        var fixture = await SeedAsync();
        using var client = CreateClient(RoleNames.SuperAdmin);
        var result = await ListAsync(client,
            $"Search={fixture.Marker}" + (isActive.HasValue ? $"&IsActive={isActive}" : string.Empty));
        Assert.Equal(count, result.Meta.TotalItems);
        Assert.Equal(count, result.Data.Count);
        if (isActive.HasValue) Assert.All(result.Data, item => Assert.Equal(isActive.Value, item.IsActive));
        else Assert.Contains(result.Data, item => !item.IsActive);
    }

    [Fact]
    public async Task CombinedFilters_RunBeforeCountAndPaging_AndRetainAllRoles()
    {
        var fixture = await SeedAsync();
        using var client = CreateClient(RoleNames.Admin);
        var query = $"Search={fixture.Marker}&Role=student&IsActive=true&PageSize=1";
        var first = await ListAsync(client, query + "&Page=1");
        Assert.Equal(new PageMetadata(1, 1, 1, 1), first.Meta);
        Assert.Equal(fixture.MultiRoleId, Assert.Single(first.Data).UserId);
        Assert.Equal(3, first.Data.Single().Roles.Length);
        var second = await ListAsync(client, query + "&Page=2");
        Assert.Empty(second.Data);
        Assert.Equal(new PageMetadata(2, 1, 1, 1), second.Meta);
    }

    [Fact]
    public async Task Paging_IsStable_ByCreatedAtDescendingThenIdAscending_WithTies()
    {
        var fixture = await SeedAsync();
        Guid[] expected;
        await using (var db = factory.CreateDbContext())
        {
            expected = await db.Users.Where(user => user.Email.Contains(fixture.Marker))
                .OrderByDescending(user => user.CreatedAt).ThenBy(user => user.Id)
                .Select(user => user.Id).ToArrayAsync();
        }
        using var client = CreateClient(RoleNames.Admin);
        var actual = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            var result = await ListAsync(client, $"Search={fixture.Marker}&Page={page}&PageSize=2");
            Assert.Equal(new PageMetadata(page, 2, 5, 3), result.Meta);
            actual.AddRange(result.Data.Select(item => item.UserId));
            var repeated = await ListAsync(client, $"Search={fixture.Marker}&Page={page}&PageSize=2");
            Assert.Equal(result.Data.Select(item => item.UserId), repeated.Data.Select(item => item.UserId));
        }
        Assert.Equal(expected, actual);
        Assert.Equal(5, actual.Distinct().Count());
    }

    [Fact]
    public async Task NoMatch_AndVeryLargePage_ReturnEmptyPagedEnvelopes()
    {
        var fixture = await SeedAsync();
        using var client = CreateClient(RoleNames.Admin);
        var missing = await ListAsync(client, $"Search=missing-{Guid.NewGuid():N}");
        Assert.Empty(missing.Data);
        Assert.Equal(new PageMetadata(1, 20, 0, 0), missing.Meta);
        var beyond = await ListAsync(client, $"Search={fixture.Marker}&Page=2147483647&PageSize=100");
        Assert.Empty(beyond.Data);
        Assert.Equal(new PageMetadata(int.MaxValue, 100, 5, 1), beyond.Meta);
    }

    [Theory]
    [InlineData("Page=0")]
    [InlineData("Page=-1")]
    [InlineData("Page=no")]
    [InlineData("Page=2147483648")]
    [InlineData("PageSize=0")]
    [InlineData("PageSize=-1")]
    [InlineData("PageSize=101")]
    [InlineData("PageSize=no")]
    [InlineData("Role=")]
    [InlineData("Role=unknown")]
    [InlineData("Role=SUPER")]
    [InlineData("Role=ADMIN,STUDENT")]
    [InlineData("Role=%20%20")]
    [InlineData("IsActive=invalid")]
    public async Task InvalidParameters_Return400(string query)
    {
        using var client = CreateClient(RoleNames.Admin);
        using var response = await client.GetAsync($"{Endpoint}?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(200, HttpStatusCode.OK)]
    [InlineData(201, HttpStatusCode.BadRequest)]
    public async Task SearchLength_IsBoundedAt200(int length, HttpStatusCode expected)
    {
        using var client = CreateClient(RoleNames.Admin);
        using var response = await client.GetAsync($"{Endpoint}?Search={new string('x', length)}");
        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(RoleNames.Student)]
    [InlineData(RoleNames.Company)]
    [InlineData(RoleNames.Lecturer)]
    [InlineData("UNKNOWN")]
    [InlineData("admin")]
    [InlineData(null)]
    public async Task NonAdmins_Return403_EvenWithAllPermissionClaims(string? role)
    {
        using var client = CreateClient(role, withPermissions: true);
        using var response = await client.GetAsync(Endpoint);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task WhitespaceSearchLength_IsValidatedBeforeBindingNormalization()
    {
        using var client = CreateClient(RoleNames.Admin);
        using var response = await client.GetAsync($"{Endpoint}?Search={Uri.EscapeDataString(new string(' ', 201))}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(Endpoint);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<PagedResponse<AdminUserListItemResponse>> ListAsync(HttpClient client, string query)
    {
        using var response = await client.GetAsync($"{Endpoint}?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PagedResponse<AdminUserListItemResponse>>(Json))!;
    }

    private HttpClient CreateClient(string? role, bool withPermissions = false)
    {
        // Refuse any non-LocalDB / non-test database before tests seed or query data.
        AssertIsolatedLocalDb();
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) };
        if (role is not null) claims.Add(new Claim(ClaimTypes.Role, role));
        if (withPermissions) claims.AddRange(PermissionNames.All.Select(permission => new Claim("permission", permission)));
        var token = new JwtSecurityToken("DNTU.SkillBridge.Api", "DNTU.SkillBridge.Web", claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    private void AssertIsolatedLocalDb()
    {
        var connection = new SqlConnectionStringBuilder(factory.ConnectionString);
        Assert.StartsWith("(localdb)\\", connection.DataSource, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("skillbridge_tests_", connection.InitialCatalog);
    }

    private async Task<DirectoryFixture> SeedAsync()
    {
        AssertIsolatedLocalDb();
        var marker = $"directory-{Guid.NewGuid():N}";
        var createdAt = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await using var db = factory.CreateDbContext();
        var roles = await db.Roles.ToDictionaryAsync(role => role.Name, role => role.Id);
        var multi = new User($"multi-{marker}@example.test", "Multi role user", "secret-password-hash");
        multi.AssignRole(roles[RoleNames.Student]);
        multi.AssignRole(roles[RoleNames.Lecturer]);
        multi.AssignRole(roles[RoleNames.Admin]);
        multi.LinkGoogleIdentity($"private-google-subject-{marker}");
        multi.SetEmailVerified();
        var company = new User($"EmailOnly-{marker}@example.test", "Company member", "secret-password-hash");
        company.AssignRole(roles[RoleNames.Company]);
        var inactive = new User($"inactive-{marker}@example.test", "Inactive student", "secret-password-hash");
        inactive.AssignRole(roles[RoleNames.Student]);
        inactive.Deactivate();
        var super = new User($"super-{marker}@example.test", "Super administrator", "secret-password-hash");
        super.AssignRole(roles[RoleNames.SuperAdmin]);
        var noRole = new User($"unassigned-{marker}@example.test", $"NameOnly-{marker}", "secret-password-hash");
        db.Users.AddRange(multi, company, inactive, super, noRole);
        await db.SaveChangesAsync();
        await db.Users.Where(user => user.Email.Contains(marker))
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.CreatedAt, createdAt));
        await db.Users.Where(user => user.Id == company.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.CreatedAt, createdAt.AddDays(1)));
        return new DirectoryFixture(marker, multi.Id, company.Id, noRole.Id, createdAt);
    }

    private sealed record DirectoryFixture(string Marker, Guid MultiRoleId, Guid CompanyId, Guid NoRoleId, DateTimeOffset CreatedAt);
}
