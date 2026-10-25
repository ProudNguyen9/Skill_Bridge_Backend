# BÁO CÁO KHẢO SÁT HIỆN TRẠNG (AUDIT REPORT)
## PHÂN HỆ WORKSPACE, HỌC THUẬT & FRONTEND DÙNG CHUNG (MODULE C & SHARED FE)
**Mã công việc:** `A-01.3` (Phase 0 - Critical)  
**Người thực hiện:** Bảo  
**Nhánh Git:** `feature/workspace-academic`  
**Căn cứ tài liệu:** Google Sheets `DNTU SkillBridge` & Mã nguồn thực tế

---

### I. MỤC TIÊU & NGUYÊN TẮC KHẢO SÁT
1.  **Chỉ đọc (Read-only):** Không can thiệp, không sửa đổi code trong bước khảo sát.
2.  **Đối chiếu thực tế:** So sánh router FE (`src/app/router/routes.tsx`), component giao diện (`src/pages/workspace`, `src/pages/lecturer`, `src/pages/student`) với Controller BE thực tế trong `src/DNTU.SkillBridge.Api/Controllers`.
3.  **Xác định nguồn dữ liệu:** Đánh giá chính xác trạng thái là **Mock**, **Một phần** hay **Live (API thật)**.

---

### II. BẢNG TỔNG HỢP HIỆN TRẠNG CÁC LUỒNG NGHIỆP VỤ (U09 - U13)

| Luồng | Tên luồng nghiệp vụ | Route FE hiện có | Component FE | Trạng thái FE | API Backend hiện có | Trạng thái BE | Đánh giá tích hợp |
|---|---|---|---|---|---|---|---|
| **U08/U09** | **Kích hoạt & Vào Workspace** | `/student/workspace/:id`<br>`/company/workspace/:id`<br>`/lecturer/workspace/:id` | [`WorkspacePage.tsx`](file:///c:/Users/Bao/OneDrive/M%C3%A1y%20t%C3%ADnh/Skill_Bridge/Skill_Bridge_Fontend/src/pages/workspace/WorkspacePage.tsx)<br>[`LecturerWorkspaceView.tsx`](file:///c:/Users/Bao/OneDrive/M%C3%A1y%20t%C3%ADnh/Skill_Bridge/Skill_Bridge_Fontend/src/pages/workspace/LecturerWorkspaceView.tsx) | **100% Mock**<br>*(Đang đọc mockData.projects)* | `GET /api/v1/workspaces/{projectId}/overview`<br>`GET /api/v1/workspaces/{projectId}/team`<br>`GET /api/v1/workspaces/{projectId}/settings` | **Đã có API**<br>*(Hỗ trợ phân quyền membership)* | **Chưa nối**<br>FE chưa gọi API; route FE dùng `workspace/:id` nhưng BE nhận `{projectId}`. Cần chuẩn hóa ID. |
| **U09** | **Quản lý Công việc (Kanban)** | Tab Tasks trong Workspace | [`KanbanBoard.tsx`](file:///c:/Users/Bao/OneDrive/M%C3%A1y%20t%C3%ADnh/Skill_Bridge/Skill_Bridge_Fontend/src/pages/workspace/components/KanbanBoard.tsx) | **100% Mock**<br>*(Local state)* | `GET /api/v1/projects/{projectId}/board`<br>`POST /api/v1/projects/{projectId}/tasks`<br>`PUT /api/v1/tasks/{taskId}`<br>`DELETE /api/v1/tasks/{taskId}` | **Đã có API**<br>*(Hỗ trợ transition, version, optimistic concurrency)* | **Chưa nối**<br>Cần tạo API service và React Query hook cho Kanban. |
| **U09** | **Tiến độ & Mốc (Milestones)** | Tab Milestones trong Workspace | Timeline trong `WorkspacePage.tsx` | **100% Mock** | `GET /api/v1/projects/{projectId}/milestones`<br>`POST /api/v1/projects/{projectId}/milestones`<br>`PUT /api/v1/milestones/{milestoneId}` | **Đã có API**<br>*(Hỗ trợ khóa mốc đã duyệt, deliverable)* | **Chưa nối**<br>Cần đồng bộ DTO milestone deliverables giữa FE và BE. |
| **U09** | **Lịch họp Sprint (Meetings)** | Tab Meetings trong Workspace | Dialog / Tab trong `WorkspacePage.tsx` | **100% Mock** | `GET /api/v1/projects/{projectId}/meetings`<br>`POST /api/v1/projects/{projectId}/meetings`<br>`POST /api/v1/meetings/{meetingId}/cancel` | **Đã có API**<br>*(Kiểm soát attendee, hủy họp)* | **Chưa nối**<br>Cần format timezone VN thống nhất. |
| **U09** | **Quản lý Tài liệu (Files)** | Tab Documents trong Workspace | Panel upload trong `WorkspacePage.tsx` | **100% Mock** | `POST /api/v1/projects/{projectId}/files/upload-url`<br>`POST /api/v1/projects/{projectId}/files/download-url`<br>`GET /api/v1/projects/{projectId}/files` | **Đã có API**<br>*(MinIO/S3 signed URLs có thời hạn)* | **Chưa nối**<br>Cần cài đặt luồng upload trực tiếp lên S3 qua presigned URL. |
| **U10** | **Nộp sản phẩm (Submissions)** | Tab Submissions trong Workspace | [`StudentSubmissionPanel.tsx`](file:///c:/Users/Bao/OneDrive/M%C3%A1y%20t%C3%ADnh/Skill_Bridge/Skill_Bridge_Fontend/src/pages/workspace/components/StudentSubmissionPanel.tsx) | **100% Mock** | `POST /api/v1/projects/{projectId}/submissions`<br>`GET /api/v1/submissions/{submissionId}`<br>`POST /api/v1/submissions/{submissionId}/resubmit` | **Đã có API**<br>*(Version tăng dần, khóa sửa bản cũ)* | **Chưa nối**<br>Cần form nhập link git, tài liệu và demo URL chuẩn. |
| **U10** | **Thẩm định Kỹ thuật (GV Review)** | `/lecturer/workspace/:id/evaluation` | [`RubricEvaluationPage.tsx`](file:///c:/Users/Bao/OneDrive/M%C3%A1y%20t%C3%ADnh/Skill_Bridge/Skill_Bridge_Fontend/src/pages/lecturer/RubricEvaluationPage.tsx) | **100% Mock** | `POST /api/v1/lecturer/submissions/{id}/technical-review`<br>`GET /api/v1/lecturer/reviews` | **Đã có API**<br>*(Đánh giá 4 trụ cột kỹ thuật)* | **Chưa nối**<br>Cần phân biệt giữa Technical Review (trung gian) và Chấm điểm Rubric tổng kết. |
| **U10** | **Thẩm định Nghiệp vụ (DN Review)** | `/company/workspace/:id` | Tab Review trong `WorkspacePage.tsx` | **100% Mock** | `POST /api/v1/company/submissions/{id}/business-review` | **Đã có API**<br>*(Chặn review trước khi Technical Review đạt)* | **Chưa nối**<br>Cần bổ sung giao diện hiển thị kết quả thẩm định kỹ thuật cho DN trước khi DN review. |
| **U10** | **Nghiệm thu kép & Rubric cuối** | `/lecturer/workspace/:id/evaluation` | [`RubricEvaluationPage.tsx`](file:///c:/Users/Bao/OneDrive/M%C3%A1y%20t%C3%ADnh/Skill_Bridge/Skill_Bridge_Fontend/src/pages/lecturer/RubricEvaluationPage.tsx) | **100% Mock** | `POST /api/v1/projects/{projectId}/students/{studentId}/evaluation`<br>`POST /.../evaluation/finalize`<br>`ProjectCompletionsController` | **Đã có API**<br>*(Hỗ trợ tính điểm theo trọng số, finalize)* | **Chưa nối**<br>Phải đảm bảo cổng chấm điểm cuối chỉ mở khi đã đạt nghiệm thu kép. |
| **U11** | **Dashboard Giảng viên & Admin** | `/lecturer/dashboard`<br>`/admin/dashboard` | `src/pages/dashboard/LecturerDashboardView.tsx`<br>`src/pages/admin/AdminDashboardView.tsx` | **100% Mock** | `GET /api/v1/dashboards/lecturer`<br>`GET /api/v1/dashboards/admin` | **Đã có API** | **Chưa nối**<br>Cần tạo hook lấy KPI thực tế thay cho dữ liệu số liệu giả. |
| **U12** | **Realtime SignalR Workspace** | Toàn bộ Workspace | Chưa có SignalR client listener | **Chưa có** | `ProjectHub` trên backend | **Đã có Hub** | **Chưa nối**<br>Cần cài đặt kết nối `@microsoft/signalr`, join group theo workspace, xử lý reconnect & snapshot. |
| **U13** | **Xác thực Kỹ năng (Phase 2)** | `/lecturer/skills/verification` | [`SkillVerificationPage.tsx`](file:///c:/Users/Bao/OneDrive/M%C3%A1y%20t%C3%ADnh/Skill_Bridge/Skill_Bridge_Fontend/src/pages/lecturer/SkillVerificationPage.tsx) | **100% Mock** | `POST /api/v1/lecturer/students/{id}/skills/{skillId}/verify`<br>`POST /.../revoke-verification` | **Đã có API** | **Chưa nối** *(Phase 2)* |
| **U13** | **Skill Passport & Export (Phase 2)** | `/student/portfolio` | [`PortfolioPage.tsx`](file:///c:/Users/Bao/OneDrive/M%C3%A1y%20t%C3%ADnh/Skill_Bridge/Skill_Bridge_Fontend/src/pages/student/PortfolioPage.tsx) | **100% Mock** | `GET /api/v1/students/me/verified-skills`<br>`GET /api/v1/students/{id}/verified-skills` | **Đã có API** | **Chưa nối** *(Phase 2)* |

---

### III. KHẢO SÁT HẠ TẦNG DÙNG CHUNG (SHARED FRONTEND)

1.  **Thư viện API Client (`src/shared/api`):**
    *   File [`api-client.ts`](file:///c:/Users/Bao/OneDrive/M%C3%A1y%20t%C3%ADnh/Skill_Bridge/Skill_Bridge_Fontend/src/shared/api/api-client.ts) đã được cấu hình với Axios instance, hỗ trợ `token-storage.ts`, request interceptor và response interceptor cơ bản.
    *   Tuy nhiên, **chưa có cơ chế Single-flight refresh token** và xử lý 401 tập trung (đang chờ hook Auth từ Hào).
    *   Chưa có wrapper typed methods cho các feature của Module C.
2.  **Định nghĩa kiểu dữ liệu (`src/shared/types`):**
    *   Mới chỉ có [`api.types.ts`](file:///c:/Users/Bao/OneDrive/M%C3%A1y%20t%C3%ADnh/Skill_Bridge/Skill_Bridge_Fontend/src/shared/types/api.types.ts) chứa các wrapper chung như `ApiResponse<T>`, `PagedResponse<T>`, `ApiErrorResponse`.
    *   Hoàn toàn **chưa có types chính thức** cho: `Workspace`, `ProjectTask`, `Milestone`, `SprintMeeting`, `Submission`, `TechnicalReview`, `BusinessReview`, `RubricEvaluation`.
3.  **Hệ thống Router (`src/app/router/routes.tsx`):**
    *   Đã khai báo sẵn các đường dẫn chuẩn cho sinh viên, doanh nghiệp, giảng viên và admin.
    *   Chưa có Route Guards kiểm tra phân quyền thực tế (đang render trực tiếp component mà không chặn khi chưa đăng nhập hoặc sai role).

---

### IV. KẾT LUẬN & ĐỀ XUẤT CHO BƯỚC TIẾP THEO
1.  **Phía Backend:** Hệ thống API Controller và Application Services của Module C đã rất hoàn thiện, tuân thủ chặt chẽ Clean Architecture, phân quyền theo role/membership và optimistic concurrency.
2.  **Phía Frontend:** Khung giao diện (UI) đã hoàn tất và đạt độ thẩm mỹ cao, nhưng toàn bộ đang tách rời khỏi Backend và chạy bằng `mockData`.
3.  **Việc cần làm ngay (Task A-02.3):** Xuất bản tài liệu **Đề xuất Hợp đồng API (API Contract Proposal)** chuẩn hóa toàn bộ Request/Response DTOs và Enums tương thích 1:1 giữa Backend C# và Frontend TypeScript để chuẩn bị tiến vào **Điểm dừng 1 (Checkpoint 1)**.
