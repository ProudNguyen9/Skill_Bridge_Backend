using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DNTU.SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRelatedNewsArticles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "news_articles",
                columns: new[]
                {
                    "Id", "Slug", "Title", "Category", "Excerpt", "ContentJson", "CoverImageUrl",
                    "CoverImageAlt", "Source", "ReadTimeMinutes", "Status", "PublishAtUtc",
                    "UnpublishAtUtc", "CreatedAt", "UpdatedAt"
                },
                values: new object[,]
                {
                    {
                        Guid.Parse("11111111-1111-4111-8111-000000000006"), "doanh-nghiep-cung-sinh-vien-hoan-thien-du-an",
                        "Doanh nghiệp cùng sinh viên hoàn thiện dự án từ nhu cầu thực tế", "Hợp tác Doanh nghiệp",
                        "Đề bài có mục tiêu, dữ liệu và các mốc phản hồi rõ ràng giúp doanh nghiệp cùng sinh viên phối hợp hiệu quả trong suốt dự án.",
                        ContentJson(
                            "Buổi trao đổi đầu tiên nên làm rõ người sử dụng, quy trình hiện tại và điểm đang tốn thời gian. Từ đó, nhóm có thể thống nhất phạm vi thử nghiệm vừa đủ để hoàn thành trong thời lượng học tập.",
                            "Trong quá trình triển khai, đại diện doanh nghiệp phản hồi theo từng mốc thay vì chờ tới cuối. Giảng viên hỗ trợ nhóm chuyển góp ý thành đầu việc và tiêu chí nghiệm thu cụ thể.",
                            "Khi bàn giao, sinh viên mô tả cách sử dụng, dữ liệu mẫu và giới hạn còn lại. Hồ sơ ngắn này giúp doanh nghiệp cân nhắc bước tiếp theo, đồng thời ghi nhận rõ phần đóng góp của từng thành viên."),
                        "/assets/dntu-computer-lab.jpg", "Sinh viên DNTU làm việc trong phòng máy",
                        "Ban biên tập DNTU SkillBridge", 4, "Published",
                        new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero), null,
                        new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero), null
                    },
                    {
                        Guid.Parse("11111111-1111-4111-8111-000000000007"), "giai-phap-quan-ly-kho-tu-bai-toan-thuc-te",
                        "Xây dựng hệ thống quản lý kho từ yêu cầu đến nguyên mẫu", "Dự án Sinh viên",
                        "Một bài toán quản lý kho có thể trở thành dự án học tập rõ ràng khi nhóm bắt đầu từ luồng nhập, xuất và kiểm kê hàng hóa.",
                        ContentJson(
                            "Trước khi thiết kế giao diện, nhóm cần ghi lại các bước đang diễn ra: ai nhập hàng, ai xác nhận xuất kho và thông tin nào cần tra cứu. Sơ đồ luồng giúp phát hiện sớm những trường hợp dễ bị bỏ sót.",
                            "Nguyên mẫu đầu tiên có thể tập trung vào danh mục hàng, phiếu nhập, phiếu xuất và số lượng tồn. Nhóm nên thử với dữ liệu mẫu để kiểm tra cách xử lý hàng trùng mã, số lượng âm và lịch sử cập nhật.",
                            "Sau khi nhận góp ý, nhóm hoàn thiện báo cáo nêu rõ chức năng đã làm, phần chưa triển khai và hướng mở rộng như phân quyền hoặc cảnh báo tồn thấp. Đây là cách trình bày minh bạch về chất lượng và giới hạn của sản phẩm."),
                        "/assets/dntu-students.jpg", "Sinh viên cùng trao đổi về dự án học tập",
                        "Ban biên tập DNTU SkillBridge", 5, "Published",
                        new DateTimeOffset(2026, 11, 15, 1, 0, 0, TimeSpan.Zero), null,
                        new DateTimeOffset(2026, 11, 15, 1, 0, 0, TimeSpan.Zero), null
                    },
                    {
                        Guid.Parse("11111111-1111-4111-8111-000000000008"), "dien-toan-dam-may-cho-du-an-sinh-vien",
                        "Điện toán đám mây trong dự án học tập: cân nhắc trước khi triển khai", "Hội thảo Công nghệ",
                        "Đưa sản phẩm lên cloud giúp nhóm dễ chia sẻ bản dùng thử, nhưng cần tính đến chi phí, dữ liệu và quyền truy cập ngay từ đầu.",
                        ContentJson(
                            "Với một nguyên mẫu nhỏ, nhóm có thể bắt đầu bằng môi trường triển khai đơn giản và dữ liệu không nhạy cảm. Mục tiêu ban đầu là giúp giảng viên, thành viên nhóm hoặc đối tác xem được sản phẩm để góp ý.",
                            "Trước khi chọn dịch vụ, hãy ước lượng tài nguyên cần dùng, cách quản lý khóa truy cập và thời gian lưu dữ liệu. Tắt tài nguyên không cần thiết và đặt cảnh báo chi phí giúp tránh phát sinh ngoài dự kiến.",
                            "Khi trình bày phương án, sinh viên nên giải thích vì sao chọn cloud thay cho chạy cục bộ, cách bảo vệ thông tin và phương án sao lưu. Lựa chọn phù hợp với mục tiêu dự án quan trọng hơn việc dùng thật nhiều dịch vụ."),
                        "/assets/news-aws-workshop.jpg", "Sinh viên tìm hiểu chủ đề công nghệ",
                        "Ban biên tập DNTU SkillBridge", 4, "Published",
                        new DateTimeOffset(2026, 11, 15, 2, 0, 0, TimeSpan.Zero), null,
                        new DateTimeOffset(2026, 11, 15, 2, 0, 0, TimeSpan.Zero), null
                    },
                    {
                        Guid.Parse("11111111-1111-4111-8111-000000000009"), "ho-so-thuc-tap-minh-chung-san-pham-phan-mem",
                        "Chuẩn bị hồ sơ thực tập với minh chứng từ sản phẩm phần mềm", "Tuyển dụng & Thực tập",
                        "Một dự án được trình bày mạch lạc giúp nhà tuyển dụng hiểu vai trò, cách giải quyết vấn đề và kỹ năng bạn đã thực hành.",
                        ContentJson(
                            "Chọn sản phẩm có phần việc cá nhân cụ thể thay vì liệt kê thật nhiều công nghệ. Hãy nêu ngắn gọn người dùng, vấn đề, chức năng chính và quyết định kỹ thuật mà bạn trực tiếp tham gia.",
                            "Đính kèm đường dẫn mã nguồn hoặc bản demo có hướng dẫn truy cập. Nếu sản phẩm làm theo nhóm, ghi rõ phần bạn phụ trách, cách phối hợp và cách kiểm tra kết quả.",
                            "Khi phỏng vấn, hãy chuẩn bị một ví dụ về lỗi hoặc trở ngại đã gặp và cách bạn xử lý. Minh chứng rõ ràng cùng phần tự đánh giá trung thực giúp cuộc trao đổi đi sâu hơn danh sách kỹ năng."),
                        "/assets/dntu-laboratory.jpg", "Sinh viên chuẩn bị hồ sơ dự án",
                        "Ban biên tập DNTU SkillBridge", 4, "Published",
                        new DateTimeOffset(2026, 11, 15, 3, 0, 0, TimeSpan.Zero), null,
                        new DateTimeOffset(2026, 11, 15, 3, 0, 0, TimeSpan.Zero), null
                    },
                    {
                        Guid.Parse("11111111-1111-4111-8111-000000000010"), "luu-y-de-xuat-du-an-skillbridge",
                        "Lưu ý trước khi gửi đề xuất dự án trên SkillBridge", "Thông báo Khoa",
                        "Chuẩn bị trước thông tin về vấn đề, người dùng, phạm vi và người hướng dẫn giúp đề xuất dự án dễ được trao đổi hơn.",
                        ContentJson(
                            "Mô tả vấn đề bằng tình huống cụ thể và cho biết ai đang gặp khó khăn. Nếu đã trao đổi với doanh nghiệp hoặc đơn vị liên quan, hãy ghi lại nhu cầu và đầu mối để các bên tiện xác nhận.",
                            "Nêu chức năng cốt lõi, kết quả dự kiến và cách kiểm tra sản phẩm. Chia phạm vi thành phần bắt buộc và phần có thể mở rộng để dự án vừa với thời lượng học tập.",
                            "Trước khi gửi, rà soát dữ liệu cần dùng, quyền truy cập, thông tin liên hệ và giảng viên dự kiến hướng dẫn. Không đính kèm dữ liệu cá nhân hoặc tài liệu nội bộ nếu chưa được phép chia sẻ."),
                        "/assets/news-hackathon-stage.jpg", "Sinh viên trình bày ý tưởng dự án",
                        "Ban biên tập DNTU SkillBridge", 3, "Published",
                        new DateTimeOffset(2026, 11, 15, 4, 0, 0, TimeSpan.Zero), null,
                        new DateTimeOffset(2026, 11, 15, 4, 0, 0, TimeSpan.Zero), null
                    }
                });
        }

        private static string ContentJson(params string[] paragraphs) => System.Text.Json.JsonSerializer.Serialize(paragraphs);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "news_articles",
                keyColumn: "Id",
                keyValues: new object[]
                {
                    Guid.Parse("11111111-1111-4111-8111-000000000006"),
                    Guid.Parse("11111111-1111-4111-8111-000000000007"),
                    Guid.Parse("11111111-1111-4111-8111-000000000008"),
                    Guid.Parse("11111111-1111-4111-8111-000000000009"),
                    Guid.Parse("11111111-1111-4111-8111-000000000010")
                });
        }
    }
}
