using System.Text.Json;
using Academix.Api.Domain.Institutional;
using Academix.Api.Domain.Registrar;
using Academix.Api.Middleware;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Academix.Api.Services.Institutional;

public sealed class DocumentPdfRenderer(IConfiguration configuration)
{
    private static readonly object SettingsLock=new();
    public string DefaultFontFamily=>configuration["Documents:DefaultFontFamily"]??(OperatingSystem.IsWindows()?"Arial":"DejaVu Sans");
    public byte[] Render(IssuedAcademicDocument document,DocumentTemplateRevision template)
    {
        if(!Enum.TryParse<LicenseType>(configuration["Documents:PdfLicense"],out var license)||!Enum.IsDefined(license))
            throw ApiFault.Conflict("Configure Documents:PdfLicense for the institution's QuestPDF license before PDF issuance.");
        lock(SettingsLock){QuestPDF.Settings.License=license;QuestPDF.Settings.UseSystemFonts=true;}
        using var content=JsonDocument.Parse(document.SnapshotJson);
        return Document.Create(c=>c.Page(page=>
        {
            page.Size(PageSizes.A4);page.Margin(36);page.DefaultTextStyle(x=>x.FontFamily(template.FontFamily).FontSize(10));
            if(template.RightToLeft)page.ContentFromRightToLeft();
            page.Header().Column(col=>{
                col.Item().Text(template.Heading).FontSize(21).Bold().FontColor(template.AccentColor);
                col.Item().Text($"{document.Kind} | {document.ReferenceNumber}").FontSize(12);
                col.Item().PaddingTop(8).LineHorizontal(1).LineColor(template.AccentColor);
            });
            page.Content().PaddingVertical(15).Column(col=>{
                col.Spacing(5);
                col.Item().Text($"Issued: {document.IssuedAtUtc:O}");col.Item().Text($"Valid until: {document.ExpiresAtUtc:O}");
                foreach(var line in Flatten(content.RootElement,""))col.Item().Text(line);
                col.Item().PaddingTop(12).Text($"Template revision {template.Revision} / Document {document.Id}").FontSize(8);
                col.Item().Text("Verification proof: "+document.LegacyProof).FontSize(7);
                col.Item().Text("Verify this reference and its proof through the institution verification service. Current revocation and supersession status applies.").FontSize(8);
            });
            page.Footer().Column(col=>{
                col.Item().Text(template.Footer).FontSize(8);
                col.Item().AlignCenter().Text(t=>{t.CurrentPageNumber();t.Span(" / ");t.TotalPages();});
            });
        })).GeneratePdf();
    }
    private static IEnumerable<string> Flatten(JsonElement node,string path)
    {
        if(node.ValueKind==JsonValueKind.Object)
        {
            foreach(var item in node.EnumerateObject())foreach(var line in Flatten(item.Value,string.IsNullOrEmpty(path)?item.Name:path+" / "+item.Name))yield return line;
        }
        else if(node.ValueKind==JsonValueKind.Array)
        {
            var i=0;foreach(var item in node.EnumerateArray())foreach(var line in Flatten(item,path+" #"+(++i)))yield return line;
        }
        else yield return path+": "+(node.ValueKind==JsonValueKind.String?node.GetString():node.ToString());
    }
}
