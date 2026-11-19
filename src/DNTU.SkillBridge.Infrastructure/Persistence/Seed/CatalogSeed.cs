using DNTU.SkillBridge.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Persistence.Seed;

/// <summary>
/// Seeds reference taxonomy only (skills, industries, faculties, majors, banks).
/// Never seeds business records, so it is safe for every environment including Production.
/// </summary>
public static class CatalogSeed
{
    public static async Task SeedAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        SeedSkills(dbContext);
        await SeedIndustriesAsync(dbContext, cancellationToken);
        await SeedFacultiesAndMajorsAsync(dbContext, cancellationToken);
        SeedBanks(dbContext);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void SeedSkills(AppDbContext dbContext)
    {
        var skills = new[]
        {
            new Skill("CLOUD_DEVOPS_CKA", "Certified Cloud & DevOps Specialist", SkillCategory.ENGINEERING, "Kiến trúc Docker & Kubernetes, CI/CD pipeline tự động và giám sát cloud.", "/assets/certificates/cloud-devops-cka.png"),
            new Skill("FULLSTACK_ENTERPRISE", "Fullstack React 19 & Enterprise Services", SkillCategory.TECHNICAL, "Phát triển web fullstack hiện đại, RESTful API và Clean Architecture.", "/assets/certificates/fullstack-java-react.png"),
            new Skill("AI_MLOPS", "AI Model Deployment & MLOps", SkillCategory.ENGINEERING, "Huấn luyện, đóng gói mô hình AI, tối ưu hóa suy luận và pipeline MLOps.", "/assets/certificates/ai-mlops.png"),
            new Skill("DATA_PIPELINE", "Microservices Data Pipeline & Kafka", SkillCategory.ENGINEERING, "Xây dựng hạ tầng xử lý dữ liệu lớn, luồng sự kiện phân tán và ETL pipeline.", "/assets/certificates/data-pipeline.png"),
            new Skill("CLEAN_ARCHITECTURE", "Clean Architecture & Hexagonal Design", SkillCategory.ENGINEERING, "Thiết kế kiến trúc sạch, phân tách domain logic, DDD và CQRS.", "/assets/certificates/clean-architecture.png"),
            new Skill("DEVSECOPS_SECURITY", "OWASP Security Testing & DevSecOps", SkillCategory.TECHNICAL, "Quản trị an toàn thông tin, rà soát lỗ hổng bảo mật và bảo vệ hạ tầng CI/CD.", "/assets/certificates/security-devsecops.png"),
            new Skill("ASPNET_CORE", "ASP.NET Core", SkillCategory.TECHNICAL, "Xây dựng API và dịch vụ backend với .NET.", "/assets/certificates/fullstack-java-react.png"),
            new Skill("REACT", "React", SkillCategory.TECHNICAL, "Xây dựng giao diện người dùng bằng React.", "/assets/certificates/fullstack-java-react.png"),
            new Skill("POSTGRESQL", "PostgreSQL", SkillCategory.TECHNICAL, "Thiết kế và truy vấn cơ sở dữ liệu PostgreSQL.", "/assets/certificates/data-pipeline.png"),
            new Skill("DOCKER", "Docker", SkillCategory.TECHNICAL, "Container hóa và triển khai ứng dụng.", "/assets/certificates/cloud-devops-cka.png"),
            new Skill("GIT", "Git", SkillCategory.TECHNICAL, "Quản lý mã nguồn và cộng tác nhóm.", "/assets/certificates/clean-architecture.png"),
            new Skill("TESTING", "Testing", SkillCategory.ENGINEERING, "Kiểm thử tự động: unit, integration, functional.", "/assets/certificates/security-devsecops.png"),
            new Skill("REQUIREMENT_ANALYSIS", "Requirement Analysis", SkillCategory.BUSINESS, "Thu thập và phân tích yêu cầu nghiệp vụ.", "/assets/certificates/clean-architecture.png"),
            new Skill("COMMUNICATION", "Communication", SkillCategory.COLLABORATION, "Giao tiếp với khách hàng và nhóm dự án.", "/assets/certificates/fullstack-java-react.png"),
            new Skill("TEAMWORK", "Teamwork", SkillCategory.COLLABORATION, "Làm việc nhóm và phối hợp công việc.", "/assets/certificates/cloud-devops-cka.png")
        };

        foreach (var skill in skills)
        {
            var existing = dbContext.Skills.FirstOrDefault(s => s.Code == skill.Code);
            if (existing is null)
            {
                dbContext.Skills.Add(skill);
            }
            else if (string.IsNullOrWhiteSpace(existing.BadgeImageUrl) && !string.IsNullOrWhiteSpace(skill.BadgeImageUrl))
            {
                existing.Update(existing.Name, existing.Category, existing.Description, skill.BadgeImageUrl);
            }
        }
    }

    private static async Task SeedIndustriesAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var existingNames = await dbContext.Industries
            .Select(industry => industry.NormalizedName)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken);

        var industries = new[]
        {
            new Industry("Công nghệ thông tin"),
            new Industry("Tài chính - Ngân hàng"),
            new Industry("Thương mại điện tử"),
            new Industry("Giáo dục"),
            new Industry("Y tế - Chăm sóc sức khỏe"),
            new Industry("Sản xuất"),
            new Industry("Logistics"),
            new Industry("Marketing - Truyền thông")
        };

        foreach (var industry in industries.Where(industry => !existingNames.Contains(industry.NormalizedName)))
        {
            dbContext.Industries.Add(industry);
        }
    }

    private static async Task SeedFacultiesAndMajorsAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var faculties = new[]
        {
            new Faculty("FIT", "Khoa Công nghệ thông tin"),
            new Faculty("FBA", "Khoa Quản trị kinh doanh"),
            new Faculty("FAC", "Khoa Kế toán - Kiểm toán"),
            new Faculty("FEE", "Khoa Điện - Điện tử"),
            new Faculty("FME", "Khoa Cơ khí - Ô tô"),
            new Faculty("FHT", "Khoa Du lịch - Nhà hàng - Khách sạn"),
            new Faculty("FNN", "Khoa Ngoại ngữ")
        };

        foreach (var faculty in faculties.Where(faculty => !dbContext.Faculties.Any(existing => existing.Code == faculty.Code)))
        {
            dbContext.Faculties.Add(faculty);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var facultyIdByCode = await dbContext.Faculties
            .ToDictionaryAsync(faculty => faculty.Code, faculty => faculty.Id, cancellationToken);

        var existingMajorNames = await dbContext.Majors
            .Select(major => major.NormalizedName)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken);

        var majorsByFaculty = new (string FacultyCode, string Code, string Name)[]
        {
            ("FIT", "SE", "Kỹ thuật phần mềm"),
            ("FIT", "IS", "Hệ thống thông tin"),
            ("FIT", "CS", "Khoa học máy tính"),
            ("FIT", "IT", "Công nghệ thông tin"),
            ("FIT", "CYS", "An ninh mạng"),
            ("FBA", "BA", "Quản trị kinh doanh"),
            ("FBA", "MKT", "Digital Marketing"),
            ("FBA", "HRM", "Quản trị nhân lực"),
            ("FAC", "ACC", "Kế toán"),
            ("FAC", "AUD", "Kiểm toán"),
            ("FEE", "ET", "Điện tử - Viễn thông"),
            ("FEE", "AUT", "Tự động hóa"),
            ("FME", "ME", "Công nghệ kỹ thuật cơ khí"),
            ("FME", "AUTECH", "Công nghệ kỹ thuật ô tô"),
            ("FHT", "TOUR", "Quản trị du lịch và lữ hành"),
            ("FHT", "HOTEL", "Quản trị nhà hàng và khách sạn"),
            ("FNN", "ENG", "Ngôn ngữ Anh"),
            ("FNN", "JP", "Ngôn ngữ Nhật")
        };

        foreach (var (facultyCode, code, name) in majorsByFaculty)
        {
            if (existingMajorNames.Contains(name.ToUpperInvariant()))
            {
                continue;
            }

            dbContext.Majors.Add(new Major(code, name, facultyIdByCode[facultyCode]));
        }
    }

    private static void SeedBanks(AppDbContext dbContext)
    {
        var banks = new[]
        {
            new Bank("VCB", "Vietcombank", "970436"),
            new Bank("TCB", "Techcombank", "970407"),
            new Bank("VPB", "VPBank", "970432"),
            new Bank("BIDV", "BIDV", "970418"),
            new Bank("CTG", "VietinBank", "970415"),
            new Bank("MBB", "MB Bank", "970422"),
            new Bank("ACB", "ACB", "970416"),
            new Bank("AGB", "Agribank", "970405"),
            new Bank("TPB", "TPBank", "970423"),
            new Bank("STB", "Sacombank", "970403"),
            new Bank("VIB", "VIB", "970441"),
            new Bank("SHB", "SHB", "970443"),
            new Bank("HDB", "HDBank", "970437"),
            new Bank("MSB", "MSB", "970426"),
            new Bank("OCB", "OCB", "970448"),
            new Bank("SEAB", "SeABank", "970440")
        };

        foreach (var bank in banks.Where(bank => !dbContext.Banks.Any(existing => existing.Code == bank.Code)))
        {
            dbContext.Banks.Add(bank);
        }
    }
}
