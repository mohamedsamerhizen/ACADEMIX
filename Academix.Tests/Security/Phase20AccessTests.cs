using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Advising;
using Academix.Api.Middleware;
using Academix.Api.Services.Advising;

namespace Academix.Tests.Security;

public sealed class Phase20AccessTests
{
    [Theory]
    [InlineData(AcademixRoles.Instructor,true,true)]
    [InlineData(AcademixRoles.Instructor,false,false)]
    [InlineData(AcademixRoles.TeachingAssistant,true,false)]
    [InlineData(AcademixRoles.Student,true,false)]
    [InlineData(AcademixRoles.InstitutionAdministrator,false,true)]
    public void StaffAccessRequiresCurrentRoleAndAssignment(string role,bool assigned,bool expected)
    {var inst=Guid.NewGuid();Assert.Equal(expected,AcademicProgressAccess.CanAccess(Guid.NewGuid(),inst,[role],inst,Guid.NewGuid(),assigned,true));}
    [Theory][InlineData(AcademixRoles.Instructor)][InlineData(AcademixRoles.InstitutionAdministrator)][InlineData(AcademixRoles.Student)]
    public void CrossInstitutionAccessDeniedEvenWithAssignment(string role)
    {Assert.False(AcademicProgressAccess.CanAccess(Guid.NewGuid(),Guid.NewGuid(),[role],Guid.NewGuid(),Guid.NewGuid(),true,false));}
    [Fact] public void StudentCanReadOwnPublicProgressButNotPrivateStaffDataOrAnotherStudent()
    {var inst=Guid.NewGuid();var student=Guid.NewGuid();Assert.True(AcademicProgressAccess.CanAccess(student,inst,[AcademixRoles.Student],inst,student,false,false));Assert.False(AcademicProgressAccess.CanAccess(student,inst,[AcademixRoles.Student],inst,student,false,true));Assert.False(AcademicProgressAccess.CanAccess(student,inst,[AcademixRoles.Student],inst,Guid.NewGuid(),false,false));}
    [Fact] public void OrdinaryStudentSessionContractContainsNoInternalNotes()
    {var names=typeof(StudentSessionView).GetProperties().Select(x=>x.Name).ToArray();Assert.DoesNotContain("InternalNotes",names);Assert.DoesNotContain("InternalSummary",names);Assert.Contains("Recommendation",names);}
    [Theory][InlineData("Approved")][InlineData("Rejected")][InlineData("Acknowledged")]
    public void CompletedStudyPlanRevisionsCannotBeReviewedAgain(string status)=>Assert.Throws<ApiFault>(()=>AcademicProgressRules.ReviewPlan(status));
    [Fact] public void StudyPlanApprovalAcknowledgementAndVersionsAreExplicit()
    {AcademicProgressRules.ReviewPlan("Proposed");AcademicProgressRules.AcknowledgePlan("Approved");AcademicProgressRules.AcknowledgePlan("Acknowledged");Assert.Throws<ApiFault>(()=>AcademicProgressRules.AcknowledgePlan("Proposed"));Assert.Throws<ApiFault>(()=>AcademicProgressRules.Version(2,1));}
    [Theory][InlineData("Approved")][InlineData("Rejected")][InlineData("Issued")]
    public void InvalidGraduationApprovalTransitionsFail(string status)=>Assert.Throws<ApiFault>(()=>AcademicProgressRules.GraduationReview(status,false,false));
    [Fact] public void ConfiguredDepartmentReviewCannotBeBypassed()
    {Assert.Throws<ApiFault>(()=>AcademicProgressRules.GraduationReview("Submitted",true,false));AcademicProgressRules.GraduationReview("UnderReview",true,true);AcademicProgressRules.GraduationReview("Submitted",false,false);}
}
