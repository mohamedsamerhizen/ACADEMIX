using Academix.Api.Data;
using Academix.Api.Domain.StudentServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Academix.Tests.Unit;

public sealed class Phase23CompletionMigrationTests
{
    [Fact]
    public void CompletionAddsThreeTablesAndPreservesExistingTables()
    {
        using var db = new AcademixDbContextFactory().CreateDbContext([]);
        var migrations = db.GetService<IMigrationsAssembly>();
        Assert.True(migrations.Migrations.TryGetValue("20261104110000_Phase23ServiceDeskCompletion", out var type));
        var m = migrations.CreateMigration(type!, db.Database.ProviderName!);
        var tables = m.UpOperations.OfType<CreateTableOperation>().Select(x => x.Name).ToArray();
        Assert.Contains("student_service_staff_grants", tables);
        Assert.Contains("student_service_referrals", tables);
        Assert.Contains("student_service_attachments", tables);
        Assert.DoesNotContain(m.UpOperations, x => x is DropTableOperation or DropColumnOperation);
        var assignedStaff = Assert.Single(m.UpOperations.OfType<AddForeignKeyOperation>());
        Assert.Equal("student_service_cases", assignedStaff.Table);
        Assert.Null(assignedStaff.Schema);
        Assert.Equal("identity_users", assignedStaff.PrincipalTable);
        Assert.Equal(new[] { "Id" }, assignedStaff.PrincipalColumns);
        Assert.Equal(ReferentialAction.Restrict, assignedStaff.OnDelete);
        Assert.NotNull(db.Model.FindEntityType(typeof(StudentServiceStaffGrant)));
        Assert.NotNull(db.Model.FindEntityType(typeof(StudentServiceReferral)));
        Assert.NotNull(db.Model.FindEntityType(typeof(StudentServiceAttachment)));
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
