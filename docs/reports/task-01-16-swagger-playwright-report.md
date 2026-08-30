# Swagger and Playwright API Validation Report — Tasks 01–16

- **Run date:** 2026-08-29
- **Scope:** completed delivery-board tasks 01 through 16. Task 17 is outside this report.
- **API target:** `http://127.0.0.1:5187`
- **OpenAPI JSON:** `GET /openapi/v1.json` (ASP.NET Core document) and `GET /swagger/v1/swagger.json` (Swagger UI document)
- **Swagger UI:** `GET /swagger` (Development/Test only)

## 1. Changes added for this validation

| Item | Result |
|---|---|
| Swagger UI | Added `Swashbuckle.AspNetCore` 10.2.3, an API v1 document, Bearer/JWT authorization input, and `/swagger` UI registration restricted to Development/Test. |
| Playwright suite | Added `tests/DNTU.SkillBridge.E2E` with UI, OpenAPI-contract, and anonymous-public-endpoint tests. |
| Test coverage intent | Swagger UI presence, Bearer scheme, representative Task 01–16 path presence, and 13 public safe GET endpoints. No write endpoint is called. |

## 2. Automation commands and observed results

| Command | Result | Notes |
|---|---|---|
| `dotnet restore .\DNTU.SkillBridge.slnx` | PASS | All projects up to date. |
| `dotnet build .\DNTU.SkillBridge.slnx --configuration Debug --no-restore` | PASS | 0 errors; 4 pre-existing warnings in `ProjectService.cs` and `CompanyProjectEndpointTests.cs`. |
| `dotnet test .\DNTU.SkillBridge.slnx --configuration Debug --no-build` | PASS | Unit: 8/8 passed. Integration: 74/74 passed. Functional project contains no discovered tests. |
| `dotnet run --no-build --project .\src\DNTU.SkillBridge.Api --urls http://127.0.0.1:5187` | BLOCKED | Startup failed before HTTP binding: PostgreSQL `28P01`, password authentication failed for user `postgres`. TCP port 5432 is reachable, but the Development connection string password is not accepted. |
| `npx playwright test` | BLOCKED | The two API-request tests receive `ECONNREFUSED` because the API did not start. The browser test also needs the Playwright 1.58 Chromium binary. |

## 3. Task-by-task validation status

`Evidence recorded` means the task's delivery-plan file marks it `DONE` and documents tests/manual checks. `Live validation blocked` means this run could not reach the API because of the local database credential failure; it is **not** a product API failure.

| Task | Feature area | Board/evidence status | Live Swagger/Playwright result |
|---:|---|---|---|
| 01 | Environment configuration and options validation | DONE | BLOCKED at startup; connection-string credential must be supplied outside source control. |
| 02 | API conventions, ProblemDetails, OpenAPI | DONE | Swagger UI integration added; live document verification BLOCKED. |
| 03 | Correlation, logging, health checks | DONE | `/health/live` planned in Playwright suite; BLOCKED before process starts. |
| 04 | EF Core PostgreSQL persistence baseline | DONE | Integration suite passes (74 total); live API startup BLOCKED by PostgreSQL authentication. |
| 05 | Identity, roles, permissions, current user | DONE | Integration suite passes; authenticated Swagger execution requires a running API plus a test account/token. |
| 06 | JWT, refresh token, sessions | DONE; task evidence records live auth workflow | OpenAPI route assertion is prepared; live execution BLOCKED. |
| 07 | Email lifecycle, password, rate limits | DONE; task evidence records unit/live HTTP checks | Routes are in Swagger once API starts; live execution BLOCKED. |
| 08 | Catalog and project metadata | DONE; evidence records 13 catalog integration tests and manual API smoke check | 8 anonymous catalog endpoints are in the Playwright suite; live execution BLOCKED. |
| 09 | Student profile, privacy, certificates, skills | DONE | Authenticated/profile-state workflows not executed because runtime is blocked. |
| 10 | Company profile, members, documents, verification | DONE | Public company directory is in the anonymous suite; profile writes require token and seed state. |
| 11 | Lecturer profile and supervision | DONE | Authenticated endpoints not executed because runtime is blocked. |
| 12 | Company project draft/create/update/deliverables/skills | DONE | Authenticated write workflow not executed because runtime is blocked. |
| 13 | Project submission, approval, publication | DONE | Authenticated state-transition workflow not executed because runtime is blocked. |
| 14 | Public projects, browse/search/filter/sort/detail | DONE; evidence records 6 endpoint tests | Public projects listing is in the anonymous suite; live execution BLOCKED. |
| 15 | Saved projects and skill match | DONE | Authenticated student workflow not executed because runtime is blocked. |
| 16 | Student applications | DONE; evidence records 7 integration tests | OpenAPI contract path is asserted by Playwright; create/list/detail/withdraw requires student/project preconditions and could not run. |

## 4. Playwright specification

File: `tests/DNTU.SkillBridge.E2E/tests/swagger-api.spec.js`

| Test | Expected result once API and browser prerequisites are available |
|---|---|
| `Swagger UI loads and exposes the v1 document` | `/swagger` renders, document is named `DNTU SkillBridge API v1`, and the Authorize dialog is present. |
| `Swagger v1 JSON documents implemented Task 01-16 routes` | `swagger/v1/swagger.json` returns v1, includes Bearer JWT scheme, and documents representative auth/profile/project/application routes. |
| `public Swagger endpoints respond without server errors` | 13 anonymous safe GET endpoints return no 5xx/401/403 response. |

## 5. Blocking issue and safe remediation

The runtime failure is:

```text
Npgsql.PostgresException: 28P01: password authentication failed for user "postgres"
```

The configured development value is in `src/DNTU.SkillBridge.Api/appsettings.Development.json`; no database password was changed or exposed during this validation. Supply the actual local secret outside version control, for example in the current PowerShell session:

```powershell
$env:ConnectionStrings__Default = 'Host=localhost;Port=5432;Database=skillbridge;Username=postgres;Password=<actual-local-password>'
dotnet run --project .\src\DNTU.SkillBridge.Api --urls http://127.0.0.1:5187
```

Then, from `tests/DNTU.SkillBridge.E2E`, run:

```powershell
npx playwright install chromium
$env:PLAYWRIGHT_BASE_URL = 'http://127.0.0.1:5187'
npm run test:e2e
```

Update this report only with the resulting actual PASS/FAIL output after the server is reachable.

## 6. Conclusion

The Swagger integration compiles and the existing automated .NET suite passes. Full Swagger/Playwright live verification of Tasks 01–16 remains **blocked by local PostgreSQL credentials**, not by an HTTP assertion failure. The test suite and report are in place for an immediate rerun once the valid local connection string and Chromium binary are available.