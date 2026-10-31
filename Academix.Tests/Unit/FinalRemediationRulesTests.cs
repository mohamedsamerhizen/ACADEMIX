using Academix.Api.Data;
using Academix.Api.Domain.Assessment;
using Academix.Api.Middleware;
using Academix.Api.Services.AcademicOperations;
using Academix.Api.Services.Assessment;
using Academix.Api.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Academix.Tests.Unit;

public sealed class FinalRemediationRulesTests
{
    [Theory]
    [InlineData(false,"{\"Version\":1}","{\"Version\":1}",true,false)]
    [InlineData(false,"{\"Version\":1}","{\"Version\":2}",true,true)]
    [InlineData(true,"{}","{}",false,true)]
    [InlineData(false,"[]","{}",true,true)]
    [InlineData(false,"[]","{}",false,false)]
    public void PublishedSourceVersionsAndLegacyUncertaintyDetermineFreshness(bool marked,string published,string current,bool external,bool stale)
        =>Assert.Equal(stale,AssessmentFreshness.Stale(marked,published,current,external));

    [Fact] public void EquivalentJsonFormattingCannotInventAChangedAssessment()
        =>Assert.True(AssessmentFreshness.Same("{\"Score\":80.00,\"Version\":2}","{\"Version\":2,\"Score\":80}"));

    [Theory]
    [InlineData("Retain",true)]
    [InlineData("Retain",false)]
    [InlineData("Recalculate",false)]
    public void ExplicitSupportedDecisionsPreserveOverrideIntent(string decision,bool manual)
        =>GradebookService.ReconciliationDecision(decision,manual);
    [Theory]
    [InlineData("Recalculate",true)]
    [InlineData("Overwrite",false)]
    [InlineData("",false)]
    public void AutomaticRecalculationCannotSilentlyDiscardAnOfficialOverride(string decision,bool manual)
        =>Assert.Throws<ApiFault>(()=>GradebookService.ReconciliationDecision(decision,manual));

    [Theory]
    [InlineData(30,1,30,false)]
    [InlineData(29,1,30,true)]
    [InlineData(30,2,32,true)]
    [InlineData(30,3,32,false)]
    public void LaterEnrollmentMustFitExistingReservedSeats(int current,int added,int seats,bool valid)
    {if(valid)CampusIntegrity.Seats(current,added,seats);else Assert.Throws<ApiFault>(()=>CampusIntegrity.Seats(current,added,seats));}
    [Theory]
    [InlineData(30,30,true)]
    [InlineData(31,30,false)]
    [InlineData(20,30,true)]
    public void CapacityChangesRespectPreviouslyAssignedRooms(int proposed,int room,bool valid)
    {if(valid)CampusIntegrity.Capacity(proposed,[room]);else Assert.Throws<ApiFault>(()=>CampusIntegrity.Capacity(proposed,[room]));}
    [Fact] public void UnassignedSectionsDoNotAcquireAnArtificialRoomLimit()=>CampusIntegrity.Capacity(100,[]);
    [Theory]
    [InlineData(30,true)]
    [InlineData(60,false)]
    [InlineData(61,false)]
    public void ConcreteConflictsUseHalfOpenIntervals(int offset,bool conflict)
    {
        var start=new DateTime(2030,1,1,9,0,0,DateTimeKind.Utc);
        Assert.Equal(conflict,CampusIntegrity.Conflicts([new(Guid.NewGuid(),start,start.AddHours(1))],[new(Guid.NewGuid(),start.AddMinutes(offset),start.AddMinutes(offset+60))]));
    }
    [Fact] public void NewMigrationPreservesHistoryAndModelsAgree()
    {
        using var db=new AcademixDbContextFactory().CreateDbContext([]);var assembly=db.GetService<IMigrationsAssembly>();
        Assert.Equal("20261106110000_RuntimeFinanceTriggerCorrection",assembly.Migrations.Last().Key);
        var migration=assembly.CreateMigration(assembly.Migrations["20261106100000_FinalEngineeringRemediation"],db.Database.ProviderName!);
        Assert.DoesNotContain(migration.UpOperations,x=>x is DropTableOperation or DropColumnOperation or DeleteDataOperation);
        Assert.False(db.Database.HasPendingModelChanges());
        var sql=db.GetService<IMigrator>().GenerateScript("20261105110000_Phase24QualityEvidenceCompletion",assembly.Migrations.Last().Key);
        Assert.Contains("No historical source versions",sql);Assert.Contains("remediation_grade_audit_immutable",sql);
        Assert.Contains("ScoreVersion",sql);Assert.DoesNotContain("\\\"ScoreVersion",sql);
        Assert.Contains(db.Model.FindEntityType(typeof(GradebookAudit))!.GetIndexes(),x=>x.IsUnique&&x.Properties.Select(p=>p.Name).SequenceEqual(new[]{"InstitutionId","CommandId"}));
    }
}
