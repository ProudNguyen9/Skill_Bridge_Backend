# Audit checkpoint — Tasks 18–20

**Date:** 2026-08-29

## Latest implementation checkpoint

- Added team nomination to applications through [`20260829173708_AddApplicationTeamSelection.cs`](../../src/DNTU.SkillBridge.Infrastructure/Persistence/Migrations/20260829173708_AddApplicationTeamSelection.cs); the local PostgreSQL `skillbridge` database is applied through this migration.
- Company review now returns a company-only applicant projection and enforces the selection boundary server-side; accepted nominated teams are locked and commitments are created for every team member in the same transaction.
- Acceptance capacity is serialized by a PostgreSQL project-row lock and backed by a partial unique accepted-application index. Integration coverage now includes parallel capacity selection and team lock/commitment creation.
- Added one functional API smoke test for health/OpenAPI/Swagger contracts. Its host points to the configured local PostgreSQL database without mutating data.
- Latest clean validation: build `0` warnings / `0` errors; `13` unit, `78` integration, and `6` functional tests pass; EF reports no pending model changes.
- Tasks 18–20 remain `IN_PROGRESS`: Task 19 now has PostgreSQL-backed HTTP/expiration coverage and Task 20 has lecturer/admin/append-only scope evidence, but neither is `DONE` until every task-file checklist item and functional evidence are complete.

## Baseline repaired

- Reconciled EF Core model/snapshot drift with `20260829154156_ReconcileTask18To31Scaffold`.
- Applied local PostgreSQL migrations through `20260829162023_AddWorkspaceSettings`.
- Confirmed `dotnet ef migrations has-pending-model-changes` reports no pending model changes.
- Clean solution build: 0 warnings, 0 errors.
- Current test baseline: 12 unit tests and 75 integration tests pass. The functional test project has no discoverable tests yet.

## Task 18 progress

- Added immutable company decision audit fields to applications.
- Added `20260829155302_AddApplicationSelectionAndProjectMembership`.
- Company review operations use server-side company scope and serializable acceptance transaction.
- Acceptance creates the required pending commitment and activity row atomically.
- Application status guards and workspace activation after confirmation are covered in integration tests.

**Remaining:** team locking/conflict rejection policy, explicit parallel acceptance test, and functional workflow evidence.

## Task 19 progress

- Added governed `WithdrawalRequest` state machine, persistence mapping, migration `20260829160333_AddGovernedWithdrawalWorkflow`, student request/read endpoints, lecturer recommendation, and admin decision endpoints.
- Approval deactivates project membership transactionally using a set-based update within the decision transaction.
- Added unit transition tests and PostgreSQL-backed HTTP integration coverage for foreign-lecturer denial, assigned-lecturer recommendation, immutable admin decision, and expiration processing.

**Remaining:** abandonment-policy enforcement and functional browser workflow evidence.

## Task 20 progress

- Added workspace settings model and migration `20260829162023_AddWorkspaceSettings`.
- Expanded workspace authorization to current active student members, owning-company members, active assigned lecturers, and administrators.
- Added overview, team, activity, settings, student project list/detail API endpoints and response contracts.
- Added integration evidence for confirmed-student/owning-company access, foreign-company and non-assigned-lecturer denial, assigned-lecturer/admin scope, and HTTP append-only protection for the activity feed.

**Remaining:** dedicated activity paging/filtering and functional workflow evidence before `DONE`.

## Delivery board discipline

Tasks 18–20 remain `IN_PROGRESS`; no task has been marked `DONE` without all required migration, contract, authorization, OpenAPI, testing, and evidence criteria.
