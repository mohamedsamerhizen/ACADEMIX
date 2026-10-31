using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.AcademicOperations;
using Academix.Api.Domain.Assessment;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Curriculum;
using Academix.Api.Domain.Identity;
using Academix.Api.Domain.Institutional;
using Academix.Api.Domain.Records;
using Academix.Api.Domain.Registrar;
using Academix.Api.DTOs.AcademicOperations;
using Academix.Api.DTOs.Institutional;
using Academix.Api.DTOs.Registrar;
using Academix.Api.Middleware;
using Academix.Api.Services.Academic;
using Academix.Api.Services.AcademicOperations;
using Academix.Api.Services.Curriculum;
using Academix.Api.Services.Infrastructure;
using Academix.Api.Services.Institutional;
using Academix.Api.Services.Records;
using Academix.Api.Services.Registrar;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academix.Tests.Integration;

public sealed partial class Phase19CompletionWorkflowTests(ApiFixture fixture):IClassFixture<ApiFixture>
{
    private sealed record Scenario(Guid Inst,Guid Program,Guid Plan,Guid Course,Guid Term,Guid Section,Guid Student,
        Guid AdminId,Guid ReviewerId,ClaimsPrincipal Admin,ClaimsPrincipal Reviewer,ClaimsPrincipal StudentUser);
    private static ClaimsPrincipal Principal(Guid id,Guid inst)=>new(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,id.ToString()),new Claim("institution_id",inst.ToString())},"test"));
    private async Task<Scenario> Setup()
    {
        await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var inst=new Institution{Code=Guid.NewGuid().ToString("N")[..15],Name="Completion University"};db.Institutions.Add(inst);await db.SaveChangesAsync();
        var manager=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        async Task<Guid> User(string role)
        {
            var email=Guid.NewGuid().ToString("N")+"@example.test";var user=new ApplicationUser{Id=Guid.NewGuid(),InstitutionId=inst.Id,UserName=email,Email=email};
            Assert.True((await manager.CreateAsync(user,"Safe!CompletionTest2026")).Succeeded);Assert.True((await manager.AddToRoleAsync(user,role)).Succeeded);return user.Id;
        }
        var admin=await User(AcademixRoles.InstitutionAdministrator);var reviewer=await User(AcademixRoles.InstitutionAdministrator);var student=await User(AcademixRoles.Student);
        var dept=new Department{InstitutionId=inst.Id,Code="D",Name="Department"};var program=new AcademicProgram{InstitutionId=inst.Id,DepartmentId=dept.Id,Code="P",Name="Program"};
        var term=new AcademicTerm{InstitutionId=inst.Id,Code="T",Name="Term",Status="Active",StartsOn=new(2026,1,1),EndsOn=new(2028,1,1)};
        var course=Course.Create(Guid.NewGuid(),inst.Id,"CS101","Foundations");var section=new CourseSection{InstitutionId=inst.Id,TermId=term.Id,CourseId=course.Id,Code="A",Status="Open",Capacity=30};
        var plan=new CurriculumPlan{InstitutionId=inst.Id,ProgramId=program.Id,CatalogYear=2026,Revision=1,Name="Original",Status="Published",PublishedAtUtc=DateTime.UtcNow,MinimumTotalCredits=3};
        db.Departments.Add(dept);db.AcademicPrograms.Add(program);db.AcademicTerms.Add(term);db.Courses.Add(course);db.CourseSections.Add(section);db.CurriculumPlans.Add(plan);
        db.CurriculumCourses.Add(new(){InstitutionId=inst.Id,PlanId=plan.Id,CourseId=course.Id,RequirementKind="Required",CreditHours=3,RecommendedTerm=1});
        db.CurriculumGradeBands.AddRange(new CurriculumGradeBand{InstitutionId=inst.Id,PlanId=plan.Id,MinimumPercentage=0,GradePoints=0,Letter="F"},new CurriculumGradeBand{InstitutionId=inst.Id,PlanId=plan.Id,MinimumPercentage=60,GradePoints=3,Letter="B"});
        db.StudentProgramMemberships.Add(new(){InstitutionId=inst.Id,StudentId=student,ProgramId=program.Id,AdmissionTermId=term.Id,CurrentTermId=term.Id});
        db.StudentCurriculumAssignments.Add(new(){InstitutionId=inst.Id,StudentId=student,PlanId=plan.Id,AssignedById=admin});await db.SaveChangesAsync();
        return new(inst.Id,program.Id,plan.Id,course.Id,term.Id,section.Id,student,admin,reviewer,Principal(admin,inst.Id),Principal(reviewer,inst.Id),Principal(student,inst.Id));
    }
    private async Task<AcademicDocumentView> Issue(Scenario s,string kind="Enrollment",Guid? supersedes=null)
    {
        await using var scope=fixture.Services.CreateAsyncScope();var docs=scope.ServiceProvider.GetRequiredService<InstitutionalDocumentService>();
        var request=await docs.SubmitApplication(s.StudentUser,new(Guid.NewGuid(),kind,"Official academic confirmation",supersedes),default);
        await docs.Review(s.Admin,request.Id,new(request.Version,true,"Academic record reviewed"),default);
        return await docs.Issue(s.Admin,request.Id,new(2),default);
    }

    [Fact]
    public async Task ApplicantAcceptanceProvisionsExactlyOnceAndRejectsPrematureOrCrossTenantAccess()
    {
        var s=await Setup();var foreign=await Setup();
        await using var scope=fixture.Services.CreateAsyncScope();var service=scope.ServiceProvider.GetRequiredService<ApplicantService>();
        var email=Guid.NewGuid().ToString("N")+"@example.test";var input=new ApplicantInvitation(Guid.NewGuid(),email,"New Applicant",s.Program,s.Term,"Application statement for admission.");
        var invite=await service.Invite(s.Admin,s.Inst,input,default);
        Assert.Equal(invite,await service.Invite(s.Admin,s.Inst,input,default));
        Assert.Equal(409,(await Assert.ThrowsAsync<ApiFault>(()=>service.Activate(new(invite.Token,"Safe!ApplicantPass2026"),default))).StatusCode);
        var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();Assert.False(await db.Users.AnyAsync(x=>x.NormalizedEmail==email.ToUpperInvariant()));
        var submitted=await service.Access(new(invite.Token),default);Assert.Equal("Submitted",submitted.Status);
        Assert.Equal(403,(await Assert.ThrowsAsync<ApiFault>(()=>service.Review(foreign.Admin,s.Inst,submitted.Id,new(submitted.Version,true,"Verified applicant documents"),default))).StatusCode);
        Assert.Equal(403,(await Assert.ThrowsAsync<ApiFault>(()=>service.Review(s.Admin,s.Inst,submitted.Id,new(submitted.Version,true,"Verified applicant documents"),default))).StatusCode);
        await service.Review(s.Reviewer,s.Inst,submitted.Id,new(submitted.Version,true,"Verified applicant documents"),default);
        async Task<ApplicantView> Activate(){await using var own=fixture.Services.CreateAsyncScope();return await own.ServiceProvider.GetRequiredService<ApplicantService>().Activate(new(invite.Token,"Safe!ApplicantPass2026"),default);}
        var converted=await Task.WhenAll(Activate(),Activate());Assert.Equal(converted[0].StudentId,converted[1].StudentId);
        Assert.Equal(1,await db.Users.CountAsync(x=>x.NormalizedEmail==email.ToUpperInvariant()));
        var student=await db.Users.SingleAsync(x=>x.Id==converted[0].StudentId);Assert.True(student.EmailConfirmed);
        Assert.True(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().IsInRoleAsync(student,AcademixRoles.Student));
        Assert.Equal(1,await db.StudentProgramMemberships.CountAsync(x=>x.StudentId==student.Id&&x.ProgramId==s.Program));
        Assert.Equal(new[]{"Invited","Submitted","Accepted","Converted"},(await service.History(s.Admin,s.Inst,invite.ApplicantId,default)).Select(x=>x.Status));
    }
    [Fact]
    public async Task RejectedApplicantAndConflictingEmailNeverGainStudentPrivileges()
    {
        var s=await Setup();await using var scope=fixture.Services.CreateAsyncScope();var service=scope.ServiceProvider.GetRequiredService<ApplicantService>();
        var invite=await service.Invite(s.Admin,s.Inst,new(Guid.NewGuid(),Guid.NewGuid().ToString("N")+"@example.test","Rejected Applicant",s.Program,s.Term,"Application with missing prerequisites."),default);
        var row=await service.Access(new(invite.Token),default);await service.Review(s.Reviewer,s.Inst,row.Id,new(row.Version,false,"Admission evidence insufficient"),default);
        Assert.Equal(409,(await Assert.ThrowsAsync<ApiFault>(()=>service.Activate(new(invite.Token,"Safe!ApplicantPass2026"),default))).StatusCode);
        var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();Assert.Null((await db.Set<Applicant>().AsNoTracking().SingleAsync(x=>x.Id==row.Id)).StudentId);
        var email=(await db.Users.SingleAsync(x=>x.Id==s.Student)).Email!;
        Assert.Equal(409,(await Assert.ThrowsAsync<ApiFault>(()=>service.Invite(s.Admin,s.Inst,new(Guid.NewGuid(),email,"Duplicate Applicant",s.Program,s.Term,"Duplicate account must not be created."),default))).StatusCode);
    }
    [Fact]
    public async Task PdfIssueSurvivesKeyRotationAndChecksStorageOwnershipRevocationAndSupersession()
    {
        var s=await Setup();var outsider=await Setup();var doc=await Issue(s);
        await using var scope=fixture.Services.CreateAsyncScope();var docs=scope.ServiceProvider.GetRequiredService<InstitutionalDocumentService>();
        var pdf=await docs.Download(s.StudentUser,doc.Id,default);Assert.Equal("%PDF-",System.Text.Encoding.ASCII.GetString(pdf.Bytes,0,5));Assert.Equal(doc.ArtifactDigest,pdf.Digest);
        await docs.RotateKey(s.Admin,s.Inst,default);Assert.True((await docs.Verify(new(doc.ReferenceNumber,doc.VerificationProof),default)).Valid);
        Assert.Equal(403,(await Assert.ThrowsAsync<ApiFault>(()=>docs.Download(outsider.StudentUser,doc.Id,default))).StatusCode);
        var replacement=await Issue(s,"Enrollment",doc.Id);
        Assert.Equal("Superseded",(await docs.Verify(new(doc.ReferenceNumber,doc.VerificationProof),default)).Status);
        Assert.True((await docs.Verify(new(replacement.ReferenceNumber,replacement.VerificationProof),default)).Valid);
        await docs.Revoke(s.Admin,replacement.Id,new(replacement.Version,"Document withdrawn by registrar"),default);
        Assert.Equal("Revoked",(await docs.Verify(new(replacement.ReferenceNumber,replacement.VerificationProof),default)).Status);
    }
    [Fact]
    public async Task StorageFailureRollsBackIssuanceAndCorruptionCannotPassVerification()
    {
        var s=await Setup();await using var scope=fixture.Services.CreateAsyncScope();var docs=scope.ServiceProvider.GetRequiredService<InstitutionalDocumentService>();
        var request=await docs.SubmitApplication(s.StudentUser,new(Guid.NewGuid(),"Enrollment","Storage failure recovery evidence"),default);await docs.Review(s.Admin,request.Id,new(1,true,"Identity and enrollment validated"),default);
        var store=scope.ServiceProvider.GetRequiredService<TestArtifactStore>();store.FailNextPut=1;
        await Assert.ThrowsAsync<IOException>(()=>docs.Issue(s.Admin,request.Id,new(2),default));
        var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();Assert.False(await db.IssuedAcademicDocuments.AnyAsync(x=>x.RequestId==request.Id));
        Assert.Equal("Approved",await db.InstitutionalDocumentRequests.Where(x=>x.Id==request.Id).Select(x=>x.Status).SingleAsync());
        var doc=await docs.Issue(s.Admin,request.Id,new(2),default);Assert.Equal(doc.Id,(await docs.Issue(s.Admin,request.Id,new(2),default)).Id);
        var key=await db.IssuedAcademicDocuments.Where(x=>x.Id==doc.Id).Select(x=>x.ArtifactKey).SingleAsync();store.Corrupt(key!);
        Assert.Equal("IntegrityFailure",(await docs.Verify(new(doc.ReferenceNumber,doc.VerificationProof),default)).Status);
        Assert.Equal(409,(await Assert.ThrowsAsync<ApiFault>(()=>docs.Download(s.StudentUser,doc.Id,default))).StatusCode);
    }
    [Fact]
    public async Task AttendanceExcuseCompletesOnlyWhenItsSingleBusinessEffectCommits()
    {
        var s=await Setup();await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        db.StudentEnrollments.Add(new(){InstitutionId=s.Inst,SectionId=s.Section,StudentId=s.Student});
        var session=new ClassSession{InstitutionId=s.Inst,SectionId=s.Section,StartsAtUtc=DateTime.UtcNow.AddHours(-2),EndsAtUtc=DateTime.UtcNow.AddHours(-1),Topic="Seminar",Status="Completed"};
        db.ClassSessions.Add(session);db.AttendanceRecords.Add(new(){InstitutionId=s.Inst,SectionId=s.Section,SessionId=session.Id,StudentId=s.Student,Status="Absent",RecordedBy=s.AdminId});await db.SaveChangesAsync();
        var student=scope.ServiceProvider.GetRequiredService<StudentAcademicOperationsService>();var registrar=scope.ServiceProvider.GetRequiredService<RegistrarOperationsService>();
        var input=new AcademicRequestCreate(s.Section,"AttendanceExcuse","Absence excuse","Medical evidence was provided to the registrar.",Guid.NewGuid(),new(session.Id,null,null,null));
        var request=await student.CreateRequest(s.StudentUser,input,default);Assert.Equal(request.Id,(await student.CreateRequest(s.StudentUser,input,default)).Id);
        var review=await registrar.BeginReview(s.Admin,s.Inst,request.Id,null,default);Assert.Equal("UnderReview",review.Status);
        var approved=await registrar.Decide(s.Admin,s.Inst,request.Id,new("Approved","Verified supporting medical evidence",review.Version),default);
        Assert.Equal("Absent",await db.AttendanceRecords.Where(x=>x.SessionId==session.Id).Select(x=>x.Status).SingleAsync());
        var completed=await registrar.Fulfill(s.Admin,s.Inst,request.Id,new(approved.Version),default);Assert.Equal("Completed",completed.Status);
        Assert.Equal(completed.Version,(await registrar.Fulfill(s.Admin,s.Inst,request.Id,new(approved.Version),default)).Version);
        Assert.Equal("Excused",await db.AttendanceRecords.Where(x=>x.SessionId==session.Id).Select(x=>x.Status).SingleAsync());
        Assert.Equal(1,await db.AcademicRequestHistories.CountAsync(x=>x.RequestId==request.Id&&x.ToStatus=="Completed"));
    }
    [Fact]
    public async Task MissingAttendanceAndCrossTenantExecutionNeverCompleteAnApprovedRequest()
    {
        var s=await Setup();var other=await Setup();await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var session=new ClassSession{InstitutionId=s.Inst,SectionId=s.Section,StartsAtUtc=DateTime.UtcNow.AddHours(-2),EndsAtUtc=DateTime.UtcNow.AddHours(-1),Topic="Seminar",Status="Completed"};
        db.ClassSessions.Add(session);db.StudentEnrollments.Add(new(){InstitutionId=s.Inst,SectionId=s.Section,StudentId=s.Student});await db.SaveChangesAsync();
        var student=scope.ServiceProvider.GetRequiredService<StudentAcademicOperationsService>();var registrar=scope.ServiceProvider.GetRequiredService<RegistrarOperationsService>();
        var req=await student.CreateRequest(s.StudentUser,new(s.Section,"AttendanceExcuse","Unrecorded absence","Attendance has not yet been recorded.",Guid.NewGuid(),new(session.Id,null,null,null)),default);
        var reviewing=await registrar.BeginReview(s.Admin,s.Inst,req.Id,null,default);
        var approved=await registrar.Decide(s.Admin,s.Inst,req.Id,new("Approved","Review completed without attendance",reviewing.Version),default);
        Assert.Equal(403,(await Assert.ThrowsAsync<ApiFault>(()=>registrar.Fulfill(other.Admin,s.Inst,req.Id,new(approved.Version),default))).StatusCode);
        Assert.Equal(409,(await Assert.ThrowsAsync<ApiFault>(()=>registrar.Fulfill(s.Admin,s.Inst,req.Id,new(approved.Version),default))).StatusCode);
        Assert.Equal("Approved",await db.StudentAcademicRequests.Where(x=>x.Id==req.Id).Select(x=>x.Status).SingleAsync());
        Assert.False(await db.AcademicRequestHistories.AnyAsync(x=>x.RequestId==req.Id&&x.ToStatus=="Completed"));
    }
    private async Task SeedPublishedHistory(Scenario s)
    {
        await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        db.StudentEnrollments.Add(new(){InstitutionId=s.Inst,SectionId=s.Section,StudentId=s.Student,Status="Withdrawn"});
        db.SectionGradePolicies.Add(new(){InstitutionId=s.Inst,SectionId=s.Section,PassingPercentage=60,PublishedAtUtc=DateTime.UtcNow,UpdatedBy=s.AdminId});
        db.GradebookResults.Add(new(){InstitutionId=s.Inst,SectionId=s.Section,StudentId=s.Student,Percentage=80,Passed=true,Letter="B",PublishedAtUtc=DateTime.UtcNow});await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<RecordsService>().SyncTranscript(s.Admin,s.Section,default);
    }
    [Fact]
    public async Task CurriculumTransitionRetainsHistoryAndRecognizesEachAchievementOnlyOnce()
    {
        var s=await Setup();await SeedPublishedHistory(s);
        await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var course=Course.Create(Guid.NewGuid(),s.Inst,"CS102","Replacement course");var plan=new CurriculumPlan{InstitutionId=s.Inst,ProgramId=s.Program,CatalogYear=2027,Revision=2,Name="Revision Two",Status="Published",PublishedAtUtc=DateTime.UtcNow,MinimumTotalCredits=3};
        db.Courses.Add(course);db.CurriculumPlans.Add(plan);db.CurriculumCourses.Add(new(){InstitutionId=s.Inst,PlanId=plan.Id,CourseId=course.Id,CreditHours=3,RequirementKind="Required",RecommendedTerm=1});
        db.CurriculumGradeBands.Add(new(){InstitutionId=s.Inst,PlanId=plan.Id,MinimumPercentage=0,GradePoints=0,Letter="F"});await db.SaveChangesAsync();
        var service=scope.ServiceProvider.GetRequiredService<CurriculumService>();
        var input=new TransitionInput(Guid.NewGuid(),s.Plan,plan.Id,DateTime.UtcNow.AddSeconds(-1),"Approved revision equivalency transition",new[]{new CourseRecognition(s.Course,course.Id)});
        Assert.Equal(409,(await Assert.ThrowsAsync<ApiFault>(()=>service.RequestTransition(s.Admin,s.Inst,s.Student,input with{Equivalencies=[]},default))).StatusCode);
        var pending=await service.RequestTransition(s.Admin,s.Inst,s.Student,input,default);Assert.Equal(pending.Id,(await service.RequestTransition(s.Admin,s.Inst,s.Student,input,default)).Id);
        Assert.Equal(403,(await Assert.ThrowsAsync<ApiFault>(()=>service.ReviewTransition(s.Admin,s.Inst,pending.Id,true,default))).StatusCode);
        await service.ReviewTransition(s.Reviewer,s.Inst,pending.Id,true,default);await service.ApplyTransition(s.Admin,s.Inst,pending.Id,default);await service.ApplyTransition(s.Admin,s.Inst,pending.Id,default);
        Assert.Equal(plan.Id,await db.StudentCurriculumAssignments.Where(x=>x.StudentId==s.Student).Select(x=>x.PlanId).SingleAsync());
        Assert.Equal(1,await db.Set<RecognizedCourseCredit>().CountAsync(x=>x.TransitionId==pending.Id));
        Assert.Contains(s.Plan.ToString(),(await db.Set<CurriculumTransition>().AsNoTracking().SingleAsync(x=>x.Id==pending.Id)).PriorAssignmentJson!);
        Assert.Equal(s.Course,await db.StudentTranscriptEntries.Where(x=>x.StudentId==s.Student).Select(x=>x.CourseId).SingleAsync());
        Assert.True(Assert.Single((await service.Audit(s.StudentUser,s.Student,default)).Courses).Completed);
    }
    [Fact]
    public async Task OutboxRecoversExpiredLeasesAndConcurrentWorkersPublishOneDurableReceipt()
    {
        var s=await Setup();Guid id;
        await using(var scope=fixture.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();var item=OutboxService.Record(db,s.Inst,"ImportRowApplied",Guid.NewGuid(),new{StudentId=s.Student});
            item.Status="Processing";item.LeaseToken=Guid.NewGuid();item.LeaseUntilUtc=DateTime.UtcNow.AddMinutes(-1);id=item.Id;await db.SaveChangesAsync();
        }
        async Task Run(){await using var scope=fixture.Services.CreateAsyncScope();await scope.ServiceProvider.GetRequiredService<OutboxService>().ProcessBatch(default);}
        await Task.WhenAll(Run(),Run());await Run();
        await using(var scope=fixture.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();Assert.Equal("Delivered",await db.InstitutionalOutboxEvents.Where(x=>x.Id==id).Select(x=>x.Status).SingleAsync());
            Assert.Equal(1,await db.Set<InstitutionalEventReceipt>().CountAsync(x=>x.EventId==id));
            var service=scope.ServiceProvider.GetRequiredService<OutboxService>();Assert.Contains(await service.Feed(s.Admin,s.Inst,1,default),x=>x.Id==id);
            Assert.Equal(403,(await Assert.ThrowsAsync<ApiFault>(()=>service.Feed(s.StudentUser,s.Inst,1,default))).StatusCode);
        }
    }
    [Fact]
    public async Task GradeCorrectionInvalidatesIssuedTranscriptUntilAReviewedReissuance()
    {
        var s=await Setup();await SeedPublishedHistory(s);var doc=await Issue(s,"Transcript");
        await using var scope=fixture.Services.CreateAsyncScope();var grades=scope.ServiceProvider.GetRequiredService<Academix.Api.Services.Assessment.GradebookService>();
        await grades.Correct(s.Admin,s.Section,s.Student,new(70,"Corrected source assessment evidence",1),default);
        var docs=scope.ServiceProvider.GetRequiredService<InstitutionalDocumentService>();Assert.Equal("SourceChanged",(await docs.Verify(new(doc.ReferenceNumber,doc.VerificationProof),default)).Status);
        await Assert.ThrowsAsync<ApiFault>(()=>Issue(s,"Transcript"));
        await scope.ServiceProvider.GetRequiredService<RecordsService>().SyncTranscript(s.Admin,s.Section,default);
        var replacement=await Issue(s,"Transcript",doc.Id);Assert.True((await docs.Verify(new(replacement.ReferenceNumber,replacement.VerificationProof),default)).Valid);
        Assert.False((await docs.Verify(new(doc.ReferenceNumber,doc.VerificationProof),default)).Valid);
    }
    [Fact]
    public async Task EnrollmentExceptionGrantsOnlyItsApprovedRuleAndNeverBypassesHoldsOrCapacity()
    {
        var s=await Setup();await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var policies=scope.ServiceProvider.GetRequiredService<AcademicPolicyService>();var registrar=scope.ServiceProvider.GetRequiredService<RegistrarOperationsService>();
        var policy=await policies.Draft(s.Admin,s.Inst,new(null,DateTime.UtcNow.AddMinutes(-1),DateTime.UtcNow.AddYears(1),new(true,true,null,true)),default);
        await policies.Publish(s.Reviewer,s.Inst,policy.Id,default);
        var request=await scope.ServiceProvider.GetRequiredService<StudentAcademicOperationsService>().CreateRequest(s.StudentUser,
            new(s.Section,"EnrollmentException","Enrollment approval","Registrar approval under the published enrollment policy.",Guid.NewGuid(),new(null,policy.Id,"EnrollmentApproval",DateTime.UtcNow.AddDays(1))),default);
        var reviewed=await registrar.BeginReview(s.Admin,s.Inst,request.Id,null,default);
        var approved=await registrar.Decide(s.Admin,s.Inst,request.Id,new("Approved","Registrar approved the stated exception",reviewed.Version),default);
        await registrar.Fulfill(s.Admin,s.Inst,request.Id,new(approved.Version),default);await registrar.Fulfill(s.Admin,s.Inst,request.Id,new(approved.Version),default);
        Assert.Equal(1,await db.ApprovedAcademicExceptions.CountAsync(x=>x.StudentId==s.Student&&x.SectionId==s.Section));
        Assert.False(await db.StudentEnrollments.AnyAsync(x=>x.StudentId==s.Student));
        var hold=await policies.AddHold(s.Admin,s.Inst,s.Student,new("Outstanding registrar clearance"),default);
        var academic=scope.ServiceProvider.GetRequiredService<AcademicOperations>();
        Assert.Contains("ActiveAcademicHold",(await Assert.ThrowsAsync<ApiFault>(()=>academic.Enroll(s.Admin,s.Section,new(s.Student),default))).Message);
        await policies.ReleaseHold(s.Admin,s.Inst,hold.Id,default);await academic.Enroll(s.Admin,s.Section,new(s.Student),default);
        await db.StudentEnrollments.Where(x=>x.SectionId==s.Section&&x.StudentId==s.Student).ExecuteUpdateAsync(set=>set.SetProperty(x=>x.Status,"Withdrawn"));
        await db.CourseSections.Where(x=>x.Id==s.Section).ExecuteUpdateAsync(set=>set.SetProperty(x=>x.Capacity,1));
        db.StudentEnrollments.Add(new(){InstitutionId=s.Inst,SectionId=s.Section,StudentId=s.ReviewerId});await db.SaveChangesAsync();
        Assert.Contains("full",(await Assert.ThrowsAsync<ApiFault>(()=>academic.Enroll(s.Admin,s.Section,new(s.Student),default))).Message);
    }
    [Fact]
    public async Task ScheduledOutboxRetriesAfterPermissionFailureAndNeverDuplicatesNotifications()
    {
        var s=await Setup();Guid announcement;
        await using(var scope=fixture.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();db.StudentEnrollments.Add(new(){InstitutionId=s.Inst,SectionId=s.Section,StudentId=s.Student});await db.SaveChangesAsync();
            var item=await scope.ServiceProvider.GetRequiredService<Academix.Api.Services.Communications.CommunicationService>().ScheduleAnnouncement(s.Admin,s.Section,
                new("Scheduled message","Durable notification payload","Students",DateTime.UtcNow.AddSeconds(-30)),default);announcement=item.Id;
            await db.Users.Where(x=>x.Id==s.AdminId).ExecuteUpdateAsync(set=>set.SetProperty(x=>x.IsActive,false));
            var delivery=scope.ServiceProvider.GetRequiredService<OutboxService>();await delivery.QueueDue(default);await delivery.ProcessBatch(default);
            var row=await db.InstitutionalOutboxEvents.AsNoTracking().SingleAsync(x=>x.InstitutionId==s.Inst&&x.Kind=="ScheduledAnnouncement");
            Assert.Equal("Retry",row.Status);Assert.NotNull(row.LastError);Assert.True(row.NextAttemptAtUtc>DateTime.UtcNow);Assert.Equal(1,row.Attempts);
            Assert.False(await db.InAppNotifications.AnyAsync(x=>x.SourceId==announcement));
            await db.Users.Where(x=>x.Id==s.AdminId).ExecuteUpdateAsync(set=>set.SetProperty(x=>x.IsActive,true));
            await db.InstitutionalOutboxEvents.Where(x=>x.Id==row.Id).ExecuteUpdateAsync(set=>set.SetProperty(x=>x.NextAttemptAtUtc,DateTime.UtcNow.AddSeconds(-1)));
        }
        async Task Process(){await using var scope=fixture.Services.CreateAsyncScope();await scope.ServiceProvider.GetRequiredService<OutboxService>().ProcessBatch(default);}
        await Task.WhenAll(Process(),Process());await Process();
        await using(var scope=fixture.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();Assert.Equal(1,await db.InAppNotifications.CountAsync(x=>x.SourceId==announcement&&x.RecipientId==s.Student));
            Assert.Equal("Delivered",await db.InstitutionalOutboxEvents.Where(x=>x.InstitutionId==s.Inst&&x.Kind=="ScheduledAnnouncement").Select(x=>x.Status).SingleAsync());
        }
    }
    [Fact]
    public async Task LegacyProofCaptureAndNewKeyRevocationAreIndependentOfJwtRotation()
    {
        var s=await Setup();var doc=await Issue(s);await using var scope=fixture.Services.CreateAsyncScope();var docs=scope.ServiceProvider.GetRequiredService<InstitutionalDocumentService>();
        var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();var config=scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        var originalJwt=config["Jwt:SigningKey"]!;var legacyRequest=new InstitutionalDocumentRequest{InstitutionId=s.Inst,StudentId=s.Student,ClientRequestId=Guid.NewGuid(),Kind="Enrollment",Purpose="Legacy preserved document",Status="Issued"};
        var legacy=new IssuedAcademicDocument{InstitutionId=s.Inst,StudentId=s.Student,RequestId=legacyRequest.Id,Kind="Enrollment",ReferenceNumber="LEGACY-"+Guid.NewGuid().ToString("N"),SnapshotJson="{}",IssuedById=s.AdminId,IssuedAtUtc=DateTime.UtcNow,ExpiresAtUtc=DateTime.UtcNow.AddDays(10)};
        db.InstitutionalDocumentRequests.Add(legacyRequest);db.IssuedAcademicDocuments.Add(legacy);await db.SaveChangesAsync();
        var expected=Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(System.Text.Encoding.UTF8.GetBytes(originalJwt),System.Text.Encoding.UTF8.GetBytes("ACADEMIX-DOCUMENT-V1:"+legacy.Id.ToString("N"))));
        await new LegacyDocumentProofInitializer(scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),config).StartAsync(default);
        try
        {
            config["Jwt:SigningKey"]="isolated-test-rotated-access-signing-key-only-2026";
            Assert.True((await docs.Verify(new(legacy.ReferenceNumber,expected),default)).Valid);Assert.True((await docs.Verify(new(doc.ReferenceNumber,doc.VerificationProof),default)).Valid);
            await docs.RotateKey(s.Admin,s.Inst,default);Assert.True((await docs.Verify(new(doc.ReferenceNumber,doc.VerificationProof),default)).Valid);
            await docs.RevokeKey(s.Admin,s.Inst,doc.VerificationKeyId!.Value,default);Assert.Equal("KeyRevoked",(await docs.Verify(new(doc.ReferenceNumber,doc.VerificationProof),default)).Status);
        }
        finally{config["Jwt:SigningKey"]=originalJwt;}
    }
    [Fact]
    public async Task ImportDatabaseFailureRollsBackMembershipProfileAndCheckpointThenResumesOnce()
    {
        var s=await Setup();Guid student;string email;Guid job;
        await using(var scope=fixture.Services.CreateAsyncScope())
        {
            var manager=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();email=Guid.NewGuid().ToString("N")+"@example.test";
            var account=new ApplicationUser{Id=Guid.NewGuid(),InstitutionId=s.Inst,Email=email,UserName=email};Assert.True((await manager.CreateAsync(account,"Safe!ImportRecovery2026")).Succeeded);Assert.True((await manager.AddToRoleAsync(account,AcademixRoles.Student)).Succeeded);student=account.Id;
            var imports=scope.ServiceProvider.GetRequiredService<InstitutionalImportService>();
            var input=new ImportUpload(Guid.NewGuid(),"SOURCE",new("id","email","program","term","plan","name"),new[]{new Dictionary<string,string?>{["id"]="STU-1",["email"]=email,["program"]=s.Program.ToString(),["term"]=s.Term.ToString(),["plan"]=s.Plan.ToString(),["name"]="Imported Profile"}});
            job=(await imports.Upload(s.Admin,s.Inst,input,default)).Job.Id;await imports.DryRun(s.Admin,s.Inst,job,default);await imports.Approve(s.Reviewer,s.Inst,job,default);
            var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            // Transactional failure injection in the isolated database, not a mocked DbContext.
            var function="p19_test_failure_"+s.Inst.ToString("N");
            var create="CREATE FUNCTION "+function+"() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.\"InstitutionId\"='"+s.Inst+"'::uuid AND NEW.\"Kind\"='ImportRowApplied' THEN RAISE EXCEPTION 'injected commit-path failure' USING ERRCODE='23514'; END IF; RETURN NEW; END $$; CREATE TRIGGER "+function+" BEFORE INSERT ON institutional_outbox_events FOR EACH ROW EXECUTE FUNCTION "+function+"();";
            await db.Database.ExecuteSqlRawAsync(create);
            try
            {
                Assert.Equal(1,(await imports.Apply(s.Admin,s.Inst,job,default)).Job.FailedRows);
                Assert.False(await db.StudentProgramMemberships.AnyAsync(x=>x.StudentId==student));Assert.False(await db.StudentCurriculumAssignments.AnyAsync(x=>x.StudentId==student));Assert.False(await db.Set<StudentProfile>().AnyAsync(x=>x.StudentId==student));
                Assert.False(await db.ExternalIdentityMaps.AnyAsync(x=>x.StudentId==student));
            }
            finally{var cleanup="DROP TRIGGER "+function+" ON institutional_outbox_events; DROP FUNCTION "+function+"();";await db.Database.ExecuteSqlRawAsync(cleanup);}
            Assert.Equal(1,(await imports.Apply(s.Admin,s.Inst,job,default)).Job.AppliedRows);Assert.Equal(1,(await imports.Apply(s.Admin,s.Inst,job,default)).Job.AppliedRows);
            Assert.Equal("Imported Profile",await db.Set<StudentProfile>().Where(x=>x.StudentId==student).Select(x=>x.FullName).SingleAsync());
            Assert.Equal("Reconciled",Assert.Single((await imports.Reconcile(s.Admin,s.Inst,job,default)).Rows).Status);
        }
    }

    [Fact]
    public async Task ImportNewIdentityWaitsForVerifiedActivationThenResumesWithoutDuplicateAccounts()
    {
        var s=await Setup();await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var imports=scope.ServiceProvider.GetRequiredService<InstitutionalImportService>();var email=Guid.NewGuid().ToString("N")+"@example.test";
        var input=new ImportUpload(Guid.NewGuid(),"NEW-SIS",new("id","email","program","term","plan","name",null,"invite"),new[]{new Dictionary<string,string?>{["id"]="NEW-1",["email"]=email,["program"]=s.Program.ToString(),["term"]=s.Term.ToString(),["plan"]=s.Plan.ToString(),["name"]="Verified Import Student",["invite"]="true"}});
        var job=(await imports.Upload(s.Admin,s.Inst,input,default)).Job.Id;Assert.Equal("Valid",Assert.Single((await imports.DryRun(s.Admin,s.Inst,job,default)).Rows).Status);
        await imports.Approve(s.Reviewer,s.Inst,job,default);
        Assert.Equal("AwaitingActivation",Assert.Single((await imports.Apply(s.Admin,s.Inst,job,default)).Rows).Status);
        Assert.Equal("AwaitingActivation",Assert.Single((await imports.Apply(s.Admin,s.Inst,job,default)).Rows).Status);
        Assert.False(await db.Users.AnyAsync(x=>x.NormalizedEmail==email.ToUpperInvariant()));
        var candidate=await db.Set<Applicant>().AsNoTracking().SingleAsync(x=>x.InstitutionId==s.Inst&&x.NormalizedEmail==email.ToUpperInvariant());Assert.Equal("Accepted",candidate.Status);Assert.Null(candidate.StudentId);
        var applicantService=scope.ServiceProvider.GetRequiredService<ApplicantService>();var token=await applicantService.RenewInvitation(s.Admin,s.Inst,candidate.Id,default);
        var active=await applicantService.Activate(new(token.Token,"Safe!ImportedAccount2026"),default);Assert.Equal("Converted",active.Status);
        Assert.Equal(1,(await imports.Apply(s.Admin,s.Inst,job,default)).Job.AppliedRows);Assert.Equal(1,(await imports.Apply(s.Admin,s.Inst,job,default)).Job.AppliedRows);
        Assert.Equal(1,await db.Users.CountAsync(x=>x.NormalizedEmail==email.ToUpperInvariant()));
        Assert.Equal(1,await db.StudentProgramMemberships.CountAsync(x=>x.StudentId==active.StudentId));Assert.Equal(s.Plan,await db.StudentCurriculumAssignments.Where(x=>x.StudentId==active.StudentId).Select(x=>x.PlanId).SingleAsync());
        Assert.Equal(1,await db.InstitutionalOutboxEvents.CountAsync(x=>x.InstitutionId==s.Inst&&x.Kind=="ImportApplicantInvitationRequired"));
        Assert.Equal("Reconciled",Assert.Single((await imports.Reconcile(s.Admin,s.Inst,job,default)).Rows).Status);
    }
    [Fact]
    public async Task ApprovedImportReusesAnIndependentlyAcceptedApplicantWithoutASecondIdentity()
    {
        var s=await Setup();await using var scope=fixture.Services.CreateAsyncScope();
        var applicants=scope.ServiceProvider.GetRequiredService<ApplicantService>();var imports=scope.ServiceProvider.GetRequiredService<InstitutionalImportService>();
        var email=Guid.NewGuid().ToString("N")+"@example.test";
        var invitation=await applicants.Invite(s.Reviewer,s.Inst,new(Guid.NewGuid(),email,"Existing Accepted Applicant",s.Program,s.Term,"Existing accepted applicant must be reused safely."),default);
        var submitted=await applicants.Access(new(invitation.Token),default);
        await applicants.Review(s.Admin,s.Inst,invitation.ApplicantId,new(submitted.Version,true,"Independent admission acceptance"),default);
        var upload=new ImportUpload(Guid.NewGuid(),"EXISTING-APPLICANT",new("id","email","program","term","plan","name",null,"invite"),new[]{new Dictionary<string,string?>{
            ["id"]="EXISTING-1",["email"]=email,["program"]=s.Program.ToString(),["term"]=s.Term.ToString(),["plan"]=s.Plan.ToString(),["name"]="Existing Accepted Applicant",["invite"]="true"}});
        var job=(await imports.Upload(s.Admin,s.Inst,upload,default)).Job.Id;
        await imports.DryRun(s.Admin,s.Inst,job,default);await imports.Approve(s.Reviewer,s.Inst,job,default);
        Assert.Equal("AwaitingActivation",Assert.Single((await imports.Apply(s.Admin,s.Inst,job,default)).Rows).Status);
        var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        Assert.Equal(invitation.ApplicantId,await db.Set<Applicant>().Where(x=>x.InstitutionId==s.Inst&&x.NormalizedEmail==email.ToUpperInvariant()).Select(x=>x.Id).SingleAsync());
        Assert.False(await db.Users.AnyAsync(x=>x.NormalizedEmail==email.ToUpperInvariant()));
        Assert.Equal("Converted",(await applicants.Activate(new(invitation.Token,"Safe!ExistingApplicant2026"),default)).Status);
        Assert.Equal(1,(await imports.Apply(s.Admin,s.Inst,job,default)).Job.AppliedRows);
    }
    [Fact]
    public async Task RenewedInvitationRejectsOldTokensAndExpiredInvitationsNeverActivate()
    {
        var s=await Setup();await using var scope=fixture.Services.CreateAsyncScope();var service=scope.ServiceProvider.GetRequiredService<ApplicantService>();
        var invite=await service.Invite(s.Admin,s.Inst,new(Guid.NewGuid(),Guid.NewGuid().ToString("N")+"@example.test","Invitation Owner",s.Program,s.Term,"Admission invitation recovery evidence."),default);
        var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();await db.Set<Applicant>().Where(x=>x.Id==invite.ApplicantId).ExecuteUpdateAsync(set=>set.SetProperty(x=>x.InvitationExpiresAtUtc,DateTime.UtcNow.AddDays(-1)));
        Assert.Equal(404,(await Assert.ThrowsAsync<ApiFault>(()=>service.Access(new(invite.Token),default))).StatusCode);
        var renewed=await service.RenewInvitation(s.Admin,s.Inst,invite.ApplicantId,default);Assert.NotEqual(invite.Token,renewed.Token);
        Assert.Equal(404,(await Assert.ThrowsAsync<ApiFault>(()=>service.Access(new(invite.Token),default))).StatusCode);
        Assert.Equal("Submitted",(await service.Access(new(renewed.Token),default)).Status);
        Assert.Equal(409,(await Assert.ThrowsAsync<ApiFault>(()=>service.Activate(new(renewed.Token,"Safe!UnacceptedUser2026"),default))).StatusCode);
    }
    [Fact]
    public async Task AuthorizationDoesNotReuseAnAccountTrackedBeforeDeactivation()
    {
        var s=await Setup();await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        Assert.True((await db.Users.FindAsync(s.AdminId))!.IsActive);
        await using(var changes=fixture.Services.CreateAsyncScope())await changes.ServiceProvider.GetRequiredService<AcademixDbContext>().Users.Where(x=>x.Id==s.AdminId).ExecuteUpdateAsync(set=>set.SetProperty(x=>x.IsActive,false));
        try{Assert.Equal(401,(await Assert.ThrowsAsync<ApiFault>(()=>scope.ServiceProvider.GetRequiredService<AcademicPolicyService>().History(s.Admin,s.Inst,1,default))).StatusCode);}
        finally{await db.Users.Where(x=>x.Id==s.AdminId).ExecuteUpdateAsync(set=>set.SetProperty(x=>x.IsActive,true));}
    }

}
