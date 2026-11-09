using System.Security.Claims;
using System.Text.Json;
using DNTU.SkillBridge.Api.Realtime;
using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Application.Commitments;
using DNTU.SkillBridge.Application.Milestones;
using DNTU.SkillBridge.Application.Workspaces;
using DNTU.SkillBridge.Domain.Commitments;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Milestones;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Domain.Workspaces;
using ProjectDifficulty = DNTU.SkillBridge.Domain.Catalog.ProjectDifficulty;
using ProjectWorkType = DNTU.SkillBridge.Domain.Catalog.ProjectWorkType;
using ProjectTaskStatus = DNTU.SkillBridge.Domain.Workspaces.ProjectTaskStatus;
using ProjectTaskPriority = DNTU.SkillBridge.Domain.Workspaces.ProjectTaskPriority;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Xunit;

namespace DNTU.SkillBridge.FunctionalTests;

public sealed class M3RealtimeAndCommitmentLifecycleAdversarialTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    #region 1. Group Naming Consistency Tests

    [Theory]
    [InlineData("11223344-5566-7788-99aa-bbccddeeff00", "project:112233445566778899aabbccddeeff00")]
    [InlineData("AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE", "project:aaaaaaaabbbbccccddddeeeeeeeeeeee")]
    [InlineData("01a04d2c-8c4a-4e1b-8c2a-123456789abc", "project:01a04d2c8c4a4e1b8c2a123456789abc")]
    public void GroupNaming_ProjectGroup_MatchesExplicitStringFormat(string inputGuidStr, string expectedGroup)
    {
        var guid = Guid.Parse(inputGuidStr);

        var resultFromHelper = ProjectHub.ProjectGroup(guid);
        var resultFromExplicitFormat = "project:" + guid.ToString("N");

        Assert.Equal(expectedGroup, resultFromHelper);
        Assert.Equal(expectedGroup, resultFromExplicitFormat);
        Assert.Equal(resultFromExplicitFormat, resultFromHelper);
    }

    [Fact]
    public void GroupNaming_ProjectGroup_DynamicGuid_MatchesExplicitStringFormat()
    {
        var guid = Guid.NewGuid();
        var resultFromHelper = ProjectHub.ProjectGroup(guid);
        var resultFromExplicitFormat = "project:" + guid.ToString("N");

        Assert.Equal(resultFromExplicitFormat, resultFromHelper);
    }

    #endregion

    #region 2. Event Payload Types & Names Across ProjectRealtimeNotifier and Frontend Contracts

    [Fact]
    public async Task RealtimeNotifier_AllNineEvents_EmitCorrectGroupAndPayloadContract()
    {
        var projectId = Guid.NewGuid();
        var expectedGroup = ProjectHub.ProjectGroup(projectId);
        var mockHubClients = new RecordingHubClients();
        var mockHubContext = new MockHubContext(mockHubClients);

        var notifier = new ProjectRealtimeNotifier(mockHubContext);

        // 1. TaskCreated
        var taskResponse = new ProjectTaskResponse(
            Guid.NewGuid(), projectId, "Implement Realtime Tests", "Description",
            ProjectTaskStatus.TODO, ProjectTaskPriority.HIGH, Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(3), 0, Guid.NewGuid(), DateTimeOffset.UtcNow);

        await notifier.NotifyTaskCreatedAsync(projectId, taskResponse);
        AssertEventSent(mockHubClients, expectedGroup, "TaskCreated", payload =>
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Assert.True(root.TryGetProperty("id", out _));
            Assert.True(root.TryGetProperty("projectId", out _));
            Assert.True(root.TryGetProperty("title", out _));
            Assert.True(root.TryGetProperty("status", out _));
            Assert.True(root.TryGetProperty("priority", out _));
            Assert.True(root.TryGetProperty("version", out _));
        });

        // 2. TaskUpdated
        await notifier.NotifyTaskUpdatedAsync(projectId, taskResponse);
        AssertEventSent(mockHubClients, expectedGroup, "TaskUpdated", payload =>
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("id", out _));
        });

        // 3. TaskMoved
        await notifier.NotifyTaskMovedAsync(projectId, taskResponse);
        AssertEventSent(mockHubClients, expectedGroup, "TaskMoved", payload =>
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("status", out _));
        });

        // 4. TaskDeleted
        var deletedTaskId = Guid.NewGuid();
        await notifier.NotifyTaskDeletedAsync(projectId, deletedTaskId);
        AssertEventSent(mockHubClients, expectedGroup, "TaskDeleted", payload =>
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("taskId", out var val));
            Assert.Equal(deletedTaskId.ToString(), val.GetString());
        });

        // 5. TaskCommentAdded
        var commentResponse = new TaskCommentResponse(
            Guid.NewGuid(), taskResponse.Id, Guid.NewGuid(), "Great progress!", DateTimeOffset.UtcNow);
        await notifier.NotifyCommentAddedAsync(projectId, commentResponse);
        AssertEventSent(mockHubClients, expectedGroup, "TaskCommentAdded", payload =>
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Assert.True(root.TryGetProperty("id", out _));
            Assert.True(root.TryGetProperty("taskId", out var tid));
            Assert.Equal(taskResponse.Id.ToString(), tid.GetString());
            Assert.True(root.TryGetProperty("authorUserId", out _));
            Assert.True(root.TryGetProperty("content", out var c));
            Assert.Equal("Great progress!", c.GetString());
            Assert.True(root.TryGetProperty("createdAt", out _));
        });

        // 6. TaskCommentDeleted
        var commentId = Guid.NewGuid();
        await notifier.NotifyCommentDeletedAsync(projectId, taskResponse.Id, commentId);
        AssertEventSent(mockHubClients, expectedGroup, "TaskCommentDeleted", payload =>
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Assert.True(root.TryGetProperty("taskId", out var tid));
            Assert.Equal(taskResponse.Id.ToString(), tid.GetString());
            Assert.True(root.TryGetProperty("commentId", out var cid));
            Assert.Equal(commentId.ToString(), cid.GetString());
        });

        // 7. ChecklistItemUpdated
        var checklistResponse = new TaskChecklistItemResponse(
            Guid.NewGuid(), taskResponse.Id, "Write Unit Tests", true, 1);
        await notifier.NotifyChecklistUpdatedAsync(projectId, checklistResponse);
        AssertEventSent(mockHubClients, expectedGroup, "ChecklistItemUpdated", payload =>
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Assert.True(root.TryGetProperty("id", out _));
            Assert.True(root.TryGetProperty("taskId", out var tid));
            Assert.Equal(taskResponse.Id.ToString(), tid.GetString());
            Assert.True(root.TryGetProperty("title", out var title));
            Assert.Equal("Write Unit Tests", title.GetString());
            Assert.True(root.TryGetProperty("isCompleted", out var comp));
            Assert.True(comp.GetBoolean());
            Assert.True(root.TryGetProperty("sortOrder", out var so));
            Assert.Equal(1, so.GetInt32());
        });

        // 8. MilestoneUpdated
        var milestoneResponse = new MilestoneResponse(
            Guid.NewGuid(), projectId, "Sprint 2 Finalization", "Final sprint wrap up",
            2, DateTimeOffset.UtcNow.AddDays(7), MilestoneStatus.IN_PROGRESS, false,
            Guid.NewGuid(), DateTimeOffset.UtcNow, [], []);
        await notifier.NotifyMilestoneUpdatedAsync(projectId, milestoneResponse);
        AssertEventSent(mockHubClients, expectedGroup, "MilestoneUpdated", payload =>
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Assert.True(root.TryGetProperty("id", out _));
            Assert.True(root.TryGetProperty("projectId", out _));
            Assert.True(root.TryGetProperty("status", out _));
            Assert.True(root.TryGetProperty("sequence", out _));
        });

        // 9. ProjectStatusChanged
        await notifier.NotifyProjectStatusChangedAsync(projectId, "IN_PROGRESS");
        AssertEventSent(mockHubClients, expectedGroup, "ProjectStatusChanged", payload =>
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Assert.True(root.TryGetProperty("projectId", out var pid));
            Assert.Equal(projectId.ToString(), pid.GetString());
            Assert.True(root.TryGetProperty("status", out var st));
            Assert.Equal("IN_PROGRESS", st.GetString());
        });
    }

    private static void AssertEventSent(RecordingHubClients clients, string expectedGroup, string expectedMethod, Action<object?> validatePayload)
    {
        var found = clients.SentEvents.Any(e => e.Group == expectedGroup && e.Method == expectedMethod);
        Assert.True(found, $"Event {expectedMethod} was not sent to group {expectedGroup}");
        var record = clients.SentEvents.Last(e => e.Group == expectedGroup && e.Method == expectedMethod);
        validatePayload(record.Arg);
    }

    #endregion

    #region 3. ProjectHub Edge Cases (Empty GUID, Non-Member, Admin)

    [Fact]
    public async Task ProjectHub_JoinProject_EmptyGuid_ForStudentWithoutAccess_ThrowsHubException()
    {
        var fakeRepo = new StubProjectAccessRepository(hasStudentAccess: false, hasCompanyAccess: false, hasLecturerAccess: false);
        var hub = CreateHub(fakeRepo, userId: Guid.NewGuid(), role: RoleNames.Student);

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.JoinProject(Guid.Empty));
        Assert.Equal("Project access is denied.", ex.Message);
    }

    [Fact]
    public async Task ProjectHub_JoinProject_NonMemberUser_ThrowsHubException()
    {
        var fakeRepo = new StubProjectAccessRepository(hasStudentAccess: false, hasCompanyAccess: false, hasLecturerAccess: false);
        var hub = CreateHub(fakeRepo, userId: Guid.NewGuid(), role: RoleNames.Student);

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.JoinProject(Guid.NewGuid()));
        Assert.Equal("Project access is denied.", ex.Message);
    }

    [Fact]
    public async Task ProjectHub_JoinProject_Admin_BypassesMembershipAndJoinsGroup()
    {
        var projectId = Guid.NewGuid();
        var fakeRepo = new StubProjectAccessRepository(hasStudentAccess: false, hasCompanyAccess: false, hasLecturerAccess: false);
        var groupManager = new MockGroupManager();
        var hub = CreateHub(fakeRepo, userId: Guid.NewGuid(), role: RoleNames.Admin, groupManager: groupManager);

        await hub.JoinProject(projectId);

        var expectedGroup = ProjectHub.ProjectGroup(projectId);
        Assert.Contains((hub.Context.ConnectionId, expectedGroup), groupManager.AddedGroups);
    }

    #endregion

    #region 4. Commitment Lifecycle & Already In-Progress Edge Cases

    [Fact]
    public async Task CommitmentConfirm_WhenProjectAlreadyInProgress_DoesNotAdvanceAgain_AndDoesNotBroadcastTwice()
    {
        var studentUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var project = CreateTestProject(projectId, ProjectStatus.IN_PROGRESS);
        var commitment = new ProjectCommitment(projectId, studentId, DateTimeOffset.UtcNow.AddDays(7));

        var fakeRepo = new StubCommitmentRepository
        {
            StudentId = studentId,
            Project = project,
            Commitment = commitment,
            HasMembership = false
        };
        var fakeUow = new StubUnitOfWork();
        var recordingNotifier = new RecordingRealtimeNotifier();

        var service = new CommitmentService(fakeRepo, fakeUow, recordingNotifier);

        // ACT
        var result = await service.ConfirmAsync(studentUserId, projectId, CancellationToken.None);

        // ASSERT
        Assert.NotNull(result);
        Assert.Equal(CommitmentStatus.CONFIRMED, commitment.Status);
        // Project status remains IN_PROGRESS (not mutated)
        Assert.Equal(ProjectStatus.IN_PROGRESS, project.Status);
        // No status change notification emitted because project was already IN_PROGRESS!
        Assert.Empty(recordingNotifier.StatusChangedBroadcasts);
        // Student member added
        Assert.Contains(fakeRepo.AddedMembers, m => m.ProjectId == projectId && m.StudentId == studentId);
        // Activity logged
        Assert.Contains(fakeRepo.AddedActivities, a => a.ProjectId == projectId && a.EventType == "COMMITMENT_CONFIRMED");
    }

    [Fact]
    public async Task CommitmentConfirm_FirstConfirmation_AdvancesProjectToInProgress_AndBroadcastsRealtime()
    {
        var studentUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var project = CreateTestProject(projectId, ProjectStatus.APPROVED);
        var commitment = new ProjectCommitment(projectId, studentId, DateTimeOffset.UtcNow.AddDays(7));

        var fakeRepo = new StubCommitmentRepository
        {
            StudentId = studentId,
            Project = project,
            Commitment = commitment,
            HasMembership = false
        };
        var fakeUow = new StubUnitOfWork();
        var recordingNotifier = new RecordingRealtimeNotifier();

        var service = new CommitmentService(fakeRepo, fakeUow, recordingNotifier);

        // ACT
        var result = await service.ConfirmAsync(studentUserId, projectId, CancellationToken.None);

        // ASSERT
        Assert.NotNull(result);
        Assert.Equal(CommitmentStatus.CONFIRMED, commitment.Status);
        // Project status advanced to IN_PROGRESS!
        Assert.Equal(ProjectStatus.IN_PROGRESS, project.Status);
        // Status change notification emitted!
        Assert.Single(recordingNotifier.StatusChangedBroadcasts);
        var broadcast = recordingNotifier.StatusChangedBroadcasts[0];
        Assert.Equal(projectId, broadcast.ProjectId);
        Assert.Equal("IN_PROGRESS", broadcast.Status);
    }

    [Fact]
    public async Task CommitmentConfirm_WhenAlreadyConfirmed_ReturnsNull_PreventingDoubleConfirm()
    {
        var studentUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var project = CreateTestProject(projectId, ProjectStatus.IN_PROGRESS);
        var commitment = new ProjectCommitment(projectId, studentId, DateTimeOffset.UtcNow.AddDays(7));
        commitment.Confirm(DateTimeOffset.UtcNow); // Already confirmed!

        var fakeRepo = new StubCommitmentRepository
        {
            StudentId = studentId,
            Project = project,
            Commitment = commitment
        };
        var fakeUow = new StubUnitOfWork();
        var recordingNotifier = new RecordingRealtimeNotifier();

        var service = new CommitmentService(fakeRepo, fakeUow, recordingNotifier);

        // ACT
        var result = await service.ConfirmAsync(studentUserId, projectId, CancellationToken.None);

        // ASSERT: null returned because commitment.Confirm throws InvalidOperationException when not PENDING
        Assert.Null(result);
        Assert.Empty(recordingNotifier.StatusChangedBroadcasts);
    }

    [Fact]
    public async Task CommitmentConfirm_WhenConfirmationDeadlineExpired_ReturnsNull()
    {
        var studentUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var project = CreateTestProject(projectId, ProjectStatus.APPROVED);
        // Expired deadline (yesterday)
        var commitment = new ProjectCommitment(projectId, studentId, DateTimeOffset.UtcNow.AddDays(-1));

        var fakeRepo = new StubCommitmentRepository
        {
            StudentId = studentId,
            Project = project,
            Commitment = commitment
        };
        var fakeUow = new StubUnitOfWork();
        var recordingNotifier = new RecordingRealtimeNotifier();

        var service = new CommitmentService(fakeRepo, fakeUow, recordingNotifier);

        // ACT
        var result = await service.ConfirmAsync(studentUserId, projectId, CancellationToken.None);

        // ASSERT: null returned because deadline expired
        Assert.Null(result);
        Assert.Empty(recordingNotifier.StatusChangedBroadcasts);
    }

    [Fact]
    public async Task CommitmentConfirm_EmptyGuid_ReturnsNull()
    {
        var studentUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var fakeRepo = new StubCommitmentRepository
        {
            StudentId = studentId,
            Project = null,
            Commitment = null
        };
        var fakeUow = new StubUnitOfWork();
        var recordingNotifier = new RecordingRealtimeNotifier();

        var service = new CommitmentService(fakeRepo, fakeUow, recordingNotifier);

        var result = await service.ConfirmAsync(studentUserId, Guid.Empty, CancellationToken.None);
        Assert.Null(result);
    }

    #endregion

    #region Helpers & Test Doubles

    private static Project CreateTestProject(Guid projectId, ProjectStatus initialStatus)
    {
        var project = new Project(
            Guid.NewGuid(), "PRJ-TEST", "Realtime Test Project",
            ProjectDifficulty.INTERMEDIATE, ProjectWorkType.HYBRID,
            8, 3, 1, 3, "Summary", "Problem", "Requirements", "Constraints",
            null, DateTimeOffset.UtcNow.AddDays(14), 1000000, "VND");

        if (initialStatus == ProjectStatus.IN_PROGRESS)
        {
            project.StartExecution();
        }
        else if (initialStatus == ProjectStatus.APPROVED)
        {
            project.SubmitForApproval(Guid.NewGuid(), DateTimeOffset.UtcNow);
            project.Approve(Guid.NewGuid(), DateTimeOffset.UtcNow);
        }

        return project;
    }

    private static ProjectHub CreateHub(
        IProjectAccessRepository repo,
        Guid userId,
        string role,
        MockGroupManager? groupManager = null)
    {
        var hub = new ProjectHub(repo);
        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        ], "TestAuth");
        var user = new ClaimsPrincipal(identity);
        hub.Context = new MockHubCallerContext(user);
        hub.Groups = groupManager ?? new MockGroupManager();
        return hub;
    }

    private sealed class StubProjectAccessRepository(bool hasStudentAccess, bool hasCompanyAccess, bool hasLecturerAccess) : IProjectAccessRepository
    {
        public Task<bool> HasActiveStudentMembershipAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) => Task.FromResult(hasStudentAccess);
        public Task<bool> HasCompanyAccessAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) => Task.FromResult(hasCompanyAccess);
        public Task<bool> HasLecturerAccessAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) => Task.FromResult(hasLecturerAccess);
    }

    private sealed class StubCommitmentRepository : ICommitmentRepository
    {
        public Guid? StudentId { get; set; }
        public Project? Project { get; set; }
        public ProjectCommitment? Commitment { get; set; }
        public bool HasMembership { get; set; }
        public List<ProjectMember> AddedMembers { get; } = [];
        public List<ProjectActivity> AddedActivities { get; } = [];

        public Task<Guid?> FindStudentIdAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(StudentId);
        public Task<CommitmentResponse?> FindCommitmentAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken) =>
            Task.FromResult(Commitment is null ? null : new CommitmentResponse(Commitment.Id, Commitment.ProjectId, Commitment.StudentId, Commitment.Status, Commitment.PolicyVersion, Commitment.ConfirmationDeadline, Commitment.ConfirmedAt));
        public Task<IReadOnlyCollection<CommitmentResponse>> ListCommitmentsAsync(Guid studentId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<CommitmentResponse>>([]);
        public Task<ProjectCommitment?> FindCommitmentForUpdateAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken) => Task.FromResult(Commitment);
        public Task<Project?> FindProjectForUpdateAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult(Project);
        public Task<bool> HasProjectMembershipAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken) => Task.FromResult(HasMembership);
        public void AddProjectMember(ProjectMember member) => AddedMembers.Add(member);
        public void AddProjectActivity(ProjectActivity activity) => AddedActivities.Add(activity);
        public Task<ProjectMember?> FindActiveMembershipAsync(Guid studentId, Guid projectId, CancellationToken cancellationToken) => Task.FromResult<ProjectMember?>(null);
        public Task<bool> HasOpenWithdrawalAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken) => Task.FromResult(false);
        public void AddWithdrawalRequest(WithdrawalRequest withdrawal) { }
        public Task<IReadOnlyCollection<WithdrawalResponse>> ListWithdrawalsAsync(Guid studentId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<WithdrawalResponse>>([]);
        public Task<WithdrawalResponse?> FindWithdrawalAsync(Guid studentId, Guid withdrawalId, CancellationToken cancellationToken) => Task.FromResult<WithdrawalResponse?>(null);
        public Task<Guid?> FindLecturerIdAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<Guid?>(null);
        public Task<WithdrawalRequest?> FindWithdrawalForUpdateAsync(Guid withdrawalId, CancellationToken cancellationToken) => Task.FromResult<WithdrawalRequest?>(null);
        public Task<bool> IsLecturerAssignedAsync(Guid projectId, Guid lecturerId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task DeactivateActiveMembershipsAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<List<ProjectCommitment>> FindPendingCommitmentsAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken) => Task.FromResult<List<ProjectCommitment>>([]);
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IUnitOfWorkTransaction>(new StubTransaction());
        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(System.Data.IsolationLevel isolationLevel, CancellationToken cancellationToken = default) =>
            Task.FromResult<IUnitOfWorkTransaction>(new StubTransaction());

        private sealed class StubTransaction : IUnitOfWorkTransaction
        {
            public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingRealtimeNotifier : IProjectRealtimeNotifier
    {
        public List<(Guid ProjectId, string Status)> StatusChangedBroadcasts { get; } = [];

        public Task NotifyTaskCreatedAsync(Guid projectId, ProjectTaskResponse task, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyTaskUpdatedAsync(Guid projectId, ProjectTaskResponse task, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyTaskMovedAsync(Guid projectId, ProjectTaskResponse task, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyTaskDeletedAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyCommentAddedAsync(Guid projectId, TaskCommentResponse comment, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyCommentDeletedAsync(Guid projectId, Guid taskId, Guid commentId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyChecklistUpdatedAsync(Guid projectId, TaskChecklistItemResponse item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyMilestoneUpdatedAsync(Guid projectId, MilestoneResponse milestone, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyProjectStatusChangedAsync(Guid projectId, string status, CancellationToken cancellationToken = default)
        {
            StatusChangedBroadcasts.Add((projectId, status));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingHubClients : IHubClients
    {
        public List<(string Group, string Method, object? Arg)> SentEvents { get; } = [];

        public IClientProxy All => throw new NotImplementedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
        public IClientProxy Client(string connectionId) => throw new NotImplementedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotImplementedException();
        public IClientProxy Group(string groupName) => new RecordingClientProxy(groupName, SentEvents);
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotImplementedException();
        public IClientProxy User(string userId) => throw new NotImplementedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotImplementedException();

        private sealed class RecordingClientProxy(string groupName, List<(string Group, string Method, object? Arg)> events) : IClientProxy
        {
            public Task SendCoreAsync(string method, object?[]? args, CancellationToken cancellationToken = default)
            {
                events.Add((groupName, method, args?.Length > 0 ? args[0] : null));
                return Task.CompletedTask;
            }
        }
    }

    private sealed class MockHubContext(IHubClients clients) : IHubContext<ProjectHub>
    {
        public IHubClients Clients => clients;
        public IGroupManager Groups => throw new NotImplementedException();
    }

    private sealed class MockHubCallerContext(ClaimsPrincipal? user) : HubCallerContext
    {
        public override string ConnectionId { get; } = "test-conn-" + Guid.NewGuid().ToString("N")[..8];
        public override string? UserIdentifier => user?.FindFirstValue(ClaimTypes.NameIdentifier);
        public override ClaimsPrincipal? User => user;
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
