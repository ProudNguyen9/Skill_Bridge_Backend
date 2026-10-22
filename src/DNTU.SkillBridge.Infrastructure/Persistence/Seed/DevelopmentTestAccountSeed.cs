using DNTU.SkillBridge.Domain.Companies;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Students;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Seed;

public static class DevelopmentTestAccountSeed
{
    public const string Password = "SkillBridge.Test#2026";

    private static readonly (string Email, string DisplayName, string RoleName)[] Accounts =
    [
        ("student.test@skillbridge.local", "Sinh viên kiểm thử", RoleNames.Student),
        ("company.test@skillbridge.local", "Doanh nghiệp kiểm thử", RoleNames.Company),
        ("lecturer.test@skillbridge.local", "Giảng viên kiểm thử", RoleNames.Lecturer),
        ("admin.test@skillbridge.local", "Quản trị viên kiểm thử", RoleNames.Admin),
        ("superadmin.test@skillbridge.local", "Quản trị cấp cao kiểm thử", RoleNames.SuperAdmin)
    ];

    public static async Task SeedAsync(
        AppDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        CancellationToken cancellationToken)
    {
        foreach (var account in Accounts)
        {
            var role = await dbContext.Roles.SingleAsync(
                item => item.NormalizedName == account.RoleName,
                cancellationToken);
            var user = await dbContext.Users.SingleOrDefaultAsync(
                item => item.NormalizedEmail == account.Email.ToUpperInvariant(),
                cancellationToken);

            if (user is null)
            {
                user = new User(account.Email, account.DisplayName, string.Empty);
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

            if (account.RoleName == RoleNames.Student)
            {
                var studentProfile = await dbContext.StudentProfiles.SingleOrDefaultAsync(
                    item => item.UserId == user.Id,
                    cancellationToken);
                if (studentProfile is null)
                {
                    studentProfile = new StudentProfile(user.Id);
                    dbContext.StudentProfiles.Add(studentProfile);
                }

                if (string.IsNullOrWhiteSpace(studentProfile.StudentCode))
                {
                    studentProfile.Update("TEST0001", studentProfile.MajorId, studentProfile.FacultyId,
                        studentProfile.AcademicYear, studentProfile.PhoneNumber, studentProfile.Bio,
                        studentProfile.GithubUrl, studentProfile.LinkedinUrl, studentProfile.PortfolioUrl,
                        studentProfile.CvUrl);
                }
            }
            else if (account.RoleName == RoleNames.Company)
            {
                var member = await dbContext.CompanyMembers.SingleOrDefaultAsync(
                    item => item.UserId == user.Id,
                    cancellationToken);
                if (member is null)
                {
                    var company = await dbContext.Companies.SingleOrDefaultAsync(
                        c => c.TaxCode == "0312345678",
                        cancellationToken);
                    if (company is null)
                    {
                        company = new Company("Doanh Nghiệp Kiểm Thử DNTU");
                        company.AssignSlug("doanh-nghiep-kiem-thu-dntu");
                        company.UpdateProfile(
                            "Doanh Nghiệp Kiểm Thử DNTU",
                            "0312345678",
                            null,
                            "https://company.skillbridge.local",
                            "Công ty đối tác kiểm thử DNTU",
                            null,
                            "Đồng Nai",
                            "company.test@skillbridge.local",
                            "+84901234567");
                        company.MarkVerified(DateTimeOffset.UtcNow, "Auto-verified seed account");
                        dbContext.Companies.Add(company);
                    }
                    company.AddMember(user.Id, CompanyMemberRole.OWNER, "CEO & Project Director");
                }
            }
            else if (account.RoleName == RoleNames.Lecturer)
            {
                var lecturerProfile = await dbContext.LecturerProfiles.SingleOrDefaultAsync(
                    item => item.UserId == user.Id,
                    cancellationToken);
                if (lecturerProfile is null)
                {
                    lecturerProfile = new LecturerProfile(user.Id);
                    lecturerProfile.Update(
                        "GV001",
                        "Khoa Công Nghệ Thông Tin",
                        "Thạc Sĩ",
                        "Giảng viên hướng dẫn đồ án",
                        null,
                        "Phòng A1-201",
                        "+84907654321",
                        true,
                        true);
                    dbContext.LecturerProfiles.Add(lecturerProfile);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
