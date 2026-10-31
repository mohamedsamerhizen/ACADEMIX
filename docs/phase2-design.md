# Phase 2 — Learning Management System

## Scope

- Versioned course units and lessons with draft/publication states.
- Announcements and assignments, submission drafts and idempotent submission.
- Manual grading with maximum-score validation.
- Lesson completion tracking and percentage calculation for enrolled students.
- Students can attach their own uploaded file to a submission; other students cannot download those files.
- Private MinIO-backed staff and enrolled-student uploads (PDF, JPEG, PNG, TXT; 20 MiB limit) and authorization-controlled downloads.
- Tenant boundaries via institution and section checks; students must have active enrollment and the section must be Open.

## Endpoints

`GET /api/lms/sections/{sectionId}/content` — published units and lessons (staff can view drafts).

`POST /api/lms/sections/{sectionId}/units` — staff; payload `title, position`.

`PUT /api/lms/units/{id}/publish` — staff; payload `isPublished, expectedVersion`.

`POST /api/lms/sections/{sectionId}/lessons` — staff; payload `unitId, title, body, position`.

`PUT /api/lms/lessons/{id}/publish` — staff; payload `isPublished, expectedVersion`.

`POST/GET /api/lms/sections/{sectionId}/announcements` — staff create, enrolled student/staff view.

`POST/GET /api/lms/sections/{sectionId}/assignments` — staff create, enrolled student/staff view.

`PUT /api/lms/assignments/{id}/publish` — staff; `isPublished, expectedVersion`.

`PUT /api/lms/assignments/{id}/my-draft` — enrolled student; `content` and optional `attachmentId` referring to their own section upload.

`POST /api/lms/assignments/{id}/submit` — enrolled student, idempotent.

`GET /api/lms/assignments/{id}/my-submission` — student only.

`GET /api/lms/assignments/{id}/submissions` — assigned staff/admin only.

`PUT /api/lms/submissions/{id}/grade` — assigned staff/admin; `score, feedback`.

`PUT /api/lms/lessons/{id}/complete` — enrolled student.

`GET /api/lms/sections/{sectionId}/my-progress` — enrolled student.

`POST /api/lms/sections/{sectionId}/files` — staff or enrolled student, multipart form field `file`.

`GET /api/lms/files/{id}/download` — section staff or actively enrolled student.

## Production constraints

- The historical MinIO image is provided for **local development only**, since the upstream community server image is archived. Production must use a supported maintained S3-compatible service with reviewed security updates and license terms.
- File content-type checks are header based; production requires AV scanning, magic-byte inspection, storage object lifecycle rules and streaming downloads.
- Submission grade changes are permitted to staff; production should add immutable grading audit history and publication rules.
- Tests execute using a real PostgreSQL container, but no MinIO binary integration tests are included yet. Test a real upload/download before release.
- No public S3 URLs are issued. HTTP download is protected by API authentication and membership.
- Phase 2 migration must be generated against **your** Phase 1 EF snapshot and inspected before execution; apply-phase2.ps1 halts on destructive operations.
