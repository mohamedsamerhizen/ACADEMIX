using System.Net.Mail;
using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Identity;
using Academix.Api.Domain.Lifecycle;
using Academix.Api.Domain.Records;
using Academix.Api.Domain.Registrar;
using Academix.Api.DTOs.Registrar;
using Academix.Api.Middleware;
using Academix.Api.Services.Academic;
using Academix.Api.Services.Identity;
using Academix.Api.Services.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Registrar;

public static class ApplicantRules
{
    public static string Text(string? value,int min,int max) => value?.Trim() is string s && s.Length>=min && s.Length<=max ? s : throw ApiFault.Bad("Invalid applicant text.");
    public static string Email(string? value)
    {
        var s=Text(value,3,256);
        return MailAddress.TryCreate(s,out var parsed)&&parsed.Address==s ? s : throw ApiFault.Bad("Invalid email.");
    }
    public static void CanActivate(string status) { if(status is not ("Accepted" or "Converted"))throw ApiFault.Conflict("Admission acceptance is required before student provisioning."); }
}
public sealed class ApplicantService(AcademixDbContext db,IdentitySecurity access,UserManager<ApplicationUser> users,
    IDataProtectionProvider protection,RetryingServiceWork retry)
{
    public Task<InvitationReceipt> RenewInvitation(ClaimsPrincipal principal,Guid inst,Guid id,CancellationToken ct)
        =>retry.ExecuteAsync<ApplicantService,InvitationReceipt>(s=>s.RenewInvitationCore(principal,inst,id,ct),ct);
    private async Task<InvitationReceipt> RenewInvitationCore(ClaimsPrincipal principal,Guid inst,Guid id,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);await RegistrationCoordination.Lock(db,inst,ct);
        var actor=await access.RequireAdminAsync(principal,inst,ct);
        var row=(await db.Set<Applicant>().FromSqlInterpolated($"SELECT * FROM registrar_applicants WHERE \"Id\"={id} AND \"InstitutionId\"={inst} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()??throw ApiFault.NotFound();
        if(row.Status is "Rejected" or "Converted")throw ApiFault.Conflict("Finalized applicant cannot receive a new activation invitation.");
        var token=IdentitySecurity.RandomToken();row.InvitationHash=IdentitySecurity.TokenHash(token);row.ProtectedInvitation=Protector.Protect(token);row.InvitationExpiresAtUtc=DateTime.UtcNow.AddDays(14);row.Version++;
        access.Audit(actor.Id,inst,"applicant.invitation.renewed",id.ToString());await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        row.InvitationExpiresAtUtc = new DateTime(row.InvitationExpiresAtUtc.Ticks / 10 * 10, DateTimeKind.Utc);
        return new(row.Id,token,row.InvitationExpiresAtUtc);
    }
    private IDataProtector Protector => protection.CreateProtector("ACADEMIX.ApplicantInvitations.v1");
    internal async Task<Applicant> PrepareImportedApplicant(Guid inst,Guid importRow,ImportStudentRecord input,Guid creator,Guid approver,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction is null||creator==approver||!await RegistrarAuthority.IsAdmin(db,approver,inst,ct))throw ApiFault.Forbidden();
        var name=ApplicantRules.Text(input.FullName,2,200);
        var existing=await db.Set<Applicant>().SingleOrDefaultAsync(x=>x.InstitutionId==inst&&x.NormalizedEmail==input.Email,ct);
        if(existing is not null)
        {
            if(existing.ProgramId!=input.ProgramId||existing.TermId!=input.AdmissionTermId||existing.FullName!=name||existing.Status is "Rejected" or "Converted"||existing.Status!="Accepted"&&existing.CreatedBy==approver)throw ApiFault.Conflict("Existing applicant conflicts with the approved import.");
            if(existing.Status!="Accepted")
            {existing.Status="Accepted";existing.ReviewedBy=approver;existing.DecisionReason="Independent institutional import approval";existing.Version++;History(existing,approver);}
            return existing;
        }
        var token=IdentitySecurity.RandomToken();
        var row=new Applicant{InstitutionId=inst,ClientRequestId=importRow,Email=input.Email,NormalizedEmail=input.Email,FullName=name,ProgramId=input.ProgramId,TermId=input.AdmissionTermId,
            Statement="Institutional student import "+importRow.ToString("N"),CreatedBy=creator,InvitationHash=IdentitySecurity.TokenHash(token),ProtectedInvitation=Protector.Protect(token),InvitationExpiresAtUtc=DateTime.UtcNow.AddDays(14)};
        db.Set<Applicant>().Add(row);History(row,creator);row.Status="Accepted";row.ReviewedBy=approver;row.DecisionReason="Independent institutional import approval";History(row,approver);
        return row;
    }
    private static ApplicantView Map(Applicant x)=>new(x.Id,x.Email,x.FullName,x.ProgramId,x.TermId,x.Statement,x.Status,x.Version,x.StudentId);
    private void History(Applicant x,Guid? actor)
    {
        db.Set<ApplicantHistory>().Add(new(){InstitutionId=x.InstitutionId,ApplicantId=x.Id,Status=x.Status,ActorId=actor});
        access.Audit(actor,x.InstitutionId,"applicant."+x.Status.ToLowerInvariant(),x.Id.ToString());
    }
    public Task<InvitationReceipt> Invite(ClaimsPrincipal principal,Guid inst,ApplicantInvitation input,CancellationToken ct)
        =>retry.ExecuteAsync<ApplicantService,InvitationReceipt>(s=>s.InviteCore(principal,inst,input,ct),ct);
    private async Task<InvitationReceipt> InviteCore(ClaimsPrincipal principal,Guid inst,ApplicantInvitation input,CancellationToken ct)
    {
        if(input is null||input.ClientRequestId==Guid.Empty)throw ApiFault.Bad("Idempotency key required.");
        var email=ApplicantRules.Email(input.Email);var name=ApplicantRules.Text(input.FullName,2,200);var statement=ApplicantRules.Text(input.Statement,10,1500);
        await using var tx=await db.Database.BeginTransactionAsync(ct);await RegistrationCoordination.Lock(db,inst,ct);
        var actor=await access.RequireAdminAsync(principal,inst,ct);
        var old=await db.Set<Applicant>().SingleOrDefaultAsync(x=>x.InstitutionId==inst&&x.ClientRequestId==input.ClientRequestId,ct);
        if(old is not null)
        {
            if(old.CreatedBy!=actor.Id||old.NormalizedEmail!=email.ToUpperInvariant()||old.ProgramId!=input.ProgramId||old.TermId!=input.TermId||old.FullName!=name||old.Statement!=statement)throw ApiFault.Conflict("Invitation key reused with different data.");
            return new(old.Id,Protector.Unprotect(old.ProtectedInvitation),old.InvitationExpiresAtUtc);
        }
        if(!await db.AcademicPrograms.AnyAsync(x=>x.Id==input.ProgramId&&x.InstitutionId==inst&&x.IsActive,ct)||
            !await db.AcademicTerms.AnyAsync(x=>x.Id==input.TermId&&x.InstitutionId==inst&&x.Status!="Closed",ct))throw ApiFault.Bad("Invalid institution program or term.");
        if(await users.FindByEmailAsync(email) is not null||await db.Set<Applicant>().AnyAsync(x=>x.InstitutionId==inst&&x.NormalizedEmail==email.ToUpperInvariant(),ct))throw ApiFault.Conflict("Identity or applicant already exists; use its existing workflow.");
        var token=IdentitySecurity.RandomToken();
        var row=new Applicant{InstitutionId=inst,ClientRequestId=input.ClientRequestId,Email=email,NormalizedEmail=email.ToUpperInvariant(),FullName=name,
            ProgramId=input.ProgramId,TermId=input.TermId,Statement=statement,CreatedBy=actor.Id,InvitationHash=IdentitySecurity.TokenHash(token),ProtectedInvitation=Protector.Protect(token),InvitationExpiresAtUtc=DateTime.UtcNow.AddDays(14)};
        db.Set<Applicant>().Add(row);History(row,actor.Id);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        row.InvitationExpiresAtUtc = new DateTime(row.InvitationExpiresAtUtc.Ticks / 10 * 10, DateTimeKind.Utc);
        return new(row.Id,token,row.InvitationExpiresAtUtc);
    }
    private async Task<Applicant> Token(string? token,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(token)||token.Length>512)throw ApiFault.NotFound();
        var hash=IdentitySecurity.TokenHash(token);
        return (await db.Set<Applicant>().FromSqlInterpolated($"SELECT * FROM registrar_applicants WHERE \"InvitationHash\"={hash} AND \"InvitationExpiresAtUtc\">{DateTime.UtcNow} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()??throw ApiFault.NotFound();
    }
    public Task<ApplicantView> Access(ApplicantAccess input,CancellationToken ct)=>retry.ExecuteAsync<ApplicantService,ApplicantView>(s=>s.AccessCore(input,ct),ct);
    private async Task<ApplicantView> AccessCore(ApplicantAccess input,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);var row=await Token(input?.Token,ct);
        if(row.Status=="Invited"||row.Status=="Accepted"&&row.VerifiedAtUtc is null)
        {if(row.Status=="Invited")row.Status="Submitted";row.VerifiedAtUtc=DateTime.UtcNow;row.Version++;History(row,null);await db.SaveChangesAsync(ct);}
        await tx.CommitAsync(ct);return Map(row);
    }
    public Task<ApplicantView> Update(ApplicantProfileUpdate input,CancellationToken ct)=>retry.ExecuteAsync<ApplicantService,ApplicantView>(s=>s.UpdateCore(input,ct),ct);
    private async Task<ApplicantView> UpdateCore(ApplicantProfileUpdate input,CancellationToken ct)
    {
        if(input is null)throw ApiFault.Bad("Profile required.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);var row=await Token(input.Token,ct);
        if(row.Status!="Submitted"||row.Version!=input.ExpectedVersion)throw ApiFault.Conflict("Applicant cannot be edited or version changed.");
        row.FullName=ApplicantRules.Text(input.FullName,2,200);row.Statement=ApplicantRules.Text(input.Statement,10,1500);row.Version++;History(row,null);
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Map(row);
    }
    public Task<ApplicantView> Review(ClaimsPrincipal principal,Guid inst,Guid id,ApplicantDecision input,CancellationToken ct)
        =>retry.ExecuteAsync<ApplicantService,ApplicantView>(s=>s.ReviewCore(principal,inst,id,input,ct),ct);
    private async Task<ApplicantView> ReviewCore(ClaimsPrincipal principal,Guid inst,Guid id,ApplicantDecision input,CancellationToken ct)
    {
        if(input is null)throw ApiFault.Bad("Decision required.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);await RegistrationCoordination.Lock(db,inst,ct);
        var actor=await access.RequireAdminAsync(principal,inst,ct);
        var row=(await db.Set<Applicant>().FromSqlInterpolated($"SELECT * FROM registrar_applicants WHERE \"Id\"={id} AND \"InstitutionId\"={inst} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()??throw ApiFault.NotFound();
        var normalizedReason=ApplicantRules.Text(input.Reason,5,500);
        if(row.ReviewedBy==actor.Id&&row.DecisionReason==normalizedReason&&((input.Accept&&row.Status is "Accepted" or "Converted")||(!input.Accept&&row.Status=="Rejected")))return Map(row);
        if(row.Status!="Submitted"||row.VerifiedAtUtc is null||row.Version!=input.ExpectedVersion)throw ApiFault.Conflict("Verified submitted applicant required.");
        if(actor.Id==row.CreatedBy)throw ApiFault.Forbidden();
        row.Status=input.Accept?"Accepted":"Rejected";row.DecisionReason=ApplicantRules.Text(input.Reason,5,500);row.ReviewedBy=actor.Id;row.Version++;History(row,actor.Id);
        OutboxService.Record(db,inst,"ApplicantDecided",row.Id,new{ApplicantId=row.Id,Accepted=input.Accept});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Map(row);
    }
    public Task<ApplicantView> Activate(ApplicantActivation input,CancellationToken ct)=>retry.ExecuteAsync<ApplicantService,ApplicantView>(s=>s.ActivateCore(input,ct),ct);
    private async Task<ApplicantView> ActivateCore(ApplicantActivation input,CancellationToken ct)
    {
        if(input is null)throw ApiFault.Bad("Activation required.");
        // Resolve tenant without holding applicant lock, then use the global lock order.
        var hash=IdentitySecurity.TokenHash(input.Token??"");
        var inst=await db.Set<Applicant>().AsNoTracking().Where(x=>x.InvitationHash==hash).Select(x=>x.InstitutionId).SingleOrDefaultAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(ct);await RegistrationCoordination.Lock(db,inst,ct);var row=await Token(input.Token,ct);
        ApplicantRules.CanActivate(row.Status);if(row.Status=="Converted")return Map(row);
        row.VerifiedAtUtc??=DateTime.UtcNow; // possession of the activation invitation proves this controlled capability
        if(!await db.AcademicPrograms.AnyAsync(x=>x.Id==row.ProgramId&&x.InstitutionId==inst&&x.IsActive,ct)||!await db.AcademicTerms.AnyAsync(x=>x.Id==row.TermId&&x.InstitutionId==inst&&x.Status!="Closed",ct))throw ApiFault.Conflict("Academic admission scope changed.");
        if(await users.FindByEmailAsync(row.Email) is not null)throw ApiFault.Conflict("Email is already provisioned; administrative reconciliation required.");
        IdentityInputPolicy.Provision(new(inst,row.Email,input.Password,AcademixRoles.Student));
        var student=new ApplicationUser{Id=Guid.NewGuid(),InstitutionId=inst,Email=row.Email,UserName=row.Email,EmailConfirmed=true};
        var created=await users.CreateAsync(student,input.Password);if(!created.Succeeded)throw ApiFault.Bad(string.Join("; ",created.Errors.Select(x=>x.Description)));
        if(!(await users.AddToRoleAsync(student,AcademixRoles.Student)).Succeeded)throw ApiFault.Conflict("Student role assignment failed.");
        var admission=new AdmissionApplication{InstitutionId=inst,StudentId=student.Id,ProgramId=row.ProgramId,TermId=row.TermId,Statement=row.Statement,Status="Approved",ReviewedById=row.ReviewedBy,ReviewedAtUtc=DateTime.UtcNow,DecisionReason=row.DecisionReason};
        db.AdmissionApplications.Add(admission);db.StudentProgramMemberships.Add(new(){InstitutionId=inst,StudentId=student.Id,ProgramId=row.ProgramId,AdmissionTermId=row.TermId,CurrentTermId=row.TermId});
        db.Set<StudentProfile>().Add(new(){StudentId=student.Id,InstitutionId=inst,FullName=row.FullName});
        row.StudentId=student.Id;row.AdmissionId=admission.Id;row.Status="Converted";row.Version++;History(row,student.Id);
        OutboxService.Record(db,inst,"ApplicantConverted",row.Id,new{ApplicantId=row.Id,StudentId=student.Id});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Map(row);
    }
    public async Task<IReadOnlyList<ApplicantView>> List(ClaimsPrincipal principal,Guid inst,int page,CancellationToken ct)
    {
        await access.RequireAdminAsync(principal,inst,ct);if(page is <1 or >100000)throw ApiFault.Bad("Invalid page.");
        return (await db.Set<Applicant>().AsNoTracking().Where(x=>x.InstitutionId==inst).OrderBy(x=>x.Id).Skip((page-1)*50).Take(50).ToListAsync(ct)).Select(Map).ToArray();
    }
    public async Task<IReadOnlyList<ApplicantStatusView>> History(ClaimsPrincipal principal,Guid inst,Guid id,CancellationToken ct)
    {
        await access.RequireAdminAsync(principal,inst,ct);if(!await db.Set<Applicant>().AnyAsync(x=>x.Id==id&&x.InstitutionId==inst,ct))throw ApiFault.NotFound();
        return await db.Set<ApplicantHistory>().AsNoTracking().Where(x=>x.InstitutionId==inst&&x.ApplicantId==id).OrderBy(x=>x.AtUtc).ThenBy(x=>x.Id).Select(x=>new ApplicantStatusView(x.Status,x.AtUtc)).ToArrayAsync(ct);
    }
}
