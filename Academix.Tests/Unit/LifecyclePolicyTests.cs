using Academix.Api.DTOs.Lifecycle;
using Academix.Api.Middleware;
using Academix.Api.Services.Lifecycle;

namespace Academix.Tests.Unit;

public sealed class LifecyclePolicyTests
{
    [Theory]
    [InlineData("Approve")]
    [InlineData("Reject")]
    public void OnlySupportedReviewDecisionsAreAccepted(string decision)
        => Assert.Equal(decision, LifecyclePolicy.Decision(decision));

    [Theory]
    [InlineData("Pending")]
    [InlineData("Issue")]
    [InlineData("approved")]
    public void InvalidDecisionIsRejected(string decision)
        => Assert.Throws<ApiFault>(() => LifecyclePolicy.Decision(decision));

    [Fact]
    public void StaleVersionCannotApproveRequest()
    {
        var ex = Assert.Throws<ApiFault>(() => LifecyclePolicy.RequirePending("Submitted", 3, 2));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public void ReviewedRequestCannotBeApprovedAgain()
        => Assert.Throws<ApiFault>(() => LifecyclePolicy.RequirePending("Approved", 2, 2));

    [Fact]
    public void TransferRejectsDuplicateTargets()
    {
        var id = Guid.NewGuid();
        var body = new SubmitTransferCredit("Other College", [
            new TransferCourseRequest(id, "X101", "Introduction", 3m),
            new TransferCourseRequest(id, "X102", "Algorithms", 3m)
        ]);
        Assert.Throws<ApiFault>(() => LifecyclePolicy.ValidateTransfer(body));
    }

    [Fact]
    public void TransferRejectsInvalidCredits()
    {
        var body = new SubmitTransferCredit("Other College", [
            new TransferCourseRequest(Guid.NewGuid(), "X101", "Introduction", 0m)
        ]);
        Assert.Throws<ApiFault>(() => LifecyclePolicy.ValidateTransfer(body));
    }

    [Fact]
    public void TransferValidatesCompleteRequest()
    {
        var body = new SubmitTransferCredit("Other College", [
            new TransferCourseRequest(Guid.NewGuid(), "X101", "Introduction", 3.5m)
        ]);
        LifecyclePolicy.ValidateTransfer(body);
    }

    [Fact]
    public void ReasonRejectsBlankOrOverlongText()
    {
        Assert.Throws<ApiFault>(() => LifecyclePolicy.Description("  ", 5, 500));
        Assert.Throws<ApiFault>(() => LifecyclePolicy.Description(new string('x', 501), 5, 500));
    }
    [Fact]
    public void AnAdministratorCannotApproveTheirOwnAcademicApplication()
    {
        var userId = Guid.NewGuid();
        Assert.Equal(403, Assert.Throws<ApiFault>(() =>
            LifecyclePolicy.DisallowSelfApproval(userId, userId)).StatusCode);
        LifecyclePolicy.DisallowSelfApproval(Guid.NewGuid(), userId);
    }
}
