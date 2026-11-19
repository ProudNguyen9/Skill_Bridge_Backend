using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DNTU.SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "news_articles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Excerpt = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    ContentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CoverImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CoverImageAlt = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReadTimeMinutes = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublishAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UnpublishAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_news_articles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "news_pin_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PinnedArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PinnedUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_news_pin_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_news_pin_settings_news_articles_PinnedArticleId",
                        column: x => x.PinnedArticleId,
                        principalTable: "news_articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_news_articles_Category_PublishAtUtc",
                table: "news_articles",
                columns: new[] { "Category", "PublishAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_news_articles_Slug",
                table: "news_articles",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_news_articles_Status_PublishAtUtc",
                table: "news_articles",
                columns: new[] { "Status", "PublishAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_news_pin_settings_PinnedArticleId",
                table: "news_pin_settings",
                column: "PinnedArticleId");
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
                        Guid.Parse("11111111-1111-4111-8111-000000000001"), "ket-noi-bai-toan-doanh-nghiep",
                        "Từ bài toán doanh nghiệp đến dự án học tập trên SkillBridge", "Hợp tác Doanh nghiệp",
                        "DNTU SkillBridge giúp doanh nghiệp mô tả nhu cầu để giảng viên và sinh viên cùng chọn đề tài phù hợp, có mục tiêu và kết quả rõ ràng.",
                        ContentJson(
                            "Doanh nghiệp có thể bắt đầu từ một vấn đề cụ thể như quy trình nhập liệu, tra cứu thông tin hoặc theo dõi tiến độ. Mô tả rõ hiện trạng và kết quả mong muốn giúp nhóm sinh viên xác định phạm vi phù hợp.",
                            "Giảng viên cùng doanh nghiệp rà soát yêu cầu, chia mục tiêu thành các bước nhỏ và chọn thời lượng phù hợp với học phần. Trong quá trình làm, nhóm cập nhật tiến độ và nhận góp ý tại những mốc đã thống nhất.",
                            "Kết quả dự án nên trình bày cả giải pháp, cách tiếp cận và giới hạn còn lại. Nhờ vậy, doanh nghiệp có thể đánh giá hướng phát triển tiếp theo, còn sinh viên có minh chứng cụ thể cho quá trình học tập."),
                        "/assets/dntu-computer-lab.jpg", "Sinh viên DNTU làm việc trong phòng máy",
                        "Ban biên tập DNTU SkillBridge", 4, "Published",
                        new DateTimeOffset(2026, 11, 14, 8, 0, 0, TimeSpan.Zero), null,
                        new DateTimeOffset(2026, 11, 14, 8, 0, 0, TimeSpan.Zero), null
                    },
                    {
                        Guid.Parse("11111111-1111-4111-8111-000000000002"), "du-an-sinh-vien-tu-yeu-cau-den-san-pham",
                        "Dự án sinh viên: bắt đầu từ yêu cầu nhỏ, hoàn thiện qua từng bước", "Dự án Sinh viên",
                        "Một dự án học tập hiệu quả có mục tiêu vừa sức, kế hoạch rõ ràng và sản phẩm được cải thiện qua từng vòng góp ý.",
                        ContentJson(
                            "Trước khi viết mã, nhóm cần thống nhất ai sẽ dùng sản phẩm, vấn đề nào cần giải quyết và thế nào được xem là hoàn thành. Một bản mô tả ngắn giúp mọi thành viên hiểu cùng một mục tiêu.",
                            "Sau đó, nhóm chia công việc thành những phần có thể kiểm tra được: phác thảo giao diện, xây dựng chức năng chính, thử với dữ liệu mẫu và ghi nhận lỗi. Việc rà soát thường xuyên giúp phát hiện sớm những điểm chưa rõ.",
                            "Cuối dự án, nhóm tổng hợp sản phẩm, quyết định kỹ thuật và phản hồi đã nhận. Phần tổng kết này giúp giảng viên đánh giá quá trình học, đồng thời giúp nhóm rút kinh nghiệm cho dự án tiếp theo."),
                        "/assets/dntu-students.jpg", "Sinh viên cùng trao đổi về dự án học tập",
                        "Ban biên tập DNTU SkillBridge", 4, "Published",
                        new DateTimeOffset(2026, 11, 13, 8, 0, 0, TimeSpan.Zero), null,
                        new DateTimeOffset(2026, 11, 13, 8, 0, 0, TimeSpan.Zero), null
                    },
                    {
                        Guid.Parse("11111111-1111-4111-8111-000000000003"), "cong-nghe-moi-cho-bai-toan-thuc-te",
                        "AI, cloud và dữ liệu: chọn công nghệ đúng với bài toán", "Hội thảo Công nghệ",
                        "Trước khi chọn một công nghệ mới, nhóm dự án nên hiểu dữ liệu, người dùng và giới hạn vận hành của bài toán mình đang giải quyết.",
                        ContentJson(
                            "AI có thể hỗ trợ phân loại, tìm kiếm hoặc tóm tắt dữ liệu, nhưng nhóm cần xác định rõ đầu vào, tiêu chí đánh giá và cách xử lý kết quả sai. Một thử nghiệm nhỏ thường giúp kiểm tra giả định trước khi xây dựng chức năng lớn.",
                            "Cloud phù hợp khi dự án cần triển khai, chia sẻ và mở rộng tài nguyên linh hoạt. Tuy nhiên, nhóm vẫn cần cân nhắc chi phí, quyền truy cập và phương án sao lưu để lựa chọn dịch vụ vừa với phạm vi học tập.",
                            "Trong buổi thảo luận công nghệ, sinh viên có thể trình bày lý do chọn giải pháp, so sánh phương án khác và nêu giới hạn của sản phẩm. Cách giải thích này quan trọng không kém việc sử dụng công nghệ mới."),
                        "/assets/news-aws-workshop.jpg", "Sinh viên tìm hiểu chủ đề công nghệ",
                        "Ban biên tập DNTU SkillBridge", 4, "Published",
                        new DateTimeOffset(2026, 11, 12, 8, 0, 0, TimeSpan.Zero), null,
                        new DateTimeOffset(2026, 11, 12, 8, 0, 0, TimeSpan.Zero), null
                    },
                    {
                        Guid.Parse("11111111-1111-4111-8111-000000000004"), "ho-so-thuc-tap-bang-minh-chung-du-an",
                        "Chuẩn bị hồ sơ thực tập bằng minh chứng từ dự án", "Tuyển dụng & Thực tập",
                        "Một hồ sơ thực tập thuyết phục cho thấy bạn đã làm gì, lựa chọn ra sao và học được điều gì từ sản phẩm của mình.",
                        ContentJson(
                            "Hãy chọn một hoặc hai dự án thể hiện rõ kỹ năng bạn muốn trình bày. Với mỗi dự án, ghi ngắn gọn mục tiêu, vai trò của mình, công cụ đã dùng và phần việc đã hoàn thành.",
                            "Đường dẫn đến mã nguồn, ảnh chụp sản phẩm hoặc bản demo giúp người xem kiểm tra minh chứng. Nếu có làm theo nhóm, hãy nói rõ đóng góp cá nhân và cách phối hợp với các thành viên.",
                            "Cuối cùng, chuẩn bị một đoạn chia sẻ về khó khăn đã gặp và cách bạn xử lý. Nhà tuyển dụng thường muốn hiểu quá trình suy nghĩ của bạn, không chỉ xem danh sách công nghệ."),
                        "/assets/dntu-laboratory.jpg", "Sinh viên chuẩn bị hồ sơ dự án",
                        "Ban biên tập DNTU SkillBridge", 4, "Published",
                        new DateTimeOffset(2026, 11, 11, 8, 0, 0, TimeSpan.Zero), null,
                        new DateTimeOffset(2026, 11, 11, 8, 0, 0, TimeSpan.Zero), null
                    },
                    {
                        Guid.Parse("11111111-1111-4111-8111-000000000005"), "huong-dan-trinh-bay-de-xuat-du-an",
                        "Hướng dẫn trình bày đề xuất dự án trên SkillBridge", "Thông báo Khoa",
                        "Đề xuất rõ vấn đề, người dùng và kết quả mong muốn sẽ giúp giảng viên và doanh nghiệp đánh giá phạm vi dự án nhanh hơn.",
                        ContentJson(
                            "Bắt đầu bằng một câu mô tả ngắn về nhu cầu hoặc vấn đề cần giải quyết. Nêu nhóm người dùng chính và bối cảnh sử dụng để người đọc hiểu vì sao đề xuất này cần thiết.",
                            "Tiếp theo, liệt kê các chức năng cốt lõi, kết quả dự kiến và cách kiểm tra sản phẩm. Chọn phạm vi phù hợp với thời lượng học phần; những ý tưởng mở rộng có thể ghi thành hướng phát triển sau.",
                            "Đính kèm tài liệu tham khảo hoặc hình phác thảo nếu có. Một đề xuất mạch lạc giúp các bên trao đổi sớm về dữ liệu, quyền truy cập, tiêu chí nghiệm thu và người hướng dẫn."),
                        "/assets/news-hackathon-stage.jpg", "Sinh viên trình bày ý tưởng dự án",
                        "Ban biên tập DNTU SkillBridge", 3, "Published",
                        new DateTimeOffset(2026, 11, 10, 8, 0, 0, TimeSpan.Zero), null,
                        new DateTimeOffset(2026, 11, 10, 8, 0, 0, TimeSpan.Zero), null
                    }
                });

            migrationBuilder.InsertData(
                table: "news_pin_settings",
                columns: new[] { "Id", "PinnedArticleId", "PinnedUntilUtc", "CreatedAt", "UpdatedAt" },
                values: new object[]
                {
                    Guid.Parse("e37e70a5-dcd2-4c93-8e8a-0f7f81200a01"),
                    Guid.Parse("11111111-1111-4111-8111-000000000001"),
                    null,
                    new DateTimeOffset(2026, 11, 14, 8, 0, 0, TimeSpan.Zero),
                    null
                });
        }

        private static string ContentJson(params string[] paragraphs) => System.Text.Json.JsonSerializer.Serialize(paragraphs);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "news_pin_settings");

            migrationBuilder.DropTable(
                name: "news_articles");
        }
    }
}
