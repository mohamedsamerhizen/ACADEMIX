using Academix.Api.Data;
using Academix.Api.Domain.StudentServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Academix.Tests.Unit;

public sealed class Phase23MigrationTests
{
    [Fact]
    public void NewCaseSchemaIsAdditiveAndInstitutionScoped()
    {
        using var db = new AcademixDbContextFactory().CreateDbContext([]);
        var assembly = db.GetService<IMigrationsAssembly>();
        const string id = "20261104100000_Phase23StudentServiceCases";
        Assert.True(assembly.Migrations.TryGetValue(id, out var type));
        var migration = assembly.CreateMigration(type!, db.Database.ProviderName!);
        Assert.Contains(migration.UpOperations.OfType<CreateTableOperation>(),
            x => x.Name == "student_service_cases" &&
                 x.ForeignKeys.Any(fk => fk.PrincipalTable == "student_service_definitions"));
        Assert.DoesNotContain(migration.UpOperations,
            x => x is DropTableOperation or DropColumnOperation);
        Assert.NotNull(db.Model.FindEntityType(typeof(StudentServiceCase)));
        Assert.NotNull(db.Model.FindEntityType(typeof(StudentServiceCaseEvent)));
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
