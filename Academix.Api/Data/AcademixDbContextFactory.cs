using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Academix.Api.Data;

// Used only by dotnet ef at design time. No real credentials belong here.
public sealed class AcademixDbContextFactory : IDesignTimeDbContextFactory<AcademixDbContext>
{
    public AcademixDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AcademixDbContext>()
            .UseNpgsql("Host=localhost;Database=academix;Username=academix;Password=design-time-placeholder")
            .Options;
        return new AcademixDbContext(options);
    }
}
