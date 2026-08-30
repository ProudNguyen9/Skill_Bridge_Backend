# Quy ước cấu trúc và phát triển

Tài liệu này là chuẩn tổ chức source cho DNTU SkillBridge. Mục tiêu: một developer mới có thể xác định đúng nơi đặt code trong vài phút, không phụ thuộc vào người viết trước.

## 1. Repository layout

```text
Backend/
├── src/        # Production projects
├── tests/      # Test projects và E2E assets
├── docs/       # Kiến trúc, plan, runbook, ADR sau này
├── infra/      # Docker, Nginx, deployment manifests
├── scripts/    # Script thao tác lặp lại được
└── artifacts/  # Output runtime/CI không commit
```

Không tạo thư mục chung chung như `Helpers`, `Utils`, `Misc`, `NewFolder`, hoặc `Services` ở repository root.

## 2. Layer boundary

```text
Api → Application → Domain
Api → Infrastructure → Application → Domain
```

- [`Domain`](../../src/DNTU.SkillBridge.Domain/DNTU.SkillBridge.Domain.csproj): entity, enum, state-machine, invariant nghiệp vụ. Không tham chiếu ASP.NET Core, EF Core, HTTP hoặc provider SDK.
- [`Application`](../../src/DNTU.SkillBridge.Application/DNTU.SkillBridge.Application.csproj): contract, DTO, outcome enum, service interface, repository interface, service implementation. Không tham chiếu controller/adapters.
- [`Infrastructure`](../../src/DNTU.SkillBridge.Infrastructure/DNTU.SkillBridge.Infrastructure.csproj): EF Core repository, UnitOfWork, outbox, background worker, storage/email/payment adapter.
- [`Api`](../../src/DNTU.SkillBridge.Api/DNTU.SkillBridge.Api.csproj): transport HTTP, middleware, auth/web adapter, SignalR hub, `Program.cs`.

## 3. Feature ownership

Một feature dùng cùng tên PascalCase xuyên layer khi có domain/persistence/API tương ứng:

```text
Domain/<Feature>/
Application/<Feature>/<Feature>Contracts.cs
Application/<Feature>/<Feature>Outcomes.cs
Application/<Feature>/<Feature>Service.cs
Application/<Feature>/<Feature>Repository.cs
Infrastructure/Repositories/<Feature>Repository.cs
Api/Controllers/<Feature>Controller.cs
```

Ví dụ chuẩn: [`Authentication`](../../src/DNTU.SkillBridge.Application/Authentication), [`Applications`](../../src/DNTU.SkillBridge.Application/Applications), [`Projects`](../../src/DNTU.SkillBridge.Application/Projects).

Khi một service chỉ phục vụ transport/API, giữ ở `Api/<Feature>`. Khi type là business rule hoặc state transition, đặt ở `Domain/<Feature>`.

## 4. Application feature layout

```text
Application/<Feature>/
├── <Feature>Contracts.cs     # Request, response, query DTO
├── <Feature>Service.cs       # Use case, transaction, scope
├── <Feature>Repository.cs     # Contract truy cập dữ liệu
├── <Feature>Outcomes.cs       # Outcome enum nghiệp vụ
└── <Feature>ServiceImpl.cs    # Implementation nếu feature tách interface/class

Api/Controllers/
└── <Feature>Controller.cs    # HTTP mapping, policy và response code
```

Controller không được chứa LINQ query phức tạp, business state transition, hoặc thao tác `DbContext` trực tiếp. Chúng gọi service ở `Application` và map outcome sang HTTP status.

## 5. Naming

- Public type chính và file phải cùng tên: `PaymentService` ở `PaymentService.cs`.
- `Request`, `Response`, `Query`, `Outcome`, `Options`, `Configuration`, `Controller`, `Service` có ý nghĩa nhất quán.
- Async method luôn hậu tố `Async`; [`CancellationToken`](../../src/DNTU.SkillBridge.Api/Program.cs:23) là tham số cuối.
- Namespace phản chiếu ownership: `DNTU.SkillBridge.Application.Payments`.
- Một public type chính mỗi file, trừ contract records gắn chặt cùng một endpoint/feature.

## 6. Persistence

- Entity configuration đặt trong [`Persistence/Configurations`](../../src/DNTU.SkillBridge.Infrastructure/Persistence/Configurations).
- Migration tạo bằng EF Core và đặt trong [`Persistence/Migrations`](../../src/DNTU.SkillBridge.Infrastructure/Persistence/Migrations).
- Không chỉnh sửa migration đã được dùng. Thêm migration mới cho mọi thay đổi schema.
- Sau đổi entity/configuration luôn chạy `dotnet ef migrations has-pending-model-changes`.

## 7. Code hygiene

- Không commit `bin`, `obj`, `node_modules`, test report, log hoặc file `.env` thật.
- Không log token, secret, raw request payment, raw file body hay full bank account.
- Truy vấn read dùng DTO projection, [`AsNoTracking()`](../../src/DNTU.SkillBridge.Application/Workspaces/WorkspaceService.cs) và pagination.
- Mọi side effect cross-feature dùng transaction/outbox; không gọi SignalR/email trực tiếp từ state transition.

## 8. Khi thêm feature mới

1. Viết/điều chỉnh entity + invariant ở `Domain`.
2. Tạo EF configuration + migration ở `Infrastructure`.
3. Tạo contracts/service tại `Application/<Feature>`.
4. Tạo controller mỏng ở `Api/Controllers`.
5. Đăng ký dependency trong [`Program.cs`](../../src/DNTU.SkillBridge.Api/Program.cs).
6. Cập nhật plan/evidence ở [`docs/plans`](../plans/README.md).
7. Chạy build, migration gate và test phù hợp.
