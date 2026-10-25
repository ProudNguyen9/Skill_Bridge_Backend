# Kiến trúc Backend DNTU SkillBridge

Tài liệu dành cho người mới tham gia backend. Nội dung mô tả cấu trúc đang có trong mã nguồn và các luồng chính. Khi tài liệu khác với code hoặc cấu hình hiện hành, hãy lấy code đang chạy làm nguồn chuẩn.

## 1. Tổng quan

Backend là ứng dụng ASP.NET Core trên .NET 10, tổ chức theo **modular monolith**: một API chứa nhiều module nghiệp vụ và dùng chung một SQL Server. Đây chưa phải hệ thống microservices; module hiện không được triển khai độc lập và không sở hữu database riêng.

~~~text
Client ── REST /api/v1 ──┐
        └─ SignalR /hubs ─┴─> API (HTTP, controllers, auth, middleware)
                                  │
                                  ▼
                            Application (use cases, DTO, interfaces)
                                  │
                                  ▼
                            Domain (entities, rules, states)

Infrastructure implements Application abstractions:
  EF Core -> SQL Server
  Outbox/background workers -> SignalR and other side effects
  File adapter -> S3-compatible storage
~~~

Application phụ thuộc Domain. Infrastructure hiện thực abstraction của Application. API tham chiếu Application và Infrastructure để cấu hình dependency injection tại composition root.

## 2. Cấu trúc thư mục

- **src/DNTU.SkillBridge.Api** — điểm khởi động ở Program.cs, controllers, HTTP routing, authentication/authorization, middleware, SignalR hubs, health checks, OpenAPI và composition root.
- **src/DNTU.SkillBridge.Application** — use case/service theo feature, DTO, kết quả xử lý, repository interfaces và các abstraction như current user.
- **src/DNTU.SkillBridge.Domain** — entities, enums, value objects và quy tắc nghiệp vụ/trạng thái; không phụ thuộc ASP.NET Core hay EF Core.
- **src/DNTU.SkillBridge.Infrastructure** — AppDbContext, EF mappings, migrations, repository implementations, Unit of Work, outbox và background workers.
- **tests** — unit, integration, functional và E2E Playwright.
- **infra** — Dockerfile, compose, Nginx/reverse proxy.

## 3. Module nghiệp vụ

Các vùng nghiệp vụ chính: Identity/Authentication; Catalog; Students, Companies, Lecturers; Projects; Applications; Teams và Commitments; Workspaces; Files; Milestones và Meetings; Submissions, Reviews và Academics; Notifications; Payments; Admin và Analytics.

Tên module và thư mục có thể khác nhau giữa API, Application và Infrastructure. Trước khi thêm feature, tìm module tương ứng ở cả ba project để tránh tạo bản sao.

## 4. Luồng xử lý request

1. Request vào ASP.NET Core pipeline; middleware xử lý correlation ID, lỗi và logging.
2. Authentication kiểm tra bearer JWT; authorization kiểm tra policy/role nếu endpoint yêu cầu.
3. Controller nhận HTTP input, gọi Application service và ánh xạ kết quả thành response/status code.
4. Application điều phối use case, gọi repository interface và Domain.
5. Repository Infrastructure dùng EF Core đọc/ghi SQL Server.
6. Unit of Work lưu thay đổi và kết thúc transaction.
7. Side effect cần xử lý nền có thể được ghi vào outbox trong cùng transaction để worker xử lý sau.

Controller nên mỏng, không chứa quy tắc nghiệp vụ hoặc truy vấn EF trực tiếp. API contracts chung gồm response đơn và response phân trang; xem Application/Common/ApiContracts.cs trước khi tạo envelope mới.

## 5. Persistence và transaction

- Database chính là SQL Server qua EF Core.
- AppDbContext tập hợp DbSet và nạp entity configurations từ assembly.
- Repository và Unit of Work được đăng ký tại Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs.
- Migrations nằm trong Infrastructure.
- Startup chỉ tự áp dụng migrations khi Database:ApplyMigrations=true; production cần có quy trình áp dụng schema rõ ràng.
- Compose hiện không khởi chạy SQL Server; cần connection string tới SQL Server bên ngoài.

Với truy vấn/report lớn, dùng projection và phân trang; tránh tải entity/quan hệ không cần thiết.

## 6. Authentication và bảo mật

- Access token là JWT; API kiểm tra issuer, audience, chữ ký và thời hạn.
- Password hashing dùng ASP.NET PasswordHasher.
- Refresh token được sinh ngẫu nhiên, backend lưu hash và xoay vòng khi refresh; có xử lý refresh-token replay.
- Claims có thể chứa user/session ID, roles, permissions và profile ID.
- Có rate limit cho đăng nhập và khôi phục mật khẩu.
- HttpCurrentUser cung cấp danh tính hiện tại cho Application.

Không log token, mật khẩu, OTP, connection string hoặc dữ liệu bí mật. Backend phải tự kiểm tra quyền truy cập tài nguyên; việc frontend ẩn nút không thay thế authorization.

## 7. Background processing, realtime và file

### Outbox và notifications

Application có thể ghi notification/outbox cùng transaction nghiệp vụ. OutboxProcessor chạy định kỳ, đọc bản ghi pending theo batch, phát notification qua SignalR và cập nhật trạng thái. Khi đổi luồng này, cần giữ khả năng retry an toàn/idempotent và có log/giám sát khi xử lý lỗi.

### SignalR

Notification hub phát sự kiện theo người dùng. Project hub kiểm tra quyền thành viên đang hoạt động trước khi cho join group của project.

### File

SQL Server lưu metadata; file thực tế nằm trên S3-compatible storage như MinIO/S3. API có thể cấp signed URL upload/download. Khi thay đổi file flow, kiểm tra giới hạn kích thước, content type và quyền truy cập.

### Payments

Module có nghiệp vụ thanh toán/tài trợ và tích hợp SePay checkout/IPN, idempotency và dữ liệu đối soát. Hoạt động với cổng thật phụ thuộc cấu hình môi trường.

## 8. Cấu hình và vận hành

- Program.cs là composition root đăng ký Application services, Infrastructure, hosted services, auth, authorization và rate limit.
- Options được bind và validate khi khởi động.
- Có health endpoints, Swagger/OpenAPI và correlation ID.
- Docker image chạy API bằng user không phải root.
- Compose có API, MinIO và Mailpit; SQL Server được cấu hình bên ngoài.
- Nginx reverse proxy hỗ trợ HTTP và SignalR WebSocket.
- Không commit file .env hoặc secrets.

Options Redis/Quartz có trong code cấu hình không tự chứng minh các dịch vụ đó đang chạy. Kiểm tra đăng ký trong Program.cs trước khi phụ thuộc vào chúng.

## 9. Thêm feature mới

1. Tìm module, entity và quy tắc trạng thái hiện có.
2. Thêm invariant/state vào Domain nếu phù hợp.
3. Khai báo DTO, outcome/interface và use case trong Application.
4. Cập nhật repository, EF mapping và migration nếu persistence thay đổi.
5. Đăng ký dependency tại composition root.
6. Thêm controller/endpoint mỏng, validation và authorization/rate limit phù hợp.
7. Cập nhật API contract phía client.
8. Thêm kiểm thử ở tầng phù hợp; dùng outbox/background processing cho side effect không nên chạy trong request.

## 10. Kiểm thử

- Unit: quy tắc Domain/Application.
- Integration: API, EF Core, repository và transaction.
- Functional: HTTP contracts/flows.
- E2E Playwright: luồng hoàn chỉnh người dùng.

Xem README.md để biết solution/project và lệnh phù hợp môi trường. Không dùng secrets thật trong fixtures.

## 11. Điểm tích hợp cần biết

- IAccountEmailSender hiện được đăng ký bằng NullAccountEmailSender; adapter này không gửi email thật. Cần sender thật trước khi xác minh tài khoản/khôi phục mật khẩu có thể hoạt động end-to-end.
- Migration tự chạy được điều khiển bởi Database:ApplyMigrations.
- SQL Server là dependency bên ngoài compose.
- Redis/Quartz options không đồng nghĩa Redis cache hoặc Quartz scheduler đã được đăng ký.
