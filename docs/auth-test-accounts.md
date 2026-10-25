# Tài khoản đăng nhập kiểm thử

Các tài khoản dưới đây được seed khi API chạy với `ASPNETCORE_ENVIRONMENT=Development` và `Authentication:SeedTestAccounts=true`. Chúng không được tạo trong môi trường khác.

Mật khẩu dùng chung: `SkillBridge.Test#2026`

## Sinh viên (5 tài khoản)

| # | Email | Mã sinh viên | Tên hiển thị |
|---|---|---|---|
| 1 | `student1.test@skillbridge.local` | `TEST0001` | Nguyễn Văn An |
| 2 | `student2.test@skillbridge.local` | `TEST0002` | Trần Thị Bích |
| 3 | `student3.test@skillbridge.local` | `TEST0003` | Lê Văn Cường |
| 4 | `student4.test@skillbridge.local` | `TEST0004` | Phạm Thị Dung |
| 5 | `student5.test@skillbridge.local` | `TEST0005` | Hoàng Văn Em |

Đăng nhập sinh viên có thể dùng email hoặc mã sinh viên cùng mật khẩu chung ở trên.

## Doanh nghiệp (5 tài khoản)

Mỗi tài khoản gắn với một công ty đã **VERIFIED** — đăng nhập vào `/company/dashboard` và đăng bài toán được ngay.

| # | Email | Công ty | MST |
|---|---|---|---|
| 1 | `company1.test@skillbridge.local` | FPT Software Biên Hòa | 0311000001 |
| 2 | `company2.test@skillbridge.local` | Cảng Đồng Nai | 0311000002 |
| 3 | `company3.test@skillbridge.local` | Viettel IDC Đồng Nai | 0311000003 |
| 4 | `company4.test@skillbridge.local` | GreenTech Smart Agriculture | 0311000004 |
| 5 | `company5.test@skillbridge.local` | Dong Nai Fintech Lab | 0311000005 |

## Giảng viên & Quản trị

| Role | Email | Ghi chú |
|---|---|---|
| Giảng viên | `lecturer.test@skillbridge.local` | Hồ sơ APPROVED (Mã GV: GV001) — vào thẳng cổng giảng viên |
| Quản trị viên | `admin.test@skillbridge.local` | Duyệt giảng viên / công ty / đề tài |
| Quản trị cấp cao | `superadmin.test@skillbridge.local` | Toàn quyền, luôn đi qua mọi gate permission |

## Ghi chú phân quyền

- Hồ sơ giảng viên **PENDING** để test luồng duyệt: tự đăng ký qua `POST /api/v1/auth/register`
  với `accountType: "Lecturer"` rồi duyệt bằng tài khoản admin tại `/admin/approvals/lecturers`
  (xem `docs/authorization-guide.md`).
- Để test luồng onboarding/duyệt công ty: đăng ký tài khoản công ty mới, hoàn thành wizard
  `/onboarding/company`, rồi admin xác minh qua `POST /api/v1/admin/companies/{id}/verify`.

Khi bật seed trong môi trường Development, mật khẩu của các tài khoản dự trữ này được đặt lại về mật khẩu kiểm thử ở trên mỗi lần API khởi động.
