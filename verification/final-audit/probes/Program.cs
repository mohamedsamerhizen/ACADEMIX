using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Curriculum;
using Academix.Api.Domain.Identity;
using Academix.Api.Domain.Records;
using Academix.Api.Domain.Registrar;
using Academix.Api.DTOs.Registrar;
using Academix.Api.Services.Assessment;
using Academix.Api.Services.Curriculum;
using Academix.Api.Services.Identity;
using Academix.Api.Services.Infrastructure;
using Academix.Api.Services.Lifecycle;
using Academix.Api.Services.Records;
using Academix.Tests.Integration;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

static ClaimsPrincipal Principal(Guid id, Guid? inst) => new(new ClaimsIdentity(
    inst is Guid tenant ? [new(ClaimTypes.NameIdentifier,id.ToString()),new("institution_id",tenant.ToString())] : [new Claim(ClaimTypes.NameIdentifier,id.ToString())],"audit-in-flight"));
static T Value<T>(object scenario,string name) => (T)scenario.GetType().GetProperty(name)!.GetValue(scenario)!;
static async Task<object> Setup(Phase19CompletionWorkflowTests tests)
{
    var task=(Task)typeof(Phase19CompletionWorkflowTests).GetMethod("Setup",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(tests,[])!;
    await task; return task.GetType().GetProperty("Result")!.GetValue(task)!;
}
static void Require(bool value,string message) { if(!value)throw new InvalidOperationException(message); }
static void Record(object value) => Console.WriteLine("AUDIT_RESULT "+JsonSerializer.Serialize(value));

if(args.Contains("--engine-only"))
{
    var source=Guid.NewGuid();var target=Guid.NewGuid();var term=Guid.NewGuid();
    var data=new DegreeAuditData(Guid.NewGuid(),Guid.NewGuid(),2027,2,3,0,0,
        [new(target,"NEW","Replacement","Required",3,1)], [new(0,0,"F"),new(60,3,"B")],
        [new(source,term,"OLD",40,false)], [new(target,"course:"+source)], [], new HashSet<Guid>(), [], [], [], 2,0);
    var recognized=DegreeAuditEngine.Evaluate(data);
    var control=DegreeAuditEngine.Evaluate(data with { Recognitions=[] });
    var substitution=DegreeAuditEngine.Evaluate(data with { Recognitions=[],Exceptions=[new(target,source,"Substitution","Approved")] });
    Record(new {Probe="EngineRecognitionWithFailedSource",SourcePassed=false,recognized.Eligibility,recognized.Audit.GraduationReady,recognized.Audit.EarnedTotalCredits,WithoutRecognition=control.Eligibility,SubstitutionWithSameFailedSource=substitution.Eligibility});
    Require(recognized.Eligibility=="Eligible"&&control.Eligibility=="NotEligible"&&substitution.Eligibility=="NotEligible","Unexpected engine behavior; do not claim reproduction.");
    return;
}
if(args.Contains("--authorization-only")){ await NoDatabaseAuthorizationProbes.Run(); return; }
var fixture = new ApiFixture { ConfigureTestServices = services => services.AddLogging(b=>b.SetMinimumLevel(LogLevel.Warning)) };
await fixture.InitializeAsync();
try
{
    var tests=new Phase19CompletionWorkflowTests(fixture);
    // Probe A: build a real approved/applied transition, correct the source grade through
    // the authoritative service, synchronize the transcript, then re-evaluate consumers.
    var s=await Setup(tests);
    await (Task)typeof(Phase19CompletionWorkflowTests).GetMethod("SeedPublishedHistory",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(tests,[s])!;
    var inst=Value<Guid>(s,"Inst");var student=Value<Guid>(s,"Student");var section=Value<Guid>(s,"Section");
    var admin=Value<ClaimsPrincipal>(s,"Admin");var reviewer=Value<ClaimsPrincipal>(s,"Reviewer");
    var source=Value<Guid>(s,"Course");var oldPlan=Value<Guid>(s,"Plan");
    await using(var scope=fixture.Services.CreateAsyncScope())
    {
        var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var replacement=Course.Create(Guid.NewGuid(),inst,"NX102","Replacement course");
        var plan=new CurriculumPlan{InstitutionId=inst,ProgramId=Value<Guid>(s,"Program"),CatalogYear=2027,Revision=2,Name="Audit transition",Status="Published",PublishedAtUtc=DateTime.UtcNow,MinimumTotalCredits=3};
        db.AddRange(replacement,plan);
        db.CurriculumCourses.Add(new(){InstitutionId=inst,PlanId=plan.Id,CourseId=replacement.Id,CreditHours=3,RequirementKind="Required",RecommendedTerm=1});
        db.CurriculumGradeBands.Add(new(){InstitutionId=inst,PlanId=plan.Id,MinimumPercentage=0,GradePoints=0,Letter="F"});
        await db.SaveChangesAsync();
        var curriculum=scope.ServiceProvider.GetRequiredService<CurriculumService>();
        var transition=await curriculum.RequestTransition(admin,inst,student,new(Guid.NewGuid(),oldPlan,plan.Id,DateTime.UtcNow.AddSeconds(-1),"Audit approved source equivalence",[new(source,replacement.Id)]),default);
        await curriculum.ReviewTransition(reviewer,inst,transition.Id,true,default);
        await curriculum.ApplyTransition(admin,inst,transition.Id,default);
        var before=await scope.ServiceProvider.GetRequiredService<DegreeAuditService>().Evaluate(admin,student,default);
        Require(before.Eligibility=="Eligible","Expected valid eligibility before correction.");
        var original=await db.GradebookResults.AsNoTracking().SingleAsync(x=>x.SectionId==section&&x.StudentId==student);
        await scope.ServiceProvider.GetRequiredService<GradebookService>().Correct(admin,section,student,new(40,"Audit correction removes the original passed achievement",original.Version),default);
        await scope.ServiceProvider.GetRequiredService<RecordsService>().SyncTranscript(admin,section,default);
        await AcademicFreshness.Ensure(db,inst,student,default);
        var after=await scope.ServiceProvider.GetRequiredService<DegreeAuditService>().Evaluate(admin,student,default);
        var sourcePassed=await db.StudentTranscriptEntries.AsNoTracking().Where(x=>x.StudentId==student&&x.CourseId==source).Select(x=>x.Passed).SingleAsync();
        var dependent=Course.Create(Guid.NewGuid(),inst,"NX103","Dependent course");var next=new CourseSection{InstitutionId=inst,CourseId=dependent.Id,TermId=Value<Guid>(s,"Term"),Code="DEP",Status="Open",Capacity=10};
        db.AddRange(dependent,next);db.CoursePrerequisites.Add(new(){InstitutionId=inst,CourseId=dependent.Id,RequiredCourseId=replacement.Id,MinimumPercentage=60});await db.SaveChangesAsync();
        var eligibility=await scope.ServiceProvider.GetRequiredService<RecordsService>().EligibilityForSection(next.Id,student,default);
        Record(new{Probe="RecognizedCreditAfterSourceFailure",SourceTranscriptPassed=sourcePassed,AcademicFreshnessAccepted=true,Before=before.Eligibility,After=after.Eligibility,after.Audit.GraduationReady,after.Audit.EarnedTotalCredits,PrerequisiteEligible=eligibility.Eligible,RecognizedRows=await db.Set<RecognizedCourseCredit>().CountAsync(x=>x.StudentId==student)});
        Require(!sourcePassed&&after.Eligibility=="Eligible"&&eligibility.Eligible,"Probe result differs from the suspected defect; inspect rather than claim confirmation.");
    }
    // Probe B: block the student user row after authorization, disable the actor using
    // the real account-state service, prove the database wait, then release the lock.
    s=await Setup(tests);inst=Value<Guid>(s,"Inst");admin=Value<ClaimsPrincipal>(s,"Admin");reviewer=Value<ClaimsPrincipal>(s,"Reviewer");
    await using(var scope=fixture.Services.CreateAsyncScope())
    {
        var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var mail=Guid.NewGuid().ToString("N")+"@example.test";var applicant=new ApplicationUser{InstitutionId=inst,UserName=mail,Email=mail};
        Require((await users.CreateAsync(applicant,"Audit!Synthetic2026")).Succeeded,"Synthetic account creation failed.");Require((await users.AddToRoleAsync(applicant,AcademixRoles.Student)).Succeeded,"Role creation failed.");
        var lifecycle=scope.ServiceProvider.GetRequiredService<LifecycleService>();
        var application=await lifecycle.Apply(Principal(applicant.Id,inst),Value<Guid>(s,"Program"),new(Value<Guid>(s,"Term"),"Synthetic admission for the controlled audit probe"),default);
        var connectionString=db.Database.GetConnectionString()!;
        await using var blocker=new NpgsqlConnection(connectionString);await blocker.OpenAsync();await using var held=await blocker.BeginTransactionAsync();
        await using(var command=new NpgsqlCommand("SELECT 1 FROM identity_users WHERE \"Id\"=@student FOR UPDATE",blocker,held)){command.Parameters.AddWithValue("student",applicant.Id);await command.ExecuteScalarAsync();}
        async Task<Academix.Api.DTOs.Lifecycle.AdmissionView> Decide(){await using var own=fixture.Services.CreateAsyncScope();return await own.ServiceProvider.GetRequiredService<LifecycleService>().DecideAdmission(admin,application.Id,new(application.Version,"Approve","Controlled audit admission approval"),default);}
        var pending=Decide();await using var observer=new NpgsqlConnection(connectionString);await observer.OpenAsync();var until=DateTime.UtcNow.AddSeconds(10);bool waiting=false;
        while(DateTime.UtcNow<until){await using var query=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE @blocker=ANY(pg_blocking_pids(pid)))",observer);query.Parameters.AddWithValue("blocker",blocker.ProcessID);waiting=(bool)(await query.ExecuteScalarAsync())!;if(waiting)break;await Task.Delay(25);}
        Require(waiting,"No observed PostgreSQL lock wait: authorization-race probe inconclusive.");
        var adminId=Value<Guid>(s,"AdminId");await scope.ServiceProvider.GetRequiredService<AuthService>().ChangeActiveAsync(reviewer,adminId,new(false),default);
        var active=await db.Users.AsNoTracking().Where(x=>x.Id==adminId).Select(x=>x.IsActive).SingleAsync();Require(!active,"Actor must be disabled before blocker release.");
        await held.CommitAsync();var result=await pending;
        var persisted=await db.StudentProgramMemberships.AsNoTracking().AnyAsync(x=>x.StudentId==applicant.Id&&x.InstitutionId==inst);
        Record(new{Probe="AdmissionAfterActorDisable",ObservedPostgresLockWait=waiting,ActorActiveBeforeRelease=active,Result=result.Status,MembershipPersisted=persisted});
        Require(result.Status=="Approved"&&persisted,"Probe did not reproduce unauthorized post-wait mutation.");
    }
}
finally { await fixture.DisposeAsync(); }