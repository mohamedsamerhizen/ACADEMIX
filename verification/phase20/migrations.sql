START TRANSACTION;
ALTER TABLE graduation_applications DROP CONSTRAINT "CK_p16_graduation_status";

ALTER TABLE graduation_applications ADD "ApprovalEvidenceJson" jsonb;

ALTER TABLE graduation_applications ADD "ClientRequestId" uuid;

ALTER TABLE graduation_applications ADD "DepartmentReviewReason" character varying(1000);

ALTER TABLE graduation_applications ADD "DepartmentReviewedAtUtc" timestamp with time zone;

ALTER TABLE graduation_applications ADD "DepartmentReviewedById" uuid;

ALTER TABLE graduation_applications ADD "FinalEvidenceJson" jsonb;

ALTER TABLE graduation_applications ADD "RequiresDepartmentReview" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE curriculum_courses ADD CONSTRAINT "AK_curriculum_courses_PlanId_CourseId_InstitutionId" UNIQUE ("PlanId", "CourseId", "InstitutionId");

CREATE TABLE academic_advising_policies (
    "InstitutionId" uuid NOT NULL,
    "MaximumAdvisees" integer,
    "RepeatedFailureThreshold" integer NOT NULL,
    "MinimumProgressCredits" numeric NOT NULL,
    "RequireGoodStanding" boolean NOT NULL,
    "RequireDepartmentReview" boolean NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_academic_advising_policies" PRIMARY KEY ("InstitutionId"),
    CONSTRAINT "CK_p20_advising_policy" CHECK (("MaximumAdvisees" IS NULL OR "MaximumAdvisees" BETWEEN 1 AND 10000) AND "RepeatedFailureThreshold" BETWEEN 2 AND 20 AND "MinimumProgressCredits" BETWEEN 0 AND 500),
    CONSTRAINT "FK_academic_advising_policies_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE academic_advising_sessions (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "StudentId" uuid NOT NULL,
    "AdvisorId" uuid NOT NULL,
    "ClientRequestId" uuid NOT NULL,
    "ScheduledAtUtc" timestamp with time zone NOT NULL,
    "Status" character varying(20) NOT NULL,
    "InternalNotes" character varying(4000) NOT NULL,
    "Recommendation" character varying(2000) NOT NULL,
    "FollowUpAtUtc" timestamp with time zone,
    "AcknowledgedAtUtc" timestamp with time zone,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_academic_advising_sessions" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_academic_advising_sessions_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "CK_p20_session" CHECK ("Status" IN ('Scheduled','Completed','Cancelled') AND "StudentId" <> "AdvisorId" AND ("AcknowledgedAtUtc" IS NULL OR "Status" = 'Completed')),
    CONSTRAINT "FK_academic_advising_sessions_identity_users_AdvisorId" FOREIGN KEY ("AdvisorId") REFERENCES identity_users ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_academic_advising_sessions_identity_users_StudentId" FOREIGN KEY ("StudentId") REFERENCES identity_users ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_academic_advising_sessions_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE academic_advisor_assignments (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "StudentId" uuid NOT NULL,
    "AdvisorId" uuid NOT NULL,
    "AssignedBy" uuid NOT NULL,
    "Reason" character varying(500) NOT NULL,
    "AssignedAtUtc" timestamp with time zone NOT NULL,
    "EndedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_academic_advisor_assignments" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_academic_advisor_assignments_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "CK_p20_advisor" CHECK ("StudentId" <> "AdvisorId" AND ("EndedAtUtc" IS NULL OR "EndedAtUtc" >= "AssignedAtUtc")),
    CONSTRAINT "FK_academic_advisor_assignments_identity_users_AdvisorId" FOREIGN KEY ("AdvisorId") REFERENCES identity_users ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_academic_advisor_assignments_identity_users_AssignedBy" FOREIGN KEY ("AssignedBy") REFERENCES identity_users ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_academic_advisor_assignments_identity_users_StudentId" FOREIGN KEY ("StudentId") REFERENCES identity_users ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_academic_advisor_assignments_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE academic_requirement_exceptions (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "StudentId" uuid NOT NULL,
    "PlanId" uuid NOT NULL,
    "TargetCourseId" uuid NOT NULL,
    "SourceCourseId" uuid,
    "Kind" character varying(20) NOT NULL,
    "Status" character varying(20) NOT NULL,
    "Reason" character varying(1000) NOT NULL,
    "RequestedBy" uuid NOT NULL,
    "ReviewedBy" uuid,
    "DecisionReason" character varying(1000),
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_academic_requirement_exceptions" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_academic_requirement_exceptions_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "CK_p20_exception" CHECK ("Status" IN ('Submitted','Approved','Rejected') AND (("Kind"='Waiver' AND "SourceCourseId" IS NULL) OR ("Kind"='Substitution' AND "SourceCourseId" IS NOT NULL AND "SourceCourseId" <> "TargetCourseId")) AND ("Status" <> 'Approved' OR ("ReviewedBy" IS NOT NULL AND "ReviewedBy" <> "RequestedBy" AND "ReviewedBy" <> "StudentId"))),
    CONSTRAINT "FK_academic_requirement_exceptions_courses_SourceCourseId_Inst~" FOREIGN KEY ("SourceCourseId", "InstitutionId") REFERENCES courses (id, institution_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_academic_requirement_exceptions_curriculum_courses_PlanId_T~" FOREIGN KEY ("PlanId", "TargetCourseId", "InstitutionId") REFERENCES curriculum_courses ("PlanId", "CourseId", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_academic_requirement_exceptions_identity_users_RequestedBy" FOREIGN KEY ("RequestedBy") REFERENCES identity_users ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_academic_requirement_exceptions_identity_users_ReviewedBy" FOREIGN KEY ("ReviewedBy") REFERENCES identity_users ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_academic_requirement_exceptions_identity_users_StudentId" FOREIGN KEY ("StudentId") REFERENCES identity_users ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_academic_requirement_exceptions_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE curriculum_elective_groups (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "PlanId" uuid NOT NULL,
    "Name" character varying(120) NOT NULL,
    "MinimumCredits" numeric NOT NULL,
    CONSTRAINT "PK_curriculum_elective_groups" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_curriculum_elective_groups_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "AK_curriculum_elective_groups_Id_PlanId_InstitutionId" UNIQUE ("Id", "PlanId", "InstitutionId"),
    CONSTRAINT "CK_p20_group_credits" CHECK ("MinimumCredits" > 0 AND "MinimumCredits" <= 500),
    CONSTRAINT "FK_curriculum_elective_groups_curriculum_plans_PlanId_Institut~" FOREIGN KEY ("PlanId", "InstitutionId") REFERENCES curriculum_plans ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_curriculum_elective_groups_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE department_graduation_reviewers (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "DepartmentId" uuid NOT NULL,
    "ReviewerId" uuid NOT NULL,
    "IsActive" boolean NOT NULL,
    CONSTRAINT "PK_department_graduation_reviewers" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_department_graduation_reviewers_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "FK_department_graduation_reviewers_departments_DepartmentId_In~" FOREIGN KEY ("DepartmentId", "InstitutionId") REFERENCES departments ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_department_graduation_reviewers_identity_users_ReviewerId" FOREIGN KEY ("ReviewerId") REFERENCES identity_users ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_department_graduation_reviewers_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE curriculum_elective_group_courses (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "PlanId" uuid NOT NULL,
    "GroupId" uuid NOT NULL,
    "CourseId" uuid NOT NULL,
    CONSTRAINT "PK_curriculum_elective_group_courses" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_curriculum_elective_group_courses_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "FK_curriculum_elective_group_courses_curriculum_courses_PlanId~" FOREIGN KEY ("PlanId", "CourseId", "InstitutionId") REFERENCES curriculum_courses ("PlanId", "CourseId", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_curriculum_elective_group_courses_curriculum_elective_group~" FOREIGN KEY ("GroupId", "PlanId", "InstitutionId") REFERENCES curriculum_elective_groups ("Id", "PlanId", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_curriculum_elective_group_courses_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE student_study_plans (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "StudentId" uuid NOT NULL,
    "CurriculumPlanId" uuid NOT NULL,
    "CurrentRevision" integer NOT NULL,
    "ApprovedRevision" integer,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_student_study_plans" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_student_study_plans_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "FK_student_study_plans_curriculum_plans_CurriculumPlanId_Insti~" FOREIGN KEY ("CurriculumPlanId", "InstitutionId") REFERENCES curriculum_plans ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_student_study_plans_identity_users_StudentId" FOREIGN KEY ("StudentId") REFERENCES identity_users ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_student_study_plans_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE study_plan_revisions (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "StudyPlanId" uuid NOT NULL,
    "Revision" integer NOT NULL,
    "Status" character varying(20) NOT NULL,
    "Rationale" character varying(1000) NOT NULL,
    "Recommendation" character varying(2000),
    "CreatedBy" uuid NOT NULL,
    "ReviewedBy" uuid,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "AcknowledgedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_study_plan_revisions" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_study_plan_revisions_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "AK_study_plan_revisions_StudyPlanId_Revision_InstitutionId" UNIQUE ("StudyPlanId", "Revision", "InstitutionId"),
    CONSTRAINT "CK_p20_plan_revision" CHECK ("Revision" > 0 AND "Status" IN ('Proposed','Approved','Rejected','Acknowledged') AND ("Status" NOT IN ('Approved','Acknowledged') OR "ReviewedBy" IS NOT NULL) AND ("Status" <> 'Acknowledged' OR "AcknowledgedAtUtc" IS NOT NULL)),
    CONSTRAINT "FK_study_plan_revisions_identity_users_CreatedBy" FOREIGN KEY ("CreatedBy") REFERENCES identity_users ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_study_plan_revisions_identity_users_ReviewedBy" FOREIGN KEY ("ReviewedBy") REFERENCES identity_users ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_study_plan_revisions_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_study_plan_revisions_student_study_plans_StudyPlanId_Instit~" FOREIGN KEY ("StudyPlanId", "InstitutionId") REFERENCES student_study_plans ("Id", "InstitutionId") ON DELETE RESTRICT
);

CREATE TABLE study_plan_courses (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "RevisionId" uuid NOT NULL,
    "CourseId" uuid NOT NULL,
    "TermId" uuid NOT NULL,
    CONSTRAINT "PK_study_plan_courses" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_study_plan_courses_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "FK_study_plan_courses_academic_terms_TermId_InstitutionId" FOREIGN KEY ("TermId", "InstitutionId") REFERENCES academic_terms ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_study_plan_courses_courses_CourseId_InstitutionId" FOREIGN KEY ("CourseId", "InstitutionId") REFERENCES courses (id, institution_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_study_plan_courses_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_study_plan_courses_study_plan_revisions_RevisionId_Institut~" FOREIGN KEY ("RevisionId", "InstitutionId") REFERENCES study_plan_revisions ("Id", "InstitutionId") ON DELETE RESTRICT
);

CREATE INDEX "IX_graduation_applications_DepartmentReviewedById" ON graduation_applications ("DepartmentReviewedById");

CREATE UNIQUE INDEX "IX_graduation_applications_InstitutionId_StudentId_ClientReque~" ON graduation_applications ("InstitutionId", "StudentId", "ClientRequestId") WHERE "ClientRequestId" IS NOT NULL;

ALTER TABLE graduation_applications ADD CONSTRAINT "CK_p16_graduation_status" CHECK ("Status" IN ('Submitted','UnderReview','Approved','Rejected','Issued'));

ALTER TABLE graduation_applications ADD CONSTRAINT "CK_p20_graduation_evidence" CHECK ("ClientRequestId" IS NULL OR "Status" <> 'Issued' OR "FinalEvidenceJson" IS NOT NULL);

CREATE INDEX "IX_academic_advising_sessions_AdvisorId" ON academic_advising_sessions ("AdvisorId");

CREATE UNIQUE INDEX "IX_academic_advising_sessions_InstitutionId_StudentId_ClientRe~" ON academic_advising_sessions ("InstitutionId", "StudentId", "ClientRequestId");

CREATE INDEX "IX_academic_advising_sessions_InstitutionId_StudentId_Schedule~" ON academic_advising_sessions ("InstitutionId", "StudentId", "ScheduledAtUtc");

CREATE INDEX "IX_academic_advising_sessions_StudentId" ON academic_advising_sessions ("StudentId");

CREATE INDEX "IX_academic_advisor_assignments_AdvisorId" ON academic_advisor_assignments ("AdvisorId");

CREATE INDEX "IX_academic_advisor_assignments_AssignedBy" ON academic_advisor_assignments ("AssignedBy");

CREATE INDEX "IX_academic_advisor_assignments_InstitutionId_AdvisorId_EndedA~" ON academic_advisor_assignments ("InstitutionId", "AdvisorId", "EndedAtUtc");

CREATE UNIQUE INDEX "IX_academic_advisor_assignments_InstitutionId_StudentId" ON academic_advisor_assignments ("InstitutionId", "StudentId") WHERE "EndedAtUtc" IS NULL;

CREATE INDEX "IX_academic_advisor_assignments_StudentId" ON academic_advisor_assignments ("StudentId");

CREATE UNIQUE INDEX "IX_academic_requirement_exceptions_InstitutionId_StudentId_Pl~1" ON academic_requirement_exceptions ("InstitutionId", "StudentId", "PlanId", "TargetCourseId") WHERE "Status" IN ('Submitted','Approved');

CREATE UNIQUE INDEX "IX_academic_requirement_exceptions_InstitutionId_StudentId_Pla~" ON academic_requirement_exceptions ("InstitutionId", "StudentId", "PlanId", "SourceCourseId") WHERE "Status" = 'Approved' AND "SourceCourseId" IS NOT NULL;

CREATE INDEX "IX_academic_requirement_exceptions_PlanId_TargetCourseId_Insti~" ON academic_requirement_exceptions ("PlanId", "TargetCourseId", "InstitutionId");

CREATE INDEX "IX_academic_requirement_exceptions_RequestedBy" ON academic_requirement_exceptions ("RequestedBy");

CREATE INDEX "IX_academic_requirement_exceptions_ReviewedBy" ON academic_requirement_exceptions ("ReviewedBy");

CREATE INDEX "IX_academic_requirement_exceptions_SourceCourseId_InstitutionId" ON academic_requirement_exceptions ("SourceCourseId", "InstitutionId");

CREATE INDEX "IX_academic_requirement_exceptions_StudentId" ON academic_requirement_exceptions ("StudentId");

CREATE INDEX "IX_curriculum_elective_group_courses_GroupId_PlanId_Institutio~" ON curriculum_elective_group_courses ("GroupId", "PlanId", "InstitutionId");

CREATE INDEX "IX_curriculum_elective_group_courses_InstitutionId" ON curriculum_elective_group_courses ("InstitutionId");

CREATE UNIQUE INDEX "IX_curriculum_elective_group_courses_PlanId_CourseId" ON curriculum_elective_group_courses ("PlanId", "CourseId");

CREATE INDEX "IX_curriculum_elective_group_courses_PlanId_CourseId_Instituti~" ON curriculum_elective_group_courses ("PlanId", "CourseId", "InstitutionId");

CREATE INDEX "IX_curriculum_elective_groups_InstitutionId" ON curriculum_elective_groups ("InstitutionId");

CREATE INDEX "IX_curriculum_elective_groups_PlanId_InstitutionId" ON curriculum_elective_groups ("PlanId", "InstitutionId");

CREATE UNIQUE INDEX "IX_curriculum_elective_groups_PlanId_Name" ON curriculum_elective_groups ("PlanId", "Name");

CREATE INDEX "IX_department_graduation_reviewers_DepartmentId_InstitutionId" ON department_graduation_reviewers ("DepartmentId", "InstitutionId");

CREATE UNIQUE INDEX "IX_department_graduation_reviewers_DepartmentId_ReviewerId" ON department_graduation_reviewers ("DepartmentId", "ReviewerId");

CREATE INDEX "IX_department_graduation_reviewers_InstitutionId" ON department_graduation_reviewers ("InstitutionId");

CREATE INDEX "IX_department_graduation_reviewers_ReviewerId" ON department_graduation_reviewers ("ReviewerId");

CREATE INDEX "IX_student_study_plans_CurriculumPlanId_InstitutionId" ON student_study_plans ("CurriculumPlanId", "InstitutionId");

CREATE INDEX "IX_student_study_plans_Id_ApprovedRevision_InstitutionId" ON student_study_plans ("Id", "ApprovedRevision", "InstitutionId");

CREATE UNIQUE INDEX "IX_student_study_plans_InstitutionId_StudentId" ON student_study_plans ("InstitutionId", "StudentId");

CREATE INDEX "IX_student_study_plans_StudentId" ON student_study_plans ("StudentId");

CREATE INDEX "IX_study_plan_courses_CourseId_InstitutionId" ON study_plan_courses ("CourseId", "InstitutionId");

CREATE INDEX "IX_study_plan_courses_InstitutionId" ON study_plan_courses ("InstitutionId");

CREATE UNIQUE INDEX "IX_study_plan_courses_RevisionId_CourseId" ON study_plan_courses ("RevisionId", "CourseId");

CREATE INDEX "IX_study_plan_courses_RevisionId_InstitutionId" ON study_plan_courses ("RevisionId", "InstitutionId");

CREATE INDEX "IX_study_plan_courses_TermId_InstitutionId" ON study_plan_courses ("TermId", "InstitutionId");

CREATE INDEX "IX_study_plan_revisions_CreatedBy" ON study_plan_revisions ("CreatedBy");

CREATE INDEX "IX_study_plan_revisions_InstitutionId" ON study_plan_revisions ("InstitutionId");

CREATE INDEX "IX_study_plan_revisions_ReviewedBy" ON study_plan_revisions ("ReviewedBy");

CREATE INDEX "IX_study_plan_revisions_StudyPlanId_InstitutionId" ON study_plan_revisions ("StudyPlanId", "InstitutionId");

CREATE UNIQUE INDEX "IX_study_plan_revisions_StudyPlanId_Revision" ON study_plan_revisions ("StudyPlanId", "Revision");

ALTER TABLE graduation_applications ADD CONSTRAINT "FK_graduation_applications_identity_users_DepartmentReviewedBy~" FOREIGN KEY ("DepartmentReviewedById") REFERENCES identity_users ("Id") ON DELETE RESTRICT;

ALTER TABLE student_study_plans ADD CONSTRAINT "FK_student_study_plans_study_plan_revisions_Id_ApprovedRevisio~" FOREIGN KEY ("Id", "ApprovedRevision", "InstitutionId") REFERENCES study_plan_revisions ("StudyPlanId", "Revision", "InstitutionId") ON DELETE RESTRICT;

ALTER TABLE study_plan_revisions ADD "CurriculumPlanId" uuid NOT NULL;

CREATE INDEX "IX_study_plan_revisions_CurriculumPlanId_InstitutionId" ON study_plan_revisions ("CurriculumPlanId", "InstitutionId");

ALTER TABLE study_plan_revisions ADD CONSTRAINT "FK_study_plan_revisions_curriculum_plans_CurriculumPlanId_Inst~" FOREIGN KEY ("CurriculumPlanId", "InstitutionId") REFERENCES curriculum_plans ("Id", "InstitutionId") ON DELETE RESTRICT;

CREATE FUNCTION p20_user_scope() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE field_name text; user_id uuid; scope_id uuid;
BEGIN
 FOREACH field_name IN ARRAY TG_ARGV LOOP
  user_id := (to_jsonb(NEW)->>field_name)::uuid;
  IF user_id IS NOT NULL THEN
   SELECT "InstitutionId" INTO scope_id FROM identity_users WHERE "Id"=user_id;
   IF scope_id IS DISTINCT FROM NEW."InstitutionId" AND
      (field_name IN ('StudentId','AdvisorId','ReviewerId') OR NOT EXISTS
       (SELECT 1 FROM "AspNetUserRoles" ur JOIN identity_roles r ON r."Id"=ur."RoleId"
        WHERE ur."UserId"=user_id AND r."Name"='SuperAdministrator'))
   THEN RAISE EXCEPTION 'Academic progress user belongs to a different institution' USING ERRCODE='23514'; END IF;
  END IF;
 END LOOP; RETURN NEW;
END $$;
CREATE TRIGGER p20_assignment_scope BEFORE INSERT OR UPDATE ON academic_advisor_assignments FOR EACH ROW EXECUTE FUNCTION p20_user_scope('StudentId','AdvisorId','AssignedBy');
CREATE TRIGGER p20_session_scope BEFORE INSERT OR UPDATE ON academic_advising_sessions FOR EACH ROW EXECUTE FUNCTION p20_user_scope('StudentId','AdvisorId');
CREATE TRIGGER p20_study_scope BEFORE INSERT OR UPDATE ON student_study_plans FOR EACH ROW EXECUTE FUNCTION p20_user_scope('StudentId');
CREATE TRIGGER p20_revision_scope BEFORE INSERT OR UPDATE ON study_plan_revisions FOR EACH ROW EXECUTE FUNCTION p20_user_scope('CreatedBy','ReviewedBy');
CREATE TRIGGER p20_exception_scope BEFORE INSERT OR UPDATE ON academic_requirement_exceptions FOR EACH ROW EXECUTE FUNCTION p20_user_scope('StudentId','RequestedBy','ReviewedBy');
CREATE TRIGGER p20_department_scope BEFORE INSERT OR UPDATE ON department_graduation_reviewers FOR EACH ROW EXECUTE FUNCTION p20_user_scope('ReviewerId');
CREATE TRIGGER p20_graduation_scope BEFORE INSERT OR UPDATE ON graduation_applications FOR EACH ROW EXECUTE FUNCTION p20_user_scope('StudentId','ReviewedById','IssuedById','DepartmentReviewedById');

CREATE FUNCTION p20_elective_draft() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE plan_id uuid;
BEGIN
 IF TG_OP='DELETE' THEN plan_id:=OLD."PlanId"; ELSE plan_id:=NEW."PlanId"; END IF;
 IF NOT EXISTS(SELECT 1 FROM curriculum_plans WHERE "Id"=plan_id AND "Status"='Draft')
 THEN RAISE EXCEPTION 'Published elective groups are immutable' USING ERRCODE='23514'; END IF;
 IF TG_OP='UPDATE' AND OLD."PlanId" IS DISTINCT FROM NEW."PlanId" THEN RAISE EXCEPTION 'Group curriculum cannot change' USING ERRCODE='23514'; END IF;
 IF TG_TABLE_NAME='curriculum_elective_group_courses' AND TG_OP<>'DELETE' AND NOT EXISTS
    (SELECT 1 FROM curriculum_courses c WHERE c."PlanId"=NEW."PlanId" AND c."CourseId"=(to_jsonb(NEW)->>'CourseId')::uuid AND c."InstitutionId"=NEW."InstitutionId" AND c."RequirementKind"='Elective')
 THEN RAISE EXCEPTION 'Group members must be curriculum electives' USING ERRCODE='23514'; END IF;
 IF TG_OP='DELETE' THEN RETURN OLD; END IF; RETURN NEW;
END $$;
CREATE TRIGGER p20_group_draft BEFORE INSERT OR UPDATE OR DELETE ON curriculum_elective_groups FOR EACH ROW EXECUTE FUNCTION p20_elective_draft();
CREATE TRIGGER p20_group_member_draft BEFORE INSERT OR UPDATE OR DELETE ON curriculum_elective_group_courses FOR EACH ROW EXECUTE FUNCTION p20_elective_draft();

CREATE FUNCTION p20_history_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Academic progress history cannot be deleted' USING ERRCODE='23514'; END IF;
 IF TG_TABLE_NAME='study_plan_courses' OR
    TG_TABLE_NAME='study_plan_revisions' AND (to_jsonb(NEW)-ARRAY['Status','Recommendation','ReviewedBy','AcknowledgedAtUtc']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['Status','Recommendation','ReviewedBy','AcknowledgedAtUtc']) OR
    TG_TABLE_NAME='academic_advisor_assignments' AND (to_jsonb(NEW)-'EndedAtUtc') IS DISTINCT FROM (to_jsonb(OLD)-'EndedAtUtc') OR
    TG_TABLE_NAME='academic_requirement_exceptions' AND ((to_jsonb(NEW)-ARRAY['Status','ReviewedBy','DecisionReason','Version']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['Status','ReviewedBy','DecisionReason','Version']) OR to_jsonb(OLD)->>'Status'<>'Submitted')
 THEN RAISE EXCEPTION 'Academic progress evidence is immutable' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER p20_revision_history BEFORE UPDATE OR DELETE ON study_plan_revisions FOR EACH ROW EXECUTE FUNCTION p20_history_guard();
CREATE TRIGGER p20_plan_course_history BEFORE UPDATE OR DELETE ON study_plan_courses FOR EACH ROW EXECUTE FUNCTION p20_history_guard();
CREATE TRIGGER p20_assignment_history BEFORE UPDATE OR DELETE ON academic_advisor_assignments FOR EACH ROW EXECUTE FUNCTION p20_history_guard();
CREATE TRIGGER p20_exception_history BEFORE UPDATE OR DELETE ON academic_requirement_exceptions FOR EACH ROW EXECUTE FUNCTION p20_history_guard();

CREATE FUNCTION p20_graduation_evidence_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF NEW."Status" IS DISTINCT FROM OLD."Status" AND NOT
    (OLD."Status"='Submitted' AND NEW."Status" IN ('UnderReview','Approved','Rejected') OR
     OLD."Status"='UnderReview' AND NEW."Status" IN ('Approved','Rejected') OR
     OLD."Status"='Approved' AND NEW."Status"='Issued')
 THEN RAISE EXCEPTION 'Invalid graduation status transition' USING ERRCODE='23514'; END IF;
 IF NEW."ClientRequestId" IS NOT NULL AND NEW."Status"='Approved' AND
    (NEW."ApprovalEvidenceJson" IS NULL OR NEW."RequiresDepartmentReview" AND
     (NEW."DepartmentReviewedAtUtc" IS NULL OR NEW."DepartmentReviewedById"=NEW."ReviewedById"))
 THEN RAISE EXCEPTION 'Graduation approval requires evidence and independent configured review' USING ERRCODE='23514'; END IF;
 IF OLD."ApprovalEvidenceJson" IS NOT NULL AND NEW."ApprovalEvidenceJson" IS DISTINCT FROM OLD."ApprovalEvidenceJson" OR
    OLD."FinalEvidenceJson" IS NOT NULL AND NEW."FinalEvidenceJson" IS DISTINCT FROM OLD."FinalEvidenceJson" OR
    OLD."Status"='Issued' AND ROW(NEW."Status",NEW."CredentialNumber",NEW."IssuedAtUtc",NEW."IssuedById",NEW."PlanId",NEW."StudentId",NEW."InstitutionId",NEW."ProgramId")
      IS DISTINCT FROM ROW(OLD."Status",OLD."CredentialNumber",OLD."IssuedAtUtc",OLD."IssuedById",OLD."PlanId",OLD."StudentId",OLD."InstitutionId",OLD."ProgramId")
 THEN RAISE EXCEPTION 'Official graduation evidence is immutable' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER p20_graduation_evidence BEFORE UPDATE ON graduation_applications FOR EACH ROW EXECUTE FUNCTION p20_graduation_evidence_guard();

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261101100000_Phase20AcademicAdvisingAndGraduation', '10.0.3');

COMMIT;

