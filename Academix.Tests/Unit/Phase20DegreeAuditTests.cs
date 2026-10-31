using Academix.Api.DTOs.Advising;
using Academix.Api.DTOs.Curriculum;
using Academix.Api.Middleware;
using Academix.Api.Services.Curriculum;

namespace Academix.Tests.Unit;

public sealed class Phase20DegreeAuditTests
{
    private static readonly Guid Required=Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Elective=Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static DegreeAuditData Data()=>new(Guid.NewGuid(),Guid.NewGuid(),2026,1,6,3,2,
        [new(Required,"REQ","Required course","Required",3,1),new(Elective,"ELE","Elective course","Elective",3,2)],
        [new(0,0,"F"),new(60,2,"C"),new(80,4,"A")],
        [Attempt(Required,80),Attempt(Elective,80)],[],[],new HashSet<Guid>(),[],[],[],2,0);
    private static TranscriptAttempt Attempt(Guid course,decimal grade,bool? passed=null)=>new(course,Guid.NewGuid(),course.ToString(),grade,passed??grade>=60);
    [Fact] public void CompletedRequirementsAreEligibleWithExplainableEvidence()
    {var result=DegreeAuditEngine.Evaluate(Data());Assert.True(result.Audit.GraduationReady);Assert.Equal("Eligible",result.Eligibility);Assert.Equal(6,result.Audit.EarnedTotalCredits);Assert.Equal(100,result.CompletionPercentage);Assert.All(result.Requirements,x=>Assert.Equal("Satisfied",x.Status));}
    [Fact] public void MissingMandatoryCourseBlocksEvenWhenElectiveCreditsCoverTotal()
    {
        var d=Data();var other=Guid.NewGuid();d=d with{Courses=d.Courses.Append(new(other,"E2","Extra","Elective",3,2)).ToArray(),Attempts=[Attempt(Elective,80),Attempt(other,80)]};
        var r=DegreeAuditEngine.Evaluate(d);Assert.Equal(6,r.Audit.EarnedTotalCredits);Assert.False(r.Audit.GraduationReady);Assert.Equal(1,r.Audit.MissingRequiredCourses);Assert.Contains(r.Requirements,x=>x.Code=="Required:"+Required&&x.Status=="Unsatisfied");
    }
    [Fact] public void ElectiveGroupMustBeSatisfiedInAdditionToOverallCreditTotals()
    {
        var d=Data();var other=Guid.NewGuid();d=d with{Courses=d.Courses.Append(new(other,"LAB","Lab","Elective",3,2)).ToArray(),Groups=[new("Laboratories",3,[other])]};
        var r=DegreeAuditEngine.Evaluate(d);Assert.False(r.Audit.GraduationReady);Assert.Contains(r.Requirements,x=>x.Code=="ElectiveGroup:Laboratories"&&x.Status=="Unsatisfied");
    }
    [Fact] public void OverlappingGroupsAreRejectedInsteadOfSharingOneAchievement()
    {var d=Data();Assert.Throws<ApiFault>(()=>DegreeAuditEngine.Evaluate(d with{Groups=[new("A",3,[Elective]),new("B",3,[Elective])]}));}
    [Fact] public void RepeatedAttemptsUseBestGradeAndEarnCreditOnce()
    {var d=Data();var r=DegreeAuditEngine.Evaluate(d with{Attempts=[Attempt(Required,20),Attempt(Required,30),Attempt(Required,90),Attempt(Elective,80)]});Assert.Equal(6,r.Audit.EarnedTotalCredits);Assert.Equal(4,r.Audit.CumulativeGpa);Assert.Contains(r.Indicators,x=>x.Code=="RepeatedFailure"&&x.CourseId==Required);}
    [Fact] public void RecognizedCreditDoesNotInventGpaOrDuplicateNativeCredit()
    {var d=Data();var r=DegreeAuditEngine.Evaluate(d with{Attempts=[Attempt(Required,80)],Recognitions=[new(Elective,"external:X"),new(Required,"external:Y")]});Assert.Equal(6,r.Audit.EarnedTotalCredits);Assert.Equal(4,r.Audit.CumulativeGpa);}
    [Fact] public void OneRecognizedSourceCannotSatisfyTwoTargets()
    {var d=Data();var r=DegreeAuditEngine.Evaluate(d with{Attempts=[],Recognitions=[new(Required,"external:X"),new(Elective,"external:X")]});Assert.Equal(3,r.Audit.EarnedTotalCredits);Assert.False(r.Audit.GraduationReady);}
    [Fact] public void CurriculumWithoutMinimumGpaDoesNotRequireInventedGradesForRecognizedCredit()
    {var d=Data();var r=DegreeAuditEngine.Evaluate(d with{MinimumGpa=0,Attempts=[],Recognitions=[new(Required,"external:X"),new(Elective,"external:Y")]});Assert.True(r.Audit.GraduationReady);Assert.Null(r.Audit.CumulativeGpa);}
    [Theory][InlineData(60,true)][InlineData(59.99,false)]
    public void GpaAndPassedCreditBoundariesAreExact(decimal grade,bool eligible)
    {var d=Data();var r=DegreeAuditEngine.Evaluate(d with{Attempts=[Attempt(Required,grade),Attempt(Elective,grade)]});Assert.Equal(eligible,r.Audit.GraduationReady);}
    [Fact] public void HigherCurriculumRevisionCanIntroduceNewMandatoryRequirements()
    {var d=Data();var next=d with{Revision=2,Courses=d.Courses.Append(new(Guid.NewGuid(),"NEW","New requirement","Required",3,3)).ToArray(),MinimumCredits=9};Assert.True(DegreeAuditEngine.Evaluate(d).Audit.GraduationReady);var result=DegreeAuditEngine.Evaluate(next);Assert.Equal(2,result.CurriculumRevision);Assert.False(result.Audit.GraduationReady);}
    [Fact] public void ApprovedWaiverSatisfiesCourseButDoesNotGrantCredits()
    {var d=Data();var r=DegreeAuditEngine.Evaluate(d with{Attempts=[Attempt(Elective,80)],Exceptions=[new(Required,null,"Waiver","Approved")]});Assert.Equal(0,r.Audit.MissingRequiredCourses);Assert.Equal(3,r.Audit.EarnedTotalCredits);Assert.False(r.Audit.GraduationReady);Assert.Equal("Waived",r.Courses.Single(x=>x.CourseId==Required).Status);}
    [Fact] public void ApprovedPassedSubstitutionIsConsumedOnlyOnce()
    {var d=Data();var source=Guid.NewGuid();var r=DegreeAuditEngine.Evaluate(d with{Attempts=[Attempt(source,80),Attempt(Elective,80)],Exceptions=[new(Required,source,"Substitution","Approved")]});Assert.True(r.Audit.GraduationReady);Assert.Equal(6,r.Audit.EarnedTotalCredits);}
    [Fact] public void SubstitutionCannotStealNativeCurriculumCredit()
    {var d=Data();var r=DegreeAuditEngine.Evaluate(d with{Attempts=[Attempt(Elective,80)],Exceptions=[new(Required,Elective,"Substitution","Approved")]});Assert.Equal(3,r.Audit.EarnedTotalCredits);Assert.Equal(1,r.Audit.MissingRequiredCourses);}
    [Fact] public void ActiveCoursesArePendingAndDeduplicatedRatherThanEarned()
    {var d=Data();var r=DegreeAuditEngine.Evaluate(d with{Attempts=[Attempt(Required,80)],InProgress=new HashSet<Guid>{Required,Elective}});Assert.Equal(3,r.InProgressCredits);Assert.Equal(3,r.Audit.EarnedTotalCredits);Assert.Equal("PendingReview",r.Eligibility);Assert.False(r.Audit.GraduationReady);}
    [Fact] public void InstitutionalStandingReviewCanBlockOtherwiseCompleteDegree()
    {var d=Data();var r=DegreeAuditEngine.Evaluate(d with{Conditions=[new("AcademicStanding","Pending",1,0,0,"Standing needs recalculation")]});Assert.Equal("PendingReview",r.Eligibility);Assert.False(r.Audit.GraduationReady);}
    [Fact] public void CurrentRecordChangesImmediatelyChangeEligibility()
    {var d=Data();Assert.True(DegreeAuditEngine.Evaluate(d).Audit.GraduationReady);Assert.False(DegreeAuditEngine.Evaluate(d with{Attempts=[Attempt(Required,30),Attempt(Elective,80)]}).Audit.GraduationReady);}
    [Fact] public void MissingPrerequisiteAndCompletionBottleneckExplainDelay()
    {var d=Data();var r=DegreeAuditEngine.Evaluate(d with{Attempts=[],Prerequisites=[new(Elective,Required,60)],MinimumProgressCredits=3});Assert.Contains(r.Indicators,x=>x.Code=="CompletionBottleneck"&&x.CourseId==Required);Assert.Contains(r.Indicators,x=>x.Code=="MissingPrerequisites"&&x.CourseId==Elective);Assert.Contains(r.Indicators,x=>x.Code=="InsufficientProgressCredits");}
    [Fact] public void PassedPrerequisiteStillMustMeetItsMinimumPercentage()
    {var d=Data();var r=DegreeAuditEngine.Evaluate(d with{Attempts=[Attempt(Required,60)],Prerequisites=[new(Elective,Required,70)]});Assert.Contains(Required,r.Courses.Single(x=>x.CourseId==Elective).MissingPrerequisites);}
}
