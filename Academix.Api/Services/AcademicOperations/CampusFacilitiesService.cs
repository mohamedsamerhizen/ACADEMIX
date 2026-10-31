using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.AcademicOperations;
using Academix.Api.DTOs.AcademicOperations;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Academix.Api.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.AcademicOperations;

/// <summary>Institution-owned physical resources; historical free-text timetable labels remain valid.</summary>
public sealed class CampusFacilitiesService(AcademixDbContext db, IdentitySecurity security, RetryingServiceWork retry)
{
    private async Task ReadAccess(ClaimsPrincipal principal, Guid institutionId, CancellationToken ct)
    {
        var (user, roles) = await security.CurrentAsync(principal, ct);
        if (!roles.Contains(Academix.Api.Domain.Identity.AcademixRoles.SuperAdministrator) && user.InstitutionId != institutionId)
            throw ApiFault.Forbidden();
        if (!await db.Institutions.AnyAsync(x => x.Id == institutionId && x.IsActive, ct))
            throw ApiFault.NotFound();
    }

    private async Task WriteAccess(ClaimsPrincipal principal, Guid institutionId, CancellationToken ct)
    {
        _ = await security.RequireAdminAsync(principal, institutionId, ct);
    }

    public async Task<IReadOnlyList<BuildingView>> Buildings(ClaimsPrincipal principal, Guid institutionId, CancellationToken ct)
    {
        await ReadAccess(principal, institutionId, ct);
        return await db.CampusBuildings.AsNoTracking().Where(x => x.InstitutionId == institutionId)
            .OrderBy(x => x.Code).Select(x => new BuildingView(x.Id, x.InstitutionId, x.Code, x.Name,
                x.IsActive, x.Version)).ToListAsync(ct);
    }

    public Task<BuildingView> SaveBuilding(ClaimsPrincipal principal, Guid institutionId,
        Guid? buildingId, BuildingInput request, CancellationToken ct)
        => retry.ExecuteAsync<CampusFacilitiesService, BuildingView>(service =>
            service.SaveBuildingCore(principal, institutionId, buildingId, request, ct), ct);

    private async Task<BuildingView> SaveBuildingCore(ClaimsPrincipal principal, Guid institutionId,
        Guid? buildingId, BuildingInput request, CancellationToken ct)
    {
        CampusSchedulingRules.Building(request.Code, request.Name);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (await db.Institutions.FromSqlInterpolated($"SELECT * FROM institutions WHERE \"Id\" = {institutionId} FOR UPDATE")
            .SingleOrDefaultAsync(ct) is null) throw ApiFault.NotFound();
        await WriteAccess(principal, institutionId, ct);
        CampusBuilding building;
        if (buildingId.HasValue)
        {
            building = await db.CampusBuildings.SingleOrDefaultAsync(x => x.Id == buildingId.Value &&
                x.InstitutionId == institutionId, ct) ?? throw ApiFault.NotFound();
            if (building.Version != request.ExpectedVersion) throw ApiFault.Conflict("Building version changed.");
            if (!request.IsActive && await db.CampusRooms.AnyAsync(x => x.InstitutionId == institutionId &&
                    x.BuildingId == building.Id && x.IsActive, ct))
                throw ApiFault.Conflict("Deactivate active rooms before closing a building.");
            building.Version++;
        }
        else
        {
            if (request.ExpectedVersion.HasValue) throw ApiFault.Bad("Unexpected version for new building.");
            building = new CampusBuilding { InstitutionId = institutionId };
            db.CampusBuildings.Add(building);
        }
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.CampusBuildings.AnyAsync(x => x.InstitutionId == institutionId && x.Id != building.Id && x.Code == code, ct))
            throw ApiFault.Conflict("Building code already exists.");
        building.Code = code; building.Name = request.Name.Trim(); building.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(building.Id, institutionId, building.Code, building.Name, building.IsActive, building.Version);
    }

    public async Task<IReadOnlyList<RoomView>> Rooms(ClaimsPrincipal principal, Guid institutionId,
        Guid? buildingId, bool activeOnly, CancellationToken ct)
    {
        await ReadAccess(principal, institutionId, ct);
        var q = db.CampusRooms.AsNoTracking().Where(x => x.InstitutionId == institutionId);
        if (buildingId.HasValue) q = q.Where(x => x.BuildingId == buildingId.Value);
        if (activeOnly) q = q.Where(x => x.IsActive);
        return await q.OrderBy(x => x.BuildingId).ThenBy(x => x.Code)
            .Select(x => new RoomView(x.Id, x.InstitutionId, x.BuildingId, x.Code, x.Name,
                x.Kind, x.Capacity, x.IsActive, x.Version)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RoomView>> AvailableRooms(ClaimsPrincipal principal, Guid institutionId,
        int weekday, int startMinute, int durationMinutes, int requiredCapacity, CancellationToken ct)
    {
        await ReadAccess(principal, institutionId, ct);
        if (weekday is < 1 or > 7 || startMinute is < 0 or > 1439 ||
            durationMinutes is < 15 or > 600 || startMinute + durationMinutes > 1440 ||
            requiredCapacity is < 1 or > 10000)
            throw ApiFault.Bad("Invalid weekly interval or required capacity.");
        var busy = db.TimetableSlots.AsNoTracking().Where(x => x.InstitutionId == institutionId &&
            x.IsActive && x.RoomId.HasValue && x.Weekday == weekday &&
            x.StartsAtMinute < startMinute + durationMinutes &&
            startMinute < x.StartsAtMinute + x.DurationMinutes);
        return await db.CampusRooms.AsNoTracking().Where(x => x.InstitutionId == institutionId &&
            x.IsActive && x.Capacity >= requiredCapacity && !busy.Any(slot => slot.RoomId == x.Id) &&
            db.CampusBuildings.Any(b => b.InstitutionId == institutionId && b.Id == x.BuildingId && b.IsActive))
            .OrderBy(x => x.BuildingId).ThenBy(x => x.Code).Take(200)
            .Select(x => new RoomView(x.Id, x.InstitutionId, x.BuildingId, x.Code, x.Name,
                x.Kind, x.Capacity, x.IsActive, x.Version)).ToListAsync(ct);
    }

    /// <summary>Actual session occupancy (UTC), not a projection of an institution-local weekly pattern.</summary>
    public async Task<IReadOnlyList<CampusRoomSessionView>> RoomOccupancy(ClaimsPrincipal principal,
        Guid institutionId, Guid roomId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        await WriteAccess(principal, institutionId, ct);
        CampusSchedulingRules.OccupancyInterval(fromUtc, toUtc);
        if (!await db.CampusRooms.AsNoTracking().AnyAsync(x =>
            x.InstitutionId == institutionId && x.Id == roomId, ct)) throw ApiFault.NotFound();
        return await db.ClassSessions.AsNoTracking().Where(session =>
                session.InstitutionId == institutionId &&
                (session.RoomId == roomId || (session.RoomId == null && session.TimetableSlotId.HasValue &&
                    db.TimetableSlots.Any(slot => slot.Id == session.TimetableSlotId &&
                        slot.InstitutionId == institutionId && slot.SectionId == session.SectionId && slot.RoomId == roomId))) &&
                session.Status != "Cancelled" && session.StartsAtUtc < toUtc && session.EndsAtUtc > fromUtc)
            .OrderBy(session => session.StartsAtUtc).ThenBy(session => session.Id)
            .Select(session => new CampusRoomSessionView(session.Id, session.SectionId,
                session.StartsAtUtc, session.EndsAtUtc, session.Status, session.TimetableSlotId,
                session.RoomId ?? roomId))
            .Take(500).ToListAsync(ct);
    }

    /// <summary>Concrete dated activity for administrators; recurring timetable templates are not occurrences.</summary>
    public async Task<IReadOnlyList<CampusScheduleEntry>> InstitutionCalendar(ClaimsPrincipal principal,
        Guid institutionId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        await WriteAccess(principal, institutionId, ct);
        CampusSchedulingRules.OccupancyInterval(fromUtc, toUtc);
        var sessions = await db.ClassSessions.AsNoTracking().Where(s =>
            s.InstitutionId == institutionId && s.StartsAtUtc < toUtc && s.EndsAtUtc > fromUtc)
            .OrderBy(s => s.StartsAtUtc).Take(500)
            .Select(s => new CampusScheduleEntry(s.Id, "ClassSession", s.SectionId,
                s.RoomId, s.StartsAtUtc, s.EndsAtUtc, s.Status)).ToListAsync(ct);
        var reservations = await db.CampusRoomReservations.AsNoTracking().Where(r =>
            r.InstitutionId == institutionId && r.StartsAtUtc < toUtc && r.EndsAtUtc > fromUtc)
            .OrderBy(r => r.StartsAtUtc).Take(500)
            .Select(r => new CampusScheduleEntry(r.Id, r.Kind, r.SectionId, r.RoomId,
                r.StartsAtUtc, r.EndsAtUtc, r.Status)).ToListAsync(ct);
        return sessions.Concat(reservations).OrderBy(x => x.StartsAtUtc).ThenBy(x => x.Id).Take(500).ToArray();
    }

    /// <summary>Actual UTC availability; weekly timetable templates are not concrete dated bookings.</summary>
    public async Task<IReadOnlyList<RoomView>> AvailableAt(ClaimsPrincipal principal,
        Guid institutionId, DateTime fromUtc, DateTime toUtc, int requiredCapacity, CancellationToken ct)
    {
        await ReadAccess(principal, institutionId, ct);
        CampusSchedulingRules.OccupancyInterval(fromUtc, toUtc);
        if (requiredCapacity is < 1 or > 10000) throw ApiFault.Bad("Invalid room capacity.");
        return await db.CampusRooms.AsNoTracking().Where(room =>
            room.InstitutionId == institutionId && room.IsActive && room.Capacity >= requiredCapacity &&
            db.CampusBuildings.Any(b => b.Id == room.BuildingId && b.InstitutionId == institutionId && b.IsActive) &&
            !db.CampusRoomReservations.Any(r => r.InstitutionId == institutionId &&
                r.RoomId == room.Id && r.Status == "Active" &&
                r.StartsAtUtc < toUtc && r.EndsAtUtc > fromUtc) &&
            !db.ClassSessions.Any(s => s.InstitutionId == institutionId && s.Status != "Cancelled" &&
                s.StartsAtUtc < toUtc && s.EndsAtUtc > fromUtc &&
                (s.RoomId == room.Id || (s.RoomId == null && s.TimetableSlotId.HasValue &&
                    db.TimetableSlots.Any(slot => slot.Id == s.TimetableSlotId &&
                        slot.SectionId == s.SectionId && slot.InstitutionId == institutionId &&
                        slot.RoomId == room.Id)))))
            .OrderBy(x => x.BuildingId).ThenBy(x => x.Code).Take(200)
            .Select(x => new RoomView(x.Id, x.InstitutionId, x.BuildingId, x.Code, x.Name,
                x.Kind, x.Capacity, x.IsActive, x.Version)).ToListAsync(ct);
    }

    public Task<RoomView> SaveRoom(ClaimsPrincipal principal, Guid institutionId,
        Guid? roomId, RoomInput request, CancellationToken ct)
        => retry.ExecuteAsync<CampusFacilitiesService, RoomView>(service =>
            service.SaveRoomCore(principal, institutionId, roomId, request, ct), ct);

    private async Task<RoomView> SaveRoomCore(ClaimsPrincipal principal, Guid institutionId,
        Guid? roomId, RoomInput request, CancellationToken ct)
    {
        CampusSchedulingRules.Room(request.Code, request.Name, request.Kind, request.Capacity);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (await db.Institutions.FromSqlInterpolated($"SELECT * FROM institutions WHERE \"Id\" = {institutionId} FOR UPDATE")
            .SingleOrDefaultAsync(ct) is null) throw ApiFault.NotFound();
        await WriteAccess(principal, institutionId, ct);
        if (!await db.CampusBuildings.AnyAsync(x => x.InstitutionId == institutionId &&
            x.Id == request.BuildingId && x.IsActive, ct)) throw ApiFault.Bad("Building is unavailable.");
        CampusRoom room;
        if (roomId.HasValue)
        {
            room = await db.CampusRooms.SingleOrDefaultAsync(x => x.Id == roomId.Value &&
                x.InstitutionId == institutionId, ct) ?? throw ApiFault.NotFound();
            if (room.Version != request.ExpectedVersion) throw ApiFault.Conflict("Room version changed.");
            if ((room.BuildingId != request.BuildingId || !request.IsActive) &&
                await db.TimetableSlots.AnyAsync(x => x.InstitutionId == institutionId && x.RoomId == room.Id && x.IsActive, ct))
                throw ApiFault.Conflict("Room has active reservations; reassign timetable slots first.");
            if ((room.BuildingId != request.BuildingId || !request.IsActive || request.Capacity < room.Capacity) &&
                await db.CampusRoomReservations.AnyAsync(x => x.InstitutionId == institutionId &&
                    x.RoomId == room.Id && x.Status == "Active" && x.EndsAtUtc > DateTime.UtcNow &&
                    (room.BuildingId != request.BuildingId || !request.IsActive ||
                     x.SeatCount.HasValue && x.SeatCount > request.Capacity), ct))
                throw ApiFault.Conflict("Resolve active room reservations before changing facility availability or capacity.");
            if (room.BuildingId != request.BuildingId && await db.ClassSessions.AnyAsync(x =>
                x.InstitutionId == institutionId && x.RoomId == room.Id && x.Status == "Planned" &&
                x.StartsAtUtc > DateTime.UtcNow, ct))
                throw ApiFault.Conflict("Resolve upcoming room bookings before moving a room between buildings.");
            if (request.Capacity < room.Capacity && await db.TimetableSlots.AnyAsync(x => x.InstitutionId == institutionId &&
                x.RoomId == room.Id && x.IsActive && db.CourseSections.Any(s => s.Id == x.SectionId &&
                    s.InstitutionId == institutionId && s.Capacity > request.Capacity), ct))
                throw ApiFault.Conflict("Room capacity is below a currently assigned section capacity.");
            if (!request.IsActive && await db.ClassSessions.AnyAsync(x => x.InstitutionId == institutionId &&
                x.RoomId == room.Id && x.Status == "Planned" && x.StartsAtUtc > DateTime.UtcNow, ct))
                throw ApiFault.Conflict("Resolve upcoming room bookings before deactivation.");
            if (request.Capacity < room.Capacity && await db.ClassSessions.AnyAsync(x =>
                x.InstitutionId == institutionId && x.RoomId == room.Id && x.Status == "Planned" &&
                x.StartsAtUtc > DateTime.UtcNow && db.CourseSections.Any(sec =>
                    sec.InstitutionId == institutionId && sec.Id == x.SectionId && sec.Capacity > request.Capacity), ct))
                throw ApiFault.Conflict("Capacity is below an upcoming session's section capacity.");
            room.Version++;
        }
        else
        {
            if (request.ExpectedVersion.HasValue) throw ApiFault.Bad("Unexpected version for new room.");
            room = new CampusRoom { InstitutionId = institutionId };
            db.CampusRooms.Add(room);
        }
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.CampusRooms.AnyAsync(x => x.Id != room.Id && x.InstitutionId == institutionId &&
            x.BuildingId == request.BuildingId && x.Code == code, ct))
            throw ApiFault.Conflict("Room code already exists in this building.");
        room.BuildingId = request.BuildingId; room.Code = code; room.Name = request.Name.Trim();
        room.Kind = request.Kind; room.Capacity = request.Capacity; room.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(room.Id, institutionId, room.BuildingId, room.Code, room.Name, room.Kind,
            room.Capacity, room.IsActive, room.Version);
    }
}
