# Google sign-in cho tài khoản DNTU

## Cấu hình OAuth Client ID

1. Trong Google Cloud Console, tạo OAuth Client ID loại **Web application**.
2. Thêm JavaScript origin đang chạy frontend, ví dụ `http://localhost:5173`. Khi triển khai, thêm origin HTTPS của website.
3. Sao chép cùng một Web Client ID vào hai biến môi trường:

```env
# Frontend/.env.local
VITE_GOOGLE_CLIENT_ID=YOUR_WEB_CLIENT_ID.apps.googleusercontent.com

# Backend/.env
GoogleAuth__ClientId=YOUR_WEB_CLIENT_ID.apps.googleusercontent.com
```

Không cần Client Secret cho luồng ID token này. Không commit file `.env` thật. Khởi động lại Vite và API sau khi đổi env.

## Luồng đăng nhập

- Google Identity Services gửi ID token về `POST /api/v1/auth/google`.
- Backend xác minh chữ ký, audience, hạn token, `email_verified`, `hd=dntu.edu.vn` và email có đúng hậu tố `@dntu.edu.vn`.
- Chỉ người đã có tài khoản SkillBridge được đăng nhập. Ở lần đầu, Google `sub` được liên kết với tài khoản có cùng email; lần sau dùng `sub` này để tìm tài khoản. Vai trò vẫn lấy từ DB.
- Tài khoản khác domain, chưa đăng ký, bị khóa, hoặc token không hợp lệ đều không được tạo phiên.

`hd` trên frontend chỉ lọc gợi ý tài khoản ở màn chọn Google. Backend mới là nơi thực thi điều kiện domain.
