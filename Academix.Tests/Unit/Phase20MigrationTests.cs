using Academix.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Academix.Tests.Unit;

public sealed class Phase20MigrationTests
{
    [Fact]
    public void Phase20FollowsPhase19PreservesDataAndMatchesTheModel()
    {
        using var db=new AcademixDbContextFactory().CreateDbContext([]);
        var assembly=db.GetService<IMigrationsAssembly>();
        var last=assembly.Migrations.Single(x=>x.Key=="20261101100000_Phase20AcademicAdvisingAndGraduation");
        Assert.True(string.CompareOrdinal("20261031190700_Phase19ArtifactContentSignature",last.Key)<0);
        Assert.True(string.CompareOrdinal(last.Key,"20261102100000_Phase21StudentFinance")<0);
        var migration=assembly.CreateMigration(last.Value,db.Database.ProviderName!);
        Assert.DoesNotContain(migration.UpOperations,x=>x is DropTableOperation or DropColumnOperation or DeleteDataOperation);
        Assert.Contains(migration.UpOperations.OfType<CreateTableOperation>(),x=>x.Name=="academic_advisor_assignments"&&x.ForeignKeys.Count>=4);
        Assert.Contains(migration.UpOperations.OfType<CreateIndexOperation>(),x=>x.Table=="academic_advisor_assignments"&&x.IsUnique&&x.Filter=="\"EndedAtUtc\" IS NULL");
        Assert.Contains(migration.UpOperations.OfType<CreateIndexOperation>(),x=>x.Table=="curriculum_elective_group_courses"&&x.IsUnique);
        Assert.False(db.Database.HasPendingModelChanges());
        var sql=db.GetService<IMigrator>().GenerateScript("20261031190700_Phase19ArtifactContentSignature",last.Key);
        Assert.Contains("p20_graduation_evidence_guard",sql);Assert.Contains("p20_user_scope",sql);Assert.Contains("UnderReview",sql);
        var roleTable=db.Model.FindEntityType(typeof(Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>))!.GetTableName();
        Assert.Contains("\""+roleTable+"\"",sql);
    }
}
