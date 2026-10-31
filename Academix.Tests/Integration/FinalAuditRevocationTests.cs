using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Curriculum;
using Academix.Api.Domain.Identity;
using Academix.Api.Domain.Records;
using Academix.Api.DTOs.Examinations;
using Academix.Api.Middleware;
using Academix.Api.Services.AcademicOperations;
using Academix.Api.Services.Curriculum;
using Academix.Api.Services.Examinations;
using Academix.Api.Services.Identity;
using Academix.Api.Services.Lifecycle;
using Academix.Api.Services.Records;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Academix.Tests.Integration;

public sealed partial class Phase19CompletionWorkflowTests
{
    public static IEnumerable<object[]> FinalAuditRevocationCases()
    {
        foreach(var operation in new[]{"Admission","Transfer","Assign","Courses","Scale","CurriculumPublish","ExamPublish","ExamEdit","ExamQuestions","ExamPools","Request","Advance","Standing"})
            foreach(var revocation in new[]{"Disabled","Role","Membership"})yield return [operation,revocation];
    }

    [Theory]
    [MemberData(nameof(FinalAuditRevocationCases))]
    public async Task FinalAuditLegacyMutationRevalidatesAfterObservedResourceWait(string operation,string revocation)
    {
        var s=await Setup();
        await using var setup=fixture.Services.CreateAsyncScope();var db=setup.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var users=setup.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var lockTable="identity_users";var lockColumn="Id";var lockId=s.Student;
        var stateTable="student_curriculum_assignments";var stateColumn="StudentId";var stateId=s.Student;
        Func<IServiceProvider,ClaimsPrincipal,Task> mutation;
        switch(operation)
        {
            case "Admission":
            case "Assign":
            {
                var mail=Guid.NewGuid().ToString("N")+"@example.test";var student=new ApplicationUser{InstitutionId=s.Inst,Email=mail,UserName=mail};
                Assert.True((await users.CreateAsync(student,"Safe!LockAudit2026")).Succeeded);Assert.True((await users.AddToRoleAsync(student,AcademixRoles.Student)).Succeeded);
                lockId=student.Id;stateId=student.Id;
                if(operation=="Admission")
                {
                    var application=await setup.ServiceProvider.GetRequiredService<LifecycleService>().Apply(Principal(student.Id,s.Inst),s.Program,new(s.Term,"Synthetic admission for deterministic authorization regression"),default);
                    stateTable="admission_applications";stateColumn="Id";stateId=application.Id;
                    mutation=async(p,u)=>{await p.GetRequiredService<LifecycleService>().DecideAdmission(u,application.Id,new(application.Version,"Approve","Independent admission approval"),default);};
                }
                else
                {
                    db.StudentProgramMemberships.Add(new(){InstitutionId=s.Inst,StudentId=student.Id,ProgramId=s.Program,AdmissionTermId=s.Term,CurrentTermId=s.Term});await db.SaveChangesAsync();
                    mutation=async(p,u)=>{await p.GetRequiredService<CurriculumService>().Assign(u,student.Id,new(s.Plan),default);};
                }
                break;
            }
            case "Transfer":
            {
                db.ProgramCourses.Add(new(){InstitutionId=s.Inst,ProgramId=s.Program,CourseId=s.Course});
                await db.SaveChangesAsync();
                var petition=await setup.ServiceProvider.GetRequiredService<LifecycleService>().RequestTransfer(s.StudentUser,new("External College",[new(s.Course,"EX101","External course achievement",3)]),default);
                stateTable="transfer_credit_petitions";stateColumn="Id";stateId=petition.Id;
                mutation=async(p,u)=>{await p.GetRequiredService<LifecycleService>().DecideTransfer(u,petition.Id,new(petition.Version,"Approve","Independent transfer decision"),default);};break;
            }
            case "Courses":
            case "Scale":
            case "CurriculumPublish":
            {
                var plan=new CurriculumPlan{InstitutionId=s.Inst,ProgramId=s.Program,CatalogYear=2030,Revision=5,Name="Mutable curriculum regression",MinimumTotalCredits=3};
                db.Add(plan);db.ProgramCourses.Add(new(){InstitutionId=s.Inst,ProgramId=s.Program,CourseId=s.Course});
                db.CurriculumCourses.Add(new(){InstitutionId=s.Inst,PlanId=plan.Id,CourseId=s.Course,CreditHours=3,RequirementKind="Required",RecommendedTerm=1});
                db.CurriculumGradeBands.AddRange(new CurriculumGradeBand{InstitutionId=s.Inst,PlanId=plan.Id,MinimumPercentage=0,GradePoints=0,Letter="F"},new CurriculumGradeBand{InstitutionId=s.Inst,PlanId=plan.Id,MinimumPercentage=60,GradePoints=3,Letter="B"});await db.SaveChangesAsync();
                lockTable=stateTable="curriculum_plans";lockId=stateId=plan.Id;stateColumn="Id";
                mutation=async(p,u)=>{var service=p.GetRequiredService<CurriculumService>();if(operation=="Courses")await service.ReplaceCourses(u,plan.Id,new(1,[new(s.Course,"Required",3,2)]),default);else if(operation=="Scale")await service.ReplaceScale(u,plan.Id,new(1,[new(0,0,"F"),new(60,2,"C")]),default);else await service.Publish(u,plan.Id,new(1),default);};break;
            }
            case "ExamPublish":
            case "ExamEdit":
            case "ExamQuestions":
            case "ExamPools":
            {
                var question=await setup.ServiceProvider.GetRequiredService<QuestionBankService>().Create(s.Admin,s.Course,new("SingleChoice","Choose the correct audit answer","Easy",1,[new("A","Correct"),new("B","Incorrect")],["A"],[],"Staff explanation",[]),default);
                var service=setup.ServiceProvider.GetRequiredService<ExamBuilderService>();
                var draft=new ExamDraftRequest("Authorization exam","Instructions",30,1,DateTime.UtcNow.AddMinutes(-1),DateTime.UtcNow.AddDays(2),false);
                var exam=await service.Create(s.Admin,s.Section,draft,default);
                var populated=await service.ReplaceQuestions(s.Admin,exam.Exam.Id,new(exam.Exam.Version,[new(question.Question.Id,1,5)]),default);
                lockTable=stateTable="exam_definitions";lockId=stateId=exam.Exam.Id;stateColumn="Id";
                mutation=async(p,u)=>{var builder=p.GetRequiredService<ExamBuilderService>();var version=populated.Exam.Version;if(operation=="ExamPublish")await builder.Publish(u,exam.Exam.Id,new(version),default);else if(operation=="ExamEdit")await builder.Edit(u,exam.Exam.Id,new(version,draft with{Title="Authorized updated exam"}),default);else if(operation=="ExamQuestions")await builder.ReplaceQuestions(u,exam.Exam.Id,new(version,[new(question.Question.Id,1,6)]),default);else await builder.ReplacePools(u,exam.Exam.Id,new(version,[]),default);};break;
            }
            case "Request":
            {
                var request=await setup.ServiceProvider.GetRequiredService<StudentAcademicOperationsService>().CreateRequest(s.StudentUser,new(null,"General","Support request","Synthetic general academic request for lock regression",Guid.NewGuid()),default);
                lockTable=stateTable="student_academic_requests";lockId=stateId=request.Id;stateColumn="Id";
                mutation=async(p,u)=>{await p.GetRequiredService<RegistrarOperationsService>().Decide(u,s.Inst,request.Id,new("Approved","Authorized general request decision",request.Version),default);};break;
            }
            case "Advance":
            {
                var next=new AcademicTerm{InstitutionId=s.Inst,Code="NEXT",Name="Next term",StartsOn=new(2029,1,1),EndsOn=new(2030,1,1)};db.Add(next);await db.SaveChangesAsync();
                lockTable=stateTable="student_program_memberships";lockColumn=stateColumn="StudentId";
                mutation=async(p,u)=>{await p.GetRequiredService<RecordsService>().Advance(u,s.Student,new(next.Id,1,"Authorized academic progression"),default);};break;
            }
            case "Standing":
            {
                await SeedPublishedHistory(s);stateTable="student_term_standings";
                mutation=async(p,u)=>{await p.GetRequiredService<RecordsService>().RecalculateStanding(u,s.Term,s.Student,default);};break;
            }
            default:throw new InvalidOperationException(operation);
        }
        var connectionString=db.Database.GetConnectionString()!;
        async Task<string> State()
        {
            await using var connection=new NpgsqlConnection(connectionString);await connection.OpenAsync();
            // All SQL identifiers come from the fixed switch above, never from a request.
            await using var query=new NpgsqlCommand($"SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY t.\"Id\")::text,'[]') FROM {stateTable} t WHERE t.\"{stateColumn}\"=@id",connection);
            query.Parameters.AddWithValue("id",stateId);return (string)(await query.ExecuteScalarAsync())!;
        }
        var before=await State();var auditCount=await db.SecurityAuditEvents.CountAsync(x=>x.InstitutionId==s.Inst);
        var outboxCount=await db.InstitutionalOutboxEvents.CountAsync(x=>x.InstitutionId==s.Inst);
        var lifecycleCount=await db.LifecycleDecisionEvents.CountAsync(x=>x.InstitutionId==s.Inst);
        var eventsCount=await db.StudentLifecycleEvents.CountAsync(x=>x.InstitutionId==s.Inst);
        await using var blocker=new NpgsqlConnection(connectionString);await blocker.OpenAsync();await using var held=await blocker.BeginTransactionAsync();
        await using(var command=new NpgsqlCommand($"SELECT 1 FROM {lockTable} WHERE \"{lockColumn}\"=@id FOR UPDATE",blocker,held)){command.Parameters.AddWithValue("id",lockId);Assert.NotNull(await command.ExecuteScalarAsync());}
        async Task Mutate(ClaimsPrincipal actor){await using var own=fixture.Services.CreateAsyncScope();await mutation(own.ServiceProvider,actor);}
        var pending=Mutate(s.Admin);await using var observer=new NpgsqlConnection(connectionString);await observer.OpenAsync();
        var limit=DateTime.UtcNow.AddSeconds(10);var waiting=false;
        while(DateTime.UtcNow<limit)
        {
            await using var query=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE @blocker=ANY(pg_blocking_pids(pid)))",observer);
            query.Parameters.AddWithValue("blocker",blocker.ProcessID);waiting=(bool)(await query.ExecuteScalarAsync())!;if(waiting)break;await Task.Delay(25);
        }
        Assert.True(waiting,"Must observe a PostgreSQL lock wait, not infer overlap from a timer.");
        var actor=(await users.FindByIdAsync(s.AdminId.ToString()))!;
        if(revocation=="Disabled")await setup.ServiceProvider.GetRequiredService<AuthService>().ChangeActiveAsync(s.Reviewer,s.AdminId,new(false),default);
        else
        {
            if(revocation=="Role")Assert.True((await users.RemoveFromRoleAsync(actor,AcademixRoles.InstitutionAdministrator)).Succeeded);
            else{actor.InstitutionId=null;Assert.True((await users.UpdateAsync(actor)).Succeeded);}
            Assert.True((await users.UpdateSecurityStampAsync(actor)).Succeeded);
        }
        // Account disable writes its own reviewer-authored security audit. Protected
        // operation effects must not add anything beyond that revocation itself.
        var auditsAfterRevocation=await db.SecurityAuditEvents.CountAsync(x=>x.InstitutionId==s.Inst);
        await held.CommitAsync();var rejected=await Assert.ThrowsAsync<ApiFault>(()=>pending);
        Assert.Contains(rejected.StatusCode,new[]{401,403});Assert.Equal(before,await State());
        Assert.Equal(auditsAfterRevocation,await db.SecurityAuditEvents.CountAsync(x=>x.InstitutionId==s.Inst));
        Assert.Equal(auditCount+(revocation=="Disabled"?1:0),auditsAfterRevocation);Assert.Equal(outboxCount,await db.InstitutionalOutboxEvents.CountAsync(x=>x.InstitutionId==s.Inst));
        Assert.Equal(lifecycleCount,await db.LifecycleDecisionEvents.CountAsync(x=>x.InstitutionId==s.Inst));
        Assert.Equal(eventsCount,await db.StudentLifecycleEvents.CountAsync(x=>x.InstitutionId==s.Inst));
        if(operation=="Admission")Assert.False(await db.StudentProgramMemberships.AnyAsync(x=>x.StudentId==lockId));
        if(operation=="Assign")Assert.False(await db.StudentCurriculumAssignments.AnyAsync(x=>x.StudentId==lockId));
        await Mutate(s.Reviewer);Assert.NotEqual(before,await State());
    }
}
