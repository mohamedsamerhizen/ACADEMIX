using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Analytics;
using Academix.Api.DTOs.Workspaces;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Analytics;

/// <summary>Read-only, on-demand analytics over institution-scoped academic records.</summary>
public sealed class AnalyticsService(AcademixDbContext db, IdentitySecurity security)
{
    private static async Task RequireCurrent(IQueryable<Academix.Api.Domain.Assessment.GradebookResult> results,CancellationToken ct)
    {if(await results.AnyAsync(x=>x.RequiresReconciliation,ct))throw ApiFault.Conflict("Current-grade analytics require reconciliation of changed assessment evidence.");}
    private sealed record SectionScope(Guid InstitutionId, Guid TermId, string SectionCode,
        string CourseCode, string CourseTitle);

    private async Task InstitutionGuard(ClaimsPrincipal principal, Guid institutionId, Guid? termId, CancellationToken ct)
    {
        await security.RequireAdminAsync(principal, institutionId, ct);
        if (!await db.Institutions.AsNoTracking().AnyAsync(x => x.Id == institutionId, ct))
            throw ApiFault.NotFound();
        if (termId.HasValue && !await db.AcademicTerms.AsNoTracking()
            .AnyAsync(x => x.Id == termId.Value && x.InstitutionId == institutionId, ct))
            throw ApiFault.NotFound();
    }

    private async Task<SectionScope> SectionGuard(ClaimsPrincipal principal, Guid sectionId, CancellationToken ct)
    {
        var item = await (from s in db.CourseSections.AsNoTracking()
            join c in db.Courses.AsNoTracking() on s.CourseId equals c.Id
            where s.Id == sectionId && c.InstitutionId == s.InstitutionId
            select new SectionScope(s.InstitutionId, s.TermId, s.Code, c.Code, c.Title))
            .SingleOrDefaultAsync(ct) ?? throw ApiFault.NotFound();
        await security.RequireSectionStaffAsync(principal, item.InstitutionId, sectionId, ct);
        return item;
    }

    private IQueryable<Guid> InstitutionSectionIds(Guid institutionId, Guid? termId)
        => db.CourseSections.AsNoTracking().Where(x => x.InstitutionId == institutionId &&
               (!termId.HasValue || x.TermId == termId.Value)).Select(x => x.Id);

    private IQueryable<Guid> EnrolledStudents(Guid institutionId, Guid sectionId)
        => db.StudentEnrollments.AsNoTracking().Where(x => x.InstitutionId == institutionId &&
            x.SectionId == sectionId && x.Status == "Active").Select(x => x.StudentId);

    private IQueryable<Guid> PublishedLessonIds(Guid institutionId, Guid sectionId)
        => from lesson in db.LearningLessons.AsNoTracking()
           join unit in db.LearningUnits.AsNoTracking() on lesson.UnitId equals unit.Id
           where lesson.InstitutionId == institutionId && lesson.SectionId == sectionId &&
                 unit.InstitutionId == institutionId && unit.SectionId == sectionId &&
                 lesson.IsPublished && unit.IsPublished
           select lesson.Id;

    public async Task<InstitutionAnalytics> InstitutionOverview(ClaimsPrincipal principal,
        Guid institutionId, Guid? termId, CancellationToken ct)
    {
        await InstitutionGuard(principal, institutionId, termId, ct);
        var ids = InstitutionSectionIds(institutionId, termId);
        var grades = db.GradebookResults.AsNoTracking().Where(x => x.InstitutionId == institutionId &&
            ids.Contains(x.SectionId));
        await RequireCurrent(grades,ct);
        return new InstitutionAnalytics(institutionId, termId,
            await ids.CountAsync(ct),
            await db.StudentEnrollments.AsNoTracking().CountAsync(x => x.InstitutionId == institutionId &&
                ids.Contains(x.SectionId) && x.Status == "Active", ct),
            await db.ClassSessions.AsNoTracking().CountAsync(x => x.InstitutionId == institutionId &&
                ids.Contains(x.SectionId) && x.Status == "Completed", ct),
            await db.AttendanceRecords.AsNoTracking().CountAsync(x => x.InstitutionId == institutionId &&
                ids.Contains(x.SectionId), ct),
            await grades.CountAsync(ct),
            await grades.CountAsync(x => x.Passed, ct),
            await grades.AverageAsync(x => (decimal?)x.Percentage, ct),
            DateTime.UtcNow);
    }

    public async Task<PageResult<SectionAnalyticsLine>> InstitutionSections(ClaimsPrincipal principal,
        Guid institutionId, Guid? termId, int page, CancellationToken ct)
    {
        var offset = AnalyticsPolicy.Offset(page);
        await InstitutionGuard(principal, institutionId, termId, ct);
        var query = from s in db.CourseSections.AsNoTracking()
                    join c in db.Courses.AsNoTracking() on s.CourseId equals c.Id
                    where s.InstitutionId == institutionId && c.InstitutionId == institutionId &&
                          (!termId.HasValue || s.TermId == termId.Value)
                    select new { Section = s, Course = c };
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Course.Code).ThenBy(x => x.Section.Code)
            .ThenBy(x => x.Section.Id).Skip(offset).Take(AnalyticsPolicy.PageSize)
            .Select(x => new SectionAnalyticsLine(x.Section.Id, x.Section.TermId, x.Section.Code,
                x.Course.Code, x.Course.Title, x.Section.Status, x.Section.Capacity,
                db.StudentEnrollments.Count(e => e.InstitutionId == institutionId &&
                    e.SectionId == x.Section.Id && e.Status == "Active"),
                db.GradebookResults.Count(g => g.InstitutionId == institutionId &&
                    g.SectionId == x.Section.Id)))
            .ToListAsync(ct);
        return new PageResult<SectionAnalyticsLine>(page, AnalyticsPolicy.PageSize, total, rows);
    }

    private async Task<AttendanceAnalytics> Attendance(Guid institutionId,
        Guid? sectionId, Guid? termId, CancellationToken ct)
    {
        var ids = sectionId.HasValue
            ? db.CourseSections.AsNoTracking().Where(x => x.Id == sectionId.Value &&
                x.InstitutionId == institutionId).Select(x => x.Id)
            : InstitutionSectionIds(institutionId, termId);
        var marks = db.AttendanceRecords.AsNoTracking().Where(x => x.InstitutionId == institutionId &&
            ids.Contains(x.SectionId));
        var byStatus = await marks.GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        var present = byStatus.Where(x => x.Status == "Present").Sum(x => x.Count);
        var late = byStatus.Where(x => x.Status == "Late").Sum(x => x.Count);
        var absent = byStatus.Where(x => x.Status == "Absent").Sum(x => x.Count);
        var excused = byStatus.Where(x => x.Status == "Excused").Sum(x => x.Count);
        var recorded = byStatus.Sum(x => x.Count);
        return new AttendanceAnalytics(sectionId, institutionId, termId,
            await db.ClassSessions.AsNoTracking().CountAsync(x => x.InstitutionId == institutionId &&
                ids.Contains(x.SectionId) && x.Status == "Completed", ct),
            recorded, present, absent, late, excused,
            AnalyticsPolicy.Rate(present + late, recorded));
    }

    public async Task<AttendanceAnalytics> InstitutionAttendance(ClaimsPrincipal principal,
        Guid institutionId, Guid? termId, CancellationToken ct)
    {
        await InstitutionGuard(principal, institutionId, termId, ct);
        return await Attendance(institutionId, null, termId, ct);
    }

    private async Task<OutcomesAnalytics> Outcomes(Guid institutionId, Guid? sectionId, Guid? termId,
        CancellationToken ct)
    {
        var sectionIds = sectionId.HasValue
            ? db.CourseSections.AsNoTracking().Where(x => x.Id == sectionId.Value &&
                x.InstitutionId == institutionId).Select(x => x.Id)
            : InstitutionSectionIds(institutionId, termId);
        var results = db.GradebookResults.AsNoTracking().Where(x => x.InstitutionId == institutionId &&
            sectionIds.Contains(x.SectionId));
        await RequireCurrent(results,ct);
        var distribution = await results.GroupBy(x => x.Percentage < 50m ? 0 : x.Percentage < 60m ? 1 :
                x.Percentage < 70m ? 2 : x.Percentage < 85m ? 3 : 4)
            .Select(g => new { Band = g.Key, Count = g.Count() }).ToListAsync(ct);
        var names = new[] { "0-49.99", "50-59.99", "60-69.99", "70-84.99", "85-100" };
        var min = new decimal[] { 0m, 50m, 60m, 70m, 85m };
        var max = new decimal[] { 49.99m, 59.99m, 69.99m, 84.99m, 100m };
        var byIndex = distribution.ToDictionary(x => x.Band, x => x.Count);
        var groups = Enumerable.Range(0, 5)
            .Select(i => new OutcomeBand(names[i], min[i], max[i], byIndex.GetValueOrDefault(i))).ToArray();
        return new OutcomesAnalytics(sectionId, institutionId, termId,
            await results.CountAsync(ct),
            await results.CountAsync(x => x.Passed, ct),
            await results.CountAsync(x => !x.Passed, ct),
            await results.AverageAsync(x => (decimal?)x.Percentage, ct), groups);
    }

    public async Task<OutcomesAnalytics> InstitutionOutcomes(ClaimsPrincipal principal,
        Guid institutionId, Guid? termId, CancellationToken ct)
    {
        await InstitutionGuard(principal, institutionId, termId, ct);
        return await Outcomes(institutionId, null, termId, ct);
    }

    public async Task<string> ExportInstitutionSections(ClaimsPrincipal principal,
        Guid institutionId, Guid? termId, CancellationToken ct)
    {
        await InstitutionGuard(principal, institutionId, termId, ct);
        var ids = InstitutionSectionIds(institutionId, termId);
        var lines = new List<string> { AnalyticsPolicy.CsvRow("SectionId", "TermId", "CourseCode", "CourseTitle",
            "SectionCode", "Status", "Capacity", "ActiveEnrollments") };
        var rows = await (from s in db.CourseSections.AsNoTracking()
                          join c in db.Courses.AsNoTracking() on s.CourseId equals c.Id
                          where ids.Contains(s.Id) && c.InstitutionId == institutionId
                          orderby c.Code, s.Code, s.Id
                          select new { s.Id, s.TermId, CourseCode = c.Code, c.Title, SectionCode = s.Code, s.Status, s.Capacity,
                              Active = db.StudentEnrollments.Count(e => e.InstitutionId == institutionId &&
                                  e.SectionId == s.Id && e.Status == "Active") })
            .Take(AnalyticsPolicy.MaximumCsvRows + 1).ToListAsync(ct);
        if (rows.Count > AnalyticsPolicy.MaximumCsvRows)
            throw ApiFault.Bad("Export exceeds 2000 sections; narrow the term filter.");
        lines.AddRange(rows.Select(x => AnalyticsPolicy.CsvRow(x.Id, x.TermId, x.CourseCode, x.Title,
            x.SectionCode, x.Status, x.Capacity, x.Active)));
        return string.Join("\r\n", lines) + "\r\n";
    }

    public async Task<SectionAnalytics> SectionOverview(ClaimsPrincipal principal, Guid sectionId, CancellationToken ct)
    {
        var s = await SectionGuard(principal, sectionId, ct);
        var enrolled = EnrolledStudents(s.InstitutionId, sectionId);
        var lessons = PublishedLessonIds(s.InstitutionId, sectionId);
        return new SectionAnalytics(sectionId, s.InstitutionId, s.TermId,
            s.SectionCode, s.CourseCode, s.CourseTitle,
            await enrolled.CountAsync(ct), await lessons.CountAsync(ct),
            await db.ClassSessions.AsNoTracking().CountAsync(x => x.InstitutionId == s.InstitutionId &&
                x.SectionId == sectionId && x.Status == "Completed", ct),
            await db.LearningAssignments.AsNoTracking().CountAsync(x => x.InstitutionId == s.InstitutionId &&
                x.SectionId == sectionId && x.IsPublished, ct),
            await db.Exams.AsNoTracking().CountAsync(x => x.InstitutionId == s.InstitutionId &&
                x.SectionId == sectionId && x.Status == "Published", ct),
            await db.GradebookResults.AsNoTracking().CountAsync(x => x.InstitutionId == s.InstitutionId &&
                x.SectionId == sectionId, ct));
    }

    public async Task<AttendanceAnalytics> SectionAttendance(ClaimsPrincipal principal, Guid sectionId, CancellationToken ct)
    {
        var s = await SectionGuard(principal, sectionId, ct);
        return await Attendance(s.InstitutionId, sectionId, s.TermId, ct);
    }

    public async Task<EngagementAnalytics> SectionEngagement(ClaimsPrincipal principal, Guid sectionId, CancellationToken ct)
    {
        var s = await SectionGuard(principal, sectionId, ct);
        var students = EnrolledStudents(s.InstitutionId, sectionId);
        var lessons = PublishedLessonIds(s.InstitutionId, sectionId);
        var enrolledCount = await students.CountAsync(ct);
        var lessonCount = await lessons.CountAsync(ct);
        var completionCount = await db.LessonCompletions.AsNoTracking().CountAsync(x =>
            x.InstitutionId == s.InstitutionId && x.SectionId == sectionId &&
            students.Contains(x.StudentId) && lessons.Contains(x.LessonId), ct);
        return new EngagementAnalytics(sectionId, enrolledCount, lessonCount, completionCount,
            AnalyticsPolicy.Rate(completionCount, (long)enrolledCount * lessonCount),
            await db.LearningAssignments.AsNoTracking().CountAsync(x => x.InstitutionId == s.InstitutionId &&
                x.SectionId == sectionId && x.IsPublished, ct),
            await db.LearningSubmissions.AsNoTracking().CountAsync(x => x.InstitutionId == s.InstitutionId &&
                x.SectionId == sectionId && students.Contains(x.StudentId) &&
                (x.Status == "Submitted" || x.Status == "Graded"), ct),
            await db.Exams.AsNoTracking().CountAsync(x => x.InstitutionId == s.InstitutionId &&
                x.SectionId == sectionId && x.Status == "Published", ct),
            await db.ExamAttempts.AsNoTracking().CountAsync(x => x.InstitutionId == s.InstitutionId &&
                x.SectionId == sectionId && students.Contains(x.StudentId) && x.Status == "Submitted", ct));
    }

    public async Task<OutcomesAnalytics> SectionOutcomes(ClaimsPrincipal principal, Guid sectionId, CancellationToken ct)
    {
        var s = await SectionGuard(principal, sectionId, ct);
        return await Outcomes(s.InstitutionId, sectionId, s.TermId, ct);
    }

    private IQueryable<StudentAnalyticsLine> StudentQuery(Guid institutionId, Guid sectionId)
    {
        var lessons = PublishedLessonIds(institutionId, sectionId);
        return from e in db.StudentEnrollments.AsNoTracking()
               join u in db.Users.AsNoTracking() on e.StudentId equals u.Id
               where e.InstitutionId == institutionId && e.SectionId == sectionId && e.Status == "Active" &&
                     u.InstitutionId == institutionId
               orderby u.Email, u.Id
               select new StudentAnalyticsLine(u.Id, u.Email,
                   db.LessonCompletions.Count(x => x.InstitutionId == institutionId && x.SectionId == sectionId &&
                       x.StudentId == u.Id && lessons.Contains(x.LessonId)),
                   db.LearningSubmissions.Count(x => x.InstitutionId == institutionId && x.SectionId == sectionId &&
                       x.StudentId == u.Id && (x.Status == "Submitted" || x.Status == "Graded")),
                   db.AttendanceRecords.Count(x => x.InstitutionId == institutionId && x.SectionId == sectionId &&
                       x.StudentId == u.Id),
                   db.AttendanceRecords.Count(x => x.InstitutionId == institutionId && x.SectionId == sectionId &&
                       x.StudentId == u.Id && (x.Status == "Present" || x.Status == "Late")),
                   db.GradebookResults.Where(x => x.InstitutionId == institutionId && x.SectionId == sectionId &&
                       x.StudentId == u.Id).Select(x => (decimal?)x.Percentage).FirstOrDefault());
    }

    public async Task<PageResult<StudentAnalyticsLine>> SectionStudents(ClaimsPrincipal principal,
        Guid sectionId, int page, CancellationToken ct)
    {
        var offset = AnalyticsPolicy.Offset(page);
        var s = await SectionGuard(principal, sectionId, ct);
        await RequireCurrent(db.GradebookResults.Where(x=>x.InstitutionId==s.InstitutionId&&x.SectionId==sectionId),ct);
        var query = StudentQuery(s.InstitutionId, sectionId);
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip(offset).Take(AnalyticsPolicy.PageSize).ToListAsync(ct);
        return new PageResult<StudentAnalyticsLine>(page, AnalyticsPolicy.PageSize, total, items);
    }

    public async Task<string> ExportSectionStudents(ClaimsPrincipal principal, Guid sectionId, CancellationToken ct)
    {
        var s = await SectionGuard(principal, sectionId, ct);
        await RequireCurrent(db.GradebookResults.Where(x=>x.InstitutionId==s.InstitutionId&&x.SectionId==sectionId),ct);
        var query = StudentQuery(s.InstitutionId, sectionId);
        var rows = await query
            .Take(AnalyticsPolicy.MaximumCsvRows + 1).ToListAsync(ct);
        if (rows.Count > AnalyticsPolicy.MaximumCsvRows)
            throw ApiFault.Bad("Export exceeds 2000 students.");
        var lines = new List<string> { AnalyticsPolicy.CsvRow("StudentId", "Email", "CompletedLessons",
            "SubmittedAssignments", "RecordedAttendance", "PresentOrLateMarks", "PublishedFinalPercentage") };
        lines.AddRange(rows.Select(x => AnalyticsPolicy.CsvRow(x.StudentId, x.Email, x.CompletedLessons,
            x.SubmittedAssignments, x.RecordedAttendance, x.PresentOrLateMarks, x.PublishedFinalPercentage)));
        return string.Join("\r\n", lines) + "\r\n";
    }

    private async Task PlatformGuard(ClaimsPrincipal principal, CancellationToken ct)
    {
        await security.RequireAdminAsync(principal, Guid.Empty, ct, superOnly: true);
    }

    public async Task<PlatformAnalytics> PlatformOverview(ClaimsPrincipal principal, CancellationToken ct)
    {
        await PlatformGuard(principal, ct);
        return new PlatformAnalytics(await db.Institutions.AsNoTracking().CountAsync(ct),
            await db.Institutions.AsNoTracking().CountAsync(x => x.IsActive, ct),
            await db.Users.AsNoTracking().CountAsync(ct),
            await db.CourseSections.AsNoTracking().CountAsync(ct),
            await db.StudentEnrollments.AsNoTracking().CountAsync(x => x.Status == "Active", ct),
            await db.GradebookResults.AsNoTracking().CountAsync(ct), DateTime.UtcNow);
    }

    public async Task<PageResult<PlatformInstitutionAnalytics>> PlatformInstitutions(ClaimsPrincipal principal,
        int page, CancellationToken ct)
    {
        var offset = AnalyticsPolicy.Offset(page);
        await PlatformGuard(principal, ct);
        var query = db.Institutions.AsNoTracking();
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Code).ThenBy(x => x.Id)
            .Skip(offset).Take(AnalyticsPolicy.PageSize)
            .Select(i => new PlatformInstitutionAnalytics(i.Id, i.Code, i.Name, i.IsActive,
                db.Users.Count(x => x.InstitutionId == i.Id && x.IsActive),
                db.CourseSections.Count(x => x.InstitutionId == i.Id),
                db.StudentEnrollments.Count(x => x.InstitutionId == i.Id && x.Status == "Active")))
            .ToListAsync(ct);
        return new PageResult<PlatformInstitutionAnalytics>(page, AnalyticsPolicy.PageSize, total, rows);
    }

    public async Task<MyAnalytics> MySummary(ClaimsPrincipal principal, CancellationToken ct)
    {
        var (user, roles) = await security.CurrentAsync(principal, ct);
        if (!roles.Contains(AcademixRoles.Student) || user.InstitutionId is not Guid institutionId)
            throw ApiFault.Forbidden();
        var sections = db.StudentEnrollments.AsNoTracking().Where(x => x.InstitutionId == institutionId &&
            x.StudentId == user.Id && x.Status == "Active").Select(x => x.SectionId);
        var results = db.GradebookResults.AsNoTracking().Where(x => x.InstitutionId == institutionId &&
            x.StudentId == user.Id && sections.Contains(x.SectionId));
        await RequireCurrent(results,ct);
        return new MyAnalytics(user.Id, institutionId, await sections.CountAsync(ct),
            await db.LessonCompletions.AsNoTracking().CountAsync(x => x.InstitutionId == institutionId &&
                x.StudentId == user.Id && sections.Contains(x.SectionId), ct),
            await db.LearningSubmissions.AsNoTracking().CountAsync(x => x.InstitutionId == institutionId &&
                x.StudentId == user.Id && sections.Contains(x.SectionId) &&
                (x.Status == "Submitted" || x.Status == "Graded"), ct),
            await db.AttendanceRecords.AsNoTracking().CountAsync(x => x.InstitutionId == institutionId &&
                x.StudentId == user.Id && sections.Contains(x.SectionId), ct),
            await db.AttendanceRecords.AsNoTracking().CountAsync(x => x.InstitutionId == institutionId &&
                x.StudentId == user.Id && sections.Contains(x.SectionId) &&
                (x.Status == "Present" || x.Status == "Late"), ct),
            await results.CountAsync(ct),
            await results.AverageAsync(x => (decimal?)x.Percentage, ct));
    }
}
