# ĐỀ XUẤT HỢP ĐỒNG DỮ LIỆU API (API CONTRACT PROPOSAL)
## PHÂN HỆ WORKSPACE, HỌC THUẬT & REALTIME (MODULE C)
**Mã công việc:** `A-02.3` (Phase 0 - Critical)  
**Người đề xuất:** Bảo (Workspace & Academic Lead)  
**Nhánh Git:** `feature/workspace-academic`  
**Gửi tới:** Hào (Auth & Notifications Lead), Doanh (Business & Projects Lead)  

---

### I. DANH SÁCH CONTRACTS BÀN GIAO LIÊN PHÂN HỆ (CROSS-MODULE HANDOFFS)

#### 1. Hợp đồng kích hoạt Workspace (`B-12` -> `C-01`)
*   **Producer:** Doanh (`B-12.1`) - Phát hành sau khi hoàn tất ký cam kết 3 bên (SV, DN, GV).
*   **Consumer:** Bảo (`B-12.2`, `C-01.1`) - Tiếp nhận và tạo duy nhất một Workspace an toàn (idempotent).
```typescript
export interface WorkspaceActivationPayload {
  projectId: string;          // GUID của dự án
  teamId?: string;            // GUID nhóm sinh viên (nếu ứng tuyển theo team)
  memberStudentIds: string[]; // Danh sách GUID của các sinh viên được chấp thuận
  lecturerId: string;         // GUID của giảng viên được phân công hướng dẫn
  activatedAt: string;        // ISO 8601 UTC timestamp
  idempotencyKey: string;     // Khóa chống trùng lặp sự kiện
}
```

#### 2. Hợp đồng sự kiện Nghiệm thu kép (`C-10` -> `B-13`)
*   **Producer:** Bảo (`C-10.1`, `C-10.2`) - Phát hành khi cả Giảng viên và Doanh nghiệp đều Review Đạt trên cùng một bản nộp.
*   **Consumer:** Doanh (`B-13.1`) - Tiếp nhận để mở khóa điều kiện chi trả phụ cấp sinh viên qua cổng SePay.
```typescript
export interface DualAcceptanceEventPayload {
  projectId: string;          // GUID của dự án
  milestoneId: string;        // GUID của mốc dự án được nghiệm thu
  submissionId: string;       // GUID của bản nộp được nghiệm thu
  submissionVersionId: string;// GUID của phiên bản nộp được nghiệm thu
  acceptedAt: string;         // ISO 8601 UTC timestamp
  technicalReviewId: string;  // GUID thẩm định kỹ thuật của GV
  businessReviewId: string;   // GUID thẩm định nghiệp vụ của DN
  idempotencyKey: string;     // Khóa chống thanh toán trùng lặp
}
```

#### 3. Hợp đồng sự kiện Workspace gửi sang Hệ thống Thông báo (`C-14.3` -> `A-08.1`)
*   **Producer:** Bảo (`C-14.3`) - Khi có biến động trong Workspace (Task đổi cột, Submission mới, Review mới).
*   **Consumer:** Hào (`A-08.1`) - Đẩy vào hàng đợi Outbox/Email.
```typescript
export interface WorkspaceDomainEvent {
  eventId: string;            // GUID sự kiện
  eventType: "TaskMoved" | "SubmissionCreated" | "ReviewCompleted" | "MilestoneLocked";
  projectId: string;
  actorUserId: string;
  recipientUserIds: string[]; // Danh sách người nhận thông báo
  title: string;
  message: string;
  deepLink: string;           // Đường dẫn điều hướng trong portal
  createdAt: string;
}
```

---

### II. CHI TIẾT ENDPOINTS VÀ KIỂU DỮ LIỆU TYPESCRIPT (DTOs)

#### 1. Quản lý Workspace (`WorkspacesController`)
*   `GET /api/v1/workspaces/{projectId}/overview` -> `ApiResponse<WorkspaceOverviewResponse>`
*   `GET /api/v1/workspaces/{projectId}/team` -> `ApiResponse<WorkspaceMemberResponse[]>`
*   `GET /api/v1/workspaces/{projectId}/activity` -> `PagedResponse<WorkspaceActivityResponse>`
*   `GET /api/v1/workspaces/{projectId}/settings` -> `ApiResponse<WorkspaceSettingsResponse>`
*   `PUT /api/v1/workspaces/{projectId}/settings` -> `ApiResponse<WorkspaceSettingsResponse>`

```typescript
export interface WorkspaceOverviewResponse {
  projectId: string;
  title: string;
  slug: string;
  status: string;
  memberCount: number;
  openTaskCount: number;
  overdueTaskCount: number;
  capabilities: string[];
}

export interface WorkspaceMemberResponse {
  studentId: string;
  displayName: string;
  joinedAt: string;
}

export interface WorkspaceActivityResponse {
  id: string;
  eventType: string;
  createdAt: string;
}

export interface WorkspaceSettingsResponse {
  projectId: string;
  membersCanCreateTasks: boolean;
  membersCanScheduleMeetings: boolean;
  workingAgreement?: string;
  updatedAt?: string;
}
```

#### 2. Quản lý Công việc & Kanban (`ProjectTasksController`)
*   `GET /api/v1/projects/{projectId}/board` -> `ApiResponse<KanbanBoardResponse>`
*   `POST /api/v1/projects/{projectId}/tasks` -> `ApiResponse<ProjectTaskResponse>` (Status 201)
*   `PUT /api/v1/tasks/{taskId}` -> `ApiResponse<ProjectTaskResponse>`
*   `DELETE /api/v1/tasks/{taskId}?version={versionGuid}` -> 204 No Content

```typescript
export type ProjectTaskStatus = "TODO" | "IN_PROGRESS" | "REVIEW" | "DONE";
export type ProjectTaskPriority = "LOW" | "MEDIUM" | "HIGH" | "URGENT";

export interface ProjectTaskResponse {
  id: string;
  projectId: string;
  title: string;
  description?: string;
  status: ProjectTaskStatus;
  priority: ProjectTaskPriority;
  assigneeStudentId?: string;
  dueAt?: string;
  sortOrder: number;
  version: string; // GUID optimistic concurrency
  createdAt: string;
}

export interface KanbanBoardResponse {
  projectId: string;
  columns: Record<ProjectTaskStatus, ProjectTaskResponse[]>;
}

export interface CreateProjectTaskRequest {
  title: string;
  description?: string;
  priority: ProjectTaskPriority;
  dueAt?: string;
}

export interface UpdateProjectTaskRequest extends CreateProjectTaskRequest {
  version: string;
}

export interface MoveProjectTaskRequest {
  version: string;
  status: ProjectTaskStatus;
  sortOrder: number;
}
```

#### 3. Quản lý Mốc tiến độ (`MilestonesController`)
*   `GET /api/v1/projects/{projectId}/milestones` -> `ApiResponse<MilestoneResponse[]>`
*   `POST /api/v1/projects/{projectId}/milestones` -> `ApiResponse<MilestoneResponse>` (Status 201)
*   `PUT /api/v1/milestones/{milestoneId}` -> `ApiResponse<MilestoneResponse>`

```typescript
export type MilestoneStatus = "PLANNED" | "IN_PROGRESS" | "SUBMITTED" | "APPROVED" | "REJECTED";

export interface MilestoneResponse {
  id: string;
  projectId: string;
  title: string;
  description?: string;
  sequence: number;
  dueAt: string;
  status: MilestoneStatus;
  isOverdue: boolean;
  version: string;
  createdAt: string;
  deliverables: MilestoneDeliverableResponse[];
  approvalHistory: MilestoneApprovalHistoryResponse[];
}

export interface MilestoneDeliverableResponse {
  id: string;
  milestoneId: string;
  name: string;
  criteria?: string;
  fileId?: string;
}

export interface MilestoneApprovalHistoryResponse {
  id: string;
  fromStatus: MilestoneStatus;
  toStatus: MilestoneStatus;
  actorUserId: string;
  note?: string;
  createdAt: string;
}
```

#### 4. Quản lý Nộp bài & Đánh giá kép (`SubmissionsController`, `TechnicalReviewsController`, `BusinessReviewsController`)
*   `POST /api/v1/projects/{projectId}/submissions` -> `ApiResponse<SubmissionResponse>` (Status 201)
*   `GET /api/v1/submissions/{submissionId}` -> `ApiResponse<SubmissionResponse>`
*   `POST /api/v1/lecturer/submissions/{submissionId}/technical-review` -> `ApiResponse<TechnicalReviewResponse>` (Status 201)
*   `POST /api/v1/company/submissions/{submissionId}/business-review` -> `ApiResponse<BusinessReviewResponse>` (Status 201)

```typescript
export interface CreateSubmissionRequest {
  milestoneId?: string;
  summary?: string;
  githubUrl?: string;
  demoUrl?: string;
  videoUrl?: string;
  fileId?: string;
}

export interface SubmissionResponse {
  id: string;
  projectId: string;
  milestoneId?: string;
  status: "DRAFT" | "SUBMITTED" | "UNDER_REVIEW" | "CHANGES_REQUESTED" | "ACCEPTED" | "REJECTED";
  currentVersionNumber: number;
  submittedByStudentId: string;
  version: string;
  createdAt: string;
}

export type TechnicalReviewDecision = "ACCEPTED" | "CHANGES_REQUESTED" | "REJECTED";

export interface CreateTechnicalReviewRequest {
  version: string;
  decision: TechnicalReviewDecision;
  feedback: string;
  criteriaNotes?: string;
}

export interface TechnicalReviewResponse {
  id: string;
  submissionId: string;
  submissionVersionId: string;
  reviewerUserId: string;
  decision: TechnicalReviewDecision;
  feedback: string;
  criteriaNotes?: string;
  createdAt: string;
}

export type BusinessReviewDecision = "ACCEPTED" | "CHANGES_REQUESTED" | "REJECTED";

export interface CreateBusinessReviewRequest {
  version: string;
  decision: BusinessReviewDecision;
  requirementsFeedback: string;
  collaborationFeedback?: string;
}

export interface BusinessReviewResponse {
  id: string;
  submissionId: string;
  submissionVersionId: string;
  companyId: string;
  reviewerUserId: string;
  decision: BusinessReviewDecision;
  requirementsFeedback: string;
  collaborationFeedback?: string;
  createdAt: string;
}
```

#### 5. Chấm điểm Rubric Tổng kết (`AcademicEvaluationsController`)
*   `GET /api/v1/projects/{projectId}/students/{studentId}/evaluation` -> `ApiResponse<AcademicEvaluationResponse>`
*   `POST /api/v1/projects/{projectId}/students/{studentId}/evaluation` -> `ApiResponse<AcademicEvaluationResponse>`
*   `POST /api/v1/projects/{projectId}/students/{studentId}/evaluation/finalize` -> 200 OK

```typescript
export interface CriterionScoreRequest {
  rubricCriterionId: string;
  score: number; // 0 - 100
}

export interface UpsertAcademicEvaluationRequest {
  rubricId: string;
  version?: string;
  scores: CriterionScoreRequest[];
}

export interface AcademicEvaluationResponse {
  id: string;
  projectId: string;
  studentId: string;
  rubricId: string;
  totalScore: number;
  isPassed: boolean;
  status: "DRAFT" | "FINALIZED";
  version: string;
  finalizedAt?: string;
}
```

---

### III. TRẠNG THÁI TIẾN TRÌNH & ĐIỂM DỪNG TIẾP THEO

Bản đề xuất hợp đồng API này đã được lưu vào mã nguồn backend.
Theo đúng quy trình phối hợp nhóm, chúng ta chuyển sang:

> [!CAUTION]
> ### 🛑 ĐIỂM DỪNG 1 (CHECKPOINT 1): DUYỆT VÀ HỢP NHẤT HỢP ĐỒNG API CHUNG
> *   **Tình trạng:** Chờ Hào (Auth/Catalog/Notif) và Doanh (Business/Project) duyệt và hoàn thiện các hợp đồng giao tiếp liên phân hệ (`B-12`, `C-10`, `C-14`).
> *   **Hành động tiếp theo sau khi duyệt:** Bảo sẽ hợp nhất các types này vào `src/shared/types` của Frontend và tiến hành xây dựng thư viện gọi API chung `src/shared/api` (`Task A-10.1`).
