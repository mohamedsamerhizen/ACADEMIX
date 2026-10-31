# سجل نتائج التدقيق الهندسي — ACADEMIX
تاريخ الجلسة: 2026-10-28، Asia/Baghdad. الحالة: نقطة حفظ أولى؛ التدقيق الشامل غير مكتمل.

المستودع المعتمد حصراً: `C:\Users\lenovo\Desktop\ACADEMIX`.
كل مرجع أدناه مسار نسبي إلى هذا الجذر، مع اسم الدالة والأسطر. النتائج المؤكدة هنا مثبتة بتتبع المصدر، ولم تُعد إنتاجها عبر HTTP/PostgreSQL. نجاح Build و217 اختباراً خارج Integration لا يثبت تشغيل هذه السيناريوهات. لم تتغير ملفات التطبيق أو الاختبارات أو إعداداتهما أو Migrations.

دلالات الأولوية: P1 خلل مؤثر في الضبط أو سلامة العملية؛ P2 خلل وظيفي أو تعاقدي؛ P3 تحسين محدود. لا توجد نتيجة P0 مثبتة في هذه الجلسة.

## AUD-001 — لا يمكن فرض موافقة مستقلة إلزامية على تغييرات العاملين
- **التصنيف:** Missing capability؛ ليست ثغرة مؤكدة أو مخالفة لشرط معلن في الكود.
- **الخطورة:** P2؛ ترتفع أولوية المنتج عند استهداف مؤسسة تفرض فصل المهام.
- **الدليل:** `Academix.Api/Entities/Governance/GovernanceEntities.cs:3` يصرح: “Optional two-person approval workflow. Existing direct role-management routes are unchanged.” `Controllers/UsersController.cs:17-28` يعرض مسارات التغيير المباشر، و`Services/Identity/AuthService.cs:107-141` يسمح لمدير المؤسسة بالأدوار غير الإدارية والتفعيل ضمن المؤسسة. `Services/Governance/GovernanceService.cs:90-149` و`GovernancePolicy.cs:36-39` يفرضان مراجعاً مستقلاً داخل مسار الطلب فقط.
- **السلوك الفعلي:** المساران متاحان لنفس مدير المؤسسة؛ optional approval يعمل كما يصفه المصدر. لا يمكن تعميم شرط المراجع المستقل على النظام كله. لا دليل على تصعيد عالمي أو وصول لمؤسسة أخرى.
- **المحفز:** مؤسسة تشترط موافقة ثنائية؛ مدير يرسل PUT roles أو activation مباشرة دون StaffChangeRequest، أو ينشئ حساب عامل عبر `CreateUserAsync:87-104`.
- **الأثر:** القدرة الحالية غير كافية لتقديم ضمان mandatory separation of duties أو سجل طلبات يشمل جميع التغييرات.
- **التوصية:** إضافة سياسة Institution-level configurable توضح التغييرات المشمولة، والتعيين الأولي، واستثناءات SuperAdministrator؛ توحيد تطبيقها على مسارات Users وGovernance.
- **الاختبارات المطلوبة:** الوضع الاختياري الحالي؛ الوضع الإلزامي لكل AddRole/RemoveRole/Activate/Deactivate والتعيين الأولي؛ رفض المراجعة الذاتية والعابرة للمؤسسات؛ عدم تغير Identity قبل الاعتماد؛ تدقيق الاستثناءات.
- **معايير القبول:** عند تفعيل السياسة الإلزامية لا مسار بديل يحدث التغيير المحمي دون القرار المستقل؛ تبقى السياسة الاختيارية متوافقة مع العقد المعلن.
- **الثقة:** High.
- **ملاحظة تحقيق:** استبعدت فرضية “approval bypass كخلل مؤكد” بعد قراءة التعليق الصريح؛ لا تعتمد صياغة الاكتشاف الأولية.
- **حد الاختبار الحالي:** `Academix.Tests/Integration/Phase11GovernanceTests.cs:59-95` يثبت تصميم اختبار المراجعة الذاتية وإسناد الدور والطلب المكرر في المسار الاختياري؛ لم ينفذ في هذه الجلسة.

## AUD-002 — إغلاق الفصل الأكاديمي لا يغلق التسجيل في شعبه المفتوحة
- **التصنيف:** Confirmed defect.
- **الخطورة:** P1.
- **الدليل:** `Academix.Api/Services/Academic/StructureService.cs:144-154`، `UpdateTerm`، يسمح Active → Closed دون منع وجود شعب Open أو إغلاقها. `Academix.Api/Services/Academic/AcademicOperations.cs:162-190`، `Enroll`، يقفل الشعبة ويتحقق من Open والسعة والطالب والمتطلبات والدرجات المنشورة، لكنه لا يقرأ حالة AcademicTerm. `GovernanceService.cs:228-264,278-329` يكرر هذا النقص في EvaluateBatch وCommitBatch. فحص الفصل Active موجود عند فتح الشعبة فقط في `AcademicOperations.cs:123-127`.
- **سيناريو الإعادة:** إنشاء فصل Planned ثم تفعيله؛ إنشاء شعبة وإسناد Instructor وفتحها؛ إغلاق الفصل عبر PUT terms مع ExpectedVersion الصحيح؛ تسجيل طالب جديد أو اعتماد Batch. مع سعة متاحة ومتطلبات مستوفاة وعدم نشر Gradebook، لا يوجد شرط يرفض التسجيل بسبب Closed.
- **الأثر:** إنشاء تسجيلات جديدة في فصل مغلق؛ تتعارض بيانات التسجيل مع حدود الدورة الأكاديمية.
- **التصحيح المقترح:** توحيد شرط السماح بالتسجيل في الفردي والجماعي والتحقق منه وقت commit. نسق إغلاق الفصل مع التسجيل بأقفال مشتركة وترتيب ثابت أو آلية ذرية مكافئة؛ فحص منفصل قبل المعاملة لا يكفي للتزامن.
- **الاختبارات المطلوبة:** إغلاق الفصل ثم التسجيل الفردي وBatch؛ انتقال الفصل إلى Closed أثناء طلب ينتظر القفل؛ لا صفوف جديدة ولا إعادة تفعيل بعد الإغلاق؛ نجاح التسجيل في الحالة المسموحة؛ تحقق مستقل من PostgreSQL.
- **معايير القبول:** لا commit جديد للتسجيل بعد نقطة إغلاق الفصل المحددة بالسياسة؛ Preview وCommit والفردي تعكس السياسة نفسها؛ لا يدمر التصحيح التسجيلات السابقة.
- **الثقة:** High.
- **حد الإثبات:** لا ندعي هنا أن التسجيل يجب أن يبدأ في يوم StartsOn تحديداً؛ نافذة التسجيل الزمنية قرار مستقل. الخلل المثبت هو غياب ربط التسجيل بحالة Closed.

## AUD-003 — قائمة الشعبة تقطع الطلاب بعد 100 سجل دون آلية استكمال
- **التصنيف:** Confirmed defect.
- **الخطورة:** P2.
- **الدليل:** `Academix.Api/Services/Academic/AcademicOperations.cs:198-203`، `Roster`، تستعمل Take(100) وتعيد جميع الحالات مرتبة بـEnrolledAtUtc فقط؛ `Academix.Api/Controllers/EnrollmentsController.cs:17-19` لا تقبل page أو cursor. نفس الخدمة تسمح بسعة حتى 10000 في `CreateSection:92` و`UpdateSection:118`.
- **سيناريو الإعادة:** شعبة تحوي 101 تسجيل؛ GET roster يعيد 100 فقط، ولا يوجد parameter للوصول إلى الأخير. عند وجود Dropped/Withdrawn قد تشغل التسجيلات القديمة الحصة وتختفي تسجيلات Active لاحقة.
- **الأثر:** قائمة تشغيلية ناقصة للمدرس والإدارة؛ لا يمكن للعميل معرفة العدد الكامل أو استكماله من هذا المسار.
- **التصحيح المقترح:** DTO مرقم بحد معروف وtotal أو cursor، وترتيب مستقر بمفتاح فريد، مع فلتر حالة موثق.
- **الاختبارات المطلوبة:** 101 و250 سجلاً، حالات مختلطة، تساوي EnrolledAtUtc، استكمال الصفحات بلا تكرار أو فقد، رفض مؤسسة أخرى.
- **معايير القبول:** يمكن استرجاع كل التسجيلات المسموح بها؛ لا قطع صامت؛ حد الصفحة لا يساوي حد عدد طلاب الشعبة.
- **الثقة:** High.

## AUD-004 — بيانات هوية ناقصة تصل إلى dereference بدلاً من رفض validation
- **التصنيف:** Confirmed defect.
- **الخطورة:** P2.
- **الدليل:** `Academix.Api/Extensions/ServiceCollectionExtensions.cs:130-131` يعطل implicit Required للأنواع المرجعية non-nullable. `Academix.Api/DTOs/Identity/AuthContracts.cs:3-4,10` لا يحدد Required أو بديل validation. `AuthService.cs:16` تنفذ `request.Email.Trim()` و`CreateUserAsync:97` تنفذ `req.Email.Trim()`، و`LogoutAsync:76` تمرر RefreshToken إلى `IdentitySecurity.TokenHash:72` ثم Encoding.UTF8.GetBytes. `GlobalExceptionHandler.cs:11-20` يصنف الاستثناء غير المعروف 500.
- **سيناريو الإعادة:** POST login بجسم `{"email":null,"password":"x"}`، أو حذف email من JSON؛ الربط يقبل قيمة مرجعية null دون Required ثم يحدث NullReferenceException. وفي logout بمصادقة صحيحة، RefreshToken=null يصل إلى ArgumentNullException. يلزم تشغيل HTTP لتوثيق النتيجة الفعلية لكل payload.
- **الأثر:** أخطاء عميل تتحول إلى أخطاء خادم ورسائل LogError؛ عقد API غير متين. لا دليل هنا على تجاوز مصادقة.
- **التصحيح المقترح:** validation صريح لحقول الهوية المطلوبة وحدود أطوالها عند boundary مع دفاع داخل الخدمة؛ حافظ على استجابة موحدة للبيانات الاعتمادية غير الصحيحة.
- **الاختبارات المطلوبة:** null/حذف/فراغ للـEmail وPassword وRefreshToken في المسارات الملائمة؛ JSON صحيح وصيغة خاطئة؛ التأكد أن 4xx لا يغير بيانات ولا يصدر tokens، وأن الطلب الصحيح يبقى ناجحاً.
- **معايير القبول:** المدخل الناقص ينتج 4xx معرفاً بالعقد دون NullReferenceException/ArgumentNullException أو 500.
- **الثقة:** High في مسار المصدر؛ إعادة الإنتاج HTTP غير منفذة.

## AUD-005 — تماثل مؤسسة الطالب/المدرس والشعبة غير مفروض في FK المستخدم
- **التصنيف:** High-confidence risk.
- **الخطورة:** P2.
- **الدليل:** `Academix.Api/Data/Configurations/AcademicConfiguration.cs:73-88` يربط كل من StudentEnrollment وTeachingAssignment بالشعبة عبر (SectionId,InstitutionId)، لكنه يربط المستخدم عبر StudentId أو UserId فقط. نفس التصميم موجود في Migration `20261024132121_Phase1Complete.cs:361-412` وفي Snapshot `AcademixDbContextModelSnapshot.cs:2268-2297`. فحوص المؤسسة والدور موجودة في `AcademicOperations.AssignStaff:135-145` و`Enroll:174-175` و`GovernanceService.EvaluateBatch:236-249`.
- **المحفز:** كتابة مباشرة أو استيراد مستقبلي/مسار بديل غير ملتزم بفحوص الخدمة يستطيع ربط مستخدم مؤسسة B بشعبة A. لم يثبت مسار HTTP حالي يتيح هذا الإدخال، فلا تصنف النتيجة ثغرة cross-tenant مؤكدة.
- **الأثر:** تعتمد سلامة علاقة المستخدم بالمؤسسة على كل كاتب للبيانات؛ FK يثبت وجود المستخدم فقط.
- **التصحيح المقترح:** تقييم مفتاح بديل (Id,InstitutionId) للمستخدم وعلاقات مركبة أو آلية DB مكافئة، مع مراعاة nullable InstitutionId للحسابات العالمية. افحص البيانات المخالفة قبل أي Migration.
- **الاختبارات المطلوبة:** PostgreSQL يرفض إدخال علاقة عابرة للمؤسسة لكلا النوعين؛ يقبل العلاقة الصحيحة؛ يحافظ على حساب SuperAdministrator غير المنتسب؛ فحص ترحيل بيانات سابقة.
- **معايير القبول:** ضمان موثق ومختبر في DB لتماثل المؤسسة أو قرار معماري صريح يحدد البديل وحدوده؛ لا فقد بيانات.
- **الثقة:** High في حدود القيود؛ Medium في احتمال وقوع فساد فعلي.

## AUD-006 — لا يوجد إثبات Integration على PostgreSQL في هذه الجلسة
- **التصنيف:** Runtime verification required.
- **الخطورة:** P1 كفجوة تحقق قبل الاعتماد المؤسسي؛ ليست علة تشغيل مثبتة.
- **الدليل:** `Academix.Tests/Integration/ApiFixture.cs:10-14,20-38` يحتاج Docker وpostgres:18-alpine، ويشغل التطبيق مع ApplyMigrationsOnStartup=true. `WebApplicationExtensions.cs:27-35` ينفذ MigrateAsync فعلياً في هذا الوضع. أمر Docker المسجل في RUNTIME_EVIDENCE.md فشل لأن pipe محرك Linux غير موجود.
- **المحفز:** الاعتماد على نجاح Unit/Security tests أو Build للحكم على migrations والأقفال والـFK.
- **الأثر:** لم تتحقق سلسلة Migrations أو SQL أو ترجمة LINQ أو سلوك التزامن أو HTTP workflows المعتمدة على القاعدة.
- **التصحيح المقترح:** تشغيل suite على PostgreSQL معزول في CI أو بيئة Docker متاحة، وتسجيل النسخ والأوامر والنتائج؛ لا تستخدم قاعدة المستخدم.
- **الاختبارات المطلوبة:** suite Integration كاملة بما فيها سحب الدور، آخر مقعد، خفض السعة، Governance، وبقية الوحدات بعد استكمال مراجعتها؛ اختبارات سلبية للنتائج أعلاه.
- **معايير القبول:** سجل تنفيذ قابل للمراجعة لتطبيق migrations على قاعدة فارغة وتشغيل Integration بلا فشل غير مفسر؛ لا يكفي الفحص البنيوي.
- **الثقة:** High.

## AUD-007 — اختبار المتطلبات السابقة ينشئ شعبة مفتوحة ثم يتوقع تعديل متطلباتها بنجاح
- **التصنيف:** Confirmed defect في اختبار Integration.
- **الخطورة:** P2.
- **الدليل:** `Academix.Tests/Integration/Phase14AcademicRecordsTests.cs:65-66` ينشئ targetSection بحالة Open. الأسطر `86-88` تتوقع OK لإضافة prerequisite لنفس targetCourse. `Academix.Api/Services/Records/RecordsService.cs:133-138` يقفل المؤسسة ثم يرفض Conflict عندما توجد شعبة Open للمقرر.
- **سيناريو الإعادة:** تشغيل `dotnet test --filter FullyQualifiedName~Phase14AcademicRecordsTests` في بيئة Testcontainers متاحة. يتوقع المصدر بلوغ HTTP 409 بدلاً من OK عند الإضافة؛ لم ينفذ السيناريو هنا.
- **الأثر:** الاختبار لا يصل إلى التحقق اللاحق من أهلية التسجيل وTranscript وStanding؛ وجود assertions لاحقة ليس دليلاً على إمكانية الوصول إليها.
- **التصحيح المقترح:** بناء prerequisite قبل فتح الشعبة في تهيئة السيناريو؛ إضافة اختبار مستقل يؤكد رفض تعديل المتطلبات بعد الفتح. لا تغير قاعدة العمل لمجرد إرضاء الاختبار.
- **الاختبارات المطلوبة:** تنفيذ الاختبار المعدل على PostgreSQL؛ سلبية Open؛ إيجابية قبل الفتح؛ تحقق من أن المتطلب يقيد التسجيل الفردي والجماعي.
- **معايير القبول:** suite تصل فعلياً لكل assertions الأكاديمية مع الحفاظ على رفض mutation أثناء Open.
- **الثقة:** High؛ تعارض مصدر حتمي في المسار الطبيعي دون تعديل خارجي.

## AUD-008 — اختبارا تزامن السعة يتشاركان المؤسسة والفترة دون عزل بيانات الفصول
- **التصنيف:** Confirmed defect في عزل اختبارات Integration.
- **الخطورة:** P2.
- **الدليل:** `Academix.Tests/Integration/Phase1ConcurrencyTests.cs:16` يستخدم IClassFixture<ApiFixture>؛ `ApiFixture.cs:10-14,20-38,41-45` ينشئ قاعدة واحدة لعمر fixture ولا يعيدها بين طرق الاختبار. اختبار آخر مقعد ينشئ فصلاً في InitialInstitution للفترة 2027-01-01 إلى 2027-06-30 ويجعله Active في `Phase1ConcurrencyTests.cs:57-63`. اختبار خفض السعة يكرر المؤسسة والفترة في `92-99`. `StructureService.CreateTerm:130-136` يرفض التداخل مع أي فصل غير Closed. لا توجد عملية إغلاق/تنظيف في نهاية أي من الاختبارين.
- **سيناريو الإعادة:** تشغيل class Phase1ConcurrencyTests كاملاً على قاعدة جديدة. أي الاختبارين ينجح أولاً يترك فصله؛ إنشاء الفصل في الثاني يرجع 409 بدلاً من 201 المتوقع في CreatedId:43-47. ترتيب xUnit لا يحل المشكلة.
- **الأثر:** لا يمكن اعتبار اختبارَي التزامن دليلاً مستقلاً عند تشغيلهما معاً؛ قد يفشل الثاني قبل الوصول إلى سباق القفل نفسه.
- **التصحيح المقترح:** مؤسسة مستقلة لكل سيناريو أو fixture/reset معزول بوضوح؛ لا تعتمد ترتيب الاختبارات ولا تغير تواريخ عشوائياً دون ضمان الانفصال.
- **الاختبارات المطلوبة:** تشغيل class كاملاً، وكل test منفرداً، وإعادة التشغيل؛ تحقق من الوصول إلى assertions التزامن واختبار القفل المحكوم.
- **معايير القبول:** نجاح الاختبارين في suite معاً دون ترتيب أو بيانات مشتركة متعارضة، مع استمرار فشل التنفيذ المعيب المفترض لقفل السعة.
- **الثقة:** High؛ لم ينفذ suite المعتمد على Docker في هذه الجلسة.

## فرضيات مفتوحة؛ ليست نتائج مؤكدة إضافية
1. **O-01 — logout لا يبطل Access Token:** `AuthService.cs:73-79` يبطل refresh token المرسل فقط. JWT لا يحتوي FamilyId/sid؛ `ValidateSessionAsync:139-177` لا يقرأ RefreshSessions. لذلك يبقى access صالحاً حتى انتهاء عشر دقائق إذا لم يتغير SecurityStamp. يلزم تحديد عقد logout؛ لا نفترض اشتراط إبطال فوري غير موثق.
2. **O-02 — سباق logout وrefresh:** القفل يخص صف refresh في `RefreshAsync:44-68`؛ logout لا يبطل العائلة. ترتيب: refresh يقفل ويولد descendant ثم logout ينتظر تحديث الصف القديم؛ قد يبقى descendant صالحاً. اختبر بتوقيت محكوم وحدد دلالة logout قبل التصنيف.
3. **O-03 — ذرية Identity/Audit وسحب الجلسات:** `CreateUserAsync:98-103` و`ChangeRoleAsync:118-122` و`ChangeActiveAsync:134-141` تستخدم عدة عمليات حفظ دون معاملة جامعة، وتتجاهل نتيجة UpdateSecurityStampAsync في مساري التغيير. Governance أفضل هنا: معاملة وفحص IdentityResult في `GovernanceService:98-148`. يلزم fault injection ودراسة أثر الفشل قبل تثبيت علة دقيقة.
4. **O-04 — تزامن شروط فتح الشعبة:** `StructureService.UpdateTerm` و`AcademicOperations.UpdateCourse/RemoveStaff/UpdateSection` لا تتشارك كل الأقفال. اختبر إغلاق الفصل/تعطيل المقرر/إزالة المدرس أثناء الفتح. لا تفترض أن قفل الشعبة يحمي صفوفاً أخرى تلقائياً.
5. **O-05 — تداخل الفصول:** CreateTerm وUpdateTerm يجريان فحصاً على صفوف أخرى، مع concurrency token لكل صف فقط ودون exclusion constraint في AcademicConfiguration. افحص migrations كافة ثم اختبر إنشاء/تفعيل فصلين متداخلين بالتزامن.
6. **O-06 — التسجيل الأكاديمي أوسع من prerequisites:** EligibilityForSection لا يتحقق من عضوية البرنامج أو standing أو curriculum أو ساعات العبء، ويستخدم Gradebook منشوراً لا transfer equivalencies. يلزم قراءة Curriculum/Lifecycle كاملاً ومراجعة سياسة المؤسسة قبل وصف قدرة مفقودة.

