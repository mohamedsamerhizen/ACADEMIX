using Academix.Api.Data;
using Academix.Api.DTOs.Courses;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Courses;

public sealed class CourseQueries(AcademixDbContext db) : ICourseQueries
{
    public async Task<IReadOnlyList<CourseDto>> GetActiveAsync(CancellationToken cancellationToken)
    {
        return await db.Courses
            .AsNoTracking()
            .Where(course => course.IsActive)
            .OrderBy(course => course.Code)
            .Select(course => new CourseDto(course.Id, course.Code, course.Title))
            .ToListAsync(cancellationToken);
    }
}
