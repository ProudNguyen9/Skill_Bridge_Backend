using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.IntegrationTests;

[Collection(CatalogApiCollection.Name)]
public sealed class AuthRoleLoginTests(CatalogApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = CatalogApiJson.Options;
    private const string Password = "AuthFixture#2026";

    [Fact]
    public async Task Login_WithValidAccountForEveryRole_ReturnsRoleFromMe()
    {
        var roles = new[]
        {
            RoleNames.Student,
            RoleNames.Company,
            RoleNames.Lecturer,
            RoleNames.Admin,
            RoleNames.SuperAdmin
        };

        foreach (var roleName in roles)
        {
            var email = $"auth-{roleName.ToLowerInvariant()}-{Guid.NewGuid():N}@skillbridge.local";
            await SeedAccountAsync(email, roleName);

            using var client = factory.CreateClient();
            using var loginResponse = await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new { email, password = Password },
                Json);

            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
            using var loginDocument = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
            var accessToken = loginDocument.RootElement
                .GetProperty("data")
                .GetProperty("accessToken")
                .GetString();
            Assert.False(string.IsNullOrWhiteSpace(accessToken));

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var meResponse = await client.GetAsync("/api/v1/auth/me");

            Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
            using var meDocument = JsonDocument.Parse(await meResponse.Content.ReadAsStringAsync());
            var returnedRoles = meDocument.RootElement
                .GetProperty("data")
                .GetProperty("roles")
                .EnumerateArray()
                .Select(role => role.GetString())
                .ToArray();
            Assert.Contains(roleName, returnedRoles);
        }
    }

    private async Task SeedAccountAsync(string email, string roleName)
    {
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var role = await dbContext.Roles.SingleAsync(item => item.NormalizedName == roleName);
            var user = new User(email, $"Fixture {roleName}", string.Empty);
            user.SetPasswordHash(new PasswordHasher<User>().HashPassword(user, Password));
            user.SetEmailVerified();
            user.AssignRole(role.Id);
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
        });
    }
}
