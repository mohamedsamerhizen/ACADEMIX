using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Courses;
using Academix.Api.Services.Identity;

namespace Academix.Tests.Unit;

public sealed class Phase1DomainTests
{
    [Fact]
    public void Course_BindsToInstitution()
    {
        var institution = Guid.NewGuid();
        var course = Course.Create(Guid.NewGuid(), institution, "CS201", "Algorithms");
        Assert.Equal(institution, course.InstitutionId);
    }
    [Fact]
    public void Course_ConcurrentVersionCheckRejectsStaleUpdate()
    {
        var course = Course.Create(Guid.NewGuid(), "CS201", "Algorithms");
        course.Change("Algorithms II", true, 1);
        Assert.Equal(2, course.Version);
        Assert.Throws<InvalidOperationException>(() => course.Change("Invalid overwrite", true, 1));
    }
    [Fact]
    public void Course_RejectsDifferentEmptyInstitution()
    {
        Assert.Throws<ArgumentException>(() => Course.Create(Guid.NewGuid(), Guid.Empty, "CS201", "Algorithms"));
    }
    [Fact]
    public void RefreshTokenHashesAreStable()
    {
        var token = IdentitySecurity.RandomToken();
        Assert.Equal(IdentitySecurity.TokenHash(token), IdentitySecurity.TokenHash(token));
        Assert.NotEqual(token, IdentitySecurity.TokenHash(token));
    }
    [Fact]
    public void RandomRefreshTokensAreIndependent()
    {
        Assert.NotEqual(IdentitySecurity.RandomToken(), IdentitySecurity.RandomToken());
    }
    [Fact]
    public void RoleCatalogIsUnique()
    {
        Assert.Equal(5, Academix.Api.Domain.Identity.AcademixRoles.All.Distinct().Count());
    }
}
