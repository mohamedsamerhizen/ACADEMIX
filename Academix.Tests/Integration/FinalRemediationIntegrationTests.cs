using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.AcademicOperations;
using Academix.Api.Domain.Assessment;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Curriculum;
using Academix.Api.Domain.Identity;
using Academix.Api.Domain.Records;
using Academix.Api.DTOs.Assessment;
using Academix.Api.DTOs.Examinations;
using Academix.Api.DTOs.AcademicOperations;
using Academix.Api.Middleware;
using Academix.Api.Services.Academic;
using Academix.Api.Services.AcademicOperations;
using Academix.Api.Services.Assessment;
using Academix.Api.Services.Curriculum;
using Academix.Api.Services.Examinations;
using Academix.Api.Services.Identity;
using Academix.Api.Services.Infrastructure;
using Academix.Api.Services.Institutional;
using Academix.Api.Services.Lifecycle;
using Academix.Api.Services.Records;
using Academix.Api.Services.StudentServices;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Academix.Tests.Integration;

// Real PostgreSQL/Testcontainers scenarios. These are compiled/discovered only while Docker is prohibited.
public sealed class FinalRemediationIntegrationTests(ApiFixture fixture):IClassFixture<ApiFixture>
{
    private sealed record Scenario(Guid Inst,Guid Program,Guid Term,Guid Course,Guid Plan,Guid Section,Guid OtherSection,Guid Student,Guid OtherStudent,Guid AdminId,Guid ReviewerId,Guid Teacher,Guid Room,ClaimsPrincipal Admin,ClaimsPrincipal Reviewer,ClaimsPrincipal StudentUser,ClaimsPrincipal OtherStudentUser);
    private static ClaimsPrincipal Principal(Guid id,Guid inst)=>new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier,id.ToString()),new("institution_id",inst.ToString())],"test"));
    private async Task<Scenario> Setup()
    {
        await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var inst=new Institution{Code=Guid.NewGuid().ToString("N")[..12],Name="Remediation University"};db.Add(inst);await db.SaveChangesAsync();
        var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        async Task<Guid> User(string role){var mail=Guid.NewGuid().ToString("N")+"@example.test";var u=new ApplicationUser{InstitutionId=inst.Id,UserName=mail,Email=mail};Assert.True((await users.CreateAsync(u,"Safe!Remediation2026")).Succeeded);Assert.True((await users.AddToRoleAsync(u,role)).Succeeded);return u.Id;}
        var admin=await User(AcademixRoles.InstitutionAdministrator);var reviewer=await User(AcademixRoles.InstitutionAdministrator);var teacher=await User(AcademixRoles.Instructor);var student=await User(AcademixRoles.Student);var other=await User(AcademixRoles.Student);
        var dept=new Department{InstitutionId=inst.Id,Code="D",Name="Department"};var program=new AcademicProgram{InstitutionId=inst.Id,DepartmentId=dept.Id,Code="P",Name="Program"};
        var term=new AcademicTerm{InstitutionId=inst.Id,Code="T",Name="Term",Status="Active",StartsOn=DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),EndsOn=DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(5))};
        var course=Course.Create(Guid.NewGuid(),inst.Id,"RM101","Recovery principles");var course2=Course.Create(Guid.NewGuid(),inst.Id,"RM102","Scheduling principles");
        var section=new CourseSection{InstitutionId=inst.Id,CourseId=course.Id,TermId=term.Id,Code="A",Status="Open",Capacity=2};var section2=new CourseSection{InstitutionId=inst.Id,CourseId=course2.Id,TermId=term.Id,Code="A",Status="Open",Capacity=2};
        var plan=new CurriculumPlan{InstitutionId=inst.Id,ProgramId=program.Id,CatalogYear=2026,Revision=1,Name="Remediation degree",MinimumTotalCredits=3};
        var building=new CampusBuilding{InstitutionId=inst.Id,Code="B",Name="Building"};var room=new CampusRoom{InstitutionId=inst.Id,BuildingId=building.Id,Code="R",Name="Room",Kind="Classroom",Capacity=2};
        db.AddRange(dept,program,term,course,course2,section,section2,plan,building,room);
        db.CurriculumCourses.Add(new(){InstitutionId=inst.Id,PlanId=plan.Id,CourseId=course.Id,CreditHours=3,RecommendedTerm=1,RequirementKind="Required"});
        db.CurriculumGradeBands.AddRange(new CurriculumGradeBand{InstitutionId=inst.Id,PlanId=plan.Id,MinimumPercentage=0,GradePoints=0,Letter="F"},new CurriculumGradeBand{InstitutionId=inst.Id,PlanId=plan.Id,MinimumPercentage=60,GradePoints=3,Letter="B"});
        foreach(var id in new[]{student,other}){db.StudentProgramMemberships.Add(new(){InstitutionId=inst.Id,StudentId=id,ProgramId=program.Id,AdmissionTermId=term.Id,CurrentTermId=term.Id});db.StudentCurriculumAssignments.Add(new(){InstitutionId=inst.Id,StudentId=id,PlanId=plan.Id,AssignedById=admin});}
        db.TeachingAssignments.Add(new(){InstitutionId=inst.Id,SectionId=section.Id,UserId=teacher,Kind=AcademixRoles.Instructor});
        db.StudentEnrollments.Add(new(){InstitutionId=inst.Id,SectionId=section.Id,StudentId=student});await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<CurriculumService>().Publish(Principal(admin,inst.Id),plan.Id,new(1),default);
        return new(inst.Id,program.Id,term.Id,course.Id,plan.Id,section.Id,section2.Id,student,other,admin,reviewer,teacher,room.Id,Principal(admin,inst.Id),Principal(reviewer,inst.Id),Principal(student,inst.Id),Principal(other,inst.Id));
    }
    private async Task<(Guid Attempt,Guid Exam,Guid Question)> PublishAssessment(Scenario s)
    {
        await using var scope=fixture.Services.CreateAsyncScope();var question=await scope.ServiceProvider.GetRequiredService<QuestionBankService>().Create(s.Admin,s.Course,new("Essay","Explain atomic recovery in detail","Medium",100,[],[],[],null,[]),default);
        var builder=scope.ServiceProvider.GetRequiredService<ExamBuilderService>();var exam=await builder.Create(s.Admin,s.Section,new("Recovery exam","",60,1,DateTime.UtcNow.AddMinutes(-1),DateTime.UtcNow.AddHours(2),false),default);
        exam=await builder.ReplaceQuestions(s.Admin,exam.Exam.Id,new(exam.Exam.Version,[new(question.Question.Id,1,100)]),default);
        await builder.Publish(s.Admin,exam.Exam.Id,new(exam.Exam.Version),default);
        var taking=scope.ServiceProvider.GetRequiredService<ExamTakingService>();var sitting=await taking.Start(s.StudentUser,exam.Exam.Id,default);await taking.Submit(s.StudentUser,sitting.Attempt.Id,default);
        var grading=scope.ServiceProvider.GetRequiredService<ExamGradingService>();var report=await grading.Initialize(s.Admin,sitting.Attempt.Id,default);var q=Assert.Single(report.Questions);
        report=await grading.Grade(s.Admin,sitting.Attempt.Id,q.PublishedQuestionId,new(report.Report.Version,80,"Initial feedback","Initial independent grading"),default);
        await grading.Publish(s.Admin,sitting.Attempt.Id,new(report.Report.Version),default);
        var gradebook=scope.ServiceProvider.GetRequiredService<GradebookService>();var policy=await gradebook.Configure(s.Admin,s.Section,new(50,0),default);
        policy=await gradebook.AddCategory(s.Admin,s.Section,new("Examination",100),default);
        policy=await gradebook.AddComponent(s.Admin,s.Section,new(policy.Categories.Single().Id,"Final exam","Exam",exam.Exam.Id,100,100),default);
        await gradebook.Publish(s.Admin,s.Section,new(policy.Version),default);
        await scope.ServiceProvider.GetRequiredService<RecordsService>().SyncTranscript(s.Admin,s.Section,default);
        return(sitting.Attempt.Id,exam.Exam.Id,q.PublishedQuestionId);
    }
    [Fact] public async Task ExamAppealRequiresReconciliationBeforeRecordsAndDegreeAuditAndPreservesManualOverride()
    {
        var s=await Setup();var exam=await PublishAssessment(s);await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();var grades=scope.ServiceProvider.GetRequiredService<GradebookService>();var grading=scope.ServiceProvider.GetRequiredService<ExamGradingService>();
        var original=await grades.MyResult(s.StudentUser,s.Section,default);Assert.Equal(80,original.Percentage);
        var appeal=await grading.Appeal(s.StudentUser,exam.Attempt,new("Please review the original examination score"),default);var current=await grading.GetStaff(s.Admin,exam.Attempt,default);
        await grading.Resolve(s.Admin,appeal.Id,new(current.Report.Version,true,"Rechecked rubric and corrected the score",exam.Question,40),default);
        var stale=await grades.MyResult(s.StudentUser,s.Section,default);Assert.True(stale.RequiresReconciliation);Assert.Equal(80,stale.Percentage);
        Assert.Equal(80,await db.StudentTranscriptEntries.Where(x=>x.StudentId==s.Student&&x.SectionId==s.Section).Select(x=>x.Percentage).SingleAsync());
        await Assert.ThrowsAsync<ApiFault>(()=>scope.ServiceProvider.GetRequiredService<RecordsService>().SyncTranscript(s.Admin,s.Section,default));
        await Assert.ThrowsAsync<ApiFault>(()=>scope.ServiceProvider.GetRequiredService<DegreeAuditService>().Evaluate(s.Admin,s.Student,default));
        await Assert.ThrowsAsync<ApiFault>(()=>scope.ServiceProvider.GetRequiredService<Academix.Api.Services.Analytics.AnalyticsService>().MySummary(s.StudentUser,default));
        Assert.True((await scope.ServiceProvider.GetRequiredService<Academix.Api.Services.Advising.AdvisingService>().Risks(s.Admin,s.Section,1,default)).Items.Single().RequiresReconciliation);
        db.ProgramCourses.Add(new(){InstitutionId=s.Inst,ProgramId=s.Program,CourseId=s.Course});await db.SaveChangesAsync();
        var quality=scope.ServiceProvider.GetRequiredService<Academix.Api.Services.Quality.QualityService>();var outcome=await quality.SaveOutcome(s.Admin,s.Inst,s.Program,null,new("PLO1","Evaluate safe recovery principles",70),default);
        await quality.MapCourse(s.Admin,s.Inst,outcome.Id,new(s.Course),default);var review=await quality.StartReview(s.Admin,s.Inst,s.Program,s.Term,default);
        await Assert.ThrowsAsync<ApiFault>(()=>quality.Publish(s.Admin,s.Inst,review.Id,default));
        var cmd=new ReconcileGradebookResult(Guid.NewGuid(),"Recalculate","Recalculate after upheld assessment appeal",stale.Version);
        var reconciled=await grades.Reconcile(s.Admin,s.Section,s.Student,cmd,default);Assert.Equal(40,reconciled.Percentage);Assert.False(reconciled.RequiresReconciliation);
        Assert.Equal(reconciled,await grades.Reconcile(s.Admin,s.Section,s.Student,cmd,default));
        await Assert.ThrowsAsync<ApiFault>(()=>grades.Reconcile(s.Admin,s.Section,s.Student,cmd with{Decision="Retain"},default));
        await scope.ServiceProvider.GetRequiredService<RecordsService>().SyncTranscript(s.Admin,s.Section,default);
        Assert.Equal(40,await db.StudentTranscriptEntries.Where(x=>x.StudentId==s.Student&&x.SectionId==s.Section).Select(x=>x.Percentage).SingleAsync());
        var publishedQuality=await quality.Publish(s.Admin,s.Inst,review.Id,default);Assert.Equal("ProxyBelowTarget",Assert.Single(publishedQuality.Evidence).AssessmentStatus);
        var over=await grades.Correct(s.Admin,s.Section,s.Student,new(75,"Official moderation override with justification",reconciled.Version),default);Assert.True(over.IsManualOverride);
        current=await grading.GetStaff(s.Admin,exam.Attempt,default);await grading.Grade(s.Admin,exam.Attempt,exam.Question,new(current.Report.Version,20,"Second correction","Second assessment correction"),default);
        stale=await grades.MyResult(s.StudentUser,s.Section,default);
        await Assert.ThrowsAsync<ApiFault>(()=>grades.Reconcile(s.Admin,s.Section,s.Student,new(Guid.NewGuid(),"Recalculate","Must preserve official override",stale.Version),default));
        var retained=await grades.Reconcile(s.Admin,s.Section,s.Student,new(Guid.NewGuid(),"Retain","Exam change reviewed; retain moderation override",stale.Version),default);Assert.Equal(75,retained.Percentage);Assert.True(retained.IsManualOverride);
        await Assert.ThrowsAsync<ApiFault>(()=>grades.Reconcile(s.StudentUser,s.Section,s.Student,new(Guid.NewGuid(),"Retain","Student cannot reconcile a result",retained.Version),default));
        Assert.Equal(2,await db.GradebookAudits.CountAsync(x=>x.InstitutionId==s.Inst&&x.Action=="SourceChanged"));
    }
    [Fact] public async Task IssuedGraduationPdfBecomesSourceChangedAndReissuanceRetainsOriginalArtifact()
    {
        var s=await Setup();var unrelated=await Setup();var exam=await PublishAssessment(s);await using var scope=fixture.Services.CreateAsyncScope();var lifecycle=scope.ServiceProvider.GetRequiredService<LifecycleService>();var docs=scope.ServiceProvider.GetRequiredService<InstitutionalDocumentService>();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var app=await lifecycle.SubmitGraduation(s.StudentUser,new(Guid.NewGuid()),default);app=await lifecycle.ReviewGraduation(s.Admin,app.Id,new(app.Version,"Approve","Authoritative degree audit reviewed"),default);app=await lifecycle.IssueGraduation(s.Admin,app.Id,new(app.Version),default);
        var request=await docs.SubmitApplication(s.StudentUser,new(Guid.NewGuid(),"Graduation","Official graduation evidence"),default);request=await docs.Review(s.Reviewer,request.Id,new(request.Version,true,"Registrar approval for official issuance"),default);var doc=await docs.Issue(s.Admin,request.Id,new(request.Version),default);
        async Task<Academix.Api.DTOs.Institutional.AcademicDocumentView> EnrollmentDocument(Scenario owner,ClaimsPrincipal student)
        {var r=await docs.SubmitApplication(student,new(Guid.NewGuid(),"Enrollment","Unaffected enrollment evidence"),default);r=await docs.Review(owner.Reviewer,r.Id,new(r.Version,true,"Independent enrollment approval"),default);return await docs.Issue(owner.Admin,r.Id,new(r.Version),default);}
        var otherDocument=await EnrollmentDocument(s,s.OtherStudentUser);var foreignDocument=await EnrollmentDocument(unrelated,unrelated.StudentUser);
        var bytes=(await docs.Download(s.StudentUser,doc.Id,default)).Bytes;Assert.True((await docs.Verify(new(doc.ReferenceNumber,doc.VerificationProof),default)).Valid);
        var grades=scope.ServiceProvider.GetRequiredService<GradebookService>();var result=await grades.MyResult(s.StudentUser,s.Section,default);await grades.Correct(s.Admin,s.Section,s.Student,new(70,"Authorized correction after academic review",result.Version),default);
        Assert.NotNull(await db.GraduationApplications.Where(x=>x.Id==app.Id).Select(x=>x.SourceInvalidatedAtUtc).SingleAsync());
        Assert.False((await docs.Verify(new(doc.ReferenceNumber,doc.VerificationProof),default)).Valid);Assert.Equal(bytes,(await docs.Download(s.StudentUser,doc.Id,default)).Bytes);
        Assert.True((await docs.Verify(new(otherDocument.ReferenceNumber,otherDocument.VerificationProof),default)).Valid);
        Assert.True((await docs.Verify(new(foreignDocument.ReferenceNumber,foreignDocument.VerificationProof),default)).Valid);
        Assert.False(await db.IssuedAcademicDocuments.AnyAsync(x=>x.InstitutionId==unrelated.Inst&&x.SourceInvalidatedAtUtc!=null));
        await scope.ServiceProvider.GetRequiredService<RecordsService>().SyncTranscript(s.Admin,s.Section,default);await lifecycle.ReassessCredential(s.Admin,s.Inst,app.Id,"Credential eligibility independently reassessed",default);
        var replacement=await docs.RequestReissuance(s.StudentUser,doc.Id,new(Guid.NewGuid(),"Replace source-changed graduation PDF"),default);replacement=await docs.Review(s.Reviewer,replacement.Id,new(replacement.Version,true,"Reissuance checked after reassessment"),default);var issued=await docs.Issue(s.Admin,replacement.Id,new(replacement.Version),default);
        Assert.True((await docs.Verify(new(issued.ReferenceNumber,issued.VerificationProof),default)).Valid);Assert.False((await docs.Verify(new(doc.ReferenceNumber,doc.VerificationProof),default)).Valid);
        Assert.Equal(bytes,(await docs.Download(s.StudentUser,doc.Id,default)).Bytes);
    }
    [Fact] public async Task ExistingPhysicalSeatsAndTimetablesConstrainLaterIndividualAndBatchEnrollment()
    {
        var s=await Setup();await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        // Published exam setup is independent of course grade publication, which closes registration.
        var exam=new Academix.Api.Domain.Examinations.ExamDefinition{InstitutionId=s.Inst,CourseId=s.Course,SectionId=s.Section,CreatedBy=s.AdminId,Title="Venue exam",Status="Published",DurationMinutes=60,MaxAttempts=1,OpensAtUtc=DateTime.UtcNow.AddDays(2),ClosesAtUtc=DateTime.UtcNow.AddDays(3)};db.Exams.Add(exam);await db.SaveChangesAsync();
        var venues=scope.ServiceProvider.GetRequiredService<CampusReservationService>();var start=DateTime.UtcNow.AddDays(2);var venue=await venues.Create(s.Admin,s.Inst,new(Guid.NewGuid(),s.Room,"Exam",start,start.AddHours(1),"One reserved seat",exam.Id,s.Teacher,1),default);
        var academic=scope.ServiceProvider.GetRequiredService<AcademicOperations>();await Assert.ThrowsAsync<ApiFault>(()=>academic.Enroll(s.Admin,s.Section,new(s.OtherStudent),default));
        await venues.Cancel(s.Admin,s.Inst,venue.Id,new(venue.Version,"Move exam before additional enrollment"),default);
        var teaching=scope.ServiceProvider.GetRequiredService<TeachingOperationsService>();await teaching.SaveSlot(s.Admin,s.Section,null,new(1,540,60,"Room",null,s.Room),default);
        await Assert.ThrowsAsync<ApiFault>(()=>academic.UpdateSection(s.Admin,s.Section,new(3,"Open",1),default));
        await teaching.SaveSlot(s.Admin,s.OtherSection,null,new(1,570,60,"Separate room"),default);
        await academic.Enroll(s.Admin,s.OtherSection,new(s.OtherStudent),default);
        await Assert.ThrowsAsync<ApiFault>(()=>academic.Enroll(s.Admin,s.Section,new(s.OtherStudent),default));
        var governance=scope.ServiceProvider.GetRequiredService<Academix.Api.Services.Governance.GovernanceService>();
        var batch=await governance.CreateBatch(s.Admin,s.Inst,new(Guid.NewGuid(),s.Section,[s.OtherStudent]),default);
        await Assert.ThrowsAsync<ApiFault>(()=>governance.CommitBatch(s.Admin,s.Inst,batch.Id,default));
        Assert.False(await db.StudentEnrollments.AnyAsync(x=>x.SectionId==s.Section&&x.StudentId==s.OtherStudent));
        var slot=(await teaching.Slots(s.Admin,s.OtherSection,default)).Single();await teaching.SaveSlot(s.Admin,s.OtherSection,slot.Id,new(1,660,60,"Separate room",slot.Version),default);
        await academic.Enroll(s.Admin,s.Section,new(s.OtherStudent),default);Assert.Equal(2,await db.StudentEnrollments.CountAsync(x=>x.SectionId==s.Section&&x.Status=="Active"));
    }
    [Theory]
    [InlineData("Reservation","Role")]
    [InlineData("Reservation","Disabled")]
    [InlineData("Reservation","Membership")]
    [InlineData("Building","Role")]
    [InlineData("Grant","Role")]
    public async Task WaitingProtectedMutationRevalidatesAuthorityAfterInstitutionLock(string operation,string revocation)
    {
        var s=await Setup();await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();var connectionString=db.Database.GetConnectionString()!;
        await using var blocker=new NpgsqlConnection(connectionString);await blocker.OpenAsync();await using var held=await blocker.BeginTransactionAsync();
        await using(var command=new NpgsqlCommand("SELECT 1 FROM institutions WHERE \"Id\"=@id FOR UPDATE",blocker,held)){command.Parameters.AddWithValue("id",s.Inst);await command.ExecuteScalarAsync();}
        async Task Mutate(){await using var own=fixture.Services.CreateAsyncScope();if(operation=="Reservation")await own.ServiceProvider.GetRequiredService<CampusReservationService>().Create(s.Admin,s.Inst,new(Guid.NewGuid(),s.Room,"Maintenance",DateTime.UtcNow.AddDays(3),DateTime.UtcNow.AddDays(3).AddHours(1),"Controlled revocation race"),default);else if(operation=="Building")await own.ServiceProvider.GetRequiredService<CampusFacilitiesService>().SaveBuilding(s.Admin,s.Inst,null,new("NEW","New building"),default);else await own.ServiceProvider.GetRequiredService<StudentServiceCaseService>().SetStaffGrant(s.Admin,s.Inst,s.Teacher,"Library",new(true),default);}
        var pending=Mutate();await using var observer=new NpgsqlConnection(connectionString);await observer.OpenAsync();var limit=DateTime.UtcNow.AddSeconds(10);bool waiting=false;
        while(DateTime.UtcNow<limit){await using var query=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE @blocker=ANY(pg_blocking_pids(pid)))",observer);query.Parameters.AddWithValue("blocker",blocker.ProcessID);waiting=(bool)(await query.ExecuteScalarAsync())!;if(waiting)break;await Task.Delay(25);}
        Assert.True(waiting,"Mutation must demonstrably wait on the held PostgreSQL lock, not a timer assumption.");
        var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();var actor=(await users.FindByIdAsync(s.AdminId.ToString()))!;
        if(revocation=="Role")Assert.True((await users.RemoveFromRoleAsync(actor,AcademixRoles.InstitutionAdministrator)).Succeeded);
        else {if(revocation=="Disabled")actor.IsActive=false;else actor.InstitutionId=null;Assert.True((await users.UpdateAsync(actor)).Succeeded);}
        await held.CommitAsync();await Assert.ThrowsAsync<ApiFault>(()=>pending);
        Assert.False(await db.CampusRoomReservations.AnyAsync(x=>x.InstitutionId==s.Inst));Assert.False(await db.CampusBuildings.AnyAsync(x=>x.InstitutionId==s.Inst&&x.Code=="NEW"));Assert.False(await db.Set<Academix.Api.Domain.StudentServices.StudentServiceStaffGrant>().AnyAsync(x=>x.InstitutionId==s.Inst));
        await scope.ServiceProvider.GetRequiredService<CampusFacilitiesService>().SaveBuilding(s.Reviewer,s.Inst,null,new("OK","Authorized building"),default);
    }
    [Fact] public async Task FailureDuringSourceInvalidationRollsBackTheAssessmentAndAllDependentMarkers()
    {
        var s=await Setup();var exam=await PublishAssessment(s);await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();var grading=scope.ServiceProvider.GetRequiredService<ExamGradingService>();var original=await grading.GetStaff(s.Admin,exam.Attempt,default);
        var suffix=Guid.NewGuid().ToString("N");var function="remediation_atomic_"+suffix;var trigger="remediation_atomic_trigger_"+suffix;
        await using var connection=new NpgsqlConnection(db.Database.GetConnectionString());await connection.OpenAsync();
        await using(var command=new NpgsqlCommand($"CREATE FUNCTION {function}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.\"InstitutionId\"='{s.Inst}'::uuid AND NEW.\"Action\"='SourceChanged' THEN RAISE EXCEPTION 'injected atomicity failure' USING ERRCODE='23514'; END IF; RETURN NEW; END $$; CREATE TRIGGER {trigger} BEFORE INSERT ON gradebook_audit FOR EACH ROW EXECUTE FUNCTION {function}();",connection))await command.ExecuteNonQueryAsync();
        try
        {
            await Assert.ThrowsAnyAsync<Exception>(()=>grading.Grade(s.Admin,exam.Attempt,exam.Question,new(original.Report.Version,10,"Correction","Atomic correction must roll back"),default));
            Assert.Equal(original.Report,(await grading.GetStaff(s.Admin,exam.Attempt,default)).Report);
            Assert.False(await db.GradebookResults.Where(x=>x.SectionId==s.Section&&x.StudentId==s.Student).Select(x=>x.RequiresReconciliation).SingleAsync());
            Assert.False(await db.GradebookAudits.AnyAsync(x=>x.InstitutionId==s.Inst&&x.Action=="SourceChanged"));
        }
        finally{await using var command=new NpgsqlCommand($"DROP TRIGGER {trigger} ON gradebook_audit; DROP FUNCTION {function}();",connection);await command.ExecuteNonQueryAsync();}
        await grading.Grade(s.Admin,exam.Attempt,exam.Question,new(original.Report.Version,10,"Correction","Retry after database fault cleared"),default);
        Assert.True(await db.GradebookResults.Where(x=>x.SectionId==s.Section&&x.StudentId==s.Student).Select(x=>x.RequiresReconciliation).SingleAsync());
    }

    [Fact] public async Task ConcurrentReservationAndEnrollmentCannotCommitInsufficientSeats()
    {
        var s=await Setup();await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var exam=new Academix.Api.Domain.Examinations.ExamDefinition{InstitutionId=s.Inst,CourseId=s.Course,SectionId=s.Section,CreatedBy=s.AdminId,Title="Concurrent venue",Status="Published",DurationMinutes=60,MaxAttempts=1,OpensAtUtc=DateTime.UtcNow.AddDays(2),ClosesAtUtc=DateTime.UtcNow.AddDays(3)};db.Exams.Add(exam);await db.SaveChangesAsync();
        async Task<int> Enroll(){await using var own=fixture.Services.CreateAsyncScope();try{await own.ServiceProvider.GetRequiredService<AcademicOperations>().Enroll(s.Admin,s.Section,new(s.OtherStudent),default);return 200;}catch(ApiFault e){return e.StatusCode;}}
        async Task<int> Reserve(){await using var own=fixture.Services.CreateAsyncScope();try{var start=DateTime.UtcNow.AddDays(2);await own.ServiceProvider.GetRequiredService<CampusReservationService>().Create(s.Admin,s.Inst,new(Guid.NewGuid(),s.Room,"Exam",start,start.AddHours(1),"One available physical seat",exam.Id,s.Teacher,1),default);return 200;}catch(ApiFault e){return e.StatusCode;}}
        var outcome=await Task.WhenAll(Enroll(),Reserve());Assert.Equal(new[]{200,409},outcome.Order().ToArray());
        var count=await db.StudentEnrollments.CountAsync(x=>x.InstitutionId==s.Inst&&x.SectionId==s.Section&&x.Status=="Active");var seats=await db.CampusRoomReservations.Where(x=>x.InstitutionId==s.Inst&&x.SectionId==s.Section&&x.Status=="Active").Select(x=>x.SeatCount).SingleOrDefaultAsync();if(seats.HasValue)Assert.True(count<=seats.Value);
    }
    [Fact] public async Task LearningCleanupIntentSurvivesRollbackAndNeverDeletesReferencedUploads()
    {
        var s=await Setup();await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();var store=scope.ServiceProvider.GetRequiredService<TestArtifactStore>();var cleanup=scope.ServiceProvider.GetRequiredService<Academix.Api.Services.Learning.LearningArtifactCleanup>();
        var suffix=Guid.NewGuid().ToString("N");var function="remediation_upload_"+suffix;var trigger="remediation_upload_trigger_"+suffix;
        await using var connection=new NpgsqlConnection(db.Database.GetConnectionString());await connection.OpenAsync();
        await using(var command=new NpgsqlCommand($"CREATE FUNCTION {function}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.\"InstitutionId\"='{s.Inst}'::uuid THEN RAISE EXCEPTION 'post-upload database fault' USING ERRCODE='23514'; END IF; RETURN NEW; END $$; CREATE TRIGGER {trigger} BEFORE INSERT ON lms_files FOR EACH ROW EXECUTE FUNCTION {function}();",connection))await command.ExecuteNonQueryAsync();
        try
        {
            using var failedContent=new MemoryStream("Uploaded before database failure"u8.ToArray());var failedFile=new Microsoft.AspNetCore.Http.FormFile(failedContent,0,failedContent.Length,"file","failed.txt"){Headers=new Microsoft.AspNetCore.Http.HeaderDictionary(),ContentType="text/plain"};
            await Assert.ThrowsAsync<DbUpdateException>(()=>scope.ServiceProvider.GetRequiredService<Academix.Api.Services.Learning.LearningOperations>().Upload(s.StudentUser,s.Section,failedFile,default));
        }
        finally{await using var command=new NpgsqlCommand($"DROP TRIGGER {trigger} ON lms_files; DROP FUNCTION {function}();",connection);await command.ExecuteNonQueryAsync();}
        Assert.False(await db.LearningFiles.AnyAsync(x=>x.InstitutionId==s.Inst));
        var payload=await db.InstitutionalOutboxEvents.Where(x=>x.InstitutionId==s.Inst&&x.Kind=="LearningArtifactCleanup").Select(x=>x.PayloadJson).SingleAsync();
        var orphan=System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(payload).GetProperty("FileId").GetGuid();var key=Academix.Api.Services.Learning.LearningArtifactCleanup.Key(s.Inst,s.Section,orphan);Assert.True(store.Contains(key));
        Assert.True(await db.InstitutionalOutboxEvents.AnyAsync(x=>x.InstitutionId==s.Inst&&x.Kind=="LearningArtifactCleanup"));
        var outbox=scope.ServiceProvider.GetRequiredService<OutboxService>();store.FailRemoveKey=key;
        await outbox.ProcessBatch(default);Assert.True(store.Contains(key));
        var intent=await db.InstitutionalOutboxEvents.AsNoTracking().SingleAsync(x=>x.InstitutionId==s.Inst&&x.Kind=="LearningArtifactCleanup");Assert.Equal("Retry",intent.Status);
        store.FailRemoveKey=null;await outbox.Control(s.Admin,s.Inst,intent.Id,false,default);await outbox.ProcessBatch(default);Assert.False(store.Contains(key));
        using var content=new MemoryStream("Submitted evidence"u8.ToArray());var file=new Microsoft.AspNetCore.Http.FormFile(content,0,content.Length,"file","evidence.txt"){Headers=new Microsoft.AspNetCore.Http.HeaderDictionary(),ContentType="text/plain"};
        await scope.ServiceProvider.GetRequiredService<Academix.Api.Services.Learning.LearningOperations>().Upload(s.StudentUser,s.Section,file,default);
        var posted=await db.LearningFiles.AsNoTracking().SingleAsync(x=>x.InstitutionId==s.Inst);
        await outbox.ProcessBatch(default);Assert.True(store.Contains(posted.ObjectKey));
        await Assert.ThrowsAsync<ApiFault>(()=>scope.ServiceProvider.GetRequiredService<Academix.Api.Services.Learning.LearningOperations>().Download(s.OtherStudentUser,posted.Id,default));
    }

    [Fact] public async Task IdenticalTimetableAndRoomCodesInDifferentInstitutionsRemainIndependent()
    {
        var first=await Setup();var other=await Setup();await using var scope=fixture.Services.CreateAsyncScope();var teaching=scope.ServiceProvider.GetRequiredService<TeachingOperationsService>();var academic=scope.ServiceProvider.GetRequiredService<AcademicOperations>();
        await teaching.SaveSlot(first.Admin,first.Section,null,new(1,540,60,"Room",null,first.Room),default);
        await teaching.SaveSlot(other.Admin,other.Section,null,new(1,540,60,"Room",null,other.Room),default);
        await academic.Enroll(first.Admin,first.Section,new(first.OtherStudent),default);await academic.Enroll(other.Admin,other.Section,new(other.OtherStudent),default);
        await Assert.ThrowsAsync<ApiFault>(()=>academic.Enroll(other.Admin,first.OtherSection,new(first.OtherStudent),default));
        var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();Assert.Equal(2,await db.StudentEnrollments.CountAsync(x=>x.InstitutionId==first.Inst&&x.SectionId==first.Section));Assert.Equal(2,await db.StudentEnrollments.CountAsync(x=>x.InstitutionId==other.Inst&&x.SectionId==other.Section));
    }

}
