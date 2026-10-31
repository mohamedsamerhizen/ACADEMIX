using Academix.Api.Data;
using Academix.Api.Domain.Quality;
using Academix.Api.Middleware;
using Academix.Api.Services.Quality;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Academix.Tests.Unit;

public sealed class Phase24QualityCompletionTests
{
    [Theory]
    [InlineData(70, 100, 70, true)]
    [InlineData(69, 100, 70, false)]
    [InlineData(7, 10, 70, true)]
    [InlineData(0, 20, 0, true)]
    [InlineData(15, 20, 80, false)]
    public void RubricProficiencyChecksActualPoints(decimal points, decimal max, decimal threshold, bool expected)
        => Assert.Equal(expected, QualityRules.CriterionIsProficient(points, max, threshold));

    [Theory]
    [InlineData(-1, 100, 50)]
    [InlineData(101, 100, 50)]
    [InlineData(5, 0, 50)]
    [InlineData(5, -1, 50)]
    [InlineData(5, 10, 101)]
    public void RejectsInvalidRubricScoresAndThresholds(decimal points, decimal max, decimal threshold)
        => Assert.Throws<ApiFault>(() => QualityRules.CriterionIsProficient(points, max, threshold));

    [Fact]
    public void UnmappedCriteriaNeverClaimAttainment()
    {
        var item = QualityRules.DirectEvidence(Guid.NewGuid(), "PLO1", 65, 0, 0, 0);
        Assert.Null(item.ProficientRate);
        Assert.Equal("UnmappedDirect", item.AssessmentStatus);
        Assert.Equal("GradedTeamProjectRubricCriterion", item.EvidenceKind);
    }

    [Fact]
    public void MissingGradedEvidenceIsUnknownNotFailure()
    {
        var item = QualityRules.DirectEvidence(Guid.NewGuid(), "PLO1", 65, 1, 0, 0);
        Assert.Null(item.ProficientRate);
        Assert.Equal("NoGradedRubricEvidence", item.AssessmentStatus);
    }

    [Theory]
    [InlineData(10, 7, 70, "DirectCriterionTargetMet")]
    [InlineData(10, 6, 70, "DirectCriterionBelowTarget")]
    [InlineData(3, 1, 34, "DirectCriterionBelowTarget")]
    public void ThresholdsMeasureCriterionObservationsNotStudentCount(int total, int proficient,
        int target, string status)
    {
        var evidence = QualityRules.DirectEvidence(Guid.NewGuid(), "PLO1", target, 2, total, proficient);
        Assert.Equal(status, evidence.AssessmentStatus);
        Assert.Equal(total, evidence.GradedCriterionObservations);
    }

    [Fact]
    public void RejectsFabricatedAchievementCounts()
        => Assert.Throws<ApiFault>(() => QualityRules.DirectEvidence(Guid.NewGuid(), "PLO1", 70, 1, 3, 4));

    [Theory]
    [InlineData("")]
    [InlineData("x")]
    [InlineData("a\nproblem")]
    public void EvidenceLabelsMustBeMeaningful(string title)
        => Assert.Throws<ApiFault>(() => QualityRules.PackLabel(title, 200));

    [Fact]
    public void EvidenceDigestsAreStableAndChangeWithContent()
    {
        var a = QualityEvidenceService.Digest("actual review evidence");
        Assert.Equal(64, a.Length);
        Assert.Equal(a, QualityEvidenceService.Digest("actual review evidence"));
        Assert.NotEqual(a, QualityEvidenceService.Digest("changed review evidence"));
    }

    [Fact]
    public void CompletionMigrationIsAdditiveWithImmutableEvidenceGuards()
    {
        using var db = new AcademixDbContextFactory().CreateDbContext([]);
        var migrations = db.GetService<IMigrationsAssembly>();
        Assert.True(migrations.Migrations.TryGetValue("20261105110000_Phase24QualityEvidenceCompletion", out var type));
        var migration = migrations.CreateMigration(type!, db.Database.ProviderName!);
        var tables = migration.UpOperations.OfType<CreateTableOperation>().Select(x => x.Name).ToArray();
        Assert.Contains("quality_rubric_alignments", tables);
        Assert.Contains("quality_action_events", tables);
        Assert.Contains("quality_accreditation_packs", tables);
        Assert.Contains("quality_pack_items", tables);
        Assert.DoesNotContain(migration.UpOperations, x => x is DropTableOperation or DropColumnOperation);
        Assert.NotNull(db.Model.FindEntityType(typeof(QualityCriterionAlignment)));
        Assert.NotNull(db.Model.FindEntityType(typeof(QualityActionEvent)));
        Assert.NotNull(db.Model.FindEntityType(typeof(QualityAccreditationPack)));
        Assert.NotNull(db.Model.FindEntityType(typeof(QualityPackItem)));
        Assert.NotNull(db.Model.FindEntityType(typeof(ProgramQualityReview))?.FindProperty("DirectSnapshotJson"));
    }
}
