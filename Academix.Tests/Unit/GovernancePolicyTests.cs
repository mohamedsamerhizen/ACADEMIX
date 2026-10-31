using Academix.Api.Domain.Identity;
using Academix.Api.Middleware;
using Academix.Api.Services.Governance;

namespace Academix.Tests.Unit;

public sealed class GovernancePolicyTests
{
    [Theory]
    [InlineData("AddRole", AcademixRoles.Instructor)]
    [InlineData("RemoveRole", AcademixRoles.TeachingAssistant)]
    [InlineData("AddRole", AcademixRoles.Student)]
    [InlineData("Activate", "")]
    [InlineData("Deactivate", "")]
    public void OnlyScopedStaffOperationsAreSupported(string action, string role)
    {
        var normalized = GovernancePolicy.StaffChange(action, role, " administration reason ");
        Assert.Equal(action, normalized.Action);
        Assert.Equal("administration reason", normalized.Reason);
    }

    [Theory]
    [InlineData("AddRole", AcademixRoles.SuperAdministrator)]
    [InlineData("AddRole", AcademixRoles.InstitutionAdministrator)]
    [InlineData("Deactivate", AcademixRoles.Student)]
    [InlineData("Arbitrary", "")]
    public void EscalationOrInvalidActionIsRejected(string action, string role)
        => Assert.Throws<ApiFault>(() => GovernancePolicy.StaffChange(action, role, "some reason"));

    [Fact]
    public void BatchValidationRejectsEmptyDuplicateOversizedOrEmptyGuids()
    {
        var id = Guid.NewGuid();
        Assert.Throws<ApiFault>(() => GovernancePolicy.BatchStudents([]));
        Assert.Throws<ApiFault>(() => GovernancePolicy.BatchStudents([id, id]));
        Assert.Throws<ApiFault>(() => GovernancePolicy.BatchStudents([Guid.Empty]));
        Assert.Throws<ApiFault>(() => GovernancePolicy.BatchStudents(Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToArray()));
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        Assert.Equal(ids.OrderBy(x => x), GovernancePolicy.BatchStudents(ids));
    }

    [Fact]
    public void ReviewRequiresSeparateApproverAndExactVersion()
    {
        var creator = Guid.NewGuid(); var target = Guid.NewGuid(); var reviewer = Guid.NewGuid();
        GovernancePolicy.Review(reviewer, creator, target, 1, 1, "Pending");
        Assert.Throws<ApiFault>(() => GovernancePolicy.Review(creator, creator, target, 1, 1, "Pending"));
        Assert.Throws<ApiFault>(() => GovernancePolicy.Review(target, creator, target, 1, 1, "Pending"));
        Assert.Throws<ApiFault>(() => GovernancePolicy.Review(reviewer, creator, target, 2, 1, "Pending"));
        Assert.Throws<ApiFault>(() => GovernancePolicy.Review(reviewer, creator, target, 1, 1, "Approved"));
    }

    [Fact]
    public void PaginationAndReasonAreBounded()
    {
        Assert.Equal((0, 50), GovernancePolicy.Page(0, 50));
        Assert.Throws<ApiFault>(() => GovernancePolicy.Page(-1, 50));
        Assert.Throws<ApiFault>(() => GovernancePolicy.Page(0, 101));
        Assert.Throws<ApiFault>(() => GovernancePolicy.RequiredText(" ", 20, "reason"));
    }
}
