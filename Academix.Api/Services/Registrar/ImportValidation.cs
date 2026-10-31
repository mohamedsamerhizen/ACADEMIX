using System.Net.Mail;
using Academix.Api.DTOs.Registrar;
using Academix.Api.Middleware;

namespace Academix.Api.Services.Registrar;

public static class ImportValidation
{
    public static IReadOnlyList<ImportStudentRecord> Parse(ImportUpload? upload)
    {
        if (upload?.Fields is null || upload.ClientRequestId == Guid.Empty || string.IsNullOrWhiteSpace(upload.SourceSystem) ||
            upload.SourceSystem.Length > 80 || upload.Rows is null || upload.Rows.Count is < 1 or > 1000)
            throw ApiFault.Bad("Source, mapping, idempotency key and 1-1000 rows required.");
        var map = upload.Fields;
        var fields = new[] { map.ExternalId, map.Email, map.ProgramId, map.AdmissionTermId, map.PlanId };
        if (fields.Any(string.IsNullOrWhiteSpace) || fields.Distinct(StringComparer.Ordinal).Count() != 5) throw ApiFault.Bad("Five distinct source fields required.");
        var optional=new[]{map.FullName,map.PublishedSectionId,map.InviteIfMissing}.Where(x=>x is not null).Select(x=>x!).ToArray();
        fields=fields.Concat(optional).ToArray();
        if(fields.Any(string.IsNullOrWhiteSpace)||fields.Distinct(StringComparer.Ordinal).Count()!=fields.Length)throw ApiFault.Bad("All mapped column names must be distinct.");
        var parsed = new List<ImportStudentRecord>();
        foreach (var row in upload.Rows)
        {
            if (row is null || fields.Any(f => !row.ContainsKey(f))) throw ApiFault.Bad("Import row does not match mapping.");
            // The supported file contract intentionally excludes credentials and unrecognized fields.
            if (row.Count != fields.Length) throw ApiFault.Bad("Only mapped fields may be uploaded.");
            string Value(string name) => row[name]?.Trim() ?? "";
            var external = Value(map.ExternalId); var email = Value(map.Email);
            if (external.Length is < 1 or > 100 || email.Length > 256 || !MailAddress.TryCreate(email, out var address) || address.Address != email ||
                !Guid.TryParse(Value(map.ProgramId), out var program) || program == Guid.Empty ||
                !Guid.TryParse(Value(map.AdmissionTermId), out var term) || term == Guid.Empty ||
                !Guid.TryParse(Value(map.PlanId), out var plan) || plan == Guid.Empty) throw ApiFault.Bad("Invalid student import row.");
            var fullName=map.FullName is null?null:ApplicantRules.Text(Value(map.FullName),2,200);
            Guid? section=null;
            if(map.PublishedSectionId is not null&&!string.IsNullOrEmpty(Value(map.PublishedSectionId)))
            {if(!Guid.TryParse(Value(map.PublishedSectionId),out var parsedSection)||parsedSection==Guid.Empty)throw ApiFault.Bad("Invalid published history section reference.");section=parsedSection;}
            var invite=false;
            if(map.InviteIfMissing is not null&&!bool.TryParse(Value(map.InviteIfMissing),out invite))throw ApiFault.Bad("InviteIfMissing must be true or false.");
            if(invite&&fullName is null)throw ApiFault.Bad("An invitation import requires a full applicant name.");
            parsed.Add(new(external, email.ToUpperInvariant(), program, term, plan,fullName,section,invite));
        }
        if (parsed.Select(x => x.ExternalId).Distinct(StringComparer.Ordinal).Count() != parsed.Count ||
            parsed.Select(x => x.Email).Distinct(StringComparer.OrdinalIgnoreCase).Count() != parsed.Count)
            throw ApiFault.Bad("Duplicate external identity or student email in import file.");
        return parsed;
    }
}
