# خريطة المستودع ومسارات التتبع
الجذر: C:/Users/lenovo/Desktop/ACADEMIX. التاريخ: 2026-10-28.
هذه خريطة مصدرية أولية وليست مصفوفة اكتمال تجاري. ما لم يوصف بأنه متتبع، اكتشف من مسارات المصدر وتسجيل DI/DbSets وأسماء الدوال فقط.

## البنية الفعلية
Academix.slnx يحوي Academix.Api وAcademix.Tests. net10.0 وSDK 10.0.103. API يستخدم MVC وIdentity/EF Core 10.0.3 وNpgsql EF 10.0.0 وMinio 6.0.5. مشروع الاختبارات xUnit وWebApplicationFactory وTestcontainers.PostgreSql.
Program.cs → AddAcademixServices → UseAcademixApi → ApplyConfiguredMigrationsAsync المشروط → RunAsync.
الـmiddleware: ExceptionHandler → Routing → Authentication → RateLimiter → Authorization → Controllers. OpenAPI في Development. Health live بلا dependencies وready يفحص DbContext.
Scoped services → DbContext/PostgreSQL. PrivateFileStore singleton يعتمد MinIO. لا مبرر في الأدلة المقروءة لإعادة كتابة المشروع أو تحويله إلى microservices.
ملفات Entities لها CLR namespaces من Domain؛ هذا موثق في docs/architecture.md ومتوافق مع المصدر. تنظيمها لا يدل على مشروع Domain مستقل.
DbContext.cs:109-127 يسجل جميع configurations؛ لا query filter عام للمؤسسة في هذا المسار. التفويض والتقييد بالموارد مسؤولية الخدمات، ولا يفترض أن Where tenant يضاف تلقائياً.

## الوحدات والاعتمادات
| الوحدة | مداخل وخدمات المصدر | التخزين/الاعتمادات المكتشفة | نطاق هذه الجلسة |
|---|---|---|---|
| Identity | AuthController/UsersController → AuthService/IdentitySecurity/TeachingRolePolicy/IdentityBootstrap | Identity UserManager/SignInManager؛ Users/Roles/RefreshSessions/SecurityAudit | متتبع |
| Academic structure | Institutions/Faculties/Departments/Programs/AcademicTerms → StructureService | Institution/Faculty/Department/Program/Term؛ AcademicConfiguration | متتبع مصدرّياً؛ concurrency جزئي |
| Courses/Sections/Enrollment | Courses/Sections/Enrollments → AcademicOperations → Records.EligibilityForSection | ProgramCourse/CourseSection/TeachingAssignment/StudentEnrollment | متتبع |
| Public catalog | CourseCatalogController → ICourseQueries/CourseQueries | Courses → CourseDto | قراءة محدودة؛ catalogue لجميع active courses |
| Learning | LearningUnits/Lessons/Assignments/Submissions/LearningFiles/Announcements → LearningOperations | Learning entities وPrivateFileStore وLearningConfiguration | تواقيع فقط؛ نقطة البداية التالية |
| Examinations | QuestionBank/ExamBuilder/ExamTaking/ExamGrading/ExamResults → الخدمات المسماة | Question/revision/pool/published/attempt/answer/report/appeal | اكتشاف فقط |
| Workspaces | Student/Instructor/InstitutionWorkspace/PlatformGovernance → الخدمات المقابلة | يجمع من DbContext وWorkspacePolicy | اكتشاف فقط |
| Academic operations | AcademicTeaching/StudentAcademic/InstitutionAcademicOperations → TeachingOperations/StudentAcademicOperations/RegistrarOperations | timetable/session/attendance/calendar/requests | اكتشاف فقط |
| Advanced learning | AdvancedDiscussions/AdvancedGroupLearning → DiscussionService/GroupLearningService/AdvancedLearningAccess | topics/posts/teams/projects/rubrics/submissions | اكتشاف فقط |
| Gradebook | GradebookController → GradebookService/GradebookPolicy | policies/categories/components/manual/results/audit | dependency لقراءة prerequisites فقط؛ الخدمة غير مراجعة |
| Communications | CommunicationsController → CommunicationService/Policy | direct messages/threads/notifications/preferences/scheduled announcements | اكتشاف فقط |
| Governance | GovernanceController → GovernanceService/Policy | staff request/batch/items + security audit؛ Records eligibility | متتبع؛ موافقة اختيارية |
| Analytics/Advising | Analytics/AdvisingController → الخدمات والسياسات المقابلة | queries/risk؛ advising case/note/task/event | اكتشاف فقط |
| Records | RecordsController → RecordsService/RecordsPolicy | membership/prerequisite/transcript/standing/lifecycle events | تسجيل/انسحاب متتبع؛ بقية الاعتمادات جزئية |
| Curriculum | CurriculumController → CurriculumService/Policy | plan/course/band/student assignment | اكتشاف فقط |
| Lifecycle | StudentLifecycleController → LifecycleService/Policy | admission/transfer lines/graduation/decision events | اكتشاف فقط |
| Institutional documents | InstitutionalDocumentsController → InstitutionalDocumentService/PolicyRules | policy/request/issued document/events | اكتشاف فقط؛ verification/revocation لم تتبع |
| Storage | PrivateFileStore | MinIO bucket creation/put/get/remove | implementation مقروء؛ لا اختبار حي ولا authorization review |

## تتبع Identity وثوابته
LoginController:16 → LoginRequest → AuthService.LoginAsync:14-24 → FindByEmail/CheckPasswordSignIn(lockoutOnFailure=true) → IsActive وInstitution active إلا global → NewSessionAsync:27-34 → SHA256 refresh hash محفوظ وJWT 10 دقائق، عائلة سبعة أيام لا تمدد مع rotation.
RefreshController:22 → RefreshRequest → RefreshAsync:37-70 → execution strategy/transaction → SELECT token FOR UPDATE → رفض missing/expired؛ revoked token يبطل descendants الحالية ويcommit ثم 401؛ السليم يبطل القديم ويحفظ الجديد في العائلة نفسها ثم commit. Unique TokenHash وUser FK في IdentityConfiguration.
JWT validation يفحص signature/issuer/audience/lifetime مع ClockSkew=0، ثم active user/security stamp/institution claim/institution active. يجلب الأدوار الحالية لاختبار global؛ فحوص خدمات IdentitySecurity تجلب الأدوار الحالية وتحدد القرار على المورد.
ثوابت: لا non-global بلا مؤسسة active؛ لا teaching access بتكليف قديم وحده؛ نوع assignment يطابق role الحالي. لا ندعي مقارنة شاملة بين JWT role claims والأدوار الحالية في middleware؛ الحماية المتتبعة في الخدمات.
ChangeRole يغير role ثم stamp ثم audit؛ ChangeActive يغير active ثم stamp ويبطل refresh عند التعطيل. عدم جمع كل خطوات Identity بمعاملة واحدة وفحص stamp outcome فرضية O-03.
فحوص أخطاء: login Email=null مسار dereference؛ AUD-004. logout لا يقرأ sid أو يغير stamp؛ O-01/O-02 تحتاجان عقداً واختبار تزامن.

## تتبع التسجيل الفردي والجماعي
EnrollmentsController:14 → EnrollmentCreate(StudentId) → AcademicOperations.Enroll:162-191 → transaction/retry strategy → قفل course_sections FOR UPDATE → Admin لنفس المؤسسة أو global → Open/not published → student active/current Student/same institution → Records.EligibilityForSection → existing status → count Active < Capacity → insert/reactivate → Save → Commit → entity response.
EligibilityForSection:176-199 يقرأ prerequisites لنفس مؤسسة المقرر، ثم GradebookResults مع SectionGradePolicies المنشورة؛ يشترط Passed وحد minimum. لا يحسب النتيجة من grades غير المنشورة. لا يثبت هذا كفاية prerequisite equivalencies أو سياسة البرنامج؛ O-06.
Bulk: create idempotency بالمؤسسة وClientRequestId ومقارنة payload/creator؛ draft student IDs قد تكون unresolved عمداً. preview يفحص current roles/institution/status/prerequisites/duplicates/capacity؛ commit يقفل الشعبة نفسها ثم batch ويعيد التقييم ويحفظ كل التسجيلات وCommitted/Audit في transaction؛ retry بعد Committed يعيد النتيجة.
ثوابت مضمونة من المصدر: السعة الفردية والجماعية وتعديل Capacity تشترك في قفل الشعبة؛ unique (SectionId,StudentId) يمنع صفاً مكرراً؛ تعيين Instructor أساسي واحد عبر filtered unique index. FK مركب يحمي مؤسسة الشعبة/المقرر/الفصل؛ FK المستخدم لا يحمي مؤسسة المستخدم (AUD-005).
ثغرات failure/state: Closed term مع شعبة Open يظل قابلاً للتسجيل AUD-002؛ لا claim بأن locks بين term وsection منسقة. لا ندعي أمان التنفيذ عند transient commit ambiguity قبل اختباره.

## الانسحاب وعرض البيانات
EnrollmentsController.Withdraw → AcademicOperations.Withdraw → Records.ChangeRegistration؛ والمسار الحديث RecordsController.ChangeRegistration يستخدم الخدمة نفسها. StudentInstitution → RequireStudent يسمح الطالب لنفسه أو admin المؤسسة أو global. يقفل الشعبة، يقرأ term ويختبر policy publication والتاريخ، يطلب Active enrollment، يغير status ويكتب lifecycle/security audit في transaction.
RecordsPolicy.Transition يفرق Drop قبل StartsOn وWithdraw ضمن الفصل ويرفض بعد EndsOn أو نشر Gradebook. هذا لا يستعمل term.Status=Closed؛ افحص العلاقة بالسياسة أثناء الاستكمال، ولا تفترض أن التاريخ يساوي الحالة.
Roster يتطلب staff/admin ثم يعيد 100 من جميع الحالات دون paging؛ AUD-003. StudentHistory يتطلب الطالب نفسه أو admin ثم Take(100). لا استنتاج صلاحية الطلاب لقراءة roster من Authorize وحده.

## البيانات وMigrations
AcademicConfiguration قُرئ بالكامل؛ concurrency token Version للهيكل والشعب؛ unique codes بالمؤسسة؛ Restrict deletes؛ composite FKs بين الموارد الأكاديمية.
IdentityConfiguration قُرئ بالكامل: unique normalized email وtoken hash، user institution FK وrefresh user FK.
GovernanceConfiguration وRecordsConfiguration قُرئا بالكامل: idempotency uniques، request Version، قيود حالات Records وبعض العلاقات المركبة. StudentEnrollment.Status لا concurrency token في mapping الحالي؛ التزامن المتتبع يعتمد قفل الشعبة.
Phase1Complete migration قرئت مقاطع academic_terms/refresh/student_enrollments/teaching_assignments والفهارس؛ Snapshot قرئت مقاطع التسجيل/التكليف وعلاقاتهما. لم تفحص كامل سلسلة migration ولا تثبت consistency أو SQL execution. لا migration جديدة.

## الوثائق وحدود الادعاءات
docs/architecture.md يتوافق مع نموذج Controllers → Services → EF في المسارات المقروءة؛ توثيق private-file authorization وsubmission locking لم يتحقق بعد.
docs/progress.md يحوي نتائج تاريخية 18 اختباراً وpending-model check؛ لا تستعملها كدليل جديد. نتائج هذه الجلسة وحدها: build ناجح و217 اختباراً خارج Integration.
وجود خدمات phases لاحقة في DI وDbSets لا يثبت اكتمالها؛ لا features تجارية مقترحة تفصيلياً أو تقييم اعتماد مؤسسي نهائي قبل المراجعة.
النواقص المثبتة تجارياً حتى الآن محدودة: mandatory approval policy وroster عملي قابل للاستكمال. تقييم finance/accreditation/campus/migration/integration/localization مؤجل لفحص الوحدات ذات الصلة.

## سجل القراءة
راجع REVIEWED_FILES.csv للملفات والقراءة الكاملة مقابل الجزئية وبصمات الاستئناف. لم يقرأ appsettings أو .env أو archives/backups أو binary outputs كدليل عمل.
