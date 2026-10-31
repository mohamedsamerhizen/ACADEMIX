using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Curriculum;
using Academix.Api.Domain.Records;
using Academix.Api.Domain.Registrar;
using Academix.Api.Middleware;
using Academix.Api.Services.Assessment;
using Academix.Api.Services.Curriculum;
using Academix.Api.Services.Institutional;
using Academix.Api.Services.Lifecycle;
using Academix.Api.Services.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academix.Tests.Integration;

public sealed partial class Phase19CompletionWorkflowTests
{
    private async Task<(Guid Plan,Guid Course)> FinalAuditTransition(Scenario s,Guid fromPlan,Guid source,int revision)
    {
        await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var course=Course.Create(Guid.NewGuid(),s.Inst,"RC"+revision.ToString("D3"),"Recognized replacement "+revision);
        var plan=new CurriculumPlan{InstitutionId=s.Inst,ProgramId=s.Program,CatalogYear=2026+revision,Revision=revision,Name="Recognition revision "+revision,Status="Published",PublishedAtUtc=DateTime.UtcNow,MinimumTotalCredits=3};
        db.AddRange(course,plan);db.ProgramCourses.Add(new(){InstitutionId=s.Inst,ProgramId=s.Program,CourseId=course.Id});
        db.CurriculumCourses.Add(new(){InstitutionId=s.Inst,PlanId=plan.Id,CourseId=course.Id,CreditHours=3,RequirementKind="Required",RecommendedTerm=1});
        db.CurriculumGradeBands.AddRange(new CurriculumGradeBand{InstitutionId=s.Inst,PlanId=plan.Id,MinimumPercentage=0,GradePoints=0,Letter="F"},new CurriculumGradeBand{InstitutionId=s.Inst,PlanId=plan.Id,MinimumPercentage=60,GradePoints=3,Letter="B"});
        await db.SaveChangesAsync();var service=scope.ServiceProvider.GetRequiredService<CurriculumService>();
        var transition=await service.RequestTransition(s.Admin,s.Inst,s.Student,new(Guid.NewGuid(),fromPlan,plan.Id,DateTime.UtcNow.AddSeconds(-1),"Independent recognition regression",[new(source,course.Id)]),default);
        await service.ReviewTransition(s.Reviewer,s.Inst,transition.Id,true,default);await service.ApplyTransition(s.Admin,s.Inst,transition.Id,default);
        return(plan.Id,course.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FinalAuditChainedRecognitionPreservesExternalCreditButRevalidatesCorrectedLocalSources(bool external)
    {
        var s=await Setup();var other=await Setup();await SeedPublishedHistory(other);
        if(!external)await SeedPublishedHistory(s);
        else
        {
            await using var original=fixture.Services.CreateAsyncScope();var lifecycle=original.ServiceProvider.GetRequiredService<LifecycleService>();
            var transferDb=original.ServiceProvider.GetRequiredService<AcademixDbContext>();
            transferDb.ProgramCourses.Add(new(){InstitutionId=s.Inst,ProgramId=s.Program,CourseId=s.Course});
            await transferDb.SaveChangesAsync();
            var petition=await lifecycle.RequestTransfer(s.StudentUser,new("Origin College",[new(s.Course,"EXT101","External academic achievement",3)]),default);
            await lifecycle.DecideTransfer(s.Admin,petition.Id,new(petition.Version,"Approve","Independent external achievement review"),default);
        }
        var first=await FinalAuditTransition(s,s.Plan,s.Course,2);var final=await FinalAuditTransition(s,first.Plan,first.Course,3);
        await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var degree=scope.ServiceProvider.GetRequiredService<DegreeAuditService>();var records=scope.ServiceProvider.GetRequiredService<RecordsService>();
        var lifecycleService=scope.ServiceProvider.GetRequiredService<LifecycleService>();var grades=scope.ServiceProvider.GetRequiredService<GradebookService>();
        Assert.Equal("Eligible",(await degree.Evaluate(s.StudentUser,s.Student,default)).Eligibility);
        Assert.Equal(403,(await Assert.ThrowsAsync<ApiFault>(()=>degree.Evaluate(other.Admin,s.Student,default))).StatusCode);
        Assert.Empty(await RecognitionQualification.Load(db,other.Inst,s.Student,final.Plan,default));
        var history=await db.Set<RecognizedCourseCredit>().AsNoTracking().Where(x=>x.InstitutionId==s.Inst&&x.StudentId==s.Student).OrderBy(x=>x.Id).ToListAsync();
        Assert.Equal(2,history.Count);Assert.All(history,x=>Assert.Equal(s.Course,x.SourceCourseId));
        var historyJson=System.Text.Json.JsonSerializer.Serialize(history);
        var dependent=Course.Create(Guid.NewGuid(),s.Inst,"RC104","Dependent advanced course");
        var section=new CourseSection{InstitutionId=s.Inst,CourseId=dependent.Id,TermId=s.Term,Code="DEP",Status="Open",Capacity=10};
        db.AddRange(dependent,section);db.CoursePrerequisites.Add(new(){InstitutionId=s.Inst,CourseId=dependent.Id,RequiredCourseId=final.Course,MinimumPercentage=60});await db.SaveChangesAsync();
        Assert.True((await records.EligibilityForSection(section.Id,s.Student,default)).Eligible);
        if(external)
        {
            Assert.False(await db.StudentTranscriptEntries.AnyAsync(x=>x.StudentId==s.Student));
            var qualified=Assert.Single(await RecognitionQualification.Load(db,s.Inst,s.Student,final.Plan,default));
            Assert.StartsWith("transfer:",qualified.Source);Assert.Equal(3,(await degree.Evaluate(s.StudentUser,s.Student,default)).Audit.EarnedTotalCredits);
            Assert.True((await records.EligibilityForSection(section.Id,s.Student,default)).Eligible);
        }
        else
        {
            var application=await lifecycleService.SubmitGraduation(s.StudentUser,new(Guid.NewGuid()),default);
            var approved=await lifecycleService.ReviewGraduation(s.Admin,application.Id,new(application.Version,"Approve","Current achievement reviewed"),default);
            var issued=await lifecycleService.IssueGraduation(s.Admin,application.Id,new(approved.Version),default);
            var current=await db.GradebookResults.AsNoTracking().SingleAsync(x=>x.StudentId==s.Student&&x.SectionId==s.Section);
            await grades.Correct(s.Admin,s.Section,s.Student,new(40,"Official correction removes original achievement",current.Version),default);
            await Assert.ThrowsAsync<ApiFault>(()=>degree.Evaluate(s.Admin,s.Student,default));
            await records.SyncTranscript(s.Admin,s.Section,default);
            Assert.Equal("NotEligible",(await degree.Evaluate(s.StudentUser,s.Student,default)).Eligibility);
            Assert.False(Assert.Single((await records.EligibilityForSection(section.Id,s.Student,default)).Requirements).Satisfied);
            Assert.Empty(await RecognitionQualification.Load(db,s.Inst,s.Student,final.Plan,default));
            Assert.Equal(409,(await Assert.ThrowsAsync<ApiFault>(()=>lifecycleService.ReassessCredential(s.Admin,s.Inst,issued.Id,"Review invalidated achievement",default))).StatusCode);
            Assert.Equal(403,(await Assert.ThrowsAsync<ApiFault>(()=>lifecycleService.ReassessCredential(s.StudentUser,s.Inst,issued.Id,"Student cannot reassess credential",default))).StatusCode);
            current=await db.GradebookResults.AsNoTracking().SingleAsync(x=>x.StudentId==s.Student&&x.SectionId==s.Section);
            await grades.Correct(s.Admin,s.Section,s.Student,new(80,"Authorized correction restores original achievement",current.Version),default);await records.SyncTranscript(s.Admin,s.Section,default);
            var reassessed=await lifecycleService.ReassessCredential(s.Reviewer,s.Inst,issued.Id,"Independent current-evidence reassessment",default);
            Assert.Null(reassessed.SourceInvalidatedAtUtc);Assert.Equal(issued.CredentialNumber,reassessed.CredentialNumber);
            Assert.Equal("Eligible",(await degree.Evaluate(s.StudentUser,s.Student,default)).Eligibility);
            Assert.True(Assert.Single((await records.EligibilityForSection(section.Id,s.Student,default)).Requirements).Satisfied);
        }
        var retained=await db.Set<RecognizedCourseCredit>().AsNoTracking().Where(x=>x.InstitutionId==s.Inst&&x.StudentId==s.Student).OrderBy(x=>x.Id).ToListAsync();
        Assert.Equal(historyJson,System.Text.Json.JsonSerializer.Serialize(retained));
        Assert.True((await scope.ServiceProvider.GetRequiredService<CurriculumService>().Audit(other.StudentUser,other.Student,default)).GraduationReady);
    }

    [Fact]
    public async Task FinalAuditCurrentTranscriptAndStandingRejectStalenessWhileHistoricalPdfRemainsAccessible()
    {
        var s=await Setup();var other=await Setup();await SeedPublishedHistory(s);await SeedPublishedHistory(other);
        await using var scope=fixture.Services.CreateAsyncScope();var records=scope.ServiceProvider.GetRequiredService<RecordsService>();
        var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();var grades=scope.ServiceProvider.GetRequiredService<GradebookService>();
        await records.RecalculateStanding(s.Admin,s.Term,s.Student,default);await records.RecalculateStanding(other.Admin,other.Term,other.Student,default);
        var document=await Issue(s,"Transcript");var documents=scope.ServiceProvider.GetRequiredService<InstitutionalDocumentService>();
        var original=(await documents.Download(s.StudentUser,document.Id,default)).Bytes;
        var current=await db.GradebookResults.AsNoTracking().SingleAsync(x=>x.StudentId==s.Student&&x.SectionId==s.Section);
        await grades.Correct(s.Admin,s.Section,s.Student,new(40,"Official academic correction for freshness regression",current.Version),default);
        Assert.Equal(409,(await Assert.ThrowsAsync<ApiFault>(()=>records.Transcript(s.StudentUser,s.Student,default))).StatusCode);
        await Assert.ThrowsAsync<ApiFault>(()=>records.Standing(s.StudentUser,s.Term,s.Student,default));
        Assert.Equal(80,Assert.Single((await records.Transcript(other.StudentUser,other.Student,default)).Entries).Percentage);
        Assert.Equal("GoodStanding",(await records.Standing(other.StudentUser,other.Term,other.Student,default)).Status);
        await records.SyncTranscript(s.Admin,s.Section,default);
        Assert.Equal(40,Assert.Single((await records.Transcript(s.StudentUser,s.Student,default)).Entries).Percentage);
        Assert.Equal(409,(await Assert.ThrowsAsync<ApiFault>(()=>records.Standing(s.StudentUser,s.Term,s.Student,default))).StatusCode);
        var recalculated=await records.RecalculateStanding(s.Reviewer,s.Term,s.Student,default);
        var standing=await records.Standing(s.StudentUser,s.Term,s.Student,default);
        Assert.Equal(recalculated,standing);Assert.NotEqual("GoodStanding",standing.Status);Assert.Equal(40,standing.AveragePercentage);
        Assert.Equal(original,(await documents.Download(s.StudentUser,document.Id,default)).Bytes);
        Assert.Equal("SourceChanged",(await documents.Verify(new(document.ReferenceNumber,document.VerificationProof),default)).Status);
        Assert.Equal(403,(await Assert.ThrowsAsync<ApiFault>(()=>records.Transcript(s.StudentUser,other.Student,default))).StatusCode);
    }
}
