# DNTU SkillBridge Backend

Backend modular monolith cho quy trình phối hợp dự án thực tế giữa sinh viên, doanh nghiệp, giảng viên và quản trị viên DNTU.

## Bắt đầu nhanh

**Yêu cầu:** .NET SDK 10, SQL Server 2022+ cho API/integration tests. Backend đọc connection string từ `.env`, kết nối host `backend.phelieuminhduc.com,32022`, database `backendskillbridge`; không cần Docker.

```powershell
# Restore và build
dotnet restore .\DNTU.SkillBridge.slnx
dotnet build .\DNTU.SkillBridge.slnx --configuration Debug --no-restore /warnaserror

# Chạy API với cấu hình Development
dotnet run --project .\src\DNTU.SkillBridge.Api
```

API đọc `.env` tại thư mục Backend khi chạy Development, hoặc tại thư mục ứng dụng khi deploy. Biến môi trường hệ thống và tham số command line được ưu tiên hơn `.env`. Điền mật khẩu vào `.env`; `.env.example` chỉ chứa placeholder. Backup cho host 2022: `artifacts/database/backendskillbridge-sqlserver2022.bak`.

## Cấu trúc repository

```text
Backend/
├── src/                          # Production code
│   ├── DNTU.SkillBridge.Domain/   # Thuần domain: entity, enum, invariant
│   ├── DNTU.SkillBridge.Application/ # Cross-cutting abstractions/contracts
│   ├── DNTU.SkillBridge.Infrastructure/ # EF Core, migrations, provider adapters
│   └── DNTU.SkillBridge.Api/      # HTTP API, DI composition, feature services
├── tests/                         # Unit, integration, functional, E2E và manual smoke scripts
├── docs/                          # Tài liệu cấu trúc và báo cáo kỹ thuật
├── infra/
│   ├── docker/                    # Compose và template local environment
│   └── nginx/                     # Reverse-proxy baseline
├── Dockerfile
├── DNTU.SkillBridge.slnx
├── Directory.Build.props          # Cấu hình build/analyzer dùng chung
└── Directory.Packages.props       # Phiên bản NuGet tập trung
```

## Quy tắc tổ chức code

### Layers

| Layer | Trách nhiệm | Không được phụ thuộc vào |
|---|---|---|
| [`Domain`](src/DNTU.SkillBridge.Domain/DNTU.SkillBridge.Domain.csproj) | Quy tắc nghiệp vụ, entity, state machine, value object | ASP.NET Core, EF Core, HTTP, storage provider |
| [`Application`](src/DNTU.SkillBridge.Application/DNTU.SkillBridge.Application.csproj) | Interface/use-case contract dùng chung giữa layers | API/controller, adapter hạ tầng |
| [`Infrastructure`](src/DNTU.SkillBridge.Infrastructure/DNTU.SkillBridge.Infrastructure.csproj) | [`AppDbContext`](src/DNTU.SkillBridge.Infrastructure/Persistence/AppDbContext.cs), EF configurations/migrations, persistence/adapters | API/controller |
| [`Api`](src/DNTU.SkillBridge.Api/DNTU.SkillBridge.Api.csproj) | Controller, request/response contract, authentication, DI, service theo feature | Truy cập DB từ controller hoặc logic domain bị lặp |

### Feature-first trong API

Mỗi feature nằm trong một thư mục PascalCase tại [`src/DNTU.SkillBridge.Api`](src/DNTU.SkillBridge.Api):

```text
<Api>/<Feature>/
├── <Feature>Contracts.cs  # Request/response, query, DTO
├── <Feature>Service.cs    # Use-case orchestration và data scope
└── ...                    # Adapter/service phụ trợ riêng feature

<Api>/Controllers/
└── <Feature>Controller.cs # HTTP/authorization/response mapping, không business logic
```

Ví dụ: [`Submissions`](src/DNTU.SkillBridge.Api/Submissions), [`Payments`](src/DNTU.SkillBridge.Api/Payments), [`Workspaces`](src/DNTU.SkillBridge.Api/Workspaces).

### Quy ước đặt tên

- Namespace luôn khớp project root: `DNTU.SkillBridge.<Layer>.<Feature>`.
- Một file chỉ chứa một public type chính; tên file khớp tên type.
- DTO request kết thúc bằng `Request`, response kết thúc bằng `Response`, result state kết thúc bằng `Outcome`.
- Service ghi transaction và authorization scope; controller chỉ điều phối HTTP.
- Entity chỉ public method cho transition hợp lệ; không public setter cho state nghiệp vụ.
- `async` method có hậu tố `Async`, nhận [`CancellationToken`](src/DNTU.SkillBridge.Api/Program.cs:23) ở tham số cuối.

## Data và migration

- Mọi thay đổi entity/configuration EF Core phải đi kèm migration ở [`Persistence/Migrations`](src/DNTU.SkillBridge.Infrastructure/Persistence/Migrations).
- Sau mỗi thay đổi schema, chạy:

```powershell
dotnet ef migrations has-pending-model-changes `
  --project .\src\DNTU.SkillBridge.Infrastructure `
  --startup-project .\src\DNTU.SkillBridge.Api --no-build
```

- SQL Server dùng migration `SqlServerInitial` và snapshot hiện tại. Database host đã restore schema này.
- Query đọc ưu tiên [`AsNoTracking()`](src/DNTU.SkillBridge.Api/Workspaces/WorkspaceService.cs:18), DTO projection và pagination server-side.

## Test và kiểm chứng

```powershell
# Unit / integration / functional
dotnet test .\DNTU.SkillBridge.slnx --configuration Debug --no-build

# E2E Playwright (khôi phục dependencies sau cleanup)
cd .\tests\DNTU.SkillBridge.E2E
npm ci
npx playwright test
```

Các smoke/debug PowerShell dùng thủ công nằm tại [`tests/DNTU.SkillBridge.E2E/scripts/manual`](tests/DNTU.SkillBridge.E2E/scripts/manual). Artifact build/test như `bin/`, `obj/`, `node_modules/`, Playwright report không phải source và không commit.

## Local infrastructure và vận hành

- Docker Compose: [`infra/docker/docker-compose.yml`](infra/docker/docker-compose.yml).
- Template secrets local: [`infra/docker/.env.example`](infra/docker/.env.example). Copy thành `.env`, không commit file thật.

## Standards bắt buộc

- Nullable reference types, centralized package version, analyzer settings theo [`Directory.Build.props`](Directory.Build.props), [`Directory.Packages.props`](Directory.Packages.props), và [`.editorconfig`](.editorconfig).
- Không commit secrets, private key, real `.env`, build output hoặc test artifacts.
- Không đưa credentials, raw file content, full bank account hoặc payment secret vào log, audit, notification, response DTO.
- Mọi thay đổi phải đi kèm test hoặc tài liệu kỹ thuật phù hợp với phạm vi thay đổi.
