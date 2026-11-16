using System.Text.Json;

namespace DNTU.SkillBridge.Application.Administration;

/// <summary>
/// Nội dung gốc chính thức của trang Giới thiệu (/about) DNTU SkillBridge.
/// Tự động khởi tạo vào bảng admin_policy_settings (category = "cms_about") nếu DB chưa có bản ghi,
/// đảm bảo hệ thống CMS không bao giờ bị trống và không dùng mock tĩnh ở client.
/// </summary>
public static class AboutCmsDefaultPayload
{
    public const string CategoryKey = "cms_about";

    public static readonly string DefaultJson = JsonSerializer.Serialize(new
    {
        hero = new
        {
            eyebrowLabel = "VỀ DNTU SKILLBRIDGE",
            eyebrowCaption = "Khoa Công nghệ Thông tin",
            titleLine1 = "Từ kiến thức",
            titlePrefixLine2 = "đến",
            titleHighlight = "sản phẩm thật.",
            lead = "Nơi sinh viên, doanh nghiệp và giảng viên cùng thực hiện một dự án. Doanh nghiệp mang đến bài toán, giảng viên hướng dẫn, sinh viên xây dựng giải pháp.",
            primaryCtaLabel = "Khám phá bài toán",
            primaryCtaHref = "/projects",
            secondaryCtaLabel = "Tham gia cùng DNTU",
            secondaryCtaHref = "/register"
        },
        ecosystemRoles = new
        {
            student = new
            {
                id = "student",
                title = "Sinh viên DNTU",
                roleBadge = "Sinh viên",
                tagline = "Làm đồ án thật • Nhận phụ cấp",
                sublabel = "Xây dựng giải pháp",
                flowLabel = "Sản phẩm bàn giao",
                desc = "Nhận bài toán từ doanh nghiệp, làm sản phẩm thực tế, tích lũy hồ sơ năng lực và nhận trợ cấp theo tiến độ.",
                image = "/assets/dntu-students.jpg",
                keyHighlight = "Nhận đề tài doanh nghiệp, làm sản phẩm thật và nhận trợ cấp theo từng mốc hoàn thành.",
                deliverables = "Mã nguồn Git • Demo trực tiếp • Báo cáo kỹ thuật",
                actionText = "Xem bài toán",
                actionLink = "/projects"
            },
            company = new
            {
                id = "company",
                title = "Doanh nghiệp đối tác",
                roleBadge = "Doanh nghiệp",
                tagline = "Đưa đề tài • Nhận sản phẩm hoàn thiện",
                sublabel = "Đưa bài toán thực tế",
                flowLabel = "Bài toán đặt hàng",
                desc = "Đưa bài toán công nghệ vào trường, cử mentor đồng hành chuyên môn và tiếp cận nhân sự chất lượng sớm.",
                image = "/assets/dntu-computer-lab.jpg",
                keyHighlight = "Đặt hàng đề tài thực tế, đồng hành cùng đội ngũ sinh viên và tuyển dụng nhân tài sớm.",
                deliverables = "Đề tài thực tế • Tiêu chí bàn giao • Quỹ phụ cấp mốc",
                actionText = "Đăng ký bài toán",
                actionLink = "/register"
            },
            teacher = new
            {
                id = "teacher",
                title = "Giảng viên hướng dẫn",
                roleBadge = "Giảng viên",
                tagline = "Bảo trợ chuyên môn • Đánh giá đồ án",
                sublabel = "Đồng hành chuyên môn",
                flowLabel = "Hướng dẫn học thuật",
                desc = "Đồng hành định hướng kỹ thuật, review chất lượng mã nguồn và đánh giá đồ án theo chuẩn đầu ra của khoa.",
                image = "/assets/dntu-engineering.jpg",
                keyHighlight = "Đồng hành chuyên môn, hướng dẫn giải thuật và đánh giá đồ án theo chuẩn đầu ra.",
                deliverables = "Review mã nguồn • Hướng dẫn kỹ thuật • Bảng điểm đồ án",
                actionText = "Xem quy trình",
                actionLink = "#pipeline-section"
            },
            center = new
            {
                id = "center",
                title = "SkillBridge Hub",
                roleBadge = "Cầu nối",
                tagline = "Kết nối 3 bên • Kiến tạo giá trị thật",
                sublabel = "Không gian kết nối",
                flowLabel = "Cốt lõi liên kết",
                desc = "Nền tảng kết nối Doanh nghiệp, Sinh viên và Giảng viên từ khi nhận đề bài đến khi bàn giao sản phẩm.",
                image = "/assets/teams-hero-3d-showcase.png",
                keyHighlight = "Kết nối 3 bên xuyên suốt từ tiếp nhận đề tài, theo dõi tiến độ đến nghiệm thu.",
                deliverables = "Workspace 3 bên • Bàn giao định kỳ • Quản lý tiến độ",
                actionText = "Khám phá Hub",
                actionLink = "/projects"
            }
        },
        pillarsSection = new
        {
            eyebrow = "MÔ HÌNH HỢP TÁC",
            title = "Ba trụ cột tạo nên giá trị thực tế",
            subtitle = "Sự liên kết chặt chẽ đảm bảo dự án vừa đáp ứng nhu cầu thực tế của doanh nghiệp, vừa chuẩn hóa kiến thức học thuật.",
            linkLabel = "Khám phá dự án đang mở",
            linkHref = "/projects",
            roadImageUrl = "/assets/about-road-v2.png",
            pillars = new object[]
            {
                new
                {
                    id = "student",
                    num = "01",
                    tag = "SINH VIÊN",
                    title = "Học từ dự án thật",
                    description = "Trải nghiệm môi trường làm việc thực thụ, giải quyết bài toán nghiệp vụ cụ thể của doanh nghiệp thay vì các bài tập giả định hay đồ án mẫu trên giảng đường.",
                    bullets = new[]
                    {
                        "Làm việc nhóm theo mô hình Agile/Kanban chuyên nghiệp.",
                        "Xác thực kỹ năng thực chiến vào Skill Passport số hóa.",
                        "Cơ hội nhận phụ cấp hỗ trợ và học bổng trực tiếp từ doanh nghiệp."
                    },
                    timecode = "00:01:24",
                    imageUrl = "/assets/dntu-computer-lab.jpg",
                    imageAlt = "Phòng Lab & Dự án Công nghệ DNTU",
                    captionPill = "Phòng Lab & Dự án Công nghệ DNTU"
                },
                new
                {
                    id = "enterprise",
                    num = "02",
                    tag = "DOANH NGHIỆP",
                    title = "Đưa bài toán, tìm cộng sự",
                    description = "Tiếp cận nguồn lực công nghệ trẻ đầy nhiệt huyết, nhận về giải pháp phần mềm cụ thể nhằm giải quyết đúng nỗi đau vận hành chuyển đổi số.",
                    bullets = new[]
                    {
                        "Giải quyết bài toán chuyển đổi số thiết thực, tiết kiệm chi phí R&D.",
                        "Tham gia nghiệm thu sản phẩm thực tế theo từng mốc Sprint.",
                        "Phát hiện và tuyển dụng nhân sự tài năng ngay từ khi còn trên ghế nhà trường."
                    },
                    timecode = "00:02:48",
                    imageUrl = "/assets/dntu-laboratory.jpg",
                    imageAlt = "Hợp tác chuyển giao giải pháp phần mềm DNTU",
                    captionPill = "Hợp tác chuyển giao giải pháp phần mềm"
                },
                new
                {
                    id = "lecturer",
                    num = "03",
                    tag = "GIẢNG VIÊN & KHOA CNTT",
                    title = "Đồng hành về chuyên môn",
                    description = "Đồng hành định hướng công nghệ, kiểm duyệt giải pháp kiến trúc hệ thống và bảo đảm chất lượng sản phẩm chuẩn hóa kiến thức học thuật.",
                    bullets = new[]
                    {
                        "Kiểm duyệt tính khả thi và phạm vi kỹ thuật của giải pháp.",
                        "Đánh giá khách quan, đo lường năng lực sinh viên theo khung chuẩn Rubric.",
                        "Cải tiến liên tục chương trình đào tạo luôn tiệm cận với thị trường."
                    },
                    timecode = "00:04:12",
                    imageUrl = "/assets/dntu-engineering.jpg",
                    imageAlt = "Nghiên cứu & Cố vấn Học thuật DNTU",
                    captionPill = "Nghiên cứu & Cố vấn Học thuật DNTU"
                }
            }
        },
        ctaSection = new
        {
            eyebrow = "KẾT NỐI NGAY HÔM NAY",
            title = "Sẵn sàng cùng DNTU giải quyết bài toán thực tế?",
            subtitle = "Dù bạn là sinh viên muốn thử sức hay doanh nghiệp tìm kiếm giải pháp, SkillBridge luôn có không gian dành cho bạn.",
            primaryLabel = "Đăng ký tham gia ngay",
            primaryHref = "/register",
            secondaryLabel = "Xem danh mục bài toán",
            secondaryHref = "/projects"
        }
    });
}
