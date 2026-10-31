using Academix.Api.Domain.Finance;
using Academix.Api.DTOs.Finance;
using Academix.Api.Middleware;
namespace Academix.Api.Services.Finance;

public sealed record DiscountRule(Guid Id,int Priority,string Code,string Category,string Mode,int BasisPoints,long FixedMinor,long AvailableFundingMinor);
public static class FinanceRules
{
    public const long MaximumMinor=9_000_000_000_000_000;
    public static IReadOnlyDictionary<string,int> Currencies=>FinanceCurrencyCatalog.MinorDigits;
    public static string Currency(string? code)
    {var value=code?.Trim().ToUpperInvariant();if(value is null||!Currencies.ContainsKey(value))throw ApiFault.Bad("Unsupported ISO currency code.");return value;}
    public static void SameCurrency(string first,string second)
    {if(Currency(first)!=Currency(second))throw ApiFault.Conflict("Cross-currency financial operations are not supported.");}
    public static long Money(long value,bool zero=false)
    {if(value<(zero?0:1)||value>MaximumMinor)throw ApiFault.Bad("Amount must be a positive, bounded integer in currency minor units.");return value;}
    public static long Sum(IEnumerable<long> values)
    {
        var result=values.Aggregate(0m,(a,b)=>a+b);
        if(result< -MaximumMinor||result>MaximumMinor)throw ApiFault.Conflict("Financial amount exceeds the supported bound.");return (long)result;
    }
    public static string Text(string? value,int max,string label)
    {if(string.IsNullOrWhiteSpace(value)||value.Trim().Length>max)throw ApiFault.Bad("A valid "+label+" is required.");return value.Trim();}
    public static string Reference(string? value)
    {var reference=Text(value,120,"payment reference").ToUpperInvariant();if(reference.Any(x=>!char.IsAsciiLetterOrDigit(x)&&!"-_/.: ".Contains(x)))throw ApiFault.Bad("Payment references support ASCII letters, digits, spaces and -_/.: punctuation.");return reference;}
    public static void Version(long actual,long expected)
    {if(actual!=expected)throw ApiFault.Conflict("Financial record version changed.");}
    public static TimeZoneInfo Zone(string value)
    {try{return TimeZoneInfo.FindSystemTimeZoneById(value);}catch(Exception e)when(e is TimeZoneNotFoundException or InvalidTimeZoneException){throw ApiFault.Bad("A supported institution time zone is required.");}}
    public static DateOnly Today(string zone,DateTimeOffset? now=null)=>DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now??DateTimeOffset.UtcNow,Zone(zone)).DateTime);
    public static void Dates(DateOnly from,DateOnly until)
    {if(from>=until||from.Year<2000||until.Year>2200)throw ApiFault.Bad("Use an ordered effective/reporting period within 2000-2200; Until is exclusive.");}
    public static long Price(long unit,decimal quantity)
    {Money(unit);if(quantity<=0||quantity>10000||decimal.Round(quantity,2)!=quantity)throw ApiFault.Bad("Invalid authoritative billing quantity.");var result=decimal.Round(unit*quantity,0,MidpointRounding.AwayFromZero);if(result>MaximumMinor)throw ApiFault.Bad("Fee exceeds the monetary bound.");return Money((long)result);}
    public static decimal FeeQuantity(string mode,decimal creditHours)=>mode switch{"Fixed"=>1,"PerCredit"=>creditHours,_=>throw ApiFault.Bad("Unsupported fee pricing mode.")};
    public static bool ScheduleScope(Guid? scheduleTerm,int? yearLabel,Guid term,DateOnly termStart,DateOnly from,DateOnly until)
        =>scheduleTerm.HasValue?scheduleTerm==term:yearLabel.HasValue&&termStart>=from&&termStart<until;
    public static void Balances(IReadOnlyDictionary<string,long> values)
    {if(values.Values.Any(x=>x< -MaximumMinor||x>MaximumMinor)||values.GetValueOrDefault("Receivable")<0||values.GetValueOrDefault("StudentCredit")>0||values.GetValueOrDefault("Funds")<0)throw ApiFault.Conflict("Posting would violate supported balance, receivable, cash-credit or funds invariants.");}
    public static (decimal Assessed,decimal Aid,decimal Collected,decimal Refunded) NetFlows(IReadOnlyDictionary<string,decimal> values)
        =>(values.GetValueOrDefault("InvoicePosted")+values.GetValueOrDefault("DebitAdjustment")-values.GetValueOrDefault("CreditNote"),
            values.GetValueOrDefault("ScholarshipGranted")-values.GetValueOrDefault("ScholarshipReclaimed"),
            values.GetValueOrDefault("PaymentPosted")-values.GetValueOrDefault("PaymentReversed"),values.GetValueOrDefault("RefundExecuted")-values.GetValueOrDefault("RefundReversed"));
    public static void Journal(long amount,IReadOnlyList<LedgerLeg> lines)
    {Money(amount);if(lines.Count!=2||lines.Any(x=>x.Bucket is not ("Receivable" or "Funds" or "StudentCredit" or "Revenue" or "Aid"))||lines[0].Bucket==lines[1].Bucket||lines.Any(x=>x.SignedMinor!=amount&&x.SignedMinor!=-amount)||Sum(lines.Select(x=>x.SignedMinor))!=0)throw ApiFault.Conflict("A journal must contain equal and opposite legs in two valid accounts.");}
    public static DateTime UtcStart(DateOnly date,string zoneId)
    {
        var zone=Zone(zoneId);var local=date.ToDateTime(TimeOnly.MinValue,DateTimeKind.Unspecified);
        for(var i=0;zone.IsInvalidTime(local)&&i<2880;i++)local=local.AddMinutes(1);
        return zone.IsAmbiguousTime(local)?new DateTimeOffset(local,zone.GetAmbiguousTimeOffsets(local).Max()).UtcDateTime:TimeZoneInfo.ConvertTimeToUtc(local,zone);
    }
    public static void PaymentPost(string status,bool verificationRequired)
    {if(status!="Verified"&&(status!="Pending"||verificationRequired))throw ApiFault.Conflict("Payment verification is required before posting.");}
    public static long AvailablePayment(long posted,long allocated,long executedRefunds,long reservations)
    {var amount=Sum([posted,-allocated,-executedRefunds,-reservations]);if(amount<0)throw ApiFault.Conflict("Cash credit is already allocated, refunded or reserved.");return amount;}
    public static void Allocate(long requested,long available,long outstanding)
    {Money(requested);if(requested>available||requested>outstanding)throw ApiFault.Conflict("Allocation exceeds unapplied cash or invoice debt.");}
    public static void Refund(long requested,long available,bool allowed,bool blockWithDebt,long debt)
    {Money(requested);if(!allowed||requested>available||blockWithDebt&&debt>0)throw ApiFault.Conflict("Refund exceeds currently eligible unallocated cash under the captured policy.");}
    public static IReadOnlyList<(DateOnly Date,long Amount)> Installments(long debt,IReadOnlyList<DateOnly> dates,int maximum,DateOnly today,DateOnly invoiceDue,int extension)
    {
        Money(debt);if(dates is null||dates.Count<1||dates.Count>maximum||debt<dates.Count||dates.Any(x=>x<today||x>invoiceDue.AddDays(extension))||!dates.SequenceEqual(dates.Distinct().Order()))throw ApiFault.Bad("Installment dates must be distinct, ordered, within policy, and reconcile to positive debt.");
        var quotient=debt/dates.Count;var remainder=debt%dates.Count;
        return dates.Select((date,i)=>(date,quotient+(i<remainder?1:0))).ToArray();
    }
    public static (IReadOnlyList<InstallmentView> Lines,long Extra) Settlement(long originalDebt,long currentDebt,IReadOnlyList<(DateOnly Due,long Amount)> schedule,DateOnly today)
    {
        if(Sum(schedule.Select(x=>x.Amount))!=originalDebt)throw ApiFault.Conflict("Installment schedule does not reconcile to its debt snapshot.");
        var settled=Math.Max(0,originalDebt-currentDebt);var result=new List<InstallmentView>();
        for(var i=0;i<schedule.Count;i++)
        {var item=schedule[i];var used=Math.Min(item.Amount,settled);settled-=used;var outstanding=item.Amount-used;
            result.Add(new(i+1,item.Due,item.Amount,used,outstanding,outstanding==0?"Settled":item.Due<today?"Overdue":used>0?"PartiallySettled":"Scheduled"));}
        return(result,Math.Max(0,currentDebt-originalDebt));
    }
    public static IReadOnlyDictionary<Guid,long> Discounts(IReadOnlyDictionary<string,long> baseByCategory,IReadOnlyList<DiscountRule> rules)
    {
        var remaining=baseByCategory.ToDictionary(x=>x.Key,x=>Money(x.Value,true),StringComparer.Ordinal);var result=new Dictionary<Guid,long>();
        foreach(var rule in rules.OrderBy(x=>x.Priority).ThenBy(x=>x.Code,StringComparer.Ordinal).ThenBy(x=>x.Id))
        {
            var categories=remaining.Keys.Where(x=>rule.Category=="All"||x==rule.Category).Order(StringComparer.Ordinal).ToArray();
            var eligible=Sum(categories.Select(x=>remaining[x]));
            var proposed=rule.Mode switch {"Full"=>eligible,"Fixed"=>rule.FixedMinor,"Percent"=>(long)decimal.Round(eligible*(decimal)rule.BasisPoints/10000,0,MidpointRounding.AwayFromZero),_=>throw ApiFault.Bad("Unsupported scholarship reduction.")};
            var grant=Math.Min(eligible,Math.Min(proposed,rule.AvailableFundingMinor));Money(grant,true);result.Add(rule.Id,grant);
            var rest=grant;foreach(var category in categories){var part=Math.Min(rest,remaining[category]);remaining[category]-=part;rest-=part;}
        }
        return result;
    }
    public static bool HoldApplies(bool enabled,long overdue,long threshold,DateOnly today,DateOnly from,DateTime invoicePosted,DateTime policyPublished,DateTime cutoff)
        =>enabled&&overdue>threshold&&today>=from&&invoicePosted<=cutoff&&policyPublished<=cutoff;
    public static int Offset(int page)
    {if(page is <1 or >10000)throw ApiFault.Bad("Page must be 1-10000.");return(page-1)*50;}
}
