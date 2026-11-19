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
            // Cloud & DevOps
            new Skill("CLOUD_DEVOPS_CKA", "Certified Cloud & DevOps Specialist (CKA)", SkillCategory.ENGINEERING, "Vận hành và quản trị cụm Kubernetes, triển khai microservices, cấu hình HPA và cluster networking.", "/assets/certificates/kubernetes-cka.png"),
            new Skill("KUBERNETES_CKAD", "Certified Kubernetes Application Developer (CKAD)", SkillCategory.ENGINEERING, "Đóng gói, thiết kế và triển khai ứng dụng cloud-native trên nền tảng Kubernetes.", "/assets/certificates/kubernetes-ckad.png"),
            new Skill("AWS_SOLUTIONS_ARCHITECT", "AWS Certified Solutions Architect - Associate", SkillCategory.ENGINEERING, "Thiết kế hệ thống chịu tải cao, kiến trúc đám mây đa tầng, VPC, ECS, S3 và tối ưu chi phí trên AWS.", "/assets/certificates/aws-solutions-architect.png"),
            new Skill("AWS_CLOUD_PRACTITIONER", "AWS Certified Cloud Practitioner", SkillCategory.TECHNICAL, "Nền tảng dịch vụ đám mây AWS, bảo mật IAM, thanh toán và mô hình trách nhiệm chia sẻ.", "/assets/certificates/aws-cloud-practitioner.png"),
            new Skill("GCP_ASSOCIATE_ENGINEER", "Google Cloud Certified Associate Cloud Engineer", SkillCategory.ENGINEERING, "Triển khai giải pháp trên Google Cloud Platform, quản lý tài nguyên Compute Engine, GKE và Cloud SQL.", "/assets/certificates/gcp-associate-engineer.png"),
            new Skill("GITHUB_ACTIONS_CICD", "GitHub Actions & Automated CI/CD Pipelines", SkillCategory.ENGINEERING, "Xây dựng pipeline tự động hóa CI/CD, build, test, scan mã nguồn và release ứng dụng tự động trên GitHub.", "/assets/certificates/github-actions.png"),
            new Skill("LINUX_SYSTEM_ADMIN", "Linux System Administration & Shell Scripting", SkillCategory.TECHNICAL, "Quản trị hệ điều hành Linux (Ubuntu/Debian/RHEL), cấu hình server, quyền hạn, Bash shell scripting và SSH.", "/assets/certificates/linux-foundations.png"),
            new Skill("DOCKER", "Docker Containerization & Multi-stage Builds", SkillCategory.TECHNICAL, "Container hóa ứng dụng, viết Dockerfile tối ưu đa tầng (multi-stage) và quản lý Docker Compose.", "/assets/certificates/cloud-devops-cka.png"),

            // Software Engineering & Languages
            new Skill("FULLSTACK_ENTERPRISE", "Fullstack React 19 & Enterprise Services", SkillCategory.TECHNICAL, "Phát triển web fullstack hiện đại, Next.js App Router, Tailwind CSS, RESTful API và Clean Architecture.", "/assets/certificates/fullstack-java-react.png"),
            new Skill("REACT", "React 19 & Modern Web Architecture", SkillCategory.TECHNICAL, "Xây dựng giao diện Single Page Application hiện đại với React 19, Server Components, State Management và Hooks.", "/assets/certificates/fullstack-java-react.png"),
            new Skill("ASPNET_CORE", "ASP.NET Core & Modern Web API", SkillCategory.TECHNICAL, "Xây dựng API hiệu năng cao và dịch vụ backend doanh nghiệp với ASP.NET Core, EF Core và C#.", "/assets/certificates/clean-architecture.png"),
            new Skill("JAVA_ENTERPRISE", "Oracle Certified Java Enterprise Developer", SkillCategory.TECHNICAL, "Lập trình hướng đối tượng chuyên sâu, Java Core đa luồng, Spring Boot và xây dựng hệ thống doanh nghiệp.", "/assets/certificates/java-oracle.png"),
            new Skill("PYTHON_DEVELOPER", "PCAP™ – Certified Associate Python Programmer", SkillCategory.TECHNICAL, "Lập trình Python chuyên nghiệp, xử lý cấu trúc dữ liệu, OOP, module hóa và tích hợp API.", "/assets/certificates/python-pcap.png"),
            new Skill("PYTHON_ENTRY_LEVEL", "PCEP™ – Certified Entry-Level Python Programmer", SkillCategory.TECHNICAL, "Kỹ năng lập trình Python cơ bản, thuật toán, xử lý logic điều khiển và chuẩn PEP 8.", "/assets/certificates/python-pcep.png"),
            new Skill("CLEAN_ARCHITECTURE", "Clean Architecture & Hexagonal Design", SkillCategory.ENGINEERING, "Thiết kế kiến trúc sạch, phân tách domain logic, Domain-Driven Design (DDD), CQRS và Repository Pattern.", "/assets/certificates/clean-architecture.png"),

            // AI, Machine Learning & Data
            new Skill("AI_MLOPS", "AI Model Deployment & Enterprise MLOps", SkillCategory.ENGINEERING, "Huấn luyện, đóng gói mô hình AI, tối ưu hóa suy luận ONNX/TensorRT và giám sát drift model trong production.", "/assets/certificates/ai-mlops.png"),
            new Skill("GENAI_GEMINI", "Google Cloud: Build with Gemini & Generative AI", SkillCategory.ENGINEERING, "Tích hợp mô hình ngôn ngữ lớn Google Gemini, Prompt Engineering, Function Calling và Multimodal AI.", "/assets/certificates/genai-gemini.png"),
            new Skill("GENAI_VERTEX_AI", "Prompt Design & Agentic Workflows in Vertex AI", SkillCategory.TECHNICAL, "Thiết kế prompt chuẩn, hệ thống RAG (Retrieval-Augmented Generation) và xây dựng AI Agent trên Vertex AI.", "/assets/certificates/genai-vertexai.png"),
            new Skill("AI_AGENTIC_IBM", "IBM Generative and Agentic AI Architecture", SkillCategory.ENGINEERING, "Kiến trúc tác nhân thông minh (AI Agents), luồng suy luận tự động hóa và tích hợp tri thức doanh nghiệp.", "/assets/certificates/ai-agentic-ibm.png"),
            new Skill("DATA_PIPELINE", "Microservices Data Pipeline & Apache Kafka", SkillCategory.ENGINEERING, "Xây dựng hạ tầng xử lý dữ liệu lớn, hàng đợi tin nhắn phân tán Event-Driven Architecture với Kafka.", "/assets/certificates/data-pipeline.png"),
            new Skill("DATA_ANALYST_PYTHON", "PCAD™ – Certified Associate Data Analyst with Python", SkillCategory.TECHNICAL, "Phân tích và trực quan hóa dữ liệu với Pandas, NumPy, Matplotlib, Seaborn và khám phá dữ liệu thống kê (EDA).", "/assets/certificates/data-analyst-python.png"),
            new Skill("POSTGRESQL", "PostgreSQL Database Architecture & Query Tuning", SkillCategory.TECHNICAL, "Thiết kế mô hình cơ sở dữ liệu quan hệ, tối ưu hóa truy vấn SQL phức tạp, lập chỉ mục Indexing và Partitioning.", "/assets/certificates/database-sql-ibm.png"),
            new Skill("DATABASE_ORACLE_SQL", "Oracle Database SQL Certified Expert", SkillCategory.TECHNICAL, "Quản trị và khai thác CSDL Oracle, viết thủ tục lưu trữ PL/SQL, trigger và tối ưu hóa hiệu năng CSDL.", "/assets/certificates/database-oracle-sql.png"),

            // Cybersecurity, Networking & QA Testing
            new Skill("COMPTIA_SECURITY", "CompTIA Security+ Cybersecurity Professional", SkillCategory.TECHNICAL, "Kiến trúc an toàn thông tin, phòng chống tấn công mạng, quản lý danh tính, mã hóa dữ liệu và tuân thủ bảo mật.", "/assets/certificates/comptia-security.png"),
            new Skill("COMPTIA_NETWORK", "CompTIA Network+ Infrastructure Specialist", SkillCategory.TECHNICAL, "Thiết kế và cấu hình mạng máy tính, giao thức TCP/IP, VLAN, định tuyến, DNS/DHCP và khắc phục sự cố mạng.", "/assets/certificates/comptia-network.png"),
            new Skill("COMPTIA_CYSA", "CompTIA CySA+ Cybersecurity Analyst", SkillCategory.TECHNICAL, "Giám sát an ninh mạng SOC, phân tích log sự kiện bảo mật, điều tra phản ứng sự cố và đánh giá điểm yếu hệ thống.", "/assets/certificates/comptia-cysa.png"),
            new Skill("CISCO_CCNA", "Cisco CCNA: Enterprise Networking & Switching", SkillCategory.TECHNICAL, "Quản trị thiết bị mạng Cisco Switch & Router, cấu hình OSPF, Spanning Tree, ACL và bảo vệ hạ tầng mạng doanh nghiệp.", "/assets/certificates/cisco-ccna.png"),
            new Skill("CISCO_CYBERSECURITY", "Cisco Introduction to Cybersecurity", SkillCategory.TECHNICAL, "Nguyên lý an ninh mạng cơ bản, nhận diện mã độc malware, kỹ thuật tấn công phi kỹ thuật và chính sách bảo mật.", "/assets/certificates/cisco-cybersecurity.png"),
            new Skill("DEVSECOPS_SECURITY", "OWASP Security Testing & DevSecOps", SkillCategory.ENGINEERING, "Tích hợp kiểm thử bảo mật tự động SAST/DAST vào quy trình CI/CD, rà soát SonarQube và vá lỗi OWASP Top 10.", "/assets/certificates/security-devsecops.png"),
            new Skill("GITHUB_SECURITY", "GitHub Advanced Security & CodeQL", SkillCategory.ENGINEERING, "Phát hiện lỗ hổng tự động với CodeQL, quản lý bí mật Secret Scanning và phân tích phụ thuộc Dependabot.", "/assets/certificates/github-security.png"),
            new Skill("KUBERNETES_SECURITY_CKS", "CKS: Certified Kubernetes Security Specialist", SkillCategory.ENGINEERING, "Bảo vệ an toàn cụm Kubernetes trong quá trình build, deploy và runtime; quản lý RBAC và NetworkPolicy.", "/assets/certificates/kubernetes-security.png"),
            new Skill("TESTING", "Automated Testing & Quality Assurance", SkillCategory.ENGINEERING, "Kiểm thử tự động toàn diện: Unit Test, Integration Test, Functional Test và Playwright E2E.", "/assets/certificates/scrum-developer.png"),

            // Agile, Collaboration & Project Management
            new Skill("SCRUM_MASTER", "Professional Scrum Master™ I (PSM I)", SkillCategory.BUSINESS, "Lãnh đạo và tạo điều kiện cho nhóm phát triển theo khung Scrum, quản trị Sprint, Daily Scrum, Review và Retrospective.", "/assets/certificates/scrum-psm.png"),
            new Skill("SCRUM_PRODUCT_OWNER", "Professional Scrum Product Owner™ I (PSPO I)", SkillCategory.BUSINESS, "Quản lý và tối ưu giá trị sản phẩm, xây dựng Product Backlog, định nghĩa User Story và tiêu chí chấp nhận.", "/assets/certificates/scrum-pspo.png"),
            new Skill("SCRUM_DEVELOPER", "Professional Scrum Developer™ I (PSD I)", SkillCategory.TECHNICAL, "Thực hành phát triển phần mềm Agile hiện đại, kỹ thuật TDD, Pair Programming, CI và Code Refactoring.", "/assets/certificates/scrum-developer.png"),
            new Skill("SCRUM_UX", "Professional Scrum™ with User Experience (PSU I)", SkillCategory.BUSINESS, "Tích hợp thiết kế trải nghiệm người dùng (UX/UI Design) vào chu trình phát triển Agile và Design Thinking.", "/assets/certificates/scrum-ux.png"),
            new Skill("PROJECT_MANAGEMENT", "CompTIA Project+ IT Project Management", SkillCategory.BUSINESS, "Quản trị vòng đời dự án CNTT, lập kế hoạch tiến độ, phân bổ nguồn lực, quản trị rủi ro và giao tiếp các bên liên quan.", "/assets/certificates/project-management-comptia.png"),
            new Skill("GIT", "Git Version Control & Repository Management", SkillCategory.COLLABORATION, "Quản lý mã nguồn phân tán, quy trình phân nhánh GitFlow, giải quyết xung đột merge và Git Rebase.", "/assets/certificates/github-foundations.png"),
            new Skill("REQUIREMENT_ANALYSIS", "Business Requirement Analysis & System Modeling", SkillCategory.BUSINESS, "Thu thập và phân tích yêu cầu nghiệp vụ, mô hình hóa quy trình phần mềm UML và viết tài liệu SRS.", "/assets/certificates/clean-architecture.png"),
            new Skill("COMMUNICATION", "Professional Technical Communication", SkillCategory.COLLABORATION, "Giao tiếp kỹ thuật chuyên nghiệp, thuyết trình giải pháp, phản biện kiến trúc và làm việc với khách hàng.", "/assets/certificates/scrum-ux.png"),
            new Skill("TEAMWORK", "Cross-Functional Agile Teamwork", SkillCategory.COLLABORATION, "Phối hợp làm việc nhóm liên chức năng, giải quyết vấn đề kỹ thuật và thúc đẩy tinh thần đồng đội.", "/assets/certificates/github-foundations.png")
        };

        foreach (var skill in skills)
        {
            var existing = dbContext.Skills.FirstOrDefault(s => s.Code == skill.Code);
            if (existing is null)
            {
                dbContext.Skills.Add(skill);
            }
            else
            {
                // Luôn cập nhật đầy đủ thông tin chuẩn hóa và ảnh huy hiệu mới nhất
                existing.Update(skill.Name, skill.Category, skill.Description, skill.BadgeImageUrl);
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
