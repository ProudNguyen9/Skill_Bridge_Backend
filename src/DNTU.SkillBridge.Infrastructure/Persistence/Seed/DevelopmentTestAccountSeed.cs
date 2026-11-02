using DNTU.SkillBridge.Domain.Companies;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Domain.Students;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Seed;

public static class DevelopmentTestAccountSeed
{
    public const string Password = "SkillBridge.Test#2026";

    private sealed record StudentAccount(string Email, string DisplayName, string StudentCode);

    private sealed record CompanyAccount(string Email, string DisplayName, string CompanyName, string Slug,
        string TaxCode, string Description, string Address, string Phone);

    // 5 sinh viên + 1 admin + 1 giảng viên: dùng chung mật khẩu dev, email @skillbridge.local tự động verified.
    private static readonly StudentAccount[] Students =
    [
        new("student1.test@skillbridge.local", "Nguyễn Văn An", "TEST0001"),
        new("student2.test@skillbridge.local", "Trần Thị Bích", "TEST0002"),
        new("student3.test@skillbridge.local", "Lê Văn Cường", "TEST0003"),
        new("student4.test@skillbridge.local", "Phạm Thị Dung", "TEST0004"),
        new("student5.test@skillbridge.local", "Hoàng Văn Em", "TEST0005"),
        new("student1@skillbridge.local", "Sinh viên 1 (DNTU)", "21DTH0001"),
        new("student2@skillbridge.local", "Sinh viên 2 (DNTU)", "21DTH0002"),
        new("student3@skillbridge.local", "Sinh viên 3 (DNTU)", "21DTH0003")
    ];

    private const string AdminEmail = "admin.test@skillbridge.local";
    private const string AdminDisplayName = "Quản trị viên DNTU";
    private const string SuperAdminEmail = "superadmin.test@skillbridge.local";
    private const string SuperAdminDisplayName = "Quản trị cấp cao DNTU";

    private sealed record LecturerAccount(string Email, string DisplayName, string LecturerCode);
    private static readonly LecturerAccount[] Lecturers =
    [
        new("lecturer.test@skillbridge.local", "Giảng viên Trần Minh Đức", "GV001"),
        new("lecturer1@skillbridge.local", "Giảng viên DNTU", "GV002")
    ];

    // Doanh nghiệp: mỗi tài khoản gắn 1 công ty VERIFIED (dùng đăng bài toán được ngay).
    private static readonly CompanyAccount[] Companies =
    [
        new("company1@skillbridge.local", "Doanh nghiệp Đối tác", "Doanh nghiệp Đối tác",
            "doanh-nghiep-doi-tac", "0311999999",
            "Doanh nghiệp đối tác chiến lược DNTU, tài trợ học bổng và đồ án công nghệ.", "Biên Hòa, Đồng Nai", "+84901999999"),
        new("company1.test@skillbridge.local", "FPT Software Biên Hòa", "FPT Software Biên Hòa",
            "fpt-software-bien-hoa", "0311000001",
            "Đơn vị phát triển phần mềm outsourcing, mentor cho dự án AI và web.", "KCN Amata, Biên Hòa, Đồng Nai", "+84901000001"),
        new("company2.test@skillbridge.local", "Cảng Đồng Nai", "Cảng Đồng Nai",
            "cang-dong-nai", "0311000002",
            "Vận hành cảng biển, bài toán logistics và định vị bãi container.", "Cảng Long Bình, Đồng Nai", "+84901000002"),
        new("company3.test@skillbridge.local", "Viettel IDC Đồng Nai", "Viettel IDC Đồng Nai",
            "viettel-idc-dong-nai", "0311000003",
            "Trung tâm dữ liệu, bài toán giám sát hạ tầng và IoT.", "KCN Nhơn Trạch, Đồng Nai", "+84901000003"),
        new("company4.test@skillbridge.local", "GreenTech Smart Agriculture", "GreenTech Smart Agriculture",
            "greentech-smart-agriculture", "0311000004",
            "Nông nghiệp thông minh, bài toán IoT và phân tích dữ liệu cây trồng.", "Trảng Bom, Đồng Nai", "+84901000004"),
        new("company5.test@skillbridge.local", "Dong Nai Fintech Lab", "Dong Nai Fintech Lab",
            "dong-nai-fintech-lab", "0311000005",
            "Phòng thí nghiệm fintech, bài toán thanh toán và chấm điểm tín dụng.", "TP. Biên Hòa, Đồng Nai", "+84901000005")
    ];

    public static async Task SeedAsync(
        AppDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        CancellationToken cancellationToken)
    {
        var studentRole = await dbContext.Roles.SingleAsync(item => item.NormalizedName == RoleNames.Student, cancellationToken);
        var companyRole = await dbContext.Roles.SingleAsync(item => item.NormalizedName == RoleNames.Company, cancellationToken);
        var lecturerRole = await dbContext.Roles.SingleAsync(item => item.NormalizedName == RoleNames.Lecturer, cancellationToken);
        var adminRole = await dbContext.Roles.SingleAsync(item => item.NormalizedName == RoleNames.Admin, cancellationToken);
        var superAdminRole = await dbContext.Roles.SingleAsync(item => item.NormalizedName == RoleNames.SuperAdmin, cancellationToken);

        // Fetch master taxonomy references
        var fitFaculty = await dbContext.Faculties.FirstOrDefaultAsync(item => item.Code == "FIT", cancellationToken);
        var itMajor = await dbContext.Majors.FirstOrDefaultAsync(item => item.Code == "IT", cancellationToken);
        var itIndustry = await dbContext.Industries.FirstOrDefaultAsync(
            item => item.Slug == "cong-nghe-thong-tin" || item.NormalizedName.Contains("THÔNG TIN") || item.NormalizedName.Contains("THONG TIN"),
            cancellationToken);

        var index = 1;
        foreach (var student in Students)
        {
            var user = await EnsureUserAsync(dbContext, passwordHasher, student.Email, student.DisplayName, studentRole, cancellationToken);
            var profile = await dbContext.StudentProfiles.SingleOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
            if (profile is null)
            {
                profile = new StudentProfile(user.Id);
                dbContext.StudentProfiles.Add(profile);
            }

            // DB dev cũ có thể còn tài khoản legacy giữ mã sinh viên này — dời mã legacy ra trước khi gán.
            var codeOwner = await dbContext.StudentProfiles.SingleOrDefaultAsync(
                item => item.StudentCode == student.StudentCode && item.UserId != user.Id,
                cancellationToken);
            if (codeOwner is not null)
            {
                codeOwner.Update($"LEGACY{student.StudentCode}", codeOwner.MajorId, codeOwner.FacultyId,
                    codeOwner.AcademicYear, codeOwner.PhoneNumber, codeOwner.Bio,
                    codeOwner.GithubUrl, codeOwner.LinkedinUrl, codeOwner.PortfolioUrl, codeOwner.CvUrl);
            }

            profile.Update(
                student.StudentCode,
                itMajor?.Id,
                fitFaculty?.Id,
                "2022-2026",
                $"+8490100000{index}",
                "Sinh viên Khoa Công nghệ thông tin - DNTU (Chuyên ngành CNTT).",
                null, null, null, null);

            index++;
        }

        var admin = await EnsureUserAsync(dbContext, passwordHasher, AdminEmail, AdminDisplayName, adminRole, cancellationToken);
        var superAdmin = await EnsureUserAsync(dbContext, passwordHasher, SuperAdminEmail, SuperAdminDisplayName, superAdminRole, cancellationToken);
        _ = admin;
        _ = superAdmin;

        foreach (var lec in Lecturers)
        {
            var lecturer = await EnsureUserAsync(dbContext, passwordHasher, lec.Email, lec.DisplayName, lecturerRole, cancellationToken);
            var lecturerProfile = await dbContext.LecturerProfiles.SingleOrDefaultAsync(item => item.UserId == lecturer.Id, cancellationToken);
            if (lecturerProfile is null)
            {
                lecturerProfile = new LecturerProfile(lecturer.Id);
                dbContext.LecturerProfiles.Add(lecturerProfile);
            }

            lecturerProfile.Update(
                lec.LecturerCode,
                "Khoa Công nghệ thông tin",
                "Thạc sĩ",
                "Giảng viên hướng dẫn đồ án",
                null,
                "Phòng A1-201",
                "+84907654321",
                true,
                true);

            // Profile tạo trước khi có ApprovalStatus (migration default PENDING) — tài khoản dev luôn được duyệt.
            if (lecturerProfile.ApprovalStatus != LecturerApprovalStatus.APPROVED)
            {
                lecturerProfile.Approve(DateTimeOffset.UtcNow);
            }

            // Clear stale assignments
            var staleAssignments = await dbContext.LecturerAssignments
                .Where(la => la.LecturerId == lecturerProfile.Id)
                .ToListAsync(cancellationToken);
            if (staleAssignments.Count > 0)
            {
                dbContext.LecturerAssignments.RemoveRange(staleAssignments);
            }
        }

        foreach (var companyAccount in Companies)
        {
            var user = await EnsureUserAsync(dbContext, passwordHasher, companyAccount.Email, companyAccount.DisplayName, companyRole, cancellationToken);
            var member = await dbContext.CompanyMembers.SingleOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);

            var company = await dbContext.Companies.SingleOrDefaultAsync(c => c.TaxCode == companyAccount.TaxCode || c.Slug == companyAccount.Slug, cancellationToken);
            if (company is null)
            {
                company = new Company(companyAccount.CompanyName);
                company.AssignSlug(companyAccount.Slug);
                dbContext.Companies.Add(company);
            }

            company.UpdateProfile(
                companyAccount.CompanyName,
                companyAccount.TaxCode,
                itIndustry?.Id,
                $"https://{companyAccount.Slug}.skillbridge.local",
                companyAccount.Description,
                null,
                companyAccount.Address,
                companyAccount.Email,
                companyAccount.Phone);
            company.MarkVerified(DateTimeOffset.UtcNow, "Auto-verified seed account");

            if (member is null)
            {
                company.AddMember(user.Id, CompanyMemberRole.OWNER, "CEO & Project Director");
            }
        }

        // Clean stale draft projects for company1
        var company1 = await dbContext.Companies.SingleOrDefaultAsync(c => c.TaxCode == "0311000001", cancellationToken);
        if (company1 is not null)
        {
            var staleProjects = await dbContext.Projects
                .Include(p => p.Skills)
                .Include(p => p.Deliverables)
                .Where(p => p.CompanyId == company1.Id && p.Status == ProjectStatus.DRAFT)
                .ToListAsync(cancellationToken);
            if (staleProjects.Count > 0)
            {
                dbContext.Projects.RemoveRange(staleProjects);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Tạo (nếu chưa có) user dev, reset mật khẩu về mức kiểm thử và đảm bảo role được gán.</summary>
    private static async Task<User> EnsureUserAsync(
        AppDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        string email,
        string displayName,
        Role role,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(
            item => item.NormalizedEmail == email.ToUpperInvariant(),
            cancellationToken);

        if (user is null)
        {
            user = new User(email, displayName, string.Empty);
            dbContext.Users.Add(user);
        }

        // These reserved development accounts intentionally reset to a known password on startup.
        user.SetPasswordHash(passwordHasher.HashPassword(user, Password));
        user.SetEmailVerified();

        var hasRole = await dbContext.UserRoles.AnyAsync(
            assignment => assignment.UserId == user.Id && assignment.RoleId == role.Id,
            cancellationToken);
        if (!hasRole)
        {
            user.AssignRole(role.Id);
        }

        return user;
    }
}
