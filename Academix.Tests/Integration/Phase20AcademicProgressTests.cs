using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Advising;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Curriculum;
using Academix.Api.Domain.Identity;
using Academix.Api.Domain.Records;
using Academix.Api.DTOs.Advising;
using Academix.Api.DTOs.Lifecycle;
using Academix.Api.Middleware;
using Academix.Api.Services.Advising;
using Academix.Api.Services.Curriculum;
using Academix.Api.Services.Infrastructure;
using Academix.Api.Services.Lifecycle;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academix.Tests.Integration;

// Deliberately not executed during container-disabled development. All scenarios
// require the existing real PostgreSQL/Testcontainers fixture, not an in-memory DB.
public sealed class Phase20AcademicProgressTests(ApiFixture fixture):IClassFixture<ApiFixture>
{
    private sealed record Scenario(Guid Inst,Guid Department,Guid Program,Guid Plan,Guid Required,Guid Elective,Guid Section,Guid Term,
        Guid FutureTerm,Guid Student,Guid OtherStudent,Guid AdminId,Guid ReviewerId,Guid AdvisorId,Guid OtherAdvisorId,
        ClaimsPrincipal StudentUser,ClaimsPrincipal OtherStudentUser,ClaimsPrincipal Admin,ClaimsPrincipal Reviewer,ClaimsPrincipal Advisor,ClaimsPrincipal OtherAdvisor);
    private static ClaimsPrincipal Principal(Guid id,Guid inst)=>new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier,id.ToString()),new("institution_id",inst.ToString())],"test"));
    private async Task<Scenario> Setup(bool completed=true)
    {
        await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var inst=new Institution{Code=Guid.NewGuid().ToString("N")[..12],Name="Phase20 University"};db.Add(inst);await db.SaveChangesAsync();
        var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        async Task<Guid> User(string role)
        {
            var email=Guid.NewGuid().ToString("N")+"@example.test";var user=new ApplicationUser{Id=Guid.NewGuid(),InstitutionId=inst.Id,UserName=email,Email=email};
            Assert.True((await users.CreateAsync(user,"Safe!Phase20Tests2026")).Succeeded);Assert.True((await users.AddToRoleAsync(user,role)).Succeeded);return user.Id;
        }
        var student=await User(AcademixRoles.Student);var otherStudent=await User(AcademixRoles.Student);var admin=await User(AcademixRoles.InstitutionAdministrator);
        var reviewer=await User(AcademixRoles.InstitutionAdministrator);var advisor=await User(AcademixRoles.Instructor);var otherAdvisor=await User(AcademixRoles.Instructor);
        var dept=new Department{InstitutionId=inst.Id,Code="D",Name="Department"};
        var program=new AcademicProgram{InstitutionId=inst.Id,DepartmentId=dept.Id,Code="P",Name="Program"};
        var term=new AcademicTerm{InstitutionId=inst.Id,Code="NOW",Name="Current",Status="Active",StartsOn=DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-2)),EndsOn=DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1))};
        var future=new AcademicTerm{InstitutionId=inst.Id,Code="NEXT",Name="Future",Status="Active",StartsOn=DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(2)),EndsOn=DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(5))};
        var required=Course.Create(Guid.NewGuid(),inst.Id,"REQ101","Required course");var elective=Course.Create(Guid.NewGuid(),inst.Id,"ELE101","Elective course");
        var section=new CourseSection{InstitutionId=inst.Id,TermId=term.Id,CourseId=required.Id,Code="A",Status="Open",Capacity=30};
        var electiveSection=new CourseSection{InstitutionId=inst.Id,TermId=term.Id,CourseId=elective.Id,Code="A",Status="Open",Capacity=30};
        var plan=new CurriculumPlan{InstitutionId=inst.Id,ProgramId=program.Id,CatalogYear=2026,Revision=1,Name="Degree",MinimumTotalCredits=6,MinimumElectiveCredits=3,MinimumCumulativeGpa=2};
        db.AddRange(dept,program,term,future,required,elective,section,electiveSection,plan);
        db.CurriculumCourses.AddRange(new CurriculumCourse{InstitutionId=inst.Id,PlanId=plan.Id,CourseId=required.Id,RequirementKind="Required",CreditHours=3,RecommendedTerm=1},
            new CurriculumCourse{InstitutionId=inst.Id,PlanId=plan.Id,CourseId=elective.Id,RequirementKind="Elective",CreditHours=3,RecommendedTerm=2});
        db.CurriculumGradeBands.AddRange(new CurriculumGradeBand{InstitutionId=inst.Id,PlanId=plan.Id,MinimumPercentage=0,GradePoints=0,Letter="F"},new CurriculumGradeBand{InstitutionId=inst.Id,PlanId=plan.Id,MinimumPercentage=60,GradePoints=3,Letter="B"});
        foreach(var id in new[]{student,otherStudent})
        {
            db.StudentProgramMemberships.Add(new(){InstitutionId=inst.Id,StudentId=id,ProgramId=program.Id,AdmissionTermId=term.Id,CurrentTermId=term.Id});
            db.StudentCurriculumAssignments.Add(new(){InstitutionId=inst.Id,StudentId=id,PlanId=plan.Id,AssignedById=admin});
        }
        await db.SaveChangesAsync();
        var curricula=scope.ServiceProvider.GetRequiredService<CurriculumService>();
        await curricula.ReplaceGroups(Principal(admin,inst.Id),plan.Id,new(1,[new("Science electives",3,[elective.Id])]),default);
        await curricula.Publish(Principal(admin,inst.Id),plan.Id,new(2),default);
        if(completed)
        {
            foreach(var pair in new[]{(required,section),(elective,electiveSection)})
                db.StudentTranscriptEntries.Add(new(){InstitutionId=inst.Id,StudentId=student,CourseId=pair.Item1.Id,SectionId=pair.Item2.Id,TermId=term.Id,
                    CourseCode=pair.Item1.Code,CourseTitle=pair.Item1.Title,Percentage=80,Passed=true,Letter="B",GradebookVersion=1});
            await db.SaveChangesAsync();
        }
        return new(inst.Id,dept.Id,program.Id,plan.Id,required.Id,elective.Id,section.Id,term.Id,future.Id,student,otherStudent,admin,reviewer,advisor,otherAdvisor,
            Principal(student,inst.Id),Principal(otherStudent,inst.Id),Principal(admin,inst.Id),Principal(reviewer,inst.Id),Principal(advisor,inst.Id),Principal(otherAdvisor,inst.Id));
    }
    [Fact]
    public async Task AdvisorWorkloadReassignmentHistoryAndCurrentRoleAuthorization()
    {
        var s=await Setup();await using var scope=fixture.Services.CreateAsyncScope();var service=scope.ServiceProvider.GetRequiredService<AdvisingService>();
        await service.ConfigureProgress(s.Admin,s.Inst,new(0,1,2,0,false,false),default);
        var first=await service.AssignAcademicAdvisor(s.Admin,s.Student,new(s.AdvisorId,"Initial assignment"),default);
        Assert.Equal(first.Id,(await service.AssignAcademicAdvisor(s.Admin,s.Student,new(s.AdvisorId,"Repeated assignment"),default)).Id);
        Assert.Equal(409,(await Assert.ThrowsAsync<ApiFault>(()=>service.AssignAcademicAdvisor(s.Admin,s.OtherStudent,new(s.AdvisorId,"Workload overflow"),default))).StatusCode);
        await service.AssignAcademicAdvisor(s.Admin,s.Student,new(s.OtherAdvisorId,"Reassign for workload"),default);
        var history=await service.AdvisorHistory(s.StudentUser,s.Student,1,default);Assert.Equal(2,history.Total);Assert.Single(history.Items,x=>x.EndedAtUtc is null);
        Assert.Equal(403,(await Assert.ThrowsAsync<ApiFault>(()=>service.StaffSessions(s.Advisor,s.Student,null,1,default))).StatusCode);
        Assert.Empty((await service.StaffSessions(s.OtherAdvisor,s.Student,null,1,default)).Items);
        await service.EndAdvisorAssignment(s.Admin,s.Student,first.Id,default); // Stale termination preserves its replacement.
        var active=Assert.Single((await service.AdvisorHistory(s.Admin,s.Student,1,default)).Items,x=>x.EndedAtUtc is null);
        await service.EndAdvisorAssignment(s.Admin,s.Student,active.Id,default);
        await service.EndAdvisorAssignment(s.Admin,s.Student,active.Id,default);
        await service.AssignAcademicAdvisor(s.Admin,s.OtherStudent,new(s.OtherAdvisorId,"Released workload slot"),default);
        Assert.Equal(403,(await Assert.ThrowsAsync<ApiFault>(()=>service.StaffSessions(s.OtherAdvisor,s.Student,null,1,default))).StatusCode);
        var manager=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();var account=await manager.FindByIdAsync(s.OtherAdvisorId.ToString());
        Assert.True((await manager.RemoveFromRoleAsync(account!,AcademixRoles.Instructor)).Succeeded);
        Assert.Equal(403,(await Assert.ThrowsAsync<ApiFault>(()=>service.StaffSessions(s.OtherAdvisor,s.OtherStudent,null,1,default))).StatusCode);
    }
    [Fact]
    public async Task SessionsKeepNotesPrivateAndRequireStudentAcknowledgement()
    {
        var s=await Setup();var foreign=await Setup();await using var scope=fixture.Services.CreateAsyncScope();var service=scope.ServiceProvider.GetRequiredService<AdvisingService>();
        await service.AssignAcademicAdvisor(s.Admin,s.Student,new(s.AdvisorId,"Academic advising"),default);
        var input=new ScheduleAdvisingSession(Guid.NewGuid(),DateTime.UtcNow.AddDays(2));var scheduled=await service.ScheduleSession(s.Advisor,s.Student,input,default);
        Assert.Equal(scheduled.Session.Id,(await service.ScheduleSession(s.Advisor,s.Student,input,default)).Session.Id);
        var completed=await service.CompleteSession(s.Advisor,scheduled.Session.Id,new(1,"Completed","CONFIDENTIAL advisor note","Take the elective next term",DateTime.UtcNow.AddDays(5)),default);
        Assert.Equal("CONFIDENTIAL advisor note",completed.InternalNotes);
        var studentView=await service.MyAcademicSessions(s.StudentUser,1,default);var json=System.Text.Json.JsonSerializer.Serialize(studentView);
        Assert.DoesNotContain("CONFIDENTIAL",json);Assert.DoesNotContain("InternalNotes",json);Assert.Contains("Take the elective",json);
        Assert.Equal(403,(await Assert.ThrowsAsync<ApiFault>(()=>service.StaffSessions(s.StudentUser,s.Student,null,1,default))).StatusCode);
        Assert.Equal(403,(await Assert.ThrowsAsync<ApiFault>(()=>service.StaffSessions(foreign.Admin,s.Student,null,1,default))).StatusCode);
        Assert.Equal(400,(await Assert.ThrowsAsync<ApiFault>(()=>service.AssignAcademicAdvisor(s.Admin,s.Student,new(foreign.AdvisorId,"Cross-institution assignment"),default))).StatusCode);
        await using(var invalidScope=fixture.Services.CreateAsyncScope())
        {
            var invalidDb=invalidScope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            invalidDb.Add(new AdvisorAssignment{InstitutionId=s.Inst,StudentId=s.OtherStudent,AdvisorId=foreign.AdvisorId,AssignedBy=s.AdminId,Reason="Database scope guard"});
            var error=await Assert.ThrowsAsync<DbUpdateException>(()=>invalidDb.SaveChangesAsync());
            Assert.Equal("23514",Assert.IsType<Npgsql.PostgresException>(error.InnerException).SqlState);
        }
        Assert.Equal(403,(await Assert.ThrowsAsync<ApiFault>(()=>service.AcknowledgeSession(s.OtherStudentUser,scheduled.Session.Id,new(2),default))).StatusCode);
        Assert.NotNull((await service.AcknowledgeSession(s.StudentUser,scheduled.Session.Id,new(2),default)).AcknowledgedAtUtc);
        Assert.NotNull((await service.AcknowledgeSession(s.StudentUser,scheduled.Session.Id,new(2),default)).AcknowledgedAtUtc);
    }
    [Fact]
    public async Task StudyPlanRevisionsRetainHistoryAndNeverCreateEnrollments()
    {
        var s=await Setup(false);await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();var service=scope.ServiceProvider.GetRequiredService<AdvisingService>();
        await service.AssignAcademicAdvisor(s.Admin,s.Student,new(s.AdvisorId,"Study planning"),default);
        var p=await service.RevisePlan(s.StudentUser,s.Student,new(0,"Initial proposed schedule",[new(s.Required,s.FutureTerm)]),default);
        Assert.Equal(403,(await Assert.ThrowsAsync<ApiFault>(()=>service.ReviewPlan(s.StudentUser,s.Student,new(p.Version,true,"Self approval"),default))).StatusCode);
        p=await service.ReviewPlan(s.Advisor,s.Student,new(p.Version,true,"Recommended sequence"),default);
        p=await service.AcknowledgePlan(s.StudentUser,s.Student,new(p.Version),default);
        Assert.Equal("Acknowledged",p.Revisions[0].Status);Assert.Equal(s.Plan,p.Revisions[0].CurriculumPlanId);
        await Assert.ThrowsAsync<ApiFault>(()=>service.RevisePlan(s.StudentUser,s.Student,new(1,"Stale revision",[new(s.Elective,s.FutureTerm)]),default));
        p=await service.RevisePlan(s.StudentUser,s.Student,new(p.Version,"Add elective next term",[new(s.Required,s.FutureTerm),new(s.Elective,s.FutureTerm)]),default);
        Assert.Equal(2,p.CurrentRevision);Assert.Equal(2,p.Revisions.Count);Assert.Equal("Acknowledged",p.Revisions.Single(x=>x.Revision==1).Status);
        Assert.False(await db.StudentEnrollments.AnyAsync(x=>x.StudentId==s.Student));
        Assert.Equal(403,(await Assert.ThrowsAsync<ApiFault>(()=>service.StudyPlan(s.OtherStudentUser,s.Student,1,default))).StatusCode);
    }
    [Fact]
    public async Task GraduationRequiresDepartmentReviewIndependentApprovalAndConcurrentFinalizationIsIdempotent()
    {
        var s=await Setup();await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var advising=scope.ServiceProvider.GetRequiredService<AdvisingService>();var lifecycle=scope.ServiceProvider.GetRequiredService<LifecycleService>();
        await advising.ConfigureProgress(s.Admin,s.Inst,new(0,10,2,0,false,true),default);
        await advising.AssignAcademicAdvisor(s.Admin,s.Student,new(s.AdvisorId,"Graduation advising"),default);
        await lifecycle.SetDepartmentReviewer(s.Admin,s.Inst,s.Department,new(s.AdvisorId,true),default);
        var request=new GraduationSubmission(Guid.NewGuid());var application=await lifecycle.SubmitGraduation(s.StudentUser,request,default);
        Assert.Equal(application.Id,(await lifecycle.SubmitGraduation(s.StudentUser,request,default)).Id);
        await Assert.ThrowsAsync<ApiFault>(()=>lifecycle.SubmitGraduation(s.StudentUser,new(Guid.NewGuid()),default));
        await Assert.ThrowsAsync<ApiFault>(()=>lifecycle.ReviewGraduation(s.Admin,application.Id,new(application.Version,"Approve","Academic approval before department review"),default));
        application=await lifecycle.BeginGraduationReview(s.Advisor,application.Id,new(application.Version),default);
        application=await lifecycle.DepartmentDecision(s.Advisor,application.Id,new(application.Version,true,"Department requirements checked"),default);
        application=await lifecycle.ReviewGraduation(s.Admin,application.Id,new(application.Version,"Approve","All academic requirements satisfied"),default);
        async Task<GraduationView> Finalize()
        {await using var own=fixture.Services.CreateAsyncScope();return await own.ServiceProvider.GetRequiredService<LifecycleService>().IssueGraduation(s.Admin,application.Id,new(application.Version),default);}
        var finalized=await Task.WhenAll(Finalize(),Finalize());Assert.Equal(finalized[0].CredentialNumber,finalized[1].CredentialNumber);
        var evidence=await lifecycle.GraduationEvidence(s.Admin,application.Id,default);Assert.NotNull(evidence.ApprovalEvidenceJson);Assert.NotNull(evidence.FinalEvidenceJson);
        Assert.Contains(s.Plan.ToString(),evidence.FinalEvidenceJson);Assert.Equal("Issued",evidence.Application.Status);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.GraduationApplications.Where(x=>x.Id==application.Id)
            .ExecuteUpdateAsync(set=>set.SetProperty(x=>x.FinalEvidenceJson,"{}")));
        Assert.Equal(1,await db.StudentLifecycleEvents.CountAsync(x=>x.StudentId==s.Student&&x.Kind=="program.completed"));
        Assert.False(await db.Set<AdvisorAssignment>().AnyAsync(x=>x.StudentId==s.Student&&x.EndedAtUtc==null));
        var actions=await db.LifecycleDecisionEvents.Where(x=>x.EntityId==application.Id).OrderBy(x=>x.AtUtc).Select(x=>x.Action).ToListAsync();
        Assert.Equal(new[]{"submitted","UnderReview","DepartmentApproved","Approved","issued"},actions);
        var eventId=await db.InstitutionalOutboxEvents.Where(x=>x.InstitutionId==s.Inst&&x.Kind=="GraduationFinalized").Select(x=>x.Id).SingleAsync();
        for(var i=0;i<20&&!await db.InstitutionalOutboxEvents.AnyAsync(x=>x.Id==eventId&&x.Status=="Delivered");i++)await scope.ServiceProvider.GetRequiredService<OutboxService>().ProcessBatch(default);
        await scope.ServiceProvider.GetRequiredService<OutboxService>().ProcessBatch(default);
        Assert.Equal(1,await db.InAppNotifications.CountAsync(x=>x.RecipientId==s.Student&&x.SourceId==eventId&&x.Kind=="AcademicProgress"));
    }
    [Fact]
    public async Task RejectedGraduationsCanResubmitAndChangedRecordsPreventFinalization()
    {
        var s=await Setup();await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();var lifecycle=scope.ServiceProvider.GetRequiredService<LifecycleService>();
        var first=await lifecycle.SubmitGraduation(s.StudentUser,new(Guid.NewGuid()),default);
        await lifecycle.ReviewGraduation(s.Admin,first.Id,new(first.Version,"Reject","Application requires resubmission"),default);
        var next=await lifecycle.SubmitGraduation(s.StudentUser,new(Guid.NewGuid()),default);Assert.NotEqual(first.Id,next.Id);
        next=await lifecycle.ReviewGraduation(s.Admin,next.Id,new(next.Version,"Approve","Academic evidence checked"),default);
        await db.StudentTranscriptEntries.Where(x=>x.StudentId==s.Student&&x.CourseId==s.Required)
            .ExecuteUpdateAsync(set=>set.SetProperty(x=>x.Percentage,40).SetProperty(x=>x.Passed,false).SetProperty(x=>x.Letter,"F").SetProperty(x=>x.Version,x=>x.Version+1));
        Assert.False((await scope.ServiceProvider.GetRequiredService<DegreeAuditService>().Evaluate(s.Admin,s.Student,default)).Audit.GraduationReady);
        await Assert.ThrowsAsync<ApiFault>(()=>lifecycle.IssueGraduation(s.Admin,next.Id,new(next.Version),default));
        Assert.Equal("Active",await db.StudentProgramMemberships.Where(x=>x.StudentId==s.Student).Select(x=>x.Status).SingleAsync());
        Assert.Null((await lifecycle.GraduationEvidence(s.Admin,next.Id,default)).FinalEvidenceJson);
    }
    [Fact]
    public async Task ExemptionsRequireIndependentApprovalAndNeverGrantInventedCredit()
    {
        var s=await Setup(false);await using var scope=fixture.Services.CreateAsyncScope();var curriculum=scope.ServiceProvider.GetRequiredService<CurriculumService>();
        var exception=await curriculum.RequestException(s.StudentUser,s.Student,new(s.Plan,s.Required,null,"Waiver","Approved academic exemption requested"),default);
        Assert.Equal(403,(await Assert.ThrowsAsync<ApiFault>(()=>curriculum.DecideException(s.StudentUser,exception.Id,new(1,true,"Self approval forbidden"),default))).StatusCode);
        await curriculum.DecideException(s.Admin,exception.Id,new(1,true,"Institution-approved academic exemption"),default);
        var audit=await scope.ServiceProvider.GetRequiredService<DegreeAuditService>().Evaluate(s.StudentUser,s.Student,default);
        Assert.Equal(0,audit.Audit.EarnedTotalCredits);Assert.Equal(0,audit.Audit.MissingRequiredCourses);Assert.Equal("NotEligible",audit.Eligibility);
        await Assert.ThrowsAsync<ApiFault>(()=>scope.ServiceProvider.GetRequiredService<LifecycleService>().SubmitGraduation(s.StudentUser,new(Guid.NewGuid()),default));
    }
}
