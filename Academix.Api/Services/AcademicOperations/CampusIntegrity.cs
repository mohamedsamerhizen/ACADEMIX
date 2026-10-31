using Academix.Api.Data;
using Academix.Api.Middleware;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.AcademicOperations;

/// <summary>Shared checks for changes to the academic side of existing campus allocations.
/// Callers hold the institution coordination lock through commit.</summary>
public static class CampusIntegrity
{
    public sealed record Appointment(Guid SectionId,DateTime Start,DateTime End,Guid? Invigilator=null);
    public static bool Conflicts(IEnumerable<Appointment> target,IEnumerable<Appointment> other)
        =>target.Any(a=>other.Any(b=>CampusSchedulingRules.Overlaps(a.Start,a.End,b.Start,b.End)));
    public static void Seats(int current,int added,int reserved)
    {if(current+added>reserved)throw ApiFault.Conflict("Existing physical examination seats cannot accommodate this enrollment; revise the venue first.");}
    public static void Capacity(int proposed,IEnumerable<int> roomCapacities)
    {if(roomCapacities.Any(c=>c<proposed))throw ApiFault.Conflict("Section capacity exceeds an assigned room; reassign the room first.");}
    private static void Transaction(AcademixDbContext db)
    {if(db.Database.CurrentTransaction is null)throw new InvalidOperationException("Campus integrity requires the owning academic transaction.");}

    private static async Task<List<Appointment>> Appointments(AcademixDbContext db,Guid inst,CancellationToken ct)
    {
        var now=DateTime.UtcNow;
        var sessions=await db.ClassSessions.AsNoTracking().Where(x=>x.InstitutionId==inst&&x.Status!="Cancelled"&&x.EndsAtUtc>now)
            .Select(x=>new Appointment(x.SectionId,x.StartsAtUtc,x.EndsAtUtc,null)).ToListAsync(ct);
        var exams=await db.CampusRoomReservations.AsNoTracking().Where(x=>x.InstitutionId==inst&&x.Status=="Active"&&x.Kind=="Exam"&&x.EndsAtUtc>now)
            .Select(x=>new Appointment(x.SectionId!.Value,x.StartsAtUtc,x.EndsAtUtc,x.InvigilatorId)).ToListAsync(ct);
        return sessions.Concat(exams).ToList();
    }
    public static async Task Enrollment(AcademixDbContext db,Guid inst,Guid section,Guid[] students,CancellationToken ct)
    {
        Transaction(db);students=students.Distinct().ToArray();
        var existing=await db.StudentEnrollments.AsNoTracking().Where(x=>x.InstitutionId==inst&&x.SectionId==section&&x.Status=="Active").Select(x=>x.StudentId).ToArrayAsync(ct);
        var added=students.Except(existing).Count();
        var seats=await db.CampusRoomReservations.AsNoTracking().Where(x=>x.InstitutionId==inst&&x.SectionId==section&&x.Kind=="Exam"&&x.Status=="Active"&&x.EndsAtUtc>DateTime.UtcNow).Select(x=>x.SeatCount!.Value).ToArrayAsync(ct);
        foreach(var count in seats)Seats(existing.Length,added,count);
        var targetTerm=await (from s in db.CourseSections join t in db.AcademicTerms on s.TermId equals t.Id where s.Id==section&&s.InstitutionId==inst select t).SingleAsync(ct);
        var slots=await (from slot in db.TimetableSlots.AsNoTracking() join s in db.CourseSections on slot.SectionId equals s.Id join t in db.AcademicTerms on s.TermId equals t.Id
            where slot.InstitutionId==inst&&slot.IsActive&&s.InstitutionId==inst&&t.InstitutionId==inst&&t.Status!="Closed"&&t.StartsOn<targetTerm.EndsOn&&targetTerm.StartsOn<t.EndsOn
            select new{slot.SectionId,slot.Weekday,slot.StartsAtMinute,slot.DurationMinutes}).ToListAsync(ct);
        var targetSlots=slots.Where(x=>x.SectionId==section).ToArray();
        var appointments=await Appointments(db,inst,ct);var target=appointments.Where(x=>x.SectionId==section).ToArray();
        var registrations=await db.StudentEnrollments.AsNoTracking().Where(x=>x.InstitutionId==inst&&students.Contains(x.StudentId)&&x.SectionId!=section&&x.Status=="Active").Select(x=>new{x.StudentId,x.SectionId}).ToListAsync(ct);
        foreach(var student in students)
        {
            var other=registrations.Where(x=>x.StudentId==student).Select(x=>x.SectionId).ToHashSet();
            if(targetSlots.Any(a=>slots.Any(b=>other.Contains(b.SectionId)&&a.Weekday==b.Weekday&&CampusSchedulingRules.Overlaps(a.StartsAtMinute,a.DurationMinutes,b.StartsAtMinute,b.DurationMinutes)))||Conflicts(target,appointments.Where(x=>other.Contains(x.SectionId))))
                throw ApiFault.Conflict("Enrollment conflicts with an existing student timetable, session or physical examination.");
        }
    }
    public static async Task SectionCapacity(AcademixDbContext db,Guid inst,Guid section,int proposed,CancellationToken ct)
    {
        Transaction(db);
        var slots=db.TimetableSlots.Where(x=>x.InstitutionId==inst&&x.SectionId==section&&x.IsActive&&x.RoomId!=null).Select(x=>x.RoomId!.Value);
        var sessions=db.ClassSessions.Where(x=>x.InstitutionId==inst&&x.SectionId==section&&x.Status!="Cancelled"&&x.EndsAtUtc>DateTime.UtcNow&&x.RoomId!=null).Select(x=>x.RoomId!.Value);
        var venues=db.CampusRoomReservations.Where(x=>x.InstitutionId==inst&&x.SectionId==section&&x.Status=="Active"&&x.Kind=="Exam"&&x.EndsAtUtc>DateTime.UtcNow).Select(x=>x.RoomId);
        var rooms=await db.CampusRooms.AsNoTracking().Where(x=>x.InstitutionId==inst&&(slots.Contains(x.Id)||sessions.Contains(x.Id)||venues.Contains(x.Id))).Select(x=>x.Capacity).ToArrayAsync(ct);
        Capacity(proposed,rooms);
    }
    public static async Task Instructor(AcademixDbContext db,Guid inst,Guid section,Guid instructor,CancellationToken ct)
    {
        Transaction(db);
        var taught=await db.TeachingAssignments.Where(x=>x.InstitutionId==inst&&x.UserId==instructor&&x.Kind=="Instructor"&&x.SectionId!=section).Select(x=>x.SectionId).ToArrayAsync(ct);
        var targetTerm=await (from s in db.CourseSections join t in db.AcademicTerms on s.TermId equals t.Id where s.Id==section&&s.InstitutionId==inst select t).SingleAsync(ct);
        var slots=await (from slot in db.TimetableSlots.AsNoTracking() join s in db.CourseSections on slot.SectionId equals s.Id join t in db.AcademicTerms on s.TermId equals t.Id
            where slot.InstitutionId==inst&&slot.IsActive&&s.InstitutionId==inst&&t.InstitutionId==inst&&t.Status!="Closed"&&t.StartsOn<targetTerm.EndsOn&&targetTerm.StartsOn<t.EndsOn
            select new{slot.SectionId,slot.Weekday,slot.StartsAtMinute,slot.DurationMinutes}).ToListAsync(ct);
        if(slots.Where(x=>x.SectionId==section).Any(a=>slots.Any(b=>taught.Contains(b.SectionId)&&a.Weekday==b.Weekday&&CampusSchedulingRules.Overlaps(a.StartsAtMinute,a.DurationMinutes,b.StartsAtMinute,b.DurationMinutes))))
            throw ApiFault.Conflict("Instructor has an existing overlapping timetable.");
        var appointments=await Appointments(db,inst,ct);
        if(Conflicts(appointments.Where(x=>x.SectionId==section),appointments.Where(x=>x.SectionId!=section&&(taught.Contains(x.SectionId)||x.Invigilator==instructor))))
            throw ApiFault.Conflict("Instructor is already teaching or invigilating an overlapping appointment.");
    }
}
