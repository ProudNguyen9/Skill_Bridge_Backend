using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace DNTU.SkillBridge.IntegrationTests;

/// <summary>
/// E2E backend cho Chat + Mạng lưới theo dõi: hội thoại 1-1, tin nhắn chờ, block,
/// follow + bảng tin, phòng nhóm dự án, chống truy cập chéo (IDOR), báo cáo vi phạm.
/// </summary>
[Collection(CatalogApiCollection.Name)]
public sealed class ChatAndSocialEndpointTests(CatalogApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = CatalogApiJson.Options;
    private const string Password = "Password#123456";
    private const string SigningKey = "integration-tests-signing-key-with-at-least-sixty-four-characters-0001";

    private readonly string prefix = $"zc{Guid.NewGuid():N}"[..10];

    // ---------- Chat: hội thoại 1-1 ----------

    [Fact]
    public async Task DirectConversation_SendMessage_UnreadCountIncreases()
    {
        var student = await RegisterStudentAsync();
        var company = await RegisterCompanyAsync();
        var (studentUserId, companyUserId) = await GetUserIdsAsync(student, company);

        var open = await student.PostAsJsonAsync("/api/v1/chat/conversations/direct",
            new { targetUserId = companyUserId }, Json);
        Assert.Equal(HttpStatusCode.OK, open.StatusCode);
        var conversation = (await open.Content.ReadFromJsonAsync<Envelope<ConversationDto>>(Json))!.Data;
        Assert.Equal("DIRECT", conversation.Type);
        Assert.Equal(companyUserId, conversation.OtherParticipant!.UserId);

        // Mở lại cùng cặp người dùng → cùng hội thoại (không phân mảnh lịch sử).
        var openAgain = await student.PostAsJsonAsync("/api/v1/chat/conversations/direct",
            new { targetUserId = companyUserId }, Json);
        var conversationAgain = (await openAgain.Content.ReadFromJsonAsync<Envelope<ConversationDto>>(Json))!.Data;
        Assert.Equal(conversation.Id, conversationAgain.Id);

        var send = await student.PostAsJsonAsync($"/api/v1/chat/conversations/{conversation.Id}/messages",
            new { messageType = "TEXT", content = "Xin chào, tôi quan tâm đề tài của công ty." }, Json);
        Assert.Equal(HttpStatusCode.OK, send.StatusCode);
        var message = (await send.Content.ReadFromJsonAsync<Envelope<MessageDto>>(Json))!.Data;
        Assert.Equal("TEXT", message.MessageType);
        Assert.False(string.IsNullOrEmpty(message.SenderDisplayName));

        // Hai bên chưa có quan hệ → công ty (người nhận) thấy hội thoại ở tab Tin nhắn chờ.
        var inbox = await company.GetAsync("/api/v1/chat/conversations?tab=REQUESTS");
        var list = (await inbox.Content.ReadFromJsonAsync<Envelope<List<ConversationDto>>>(Json))!.Data;
        var mine = list.Single(item => item.Id == conversation.Id);
        Assert.Equal(1, mine.UnreadCount);
        Assert.Equal("Xin chào, tôi quan tâm đề tài của công ty.", mine.LastMessage!.Preview);

        // Chấp nhận hội thoại rồi mới hiện trong hộp thư chính.
        Assert.Equal(HttpStatusCode.OK,
            (await company.PostAsync($"/api/v1/chat/conversations/{conversation.Id}/accept", null)).StatusCode);
        var mainInbox = (await (await company.GetAsync("/api/v1/chat/conversations"))
            .Content.ReadFromJsonAsync<Envelope<List<ConversationDto>>>(Json))!.Data;
        Assert.Contains(mainInbox, item => item.Id == conversation.Id);

        var unreadTotal = (await (await company.GetAsync("/api/v1/chat/conversations/unread-count"))
            .Content.ReadFromJsonAsync<Envelope<int>>(Json))!.Data;
        Assert.Equal(1, unreadTotal);

        // Đánh dấu đã đọc → 0.
        Assert.Equal(HttpStatusCode.OK,
            (await company.PostAsJsonAsync($"/api/v1/chat/conversations/{conversation.Id}/read", new { }, Json)).StatusCode);
        var unreadAfter = (await (await company.GetAsync("/api/v1/chat/conversations/unread-count"))
            .Content.ReadFromJsonAsync<Envelope<int>>(Json))!.Data;
        Assert.Equal(0, unreadAfter);
    }

    [Fact]
    public async Task MessageRequest_StrangerGoesToPendingTab_ThenAccept()
    {
        var studentA = await RegisterStudentAsync();
        var studentB = await RegisterStudentAsync();
        var (_, userB) = await GetUserIdsAsync(studentA, studentB);

        // Hai sinh viên lạ (không chung dự án, chưa follow) → hội thoại vào Tin nhắn chờ của B.
        var open = await studentA.PostAsJsonAsync("/api/v1/chat/conversations/direct",
            new { targetUserId = userB }, Json);
        Assert.Equal(HttpStatusCode.OK, open.StatusCode);
        var conversation = (await open.Content.ReadFromJsonAsync<Envelope<ConversationDto>>(Json))!.Data;
        // Người gửi luôn ACCEPTED; trạng thái PENDING_REQUEST nằm ở phía người nhận.
        Assert.Equal("ACCEPTED", conversation.RequestStatus);

        Assert.Equal(HttpStatusCode.OK,
            (await studentA.PostAsJsonAsync($"/api/v1/chat/conversations/{conversation.Id}/messages",
                new { messageType = "TEXT", content = "Chào bạn, mình có thể ghép nhóm không?" }, Json)).StatusCode);

        // Tab Tin nhắn chờ của B chứa hội thoại; tab ALL không chứa.
        var requests = (await (await studentB.GetAsync("/api/v1/chat/conversations?tab=REQUESTS"))
            .Content.ReadFromJsonAsync<Envelope<List<ConversationDto>>>(Json))!.Data;
        Assert.Contains(requests, item => item.Id == conversation.Id);

        var all = (await (await studentB.GetAsync("/api/v1/chat/conversations"))
            .Content.ReadFromJsonAsync<Envelope<List<ConversationDto>>>(Json))!.Data;
        Assert.DoesNotContain(all, item => item.Id == conversation.Id);

        // Chấp nhận → xuất hiện trong hộp thư chính.
        Assert.Equal(HttpStatusCode.OK,
            (await studentB.PostAsync($"/api/v1/chat/conversations/{conversation.Id}/accept", null)).StatusCode);
        var allAfter = (await (await studentB.GetAsync("/api/v1/chat/conversations"))
            .Content.ReadFromJsonAsync<Envelope<List<ConversationDto>>>(Json))!.Data;
        Assert.Contains(allAfter, item => item.Id == conversation.Id);
    }

    [Fact]
    public async Task ReplyEditReactionDelete_FullMessageLifecycle()
    {
        var student = await RegisterStudentAsync();
        var company = await RegisterCompanyAsync();
        var (_, companyUserId) = await GetUserIdsAsync(student, company);
        var conversationId = await OpenDirectAsync(student, companyUserId);

        var send = await student.PostAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/messages",
            new { messageType = "TEXT", content = "Tin nhắn gốc" }, Json);
        var message = (await send.Content.ReadFromJsonAsync<Envelope<MessageDto>>(Json))!.Data;

        // Hai bên chưa quan hệ → công ty phải chấp nhận tin nhắn chờ trước khi trả lời.
        Assert.Equal(HttpStatusCode.OK,
            (await company.PostAsync($"/api/v1/chat/conversations/{conversationId}/accept", null)).StatusCode);

        // Reply.
        var reply = await company.PostAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/messages",
            new { messageType = "TEXT", content = "Trả lời trích dẫn", replyToMessageId = message.Id }, Json);
        Assert.Equal(HttpStatusCode.OK, reply.StatusCode);
        var replyMessage = (await reply.Content.ReadFromJsonAsync<Envelope<MessageDto>>(Json))!.Data;
        Assert.Equal(message.Id, replyMessage.ReplyToMessageId);
        Assert.NotNull(replyMessage.ReplyToPreview);

        // Reaction toggle của công ty.
        var reaction = await company.PostAsJsonAsync($"/api/v1/chat/messages/{message.Id}/reactions",
            new { emoji = "👍" }, Json);
        Assert.Equal(HttpStatusCode.OK, reaction.StatusCode);
        var reacted = (await reaction.Content.ReadFromJsonAsync<Envelope<MessageDto>>(Json))!.Data;
        var thumbUp = Assert.Single(reacted.Reactions);
        Assert.Equal("👍", thumbUp.Emoji);
        Assert.Equal(1, thumbUp.Count);

        // Toggle lần nữa → xóa reaction.
        var toggledOff = await company.PostAsJsonAsync($"/api/v1/chat/messages/{message.Id}/reactions",
            new { emoji = "👍" }, Json);
        var off = (await toggledOff.Content.ReadFromJsonAsync<Envelope<MessageDto>>(Json))!.Data;
        Assert.Empty(off.Reactions);

        // Edit trong 15 phút.
        var edit = await student.PatchAsJsonAsync($"/api/v1/chat/messages/{message.Id}",
            new { content = "Tin nhắn đã chỉnh sửa" }, Json);
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var edited = (await edit.Content.ReadFromJsonAsync<Envelope<MessageDto>>(Json))!.Data;
        Assert.True(edited.IsEdited);

        // Delete (soft).
        Assert.Equal(HttpStatusCode.NoContent,
            (await student.DeleteAsync($"/api/v1/chat/messages/{message.Id}")).StatusCode);
        var page = (await (await student.GetAsync($"/api/v1/chat/conversations/{conversationId}/messages"))
            .Content.ReadFromJsonAsync<Envelope<MessagesPageDto>>(Json))!.Data;
        var deleted = page.Messages.Single(m => m.Id == message.Id);
        Assert.True(deleted.IsDeleted);
        Assert.Equal(string.Empty, deleted.Content);
    }

    // ---------- Chat: chống truy cập chéo (IDOR) ----------

    [Fact]
    public async Task Idor_OutsiderCannotReadOrWrite()
    {
        var student = await RegisterStudentAsync();
        var company = await RegisterCompanyAsync();
        var outsider = await RegisterStudentAsync();
        var (_, companyUserId) = await GetUserIdsAsync(student, company);

        var conversationId = await OpenDirectAsync(student, companyUserId);
        Assert.Equal(HttpStatusCode.OK,
            (await student.PostAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/messages",
                new { messageType = "TEXT", content = "Riêng tư" }, Json)).StatusCode);

        // Người ngoài đọc lịch sử → 403.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await outsider.GetAsync($"/api/v1/chat/conversations/{conversationId}/messages")).StatusCode);
        // Người ngoài gửi tin → 403.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await outsider.PostAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/messages",
                new { messageType = "TEXT", content = " xen" }, Json)).StatusCode);
        // Người ngoài xem chi tiết hội thoại → 404.
        Assert.Equal(HttpStatusCode.NotFound,
            (await outsider.GetAsync($"/api/v1/chat/conversations/{conversationId}")).StatusCode);
    }

    // ---------- Chat: Block ----------

    [Fact]
    public async Task Block_StopsMessages_Follow_AndHidesInbox()
    {
        var student = await RegisterStudentAsync();
        var company = await RegisterCompanyAsync();
        var (studentUserId, companyUserId) = await GetUserIdsAsync(student, company);
        var conversationId = await OpenDirectAsync(student, companyUserId);

        // Follow trước khi block.
        Assert.Equal(HttpStatusCode.OK,
            (await student.PostAsync($"/api/v1/social/follows/{companyUserId}", null)).StatusCode);

        // Công ty chặn sinh viên.
        Assert.Equal(HttpStatusCode.OK,
            (await company.PostAsync($"/api/v1/social/blocks/{studentUserId}", null)).StatusCode);

        // Chặn hủy Follow hai chiều.
        var status = (await (await student.GetAsync($"/api/v1/social/users/{companyUserId}/follow-status"))
            .Content.ReadFromJsonAsync<Envelope<FollowStatusDto>>(Json))!.Data;
        Assert.False(status.IsFollowing);

        // Sinh viên không thể gửi tin mới.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await student.PostAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/messages",
                new { messageType = "TEXT", content = "spam?" }, Json)).StatusCode);

        // Sinh viên không thể Follow lại.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await student.PostAsync($"/api/v1/social/follows/{companyUserId}", null)).StatusCode);
    }

    // ---------- Chat: phòng nhóm dự án ----------

    [Fact]
    public async Task ProjectConversation_CompanyCanOpen_OutsiderForbidden()
    {
        var student = await RegisterStudentAsync();
        var company = await CreateVerifiedCompanyAsync();
        var admin = await CreateAdminClientAsync();
        var skillIds = await GetActiveSkillIdsAsync(1);
        var projectId = await PublishProjectAsync(company, admin, $"{prefix} Chat dự án", skillIds);

        var open = await company.PostAsync($"/api/v1/chat/conversations/project/{projectId}", null);
        Assert.Equal(HttpStatusCode.OK, open.StatusCode);
        var conversation = (await open.Content.ReadFromJsonAsync<Envelope<ConversationDto>>(Json))!.Data;
        Assert.Equal("PROJECT_GROUP", conversation.Type);
        Assert.Equal(projectId, conversation.ProjectId);

        // Sinh viên ngoài dự án → 403.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await student.PostAsync($"/api/v1/chat/conversations/project/{projectId}", null)).StatusCode);

        // Người ngoài đọc tin nhắn phòng dự án → 403.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await student.GetAsync($"/api/v1/chat/conversations/{conversation.Id}/messages")).StatusCode);
    }

    // ---------- Social: Follow & Feed ----------

    [Fact]
    public async Task Follow_Counts_Status_AndFeed()
    {
        var student = await RegisterStudentAsync();
        var company = await CreateVerifiedCompanyAsync();
        var admin = await CreateAdminClientAsync();
        var (studentUserId, companyUserId) = await GetUserIdsAsync(student, company);

        Assert.Equal(HttpStatusCode.OK,
            (await student.PostAsync($"/api/v1/social/follows/{companyUserId}", null)).StatusCode);

        // Follow idempotent.
        Assert.Equal(HttpStatusCode.OK,
            (await student.PostAsync($"/api/v1/social/follows/{companyUserId}", null)).StatusCode);

        var status = (await (await student.GetAsync($"/api/v1/social/users/{companyUserId}/follow-status"))
            .Content.ReadFromJsonAsync<Envelope<FollowStatusDto>>(Json))!.Data;
        Assert.True(status.IsFollowing);
        Assert.False(status.IsFollowedBy);
        Assert.False(status.IsMutual);
        // Số lượng tính trên hồ sơ của công ty (đối tượng đang xem).
        Assert.Equal(1, status.FollowersCount);
        Assert.Equal(0, status.FollowingCount);

        // Danh sách Đang theo dõi của chính sinh viên có 1 dòng là công ty.
        var following = (await (await student.GetAsync($"/api/v1/social/users/{studentUserId}/network?direction=FOLLOWING"))
            .Content.ReadFromJsonAsync<PageDto<NetworkUserDto>>(Json))!;
        Assert.Equal(1, following.Meta.TotalItems);
        Assert.Equal("COMPANY", following.Data[0].Role);

        // Network theo vai trò.
        var followers = (await (await student.GetAsync($"/api/v1/social/users/{companyUserId}/network?direction=FOLLOWERS"))
            .Content.ReadFromJsonAsync<PageDto<NetworkUserDto>>(Json))!;
        Assert.Equal(1, followers.Meta.TotalItems);
        // Người theo dõi công ty là sinh viên.
        Assert.Equal("STUDENT", followers.Data[0].Role);

        // Follow chính mình → 400; follow admin → 404.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await student.PostAsync($"/api/v1/social/follows/{studentUserId}", null)).StatusCode);

        // Doanh nghiệp đăng đề tài (admin duyệt) → sinh viên thấy PROJECT_PUBLISHED trong bảng tin.
        var skillIds = await GetActiveSkillIdsAsync(1);
        var projectId = await PublishProjectAsync(company, admin, $"{prefix} Bảng tin", skillIds);

        var feed = (await (await student.GetAsync("/api/v1/social/feed"))
            .Content.ReadFromJsonAsync<PageDto<FeedItemDto>>(Json))!;
        Assert.Contains(feed.Data, item => item.ActivityType == "PROJECT_PUBLISHED" && item.LinkUrl == $"/projects/{projectId}");

        // Unfollow → đếm về 0.
        Assert.Equal(HttpStatusCode.OK,
            (await student.DeleteAsync($"/api/v1/social/follows/{companyUserId}")).StatusCode);
        var statusAfter = (await (await student.GetAsync($"/api/v1/social/users/{companyUserId}/follow-status"))
            .Content.ReadFromJsonAsync<Envelope<FollowStatusDto>>(Json))!.Data;
        Assert.Equal(0, statusAfter.FollowersCount);
    }

    // ---------- Chat: báo cáo vi phạm + Admin kiểm duyệt ----------

    [Fact]
    public async Task ReportMessage_AdminResolvesWithWarning()
    {
        var student = await RegisterStudentAsync();
        var company = await RegisterCompanyAsync();
        var (_, companyUserId) = await GetUserIdsAsync(student, company);
        var conversationId = await OpenDirectAsync(student, companyUserId);

        var send = await student.PostAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/messages",
            new { messageType = "TEXT", content = "Mua hàng giá rẻ inbox ngoài !" }, Json);
        var message = (await send.Content.ReadFromJsonAsync<Envelope<MessageDto>>(Json))!.Data;

        Assert.Equal(HttpStatusCode.OK,
            (await company.PostAsJsonAsync("/api/v1/chat/reports",
                new { messageId = message.Id, reason = "Nội dung quảng cáo vi phạm quy tắc" }, Json)).StatusCode);

        // Báo cáo trùng → 409.
        Assert.Equal(HttpStatusCode.Conflict,
            (await company.PostAsJsonAsync("/api/v1/chat/reports",
                new { messageId = message.Id, reason = "Báo cáo lại" }, Json)).StatusCode);

        var admin = await CreateAdminClientAsync();
        var reports = (await (await admin.GetAsync("/api/v1/admin/chat/reports?status=OPEN"))
            .Content.ReadFromJsonAsync<PageDto<ReportDto>>(Json))!;
        var report = Assert.Single(reports.Data, r => r.Message!.Id == message.Id);
        Assert.Equal("Mua hàng giá rẻ inbox ngoài !", report.Message!.Content);

        Assert.Equal(HttpStatusCode.OK,
            (await admin.PostAsJsonAsync($"/api/v1/admin/chat/reports/{report.Id}/resolve",
                new { decision = "WARNED", note = "Cảnh cáo lần 1" }, Json)).StatusCode);

        var resolved = (await (await admin.GetAsync("/api/v1/admin/chat/reports?status=RESOLVED"))
            .Content.ReadFromJsonAsync<PageDto<ReportDto>>(Json))!;
        Assert.Contains(resolved.Data, r => r.Id == report.Id);
    }

    // ---------- Social: gợi ý kết nối ----------

    [Fact]
    public async Task Suggestions_SkillMatchedStudents()
    {
        var studentA = await RegisterStudentAsync();
        var studentB = await RegisterStudentAsync();
        var skillId = await GetLeastUsedSkillIdAsync();

        foreach (var client in new[] { studentA, studentB })
        {
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/v1/students/me/skills",
                new { skills = new object[] { new { skillId, level = "ADVANCED" } } }, Json)).StatusCode);
        }

        var suggestions = (await (await studentA.GetAsync("/api/v1/social/suggestions"))
            .Content.ReadFromJsonAsync<Envelope<List<SuggestionDto>>>(Json))!.Data;
        var (_, userB) = await GetUserIdsAsync(studentA, studentB);
        Assert.Contains(suggestions, s => s.UserId == userB && s.SharedCount >= 1);
    }

    // ---------- Helpers ----------

    private async Task<Guid> OpenDirectAsync(HttpClient requester, Guid targetUserId)
    {
        var open = await requester.PostAsJsonAsync("/api/v1/chat/conversations/direct",
            new { targetUserId }, Json);
        Assert.Equal(HttpStatusCode.OK, open.StatusCode);
        return (await open.Content.ReadFromJsonAsync<Envelope<ConversationDto>>(Json))!.Data.Id;
    }

    private async Task<(Guid StudentUserId, Guid OtherUserId)> GetUserIdsAsync(HttpClient a, HttpClient b)
    {
        Guid idA = default, idB = default;
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var emails = a.DefaultRequestHeaders.Authorization!.Parameter;
            var meA = await a.GetAsync("/api/v1/auth/me");
            var meB = await b.GetAsync("/api/v1/auth/me");
            var authA = (await meA.Content.ReadFromJsonAsync<Envelope<MeDto>>(Json))!.Data;
            var authB = (await meB.Content.ReadFromJsonAsync<Envelope<MeDto>>(Json))!.Data;
            idA = Guid.Parse(authA.UserId);
            idB = Guid.Parse(authB.UserId);
        });
        return (idA, idB);
    }

    private async Task<HttpClient> RegisterStudentAsync()
    {
        var email = $"{prefix}-sv-{Guid.NewGuid():N}@skillbridge.local";
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            displayName = "Sinh viên Chat",
            studentCode = "22" + Random.Shared.Next(100000, 999999),
            password = Password,
            accountType = "Student"
        }, Json);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<Envelope<AuthDto>>(Json))!.Data;
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        return client;
    }

    private async Task<HttpClient> RegisterCompanyAsync(string? companyName = null)
    {
        var email = $"{prefix}-dn-{Guid.NewGuid():N}@skillbridge.local";
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            displayName = "Công ty Chat",
            password = Password,
            accountType = "Company"
        }, Json);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<Envelope<AuthDto>>(Json))!.Data;
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        await client.PutAsJsonAsync("/api/v1/companies/me", new { name = companyName ?? $"Công ty Chat {Guid.NewGuid():N}" }, Json);
        return client;
    }

    private async Task<HttpClient> CreateVerifiedCompanyAsync()
    {
        var companyName = $"Công ty Chat {Guid.NewGuid():N}";
        var client = await RegisterCompanyAsync(companyName);
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var company = await dbContext.Companies.SingleAsync(item => item.Name == companyName);
            company.MarkVerified(DateTimeOffset.UtcNow, null);
            await dbContext.SaveChangesAsync();
        });
        return client;
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        Guid adminUserId = default;
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var user = new User($"admin-{Guid.NewGuid():N}@skillbridge.local", "Quản trị viên", "seed-hash");
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
            adminUserId = user.Id;
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(RoleNames.Admin, adminUserId));
        return client;
    }

    private static string CreateToken(string role, Guid userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("session_id", Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role)
        };
        foreach (var permission in DNTU.SkillBridge.Infrastructure.Persistence.Seed.IdentitySeed.GetPermissionsForRole(role))
        {
            claims.Add(new Claim("permission", permission));
        }

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

    private async Task<List<Guid>> GetActiveSkillIdsAsync(int count)
    {
        var anonymous = factory.CreateClient();
        var skills = (await anonymous.GetFromJsonAsync<ListEnvelope<CatalogSkillDto>>("/api/v1/catalog/skills", Json))!.Data;
        Assert.True(skills.Count >= count);
        return skills.Take(count).Select(skill => skill.Id).ToList();
    }

    /// <summary>Lấy skill CUỐI danh mục (ít bị test khác dùng) để test gợi ý không dính flaky.</summary>
    private async Task<Guid> GetLeastUsedSkillIdAsync()
    {
        var anonymous = factory.CreateClient();
        var skills = (await anonymous.GetFromJsonAsync<ListEnvelope<CatalogSkillDto>>("/api/v1/catalog/skills", Json))!.Data;
        Assert.NotEmpty(skills);
        return skills[^1].Id;
    }

    private async Task<Guid> PublishProjectAsync(HttpClient company, HttpClient admin, string title, IReadOnlyList<Guid> skillIds)
    {
        var created = await company.PostAsJsonAsync("/api/v1/company/projects", new
        {
            title,
            difficulty = "BEGINNER",
            workType = "ONSITE",
            durationWeeks = 4,
            expectedStudentCount = 3,
            minTeamSize = 2,
            maxTeamSize = 4,
            skills = skillIds.Select(skillId => new { skillId, requirementLevel = "MUST_HAVE", isRequired = true }).ToArray(),
            deliverables = new[] { new { name = "Báo cáo bàn giao", description = (string?)null } }
        }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var projectId = (await created.Content.ReadFromJsonAsync<Envelope<ProjectDto>>(Json))!.Data.Id;

        var updated = await company.PutAsJsonAsync($"/api/v1/company/projects/{projectId}",
            new
            {
                title,
                summary = $"Tóm tắt {title}",
                problemStatement = "Vấn đề thực tế cần giải quyết.",
                businessRequirements = "Yêu cầu nghiệp vụ rõ ràng.",
                technicalConstraints = "Ràng buộc kỹ thuật .NET.",
                difficulty = "BEGINNER",
                workType = "ONSITE",
                durationWeeks = 4,
                applicationDeadline = DateTimeOffset.UtcNow.AddDays(30),
                expectedStudentCount = 3,
                minTeamSize = 2,
                maxTeamSize = 4,
                allowanceAmount = 2_000_000,
                allowanceCurrency = "VND",
                skills = skillIds.Select(skillId => new { skillId, requirementLevel = "MUST_HAVE", isRequired = true }).ToArray(),
                deliverables = new[] { new { name = "Báo cáo bàn giao", description = (string?)null } }
            }, Json);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await company.PostAsync($"/api/v1/company/projects/{projectId}/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await admin.PostAsJsonAsync($"/api/v1/admin/projects/{projectId}/approve", new { }, Json)).StatusCode);
        return projectId;
    }

    // ---------- DTO shapes ----------

    private sealed record Envelope<T>(T Data);

    private sealed record ListEnvelope<T>(IReadOnlyList<T> Data);

    private sealed record PageDto<T>(IReadOnlyList<T> Data, MetaDto Meta);

    private sealed record MetaDto(int Page, int PageSize, int TotalItems, int TotalPages);

    private sealed record AuthDto(string AccessToken);

    private sealed record MeDto(string UserId);

    private sealed record ProjectDto(Guid Id);

    private sealed record CatalogSkillDto(Guid Id);

    private sealed record ConversationDto(
        Guid Id,
        string Type,
        Guid? ProjectId,
        OtherUserDto? OtherParticipant,
        LastMessageDto? LastMessage,
        int UnreadCount,
        string? RequestStatus);

    private sealed record OtherUserDto(Guid UserId, string DisplayName, bool IsOnline);

    private sealed record LastMessageDto(Guid Id, string Preview, Guid SenderUserId, string MessageType, DateTimeOffset CreatedAt);

    private sealed record MessageDto(
        Guid Id,
        Guid ConversationId,
        Guid SenderUserId,
        string SenderDisplayName,
        string MessageType,
        string Content,
        Guid? ReplyToMessageId,
        string? ReplyToPreview,
        bool IsEdited,
        bool IsDeleted,
        IReadOnlyList<ReactionDto> Reactions,
        DateTimeOffset CreatedAt);

    private sealed record ReactionDto(string Emoji, int Count, IReadOnlyList<Guid> UserIds);

    private sealed record MessagesPageDto(IReadOnlyList<MessageDto> Messages, bool HasMore);

    private sealed record FollowStatusDto(bool IsFollowing, bool IsFollowedBy, bool IsMutual, int FollowersCount, int FollowingCount);

    private sealed record ReportDto(Guid Id, MessageDto? Message, string Reason, string Status, Guid ReporterUserId);

    private sealed record SuggestionDto(Guid UserId, string DisplayName, string Role, string Reason, int SharedCount, bool IsFollowing);

    private sealed record NetworkUserDto(Guid UserId, string DisplayName, string Role, DateTimeOffset FollowedAt);

    private sealed record FeedItemDto(Guid Id, string ActivityType, string Title, string Summary, string LinkUrl, DateTimeOffset CreatedAt);
}
