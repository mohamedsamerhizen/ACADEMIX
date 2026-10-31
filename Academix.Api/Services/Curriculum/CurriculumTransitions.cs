using System.Security.Claims;
using System.Text.Json;
using Academix.Api.Domain.Registrar;
using Academix.Api.DTOs.Registrar;
using Academix.Api.Middleware;
using Academix.Api.Services.Academic;
using Academix.Api.Services.Infrastructure;
using Academix.Api.Services.Registrar;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Curriculum;

public static class CurriculumTransitionRules
{
    public static void ValidateMappings(IReadOnlyList<CourseRecognition>? mappings)
    {
        if(mappings is null||mappings.Count>500||mappings.Any(x=>x.SourceCourseId==Guid.Empty||x.TargetCourseId==Guid.Empty||x.SourceCourseId==x.TargetCourseId)||
            mappings.Select(x=>x.SourceCourseId).Distinct().Count()!=mappings.Count||mappings.Select(x=>x.TargetCourseId).Distinct().Count()!=mappings.Count)
            throw ApiFault.Bad("Each recognized achievement must map once to a distinct different target course.");
    }
    public static void CanApply(string state,DateTime effective,DateTime now)
    {if(state!="Approved"||effective>now)throw ApiFault.Conflict("An approved due transition is required.");}
}
public sealed partial class CurriculumService
{
    private static TransitionView TransitionView(CurriculumTransition x)=>new(x.Id,x.StudentId,x.FromPlanId,x.ToPlanId,x.EffectiveAtUtc,x.Status,x.Reason);
    public Task<TransitionView> RequestTransition(ClaimsPrincipal user,Guid inst,Guid student,TransitionInput input,CancellationToken ct)
        =>retry.ExecuteAsync<CurriculumService,TransitionView>(s=>s.RequestTransitionCore(user,inst,student,input,ct),ct);
    private async Task<TransitionView> RequestTransitionCore(ClaimsPrincipal user,Guid inst,Guid student,TransitionInput input,CancellationToken ct)
    {
        if(input is null||input.ClientRequestId==Guid.Empty||input.FromPlanId==input.ToPlanId||input.EffectiveAtUtc.Kind!=DateTimeKind.Utc)throw ApiFault.Bad("Valid transition IDs and UTC effective date required.");
        CurriculumTransitionRules.ValidateMappings(input.Equivalencies);var reason=ApplicantRules.Text(input.Reason,5,500);
        await using var tx=await db.Database.BeginTransactionAsync(ct);await RegistrationCoordination.Lock(db,inst,ct);var actor=await security.RequireAdminAsync(user,inst,ct);
        var prior=await db.Set<CurriculumTransition>().SingleOrDefaultAsync(x=>x.InstitutionId==inst&&x.StudentId==student&&x.ClientRequestId==input.ClientRequestId,ct);
        var json=JsonSerializer.Serialize(input.Equivalencies.OrderBy(x=>x.SourceCourseId));
        if(prior is not null)
        {if(prior.FromPlanId!=input.FromPlanId||prior.ToPlanId!=input.ToPlanId||!JsonSerializer.Deserialize<CourseRecognition[]>(prior.MappingJson)!.SequenceEqual(input.Equivalencies.OrderBy(x=>x.SourceCourseId))||prior.EffectiveAtUtc.Ticks / 10 != input.EffectiveAtUtc.Ticks / 10||prior.Reason!=reason)throw ApiFault.Conflict("Transition idempotency key reused.");return TransitionView(prior);}
        if(!await db.Users.AnyAsync(x=>x.Id==student&&x.InstitutionId==inst&&x.IsActive,ct)||!await db.StudentCurriculumAssignments.AnyAsync(x=>x.StudentId==student&&x.InstitutionId==inst&&x.PlanId==input.FromPlanId,ct))throw ApiFault.NotFound();
        if(await db.Set<CurriculumTransition>().AnyAsync(x=>x.StudentId==student&&x.InstitutionId==inst&&(x.Status=="Pending"||x.Status=="Approved"),ct))throw ApiFault.Conflict("A transition is already pending.");
        var row=new CurriculumTransition{InstitutionId=inst,StudentId=student,ClientRequestId=input.ClientRequestId,FromPlanId=input.FromPlanId,ToPlanId=input.ToPlanId,
            MappingJson=json,EffectiveAtUtc=input.EffectiveAtUtc,Reason=reason,RequestedBy=actor.Id};
        await ValidateTransition(row,ct);db.Set<CurriculumTransition>().Add(row);security.Audit(actor.Id,inst,"curriculum.transition.requested",row.Id.ToString());
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return TransitionView(row);
    }
    private async Task<List<(Guid Source,Guid Target,decimal Credits)>> ValidateTransition(CurriculumTransition row,CancellationToken ct)
    {
        await AcademicFreshness.Ensure(db,row.InstitutionId,row.StudentId,ct);
        var current=await db.StudentCurriculumAssignments.AsNoTracking().SingleOrDefaultAsync(x=>x.StudentId==row.StudentId&&x.InstitutionId==row.InstitutionId,ct);
        if(current?.PlanId!=row.FromPlanId)throw ApiFault.Conflict("Current curriculum changed.");
        var old=await Plan(row.FromPlanId,ct);var target=await Plan(row.ToPlanId,ct);
        if(old.InstitutionId!=row.InstitutionId||target.InstitutionId!=row.InstitutionId||target.Status!="Published"||
            !await db.AcademicPrograms.AnyAsync(x=>x.Id==target.ProgramId&&x.InstitutionId==row.InstitutionId&&x.IsActive,ct))throw ApiFault.Bad("Target must be an active institution program's published curriculum.");
        if(target.ProgramId==old.ProgramId&&(target.CatalogYear<old.CatalogYear||target.CatalogYear==old.CatalogYear&&target.Revision<=old.Revision))throw ApiFault.Conflict("A curriculum revision transition must move forward.");
        if(await db.Set<CurriculumTransition>().AnyAsync(x=>x.InstitutionId==row.InstitutionId&&x.StudentId==row.StudentId&&x.Status=="Applied"&&(x.FromPlanId==target.Id||x.ToPlanId==target.Id),ct))throw ApiFault.Conflict("A historical assignment cannot be reused; publish an eligible new revision.");
        if(!await db.StudentProgramMemberships.AnyAsync(x=>x.InstitutionId==row.InstitutionId&&x.StudentId==row.StudentId&&x.ProgramId==old.ProgramId&&x.Status=="Active",ct)||
            await db.GraduationApplications.AnyAsync(x=>x.StudentId==row.StudentId&&x.InstitutionId==row.InstitutionId&&(x.Status=="Approved"||x.Status=="Issued"),ct))throw ApiFault.Conflict("Resolve graduation/program status before transitioning.");
        if(await db.StudentEnrollments.AnyAsync(x=>x.StudentId==row.StudentId&&x.InstitutionId==row.InstitutionId&&x.Status=="Active",ct))throw ApiFault.Conflict("Resolve active section enrollments before transitioning.");
        var oldCourses=await db.CurriculumCourses.AsNoTracking().Where(x=>x.PlanId==old.Id).ToDictionaryAsync(x=>x.CourseId,x=>x.CreditHours,ct);
        var newCourses=await db.CurriculumCourses.AsNoTracking().Where(x=>x.PlanId==target.Id).ToDictionaryAsync(x=>x.CourseId,x=>x.CreditHours,ct);
        var passed=await RecognitionQualification.LocalSources(db,row.InstitutionId,row.StudentId,ct);
        var transferred=await (from l in db.TransferCreditLines.AsNoTracking() join p in db.TransferCreditPetitions.AsNoTracking() on l.PetitionId equals p.Id
            where p.InstitutionId==row.InstitutionId&&p.StudentId==row.StudentId&&p.ProgramId==old.ProgramId&&p.Status=="Approved" select l.TargetCourseId).ToListAsync(ct);
        var graded=passed.ToHashSet();
        passed.UnionWith(transferred);
        var recognized=(await RecognitionQualification.Load(db,row.InstitutionId,row.StudentId,old.Id,ct))
            .ToDictionary(x=>x.Credit.TargetCourseId,x=>x.Credit);
        passed.UnionWith(recognized.Keys);
        var mappings=JsonSerializer.Deserialize<CourseRecognition[]>(row.MappingJson)!;CurriculumTransitionRules.ValidateMappings(mappings);
        var results=new List<(Guid,Guid,decimal)>();
        foreach(var mapping in mappings)
        {
            if(!passed.Contains(mapping.SourceCourseId)||!oldCourses.TryGetValue(mapping.SourceCourseId,out var credits)||!newCourses.TryGetValue(mapping.TargetCourseId,out var targetCredits)||
                credits<targetCredits||passed.Contains(mapping.TargetCourseId)||newCourses.ContainsKey(mapping.SourceCourseId))throw ApiFault.Conflict("Mapping is incomplete, duplicates credit or lacks a completed source/target credit equivalence.");
            results.Add((recognized.TryGetValue(mapping.SourceCourseId,out var original)?original.SourceCourseId:mapping.SourceCourseId,mapping.TargetCourseId,targetCredits));
        }
        foreach(var completed in oldCourses.Keys.Where(passed.Contains))
        {
            if(newCourses.TryGetValue(completed,out var credits)&&credits<=oldCourses[completed])
            {if(recognized.TryGetValue(completed,out var prior))results.Add((prior.SourceCourseId,completed,credits));else if(!graded.Contains(completed)&&transferred.Contains(completed))results.Add((completed,completed,credits));continue;}
            if(!mappings.Any(x=>x.SourceCourseId==completed))throw ApiFault.Conflict("Every completed course leaving the curriculum requires an explicit equivalency mapping.");
        }
        if(results.Select(x=>x.Item1).Distinct().Count()!=results.Count)throw ApiFault.Conflict("A source achievement cannot be counted twice.");
        return results;
    }
    public Task<TransitionView> ReviewTransition(ClaimsPrincipal user,Guid inst,Guid id,bool approve,CancellationToken ct)
        =>retry.ExecuteAsync<CurriculumService,TransitionView>(s=>s.ReviewTransitionCore(user,inst,id,approve,ct),ct);
    private async Task<TransitionView> ReviewTransitionCore(ClaimsPrincipal user,Guid inst,Guid id,bool approve,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);await RegistrationCoordination.Lock(db,inst,ct);var actor=await security.RequireAdminAsync(user,inst,ct);
        var row=await db.Set<CurriculumTransition>().SingleOrDefaultAsync(x=>x.Id==id&&x.InstitutionId==inst,ct)??throw ApiFault.NotFound();
        if(row.RequestedBy==actor.Id||row.StudentId==actor.Id)throw ApiFault.Forbidden();if(row.Status!="Pending")throw ApiFault.Conflict("Transition already reviewed.");
        if(approve)await ValidateTransition(row,ct);
        row.Status=approve?"Approved":"Rejected";row.ApprovedBy=actor.Id;security.Audit(actor.Id,inst,"curriculum.transition.reviewed",id.ToString());
        if(approve){var item=OutboxService.Record(db,inst,"CurriculumTransitionApproved",row.Id,new{TransitionId=row.Id});item.NextAttemptAtUtc=row.EffectiveAtUtc;}
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return TransitionView(row);
    }
    public Task<TransitionView> ApplyTransition(ClaimsPrincipal user,Guid inst,Guid id,CancellationToken ct)
        =>retry.ExecuteAsync<CurriculumService,TransitionView>(s=>s.ApplyTransitionCore(user,inst,id,ct),ct);
    internal Task<TransitionView> ApplyApprovedTransition(Guid inst,Guid id,CancellationToken ct)
        =>retry.ExecuteAsync<CurriculumService,TransitionView>(s=>s.ApplyTransitionCore(null,inst,id,ct),ct);
    private async Task<TransitionView> ApplyTransitionCore(ClaimsPrincipal? user,Guid inst,Guid id,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);await RegistrationCoordination.Lock(db,inst,ct);
        if(user is not null)await security.RequireAdminAsync(user,inst,ct);
        var row=await db.Set<CurriculumTransition>().SingleOrDefaultAsync(x=>x.Id==id&&x.InstitutionId==inst,ct)??throw ApiFault.NotFound();
        if(row.Status=="Applied")return TransitionView(row);CurriculumTransitionRules.CanApply(row.Status,row.EffectiveAtUtc,DateTime.UtcNow);
        if(row.ApprovedBy is not Guid actor||!await RegistrarAuthority.IsAdmin(db,actor,inst,ct))throw ApiFault.Forbidden();
        var credits=await ValidateTransition(row,ct);var target=await Plan(row.ToPlanId,ct);
        var assignment=await db.StudentCurriculumAssignments.SingleAsync(x=>x.StudentId==row.StudentId&&x.InstitutionId==inst,ct);
        var membership=await db.StudentProgramMemberships.SingleAsync(x=>x.StudentId==row.StudentId&&x.InstitutionId==inst,ct);
        row.PriorAssignmentJson=JsonSerializer.Serialize(new{Assignment=assignment,Membership=membership});
        assignment.PlanId=target.Id;assignment.AssignedAtUtc=DateTime.UtcNow;assignment.AssignedById=actor;membership.ProgramId=target.ProgramId;membership.Version++;
        foreach(var c in credits)db.Set<RecognizedCourseCredit>().Add(new(){InstitutionId=inst,StudentId=row.StudentId,TransitionId=id,SourceCourseId=c.Source,TargetCourseId=c.Target,PlanId=target.Id,CreditHours=c.Credits});
        row.Status="Applied";row.AppliedAtUtc=DateTime.UtcNow;
        await AcademicFreshness.Invalidate(db,inst,row.StudentId,true,ct);
        security.Audit(actor,inst,"curriculum.transition.applied",id.ToString());OutboxService.Record(db,inst,"CurriculumTransitionApplied",id,new{TransitionId=id,StudentId=row.StudentId});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return TransitionView(row);
    }
    public async Task<IReadOnlyList<TransitionView>> Transitions(ClaimsPrincipal user,Guid inst,Guid student,CancellationToken ct)
    {
        await security.RequireStudentAsync(user,student,inst,ct);
        return (await db.Set<CurriculumTransition>().AsNoTracking().Where(x=>x.InstitutionId==inst&&x.StudentId==student).OrderBy(x=>x.EffectiveAtUtc).ThenBy(x=>x.Id).ToListAsync(ct)).Select(TransitionView).ToArray();
    }
}
