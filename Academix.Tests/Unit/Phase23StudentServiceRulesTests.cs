using Academix.Api.DTOs.StudentServices;
using Academix.Api.Middleware;
using Academix.Api.Services.StudentServices;

namespace Academix.Tests.Unit;

public sealed class Phase23StudentServiceRulesTests
{
    [Theory]
    [InlineData("Submitted", "Start", false, "InReview")]
    [InlineData("InReview", "RequestInfo", false, "AwaitingStudent")]
    [InlineData("AwaitingStudent", "Reply", true, "InReview")]
    [InlineData("InReview", "Resolve", false, "Resolved")]
    [InlineData("InReview", "Reject", false, "Rejected")]
    [InlineData("Resolved", "Close", false, "Closed")]
    [InlineData("Submitted", "Cancel", true, "Cancelled")]
    [InlineData("AwaitingStudent", "Cancel", true, "Cancelled")]
    public void AuthorizedTransition(string from, string action, bool student, string result)
        => Assert.Equal(result, StudentServiceRules.Transition(from, action, student));

    [Theory]
    [InlineData("Submitted", "Resolve", false)]
    [InlineData("Submitted", "Start", true)]
    [InlineData("InReview", "Resolve", true)]
    [InlineData("Resolved", "Resolve", false)]
    [InlineData("Rejected", "Reply", true)]
    [InlineData("Closed", "Start", false)]
    [InlineData("Cancelled", "Start", false)]
    public void InvalidTransition(string from, string action, bool student)
        => Assert.Throws<ApiFault>(() => StudentServiceRules.Transition(from, action, student));

    [Fact] public void FinanceAndAcademicServiceDuplicationBlocked()
        => Assert.Throws<ApiFault>(() => StudentServiceRules.ValidateDefinition(
            new("Tuition", "Tuition", "Not in case management", 24, true), true));

    [Fact] public void NewCatalogEntryIsValidated()
        => StudentServiceRules.ValidateDefinition(new("ITSupport", "Helpdesk", "Access support", 48, true), true);

    [Fact] public void StableClientRequestIdIsMandatory()
        => Assert.Throws<ApiFault>(() => StudentServiceRules.ValidateSubmission(
            new(Guid.NewGuid(), Guid.Empty, "Subject", "Details")));

    [Fact] public void StudentCannotSupplyPrivateNotes()
        => Assert.Throws<ApiFault>(() => StudentServiceRules.ValidateAction(
            new("Reply", 1, "Here is the required information", "private"), true));

    [Fact] public void EveryActionRequiresOptimisticVersion()
        => Assert.Throws<ApiFault>(() => StudentServiceRules.ValidateAction(
            new("Resolve", 0, "Solved", ""), false));

    [Fact] public void PublicTimelineContractCannotSerializeStaffNotes()
        => Assert.DoesNotContain("PrivateStaffNote", typeof(ServiceCaseEventView)
            .GetProperties().Select(x => x.Name));
}
