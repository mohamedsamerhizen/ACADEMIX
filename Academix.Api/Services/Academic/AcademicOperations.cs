using Academix.Api.Services.Infrastructure;
using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Academic;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Academix.Api.Services.Records;
using Academix.Api.DTOs.Records;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Academic;

public sealed class AcademicOperations(AcademixDbContext db, IdentitySecurity access, UserManager<ApplicationUser> users, RecordsService records, RetryingServiceWork retry)
{
    private async Task Admin(ClaimsPrincipal user, Guid inst, CancellationToken ct)
    {
        await access.RequireAdminAsync(user, inst, ct);
        if (!await db.Institutions.AnyAsync(i => i.Id == inst && i.IsActive, ct)) throw ApiFault.Bad("Institution inactive.");
    }
    private static string CleanCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 30) throw ApiFault.Bad("Invalid code.");
        return code.Trim().ToUpperInvariant();
    }
    public async Task<object> CreateCourse(ClaimsPrincipal user, CourseCreate req, CancellationToken ct)
    {
        await Admin(user, req.InstitutionId, ct);
        Course x;
        try { x = Course.Create(Guid.NewGuid(), req.InstitutionId, req.Code, req.Title); }
        catch (ArgumentException ex) { throw ApiFault.Bad(ex.Message); }
        db.Courses.Add(x); await db.SaveChangesAsync(ct);
        return new { x.Id, x.InstitutionId, x.Code, x.Title, x.Version };
    }
    public async Task<object> ListCourses(ClaimsPrincipal user, Guid inst, int page, CancellationToken ct)
    {
        await Admin(user, inst, ct);
        return await db.Courses.AsNoTracking().Where(x => x.InstitutionId == inst).OrderBy(x => x.Code)
            .Skip((Math.Clamp(page, 1, 100000) - 1) * 50).Take(50)
            .Select(x => new { x.Id, x.InstitutionId, x.Code, x.Title, x.IsActive, x.Version }).ToListAsync(ct);
    }
    public async Task<object> GetSection(ClaimsPrincipal user, Guid id, CancellationToken ct)
    {
        var x = await db.CourseSections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiFault.NotFound();
        await access.RequireSectionStaffAsync(user, x.InstitutionId, id, ct);
        return x;
    }
    public async Task<object> UpdateCourse(ClaimsPrincipal user, Guid id, CourseUpdate req, CancellationToken ct)
    {
        var x = await db.Courses.SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw ApiFault.NotFound();
        await Admin(user, x.InstitutionId, ct);
        if (!req.IsActive && await db.CourseSections.AnyAsync(s => s.CourseId == id && s.Status == "Open", ct)) throw ApiFault.Conflict("Close open sections first.");
        try { x.Change(req.Title, req.IsActive, req.ExpectedVersion); }
        catch (InvalidOperationException) { throw ApiFault.Conflict("Stale course version."); }
        catch (ArgumentException ex) { throw ApiFault.Bad(ex.Message); }
        await db.SaveChangesAsync(ct); return new { x.Id, x.Code, x.Title, x.IsActive, x.Version };
    }
    public async Task<object> GetCourse(ClaimsPrincipal user, Guid id, CancellationToken ct)
    {
        var x = await db.Courses.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw ApiFault.NotFound();
        await Admin(user, x.InstitutionId, ct);
        return new { x.Id, x.InstitutionId, x.Code, x.Title, x.IsActive, x.Version };
    }
    public async Task AssignCourse(ClaimsPrincipal user, Guid programId, Guid courseId, CancellationToken ct)
    {
        var program = await db.AcademicPrograms.FindAsync([programId], ct) ?? throw ApiFault.NotFound();
        await Admin(user, program.InstitutionId, ct);
        var course = await db.Courses.SingleOrDefaultAsync(c => c.Id == courseId && c.InstitutionId == program.InstitutionId && c.IsActive, ct);
        if (course is null || !program.IsActive) throw ApiFault.Bad("Invalid program-course relation.");
        if (await db.ProgramCourses.AnyAsync(x => x.ProgramId == programId && x.CourseId == courseId, ct)) throw ApiFault.Conflict("Course already associated.");
        db.ProgramCourses.Add(new ProgramCourse { InstitutionId = program.InstitutionId, ProgramId = programId, CourseId = courseId });
        await db.SaveChangesAsync(ct);
    }
    public async Task UnassignCourse(ClaimsPrincipal user, Guid programId, Guid courseId, CancellationToken ct)
    {
        var program = await db.AcademicPrograms.FindAsync([programId], ct) ?? throw ApiFault.NotFound();
        await Admin(user, program.InstitutionId, ct);
        var relation = await db.ProgramCourses.SingleOrDefaultAsync(x => x.ProgramId == programId && x.CourseId == courseId, ct) ?? throw ApiFault.NotFound();
        db.ProgramCourses.Remove(relation); await db.SaveChangesAsync(ct);
    }
    public async Task<object> ListProgramCourses(ClaimsPrincipal user, Guid programId, CancellationToken ct)
    {
        var program = await db.AcademicPrograms.FindAsync([programId], ct) ?? throw ApiFault.NotFound();
        await Admin(user, program.InstitutionId, ct);
        return await (from rel in db.ProgramCourses.AsNoTracking() join course in db.Courses on rel.CourseId equals course.Id where rel.ProgramId == programId select new { course.Id, course.Code, course.Title }).ToListAsync(ct);
    }
    public async Task<object> CreateSection(ClaimsPrincipal user, SectionCreate req, CancellationToken ct)
    {
        await Admin(user, req.InstitutionId, ct);
        if (req.Capacity < 1 || req.Capacity > 10000) throw ApiFault.Bad("Capacity must be 1-10000.");
        if (!await db.Courses.AnyAsync(c => c.Id == req.CourseId && c.InstitutionId == req.InstitutionId && c.IsActive, ct) ||
            !await db.AcademicTerms.AnyAsync(t => t.Id == req.TermId && t.InstitutionId == req.InstitutionId && t.Status != "Closed", ct))
            throw ApiFault.Bad("Course and term must be active and belong to this institution.");
        var x = new CourseSection { InstitutionId = req.InstitutionId, CourseId = req.CourseId, TermId = req.TermId, Code = CleanCode(req.Code), Capacity = req.Capacity };
        db.CourseSections.Add(x); await db.SaveChangesAsync(ct); return x;
    }
    public async Task<object> ListSections(ClaimsPrincipal user, Guid inst, int page, CancellationToken ct)
    {
        await Admin(user, inst, ct);
        return await db.CourseSections.AsNoTracking().Where(x => x.InstitutionId == inst).OrderBy(x => x.Code)
            .Skip((Math.Clamp(page, 1, 100000) - 1) * 50).Take(50).ToListAsync(ct);
    }
    public Task<object> UpdateSection(ClaimsPrincipal user, Guid id, SectionUpdate req, CancellationToken ct)
        => retry.ExecuteAsync<AcademicOperations, object>(service => service.UpdateSectionCore(user, id, req, ct), ct);

    private async Task<object> UpdateSectionCore(ClaimsPrincipal user, Guid id, SectionUpdate req, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var institutionId = await db.CourseSections.AsNoTracking().Where(x => x.Id == id)
                .Select(x => x.InstitutionId).SingleOrDefaultAsync(ct);
            await RegistrationCoordination.Lock(db, institutionId, ct);
            // The enrollment path takes the same section-row lock. Read the current
            // capacity and active enrollment count only AFTER acquiring this lock.
            var x = (await db.CourseSections
                .FromSqlInterpolated($"SELECT * FROM course_sections WHERE \"Id\" = {id} FOR UPDATE")
                .ToListAsync(ct)).SingleOrDefault() ?? throw ApiFault.NotFound();
            await Admin(user, x.InstitutionId, ct);
            if (x.Version != req.ExpectedVersion) throw ApiFault.Conflict("Stale section version.");
            if (req.Capacity < 1 || req.Capacity > 10000) throw ApiFault.Bad("Capacity must be 1-10000.");
            if (req.Capacity < await db.StudentEnrollments.CountAsync(e => e.SectionId == id && e.Status == "Active", ct))
                throw ApiFault.Conflict("Capacity below active enrollment count.");
            if (!(x.Status == req.Status || x.Status == "Draft" && req.Status == "Open" || x.Status == "Open" && (req.Status == "Closed" || req.Status == "Cancelled")))
                throw ApiFault.Bad("Invalid section transition.");
            if (req.Status == "Open")
            {
                if (!await db.TeachingAssignments.AnyAsync(t => t.SectionId == id && t.Kind == "Instructor", ct)) throw ApiFault.Conflict("Primary instructor required.");
                if (!await db.AcademicTerms.AnyAsync(t => t.Id == x.TermId && t.Status == "Active", ct)) throw ApiFault.Conflict("Academic term must be active.");
                if (!await db.Courses.AnyAsync(c => c.Id == x.CourseId && c.IsActive, ct)) throw ApiFault.Conflict("Course inactive.");
            }
            await Academix.Api.Services.AcademicOperations.CampusIntegrity.SectionCapacity(db,x.InstitutionId,id,req.Capacity,ct);
            x.Capacity = req.Capacity; x.Status = req.Status; x.Version++;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return (object)x;
        });
    }
    public Task<object> AssignStaff(ClaimsPrincipal user, Guid sectionId, TeachingCreate req, CancellationToken ct)
        => retry.ExecuteAsync<AcademicOperations,object>(s=>s.AssignStaffCore(user,sectionId,req,ct),ct);
    private async Task<object> AssignStaffCore(ClaimsPrincipal user, Guid sectionId, TeachingCreate req, CancellationToken ct)
    {
        var section = await db.CourseSections.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==sectionId,ct) ?? throw ApiFault.NotFound();
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        await RegistrationCoordination.Lock(db,section.InstitutionId,ct);
        await Admin(user, section.InstitutionId, ct);
        if (req.Kind is not (AcademixRoles.Instructor or AcademixRoles.TeachingAssistant)) throw ApiFault.Bad("Only Instructor or TeachingAssistant allowed.");
        var staff = await users.FindByIdAsync(req.UserId.ToString());
        if (staff is null || !staff.IsActive || staff.InstitutionId != section.InstitutionId || !await users.IsInRoleAsync(staff, req.Kind)) throw ApiFault.Bad("Invalid teaching staff.");
        if (req.Kind == AcademixRoles.Instructor && await db.TeachingAssignments.AnyAsync(x => x.SectionId == sectionId && x.Kind == req.Kind, ct)) throw ApiFault.Conflict("Primary instructor already assigned.");
        if (await db.TeachingAssignments.AnyAsync(x => x.SectionId == sectionId && x.UserId == req.UserId, ct)) throw ApiFault.Conflict("Already assigned.");
        if(req.Kind==AcademixRoles.Instructor)await Academix.Api.Services.AcademicOperations.CampusIntegrity.Instructor(db,section.InstitutionId,sectionId,req.UserId,ct);
        var relation = new TeachingAssignment { InstitutionId = section.InstitutionId, SectionId = sectionId, UserId = req.UserId, Kind = req.Kind };
        db.TeachingAssignments.Add(relation); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return relation;
    }
    public async Task RemoveStaff(ClaimsPrincipal user, Guid sectionId, Guid staffId, CancellationToken ct)
    {
        var section = await db.CourseSections.FindAsync([sectionId], ct) ?? throw ApiFault.NotFound();
        await Admin(user, section.InstitutionId, ct);
        var relation = await db.TeachingAssignments.SingleOrDefaultAsync(x => x.SectionId == sectionId && x.UserId == staffId, ct) ?? throw ApiFault.NotFound();
        if (section.Status == "Open" && relation.Kind == AcademixRoles.Instructor) throw ApiFault.Conflict("Cannot remove primary instructor from open section.");
        db.TeachingAssignments.Remove(relation); await db.SaveChangesAsync(ct);
    }
    public async Task<object> ListStaff(ClaimsPrincipal user, Guid sectionId, CancellationToken ct)
    {
        var section = await db.CourseSections.FindAsync([sectionId], ct) ?? throw ApiFault.NotFound();
        await access.RequireSectionStaffAsync(user, section.InstitutionId, sectionId, ct);
        return await db.TeachingAssignments.AsNoTracking().Where(x => x.SectionId == sectionId).Select(x => new { x.UserId, x.Kind }).ToListAsync(ct);
    }

    public Task<object> Enroll(ClaimsPrincipal principal, Guid sectionId, EnrollmentCreate req, CancellationToken ct)
        => retry.ExecuteAsync<AcademicOperations, object>(service => service.EnrollCore(principal, sectionId, req, ct), ct);

    private async Task<object> EnrollCore(ClaimsPrincipal principal, Guid sectionId, EnrollmentCreate req, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var institutionId = await db.CourseSections.AsNoTracking().Where(x => x.Id == sectionId)
                .Select(x => x.InstitutionId).SingleOrDefaultAsync(ct);
            await RegistrationCoordination.Lock(db, institutionId, ct);
            // Lock the section row before evaluating capacity and enrollment history.
            var section = (await db.CourseSections.FromSqlInterpolated($"SELECT * FROM course_sections WHERE \"Id\" = {sectionId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault() ?? throw ApiFault.NotFound();
            await Admin(principal, section.InstitutionId, ct);
            if (!await db.AcademicTerms.AsNoTracking().AnyAsync(t => t.Id == section.TermId &&
                t.InstitutionId == section.InstitutionId && t.Status == "Active", ct))
                throw ApiFault.Conflict("Academic term is not active.");
            if (section.Status != "Open") throw ApiFault.Conflict("Section not open.");
            if (await db.SectionGradePolicies.AnyAsync(x => x.SectionId == sectionId && x.PublishedAtUtc != null, ct))
                throw ApiFault.Conflict("Enrollment is closed after final gradebook publication.");
            var student = await users.FindByIdAsync(req.StudentId.ToString());
            if (student is null || !student.IsActive || student.InstitutionId != section.InstitutionId || !await users.IsInRoleAsync(student, AcademixRoles.Student)) throw ApiFault.Bad("Invalid student.");
            var eligibility=await records.EligibilityForSection(sectionId,req.StudentId,ct,true);
            if(!eligibility.Eligible)throw ApiFault.Conflict("Enrollment rejected: "+string.Join(", ",eligibility.RejectionReasons??[]));
            var existing = await db.StudentEnrollments.SingleOrDefaultAsync(x => x.SectionId == sectionId && x.StudentId == req.StudentId, ct);
            if (existing?.Status == "Active") throw ApiFault.Conflict("Student already enrolled.");
            var count = await db.StudentEnrollments.CountAsync(x => x.SectionId == sectionId && x.Status == "Active", ct);
            if (count >= section.Capacity) throw ApiFault.Conflict("Section is full.");
            await Academix.Api.Services.AcademicOperations.CampusIntegrity.Enrollment(db,section.InstitutionId,sectionId,[req.StudentId],ct);
            if (existing is null)
            {
                existing = new StudentEnrollment { SectionId = sectionId, InstitutionId = section.InstitutionId, StudentId = req.StudentId };
                db.StudentEnrollments.Add(existing);
            }
            else { existing.Status = "Active"; existing.EnrolledAtUtc = DateTime.UtcNow; }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return (object)existing;
        });
    }
    public async Task Withdraw(ClaimsPrincipal user, Guid sectionId, Guid studentId, CancellationToken ct)
    {
        await records.ChangeRegistration(user, sectionId, studentId,
            new RegistrationChange("Withdraw", "Student withdrawal request"), ct);
    }
    private static int PageOffset(int page)
    {
        if (page is < 1 or > 100000) throw ApiFault.Bad("Page must be 1-100000.");
        return (page - 1) * 100;
    }
    public async Task<object> Roster(ClaimsPrincipal user, Guid sectionId, CancellationToken ct, int page = 1)
    {
        var section = await db.CourseSections.FindAsync([sectionId], ct) ?? throw ApiFault.NotFound();
        await access.RequireSectionStaffAsync(user, section.InstitutionId, sectionId, ct);
        return await db.StudentEnrollments.AsNoTracking().Where(e => e.SectionId == sectionId)
            .OrderBy(e => e.EnrolledAtUtc).ThenBy(e => e.Id).Skip(PageOffset(page)).Select(e => new { e.Id, e.StudentId, e.Status, e.EnrolledAtUtc }).Take(100).ToListAsync(ct);
    }
    public async Task<object> StudentHistory(ClaimsPrincipal user, Guid studentId, CancellationToken ct, int page = 1)
    {
        var student = await users.FindByIdAsync(studentId.ToString()) ?? throw ApiFault.NotFound();
        if (student.InstitutionId is not Guid inst) throw ApiFault.NotFound();
        await access.RequireStudentAsync(user, studentId, inst, ct);
        return await db.StudentEnrollments.AsNoTracking().Where(e => e.StudentId == studentId && e.InstitutionId == inst).OrderByDescending(x => x.EnrolledAtUtc).ThenBy(x => x.Id).Skip(PageOffset(page))
            .Select(e => new { e.Id, e.SectionId, e.Status, e.EnrolledAtUtc }).Take(100).ToListAsync(ct);
    }
}
