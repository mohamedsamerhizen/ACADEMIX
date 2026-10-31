using Microsoft.VisualBasic.FileIO;
using Academix.Api.Middleware;
namespace Academix.Api.Services.Registrar;
public static class ImportCsvParser
{
    public static IReadOnlyList<Dictionary<string,string?>> Parse(TextReader reader)
    {
        using var parser=new TextFieldParser(reader){TextFieldType=FieldType.Delimited,HasFieldsEnclosedInQuotes=true,TrimWhiteSpace=true};parser.SetDelimiters(",");
        try
        {
            var header=parser.ReadFields();if(header is null||header.Length is <5 or >8||header.Any(string.IsNullOrWhiteSpace)||header.Distinct(StringComparer.Ordinal).Count()!=header.Length)throw ApiFault.Bad("CSV requires distinct mapped column names.");
            var result=new List<Dictionary<string,string?>>();
            while(!parser.EndOfData)
            {
                var fields=parser.ReadFields();if(fields is null||fields.Length!=header.Length||result.Count>=1000)throw ApiFault.Bad("Invalid CSV row count or column count.");
                result.Add(header.Select((name,i)=>(name,value:(string?)fields[i])).ToDictionary(x=>x.name,x=>x.value,StringComparer.Ordinal));
            }
            if(result.Count==0)throw ApiFault.Bad("CSV contains no records.");return result;
        }
        catch(MalformedLineException){throw ApiFault.Bad("Malformed quoted CSV.");}
    }
}
