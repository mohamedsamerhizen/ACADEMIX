using Academix.Api.Data;
using Academix.Api.Domain.Quality;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Academix.Tests.Unit;

public sealed class Phase24QualityMigrationTests
{
    [Fact]
    public void QualityMigrationIsAdditiveAndModelsHaveInstitutionScopedKeys()
    {
        using var db = new AcademixDbContextFactory().CreateDbContext([]);
        var migrations = db.GetService<IMigrationsAssembly>();
        Assert.True(migrations.Migrations.TryGetValue("20261105100000_Phase24ProgramQualityFoundation", out var type));
        var migration = migrations.CreateMigration(type!, db.Database.ProviderName!);
        var tables = migration.UpOperations.OfType<CreateTableOperation>().Select(x => x.Name).ToArray();
        Assert.Contains("quality_program_outcomes", tables);
        Assert.Contains("quality_course_alignments", tables);
        Assert.Contains("quality_program_reviews", tables);
        Assert.Contains("quality_improvement_actions", tables);
        Assert.DoesNotContain(migration.UpOperations, x => x is DropTableOperation or DropColumnOperation);
        Assert.NotNull(db.Model.FindEntityType(typeof(ProgramQualityOutcome)));
        Assert.NotNull(db.Model.FindEntityType(typeof(ProgramOutcomeCourseMap)));
        Assert.NotNull(db.Model.FindEntityType(typeof(ProgramQualityReview)));
        Assert.NotNull(db.Model.FindEntityType(typeof(QualityImprovementAction)));
    }
}
