using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DNTU.SkillBridge.Application.Academics;
using DNTU.SkillBridge.Application.Projects;
using DNTU.SkillBridge.Application.Students;
using DNTU.SkillBridge.Domain.Academics;
using DNTU.SkillBridge.Domain.Catalog;
using DNTU.SkillBridge.Domain.Companies;
using DNTU.SkillBridge.Domain.Files;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Milestones;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Domain.Students;
using DNTU.SkillBridge.Domain.Submissions;
using DNTU.SkillBridge.Domain.Workspaces;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace DNTU.SkillBridge.IntegrationTests;

[Collection(CatalogApiCollection.Name)]
public sealed class Stage5And6EmpiricalChallengeTests(CatalogApiFactory factory)
{
    private const string SigningKey = "integration-tests-signing-key-with-at-least-sixty-four-characters-0001";
    private static readonly JsonSerializerOptions Json = CatalogApiJson.Options;

    [Fact]
    public async Task StateMachine_BusinessReviewBeforeTechnicalApproval_Returns409Conflict()
    {
        var fixture = await SeedChallengeWorkflowAsync();
        var companyClient = CreateRoleClient(RoleNames.Company, fixture.CompanyUserId);

        // Attempt business review while submission is in SUBMITTED status (not yet technically approved)
        var response = await companyClient.PostAsJsonAsync($"/api/v1/company/submissions/{fixture.SubmissionId}/business-review", new
        {
            version = fixture.SubmissionVersion,
            decision = "ACCEPTED",
            requirementsFeedback = "Yêu cầu nghiệp vụ thỏa mãn sớm.",
            collaborationFeedback = "Hợp tác tốt."
        }, Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        // Verify database state: status remains SUBMITTED
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var submission = await dbContext.ProjectSubmissions.SingleAsync(s => s.Id == fixture.SubmissionId);
            Assert.Equal(SubmissionStatus.SUBMITTED, submission.Status);
        });
    }

    [Fact]
    public async Task StateMachine_SequentialTransitions_Submitted_To_TechnicalApproved_To_BusinessAccepted()
    {
        var fixture = await SeedChallengeWorkflowAsync();
        var lecturerClient = CreateRoleClient(RoleNames.Lecturer, fixture.LecturerUserId);
        var companyClient = CreateRoleClient(RoleNames.Company, fixture.CompanyUserId);

        // 1. Technical review approval: SUBMITTED -> TECHNICAL_APPROVED
        var techResponse = await lecturerClient.PostAsJsonAsync($"/api/v1/lecturer/submissions/{fixture.SubmissionId}/technical-review", new
        {
            version = fixture.SubmissionVersion,
            decision = "APPROVED",
            feedback = "Kỹ thuật đạt chuẩn.",
            criteriaNotes = "{\"cleanCode\":true}"
        }, Json);

        Assert.Equal(HttpStatusCode.Created, techResponse.StatusCode);

        // Fetch updated submission version token
        Guid updatedToken = Guid.Empty;
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var submission = await dbContext.ProjectSubmissions.SingleAsync(s => s.Id == fixture.SubmissionId);
            Assert.Equal(SubmissionStatus.TECHNICAL_APPROVED, submission.Status);
            updatedToken = submission.Version;
        });

        // 2. Re-attempting technical review on TECHNICAL_APPROVED must fail with 409 Conflict
        var secondTech = await lecturerClient.PostAsJsonAsync($"/api/v1/lecturer/submissions/{fixture.SubmissionId}/technical-review", new
        {
            version = updatedToken,
            decision = "APPROVED",
            feedback = "Thử duyệt kỹ thuật lần 2.",
            criteriaNotes = "{}"
        }, Json);
        Assert.Equal(HttpStatusCode.Conflict, secondTech.StatusCode);

        // 3. Business review acceptance: TECHNICAL_APPROVED -> BUSINESS_ACCEPTED
        var bizResponse = await companyClient.PostAsJsonAsync($"/api/v1/company/submissions/{fixture.SubmissionId}/business-review", new
        {
            version = updatedToken,
            decision = "ACCEPTED",
            requirementsFeedback = "Sản phẩm đáp ứng mục tiêu kinh doanh.",
            collaborationFeedback = "Giao hàng đúng hẹn."
        }, Json);

        Assert.Equal(HttpStatusCode.Created, bizResponse.StatusCode);

        // Verify final state
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var submission = await dbContext.ProjectSubmissions.SingleAsync(s => s.Id == fixture.SubmissionId);
            Assert.Equal(SubmissionStatus.BUSINESS_ACCEPTED, submission.Status);
        });
    }

    [Fact]
    public async Task ProjectCompletion_TriplePrerequisite_StrictlyEnforced()
    {
        var fixture = await SeedChallengeWorkflowAsync();
        var lecturerClient = CreateRoleClient(RoleNames.Lecturer, fixture.LecturerUserId);
        var companyClient = CreateRoleClient(RoleNames.Company, fixture.CompanyUserId);

        // Seed an unapproved milestone
        Guid milestoneId = Guid.Empty;
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var milestone = new ProjectMilestone(fixture.ProjectId, "M1 Core Engine", "Xây dựng core", 1, DateTimeOffset.UtcNow.AddDays(7));
            dbContext.ProjectMilestones.Add(milestone);
            await dbContext.SaveChangesAsync();
            milestoneId = milestone.Id;
        });

        // Test Prerequisite 1 failure: Milestone is unapproved (Status = PLANNED)
        var completeRes1 = await lecturerClient.PostAsJsonAsync($"/api/v1/projects/{fixture.ProjectId}/complete", new { }, Json);
        Assert.Equal(HttpStatusCode.Conflict, completeRes1.StatusCode);

        // Approve the milestone
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var milestone = await dbContext.ProjectMilestones.SingleAsync(m => m.Id == milestoneId);
            milestone.Start(milestone.Version);
            milestone.Submit(milestone.Version);
            milestone.Approve(milestone.Version);
            await dbContext.SaveChangesAsync();
        });

        // Test Prerequisite 2 failure: Submission is not BUSINESS_ACCEPTED (currently SUBMITTED)
        var completeRes2 = await lecturerClient.PostAsJsonAsync($"/api/v1/projects/{fixture.ProjectId}/complete", new { }, Json);
        Assert.Equal(HttpStatusCode.Conflict, completeRes2.StatusCode);

        // Move submission to BUSINESS_ACCEPTED
        var tech = await lecturerClient.PostAsJsonAsync($"/api/v1/lecturer/submissions/{fixture.SubmissionId}/technical-review", new
        {
            version = fixture.SubmissionVersion,
            decision = "APPROVED",
            feedback = "Approved.",
            criteriaNotes = "{}"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, tech.StatusCode);

        Guid bizVersion = Guid.Empty;
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            bizVersion = (await dbContext.ProjectSubmissions.SingleAsync(s => s.Id == fixture.SubmissionId)).Version;
        });

        var biz = await companyClient.PostAsJsonAsync($"/api/v1/company/submissions/{fixture.SubmissionId}/business-review", new
        {
            version = bizVersion,
            decision = "ACCEPTED",
            requirementsFeedback = "Accepted.",
            collaborationFeedback = "Great."
        }, Json);
        Assert.Equal(HttpStatusCode.Created, biz.StatusCode);

        // Test Prerequisite 3 failure: Active student has NO evaluation
        var completeRes3 = await lecturerClient.PostAsJsonAsync($"/api/v1/projects/{fixture.ProjectId}/complete", new { }, Json);
        Assert.Equal(HttpStatusCode.Conflict, completeRes3.StatusCode);

        // Upsert rubric evaluation (Status = READY, not FINALIZED)
        var upsertEval = await lecturerClient.PostAsJsonAsync($"/api/v1/projects/{fixture.ProjectId}/students/{fixture.StudentId}/evaluation", new
        {
            rubricId = fixture.RubricId,
            scores = new[]
            {
                new { rubricCriterionId = fixture.CriteriaIds[0], score = 85.0m },
                new { rubricCriterionId = fixture.CriteriaIds[1], score = 90.0m },
                new { rubricCriterionId = fixture.CriteriaIds[2], score = 88.0m },
                new { rubricCriterionId = fixture.CriteriaIds[3], score = 92.0m },
                new { rubricCriterionId = fixture.CriteriaIds[4], score = 80.0m },
                new { rubricCriterionId = fixture.CriteriaIds[5], score = 87.0m }
            }
        }, Json);
        Assert.Equal(HttpStatusCode.OK, upsertEval.StatusCode);

        // Test Prerequisite 3 failure: Evaluation is still READY, not FINALIZED
        var completeRes4 = await lecturerClient.PostAsJsonAsync($"/api/v1/projects/{fixture.ProjectId}/complete", new { }, Json);
        Assert.Equal(HttpStatusCode.Conflict, completeRes4.StatusCode);

        // Finalize evaluation
        var finalizeRes = await lecturerClient.PostAsJsonAsync($"/api/v1/projects/{fixture.ProjectId}/students/{fixture.StudentId}/evaluation/finalize", new { }, Json);
        Assert.Equal(HttpStatusCode.OK, finalizeRes.StatusCode);

        // ALL 3 PREREQUISITES SATISFIED -> Project completion must succeed
        var completeSuccess = await lecturerClient.PostAsJsonAsync($"/api/v1/projects/{fixture.ProjectId}/complete", new { }, Json);
        Assert.Equal(HttpStatusCode.OK, completeSuccess.StatusCode);

        // Verify project completed in database
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var project = await dbContext.Projects.SingleAsync(p => p.Id == fixture.ProjectId);
            Assert.Equal(ProjectStatus.COMPLETED, project.Status);
            var completion = await dbContext.ProjectCompletions.SingleOrDefaultAsync(c => c.ProjectId == fixture.ProjectId);
            Assert.NotNull(completion);
        });
    }

    [Fact]
    public void CryptographicProofHash_SHA256_ExactMathAndConsistency()
    {
        var studentId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var rubricId = Guid.NewGuid();
        var finalScore = 87.50m;
        var finalizedAtUtc = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

        // Test Oracle calculation:
        var rawExpected = $"{studentId:D}:{projectId:D}:{finalScore:F2}:{finalizedAtUtc:O}";
        var expectedHash = BitConverter.ToString(SHA256.HashData(Encoding.UTF8.GetBytes(rawExpected)))
            .Replace("-", "")
            .ToLowerInvariant();

        // System under test calculation:
        var actualHash = AcademicEvaluation.ComputeProofHash(studentId, projectId, finalScore, finalizedAtUtc);

        Assert.Equal(expectedHash, actualHash);
        Assert.Equal(64, actualHash.Length);
        Assert.True(actualHash.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')));

        // Check evaluation entity behavior
        var evaluation = new AcademicEvaluation(projectId, studentId, rubricId, Guid.NewGuid());
        Assert.Null(evaluation.ProofHash); // Null prior to finalization

        evaluation.ReplaceScores([
            new AcademicCriterionScore(evaluation.Id, Guid.NewGuid(), 100m, 100m)
        ]);
        Assert.Null(evaluation.ProofHash); // Still null when READY

        evaluation.FinalizeEvaluation(finalizedAtUtc);
        Assert.NotNull(evaluation.ProofHash);
        Assert.Equal(AcademicEvaluation.ComputeProofHash(studentId, projectId, evaluation.TotalScore, finalizedAtUtc), evaluation.ProofHash);
    }

    [Fact]
    public async Task EvaluationFinalization_Requires_BusinessAcceptedSubmission_And_Matches_SkillPassport()
    {
        var fixture = await SeedChallengeWorkflowAsync();
        var lecturerClient = CreateRoleClient(RoleNames.Lecturer, fixture.LecturerUserId);
        var companyClient = CreateRoleClient(RoleNames.Company, fixture.CompanyUserId);
        var studentClient = CreateRoleClient(RoleNames.Student, fixture.StudentUserId);

        // 1. Upsert evaluation before business approval
        var upsertRes = await lecturerClient.PostAsJsonAsync($"/api/v1/projects/{fixture.ProjectId}/students/{fixture.StudentId}/evaluation", new
        {
            rubricId = fixture.RubricId,
            scores = new[]
            {
                new { rubricCriterionId = fixture.CriteriaIds[0], score = 85.0m },
                new { rubricCriterionId = fixture.CriteriaIds[1], score = 90.0m },
                new { rubricCriterionId = fixture.CriteriaIds[2], score = 88.0m },
                new { rubricCriterionId = fixture.CriteriaIds[3], score = 92.0m },
                new { rubricCriterionId = fixture.CriteriaIds[4], score = 80.0m },
                new { rubricCriterionId = fixture.CriteriaIds[5], score = 87.0m }
            }
        }, Json);
        Assert.Equal(HttpStatusCode.OK, upsertRes.StatusCode);

        // 2. Finalize attempt fails with 409 Conflict because submission is not yet BUSINESS_ACCEPTED
        var prematureFinalize = await lecturerClient.PostAsJsonAsync($"/api/v1/projects/{fixture.ProjectId}/students/{fixture.StudentId}/evaluation/finalize", new { }, Json);
        Assert.Equal(HttpStatusCode.Conflict, prematureFinalize.StatusCode);

        // 3. Technical approval + Business acceptance
        var tech = await lecturerClient.PostAsJsonAsync($"/api/v1/lecturer/submissions/{fixture.SubmissionId}/technical-review", new
        {
            version = fixture.SubmissionVersion,
            decision = "APPROVED",
            feedback = "Pass.",
            criteriaNotes = "{}"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, tech.StatusCode);

        Guid bizVersion = Guid.Empty;
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            bizVersion = (await dbContext.ProjectSubmissions.SingleAsync(s => s.Id == fixture.SubmissionId)).Version;
        });

        var biz = await companyClient.PostAsJsonAsync($"/api/v1/company/submissions/{fixture.SubmissionId}/business-review", new
        {
            version = bizVersion,
            decision = "ACCEPTED",
            requirementsFeedback = "Accepted.",
            collaborationFeedback = "Great."
        }, Json);
        Assert.Equal(HttpStatusCode.Created, biz.StatusCode);

        // 4. Finalize evaluation now succeeds
        var finalizeRes = await lecturerClient.PostAsJsonAsync($"/api/v1/projects/{fixture.ProjectId}/students/{fixture.StudentId}/evaluation/finalize", new { }, Json);
        Assert.Equal(HttpStatusCode.OK, finalizeRes.StatusCode);
        var finalizedEval = (await finalizeRes.Content.ReadFromJsonAsync<Envelope<AcademicEvaluationResponse>>(Json))!.Data;
        Assert.NotNull(finalizedEval.ProofHash);
        Assert.Equal(64, finalizedEval.ProofHash.Length);

        // 5. Query SkillPassport and verify hash consistency
        var passportRes = await studentClient.GetAsync($"/api/v1/students/{fixture.StudentId}/skill-passport");
        Assert.Equal(HttpStatusCode.OK, passportRes.StatusCode);
        var passport = (await passportRes.Content.ReadFromJsonAsync<Envelope<SkillPassportResponse>>(Json))!.Data;

        Assert.NotNull(passport.Certificates);
        Assert.NotEmpty(passport.Certificates);
        var matchingCert = passport.Certificates.Single(c => c.ProjectId == fixture.ProjectId);
        Assert.Equal(finalizedEval.ProofHash, matchingCert.ProofHash);
        Assert.Equal(finalizedEval.ProofHash, passport.PrimaryProofHash);
    }

    private async Task<ChallengeWorkflowFixture> SeedChallengeWorkflowAsync()
    {
        ChallengeWorkflowFixture? fixture = null;
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var studentUser = await AddUserAsync(dbContext, RoleNames.Student, "challenge-student");
            var companyUser = await AddUserAsync(dbContext, RoleNames.Company, "challenge-company");
            var lecturerUser = await AddUserAsync(dbContext, RoleNames.Lecturer, "challenge-lecturer");
            var adminUser = await AddUserAsync(dbContext, RoleNames.Admin, "challenge-admin");

            var studentProfile = new StudentProfile(studentUser.Id);
            studentProfile.Privacy.Update(true, true, true, true);
            var lecturerProfile = new LecturerProfile(lecturerUser.Id);
            var company = new Company($"Challenge Company {Guid.NewGuid():N}");
            dbContext.StudentProfiles.Add(studentProfile);
            dbContext.LecturerProfiles.Add(lecturerProfile);
            dbContext.Companies.Add(company);
            await dbContext.SaveChangesAsync();

            dbContext.CompanyMembers.Add(new CompanyMember(company.Id, companyUser.Id, CompanyMemberRole.OWNER, "Owner"));
            var project = new Project(company.Id, $"PRJ-{Guid.NewGuid():N}"[..12], $"Challenge Project {Guid.NewGuid():N}", ProjectDifficulty.BEGINNER, ProjectWorkType.REMOTE, 4, 3, 1, 4, null, null, null, null, null, null, null, null);
            project.StartExecution();
            dbContext.Projects.Add(project);
            await dbContext.SaveChangesAsync();

            dbContext.ProjectMembers.Add(new ProjectMember(project.Id, studentProfile.Id));
            var assignment = new LecturerAssignment(lecturerProfile.Id, project.Id, LecturerAssignmentRole.PRIMARY, "Supervisor");
            assignment.Accept(DateTimeOffset.UtcNow);
            dbContext.LecturerAssignments.Add(assignment);

            var file = new FileRecord(studentUser.Id, project.Id, "deliverable.zip", $"submissions/{Guid.NewGuid():N}.zip", "application/zip", 2048, DateTimeOffset.UtcNow.AddHours(2));
            file.Complete(new string('b', 64), DateTimeOffset.UtcNow);
            dbContext.FileRecords.Add(file);

            var submission = new ProjectSubmission(project.Id, null, studentProfile.Id, new SubmissionVersion("Initial deliverables", "https://github.com/dntu/test", "https://demo.test", null, file.Id));
            dbContext.ProjectSubmissions.Add(submission);
            await dbContext.SaveChangesAsync();

            // Seed Course, locked Rubric and CourseProject mapping
            var course = new Course($"CAP-{Guid.NewGuid():N}"[..8], "Capstone Course 2026");
            dbContext.Courses.Add(course);
            await dbContext.SaveChangesAsync();

            var rubric = new Rubric(lecturerProfile.Id, "Capstone Evaluation Rubric 2026");
            rubric.AddCriterion("Kiến trúc hệ thống", 17.00m, 1);
            rubric.AddCriterion("Chất lượng mã nguồn", 17.00m, 2);
            rubric.AddCriterion("Cơ sở dữ liệu & Tối ưu", 18.00m, 3);
            rubric.AddCriterion("Kiểm thử & Tự động hóa", 17.00m, 4);
            rubric.AddCriterion("Tài liệu kỹ thuật & Bàn giao", 16.00m, 5);
            rubric.AddCriterion("Đóng góp cá nhân & Làm việc nhóm", 15.00m, 6);
            rubric.Lock();
            dbContext.Rubrics.Add(rubric);
            await dbContext.SaveChangesAsync();

            var courseProject = new CourseProject(course.Id, project.Id, rubric.Id, lecturerUser.Id);
            courseProject.Approve(adminUser.Id, DateTimeOffset.UtcNow);
            dbContext.CourseProjects.Add(courseProject);
            await dbContext.SaveChangesAsync();

            var criteriaIds = rubric.Criteria.OrderBy(c => c.SortOrder).Select(c => c.Id).ToList();

            fixture = new ChallengeWorkflowFixture(
                project.Id,
                studentUser.Id,
                studentProfile.Id,
                companyUser.Id,
                lecturerUser.Id,
                lecturerProfile.Id,
                submission.Id,
                submission.Version,
                rubric.Id,
                criteriaIds);
        });

        return fixture!;
    }

    private static async Task<User> AddUserAsync(AppDbContext dbContext, string roleName, string prefix)
    {
        var user = new User($"{prefix}-{Guid.NewGuid():N}@skillbridge.local", "Challenge User", "not-a-real-hash");
        var role = await dbContext.Roles.SingleAsync(r => r.NormalizedName == roleName);
        user.AssignRole(role.Id);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }

    private HttpClient CreateRoleClient(string role, Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(role, userId));
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
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken("DNTU.SkillBridge.Api", "DNTU.SkillBridge.Web", claims, expires: DateTime.UtcNow.AddMinutes(15), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record ChallengeWorkflowFixture(
        Guid ProjectId,
        Guid StudentUserId,
        Guid StudentId,
        Guid CompanyUserId,
        Guid LecturerUserId,
        Guid LecturerProfileId,
        Guid SubmissionId,
        Guid SubmissionVersion,
        Guid RubricId,
        IReadOnlyList<Guid> CriteriaIds);

    private sealed record Envelope<T>(T Data);
}
