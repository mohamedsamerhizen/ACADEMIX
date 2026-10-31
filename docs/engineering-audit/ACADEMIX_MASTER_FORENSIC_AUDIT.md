# ACADEMIX — MASTER FORENSIC ENGINEERING AUDIT
**التاريخ:** 2026-10-28 — Asia/Baghdad. **المستودع:** C:/Users/lenovo/Desktop/ACADEMIX.
**حالة التقرير:** مراجعة مصدرية واسعة للوحدات، مع تغطية غير مكتملة لبعض العقود والاختبارات وعمليات Migrations التفصيلية. ليس شهادة تدقيق شاملة لكل سطر، ولا موافقة نشر. حدود التغطية محددة أدناه. هذا هو التقرير الجديد الوحيد؛ الوثائق السابقة بقيت دون تعديل.

## 1 — الحكم التقني التنفيذي
ACADEMIX مشروع Backend كبير وذو قيمة هندسية حقيقية: modular monolith متماسك، تفويض على الموارد، snapshots للأسئلة، تسلسل autosave، فواصل للنشر، معاملات وأقفال PostgreSQL، GPA ومناهج ومعادلات ووثائق. لكن وجود هذه الآليات لا يعني سلامة تشغيلها معاً.
أبرز مانعين تقنيين: سلسلة Migrations تتضمن أسماء أعمدة FK غير مطابقة لجدول courses، ومسارات كتابة في LMS والمشاريع والمراسلات والحضور تبدأ معاملات دون execution strategy رغم تفعيل retries. الأول يهدد إنشاء القاعدة والوصول إلى الاختبارات؛ الثاني يقطع workflows أساسية بعد حل الأول. نجاح Release Build و217 اختباراً خارج Integration لم يكشفهما.
الحصيلة: **9 عيوب مؤكدة مصدرّياً** (6 في التطبيق/سلسلة القاعدة و3 في الاختبارات)، و**6 مخاطر/فجوات تحقق عالية الخطورة P1**، إضافة إلى مخاطر P2 موضحة منفصلة. حددت **7 قدرات موجودة جزئياً** تحتاج استكمالاً؛ لا يعني ذلك أن بقية المنصة مكتملة. لا نتيجة P0 أو اختراق cross-tenant مثبت.
المشروع مناسب كـportfolio متقدم بعد عرض القيود بأمانة، وقابل للتطوير إلى تجربة جامعية محدودة بعد consolidation. ليس منتجاً تجارياً جاهزاً: لا واجهة مستخدم مثبتة في المصدر، لا تكامل PostgreSQL منفذ، ولا دليل restore/load/operational readiness. لا أوصي بإعادة كتابة البنية.

## 2 — خط الأساس المتحقق
- تأكد الجذر الصحيح ووجود Academix.Api وAcademix.Tests وAcademix.slnx وقرئت AGENTS.md. لم يعتمد مجلد Documents/ChatGPT أو backups.
- المشروعان net10.0؛ SDK المحلي 10.0.103. Identity/EF Core/JWT/OpenAPI 10.0.3؛ Npgsql EF 10.0.0؛ Minio 6.0.5؛ xUnit 2.9.3؛ Testcontainers.PostgreSql 4.16.0. هذه نسخ المشروع، وليست توصية بإصدارات أحدث.
- قورنت بصمات الملفات الـ67 في سجل التدقيق السابق بالمصدر الحالي: **0 ملفات مختلفة**. ثم أعيدت قراءة الأدلة الرئيسية؛ لم تقبل النتائج القديمة لمجرد وجودها في التقرير.
- git status في الجلسة السابقة فشل برسالة not a work tree؛ لا commit SHA أو ادعاء clean Git. المجلد المشترك للمحادثة ليس الجذر المعتمد.
- لم تعدل مصادر أو اختبارات أو إعدادات أو أسرار أو migrations، ولم تحذف بيانات أو تثبت dependencies جديدة. أوامر Release استعملت --no-restore والتبعيات المستعادة سابقاً.
- Docker Desktop Linux engine غير متاح في الفحص السابق من نفس المحادثة؛ لم أعد محاولات تشغيله أو إصلاحه، ولم أشغل Integration أو التطبيق أو MinIO أو أطبق migrations.

الأمر المنفذ فعلياً:
```powershell
dotnet build Academix.slnx --configuration Release -warnaserror --no-restore --artifacts-path docs/engineering-audit/runtime-artifacts --verbosity minimal
```
**exit 0؛ 0 warnings، 0 errors؛ 00:00:15.05.**
```powershell
dotnet test Academix.Tests/Academix.Tests.csproj --configuration Release --no-build --no-restore --artifacts-path docs/engineering-audit/runtime-artifacts --filter 'FullyQualifiedName!~Academix.Tests.Integration' --verbosity minimal
```
**exit 0؛ Passed 217، Failed 0، Skipped 0؛ 818 ms.** لم ينتج تقرير اختبارات جديداً. runtime-artifacts مخرجات build داخل مجلد التدقيق وليست تغييرات تطبيق. لا EF pending-model check منفذ في هذه الجولة.

## 3 — نطاق التغطية الفعلي
**المعيار:** «متتبع مصدرّياً» يعني أن الخدمة وقراراتها واستعلاماتها ومعاملاتها قرئت وربطت بالـmapping والمداخل المتاحة؛ لا يعني نجاح HTTP أو PostgreSQL. «جزئي» يعني أن بعض DTOs أو helpers أو الاختبارات أو فروع التزامن لم تراجع كاملاً.
| الوحدة | ما فُحص | حدود التغطية |
|---|---|---|
| Identity/Academic/Governance/Records | خدماتها وكياناتها وEF وControllers والعقود الأساسية، الاختبارات السابقة وأدلتها أعيد التحقق منها | حالات فشل Identity وتزامن retries غير منفذة |
| LMS/Files | LearningOperations كاملاً، content policy، LearningFilesController، LearningContracts، LearningEntities وLearningConfiguration، Phase2 workflow test | بقية LMS controllers وعقد HTTP الكامل جزئي؛ MinIO غير مشغل |
| Question bank/Builder | الخدمات كاملة بما فيها preview، policies/validation/pools، entities وExaminationConfiguration | اختبارات bank/builder/pool فحص انتقائي وليس كل assertions |
| Attempts/Grades/Appeals | ExamTakingService وExamGradingService وpolicies وAttemptConfiguration وGradingConfiguration وDTO attempts وControllers المعنية | عقود grading وبعض tests جزئية؛ لا امتحان فعلي منفذ |
| Gradebook | الخدمة والكيانات والـmapping كاملة؛ اختبار Phase9 قرئ | GradebookPolicy وجميع عقوده واختبارات مزج المصادر ليست مراجعة كاملة |
| Academic operations | Teaching/Student/Registrar services، entities وconfiguration | policy وبعض DTOs وPhase7 tests جزئية |
| Discussions/Projects | Discussion/GroupLearning/Access كاملة وكياناتها وmapping وعقودها | RubricPolicy وتزامن عضوية الفريق واختبارات Phase8 جزئية |
| Communication | الخدمة والكيانات وmapping كاملة؛ routes انتقائية | policy وPhase10 tests وdelivery تحت الفشل جزئية |
| Workspaces/Analytics | خدمات Workspaces الأربع وAnalytics كاملة | بعض policies/contracts/tests جزئية؛ لا SQL plans |
| Advising | الخدمة والكيانات وmapping ومشاهد الخصوصية قرئت | AdvisingPolicy/كل اختبارات Phase13 جزئية |
| Curriculum/Lifecycle | الخدمتان وCurriculumPolicy وLifecyclePolicy والكيانات وmapping، routes والعقود الأساسية، الجزء الوظيفي من Phase16 test | نجاح إصدار التخرج عبر كل الاعتمادات غير متحقق |
| Documents | الخدمة كاملة وكياناتها وmapping وDTO/routes وPhase17 workflow | PolicyRules ليس فحصاً مستقلاً لكل حد؛ لا rendering أو rotation runtime |
| Migrations | جرد زمني لجميع ملفات التنفيذ الـ18؛ قراءة constraints/indexes والعمليات الرئيسية بدرجات متفاوتة، فحص source مباشر لأخطاء Phase14/15/16 | **ليس فحصاً كاملاً لكل عمود وDown وDesigner**؛ لا SQL generation/run أو model diff |
| Deployment | Dockerfile API وdockerignore وglobal/props وحقول تشغيل compose وverify script | أسرار/config live لم تقرأ؛ CI/backups/network deployment غير مدققة بالكامل |

الخدمات الأساسية عبر جميع الوحدات قرئت؛ لم أستنتج صحة وحدة من اسمها. سجل الملفات الإضافي في ملحق هذا التقرير يفرق القراءة الكاملة والانتقائية. الأجزاء غير المنجزة: جميع test bodies المتبقية، كل DTO validation boundary، المصادقة تحت fault injection، كل Designer/Down ومقارنة النموذج، الأداء/الاستعادة/الواجهة. اتساع التغطية لا يلغي هذه الحدود.

## 4 — تقييم البنية
Program.cs:27-34 يركب الخدمات ثم pipeline ثم migrations المشروطة. Extensions/ServiceCollectionExtensions.cs:37-58 يسجل DbContext/Identity؛ 98-127 يسجل الخدمات؛ WebApplicationExtensions.cs:10-23 يضبط ExceptionHandler → Routing → Authentication → RateLimiter → Authorization. Controllers في المسارات المتتبعة تمرر User وCancellationToken إلى الخدمات؛ business rules ليست مزروعة داخل endpoint.
الاعتمادات الفعلية: AcademicOperations → RecordsService.EligibilityForSection؛ GovernanceService → RecordsService؛ LifecycleService → CurriculumService؛ LearningOperations → IdentitySecurity/PrivateFileStore؛ CommunicationService → AdvancedLearningAccess/IdentitySecurity/UserManager؛ GradebookService يقرأ LMS/Exams/GroupProject. الربط مناسب لمونوليث، لكن الثوابت المشتركة موزعة: publication authorization يعاد في Builder/Grading/Gradebook، وsnapshot freshness لا ينسق بين Grades/Transcript/GPA/Graduation.
التحسين المستهدف: توحيد transaction/retry runner ذي context جديد لكل محاولة، selectors مشتركة للـtenant/staff مع capability publish منفصلة، وسياسة academic eligibility versioned. لا حاجة إلى CQRS أو microservices أو Repository يلف EF بلا فائدة. تفصل الحسابات النقية عن I/O كما تفعل ExamAttemptPolicy وCurriculumPolicy بالفعل.
المشكلات القابلة للقياس: استعلام لكل طالب ولكل component في Gradebook.Compute (AUD-028)، وعقود object/entity غير متجانسة (AUD-029). حجم الخدمة وحده ليس عيباً؛ مصدر العيب هو فقد invariant عبر الحدود، لا عدد الأسطر.

## 5 — المصادقة والأمن
Login: AuthController:13-23 → AuthService.LoginAsync:14-24 → FindByEmail/CheckPasswordSignIn(lockoutOnFailure=true) → active account/institution → NewSession. السياسة: كلمة مرور 12 حرفاً ومتطلبات تركيب؛ lockout خمس محاولات/15 دقيقة؛ rate-limit عشر طلبات/دقيقة لكل RemoteIpAddress. لا استنتاج حماية موزعة أو trusted-proxy configuration من هذه السياسة المحلية.
JWT: ServiceCollectionExtensions.cs:64-80 يفحص issuer/audience/key/lifetime، ClockSkew=0؛ ValidateSessionAsync:139-177 يعيد قراءة active user وsecurity stamp وinstitution claim والمؤسسة. IdentitySecurity.CurrentAsync:16-31 يقرأ الأدوار الحالية. لا ادعاء بأن middleware يستبدل كل role claims؛ القرارات المتتبعة في الخدمات لا تعتمد عليها وحدها.
Refresh: AuthService.cs:37-70 يقفل token row داخل execution strategy ومعاملة؛ يخزن SHA256 hash لا token نصياً، ويولد 48 random bytes؛ rotation ضمن family بتاريخ انتهاء ثابت. replay للقديم يبطل descendants الحالية ويcommit قبل رفض الطلب. هذا تصميم جيد مصدرّياً؛ لا يثبت سلامة كل interleaving بين refresh/logout.
Logout:73-79 يبطل refresh المرسل وحده، ولا يبطل access JWT أو family بالكامل. سحب الدور يحدث stamp، لكن نتائج stamp update غير مفحوصة في المسارات المباشرة؛ AUD-013/AUD-018.
لم أجد API password reset/confirm email/MFA enrollment في البحث الموجه للمصدر، رغم وجود token providers وحقول Identity؛ لا تعتبر حقول EF ميزة مستخدم مكتملة. المدخلات null في Identity مشكلة مثبتة AUD-004.
مفاتيح الإجابة: ExamTakingService.Detail:67-80 يعيد Stem/Options/Points بلا Explanation/AnswerKey؛ StudentExamQuestion لا يحتوي المفتاح. ExamGradingService.Result:281-294 يشترط Published ويعيد درجات/feedback فقط؛ MyResults يقيد الطالب. QuestionBankService.RequireCourseStaff:18-31 يقصر bank بما فيه keys على موظف حالٍ لنفس المقرر أو admin مناسب. لم يثبت تسريب مفتاح عبر المسارات المقروءة؛ ليس هذا بديلاً لاختبارات HTTP سلبية لكل DTO.

## 6 — تعدد المؤسسات والتفويض
IdentitySecurity.RequireAdminAsync:34-40 يقبل global أو admin المؤسسة المحددة؛ RequireStudentAsync:64-69 يقبل الطالب نفسه أو admin المؤسسة/global؛ RequireSectionStaffAsync:43-61 يتطلب المؤسسة والدور الحالي ونوع teaching assignment المطابق. TeachingRoleRevocationTests يختبر stale assignment مباشرة وعبر HTTP؛ لم ينفذ هنا.
Global lookup ثم guard ليس IDOR تلقائياً. في LMS/AdvancedLearning/ExamTaking، يجلب المورد أولاً ثم يتحقق section/institution/student ownership قبل إعادته. الفروق 403/404 قد تكشف وجود معرف معروف لكنها لا تثبت تسريب محتواه. غياب Where InstitutionId في استعلام لاحق لا يعد خللاً منفرداً إذا كان الأصل قُيّد وفُوض وFK يضمن العلاقة.
الحد DB: composite FKs تحمي كثيراً من parent/child scopes؛ المستخدم غالباً مرتبط بـUserId فقط (AUD-005). لا global query filter أو PostgreSQL RLS مثبت في المسار المقروء؛ لا تفترض دفاعاً تلقائياً عند كتابة query جديدة.
TA يمكنه إنشاء/تعديل bank وبناء draft وتصحيح draft reports، لكنه لا ينشر exam/grades أو يصحح Published exam report وفق Staff(publish=true). في LMS وgroup projects وattendance وadvising يستخدم RequireSectionStaff، ولذلك TA يملك هذه الأعمال مثل Instructor؛ هذا حد صلاحية فعلي يحتاج configuration مؤسسية، وليس privilege escalation مثبتاً.
الخصوصية: Advising.MyCases:270-288 يصوغ StudentCaseView بلا InternalSummary/Notes، وDetail:137-147 للـstaff فقط. Communication.MyThread:37-49 يفحص أن الطالب/الموظف طرف في thread ومخوّل للشعبة. Documents.Verify:378-393 يعيد حالة ونوعاً وتواريخ بلا هوية/درجات، ويقارن proof بـFixedTimeEquals؛ هذا endpoint public مقصود ومحمي بمحدد معدل في Controller:71-75.
Files: Upload يحد 20MB وأنواعاً محددة ويختبر magic bytes أو UTF-8؛ الاسم Path.GetFileName بلا control characters؛ key عشوائي ضمن institution/section. SaveDraft:152-155 يفرض ملكية الطالب وsection للمرفق. Download:268-271 يرفض ملف طالب آخر لغير staff؛ Controller يرسل attachment وnosniff. لا presigned URLs. bytes checks ليست malware scanner؛ الملفات المشتركة staff uploads ليس لها lifecycle نشر مستقل في النموذج، لذلك لا تقدم ضمان إخفائها حتى درس منشور. PrivateFileStore.GetAsync يحمّل byte[] كاملاً؛ اختبر الذاكرة تحت التحميل.

## 7 — PostgreSQL وEF Core
الكيانات والمappings تشتمل UUIDs من التطبيق، Restrict deletes، decimal precision، alternate tenant keys، unique enrollment/team/student/request keys، checks لحالات attempts/grade/requests/curriculum وغيرها. Enrollment uniqueness لا يضمن السعة وحده؛ الضمان المقصود من قفل section. CHECK لا يمكنه ببساطة فرض عدد الصفوف التابعة. حالات core وعدد من حقول LMS تعتمد على application validation أكثر من CHECK.
الجدول courses استثناء تسمية مهم: أعمدته id/institution_id/code/title/is_active/version. لا يجوز نسخ CLR property names إلى Migration principalColumns دون استعمال اسم العمود الحقيقي. AUD-011 يثبت أن هذا وقع في سلسلة فعلية.
| الترتيب | Migration implementation | اعتماد/ملاحظة بنيوية |
|---|---|---|
| 1 | 20261024000000_InitialCreate.cs | courses بأعمدة lowercase وseed |
| 2 | 20261024132121_Phase1Complete.cs | Identity/core، إضافة institution_id/version؛ seed institution قبل courses FK؛ backfill version للمقررين المزروعين فقط |
| 3 | 20261024141037_Phase2Learning.cs | units/lessons/assignments/files/submissions/completions؛ parents تسبق children |
| 4 | 20261026140000_Phase3QuestionBank.cs | bank/revisions، يستعمل courses id/institution_id الصحيحين |
| 5 | 20261026151000_Phase3ExamBuilder.cs | definitions/draft/published snapshots |
| 6 | 20261026163000_Phase3QuestionPools.cs | quantity/points CHECK وpool uniqueness |
| 7 | 20261026180000_Phase4ExamExecution.cs | attempts → questions → answers وعلاقات مركبة |
| 8 | 20261026193000_Phase5GradingResultsAppeals.cs | report/items/audit/appeals؛ report واحد لكل attempt |
| 9 | 20261027160000_Phase7AcademicOperations.cs | slots/sessions/attendance/calendar/requests/history |
| 10 | 20261027190000_Phase8AdvancedLearning.cs | discussions/teams/rubrics/projects/submissions/audit |
| 11 | 20261027210000_Phase9AcademicAssessment.cs | grade policy/categories/components/manual/results/audit |
| 12 | 20261027230000_Phase10Communications.cs | threads/messages/preferences/notifications/announcements |
| 13 | 20261027234500_Phase11Governance.cs | staff requests وenrollment batches/items |
| 14 | 20261028100000_Phase13Advising.cs | cases/notes/tasks/events |
| 15 | 20261028110000_Phase14AcademicRecords.cs | memberships/prerequisites/transcripts/standing؛ **courses FK غير صحيح** |
| 16 | 20261029110000_Phase15CurriculumPlanning.cs | plans/courses/bands/assignment؛ **courses FK غير صحيح** |
| 17 | 20261029120000_Phase16AdmissionsGraduation.cs | admission/transfer/graduation؛ **courses FK غير صحيح** |
| 18 | 20261030100000_Phase17InstitutionalDocuments.cs | policy/request/issued/events، idempotency وone-document-per-request |

الترقيم Phase6/12 ليس دليلاً على missing migration؛ بعض الوحدات قراءة فقط ولا تتطلب جداول جديدة. تواريخ filenames لا تثبت أنها طُبقت أو أن تاريخ التطوير مطابق لتاريخ الجهاز.
**الحكم البنيوي:** السلسلة ليست STATICALLY CONSISTENT بالكامل بسبب AUD-011. **EXECUTED SUCCESSFULLY ON POSTGRESQL: غير مثبت.** Snapshot/Designer يمكن أن يبدوا متسقين مع model بينما Up خطأ؛ لا يستبدلان تحليل التاريخ. لم أنفذ EF script أو pending-model check. فحص كل Down والـbackfill لكل بيانات سابقة يبقى ناقصاً، فلا ادعاء أن الجدول أعلاه migration certification.
استراتيجية التحقق مرتبة: (1) generate SQL offline ومراجعة FK quoted identifiers؛ (2) migrate قاعدة PostgreSQL معزولة من صفر مع حفظ history/schema؛ (3) upgrade snapshots من core/Phase13 مع بيانات واقعية؛ (4) مقارنة expected model/schema وأسماء القيود، وفحص قيم default/version القديمة؛ (5) اختبارات إدخالات tenant خاطئة وchecks وuniques؛ (6) backup/restore وتحقق من الوثائق والدرجات بعد الاستعادة. لا تعالج migration مطبقة تاريخياً بتعديلها دون تحديد حالة history والبيئات أولاً.

## 8 — التزامن والمعاملات
| العملية | الآلية المقروءة | ما لم تثبته الآلية |
|---|---|---|
| آخر مقعد/خفض السعة/bulk | section FOR UPDATE قبل count، نفس ترتيب section ثم batch | Closed term أو user/role rows لا تقفل بهذه الآلية |
| LMS draft/submit | assignment lock ثم إعادة فحص deadline/publication، unique student/assignment | التنفيذ الحالي يعترضه AUD-009؛ draft لا يحمل expected revision |
| Group draft/submit/grade | project lock، ExpectedVersion للتسليم، Version concurrency | retry wrapper مفقود؛ membership freeze ليس ضمن نفس القفل |
| Question revisions/publish | question/exam row lock وappend revisions؛ immutable published JSON | archive/revise/role changes أثناء انتظار قفل مورد آخر تحتاج اختبارات |
| Start attempt | exam row lock ثم attempts/limits؛ duplicate start resumes | يجمع كل طلاب الامتحان على قفل واحد؛ أثر الحمل غير مقاس |
| Autosave/final submit | attempt row lock وClientSequence لكل سؤال مع equal-value idempotency | لا يمنح at-most-once end-to-end عند فشل commit بلا state verification |
| Grading/publication/appeal | report lock وExpectedVersion؛ TA يعاد فحصه بعد lock في Grade | source Gradebook أو Transcript لا يحدث تلقائياً عند appeal |
| Gradebook publish | section ثم policy lock، 100% weights/complete grades | scores التابعة لا تشارك دائماً policy lock؛ AUD-014 |
| Transcript sync | section ثم policy lock، versioned snapshot | GPA/Graduation يمكنهما قراءة snapshot سابق؛ AUD-015 |
| Admission/transfer/graduation | user/request/membership locks وunique indexes | locks لا تشمل مصادر eligibility المتغيرة كلها |
| Document issue/revoke | request/document locks وVersion وunique request | proof rotation وsnapshot freshness وtimeout retries غير محسومة |
| Messages/dispatch | thread/announcement lock وunique source/message keys | execution strategy غائبة؛ لا worker dispatch تلقائي في DI |

لا تغيير isolation level صريح في المعاملات المقروءة؛ لا أنسب إليها Serializable. أقفال PostgreSQL وحدها لا تكفي حين DbContext يعيد entity tracked من محاولة سابقة؛ AUD-017. مرجع EF الرسمي يوضح ضرورة تغليف user transactions داخل execution strategy والتعامل مع commit ambiguity: [Connection resiliency](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency). هذا مرجع سلوك المكتبة، لا دليل تشغيل المشروع.

## 9 — جرد الميزات الفعلي
| المجال | ما تنفذه الشفرة | ما يمنع اكتماله مؤسسياً | الحالة التشغيلية |
|---|---|---|---|
| Academic core | hierarchy ومقررات وربط program/course وفصول وشعب وتكليف وتسجيل/انسحاب | فصل مغلق لا يمنع enrollment؛ لا policy موحدة لساعات العبء/holds؛ roster legacy مقطوع | مصدر فقط مع عيوب مثبتة |
| LMS | نشر unit/lesson وassignment، completion/progress، مرفق مملوك للطالب، draft/submit/grade | transaction/retry عاطل؛ لا grade moderation/release مستقل للواجب؛ لا إدارة حذف/احتفاظ كاملة | الاختبار غير منفذ |
| Bank/Builder | خمسة أنواع أسئلة، revisions، archive، اختيار يدوي/pools، snapshots عند النشر | لا approval/moderation مستقل أو psychometrics؛ pool يتجمد لكل exam وليس توليد variant مستقل لكل طالب | متتبع مصدرّياً |
| Attempts | نافذة UTC، max attempts، persisted shuffle، resume، sequence autosave، idempotent submit | expired لا يصبح قابلاً للتصحيح؛ لا accommodation/extra time موثق؛ قفل exam مشترك عند Start | غير مختبر PostgreSQL |
| Grading/Results | auto score وmanual essay، report publication وhistory، appeal خلال 14 يوماً وتصحيح | downstream Gradebook/Transcript freshness ليست موحدة؛ لا سياسة appeal configurable | مصدر فقط |
| Workspaces | student overview/courses/progress/calendar، instructor sections/roster/performance، institutional directory، global provisioning | تجميع Backend لا واجهات end-user؛ تقاويم مجزأة بين خدمتين وحدود عددية | مصدر فقط |
| Academic operations | slots، sessions، marks، calendar، request statuses/history | conflict داخل section فقط؛ لا room/instructor/student collision عبر sections؛ Approved request لا ينفذ أثره الأكاديمي | كتابة متأثرة AUD-009 |
| Advanced learning | threads/replies/moderation، teams، one-team-per-section، rubrics، group grading/audit | فريق ثابت بعد أول draft؛ قفل freeze غير ذري؛ group writes متأثرة AUD-009 | مصدر فقط |
| Gradebook | 100% category/component weights، manual/assignment/exam/group sources، normalize best published attempt، publish/correct/audit | >500 طالب ممنوع؛ N×M queries؛ correction snapshots تحتاج تنسيقاً | مصدر فقط |
| Communication | student↔staff threads، idempotent messages، unread/preferences، scheduled announcements وdispatch يدوي | لا worker dispatch ظاهر؛ >500 recipients ممنوع؛ writes متأثرة AUD-009 | مصدر فقط |
| Governance | optional independent review، protected admin roles، batch preview/commit/audit | mandatory policy غير موجودة؛ retries مع tracked context تحتاج اختبار فشل | مصدر فقط |
| Analytics/Advising | institution/section/platform aggregates وCSV محدود، risk flags، case/notes/tasks/report/review/closure | مؤشرات on-demand وليست predictive model؛ staff مرتبط بالشعبة، لا مرشد مؤسسي مستقل؛ تقارير كبيرة محدودة | مصدر فقط |
| Records | prerequisites graph، admit/progress، drop/withdraw، transcript sync، standing | derived snapshots يدوية؛ standing بسياسة ثابتة؛ admission المباشر يبقى متاحاً للإدارة؛ لا برنامج ثانٍ للطالب | مصدر فقط |
| Curriculum/GPA | catalog/revision immutable بعد Publish، Required/Elective credits، grade bands، weighted GPA وBestAttempt، degree audit | no reassignment صريح؛ لا elective groups/substitution/residency rules؛ GPA scheme محصور 0..4 | مصدر فقط |
| Lifecycle | admission application/decision → membership؛ transfer petitions المعتمدة تحت published curriculum؛ graduation review/issue | applicant يحتاج مسبقاً حساب Student؛ equivalency لا تغذي prerequisites؛ credential رقم لا ملف شهادة | مصدر فقط |
| Documents | policy/idempotent request/review/cancel/issue/revoke/public proof verify، immutable stored snapshot | JSON وليس PDF رسمي موقع؛ signing key lifecycle غير مستقل؛ snapshots قديمة ممكنة | مصدر فقط |

مثال end-to-end مكسور رغم وجود endpoints: أول draft واجب يمر authorization وvalidation ثم يصل transaction غير مغلفة؛ لا يصل Submit/Grade الطبيعي. مثال اعتماد متبادل: Phase5 grading test يطلب revision ثانية غير موجودة في Prepare، فلا يصل assertion نشر النتيجة. مثال قدرة جزئية: انتهاء امتحان يحفظ Answer rows لكنه يترك Attempt=Expired وQueue تقبل Submitted فقط.

## 10 — مصفوفة الأدوار والصلاحيات من المصدر
المقصود I/T هو Instructor/TeachingAssistant حالٍ مع teaching assignment من النوع نفسه. IA يحتاج المؤسسة نفسها، SA استثناء عالمي حيث يستخدم RequireAdmin/Staff. صلاحية staff لا تمنح الطالب ownership لدور آخر، وقد يملك الحساب عدة أدوار.
| العملية | Student | Instructor | TA | Institution Admin | Super Admin | المصدر |
|---|---|---|---|---|---|---|
| إنشاء مؤسسة | لا | لا | لا | لا | نعم | Workspaces/PlatformGovernanceService:30-47 |
| hierarchy/courses/terms/sections/enroll | لا تسجيل ذاتي مباشر؛ انسحاب نفسه وفق السياسة | لا إدارة الهيكل | لا | ضمن مؤسسته | نعم | AcademicOperations:18-21,162-190؛ StructureService.Guard |
| تغيير الأدوار | لا | لا | لا | non-admin roles ضمن المؤسسة | جميع الأدوار مع قيود global | Identity/AuthService:107-122 |
| roster | لا | الشعبة المسندة | الشعبة المسندة | المؤسسة | نعم | AcademicOperations:198-203؛ InstructorWorkspace:47-76 |
| LMS publish/content/grade/file staff | محتوى منشور/تسليم نفسه/ملفاته | staff للشعبة | مثل Instructor هنا | المؤسسة | نعم | LearningOperations:48-67,196-207,266-271 |
| bank وexam draft | لا | مقرر/شعبة مسندة | مقرر/شعبة مسندة | المؤسسة | نعم | QuestionBankService:18-31؛ ExamBuilderService:16-32 |
| نشر exam أو grade report | لا | Instructor assignment فقط | لا | المؤسسة | نعم | ExamBuilderService:25-30؛ ExamGradingService:24-30 |
| attempt/autosave/submit | نفسه + enrollment نشط | فقط إن كان أيضاً Student | مثل ذلك | لا صلاحية جلوس لمجرد الإدارة | مثل ذلك | ExamTakingService.Student/Owned |
| manual exam grading | لا | الشعبة؛ Published يتطلب publisher | Draft فقط | المؤسسة | نعم | ExamGradingService:174-213 |
| appeal/results | نتيجته المنشورة/اعتراضه | staff read؛ resolve publisher | قراءة queue/appeals؛ لا resolve | المؤسسة | نعم | ExamGradingService:278-401 |
| teams/rubrics/group grading/discussions moderation | فريقه/مناقشات الشعبة | staff | مثل Instructor | المؤسسة | نعم | AdvancedLearningAccess؛ GroupLearningService |
| timetable/session/attendance | جدوله وmarks الخاصة به | staff | مثل Instructor | المؤسسة | نعم | TeachingOperationsService.Institution؛ StudentAcademicOperations.Student |
| Gradebook policy/publish/correct | نتيجته | publisher | لا publish/config؛ manual draft مسموح | المؤسسة | نعم | GradebookService.Staff:18-37 |
| private messages | طرف thread وviewer للشعبة | طرف thread | طرف thread | طرف thread | ليس مشرف محادثات تلقائياً بلا institution membership | CommunicationService:30-49 |
| advising internal notes | لا؛ يشاهد action tasks المخصصة له فقط | staff للشعبة | مثل Instructor | المؤسسة | نعم | AdvisingService.StaffCase/MyCases |
| transcript/GPA/curriculum student view | نفسه | لا بصفة التدريس وحدها | لا | المؤسسة | نعم | IdentitySecurity.RequireStudent؛ Curriculum.Assigned |
| admission/transfer/graduation decisions | لا؛ لا اعتماد نفسه بحساب متعدد الأدوار | لا | لا | المؤسسة، مع منع الطالب نفسه | نعم مع نفس self rule | LifecycleService وLifecyclePolicy:39-47 |
| document review/issue/revoke | طلبه ووثيقته؛ cancel Submitted | لا | لا | المؤسسة؛ review/issue ليس لنفس applicant | نعم | InstitutionalDocumentService:180-227,273-365 |
| public verification | أي شخص معه reference+proof | نفس الشيء | نفس الشيء | نفس الشيء | نفس الشيء | InstitutionalDocumentsController:71-75 |

تقييم المستخدم: الطالب يستطيع أجزاء من التعلم والدرجات والطلبات بعد provisioning؛ لا lifecycle مستقل منذ applicant. المدرس يملك workflows تدريس جيدة التصميم لكن الكتابة الأساسية تحتاج إصلاحاً. TA غير مفصول عن Instructor في LMS/attendance/advising، مع فصل جيد للنشر الامتحاني. IA يملك إدارة واسعة، لكنه لا يملك أدوات import/policy/مالية كافية. SA يستطيع إنشاء المؤسسات وإدارة المستخدمين وoverview؛ لا control plane تجاري للفوترة/SLA/multi-campus مثبت.

## 11 — API وDTO وValidation
العقود الحديثة في Exams/Curriculum/Documents مكتوبة typed records مع student projections؛ أجزاء Academic/LMS ترجع object وEF entities. Return type object لا يثبت تسريباً وحده، لكنه يمنع مراجعة schema/compatibility موثوقة ويجعل إضافة property entity تغييراً خارجياً؛ AUD-029.
ApiController لا يعوض validation مفقوداً عندما SuppressImplicitRequiredAttributeForNonNullableReferenceTypes=true. Good patterns: QuestionValidation يعالج request=null ويحد النص والخيارات والـkeys؛ ExamAttemptPolicy يثبت JSON shape وsequence ويمنع duplicate options؛ CurriculumPolicy يثبت bands المتزايدة والحدود والدقة. Weak patterns: Identity.Email.Trim بلا guard؛ بعض قوائم الكائنات لا ترفض null elements قبل dereference. ابدأ بجدول حدود input لكل DTO واختبارات HTTP، دون تبديل شامل لـvalidation framework.
GlobalExceptionHandler يحول ApiFault وconcurrency إلى 4xx، و23505/23503 إلى409، وما عداها500. لا يعد كل FK conflict حالة تجارية مفهومة؛ يجب ترجمة constraint errors المعروفة إلى ProblemDetails مستقرة. لم يثبت تسريب token/secret في استجابات الأخطاء التي قرئت.
Created locations عامة أحياناً، مثل /api/academic/sections بدلاً من URL عنصر؛ هذا تحسين تعاقدي أقل أولوية من الأعطال. Pagination متجانس في Workspaces/Analytics وأقل نضجاً في legacy/Lifecycle/LMS؛ وجود حد للحماية لا يبرر قطع قائمة تشغيلية بلا cursor. استعمل facade DTOs تدريجياً مع OpenAPI compatibility tests؛ لا حاجة API rewrite.

## 12 — تقييم ضمان الاختبارات
**النتيجة الحالية تضمن compilation وبعض السياسات والعقود فقط.** Integration fixtures تستخدم postgres:18-alpine وتبدأ MigrateAsync عند CreateClient؛ AUD-011 قد يمنع الوصول إلى طرق الاختبار أصلاً. بعد تصحيح ذلك ما زالت AUD-007/008/010 تعارضات مصدرية مستقلة؛ وبعدها سيكشف تشغيل المسارات AUD-009.
معنى الاختبارات المقروءة:
- Phase1ConcurrencyTests:74-82 يرسل طلبين عبر Task.WhenAll ويثبت created واحداً وconflict واحداً؛ اختبار capacity:121-159 يفرض انتظار قفل ويقرأ DB. تصميم ذو قيمة، لكن fixture isolation مكسور بين الطريقتين.
- AssignmentDeadlineConcurrencyTests:71-98,116-152 يراقب pg_stat_activity ويغير deadline أثناء lock wait؛ أقوى من sleep عشوائي. لكنه نفسه يستخدم explicit transaction ويتأثر بإعداد retry، فلا تسوّقه كاختبار ناجح غير منفذ.
- TeachingRoleRevocationTests:97-115 يختبر الخدمة مباشرة بعد سحب الدور إضافة إلى JWT endpoint؛ لا يسمح للـmiddleware بإخفاء خلل stale-assignment.
- Phase2LearningTests يثبت hidden/published content وoutsider وdraft/submit/retry/immutable after submit؛ assertions مثل Contains("100") في progress أضعف من فحص الحقول الرقمية.
- Phase4ExamExecutionTests:98-142 يختبر start resume وsame-sequence replay/conflict وsubmit retry؛ هذه طلبات متتابعة، لا اختبار concurrent autosave/submit. :146-171 يثبت رفض late work فقط، ولا يثبت grading لمحتوى محفوظ قبل timeout.
- Phase5GradingResultsTests يهدف auto/manual/publish/appeal/audit؛ Prepare يطلب revision غير موجودة. Phase9GradebookTests يختبر manual source وpublication/correction وoutsider، ولا يغطي mix Assignment/Exam/GroupProject أو concurrent source correction.
- Phase16LifecycleTests:88-104 يثبت رفض transfer بلا assigned plan ورفض graduation غير المؤهل؛ لا يثبت full successful graduation أو duplicate credential race. Phase17DocumentsTests:74-115 سيناريو issuance/verification/revocation meaningful، لكنه لا يختبر key rotation أو parallel issue/revoke أو stale transcript.
- ControllerAuthorizationTests وGradeContractSecurityTests وExamAttemptContractTests تفحص metadata/properties بالـreflection؛ مفيدة لمنع إضافة حقول ممنوعة، لكنها لا تثبت resource authorization أو serialized nested payload فعلياً.

الحد الأدنى المفقود: matrix HTTP لمؤسستين وطالبين وكل state؛ healthy startup/migration كامل؛ 13 مسار transaction تحت retry configuration؛ out-of-order parallel autosave؛ expiry finalization؛ source grading أثناء Gradebook publish؛ rollback/retry وcommit ambiguity؛ transfer→prerequisite؛ graduation→transcript correction؛ MinIO owner isolation وorphan cleanup؛ workflow counts فوق500؛ restore+load.
لا blanket mock assurance: fixtures تستعمل PostgreSQL حقيقياً من حيث التصميم، وهذا إيجابي. المشكلة أن التنفيذ غير موجود ومعه عيوب تمنع أن يحقق التصميم ضمانه. الاختبارات الـ217 ليست مقياساً لنسبة domain coverage.

## 13 — العيوب المؤكدة مرتبة
**قواعد العد:** تسعة root findings أدناه؛ لا نعد كل endpoint متأثر AUD-009 عيباً إضافياً، ولا نعد كل migration متأثرة AUD-011 نتيجة منفصلة. جميعها Confirmed defect من المصدر؛ لا ادعاء بإعادة إنتاجها في PostgreSQL. P1 أولوية عالية؛ P2 متوسطة.

### AUD-011 — FK إلى أعمدة غير موجودة في courses عبر ثلاث Migrations
- **Classification/Severity/Confidence:** Confirmed defect — P1 — High.
- **Evidence:** Academix.Api/Data/Migrations/20261024000000_InitialCreate.cs:16-25 ينشئ id lowercase؛ 20261024132121_Phase1Complete.cs:25-41 يضيف institution_id ويعرف unique عليه. CourseConfiguration.cs:14-23 يؤكد mapping. لكن 20261028110000_Phase14AcademicRecords.cs:52-55 و20261029110000_Phase15CurriculumPlanning.cs:52-53 و20261029120000_Phase16AdmissionsGraduation.cs:155-156 تحدد principal columns باسم Id/InstitutionId. بحث RenameColumn/RenameTable لم يجد ترحيلاً يغيرهما.
- **Observed/Expected:** Up يشير إلى أعمدة CLR-case غير مطابقة لأسماء DB؛ المطلوب أن تشير كل ForeignKeyOperation إلى الأعمدة الموجودة بأسمائها الفعلية.
- **Trigger/Root cause:** migrate من صفر أو من Phase13 إلى Phase14؛ نسخ أسماء property إلى hand-authored migration بدلاً من أسماء الأعمدة.
- **Impact:** bootstrap/migration chain تتوقف قبل records/curriculum/lifecycle/documents؛ ApiFixture لا يصل لاختبارات workflows. لا ندعي أننا شاهدنا خطأ SQL منفذاً.
- **Correction:** صحح خطة migration بحسب history الحقيقية لكل بيئة. إذا لم تُطبق هذه الملفات قط، صحح principalColumns وراجع generated SQL؛ إن توجد بيئات مطبقة بschema مختلفة، صمم مسار توافق منفصلاً دون محو history أو البيانات.
- **Tests/Acceptance:** generate/review SQL ثم apply كل18 على DB جديدة، upgrade من snapshot Phase13 يحوي بيانات، تحقق pg_constraint وschema/model diff. نجاح جميع المسارات مع حفظ البيانات، لا مجرد Build.

### AUD-009 — 13 مسار معاملة غير متوافق مع retry strategy المفعلة
- **Classification/Severity/Confidence:** Confirmed defect — P1 — High في تركيب المصدر؛ HTTP غير منفذ.
- **Evidence:** Academix.Api/Extensions/ServiceCollectionExtensions.cs:37-42 يضبط EnableRetryOnFailure(3). LearningOperations.cs:145-176,178-194 يبدأ BeginTransaction ثم EF query بلا CreateExecutionStrategy. النمط نفسه في GroupLearningService.cs:210-240,243-260,284-331؛ CommunicationService.cs:122-149,152-159,293-353؛ TeachingOperationsService.cs:31-68,95-119,140-180؛ StudentAcademicOperationsService.cs:104-121؛ RegistrarOperationsService.cs:65-86.
- **Observed/Expected:** user-initiated transaction تدخل EF retrying execution strategy خارج delegate جامعة؛ EF يرفضها. المطلوب replay للمعاملة كوحدة. [توثيق EF الرسمي](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency) يشرح هذا القيد.
- **Trigger/Root cause:** طلب draft صحيح أو رسالة أو slot/session/attendance/decision بعد توافر DB؛ يبدأ transaction ثم أول EF operation داخلها يرمي InvalidOperationException. LockAssignment raw ADO لا يصلح المشكلة: استعلام EF الذي يليه يقع داخل المعاملة نفسها.
- **Impact:** قطع مسارات التعلم والمشاريع والمراسلات والعمليات الأكاديمية بـ500 بدلاً من نتيجة business؛ لا تتطلب علة فشل شبكة transient لكي تظهر.
- **Correction:** تشغيل كامل المعاملة عبر execution strategy مع context جديد لكل retry، وإعادة تحميل الحالة/التفويض الحساسة ضمنها؛ لا تعطل retries عالمياً لإخفائها. صمم idempotency/verifySucceeded عند حدود commit.
- **Tests/Acceptance:** smoke حقيقي لكل13 عملية بالإعداد الفعلي، deadline-lock regressions، forced transient rollback/retry. لا user-initiated transaction exception، ولا duplicate أو false-success بعد retry.

### AUD-002 — التسجيل مستمر بعد إغلاق AcademicTerm
- **Classification/Severity/Confidence:** Confirmed defect — P1 — High.
- **Evidence:** Academix.Api/Services/Academic/StructureService.cs:144-154 يسمح Active→Closed بلا إغلاق شعب؛ AcademicOperations.cs:162-190 يفحص section.Open وstudent/prereq/capacity/publication فقط. GovernanceService.cs:228-264,278-329 يستعمل نفس المنطق. فحص term.Active في UpdateSection:123-127 عند الفتح وحده.
- **Observed/Expected:** شعبة Open باقية داخل term.Closed تستقبل التسجيل؛ يجب أن تشارك paths تعريف حالة التسجيل المسموحة.
- **Trigger/Root cause:** افتح شعبة ثم أغلق فصلها ثم enroll أو commit batch؛ غياب term state من commit invariant.
- **Impact:** تسجيل جديد في دورة مغلقة ومخالفة حدود بيانات registrar.
- **Correction:** سياسة واحدة للفردي والجماعي، مع synchronization متوافق مع إغلاق الفصل؛ لا check قبلي منفصل فقط.
- **Tests/Acceptance:** Closed قبل التسجيل وأثناء lock wait، الفردي والجماعي/reactivation؛ لا row commit بعد نقطة الإغلاق المحددة، مع بقاء التسجيلات السابقة.

### AUD-012 — سعة الشعب تسمح بـ10000 لكن Gradebook يرفض ما فوق500
- **Classification/Severity/Confidence:** Confirmed defect — P2 — High.
- **Evidence:** Academix.Api/Services/Academic/AcademicOperations.cs:92,118 يقبل Capacity حتى10000؛ GradebookService.cs:274-278 يعد Active enrollments ويرمي Conflict إذا >500؛ Publish:358 يستدعي Compute.
- **Observed/Expected:** شعبة501 طلاب صحيحة حسب academic API يستحيل نشر نتائجها مهما اكتملت درجاتها. المطلوب توافق حدود admission/assessment أو نشر scalable.
- **Trigger/Root cause:** سجّل501 طالباً وجهز policy ودرجات كاملة؛ Compute يرفض بحماية عددية ثابتة بدلاً من معالجة chunks.
- **Impact:** مؤسسات ذات محاضرات كبيرة تصل إلى dead end أكاديمي؛ enrollment API لا يحذر مبكراً.
- **Correction:** اجعل النشر batched/atomic result set مع policy/roster version أو حد مدعوم موحد ومعلن؛ لا تقسّم الشعب الفعلية قسراً دون سياسة.
- **Tests/Acceptance:** 500/501/1000؛ نتائج كاملة لكل طالب مرة واحدة دون partial publication، مع budget أداء متفق عليه.

### AUD-003 — قطع Roster القديم دون paging
- **Classification/Severity/Confidence:** Confirmed defect — P2 — High.
- **Evidence:** Academix.Api/Services/Academic/AcademicOperations.cs:198-203 يستعمل Take(100) لكل الحالات؛ Controllers/EnrollmentsController.cs:17-19 لا يقبل page. StudentHistory:205-211 له حد مماثل.
- **Observed/Expected:** طلب roster لـ101 تسجيل يعيد100 دون total/next؛ المطلوب استكمال موثق وترتيب فريد.
- **Trigger/Root cause:** تجاوز100، أو تاريخ انسحابات يزاحم Active؛ حد حماية تحول إلى فقد صامت في contract.
- **Impact:** العميل الذي يعتمد legacy route يملك قائمة ناقصة. **تصحيح مهم للتقرير القديم:** InstructorWorkspaceService.cs:47-76 يوفر roster مرقماً للـActive بالفعل؛ لذلك ليس صحيحاً أن كل المنصة تفتقر لأي طريقة استرجاع طلاب إضافيين. لا يعالج ذلك عقد legacy أو history.
- **Correction:** توحيد DTO paging/status filter أو deprecate موثق مع مسار بديل؛ لا إضافة خدمة roster جديدة مكررة.
- **Tests/Acceptance:** صفحات101/250 وحالات مختلطة وtimestamps متساوية؛ استرجاع كل المطلوب دون فقد أو تكرار.

### AUD-004 — Identity null input يصل لاستثناء خادم
- **Classification/Severity/Confidence:** Confirmed defect — P2 — High في trace، لا HTTP replay.
- **Evidence:** Academix.Api/Extensions/ServiceCollectionExtensions.cs:130-131 يعطل implicit Required؛ DTOs/Identity/AuthContracts.cs:3-4,10 بلا Required؛ AuthService.cs:16,97 ينفذ Email.Trim؛ LogoutAsync:76 → IdentitySecurity.TokenHash:72 → Encoding.GetBytes؛ GlobalExceptionHandler.cs:11-20 يصنف الاستثناء500.
- **Observed/Expected:** email:null أو RefreshToken:null المناسب يصل dereference/argument exception؛ المطلوب 4xx validation مستقر بلا state change.
- **Trigger/Root cause:** login JSON ناقص أو email=null؛ logout authenticated مع refreshToken=null. nullable annotations ليست runtime validation.
- **Impact:** فشل تعاقدي وLogError للمدخلات غير الصحيحة؛ لا تجاوز مصادقة مثبت.
- **Correction:** validation صريح للrequired/length والـnested collection elements مع service guards، دون كشف اختلاف معلومات الحساب.
- **Tests/Acceptance:** null/missing/empty/oversized لكل auth payload؛ 4xx دون500 أو token/session جديد، والحالات السليمة محفوظة.

### AUD-010 — Prepare في اختبار تصحيح الامتحان يطلب revision غير موجودة
- **Classification/Severity/Confidence:** Confirmed defect في اختبار — P2 — High.
- **Evidence:** Academix.Tests/Integration/Phase5GradingResultsTests.cs:75-86 ينشئ q1 وq2 كل مرة واحدة؛ :93-96 يختار q2.RevisionNumber=2. QuestionBankService.cs:65-69 ينشئ RevisionNumber=1؛ ExamBuilderService.cs:159-166 يرفض selection غير الموجود.
- **Observed/Expected:** replace يرجع BadRequest بدلاً من OK المتوقع في test:97؛ يجب تجهيز revision2 أو اختيار revision1.
- **Trigger/Root cause:** تشغيل test بعد حل startup migrations؛ الخلط بين ترتيب السؤال ورقم مراجعته.
- **Impact:** assertions النشر والاعتراض والتصحيح لا تبلغ التنفيذ، رغم مظهر الاختبار المتكامل.
- **Correction:** صحح بيانات التهيئة فقط، وأضف test مستقل يرفض revision مفقودة؛ لا تضعف validation الخدمة.
- **Tests/Acceptance:** وصول الاختبار لكل assertions حتى AppealCorrection، ورفض revision غير موجودة؛ تنفيذ PostgreSQL فعلي.

### AUD-007 — prerequisite test يناقض منع تعديل الشعبة المفتوحة
- **Classification/Severity/Confidence:** Confirmed defect في اختبار — P2 — High.
- **Evidence:** Academix.Tests/Integration/Phase14AcademicRecordsTests.cs:65-66 ينشئ targetSection Open ثم :86-88 يتوقع OK لإضافة prerequisite. RecordsService.cs:133-138 يرفض إذا وجد section.Open لنفس course.
- **Observed/Expected:** HTTP409 متوقع من المصدر قبل eligibility assertions؛ يجب تجهيز prerequisite قبل فتح الشعبة.
- **Trigger/Root cause:** تشغيل اختبار Phase14 على fixture سليمة؛ ترتيب التهيئة يناقض invariant.
- **Impact:** لم يثبت الاختبار سلسلة prerequisites→enrollment→transcript/standing حتى لو كان compilable.
- **Correction:** أضف المتطلب قبل الفتح، واحتفظ بسلبية رفض التعديل أثناء Open.
- **Tests/Acceptance:** نجاح السلسلة والوصول لassertions اللاحقة؛ 409 للتعديل عند الفتح دون إضعاف القاعدة.

### AUD-008 — تداخل term data بين اختباري السعة
- **Classification/Severity/Confidence:** Confirmed defect في اختبار — P2 — High.
- **Evidence:** Academix.Tests/Integration/Phase1ConcurrencyTests.cs:16 يستعمل IClassFixture<ApiFixture>؛ :57-63 و:92-99 يستعملان InitialInstitution ونفس الفترة2027-01-01..2027-06-30 ويتركان term Active؛ ApiFixture.cs:20-45 قاعدة واحدة لعمرclass بلا reset بين tests. StructureService.cs:134 يرفض overlap.
- **Observed/Expected:** ثاني test يفشل create term قبل الوصول إلى سباق السعة؛ يجب استقلال بيانات السيناريوهين.
- **Trigger/Root cause:** تشغيل class كاملاً بأي ترتيب؛ اختلاف code لا يمنع overlap.
- **Impact:** ضمان التزامن الظاهر يتعطل بسبب fixture interference؛ ترتيب test ليس علاجاً.
- **Correction:** مؤسسة مستقلة لكل scenario أو isolation/reset معزول؛ لا تنظيف قاعدة حقيقية.
- **Tests/Acceptance:** تشغيل الاختبارين معاً ومنفردين وإعادة suite بلا order dependency؛ الوصول لنقاط انتظار القفل.

## 14 — المخاطر المفتوحة والتحقق المطلوب
النتائج هنا ليست exploits مؤكدة. **الستة المصنفة P1 هي AUD-006 وAUD-013..017**. بقية النتائج P2، ولا تدخل رقم المخاطر الجسيمة. أي interleaving أدناه اختبار مطلوب لا ادعاء بتجربة ناجحة.

### AUD-006 — لا إثبات Integration/Migration/PostgreSQL صالح
- **Classification/Severity/Confidence:** Runtime verification required — P1 — High لغياب الدليل.
- **Evidence/Observed:** Academix.Tests/Integration/ApiFixture.cs:10-14,20-38 يحتاج container ويشغل MigrateAsync؛ Extensions/WebApplicationExtensions.cs:27-35 يطبق migrations عندما enabled. هذه الاختبارات مستبعدة صراحة من أمر التنفيذ. AUD-011 علة مصدرية محددة فوق فجوة البنية.
- **Expected/Trigger/Root cause:** جاهزية pilot تتطلب تنفيذ schema/workflows/locks على PostgreSQL؛ إطلاق اعتماداً على Build أو217 يتجاوز هذا الشرط بسبب عدم تشغيل البنية.
- **Impact/Correction:** صحة SQL وFK/retries والترجمة غير مبرهنة؛ شغل suite في CI/بيئة معزولة متاحة بعد إصلاح root blockers، مع datasets متعارضة وحفظ النتائج.
- **Tests/Acceptance:** clean migrate + upgrade + integration suite كاملة مع تفسير كل failure، لا tests skipped باعتبارها ناجحة؛ restore/load مستقلان. **الثقة في صحة التطبيق نفسه لا تستنتج من غياب الاختبار.**

### AUD-013 — تغييرات Identity والأثر التدقيقي ليست وحدة ذرية
- **Classification/Severity/Confidence:** High-confidence risk — P1 — Medium في سيناريو الفشل، High في شكل الكود.
- **Evidence:** Academix.Api/Services/Identity/AuthService.cs:97-103 ينفذ Create ثم AddToRole ثم Audit؛ :118-122 يغير الدور ثم UpdateSecurityStampAsync دون فحص نتيجته ثم Save audit؛ :134-141 تحديث التفعيل ثم stamp ثم refresh revoke ثم audit. لا transaction جامعة. بالمقابل GovernanceService.cs:98-148 يفحص IdentityResult داخل transaction.
- **Observed/Expected:** عدة عمليات حفظ مستقلة قد تترك حساباً بلا دور أو تغييراً بلا audit؛ المطلوب all-or-nothing أو تعويض صريح ودليل failure. فشل stamp ليس برهان أن كل الخدمات تمنح صلاحية مسحوبة، لأنها تعيد قراءة الأدوار.
- **Trigger/Root cause:** role save ينجح ثم audit save أو stamp update يفشل؛ caller يستلم خطأ مع أثر جزئي. فصل stores/SaveChanges بلا boundary جامعة.
- **Impact/Correction:** حسابات orphan وسجل أمن غير كامل، وتعارض UX/session semantics؛ اجمع mutation/revocation/audit ضمن استراتيجية معاملة ملائمة، افحص كل IdentityResult وحدد تعويض الإنشاء.
- **Tests/Acceptance:** fault injection لكل خطوة، rollback أو حالة معلنة قابلة للإصلاح؛ لا نجاح بلا audit أو account جزئي صامت؛ اختبارات stale JWT/refresh بعد قرار الدور.

### AUD-014 — Gradebook snapshot لا يثبت نسخة متجانسة من مصادر الدرجات
- **Classification/Severity/Confidence:** High-confidence risk — P1 — Medium.
- **Evidence:** Academix.Api/Services/Assessment/GradebookService.cs:343-379 يقفل section/policy ثم Compute:280-300 ينفذ Earned استعلاماً لكل source/student. LearningOperations.Grade:196-207 لا يأخذ policy lock؛ ExamGradingService.Grade:187-212 يقفل report فقط.
- **Observed/Expected:** policy/roster ثابتان نسبياً لكن source scores يمكنها التغير بين استعلامين؛ المطلوب snapshot/version-set واضح عند النشر.
- **Trigger/Root cause:** publish يقرأ درجة طالب A، ثم مصحح يغيّر report/assignment، ثم publish يقرأ طالب B أو component آخر؛ row locks على موارد مختلفة وتعدد queries تحت isolation غير مرفوع.
- **Impact/Correction:** نتائج منشورة من لحظات/نسخ مختلفة؛ اختر consistent snapshot isolation مع retry واختبار أو source version manifest/freeze protocol، ولا تقفل كل الجداول بلا تصميم. لاحظ أن snapshot transaction لا يحل وحده سياسة correction بعد النشر.
- **Tests/Acceptance:** قفل/حواجز محكومة أثناء مصدرين متغيرين؛ نشر نسخة كاملة موثقة أو409 وإعادة حساب، بلا مزج غير قابل للتتبع.

### AUD-015 — أهلية التخرج والوثائق يمكن أن تقرأ Transcript مشتقاً قديماً
- **Classification/Severity/Confidence:** High-confidence risk — P1 — Medium.
- **Evidence:** Academix.Api/Services/Records/RecordsService.cs:245-292 يزامن Transcript يدوياً؛ GradebookService.Correct:400-427 لا يستدعي sync. CurriculumService.StudentData:233-237 يقرأ Transcript؛ LifecycleService.IssueGraduation:333-356 يقفل membership/application ويعيد Audit لكنه لا يقفل grade policy أو يثبت freshness manifest؛ InstitutionalDocumentService.Snapshot:248-268 يقرأ transcript/standing المحفوظين.
- **Observed/Expected:** إعادة Audit قد تعيد نفس snapshot السابق رغم تصحيح المصدر؛ المطلوب تعريف authoritative version وألا تُصدر شهادة باعتبار اشتقاق قديم حديثاً.
- **Trigger/Root cause:** sync ناجح ثم grade correction يخفض طالباً تحت حد التخرج، ثم issue قبل إعادة sync؛ أو sync/correction متزامنان مع Audit. اعتماد مشتقات يدوية بلا dirty/freshness/version-link.
- **Impact/Correction:** credential أو document يصف واقعاً أكاديمياً تجاوزته corrections؛ أضف source versions/dirty-state وreconciliation gate، وامنع issuance عند عدم تزامن البيانات. لا تعِد كتابة التاريخ الصادر؛ أضف supersession/revocation policy.
- **Tests/Acceptance:** correction بين review/issue، stale standing، transfer source change؛ رفض الإصدار أو snapshot حديث متسق، ثم مسار إعادة إصدار مدقق. قرار أثر تصحيح ما بعد التخرج يحتاج سياسة مؤسسة.

### AUD-016 — فحص prerequisites وتغيير حالة الشعبة لا يتشاركان بروتوكول قفل
- **Classification/Severity/Confidence:** High-confidence risk — P1 — Medium.
- **Evidence:** Academix.Api/Services/Records/RecordsService.cs:129-149 يقفل institution ثم يبحث عن Open sections قبل إضافة prerequisite؛ AcademicOperations.UpdateSection:110-130 يقفل section ولا يأخذ institution lock. Enroll:176 يستدعي eligibility بعد قفل section فقط.
- **Observed/Expected:** guard “أغلق الشعب قبل تعديل المتطلبات” ليس مستقراً بين موردين؛ المطلوب serialization/version protocol واحد يحمي سياسة المتطلبات المستعملة عند قبول enrollment.
- **Trigger/Root cause:** mutation يقفل institution ويرى Draft؛ open يفتح section ويcommit؛ enroll يرى prerequisites القديمة؛ mutation يكمل إضافة prerequisite بعد فتح وقبول الطالب. لا exclusion/trigger يحمي invariant.
- **Impact/Correction:** تسجيلات لا تحقق policy التي أصبحت سارية في الشعبة نفسها؛ ضع policy revision frozen per section أو ترتيب قفل مشترك ومحدد. راجع أيضاً term create/activate overlap؛ Version لكل صف لا يحمي شرطاً على صفين.
- **Tests/Acceptance:** barrier بين check وinsert وبين open/enroll؛ لا قبول خارج النسخة المعتمدة، ولا deadlocks غير معالجة؛ عكس ترتيب الطلبات أيضاً.

### AUD-017 — retries تعيد استعمال tracked DbContext بعد فشل المعاملة
- **Classification/Severity/Confidence:** High-confidence risk — P1 — Medium، يحتاج provider fault injection.
- **Evidence:** Academix.Api/Services/Governance/GovernanceService.cs:282-329 يعيد delegate على نفس db؛ :293-303 يجلب batch tracking ويعيد النجاح إذا Status=Committed؛ :326-329 يغير الحالة ثم SaveChanges ثم Commit. النمط scoped db المغلق عليه يتكرر في Auth/Academic/Exam/Curriculum/Lifecycle.
- **Observed/Expected:** بعد SaveChanges يقبل EF states قبل التأكد من commit؛ retry لا يعني تنظيف tracker. المطلوب context fresh أو explicit state verification وإدارة AcceptAllChanges.
- **Trigger/Root cause:** SaveChanges ينجح ثم transient failure يسبب rollback عند commit؛ batch في tracker ما زال Committed. retry يقرأ known AsNoTracking=Draft، لكن tracking FromSql قد يعيد instance Committed، فيخرج idempotent-success بلا إدخال حقيقي. يوجد أيضاً commit-unknown converse يحتاج تحقق.
- **Impact/Correction:** false success/duplicate أو stale entity decision؛ استخدم DbContext جديداً داخل replay delegate وstable operation ID وverifySucceeded، ولا تفترض FOR UPDATE يعيد ملء tracked properties.
- **Tests/Acceptance:** خطأ محدد بعد save وقبل commit، وخطأ بعد server commit مع response loss؛ batch/result في DB يتطابق مع response دائماً وبلا duplicate. المرجع الرسمي لسلوك commit/retry مذكور في القسم8.

### AUD-005 — FK المستخدم لا يفرض تطابق المؤسسة
- **Classification/Severity/Confidence:** High-confidence risk — P2 — High في القيود، Medium في أثره عبر الكاتب الحالي.
- **Evidence:** Academix.Api/Data/Configurations/AcademicConfiguration.cs:73-88 يحمي (SectionId,InstitutionId) لكنه يربط UserId/StudentId مفرداً؛ Snapshot:2268-2297 وPhase1Complete:361-412 يطابقان ذلك. خدمات Enroll/AssignStaff/EvaluateBatch تفحص المؤسسة فعلاً.
- **Observed/Expected/Trigger:** import/direct SQL أو كاتب جديد يستطيع إدخال cross-institution user relation رغم سلامة أصل الشعبة؛ المطلوب DB defense أو بديل موثق. **لم يثبت endpoint يستغل هذا الآن.**
- **Root/Impact/Correction:** tenant discriminator غير داخل user FK؛ خطر فساد علاقات. قيّم (Id,InstitutionId) alternate key وعلاقات مركبة مع معالجة global users/nulls، وافحص المخالفات قبل migration.
- **Tests/Acceptance:** PostgreSQL يرفض cross-tenant enrollment/teaching assignment ويقبل الصحيح وحساب global غير المنتسب؛ لا فقد بيانات أثناء الترحيل.

### AUD-018 — دلالة Logout بين JWT وRefresh family غير محددة بما يكفي
- **Classification/Severity/Confidence:** Runtime verification required — P2 — Medium.
- **Evidence:** Academix.Api/Services/Identity/AuthService.cs:73-79 يبطل token hash المرسل فقط؛ RefreshAsync:44-68 يقفل القديم وينشئ descendant؛ IdentitySecurity.IssueAccessToken:75-91 لا يضع session/family claim؛ ValidateSessionAsync لا يقرأ RefreshSessions.
- **Observed/Expected:** access يبقى حتى10 دقائق دون stamp change؛ لا نفترض أن إبطال access فوراً شرط قائم. المطلوب عقد session/logout موثق ومختبر.
- **Trigger/Root cause:** refresh للقديم يسبق logout عليه، فيبقى descendant؛ session represented بـrefresh row دون family revoke فيlogout.
- **Impact/Correction:** المستخدم قد يظن أنه أنهى الجلسة كلها بينما credentials مشتقة ما زالت فعالة؛ حدد logout-current-family مقابل all-sessions وaccess policy ثم نفذه، دون ادعاء ثغرة مخالفة لعقد غير موجود.
- **Tests/Acceptance:** جميع ترتيبات parallel logout/refresh، replay/descendant واستخدام access بعد logout حسب العقد المختار؛ لا resurrection غير موثقة.

### AUD-019 — تدوير JWT key يغير Proof الوثائق القديمة
- **Classification/Severity/Confidence:** High-confidence risk — P2 — High.
- **Evidence:** Academix.Api/Services/Institutional/InstitutionalDocumentService.cs:28-38 يولد Proof من Jwt:SigningKey وdocumentId في كل قراءة؛ :387-389 يعيد الحساب بالمفتاح الحالي. IssuedAcademicDocument entity لا يحتوي KeyId أو proof version محفوظاً.
- **Observed/Expected/Trigger:** نسخة proof صدرت بمفتاح K1 لا تساوي Proof(K2) بعد rotation عادي؛ يجب استمرار تحقق الوثائق طوال صلاحيتها وفق سياسة key lifecycle مستقلة.
- **Root/Impact/Correction:** coupling لمفتاح auth قصير العمر بلا key ring؛ الروابط القديمة تفقد صلاحيتها رغم بقاء الوثيقة Valid في DB. استخدم document key ring versioned أو محفوظ hash/token عشوائي مع سياسة rotation، ولا تخلط إثبات المرجع بتوقيع محتوى PDF.
- **Tests/Acceptance:** issue K1 ثم rotate K2 ثم verify proof الأصلي، revoke/expiry والنسخ الجديدة؛ لا إعادة توليد أوراق قديمة إجبارياً بسبب auth rotation.

### AUD-020 — تجميد أعضاء الفريق بعد أول Draft يعتمد check منفصلاً
- **Classification/Severity/Confidence:** High-confidence risk — P2 — Medium؛ خطر كامن بعد إصلاح AUD-009.
- **Evidence:** Academix.Api/Services/AdvancedLearning/GroupLearningService.cs:48-79 يفحص GroupProjectSubmissions.Any ثم يضيف/يحذف member دون project/team lock مشترك؛ SaveDraft:218-240 يقفل project فقط، ولا يعيد membership check بعد القفل.
- **Observed/Expected/Trigger:** Add/Remove يمر check، Draft يcommit ثم membership change يcommit؛ المطلوب ثبات فريق كل submission من نقطة التجميد.
- **Root/Impact/Correction:** split check/write بلا aggregate lock؛ طالب يمكنه فقد/اكتساب حق تسليم مشترك بعد الحد المقصود. اقفل team أولاً في membership mutation وأول draft أو خزّن roster snapshot للمشروع.
- **Tests/Acceptance:** أول draft بالتزامن مع Add/Remove؛ membership ثابتة وفق policy، وgradebook team→student لا ينقل رصيداً لعضو غير مستحق.

## 15 — الفجوات الوظيفية والقدرات الجزئية
القدرات السبع التالية **موجودة جزئياً** وليست سبعة modules معدومة. لا تحسب AUD-001 أو فرص التوسع كعيوب مؤكدة. لكل قدرة سيناريو قبول يمكن تحويله مباشرة إلى backlog.

### AUD-021 — انتهاء الامتحان لا يكمل دورة التقييم
**Missing capability، P1، High.** Evidence: Academix.Api/Services/Examinations/ExamTakingService.cs:47-54,291-316 ينقل الحالة Expired ويرفض submit؛ ExamGradingService.cs:84-85,115 يقبل Submitted فقط. Observed: جواب محفوظ قبل deadline لا يصل للتصحيح عند timeout. Expected: سياسة مؤسسية تحدد auto-finalize أو non-submission مع احتساب واضح. Trigger: انقطاع العميل بعد autosave حتى انتهاء الوقت. Root: terminal states غير مرتبطة بمسار تقييم. Impact: gap امتحاني حرج، لا تصفه كتسريب أو كخرق للقاعدة الحالية التي تمنع التسليم المتأخر. Correction: finalized snapshot بسبب timeout أو grading eligibility مضبوطة، بلا قبول جواب متأخر. Tests/Acceptance: نفس saved answers قبلDeadline، رفض ما بعده، نتيجة قابلة للتقييم مرة واحدة وaudit للسبب.

### AUD-022 — معادلات التحويل لا تغذي prerequisite eligibility
**Missing capability، P2، High.** Evidence: Academix.Api/Services/Curriculum/CurriculumService.cs:245-255 يحسب Approved transfer courses نحو degree credits؛ RecordsService.cs:176-199 يبحث Gradebook منشوراً فقط. Observed/Trigger: طالب معادَل له A يستوفي درجة التخرج لكنه لا يحقق prerequisite A لدراسة B. Expected: policy صريحة لما إذا كانت equivalency تحقق minimum percentage أو exemption. Root: نموذجان للأهلية بلا resolver مشترك. Impact: تعطيل registrar أو workaround fabricated grades. Correction: reusable completion/equivalency evidence مع source/reviewer وoverride reason؛ لا تصنع Gradebook درجة وهمية. Tests/Acceptance: approved/rejected/insufficient credit/threshold policies، الفردي والجماعي وGPA لا يتلوث.

### AUD-023 — Admissions يبدأ بعد إنشاء Student مسبقاً
**Missing capability، P2، High.** Evidence: Academix.Api/Services/Lifecycle/LifecycleService.cs:27-31,44-48 يتطلب Student active/institution؛ AuthController لا يوفر applicant registration؛ CreateUser بيد الإدارة. Observed: application workflow لمستخدم طالب موجود، وليس onboarding مرشح جديد. Expected: intake آمن أو مسار provisioning مؤسسي موثق. Trigger: applicant بلا حساب يحاول التقديم. Root: دمج Applicant وStudent identity lifecycle. Impact: تشغيل يدوي واسع قبل القبول. Correction: applicant profile/invite/email verification ثم قرار admission يربط identity/student بدون duplicate. Tests/Acceptance: pending applicant لا academic access، admission واحد وربط مدقق وآمن. لا تفترض self-signup المفتوح مناسباً لكل مؤسسة.

### AUD-024 — اعتماد الطلب الأكاديمي لا ينفذ أثره
**Missing capability، P2، High.** Evidence: Academix.Api/Services/AcademicOperations/RegistrarOperationsService.cs:65-86 يعدل status/reason/history فقط؛ أنواع الطلب في OperationEntities.cs:63 تشمل AttendanceExcuse/EnrollmentException دون SessionId/action payload قوي. Observed: Approved ليست Excused mark أو enrollment override. Expected: إما case tracking مصرح به أو عملية fulfill مستقلة تربط القرار بأثره. Trigger: قبول عذر غياب ثم استمرار mark Absent. Root: قرار مجرد بلا action handler/version link. Impact: موافقات غير قابلة للتنفيذ/القياس آلياً. Correction: typed request details وapprove→fulfill audited/idempotent. Tests/Acceptance: الطلب يغير مورده المقصود مرة واحدة فقط أو يبقى AwaitingFulfillment مع سبب، ومنع cross-tenant/self-approval حسب السياسة.

### AUD-025 — الإعلان المجدول يحتاج Dispatch يدوياً
**Missing capability، P2، High في عدم وجود worker داخل API المقروء.** Evidence: Academix.Api/Services/Communications/CommunicationService.cs:225-238 يخزن ScheduledForUtc؛ :293-353 يرسل من endpoint؛ ServiceCollectionExtensions لا يسجل HostedService/worker، والبحث الموجه لم يجد BackgroundService/Quartz/Hangfire. Observed: الإعلان يمكن أن يظهر عند موعده في GET :275-290 دون إنشاء notifications تلقائياً. Expected: فصل visibility عن delivery مع ضمان تشغيل جدولة. Trigger: لا أحد يستدعي dispatch عند الموعد. Root: scheduler service غير موصول. Impact: “scheduled” لا يعني notification delivery. Correction: durable dispatcher/outbox له lease/retry/backoff، مع إمكان external scheduler موثق؛ لا تنفذ automation في هذا التدقيق. Tests/Acceptance: restart قبل/بعد الموعد، duplicate dispatch وopt-out ولا تكرار notifications.

### AUD-026 — الوثيقة الرسمية Snapshot JSON وليست Artifact نهائياً
**Missing capability، P2، High.** Evidence: Academix.Api/Services/Institutional/InstitutionalDocumentService.cs:229-270 يبني JsonSerializer snapshot؛ :36-38 يعيد SnapshotJson/proof؛ DTOs/Institutional/InstitutionalContracts.cs يعرض النص؛ LifecycleService:349-350 يولد CredentialNumber فقط. Observed: approval/issue/verify/revoke حقيقية في المصدر، لكن لا PDF/template/signature/content digest محفوظ. Expected: document usable بمواصفات المؤسسة وهوية مقروءة وتصميم معتمد ومرجع تحقق. Trigger: registrar يحتاج تسليم شهادة رسمية قابلة للطباعة. Root: data issuance فصل غير مكتمل عن rendering. Impact: Backend API لا يساوي service وثائق تجارية. Correction: versioned templates وimmutable rendered artifact/hash مع key lifecycle وعرض verification لا يكشف درجات. Tests/Acceptance: reproducible snapshot/template/hash، tenant branding، revoked/superseded، rendering failure بلا issued record ناقص.

### AUD-027 — Curriculum assignment لا يدعم الانتقال المدقق
**Missing capability، P2، High.** Evidence: Academix.Api/Services/Curriculum/CurriculumService.cs:186-188 يرفض أي assignment سابق ويصرح أن reassignment غير مدعوم؛ RecordsService.Admit يفرض membership واحدة بالمؤسسة. Observed: catalog versioning/create/publish موجود، لكن انتقال الطالب لخطة أخرى أو برنامج لاحق لا workflow له. Expected: انتقال مع equivalency/mapping وeffective date وتاريخ محفوظ. Trigger: تغيير خطة/تحويل برنامج/درجة ثانية بعد completion. Root: unique current assignment ومفهوم membership واحد بلا lifecycle history. Impact: تعديل DB يدوي أو توقف المسار. Correction: current pointer + history ومراجعة فروق credits/GPA، مع منع overwrite التاريخ. Tests/Acceptance: old plan history وdegree audits قابلة للإعادة قبل/بعد transition، رفض مؤسسات أخرى وعدم double credit.

### AUD-001 — Mandatory two-person policy غير موجودة
**Missing capability، P2، High.** Evidence: Academix.Api/Entities/Governance/GovernanceEntities.cs:3 يعلن optional workflow؛ AuthService.cs:107-141 يبقي direct role/activation. Observed: requester لا يعتمد نفس Governance request لكن يستطيع استعمال مسار الإدارة المباشر. Expected: إذا اشترطت المؤسسة mandatory approval يجب أن تغطي السياسة كل writer. Trigger: مؤسسة تفرض separation of duties. Root: optional feature دون configuration enforce. Impact: لا يصلح الادعاء بامتثال إلزامي. Correction: Institution policy وaudit emergency override. Tests/Acceptance: كل المسارات والتعيين الأولي وglobal exception المعلن؛ لا تغيير محمي قبل decision. **فرضية bypass كثغرة حالية مستبعدة.**

### AUD-028 — Compute ينفذ استعلامات طالب×مكون أثناء قفل النشر
**Maintainability problem، P2، High مصدرّياً، الأداء الفعلي غير مقاس.** Evidence: Academix.Api/Services/Assessment/GradebookService.cs:280-300 يستدعي Earned:305-333 داخل loop لكل student/component؛ Publish يمسك section/policy حتى ينتهي. Observed: 500 طالب×20مكون قد يصل إلى10000 round-trips الأساسية، إضافة إلى base queries. Expected: bounded query budget مستقل قدر الإمكان عن N×M. Root: DB access داخل loop حسابي. Impact: lock hold time/timeout وتكاليف تشغيل؛ لا latency benchmark مخترع. Correction: batch-load scores keyed by student/source ثم pure calculation، مع source-version consistency AUD-014. Tests/Acceptance: DB-command counter و500/1000 students، ميزانيةqueries مقبولة ونتائج مطابقة، ثم load target مع المؤسسة.

### AUD-029 — عقود object/entity تجعل التغيير الداخلي عقداً خارجياً
**Maintainability problem، P2، High.** Evidence: Academix.Api/Controllers/SectionsController.cs:14-27 ActionResult<object>؛ AcademicOperations.CreateSection:89-97/GetSection:44-48 يعيدان entity؛ LearningOperations.MySubmission:209-214 يعيد entity أو anonymous NotStarted. Observed: response schemas غير محددة في type system. Expected: response DTOs موثقة وثابتة دون توسيع تلقائي. Trigger: إضافة property داخل entity أو توليد frontend client من OpenAPI. Root: contracts مرتبطة persistence أو object union ضمني. Impact: compatibility drift وتدقيق field exposure أصعب؛ ليس إثبات تسريب فعلي. Correction: typed DTOs ومميز حالة صريح بانتقال متوافق، مع إعادة استعمال contracts الموجودة. Tests/Acceptance: serialized schema snapshots، NotStarted/submitted/graded DTO موثق، لا secrets/storage keys غير مقصودة في API.

## 16 — الجاهزية التجارية
التمييز الضروري: معظم المجالات **implemented in source**؛ أجزاء من الحسابات **operationally verified** باختبارات نقية؛ الـHTTP/DB workflows **غير متحققة تشغيلياً**؛ بعض القواعد configurable (GPA bands/weights/document policy)، وأخرى hardcoded (14-day appeal، standing thresholds، BestAttempt، حدود500)؛ العديد من احتياجات المؤسسة **missing**.
ما يمنع جامعة كبيرة اليوم: إنشاء/ترقية DB غير صالح في AUD-011؛ كتابة workflows أساسية متأثرة AUD-009؛ لا assurance لامتحان جماعي وتحمّل أخطاء؛ حدود نشر/قوائم لا توافق أحجام التسجيل؛ eligibility مشتقة لا يتضح freshness لها؛ intake/registrar/documents جزئية؛ لا SIS integrations/import pipeline أو student finance؛ لا frontend/accessibility workflow مثبت؛ لا restore drill أو HA/retention/runbook evidence.
Compose الحالي **ملف تطوير**: compose.yaml:39 يختار Development و:42 يطبق migrations عند startup؛ MinIO image latest في:20؛ API Dockerfile بلا USER صريح. لا أصف هذه كتجربة production فاشلة أو vulnerability منشورة، لكن لا تستخدم الملف وحده كحزمة نشر مؤسسي. Health ready يفحص DB فقط ولا يثبت storage أو سلامة schema business؛ liveness لا يعتمد DB وهذا مقصود.
لا دليل كافياً لتحديد السعر أو المنافسة السوقية أو تكاليف الدعم؛ فرص القسم التالي تقدير منتج مبني على الفجوات المقروءة، وليس دراسة سوق. لا توجد منافع مالية مختلقة. الاحتفاظ بالبيانات والخصوصية وصلاحيات المرشد والموظفين يحتاجان policy مؤسسية ومراجعة مستقلة قبل pilot.

## 17 — أقوى عشر فرص توسع مرتبة
الترتيب حسب أثر الاعتماد المؤسسي والاعتمادات الحالية، وليس عدد endpoints. كل ما يلي تصميم مقترح لم يُنفذ. التعقيد تقدير نوعي، وليس وعد مدة أو تكلفة. **الوحدات الاستراتيجية الثلاث الأهم: Academic Policy & Registrar Decisions، Institutional Interoperability & Migration، Student Finance & Aid.** استكمال عيوب الأساس يسبق إطلاق أي منها.

### OPP-01 — Academic Policy & Registrar Decision Engine
- **المشكلة/الاستخدام/المستفيدون:** جامعة تختلف لوائح التسجيل والإعادة والعبء وstanding والتخرج بين دفعات وبرامج؛ registrar والطالب واللجان يحتاجون قراراً قابلًا للتفسير حسب النسخة السارية.
- **قصور الحالي:** RecordsPolicy hardcoded، Curriculum يحوي credits/bands جيدة لكنه لا يوحد term state/prereq/equivalency/holds. لا تعِد بناء GPA أو curricula الموجودة.
- **الكيانات:** InstitutionAcademicPolicy، PolicyRevision، ProgramPolicyBinding، StudentHold، EligibilityDecision، ApprovedException؛ ربط Institution/Program/Curriculum/Section.
- **Workflow/API:** Draft→validate→approve→effective؛ evaluate التسجيل/التخرج→explain→override؛ PolicyController وRegistrarDecisionController بعمليات /policies/{id}/publish و/eligibility/evaluate و/exceptions/{id}/approve؛ DTO rules محصورة typed وليست execution arbitrary scripts.
- **الاعتمادات/البيانات:** إصلاح AUD-002/015/016، resolver واحد للcompletion، نسخ immutable وeffective dates بلا overlap، حفظ decision inputs/source versions.
- **التفويض:** IA يهيئ، reviewer مستقل عند policy mandatory؛ الطالب يرى أسباب أهليته الآمنة فقط؛ SA لا يبدل لائحة مؤسسة بصمت.
- **التعقيد/القيمة/الأولوية:** عالٍ؛ قيمة تجارية وهندسية عالية جداً؛ **أولوية1**. القبول: نفس policy version ينتج نفس القرار، overrides مدققة ولا cross-tenant.

### OPP-02 — Institutional Interoperability & Legacy Migration
- **المشكلة/الاستخدام/المستفيدون:** لا تتبنى جامعة نظاماً لا يستورد سنوات السجلات ولا يتكامل مع هويتها وأنظمتها؛ registrar وIT والموردون يحتاجون staging/reconciliation.
- **قصور الحالي:** EnrollmentBatch يعالج قائمة IDs في شعبة، وليس import للطلاب/المناهج/الدرجات؛ CSV Analytics exports موجودة لكنها ليست migration pipeline أو API integration.
- **الكيانات:** ImportJob، ImportRow، ExternalIdentityMap، MappingProfile، ReconciliationReport، IntegrationClient، OutboxEvent، WebhookDelivery؛ FK tenant إلى الموارد الموجودة.
- **Workflow/API:** upload→parse→validate→dry-run→approve→apply chunks→reconcile؛ /imports/{id}/validate|preview|commit|report؛ scoped external APIs وwebhook subscribe/retry.
- **الاعتمادات/البيانات:** tenant constraints، alternate external key unique بالمؤسسة، durable idempotency، commit checkpoints، provenance؛ لا direct SQL يتجاوز business invariants.
- **التفويض:** Institution IT/registrar capabilities محددة؛ secrets خارج payload/logs؛ read-only preview قبل قرار Apply؛ توقيع webhook ومفتاح versioned.
- **التعقيد/القيمة/الأولوية:** عالٍ؛ تجاري عالٍ جداً/هندسي عالٍ؛ **أولوية2**. القبول: إعادة import لا تضاعف الصفوف، رفض row عابرة للمؤسسة، تطابق totals مع legacy وإمكان تعويض موثق.

### OPP-03 — Student Finance, Tuition & Financial Aid
- **المشكلة/الاستخدام/المستفيدون:** تسجيل وقبول وتخرج بلا رسوم/منح وتسويات لا يحقق SIS تجارياً لجامعة مدفوعة؛ الطالب وfinance وregistrar.
- **قصور الحالي:** لا ledger أو invoices/payment allocation أو scholarships في الكيانات/الخدمات المقروءة؛ إضافة مبلغ إلى Student ليست حلاً.
- **الكيانات:** FeeScheduleRevision، StudentAccount، Charge، Invoice، Payment، Allocation، CreditNote، ScholarshipAward، FinancialHold؛ ربط Term/Enrollment/Admission.
- **Workflow/API:** assess رسوم من snapshot سياسة→invoice→payment/credit→allocation/reconcile→hold release؛ FinanceController بعمليات charges/allocations/refunds مع financial DTOs وreceipts.
- **الاعتمادات/البيانات:** Policy/Interoperability، currency+decimal precision، immutable postings/reversals وbalanced reconciliation، idempotent gateway events؛ لا card data في ACADEMIX.
- **التفويض:** financial capability مستقلة عن المدرس، dual approval للrefunds، الطالب حسابه فقط؛ IA ليست تلقائياً accountant بلا سياسة.
- **التعقيد/القيمة/الأولوية:** عالٍ جداً؛ تجاري عالٍ جداً/هندسي عالٍ؛ **أولوية3 بعد pilot الأساس**. القبول: totals قابلة لإعادة الحساب، duplicate callbacks بلا تكرار، refund history كامل ومؤسسة معزولة.

### OPP-04 — Accreditation & Outcome-Based Education
- **المشكلة/الاستخدام/المستفيدون:** لجان الجودة تحتاج ربط البرنامج والمقرر والتقييم بنتائج تعلم وأدلة attainment عبر الدفعات؛ program leaders والمدرسون والجودة.
- **قصور الحالي:** Curriculum يحدد مقررات/credits وGradebook أوزاناً، وAnalytics نسباً إجمالية؛ لا PLO/CLO mapping أو assessment evidence مخصص للاعتماد.
- **الكيانات:** ProgramOutcome، CourseOutcome، OutcomeRevision، AssessmentOutcomeMap، AttainmentTarget، OutcomeEvidenceSnapshot، ImprovementAction؛ تعيد استعمال Program/Course/GradeComponent/Rubric.
- **Workflow/API:** version outcomes→map assessments→moderate data→calculate attainment→committee action→follow-up؛ OutcomesController وQualityAssuranceController وexport evidence package.
- **الاعتمادات/البيانات:** source score versioning AUD-014/015، frozen cohort definitions، منع double weighting، سجل formula/version/denominator.
- **التفويض:** مدرس مقرر scope، program quality reviewer، الطالب لا staff comments؛ IA approval حسب المؤسسة.
- **التعقيد/القيمة/الأولوية:** متوسط إلىعالٍ؛ تجاري وهندسي عاليان؛ **أولوية4**. القبول: إعادة حساب مستقلة تطابق snapshot، missing evidence يميز عن zero attainment، دليل متاح دون تسريب طالب.

### OPP-05 — Examination Moderation, Integrity & Item Analysis
- **المشكلة/الاستخدام/المستفيدون:** فرق الامتحان تحتاج مراجعة سرية قبل النشر وتحليل جودة السؤال ومعالجة اعتراض جماعي؛ مدرس ومراجع ولجنة نزاهة.
- **قصور الحالي:** bank revisions/pools والgrading/appeal موجودة؛ لا exam approval quorum أو item discrimination/difficulty evidence أو case workflow؛ لا تقترح bank CRUD جديداً.
- **الكيانات:** ExamModerationRequest، ReviewerAssignment، ApprovedBlueprint، ExamAccommodation، IntegrityCase، ItemStatisticsSnapshot؛ ربط QuestionRevision/PublishedQuestion/Attempt.
- **Workflow/API:** draft→blind review→approved version→publish؛ timeout finalization؛ post-exam analysis→question decision؛ /moderation/{id}/decision، /accommodations، /item-analysis، /integrity-cases.
- **الاعتمادات/البيانات:** إصلاح expiry وsnapshot/retry، الحفظ على version approved لا latest mutable، metrics تعلن حجم العينة ولا تنشر keys أو PII.
- **التفويض:** Reviewer مستقل وpublisher؛ TA بلا نشر افتراضي؛ لجان integrity capabilities منفصلة؛ accommodation لطالب محدد ومدقق.
- **التعقيد/القيمة/الأولوية:** عالٍ؛ تجاري وهندسي عاليان؛ **أولوية5**. القبول: تغيير exam يبطل الموافقة السابقة، extra time ثابت، case/analysis لا تكشف إجابات لطلاب آخرين.

### OPP-06 — Multi-Campus Resources & Constraint-Based Timetabling
- **المشكلة/الاستخدام/المستفيدون:** جدولة قاعات ومدرسين وشعب مشتركة عبر campuses؛ planner والطلبة والمدرسون يحتاجون جدولاً بلا تعارض.
- **قصور الحالي:** Room نص داخل Slot، overlap يفحص section فقط؛ لا Room/Campus resource ولا travel/time-zone policy.
- **الكيانات:** Campus، Building، Room، ResourceAvailability، TeachingAvailability، SchedulePlanRevision، Conflict، Allocation؛ FK مركبة institution/campus.
- **Workflow/API:** collect constraints→generate draft→explain conflicts→manual override→publish revision؛ /schedule-plans/generate|validate|publish، وavailability/resources.
- **الاعتمادات/البيانات:** Policy module وworkload، institution timezone/term calendar، room occupancy constraints، solver قابل للاستبدال لا microservice إلزامية.
- **التفويض:** campus planner داخل المؤسسة؛ Instructor يقترح availability؛ الطالب جدول منشور فقط.
- **التعقيد/القيمة/الأولوية:** عالٍ جداً؛ تجاري عالٍ/هندسي عالٍ؛ **أولوية6**. القبول: room/instructor/student collision tests بين sections وcampuses وDST، نشر جدول ذري.

### OPP-07 — Faculty Workload & Teaching Allocation
- **المشكلة/الاستخدام/المستفيدون:** رئيس القسم يحتاج توزيع credits/contact hours وعبء التصحيح والإشراف قبل إسناد المدرسين.
- **قصور الحالي:** TeachingAssignment يمنع Instructor مزدوجاً للشعبة لكنه لا يقيس عبء المدرس أو عقده أو اعتماده الأكاديمي.
- **الكيانات:** StaffAppointment، WorkloadPolicyRevision، WorkloadPlan، WorkloadItem، OverloadApproval؛ ربط User/Department/Term/TeachingAssignment.
- **Workflow/API:** estimate→allocate→review overload→approve→reconcile actual؛ /workload-plans و/allocations و/overload-requests.
- **الاعتمادات/البيانات:** Policy/Timetabling، unique assignment/plan version، effective employment state، freeze totals عند الاعتماد.
- **التفويض:** head/IA capabilities؛ الموظف عبئه فقط؛ TA نوع ومعدل عبء مستقلان.
- **التعقيد/القيمة/الأولوية:** متوسط إلىعالٍ؛ تجاري عالٍ/هندسي متوسط؛ **أولوية7**. القبول: لا ازدواج ساعات أو سريان تكليف بعد انتهاء appointment دون استثناء.

### OPP-08 — Registrar Case Fulfillment & Student Self-Service
- **المشكلة/الاستخدام/المستفيدون:** اعتماد عذر/استثناء/طلب لا يساوي إنجازه؛ الطالب وregistrar واللجان يحتاجون status وSLA وإثبات أثر.
- **قصور الحالي:** StudentAcademicRequests وAdvising مفيدان لكن request effects غير مرتبطة بالمورد، ولا workflow متكامل للمرشد الإداري خارج section.
- **الكيانات:** CaseTypeRevision، CasePayload، WorkflowStep، AssignmentQueue، ApprovedAction، FulfillmentEvent، ServiceDeadline؛ إعادة استعمال request/history وadvising tasks دون دمج خصوصيتهما عشوائياً.
- **Workflow/API:** submit→triage→request evidence→decision→fulfill→acknowledge/appeal؛ /cases/{id}/assign|decide|fulfill|appeal وstudent portal contracts.
- **الاعتمادات/البيانات:** Policy، Documents، outbox؛ exactly-once business effect via idempotency، redacted public timeline وinternal notes منفصلة.
- **التفويض:** case capabilities حسب النوع، independent approvals عند اللزوم، الطالب حالته فقط.
- **التعقيد/القيمة/الأولوية:** عالٍ؛ تجاري عالٍ/هندسي عالٍ؛ **أولوية8**. القبول: case مع Approved لكن بلا تنفيذ يظهر AwaitingFulfillment؛ effects آمنة عند retry.

### OPP-09 — Institutional Document Artifacts & Trust Lifecycle
- **المشكلة/الاستخدام/المستفيدون:** الجامعة تحتاج وثائق ذات قالب رسمي وهوية وتوقيع ومراجعة وإمكان supersession؛ registrar والطلاب وجهات التحقق.
- **قصور الحالي:** requests/issue/revoke/proof موجودة؛ JSON ورقم credential لا يكفيان لملف قابل للتسليم.
- **الكيانات:** DocumentTemplateRevision، DocumentArtifact، DocumentKeyVersion، DocumentSupersession، RenderJob؛ ربط IssuedAcademicDocument والـsnapshot الموجودة.
- **Workflow/API:** approved snapshot→render→quality gate→publish artifact→verify hash/state→revoke/supersede؛ /documents/{id}/artifact و/template-revisions و/key-versions.
- **الاعتمادات/البيانات:** private storage، source freshness، stable content digest وkey ring وtemplate version، transaction/outbox يفصل rendering عن business issuance الآمن.
- **التفويض:** template editor/reviewer منفصلان عند السياسة؛ الطالب artifact الخاص به؛ public verification حد أدنى بلا grades.
- **التعقيد/القيمة/الأولوية:** متوسط إلىعالٍ؛ تجاري عالٍ/هندسي عالٍ؛ **أولوية9**. القبول: تغيير JWT key لا يكسر وثيقة، artifact immutable ومطابق للsnapshot، revoke يظهر فوراً للتحقق.

### OPP-10 — Internship & Field Training Assessment
- **المشكلة/الاستخدام/المستفيدون:** برامج مهنية تحتاج placement/supervision/hours/evidence وتقييم جهة التدريب؛ الطالب وcoordinator والمشرف الخارجي.
- **قصور الحالي:** group projects/rubrics تصلح كلبنة تقييم، لكن لا host organization/placement أو external assessor أو attendance-hours signoff.
- **الكيانات:** TrainingHost، Placement، SupervisorAssignment، TrainingAgreement، HoursLog، Evidence، SupervisorAssessment، CompletionDecision؛ ربط CurriculumCourse/StudentProgramMembership.
- **Workflow/API:** approve host→place student→log/verify hours→assess rubric→moderate→record credits؛ /placements و/hours/approve و/assessments/submit و/completion.
- **الاعتمادات/البيانات:** Policy/identity invitations/files، no duplicate hours وimmutable approvals، controlled mapping إلى academic result لا كتابة grade مباشرة.
- **التفويض:** external assessor نطاق placement فقط، coordinator المؤسسة، student evidence الخاص به؛ لا grant Instructor عالمي للضيف.
- **التعقيد/القيمة/الأولوية:** عالٍ؛ تجاري متوسط إلىعالٍ للبرامج المهنية/هندسي عالٍ؛ **أولوية10**. القبول: guest لا يرى شعبة/طالباً آخر، hours/assessment تكفي completion حسب نسخة policy.

## 18 — الدرجات المهنية مع مستوى الثقة
هذه درجات تقديرية مبنية على الأدلة المتاحة، وليست benchmark أو شهادة امتثال. 0=غير صالح/غير موجود تقريباً، 5=تنفيذ ذو قيمة لكنه يحتاج consolidation، 10=مثبت إنتاجياً في نطاق واسع. لم تمنح درجات عالية للتشغيل لأن لا دليل عليه. الثقة متوسطة أو منخفضة حيث التغطية ناقصة.
| البعد | /10 | الثقة | الدليل المؤثر |
|---|---:|---|---|
| Architecture | 7 | متوسطة | two-project modular monolith وخدمات واضحة؛ cross-module invariants تحتاج توحيداً |
| Maintainability | 6 | متوسطة | folders/policies جيدة، لكن object contracts وN×M وtransaction patterns متباينة |
| Domain correctness | 5 | متوسطة | GPA/credit/appeal/ownership حقيقية؛ Closed enrollment وpartial lifecycle |
| Security | 6 | متوسطة | DB role/stamp/resource checks وsafe student DTO؛ لا runtime security matrix، لا recovery lifecycle كامل |
| Multi-tenancy | 6 | متوسطة | guards وcomposite FK كثيرة؛ user tenant consistency تطبيقية وبعض mappings غير مكتملة الدفاع |
| Database integrity | 4 | عالية للعيب/متوسطة للتقييم | migration columns غير صحيحة رغم مفاتيح/uniques جيدة؛ التنفيذ غير مثبت |
| Concurrency | 4 | متوسطة | locking/sequence/version متقدمة، لكن explicit transaction/retry blocker وstale tracking risk |
| API quality | 5 | متوسطة | typed DTO حديثة وProblemDetails؛ legacy schemas/paging/null handling ضعيفة |
| Test reliability | 4 | متوسطة | 217 ناجحاً خارج Integration؛ ثلاث عيوب fixtures/arrangement وسلسلة migration مانعة |
| Feature completeness | 5 | متوسطة | مجالات كثيرة ذات workflows فعلية، سبع قدرات جزئية محددة وحدود policy/scope |
| Operational readiness | 2 | منخفضة | لا DB/load/restore proof؛ compose Development وmigration-on-startup |
| Commercial readiness | 2 | متوسطة | Backend لا منتج end-user كامل، onboarding/import/finance/operations ناقصة |
| Portfolio value | 8 | متوسطة | breadth وbusiness policies وlocks/security قابلة للعرض، بشرط عدم ادعاء readiness غير مثبتة |

## 19 — المرحلة التالية: ENTERPRISE CONSOLIDATION & STRATEGIC EXPANSION
**هدف إصدار واحد متكامل:** تحويل المسارات الأساسية إلى baseline قابلة لإعادة التشغيل والتحقق، ثم إضافة نواة Academic Policy وMigration/Interoperability محدودة قابلة للاستعمال في pilot. لا تجمع finance/accreditation/solver/research كلها في هذه المرحلة. الوحدات الثلاث الاستراتيجية خارطة منتج؛ module Finance التالي بعد إثبات الأساس، وليس جزءاً ناقصاً مخفياً في هذا الإصدار.

### A — التصحيحات الملزمة
1. معالجة AUD-011 بعد inventory فعلي لـmigration histories، وتثبيت SQL/schema tests من الصفر ومن snapshots؛ لا حذف migrations أو reset بيانات.
2. إصلاح AUD-009 وAUD-013/017 بboundary واحدة للمعاملة/replay مع context جديد وidempotency وaudit atomicity؛ لا تعطيل retries عالمياً.
3. إصلاح AUD-002 وبروتوكول prerequisite/open/enroll، ومعالجة source-version snapshot للنشر/Transcript/GPA/Graduation في AUD-014/015.
4. إزالة dead ends للحجم AUD-012 وتوحيد paging AUD-003 عبر reuse InstructorWorkspace roster، وضبط validation AUD-004.
5. تصحيح تهيئة/عزل الاختبارات AUD-007/008/010 وإثبات أن tests تصل فعلياً إلى assertions، مع الاحتفاظ بالسلبية لا إسكاتها.

### B — الاستكمال الوظيفي داخل الإصدار
- انتهاء الامتحان: policy finalization تستخدم آخر جواب محفوظ قبل deadline، idempotent gradeable terminal state، وإعادة استعمال ExamAttemptPolicy/ExamGradingPolicy.
- Completion/equivalency resolver موحد للتسجيل وdegree audit، دون توليد درجات وهمية؛ ضوابط grandfathering للpolicy revisions.
- requests: تنفيذ AttendanceExcuse وEnrollmentException كأفعال typed بعد قرار مدقق؛ unknown general requests تبقى cases بلا side effect عشوائي.
- durable announcement dispatcher/outbox؛ ownership and opt-out محفوظة، notifications exactly-once business keys.
- وثائق enrollment/transcript قالب أول موثق وartifact/hash مستقل، document key lifecycle، freshness gate؛ بقية قوالب institution configurable تؤجل بوضوح.
- applicant onboarding minimal بمؤسسة pilot وبـinvitation/verified identity، دون public self-signup عابر للمؤسسات؛ curriculum transitions كحالة registrar مدققة محدودة.

### C — التوسع الجديد المحدد
**AcademicPolicy module:** rules محصورة لتسجيل term state/prereq/equivalency وcredit load وmandatory approval؛ version/publish/effective/explain، لا لغة scripts عامة ولا engine عشوائية.
**Interoperability module MVP:** imports للstudent profiles/program membership/curriculum assignment وسجلات منشأ أكاديمية مع reconciliation؛ dry-run/approval/apply/report؛ baseline outbound events لقرارات التسجيل/نشر النتائج/إصدار الوثائق. استيراد ماليات وتكامل gateway خارج النطاق.

### النموذج والعقود وAPI
| الحزمة | إعادة استعمال | إضافات محددة |
|---|---|---|
| Policy | Institution/Program/Curriculum، RecordsPolicy/CurriculumPolicy | AcademicPolicyRevision وBinding وEligibilityDecision وApprovedException؛ typed RuleSet/Decision DTO |
| Reconciliation | GradebookResult.Version وTranscript.GradebookVersion | AcademicSourceVersion/DerivedSnapshotStatus؛ gate قبل credential/document issuance |
| Execution | DbContext/IdentitySecurity/ApiFault | transaction executor context factory، stable OperationId وresult verification |
| Imports | EnrollmentBatch idempotency pattern لا نسخه كgeneric import | ImportJob/Row/ExternalIdentityMap/ValidationIssue/ApplyCheckpoint؛ row DTO/type+source provenance |
| Delivery | CommunicationNotification وunique source | OutboxEvent/DeliveryAttempt/Lease؛ no secret payload |
| Documents | IssuedAcademicDocument/SnapshotJson/PrivateFileStore | ArtifactHash/TemplateVersion/KeyId/RenderState وSupersession linkage |

Controllers جديدة: AcademicPoliciesController وRegistrarDecisionsController وInstitutionImportsController. الخدمات المقابلة مستقلة داخل Academix.Api؛ تبقى Controllers thin. توسيع DTO الحالية لـoperation IDs/source versions تدريجي مع compatibility، لا استبدال API بالكامل. Endpoints الأساسية: policy validate/publish، eligibility explain، exception decision، import validate/preview/apply/reconcile، document artifact. Authorization على كل job/row/resource لا role فقط؛ لا وصول طالب إلى ملفات الاستيراد أو تقييمات طلبة آخرين.

### قاعدة البيانات وMigrations
كل إضافة append-only history أو جدول جديد قدر الإمكان. tenant-aware FKs وunique (InstitutionId,ExternalSystem,ExternalId) وidempotency keys؛ policy effective ranges محمية بتصميم يراجع على PostgreSQL؛ job states/checks وconcurrency version؛ dirty snapshots لها source versions واضحة. افحص بيانات موجودة قبل فرض tenant FK أو max constraints؛ migration backfill موثق وقابل للتجزئة. لا تعِد كتابة التاريخ الأكاديمي لتحسين GPA؛ احتفظ correction/supersession audit.

### الاعتمادات والمخاطر والتعقيد
الترتيب: baseline schema → transaction/retry correctness → resource isolation/negative tests → consistent sources → functional completion → policy → import/outbox → pilot. التعقيد **عالٍ** ويحتاج slices داخلية متتابعة مع package نهائي واحد؛ لا يمكن إعطاء مدة موثوقة دون فريق وبيئة بيانات. المخاطر: schema history مختلفة، سريان policy على طلاب حاليين، dual writes/import provenance، key rotation، reconciliation costs، breaking clients. عالجها migration rehearsal وcompatibility tests وfeature flags معلنة للمؤسسة.

### بوابات القبول والتسليم ZIP
- Release -warnaserror ينجح؛ unit/contract suite و**Integration كاملة على PostgreSQL معزول** تنجح مع artifacts تنفيذ؛ لا تسمية skipped ناجحاً.
- migrate من صفر وupgrade من بيانات core/Phase13/نسخة institution ممثلة بلا orphan/tenant corruption؛ schema diff متوقع ومراجع.
- concurrent last seat/capacity/bulk/close-term، autosave sequence/submit/expiry، grade publication/source corrections، graduation/credential/document issue/revoke، logout/refresh وforced rollback/commit-unknown.
- مدرستان وطالبان على الأقل لكل private-resource matrix؛ TA/current role/revoked role/student owner والـpublisher tested، دون keys/unpublished grades/notes leaks.
- import preview بلا business writes، apply قابل للاستئناف ولا تكرار، reject rows لا يعبر المؤسسة، reconciliation counts وhashes.
- load profile واقعي متفق عليه مع مؤسسة pilot: concurrent starts/autosaves وgrade publish/large sections، p95/error budget وcommand counts، ثم restore drill يقارن rows/grades/artifacts.
- ZIP واحد يحوي source/build identity وSHA256 manifest وbaseline commands/results ونسخdependencies وmigration plan/compatibility matrix وknown limitations وrunbook وrollback/forward-recovery scripts **مراجعة**. لا .env أو passwords/tokens أو live student data في package. rollback لا يعني Down عشوائياً؛ وثق data backup/PITR وخطة التعويض للتغييرات غير القابلة للعكس.
- لا يطلق pilot حتى تنجح workflow ممثلة من applicant/provisioning إلى enrollment/learning/exam/result/transcript/curriculum/graduation/document verification تحت supervision، مع أدوار المؤسسة الفعلية.

## 20 — القرار الهندسي النهائي
1. **هل هو Backend قوي؟** نعم من حيث حجم domain والعمل الهندسي الحقيقي، لكنه يحتاج مرحلة تثبيت لتصبح قوته التشغيلية بمستوى تصميمه؛ لا أساوي breadth بالاعتماد الإنتاجي.
2. **مواضع النضج:** current-role resource checks، composite academic scopes، immutable exam snapshots، sequenced autosave، publisher separation، grade/audit history، weighted GPA/degree audit وequivalencies دون fake grades.
3. **الأضعف مما يبدو:** 217 tests لا تختبر SQL، Migrations hand-authored ليست سليمة لمجرد وجود Designer، معاملات صريحة لا تعمل مع configuration الحالية، نجاح اختبارات lifecycle موهوم حين التهيئة تمنع الوصول.
4. **أشد العيوب:** AUD-011 migration FK names، AUD-009 retry/transactions، AUD-002 closed enrollment؛ يليها capacity500 وlegacy paging/null contracts وثلاثة test defects.
5. **أكبر غير المتحقق:** PostgreSQL baseline؛ atomic Identity؛ consistent grade snapshots/fresh transcript؛ policy-lock interleavings؛ retry tracked state/commit ambiguity.
6. **الوظائف غير المكتملة:** القدرات السبع AUD-021..027؛ إضافة mandatory approval requirement حسب المؤسسة، وليس ادعاء فقد الموافقة الاختيارية الحالية.
7. **أعلى قيمة تجارية:** policy/registrar decisions، legacy import/integrations، student finance/aid، accreditation/outcomes، exam moderation/analysis.
8. **مانع النشر التجاري:** أعطال baseline والتكامل غير المتحقق وغياب operational/restore/load evidence وfrontend/self-service وتهيئة المؤسسة والتكامل والمالية. مجرد Docker compose ليس نظام تشغيل جامعي.
9. **ما ينفذ أولاً:** source-proven migrations ثم execution strategy ثم تشغيل integration وكشف بقية الأعطال، بالتوازي المنطقي مع تصميم policy/freshness دون إدخال feature surface قبل ثبات الأساس.
10. **المرحلة الأعلى قيمة:** ENTERPRISE CONSOLIDATION & STRATEGIC EXPANSION بالنطاق المحدد في القسم19؛ release واحد متكامل يثبت baseline ويضيف policy/import essentials.
11. **ما لا يعاد كتابته:** المشروعان، Controllers→Services→EF، Identity الأساسية، student-safe projections، exam revision/snapshot model، policies الحسابية وtenant guards وunique/idempotency patterns الصحيحة. أصلح boundaries والتنفيذ بدل framework migration.
12. **إثبات pilot:** clean/upgrade schema + كل Integration + adversarial tenant/role matrix + fault/retry/concurrency tests + realistic exam load + backup/restore + user acceptance لworkflow كاملة ومخرجات وثائق فعلية. التقرير لا يقدم هذا الإثبات بديلاً عن تشغيله.

### مراجعة ثانية وتصحيح الفرضيات
- أعيدت قراءة P1 المؤكدة AUD-011/AUD-009/AUD-002 مباشرة من المصدر؛ روجعت نصوص EnableRetryOnFailure والمعاملات ومراجع الأعمدة وعدم وجود Rename.
- فحص سلوك المكتبة بالإصدار نفسه: [EF Core 10.0.3 ExecutionStrategy.OnFirstExecution](https://github.com/dotnet/efcore/blob/v10.0.3/src/EFCore/Storage/ExecutionStrategy.cs#L351-L369) يرفض CurrentTransaction عند retries؛ هذا يعزز AUD-009 دون ادعاء HTTP runtime.
- راجعت P1 risks: policy/section locks لا تقفل grade sources، transcript sync يدوي، approval/issuance لا يثبت freshness، batch tracking يعاد داخل نفس db؛ بقيت مخاطر مشروطة ولم ترفع إلى confirmed exploit.
- استبعدت approval bypass كثغرة لأن الكود يعلن optional، وقيدت roster defect بالمسار القديم بسبب وجود Workspace pagination. لم أقترح إضافة GPA أو transfer equivalencies أو notifications CRUD أو document verification كأنها غير موجودة.
- لا نتائج integration مزعومة، ولا migration execution أو pending-model check أو Docker health منقولة من وثائق قديمة. درجات القسم18 provisional. لا أسعار أو latency أو exploit counts مخترعة.
- لا duplicated findings بعدد endpoints أوmigration files؛ count9 ثابت؛ serious risks6 وفق تعريف القسم14؛ partial capabilities7 وفق AUD-021..027.
- **نقص التغطية المتبقي:** كل DTO/HTTP boundary والفروع السلبية، كل tests المتبقية، كل migration column/Down/Designer ومقارنة model/schema، وسياسات deployment/backups/privacy، وruntime كله المعتمد على PostgreSQL/MinIO والواجهة. هذا التقرير واسع ومتماسك لكنه **غير مكتمل كتدقيق exhaustive**. لم تُملأ هذه الأجزاء باستنتاجات عامة أو شهادة سلامة.

### ملحق — سجل المصادر وحدود القراءة
المسارات التالية نسبية إلى الجذر المعتمد. A: قرئ الجسم الوظيفي كاملاً، مع حذف usings/blank braces أحياناً من العرض. B: قراءة انتقائية للroutes/الاختبار/القيود؛ ليست full-file assurance. C: مصدر التدقيق السابق مطابق SHA256 مع إعادة فحص أدلته الرئيسية في هذه الجولة؛ يحتفظ بنطاق القراءة السابق. الجرد ليس مقياس الاكتمال. تبقى اختبارات كثيرة خارج هذا السجل، وقد نفذت بالفلتر دون مراجعة أجسامها.

| المسار | النطاق |
|---|---|
| AGENTS.md | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.slnx | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| global.json | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Directory.Build.props | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Academix.Api.csproj | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Tests/Academix.Tests.csproj | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Program.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Extensions/ServiceCollectionExtensions.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Extensions/WebApplicationExtensions.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Services/Identity/AuthService.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Services/Identity/IdentitySecurity.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Services/Identity/TeachingRolePolicy.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Services/Identity/IdentityBootstrap.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Controllers/AuthController.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Controllers/UsersController.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/DTOs/Identity/AuthContracts.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Entities/Identity/ApplicationUser.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Data/Configurations/IdentityConfiguration.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Services/Academic/AcademicOperations.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Services/Academic/StructureService.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Controllers/EnrollmentsController.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Controllers/SectionsController.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Controllers/InstitutionsController.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Controllers/AcademicTermsController.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Controllers/FacultiesController.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Controllers/DepartmentsController.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Controllers/ProgramsController.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Controllers/CoursesController.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/DTOs/Academic/AcademicContracts.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Entities/Academic/AcademicEntities.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Entities/Courses/Course.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Data/Configurations/AcademicConfiguration.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Data/Configurations/CourseConfiguration.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Data/AcademixDbContext.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Middleware/GlobalExceptionHandler.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Middleware/ApiFault.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Services/Governance/GovernanceService.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Services/Governance/GovernancePolicy.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Controllers/GovernanceController.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/DTOs/Governance/GovernanceContracts.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Entities/Governance/GovernanceEntities.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Data/Configurations/GovernanceConfiguration.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Services/Records/RecordsService.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Services/Records/RecordsPolicy.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Controllers/RecordsController.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/DTOs/Records/RecordsContracts.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Entities/Records/RecordsEntities.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Data/Configurations/RecordsConfiguration.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Storage/PrivateFileStore.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Services/Courses/CourseQueries.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Services/Courses/ICourseQueries.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Controllers/CourseCatalogController.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/DTOs/Courses/CourseDto.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Tests/Integration/ApiFixture.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Tests/Integration/Phase1EndpointsTests.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Tests/Integration/Phase1ConcurrencyTests.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Tests/Integration/TeachingRoleRevocationTests.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Tests/Integration/Phase11GovernanceTests.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Tests/Integration/Phase14AcademicRecordsTests.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Tests/Unit/Phase1DomainTests.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Tests/Unit/TeachingRolePolicyTests.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Tests/Unit/RecordsPolicyTests.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Tests/Security/ControllerAuthorizationTests.cs | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| docs/architecture.md | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| docs/progress.md | C — قراءة كاملة للمصدر؛ عمق تتبع workflow موضح في AUDIT_PROGRESS.md |
| Academix.Api/Data/Migrations/20261024132121_Phase1Complete.cs | C — جزئي: 110-134 و338-414 و598-629؛ بحث مواضع القيود والفهارس |
| Academix.Api/Data/Migrations/AcademixDbContextModelSnapshot.cs | C — جزئي: 1383-1445 و2268-2297؛ بحث مواضع check constraints |
| Academix.Api/Services/Learning/LearningOperations.cs | A |
| Academix.Api/Services/Learning/LearningFileContentPolicy.cs | A |
| Academix.Api/Controllers/LearningFilesController.cs | A |
| Academix.Api/DTOs/Learning/LearningContracts.cs | A |
| Academix.Api/Services/Examinations/ExamTakingService.cs | A |
| Academix.Api/Services/Examinations/ExamAttemptPolicy.cs | A |
| Academix.Api/Data/Configurations/AttemptConfiguration.cs | A |
| Academix.Api/DTOs/Examinations/AttemptContracts.cs | A |
| Academix.Api/Entities/Examinations/AttemptEntities.cs | A |
| Academix.Api/Controllers/ExamTakingController.cs | A |
| Academix.Api/Services/Examinations/ExamBuilderService.cs | A |
| Academix.Api/Services/Examinations/QuestionBankService.cs | A |
| Academix.Api/Services/Examinations/ExamGradingService.cs | A |
| Academix.Api/Services/Assessment/GradebookService.cs | A |
| Academix.Api/Services/Lifecycle/LifecycleService.cs | A |
| Academix.Api/Services/Curriculum/CurriculumService.cs | A |
| Academix.Api/Services/Institutional/InstitutionalDocumentService.cs | A |
| Academix.Api/Services/AdvancedLearning/AdvancedLearningAccess.cs | A |
| Academix.Api/Services/AdvancedLearning/GroupLearningService.cs | A |
| Academix.Api/Services/AdvancedLearning/DiscussionService.cs | A |
| Academix.Api/Services/Communications/CommunicationService.cs | A |
| Academix.Api/Services/AcademicOperations/TeachingOperationsService.cs | A |
| Academix.Api/Services/AcademicOperations/StudentAcademicOperationsService.cs | A |
| Academix.Api/Services/AcademicOperations/RegistrarOperationsService.cs | A |
| Academix.Api/Services/Advising/AdvisingService.cs | A |
| Academix.Api/Services/Analytics/AnalyticsService.cs | A |
| Academix.Api/Services/Workspaces/PlatformGovernanceService.cs | A |
| Academix.Api/Services/Workspaces/StudentWorkspaceService.cs | A |
| Academix.Api/Services/Workspaces/InstructorWorkspaceService.cs | A |
| Academix.Api/Services/Workspaces/InstitutionWorkspaceService.cs | A |
| Academix.Api/Data/Configurations/LearningConfiguration.cs | A |
| Academix.Api/Data/Configurations/AdvancedLearningConfiguration.cs | A |
| Academix.Api/Data/Configurations/ExaminationConfiguration.cs | A |
| Academix.Api/Data/Configurations/GradingConfiguration.cs | A |
| Academix.Api/Data/Configurations/GradebookConfiguration.cs | A |
| Academix.Api/Data/Configurations/CurriculumConfiguration.cs | A |
| Academix.Api/Data/Configurations/LifecycleConfiguration.cs | A |
| Academix.Api/Data/Configurations/InstitutionalDocumentConfiguration.cs | A |
| Academix.Api/Data/Configurations/CommunicationsConfiguration.cs | A |
| Academix.Api/Data/Configurations/AcademicOperationsConfiguration.cs | A |
| Academix.Api/Services/Curriculum/CurriculumPolicy.cs | A |
| Academix.Api/Services/Lifecycle/LifecyclePolicy.cs | A |
| Academix.Api/Services/Examinations/ExamGradingPolicy.cs | A |
| Academix.Api/Services/Examinations/PoolAssembly.cs | A |
| Academix.Api/Services/Examinations/ExamValidation.cs | A |
| Academix.Api/Services/Examinations/QuestionValidation.cs | A |
| Academix.Api/Entities/Learning/LearningEntities.cs | A |
| Academix.Api/Entities/Examinations/QuestionEntities.cs | A |
| Academix.Api/Entities/Examinations/ExamEntities.cs | A |
| Academix.Api/Entities/Examinations/GradingEntities.cs | A |
| Academix.Api/Entities/AdvancedLearning/AdvancedLearningEntities.cs | A |
| Academix.Api/Entities/Assessment/GradebookEntities.cs | A |
| Academix.Api/Entities/Lifecycle/LifecycleEntities.cs | A |
| Academix.Api/Entities/Institutional/InstitutionalEntities.cs | A |
| Academix.Api/Entities/Curriculum/CurriculumEntities.cs | A |
| Academix.Api/Entities/Communications/CommunicationEntities.cs | A |
| Academix.Api/Entities/Advising/AdvisingEntities.cs | A |
| Academix.Api/Entities/AcademicOperations/OperationEntities.cs | A |
| Academix.Api/DTOs/Institutional/InstitutionalContracts.cs | A |
| Academix.Api/DTOs/Lifecycle/LifecycleContracts.cs | A |
| Academix.Api/DTOs/AdvancedLearning/AdvancedLearningContracts.cs | A |
| Academix.Tests/Integration/Phase2LearningTests.cs | A |
| Academix.Tests/Integration/Phase4ExamExecutionTests.cs | A |
| Academix.Tests/Integration/Phase9GradebookTests.cs | A |
| Academix.Tests/Security/GradeContractSecurityTests.cs | A |
| Academix.Tests/Security/ExamAttemptContractTests.cs | A |
| Academix.Api/Dockerfile | A |
| Academix.Api/Data/Configurations/AdvisingConfiguration.cs | B |
| Academix.Api/Controllers/InstitutionalDocumentsController.cs | B |
| Academix.Api/Controllers/StudentLifecycleController.cs | B |
| Academix.Api/Controllers/CurriculumController.cs | B |
| Academix.Api/Controllers/AdvancedGroupLearningController.cs | B |
| Academix.Api/Controllers/CommunicationsController.cs | B |
| Academix.Api/Controllers/AcademicTeachingOperationsController.cs | B |
| Academix.Api/Controllers/ExamResultsController.cs | B |
| Academix.Api/Controllers/ExamGradingController.cs | B |
| Academix.Tests/Integration/Phase5GradingResultsTests.cs | B |
| Academix.Tests/Integration/Phase16LifecycleTests.cs | B |
| Academix.Tests/Integration/Phase17InstitutionalDocumentsTests.cs | B |
| Academix.Tests/Integration/AssignmentDeadlineConcurrencyTests.cs | B |
| Academix.Tests/Unit/ControllerRouteTests.cs | B |
| Academix.Tests/Unit/ExamAttemptPolicyTests.cs | B |
| Academix.Tests/Unit/PoolAssemblyTests.cs | B |
| Academix.Tests/Unit/LifecycleEquivalencyTests.cs | B |
| Academix.Tests/Unit/LearningFileContentPolicyTests.cs | B |
| compose.yaml | B |
| scripts/verify.ps1 | B |
| Academix.Api/Data/Migrations/20261024000000_InitialCreate.cs | B — جرد زمني وقيود/عمليات منتقاة؛ Phase3QuestionBank وPhase17 قرئا كاملين؛ لا execution |
| Academix.Api/Data/Migrations/20261024132121_Phase1Complete.cs | B — جرد زمني وقيود/عمليات منتقاة؛ Phase3QuestionBank وPhase17 قرئا كاملين؛ لا execution |
| Academix.Api/Data/Migrations/20261024141037_Phase2Learning.cs | B — جرد زمني وقيود/عمليات منتقاة؛ Phase3QuestionBank وPhase17 قرئا كاملين؛ لا execution |
| Academix.Api/Data/Migrations/20261026140000_Phase3QuestionBank.cs | B — جرد زمني وقيود/عمليات منتقاة؛ Phase3QuestionBank وPhase17 قرئا كاملين؛ لا execution |
| Academix.Api/Data/Migrations/20261026151000_Phase3ExamBuilder.cs | B — جرد زمني وقيود/عمليات منتقاة؛ Phase3QuestionBank وPhase17 قرئا كاملين؛ لا execution |
| Academix.Api/Data/Migrations/20261026163000_Phase3QuestionPools.cs | B — جرد زمني وقيود/عمليات منتقاة؛ Phase3QuestionBank وPhase17 قرئا كاملين؛ لا execution |
| Academix.Api/Data/Migrations/20261026180000_Phase4ExamExecution.cs | B — جرد زمني وقيود/عمليات منتقاة؛ Phase3QuestionBank وPhase17 قرئا كاملين؛ لا execution |
| Academix.Api/Data/Migrations/20261026193000_Phase5GradingResultsAppeals.cs | B — جرد زمني وقيود/عمليات منتقاة؛ Phase3QuestionBank وPhase17 قرئا كاملين؛ لا execution |
| Academix.Api/Data/Migrations/20261027160000_Phase7AcademicOperations.cs | B — جرد زمني وقيود/عمليات منتقاة؛ Phase3QuestionBank وPhase17 قرئا كاملين؛ لا execution |
| Academix.Api/Data/Migrations/20261027190000_Phase8AdvancedLearning.cs | B — جرد زمني وقيود/عمليات منتقاة؛ Phase3QuestionBank وPhase17 قرئا كاملين؛ لا execution |
| Academix.Api/Data/Migrations/20261027210000_Phase9AcademicAssessment.cs | B — جرد زمني وقيود/عمليات منتقاة؛ Phase3QuestionBank وPhase17 قرئا كاملين؛ لا execution |
| Academix.Api/Data/Migrations/20261027230000_Phase10Communications.cs | B — جرد زمني وقيود/عمليات منتقاة؛ Phase3QuestionBank وPhase17 قرئا كاملين؛ لا execution |
| Academix.Api/Data/Migrations/20261027234500_Phase11Governance.cs | B — جرد زمني وقيود/عمليات منتقاة؛ Phase3QuestionBank وPhase17 قرئا كاملين؛ لا execution |
| Academix.Api/Data/Migrations/20261028100000_Phase13Advising.cs | B — جرد زمني وقيود/عمليات منتقاة؛ Phase3QuestionBank وPhase17 قرئا كاملين؛ لا execution |
| Academix.Api/Data/Migrations/20261028110000_Phase14AcademicRecords.cs | B — جرد زمني وقيود/عمليات منتقاة؛ Phase3QuestionBank وPhase17 قرئا كاملين؛ لا execution |
| Academix.Api/Data/Migrations/20261029110000_Phase15CurriculumPlanning.cs | B — جرد زمني وقيود/عمليات منتقاة؛ Phase3QuestionBank وPhase17 قرئا كاملين؛ لا execution |
| Academix.Api/Data/Migrations/20261029120000_Phase16AdmissionsGraduation.cs | B — جرد زمني وقيود/عمليات منتقاة؛ Phase3QuestionBank وPhase17 قرئا كاملين؛ لا execution |
| Academix.Api/Data/Migrations/20261030100000_Phase17InstitutionalDocuments.cs | B — جرد زمني وقيود/عمليات منتقاة؛ Phase3QuestionBank وPhase17 قرئا كاملين؛ لا execution |

Designer/Snapshot: بحث targeted للأعمدة والعلاقات، وليس مراجعة كاملة. .dockerignore وAcademix.slnx وglobal.json وDirectory.Build.props قرئت. لا .env أو live secrets أو backup source استعملت كدليل. أسماء الملفات المختصرة داخل النص تشير إلى المسار الكامل أعلاه، ومرجع finding النسبي يحدد المورد المعني.
