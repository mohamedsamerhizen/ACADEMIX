using Academix.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Academix.Tests.Unit;

// Model/SQL checks only: these never claim PostgreSQL execution.
public sealed class Phase19MigrationTests
{
    [Fact]
    public void CourseForeignKeysUseActualPostgreSqlColumnNamesAcrossHistory()
    {
        using var db = new AcademixDbContextFactory().CreateDbContext([]);
        var assembly = db.GetService<IMigrationsAssembly>();
        var checkedMigrations = new HashSet<string>();
        foreach (var migrationType in assembly.Migrations.Values)
        {
            var migration = assembly.CreateMigration(migrationType, db.Database.ProviderName!);
            var keys = migration.UpOperations.OfType<AddForeignKeyOperation>()
                .Concat(migration.UpOperations.OfType<CreateTableOperation>().SelectMany(x => x.ForeignKeys));
            foreach (var key in keys.Where(x => x.PrincipalTable == "courses"))
            {
                checkedMigrations.Add(migration.GetType().Name);
                Assert.All(Assert.IsType<string[]>(key.PrincipalColumns), column => Assert.Contains(column, new[] { "id", "institution_id" }));
            }
        }
        Assert.Contains("Phase14AcademicRecords", checkedMigrations);
        Assert.Contains("Phase15CurriculumPlanning", checkedMigrations);
        Assert.Contains("Phase16AdmissionsGraduation", checkedMigrations);
    }

    [Fact]
    public void Phase19MigrationsFollowExistingHistoryAndNeverDropAcademicDataInUp()
    {
        using var db = new AcademixDbContextFactory().CreateDbContext([]);
        var assembly = db.GetService<IMigrationsAssembly>();
        var ids = assembly.Migrations.Keys.Order().ToArray();
        var previous = Array.FindIndex(ids, id => id.EndsWith("_Phase17InstitutionalDocuments", StringComparison.Ordinal));
        var additions = ids.Where(id => id.Contains("_Phase19", StringComparison.Ordinal)).ToArray();
        Assert.Equal(new[]{"20261031190000_Phase19RegistrarPoliciesAndImports","20261031190100_Phase19RegistrarForeignKeys","20261031190200_Phase19WorkflowCompletion","20261031190300_Phase19DocumentReissuance","20261031190400_Phase19TenantAndArtifactGuards","20261031190500_Phase19ApplicantAndIssuanceInvariants","20261031190600_Phase19ImportIdentityActivation","20261031190700_Phase19ArtifactContentSignature"}, additions);
        Assert.All(additions, id => Assert.True(Array.IndexOf(ids, id) > previous));
        foreach (var id in additions)
        {
            var operations = assembly.CreateMigration(assembly.Migrations[id], db.Database.ProviderName!).UpOperations;
            Assert.DoesNotContain(operations, op => op is DropTableOperation or DropColumnOperation or DeleteDataOperation);
        }
    }
}
