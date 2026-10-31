using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Assessment;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Curriculum;
using Academix.Api.Domain.Identity;
using Academix.Api.Domain.Institutional;
using Academix.Api.Domain.Lifecycle;
using Academix.Api.Domain.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit.Abstractions;

namespace Academix.Tests.Integration;

public sealed class FinalMigrationUpgradeTests(ITestOutputHelper output)
{
    [Fact]
    public async Task PopulatedPhase24UpgradePreservesEvidenceAndEnforcesRemediationGuards()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("remediation_upgrade").WithUsername("upgrade_tests")
            .WithPassword("isolated-upgrade-tests-only").Build();
        await postgres.StartAsync();
        await using var db = new AcademixDbContext(new DbContextOptionsBuilder<AcademixDbContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options);
        await db.GetService<IMigrator>().MigrateAsync("20261105110000_Phase24QualityEvidenceCompletion");
        await using (var versionConnection = new NpgsqlConnection(postgres.GetConnectionString()))
        {
            await versionConnection.OpenAsync(); output.WriteLine($"PostgreSQL server: {versionConnection.ServerVersion}");
        }
        var institution = new Institution { Code = "UPGRADE", Name = "Synthetic upgrade institution" };
        var department = new Department { InstitutionId = institution.Id, Code = "D", Name = "Department" };
        var program = new AcademicProgram { InstitutionId = institution.Id, DepartmentId = department.Id, Code = "P", Name = "Program" };
        var plan = new CurriculumPlan { InstitutionId = institution.Id, ProgramId = program.Id, CatalogYear = 2026, Revision = 1, Name = "Historical plan" };
        var term = new AcademicTerm { InstitutionId = institution.Id, Code = "T", Name = "Term", StartsOn = new(2026,1,1), EndsOn = new(2026,12,31) };
        var course = Course.Create(Guid.NewGuid(), institution.Id, "UP101", "Upgrade evidence");
        var actor = new ApplicationUser { InstitutionId = institution.Id };
        var externalStudent = new ApplicationUser { InstitutionId = institution.Id };
        var overrideStudent = new ApplicationUser { InstitutionId = institution.Id };
        var manualStudent = new ApplicationUser { InstitutionId = institution.Id };
        var external = new CourseSection { InstitutionId = institution.Id, CourseId = course.Id, TermId = term.Id, Code = "E", Capacity = 10 };
        var manual = new CourseSection { InstitutionId = institution.Id, CourseId = course.Id, TermId = term.Id, Code = "M", Capacity = 10 };
        db.AddRange(institution, department, program, plan, term, course, actor, externalStudent, overrideStudent, manualStudent, external, manual);
        await db.SaveChangesAsync();
        foreach (var section in new[] { external, manual })
        {
            var category = new GradeCategory { InstitutionId = institution.Id, SectionId = section.Id, Name = "Final", WeightPercentage = 100 };
            db.AddRange(new SectionGradePolicy { InstitutionId = institution.Id, SectionId = section.Id, UpdatedBy = actor.Id }, category,
                new GradeComponent { InstitutionId = institution.Id, SectionId = section.Id, CategoryId = category.Id,
                    Name = "Historical component", SourceKind = section == external ? "Exam" : "Manual", WeightPercentage = 100, MaximumPoints = 100 });
        }
        var credential = new GraduationApplication { InstitutionId = institution.Id, StudentId = externalStudent.Id, ProgramId = program.Id,
            PlanId = plan.Id, Status = "Issued", IssuedById = actor.Id, CredentialNumber = "UPGRADE-001", IssuedAtUtc = DateTime.UtcNow };
        db.Add(credential);
        var documentIds = new List<Guid>();
        foreach (var item in new[] { (externalStudent.Id, "Transcript"), (externalStudent.Id, "Graduation"), (manualStudent.Id, "Transcript"), (externalStudent.Id, "Enrollment") })
        {
            var request = new InstitutionalDocumentRequest { InstitutionId = institution.Id, StudentId = item.Id,
                Kind = item.Item2, ClientRequestId = Guid.NewGuid(), Purpose = "Synthetic preserved evidence" };
            var document = new IssuedAcademicDocument { InstitutionId = institution.Id, StudentId = item.Id,
                RequestId = request.Id, Kind = item.Item2, ReferenceNumber = "UP-" + Guid.NewGuid().ToString("N"),
                SnapshotJson = "{\"historical\":true}", IssuedById = actor.Id, ExpiresAtUtc = DateTime.UtcNow.AddYears(1) };
            db.AddRange(request, document); documentIds.Add(document.Id);
        }
        db.StudentTranscriptEntries.Add(new() { InstitutionId = institution.Id, StudentId = externalStudent.Id, SectionId = external.Id,
            CourseId = course.Id, TermId = term.Id, CourseCode = "UP101", CourseTitle = "Upgrade evidence", Percentage = 80, Passed = true, Letter = "B", GradebookVersion = 7 });
        await db.SaveChangesAsync();
        foreach (var item in new[] { (externalStudent.Id, external.Id, 80m), (overrideStudent.Id, external.Id, 75m), (manualStudent.Id, manual.Id, 90m) })
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO gradebook_results ("Id","InstitutionId","SectionId","StudentId","Percentage","Passed","Letter","Version","PublishedAtUtc")
                VALUES ({Guid.NewGuid()},{institution.Id},{item.Item2},{item.Item1},{item.Item3},true,'B',7,CURRENT_TIMESTAMP)
                """);
        var auditId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO gradebook_audit ("Id","InstitutionId","SectionId","StudentId","ActorId","Action","PreviousPercentage","NewPercentage","Reason","AtUtc")
            VALUES ({auditId},{institution.Id},{external.Id},{overrideStudent.Id},{actor.Id},'Corrected',70,75,'Historical moderation',CURRENT_TIMESTAMP)
            """);
        db.ChangeTracker.Clear();
        await db.Database.MigrateAsync();
        Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        var results = await db.GradebookResults.AsNoTracking().OrderBy(x => x.Percentage).ToListAsync();
        Assert.Equal(3, results.Count);
        var ordinary = results.Single(x => x.StudentId == externalStudent.Id);
        Assert.Equal(80, ordinary.Percentage); Assert.True(ordinary.RequiresReconciliation); Assert.False(ordinary.IsManualOverride); Assert.Equal(8, ordinary.Version); Assert.Equal("[]", ordinary.SourceEvidenceJson);
        var corrected = results.Single(x => x.StudentId == overrideStudent.Id);
        Assert.Equal(75, corrected.Percentage); Assert.True(corrected.RequiresReconciliation); Assert.True(corrected.IsManualOverride); Assert.Equal(8, corrected.Version);
        var manualOnly = results.Single(x => x.StudentId == manualStudent.Id);
        Assert.Equal(90, manualOnly.Percentage); Assert.False(manualOnly.RequiresReconciliation); Assert.False(manualOnly.IsManualOverride); Assert.Equal(7, manualOnly.Version);
        Assert.NotNull((await db.GraduationApplications.SingleAsync()).SourceInvalidatedAtUtc);
        var documents = await db.IssuedAcademicDocuments.OrderBy(x => x.Kind).ToListAsync();
        Assert.Equal(4, documents.Count);
        foreach (var document in documents)
        {
            Assert.Contains(document.Id, documentIds); Assert.Equal("{\"historical\":true}", document.SnapshotJson);
            Assert.Equal(document.StudentId == externalStudent.Id && document.Kind != "Enrollment", document.SourceInvalidatedAtUtc != null);
        }
        var transcript = await db.StudentTranscriptEntries.SingleAsync(); Assert.Equal(80, transcript.Percentage); Assert.Equal(7, transcript.GradebookVersion);
        var audit = await db.GradebookAudits.SingleAsync(); Assert.Equal(auditId, audit.Id); Assert.Equal("Historical moderation", audit.Reason); Assert.Equal(75, audit.NewPercentage);
        var immutable = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE gradebook_audit SET \"Reason\"='tampered' WHERE \"Id\"={auditId}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, immutable.SqlState);
        var badEvidence = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE gradebook_results SET \"SourceEvidenceJson\"='1' WHERE \"StudentId\"={externalStudent.Id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, badEvidence.SqlState);
        var badCommand = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO gradebook_audit ("Id","InstitutionId","SectionId","StudentId","ActorId","Action","NewPercentage","Reason","AtUtc","CommandId")
            VALUES ({Guid.NewGuid()},{institution.Id},{external.Id},{externalStudent.Id},{actor.Id},'Reconciled',80,'Invalid command evidence',CURRENT_TIMESTAMP,{Guid.NewGuid()})
            """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, badCommand.SqlState);
        output.WriteLine($"Applied {(await db.Database.GetAppliedMigrationsAsync()).Count()} migrations; preserved 3 grades, 1 audit, 1 transcript, 1 issued credential, 4 documents; flagged 2 grades and invalidated 2 documents; append-only/evidence/command guards rejected tampering.");
    }
}
