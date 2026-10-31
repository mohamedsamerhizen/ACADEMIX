using Academix.Api.Data;
using Academix.Api.Middleware;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Academic;

/// <summary>Shared order for enrollment, opening/closing and prerequisite changes.</summary>
public static class RegistrationCoordination
{
    public static async Task Lock(AcademixDbContext db, Guid institutionId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Transaction required.");
        var institution = (await db.Institutions.FromSqlInterpolated(
            $"SELECT * FROM institutions WHERE \"Id\" = {institutionId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
        if (institution is null || !institution.IsActive) throw ApiFault.NotFound();
    }
}
