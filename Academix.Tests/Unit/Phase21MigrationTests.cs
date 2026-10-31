using Academix.Api.Data;
using Academix.Api.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
namespace Academix.Tests.Unit;

public sealed class Phase21MigrationTests
{
    [Fact] public void FinanceMigrationIsAdditiveAndCurrentModelHasNoPendingChanges()
    {
        using var db=new AcademixDbContextFactory().CreateDbContext([]);
        var migrations=db.GetService<IMigrationsAssembly>();
        const string financeMigrationId = "20261102100000_Phase21StudentFinance";
        Assert.True(migrations.Migrations.TryGetValue(financeMigrationId, out var financeMigrationType),
            "Phase 21 finance migration must remain in the migration history.");
        var migration=migrations.CreateMigration(financeMigrationType!,db.Database.ProviderName!);
        Assert.DoesNotContain(migration.UpOperations,x=>x is DropTableOperation or DropColumnOperation or DeleteDataOperation);
        Assert.Equal(22,migration.UpOperations.OfType<CreateTableOperation>().Count());Assert.False(db.Database.HasPendingModelChanges());
        var sql=db.GetService<IMigrator>().GenerateScript("20261101100000_Phase20AcademicAdvisingAndGraduation",financeMigrationId);
        Assert.Contains("DEFERRABLE INITIALLY DEFERRED",sql);Assert.Contains("p21_validate_journal",sql);Assert.Contains("p21_validate_account",sql);Assert.Contains("p21_immutable",sql);
        Assert.Contains("'IQD'",sql);Assert.Contains("'CLF'",sql);
    }
    [Fact] public void NaturalKeysPreventDuplicateTuitionReferencesPostingAndRefundExecution()
    {
        using var db=new AcademixDbContextFactory().CreateDbContext([]);
        void Unique<T>(params string[] properties)
        {Assert.Contains(db.Model.FindEntityType(typeof(T))!.GetIndexes(),x=>x.IsUnique&&x.Properties.Select(p=>p.Name).SequenceEqual(properties));}
        Unique<FinancialAccount>("InstitutionId","StudentId","Currency");Unique<FinanceInvoice>("AccountId","TermId","Scope");Unique<FinanceCommand>("InstitutionId","RequestId");
        Unique<FinancePayment>("InstitutionId","Reference");Unique<FinanceJournal>("InstitutionId","PostingKey");Unique<FinanceReceipt>("PaymentId");Unique<FinanceRefund>("InstitutionId","ExecutionReference");
        Unique<ScholarshipAward>("InvoiceId","ProgramCode");
        Assert.Contains(db.Model.FindEntityType(typeof(FinancePaymentPlan))!.GetIndexes(),x=>x.IsUnique&&x.GetFilter()=="\"Status\"='Approved'");
        Assert.Contains(db.Model.FindEntityType(typeof(FinancePaymentPlan))!.GetIndexes(),x=>x.IsUnique&&x.GetFilter()=="\"Status\"='Proposed'");
    }
    [Fact] public void AllocationsHaveTenantAccountAndCurrencyBoundReferencesOnBothSides()
    {
        using var db=new AcademixDbContextFactory().CreateDbContext([]);var allocation=db.Model.FindEntityType(typeof(FinanceAllocation))!;
        foreach(var parent in new[]{typeof(FinanceInvoice),typeof(FinancePayment)})
        {var fk=Assert.Single(allocation.GetForeignKeys(),x=>x.PrincipalEntityType.ClrType==parent);Assert.Contains(fk.Properties,x=>x.Name=="AccountId");Assert.Contains(fk.Properties,x=>x.Name=="InstitutionId");Assert.Contains(fk.Properties,x=>x.Name=="Currency");}
    }
}
