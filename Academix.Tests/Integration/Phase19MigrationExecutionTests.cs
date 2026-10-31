using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace Academix.Tests.Integration;

public sealed class Phase19MigrationExecutionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FullHistoryAppliesToPostgreSqlAndPreservesPrePhase19Data(bool upgrade)
    {
        await using var postgres=new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("migration_verification").WithUsername("migration_tests")
            .WithPassword("isolated-migration-tests-only").Build();
        await postgres.StartAsync();
        await using var db=new AcademixDbContext(new DbContextOptionsBuilder<AcademixDbContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options);
        var migrator=db.GetService<IMigrator>();
        var sentinel=new Institution { Code="P19-UPGRADE", Name="Preserved pre-Phase-19 institution" };
        if(upgrade)
        {
            await migrator.MigrateAsync("20261030100000_Phase17InstitutionalDocuments");
            db.Institutions.Add(sentinel);await db.SaveChangesAsync();db.ChangeTracker.Clear();
        }
        await db.Database.MigrateAsync();
        Assert.Equal(db.Database.GetMigrations(),await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        if(upgrade)Assert.Equal(sentinel.Name,await db.Institutions.Where(x=>x.Id==sentinel.Id).Select(x=>x.Name).SingleAsync());
    }
}
