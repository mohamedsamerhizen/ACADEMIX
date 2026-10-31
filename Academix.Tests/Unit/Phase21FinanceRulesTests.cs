using Academix.Api.DTOs.Finance;
using Academix.Api.Middleware;
using Academix.Api.Services.Finance;
namespace Academix.Tests.Unit;

public sealed class Phase21FinanceRulesTests
{
    [Theory][InlineData("USD",2)][InlineData("IQD",3)][InlineData("JPY",0)][InlineData("CLF",4)]
    public void CurrencyUsesOfficialMinorUnitsRatherThanLocaleFormatting(string code,int digits)
    {Assert.Equal(code,FinanceRules.Currency(" "+code.ToLowerInvariant()+" "));Assert.Equal(digits,FinanceRules.Currencies[code]);Assert.True(FinanceRules.Currencies.Count>=160);}
    [Theory][InlineData("XYZ")][InlineData("XAU")][InlineData("")][InlineData(null)]
    public void InvalidOrNonMonetaryCurrencyCodesAreRejected(string? code)=>Assert.Throws<ApiFault>(()=>FinanceRules.Currency(code));
    [Fact] public void NoImplicitCurrencyConversionOrCrossCurrencyAllocation()
    {FinanceRules.SameCurrency("usd","USD");Assert.Throws<ApiFault>(()=>FinanceRules.SameCurrency("USD","IQD"));}
    [Theory][InlineData(0)][InlineData(-1)][InlineData(long.MinValue)][InlineData(long.MaxValue)]
    public void MonetaryAmountsArePositiveAndBounded(long amount)=>Assert.Throws<ApiFault>(()=>FinanceRules.Money(amount));
    [Fact] public void FixedAndCreditHourPricingDeriveDifferentAuthoritativeQuantities()
    {Assert.Equal(10000,FinanceRules.Price(10000,FinanceRules.FeeQuantity("Fixed",12)));Assert.Equal(12500,FinanceRules.Price(1000,FinanceRules.FeeQuantity("PerCredit",12.5m)));Assert.Throws<ApiFault>(()=>FinanceRules.FeeQuantity("ClientTotal",12));}
    [Fact] public void AcademicYearScopesUseExplicitInstitutionalPeriodsAcrossCalendarYears()
    {var term=Guid.NewGuid();Assert.True(FinanceRules.ScheduleScope(null,2026,term,new(2027,2,1),new(2026,9,1),new(2027,9,1)));Assert.False(FinanceRules.ScheduleScope(null,2026,term,new(2027,10,1),new(2026,9,1),new(2027,9,1)));Assert.True(FinanceRules.ScheduleScope(term,null,term,new(2027,2,1),new(2026,9,1),new(2027,9,1)));}
    [Theory][InlineData(101,1.5,152)][InlineData(101,1,101)][InlineData(999,3.25,3247)]
    public void CreditPricingRoundsOnceInMinorUnitsAwayFromZero(long rate,decimal credits,long expected)=>Assert.Equal(expected,FinanceRules.Price(rate,credits));
    [Fact] public void FractionalMinorUnitUnderflowAndOverflowFailExplicitly()
    {Assert.Throws<ApiFault>(()=>FinanceRules.Price(1,.01m));Assert.Throws<ApiFault>(()=>FinanceRules.Price(FinanceRules.MaximumMinor,2));Assert.Throws<ApiFault>(()=>FinanceRules.Sum([FinanceRules.MaximumMinor,1]));}
    [Fact] public void VersionChecksProtectPublishedPricingAndConflictingAdjustments()
    {FinanceRules.Version(2,2);Assert.Throws<ApiFault>(()=>FinanceRules.Version(2,1));}
    [Fact] public void JournalsMustBeBalancedCompleteAndUseDistinctValidBuckets()
    {
        FinanceRules.Journal(100,[new("Receivable",100),new("Revenue",-100)]);
        Assert.Throws<ApiFault>(()=>FinanceRules.Journal(100,[new("Receivable",100)]));
        Assert.Throws<ApiFault>(()=>FinanceRules.Journal(100,[new("Receivable",100),new("Revenue",-99)]));
        Assert.Throws<ApiFault>(()=>FinanceRules.Journal(100,[new("Receivable",100),new("Receivable",-100)]));
        Assert.Throws<ApiFault>(()=>FinanceRules.Journal(100,[new("Funds",long.MinValue),new("Revenue",-100)]));
    }
    [Fact] public void CashAllocationOverpaymentAndReversalArithmeticReconcile()
    {
        var ledger=new Dictionary<string,long>();
        void Apply(long amount,string debit,string credit)
        {FinanceRules.Journal(amount,[new(debit,amount),new(credit,-amount)]);ledger[debit]=ledger.GetValueOrDefault(debit)+amount;ledger[credit]=ledger.GetValueOrDefault(credit)-amount;FinanceRules.Balances(ledger);}
        Apply(100,"Receivable","Revenue");Apply(150,"Funds","StudentCredit");Apply(100,"StudentCredit","Receivable");
        Assert.Equal(0,ledger["Receivable"]);Assert.Equal(-50,ledger["StudentCredit"]);Assert.Equal(150,ledger["Funds"]);
        Apply(100,"Receivable","StudentCredit");Apply(150,"StudentCredit","Funds");
        Assert.Equal(100,ledger["Receivable"]);Assert.Equal(0,ledger["StudentCredit"]);Assert.Equal(0,ledger["Funds"]);Assert.Equal(0,ledger.Values.Sum());
    }
    [Theory][InlineData("Receivable",-1)][InlineData("Funds",-1)][InlineData("StudentCredit",1)]
    public void NegativeDebtUnderfundedRefundAndUnbackedCreditFail(string bucket,long amount)=>Assert.Throws<ApiFault>(()=>FinanceRules.Balances(new Dictionary<string,long>{{bucket,amount}}));
    [Fact] public void PendingClaimsDoNotBecomeCollectedMoney()
    {Assert.Throws<ApiFault>(()=>FinanceRules.PaymentPost("Pending",true));FinanceRules.PaymentPost("Verified",true);FinanceRules.PaymentPost("Pending",false);Assert.Throws<ApiFault>(()=>FinanceRules.PaymentPost("Rejected",false));}
    [Fact] public void DuplicatePaymentReferencesNormalizeConsistently()
    {Assert.Equal(FinanceRules.Reference(" bank-123 "),FinanceRules.Reference("BANK-123"));Assert.Throws<ApiFault>(()=>FinanceRules.Reference("BANK\n123"));}
    [Fact] public void AllocationsRespectBothInvoiceDebtAndUnreservedSourceCash()
    {FinanceRules.Allocate(40,50,40);Assert.Throws<ApiFault>(()=>FinanceRules.Allocate(41,50,40));Assert.Throws<ApiFault>(()=>FinanceRules.Allocate(51,50,100));Assert.Equal(20,FinanceRules.AvailablePayment(100,50,10,20));}
    [Fact] public void ConcurrentRefundReservationInvariantLeavesOnlyUnreservedCash()
    {
        var initial=FinanceRules.AvailablePayment(100,0,0,0);FinanceRules.Refund(60,initial,true,false,0);
        var afterFirstReservation=FinanceRules.AvailablePayment(100,0,0,60);
        Assert.Equal(40,afterFirstReservation);Assert.Throws<ApiFault>(()=>FinanceRules.Refund(60,afterFirstReservation,true,false,0));
        FinanceRules.Refund(40,afterFirstReservation,true,false,0);Assert.Throws<ApiFault>(()=>FinanceRules.AvailablePayment(100,0,50,60));
    }
    [Fact] public void RefundabilityRequiresExplicitPolicyAndHonorsDebtRules()
    {Assert.Throws<ApiFault>(()=>FinanceRules.Refund(10,100,false,false,0));Assert.Throws<ApiFault>(()=>FinanceRules.Refund(10,100,true,true,1));FinanceRules.Refund(10,100,true,false,1);}
    [Fact] public void InstallmentsReconcileRemaindersWithoutAdditionalDebt()
    {
        var today=new DateOnly(2026,10,30);var rows=FinanceRules.Installments(100,[today,today.AddDays(30),today.AddDays(60)],3,today,today,90);
        Assert.Equal(new long[]{34,33,33},rows.Select(x=>x.Amount));Assert.Equal(100,rows.Sum(x=>x.Amount));
        var state=FinanceRules.Settlement(100,50,rows.Select(x=>(x.Date,x.Amount)).ToArray(),today.AddDays(40));
        Assert.Equal(34,state.Lines[0].SettledMinor);Assert.Equal(16,state.Lines[1].SettledMinor);Assert.Equal(17,state.Lines[1].OutstandingMinor);Assert.Equal("Overdue",state.Lines[1].Status);Assert.Equal(50,state.Lines.Sum(x=>x.OutstandingMinor));
    }
    [Fact] public void ReschedulingKeepsOriginalDebtSnapshotAndExposesNewUnscheduledCharges()
    {var today=new DateOnly(2026,10,30);var state=FinanceRules.Settlement(100,120,[(today,50),(today.AddDays(30),50)],today);Assert.Equal(20,state.Extra);Assert.Equal(120,state.Lines.Sum(x=>x.OutstandingMinor)+state.Extra);Assert.Throws<ApiFault>(()=>FinanceRules.Settlement(100,100,[(today,99)],today));}
    [Fact] public void InstallmentDatesAndCountsFollowInstitutionalPolicy()
    {var today=new DateOnly(2026,10,30);Assert.Throws<ApiFault>(()=>FinanceRules.Installments(100,[today,today],3,today,today,90));Assert.Throws<ApiFault>(()=>FinanceRules.Installments(100,[today.AddDays(91)],3,today,today,90));Assert.Throws<ApiFault>(()=>FinanceRules.Installments(100,[today,today.AddDays(1)],1,today,today,90));}
    [Fact] public void ScholarshipsHaveDeterministicOrderingAndCannotExceedChargeOrBudget()
    {
        var fixedId=Guid.NewGuid();var percentId=Guid.NewGuid();var fullId=Guid.NewGuid();var rules=new[]{new DiscountRule(fullId,30,"FULL","All","Full",0,0,1000),new(percentId,20,"PCT","All","Percent",5000,0,1000),new(fixedId,10,"FIX","Tuition","Fixed",0,30,20)};
        var result=FinanceRules.Discounts(new Dictionary<string,long>{{"Tuition",100}},rules);
        Assert.Equal(20,result[fixedId]);Assert.Equal(40,result[percentId]);Assert.Equal(40,result[fullId]);Assert.Equal(100,result.Values.Sum());
        Assert.Equal(result.OrderBy(x=>x.Key),FinanceRules.Discounts(new Dictionary<string,long>{{"Tuition",100}},rules.Reverse().ToArray()).OrderBy(x=>x.Key));
    }
    [Fact] public void ScholarshipCategoryCannotConsumeOtherFeesAndBudgetExhaustionGivesZero()
    {var id=Guid.NewGuid();var result=FinanceRules.Discounts(new Dictionary<string,long>{{"Tuition",60},{"Laboratory",40}},[new(id,1,"LAB","Laboratory","Fixed",0,100,100)]);Assert.Equal(40,result[id]);Assert.Equal(0,FinanceRules.Discounts(new Dictionary<string,long>{{"Tuition",100}},[new(id,1,"ZERO","All","Full",0,0,0)])[id]);}
    [Fact] public void ReportingNetsCorrectionsRatherThanCountingReversedCashOrAidTwice()
    {var result=FinanceRules.NetFlows(new Dictionary<string,decimal>{{"InvoicePosted",1000},{"DebitAdjustment",50},{"CreditNote",150},{"ScholarshipGranted",200},{"ScholarshipReclaimed",50},{"PaymentPosted",500},{"PaymentReversed",100},{"RefundExecuted",60},{"RefundReversed",10}});Assert.Equal(900,result.Assessed);Assert.Equal(150,result.Aid);Assert.Equal(400,result.Collected);Assert.Equal(50,result.Refunded);}
    [Fact] public void FinancialRestrictionsAreOptInAndCannotRetroactivelyApplyNewPolicies()
    {
        var today=new DateOnly(2026,10,30);var cutoff=new DateTime(2026,10,29,12,0,0,DateTimeKind.Utc);var original=cutoff.AddDays(-1);
        Assert.False(FinanceRules.HoldApplies(false,100,0,today,today,original,original,cutoff));
        Assert.False(FinanceRules.HoldApplies(true,100,0,today,today,original,cutoff.AddDays(1),cutoff));
        Assert.False(FinanceRules.HoldApplies(true,100,0,today,today,cutoff.AddDays(1),original,cutoff));
        Assert.True(FinanceRules.HoldApplies(true,100,0,today,today,original,original,cutoff));
        Assert.False(FinanceRules.HoldApplies(true,0,0,today,today,original,original,cutoff));
    }
    [Fact] public void HoldThresholdAndGraceBoundariesAreStrict()
    {var now=DateTime.UtcNow;var today=new DateOnly(2026,10,30);Assert.False(FinanceRules.HoldApplies(true,100,100,today,today,now,now,now));Assert.False(FinanceRules.HoldApplies(true,101,100,today,today.AddDays(1),now,now,now));Assert.True(FinanceRules.HoldApplies(true,101,100,today,today,now,now,now));}
    [Fact] public void InstitutionLocalDatesCanDifferFromUtc()
    {var instant=new DateTimeOffset(2026,10,30,22,30,0,TimeSpan.Zero);Assert.Equal(new DateOnly(2026,10,31),FinanceRules.Today("Asia/Baghdad",instant));Assert.Equal(new DateTime(2026,10,29,21,0,0,DateTimeKind.Utc),FinanceRules.UtcStart(new(2026,10,30),"Asia/Baghdad"));}
}
