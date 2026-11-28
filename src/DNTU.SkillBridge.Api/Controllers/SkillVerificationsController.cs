using System.Text.Json;
using Asp.Versioning;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Application.Social;
using DNTU.SkillBridge.Application.Students;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Portfolio;
using DNTU.SkillBridge.Domain.Students;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v1")]
[Authorize]
[Produces("application/json")]
public sealed class SkillVerificationsController(
    AppDbContext dbContext,
    ICurrentUser currentUser,
    ISocialActivityWriter socialActivityWriter,
    DNTU.SkillBridge.Application.Notifications.INotificationWriter? notificationWriter = null) : ControllerBase
{
    /// <summary>Sinh viên xem danh sách các yêu cầu xác thực kỹ năng của bản thân.</summary>
    [HttpGet("students/skills/requests")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<SkillVerificationRequestResponse>>>> GetMyRequests(CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var studentId = currentUser.StudentId!.Value;

        var requests = await dbContext.SkillVerificationRequests.AsNoTracking()
            .Where(r => r.StudentId == studentId)
            .OrderByDescending(r => r.SubmittedAt)
            .ToListAsync(cancellationToken);

        var responses = await MapRequestsAsync(requests, cancellationToken);
        return Ok(new ApiResponse<IReadOnlyCollection<SkillVerificationRequestResponse>>(responses));
    }

    /// <summary>Sinh viên gửi đăng ký xác thực một kỹ năng từ danh mục hệ thống.</summary>
    [HttpPost("students/skills/requests")]
    public async Task<ActionResult<ApiResponse<SkillVerificationRequestResponse>>> SubmitRequest(
        [FromBody] SubmitSkillVerificationRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var studentId = currentUser.StudentId!.Value;

        var skill = await dbContext.Skills.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.SkillId, cancellationToken);
        if (skill is null)
        {
            return Problem(
                title: "Không tìm thấy kỹ năng",
                detail: "Không tìm thấy kỹ năng trong danh mục chuẩn.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (!skill.IsActive)
        {
            return Problem(
                title: "Kỹ năng đã ngừng áp dụng",
                detail: "Kỹ năng/chứng chỉ này đã ngừng áp dụng trong danh mục.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var normalizedFileUrls = (request.EvidenceFileUrls ?? [])
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u.Trim())
            .Distinct()
            .ToList();

        if (string.IsNullOrWhiteSpace(request.EvidenceUrl)
            && normalizedFileUrls.Count == 0
            && string.IsNullOrWhiteSpace(request.StudentNote))
        {
            return Problem(
                title: "Thiếu minh chứng",
                detail: "Vui lòng cung cấp ít nhất một đường dẫn minh chứng, tệp đính kèm hoặc mô tả giải trình.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var hasActiveRequest = await dbContext.SkillVerificationRequests.AsNoTracking()
            .AnyAsync(r => r.StudentId == studentId
                           && r.SkillId == request.SkillId
                           && (r.Status == SkillVerificationStatus.PENDING
                               || r.Status == SkillVerificationStatus.CHANGES_REQUESTED
                               || r.Status == SkillVerificationStatus.APPROVED),
                cancellationToken);
        var hasVerifiedSkill = await dbContext.VerifiedSkills.AsNoTracking()
            .AnyAsync(v => v.StudentId == studentId && v.SkillId == request.SkillId && !v.IsRevoked, cancellationToken);
        if (hasActiveRequest || hasVerifiedSkill)
        {
            return Problem(
                title: "Trùng yêu cầu xác thực",
                detail: "Bạn đã có yêu cầu đang chờ thẩm định hoặc đã được xác thực kỹ năng này.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Xác định ProjectId và Giảng viên hướng dẫn nếu sinh viên chưa truyền
        var projectId = request.ProjectId;
        var lecturerId = request.LecturerId;

        if (!projectId.HasValue)
        {
            var projectMember = await dbContext.ProjectMembers.AsNoTracking()
                .Where(m => m.StudentId == studentId && m.IsActive)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (projectMember is not null)
            {
                projectId = projectMember.ProjectId;
            }
        }

        if (projectId.HasValue && !lecturerId.HasValue)
        {
            var lecturerAssignment = await dbContext.LecturerAssignments.AsNoTracking()
                .Where(a => a.ProjectId == projectId.Value && a.Status == Domain.Lecturers.LecturerAssignmentStatus.ACTIVE)
                .FirstOrDefaultAsync(cancellationToken);
            if (lecturerAssignment is not null)
            {
                lecturerId = lecturerAssignment.LecturerId;
            }
        }

        var filesJson = normalizedFileUrls.Count > 0
            ? JsonSerializer.Serialize(normalizedFileUrls)
            : null;

        var verificationRequest = new SkillVerificationRequest(
            studentId,
            request.SkillId,
            projectId,
            lecturerId,
            request.Level,
            request.EvidenceUrl,
            filesJson,
            request.StudentNote);

        dbContext.SkillVerificationRequests.Add(verificationRequest);

        if (notificationWriter is not null && lecturerId.HasValue)
        {
            var lecturerUserId = await dbContext.LecturerProfiles.AsNoTracking()
                .Where(lp => lp.Id == lecturerId.Value && lp.IsActive)
                .Select(lp => (Guid?)lp.UserId)
                .SingleOrDefaultAsync(cancellationToken);
            if (lecturerUserId.HasValue)
            {
                var studentName = await dbContext.Users.AsNoTracking()
                    .Where(u => u.Id == currentUser.UserId!.Value)
                    .Select(u => u.DisplayName)
                    .SingleOrDefaultAsync(cancellationToken) ?? "Sinh viên";
                notificationWriter.Add(new DNTU.SkillBridge.Application.Notifications.NotificationMessage(
                    lecturerUserId.Value,
                    "SKILL_VERIFICATION_REQUESTED",
                    $"{studentName} gửi yêu cầu xác thực kỹ năng {skill.Name}",
                    JsonSerializer.Serialize(new
                    {
                        skillId = skill.Id,
                        skillName = skill.Name,
                        senderName = studentName,
                        url = "/lecturer/skills/verification",
                    })));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var responses = await MapRequestsAsync([verificationRequest], cancellationToken);
        return Created($"/api/v1/skills/requests/{verificationRequest.Id}", new ApiResponse<SkillVerificationRequestResponse>(responses.First()));
    }

    /// <summary>Sinh viên cập nhật/bổ sung minh chứng cho yêu cầu xác thực.</summary>
    [HttpPut("students/skills/requests/{id:guid}")]
    public async Task<ActionResult<ApiResponse<SkillVerificationRequestResponse>>> UpdateEvidence(
        Guid id,
        [FromBody] UpdateSkillVerificationRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsStudent()) return Forbid();
        var studentId = currentUser.StudentId!.Value;

        var entity = await dbContext.SkillVerificationRequests.FirstOrDefaultAsync(r => r.Id == id && r.StudentId == studentId, cancellationToken);
        if (entity is null) return NotFound();

        if (entity.Status is not (SkillVerificationStatus.PENDING or SkillVerificationStatus.CHANGES_REQUESTED))
        {
            return Problem(
                title: "Không thể cập nhật",
                detail: "Chỉ có thể cập nhật minh chứng khi hồ sơ đang chờ thẩm định hoặc được yêu cầu bổ sung.",
                statusCode: StatusCodes.Status409Conflict);
        }

        string? filesJson;
        if (request.EvidenceFileUrls is not null)
        {
            var normalizedFiles = request.EvidenceFileUrls
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(u => u.Trim())
                .Distinct()
                .ToList();
            filesJson = normalizedFiles.Count > 0 ? JsonSerializer.Serialize(normalizedFiles) : null;
        }
        else
        {
            filesJson = entity.EvidenceFileUrlsJson;
        }

        if (string.IsNullOrWhiteSpace(request.EvidenceUrl)
            && string.IsNullOrWhiteSpace(filesJson)
            && string.IsNullOrWhiteSpace(request.StudentNote))
        {
            return Problem(
                title: "Thiếu minh chứng",
                detail: "Vui lòng cung cấp ít nhất một đường dẫn minh chứng, tệp đính kèm hoặc mô tả giải trình.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var wasChangesRequested = entity.Status == SkillVerificationStatus.CHANGES_REQUESTED;
        entity.UpdateEvidence(request.Level, request.EvidenceUrl, filesJson, request.StudentNote);

        if (wasChangesRequested && notificationWriter is not null && entity.AssignedLecturerId.HasValue)
        {
            var lecturerUserId = await dbContext.LecturerProfiles.AsNoTracking()
                .Where(lp => lp.Id == entity.AssignedLecturerId.Value && lp.IsActive)
                .Select(lp => (Guid?)lp.UserId)
                .SingleOrDefaultAsync(cancellationToken);
            if (lecturerUserId.HasValue)
            {
                var studentName = await dbContext.Users.AsNoTracking()
                    .Where(u => u.Id == currentUser.UserId!.Value)
                    .Select(u => u.DisplayName)
                    .SingleOrDefaultAsync(cancellationToken) ?? "Sinh viên";
                var skillName = await dbContext.Skills.AsNoTracking()
                    .Where(s => s.Id == entity.SkillId)
                    .Select(s => s.Name)
                    .SingleOrDefaultAsync(cancellationToken) ?? "kỹ năng";
                notificationWriter.Add(new DNTU.SkillBridge.Application.Notifications.NotificationMessage(
                    lecturerUserId.Value,
                    "SKILL_VERIFICATION_REQUESTED",
                    $"{studentName} đã bổ sung minh chứng cho kỹ năng {skillName}",
                    JsonSerializer.Serialize(new
                    {
                        skillId = entity.SkillId,
                        skillName,
                        senderName = studentName,
                        url = "/lecturer/skills/verification",
                    })));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var responses = await MapRequestsAsync([entity], cancellationToken);
        return Ok(new ApiResponse<SkillVerificationRequestResponse>(responses.First()));
    }

    /// <summary>Giảng viên xem danh sách các yêu cầu xác thực kỹ năng cần thẩm định.</summary>
    [HttpGet("lecturer/skills/requests")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<SkillVerificationRequestResponse>>>> GetLecturerRequests(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        if (!IsLecturer() && !IsAdmin()) return Forbid();

        var query = dbContext.SkillVerificationRequests.AsNoTracking().AsQueryable();

        if (currentUser.LecturerId.HasValue && !IsAdmin())
        {
            var lecturerId = currentUser.LecturerId.Value;
            // Lấy các project mà giảng viên đang hướng dẫn
            var supervisedProjectIds = await dbContext.LecturerAssignments.AsNoTracking()
                .Where(a => a.LecturerId == lecturerId)
                .Select(a => a.ProjectId)
                .ToListAsync(cancellationToken);

            query = query.Where(r => r.AssignedLecturerId == lecturerId ||
                                     (r.ProjectId.HasValue && supervisedProjectIds.Contains(r.ProjectId.Value)) ||
                                     r.AssignedLecturerId == null);
        }

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<SkillVerificationStatus>(status.Trim(), ignoreCase: true, out var parsedStatus))
            {
                query = query.Where(r => r.Status == parsedStatus);
            }
        }

        var requests = await query.OrderByDescending(r => r.SubmittedAt).ToListAsync(cancellationToken);
        var responses = await MapRequestsAsync(requests, cancellationToken);
        return Ok(new ApiResponse<IReadOnlyCollection<SkillVerificationRequestResponse>>(responses));
    }

    /// <summary>Giảng viên thẩm định & phê duyệt kỹ năng cho sinh viên.</summary>
    [HttpPost("lecturer/skills/requests/{id:guid}/approve")]
    public async Task<ActionResult<ApiResponse<SkillVerificationRequestResponse>>> ApproveRequest(
        Guid id,
        [FromBody] ReviewSkillVerificationRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsLecturer() && !IsAdmin()) return Forbid();

        var entity = await dbContext.SkillVerificationRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity is null) return NotFound();

        if (entity.Status == SkillVerificationStatus.APPROVED)
        {
            return Problem(
                title: "Hồ sơ đã được phê duyệt",
                detail: "Yêu cầu xác thực này đã được phê duyệt trước đó.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var lecturerUserId = currentUser.UserId!.Value;
        entity.Approve(lecturerUserId, request.Feedback, request.Level);

        // Cấp huy hiệu VerifiedSkill cho sinh viên
        var skillLevel = ParseSkillLevel(request.Level ?? entity.Level);
        var projectId = entity.ProjectId ?? Guid.Empty;

        var existingSkill = await dbContext.VerifiedSkills
            .FirstOrDefaultAsync(v => v.StudentId == entity.StudentId && v.SkillId == entity.SkillId, cancellationToken);

        if (existingSkill is null)
        {
            var verified = new VerifiedSkill(entity.StudentId, entity.SkillId, projectId, lecturerUserId, skillLevel);
            dbContext.VerifiedSkills.Add(verified);
        }
        else
        {
            existingSkill.Restore(lecturerUserId, skillLevel, null, null);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Ghi hoạt động SKILL_VERIFIED cho Bảng tin của người theo dõi sinh viên.
        var studentUser = await dbContext.StudentProfiles.AsNoTracking()
            .Where(sp => sp.Id == entity.StudentId)
            .Select(sp => new { sp.UserId })
            .FirstOrDefaultAsync(cancellationToken);
        var skillName = await dbContext.Skills.AsNoTracking()
            .Where(sk => sk.Id == entity.SkillId)
            .Select(sk => sk.Name)
            .FirstOrDefaultAsync(cancellationToken);
        var lecturerName = await dbContext.Users.AsNoTracking()
            .Where(u => u.Id == lecturerUserId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);
        if (studentUser is not null)
        {
            socialActivityWriter.Add(new SocialActivityMessage(
                studentUser.UserId,
                DNTU.SkillBridge.Domain.Social.SocialActivityType.SKILL_VERIFIED,
                $"Kỹ năng {skillName} đã được xác thực",
                $"{lecturerName} vừa xác nhận kỹ năng {skillName} của sinh viên.",
                "/student/portfolio",
                entity.SkillId));

            // Thông báo cho sinh viên: chứng chỉ kỹ năng đã được giảng viên xác thực.
            notificationWriter?.Add(new DNTU.SkillBridge.Application.Notifications.NotificationMessage(
                studentUser.UserId,
                "SKILL_VERIFIED",
                $"Kỹ năng {skillName} đã được xác thực bởi {lecturerName}",
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    skillId = entity.SkillId,
                    skillName,
                    url = "/student/portfolio",
                })));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var responses = await MapRequestsAsync([entity], cancellationToken);
        return Ok(new ApiResponse<SkillVerificationRequestResponse>(responses.First()));
    }

    /// <summary>Giảng viên yêu cầu sinh viên bổ sung thêm minh chứng thẩm định.</summary>
    [HttpPost("lecturer/skills/requests/{id:guid}/request-changes")]
    public async Task<ActionResult<ApiResponse<SkillVerificationRequestResponse>>> RequestChanges(
        Guid id,
        [FromBody] ReviewSkillVerificationRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsLecturer() && !IsAdmin()) return Forbid();

        var entity = await dbContext.SkillVerificationRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity is null) return NotFound();

        if (entity.Status == SkillVerificationStatus.APPROVED)
        {
            return Problem(
                title: "Hồ sơ đã được phê duyệt",
                detail: "Không thể thay đổi trạng thái hồ sơ đã được phê duyệt.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var lecturerUserId = currentUser.UserId!.Value;
        var feedbackText = request.Feedback ?? "Vui lòng bổ sung thêm minh chứng hình ảnh/link sản phẩm.";
        entity.RequestChanges(lecturerUserId, feedbackText);

        if (notificationWriter is not null)
        {
            var studentUserId = await dbContext.StudentProfiles.AsNoTracking()
                .Where(sp => sp.Id == entity.StudentId)
                .Select(sp => (Guid?)sp.UserId)
                .SingleOrDefaultAsync(cancellationToken);
            var skillName = await dbContext.Skills.AsNoTracking()
                .Where(s => s.Id == entity.SkillId)
                .Select(s => s.Name)
                .SingleOrDefaultAsync(cancellationToken) ?? "kỹ năng";
            if (studentUserId.HasValue)
            {
                notificationWriter.Add(new DNTU.SkillBridge.Application.Notifications.NotificationMessage(
                    studentUserId.Value,
                    "SKILL_CHANGES_REQUESTED",
                    $"Kỹ năng {skillName} cần bổ sung thêm minh chứng",
                    JsonSerializer.Serialize(new
                    {
                        skillId = entity.SkillId,
                        skillName,
                        feedback = feedbackText,
                        url = "/student/portfolio",
                    })));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var responses = await MapRequestsAsync([entity], cancellationToken);
        return Ok(new ApiResponse<SkillVerificationRequestResponse>(responses.First()));
    }

    /// <summary>Giảng viên từ chối yêu cầu xác thực.</summary>
    [HttpPost("lecturer/skills/requests/{id:guid}/reject")]
    public async Task<ActionResult<ApiResponse<SkillVerificationRequestResponse>>> RejectRequest(
        Guid id,
        [FromBody] ReviewSkillVerificationRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsLecturer() && !IsAdmin()) return Forbid();

        var entity = await dbContext.SkillVerificationRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity is null) return NotFound();

        if (entity.Status == SkillVerificationStatus.APPROVED)
        {
            return Problem(
                title: "Hồ sơ đã được phê duyệt",
                detail: "Không thể thay đổi trạng thái hồ sơ đã được phê duyệt.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var lecturerUserId = currentUser.UserId!.Value;
        var feedbackText = request.Feedback ?? "Hồ sơ không đáp ứng tiêu chuẩn thẩm định.";
        entity.Reject(lecturerUserId, feedbackText);

        if (notificationWriter is not null)
        {
            var studentUserId = await dbContext.StudentProfiles.AsNoTracking()
                .Where(sp => sp.Id == entity.StudentId)
                .Select(sp => (Guid?)sp.UserId)
                .SingleOrDefaultAsync(cancellationToken);
            var skillName = await dbContext.Skills.AsNoTracking()
                .Where(s => s.Id == entity.SkillId)
                .Select(s => s.Name)
                .SingleOrDefaultAsync(cancellationToken) ?? "kỹ năng";
            if (studentUserId.HasValue)
            {
                notificationWriter.Add(new DNTU.SkillBridge.Application.Notifications.NotificationMessage(
                    studentUserId.Value,
                    "SKILL_REJECTED",
                    $"Yêu cầu xác thực kỹ năng {skillName} chưa được chấp nhận",
                    JsonSerializer.Serialize(new
                    {
                        skillId = entity.SkillId,
                        skillName,
                        feedback = feedbackText,
                        url = "/student/portfolio",
                    })));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var responses = await MapRequestsAsync([entity], cancellationToken);
        return Ok(new ApiResponse<SkillVerificationRequestResponse>(responses.First()));
    }

    /// <summary>Chi tiết một yêu cầu xác thực kỹ năng.</summary>
    [HttpGet("skills/requests/{id:guid}")]
    public async Task<ActionResult<ApiResponse<SkillVerificationRequestResponse>>> GetRequestDetail(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.SkillVerificationRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity is null) return NotFound();

        // Kiểm tra quyền: Student owner, Lecturer, hoặc Admin
        if (IsStudent() && currentUser.StudentId.HasValue && entity.StudentId != currentUser.StudentId.Value)
        {
            return Forbid();
        }

        var responses = await MapRequestsAsync([entity], cancellationToken);
        return Ok(new ApiResponse<SkillVerificationRequestResponse>(responses.First()));
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────────

    private async Task<IReadOnlyCollection<SkillVerificationRequestResponse>> MapRequestsAsync(
        IReadOnlyCollection<SkillVerificationRequest> requests,
        CancellationToken cancellationToken)
    {
        if (requests.Count == 0) return [];

        var studentIds = requests.Select(r => r.StudentId).Distinct().ToList();
        var skillIds = requests.Select(r => r.SkillId).Distinct().ToList();
        var projectIds = requests.Where(r => r.ProjectId.HasValue).Select(r => r.ProjectId!.Value).Distinct().ToList();
        var lecturerIds = requests.Where(r => r.AssignedLecturerId.HasValue).Select(r => r.AssignedLecturerId!.Value).Distinct().ToList();
        var reviewerUserIds = requests.Where(r => r.ReviewedByLecturerId.HasValue).Select(r => r.ReviewedByLecturerId!.Value).Distinct().ToList();

        var students = await dbContext.StudentProfiles.AsNoTracking()
            .Where(s => studentIds.Contains(s.Id))
            .Join(dbContext.Users.AsNoTracking(), s => s.UserId, u => u.Id, (s, u) => new { s.Id, s.StudentCode, Class = s.AcademicYear, FullName = u.DisplayName, s.AvatarUrl })
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        var skills = await dbContext.Skills.AsNoTracking()
            .Where(s => skillIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        var projects = projectIds.Count > 0
            ? await dbContext.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken)
            : [];

        var lecturers = lecturerIds.Count > 0
            ? await dbContext.LecturerProfiles.AsNoTracking()
                .Where(l => lecturerIds.Contains(l.Id))
                .Join(dbContext.Users.AsNoTracking(), l => l.UserId, u => u.Id, (l, u) => new { l.Id, FullName = u.DisplayName })
                .ToDictionaryAsync(l => l.Id, l => l.FullName, cancellationToken)
            : [];

        var reviewers = reviewerUserIds.Count > 0
            ? await dbContext.Users.AsNoTracking()
                .Where(u => reviewerUserIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken)
            : [];

        var list = new List<SkillVerificationRequestResponse>(requests.Count);
        foreach (var r in requests)
        {
            students.TryGetValue(r.StudentId, out var student);
            skills.TryGetValue(r.SkillId, out var skill);
            projects.TryGetValue(r.ProjectId ?? Guid.Empty, out var project);

            string? lecturerName = null;
            if (r.ReviewedByLecturerId.HasValue)
            {
                reviewers.TryGetValue(r.ReviewedByLecturerId.Value, out lecturerName);
            }
            if (string.IsNullOrWhiteSpace(lecturerName) && r.AssignedLecturerId.HasValue)
            {
                lecturers.TryGetValue(r.AssignedLecturerId.Value, out lecturerName);
            }

            IReadOnlyList<string> fileUrls = [];
            if (!string.IsNullOrWhiteSpace(r.EvidenceFileUrlsJson))
            {
                try
                {
                    fileUrls = JsonSerializer.Deserialize<List<string>>(r.EvidenceFileUrlsJson) ?? [];
                }
                catch
                {
                    fileUrls = [];
                }
            }

            list.Add(new SkillVerificationRequestResponse(
                r.Id,
                r.StudentId,
                student?.FullName ?? "Sinh viên",
                student?.StudentCode ?? "MSSV",
                student?.AvatarUrl,
                student?.Class,
                r.SkillId,
                skill?.Name ?? "Kỹ năng",
                skill?.Code ?? "SKILL",
                skill?.Category.ToString() ?? "TECHNICAL",
                skill?.BadgeImageUrl,
                r.ProjectId,
                project?.Title,
                project != null ? $"#PRJ-{project.Id.ToString()[..8].ToUpperInvariant()}" : null,
                r.AssignedLecturerId,
                lecturerName,
                r.Status.ToString(),
                r.Level,
                r.EvidenceUrl,
                fileUrls,
                r.StudentNote,
                r.LecturerFeedback,
                r.SubmittedAt,
                r.ReviewedAt));
        }

        return list;
    }

    private static SkillLevel ParseSkillLevel(string? levelStr)
    {
        if (string.IsNullOrWhiteSpace(levelStr)) return SkillLevel.ADVANCED;
        var s = levelStr.ToUpperInvariant();
        if (s.Contains("4") || s.Contains("CHUYÊN GIA") || s.Contains("EXPERT")) return SkillLevel.EXPERT;
        if (s.Contains("3") || s.Contains("NÂNG CAO") || s.Contains("ADVANCED")) return SkillLevel.ADVANCED;
        if (s.Contains("2") || s.Contains("TRUNG CẤP") || s.Contains("INTERMEDIATE")) return SkillLevel.INTERMEDIATE;
        return SkillLevel.BEGINNER;
    }

    private bool IsStudent() => currentUser.IsAuthenticated && currentUser.Roles.Contains(RoleNames.Student) && currentUser.StudentId.HasValue;
    private bool IsLecturer() => currentUser.IsAuthenticated && (currentUser.Roles.Contains(RoleNames.Lecturer) || currentUser.LecturerId.HasValue);
    private bool IsAdmin() => currentUser.IsAuthenticated && (currentUser.Roles.Contains(RoleNames.Admin) || currentUser.Roles.Contains(RoleNames.SuperAdmin));
}
