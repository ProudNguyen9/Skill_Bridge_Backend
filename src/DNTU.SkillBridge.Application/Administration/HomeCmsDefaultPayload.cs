namespace DNTU.SkillBridge.Application.Administration;

/// <summary>Nội dung mặc định của trang chủ SkillBridge, lưu trong admin_policy_settings.</summary>
public static class HomeCmsDefaultPayload
{
    public const string CategoryKey = "cms_home";

    public const string DefaultJson = """
    {
      "sectionNav": [
        { "id": "overview", "label": "Tổng quan" },
        { "id": "projects", "label": "Bài toán doanh nghiệp" },
        { "id": "workspace", "label": "Mô hình 3 bên" },
        { "id": "passport", "label": "Hồ sơ năng lực" },
        { "id": "partners", "label": "Đối tác" },
        { "id": "get-started", "label": "Bắt đầu" }
      ],
      "header": {
        "brand": { "kicker": "DNTU", "name": "SkillBridge", "subtitle": "Kết nối tri thức & thực tiễn", "imageUrl": "/assets/dntu-logo.png", "imageAlt": "DNTU" },
        "universityName": "TRƯỜNG ĐẠI HỌC CÔNG NGHỆ ĐỒNG NAI",
        "universityLinkLabel": "Cổng thông tin DNTU",
        "universityLinkHref": "https://dntu.edu.vn",
        "navLinks": [
          { "label": "Trang chủ", "href": "/" },
          { "label": "Bài toán doanh nghiệp", "href": "/projects" },
          { "label": "Tin tức", "href": "/news" },
          { "label": "Về SkillBridge", "href": "/about" }
        ],
        "loginLabel": "Đăng nhập",
        "joinLabel": "Tham gia ngay",
        "joinHref": "/register"
      },
      "hero": {
        "eyebrowLabel": "DNTU SKILLBRIDGE",
        "eyebrowCaption": "Khoa Công nghệ Thông tin",
        "titleBefore": "Mang kiến thức của bạn đến với những",
        "titleHighlight": "bài toán thực tế",
        "titleAfter": ".",
        "lead": "Không gian kết nối sinh viên, giảng viên và doanh nghiệp cùng giải quyết các bài toán thực tiễn, kiến tạo sản phẩm chất lượng và ghi nhận năng lực qua Skill Passport.",
        "primaryLabel": "Bắt đầu ngay",
        "primaryTarget": "get-started",
        "secondaryLabel": "Khám phá bài toán",
        "secondaryHref": "/projects",
        "tertiaryLabel": "Mô hình 3 bên",
        "tertiaryTarget": "workspace",
        "searchLabel": "Tìm bài toán hoặc kỹ năng",
        "searchPlaceholder": "Tìm theo bài toán, lĩnh vực hoặc kỹ năng...",
        "searchButtonLabel": "Tìm bài toán",
        "suggestionsLabel": "Gợi ý phổ biến:",
        "suggestions": ["React", "Dashboard", "IoT", "Logistics", "Mobile"],
        "visual": {
          "stageImageUrl": "/assets/4_buc_san_khau_va_trang_tri_ben_phai.png",
          "stageAlt": "Bục sân khấu và con đường kết nối thực tiễn DNTU",
          "decorImageUrl": "/assets/2_cum_3D_ben_trai_tach_nen.png",
          "decorAlt": "Cụm khối 3D biểu tượng tương tác DNTU",
          "screenImageUrl": "/assets/4_man_hinh_cuoc_hop_tach_nen.png",
          "screenAlt": "Màn hình hội đồng bảo vệ đồ án CNTT DNTU và Copilot",
          "phoneImageUrl": "/assets/5_dien_thoai_Copilot_tach_nen.png",
          "phoneAlt": "Trợ lý DNTU AI Copilot trên di động",
          "badgeLabel": "Lịch nghiệm thu đồ án"
        }
      },
      "projects": {
        "eyebrow": "CƠ HỘI NỔI BẬT",
        "title": "Bài toán thực tế từ doanh nghiệp",
        "subtitle": "Các bài toán đang mở tuyển sinh viên giải quyết cùng sự đồng hành, bảo trợ chuyên môn từ giảng viên khoa CNTT.",
        "allLabel": "Khám phá tất cả",
        "categories": [
          { "id": "all", "label": "Tất cả bài toán" },
          { "id": "web", "label": "Web & ứng dụng" },
          { "id": "data", "label": "Dữ liệu & IoT" }
        ]
      },
      "workspace": {
        "eyebrow": "MÔ HÌNH HỢP TÁC",
        "title": "Ba bên cùng đồng hành trong từng dự án",
        "subtitle": "Chọn vai trò để xem phần việc cụ thể, người đồng hành và các cột mốc kiểm thử tiếp theo.",
        "ctaLabel": "Doanh nghiệp đăng bài toán",
        "ctaHref": "/company/projects/create",
        "roles": [
          {
            "id": "student", "roleName": "Sinh viên", "title": "SINH VIÊN THỰC CHIẾN", "shortSubtitle": "Làm dự án thật cùng đội nhóm",
            "points": [
              { "label": "Thực hiện", "text": "Chọn đề bài từ doanh nghiệp, lập nhóm và phát triển sản phẩm theo sprint thực tế." },
              { "label": "Đồng hành", "text": "Được giảng viên khoa CNTT và chuyên gia doanh nghiệp trực tiếp bảo trợ chuyên môn." },
              { "label": "Đầu ra", "text": "Tích lũy minh chứng Skill Passport số và mở rộng cơ hội tuyển dụng chính thức." }
            ],
            "actionText": "KHÁM PHÁ DỰ ÁN", "actionLink": "/student/dashboard", "cardTag": "01 • SINH VIÊN",
            "photoTitle": "ĐỘI NGŨ KỸ THUẬT LAB DNTU", "photoSubtitle": "Sinh viên nghiên cứu giải pháp, lập trình và bảo vệ đồ án thực tế",
            "image": "/assets/dntu-students.jpg", "fallbackImage": "/assets/dntu-computer-lab.jpg",
            "previewTitle": "Nhận việc, làm cùng đội nhóm", "previewDesc": "Chọn một bài toán và biến ý tưởng thành sản phẩm có thể nghiệm thu."
          },
          {
            "id": "company", "roleName": "Doanh nghiệp", "title": "DOANH NGHIỆP ĐỐI TÁC", "shortSubtitle": "Đặt bài toán, nhận giải pháp",
            "points": [
              { "label": "Đặt bài toán", "text": "Đưa ra nhu cầu thực tế, cung cấp dữ liệu mẫu và tiêu chuẩn nghiệm thu rõ ràng." },
              { "label": "Cố vấn", "text": "Cử kỹ sư đồng hành định hướng nghiệp vụ, theo sát tiến độ giải pháp của sinh viên." },
              { "label": "Tiếp nhận", "text": "Tiếp nhận sản phẩm giải pháp hoàn thiện, tối ưu chi phí và tiếp cận sớm nhân tài." }
            ],
            "actionText": "THAM GIA CÙNG DNTU", "actionLink": "/company/dashboard", "cardTag": "02 • DOANH NGHIỆP TRỌNG TÂM",
            "photoTitle": "CHỦ QUẢN ĐẦU BÀI & TÀI TRỢ DỮ LIỆU", "photoSubtitle": "Hội đồng doanh nghiệp duyệt lộ trình chuyển giao công nghệ",
            "image": "/assets/dntu-computer-lab.jpg", "fallbackImage": "/assets/teams-hero-3d-showcase.png",
            "previewTitle": "Đặt bài toán, nhận sản phẩm", "previewDesc": "Gửi nhu cầu, xem tiến độ và nghiệm thu theo từng mốc."
          },
          {
            "id": "lecturer", "roleName": "Giảng viên", "title": "GIẢNG VIÊN & CỐ VẤN", "shortSubtitle": "Cố vấn và đánh giá dự án",
            "points": [
              { "label": "Thẩm định", "text": "Thẩm định tính thực tiễn của đề bài, đảm bảo chuẩn mực học thuật của khoa CNTT." },
              { "label": "Bảo trợ", "text": "Hướng dẫn phương pháp luận nghiên cứu, giám sát mã nguồn và tiến độ đồ án." },
              { "label": "Đánh giá", "text": "Chấm điểm nghiệm thu chính quy, xác thực minh chứng năng lực số cho sinh viên." }
            ],
            "actionText": "VÀO KHÔNG GIAN GIẢNG VIÊN", "actionLink": "/lecturer/dashboard", "cardTag": "03 • GIẢNG VIÊN",
            "photoTitle": "HỘI ĐỒNG & MENTOR DNTU", "photoSubtitle": "Khoa CNTT - DNTU bảo trợ phương pháp luận và chuẩn mực giải pháp",
            "image": "/assets/dntu-engineering.jpg", "fallbackImage": "/assets/dntu-laboratory.jpg",
            "previewTitle": "Dẫn dắt chất lượng", "previewDesc": "Góp ý đúng lúc, giữ chuẩn học thuật và xác thực năng lực."
          }
        ]
      },
      "passport": {
        "eyebrow": "DNTU SKILL PASSPORT",
        "title": "Năng lực được ghi nhận từ chính điều bạn làm",
        "lead": "Mỗi dự án hoàn thành là một dấu mốc đáng tin cậy trong hồ sơ của bạn, với sản phẩm thực tế, đóng góp cụ thể và đánh giá chính thức từ giảng viên hướng dẫn.",
        "imageUrl": "/assets/dntu-students.jpg",
        "imageAlt": "Sinh viên DNTU cùng trao đổi trên máy tính tại khuôn viên trường",
        "imageCaption": "Sinh viên Khoa CNTT Trường Đại học Công nghệ Đồng Nai",
        "benefits": [
          { "title": "Sản phẩm thực tế trong hồ sơ năng lực", "description": "Minh chứng năng lực qua mã nguồn, tài liệu và sản phẩm được triển khai." },
          { "title": "Kỹ năng được giảng viên xác thực", "description": "Được chấm điểm, ghi nhận chuyên môn chính quy từ khoa CNTT." },
          { "title": "Kết nối cơ hội tuyển dụng cùng doanh nghiệp", "description": "Rút ngắn khoảng cách từ giảng đường đến các công ty công nghệ hàng đầu." }
        ],
        "actionLabel": "Khám phá hồ sơ năng lực",
        "actionHref": "/student/portfolio"
      },
      "audience": {
        "eyebrow": "BẮT ĐẦU HÀNH TRÌNH",
        "title": "Chọn lối đi phù hợp với bạn trên DNTU SkillBridge",
        "subtitle": "Hệ sinh thái mở kết nối toàn diện sinh viên, doanh nghiệp, giảng viên và các đơn vị nghiên cứu ứng dụng thực tế.",
        "cards": [
          {
            "id": "student", "roleLabel": "Dành cho Sinh viên", "title": "Làm đồ án thật - Nhận trợ cấp thật",
            "description": "Nhận bài toán thực tế trực tiếp từ công ty, giảng viên hướng dẫn bảo trợ, tích lũy điểm học phần chuyên ngành và chứng nhận năng lực số.",
            "badges": ["Có phụ cấp dự án", "Tích lũy Skill Passport", "Quy đổi điểm đồ án môn"], "actionLabel": "Khám phá bài toán", "actionHref": "/projects"
          },
          {
            "id": "company", "roleLabel": "Dành cho Doanh nghiệp", "title": "Đưa bài toán vào trường - Nhận sản phẩm",
            "description": "Tiếp cận sinh viên công nghệ được đào tạo bài bản, nghiệm thu mã nguồn theo chuẩn công nghiệp và tối ưu chi phí R&D.",
            "badges": ["Nghiệm thu theo Sprint", "Giảng viên thẩm định", "Tuyển dụng nhân tài sớm"], "actionLabel": "Đăng bài toán ngay", "actionHref": "/company/projects/create"
          },
          {
            "id": "lecturer", "roleLabel": "Dành cho Giảng viên", "title": "Bảo trợ học thuật - Chuyển giao tri thức",
            "description": "Đóng vai trò Mentor chuyên môn, giám sát tiến độ Git/Clean Code và chấm điểm chuẩn mực theo Rubric khoa CNTT.",
            "badges": ["Chuẩn Rubric Khoa", "Giám sát Git và Code quality", "Minh chứng NCKH"], "actionLabel": "Vào cổng giảng viên", "actionHref": "/lecturer/dashboard"
          },
          {
            "id": "faculty", "roleLabel": "Khoa & Đơn vị liên kết", "title": "Chuẩn hóa liên kết đào tạo ứng dụng",
            "description": "Giám sát toàn diện các chỉ số liên kết doanh nghiệp, lưu trữ minh chứng chuẩn đầu ra ABET/AUN và nâng cao chất lượng đào tạo.",
            "badges": ["Chuẩn đầu ra ABET/AUN", "Dashboard đo lường KPI", "Lưu trữ minh chứng số"], "actionLabel": "Tìm hiểu mô hình", "actionHref": "/about"
          }
        ]
      },
      "footer": {
        "description": "Kết nối sinh viên DNTU với bài toán doanh nghiệp. Cùng tạo nên những giá trị từ kiến thức và trải nghiệm.",
        "columns": [
          { "title": "Khám phá", "links": [
            { "label": "Bài toán doanh nghiệp", "href": "/projects" },
            { "label": "Tin tức & Sự kiện", "href": "/news" },
            { "label": "Về SkillBridge", "href": "/about" },
            { "label": "Hồ sơ năng lực", "href": "/student/portfolio" }
          ] },
          { "title": "Dành cho bạn", "links": [
            { "label": "Sinh viên", "href": "/student/dashboard" },
            { "label": "Doanh nghiệp", "href": "/company/dashboard" },
            { "label": "Giảng viên", "href": "/lecturer/dashboard" },
            { "label": "Quản trị khoa", "href": "/admin/dashboard" }
          ] }
        ],
        "contactTitle": "Kết nối với DNTU",
        "department": "Khoa Công nghệ Thông tin",
        "university": "Trường Đại học Công nghệ Đồng Nai",
        "email": "fit@dntu.edu.vn",
        "emailHref": "mailto:fit@dntu.edu.vn",
        "websiteLabel": "Website nhà trường",
        "websiteHref": "https://dntu.edu.vn",
        "copyright": "© 2026 DNTU SkillBridge",
        "tagline": "Tri thức kết nối. Giá trị lan tỏa."
      }
    }
    """;
}
