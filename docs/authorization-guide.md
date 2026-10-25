# Hướng dẫn Phân quyền & Xác thực (Authorization Guide)

> Tài liệu mô tả cách hệ thống DNTU SkillBridge phân quyền, luồng đăng ký của 3 vai trò,
> và cách thêm quyền mới / kiểm tra quyền khi phát triển. Code là nguồn sự thật cuối cùng —
> tài liệu này cập nhật theo commit hiện tại.

## 1. Mô hình tổng quan

Hệ thống dùng mô hình **3 tầng**:

~~~text
┌─────────────────────────────────────────────────────────────────┐
│ Tầng 1: ROLE (vai trò) — vạch hàng rào lớn                      │
│   STUDENT | COMPANY | LECTURER | ADMIN | SUPER_ADMIN            │
│   → Quyết định NHÓM endpoint được phép truy cập                 │
├─────────────────────────────────────────────────────────────────┤
│ Tầng 2: PERMISSION (quyền chi tiết) — chốt các nút nhạy cảm     │
│   JWT chứa claim "permission" (sinh từ role_permissions)        │
│   → Kiểm tra bằng [Authorize(Policy = "permission:<tên>")]      │
├─────────────────────────────────────────────────────────────────┤
│ Tầng 3: OWNERSHIP (sở hữu tài nguyên) — "của ai người đó làm"   │
│   Check trong service layer: companyId, supervisorId, memberId  │
└─────────────────────────────────────────────────────────────────┘
~~~

- **Role** gán cho User qua bảng `user_roles`.
- **Permission** gán cho Role qua bảng `role_permissions` (ma trận seed trong
  `IdentitySeed.RolePermissionMatrix`). JWT chứa claim `permission` cho mỗi quyền
  → không cần truy vấn DB khi check.
- **SUPER_ADMIN** luôn đi qua mọi gate permission (xử lý trong `PermissionAuthorizationHandler`).
- **ADMIN** được seed toàn bộ permission trong ma trận.

## 2. Ma trận Role → Permission

| Permission | STUDENT | COMPANY | LECTURER | ADMIN/SUPER_ADMIN |
|---|:-:|:-:|:-:|:-:|
| files.upload | ✅ | ✅ | ✅ | ✅ |
| files.read | ✅ | ✅ | ✅ | ✅ |
| files.delete | — | — | — | ✅ |
| projects.create | — | ✅ | — | ✅ |
| projects.approve | — | — | ✅ | ✅ |
| projects.apply | ✅ | — | — | ✅ |
| applications.review | — | ✅ | — | ✅ |
| tasks.manage | — | — | — | ✅ |
| meetings.manage | — | — | — | ✅ |
| submissions.create | ✅ | — | — | ✅ |
| submissions.review.technical | — | — | ✅ | ✅ |
| submissions.review.business | — | ✅ | — | ✅ |
| rubrics.manage | — | — | ✅ | ✅ |
| evaluations.manage | — | — | ✅ | ✅ |
| skills.verify | — | — | ✅ | ✅ |
| payments.view | — | ✅ | — | ✅ |
| payments.fund | — | — | — | ✅ |
| payments.reconcile | — | — | — | ✅ |
| disbursements.manage | — | — | — | ✅ |
| admin.settings.manage | — | — | — | ✅ |
| companies.verify | — | — | — | ✅ |
| lecturers.approve | — | — | — | ✅ |

Nguồn: `IdentitySeed.RolePermissionMatrix` + `GetPermissionsForRole()` trong
`src/DNTU.SkillBridge.Infrastructure/Persistence/Seed/IdentitySeed.cs`.

## 3. Cách kiểm tra quyền khi phát triển

### 3.1. Endpoint chỉ cần permission → dùng Policy attribute

~~~csharp
using DNTU.SkillBridge.Api.Security;
using DNTU.SkillBridge.Domain.Identity;

[HttpGet("things")]
[Authorize(Policy = AuthorizationPolicies.For(PermissionNames.AdminSettingsManage))]
public IActionResult ListThings() => Ok(...);
~~~

Policy `permission:<tên>` được sinh tự động trong `Program.cs` cho từng permission.
Handler `PermissionAuthorizationHandler` kiểm tra claim `permission` trong JWT.

### 3.2. Endpoint cần permission + role/điều kiện khác → check imperative

~~~csharp
// Dùng extension trong Application/Common/Security/CurrentUserAuthorizationExtensions.cs
private bool CanCreate() => currentUser.Roles.Contains(RoleNames.Student) &&
    currentUser.HasPermission(PermissionNames.SubmissionsCreate);
~~~

`HasPermission` trả true khi user có permission claim HOẶC là SUPER_ADMIN.

### 3.3. Ownership (sở hữu tài nguyên)

Check trong service layer bằng quan hệ đã lưu, ví dụ:
- Dự án thuộc công ty: resolve `company_id` claim (từ `CompanyMember`) rồi so với dự án.
- Giảng viên duyệt submission: `LecturerAssignment` phải ACTIVE và trỏ vào submission đó.
- Công ty xác minh: chỉ ADMIN với permission `companies.verify`.

### 3.4. Frontend

- Role: `useHasRole("STUDENT")` hoặc route guard `ProtectedRoute roles={[ROLES.student]}`.
- Permission: `<Can permission="projects.create">...</Can>` (`modules/auth/components/Can.tsx`).
- Trạng thái duyệt: `user.lecturerApprovalStatus`, `user.companyId` từ `/auth/me`.

## 4. Thêm permission mới (checklist 5 bước)

1. Thêm const vào `src/DNTU.SkillBridge.Domain/Identity/PermissionNames.cs`
   (và thêm vào mảng `All`).
2. Thêm vào ma trận trong `IdentitySeed.RolePermissionMatrix` cho role tương ứng
   (ADMIN/SUPER_ADMIN tự có).
3. Chạy lại ứng dụng — seed tự tạo permission + `role_permissions` (idempotent).
4. Gắn gate: `[Authorize(Policy = AuthorizationPolicies.For(PermissionNames.<Tên>))]`
   hoặc `currentUser.HasPermission(...)`.
5. Frontend: thêm constant và dùng `<Can permission="...">` / check `user.permissions`.
   User cần **đăng nhập lại** để nhận JWT mới chứa claim.

## 5. Luồng đăng ký 3 vai trò

### 5.1. Sinh viên

~~~text
Đăng ký (email @dntu.edu.vn + MSSV) → OTP xác thực email → đăng nhập → vào /student/dashboard
~~~

- Backend: `POST /api/v1/auth/register` (`accountType: "Student"`) — tạo User (role STUDENT)
  + `StudentProfile` (StudentCode có unique index).
- Không cần admin duyệt.

### 5.2. Doanh nghiệp

~~~text
Đăng ký (email bất kỳ) → OTP → đăng nhập → Onboarding wizard /onboarding/company
→ tạo Company (PENDING) → admin xác minh (companies.verify) → được đăng bài toán
~~~

- Backend: đăng ký tạo User (role COMPANY) — **chưa tạo Company**.
- FE chặn: `ProtectedRoute` redirect COMPANY chưa có `companyId` → `/onboarding/company`.
- Onboarding gọi `PUT /api/v1/companies/me` → tạo `Company` (PENDING) + `CompanyMember` OWNER.
- Admin duyệt: `POST /api/v1/admin/companies/{id}/verify` (permission `companies.verify`).
- Chỉ công ty VERIFIED mới đăng được bài toán (`Company.CanPublishProjects()`).

### 5.3. Giảng viên

~~~text
Đăng ký (email @dntu.edu.vn + mã GV) → OTP → đăng nhập → /lecturer/pending (chờ duyệt)
→ admin phê duyệt (lecturers.approve) → /auth/me trả APPROVED → vào cổng giảng viên
~~~

- Backend: `POST /api/v1/auth/register` (`accountType: "Lecturer"`, `lecturerCode` có unique
  index) — tạo User (role LECTURER) + `LecturerProfile` với `ApprovalStatus = PENDING`.
- `GET /api/v1/auth/me` trả thêm `lecturerApprovalStatus`.
- FE: `ProtectedRoute` redirect giảng viên chưa APPROVED → `/lecturer/pending`.
- Admin duyệt: `GET /api/v1/admin/lecturers?status=pending` +
  `POST /api/v1/admin/lecturers/{id}/approve` hoặc `/reject` (permission `lecturers.approve`).
- Giảng viên chỉ nhận được tham gia hướng dẫn khi `IsActive && ApprovalStatus == APPROVED`
  (`LecturerProfile.CanTakeAssignments()`).
- Hồ sơ giảng viên tạo qua Google SSO / dev seed được **APPROVED sẵn** (admin-provisioned).

### 5.4. Endpoints duyệt của admin

| Method | Route | Permission | Mô tả |
|---|---|---|---|
| GET | `/api/v1/admin/lecturers?status=pending\|approved\|rejected` | lecturers.approve | Danh sách hồ sơ |
| POST | `/api/v1/admin/lecturers/{id}/approve` | lecturers.approve | Duyệt (trigger `/reject` với `{ note }`) |
| POST | `/api/v1/admin/lecturers/{id}/reject` | lecturers.approve | Từ chối kèm lý do |
| GET | `/api/v1/admin/companies?status=pending\|verified\|rejected\|suspended` | companies.verify | Danh sách công ty |
| POST | `/api/v1/admin/companies/{id}/verify` | companies.verify | Xác minh |
| POST | `/api/v1/admin/companies/{id}/reject` | companies.verify | Từ chối |

Mọi quyết định đều ghi `AuditLog` (`audit_logs`) với action
`LECTURER_APPROVED / LECTURER_REJECTED / COMPANY_VERIFIED / COMPANY_REJECTED`.

## 6. Tài khoản kiểm thử (dev)

Bật `Authentication:SeedTestAccounts=true` (đã bật trong Development). Chi tiết xem
`docs/auth-test-accounts.md`. Mật khẩu chung: `SkillBridge.Test#2026`.

Ví dụ curl duyệt giảng viên:

~~~bash
# 1. Đăng nhập admin lấy token
curl -X POST http://localhost:5017/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin.test@skillbridge.local","password":"SkillBridge.Test#2026"}'

# 2. Xem hồ sơ chờ duyệt
curl "http://localhost:5017/api/v1/admin/lecturers?status=pending" \
  -H "Authorization: Bearer <ACCESS_TOKEN>"

# 3. Duyệt
curl -X POST http://localhost:5017/api/v1/admin/lecturers/<LECTURER_ID>/approve \
  -H "Authorization: Bearer <ACCESS_TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{"note":"Đã xác minh với khoa"}'
~~~

## 7. Kiểm thử tự động

- `tests/DNTU.SkillBridge.IntegrationTests/PermissionMatrixTests.cs` — ma trận quyền
  từng role (đọc lại qua `/auth/me`) + 403 khi thiếu quyền.
- `tests/DNTU.SkillBridge.IntegrationTests/LecturerCompanyApprovalFlowTests.cs` —
  luồng đăng ký giảng viên/công ty → admin duyệt → trạng thái cập nhật.

Chạy:

~~~bash
dotnet test tests/DNTU.SkillBridge.IntegrationTests --filter "FullyQualifiedName~PermissionMatrixTests"
dotnet test tests/DNTU.SkillBridge.IntegrationTests --filter "FullyQualifiedName~LecturerCompanyApprovalFlowTests"
~~~
