using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Curriculum;
using Academix.Api.Domain.Finance;
using Academix.Api.Domain.Identity;
using Academix.Api.Domain.Records;
using Academix.Api.DTOs.Finance;
using Academix.Api.Middleware;
using Academix.Api.Services.Finance;
using Academix.Api.Services.Infrastructure;
using Academix.Api.Services.Institutional;
using Academix.Api.Services.Registrar;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Academix.Tests.Integration;

// These scenarios use the real PostgreSQL fixture. They are compilation/discovery
// evidence only during the explicitly container-disabled Phase21 implementation.
#pragma warning disable EF1002 // Fault-injection SQL identifiers/literals are generated GUIDs only.
public sealed class Phase21FinanceTests(ApiFixture fixture):IClassFixture<ApiFixture>
{
    private sealed record Scenario(Guid Inst,Guid Student,Guid OtherStudent,Guid AdminId,Guid ReviewerId,Guid Program,Guid Term,Guid Course,Guid Section,Guid Account,Guid Schedule,
        ClaimsPrincipal StudentUser,ClaimsPrincipal OtherStudentUser,ClaimsPrincipal Admin,ClaimsPrincipal Reviewer);
    private static ClaimsPrincipal Principal(Guid id,Guid inst)=>new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier,id.ToString()),new("institution_id",inst.ToString())],"test"));
    private async Task<Scenario> Setup(bool restrict=false)
    {
        await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var inst=new Institution{Code=Guid.NewGuid().ToString("N")[..12],Name="Finance University"};db.Add(inst);await db.SaveChangesAsync();
        var manager=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        async Task<Guid> User(string role)
        {var email=Guid.NewGuid().ToString("N")+"@example.test";var row=new ApplicationUser{Id=Guid.NewGuid(),InstitutionId=inst.Id,UserName=email,Email=email};Assert.True((await manager.CreateAsync(row,"Safe!FinanceTests2026")).Succeeded);Assert.True((await manager.AddToRoleAsync(row,role)).Succeeded);return row.Id;}
        var admin=await User(AcademixRoles.InstitutionAdministrator);var reviewer=await User(AcademixRoles.InstitutionAdministrator);var student=await User(AcademixRoles.Student);var other=await User(AcademixRoles.Student);
        var department=new Department{InstitutionId=inst.Id,Code="D",Name="Department"};var program=new AcademicProgram{InstitutionId=inst.Id,DepartmentId=department.Id,Code="P",Name="Program"};
        var today=FinanceRules.Today("Asia/Baghdad");var term=new AcademicTerm{InstitutionId=inst.Id,Code="T",Name="Term",Status="Active",StartsOn=today.AddMonths(-1),EndsOn=today.AddMonths(4)};
        var course=Course.Create(Guid.NewGuid(),inst.Id,"FIN101","Foundations");var section=new CourseSection{InstitutionId=inst.Id,CourseId=course.Id,TermId=term.Id,Code="A",Status="Open",Capacity=30};
        var plan=new CurriculumPlan{InstitutionId=inst.Id,ProgramId=program.Id,CatalogYear=2026,Revision=1,Name="Curriculum",Status="Published",PublishedAtUtc=DateTime.UtcNow,MinimumTotalCredits=3,MinimumCumulativeGpa=2};
        db.AddRange(department,program,term,course,section,plan);db.ProgramCourses.Add(new(){InstitutionId=inst.Id,ProgramId=program.Id,CourseId=course.Id});
        db.CurriculumCourses.Add(new(){InstitutionId=inst.Id,PlanId=plan.Id,CourseId=course.Id,CreditHours=3,RecommendedTerm=1});
        db.CurriculumGradeBands.AddRange(new CurriculumGradeBand{InstitutionId=inst.Id,PlanId=plan.Id,MinimumPercentage=0,GradePoints=0,Letter="F"},new CurriculumGradeBand{InstitutionId=inst.Id,PlanId=plan.Id,MinimumPercentage=60,GradePoints=3,Letter="B"});
        db.StudentProgramMemberships.Add(new(){InstitutionId=inst.Id,StudentId=student,ProgramId=program.Id,AdmissionTermId=term.Id,CurrentTermId=term.Id});
        db.StudentCurriculumAssignments.Add(new(){InstitutionId=inst.Id,StudentId=student,PlanId=plan.Id,AssignedById=admin});db.StudentEnrollments.Add(new(){InstitutionId=inst.Id,StudentId=student,SectionId=section.Id});
        db.StudentTranscriptEntries.Add(new(){InstitutionId=inst.Id,StudentId=student,CourseId=course.Id,SectionId=section.Id,TermId=term.Id,CourseCode=course.Code,CourseTitle=course.Title,Percentage=80,Passed=true,Letter="B",GradebookVersion=1});await db.SaveChangesAsync();
        var finance=scope.ServiceProvider.GetRequiredService<FinanceService>();var adminUser=Principal(admin,inst.Id);var reviewerUser=Principal(reviewer,inst.Id);
        await finance.SetAuthority(adminUser,inst.Id,admin,new(Guid.NewGuid(),0,(FinancePermission)255),default);await finance.SetAuthority(adminUser,inst.Id,reviewer,new(Guid.NewGuid(),0,(FinancePermission)255),default);
        await finance.ConfigurePolicy(adminUser,inst.Id,Policy("USD",restrict),default);
        var account=await finance.OpenAccount(adminUser,inst.Id,student,new(Guid.NewGuid(),"USD"),default);
        var schedule=await finance.CreateSchedule(adminUser,inst.Id,Schedule(program.Id,term.Id,"USD"),default);
        await finance.ScheduleAction(adminUser,schedule.Id,new(Guid.NewGuid(),1,"Publish","Published tuition evidence"),default);
        return new(inst.Id,student,other,admin,reviewer,program.Id,term.Id,course.Id,section.Id,account.Id,schedule.Id,Principal(student,inst.Id),Principal(other,inst.Id),adminUser,reviewerUser);
    }
    private static PolicyInput Policy(string currency,bool restrict=false)
    {var today=FinanceRules.Today("Asia/Baghdad");return new(Guid.NewGuid(),currency,"Asia/Baghdad",today.AddMonths(-6),today.AddMonths(6),true,true,true,3,365,true,0,0,restrict,restrict,restrict);}
    private static FeeScheduleInput Schedule(Guid program,Guid term,string currency)
    {var today=FinanceRules.Today("Asia/Baghdad");return new(Guid.NewGuid(),program,term,null,currency,"TUITION",1,today.AddMonths(-6),today.AddMonths(6),[new("TUITION","Tuition","PerCredit",1000,false),new("REG","Registration","Fixed",1000,false)]);}
    private async Task<InvoiceView> Invoice(Scenario s)
    {await using var scope=fixture.Services.CreateAsyncScope();var finance=scope.ServiceProvider.GetRequiredService<FinanceService>();var draft=await finance.Assess(s.Admin,s.Account,new(Guid.NewGuid(),s.Schedule,s.Term,FinanceRules.Today("Asia/Baghdad").AddDays(-10),[]),default);await finance.InvoiceAction(s.Admin,draft.Id,new(Guid.NewGuid(),1,"Post","Authoritative tuition posting"),default);return await finance.InvoiceDetail(s.StudentUser,draft.Id,default);}
    private async Task<FinanceResult> PostedPayment(Scenario s,long amount)
    {await using var scope=fixture.Services.CreateAsyncScope();var finance=scope.ServiceProvider.GetRequiredService<FinanceService>();var payment=await finance.RegisterPayment(s.Admin,s.Account,new(Guid.NewGuid(),"USD","Cash","CASH-"+Guid.NewGuid().ToString("N"),amount),default);await finance.PaymentAction(s.Reviewer,payment.Id,new(Guid.NewGuid(),1,"Verify","Cash count independently checked"),default);return await finance.PaymentAction(s.Admin,payment.Id,new(Guid.NewGuid(),2,"Post","Confirmed cash received internally"),default);}

    [Fact]
    public async Task BillingCashAllocationRefundAndReversalsReconcileWithVerifiableReceipts()
    {
        var s=await Setup();var invoice=await Invoice(s);Assert.Equal(4000,invoice.OriginalMinor);var payment=await PostedPayment(s,6000);
        await using var scope=fixture.Services.CreateAsyncScope();var finance=scope.ServiceProvider.GetRequiredService<FinanceService>();
        await finance.AllocatePayment(s.Admin,payment.Id,new(Guid.NewGuid(),invoice.Id,"USD",4000),default);
        var statement=await finance.Statement(s.StudentUser,s.Account,1,null,null,default);Assert.Equal(0,statement.Balances.ReceivableMinor);Assert.Equal(2000,statement.Balances.UnappliedCashMinor);Assert.Equal(2000,statement.Balances.RefundableMinor);
        var receipt=await finance.Receipt(s.StudentUser,payment.Id,default);Assert.True((await finance.VerifyReceipt(new(receipt.Number,receipt.Proof),default)).Valid);
        await scope.ServiceProvider.GetRequiredService<InstitutionalDocumentService>().RotateKey(s.Admin,s.Inst,default);Assert.True((await finance.VerifyReceipt(new(receipt.Number,receipt.Proof),default)).Valid);
        var pdf=await finance.ReceiptPdf(s.StudentUser,payment.Id,default);Assert.Equal("%PDF-",System.Text.Encoding.ASCII.GetString(pdf.Bytes,0,5));
        var refund=await finance.RequestRefund(s.StudentUser,payment.Id,new(Guid.NewGuid(),"USD",1500,"Return eligible cash credit"),default);
        await finance.RefundDecision(s.Reviewer,refund.Id,new(Guid.NewGuid(),1,"Approve","Refund eligibility independently checked"),default);
        var execute=new RefundAction(Guid.NewGuid(),2,"Execute","Record authorized disbursement instruction outcome","REF-"+Guid.NewGuid().ToString("N"));
        await finance.RefundDecision(s.Admin,refund.Id,execute,default);await finance.RefundDecision(s.Admin,refund.Id,execute,default);
        Assert.Equal(4500,(await finance.Statement(s.StudentUser,s.Account,1,null,null,default)).Balances.NetRecordedFundsMinor);
        var verified=await finance.VerifyReceipt(new(receipt.Number,receipt.Proof),default);Assert.Equal("PartiallyRefunded",verified.Status);Assert.Equal(1500,verified.RefundedMinor);
        await finance.RefundDecision(s.Reviewer,refund.Id,new(Guid.NewGuid(),3,"Reverse","Correct the recorded disbursement; no bank call"),default);
        await finance.PaymentAction(s.Admin,payment.Id,new(Guid.NewGuid(),3,"Reverse","Correct original recorded receipt"),default);
        statement=await finance.Statement(s.StudentUser,s.Account,1,null,null,default);Assert.Equal(4000,statement.Balances.ReceivableMinor);Assert.Equal(0,statement.Balances.UnappliedCashMinor);Assert.Equal(0,statement.Balances.NetRecordedFundsMinor);
        Assert.True((await finance.ReconcileAccount(s.Admin,s.Account,default)).Balanced);Assert.Equal("Reversed",(await finance.VerifyReceipt(new(receipt.Number,receipt.Proof),default)).Status);
        var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();var rows=await db.Set<FinanceJournalLine>().Where(x=>db.Set<FinanceJournal>().Any(j=>j.Id==x.JournalId&&j.AccountId==s.Account)).GroupBy(x=>x.JournalId).Select(g=>new{Count=g.Count(),Total=g.Sum(x=>x.SignedMinor)}).ToListAsync();Assert.All(rows,x=>{Assert.Equal(2,x.Count);Assert.Equal(0,x.Total);});
        Assert.Equal(80,await db.StudentTranscriptEntries.Where(x=>x.StudentId==s.Student&&x.CourseId==s.Course).Select(x=>x.Percentage).SingleAsync());Assert.Equal("Active",await db.StudentEnrollments.Where(x=>x.StudentId==s.Student&&x.SectionId==s.Section).Select(x=>x.Status).SingleAsync());
    }
    [Fact]
    public async Task RepeatedConcurrentPostingAndDuplicateReferencesCannotCollectMoneyTwice()
    {
        var s=await Setup();await using var scope=fixture.Services.CreateAsyncScope();var finance=scope.ServiceProvider.GetRequiredService<FinanceService>();var reference="BANK-"+Guid.NewGuid().ToString("N");
        var input=new PaymentInput(Guid.NewGuid(),"USD","BankTransfer",reference,6000);var pending=await finance.RegisterPayment(s.StudentUser,s.Account,input,default);
        Assert.Equal(pending.Id,(await finance.RegisterPayment(s.StudentUser,s.Account,input,default)).Id);Assert.Equal(0,(await finance.Statement(s.StudentUser,s.Account,1,null,null,default)).Balances.NetRecordedFundsMinor);
        await Assert.ThrowsAsync<ApiFault>(()=>finance.PaymentAction(s.Admin,pending.Id,new(Guid.NewGuid(),1,"Post","Cannot post unverified claim"),default));
        await finance.PaymentAction(s.Reviewer,pending.Id,new(Guid.NewGuid(),1,"Verify","Bank evidence manually checked"),default);
        var command=new FinanceAction(Guid.NewGuid(),2,"Post","Post verified transfer reference");
        async Task<FinanceResult> Post(){await using var own=fixture.Services.CreateAsyncScope();return await own.ServiceProvider.GetRequiredService<FinanceService>().PaymentAction(s.Admin,pending.Id,command,default);}
        var results=await Task.WhenAll(Post(),Post());Assert.Equal(results[0],results[1]);
        await Assert.ThrowsAsync<ApiFault>(()=>finance.RegisterPayment(s.Admin,s.Account,new(Guid.NewGuid(),"USD","BankTransfer",reference.ToLowerInvariant(),6000),default));
        await Assert.ThrowsAsync<ApiFault>(()=>finance.RegisterPayment(s.Admin,s.Account,input with{AmountMinor=6001},default));
        var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();Assert.Equal(1,await db.Set<FinanceJournal>().CountAsync(x=>x.SourceId==pending.Id&&x.Kind=="PaymentPosted"));Assert.Equal(1,await db.Set<FinanceReceipt>().CountAsync(x=>x.PaymentId==pending.Id));Assert.Equal(6000,(await finance.Statement(s.StudentUser,s.Account,1,null,null,default)).Balances.NetRecordedFundsMinor);
    }
    [Fact]
    public async Task SimultaneousRefundRequestsReserveOnlyAvailableCash()
    {
        var s=await Setup();var invoice=await Invoice(s);var payment=await PostedPayment(s,6000);await using var scope=fixture.Services.CreateAsyncScope();var finance=scope.ServiceProvider.GetRequiredService<FinanceService>();await finance.AllocatePayment(s.Admin,payment.Id,new(Guid.NewGuid(),invoice.Id,"USD",4000),default);
        async Task<int> Request(){await using var own=fixture.Services.CreateAsyncScope();try{await own.ServiceProvider.GetRequiredService<FinanceService>().RequestRefund(s.StudentUser,payment.Id,new(Guid.NewGuid(),"USD",1500,"Concurrent refund request"),default);return 200;}catch(ApiFault e){return e.StatusCode;}}
        var results=await Task.WhenAll(Request(),Request());Assert.Equal(new[]{200,409},results.Order().ToArray());
        var statement=await finance.Statement(s.StudentUser,s.Account,1,null,null,default);Assert.Equal(1500,statement.Balances.ReservedRefundMinor);Assert.Equal(500,statement.Balances.RefundableMinor);Assert.Equal(6000,statement.Balances.NetRecordedFundsMinor);Assert.True((await finance.ReconcileAccount(s.Admin,s.Account,default)).Balanced);
    }
    [Fact]
    public async Task PaidInvoiceScholarshipsCreditNotesRevocationAndReschedulingUseCorrectingEntries()
    {
        var s=await Setup();var invoice=await Invoice(s);var payment=await PostedPayment(s,6000);await using var scope=fixture.Services.CreateAsyncScope();var finance=scope.ServiceProvider.GetRequiredService<FinanceService>();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();await finance.AllocatePayment(s.Admin,payment.Id,new(Guid.NewGuid(),invoice.Id,"USD",4000),default);
        var today=FinanceRules.Today("Asia/Baghdad");var programme=await finance.CreateScholarship(s.Admin,s.Inst,new(Guid.NewGuid(),"MERIT",1,"USD",s.Program,s.Term,1,"Tuition","Percent",5000,0,2000,3,false,today.AddDays(-1),today.AddMonths(1)),default);
        var award=await finance.RequestAward(s.StudentUser,invoice.Id,new(Guid.NewGuid(),programme.Id,"Merit application"),default);await finance.AwardAction(s.Reviewer,award.Id,new(Guid.NewGuid(),1,"Approve","Authoritative GPA and available budget checked"),default);
        Assert.Equal(2500,(await finance.InvoiceDetail(s.StudentUser,invoice.Id,default)).NetChargesMinor);Assert.Equal(3500,(await finance.Statement(s.StudentUser,s.Account,1,null,null,default)).Balances.UnappliedCashMinor);
        await Assert.ThrowsAsync<ApiFault>(()=>finance.RequestAward(s.StudentUser,invoice.Id,new(Guid.NewGuid(),programme.Id,"Cannot double-discount"),default));
        var line=Assert.Single(invoice.Lines,x=>x.Code=="REG");await finance.Adjust(s.Admin,invoice.Id,new(Guid.NewGuid(),invoice.Version,line.Id,"Credit",500,"USD","Correct registration charge"),default);
        Assert.Equal(2000,(await finance.InvoiceDetail(s.StudentUser,invoice.Id,default)).NetChargesMinor);Assert.Equal(4000,(await finance.Statement(s.StudentUser,s.Account,1,null,null,default)).Balances.UnappliedCashMinor);
        var revoke=new FinanceAction(Guid.NewGuid(),2,"Revoke","Authorized scholarship withdrawal");
        await finance.AwardAction(s.Admin,award.Id,revoke,default);var movements=await db.Set<FinanceJournal>().CountAsync(x=>x.AccountId==s.Account);
        await finance.AwardAction(s.Admin,award.Id,revoke,default);Assert.Equal(movements,await db.Set<FinanceJournal>().CountAsync(x=>x.AccountId==s.Account));
        await Assert.ThrowsAsync<ApiFault>(()=>finance.AwardAction(s.StudentUser,award.Id,revoke,default));
        var awardEvents=await db.InstitutionalOutboxEvents.Where(x=>x.InstitutionId==s.Inst&&x.Kind=="FinanceAwardReviewed").ToArrayAsync();
        Assert.Equal(2,awardEvents.Length);Assert.Equal(2,awardEvents.Select(x=>x.DeduplicationKey).Distinct().Count());
        Assert.All(awardEvents,x=>Assert.Contains(award.Id.ToString(),x.PayloadJson));
        Assert.Equal(1500,(await finance.InvoiceDetail(s.StudentUser,invoice.Id,default)).OutstandingMinor);
        var journals=await db.Set<FinanceJournal>().CountAsync(x=>x.AccountId==s.Account);
        var plan=await finance.ProposePlan(s.StudentUser,invoice.Id,new(Guid.NewGuid(),[today.AddDays(30),today.AddDays(60)],"Proposed installments"),default);await finance.DecidePlan(s.Reviewer,plan.Id,new(Guid.NewGuid(),1,"Approve","Debt and due dates checked"),default);
        var revision=await finance.ProposePlan(s.StudentUser,invoice.Id,new(Guid.NewGuid(),[today.AddDays(90),today.AddDays(120)],"Reschedule unchanged debt"),default);await finance.DecidePlan(s.Reviewer,revision.Id,new(Guid.NewGuid(),1,"Approve","Revised due dates approved"),default);
        Assert.Equal(journals,await db.Set<FinanceJournal>().CountAsync(x=>x.AccountId==s.Account));Assert.Equal(1,await db.Set<FinancePaymentPlan>().CountAsync(x=>x.InvoiceId==invoice.Id&&x.Status=="Approved"));Assert.Equal("Superseded",await db.Set<FinancePaymentPlan>().Where(x=>x.Id==plan.Id).Select(x=>x.Status).SingleAsync());Assert.True((await finance.ReconcileAccount(s.Admin,s.Account,default)).Balanced);
    }
    [Fact]
    public async Task FailureAfterJournalWriteRollsBackLiabilityOutboxAndIdempotencyTogether()
    {
        var s=await Setup();await using var scope=fixture.Services.CreateAsyncScope();var finance=scope.ServiceProvider.GetRequiredService<FinanceService>();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var draft=await finance.Assess(s.Admin,s.Account,new(Guid.NewGuid(),s.Schedule,s.Term,FinanceRules.Today("Asia/Baghdad"),[]),default);var command=new FinanceAction(Guid.NewGuid(),1,"Post","Failure after journal persistence");var name="p21_failure_"+s.Inst.ToString("N");
        await db.Database.ExecuteSqlRawAsync($"""
CREATE FUNCTION {name}() RETURNS trigger LANGUAGE plpgsql AS $body$ BEGIN IF NEW."RequestId"='{command.RequestId}'::uuid THEN RAISE EXCEPTION 'Injected final command write failure'; END IF; RETURN NEW; END $body$;
CREATE TRIGGER {name} BEFORE INSERT ON finance_commands FOR EACH ROW EXECUTE FUNCTION {name}();
""");
        try{await Assert.ThrowsAsync<DbUpdateException>(()=>finance.InvoiceAction(s.Admin,draft.Id,command,default));}
        finally{await db.Database.ExecuteSqlRawAsync($"DROP TRIGGER {name} ON finance_commands; DROP FUNCTION {name}();");}
        Assert.Equal("Draft",await db.Set<FinanceInvoice>().Where(x=>x.Id==draft.Id).Select(x=>x.Status).SingleAsync());Assert.False(await db.Set<FinanceJournal>().AnyAsync(x=>x.InvoiceId==draft.Id));Assert.False(await db.Set<FinanceCommand>().AnyAsync(x=>x.RequestId==command.RequestId));Assert.False(await db.InstitutionalOutboxEvents.AnyAsync(x=>x.InstitutionId==s.Inst&&x.Kind=="FinanceInvoicePosted"));
        await finance.InvoiceAction(s.Admin,draft.Id,command,default);Assert.Equal(4000,(await finance.Statement(s.StudentUser,s.Account,1,null,null,default)).Balances.ReceivableMinor);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Set<FinanceInvoice>().Where(x=>x.Id==draft.Id).ExecuteUpdateAsync(set=>set.SetProperty(x=>x.TotalMinor,1)));
        await using var invalidScope=fixture.Services.CreateAsyncScope();var invalid=invalidScope.ServiceProvider.GetRequiredService<AcademixDbContext>();var bad=new FinanceJournal{InstitutionId=s.Inst,AccountId=s.Account,Currency="USD",Kind="DebitAdjustment",SourceId=Guid.NewGuid(),InvoiceId=draft.Id,AmountMinor=1,ActorId=s.AdminId,PostingKey="unbalanced-"+Guid.NewGuid().ToString("N"),Reason="Missing journal legs"};invalid.Add(bad);
        var error=await Assert.ThrowsAsync<DbUpdateException>(()=>invalid.SaveChangesAsync());Assert.Equal("23514",Assert.IsType<Npgsql.PostgresException>(error.InnerException).SqlState);Assert.False(await db.Set<FinanceJournal>().AnyAsync(x=>x.Id==bad.Id));
    }
    [Fact]
    public async Task FinancialHoldsRespectPolicyDueDatesStudentAccessAndApprovedRescheduling()
    {
        var s=await Setup(true);var invoice=await Invoice(s);await using var scope=fixture.Services.CreateAsyncScope();var finance=scope.ServiceProvider.GetRequiredService<FinanceService>();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var blocked=await scope.ServiceProvider.GetRequiredService<AcademicPolicyEngine>().Evaluate(s.Inst,s.Student,s.Section,true,default);Assert.Contains(blocked.Reasons,x=>x.StartsWith("FinancialOverdue:",StringComparison.Ordinal));
        Assert.Equal(4000,(await finance.Statement(s.StudentUser,s.Account,1,null,null,default)).Balances.ReceivableMinor);
        await FinanceDebt.RequireClearance(db,s.Inst,s.Student,"Graduation",DateTime.UtcNow.AddDays(-1),default);
        await Assert.ThrowsAsync<ApiFault>(()=>FinanceDebt.RequireClearance(db,s.Inst,s.Student,"Graduation",DateTime.UtcNow,default));
        await Assert.ThrowsAsync<ApiFault>(()=>FinanceDebt.RequireClearance(db,s.Inst,s.Student,"Documents",DateTime.UtcNow,default));
        var today=FinanceRules.Today("Asia/Baghdad");var plan=await finance.ProposePlan(s.StudentUser,invoice.Id,new(Guid.NewGuid(),[today.AddDays(30),today.AddDays(60)],"Approved installment relief"),default);await finance.DecidePlan(s.Reviewer,plan.Id,new(Guid.NewGuid(),1,"Approve","Rescheduling replaces overdue obligations"),default);
        Assert.Empty(await FinanceDebt.Restrictions(db,s.Inst,s.Student,"Registration",DateTime.UtcNow,default));Assert.Equal(3,await db.Set<FinanceHold>().CountAsync(x=>x.AccountId==s.Account&&x.Status=="Released"));
        var payment=await PostedPayment(s,4000);await finance.AllocatePayment(s.Admin,payment.Id,new(Guid.NewGuid(),invoice.Id,"USD",4000),default);Assert.False(await db.Set<FinanceHold>().AnyAsync(x=>x.AccountId==s.Account&&x.Status=="Open"));
        await Assert.ThrowsAsync<ApiFault>(()=>finance.Statement(s.OtherStudentUser,s.Account,1,null,null,default));
    }
    [Fact]
    public async Task ReportingKeepsCurrenciesSeparateAndReconciliationNeverCreatesCashOrBankConfirmation()
    {
        var s=await Setup();var invoice=await Invoice(s);var payment=await PostedPayment(s,6000);await using var scope=fixture.Services.CreateAsyncScope();var finance=scope.ServiceProvider.GetRequiredService<FinanceService>();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();await finance.AllocatePayment(s.Admin,payment.Id,new(Guid.NewGuid(),invoice.Id,"USD",4000),default);
        await finance.ConfigurePolicy(s.Admin,s.Inst,Policy("IQD"),default);var account=await finance.OpenAccount(s.Admin,s.Inst,s.Student,new(Guid.NewGuid(),"IQD"),default);var schedule=await finance.CreateSchedule(s.Admin,s.Inst,Schedule(s.Program,s.Term,"IQD"),default);await finance.ScheduleAction(s.Admin,schedule.Id,new(Guid.NewGuid(),1,"Publish","IQD schedule"),default);var draft=await finance.Assess(s.Admin,account.Id,new(Guid.NewGuid(),schedule.Id,s.Term,FinanceRules.Today("Asia/Baghdad"),[]),default);await finance.InvoiceAction(s.Admin,draft.Id,new(Guid.NewGuid(),1,"Post","IQD assessment"),default);
        await Assert.ThrowsAsync<ApiFault>(()=>finance.AllocatePayment(s.Admin,payment.Id,new(Guid.NewGuid(),draft.Id,"IQD",1),default));
        var today=FinanceRules.Today("Asia/Baghdad");var report=await finance.Report(s.Admin,s.Inst,today.AddDays(-1),today.AddDays(1),default);Assert.Equal(new[]{"IQD","USD"},report.Currencies.Select(x=>x.Currency).ToArray());var usd=Assert.Single(report.Currencies,x=>x.Currency=="USD");Assert.Equal(6000,usd.CollectedMinor);Assert.Equal(0,usd.ReceivablesMinor);Assert.Equal(2000,usd.UnappliedMinor);Assert.Equal(4000,Assert.Single(usd.AllocatedByTerm.Items).AmountMinor);Assert.Equal(4000,Assert.Single(report.Currencies,x=>x.Currency=="IQD").ReceivablesMinor);
        var record=await db.Set<FinancePayment>().AsNoTracking().SingleAsync(x=>x.Id==payment.Id);var before=await db.Set<FinanceJournal>().CountAsync(x=>x.AccountId==s.Account);
        var batch=await finance.ReconcileBank(s.Admin,s.Inst,new(Guid.NewGuid(),"Provided test statement",[new(record.Reference,"USD",6000,today),new("UNMATCHED-"+Guid.NewGuid().ToString("N"),"USD",500,today)]),default);var comparison=await finance.BankReconciliation(s.Admin,batch.Id,default);Assert.Contains("MatchedProvidedData",comparison.ResultsJson);Assert.Contains("MissingInternal",comparison.ResultsJson);Assert.Equal(before,await db.Set<FinanceJournal>().CountAsync(x=>x.AccountId==s.Account));
        var eventId=await db.InstitutionalOutboxEvents.Where(x=>x.InstitutionId==s.Inst&&x.Kind=="FinancePaymentPosted").Select(x=>x.Id).SingleAsync();
        for(var i=0;i<20&&!await db.InstitutionalOutboxEvents.AnyAsync(x=>x.Id==eventId&&x.Status=="Delivered");i++)await scope.ServiceProvider.GetRequiredService<OutboxService>().ProcessBatch(default);
        var notification=await db.InAppNotifications.SingleAsync(x=>x.SourceId==eventId&&x.RecipientId==s.Student);Assert.Equal("Finance",notification.Kind);Assert.DoesNotContain(record.Reference,notification.Body);Assert.DoesNotContain("6000",notification.Body);
    }
    [Fact]
    public async Task FinancialPermissionRevocationAndInstitutionOwnershipApplyEvenToIdempotentRetries()
    {
        var s=await Setup();var foreign=await Setup();await using var scope=fixture.Services.CreateAsyncScope();var finance=scope.ServiceProvider.GetRequiredService<FinanceService>();
        await Assert.ThrowsAsync<ApiFault>(()=>finance.Statement(foreign.Admin,s.Account,1,null,null,default));
        await Assert.ThrowsAsync<ApiFault>(()=>finance.SetAuthority(s.Admin,s.Inst,s.Student,new(Guid.NewGuid(),0,FinancePermission.Approve),default));
        var draft=await finance.Assess(s.Admin,s.Account,new(Guid.NewGuid(),s.Schedule,s.Term,FinanceRules.Today("Asia/Baghdad"),[]),default);var command=new FinanceAction(Guid.NewGuid(),1,"Post","Posted before role revocation");await finance.InvoiceAction(s.Admin,draft.Id,command,default);
        var manager=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();var actor=await manager.FindByIdAsync(s.AdminId.ToString());Assert.True((await manager.RemoveFromRoleAsync(actor!,AcademixRoles.InstitutionAdministrator)).Succeeded);
        Assert.Equal(403,(await Assert.ThrowsAsync<ApiFault>(()=>finance.InvoiceAction(s.Admin,draft.Id,command,default))).StatusCode);
        Assert.Equal(403,(await Assert.ThrowsAsync<ApiFault>(()=>finance.Statement(s.Admin,s.Account,1,null,null,default))).StatusCode);
        Assert.Equal(4000,(await finance.Statement(s.StudentUser,s.Account,1,null,null,default)).Balances.ReceivableMinor);
    }
}
