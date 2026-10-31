using Academix.Api.Services.Registrar;
using System.Security.Claims;
using Academix.Api.Domain.Registrar;
using Academix.Api.DTOs.Registrar;
using Academix.Api.Middleware;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Institutional;

public sealed partial class InstitutionalDocumentService
{
    public async Task<Academix.Api.DTOs.Institutional.DocumentRequestView> RequestReissuance(ClaimsPrincipal user,Guid id,Academix.Api.DTOs.Institutional.ReissueDocumentRequest input,CancellationToken ct)
    {
        if(input is null)throw ApiFault.Bad("Reissuance request required.");
        var doc=await db.IssuedAcademicDocuments.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw ApiFault.NotFound();
        await security.RequireStudentAsync(user,doc.StudentId,doc.InstitutionId,ct);
        return await SubmitApplication(user,new(input.ClientRequestId,doc.Kind,input.Purpose,id),ct);
    }
    private static TemplateView TemplateView(DocumentTemplateRevision x)=>new(x.Id,x.Kind,x.Revision,x.Heading,x.Footer,x.AccentColor,x.FontFamily,x.RightToLeft,x.PublishedAtUtc.HasValue);
    public Task<TemplateView> CreateTemplate(ClaimsPrincipal user,Guid inst,TemplateInput input,CancellationToken ct)
        =>retry.ExecuteAsync<InstitutionalDocumentService,TemplateView>(s=>s.CreateTemplateCore(user,inst,input,ct),ct);
    private async Task<TemplateView> CreateTemplateCore(ClaimsPrincipal user,Guid inst,TemplateInput input,CancellationToken ct)
    {
        if(input is null||input.Revision<1||input.AccentColor is not {Length:7}||input.AccentColor[0]!='#'||!input.AccentColor[1..].All(Uri.IsHexDigit))throw ApiFault.Bad("Valid template revision/color required.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);await Academix.Api.Services.Academic.RegistrationCoordination.Lock(db,inst,ct);
        var actor=await security.RequireAdminAsync(user,inst,ct);var kind=InstitutionalDocumentPolicyRules.Kind(input.Kind);
        if(await db.Set<DocumentTemplateRevision>().AnyAsync(x=>x.InstitutionId==inst&&x.Kind==kind&&x.Revision==input.Revision,ct))throw ApiFault.Conflict("Template revision already exists.");
        var row=new DocumentTemplateRevision{InstitutionId=inst,Kind=kind,Revision=input.Revision,Heading=ApplicantRules.Text(input.Heading,2,200),
            Footer=ApplicantRules.Text(input.Footer,1,1000),FontFamily=ApplicantRules.Text(input.FontFamily,1,100),AccentColor=input.AccentColor,RightToLeft=input.RightToLeft,CreatedBy=actor.Id};
        db.Set<DocumentTemplateRevision>().Add(row);security.Audit(actor.Id,inst,"document.template.created",row.Id.ToString());
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return TemplateView(row);
    }
    public Task<TemplateView> PublishTemplate(ClaimsPrincipal user,Guid inst,Guid id,CancellationToken ct)
        =>retry.ExecuteAsync<InstitutionalDocumentService,TemplateView>(s=>s.PublishTemplateCore(user,inst,id,ct),ct);
    private async Task<TemplateView> PublishTemplateCore(ClaimsPrincipal user,Guid inst,Guid id,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);await Academix.Api.Services.Academic.RegistrationCoordination.Lock(db,inst,ct);
        var actor=await security.RequireAdminAsync(user,inst,ct);
        var row=await db.Set<DocumentTemplateRevision>().SingleOrDefaultAsync(x=>x.Id==id&&x.InstitutionId==inst,ct)??throw ApiFault.NotFound();
        if(row.PublishedAtUtc is null)
        {
            // A font/layout failure must be discovered before this revision becomes active.
            pdfRenderer.Render(new(){InstitutionId=inst,Kind=row.Kind,ReferenceNumber="TEMPLATE-PREVIEW",SnapshotJson="{}",IssuedAtUtc=DateTime.UtcNow,ExpiresAtUtc=DateTime.UtcNow.AddDays(1),LegacyProof=new string('0',64)},row);
            row.PublishedAtUtc=DateTime.UtcNow;security.Audit(actor.Id,inst,"document.template.published",id.ToString());await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);return TemplateView(row);
    }
    public async Task<IReadOnlyList<TemplateView>> Templates(ClaimsPrincipal user,Guid inst,CancellationToken ct)
    {
        await security.RequireAdminAsync(user,inst,ct);
        return (await db.Set<DocumentTemplateRevision>().AsNoTracking().Where(x=>x.InstitutionId==inst).OrderBy(x=>x.Kind).ThenByDescending(x=>x.Revision).ToListAsync(ct)).Select(TemplateView).ToArray();
    }
    private async Task<DocumentTemplateRevision> Template(Guid inst,string kind,Guid actor,CancellationToken ct)
    {
        var template=await db.Set<DocumentTemplateRevision>().Where(x=>x.InstitutionId==inst&&x.Kind==kind&&x.PublishedAtUtc!=null).OrderByDescending(x=>x.Revision).FirstOrDefaultAsync(ct);
        if(template is not null)return template;
        // Institution-specific built-in revision is immutable like an explicitly configured template.
        var revision=(await db.Set<DocumentTemplateRevision>().Where(x=>x.InstitutionId==inst&&x.Kind==kind).Select(x=>(int?)x.Revision).MaxAsync(ct)??0)+1;
        var name=await db.Institutions.Where(x=>x.Id==inst).Select(x=>x.Name).SingleAsync(ct);
        template=new(){InstitutionId=inst,Kind=kind,Revision=revision,Heading=name,FontFamily=pdfRenderer.DefaultFontFamily,RightToLeft=name.Any(c=>c>=0x0600&&c<=0x06ff),Footer="Issued by the institution registrar. Verify current validity before relying on this document.",CreatedBy=actor,PublishedAtUtc=DateTime.UtcNow};
        db.Set<DocumentTemplateRevision>().Add(template);return template;
    }
    public Task<VerificationKeyView> RotateKey(ClaimsPrincipal user,Guid inst,CancellationToken ct)
        =>retry.ExecuteAsync<InstitutionalDocumentService,VerificationKeyView>(s=>s.RotateKeyCore(user,inst,ct),ct);
    private async Task<VerificationKeyView> RotateKeyCore(ClaimsPrincipal user,Guid inst,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);await Academix.Api.Services.Academic.RegistrationCoordination.Lock(db,inst,ct);
        var actor=await security.RequireAdminAsync(user,inst,ct);var now=DateTime.UtcNow;
        await db.Set<DocumentVerificationKey>().Where(x=>x.InstitutionId==inst&&x.RetiredAtUtc==null&&x.RevokedAtUtc==null).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.RetiredAtUtc,now),ct);
        var key=await documentKeys.Active(inst,ct);security.Audit(actor.Id,inst,"document.key.rotated",key.Id.ToString());
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return new(key.Id,key.CreatedAtUtc,key.RetiredAtUtc,key.RevokedAtUtc);
    }
    public async Task<IReadOnlyList<VerificationKeyView>> Keys(ClaimsPrincipal user,Guid inst,CancellationToken ct)
    {
        await security.RequireAdminAsync(user,inst,ct);
        return await db.Set<DocumentVerificationKey>().AsNoTracking().Where(x=>x.InstitutionId==inst).OrderBy(x=>x.CreatedAtUtc).Select(x=>new VerificationKeyView(x.Id,x.CreatedAtUtc,x.RetiredAtUtc,x.RevokedAtUtc)).ToListAsync(ct);
    }
    public Task RevokeKey(ClaimsPrincipal user,Guid inst,Guid id,CancellationToken ct)=>retry.ExecuteAsync<InstitutionalDocumentService>(s=>s.RevokeKeyCore(user,inst,id,ct),ct);
    private async Task RevokeKeyCore(ClaimsPrincipal user,Guid inst,Guid id,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);await Academix.Api.Services.Academic.RegistrationCoordination.Lock(db,inst,ct);
        var actor=await security.RequireAdminAsync(user,inst,ct);var key=await db.Set<DocumentVerificationKey>().SingleOrDefaultAsync(x=>x.Id==id&&x.InstitutionId==inst,ct)??throw ApiFault.NotFound();
        if(key.RevokedAtUtc is null){key.RevokedAtUtc=DateTime.UtcNow;key.RetiredAtUtc??=DateTime.UtcNow;security.Audit(actor.Id,inst,"document.key.revoked",id.ToString());await db.SaveChangesAsync(ct);}
        await tx.CommitAsync(ct);
    }
    public async Task<ArtifactContent> Download(ClaimsPrincipal user,Guid id,CancellationToken ct)
    {
        var doc=await db.IssuedAcademicDocuments.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw ApiFault.NotFound();
        await security.RequireStudentAsync(user,doc.StudentId,doc.InstitutionId,ct);
        if(doc.ArtifactKey is null||doc.ArtifactDigest is null)throw ApiFault.Conflict("Legacy JSON-only document: request a current PDF reissuance.");
        var bytes=await documentFiles.GetAsync(doc.ArtifactKey,ct);
        if(!DocumentIntegrity.Equal(DocumentIntegrity.Digest(bytes),doc.ArtifactDigest)||!await documentKeys.ArtifactSignatureValid(doc,ct))throw ApiFault.Conflict("Stored document integrity failure.");
        return new(bytes,doc.ArtifactDigest,doc.ReferenceNumber+".pdf");
    }
    public Task Supersede(ClaimsPrincipal user,Guid id,SupersedeDocument input,CancellationToken ct)=>retry.ExecuteAsync<InstitutionalDocumentService>(s=>s.SupersedeCore(user,id,input,ct),ct);
    private async Task SupersedeCore(ClaimsPrincipal user,Guid id,SupersedeDocument input,CancellationToken ct)
    {
        if(input is null||input.ReplacementId==id)throw ApiFault.Bad("A different replacement document is required.");
        var initial=await db.IssuedAcademicDocuments.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw ApiFault.NotFound();
        await using var tx=await db.Database.BeginTransactionAsync(ct);await Academix.Api.Services.Academic.RegistrationCoordination.Lock(db,initial.InstitutionId,ct);
        var actor=await security.RequireAdminAsync(user,initial.InstitutionId,ct);
        var old=await db.IssuedAcademicDocuments.SingleAsync(x=>x.Id==id,ct);
        if(old.SupersededById==input.ReplacementId)return;
        InstitutionalDocumentPolicyRules.EnsureVersion(old.Version,input.ExpectedVersion);
        var replacement=await db.IssuedAcademicDocuments.SingleOrDefaultAsync(x=>x.Id==input.ReplacementId&&x.InstitutionId==old.InstitutionId&&x.StudentId==old.StudentId&&x.Kind==old.Kind,ct)??throw ApiFault.NotFound();
        if(old.SupersededById.HasValue||replacement.IssuedAtUtc<=old.IssuedAtUtc||replacement.RevokedAtUtc.HasValue||replacement.SourceInvalidatedAtUtc.HasValue||replacement.SupersededById.HasValue||replacement.ArtifactKey is null||replacement.ExpiresAtUtc<=DateTime.UtcNow)throw ApiFault.Conflict("Replacement must be a newer valid PDF issuance of the same document kind.");
        if(replacement.VerificationKeyId is Guid replacementKey&&await db.Set<DocumentVerificationKey>().AnyAsync(x=>x.Id==replacementKey&&x.RevokedAtUtc!=null,ct))throw ApiFault.Conflict("Replacement verification key is revoked.");
        old.SupersededById=replacement.Id;old.Version++;
        Event(await db.InstitutionalDocumentRequests.SingleAsync(x=>x.Id==old.RequestId,ct),actor.Id,"superseded",ApplicantRules.Text(input.Reason,5,500),old.Id);
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }
}
