# Tài khoản đăng nhập kiểm thử

Các tài khoản dưới đây được seed khi API chạy với `ASPNETCORE_ENVIRONMENT=Development` và `Authentication:SeedTestAccounts=true`. Chúng không được tạo trong môi trường khác.

Mật khẩu dùng chung: `SkillBridge.Test#2026`

| Role | Email | Mã sinh viên |
| --- | --- | --- |
| Sinh viên | `student.test@skillbridge.local` | `TEST0001` |
| Doanh nghiệp | `company.test@skillbridge.local` | — |
| Giảng viên | `lecturer.test@skillbridge.local` | — |
| Quản trị viên | `admin.test@skillbridge.local` | — |
| Quản trị viên cấp cao | `superadmin.test@skillbridge.local` | — |

Đăng nhập sinh viên kiểm thử có thể dùng email hoặc `TEST0001` cùng mật khẩu chung ở trên.

Khởi động lại API sau khi cấu hình database và chạy migrations. Khi bật seed trong môi trường Development, mật khẩu của các tài khoản dự trữ này được đặt lại về mật khẩu kiểm thử ở trên mỗi lần API khởi động.
