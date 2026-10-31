using Academix.Api.Domain.Identity;
using Academix.Api.Services.Identity;

namespace Academix.Tests.Unit;

public sealed class TeachingRolePolicyTests
{
    [Fact]
    public void StudentOrAdministratorRolesAloneDoNotGrantTeachingAssignments()
    {
        Assert.Empty(TeachingRolePolicy.ActiveAssignmentKinds(
            [AcademixRoles.Student, AcademixRoles.InstitutionAdministrator]));
    }

    [Fact]
    public void InstructorCanOnlyUseInstructorAssignment()
    {
        Assert.Equal(new[] { AcademixRoles.Instructor },
            TeachingRolePolicy.ActiveAssignmentKinds([AcademixRoles.Instructor]));
    }

    [Fact]
    public void TeachingAssistantCanOnlyUseTeachingAssistantAssignment()
    {
        Assert.Equal(new[] { AcademixRoles.TeachingAssistant },
            TeachingRolePolicy.ActiveAssignmentKinds([AcademixRoles.TeachingAssistant]));
    }

    [Fact]
    public void BothActiveRolesAllowTheirMatchingAssignments()
    {
        Assert.Equal(new[] { AcademixRoles.Instructor, AcademixRoles.TeachingAssistant },
            TeachingRolePolicy.ActiveAssignmentKinds(
                [AcademixRoles.Student, AcademixRoles.TeachingAssistant, AcademixRoles.Instructor]));
    }
}
